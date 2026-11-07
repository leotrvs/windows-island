using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Every option in one place, laid out like iOS Settings: grouped cards of rows. Changes apply at once;
    // the file is written shortly after the last change, so dragging a slider does not hammer the disk.
    public partial class SettingsWindow : Window
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly Settings settings;
        readonly Action changed;
        readonly Action checkForUpdates;
        readonly Action peekExpanded;
        readonly DispatcherTimer saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        StackPanel card;

        internal SettingsWindow(Settings settings, Action changed, Action checkForUpdates, Action peekExpanded)
        {
            this.settings = settings;
            this.changed = changed;
            this.checkForUpdates = checkForUpdates;
            this.peekExpanded = peekExpanded;
            InitializeComponent();

            Title = Installer.Name + " – " + Strings.SettingsTitle;
            Heading.Text = Strings.SettingsTitle;
            FontSize = 13;
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); settings.Save(); };
            SourceInitialized += (s, e) =>
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                int on = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
                int border = 0x00382E2E; // A faint outline, since there is no title bar to frame the window
                DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
            };
            Closed += (s, e) => { if (saveTimer.IsEnabled) { saveTimer.Stop(); settings.Save(); } };
            CloseButton.MouseEnter += (s, e) => CloseButton.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
            CloseButton.MouseLeave += (s, e) => CloseButton.Background = Brushes.Transparent;
            CloseButton.MouseLeftButtonUp += (s, e) => Close();
            PlaceNearTray();
            BuildTabs();
            Build(tab);
        }

        static int tab; // Remembered while the app runs
        bool showTitles;
        const int AllTabs = -1;

        // Opens in the corner by the taskbar, where the tray icon is, and can be moved like any window.
        void PlaceNearTray()
        {
            Display display = Displays.Primary();
            Rect work = display.WorkArea, bounds = display.Bounds;
            Height = Math.Min(Height, Math.Max(MinHeight, work.Height - 24));
            bool taskbarLeft = work.Left > bounds.Left + 1, taskbarTop = work.Top > bounds.Top + 1;
            Left = taskbarLeft ? work.Left + 12 : work.Right - Width - 12;
// Left well above the taskbar: the hidden-icons flyout opens right there and would cover the window.
            Top = taskbarTop ? work.Top + 12 : Math.Max(work.Top + 12, work.Bottom - Height - 250);
        }

        void BuildTabs()
        {
            string[] names = { Strings.SectionActivities, Strings.SectionBehavior, Strings.SectionAppearance, Strings.SectionSystem };
            StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
            string group = Guid.NewGuid().ToString();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                RadioButton item = new RadioButton { Content = names[i], GroupName = group, Style = (Style)FindResource("Segment"), IsChecked = i == tab };
                item.Checked += (s, e) => { tab = index; Rebuild(); };
                items.Children.Add(item);
            }
            TabBar.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = items,
            };
        }

        void Build(int only)
        {
            bool all = only == AllTabs;
            showTitles = all;
            if (all)
                Content.Children.Add(new TextBlock { Text = Strings.SettingsTitle, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 30, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 14) });
            if (all || only == 0) BuildActivities();
            if (all || only == 1) BuildBehavior();
            if (all || only == 2) BuildAppearance();
            if (all || only == 3) BuildSystem();
        }

        void BuildActivities()
        {
            Section(Strings.SectionActivities);
            Toggle(Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle(Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle(Strings.MenuBanners, null, () => settings.ClaudeBanners, v => settings.ClaudeBanners = v);
            Toggle(Strings.MenuCodex, null, () => settings.ShowCodex, v => settings.ShowCodex = v);
            Toggle(Strings.MenuCodexBanners, null, () => settings.CodexBanners, v => settings.CodexBanners = v);
            Toggle(Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
            Toggle(Strings.MenuBatteryAlerts, Strings.HintBatteryAlerts, () => settings.BatteryAlerts, v => settings.BatteryAlerts = v);
            Toggle(Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
        }

        void BuildBehavior()
        {
            Section(Strings.SectionBehavior);
            Toggle(Strings.MenuEmptyIsland, Strings.HintEmptyIsland, () => settings.EmptyIsland, v => settings.EmptyIsland = v);
            Toggle(Strings.MenuIdlePill, null, () => settings.ShowIdlePill, v => settings.ShowIdlePill = v);
            Toggle(Strings.MenuHover, null, () => settings.ExpandOnHover, v => settings.ExpandOnHover = v);
            Toggle(Strings.MenuFullscreen, null, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v);
            Choice(Strings.MenuAutoHide, Strings.HintAutoHide, new[] { 0, 5, 10, 30, 60, 300 }, true, () => settings.AutoHideSeconds, v => settings.AutoHideSeconds = v);
            Choice(Strings.MenuAlertDuration, null, new[] { 3, 5, 8, 12, 20 }, false, () => settings.AlertSeconds, v => settings.AlertSeconds = v);
            Choice(Strings.MenuPausedMusic, null, new[] { 0, 2, 5, 15, 30, 60 }, false, () => settings.PausedMusicSeconds, v => settings.PausedMusicSeconds = v);
        }

        void BuildAppearance()
        {
            Section(Strings.SectionAppearance);
            Segmented(Strings.MenuPosition, new[] { Strings.MenuTop, Strings.MenuBottom },
                () => string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.Position = i == 1 ? "Bottom" : "Top");
            MonitorRow();
            string[] ringValues = { "Blue", "Green", "Purple" };
            Segmented(Strings.RingColorLabel, new[] { Strings.ColorBlue, Strings.ColorGreen, Strings.ColorPurple },
                () => Math.Max(0, Array.FindIndex(ringValues, v => string.Equals(v, settings.RingColor, StringComparison.OrdinalIgnoreCase))),
                i => settings.RingColor = ringValues[i]);
            SliderRow(Strings.CollapsedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.CollapsedScale, v => settings.CollapsedScale = v, false);
            SliderRow(Strings.ExpandedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.ExpandedScale, v => settings.ExpandedScale = v, true);
            SliderRow(Strings.DistanceFromEdge, Settings.MinOffsetY, Settings.MaxOffsetY, 1, Pixels, () => settings.OffsetY, v => settings.OffsetY = v, false);
            SliderRow(Strings.HorizontalPosition, -Settings.MaxOffsetX, Settings.MaxOffsetX, 10, Horizontal, () => settings.OffsetX, v => settings.OffsetX = v, false);
            ActionRow(Strings.ResetAppearance, () =>
            {
                settings.ResetAppearance();
                Apply();
                Rebuild();
            });
        }

        void BuildSystem()
        {
            Section(Strings.SectionSystem);
            Toggle(Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);
            Toggle(Strings.MenuAutoUpdate, null, () => settings.AutoUpdate, v => settings.AutoUpdate = v);
            ActionRow(Strings.MenuCheckUpdates, checkForUpdates);
            ActionRow(Strings.MenuOpenSettings, () =>
            {
                settings.Save();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.FilePath) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            });

            TextBlock version = new TextBlock
            {
                Text = Installer.Name + " " + Installer.Version,
                Foreground = (Brush)FindResource("Secondary"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 0),
            };
            Content.Children.Add(version);
        }

        void Rebuild()
        {
            Content.Children.Clear();
            Build(tab);
        }

        void Apply()
        {
            changed();
            saveTimer.Stop();
            saveTimer.Start();
        }

        // ---------- Building blocks ----------

        void Section(string title)
        {
            if (showTitles)
                Content.Children.Add(new TextBlock
                {
                    Text = title.ToUpper(CultureInfo.CurrentCulture),
                    Foreground = (Brush)FindResource("Secondary"),
                    FontSize = 12,
                    Margin = new Thickness(16, 18, 0, 6),
                });
            card = new StackPanel();
            Content.Children.Add(new Border
            {
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(12),
                Child = card,
            });
        }

        // One row: label (and hint) on the left, a control on the right, a hairline above all but the first.
        void Row(string label, string hint, UIElement control, UIElement below = null, UIElement left = null)
        {
            Grid grid = new Grid { Margin = new Thickness(16, 10, 14, 10), MinHeight = 30 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (left != null)
            {
                grid.Children.Add(left);
            }
            else
            {
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                text.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
                if (hint != null)
                    text.Children.Add(new TextBlock { Text = hint, FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                grid.Children.Add(text);
            }

            if (control != null)
            {
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
            }

            StackPanel row = new StackPanel();
            if (card.Children.Count > 0)
                row.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Separator"), Margin = new Thickness(16, 0, 0, 0), SnapsToDevicePixels = true });
            row.Children.Add(grid);
            if (below != null) row.Children.Add(below);
            card.Children.Add(row);
        }

        void Toggle(string label, string hint, Func<bool> get, Action<bool> set)
        {
            CheckBox toggle = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = get(), VerticalAlignment = VerticalAlignment.Center };
            toggle.ApplyTemplate();
            Border track = (Border)toggle.Template.FindName("Track", toggle);
            // Template parts are frozen, so the knob gets a transform of its own that can move.
            TranslateTransform knob = new TranslateTransform();
            ((FrameworkElement)toggle.Template.FindName("Knob", toggle)).RenderTransform = knob;
            SolidColorBrush color = new SolidColorBrush(get() ? SwitchOn : SwitchOff);
            track.Background = color;
            knob.X = get() ? KnobTravel : 0;

            toggle.Checked += (s, e) => { AnimateSwitch(color, knob, true); set(true); Apply(); };
            toggle.Unchecked += (s, e) => { AnimateSwitch(color, knob, false); set(false); Apply(); };
            Row(label, hint, toggle);
        }

        static readonly Color SwitchOn = Color.FromRgb(0x30, 0xD1, 0x58);
        static readonly Color SwitchOff = Color.FromRgb(0x39, 0x39, 0x3D);
        const double KnobTravel = 15;

        static void AnimateSwitch(SolidColorBrush color, TranslateTransform knob, bool on)
        {
            color.BeginAnimation(SolidColorBrush.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(on ? SwitchOn : SwitchOff, TimeSpan.FromMilliseconds(180)));
            knob.BeginAnimation(TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(on ? KnobTravel : 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 },
                });
        }

        // Duration choices as a segmented control under the label.
        void Choice(string label, string hint, int[] seconds, bool zeroIsNever, Func<int> get, Action<int> set)
        {
            string[] names = new string[seconds.Length];
            for (int i = 0; i < seconds.Length; i++) names[i] = Strings.Duration(seconds[i], zeroIsNever);
            int current = Array.IndexOf(seconds, get());
            Row(label, hint, null, SegmentBar(names, current, i => set(seconds[i])));
        }

        // Primary, the one with the mouse, all, then each other monitor by the number Windows gives it.
        void MonitorRow()
        {
            System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string> { "Primary", "Active", "All" };
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string> { Strings.MonitorPrimary, Strings.MonitorActive, Strings.MonitorAll };
            foreach (Display display in Displays.All())
            {
                if (display.Primary) continue;
                values.Add(display.Id);
                names.Add(Strings.MonitorNumber(display.Number));
            }
            int current = values.FindIndex(v => string.Equals(v, settings.Monitor, StringComparison.OrdinalIgnoreCase));
            Row(Strings.MonitorLabel, Strings.HintMonitor, null, SegmentBar(names.ToArray(), Math.Max(0, current), i => settings.Monitor = values[i]));
        }

        void Segmented(string label, string[] names, Func<int> get, Action<int> set)
        {
            Row(label, null, SegmentBar(names, get(), set, true));
        }

        FrameworkElement SegmentBar(string[] names, int current, Action<int> set, bool inline = false)
        {
            StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
            string group = Guid.NewGuid().ToString();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                RadioButton item = new RadioButton { Content = names[i], GroupName = group, Style = (Style)FindResource("Segment"), IsChecked = i == current };
                item.Checked += (s, e) => { set(index); Apply(); };
                items.Children.Add(item);
            }
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(1),
                HorizontalAlignment = inline ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = inline ? new Thickness(0) : new Thickness(14, 0, 14, 12),
                Child = items,
            };
        }

        void SliderRow(string label, double min, double max, double step, Func<double, string> format,
                       Func<double> get, Action<double> set, bool peek)
        {
            TextBlock value = new TextBlock { Text = format(get()), Foreground = (Brush)FindResource("Secondary"), VerticalAlignment = VerticalAlignment.Center };
            Slider slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = get(),
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                SmallChange = step,
                LargeChange = step * 2,
                Margin = new Thickness(16, 0, 16, 10),
            };
            slider.ValueChanged += (s, e) =>
            {
                set(Math.Round(e.NewValue, 3));
                value.Text = format(e.NewValue);
                if (peek) peekExpanded();
                Apply();
            };
            Row(label, null, value, slider);
        }

        void ActionRow(string label, Action action)
        {
            Button button = new Button { Content = label, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (s, e) => action();
            Row(null, null, null, null, button);
        }

        // Draws the whole settings page to a PNG for the README, at its full height.
        public void SaveSnapshot(string file)
        {
            const double scale = 2, width = 440;
            Content.Children.Clear();
            Build(AllTabs);
            Content.Margin = new Thickness(20, 18, 20, 28);
            Content.Measure(new Size(width, double.PositiveInfinity));
            Size size = new Size(width, Content.DesiredSize.Height);
            Content.Arrange(new Rect(size));
            Content.UpdateLayout();

            // The brush covers the panel without its margin, so it is placed inside the margin.
            Thickness margin = Content.Margin;
            Rect inner = new Rect(margin.Left, margin.Top, Content.ActualWidth, Content.ActualHeight);
            DrawingVisual page = new DrawingVisual();
            using (DrawingContext context = page.RenderOpen())
            {
                context.DrawRoundedRectangle(Brushes.Black, null, new Rect(size), 16, 16);
                context.DrawRectangle(new VisualBrush(Content) { Stretch = Stretch.Fill }, null, inner);
            }
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(page);
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
        }

        static string Percent(double value) { return Math.Round(value * 100) + "%"; }
        static string Pixels(double value) { return Math.Round(value) + " px"; }

        static string Horizontal(double value)
        {
            if (Math.Abs(value) < 1) return Strings.Centered;
            return (value > 0 ? "+" : "−") + Math.Round(Math.Abs(value)) + " px";
        }
    }
}
