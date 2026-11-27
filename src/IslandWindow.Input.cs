using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsIsland
{
    // Pages picked with the wheel or the dots, and what the mouse does on the pill: hover, click, drag, reveal.
    public partial class IslandWindow
    {
        Activity? page; // The page picked with the wheel or the dots; null = the default one

        List<Activity> Pages()
        {
            List<Activity> pages = new List<Activity>();
            foreach (string name in settings.PageOrderList())
            {
                if (name == "Music" && settings.PageMusic && settings.ShowMusic && media.HasSession) pages.Add(Activity.Music);
                else if (name == "Claude")
                {
                    if (settings.PageClaude && (settings.ShowClaude || CodexSectionVisible)) pages.Add(Activity.Claude);
                    if (ApiActive) pages.Add(Activity.External); // Apps' own activities sit right after Claude
                }
                else if (name == "System" && settings.PageSystem) pages.Add(Activity.System);
                else if (name == "Timer" && settings.PageTimer) pages.Add(Activity.Timer);
                else if (name == "Quick" && settings.PageQuick) pages.Add(Activity.Quick);
                else if (name == "Clipboard" && settings.ClipboardHistory && clipboardList != null) pages.Add(Activity.Clipboard); // Only while the option is on
                else if (name == "Reminders" && settings.ShowReminders && (reminders != null || reminderSample)) pages.Add(Activity.Reminder);
            }
            return pages;
        }

        // The page the island opens on: the one for what is happening, if it is on, else the first page that is.
        View DefaultPage(Activity primary)
        {
            Activity wanted = primary == Activity.None && settings.SystemMonitor ? Activity.System
                            : primary == Activity.Music ? Activity.Music
                            : primary == Activity.Timer ? Activity.Timer
                            : primary == Activity.External ? Activity.External
                            : settings.ShowClaude || CodexSectionVisible ? Activity.Claude
                            : settings.ShowMusic && media.HasSession ? Activity.Music
                            : Activity.None;
            List<Activity> pages = Pages();
            if (wanted != Activity.None && pages.Contains(wanted)) return ExpandedFor(wanted);
            return pages.Count > 0 ? ExpandedFor(pages[0]) : View.Idle;
        }

        static View ExpandedFor(Activity activity)
        {
            return activity == Activity.Music ? View.MusicExpanded : activity == Activity.Timer ? View.TimerExpanded : activity == Activity.System ? View.SystemExpanded : activity == Activity.External ? View.ExternalExpanded : activity == Activity.Quick ? View.QuickExpanded : activity == Activity.Clipboard ? View.ClipboardExpanded : activity == Activity.Reminder ? View.ReminderExpanded : View.ClaudeExpanded;
        }

        static Activity ActivityOf(View value)
        {
            return value == View.MusicExpanded ? Activity.Music : value == View.TimerExpanded ? Activity.Timer : value == View.SystemExpanded ? Activity.System : value == View.ExternalExpanded ? Activity.External : value == View.QuickExpanded ? Activity.Quick : value == View.ClipboardExpanded ? Activity.Clipboard : value == View.ReminderExpanded ? Activity.Reminder : Activity.Claude;
        }

        View? PickedPage()
        {
            if (!page.HasValue || !Pages().Contains(page.Value)) return null;
            return ExpandedFor(page.Value);
        }

        DateTime lastWheel;
        void OnPillWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsExpandedView(view) || view == View.Banner || view == View.Permission) return;
            e.Handled = true;
            if (typing) return; // The page stays while a reminder is being typed
            if (DateTime.Now - lastWheel < TimeSpan.FromMilliseconds(280)) return; // A touchpad sends a burst
            lastWheel = DateTime.Now;
            List<Activity> pages = Pages();
            if (pages.Count == 0) return;
            int index = pages.IndexOf(ActivityOf(view));
            page = pages[((index < 0 ? 0 : index) + (e.Delta < 0 ? 1 : -1) + pages.Count) % pages.Count];
            Recompute();
        }

        // One dot per page under the content, the open one lit. A click on a dot opens that page.
        List<Activity> drawnPages;
        Activity drawnCurrent;
        void UpdateDots()
        {
            List<Activity> pages = Pages();
            bool show = IsExpandedView(view) && view != View.Banner && view != View.Permission && pages.Count > 1;
            PageDots.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            Activity current = ActivityOf(view);
            if (drawnPages != null && drawnPages.SequenceEqual(pages) && drawnCurrent == current) return;
            drawnPages = pages;
            drawnCurrent = current;
            PageDots.Children.Clear();
            foreach (Activity activity in pages)
            {
                Activity target = activity;
                Border dot = new Border
                {
                    Width = 18, Height = 14, Background = Brushes.Transparent, Cursor = Cursors.Hand,
                    Child = new Ellipse { Width = 5, Height = 5, Fill = activity == current ? White : DimDot },
                };
                dot.MouseLeftButtonUp += (s, e) => { e.Handled = true; page = target; Recompute(); };
                dot.MouseLeftButtonDown += (s, e) => e.Handled = true;
                PageDots.Children.Add(dot);
            }
        }
        static readonly Brush DimDot = Freeze(new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)));

        void SetHovering(bool value)
        {
            if (hovering == value) return;
            hovering = value;
            if (!value && typing) { hovering = true; return; } // Typing a reminder: the island stays open
            UpdatePrivacy();
            if (!value) page = null; // The next visit starts on the default page again
            Recompute();
        }

        void OnPillEnter(object sender, MouseEventArgs e)
        {
            leaveTimer.Stop();
            if (!hovering && settings.ExpandOnHover) enterTimer.Start();
        }

        void OnPillLeave(object sender, MouseEventArgs e)
        {
            enterTimer.Stop();
            if (hovering) leaveTimer.Start();
        }

        void OnPillClick(object sender, MouseButtonEventArgs e)
        {
            if (suppressClick) { suppressClick = false; return; } // That press was a drag, not a click
            enterTimer.Stop();
            if (view == View.ReminderExpanded && e.OriginalSource is DependencyObject source && (source == ReminderInput || ReminderInputBorder.IsAncestorOf(source))) return; // A click in the field is for typing
            if (view == View.Banner)
            {
                bool fromClaude = currentBanner == null || currentBanner.FromClaude || currentBanner.FromCodex;
                if (currentBanner?.OnClick != null) { AnswerBannerQuestion(); return; }
                Action tap = currentBanner?.OnTap; // A click on the note itself (a finished download opens Explorer)
                bannerUntil = DateTime.MinValue;
                if (tap != null) { currentBanner.OnTap = null; if (banners.Count > 0) NextBanner(); else Recompute(); tap(); return; }
                if (banners.Count > 0) { NextBanner(); return; }
                if (!fromClaude) { Recompute(); return; } // A notification click just dismisses it
                SetHovering(true);
                return;
            }
            SetHovering(!hovering);
        }

        // ---------- Tucking the island away ----------
        // Dragging the island up (down when it sits at the bottom) puts it away, so what is behind can be used.
        // It comes back with new activity or an alert, or when the mouse rests on its spot, or on the screen edge above it, for a moment.
        const double TuckDistance = 10, TuckRevealSeconds = 2.5;
        bool tucked, tuckPrivacy, suppressClick;
        Activity tuckPrimary, lastPrimary;
        DateTime? dwellStart;
        Point? dragStart;

        void OnPillDrag(object sender, MouseEventArgs e)
        {
            if (!dragStart.HasValue || e.LeftButton != MouseButtonState.Pressed) { dragStart = null; return; }
            double dy = e.GetPosition(this).Y - dragStart.Value.Y;
            if ((IsBottom ? dy : -dy) < TuckDistance) return;
            dragStart = null;
            suppressClick = true;
            tucked = true;
            tuckPrimary = lastPrimary;
            tuckPrivacy = PrivacyOn;
            dwellStart = null;
            enterTimer.Stop();
            leaveTimer.Stop();
            SetHovering(false);
            Recompute();
        }

        // While auto-hidden, bringing the mouse to the island's spot brings it back.
        void CheckReveal()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null) return;
            Point cursor = source.CompositionTarget.TransformFromDevice.Transform(Native.CursorPosition());
            if (double.IsNaN(cursor.X)) return;

            double halfWidth = 150 * settings.CollapsedScale, reach = 36 * settings.CollapsedScale;
            double center = Left + Width / 2;
            double edge = IsBottom ? Top + Height : Top;
            double depth = settings.OffsetY + reach;
            bool insideX = Math.Abs(cursor.X - center) <= halfWidth;
            bool insideY = IsBottom ? cursor.Y >= edge - depth && cursor.Y <= edge : cursor.Y >= edge && cursor.Y <= edge + depth;
            // A tucked island is not there to aim at, so the screen edge in the middle, where it sits, also counts
            bool atEdge = tucked && Math.Abs(cursor.X - center) <= 250 * settings.CollapsedScale &&
                          (IsBottom ? cursor.Y >= edge - 4 : cursor.Y <= edge + 4);
            if (!(insideX && insideY) && !atEdge) { dwellStart = null; return; }

            if (tucked)
            {
                // A tucked island comes back after the mouse has rested on its spot for a while
                if (!dwellStart.HasValue) { dwellStart = DateTime.Now; return; }
                if (DateTime.Now - dwellStart.Value < TimeSpan.FromSeconds(TuckRevealSeconds)) return;
                tucked = false;
                dwellStart = null;
            }
            lastActive = DateTime.Now;
            Recompute();
        }
    }
}
