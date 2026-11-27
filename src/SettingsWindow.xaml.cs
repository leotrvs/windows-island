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

        // Titles, tabs and rows again, in the language just chosen.
        void RefreshTexts()
        {
            Title = Installer.Name + " – " + Strings.SettingsTitle;
            Heading.Text = Strings.SettingsTitle;
            SearchHint.Text = Strings.SettingsSearch;
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
    }
}
