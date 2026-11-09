using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowsIsland
{
    public partial class IslandWindow : Window
    {
        enum View { Hidden, Minimal, MusicPeek, Idle, MusicCompact, ClaudeCompact, CodexCompact, TimerCompact, BatteryCompact, ExternalCompact, SystemCompact, MusicExpanded, ClaudeExpanded, TimerExpanded, SystemExpanded, ExternalExpanded, Banner, Toast, Level, Permission }
        enum Activity { None, Music, Claude, Codex, Timer, Battery, System, External }

        static readonly Dictionary<View, Size> Sizes = new Dictionary<View, Size>
        {
            [View.Hidden] = new Size(90, 24),
            [View.Minimal] = new Size(76, 6),
            [View.MusicPeek] = new Size(236, 36),
            [View.Idle] = new Size(112, 30),
            [View.MusicCompact] = new Size(176, 34),
            [View.SystemCompact] = new Size(140, 34),
            [View.ClaudeCompact] = new Size(176, 34),
            [View.CodexCompact] = new Size(176, 34),
            [View.TimerCompact] = new Size(150, 34),
            [View.BatteryCompact] = new Size(150, 34),
            [View.ExternalCompact] = new Size(190, 34),
            [View.ExternalExpanded] = new Size(372, 150),
            [View.MusicExpanded] = new Size(372, 152),
            [View.TimerExpanded] = new Size(372, 150),
            [View.SystemExpanded] = new Size(372, 150),
            [View.ClaudeExpanded] = new Size(372, 156),
            [View.Banner] = new Size(352, 74),
            [View.Toast] = new Size(184, 36),
            [View.Level] = new Size(220, 36),
            [View.Permission] = new Size(372, 138),
        };

        const double MaxRadius = 38;
        const double BubbleSize = 34;
        const double BubbleGap = 7;
        static readonly Brush White = Freeze(new SolidColorBrush(Colors.White));
        static readonly Brush Orange = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x87, 0x5F)));
        static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)));
        static readonly Brush Amber = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)));
        static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));
        static readonly Brush ClaudeTint = Freeze(new SolidColorBrush(Color.FromArgb(0x29, 0xE8, 0x87, 0x5F)));
        static readonly Brush CodexTint = Brushes.Transparent; // The Codex icon brings its own color
        static readonly Brush Blue = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x8C, 0xFF)));
        static readonly Brush Purple = Freeze(new SolidColorBrush(Color.FromRgb(0x9B, 0x6B, 0xFF)));
        static readonly Brush NotificationTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));

        readonly Settings settings;
        readonly MediaService media;
        readonly ClaudeService claude;
        readonly CodexService codex;
        readonly TimerService timer;
        readonly PrivacyService privacy;
        readonly Spring privacyOpacity = new Spring(0, 0.002, 320, 1);
        readonly Spring privacyShift = new Spring(0, 0.05, 260, 0.7);

        // Shape: slightly bouncy, like the real island.
        readonly Spring width = new Spring(112, 0.05, 280, 0.72);
        readonly Spring height = new Spring(30, 0.05, 280, 0.72);
        readonly Spring shown = new Spring(0, 0.002, 200, 0.7);
        readonly Spring stateScale = new Spring(1, 0.001, 280, 0.72); // The user's size for the current state
        readonly Spring bubbleShift = new Spring(20, 0.05, 260, 0.7);
        readonly Spring bubbleScale = new Spring(0.4, 0.002, 260, 0.6);
        readonly Spring bubbleOpacity = new Spring(0, 0.002, 320, 1);
        readonly Spring sessionArc = new Spring(0, 0.05, 90, 1);
        readonly Spring weeklyArc = new Spring(0, 0.05, 90, 1);

        readonly Dictionary<View, FrameworkElement> panels = new Dictionary<View, FrameworkElement>();
        readonly Dictionary<FrameworkElement, Spring> presence = new Dictionary<FrameworkElement, Spring>();
        readonly Rectangle[] compactBars = new Rectangle[4];
        readonly Rectangle[] expandedBars = new Rectangle[4];
        readonly double[] eqLevel = { 0.2, 0.2, 0.2, 0.2 };
        readonly double[] eqTarget = { 0.2, 0.2, 0.2, 0.2 };
        readonly Random random = new Random();
        readonly RectangleGeometry stageClip = new RectangleGeometry();
        readonly DropShadowEffect shadow = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Direction = 270, Opacity = 0, Color = Colors.Black };
        readonly DispatcherTimer bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        readonly DispatcherTimer enterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        readonly DispatcherTimer leaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        readonly DispatcherTimer pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        View view = View.Idle;
        View? forced;
        Activity bubble = Activity.None;
        bool hovering, fullscreen, maximized, rendering, autoHidden;
        DateTime lastActive = DateTime.Now;
        DateTime peekUntil = DateTime.MinValue;
        readonly DispatcherTimer revealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        DateTime bannerUntil = DateTime.MinValue;
        TimeSpan lastFrame;
        double clock, eqClock;
        double drawnSessionArc = -1, drawnWeeklyArc = -1;
        string lastElapsed, lastRemaining, lastCompactText, lastStatus;
        IntPtr hwnd;

        internal IslandWindow(Settings settings, MediaService media, ClaudeService claude, CodexService codex, TimerService timer, PrivacyService privacy)
        {
            this.timer = timer;
            this.privacy = privacy;
            this.settings = settings;
            this.media = media;
            this.claude = claude;
            this.codex = codex;
            InitializeComponent();

            panels[View.Idle] = IdlePanel;
            panels[View.Minimal] = MinimalPanel;
            panels[View.MusicPeek] = MusicPeek;
            panels[View.MusicCompact] = MusicCompact;
            panels[View.ClaudeCompact] = ClaudeCompact;
            panels[View.CodexCompact] = CodexCompact;
            panels[View.TimerCompact] = TimerCompact;
            panels[View.BatteryCompact] = BatteryCompact;
            panels[View.ExternalCompact] = ExternalCompact;
            panels[View.SystemCompact] = SystemCompact;
            panels[View.ExternalExpanded] = ExternalExpanded;
            panels[View.TimerExpanded] = TimerExpanded;
            panels[View.SystemExpanded] = SystemExpanded;
            panels[View.MusicExpanded] = MusicExpanded;
            panels[View.ClaudeExpanded] = ClaudeExpanded;
            panels[View.Banner] = Banner;
            panels[View.Toast] = Toast;
            panels[View.Level] = Level;
            panels[View.Permission] = PermissionPanel;
            foreach (FrameworkElement panel in panels.Values)
            {
                presence[panel] = new Spring(0, 0.002, 300, 0.85);
                panel.Opacity = 0;
                panel.Visibility = Visibility.Hidden;
                panel.RenderTransform = new ScaleTransform(1, 1);
            }
            presence[IdlePanel].Snap(1);
            Stage.Clip = stageClip;

            BuildBars(CompactEq, compactBars, 3, 2.6);
            BuildBars(ExpandedEq, expandedBars, 3.6, 3);
            SetupButton(PreviousButton, media.Previous);
            SetupButton(PlayButton, media.TogglePlayPause);
            SetupButton(NextButton, media.Next);
            SetupButton(BtnMinus, () => timer.Nudge(-1));
            SetupButton(BtnPlus, () => timer.Nudge(1));
            SetupButton(BtnStart, () => timer.StartCountdown(timer.ChosenMinutes));
            SetupButton(BtnWatch, timer.StartStopwatch);
            SetupButton(BtnPause, timer.PauseOrResume);
            SetupButton(BtnStop, timer.Stop);
            Pill.MouseWheel += OnPillWheel;
            ProgressHit.MouseLeftButtonUp += Seek;
            ProgressHit.MouseLeftButtonDown += (s, e) => e.Handled = true;

            ApplyTexts();
            ApplyBannerSize();

            monitorTimer.Tick += (s, e) => UpdateSystemMonitor();
            Pill.PreviewMouseLeftButtonDown += (s, e) => { dragStart = e.GetPosition(this); suppressClick = false; };
            Pill.PreviewMouseMove += OnPillDrag;
            Pill.PreviewMouseLeftButtonUp += (s, e) => dragStart = null;
            PrivacyDots.MouseEnter += (s, e) => { dotHover = true; UpdatePrivacy(); };
            PrivacyDots.MouseLeave += (s, e) => { dotHover = false; UpdatePrivacy(); };
            Pill.MouseEnter += OnPillEnter;
            Pill.MouseLeave += OnPillLeave;
            Pill.MouseLeftButtonUp += OnPillClick;
            enterTimer.Tick += (s, e) => { enterTimer.Stop(); SetHovering(true); };
            // A click counts only when it is pressed and released on the same button, and only a moment after the
            // prompt appeared, so a click meant for the window underneath (or a double click) cannot answer it
            PermAllow.MouseLeftButtonDown += (s, e) => { e.Handled = true; permissionPressed = PermAllow; PermAllow.CaptureMouse(); };
            PermDeny.MouseLeftButtonDown += (s, e) => { e.Handled = true; permissionPressed = PermDeny; PermDeny.CaptureMouse(); };
            PermAllow.MouseLeftButtonUp += (s, e) => AnswerPermission(PermAllow, "allow", e);
            PermDeny.MouseLeftButtonUp += (s, e) => AnswerPermission(PermDeny, "deny", e);
            PermOpen.MouseLeftButtonUp += (s, e) => { e.Handled = true; ClaudeWindow.Focus(); };
            PermOpen.MouseLeftButtonDown += (s, e) => e.Handled = true;
            OpenClaudeLink.MouseLeftButtonUp += (s, e) => { e.Handled = true; ClaudeWindow.Focus(); };
            OpenClaudeLink.MouseLeftButtonDown += (s, e) => e.Handled = true;
            // Resting the mouse on the small second activity opens the island straight on that activity's page
            bubbleTimer.Tick += (s, e) =>
            {
                bubbleTimer.Stop();
                if (bubble == Activity.None || hovering) return;
                Activity target = bubble == Activity.Codex ? Activity.Claude : bubble;
                if (Pages().Contains(target)) page = target;
                SetHovering(true);
            };
            Bubble.MouseEnter += (s, e) => { if (settings.ExpandOnHover && bubble != Activity.None && !hovering) bubbleTimer.Start(); };
            Bubble.MouseLeave += (s, e) => bubbleTimer.Stop();
            leaveTimer.Tick += (s, e) => { leaveTimer.Stop(); SetHovering(false); };
            pollTimer.Tick += (s, e) => Poll();
            revealTimer.Tick += (s, e) => CheckReveal();

            media.Changed += OnMediaChanged;
            claude.Changed += OnClaudeChanged;
            claude.Alert += ShowBanner;
            codex.Changed += OnCodexChanged;
            codex.Finished += OnCodexFinished;
            timer.Changed += OnTimerChanged;
            privacy.Changed += OnPrivacyChanged;
            discord = (Application.Current as App)?.Discord;
            if (discord != null) discord.Changed += OnDiscordChanged;
            battery = (Application.Current as App)?.Battery;
            if (battery != null) battery.Changed += OnBatteryChanged;
            api = (Application.Current as App)?.Api;
            if (api != null) { api.ActivityUpdated += OnApiActivity; api.ActivityEnded += OnApiEnded; }
            focusState = (Application.Current as App)?.Focus;
            if (focusState != null) focusState.Changed += OnFocusChanged;

            SourceInitialized += (s, e) =>
            {
                hwnd = new WindowInteropHelper(this).Handle;
                Native.MakeOverlay(hwnd);
            };
            Loaded += (s, e) =>
            {
                PlaceWindow();
                UpdateMusic();
                UpdateClaude();
                UpdateCodex();
                UpdatePrivacy();
                UpdateTimerPage();
                shown.Target = 1;
                Recompute();
                StartLoop();
                pollTimer.Start();
            };
        }

        void OnMediaChanged() { UpdateMusic(); MusicPeekCheck(); Recompute(); }

        // While the island is a thin line, a change in the music (pause, play, next, previous) makes it grow for a moment,
        // with the cover, a symbol for what happened and the song's name.
        bool musicPrimed, musicWasPlaying;
        string musicTitle = "", musicBefore = "";
        DateTime musicPeekUntil = DateTime.MinValue;
        DispatcherTimer peekTimer;
        bool ThinActive { get { return settings.MaximizedActivitiesThin && maximized && !IsBottom && settings.MaximizedMode == "Thin"; } }

        void MusicPeekCheck()
        {
            string title = media.Title ?? "";
            bool playing = media.IsPlaying;
            if (!musicPrimed)
            {
                musicPrimed = true;
                musicTitle = title;
                musicWasPlaying = playing;
                return;
            }
            string glyph = null;
            if (title.Length > 0 && title != musicTitle)
            {
                glyph = title == musicBefore ? "\uE892" : "\uE893"; // Back to the song before, or onwards
                musicBefore = musicTitle;
                musicTitle = title;
            }
            else if (playing != musicWasPlaying) glyph = playing ? "\uE768" : "\uE769";
            musicWasPlaying = playing;
            if (glyph == null || !settings.ShowMusic || !ThinActive) return;
            PeekGlyph.Text = glyph;
            PeekTitle.Text = string.IsNullOrEmpty(title) ? Strings.UnknownTrack : title;
            musicPeekUntil = DateTime.Now.AddSeconds(2.6);
            if (peekTimer == null)
            {
                peekTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.7) };
                peekTimer.Tick += (s, e) => { peekTimer.Stop(); Recompute(); };
            }
            peekTimer.Stop();
            peekTimer.Start();
        }
        void OnClaudeChanged() { UpdateClaude(); Recompute(); }

        void OnTimerChanged() { UpdateTimerPage(); Recompute(); }
        void OnPrivacyChanged() { UpdatePrivacy(); Recompute(); }

        // Muting or unmuting in Discord brings the island back for a moment, even if it was put away or auto-hidden,
        // so the change is noticed; the crossed icon stays beside it while you are muted.
        bool discordWasMuted, discordWasDeaf;
        DateTime discordChangedAt = DateTime.MinValue;
        void OnDiscordChanged()
        {
            bool changed = discord != null && (discord.Muted != discordWasMuted || discord.Deafened != discordWasDeaf);
            if (discord != null) { discordWasMuted = discord.Muted; discordWasDeaf = discord.Deafened; }
            if (changed && settings.DiscordEnabled)
            {
                tucked = false;
                lastActive = DateTime.Now; // The auto-hide time starts again
                discordChangedAt = DateTime.Now;
            }
            UpdatePrivacy();
            Recompute();
        }
        void OnBatteryChanged() { UpdateBattery(); Recompute(); }

        // The clock and system page: the time, and the processor and memory load, refreshed once a second.
        readonly SystemStats systemStats = new SystemStats();
        DateTime lastSystemUpdate = DateTime.MinValue;
        void UpdateSystemPage()
        {
            DateTime now = DateTime.Now;
            string time = now.ToString("t");
            if (ClockBig.Text != time) ClockBig.Text = time;
            if (now - lastSystemUpdate < TimeSpan.FromSeconds(1) && lastSystemUpdate != DateTime.MinValue) return;
            if (now - lastSystemUpdate > TimeSpan.FromSeconds(3)) systemStats.Reset();
            lastSystemUpdate = now;
            systemStats.Update();
            ClockDate.Text = now.ToString("dddd, d MMMM");
            CpuLabel.Text = Strings.CpuLabel;
            MemoryLabel.Text = Strings.MemoryLabel;
            CpuText.Text = systemStats.Cpu + "%";
            MemoryText.Text = systemStats.Memory + "%";
            CpuFill.Width = 132.0 * systemStats.Cpu / 100;
            MemoryFill.Width = 132.0 * systemStats.Memory / 100;
        }
        // The system monitor on the collapsed island: processor and memory as rings, bars or numbers, once a second.
        static readonly Brush MonitorBlue = Freeze(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
        static readonly Brush MonitorGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)));
        readonly DispatcherTimer monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        double monitorWidth = 140;
        string monitorStyleDrawn;

        string MonitorStyle
        {
            get
            {
                string style = settings.MonitorStyle ?? "";
                return style.Equals("Bars", StringComparison.OrdinalIgnoreCase) ? "Bars" : style.Equals("Numbers", StringComparison.OrdinalIgnoreCase) ? "Numbers" : "Rings";
            }
        }

        void UpdateSystemMonitor()
        {
            UpdateSystemPage();
            int cpu = systemStats.Cpu, memory = systemStats.Memory;
            string style = MonitorStyle;
            if (style != monitorStyleDrawn)
            {
                monitorStyleDrawn = style;
                SysRings.Visibility = style == "Rings" ? Visibility.Visible : Visibility.Collapsed;
                SysBars.Visibility = style == "Bars" ? Visibility.Visible : Visibility.Collapsed;
                SysNumbers.Visibility = style == "Numbers" ? Visibility.Visible : Visibility.Collapsed;
                FrameworkElement active = style == "Rings" ? (FrameworkElement)SysRings : style == "Bars" ? SysBars : SysNumbers;
                active.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                monitorWidth = Math.Ceiling(active.DesiredSize.Width) + 34; // The same breathing room on both sides
                SystemCompact.Width = monitorWidth;
                if (view == View.SystemCompact) ApplyTargets();
            }
            Brush cpuBrush = cpu >= 90 ? Red : cpu >= 70 ? Amber : MonitorBlue;
            Brush memoryBrush = memory >= 90 ? Red : memory >= 70 ? Amber : MonitorGreen;
            if (style == "Rings")
            {
                SysCpuRing.Data = Arc(Math.Max(cpu, 2), 20, 3);
                SysMemRing.Data = Arc(Math.Max(memory, 2), 20, 3);
                SysCpuRing.Stroke = cpuBrush;
                SysMemRing.Stroke = memoryBrush;
                SysCpuRingText.Text = cpu + "%";
                SysMemRingText.Text = memory + "%";
            }
            else if (style == "Bars")
            {
                SysCpuBar.Width = 42.0 * cpu / 100;
                SysMemBar.Width = 42.0 * memory / 100;
                SysCpuBar.Background = cpuBrush;
                SysMemBar.Background = memoryBrush;
            }
            else
            {
                SysCpuNumber.Text = cpu + "%";
                SysMemNumber.Text = memory + "%";
            }
        }

        void OnFocusChanged() { UpdateFocusMoon(); }

        // ---- Live activities pushed by scripts and apps through the local API ----
        LocalApi api;
        DiscordService discord;
        void OnApiActivity(ApiActivity activity) { UpdateExternal(); Recompute(); }
        void OnApiEnded(string id) { UpdateExternal(); Recompute(); }
        bool ApiActive { get { return api != null && api.Activities.Count > 0; } }

        static readonly Brush ApiBlue = Freeze(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
        static readonly Brush ApiGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)));
        static readonly Brush ApiRed = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));
        static Brush StateBrush(string state) { return state == "done" ? ApiGreen : state == "error" ? ApiRed : ApiBlue; }
        static string SideOf(ApiActivity a) { return a.State == "done" ? "\u2713" : a.State == "error" ? "\u2715" : a.Progress >= 0 ? a.Progress + "%" : "\u2026"; }

        void UpdateExternal()
        {
            if (api == null) return;
            List<ApiActivity> list = api.Activities.OrderByDescending(a => a.Updated).ToList();
            if (list.Count == 0) return;
            BuildExternal(list);
        }

        void BuildExternal(List<ApiActivity> list)
        {
            ApiActivity first = list[0];
            ExternalGlyph.Text = first.IconGlyph;
            ExternalGlyph.Foreground = StateBrush(first.State);
            ExternalTitle.Text = first.Title;
            ExternalSide.Text = SideOf(first);
            ExternalSide.Foreground = first.State == "running" ? White : StateBrush(first.State);
            BubbleExternal.Text = first.IconGlyph;
            BubbleExternal.Foreground = StateBrush(first.State);

            // The progress bar under the main activity
            bool running = first.State == "running" && first.Progress >= 0;
            ExternalBarTrack.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            ExternalBar.Width = 158.0 * Math.Clamp(first.Progress, 0, 100) / 100;
            ExternalBar.Background = StateBrush(first.State);

            // As the second activity: a small icon inside a ring that fills; when it is done, a green disc with a check that pops in
            bool done = first.State == "done";
            BubbleRingTrack.Visibility = BubbleRing.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            BubbleRing.Data = Arc(first.Progress, 28, 2.5);
            BubbleExternal.FontSize = running ? 11 : 16;
            BubbleExternal.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
            if (done && BubbleDone.Visibility != Visibility.Visible)
            {
                BubbleDone.Visibility = Visibility.Visible;
                DoubleAnimation pop = new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 } };
                BubbleDoneScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                BubbleDoneScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            }
            else if (!done) BubbleDone.Visibility = Visibility.Collapsed;

            // The page: up to three rows, each with who sent it
            ExternalList.Children.Clear();
            foreach (ApiActivity a in list.Take(3))
            {
                Grid row = new Grid { Height = 38 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = a.IconGlyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = StateBrush(a.State), VerticalAlignment = VerticalAlignment.Center });
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = a.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                string caption = string.IsNullOrEmpty(a.Text) ? a.App : a.App + " \u00B7 " + a.Text;
                text.Children.Add(new TextBlock { Text = caption, FontSize = 11, Foreground = (Brush)FindResource("Secondary"), TextTrimming = TextTrimming.CharacterEllipsis });
                if (a.Progress >= 0)
                {
                    Grid track = new Grid { Height = 3, Margin = new Thickness(0, 3, 0, 0) };
                    track.Children.Add(new Border { CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) });
                    Border fill = new Border { CornerRadius = new CornerRadius(1.5), Background = StateBrush(a.State), HorizontalAlignment = HorizontalAlignment.Left, Width = 292.0 * a.Progress / 100 };
                    track.Children.Add(fill);
                    text.Children.Add(track);
                }
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                TextBlock side = new TextBlock { Text = SideOf(a), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = a.State == "running" ? White : StateBrush(a.State) };
                Grid.SetColumn(side, 2);
                row.Children.Add(side);
                ExternalList.Children.Add(row);
            }
            if (list.Count > 3)
                ExternalList.Children.Add(new TextBlock { Text = "+" + (list.Count - 3), FontSize = 11, Foreground = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Right });
        }

        // An alert from a script or app: a banner that names who sent it.
        internal void ShowApiAlert(ApiAlert alert)
        {
            if (fullscreen && settings.HideInFullscreen) return;
            Enqueue(new BannerItem { App = alert.App, Title = alert.Title, Subtitle = alert.Text, Glyph = alert.IconGlyph, Seconds = alert.Seconds });
        }

        FocusService focusState;
        void UpdateFocusMoon()
        {
            FocusMoon.Visibility = settings.ShowFocus && focusState != null && focusState.On ? Visibility.Visible : Visibility.Collapsed;
        }

        BatteryService battery;
        bool BatteryActive { get { return settings.BatteryActivity && battery != null && battery.Charging; } }

        void UpdateBattery()
        {
            if (battery != null && battery.HasBattery) BatteryCompactText.Text = battery.Percent + "%";
        }

        bool ScreenOn { get { return settings.ShowPrivacyDots && settings.ShowScreenShare && privacy.Screen; } }
        bool PrivacyOn { get { return settings.ShowPrivacyDots && (privacy.Microphone || privacy.Camera || ScreenOn); } }

        string privacyLabel = "";
        bool dotHover; // The mouse is over the dots themselves: that is what shows the app name
        DateTime privacyNameUntil;
        DispatcherTimer privacyNameTimer;

        // "Zoom", or "Zoom +1" when several apps are recording.
        string PrivacyLabel()
        {
            List<string> names = new List<string>();
            if (privacy.Microphone) names.AddRange(privacy.MicrophoneApps);
            if (privacy.Camera) names.AddRange(privacy.CameraApps);
            if (ScreenOn) names.AddRange(privacy.ScreenApps);
            names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0) return "";
            string first = names[0].Length > 20 ? names[0].Substring(0, 19) + "…" : names[0];
            return names.Count > 1 ? first + " +" + (names.Count - 1) : first;
        }

        void UpdatePrivacy()
        {
            UpdateFocusMoon();
            bool mic = privacy.Microphone && settings.ShowPrivacyDots, cam = privacy.Camera && settings.ShowPrivacyDots, screen = ScreenOn;
            MicDot.Visibility = mic ? Visibility.Visible : Visibility.Collapsed;
            CamDot.Visibility = cam ? Visibility.Visible : Visibility.Collapsed;
            ScreenDot.Visibility = screen ? Visibility.Visible : Visibility.Collapsed;
            // Muted or deafened in Discord, while the Discord option is on and connected
            bool deaf = settings.DiscordEnabled && discord != null && discord.Deafened;
            bool muted = settings.DiscordEnabled && discord != null && discord.Muted && !deaf;
            DiscordDeaf.Visibility = deaf ? Visibility.Visible : Visibility.Collapsed;
            DiscordMute.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            bool discordShown = deaf || muted;

            // Hovering over the dots (not the island) shows the name of the app using the microphone or camera; a new app also shows it by itself if the setting asks for it.
            string label = mic || cam || screen ? PrivacyLabel() : "";
            bool anything = mic || cam || screen || discordShown;
            if (label != privacyLabel)
            {
                bool added = label.Length > 0;
                privacyLabel = label;
                if (added && settings.PrivacyNameSeconds > 0)
                {
                    privacyNameUntil = DateTime.Now.AddSeconds(settings.PrivacyNameSeconds);
                    if (privacyNameTimer == null)
                    {
                        privacyNameTimer = new DispatcherTimer();
                        privacyNameTimer.Tick += (s, e) => { privacyNameTimer.Stop(); UpdatePrivacy(); };
                    }
                    privacyNameTimer.Interval = TimeSpan.FromSeconds(settings.PrivacyNameSeconds + 0.1);
                    privacyNameTimer.Stop();
                    privacyNameTimer.Start();
                }
            }
            bool showName = label.Length > 0 && (dotHover || DateTime.Now < privacyNameUntil);
            PrivacyDots.IsHitTestVisible = anything; // Only while a dot is there, so the spot is empty the rest of the time
            double dotsWidth = 10 + 10 * ((mic ? 1 : 0) + (cam ? 1 : 0) + (screen ? 1 : 0)) + (discordShown ? 17 : 0), nameWidth = 0;
            PrivacyName.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;
            if (showName)
            {
                if (PrivacyName.Text != label) PrivacyName.Text = label;
                PrivacyName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                nameWidth = PrivacyName.DesiredSize.Width + PrivacyName.Margin.Left + PrivacyName.Margin.Right;
            }
            PrivacyDots.Width = dotsWidth + nameWidth;
            privacyOpacity.Target = anything ? 1 : 0;
        }

        string lastTimerText;
        void UpdateTimerLive()
        {
            string text = TimerService.Format(timer.Running ? timer.Shown : TimeSpan.FromMinutes(timer.ChosenMinutes));
            if (text != lastTimerText)
            {
                lastTimerText = text;
                TimerCompactText.Text = text;
                TimerBig.Text = text;
            }
            TimerCompactText.Opacity = timer.Paused ? 0.5 : 1;
        }

        // What the timer page offers: choose a length and start, or pause and stop what is running.
        void UpdateTimerPage()
        {
            bool running = timer.Running;
            TimerHeader.Text = !running ? Strings.TimerPickLength
                             : (timer.Mode == TimerService.Kind.Countdown ? Strings.TimerLabel : Strings.StopwatchLabel)
                               + (timer.Paused ? " · " + Strings.TimerPaused : "");
            BtnMinus.Visibility = BtnPlus.Visibility = running ? Visibility.Hidden : Visibility.Visible;
            BtnStart.Visibility = BtnWatch.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
            BtnPause.Visibility = BtnStop.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            BtnPauseText.Text = timer.Paused ? Strings.TimerResume : Strings.TimerPause;
            TimerBig.Opacity = timer.Paused ? 0.5 : 1;
            lastTimerText = null;
            UpdateTimerLive();
        }

        void OnCodexChanged()
        {
            UpdateCodex();
            ApplyTargets(); // The Claude panel grows or shrinks with the Codex section
            Recompute();
        }

        void OnCodexFinished()
        {
            if (!settings.ShowCodex || !settings.CodexBanners) return;
            Enqueue(new BannerItem { FromCodex = true, Title = Strings.CodexDone, Subtitle = "Codex", Seconds = settings.AlertSeconds });
        }

        bool CodexActive
        {
            get { return settings.ShowCodex && codex.Working; }
        }

        // The Codex part of the expanded panel shows only while Codex is working (and turned on).
        bool CodexSectionVisible
        {
            get { return settings.ShowCodex && codex.Working; }
        }

        Size SizeOf(View value)
        {
            if (value == View.ClaudeExpanded) return new Size(372, ClaudePanelHeight);
            if (value == View.Banner) return new Size(Banner.Width, Banner.Height);
            if (value == View.Permission) return new Size(372, permissionHeight);
            if (value == View.SystemCompact) return new Size(monitorWidth, 34);
            if (value == View.Minimal && minimalHasContent) return new Size(76, 16);
            return Sizes[value];
        }

        double ClaudePanelHeight
        {
            get { return CodexSectionVisible ? 248 : 156; }
        }

        void UpdateCodex()
        {
            bool visible = CodexSectionVisible;
            CodexSection.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ClaudeExpanded.Height = ClaudePanelHeight;
            if (!visible) return;

            UpdateCodexLive();
            UsageWindow session = codex.Session;
            UsageWindow weekly = codex.Weekly;
            int sessionPercent = session?.EffectivePercent ?? 0;
            int weeklyPercent = weekly?.EffectivePercent ?? 0;
            CodexSessionPercent.Text = session?.Percent != null ? sessionPercent + "%" : "–";
            CodexWeeklyPercent.Text = weekly?.Percent != null ? weeklyPercent + "%" : "–";
            CodexSessionArc.Data = Arc(sessionPercent, 36, 4);
            CodexWeeklyArc.Data = Arc(weeklyPercent, 36, 4);
            CodexSessionArc.Stroke = RingBrush(sessionPercent);
            CodexWeeklyArc.Stroke = RingBrush(weeklyPercent);
        }

        // The parts that change every second while Codex works.
        void UpdateCodexLive()
        {
            string elapsed = codex.WorkingSince.HasValue ? FormatTime(DateTime.Now - codex.WorkingSince.Value) : "";
            if (CodexCompactText.Text != elapsed) CodexCompactText.Text = elapsed;
            string status = codex.Working ? (Strings.Working + " " + elapsed).Trim()
                          : codex.Session?.Percent != null ? Strings.CodexIdle
                          : Strings.CodexNoData;
            if (CodexStatus.Text != status) CodexStatus.Text = status;
        }

        // The monitor this island lives on. Null means the primary monitor.
        internal Display Target { get; private set; }

        // Moves the island to another monitor. With animate, it pops in there like at startup.
        internal void MoveTo(Display display, bool animate)
        {
            Target = display;
            if (!IsLoaded) return;
            if (animate)
            {
                shown.Value = 0; // Pops in from nothing; the target stays where the state puts it
                shown.Velocity = 0;
                lastActive = DateTime.Now; // Arriving on a screen counts as activity, so auto-hide waits its full time
            }
            PlaceWindow();
            Recompute();
            ApplyTargets();
            StartLoop();
        }

        // Called before the island is closed for good, so the shared services no longer reach it.
        internal void Detach()
        {
            CancelPermissions();
            media.Changed -= OnMediaChanged;
            claude.Changed -= OnClaudeChanged;
            claude.Alert -= ShowBanner;
            codex.Changed -= OnCodexChanged;
            codex.Finished -= OnCodexFinished;
            timer.Changed -= OnTimerChanged;
            privacy.Changed -= OnPrivacyChanged;
            if (discord != null) discord.Changed -= OnDiscordChanged;
            if (battery != null) battery.Changed -= OnBatteryChanged;
            if (focusState != null) focusState.Changed -= OnFocusChanged;
            if (api != null) { api.ActivityUpdated -= OnApiActivity; api.ActivityEnded -= OnApiEnded; }
            enterTimer.Stop();
            leaveTimer.Stop();
            pollTimer.Stop();
            revealTimer.Stop();
            StopLoop();
        }

        bool IsBottom
        {
            get { return string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase); }
        }

        bool MusicActive
        {
            get
            {
                return settings.ShowMusic && media.HasSession && !string.IsNullOrEmpty(media.Title) &&
                       (media.IsPlaying || DateTime.Now - media.LastPlayingAt < TimeSpan.FromSeconds(settings.PausedMusicSeconds));
            }
        }

        bool ClaudeActive
        {
            get { return settings.ShowClaude && (claude.Busy > 0 || claude.Waiting > 0); }
        }

        // Shows one state for screenshots and design work, e.g. --preview=MusicExpanded.
        public void Preview(string name)
        {
            View value;
            if (!Enum.TryParse(name, true, out value)) return;
            forced = value;
            BuildExternal(new List<ApiActivity>
            {
                new ApiActivity { Id = "build", App = "PowerShell", Title = "Build", Text = "compiling", Progress = 40, IconGlyph = LocalApi.Glyph("build"), State = "running", Updated = DateTime.UtcNow },
                new ApiActivity { Id = "dl", App = "curl", Title = "Download", Text = "", Progress = 72, IconGlyph = LocalApi.Glyph("download"), State = "running", Updated = DateTime.UtcNow },
            });
            BatteryCompactText.Text = "54%";
            LevelGlyph.Text = ""; LevelFill.Width = 84.5; LevelText.Text = "65%";
            UpdateSystemPage();
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;
        }

        // Draws every state to PNG files for the README, from sample data and without touching the screen.
        public void RenderStates(string folder, string suffix)
        {
            Directory.CreateDirectory(folder);
            View[] states = { View.Idle, View.MusicCompact, View.ClaudeCompact, View.CodexCompact, View.BatteryCompact, View.ExternalCompact, View.SystemCompact, View.ExternalExpanded, View.MusicExpanded, View.ClaudeExpanded, View.TimerExpanded, View.SystemExpanded, View.Level, View.Banner };
            double[] levels = { 0.55, 0.95, 0.4, 0.75 };
            Array.Copy(levels, eqLevel, levels.Length);
            BuildExternal(new List<ApiActivity>
            {
                new ApiActivity { Id = "build", App = "PowerShell", Title = "Build", Text = "compiling", Progress = 40, IconGlyph = LocalApi.Glyph("build"), State = "running", Updated = DateTime.UtcNow },
                new ApiActivity { Id = "dl", App = "curl", Title = "Download", Text = "", Progress = 72, IconGlyph = LocalApi.Glyph("download"), State = "running", Updated = DateTime.UtcNow },
            });
            BatteryCompactText.Text = "54%";
            LevelGlyph.Text = ""; LevelFill.Width = 84.5; LevelText.Text = "65%";
            UpdateSystemPage();
            BatteryCompactText.Text = "54%";
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;

            foreach (View state in states)
            {
                forced = state;
                Recompute();
                SnapAll();
                ApplyFrame();
                bubbleShift.Snap(bubbleShift.Target);
                LayoutBars(compactBars, CompactEq.Height);
                LayoutBars(expandedBars, ExpandedEq.Height);
                CompactSpark.Thinking = ExpandedSpark.Thinking = BubbleSpark.Thinking = false;
                Root.UpdateLayout();
                UpdateProgress();
                ApplyFrame();
                Root.UpdateLayout();
                SaveSnapshot(System.IO.Path.Combine(folder, state.ToString().ToLowerInvariant() + suffix + ".png"));
            }


            // A made-up Windows notification on the same banner
            forced = View.Banner; // The view is the banner before its text changes (after the monitor looks above), as it is in use
            Recompute();
            SnapAll();
            banners.Enqueue(new BannerItem { App = "Mail", Title = Strings.SampleNotificationTitle, Subtitle = Strings.SampleNotificationBody, Seconds = 5 });
            NextBanner();
            Root.UpdateLayout();
            ApplyFrame();
            SaveSnapshot(System.IO.Path.Combine(folder, "notification" + suffix + ".png"));

            // Claude asking for you (a permission or a question), on the same banner
            banners.Enqueue(new BannerItem { FromClaude = true, Title = Strings.ClaudeNeedsYou, Subtitle = Strings.SampleQuestion, Seconds = 5 });
            NextBanner();
            Root.UpdateLayout();
            ApplyFrame();
            SaveSnapshot(System.IO.Path.Combine(folder, "banner-waiting" + suffix + ".png"));

            // What the battery alerts look like on a laptop
            foreach (string kind in new[] { "charging", "low" })
            {
                BannerItem alert = new BannerItem
                {
                    App = Installer.Name,
                    Title = kind == "low" ? Strings.BatteryLow : Strings.BatteryCharging,
                    Side = kind == "low" ? "18%" : "54%",
                    Seconds = 5,
                };
                ApplyKind(alert, kind);
                banners.Enqueue(alert);
                NextBanner();
                SnapAll();
                Root.UpdateLayout();
                ApplyFrame();
                SaveSnapshot(System.IO.Path.Combine(folder, "battery-" + kind + suffix + ".png"));
            }

            // The limit alerts
            {
                DateTime fiveReset = DateTime.Now.AddHours(2.5), weekReset = DateTime.Now.Date.AddDays(3).AddHours(9);
                (string key, string title, string sub, string side, Brush color)[] specs =
                {
                    ("75", Strings.LimitFiveHour, Strings.Resets(fiveReset), "75%", RingBrush(75)),
                    ("90", Strings.LimitFiveHour, Strings.Resets(fiveReset), "90%", RingBrush(90)),
                    ("100", Strings.LimitReachedTitle, Strings.WindowFiveHour + " · " + Strings.Resets(fiveReset), "100%", RingBrush(100)),
                    ("week90", Strings.LimitWeekly, Strings.Resets(weekReset), "90%", RingBrush(90)),
                    ("reset5", Strings.LimitRenewed(Strings.LimitFiveHour), Strings.LimitFull, "0%", Green),
                    ("resetweek", Strings.LimitRenewed(Strings.LimitWeekly), Strings.LimitFull, "0%", Green),
                };
                foreach (var spec in specs)
                {
                    banners.Enqueue(new BannerItem { FromClaude = true, Title = spec.title, Subtitle = spec.sub, Side = spec.side, GlyphBrush = spec.color, Seconds = 5 });
                    NextBanner();
                    SnapAll();
                    Root.UpdateLayout();
                    ApplyFrame();
                    SaveSnapshot(System.IO.Path.Combine(folder, "usage-" + spec.key + suffix + ".png"));
                }
            }

            // Several chats at once, and a chat that waits for you
            string[] chatNames = { "chats-working", "chats-waiting" };
            for (int i = 0; i < chatNames.Length; i++)
            {
                claude.UseSampleChats(i);
                lastCompactText = null; lastStatus = null;
                foreach (View chatView in new[] { View.ClaudeCompact, View.ClaudeExpanded })
                {
                    forced = chatView;
                    UpdateClaude();
                    UpdateClaudeLive();
                    Recompute();
                    SnapAll();
                    ApplyFrame();
                    CompactSpark.Thinking = ExpandedSpark.Thinking = BubbleSpark.Thinking = false;
                    Root.UpdateLayout();
                    ApplyFrame();
                    Root.UpdateLayout();
                    SaveSnapshot(System.IO.Path.Combine(folder, chatNames[i] + (chatView == View.ClaudeCompact ? "-compact" : "-expanded") + suffix + ".png"));
                }
            }
            // The other two looks of the system monitor
            foreach (string style in new[] { "Bars", "Numbers" })
            {
                settings.MonitorStyle = style;
                forced = View.SystemCompact;
                UpdateSystemMonitor();
                Recompute();
                SnapAll();
                ApplyFrame();
                Root.UpdateLayout();
                ApplyFrame();
                Root.UpdateLayout();
                SaveSnapshot(System.IO.Path.Combine(folder, "systemcompact-" + style.ToLowerInvariant() + suffix + ".png"));
            }
            settings.MonitorStyle = "Rings";
            UpdateSystemMonitor();

            // A made-up permission request with Allow and Deny
            settings.PermissionPrompts = true;
            ApiPermission sample = new ApiPermission { Tool = "mcp__supabase__apply_migration", Summary = "create table invoices (id uuid primary key default gen_random_uuid(), amount numeric not null, paid boolean default false)" };
            sample.Closed += () => { };
            ShowPermission(sample);
            forced = View.Permission;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "permission" + suffix + ".png"));
            sample.Cancel();
            settings.PermissionPrompts = false;
        }

        void SnapAll()
        {
            foreach (Spring spring in new[] { width, height, shown, stateScale, bubbleScale, bubbleOpacity, privacyOpacity, sessionArc, weeklyArc })
                spring.Snap(spring.Target);
            foreach (Spring spring in presence.Values) spring.Snap(spring.Target);
        }

        // Records the README animation: the island going through its states, one PNG per frame, from sample data.
        public void RenderAnimation(string folder)
        {
            Directory.CreateDirectory(folder);
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;
            const int fps = 20, substeps = 3;
            (View view, double seconds)[] script =
            {
                (View.Idle, 0.8), (View.MusicCompact, 2.2), (View.MusicExpanded, 2.4), (View.ClaudeExpanded, 2.6),
                (View.TimerExpanded, 1.8), (View.ClaudeCompact, 2.4), (View.Banner, 2.4), (View.Idle, 1.0),
            };
            forced = View.Idle;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            int frame = 0;
            foreach ((View view, double seconds) step in script)
            {
                forced = step.view;
                Recompute();
                int count = (int)Math.Round(step.seconds * fps);
                for (int i = 0; i < count; i++)
                {
                    for (int k = 0; k < substeps; k++)
                    {
                        bool moving, continuous;
                        Advance(1.0 / (fps * substeps), out moving, out continuous);
                    }
                    Root.UpdateLayout();
                    SaveFrame(System.IO.Path.Combine(folder, "frame" + frame++.ToString("0000") + ".png"));
                }
            }
            StopLoop();
        }

        // A fixed window around the island, so every frame has the same size.
        void SaveFrame(string file)
        {
            const double scale = 1.25, width = 460, height = 300;
            RenderTargetBitmap full = new RenderTargetBitmap((int)(Root.ActualWidth * scale), (int)(Root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            full.Render(Root);
            Int32Rect crop = new Int32Rect((int)((Root.ActualWidth - width) / 2 * scale), 0, (int)(width * scale), (int)(height * scale));
            DrawingVisual card = new DrawingVisual();
            using (DrawingContext context = card.RenderOpen())
            {
                Rect area = new Rect(0, 0, crop.Width, crop.Height);
                context.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xEE, 0xEC, 0xF6), Color.FromRgb(0xD5, 0xD9, 0xEA), 90), null, area);
                context.DrawImage(new CroppedBitmap(full, crop), area);
            }
            RenderTargetBitmap output = new RenderTargetBitmap(crop.Width, crop.Height, 96, 96, PixelFormats.Pbgra32);
            output.Render(card);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using (FileStream stream = File.Create(file)) encoder.Save(stream);
        }

        void SaveSnapshot(string file)
        {
            const double scale = 2, padding = 28;
            Rect bounds = Pill.TransformToAncestor(Root).TransformBounds(new Rect(Pill.RenderSize));
            if (Bubble.Opacity > 0.01)
                bounds.Union(Bubble.TransformToAncestor(Root).TransformBounds(new Rect(Bubble.RenderSize)));
            bounds.Inflate(padding + 12, padding);
            bounds.Intersect(new Rect(Root.RenderSize));

            RenderTargetBitmap full = new RenderTargetBitmap((int)(Root.ActualWidth * scale), (int)(Root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            full.Render(Root);
            Int32Rect crop = new Int32Rect((int)(bounds.X * scale), (int)(bounds.Y * scale), (int)(bounds.Width * scale), (int)(bounds.Height * scale));

            // A soft wallpaper-like card behind the island, so it reads on both light and dark GitHub themes.
            DrawingVisual card = new DrawingVisual();
            using (DrawingContext context = card.RenderOpen())
            {
                Rect area = new Rect(0, 0, crop.Width, crop.Height);
                LinearGradientBrush wallpaper = new LinearGradientBrush(Color.FromRgb(0xEE, 0xEC, 0xF6), Color.FromRgb(0xD5, 0xD9, 0xEA), 90);
                context.DrawRoundedRectangle(wallpaper, null, area, 18 * scale, 18 * scale);
                context.DrawImage(new CroppedBitmap(full, crop), area);
            }
            RenderTargetBitmap output = new RenderTargetBitmap(crop.Width, crop.Height, 96, 96, PixelFormats.Pbgra32);
            output.Render(card);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using (FileStream stream = File.Create(file)) encoder.Save(stream);
        }

        // Fixed texts, again after the language changes.
        void ApplyTexts()
        {
            SessionLabel.Text = Strings.Session5h;
            WeeklyLabel.Text = Strings.Weekly;
            CodexWeeklyLabel.Text = Strings.Weekly;
            BtnStartText.Text = Strings.TimerStart;
            BtnWatchText.Text = Strings.MenuStopwatch;
            BtnStopText.Text = Strings.MenuStopTimer;
        }

        // Alerts come in two sizes; the content is scaled as a whole, so nothing else changes.
        void ApplyBannerSize()
        {
            double k = string.Equals(settings.BannerSize, "Normal", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.8;
            Banner.Width = 352 * k;
            Banner.Height = 74 * k;
        }

        public void SettingsChanged()
        {
            ApplyTexts();
            ApplyBannerSize();
            UpdateTimerPage();
            UpdatePrivacy();
            UpdateCodex();
            UpdateSystemMonitor();
            PlaceWindow();
            UpdateMusic();
            UpdateClaude();
            Recompute();
            ApplyTargets(); // Sizes may have changed even when the state did not
            StartLoop();
        }

        // The keyboard shortcut: opens the island, or closes it again when it is already open.
        internal void ToggleOpen()
        {
            if (hovering || DateTime.Now < peekUntil)
            {
                peekUntil = DateTime.MinValue;
                SetHovering(false);
            }
            else
            {
                lastActive = DateTime.Now; // Also brings back an auto-hidden island
                tucked = false;
                SetHovering(true);
            }
            Recompute();
        }

        // Opens the island for a moment, so a change to the expanded size can be seen while it is made.
        internal void PeekExpanded()
        {
            peekUntil = DateTime.Now.AddSeconds(1.8);
            Recompute();
        }

        // ---------- State ----------

        void Recompute()
        {
            bool music = MusicActive;
            bool working = ClaudeActive;

            // Priority: Claude waiting for you, then music, then Claude working, then Codex working.
            List<Activity> active = new List<Activity>();
            if (working && claude.Waiting > 0) active.Add(Activity.Claude);
            if (music) active.Add(Activity.Music);
            if (working && claude.Waiting == 0) active.Add(Activity.Claude);
            if (CodexActive) active.Add(Activity.Codex);
            if (ApiActive) active.Add(Activity.External);
            if (BatteryActive) active.Add(Activity.Battery);
            if (timer.Running) active.Insert(working && claude.Waiting > 0 ? 1 : 0, Activity.Timer);
            Activity primary = active.Count > 0 ? active[0] : Activity.None;
            lastPrimary = primary;
            if (tucked)
            {
                // Something new brings the island back: another activity, an alert or a note, or a microphone or camera in use
                if (primary != tuckPrimary) { if (primary != Activity.None) tucked = false; else tuckPrimary = Activity.None; }
                if (DateTime.Now < bannerUntil || DateTime.Now < toastUntil) tucked = false;
                if (PrivacyOn && !tuckPrivacy) tucked = false;
                tuckPrivacy = PrivacyOn;
            }
            Activity second = active.Count > 1 ? active[1] : Activity.None;

            View next;
            if (forced.HasValue) next = forced.Value;
            else if (fullscreen && settings.HideInFullscreen) next = View.Hidden;
            else if (permission != null) next = View.Permission; // A question that waits for a click comes first
            else if (tucked) next = View.Hidden;
            else if (hovering || DateTime.Now < peekUntil)
                next = PickedPage() ?? DefaultPage(primary);
            else if (DateTime.Now < bannerUntil) next = View.Banner;
            else if (DateTime.Now < toastUntil) next = View.Toast;
            else if (DateTime.Now < levelUntil) next = View.Level;
            // The same thin line for the activities too, when asked: hover to see them
            else if (DateTime.Now < musicPeekUntil && ThinActive) next = View.MusicPeek;
            else if (ThinActive) next = View.Minimal;
            // With the system monitor on, it stays and the activity is the small bubble beside it
            else if (KeepsMonitor(primary)) next = View.SystemCompact;
            // Empty island: the collapsed island stays a plain pill. Activities only show when it expands.
            else if (primary == Activity.Music && !settings.EmptyIsland) next = View.MusicCompact;
            else if (primary == Activity.Claude && !settings.EmptyIsland) next = View.ClaudeCompact;
            else if (primary == Activity.Codex && !settings.EmptyIsland) next = View.CodexCompact;
            else if (primary == Activity.External && !settings.EmptyIsland) next = View.ExternalCompact;
            else if (primary == Activity.Battery && !settings.EmptyIsland) next = View.BatteryCompact;
            else if (primary == Activity.Timer) next = View.TimerCompact;
            else if (maximized && settings.MaximizedMode == "Hidden") next = View.Hidden; // A game or a video that fills the screen
            else if (settings.SystemMonitor && maximized && !IsBottom && settings.MaximizedMode == "Thin") next = View.Minimal;
            else if (settings.SystemMonitor) next = View.SystemCompact;
            else if (!settings.ShowIdlePill && !PrivacyOn && DateTime.Now - discordChangedAt > TimeSpan.FromSeconds(4)) next = View.Hidden;
            else if (maximized && !IsBottom && settings.MaximizedMode == "Thin") next = View.Minimal; // Stay out of the way of tab strips
            else if (settings.IdleStyle == "Line") next = View.Minimal; // The thin line as the idle look
            else next = View.Idle;

            // Auto-hide: an island that has shown nothing for a while steps away until something happens
            // or the mouse comes to its spot.
            bool idle = next == View.Idle || next == View.Minimal;
            if (!idle || hovering || PrivacyOn) lastActive = DateTime.Now;
            autoHidden = idle && !forced.HasValue && settings.AutoHideSeconds > 0 &&
                         DateTime.Now - lastActive > TimeSpan.FromSeconds(settings.AutoHideSeconds);
            if (autoHidden) next = View.Hidden;
            bool watchMouse = autoHidden || tucked;
            if (watchMouse != revealTimer.IsEnabled) revealTimer.IsEnabled = watchMouse;

            Activity shownActivity = next == View.MusicCompact ? Activity.Music
                                   : next == View.ClaudeCompact ? Activity.Claude
                                   : next == View.CodexCompact ? Activity.Codex
                                   : next == View.TimerCompact ? Activity.Timer
                                   : next == View.BatteryCompact ? Activity.Battery
                                   : next == View.ExternalCompact ? Activity.External
                                   : Activity.None;
            Activity other = primary == shownActivity ? second : primary;
            Activity nextBubble = shownActivity != Activity.None && other != shownActivity ? other : Activity.None;
            if (next == View.SystemCompact && KeepsMonitor(primary)) nextBubble = primary; // The monitor keeps its place, the activity is the bubble
            bool minimalChanged = next == View.Minimal ? UpdateMinimal(primary) : ClearMinimal();
            if (next != view || nextBubble != bubble || minimalChanged)
            {
                view = next;
                bubble = nextBubble;
                ApplyTargets();
            }
            StartLoop();
        }

        // ---------- The thin line's content: the state that matters most, as one tiny icon and a rail ----------

        enum Thin { None, Music, Claude, ClaudeWaiting, Timer, External, Battery, Focus }
        Thin minimalKind = Thin.None;
        bool minimalHasContent;
        string minimalSig = "";
        static readonly FontFamily FluentIcons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");
        static readonly Brush ThinPink = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x37, 0x5F)));
        static readonly Brush ThinClay = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57)));
        static readonly Brush ThinYellow = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x0A)));
        static readonly Brush ThinMoon = Freeze(new SolidColorBrush(Color.FromRgb(0xB4, 0xB4, 0xFF)));

        bool ClearMinimal()
        {
            bool changed = minimalHasContent;
            minimalHasContent = false;
            minimalKind = Thin.None;
            minimalSig = "";
            return changed;
        }

        // Returns true when the line has to change size because content came or went.
        bool UpdateMinimal(Activity primary)
        {
            Thin kind = Thin.None;
            string glyph = null;
            FontFamily font = FluentIcons;
            Brush brush = White;
            bool track = false;
            double fill = 0;

            if (primary == Activity.Claude)
            {
                kind = claude.Waiting > 0 ? Thin.ClaudeWaiting : Thin.Claude;
                glyph = "\u2731";
                font = SymbolFont;
                int percent = claude.Session != null && claude.Session.Percent.HasValue ? claude.Session.EffectivePercent : 0;
                brush = percent >= 90 ? ApiRed : percent >= 75 ? ThinYellow : ThinClay; // The limit tints the spark
            }
            else if (primary == Activity.Music)
            {
                kind = Thin.Music;
                glyph = "\u266B";
                font = SymbolFont;
                brush = ThinPink;
                track = settings.MinimalMusicPosition;
            }
            else if (primary == Activity.Timer)
            {
                kind = Thin.Timer;
                glyph = "\uE916";
                brush = ThinYellow;
                track = timer.Mode == TimerService.Kind.Countdown;
            }
            else if (primary == Activity.External && api != null && api.Activities.Count > 0)
            {
                ApiActivity first = api.Activities.OrderByDescending(a => a.Updated).First();
                kind = Thin.External;
                glyph = first.State == "done" ? "\uE73E" : first.IconGlyph;
                brush = StateBrush(first.State);
                track = first.State == "running" && first.Progress >= 0;
                fill = first.Progress / 100.0;
            }
            else if (primary == Activity.Battery && battery != null)
            {
                kind = Thin.Battery;
                glyph = "\uE83E";
                brush = ApiGreen;
                track = true;
                fill = battery.Percent / 100.0;
            }
            else if (settings.ShowFocus && focusState != null && focusState.On)
            {
                kind = Thin.Focus;
                glyph = "\uE708";
                brush = ThinMoon;
            }

            minimalKind = kind;
            MinimalGlyph.Visibility = glyph != null ? Visibility.Visible : Visibility.Collapsed;
            if (glyph != null)
            {
                MinimalGlyph.Text = glyph;
                MinimalGlyph.FontFamily = font;
                MinimalGlyph.Foreground = brush;
            }
            MinimalTrack.Visibility = track ? Visibility.Visible : Visibility.Collapsed;
            MinimalFill.Background = brush;
            MinimalFill.Width = 60 * Math.Clamp(fill, 0, 1);

            bool has = glyph != null;
            string sig = kind + "|" + (glyph ?? "") + "|" + track;
            bool changed = has != minimalHasContent || sig != minimalSig;
            minimalHasContent = has;
            minimalSig = sig;
            return changed;
        }

        // The parts of the line that move: the rail of the timer, the song and the breathing of the spark. True while they must.
        bool AnimateMinimal()
        {
            if (!minimalHasContent) return false;
            switch (minimalKind)
            {
                case Thin.Claude:
                    MinimalGlyph.Opacity = 0.7 + 0.3 * (0.5 + 0.5 * Math.Cos(clock * 2.4));
                    return true;
                case Thin.ClaudeWaiting:
                    MinimalGlyph.Opacity = 0.3 + 0.7 * (0.5 + 0.5 * Math.Cos(clock * 4.5));
                    return true;
                case Thin.Timer:
                    MinimalGlyph.Opacity = 1;
                    MinimalFill.Width = 60 * timer.Fraction;
                    return timer.Running && !timer.Paused;
                case Thin.Music:
                    MinimalGlyph.Opacity = media.IsPlaying ? 1 : 0.5;
                    if (settings.MinimalMusicPosition && media.Duration > TimeSpan.Zero)
                        MinimalFill.Width = 60 * Math.Clamp(media.Position.TotalSeconds / media.Duration.TotalSeconds, 0, 1);
                    return media.IsPlaying && settings.MinimalMusicPosition;
                default:
                    MinimalGlyph.Opacity = 1;
                    return false;
            }
        }

        // The monitor is not covered by an activity while "keep the monitor" is on (and the island is not an empty pill).
        bool KeepsMonitor(Activity primary)
        {
            return settings.SystemMonitor && settings.MonitorKeepsPlace && !settings.EmptyIsland && primary != Activity.None && primary != Activity.System;
        }

        void ApplyTargets()
        {
            UpdateDots();
            Bubble.IsHitTestVisible = bubble != Activity.None && view != View.Hidden; // The small second activity can be pointed at
            Size size = SizeOf(view);
            width.Target = size.Width;
            height.Target = size.Height;
            shown.Target = view == View.Hidden ? 0 : 1;
            stateScale.Target = IsExpandedView(view) ? settings.ExpandedScale : settings.CollapsedScale;
            Pill.IsHitTestVisible = view != View.Hidden;
            if (view == View.SystemCompact)
            {
                if (!monitorTimer.IsEnabled) monitorTimer.Start();
                UpdateSystemMonitor();
            }
            else monitorTimer.Stop();
            if (view == View.ClaudeExpanded && forced == null) claude.RefreshUsage();

            foreach (KeyValuePair<View, FrameworkElement> pair in panels)
            {
                Spring spring = presence[pair.Value];
                bool active = pair.Key == view;
                spring.Target = active ? 1 : 0;
                // The outgoing content clears quickly so it never fights with the incoming one.
                if (active) spring.Tune(240, 0.85);
                else spring.Tune(900, 1);
            }

            bool hasBubble = bubble != Activity.None;
            if (hasBubble)
            {
                BubbleSpark.Visibility = bubble == Activity.Claude ? Visibility.Visible : Visibility.Collapsed;
                BubbleArt.Visibility = bubble == Activity.Music ? Visibility.Visible : Visibility.Collapsed;
                BubbleCodex.Visibility = bubble == Activity.Codex ? Visibility.Visible : Visibility.Collapsed;
                BubbleTimer.Visibility = bubble == Activity.Timer ? Visibility.Visible : Visibility.Collapsed;
                BubbleBattery.Visibility = bubble == Activity.Battery ? Visibility.Visible : Visibility.Collapsed;
                BubbleExternalGroup.Visibility = bubble == Activity.External ? Visibility.Visible : Visibility.Collapsed;
            }
            bubbleOpacity.Target = hasBubble ? 1 : 0;
            bubbleScale.Target = hasBubble ? 1 : 0.4;
        }

        // ---------- Pages ----------

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
            return activity == Activity.Music ? View.MusicExpanded : activity == Activity.Timer ? View.TimerExpanded : activity == Activity.System ? View.SystemExpanded : activity == Activity.External ? View.ExternalExpanded : View.ClaudeExpanded;
        }

        static Activity ActivityOf(View value)
        {
            return value == View.MusicExpanded ? Activity.Music : value == View.TimerExpanded ? Activity.Timer : value == View.SystemExpanded ? Activity.System : value == View.ExternalExpanded ? Activity.External : Activity.Claude;
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
            if (view == View.Banner)
            {
                bool fromClaude = currentBanner == null || currentBanner.FromClaude || currentBanner.FromCodex;
                bannerUntil = DateTime.MinValue;
                if (banners.Count > 0) { NextBanner(); return; }
                if (!fromClaude) { Recompute(); return; } // A notification click just dismisses it
                SetHovering(true);
                return;
            }
            SetHovering(!hovering);
        }

        // ---------- Banners ----------

        sealed class BannerItem
        {
            public bool FromClaude;
            public bool FromCodex;
            public string Side;        // A large value shown on the right
            public string Glyph;       // A Segoe icon instead of the bell, such as for the battery
            public Brush GlyphBrush;
            public ImageSource Icon;   // The sending app's own icon, for Windows notifications
            public string App;
            public string Title;
            public string Subtitle;
            public double Seconds;
        }

        readonly Queue<BannerItem> banners = new Queue<BannerItem>();
        BannerItem currentBanner;

        void ShowBanner(bool waiting, string name)
        {
            if (!settings.ShowClaude || !settings.ClaudeBanners) return;
            Enqueue(new BannerItem
            {
                FromClaude = true,
                Title = waiting ? Strings.ClaudeNeedsYou : Strings.ClaudeDone,
                Subtitle = waiting && !string.IsNullOrWhiteSpace(claude.LastQuestion) ? claude.LastQuestion : name,
                Seconds = settings.AlertSeconds + (waiting ? 1 : 0), // A question waits a little longer
            });
        }

        internal void ShowNotification(IslandNotification notification)
        {
            if (!settings.ShowNotifications) return;
            // The island already announces Claude itself, so the Claude app's own toasts would repeat it.
            if (settings.ShowClaude && settings.ClaudeBanners && notification.App == "Claude") return;
            Enqueue(new BannerItem
            {
                App = notification.App,
                Icon = notification.Icon,
                Title = notification.Title,
                Subtitle = notification.Body,
                Seconds = settings.NotificationSeconds,
            });
        }

        // Messages from the app itself, such as update results. Not gated by the notifications option.
        DateTime toastUntil;

        // The volume or screen brightness while it is being changed.
        DateTime levelUntil;
        internal void ShowLevel(LevelService.Kind kind, int percent, bool muted)
        {
            if (!settings.ShowLevels) return;
            if (fullscreen && settings.HideInFullscreen) return;
            percent = Math.Clamp(percent, 0, 100);
            LevelGlyph.Text = kind == LevelService.Kind.Brightness ? "\uE706" : muted || percent == 0 ? "\uE74F" : percent < 34 ? "\uE992" : percent < 67 ? "\uE993" : "\uE767";
            // The bar glides to each new level instead of jumping, so a held key looks smooth
            double fill = Math.Max(0, 130.0 * (muted ? 0 : percent) / 100);
            if (view == View.Level && !rendering)
                LevelFill.BeginAnimation(FrameworkElement.WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(fill, TimeSpan.FromMilliseconds(90)));
            else
            {
                LevelFill.BeginAnimation(FrameworkElement.WidthProperty, null);
                LevelFill.Width = fill;
            }
            LevelText.Text = muted ? "" : percent + "%";
            levelUntil = DateTime.Now.AddSeconds(1.4);
            Recompute();
        }

        // A short note that never queues: a new one just restarts the time.
        internal void ShowToast(string text, string glyph, double seconds)
        {
            ToastText.Text = text;
            ToastGlyph.Text = glyph;
            toastUntil = DateTime.Now.AddSeconds(seconds);
            Recompute();
        }

        // ---------- Permissions asked by Claude Code (through its hook) ----------

        static readonly Brush SupabaseTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0x3E, 0xCF, 0x8E)));
        static readonly Brush AmberTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x9F, 0x0A)));
        Border permissionPressed;
        DateTime permissionArmedAt;
        DispatcherTimer permissionArmTimer;
        double permissionHeight = 138;

        void AnswerPermission(Border button, string decision, MouseButtonEventArgs e)
        {
            e.Handled = true;
            bool pressedHere = permissionPressed == button;
            permissionPressed = null;
            button.ReleaseMouseCapture();
            if (!pressedHere || permission == null || DateTime.Now < permissionArmedAt) return;
            Point at = e.GetPosition(button);
            if (at.X < 0 || at.Y < 0 || at.X > button.ActualWidth || at.Y > button.ActualHeight) return; // Released somewhere else
            permission.Resolve(decision);
        }

        ApiPermission permission;
        readonly Queue<ApiPermission> permissionQueue = new Queue<ApiPermission>();

        internal void ShowPermission(ApiPermission request)
        {
            // Not shown at all (off, or hidden for a full-screen window): no decision, so Claude asks as usual
            if (!settings.PermissionPrompts || (fullscreen && settings.HideInFullscreen)) { request.Resolve("none"); return; }
            request.Closed += () => OnPermissionClosed(request);
            permissionQueue.Enqueue(request);
            if (permission == null) NextPermission();
            else PermQueue.Text = PendingCount() > 0 ? "+" + PendingCount() : "";
        }

        int PendingCount()
        {
            int count = 0;
            foreach (ApiPermission p in permissionQueue) if (!p.IsClosed) count++;
            return count;
        }

        // The island is going away: whatever waits for an answer gets none.
        void CancelPermissions()
        {
            foreach (ApiPermission p in permissionQueue.ToArray()) p.Cancel();
            permissionQueue.Clear();
            if (permission != null) permission.Cancel();
        }

        void NextPermission()
        {
            permission = null;
            while (permissionQueue.Count > 0 && permission == null)
            {
                ApiPermission next = permissionQueue.Dequeue();
                if (!next.IsClosed) permission = next;
            }
            if (permission != null)
            {
                PermTitle.Text = Strings.PermissionAsks;
                PermTool.Text = ToolName(permission.Tool) + (string.IsNullOrEmpty(permission.Project) ? "" : " \u00B7 " + permission.Project);
                bool supabase = permission.Tool != null && permission.Tool.StartsWith("mcp__", StringComparison.Ordinal) &&
                                permission.Tool.IndexOf("supabase", StringComparison.OrdinalIgnoreCase) >= 0;
                PermSupabase.Visibility = supabase ? Visibility.Visible : Visibility.Collapsed;
                PermLock.Visibility = supabase ? Visibility.Collapsed : Visibility.Visible;
                PermIconBack.Background = supabase ? SupabaseTint : AmberTint;
                PermSummary.Text = permission.Summary;
                PermQueue.Text = PendingCount() > 0 ? "+" + PendingCount() : "";
                // A summary that is cut, or is only the raw arguments, is not enough to approve on: Deny stays, Allow is replaced by a link to Claude
                PermAllow.Visibility = permission.Partial ? Visibility.Collapsed : Visibility.Visible;
                PermOpen.Visibility = permission.Partial ? Visibility.Visible : Visibility.Collapsed;
                PermOpenText.Text = Strings.OpenClaude;
                PermCheck.Text = permission.Partial ? Strings.PermissionPartial : Strings.PermissionCheck;
                int lines = Math.Clamp((permission.Summary.Length + 51) / 52, 2, 6);
                permissionHeight = 138 + (lines - 2) * 15;
                PermissionPanel.Height = permissionHeight;
                PermSummary.MaxHeight = lines * 15 + 4;
                // The buttons wake up a moment after the prompt shows
                permissionArmedAt = DateTime.Now.AddMilliseconds(650);
                PermAllow.Opacity = PermDeny.Opacity = 0.45;
                if (permissionArmTimer == null)
                {
                    permissionArmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(660) };
                    permissionArmTimer.Tick += (s, e) => { permissionArmTimer.Stop(); PermAllow.Opacity = PermDeny.Opacity = 1; };
                }
                permissionArmTimer.Stop();
                permissionArmTimer.Start();
                PermAllowText.Text = Strings.Allow;
                PermDenyText.Text = Strings.Deny;
                tucked = false;
                lastActive = DateTime.Now;
            }
            Recompute();
        }

        void OnPermissionClosed(ApiPermission request)
        {
            if (permission == request) NextPermission();
        }

        // "mcp__supabase__execute_sql" -> "supabase · execute_sql"
        static string ToolName(string tool)
        {
            if (tool != null && tool.StartsWith("mcp__", StringComparison.Ordinal))
            {
                string[] parts = tool.Substring(5).Split(new[] { "__" }, 2, StringSplitOptions.None);
                return parts.Length == 2 ? parts[0] + " \u00B7 " + parts[1] : parts[0];
            }
            return tool ?? "";
        }

        // The Claude 5-hour or weekly limit reached a level, or renewed (level 0).
        internal void ShowLimitAlert(bool weekly, int level, int percent, DateTime? resets)
        {
            if (!settings.ShowClaude || !settings.ShowLimitAlerts) return;
            if (level == 0 ? !settings.LimitRenewed : level >= 100 ? !settings.LimitAt100 : level >= 90 ? !settings.LimitAt90 : !settings.LimitAt75) return;
            string window = weekly ? Strings.LimitWeekly : Strings.LimitFiveHour;
            BannerItem item = new BannerItem { FromClaude = true, Seconds = settings.LimitAlertSeconds, Side = percent + "%" };
            if (level == 0)
            {
                item.Title = Strings.LimitRenewed(window);
                item.Subtitle = Strings.LimitFull;
                item.GlyphBrush = Green;
            }
            else if (level >= 100)
            {
                item.Title = Strings.LimitReachedTitle;
                item.Subtitle = (weekly ? Strings.WindowWeekly : Strings.WindowFiveHour) + " · " + Strings.Resets(resets);
                item.GlyphBrush = RingBrush(100);
            }
            else
            {
                item.Title = window;
                item.Subtitle = Strings.Resets(resets);
                item.GlyphBrush = RingBrush(level);
            }
            Enqueue(item);
        }

        internal void ShowSystemBanner(string title, string body, string kind = null)
        {
            BannerItem item = new BannerItem { App = Installer.Name, Title = title, Subtitle = body, Seconds = settings.AlertSeconds };
            ApplyKind(item, kind);
            if (kind != null) { item.Side = item.Subtitle; item.Subtitle = null; } // Battery: the percentage goes big on the right
            Enqueue(item);
        }

        // Battery alerts carry an icon of their own.
        static void ApplyKind(BannerItem item, string kind)
        {
            switch (kind)
            {
                case "charging": item.Glyph = "\uE83E"; item.GlyphBrush = Green; break;
                case "full": item.Glyph = "\uE83F"; item.GlyphBrush = Green; break;
                case "low": item.Glyph = "\uE850"; item.GlyphBrush = Red; break;
                case "unplugged": item.Glyph = "\uE83F"; break;
            }
        }

        // True while the user is looking at or using the island, so an update restart waits.
        internal bool IsBusy
        {
            get { return hovering || view == View.Banner || view == View.Permission || view == View.MusicExpanded || view == View.ClaudeExpanded || view == View.TimerExpanded || view == View.SystemExpanded || view == View.ExternalExpanded; }
        }

        void Enqueue(BannerItem item)
        {
            if (fullscreen && settings.HideInFullscreen) return;
            if (banners.Count >= 5) banners.Dequeue(); // A burst of notifications never piles up for minutes
            banners.Enqueue(item);
            if (DateTime.Now >= bannerUntil) NextBanner();
        }

        void NextBanner()
        {
            if (banners.Count == 0)
            {
                currentBanner = null;
                return;
            }
            BannerItem item = banners.Dequeue();
            currentBanner = item;

            BannerSpark.Visibility = item.FromClaude ? Visibility.Visible : Visibility.Collapsed;
            BannerCodex.Visibility = item.FromCodex ? Visibility.Visible : Visibility.Collapsed;
            bool glyph = item.Glyph != null;
            BannerGlyph.Visibility = glyph ? Visibility.Visible : Visibility.Collapsed;
            if (glyph)
            {
                BannerGlyph.Text = item.Glyph;
                BannerGlyph.Foreground = item.GlyphBrush ?? White;
            }
            bool appIcon = item.Icon != null && !item.FromClaude && !item.FromCodex && !glyph;
            BannerAppIcon.Visibility = appIcon ? Visibility.Visible : Visibility.Collapsed;
            BannerAppIcon.Source = appIcon ? item.Icon : null;
            BannerBell.Visibility = item.FromClaude || item.FromCodex || glyph || appIcon ? Visibility.Collapsed : Visibility.Visible;
            // An app or site icon sits straight on the island (no gray tile behind it), with softly rounded corners
            BannerIcon.Background = item.FromClaude ? ClaudeTint : item.FromCodex ? CodexTint : appIcon ? Brushes.Transparent : NotificationTint;
            BannerAppIcon.Clip = new RectangleGeometry(new Rect(0, 0, 30, 30), 7, 7);
            BannerApp.Text = item.App ?? "";
            BannerApp.Visibility = item.FromClaude || string.IsNullOrEmpty(item.App) ? Visibility.Collapsed : Visibility.Visible;
            BannerTitle.Text = item.Title;
            BannerSubtitle.Text = item.Subtitle ?? "";
            BannerSubtitle.Visibility = string.IsNullOrEmpty(item.Subtitle) ? Visibility.Collapsed : Visibility.Visible;
            BannerSide.Text = item.Side ?? "";
            BannerSide.Foreground = item.GlyphBrush ?? White;
            BannerSide.Visibility = string.IsNullOrEmpty(item.Side) ? Visibility.Collapsed : Visibility.Visible;

            bannerUntil = DateTime.Now.AddSeconds(item.Seconds);
            Recompute();
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

        void Poll()
        {
            ScreenState state = Native.GetScreenState(hwnd);
            fullscreen = state.Fullscreen;
            maximized = state.Maximized;
            if (view != View.Hidden) Native.KeepTopmost(hwnd);
            if (DateTime.Now >= bannerUntil && banners.Count > 0 && !hovering) NextBanner();
            Recompute();
        }

        static bool IsExpandedView(View value)
        {
            return value == View.MusicExpanded || value == View.ClaudeExpanded || value == View.TimerExpanded || value == View.SystemExpanded || value == View.ExternalExpanded || value == View.Banner || value == View.Permission;
        }

        void PlaceWindow()
        {
            Display display = Target ?? Displays.Primary();
            Rect screen = display.PxBounds;
            Rect work = display.PxWorkArea;
            double scale = display.Scale;
            bool bottom = IsBottom;

            // Room for the largest state at the user's sizes, plus the bubble and the shadow (all in DIPs).
            double largest = Math.Max(settings.ExpandedScale, settings.CollapsedScale);
            double wantWidth = Math.Max(520, 372 * largest + 140);
            double tallest = Math.Max(Math.Max(Math.Max(Sizes[View.MusicExpanded].Height, Sizes[View.TimerExpanded].Height), Sizes[View.SystemExpanded].Height), ClaudePanelHeight);
            double wantHeight = Math.Max(tallest * settings.ExpandedScale, 34 * settings.CollapsedScale) + settings.OffsetY + 44;

            // Positions are physical pixels so each monitor keeps its own scale: first step onto the monitor
            // (which switches the window's DPI), then size in DIPs, then settle the exact position.
            double pxWidth = Math.Round(wantWidth * scale), pxHeight = Math.Round(wantHeight * scale);
            double pxLeft = Math.Clamp(screen.Left + (screen.Width - pxWidth) / 2 + settings.OffsetX * scale, screen.Left, Math.Max(screen.Left, screen.Right - pxWidth));
            double pxTop = bottom ? work.Bottom - pxHeight : work.Top;
            Native.MoveTo(hwnd, (int)Math.Round(pxLeft), (int)Math.Round(pxTop));
            Width = wantWidth;
            Height = wantHeight;
            Native.MoveTo(hwnd, (int)Math.Round(pxLeft), (int)Math.Round(pxTop));

            VerticalAlignment align = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            Thickness margin = bottom ? new Thickness(0, 0, 0, settings.OffsetY) : new Thickness(0, settings.OffsetY, 0, 0);
            Pill.VerticalAlignment = align;
            Bubble.VerticalAlignment = align;
            Pill.Margin = margin;
            Bubble.Margin = margin;
            PrivacyDots.VerticalAlignment = align;
            PrivacyDots.Margin = margin;
            Pill.RenderTransformOrigin = new Point(0.5, bottom ? 1 : 0);
            ApplyFrame();
        }

        // ---------- Content ----------

        void UpdateMusic()
        {
            TrackTitle.Text = string.IsNullOrEmpty(media.Title) ? Strings.UnknownTrack : media.Title;
            TrackArtist.Text = media.Artist;

            ImageBrush artBrush = null;
            if (media.Art != null)
            {
                artBrush = new ImageBrush(media.Art) { Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapScalingMode(artBrush, BitmapScalingMode.HighQuality); // Small icons stay smooth, not blocky
            }
            Brush art = artBrush != null ? Freeze(artBrush) : (Brush)FindResource("ArtPlaceholder");
            CompactArt.Background = art;
            ExpandedArt.Background = art;
            BubbleArt.Fill = art;
            PeekArt.Background = art;

            Brush accent = Freeze(new SolidColorBrush(media.Accent));
            foreach (Rectangle bar in compactBars) bar.Fill = accent;
            foreach (Rectangle bar in expandedBars) bar.Fill = accent;
            PlayGlyphPath.Data = (Geometry)FindResource(media.IsPlaying ? "PauseGlyph" : "PlayGlyph");
            if (presence[MusicExpanded].Value > 0.01) UpdateProgress();
        }

        void UpdateClaude()
        {
            UpdateClaudeLive();

            UsageWindow session = claude.Session;
            bool hasUsage = session != null && session.Percent.HasValue;
            UsageRow.Visibility = hasUsage ? Visibility.Visible : Visibility.Collapsed;
            NoUsageText.Visibility = hasUsage ? Visibility.Collapsed : Visibility.Visible;
            if (!hasUsage)
            {
                NoUsageText.Text = claude.ClaudeFound ? Strings.ReadingUsage : Strings.ClaudeNotFound;
                return;
            }

            UsageWindow weekly = claude.Weekly ?? new UsageWindow();
            int sessionPercent = session.EffectivePercent;
            int weeklyPercent = weekly.EffectivePercent;
            sessionArc.Target = sessionPercent;
            weeklyArc.Target = weeklyPercent;
            SessionPercent.Text = sessionPercent + "%";
            WeeklyPercent.Text = weekly.Percent.HasValue ? weeklyPercent + "%" : "–";
            SessionArc.Stroke = RingBrush(sessionPercent);
            WeeklyArc.Stroke = RingBrush(weeklyPercent);
            SessionReset.Text = Strings.Resets(session.EffectiveReset);
            WeeklyReset.Text = Strings.Resets(weekly.EffectiveReset);
            StartLoop();
        }

        // The parts that change every second while Claude works.
        void UpdateClaudeLive()
        {
            string elapsed = claude.BusySince.HasValue ? FormatTime(DateTime.Now - claude.BusySince.Value) : "";
            bool waiting = claude.Waiting > 0;

            // With several chats working, a count comes before the time so it is clear it is more than one
            int many = claude.Active.Count;
            // "1/2 · Waiting" reads as one of two chats waiting; "2 · 0:58" as two chats working
            string compact = waiting ? (many > 1 ? claude.Waiting + "/" + many + " · " : "") + Strings.Waiting
                                     : (many > 1 ? many + " · " : "") + elapsed;
            if (compact != lastCompactText)
            {
                lastCompactText = compact;
                ClaudeCompactText.Text = compact;
                ClaudeCompactText.Foreground = waiting ? Orange : White;
            }

            ClaudeTitle.Visibility = many > 1 ? Visibility.Collapsed : Visibility.Visible; // The lines take its place
            OpenClaudeLink.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed; // A question is answered in the Claude app
            if (waiting && OpenClaudeLink.Text.Length == 0) OpenClaudeLink.Text = Strings.OpenClaude;
            string status;
            if (many > 1)
            {
                // One line per chat (up to three): what it is doing and for how long
                System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                for (int i = 0; i < Math.Min(3, many); i++)
                {
                    ClaudeService.ActiveSession item = claude.Active[i];
                    lines.Add(item.Waiting ? Join(Strings.WaitingForYou, string.IsNullOrWhiteSpace(item.Question) ? item.Name : item.Name + ": " + item.Question) : Join(Strings.Working + " " + FormatTime(DateTime.Now - item.Since), item.Name));
                }
                if (many > 3) lines.Add("+" + (many - 3));
                status = string.Join(Environment.NewLine, lines);
            }
            else status = waiting ? Join(Strings.WaitingForYou, claude.WaitingName)
                          : claude.Busy > 0 ? Join(Strings.Working + " " + elapsed, claude.BusyName)
                          : claude.Running > 0 ? Strings.IdleSessions(claude.Running)
                          : Strings.NoSession;
            if (status != lastStatus)
            {
                lastStatus = status;
                ClaudeStatus.Text = status;
            }
        }

        void UpdateProgress()
        {
            TimeSpan duration = media.Duration;
            TimeSpan position = media.Position;
            double fraction = duration.TotalSeconds > 0 ? position.TotalSeconds / duration.TotalSeconds : 0;
            ProgressFill.Width = ProgressTrack.ActualWidth * Math.Clamp(fraction, 0, 1);

            string elapsed = duration > TimeSpan.Zero ? FormatTime(position) : "";
            string remaining = duration > TimeSpan.Zero ? "-" + FormatTime(duration - position) : "";
            if (elapsed != lastElapsed) { lastElapsed = elapsed; TimeElapsed.Text = elapsed; }
            if (remaining != lastRemaining) { lastRemaining = remaining; TimeRemaining.Text = remaining; }
        }

        void Seek(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            double track = ProgressTrack.ActualWidth;
            if (track <= 0 || media.Duration <= TimeSpan.Zero) return;
            double fraction = Math.Clamp(e.GetPosition(ProgressTrack).X / track, 0, 1);
            media.Seek(TimeSpan.FromTicks((long)(media.Duration.Ticks * fraction)));
            UpdateProgress();
        }

        // ---------- Animation ----------

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
            moving |= privacyShift.Step(dt);
            moving |= sessionArc.Step(dt);
            moving |= weeklyArc.Step(dt);
            foreach (Spring spring in presence.Values) moving |= spring.Step(dt);

            ApplyFrame();
            continuous = AnimateLive(dt);
        }

        void ApplyFrame()
        {
            double w = Math.Max(2, width.Value);
            double h = Math.Max(2, height.Value);
            double radius = Math.Min(h / 2, MaxRadius);
            bool bottom = IsBottom;

            Pill.Width = w;
            Pill.Height = h;
            Pill.CornerRadius = new CornerRadius(radius);
            stageClip.Rect = new Rect(0, 0, w, h);
            stageClip.RadiusX = radius;
            stageClip.RadiusY = radius;

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
                double opacity = Math.Clamp(p, 0, 1);
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
                Canvas.SetLeft(panel, (w - panel.Width) / 2);
                Canvas.SetTop(panel, bottom ? h - panel.Height : 0);

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
            bubbleShift.Target = (bubble != Activity.None ? w / 2 + BubbleGap + BubbleSize / 2 : w / 2 - BubbleSize / 2 - 2) * s;
            BubbleShift.X = bubbleShift.Value;
            BubbleShift.Y = (bottom ? -1 : 1) * BubbleSize / 2 * (s - 1); // Keeps it level with the scaled island
            BubbleScale.ScaleX = BubbleScale.ScaleY = Math.Max(0, bubbleScale.Value * s);
            Bubble.Opacity = Math.Clamp(bubbleOpacity.Value, 0, 1) * visible;

            // The microphone and camera dots stay where they are while the island is put away or auto-hidden
            // (they must never hide), and keep the idle island's spot instead of sliding with its shrinking width.
            bool away = view == View.Hidden && !(fullscreen && settings.HideInFullscreen);
            double dotsBase = away ? SizeOf(View.Idle).Width : w;
            privacyShift.Target = -(dotsBase / 2 + BubbleGap + PrivacyDots.Width / 2) * s;
            PrivacyShift.X = privacyShift.Value;
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

        // Equalizer, progress, the working spark and the timer. Returns true while any of them must keep moving.
        bool AnimateLive(double dt)
        {
            bool keepGoing = false;

            if (presence[MinimalPanel].Value > 0.01) keepGoing |= AnimateMinimal();

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

        bool AnimateEqualizer(double dt)
        {
            eqClock -= dt;
            if (eqClock <= 0)
            {
                eqClock = 0.11 + random.NextDouble() * 0.09;
                for (int i = 0; i < eqTarget.Length; i++)
                    eqTarget[i] = media.IsPlaying ? 0.28 + random.NextDouble() * 0.72 : 0.22;
            }

            bool settled = true;
            for (int i = 0; i < eqLevel.Length; i++)
            {
                double difference = eqTarget[i] - eqLevel[i];
                eqLevel[i] += difference * Math.Min(1, dt * 15);
                if (Math.Abs(difference) > 0.01) settled = false;
            }
            LayoutBars(compactBars, CompactEq.Height);
            LayoutBars(expandedBars, ExpandedEq.Height);
            return media.IsPlaying || !settled;
        }

        void LayoutBars(Rectangle[] bars, double canvasHeight)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                double barHeight = Math.Max(bars[i].Width, eqLevel[i] * canvasHeight);
                bars[i].Height = barHeight;
                Canvas.SetTop(bars[i], (canvasHeight - barHeight) / 2);
            }
        }

        // ---------- Helpers ----------

        static void BuildBars(Canvas canvas, Rectangle[] bars, double barWidth, double gap)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                Rectangle bar = new Rectangle { Width = barWidth, Height = barWidth, RadiusX = barWidth / 2, RadiusY = barWidth / 2, Fill = White };
                Canvas.SetLeft(bar, i * (barWidth + gap));
                canvas.Children.Add(bar);
                bars[i] = bar;
            }
        }

        static void SetupButton(Border button, Action action)
        {
            ScaleTransform scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                button.CaptureMouse();
                AnimateScale(scale, 0.8, 90, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            };
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                bool inside = button.IsMouseOver;
                button.ReleaseMouseCapture();
                AnimateScale(scale, 1, 320, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 });
                if (inside) action();
            };
        }

        static void AnimateScale(ScaleTransform scale, double to, int milliseconds, IEasingFunction easing)
        {
            DoubleAnimation animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        static Geometry Arc(double percent, double size, double thickness)
        {
            double fraction = Math.Clamp(percent / 100, 0, 1);
            if (fraction <= 0.001) return Geometry.Empty;
            double r = (size - thickness) / 2;
            Point center = new Point(size / 2, size / 2);
            if (fraction >= 0.999)
            {
                EllipseGeometry full = new EllipseGeometry(center, r, r);
                full.Freeze();
                return full;
            }

            double angle = fraction * 2 * Math.PI;
            Point start = new Point(center.X, center.Y - r);
            Point end = new Point(center.X + r * Math.Sin(angle), center.Y - r * Math.Cos(angle));
            PathFigure figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        // The usage rings use the chosen color, then turn amber and red as the limit gets close.
        Brush RingBrush(int percent)
        {
            if (percent >= 90) return Red;
            if (percent >= 70) return Amber;
            string choice = settings.RingColor ?? "Blue";
            return choice.Equals("Green", StringComparison.OrdinalIgnoreCase) ? Green
                 : choice.Equals("Purple", StringComparison.OrdinalIgnoreCase) ? Purple
                 : Blue;
        }

        static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
        }

        static string Join(string first, string second)
        {
            return string.IsNullOrEmpty(second) ? first : first + " · " + second;
        }

        static T Freeze<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
