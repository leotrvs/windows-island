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
        // A field to type in ("call John 3pm") and the next 5 reminders. The island is a no-activate overlay, so a click on the
        // field lifts that style and brings the window forward (Native.TakeFocus); Enter, Esc or leaving the field gives it
        // back and returns the focus to the window that had it. The island stays open while the field has the focus.

        ReminderService reminders;
        bool reminderSample;                                   // README images: made-up reminders, nothing is saved
        List<ReminderStore.Item> reminderSampleItems;
        string reminderDrawn;
        bool typing;                                           // The field has the keyboard
        IntPtr typingPrevious;                                 // The window that had the focus before
        DispatcherTimer reminderMessageTimer;

        IReadOnlyList<ReminderStore.Item> ReminderItems
        {
            get
            {
                if (reminderSample) return reminderSampleItems;
                return reminders != null ? reminders.Items : new List<ReminderStore.Item>();
            }
        }

        void WireReminders()
        {
            reminders = (Application.Current as App)?.Reminders;
            if (reminders != null) reminders.Changed += OnRemindersChanged;
            ReminderInput.PreviewMouseLeftButtonDown += (s, e) => BeginTyping();
            ReminderInput.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; ReminderInput.Text = ""; EndTyping(true); }
                else if (e.Key == Key.Enter) { e.Handled = true; SubmitReminder(); }
            };
            ReminderInput.TextChanged += (s, e) => ReminderPlaceholder.Visibility = ReminderInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ReminderInput.LostKeyboardFocus += (s, e) => { if (typing) EndTyping(true); };
            Deactivated += (s, e) => { if (typing) EndTyping(false); }; // The user clicked another window: it keeps the focus
            ReminderInputBorder.MouseLeftButtonDown += (s, e) => { if (!ReminderInput.IsKeyboardFocused) { BeginTyping(); ReminderInput.Focus(); } e.Handled = true; };
        }

        void OnRemindersChanged() { UpdateReminders(true); }

        void BeginTyping()
        {
            if (typing || reminderSample) return;
            typingPrevious = Native.Foreground();
            typing = true;
            leaveTimer.Stop();
            Native.TakeFocus(hwnd);
            Activate();
            Dispatcher.BeginInvoke(new Action(() => { if (typing) { ReminderInput.Focus(); Keyboard.Focus(ReminderInput); } }), DispatcherPriority.Input);
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

        void SubmitReminder()
        {
            string input = ReminderInput.Text;
            if (string.IsNullOrWhiteSpace(input)) return;
            ReminderParser.Result parsed = ReminderParser.Parse(input, DateTime.Now);
            if (!parsed.Ok) { ShowReminderMessage(Strings.ReminderNoTime); return; }
            if (reminders == null || !reminders.Add(parsed.Text, parsed.When)) { ShowReminderMessage(Strings.ReminderNoTime); return; }
            ReminderInput.Text = "";
            EndTyping(true);
        }

        void ShowReminderMessage(string text)
        {
            ReminderMessageText.Text = text;
            ReminderMessageText.Visibility = Visibility.Visible;
            if (reminderMessageTimer == null)
            {
                reminderMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                reminderMessageTimer.Tick += (s, e) => { reminderMessageTimer.Stop(); ReminderMessageText.Visibility = Visibility.Collapsed; };
            }
            reminderMessageTimer.Stop();
            reminderMessageTimer.Start();
        }

        static string ReminderWhen(DateTime dueUtc)
        {
            DateTime local = dueUtc.ToLocalTime();
            string time = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            if (local.Date == DateTime.Today) return time;
            if (local.Date == DateTime.Today.AddDays(1)) return Strings.ReminderTomorrow + " " + time;
            return local.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture) + " " + time;
        }

        void UpdateReminders(bool force)
        {
            IReadOnlyList<ReminderStore.Item> items = ReminderItems;
            System.Text.StringBuilder key = new System.Text.StringBuilder(Strings.Current);
            int shown = Math.Min(5, items.Count);
            for (int i = 0; i < shown; i++) key.Append('|').Append(items[i].Id).Append(ReminderWhen(items[i].DueUtc));
            string drawn = key.ToString();
            if (!force && drawn == reminderDrawn) return;
            reminderDrawn = drawn;

            ReminderTitleText.Text = Strings.ReminderTitle;
            ReminderPlaceholder.Text = Strings.ReminderPlaceholder;
            ReminderPlaceholder.Visibility = ReminderInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ReminderEmptyText.Text = Strings.ReminderEmpty;
            ReminderEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ReminderList.Children.Clear();
            for (int i = 0; i < shown; i++)
            {
                ReminderStore.Item item = items[i];
                string id = item.Id;
                Border row = new Border { Height = 17, CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Margin = new Thickness(-6, 0, -6, 0), Padding = new Thickness(6, 0, 3, 0) };
                Grid line = new Grid();
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.Children.Add(new TextBlock { Text = item.Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                TextBlock when = new TextBlock { Text = ReminderWhen(item.DueUtc), FontSize = 10.5, Margin = new Thickness(10, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Secondary") };
                Grid.SetColumn(when, 1);
                line.Children.Add(when);
                Border x = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
                x.Child = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 9, Foreground = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
