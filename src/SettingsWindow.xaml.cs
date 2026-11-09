using System;
using System.Linq;
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

        bool welcome;     // The first-run steps instead of the settings
        int welcomeStep;

        internal SettingsWindow(Settings settings, Action changed, UpdateFlow updates, Action peekExpanded, bool welcome = false)
        {
            this.welcome = welcome;
            this.settings = settings;
            this.changed = changed;
            this.updates = updates;
            this.peekExpanded = peekExpanded;
            InitializeComponent();
            SettingsTheme.Choose(settings.Theme);
            ApplyTheme();
            SearchHint.Text = Strings.SettingsSearch;
            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            searchTimer.Tick += (sender, args) => { searchTimer.Stop(); RunSearch(); };
            SearchBox.TextChanged += (sender, args) =>
            {
                SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                SearchClear.Visibility = SearchBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                // Typing is never slowed down: the page changes once the typing pauses
                searchTimer.Stop();
                searchTimer.Start();
            };
            SearchClear.MouseLeftButtonUp += (sender, args) => { SearchBox.Clear(); SearchBox.Focus(); };

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
                int round = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
                ApplyFrameTheme();
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
            CloseButton.MouseEnter += (s, e) => CloseButton.Background = new SolidColorBrush(SettingsTheme.Hover);
            CloseButton.MouseLeave += (s, e) => CloseButton.Background = Brushes.Transparent;
            CloseButton.MouseLeftButtonUp += (s, e) => Close();
            PlaceNearTray();
            BuildTabs();
            Build(tab);
            Loaded += (s, e) => FitHeight(false); // Before the first frame, so it opens at the right height
        }

        static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static Brush IconBrush = new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xBC));

        // Paints the window and its shared brushes with the chosen colors (they are shared, so every user of one follows).
        void ApplyTheme()
        {
            Background = new SolidColorBrush(SettingsTheme.WindowBack);
            Foreground = new SolidColorBrush(SettingsTheme.Text);
            SetBrush("Card", SettingsTheme.Card);
            SetBrush("Separator", SettingsTheme.Separator);
            SetBrush("Secondary", SettingsTheme.Secondary);
            SetBrush("Accent", SettingsTheme.Accent);
            SetBrush("On", SettingsTheme.On);
            SetBrush("Off", SettingsTheme.SwitchOff);
            SetBrush("ChipHover", SettingsTheme.ChipHover);
            SetBrush("SliderTrack", SettingsTheme.SliderTrack);
            SetBrush("Thumb", SettingsTheme.Thumb);
            IconBrush = new SolidColorBrush(SettingsTheme.Icon);
        }

        void SetBrush(string key, Color color)
        {
            SolidColorBrush brush = FindResource(key) as SolidColorBrush;
            if (brush != null && !brush.IsFrozen) brush.Color = color;
            else Resources[key] = new SolidColorBrush(color);
        }

        // The title bar is drawn by Windows, so its dark mode and outline follow the theme too.
        void ApplyFrameTheme()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            int dark = SettingsTheme.Light ? 0 : 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int border = SettingsTheme.BorderColorRef; // A faint outline, since there is no title bar to frame the window
            DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
        }

        // A new theme choice: everything that was built with the old colors is built again.
        void ChangeTheme()
        {
            SettingsTheme.Choose(settings.Theme);
            ApplyTheme();
            ApplyFrameTheme();
            BuildTabs();
            Rebuild();
        }

        // ---------- Language ----------

        void LanguageRow()
        {
            string[] values = { "Auto", "en", "pt", "es", "ru" };
            string[] names = { Strings.LanguageAuto, "English", "Português", "Español", "Русский" };
            int current = Math.Max(0, Array.FindIndex(values, v => string.Equals(v, settings.Language, StringComparison.OrdinalIgnoreCase)));
            StackPanel flags = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                Border chip = new Border
                {
                    Width = 34, Height = 28, CornerRadius = new CornerRadius(8), Margin = new Thickness(2, 0, 0, 0),
                    Background = index == current ? new SolidColorBrush(SettingsTheme.Pill) : Brushes.Transparent,
                    BorderBrush = index == current ? new SolidColorBrush(SettingsTheme.Accent) : Brushes.Transparent, BorderThickness = new Thickness(1.2),
                    Cursor = System.Windows.Input.Cursors.Hand, ToolTip = names[i],
                    Child = Flag(values[i]),
                };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    settings.Language = values[index];
                    Strings.Use(settings.Language);
                    Apply();
                    Dispatcher.BeginInvoke(new Action(RefreshTexts)); // After this click has finished
                };
                flags.Children.Add(chip);
            }
            Row("\uE774", Strings.LanguageLabel, null, flags);
        }

        // A small flag drawn in 20 x 14 (the system's fonts have no flag emoji): a globe for "automatic"
        FrameworkElement Flag(string code)
        {
            if (code == "Auto")
                return new TextBlock { Text = "\uE774", FontFamily = IconFont, FontSize = 15, Foreground = IconBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Canvas canvas = new Canvas { Width = 20, Height = 14 };
            Func<string, Brush> brush = hex => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            Action<double, double, double, double, string> rect = (x, y, w, h, hex) =>
            {
                System.Windows.Shapes.Rectangle r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = brush(hex) };
                Canvas.SetLeft(r, x); Canvas.SetTop(r, y); canvas.Children.Add(r);
            };
            Action<double, double, double, double, string, double> line = (x1, y1, x2, y2, hex, width) =>
                canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush(hex), StrokeThickness = width });
            if (code == "pt")
            {
                rect(0, 0, 20, 14, "#009B3A");
                canvas.Children.Add(new System.Windows.Shapes.Polygon { Points = new PointCollection { new Point(10, 1.6), new Point(18.4, 7), new Point(10, 12.4), new Point(1.6, 7) }, Fill = brush("#FEDF00") });
                System.Windows.Shapes.Ellipse disc = new System.Windows.Shapes.Ellipse { Width = 6.6, Height = 6.6, Fill = brush("#002776") };
                Canvas.SetLeft(disc, 6.7); Canvas.SetTop(disc, 3.7); canvas.Children.Add(disc);
            }
            else if (code == "es")
            {
                rect(0, 0, 20, 14, "#AA151B");
                rect(0, 3.5, 20, 7, "#F1BF00");
            }
            else if (code == "ru")
            {
                rect(0, 0, 20, 4.7, "#FFFFFF");
                rect(0, 4.7, 20, 4.7, "#0039A6");
                rect(0, 9.4, 20, 4.6, "#D52B1E");
            }
            else
            {
                rect(0, 0, 20, 14, "#012169");
                line(0, 0, 20, 14, "#FFFFFF", 3); line(20, 0, 0, 14, "#FFFFFF", 3);
                line(0, 0, 20, 14, "#C8102E", 1.1); line(20, 0, 0, 14, "#C8102E", 1.1);
                rect(0, 4.6, 20, 4.8, "#FFFFFF"); rect(8.1, 0, 3.8, 14, "#FFFFFF");
                rect(0, 5.7, 20, 2.6, "#C8102E"); rect(8.9, 0, 2.2, 14, "#C8102E");
            }
            return new Border { Width = 20, Height = 14, CornerRadius = new CornerRadius(2.5), ClipToBounds = true, Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        // Titles, tabs and rows again, in the language just chosen.
        void RefreshTexts()
        {
            Title = Installer.Name + " – " + Strings.SettingsTitle;
            Heading.Text = Strings.SettingsTitle;
            SearchHint.Text = Strings.SettingsSearch;
            BuildTabs();
            Rebuild();
        }

        // ---------- Updates ----------

        TextBlock updateGlyph, updateText;
        RotateTransform spin;

        void UpdateRow()
        {
            Row(null, null, null, null, null, MakeUpdateLine());
        }

        FrameworkElement MakeUpdateLine()
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
            if (updates != null)
            {
                updates.Changed -= OnUpdateChanged;
                updates.Changed += OnUpdateChanged;
            }
            RefreshUpdateRow();
            return line;
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
            if (welcome) return;
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
            fitTop = fitHeight = double.NaN;
            // Start the slide-up from below before the window is first shown, so it does not appear and then jump
            if (SystemParameters.ClientAreaAnimation) Top = restingTop + 22;
        }

        // The version on the left and the project's page on the right, always at the bottom
        void BuildFooter()
        {
            // Version on the left, the project's page on the right
            Grid footer = new Grid { Margin = new Thickness(0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(new TextBlock { Text = Installer.Name + " v" + Installer.Version, Foreground = (Brush)FindResource("Secondary"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            Button repo = new Button { Style = (Style)FindResource("LinkButton"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            repo.Content = GitHubLabel(repo, "@leotrvs/windows-island");
            repo.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island") { UseShellExecute = true }); } catch (Exception) { } };
            Grid.SetColumn(repo, 1);
            footer.Children.Add(repo);
            FooterHost.Child = footer;
        }

        void BuildTabs()
        {
            BuildFooter();
            if (welcome)
            {
                ModeBar.Visibility = TabBar.Visibility = SearchHost.Visibility = FooterHost.Visibility = Visibility.Collapsed;
                return;
            }
            bool essentials = !string.Equals(settings.SettingsMode, "Advanced", StringComparison.OrdinalIgnoreCase);
            ModeBar.Content = SlidingBar(new[] { Strings.ModeEssentials, Strings.ModeAdvanced }, essentials ? 0 : 1, index =>
            {
                settings.SettingsMode = index == 0 ? "Essentials" : "Advanced";
                Apply();
                BuildTabs();
                Rebuild();
            }, SettingsTheme.TabTrack, HorizontalAlignment.Left, new Thickness(0));
            TabBar.Visibility = essentials ? Visibility.Collapsed : Visibility.Visible; // The tabs belong to the advanced settings
            string[] names = { Strings.SectionActivities, Strings.SectionBehavior, Strings.SectionAppearance, Strings.SectionSystem };
            TabBar.Content = SlidingBar(names, tab, index => { tab = index; Rebuild(); }, SettingsTheme.TabTrack, HorizontalAlignment.Left, new Thickness(0));
        }

        // Choices in a rounded track, with a pill that slides to the one picked.
        FrameworkElement SlidingBar(string[] names, int current, Action<int> pick, Color track, HorizontalAlignment align, Thickness margin)
        {
            Grid host = new Grid();
            Border pill = new Border
            {
                Background = new SolidColorBrush(SettingsTheme.Pill),
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
                    Child = new TextBlock { Text = names[i], FontSize = 12, Foreground = new SolidColorBrush(SettingsTheme.Text) },
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
            if (welcome) { BuildWelcome(); return; }
            if (filter.Length == 0 && !all) AddWhatsNew();
            if (!all && !string.Equals(settings.SettingsMode, "Advanced", StringComparison.OrdinalIgnoreCase)) { BuildEssentials(); return; }
            if (all && filter.Length == 0)
                Content.Children.Add(new TextBlock { Text = Strings.SettingsTitle, FontSize = 30, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 14) });
            if (all || only == 0) BuildActivities();
            if (all || only == 1) BuildBehavior();
            if (all || only == 2) BuildAppearance();
            if (all || only == 3) BuildSystem();
        }

        // The news of the latest release, on top of Settings until it is dismissed (the cross)
        void AddWhatsNew()
        {
            if (string.Equals(settings.WhatsNewSeen, WhatsNew.Key, StringComparison.Ordinal)) return;
            Grid head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = WhatsNew.Title, FontWeight = FontWeights.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            TextBlock close = new TextBlock
            {
                Text = "\uE711", FontFamily = IconFont, FontSize = 11, Padding = new Thickness(8, 4, 2, 4), Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent, ToolTip = Strings.WhatsNewGotIt, VerticalAlignment = VerticalAlignment.Center,
            };
            close.MouseLeftButtonUp += (s, e) =>
            {
                settings.WhatsNewSeen = WhatsNew.Key;
                Apply();
                Rebuild();
            };
            Grid.SetColumn(close, 1);
            head.Children.Add(close);

            StackPanel body = new StackPanel { Margin = new Thickness(16, 12, 12, 14) };
            body.Children.Add(head);
            foreach (string[] item in WhatsNew.Items)
            {
                body.Children.Add(new TextBlock { Text = item[0], FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });
                body.Children.Add(new TextBlock { Text = item[1], FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Secondary"), Margin = new Thickness(0, 1, 18, 0) });
            }
            Button all = new Button { Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
            all.Content = GitHubLabel(all, Strings.WhatsNewAll);
            all.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WhatsNew.ChangelogUrl(settings.Language)) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            };
            body.Children.Add(all);
            Content.Children.Add(new Border
            {
                Child = body,
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(SettingsTheme.Accent),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14),
            });
        }

        // A link that leaves the app: the text and a small arrow pointing up and to the right
        FrameworkElement ExternalLabel(string text)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = "\u2197", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 12, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        // The same, with the GitHub mark in front, in the color of its button
        FrameworkElement GitHubLabel(Button owner, string text)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            System.Windows.Shapes.Path mark = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"),
                Width = 14, Height = 14, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            mark.SetBinding(System.Windows.Shapes.Shape.FillProperty, new System.Windows.Data.Binding("Foreground") { Source = owner });
            panel.Children.Add(mark);
            panel.Children.Add(ExternalLabel(text));
            return panel;
        }

        // A few switches, for someone who just wants it to work; "More settings" opens all of them.
        void BuildEssentials()
        {
            Section(Strings.SectionEssentials);
            Toggle("\uE740", Strings.MenuAlwaysThin, Strings.HintAlwaysThin, () => settings.AlwaysThin, v => settings.AlwaysThin = v);
            Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle("\uE7E7", Strings.MenuNotifications, null, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
            Toggle("\uE720", Strings.MenuPrivacyDots, null, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
            Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);

            // Not part of the list: a row of its own, split in two (more settings | updates)
            Grid split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel more = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 14, 0, 14) };
            more.Children.Add(new TextBlock { Text = "\uE713", FontFamily = IconFont, FontSize = 16, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            more.Children.Add(new TextBlock { Text = Strings.MoreSettings, Foreground = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center });
            more.MouseLeftButtonUp += (s, e) =>
            {
                settings.SettingsMode = "Advanced";
                Apply();
                BuildTabs();
                Rebuild();
            };
            split.Children.Add(more);
            Border divider = new Border { Background = (Brush)FindResource("Separator"), Margin = new Thickness(0, 8, 0, 8) };
            Grid.SetColumn(divider, 1);
            split.Children.Add(divider);
            FrameworkElement update = MakeUpdateLine();
            update.HorizontalAlignment = HorizontalAlignment.Center;
            update.VerticalAlignment = VerticalAlignment.Center;
            update.Margin = new Thickness(0, 14, 0, 14);
            Grid.SetColumn(update, 2);
            split.Children.Add(update);
            Content.Children.Add(new Border { Child = split, Background = (Brush)FindResource("Card"), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 12, 0, 0) });
        }

        // ---- The first-run welcome: how it looks, what it shows, done ----

        void BuildWelcome()
        {
            Heading.Text = Strings.WelcomeTitle;
            string[] titles = { Strings.WelcomeLookTitle, Strings.WelcomeShowTitle, Strings.WelcomeDoneTitle };
            StackPanel steps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 10) };
            for (int i = 0; i < 3; i++)
                steps.Children.Add(new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 0, 5, 0), Background = new SolidColorBrush(i <= welcomeStep ? SettingsTheme.Accent : SettingsTheme.SwitchOff) });
            Content.Children.Add(steps);
            Content.Children.Add(new TextBlock { Text = titles[welcomeStep], FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
            Content.Children.Add(new TextBlock { Text = welcomeStep == 2 ? Strings.WelcomeDoneText : Strings.WelcomeChangeLater, Foreground = (Brush)FindResource("Secondary"), FontSize = 13, Margin = new Thickness(4, 0, 0, 14), TextWrapping = TextWrapping.Wrap });

            if (welcomeStep == 0)
            {
                // Three looks; the one picked has a blue edge
                int pick = settings.AlwaysThin ? 1 : string.Equals(settings.IdleStyle, "Pill", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
                Grid cards = new Grid();
                for (int i = 0; i < 3; i++) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                string[] looks = { Strings.LookIdleLine, Strings.LookAlwaysLine, Strings.LookPill };
                string[] hints = { Strings.LookIdleLineHint, Strings.LookAlwaysLineHint, Strings.LookPillHint };
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    Border preview = new Border { Height = 46, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(0x55, 0x80, 0x80, 0x80)), Margin = new Thickness(0, 0, 0, 8) }; // Grey, so the black shapes show on it
                    Border shape = new Border { Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(SettingsTheme.Separator), BorderThickness = new Thickness(0.5) };
                    if (i == 2) { shape.Width = 62; shape.Height = 18; shape.CornerRadius = new CornerRadius(9); }
                    else { shape.Width = 58; shape.Height = 7; shape.CornerRadius = new CornerRadius(3.5); }
                    preview.Child = shape;
                    StackPanel body = new StackPanel { Margin = new Thickness(8, 8, 8, 10) };
                    body.Children.Add(preview);
                    body.Children.Add(new TextBlock { Text = looks[i], FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    body.Children.Add(new TextBlock { Text = hints[i], FontSize = 11, Foreground = (Brush)FindResource("Secondary"), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    Border card = new Border
                    {
                        Child = body,
                        Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 0),
                        CornerRadius = new CornerRadius(12),
                        Background = (Brush)FindResource("Card"),
                        BorderThickness = new Thickness(1.5),
                        BorderBrush = i == pick ? new SolidColorBrush(SettingsTheme.Accent) : new SolidColorBrush(SettingsTheme.Separator),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        settings.AlwaysThin = index == 1;
                        settings.IdleStyle = index == 2 ? "Pill" : "Line";
                        Apply();
                        Rebuild();
                    };
                    Grid.SetColumn(card, i);
                    cards.Children.Add(card);
                }
                Content.Children.Add(cards);
            }
            else if (welcomeStep == 1)
            {
                Section(Strings.SectionActivities);
                Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
                Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
                Toggle("\uE7E7", Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
                Toggle("\uE720", Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
                Toggle("\uE72E", Strings.MenuPermissionPrompts, Strings.HintPermissionPrompts, () => settings.PermissionPrompts, SetPermissionPrompts);
                Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);
            }

            // The buttons: the blue one goes on, the plain ones go back or skip
            StackPanel buttons = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            Border next = new Border
            {
                Background = new SolidColorBrush(SettingsTheme.Accent),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0, 9, 0, 9),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = welcomeStep == 2 ? Strings.WelcomeFinish : Strings.WelcomeContinue, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center },
            };
            next.MouseLeftButtonUp += (s, e) =>
            {
                if (welcomeStep < 2) { welcomeStep++; Rebuild(); }
                else FinishWelcome();
            };
            buttons.Children.Add(next);
            Border plain = new Border
            {
                Padding = new Thickness(0, 10, 0, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new TextBlock { Text = welcomeStep == 0 ? Strings.WelcomeSkip : Strings.WelcomeBack, Foreground = (Brush)FindResource("Secondary"), TextAlignment = TextAlignment.Center, FontSize = 12.5 },
            };
            plain.MouseLeftButtonUp += (s, e) =>
            {
                if (welcomeStep == 0) FinishWelcome();
                else { welcomeStep--; Rebuild(); }
            };
            buttons.Children.Add(plain);
            Content.Children.Add(buttons);
        }

        // The whole window as it is, for checking the layout (not part of the README images)
        internal void SaveWindowShot(string file, string mode, int tabIndex)
        {
            settings.SettingsMode = mode;
            tab = tabIndex;
            Show();
            BuildTabs();
            Rebuild();
            UpdateLayout();
            FitHeight(false);
            UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ActualWidth * 2), (int)Math.Ceiling(ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render((Visual)VisualTreeHelper.GetChild(this, 0));
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
            Close();
        }

        internal void WelcomeStep(int step) { welcomeStep = step; }

        void FinishWelcome()
        {
            settings.OnboardingDone = true;
            settings.Save();
            changed();
            Close();
        }

        void BuildActivities()
        {
            Section(Strings.SectionActivities);
            Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle("\uEA8F", Strings.MenuBanners, null, () => settings.ClaudeBanners, v => settings.ClaudeBanners = v);
            Group("\uE9D9", Strings.GroupLimits, () =>
            {
                Toggle("\uE9D9", Strings.MenuLimitAlerts, Strings.HintLimitAlerts, () => settings.ShowLimitAlerts, v => settings.ShowLimitAlerts = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(75), null, () => settings.LimitAt75, v => settings.LimitAt75 = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(90), null, () => settings.LimitAt90, v => settings.LimitAt90 = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(100), null, () => settings.LimitAt100, v => settings.LimitAt100 = v);
                Toggle("\uE777", Strings.MenuLimitRenewed, null, () => settings.LimitRenewed, v => settings.LimitRenewed = v);
            });
            Toggle("\uE72E", Strings.MenuPermissionPrompts, Strings.HintPermissionPrompts, () => settings.PermissionPrompts, SetPermissionPrompts);
            Toggle("img:codex", Strings.MenuCodex, null, () => settings.ShowCodex, v => settings.ShowCodex = v);
            Toggle("\uEA8F", Strings.MenuCodexBanners, null, () => settings.CodexBanners, v => settings.CodexBanners = v);
            Group("\uE720", Strings.GroupPrivacy, () =>
            {
                Toggle("\uE720", Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
                Toggle("\uE7F4", Strings.MenuScreenShare, Strings.HintScreenShare, () => settings.ShowScreenShare, v => settings.ShowScreenShare = v);
                // Apps seen using the microphone, camera or screen: one can be turned off here, like a call app that keeps the microphone busy
                if (settings.PrivacySeen.Count > 0 || settings.PrivacyIgnored.Count > 0) HiddenAppsRow();
                int[] nameSeconds = { 0, 1, 2, 3 };
                string[] nameLabels = { Strings.Off, Strings.Duration(1, false), Strings.Duration(2, false), Strings.Duration(3, false) };
                Row("\uE77B", Strings.MenuPrivacyNames, Strings.HintPrivacyNames, null,
                    SegmentBar(nameLabels, Math.Max(0, Array.IndexOf(nameSeconds, settings.PrivacyNameSeconds)), i => settings.PrivacyNameSeconds = nameSeconds[i]));
            });
            Group("\uE8F1", Strings.GroupMore, () =>
            {
            Toggle("\uE9D9", Strings.MenuFootball, Strings.HintFootball, () => settings.ShowFootball, v => { settings.ShowFootball = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
            if (settings.ShowFootball)
            {
                InputRow(Strings.FootballTeamLabel, () => settings.FootballTeam, v => settings.FootballTeam = (v ?? "").Trim(), false);
                InputRow(Strings.FootballLeaguesLabel, () => settings.FootballLeagues, v => settings.FootballLeagues = (v ?? "").Trim(), false);
            }
            Toggle("\uE7C8", Strings.MenuObs, Strings.HintObs, () => settings.ShowObs, v => settings.ShowObs = v);
            Toggle("\uE895", Strings.MenuWindowsUpdate, Strings.HintWindowsUpdate, () => settings.ShowWindowsUpdate, v => settings.ShowWindowsUpdate = v);
            Toggle("\uE896", Strings.MenuSteam, Strings.HintSteam, () => settings.ShowSteamDownloads, v => settings.ShowSteamDownloads = v);
            });
            Group("\uE720", Strings.GroupDiscord, DiscordRows);
            Group("\uE83E", Strings.GroupBattery, () =>
            {
                Toggle("\uE83E", Strings.MenuBatteryActivity, Strings.HintBatteryActivity, () => settings.BatteryActivity, v => settings.BatteryActivity = v);
                Toggle("\uE83F", Strings.MenuBatteryAlerts, Strings.HintBatteryAlerts, () => settings.BatteryAlerts, v => settings.BatteryAlerts = v);
            });
            Group("\uEA8F", Strings.GroupAlerts, () =>
            {
                Toggle("\uE702", Strings.MenuDevices, Strings.HintDevices, () => settings.ShowDeviceAlerts, v => settings.ShowDeviceAlerts = v);
                Toggle("\uE701", Strings.MenuNetworkAlerts, Strings.HintNetworkAlerts, () => settings.ShowNetworkAlerts, v => settings.ShowNetworkAlerts = v);
                Toggle("\uE708", Strings.MenuFocus, Strings.HintFocus, () => settings.ShowFocus, v => settings.ShowFocus = v);
                Toggle("\uE722", Strings.MenuScreenshot, Strings.HintScreenshot, () => settings.ShowScreenshotAlert, v => settings.ShowScreenshotAlert = v);
                Toggle("\uE7E7", Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
            });
            Group("\uE767", Strings.GroupLevels, () =>
            {
                Toggle("\uE767", Strings.MenuLevels, Strings.HintLevels, () => settings.ShowLevels, v => settings.ShowLevels = v);
                Toggle("\uE74F", Strings.MenuHideOsd, Strings.HintHideOsd, () => settings.HideWindowsOsd, v => settings.HideWindowsOsd = v);
            });
        }

        // One row for every app whose dot is hidden: hidden ones are chips with an x, apps seen lately are chips with a +.
        void HiddenAppsRow()
        {
            WrapPanel chips = new WrapPanel { Margin = new Thickness(44, 0, 14, 10) };
            foreach (string name in settings.PrivacyIgnored.ToArray())
            {
                string app = name;
                chips.Children.Add(Chip(app + "  \u2715", true, () => { settings.PrivacyIgnored.RemoveAll(n => string.Equals(n, app, StringComparison.OrdinalIgnoreCase)); Apply(); Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
            }
            foreach (string name in settings.PrivacySeen.ToArray())
            {
                string app = name;
                if (settings.PrivacyIgnored.Contains(app, StringComparer.OrdinalIgnoreCase)) continue;
                chips.Children.Add(Chip("+  " + app, false, () => { settings.PrivacyIgnored.Add(app); Apply(); Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
            }
            Row("\uE77B", Strings.MenuHiddenApps, Strings.HintHiddenApps, null, chips);
        }

        Border Chip(string text, bool solid, Action click)
        {
            Border chip = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(solid ? Color.FromArgb(0x40, 0x0A, 0x84, 0xFF) : Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
                Child = new TextBlock { Text = text, FontSize = 12.5, Foreground = solid ? (Brush)IconBrush : (Brush)FindResource("Secondary") },
            };
            chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
            return chip;
        }

        // The Discord rows: the switch, the app's id and secret, the state of the connection and a guide.
        void DiscordRows()
        {
            DiscordService discord = (Application.Current as App)?.Discord;
            Toggle("\uE720", Strings.MenuDiscord, Strings.HintDiscord, () => settings.DiscordEnabled, v => settings.DiscordEnabled = v);
            InputRow(Strings.DiscordClientIdLabel, () => settings.DiscordClientId, v => settings.DiscordClientId = (v ?? "").Trim(), false);
            InputRow(Strings.DiscordClientSecretLabel, () => DiscordSecrets.Load().ClientSecret, v =>
            {
                DiscordSecrets secrets = DiscordSecrets.Load();
                secrets.ClientSecret = (v ?? "").Trim();
                secrets.AccessToken = secrets.RefreshToken = ""; // A new secret means a new authorization
                secrets.Save();
                if (settings.DiscordEnabled && discord != null) discord.Restart(settings.DiscordClientId);
            }, true);

            TextBlock status = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(60, 0, 14, 10) };
            Action show = () => status.Text = discord == null || !settings.DiscordEnabled ? "" : Strings.DiscordStatus(discord.Status, discord.Detail);
            show();
            if (discord != null)
            {
                Action changed = () => Dispatcher.BeginInvoke(show);
                discord.Changed += changed;
                Closed += (s, e) => discord.Changed -= changed;
            }
            ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(status);

            Button guide = new Button { Content = Strings.DiscordGuide, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            guide.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/blob/main/docs/DISCORD.md") { UseShellExecute = true }); } catch (Exception) { } };
            Row("\uE897", null, null, null, null, guide);
            ActionRow("\uE74D", Strings.DiscordForget, () =>
            {
                // The secret and the tokens are deleted, the id cleared and the connection closed
                DiscordSecrets.Delete();
                settings.DiscordClientId = "";
                settings.DiscordEnabled = false;
                Apply();
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
            }, true);
        }

        // A text box (or a password box) under its label; the value is saved when the box loses the focus.
        void InputRow(string label, Func<string> get, Action<string> set, bool secret)
        {
            Control box;
            string shown = get() ?? "";
            if (secret)
            {
                PasswordBox password = new PasswordBox { Password = shown.Length == 0 ? "" : new string('\u2022', 12) };
                bool touched = false;
                password.PasswordChanged += (s, e) => touched = true;
                password.GotFocus += (s, e) => { if (!touched && shown.Length > 0) password.Clear(); }; // A saved secret is not shown: typing replaces it
                password.LostFocus += (s, e) =>
                {
                    string mask = new string('\u2022', 12);
                    if (touched && password.Password.Length > 0 && password.Password != mask) { set(password.Password); Apply(); }
                    password.Password = (get() ?? "").Length > 0 ? mask : "";
                    touched = false;
                };
                box = password;
            }
            else
            {
                TextBox text = new TextBox { Text = shown };
                text.LostFocus += (s, e) => { set(text.Text); Apply(); };
                text.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) { set(text.Text); Apply(); } };
                box = text;
            }
            box.Background = Brushes.Transparent;
            box.Foreground = new SolidColorBrush(SettingsTheme.Text);
            box.BorderThickness = new Thickness(0);
            box.Padding = new Thickness(10, 7, 10, 7);
            box.FontSize = 13;
            if (box is TextBox) ((TextBox)box).CaretBrush = new SolidColorBrush(SettingsTheme.Text);
            if (box is PasswordBox) ((PasswordBox)box).CaretBrush = new SolidColorBrush(SettingsTheme.Text);
            Border frame = new Border { CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(SettingsTheme.SegTrack), Child = box, Margin = new Thickness(14, 0, 14, 12) };
            Row(null, label, null, null, frame);
        }

        // Adds or removes Claude Code's hook that sends permission requests to the island. The hook needs the local API.
        void SetPermissionPrompts(bool on)
        {
            string error;
            if (!ClaudeHook.Apply(on, out error))
            {
                settings.PermissionPrompts = false;
                MessageBox.Show(Strings.PermissionHookFailed + (error == null ? "" : "\n" + error), Installer.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                return;
            }
            settings.PermissionPrompts = on;
            if (on) settings.ApiEnabled = true;
            Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
        }

        // How the island behaves: when it opens, when it steps away, the keyboard shortcut, the times and the pages.
        void BuildBehavior()
        {
            Section(Strings.SectionBehavior);
            Toggle("\uE7C9", Strings.MenuHover, null, () => settings.ExpandOnHover, v => settings.ExpandOnHover = v);
            Toggle("\uE740", Strings.MenuFullscreen, null, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v);
            Toggle("\uE7B3", Strings.MenuEmptyIsland, Strings.HintEmptyIsland, () => settings.EmptyIsland, v => settings.EmptyIsland = v);
            Toggle("\uE7F8", Strings.MenuIdlePill, null, () => settings.ShowIdlePill, v => settings.ShowIdlePill = v);
            Toggle("\uE765", Strings.MenuHotkey, Strings.HintHotkey, () => settings.HotkeyEnabled, v => { settings.HotkeyEnabled = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
            if (settings.HotkeyEnabled) HotkeyRow();
            Group("\uE916", Strings.GroupTimes, () =>
            {
                Choice("\uE916", Strings.MenuAutoHide, Strings.HintAutoHide, new[] { 0, 5, 10, 30, 60, 300 }, true, () => settings.AutoHideSeconds, v => settings.AutoHideSeconds = v);
                Choice("\uE81C", Strings.MenuAlertDuration, null, new[] { 3, 5, 8, 12, 20 }, false, () => settings.AlertSeconds, v => settings.AlertSeconds = v);
                Choice("\uE81C", Strings.MenuNotificationDuration, null, new[] { 2, 3, 5, 8, 12 }, false, () => settings.NotificationSeconds, v => settings.NotificationSeconds = v);
                Choice("\uE81C", Strings.MenuLimitDuration, null, new[] { 3, 4, 6, 8, 12 }, false, () => settings.LimitAlertSeconds, v => settings.LimitAlertSeconds = v);
                Choice("\uE769", Strings.MenuPausedMusic, null, new[] { 0, 2, 5, 15, 30, 60 }, false, () => settings.PausedMusicSeconds, v => settings.PausedMusicSeconds = v);
            });

            Group("\uE916", Strings.GroupPomodoro, () =>
            {
                MinutesRow("\uE916", Strings.PomodoroFocusLabel, new[] { 15, 20, 25, 30, 45, 50 }, () => settings.PomodoroFocus, v => settings.PomodoroFocus = v);
                MinutesRow("\uE916", Strings.PomodoroBreakLabel, new[] { 3, 5, 10 }, () => settings.PomodoroBreak, v => settings.PomodoroBreak = v);
                MinutesRow("\uE916", Strings.PomodoroLongLabel, new[] { 10, 15, 20, 30 }, () => settings.PomodoroLong, v => settings.PomodoroLong = v);
            });

            Section(Strings.SectionPages);
            System.Collections.Generic.List<string> pageOrder = settings.PageOrderList();
            for (int i = 0; i < pageOrder.Count; i++)
            {
                string name = pageOrder[i];
                string hint = i == 0 ? Strings.HintPages : null; // The arrows change the order the pages are shown in
                UIElement arrows = PageArrows(name, i, pageOrder.Count);
                if (name == "Music") Toggle("\uE8D6", Strings.PageMusicLabel, hint, () => settings.PageMusic, v => settings.PageMusic = v, arrows);
                else if (name == "Claude") Toggle("img:claude", Strings.PageClaudeLabel, hint, () => settings.PageClaude, v => settings.PageClaude = v, arrows);
                else if (name == "System") Toggle("\uE9D9", Strings.PageSystemLabel, hint, () => settings.PageSystem, v => settings.PageSystem = v, arrows);
                else if (name == "Quick") Toggle("\uE713", Strings.PageQuickLabel, hint, () => settings.PageQuick, v => settings.PageQuick = v, arrows);
                else Toggle("\uE916", Strings.PageTimerLabel, hint, () => settings.PageTimer, v => settings.PageTimer = v, arrows);
            }
        }

        void MinutesRow(string icon, string label, int[] values, Func<int> get, Action<int> set)
        {
            string[] names = values.Select(v => v + " min").ToArray();
            Row(icon, label, null, null, SegmentBar(names, Math.Max(0, Array.IndexOf(values, get())), i => set(values[i])));
        }

        // How the island looks: the theme, where it sits, the thin line when it is collapsed, the system monitor and the sizes.
        void BuildAppearance()
        {
            Section(Strings.SectionAppearance);
            string[] themeValues = { "Auto", "Dark", "Light" };
            Segmented("\uE790", Strings.MenuTheme, new[] { Strings.ThemeAuto, Strings.ThemeDark, Strings.ThemeLight },
                () => Math.Max(0, Array.FindIndex(themeValues, v => string.Equals(v, settings.Theme, StringComparison.OrdinalIgnoreCase))),
                i => { settings.Theme = themeValues[i]; Dispatcher.BeginInvoke(new Action(ChangeTheme), DispatcherPriority.Background); });
            Segmented("\uE745", Strings.MenuPosition, new[] { Strings.MenuTop, Strings.MenuBottom },
                () => string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.Position = i == 1 ? "Bottom" : "Top");
            MonitorRow();
            string[] ringValues = { "Blue", "Green", "Purple" };
            Segmented("\uE790", Strings.RingColorLabel, new[] { Strings.ColorBlue, Strings.ColorGreen, Strings.ColorPurple },
                () => Math.Max(0, Array.FindIndex(ringValues, v => string.Equals(v, settings.RingColor, StringComparison.OrdinalIgnoreCase))),
                i => settings.RingColor = ringValues[i]);
            string[] bannerValues = { "Small", "Normal" };
            Segmented("\uE8A1", Strings.BannerSizeLabel, new[] { Strings.BannerSmall, Strings.BannerNormal },
                () => string.Equals(settings.BannerSize, "Normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.BannerSize = bannerValues[i]);

            // The collapsed island: the thin line, when it shows and what it shows
            Group("\uE740", Strings.GroupThinLine, () =>
            {
                Toggle("\uE740", Strings.MenuAlwaysThin, Strings.HintAlwaysThin, () => settings.AlwaysThin, v => { settings.AlwaysThin = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
                if (!settings.AlwaysThin)
                {
                    string[] idleValues = { "Pill", "Line" };
                    Row("\uE7F8", Strings.MenuIdleStyle, null, null,
                        SegmentBar(new[] { Strings.IdlePill, Strings.IdleLine },
                            string.Equals(settings.IdleStyle, "Line", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                            i => settings.IdleStyle = idleValues[i]));
                    string[] maxValues = { "Thin", "Normal", "Hidden" };
                    Row("\uE740", Strings.MenuMaximized, Strings.HintMaximized, null,
                        SegmentBar(new[] { Strings.MaxThin, Strings.MaxNormal, Strings.MaxHidden },
                            Math.Max(0, Array.FindIndex(maxValues, v => string.Equals(v, settings.MaximizedMode, StringComparison.OrdinalIgnoreCase))),
                            i => { settings.MaximizedMode = maxValues[i]; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
                    if (string.Equals(settings.MaximizedMode, "Thin", StringComparison.OrdinalIgnoreCase))
                        Toggle("\uE740", Strings.MenuMaxActivities, Strings.HintMaxActivities, () => settings.MaximizedActivitiesThin, v => settings.MaximizedActivitiesThin = v);
                }
                Toggle("\uE8D6", Strings.MenuMinimalPosition, null, () => settings.MinimalMusicPosition, v => settings.MinimalMusicPosition = v);
            }, true);

            // Processor and memory
            Group("\uE9D9", Strings.GroupMonitor, () =>
            {
                Toggle("\uE9D9", Strings.MenuSystemMonitor, Strings.HintSystemMonitor, () => settings.SystemMonitor, v =>
                {
                    settings.SystemMonitor = v;
                    Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                });
                if (settings.SystemMonitor)
                {
                    Toggle("\uE7C9", Strings.MenuMonitorKeeps, Strings.HintMonitorKeeps, () => settings.MonitorKeepsPlace, v => settings.MonitorKeepsPlace = v);
                    string[] monitorValues = { "Rings", "Bars", "Numbers" };
                    Segmented("\uE790", Strings.MonitorStyleLabel, new[] { Strings.StyleRings, Strings.StyleBars, Strings.StyleNumbers },
                        () => Math.Max(0, Array.FindIndex(monitorValues, v => string.Equals(v, settings.MonitorStyle, StringComparison.OrdinalIgnoreCase))),
                        i => settings.MonitorStyle = monitorValues[i]);
                }
            });

            Group("\uE740", Strings.GroupSizePos, () =>
            {
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
            // Help: one button copies a short report (nothing is sent) and opens the page to report the problem
            Button report = new Button { Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            report.Content = GitHubLabel(report, Strings.DiagReport);
            report.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(Diagnostics.Report(settings));
                    report.Content = new TextBlock { Text = Strings.DiagCopied };
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/issues/new") { UseShellExecute = true })?.Dispose();
                }
                catch (Exception)
                {
                    report.Content = GitHubLabel(report, Strings.DiagReport);
                }
                DispatcherTimer back = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                back.Tick += (a2, b2) => { back.Stop(); report.Content = GitHubLabel(report, Strings.DiagReport); };
                back.Start();
            };
            Row("\uE897", null, null, null, null, report);
            ActionRow("\uE81C", Strings.MenuChangelog, () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WhatsNew.ChangelogUrl(settings.Language)) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            }, false, true);
            ActionRow("\uE8A5", Strings.MenuOpenSettings, () =>
            {
                settings.Save();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.FilePath) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            });
            // The last row, in red, since it removes the app: the same uninstaller Windows' apps list runs; it asks first
            if (Installer.IsInstalledCopy)
                ActionRow("\uE74D", Strings.MenuUninstall, () =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Installer.InstalledExe, "--uninstall") { UseShellExecute = true })?.Dispose(); }
                    catch (Exception) { }
                }, true);


        }

        string filter = "";       // What the search box holds
        DispatcherTimer searchTimer;
        bool searching;           // The page built for searching (every tab) is on screen

        // The text of the search box changed and the typing paused. The page is built once for the whole search; each new
        // word only hides and shows rows.
        internal void RunSearch()
        {
            string text = (SearchBox.Text ?? "").Trim();
            if (text.Length == 0 || !searching)
            {
                Rebuild();
                return;
            }
            filter = text;
            ApplyFilter();
            FitSoon();
        }

        void Rebuild()
        {
            Content.Children.Clear();
            filter = (SearchBox.Text ?? "").Trim();
            searching = filter.Length > 0;
            Build(searching ? AllTabs : tab);
            if (searching) ApplyFilter();
            FitSoon();
        }

        // Searching: every row is built as usual, then the ones that do not match are hidden (and the cards and folds that
        // end up empty). The rows carry their label and hint in their Tag.
        void ApplyFilter()
        {
            for (int i = 0; i < Content.Children.Count; i++)
            {
                Border border = Content.Children[i] as Border;
                StackPanel inner = border == null ? null : border.Child as StackPanel;
                if (inner == null) continue;
                bool any = FilterPanel(inner);
                border.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
                if (i > 0 && Content.Children[i - 1] is TextBlock) Content.Children[i - 1].Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        bool FilterPanel(StackPanel panel)
        {
            bool any = false;
            UIElement previous = null;
            foreach (UIElement child in panel.Children)
            {
                bool show = false;
                StackPanel row = child as StackPanel;
                Border fold = child as Border;
                if (row != null && row.Tag is string) show = Matches(row.Tag as string, null);
                else if (fold != null && fold.Child is StackPanel) show = FilterPanel((StackPanel)fold.Child);
                child.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (fold != null && show && previous != null) previous.Visibility = Visibility.Visible; // The header of a fold with a match
                any |= show;
                previous = child;
            }
            return any;
        }

        bool Matches(string label, string hint)
        {
            if (filter.Length == 0) return true;
            CompareInfo compare = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            return (label != null && compare.IndexOf(label, filter, options) >= 0) || (hint != null && compare.IndexOf(hint, filter, options) >= 0);
        }

        // The window grows or shrinks to the height its content needs, so nothing has to be scrolled
        // (as far as the screen allows). Done after the layout has settled.
        void FitSoon()
        {
            Dispatcher.BeginInvoke(new Action(() => FitHeight(true)), DispatcherPriority.Loaded);
        }

        double fitTop = double.NaN, fitHeight = double.NaN; // Where the last fit is going (it may still be moving)

        void FitHeight(bool animate)
        {
            if (!IsLoaded && animate) return;
            UpdateLayout();
            double chrome = ActualHeight - Scroller.ActualHeight;
            // The panel is stretched to fill the scroll area, so its own height cannot tell when the content shrank: measure what it needs
            Content.Measure(new Size(Math.Max(1, Content.ActualWidth), double.PositiveInfinity));
            double want = chrome + Content.DesiredSize.Height;
            double cx = Left + Width / 2;
            Rect work = Displays.Primary().WorkArea;
            foreach (Display d in Displays.All()) if (cx >= d.WorkArea.Left && cx <= d.WorkArea.Right) { work = d.WorkArea; break; }
            want = Math.Clamp(Math.Ceiling(want), MinHeight, Math.Max(MinHeight, work.Height - 24));
            if (searching) want = Math.Min(want, Math.Max(MinHeight, Math.Min(560, work.Height - 24))); // Results scroll instead
            double currentHeight = double.IsNaN(fitHeight) ? ActualHeight : fitHeight;
            if (Math.Abs(want - currentHeight) < 1) return;

            // Keep it inside the screen: when it would pass the bottom edge, it grows upwards instead
            // The edge nearest the taskbar stays where it is: a window in the lower half of the screen keeps its
            // bottom edge (growing and shrinking upwards), one in the upper half keeps its top edge.
            double top = !double.IsNaN(fitTop) ? fitTop : animate ? Top : restingTop;
            double now = !double.IsNaN(fitHeight) ? fitHeight : animate ? ActualHeight : Height;
            bool bottomFixed = top + now / 2 > work.Top + work.Height / 2;
            double newTop = bottomFixed ? top + now - want : top;
            newTop = Math.Min(newTop, work.Bottom - 12 - want);
            newTop = Math.Max(newTop, work.Top + 12);
            fitTop = newTop;
            fitHeight = want;

            if (!animate)
            {
                Height = want;
                restingTop = newTop;
                Top = newTop + (SystemParameters.ClientAreaAnimation ? 22 : 0);
                return;
            }
            System.Windows.Media.Animation.CubicEase ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            TimeSpan time = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 200 : 1);
            System.Windows.Media.Animation.DoubleAnimation grow = new System.Windows.Media.Animation.DoubleAnimation(ActualHeight, want, time) { EasingFunction = ease };
            grow.Completed += (a, b) => { BeginAnimation(HeightProperty, null); Height = want; if (Math.Abs(fitHeight - want) < 1) fitHeight = double.NaN; };
            BeginAnimation(HeightProperty, grow);
            if (Math.Abs(newTop - Top) > 0.5)
            {
                System.Windows.Media.Animation.DoubleAnimation move = new System.Windows.Media.Animation.DoubleAnimation(Top, newTop, time) { EasingFunction = ease };
                move.Completed += (a, b) => { BeginAnimation(TopProperty, null); Top = newTop; restingTop = newTop; if (Math.Abs(fitTop - newTop) < 1) fitTop = double.NaN; };
                BeginAnimation(TopProperty, move);
            }
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
            Border border = new Border
            {
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, !showTitles && Content.Children.Count > 0 ? 14 : 0, 0, 0), // Cards stacked without a title keep a gap
                Child = card,
            };
            Content.Children.Add(border);
        }

        static readonly System.Collections.Generic.HashSet<string> openGroups = new System.Collections.Generic.HashSet<string>(); // Remembered while the app runs

        // A row with an arrow that folds away the rows inside it, so a tab shows the main choices first.
        static readonly System.Collections.Generic.HashSet<string> openedOnce = new System.Collections.Generic.HashSet<string>();

        void Group(string icon, string title, Action build, bool openByDefault = false)
        {
            if (openByDefault && openedOnce.Add(title)) openGroups.Add(title);
            bool open = openGroups.Contains(title) || filter.Length > 0; // Searching opens every fold; the rows that do not match are hidden
            TextBlock arrow = new TextBlock
            {
                Text = "\uE70D",
                FontFamily = IconFont,
                FontSize = 12,
                Foreground = (Brush)FindResource("Secondary"),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(open ? 180 : 0),
            };
            Row(icon, title, null, arrow);
            StackPanel header = (StackPanel)card.Children[card.Children.Count - 1];
            header.Background = Brushes.Transparent;
            header.Cursor = System.Windows.Input.Cursors.Hand;

            // What is inside is set apart: darker, indented and with a blue edge, so it never reads as more of the same list
            StackPanel body = new StackPanel();
            Border inset = new Border
            {
                Child = body,
                Visibility = open ? Visibility.Visible : Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)),
                BorderThickness = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(0, 9, 9, 0),
                Margin = new Thickness(18, 0, 10, 10),
            };
            card.Children.Add(inset);
            header.MouseLeftButtonUp += (s, e) =>
            {
                bool now = inset.Visibility != Visibility.Visible;
                inset.Visibility = now ? Visibility.Visible : Visibility.Collapsed;
                if (now) openGroups.Add(title); else openGroups.Remove(title);
                ((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(now ? 180 : 0, TimeSpan.FromMilliseconds(180)));
                FitSoon();
            };

            StackPanel outer = card;
            card = body;
            try { build(); }
            finally { card = outer; }
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
                // The mark is a white shape: its outline cuts a rectangle of the icon color, so it fits a light window too
                ImageBrush shape = new ImageBrush(new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/" + icon.Substring(4) + "-mark.png")));
                RenderOptions.SetBitmapScalingMode(shape, BitmapScalingMode.HighQuality);
                System.Windows.Shapes.Rectangle mark = new System.Windows.Shapes.Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = IconBrush,
                    OpacityMask = shape,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
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

            StackPanel row = new StackPanel { Tag = (label ?? "") + "\n" + (hint ?? "") };
            if (card.Children.Count > 0)
                row.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Separator"), Margin = new Thickness(16, 0, 0, 0), SnapsToDevicePixels = true });
            row.Children.Add(grid);
            if (below != null) row.Children.Add(below);
            card.Children.Add(row);
        }

        void Toggle(string icon, string label, string hint, Func<bool> get, Action<bool> set, UIElement before = null)
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
            if (before == null) { Row(icon, label, hint, toggle); return; }
            StackPanel both = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            both.Children.Add(before);
            both.Children.Add(toggle);
            Row(icon, label, hint, both);
        }

        // Up and down arrows that move a page one place in the order the pages are shown.
        UIElement PageArrows(string name, int index, int count)
        {
            StackPanel arrows = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            foreach (int step in new[] { -1, 1 })
            {
                int target = index + step;
                bool possible = target >= 0 && target < count;
                TextBlock arrow = new TextBlock
                {
                    Text = step < 0 ? "\uE70E" : "\uE70D",
                    FontFamily = IconFont,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("Secondary"),
                    Padding = new Thickness(5, 6, 5, 6),
                    Opacity = possible ? 1 : 0.25,
                    Cursor = possible ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Background = Brushes.Transparent,
                };
                if (possible)
                    arrow.MouseLeftButtonUp += (s, e) =>
                    {
                        System.Collections.Generic.List<string> order = settings.PageOrderList();
                        string other = order[target];
                        order[target] = name;
                        order[index] = other;
                        settings.PageOrder = string.Join(",", order);
                        Apply();
                        Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                    };
                arrows.Children.Add(arrow);
            }
            return arrows;
        }

        static Color SwitchOn { get { return SettingsTheme.On; } }
        static Color SwitchOff { get { return SettingsTheme.SwitchOff; } }
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
            return SlidingBar(names, current, i => { set(i); Apply(); }, SettingsTheme.SegTrack,
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

        // Another program can hold the clipboard for a moment, so it is retried (waiting without freezing the window).
        static async System.Threading.Tasks.Task<bool> CopyText(string text)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    if (Clipboard.ContainsText() && Clipboard.GetText() == text) return true;
                }
                catch (Exception) { }
                await System.Threading.Tasks.Task.Delay(80);
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
            Group("\uE8C8", Strings.GroupApiMore, () => ApiDetails(api));
        }

        void ApiDetails(LocalApi api)
        {

            TextBlock note = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(60, 0, 14, 10) };
            Button copy = new Button { Content = Strings.ApiCopyToken, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            copy.Click += (s, e) =>
            {
                string token = api.Token;
                if (string.IsNullOrEmpty(token)) return;
                try
                {
                    // Out of the Win+V history and the cloud clipboard: the token is a secret
                    DataObject data = new DataObject();
                    data.SetText(token);
                    data.SetData("ExcludeClipboardContentFromMonitorProcessing", new System.IO.MemoryStream(new byte[] { 1, 0, 0, 0 }));
                    data.SetData("CanIncludeInClipboardHistory", new System.IO.MemoryStream(new byte[] { 0, 0, 0, 0 }));
                    data.SetData("CanUploadToCloudClipboard", new System.IO.MemoryStream(new byte[] { 0, 0, 0, 0 }));
                    Clipboard.SetDataObject(data, true);
                }
                catch (Exception) { return; }
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
            Button guide = new Button { Content = ExternalLabel(Strings.ApiGuide), Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            guide.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/blob/main/docs/API.md") { UseShellExecute = true }); } catch (Exception) { } };
            Row("\uE897", null, null, null, null, guide);

            StackPanel browsers = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (bool edge in new[] { false, true })
            {
                bool isEdge = edge;
                Button install = new Button { Content = isEdge ? "Edge" : "Chrome", Style = (Style)FindResource("LinkButton"), Margin = new Thickness(0, 0, 14, 0) };
                install.Click += async (s, e) =>
                {
                    try
                    {
                        string token = api.Token ?? api.RegenerateToken();
                        string folder = ExtensionInstaller.Extract(token, api.Port > 0 ? api.Port : settings.ApiPort);
                        // When the copy still fails after the retries, the folder opens in Explorer so its address
                        // can be copied from there.
                        bool copied = await CopyText(folder);
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
            Row("\uE896", Strings.ExtInstall, Strings.ExtHint, browsers); // The name and what it does on the left, the two browsers on the right
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

        static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        void ActionRow(string icon, string label, Action action, bool danger = false, bool external = false)
        {
            Button button = new Button { Content = external ? ExternalLabel(label) : (object)label, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            if (danger) button.Foreground = DangerBrush;
            button.Click += (s, e) => action();
            Row(icon, null, null, null, null, button);
            if (danger)
            {
                // The icon in red too
                StackPanel row = (StackPanel)card.Children[card.Children.Count - 1];
                Grid grid = row.Children[row.Children.Count - 1] as Grid;
                TextBlock mark = grid != null && grid.Children.Count > 0 ? grid.Children[0] as TextBlock : null;
                if (mark != null) mark.Foreground = DangerBrush;
            }
        }

        // Draws the settings to a PNG for the README: each tab as a column, side by side (a single column for the welcome).
        public void SaveSnapshot(string file)
        {
            const double scale = 2, colWidth = 440, gap = 16, margin = 22, partGap = 14;
            string mode = settings.SettingsMode;
            settings.SettingsMode = "Advanced";
            try
            {
                Action[] builds = welcome ? new Action[] { () => BuildWelcome() } : new Action[] { BuildActivities, BuildBehavior, BuildAppearance, BuildSystem };
                System.Collections.Generic.List<System.Windows.Media.Imaging.RenderTargetBitmap> parts = new System.Collections.Generic.List<System.Windows.Media.Imaging.RenderTargetBitmap>();
                System.Collections.Generic.List<double> heights = new System.Collections.Generic.List<double>();
                foreach (Action build in builds)
                {
                    Content.Children.Clear();
                    showTitles = !welcome;
                    Content.Margin = new Thickness(0);
                    Content.Width = colWidth; // Each column is exactly this wide, whatever the window is
                    build();
                    Content.Measure(new Size(colWidth, double.PositiveInfinity));
                    Size size = new Size(colWidth, Content.DesiredSize.Height);
                    Content.Arrange(new Rect(size));
                    Content.UpdateLayout();
                    DrawingVisual part = new DrawingVisual();
                    using (DrawingContext context = part.RenderOpen())
                        context.DrawRectangle(new VisualBrush(Content) { Stretch = Stretch.Fill }, null, new Rect(0, 0, Content.ActualWidth, Content.ActualHeight));
                    System.Windows.Media.Imaging.RenderTargetBitmap piece = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    piece.Render(part);
                    parts.Add(piece);
                    heights.Add(size.Height);
                }

                // Each piece goes to the shortest column, in order
                int columns = welcome ? 1 : 3;
                double[] used = new double[columns];
                double[] top = new double[parts.Count];
                int[] where = new int[parts.Count];
                double title = welcome ? 0 : 46;
                for (int i = 0; i < parts.Count; i++)
                {
                    int c = 0;
                    for (int k = 1; k < columns; k++) if (used[k] < used[c]) c = k;
                    where[i] = c;
                    top[i] = used[c];
                    used[c] += heights[i] + partGap;
                }
                double totalWidth = margin * 2 + columns * colWidth + (columns - 1) * gap;
                double totalHeight = margin * 2 + title + (used.Max() - partGap);
                DrawingVisual page = new DrawingVisual();
                using (DrawingContext context = page.RenderOpen())
                {
                    context.DrawRoundedRectangle(new SolidColorBrush(SettingsTheme.WindowBack), null, new Rect(0, 0, totalWidth, totalHeight), 16, 16);
                    if (!welcome)
                    {
                        FormattedText heading = new FormattedText(Strings.SettingsTitle, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 30, new SolidColorBrush(SettingsTheme.Text), 1.0);
                        context.DrawText(heading, new Point(margin + 4, margin - 4));
                    }
                    for (int i = 0; i < parts.Count; i++)
                        context.DrawImage(parts[i], new Rect(margin + where[i] * (colWidth + gap), margin + title + top[i], colWidth, heights[i]));
                }
                System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    (int)(totalWidth * scale), (int)(totalHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(page);
                System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
            }
            finally
            {
                settings.SettingsMode = mode;
                Content.Width = double.NaN;
            }
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
