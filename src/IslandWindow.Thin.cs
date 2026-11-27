using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace WindowsIsland
{
    // The thin line (the minimal island) and what it shows.
    public partial class IslandWindow
    {
        bool ThinActive { get { return settings.AlwaysThin || (settings.MaximizedActivitiesThin && maximized && !IsBottom && settings.MaximizedMode == "Thin"); } }

        // ---------- The thin line: the state that matters most, drawn on the line itself ----------
        // Claude working: a glow sweeps along it; waiting for you: it pulses; music: a tiny equalizer; a timer, a download or
        // the charge: it fills; an alert: it pulses red; done: green. Only the most important state shows, never a mix.

        enum Thin { None, ClaudeWorking, ClaudeWaiting, Music, Timer, Progress, Busy, Alert, Done, Charging, Focus, Offline, Recording }
        bool networkOffline;
        DateTime unreadAt = DateTime.MinValue; // A notification came and nobody looked yet
        readonly LinearGradientBrush dashes = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(0.14, 0.5), SpreadMethod = GradientSpreadMethod.Repeat };

        internal void SetOnline(bool online) { networkOffline = !online; Recompute(); }
        Thin minimalKind = Thin.None;
        bool minimalHasContent; // Only the moon needs more room than the line
        Color minimalColor = Colors.White;
        double minimalFraction;
        static readonly FontFamily FluentIcons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly Color ThinPink = Color.FromRgb(0xFF, 0x37, 0x5F);
        static readonly Color ThinClay = Color.FromRgb(0xD9, 0x77, 0x57);
        static readonly Color ThinYellow = Color.FromRgb(0xFF, 0xD6, 0x0A);
        static readonly Color ThinBlue = Color.FromRgb(0x0A, 0x84, 0xFF);
        static readonly Color ThinGreen = Color.FromRgb(0x30, 0xD1, 0x58);
        static readonly Color ThinRed = Color.FromRgb(0xFF, 0x45, 0x3A);
        static readonly Brush ThinMoon = Freeze(new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xFF)));
        readonly LinearGradientBrush sweep = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), SpreadMethod = GradientSpreadMethod.Repeat };
        readonly System.Windows.Shapes.Rectangle[] thinBars = new System.Windows.Shapes.Rectangle[10];

        bool ClearMinimal()
        {
            bool changed = minimalHasContent;
            minimalHasContent = false;
            minimalKind = Thin.None;
            return changed;
        }

        // Returns true when the line has to change size (the moon needs a little more room).
        bool minimalPrimaryNone;
        bool renderThin, renderGauges, minimalFixed; // Only while drawing the README images and animation
        Activity renderBubble = Activity.None;

        DateTime thinFlashUntil = DateTime.MinValue;
        Color thinFlashColor = Colors.White;

        // The thin line turns one color for a moment (a pomodoro phase changed, Claude finished)
        internal void FlashThin(bool green)
        {
            thinFlashColor = green ? ThinGreen : ThinYellow;
            thinFlashUntil = DateTime.Now.AddSeconds(1.6);
            Recompute();
        }

        bool UpdateMinimal(Activity primary)
        {
            minimalPrimaryNone = primary == Activity.None;
            Thin kind = Thin.None;
            Color color = Colors.White;
            double fraction = 0;

            if (primary == Activity.Claude)
            {
                kind = ClaudeWaitingNow ? Thin.ClaudeWaiting : Thin.ClaudeWorking;
                color = ThinClay; // Always Claude's own color: the limit has its alerts, the line does not change with it
            }
            else if (primary == Activity.Music)
            {
                kind = Thin.Music;
                color = media.Accent; // The color of the cover
            }
            else if (primary == Activity.Timer)
            {
                kind = Thin.Timer;
                color = OnBreak ? ThinGreen : ThinYellow;
            }
            else if (primary == Activity.External && api != null && api.Activities.Count > 0)
            {
                ApiActivity first = api.Activities.OrderByDescending(a => a.Updated).First();
                bool recording = IsRecording(first);
                if (first.State == "done") { kind = Thin.Done; color = ThinGreen; }
                else if (recording) { kind = Thin.Recording; color = ThinRed; } // A steady red line with a blinking dot: recording is not an error
                else if (first.State == "error") { kind = Thin.Alert; color = ThinRed; }
                else if (first.Progress >= 0) { kind = Thin.Progress; color = ThinBlue; fraction = first.Progress / 100.0; }
                else { kind = Thin.Busy; color = ThinBlue; }
            }
            else if (primary == Activity.Battery && battery != null)
            {
                kind = Thin.Charging;
                color = ThinGreen;
                fraction = battery.Percent / 100.0;
            }
            else if (networkOffline && settings.ShowNetworkAlerts)
            {
                kind = Thin.Offline;
                color = ThinRed;
            }
            else if (battery != null && battery.HasBattery && !battery.Charging && battery.Percent <= 20 && settings.BatteryAlerts)
            {
                kind = Thin.Alert; // The line pulses red
                color = ThinRed;
            }
            else if (settings.ShowFocus && focusState != null && focusState.On)
            {
                kind = Thin.Focus;
            }

            if (DateTime.Now < thinFlashUntil) { kind = Thin.Done; color = thinFlashColor; fraction = 0; }
            minimalKind = kind;
            minimalColor = color;
            minimalFraction = fraction;
            bool has = kind == Thin.Focus;
            bool had = minimalHasContent;
            minimalHasContent = has;
            return has != had;
        }

        // What moves on the line. True while something must keep moving.
        bool AnimateMinimal()
        {
            if (thinBars[0] == null)
            {
                for (int i = 0; i < thinBars.Length; i++)
                {
                    thinBars[i] = new System.Windows.Shapes.Rectangle { Width = 2.4, Height = 2, RadiusX = 1.2, RadiusY = 1.2, Margin = new Thickness(1.3, 0, 1.3, 0), VerticalAlignment = VerticalAlignment.Center, Fill = new SolidColorBrush(ThinPink) };
                    MinimalBars.Children.Add(thinBars[i]);
                }
            }

            bool glow = minimalKind == Thin.ClaudeWorking || minimalKind == Thin.Busy;
            bool recordingNow = RecordingOn;
            MinimalRec.Visibility = recordingNow ? Visibility.Visible : Visibility.Collapsed;
            if (recordingNow) MinimalRec.Opacity = clock % 1.2 < 0.7 ? 1 : 0.15; // Blinks like a recording light
            bool solid = minimalKind == Thin.Recording || minimalKind == Thin.ClaudeWaiting || minimalKind == Thin.Alert || minimalKind == Thin.Done || minimalKind == Thin.Offline;
            bool unread = minimalPrimaryNone && DateTime.Now - unreadAt < TimeSpan.FromMinutes(10);
            MinimalUnread.Visibility = unread ? Visibility.Visible : Visibility.Collapsed;
            if (unread) MinimalUnread.Opacity = 0.3 + 0.7 * (0.5 + 0.5 * Math.Cos(clock * 4));
            bool fills = minimalKind == Thin.Timer || minimalKind == Thin.Progress || minimalKind == Thin.Charging;

            MinimalBase.Opacity = glow || solid ? 1 : 0;
            MinimalGlyph.Visibility = minimalKind == Thin.Focus ? Visibility.Visible : Visibility.Collapsed;
            MinimalBars.Visibility = minimalKind == Thin.Music ? Visibility.Visible : Visibility.Collapsed;
            bool track = fills || (minimalKind == Thin.Music && settings.MinimalMusicPosition);
            MinimalTrack.Visibility = track ? Visibility.Visible : Visibility.Collapsed;

            switch (minimalKind)
            {
                case Thin.ClaudeWorking:
                case Thin.Busy:
                {
                    // Glows repeat along the line (a wide one every 0.9 of its length) and travel to the right, quickly,
                    // so most of the line is lit instead of dark
                    double p = (clock % 1.0) * 0.9;
                    sweep.StartPoint = new Point(p, 0.5);
                    sweep.EndPoint = new Point(p + 0.9, 0.5);
                    if (sweep.GradientStops.Count != 3)
                    {
                        sweep.GradientStops.Clear();
                        sweep.GradientStops.Add(new GradientStop(Colors.Black, 0));
                        sweep.GradientStops.Add(new GradientStop(minimalColor, 0.5));
                        sweep.GradientStops.Add(new GradientStop(Colors.Black, 1));
                    }
                    sweep.GradientStops[1].Color = minimalColor;
                    MinimalBase.Background = sweep;
                    return true;
                }
                case Thin.Offline:
                    if (dashes.GradientStops.Count != 4)
                    {
                        dashes.GradientStops.Clear();
                        dashes.GradientStops.Add(new GradientStop(ThinRed, 0));
                        dashes.GradientStops.Add(new GradientStop(ThinRed, 0.6));
                        dashes.GradientStops.Add(new GradientStop(Colors.Black, 0.6));
                        dashes.GradientStops.Add(new GradientStop(Colors.Black, 1));
                    }
                    MinimalBase.Background = dashes;
                    MinimalBase.Opacity = 0.3 + 0.7 * (0.5 + 0.5 * Math.Cos(clock * 5)); // A dashed red line that blinks
                    return true;
                case Thin.ClaudeWaiting:
                case Thin.Alert:
                    MinimalBase.Background = new SolidColorBrush(minimalColor);
                    MinimalBase.Opacity = 0.3 + 0.7 * (0.5 + 0.5 * Math.Cos(clock * 4.5));
                    return true;
                case Thin.Done:
                    MinimalBase.Background = new SolidColorBrush(minimalColor);
                    return false;
                case Thin.Recording:
                    MinimalBase.Background = new SolidColorBrush(ThinRed);
                    MinimalBase.Opacity = 0.32; // A dim red line; the dot is the light
                    return true;
                case Thin.Timer:
                {
                    double left = minimalFixed ? minimalFraction : timer.Fraction;
                    MinimalFill.Background = new SolidColorBrush(left < 0.1 ? ThinRed : OnBreak && !minimalFixed ? ThinGreen : ThinYellow);
                    MinimalFill.Width = 108 * left;
                    return timer.Running && !timer.Paused;
                }
                case Thin.Progress:
                case Thin.Charging:
                    MinimalFill.Background = new SolidColorBrush(minimalColor);
                    MinimalFill.Width = 108 * Math.Clamp(minimalFraction, 0, 1);
                    return false;
                case Thin.Music:
                {
                    bool playing = media.IsPlaying;
                    for (int i = 0; i < thinBars.Length; i++)
                    {
                        thinBars[i].Height = playing ? 2 + 5 * Math.Abs(Math.Sin(clock * (4.5 + i * 1.1) + i * 0.9)) : 2;
                        ((SolidColorBrush)thinBars[i].Fill).Color = media.Accent;
                    }
                    if (settings.MinimalMusicPosition && media.Duration > TimeSpan.Zero)
                    {
                        MinimalFill.Background = new SolidColorBrush(Color.FromArgb(0x70, media.Accent.R, media.Accent.G, media.Accent.B));
                        MinimalFill.Width = 108 * Math.Clamp(media.Position.TotalSeconds / media.Duration.TotalSeconds, 0, 1);
                    }
                    return playing;
                }
                case Thin.Focus:
                    MinimalGlyph.Text = "\uE708";
                    MinimalGlyph.FontFamily = FluentIcons;
                    MinimalGlyph.Foreground = ThinMoon;
                    return false;
                default:
                    return false;
            }
        }

        // The monitor is not covered by an activity while "keep the monitor" is on (and the island is not an empty pill).
        bool KeepsMonitor(Activity primary)
        {
            return settings.SystemMonitor && settings.MonitorKeepsPlace && !settings.EmptyIsland && primary != Activity.None && primary != Activity.System;
        }
    }
}
