using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WindowsIsland
{
    // The animation loop: every spring, each frame, the cascade of the panels and the live parts.
    public partial class IslandWindow
    {
        void StartLoop()
        {
            if (rendering) return;
            rendering = true;
            lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }

        void StopLoop()
        {
            if (!rendering) return;
            rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        void OnRendering(object sender, EventArgs e)
        {
            TimeSpan time = ((RenderingEventArgs)e).RenderingTime;
            if (time == lastFrame) return; // WPF can raise the event more than once per frame
            double dt = lastFrame == TimeSpan.Zero ? 1 / 60.0 : (time - lastFrame).TotalSeconds;
            lastFrame = time;
            dt = Math.Min(dt, 1 / 24.0);

            bool moving, continuous;
            Advance(dt, out moving, out continuous);
            if (!moving && !continuous) StopLoop();
        }

        // One step of the animation: every spring, the frame, and the live parts.
        void Advance(double dt, out bool moving, out bool continuous)
        {
            clock += dt;
            moving = false;
            moving |= width.Step(dt);
            moving |= height.Step(dt);
            moving |= shown.Step(dt);
            moving |= stateScale.Step(dt);
            moving |= bubbleShift.Step(dt);
            moving |= bubbleScale.Step(dt);
            moving |= bubbleOpacity.Step(dt);
            moving |= privacyOpacity.Step(dt);
            moving |= thinGaugesOpacity.Step(dt);
            moving |= sessionArc.Step(dt);
            moving |= weeklyArc.Step(dt);
            foreach (Spring spring in presence.Values) moving |= spring.Step(dt);

            ApplyFrame();
            continuous = AnimateLive(dt);
        }

        // How far the shape is on its way to the new size (0 to 1), and, from that, how much of the incoming content shows:
        // none until the shape is about a third of the way, all of it by the time it is four fifths there.
        double morphFromW = 112, morphFromH = 30, morphToW = 112, morphToH = 30;
        View lastTargetView = View.Hidden;

        double MorphGate(double w, double h)
        {
            double dw = morphToW - morphFromW, dh = morphToH - morphFromH;
            double progress = 1;
            if (Math.Abs(dw) > 8 || Math.Abs(dh) > 8)
                progress = Math.Max(Math.Abs(dw) > 8 ? (w - morphFromW) / dw : 0, Math.Abs(dh) > 8 ? (h - morphFromH) / dh : 0);
            double t = Math.Clamp((progress - 0.33) / 0.47, 0, 1);
            return t * t * (3 - 2 * t);
        }

        void ApplyFrame()
        {
            double w = Math.Max(2, width.Value);
            double h = Math.Max(2, height.Value);
            double gate = MorphGate(w, h);
            double radius = Math.Min(h / 2, MaxRadius);
            bool bottom = IsBottom;

            Pill.Width = w;
            Pill.Height = h;
            Pill.CornerRadius = new CornerRadius(radius);
            stageClip.Rect = new Rect(0, 0, w, h);
            stageClip.RadiusX = radius;
            stageClip.RadiusY = radius;
            UpdateTimerRing(w, h);

            double visible = Math.Clamp(shown.Value, 0, 1);
            Pill.Opacity = visible;
            double s = stateScale.Value;
            PillScale.ScaleX = PillScale.ScaleY = Math.Max(0, (0.7 + 0.3 * shown.Value) * s);

            // A soft shadow only while expanded; a compact island has none, so clicks around it pass through.
            double expansion = Math.Clamp((h - 44) / 60, 0, 1) * visible;
            if (expansion > 0.01)
            {
                shadow.Opacity = 0.55 * expansion;
                if (Pill.Effect == null) Pill.Effect = shadow;
            }
            else if (Pill.Effect != null)
            {
                Pill.Effect = null;
            }

            foreach (KeyValuePair<View, FrameworkElement> pair in panels)
            {
                FrameworkElement panel = pair.Value;
                double p = presence[panel].Value;
                bool incoming = presence[panel].Target > 0.5;
                double opacity = Math.Clamp(p, 0, 1) * (incoming ? gate : 1);
                if (opacity < 0.004)
                {
                    if (panel.Visibility != Visibility.Hidden) panel.Visibility = Visibility.Hidden;
                    continue;
                }
                if (panel.Visibility != Visibility.Visible) panel.Visibility = Visibility.Visible;

                panel.Opacity = opacity;
                ScaleTransform scale = (ScaleTransform)panel.RenderTransform;
                double k = 0.84 + 0.16 * p;
                scale.ScaleX = k;
                scale.ScaleY = k;
                scale.CenterX = panel.Width / 2;
                scale.CenterY = bottom ? panel.Height : 0;
                // The old content slides up as it leaves and the new one comes up from below, a little
                double slide = (incoming ? 1 : -1) * (1 - Math.Clamp(p, 0, 1)) * 9 * (bottom ? -1 : 1);
                if (pair.Key == View.MusicCompact) panel.Width = Math.Max(90, Math.Min(w, 236)); // The cover and the equalizer stay at the island's edges while it grows and shrinks
                Canvas.SetLeft(panel, (w - panel.Width) / 2);
                Canvas.SetTop(panel, (bottom ? h - panel.Height : 0) + slide);
                if (pair.Key == View.MusicExpanded || pair.Key == View.ClaudeExpanded || pair.Key == View.TimerExpanded || pair.Key == View.QuickExpanded || pair.Key == View.ClipboardExpanded || pair.Key == View.ReminderExpanded || pair.Key == View.SystemExpanded || pair.Key == View.ExternalExpanded)
                    Cascade(panel, incoming, bottom, opacity);

                double blur = (1 - opacity) * 12;
                BlurEffect effect = panel.Effect as BlurEffect;
                if (blur < 0.4)
                {
                    if (effect != null) panel.Effect = null;
                }
                else
                {
                    if (effect == null) panel.Effect = effect = new BlurEffect { KernelType = KernelType.Gaussian };
                    effect.Radius = blur;
                }
            }

            // The bubble follows the island's edge, sliding out from behind it.
            // Everything is measured at size 100% and multiplied by the user's size.
            bool thinLine = view == View.Minimal && bubble != Activity.None;
            double bubbleSize = thinLine ? BubbleSize * 0.42 : BubbleSize, bubbleGap = thinLine ? 5 + (thinGauges ? 34 : 0) : BubbleGap; // Beyond the rings, when they are there
            bubbleShift.Target = (bubble != Activity.None ? w / 2 + bubbleGap + bubbleSize / 2 : w / 2 - BubbleSize / 2 - 2) * s;
            BubbleShift.X = bubbleShift.Value;
            BubbleShift.Y = thinLine ? (bottom ? 17 - 4.5 * s : 4.5 * s - 17) // Level with the thin line
                                     : (bottom ? -1 : 1) * BubbleSize / 2 * (s - 1); // Keeps it level with the scaled island
            BubbleScale.ScaleX = BubbleScale.ScaleY = Math.Max(0, bubbleScale.Value * s);
            Bubble.Opacity = Math.Clamp(bubbleOpacity.Value, 0, 1) * visible;

            // The microphone and camera dots stay where they are while the island is put away or auto-hidden
            // (they must never hide), and keep the idle island's spot instead of sliding with its shrinking width.
            bool away = view == View.Hidden && !(fullscreen && settings.HideInFullscreen);
            double dotsBase = away ? SizeOf(View.Idle).Width : w;
            ThinGaugesScale.ScaleX = ThinGaugesScale.ScaleY = s;
            ThinGaugesShift.X = (w / 2 + 5 + 15.5) * s;
            ThinGaugesShift.Y = bottom ? 7 - 4.5 * s : 4.5 * s - 7; // Level with the thin line
            ThinGauges.Opacity = Math.Clamp(thinGaugesOpacity.Value, 0, 1) * visible;
            PrivacyShift.X = -(dotsBase / 2 + BubbleGap + PrivacyDots.Width / 2) * s; // Straight to its place: it follows the island's own spring
            PrivacyShift.Y = (bottom ? -1 : 1) * (Math.Min(h, 34) * s - PrivacyDots.Height) / 2; // Level with a compact island
            PrivacyDots.Opacity = Math.Clamp(privacyOpacity.Value, 0, 1) * (away ? 1 : visible);

            if (PageDots.Visibility == Visibility.Visible)
            {
                Canvas.SetLeft(PageDots, (w - PageDots.Children.Count * 18) / 2);
                Canvas.SetTop(PageDots, h - 15);
                PageDots.Opacity = Math.Clamp((h - 110) / 30, 0, 1);
            }

            if (Math.Abs(sessionArc.Value - drawnSessionArc) > 0.05)
            {
                drawnSessionArc = sessionArc.Value;
                SessionArc.Data = Arc(drawnSessionArc, 48, 5);
            }
            if (Math.Abs(weeklyArc.Value - drawnWeeklyArc) > 0.05)
            {
                drawnWeeklyArc = weeklyArc.Value;
                WeeklyArc.Data = Arc(drawnWeeklyArc, 48, 5);
            }
        }

        // When a page opens, its rows come in one after the other (a little later and a little lower each), instead of all at once
        readonly Dictionary<UIElement, double> cascadeBase = new Dictionary<UIElement, double>();
        readonly Dictionary<FrameworkElement, DateTime> cascadeStart = new Dictionary<FrameworkElement, DateTime>();
        static readonly DateTime CascadeDone = DateTime.MaxValue;
        bool cascadeRunning, cascadeOff; // Off for the still images of the README (a page caught half way in would be empty)

        void Cascade(FrameworkElement panel, bool incoming, bool bottom, double visibleNow)
        {
            if (cascadeOff) return;
            Panel host = panel as Panel;
            if (host == null) return;
            if (host.Children.Count == 1 && host.Children[0] is Panel inner) host = inner;
            DateTime start;
            bool known = cascadeStart.TryGetValue(panel, out start);
            if (!incoming)
            {
                if (known) { cascadeStart.Remove(panel); CascadeRestore(host); }
                return;
            }
            if (known && start == CascadeDone) return;
            if (!known)
            {
                if (visibleNow < 0.05) return; // It starts when the page can be seen
                start = SampleClock.UtcNow;
                cascadeStart[panel] = start;
                foreach (UIElement child in host.Children) cascadeBase[child] = child.Opacity;
            }
            double t = (SampleClock.UtcNow - start).TotalSeconds;
            int index = 0;
            bool finished = true;
            foreach (UIElement child in host.Children)
            {
                double c = Math.Clamp((t - 0.03 - 0.07 * index++) / 0.30, 0, 1);
                if (c < 1) finished = false;
                double eased = 1 - Math.Pow(1 - c, 3);
                double original;
                child.Opacity = (cascadeBase.TryGetValue(child, out original) ? original : 1) * eased;
                TranslateTransform shift = child.RenderTransform as TranslateTransform;
                if (shift == null && (child.RenderTransform == null || child.RenderTransform.Value.IsIdentity))
                    child.RenderTransform = shift = new TranslateTransform();
                if (shift != null) shift.Y = (1 - eased) * 12 * (bottom ? -1 : 1);
            }
            if (finished) { CascadeRestore(host); cascadeStart[panel] = CascadeDone; }
            else cascadeRunning = true;
        }

        void CascadeRestore(Panel host)
        {
            foreach (UIElement child in host.Children)
            {
                double original;
                child.Opacity = cascadeBase.TryGetValue(child, out original) ? original : 1;
                TranslateTransform shift = child.RenderTransform as TranslateTransform;
                if (shift != null) shift.Y = 0;
            }
        }

        // Equalizer, progress, the working spark and the timer. Returns true while any of them must keep moving.
        bool AnimateLive(double dt)
        {
            bool keepGoing = cascadeRunning;
            cascadeRunning = false;

            if (presence[MinimalPanel].Value > 0.01) keepGoing |= AnimateMinimal() || RecordingOn;

            bool musicVisible = presence[MusicCompact].Value > 0.01 || presence[MusicExpanded].Value > 0.01;
            if (musicVisible) keepGoing |= AnimateEqualizer(dt);
            if (presence[MusicExpanded].Value > 0.01)
            {
                UpdateProgress();
                keepGoing |= media.IsPlaying;
            }

            if (presence[SystemExpanded].Value > 0.01)
            {
                UpdateSystemPage();
                keepGoing = true;
            }

            if (presence[QuickExpanded].Value > 0.01)
            {
                UpdateQuick(false);
                keepGoing = true;
            }

            if (presence[ClipboardExpanded].Value > 0.01)
            {
                UpdateClipboard(false);
                keepGoing = true;
            }

            if (presence[ReminderExpanded].Value > 0.01)
            {
                UpdateReminders(false);
                keepGoing = true;
            }

            if ((presence[TimerCompact].Value > 0.01 || presence[TimerExpanded].Value > 0.01) && timer.Running)
            {
                UpdateTimerLive();
                keepGoing |= !timer.Paused;
            }

            // Codex: the prompt's cursor blinks softly and the timer counts while it works.
            bool codexVisible = presence[CodexCompact].Value > 0.01 || presence[ClaudeExpanded].Value > 0.01 ||
                                (bubble == Activity.Codex && bubbleOpacity.Value > 0.01);
            if (codexVisible && codex.Working)
            {
                double blink = 0.55 + 0.45 * (0.5 + 0.5 * Math.Cos(clock * 3.2));
                CodexCompactGlyph.Opacity = ExpandedCodexGlyph.Opacity = BubbleCodex.Opacity = blink;
                UpdateCodexLive();
                keepGoing = true;
            }
            else
            {
                CodexCompactGlyph.Opacity = ExpandedCodexGlyph.Opacity = BubbleCodex.Opacity = 1;
            }

            bool claudeVisible = presence[ClaudeCompact].Value > 0.01 || presence[ClaudeExpanded].Value > 0.01 ||
                                 (bubble == Activity.Claude && bubbleOpacity.Value > 0.01);
            bool thinking = claudeVisible && claude.Busy > 0 && claude.Waiting == 0;
            CompactSpark.Thinking = ExpandedSpark.Thinking = BubbleSpark.Thinking = thinking;
            if (claudeVisible && (claude.Busy > 0 || claude.Waiting > 0))
            {
                double sparkOpacity = 1;
                if (claude.Waiting > 0)
                    sparkOpacity = 0.45 + 0.55 * (0.5 + 0.5 * Math.Cos(clock * 4.5)); // Breathing: needs attention
                if (presence[ClaudeCompact].Value > 0.01) CompactSpark.Advance(dt);
                if (presence[ClaudeExpanded].Value > 0.01) ExpandedSpark.Advance(dt);
                if (bubble == Activity.Claude) BubbleSpark.Advance(dt);
                CompactSpark.Opacity = ExpandedSpark.Opacity = BubbleSpark.Opacity = sparkOpacity;
                UpdateClaudeLive();
                keepGoing = true;
            }
            else
            {
                CompactSpark.Opacity = ExpandedSpark.Opacity = BubbleSpark.Opacity = 1;
            }
            return keepGoing;
        }
    }
}
