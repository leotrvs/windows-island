using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly Settings settings;
        readonly Action changed;
        readonly UpdateFlow updates;
        readonly Action peekExpanded;
        readonly DispatcherTimer saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        StackPanel card;
        double restingTop;

        internal SettingsWindow(Settings settings, Action changed, UpdateFlow updates, Action peekExpanded)
        {
            this.settings = settings;
            this.changed = changed;
            this.updates = updates;
            this.peekExpanded = peekExpanded;
            InitializeComponent();

            // The scroll bar inside the ScrollViewer ignores implicit styles, so it gets its thin one by hand.
            Scroller.ApplyTemplate();
            System.Windows.Controls.Primitives.ScrollBar bar = Scroller.Template.FindName("PART_VerticalScrollBar", Scroller) as System.Windows.Controls.Primitives.ScrollBar;
            if (bar != null) bar.Style = (Style)FindResource("ThinScroll");

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
            Closed += (s, e) =>
            {
                try
                {
                    Rect r = RestoreBounds;
                    if (!r.IsEmpty) { settings.SettingsLeft = r.Left; settings.SettingsTop = r.Top; settings.SettingsWidth = r.Width; settings.SettingsHeight = r.Height; }
                    saveTimer.Stop();
                    settings.Save();
                }
                catch (Exception) { }
                if (updates != null) updates.Changed -= OnUpdateChanged;
            };
            // Like a tray flyout: it takes the focus (which closes the hidden-icons panel), slides up into the
            // corner, stays above other windows until you click elsewhere, and Esc closes it.
            Topmost = true;
            ContentRendered += (s, e) =>
            {
                SetForegroundWindow(new WindowInteropHelper(this).Handle);
                Activate();
                double slide = SystemParameters.ClientAreaAnimation ? 22 : 0;
                if (slide > 0)
                {
                    System.Windows.Media.Animation.DoubleAnimation up = new System.Windows.Media.Animation.DoubleAnimation(restingTop + slide, restingTop, TimeSpan.FromMilliseconds(220))
                    {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
                    };
                    up.Completed += (a, b) => { BeginAnimation(TopProperty, null); Top = restingTop; }; // Let the window be dragged afterwards
                    BeginAnimation(TopProperty, up);
                }
            };
            Deactivated += (s, e) => Topmost = false;
            Activated += (s, e) => Topmost = true;
            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
            CloseButton.MouseEnter += (s, e) => CloseButton.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
            CloseButton.MouseLeave += (s, e) => CloseButton.Background = Brushes.Transparent;
            CloseButton.MouseLeftButtonUp += (s, e) => Close();
            PlaceNearTray();
            BuildTabs();
            Build(tab);
        }

        static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly Brush IconBrush = new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xBC));

        // ---------- Language ----------

        void LanguageRow()
        {
            string[] values = { "Auto", "en", "pt", "es", "ru" };
            string[] names = { Strings.LanguageAuto, "English", "Português", "Español", "Русский" };
            int current = Math.Max(0, Array.FindIndex(values, v => string.Equals(v, settings.Language, StringComparison.OrdinalIgnoreCase)));
            Row("\uE774", Strings.LanguageLabel, null, null, SegmentBar(names, current, i =>
            {
                settings.Language = values[i];
                Strings.Use(settings.Language);
                Apply();
                Dispatcher.BeginInvoke(new Action(RefreshTexts)); // After this click has finished
            }));
        }

        // Titles, tabs and rows again, in the language just chosen.
        void RefreshTexts()
        {
            Title = Installer.Name + " – " + Strings.SettingsTitle;
            Heading.Text = Strings.SettingsTitle;
            BuildTabs();
            Rebuild();
        }

        // ---------- Updates ----------

        TextBlock updateGlyph, updateText;
        RotateTransform spin;

        void UpdateRow()
        {
            spin = new RotateTransform();
            updateGlyph = new TextBlock { FontFamily = IconFont, FontSize = 16, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, RenderTransform = spin, RenderTransformOrigin = new Point(0.5, 0.5) };
            updateText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
            line.Children.Add(updateGlyph);
            line.Children.Add(updateText);
            line.MouseLeftButtonUp += (s, e) =>
            {
                if (updates == null) return;
                if (updates.State == UpdateFlow.Phase.Available) _ = updates.InstallAsync();
                else if (updates.State == UpdateFlow.Phase.Idle || updates.State == UpdateFlow.Phase.UpToDate || updates.State == UpdateFlow.Phase.Failed) _ = updates.CheckAsync(true);
            };
            Row(null, null, null, null, null, line);
            if (updates != null)
            {
                updates.Changed -= OnUpdateChanged;
                updates.Changed += OnUpdateChanged;
            }
            RefreshUpdateRow();
        }

        void OnUpdateChanged()
        {
            Dispatcher.BeginInvoke(new Action(RefreshUpdateRow));
        }

        void RefreshUpdateRow()
        {
            if (updateText == null) return;
            UpdateFlow.Phase phase = updates?.State ?? UpdateFlow.Phase.Idle;
            string glyph = "\uE895";
            string text = Strings.MenuCheckUpdates;
            Brush color = (Brush)FindResource("Accent");
            bool spinning = false;
            switch (phase)
            {
                case UpdateFlow.Phase.Checking: glyph = "\uE895"; text = Strings.UpdateChecking; color = (Brush)FindResource("Secondary"); spinning = true; break;
                case UpdateFlow.Phase.Available: glyph = "\uE896"; text = Strings.UpdateDownload(updates.Version); break;
                case UpdateFlow.Phase.Downloading: glyph = "\uE895"; text = Strings.UpdateDownloading; color = (Brush)FindResource("Secondary"); spinning = true; break;
                case UpdateFlow.Phase.UpToDate: glyph = "\uE73E"; text = Strings.UpToDate; color = (Brush)FindResource("On"); break;
                case UpdateFlow.Phase.Failed: glyph = "\uE7BA"; text = Strings.UpdateFailed; color = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)); break;
            }
            updateGlyph.Text = glyph;
            updateGlyph.Foreground = color;
            updateText.Text = text;
            updateText.Foreground = color;
            if (spinning)
                spin.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
            else
                spin.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        // Opens on the System tab, where updates live.
        public void GoToSystem()
        {
            tab = 3;
            BuildTabs();
            Rebuild();
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
            Top = taskbarTop ? work.Top + 12 : work.Bottom - Height - 12;

            // Where it was left last time, if that is still on a screen
            if (settings.SettingsLeft.HasValue && settings.SettingsTop.HasValue && settings.SettingsWidth.HasValue && settings.SettingsHeight.HasValue)
            {
                Rect saved = new Rect(settings.SettingsLeft.Value, settings.SettingsTop.Value, Math.Max(MinWidth, settings.SettingsWidth.Value), Math.Max(MinHeight, settings.SettingsHeight.Value));
                foreach (Display d in Displays.All())
                {
                    Rect visible = Rect.Intersect(saved, d.WorkArea);
                    if (!visible.IsEmpty && visible.Width >= 120 && visible.Height >= 80 && saved.Top >= d.WorkArea.Top - 4)
                    {
                        Width = saved.Width; Height = saved.Height; Left = saved.Left; Top = saved.Top;
                        break;
                    }
                }
            }
            restingTop = Top;
            // Start the slide-up from below before the window is first shown, so it does not appear and then jump
            if (SystemParameters.ClientAreaAnimation) Top = restingTop + 22;
        }

        void BuildTabs()
        {
            string[] names = { Strings.SectionActivities, Strings.SectionBehavior, Strings.SectionAppearance, Strings.SectionSystem };
            TabBar.Content = SlidingBar(names, tab, index => { tab = index; Rebuild(); }, Color.FromRgb(0x1C, 0x1C, 0x1E), HorizontalAlignment.Left, new Thickness(0));
        }

        // Choices in a rounded track, with a pill that slides to the one picked.
        FrameworkElement SlidingBar(string[] names, int current, Action<int> pick, Color track, HorizontalAlignment align, Thickness margin)
        {
            Grid host = new Grid();
            Border pill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x63, 0x63, 0x66)),
                CornerRadius = new CornerRadius(7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 1.5, 0, 1.5),
                Width = 0,
            };
            TranslateTransform move = new TranslateTransform();
            pill.RenderTransform = move;
            StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
            int selected = Math.Clamp(current, 0, names.Length - 1);

            Action<bool> place = animate =>
            {
                Border chip = (Border)items.Children[selected];
                if (chip.ActualWidth <= 0) return;
                double x = chip.TranslatePoint(new Point(0, 0), host).X, w = chip.ActualWidth;
                if (!animate)
                {
                    move.BeginAnimation(TranslateTransform.XProperty, null);
                    pill.BeginAnimation(FrameworkElement.WidthProperty, null);
                    move.X = x;
                    pill.Width = w;
                    return;
                }
                System.Windows.Media.Animation.CubicEase ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                move.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(x, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                pill.BeginAnimation(FrameworkElement.WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(w, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            };

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Border chip = new Border
                {
                    Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(1.5),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock { Text = names[i], FontSize = 12, Foreground = Brushes.White },
                };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    if (index == selected) return;
                    selected = index;
                    place(true);
                    pick(index);
                };
                items.Children.Add(chip);
            }
            host.Children.Add(pill);
            host.Children.Add(items);
            host.Loaded += (s, e) => { host.UpdateLayout(); place(false); };
            host.SizeChanged += (s, e) => place(false);

            return new Border
            {
                Background = new SolidColorBrush(track),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(1),
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Child = host,
            };
        }

        void Build(int only)
        {
            bool all = only == AllTabs;
            showTitles = all;
            if (all)
                Content.Children.Add(new TextBlock { Text = Strings.SettingsTitle, FontSize = 30, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 14) });
            if (all || only == 0) BuildActivities();
            if (all || only == 1) BuildBehavior();
            if (all || only == 2) BuildAppearance();
            if (all || only == 3) BuildSystem();
        }

        void BuildActivities()
        {
            Section(Strings.SectionActivities);
            Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle("\uEA8F", Strings.MenuBanners, null, () => settings.ClaudeBanners, v => settings.ClaudeBanners = v);
            Toggle("img:codex", Strings.MenuCodex, null, () => settings.ShowCodex, v => settings.ShowCodex = v);
            Toggle("\uEA8F", Strings.MenuCodexBanners, null, () => settings.CodexBanners, v => settings.CodexBanners = v);
            Toggle("\uE720", Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
            int[] nameSeconds = { 0, 1, 2, 3 };
            string[] nameLabels = { Strings.Off, Strings.Duration(1, false), Strings.Duration(2, false), Strings.Duration(3, false) };
            Row("\uE77B", Strings.MenuPrivacyNames, Strings.HintPrivacyNames, null,
                SegmentBar(nameLabels, Math.Max(0, Array.IndexOf(nameSeconds, settings.PrivacyNameSeconds)), i => settings.PrivacyNameSeconds = nameSeconds[i]));
            Toggle("\uE83E", Strings.MenuBatteryActivity, Strings.HintBatteryActivity, () => settings.BatteryActivity, v => settings.BatteryActivity = v);
            Toggle("\uE83F", Strings.MenuBatteryAlerts, Strings.HintBatteryAlerts, () => settings.BatteryAlerts, v => settings.BatteryAlerts = v);
            Toggle("\uE702", Strings.MenuDevices, Strings.HintDevices, () => settings.ShowDeviceAlerts, v => settings.ShowDeviceAlerts = v);
            Toggle("\uE701", Strings.MenuNetworkAlerts, Strings.HintNetworkAlerts, () => settings.ShowNetworkAlerts, v => settings.ShowNetworkAlerts = v);
            Toggle("\uE708", Strings.MenuFocus, Strings.HintFocus, () => settings.ShowFocus, v => settings.ShowFocus = v);
            Toggle("\uE767", Strings.MenuLevels, Strings.HintLevels, () => settings.ShowLevels, v => settings.ShowLevels = v);
            Toggle("\uE74F", Strings.MenuHideOsd, Strings.HintHideOsd, () => settings.HideWindowsOsd, v => settings.HideWindowsOsd = v);
            Toggle("\uE722", Strings.MenuScreenshot, Strings.HintScreenshot, () => settings.ShowScreenshotAlert, v => settings.ShowScreenshotAlert = v);
            Toggle("\uE7E7", Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
        }

        void BuildBehavior()
        {
            Section(Strings.SectionBehavior);
            Toggle("\uE7B3", Strings.MenuEmptyIsland, Strings.HintEmptyIsland, () => settings.EmptyIsland, v => settings.EmptyIsland = v);
            Toggle("\uE7F8", Strings.MenuIdlePill, null, () => settings.ShowIdlePill, v => settings.ShowIdlePill = v);
            Toggle("\uE7C9", Strings.MenuHover, null, () => settings.ExpandOnHover, v => settings.ExpandOnHover = v);
            Toggle("\uE740", Strings.MenuFullscreen, null, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v);
            Toggle("\uE765", Strings.MenuHotkey, Strings.HintHotkey, () => settings.HotkeyEnabled, v => { settings.HotkeyEnabled = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
            if (settings.HotkeyEnabled) HotkeyRow();
            Choice("\uE916", Strings.MenuAutoHide, Strings.HintAutoHide, new[] { 0, 5, 10, 30, 60, 300 }, true, () => settings.AutoHideSeconds, v => settings.AutoHideSeconds = v);
            Choice("\uE81C", Strings.MenuAlertDuration, null, new[] { 3, 5, 8, 12, 20 }, false, () => settings.AlertSeconds, v => settings.AlertSeconds = v);
            Choice("\uE769", Strings.MenuPausedMusic, null, new[] { 0, 2, 5, 15, 30, 60 }, false, () => settings.PausedMusicSeconds, v => settings.PausedMusicSeconds = v);

            Section(Strings.SectionPages);
            Toggle("\uE8D6", Strings.PageMusicLabel, Strings.HintPages, () => settings.PageMusic, v => settings.PageMusic = v);
            Toggle("img:claude", Strings.PageClaudeLabel, null, () => settings.PageClaude, v => settings.PageClaude = v);
            Toggle("\uE9D9", Strings.PageSystemLabel, null, () => settings.PageSystem, v => settings.PageSystem = v);
            Toggle("\uE916", Strings.PageTimerLabel, null, () => settings.PageTimer, v => settings.PageTimer = v);
        }

        void BuildAppearance()
        {
            Section(Strings.SectionAppearance);
            Segmented("\uE745", Strings.MenuPosition, new[] { Strings.MenuTop, Strings.MenuBottom },
                () => string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.Position = i == 1 ? "Bottom" : "Top");
            MonitorRow();
            string[] ringValues = { "Blue", "Green", "Purple" };
            Segmented("\uE790", Strings.RingColorLabel, new[] { Strings.ColorBlue, Strings.ColorGreen, Strings.ColorPurple },
                () => Math.Max(0, Array.FindIndex(ringValues, v => string.Equals(v, settings.RingColor, StringComparison.OrdinalIgnoreCase))),
                i => settings.RingColor = ringValues[i]);
            Toggle("\uE9D9", Strings.MenuSystemMonitor, Strings.HintSystemMonitor, () => settings.SystemMonitor, v => settings.SystemMonitor = v);
            string[] monitorValues = { "Rings", "Bars", "Numbers" };
            Segmented("\uE790", Strings.MonitorStyleLabel, new[] { Strings.StyleRings, Strings.StyleBars, Strings.StyleNumbers },
                () => Math.Max(0, Array.FindIndex(monitorValues, v => string.Equals(v, settings.MonitorStyle, StringComparison.OrdinalIgnoreCase))),
                i => settings.MonitorStyle = monitorValues[i]);
            string[] bannerValues = { "Small", "Normal" };
            Segmented("\uE8A1", Strings.BannerSizeLabel, new[] { Strings.BannerSmall, Strings.BannerNormal },
                () => string.Equals(settings.BannerSize, "Normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.BannerSize = bannerValues[i]);
            SliderRow("\uE740", Strings.CollapsedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.CollapsedScale, v => settings.CollapsedScale = v, false);
            SliderRow("\uE740", Strings.ExpandedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.ExpandedScale, v => settings.ExpandedScale = v, true);
            SliderRow("\uE745", Strings.DistanceFromEdge, Settings.MinOffsetY, Settings.MaxOffsetY, 1, Pixels, () => settings.OffsetY, v => settings.OffsetY = v, false);
            SliderRow("\uE7C9", Strings.HorizontalPosition, -Settings.MaxOffsetX, Settings.MaxOffsetX, 10, Horizontal, () => settings.OffsetX, v => settings.OffsetX = v, false);
            ActionRow("\uE777", Strings.ResetAppearance, () =>
            {
                settings.ResetAppearance();
                Apply();
                Rebuild();
            });
        }

        void BuildSystem()
        {
            Section(Strings.SectionSystem);
            Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);
            Toggle("\uE895", Strings.MenuAutoUpdate, null, () => settings.AutoUpdate, v => settings.AutoUpdate = v);
            ApiRows();
            UpdateRow();
            LanguageRow();
            ActionRow("\uE8A5", Strings.MenuOpenSettings, () =>
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
        void Row(string icon, string label, string hint, UIElement control, UIElement below = null, UIElement left = null)
        {
            Grid grid = new Grid { Margin = new Thickness(14, 10, 14, 10), MinHeight = 30 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (icon != null && icon.StartsWith("img:"))
            {
                Image mark = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/" + icon.Substring(4) + "-mark.png")),
                    Width = 16,
                    Height = 16,
                    Opacity = 0.72,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
                grid.Children.Add(mark);
            }
            else if (icon != null)
            {
                grid.Children.Add(new TextBlock
                {
                    Text = icon,
                    FontFamily = IconFont,
                    FontSize = 16,
                    Foreground = IconBrush,
                    Width = 30,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            if (left != null)
            {
                Grid.SetColumn(left, icon != null ? 1 : 0);
                Grid.SetColumnSpan(left, icon != null ? 1 : 2);
                grid.Children.Add(left);
            }
            else
            {
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(text, 1);
                text.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
                if (hint != null)
                    text.Children.Add(new TextBlock { Text = hint, FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                grid.Children.Add(text);
            }

            if (control != null)
            {
                Grid.SetColumn(control, 2);
                grid.Children.Add(control);
            }

            StackPanel row = new StackPanel();
            if (card.Children.Count > 0)
                row.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Separator"), Margin = new Thickness(16, 0, 0, 0), SnapsToDevicePixels = true });
            row.Children.Add(grid);
            if (below != null) row.Children.Add(below);
            card.Children.Add(row);
        }

        void Toggle(string icon, string label, string hint, Func<bool> get, Action<bool> set)
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
            Row(icon, label, hint, toggle);
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
        void Choice(string icon, string label, string hint, int[] seconds, bool zeroIsNever, Func<int> get, Action<int> set)
        {
            string[] names = new string[seconds.Length];
            for (int i = 0; i < seconds.Length; i++) names[i] = Strings.Duration(seconds[i], zeroIsNever);
            int current = Array.IndexOf(seconds, get());
            Row(icon, label, hint, null, SegmentBar(names, current, i => set(seconds[i])));
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
            Row("\uE7F4", Strings.MonitorLabel, Strings.HintMonitor, null, SegmentBar(names.ToArray(), Math.Max(0, current), i => settings.Monitor = values[i]));
        }

        void Segmented(string icon, string label, string[] names, Func<int> get, Action<int> set)
        {
            Row(icon, label, null, SegmentBar(names, get(), set, true));
        }

        FrameworkElement SegmentBar(string[] names, int current, Action<int> set, bool inline = false)
        {
            return SlidingBar(names, current, i => { set(i); Apply(); }, Color.FromRgb(0x2C, 0x2C, 0x2E),
                inline ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                inline ? new Thickness(0) : new Thickness(14, 0, 14, 12));
        }

        void SliderRow(string icon, string label, double min, double max, double step, Func<double, string> format,
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
            Row(icon, label, null, value, slider);
        }

        static bool CopyText(string text)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    if (Clipboard.ContainsText() && Clipboard.GetText() == text) return true;
                }
                catch (Exception) { } // The clipboard is held by another program for a moment
                System.Threading.Thread.Sleep(80);
            }
            return false;
        }

        // The local API for scripts and apps: a switch, and while it is on, the token to hand to them.
        void ApiRows()
        {
            Toggle("\uE943", Strings.MenuApi, Strings.HintApi, () => settings.ApiEnabled, v =>
            {
                settings.ApiEnabled = v;
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
            });
            if (!settings.ApiEnabled) return;
            LocalApi api = (Application.Current as App)?.Api;
            if (api == null) return;

            TextBlock note = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(60, 0, 14, 10) };
            Button copy = new Button { Content = Strings.ApiCopyToken, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            copy.Click += (s, e) =>
            {
                string token = api.Token;
                if (string.IsNullOrEmpty(token)) return;
                try { Clipboard.SetText(token); } catch (Exception) { return; }
                note.Text = Strings.ApiTokenCopied;
                note.Visibility = Visibility.Visible;
            };
            Row("\uE8C8", Strings.ApiTokenLabel, Strings.ApiTokenHint, null, null, copy);
            Button renew = new Button { Content = Strings.ApiNewToken, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            renew.Click += (s, e) =>
            {
                string fresh = api.RegenerateToken();
                // An unpacked extension keeps its own copy of the token: refresh it so it keeps working
                try { if (System.IO.Directory.Exists(ExtensionInstaller.Folder)) ExtensionInstaller.Extract(fresh, api.Port); } catch (Exception) { }
                note.Text = Strings.ApiNewTokenDone;
                note.Visibility = Visibility.Visible;
            };
            Row("\uE72C", null, null, null, null, renew);
            Button guide = new Button { Content = Strings.ApiGuide, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            guide.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/blob/main/docs/API.md") { UseShellExecute = true }); } catch (Exception) { } };
            Row("\uE897", null, null, null, null, guide);

            StackPanel browsers = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (bool edge in new[] { false, true })
            {
                bool isEdge = edge;
                Button install = new Button { Content = isEdge ? "Edge" : "Chrome", Style = (Style)FindResource("LinkButton"), Margin = new Thickness(0, 0, 14, 0) };
                install.Click += (s, e) =>
                {
                    try
                    {
                        string token = api.Token ?? api.RegenerateToken();
                        string folder = ExtensionInstaller.Extract(token, api.Port > 0 ? api.Port : settings.ApiPort);
                        // Another program can hold the clipboard for a moment, so it is retried and checked; when it
                        // still fails, the folder opens in Explorer so its address can be copied from there.
                        bool copied = CopyText(folder);
                        if (!copied)
                        {
                            try { System.Diagnostics.Process.Start("explorer.exe", (char)34 + folder + (char)34); } catch (Exception) { }
                        }
                        bool opened = ExtensionInstaller.OpenExtensionsPage(isEdge);
                        note.Text = !opened ? Strings.ExtNoBrowser + " " + folder : copied ? Strings.ExtSteps(folder) : Strings.ExtStepsNoCopy(folder);
                    }
                    catch (Exception)
                    {
                        note.Text = Strings.ExtFailed;
                    }
                    note.Visibility = Visibility.Visible;
                };
                browsers.Children.Add(install);
            }
            Row("\uE896", Strings.ExtInstall, Strings.ExtHint, null, null, browsers);
            if (api.LastStartFailed)
                ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(new TextBlock { Text = Strings.ApiPortBusy, FontSize = 12, Foreground = Brushes.IndianRed, Margin = new Thickness(60, 0, 14, 10) });
            ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(note);
        }

        // Shows the current shortcut; a click waits for the next key combination and saves it.
        void HotkeyRow()
        {
            Button button = new Button { Content = settings.HotkeyCombo, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Focusable = true };
            bool capturing = false;
            button.Click += (s, e) =>
            {
                capturing = true;
                button.Content = Strings.HotkeyPress;
                button.Focus();
            };
            button.LostKeyboardFocus += (s, e) =>
            {
                if (!capturing) return;
                capturing = false;
                button.Content = settings.HotkeyCombo;
            };
            button.PreviewKeyDown += (s, e) =>
            {
                if (!capturing) return;
                e.Handled = true;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.Escape)
                {
                    capturing = false;
                    button.Content = settings.HotkeyCombo;
                    return;
                }
                string combo = HotkeyService.Format(Keyboard.Modifiers, key);
                if (combo == null) return; // Only a modifier so far, or no modifier at all
                capturing = false;
                settings.HotkeyCombo = combo;
                Apply();
                Rebuild();
            };
            Row("\uE765", null, null, null, null, button);
            if (App.HotkeyFailed)
            {
                TextBlock warning = new TextBlock { Text = Strings.HotkeyTaken, FontSize = 12, Foreground = Brushes.IndianRed, Margin = new Thickness(60, 0, 14, 10), TextWrapping = TextWrapping.Wrap };
                ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(warning);
            }
        }

        void ActionRow(string icon, string label, Action action)
        {
            Button button = new Button { Content = label, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            button.Click += (s, e) => action();
            Row(icon, null, null, null, null, button);
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
