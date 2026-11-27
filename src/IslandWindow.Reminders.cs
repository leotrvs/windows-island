using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The reminders page: typing a reminder and the list.
    public partial class IslandWindow
    {
        // Two fields: what to remember, and when ("amanhã 15h", "sexta 10h", "28/11 14h"). Both can also go together in the
        // first one ("ligar pro João amanhã 15h"); a line under the fields says so. The island is a no-activate overlay, so a
        // click on a field lifts that style and brings the window forward (Native.TakeFocus); Enter, Esc or leaving both fields
        // gives it back and returns the focus to the window that had it. The island stays open while a field has the focus.

        ReminderService reminders;
        bool reminderSample;                                   // README images: made-up reminders, nothing is saved
        List<ReminderStore.Item> reminderSampleItems;
        string reminderDrawn;
        bool typing;                                           // A field has the keyboard
        IntPtr typingPrevious;                                 // The window that had the focus before
        DispatcherTimer reminderMessageTimer;
        const int ReminderRows = 4;

        IReadOnlyList<ReminderStore.Item> ReminderItems
        {
            get
            {
                if (reminderSample) return reminderSampleItems;
                return reminders != null ? reminders.Items : new List<ReminderStore.Item>();
            }
        }

        bool IsReminderField(object element)
        {
            return element is DependencyObject d && (d == ReminderInput || d == ReminderWhenInput || ReminderInputBorder.IsAncestorOf(d) || ReminderWhenBorder.IsAncestorOf(d));
        }

        void WireReminders()
        {
            reminders = (Application.Current as App)?.Reminders;
            if (reminders != null) reminders.Changed += OnRemindersChanged;
            foreach (TextBox field in new[] { ReminderInput, ReminderWhenInput })
            {
                TextBox box = field;
                box.PreviewMouseLeftButtonDown += (s, e) => BeginTyping(box);
                box.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Escape) { e.Handled = true; ReminderInput.Text = ""; ReminderWhenInput.Text = ""; EndTyping(true); }
                    else if (e.Key == Key.Enter) { e.Handled = true; SubmitReminder(); }
                };
                box.TextChanged += (s, e) =>
                {
                    ReminderPlaceholder.Visibility = ReminderInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                    ReminderWhenPlaceholder.Visibility = ReminderWhenInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                    UpdateReminderHint();
                };
                // Moving from one field to the other is still typing
                box.GotKeyboardFocus += (s, e) => UpdateReminderGuide();
                box.LostKeyboardFocus += (s, e) => { UpdateReminderGuide(); if (typing && !IsReminderField(e.NewFocus)) EndTyping(true); };
            }
            Deactivated += (s, e) => { if (typing) EndTyping(false); }; // The user clicked another window: it keeps the focus
            ReminderInputBorder.MouseLeftButtonDown += (s, e) => { if (!ReminderInput.IsKeyboardFocused) { BeginTyping(ReminderInput); ReminderInput.Focus(); } e.Handled = true; };
            ReminderWhenBorder.MouseLeftButtonDown += (s, e) => { if (!ReminderWhenInput.IsKeyboardFocused) { BeginTyping(ReminderWhenInput); ReminderWhenInput.Focus(); } e.Handled = true; };
        }

        void OnRemindersChanged() { UpdateReminders(true); }

        void BeginTyping(TextBox field)
        {
            if (typing || reminderSample) return;
            typingPrevious = Native.Foreground();
            typing = true;
            leaveTimer.Stop();
            Native.TakeFocus(hwnd);
            Activate();
            Dispatcher.BeginInvoke(new Action(() => { if (typing) { field.Focus(); Keyboard.Focus(field); } }), DispatcherPriority.Input);
        }

        void EndTyping(bool giveBack, bool recompute = true)
        {
            if (!typing) return;
            typing = false;
            Native.ReleaseFocus(hwnd, giveBack ? typingPrevious : IntPtr.Zero);
            typingPrevious = IntPtr.Zero;
            if (!Pill.IsMouseOver) leaveTimer.Start(); // Nobody is pointing at the island: it can close now
            if (recompute) Recompute();
        }

        // What the two fields say together: the "when" field goes after the text, so the parser reads them as one sentence
        string ReminderSentence
        {
            get
            {
                string what = ReminderInput.Text.Trim(), when = ReminderWhenInput.Text.Trim();
                return when.Length == 0 ? what : what + " " + when;
            }
        }

        void SubmitReminder()
        {
            if (string.IsNullOrWhiteSpace(ReminderInput.Text) && string.IsNullOrWhiteSpace(ReminderWhenInput.Text)) return;
            ReminderParser.Result parsed = ReminderParser.Parse(ReminderSentence, DateTime.Now);
            if (!parsed.Ok || reminders == null || !reminders.Add(parsed.Text, parsed.When))
            {
                ShowReminderMessage(Strings.ReminderNoTime);
                if (!ReminderWhenInput.IsKeyboardFocused) { ReminderWhenInput.Focus(); Keyboard.Focus(ReminderWhenInput); } // The time goes there
                return;
            }
            ReminderInput.Text = "";
            ReminderWhenInput.Text = "";
            EndTyping(true);
        }

        // Enter without a time: the hint at the top right turns orange for a moment, then goes back to the live hint
        void ShowReminderMessage(string text)
        {
            ReminderMessageText.Text = text;
            ReminderMessageText.Foreground = Amber;
            ReminderMessageText.Visibility = Visibility.Visible;
            if (reminderMessageTimer == null)
            {
                reminderMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                reminderMessageTimer.Tick += (s, e) => { reminderMessageTimer.Stop(); UpdateReminderHint(); };
            }
            reminderMessageTimer.Stop();
            reminderMessageTimer.Start();
        }

        // While typing, the top right says when the reminder would go off ("→ sexta 10:00"). Nothing while there is no time
        // yet, and no orange until Enter: half a sentence is not a mistake.
        void UpdateReminderHint()
        {
            reminderMessageTimer?.Stop();
            UpdateReminderGuide();
            ReminderParser.Result parsed = string.IsNullOrWhiteSpace(ReminderSentence) ? null : ReminderParser.Parse(ReminderSentence, DateTime.Now);
            if (parsed == null || !parsed.Ok) { ReminderMessageText.Visibility = Visibility.Collapsed; return; }
            ReminderMessageText.Text = "→ " + ReminderWhen(parsed.When.ToUniversalTime(), true);
            ReminderMessageText.Foreground = (Brush)FindResource("Secondary");
            ReminderMessageText.Visibility = Visibility.Visible;
        }

        // The line under the fields: examples of a time, or, once the first field is being filled, that it can hold both
        void UpdateReminderGuide()
        {
            bool together = ReminderInput.Text.Length > 0 && !ReminderWhenInput.IsKeyboardFocused && ReminderWhenInput.Text.Length == 0;
            ReminderGuideText.Text = together ? Strings.ReminderGuideTogether : Strings.ReminderGuide;
        }

        // A reminder went off: its own text is the big line, "Reminder · 15:00" the small one above it
        internal void ShowReminderBanner(string text, bool late)
        {
            string label = Strings.ReminderBanner + " · " + DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) + (late ? " " + Strings.ReminderLate : "");
            BannerItem item = new BannerItem { App = label, Title = text, Seconds = Math.Max(settings.AlertSeconds, 10), Urgent = true };
            ApplyKind(item, "reminder");
            Enqueue(item);
        }

        // "15:00" today, "amanhã 15:00", "sex 15:00" this week, "28/11 15:00" further on. The hint says "hoje" too when asked.
        static string ReminderWhen(DateTime dueUtc, bool sayToday = false)
        {
            DateTime local = dueUtc.ToLocalTime();
            string time = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            if (local.Date == DateTime.Today) return sayToday ? Strings.ReminderToday + " " + time : time;
            if (local.Date == DateTime.Today.AddDays(1)) return Strings.ReminderTomorrow + " " + time;
            if (local.Date < DateTime.Today.AddDays(7)) return Strings.WeekdayShort(local.DayOfWeek) + " " + time;
            return local.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture) + " " + time;
        }

        void UpdateReminders(bool force)
        {
            IReadOnlyList<ReminderStore.Item> items = ReminderItems;
            System.Text.StringBuilder key = new System.Text.StringBuilder(Strings.Current);
            int shown = Math.Min(ReminderRows, items.Count);
            for (int i = 0; i < shown; i++) key.Append('|').Append(items[i].Id).Append(ReminderWhen(items[i].DueUtc));
            string drawn = key.ToString();
            if (!force && drawn == reminderDrawn) return;
            reminderDrawn = drawn;

            ReminderTitleText.Text = Strings.ReminderTitle;
            ReminderPlaceholder.Text = Strings.ReminderPlaceholder;
            ReminderPlaceholder.Visibility = ReminderInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ReminderWhenPlaceholder.Text = Strings.ReminderWhenPlaceholder;
            ReminderWhenPlaceholder.Visibility = ReminderWhenInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateReminderGuide();
            ReminderEmptyText.Text = Strings.ReminderEmpty;
            ReminderEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ReminderList.Children.Clear();
            for (int i = 0; i < shown; i++)
            {
                ReminderStore.Item item = items[i];
                string id = item.Id;
                Border row = new Border { Height = 22, CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Margin = new Thickness(-6, 0, -6, 0), Padding = new Thickness(6, 0, 3, 0) };
                Grid line = new Grid();
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.Children.Add(new TextBlock { Text = item.Text, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                TextBlock when = new TextBlock { Text = ReminderWhen(item.DueUtc), FontSize = 12.5, Margin = new Thickness(10, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Secondary") };
                Grid.SetColumn(when, 1);
                line.Children.Add(when);
                Border x = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
                x.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 10, Foreground = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                x.MouseEnter += (s, e) => x.Background = ClipboardHover;
                x.MouseLeave += (s, e) => x.Background = Brushes.Transparent;
                x.MouseLeftButtonDown += (s, e) => e.Handled = true;
                x.MouseLeftButtonUp += (s, e) => { e.Handled = true; if (!reminderSample) reminders?.Remove(id); };
                Grid.SetColumn(x, 2);
                line.Children.Add(x);
                row.Child = line;
                ReminderList.Children.Add(row);
            }
        }
    }
}
