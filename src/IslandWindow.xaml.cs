using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WindowsIsland
{
    public partial class IslandWindow : Window
    {
        enum View { Hidden, Minimal, MusicPeek, LevelMini, QuickExpanded, ClipboardExpanded, ReminderExpanded, Idle, MusicCompact, ClaudeCompact, CodexCompact, TimerCompact, BatteryCompact, ExternalCompact, SystemCompact, MusicExpanded, ClaudeExpanded, TimerExpanded, SystemExpanded, ExternalExpanded, Banner, Toast, Level, Permission }
        enum Activity { None, Music, Claude, Codex, Timer, Battery, System, External, Quick, Clipboard, Reminder }

        static readonly Dictionary<View, Size> Sizes = new Dictionary<View, Size>
        {
            [View.Hidden] = new Size(90, 24),
            [View.Minimal] = new Size(108, 9),
            [View.MusicPeek] = new Size(236, 36),
            [View.LevelMini] = new Size(104, 20),
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
            [View.QuickExpanded] = new Size(372, 150),
            [View.ClipboardExpanded] = new Size(372, 150),
            [View.ReminderExpanded] = new Size(372, 206),
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
        static readonly Brush SystemBlue = Freeze(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
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
        readonly Spring privacyOpacity = new Spring(0, 0.002, 700, 1);

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
            Stage.Children.Add(timerRail); // The timer ring sits on top of the panels
            Stage.Children.Add(timerRing);

            panels[View.Idle] = IdlePanel;
            panels[View.Minimal] = MinimalPanel;
            panels[View.MusicPeek] = MusicPeek;
            panels[View.LevelMini] = LevelMini;
            panels[View.MusicCompact] = MusicCompact;
            panels[View.ClaudeCompact] = ClaudeCompact;
            panels[View.CodexCompact] = CodexCompact;
            panels[View.TimerCompact] = TimerCompact;
            panels[View.BatteryCompact] = BatteryCompact;
            panels[View.ExternalCompact] = ExternalCompact;
            panels[View.SystemCompact] = SystemCompact;
            panels[View.ExternalExpanded] = ExternalExpanded;
            panels[View.TimerExpanded] = TimerExpanded;
            panels[View.QuickExpanded] = QuickExpanded;
            panels[View.ClipboardExpanded] = ClipboardExpanded;
            panels[View.ReminderExpanded] = ReminderExpanded;
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
            plusFont = BtnPlusText.FontFamily;
            presence[IdlePanel].Snap(1);
            Stage.Clip = stageClip;

            BuildBars(CompactEq, compactBars, 3, 2.6);
            BuildBars(ExpandedEq, expandedBars, 3.6, 3);
            SetupButton(PreviousButton, media.Previous);
            SetupButton(PlayButton, media.TogglePlayPause);
            SetupButton(NextButton, media.Next);
            SetupButton(BtnMinus, () => timer.Nudge(-1));
            SetupButton(BtnPlus, () => { if (timer.Pomodoro && timer.Running) timer.Skip(); else timer.Nudge(1); }); // While a pomodoro runs it skips to the next phase
            SetupButton(BtnStart, () => timer.StartCountdown(timer.ChosenMinutes));
            SetupButton(BtnWatch, timer.StartStopwatch);
            SetupButton(BtnPomodoro, () => { SyncPomodoro(); timer.StartPomodoro(); });
            SetupButton(QuickMicButton, () => { if (!quickSample) AudioControl.SetMuted(AudioControl.Capture, !AudioControl.IsMuted(AudioControl.Capture)); UpdateQuick(true); });
            SetupButton(QuickSpeakerButton, () => { if (!quickSample) AudioControl.SetMuted(AudioControl.Render, !AudioControl.IsMuted(AudioControl.Render)); UpdateQuick(true); });
            SetupButton(QuickShotButton, () => { if (!quickSample) AudioControl.Screenshot(); });
            SetupButton(QuickSoundButton, () => { if (!quickSample) AudioControl.OpenSoundSettings(); });
            QuickVolumeArea.MouseLeftButtonDown += (s, e) => { e.Handled = true; QuickVolumeArea.CaptureMouse(); SetQuickVolume(e.GetPosition(QuickVolumeTrack).X); };
            QuickVolumeArea.MouseMove += (s, e) => { if (QuickVolumeArea.IsMouseCaptured) SetQuickVolume(e.GetPosition(QuickVolumeTrack).X); };
            QuickVolumeArea.MouseLeftButtonUp += (s, e) => QuickVolumeArea.ReleaseMouseCapture();
            SetupButton(BtnPause, timer.PauseOrResume);
            SetupButton(BtnStop, timer.Stop);
            Pill.MouseWheel += OnPillWheel;
            ProgressHit.MouseLeftButtonUp += Seek;
            ProgressHit.MouseLeftButtonDown += (s, e) => e.Handled = true;
            // A click on the cover, title or artist brings the player (Spotify, the browser) to the front. If no window
            // of it is found, the click falls through to the island as before.
            TrackInfo.MouseLeftButtonDown += (s, e) => e.Handled = true;
            TrackInfo.MouseLeftButtonUp += (s, e) =>
            {
                if (suppressClick) return; // A drag: the island's own handler clears it
                if (media.FocusSource()) e.Handled = true;
            };

            ApplyTexts();
            ApplyBannerSize();

            monitorTimer.Tick += (s, e) => { if (view == View.SystemCompact) UpdateSystemMonitor(); else UpdateThinGauges(); };
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
            BannerAction.MouseLeftButtonUp += (s, e) => { e.Handled = true; AnswerBannerQuestion(); };
            PermAllow.MouseLeftButtonUp += (s, e) => AnswerPermission(PermAllow, "allow", e);
            PermDeny.MouseLeftButtonUp += (s, e) => AnswerPermission(PermDeny, "deny", e);
            PermOpen.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenClaudeNeedsInput(); };
            PermOpen.MouseLeftButtonDown += (s, e) => e.Handled = true;
            OpenClaudeLink.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenClaudeNeedsInput(); };
            OpenClaudeLink.MouseLeftButtonDown += (s, e) => e.Handled = true;
            WireChatLines();
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
            media.WatchMediaKeys();
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
            clipboardList = (Application.Current as App)?.ClipboardList;
            if (clipboardList != null) clipboardList.Changed += OnClipboardChanged;
            SetupButton(ClipboardClearButton, () => { if (!clipboardSample) clipboardList?.Clear(); });
            WireReminders();

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

        Size SizeOf(View value)
        {
            if (value == View.ClaudeExpanded) return new Size(372, ClaudePanelHeight);
            if (value == View.Banner) return new Size(Banner.Width, Banner.Height);
            if (value == View.Permission) return new Size(372, permissionHeight);
            if (value == View.SystemCompact) return new Size(monitorWidth, 34);
            if (value == View.MusicCompact && SongNameShown) return new Size(236, 34);
            if (value == View.Minimal && minimalHasContent) return new Size(108, 18);
            return Sizes[value];
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
            if (clipboardList != null) clipboardList.Changed -= OnClipboardChanged;
            if (reminders != null) reminders.Changed -= OnRemindersChanged;
            EndTyping(false, false);
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

        // Fixed texts, again after the language changes.
        void ApplyTexts()
        {
            SessionLabel.Text = Strings.Session5h;
            WeeklyLabel.Text = Strings.Weekly;
            CodexWeeklyLabel.Text = Strings.Weekly;
            BtnStartText.Text = Strings.TimerStart;
            BtnWatchText.Text = Strings.MenuStopwatch;
            BtnPomodoroText.Text = Strings.MenuPomodoro;
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
            if (hovering) { unreadAt = DateTime.MinValue; toastWaitingUntil = DateTime.MinValue; } // Looked at
            bool presenting = Presenting;
            bool quiet = muted || presenting; // Not the active monitor, or sharing the screen: only the timer stays
            bool music = MusicActive && !quiet;
            bool working = ClaudeActive && !quiet;

            // Priority: Claude waiting for you, then music, then Claude working, then Codex working.
            List<Activity> active = new List<Activity>();
            if (working && ClaudeWaitingNow) active.Add(Activity.Claude);
            if (music) active.Add(Activity.Music);
            if (working && !ClaudeWaitingNow) active.Add(Activity.Claude);
            if (CodexActive && !quiet) active.Add(Activity.Codex);
            if (ApiActive && !quiet) active.Add(Activity.External);
            if (BatteryActive && !quiet) active.Add(Activity.Battery);
            if (timer.Running) active.Insert(working && ClaudeWaitingNow ? 1 : 0, Activity.Timer);
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
            else if (fullscreen && settings.HideInFullscreen && !UrgentShowing) next = View.Hidden;
            else if (presenting) next = UrgentShowing ? View.Banner : timer.Running ? View.TimerCompact : View.Hidden; // Nothing but the timer
            else if (permission != null) next = View.Permission; // A question that waits for a click comes first
            else if (tucked) next = View.Hidden;
            else if (DateTime.Now < bannerUntil && currentBanner?.OnClick != null) next = View.Banner; // A question waits for its button, even under the mouse
            else if (hovering || DateTime.Now < peekUntil)
                next = PickedPage() ?? DefaultPage(primary);
            else if (DateTime.Now < bannerUntil) next = View.Banner;
            else if (DateTime.Now < toastUntil) next = View.Toast;
            else if (DateTime.Now < levelUntil) next = ThinActive ? View.LevelMini : View.Level;
            // The same thin line for the activities too, when asked: hover to see them
            else if (DateTime.Now < musicPeekUntil && ThinActive) next = View.MusicPeek;
            else if (ThinActive && primary != Activity.None) next = View.Minimal;
            else if (settings.MaximizedActivitiesThin && maximized && !IsBottom && settings.MaximizedMode == "Thin") next = View.Minimal;
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
            else if (settings.SystemMonitor && (settings.AlwaysThin || (maximized && !IsBottom && settings.MaximizedMode == "Thin"))) next = View.Minimal; // The thin line with two tiny rings
            else if (settings.SystemMonitor) next = View.SystemCompact;
            else if (!settings.ShowIdlePill && !PrivacyOn && DateTime.Now - discordChangedAt > TimeSpan.FromSeconds(4)) next = View.Hidden;
            else if (maximized && !IsBottom && settings.MaximizedMode == "Thin") next = View.Minimal; // Stay out of the way of tab strips
            else if (settings.IdleStyle == "Line" || settings.AlwaysThin) next = View.Minimal; // The thin line as the idle look
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
            if (next == View.Minimal && primary != Activity.None && second != primary) nextBubble = second; // On the thin line, the second activity is a tiny bubble beside it
            if (next == View.SystemCompact && KeepsMonitor(primary)) nextBubble = primary; // The monitor keeps its place, the activity is the bubble
            bool minimalChanged = next == View.Minimal ? UpdateMinimal(primary) : ClearMinimal();
            bool gauges = next == View.Minimal && settings.SystemMonitor && (primary == Activity.None || (settings.MonitorKeepsPlace && !settings.EmptyIsland));
            if (renderThin && next == View.Minimal) { gauges = renderGauges; nextBubble = renderBubble; }
            bool gaugesChanged = gauges != thinGauges;
            thinGauges = gauges;
            Size wanted = SizeOf(next);
            bool sizeChanged = next == view && (Math.Abs(wanted.Width - morphToW) > 0.5 || Math.Abs(wanted.Height - morphToH) > 0.5); // The name opening beside the cover widens the island in the same view
            if (typing && next != View.ReminderExpanded) EndTyping(true, false); // The page went away (banner, other page): the keyboard goes back
            if (next != view || nextBubble != bubble || minimalChanged || gaugesChanged || sizeChanged)
            {
                view = next;
                bubble = nextBubble;
                ApplyTargets();
            }
            StartLoop();
        }

        void ApplyTargets()
        {
            UpdateDots();
            UpdatePrivacy(); // The dots move into the thin line, or out of it
            double dot = view == View.Minimal ? 4.5 : 6; // Smaller beside the thin line
            MicDot.Width = MicDot.Height = CamDot.Width = CamDot.Height = ScreenDot.Width = ScreenDot.Height = dot;
            Bubble.IsHitTestVisible = bubble != Activity.None && view != View.Hidden; // The small second activity can be pointed at
            Size size = SizeOf(view);
            bool sameView = view == lastTargetView;
            lastTargetView = view;
            if (Math.Abs(size.Width - morphToW) > 0.5 || Math.Abs(size.Height - morphToH) > 0.5)
            {
                if (sameView)
                {
                    // The same view with another size (the name opening beside the cover): its content stays, it does not wait for the shape
                    morphFromW = morphToW = size.Width; morphFromH = morphToH = size.Height;
                }
                else
                {
                    // A new size: the content of the incoming view waits until the shape has got most of the way
                    morphFromW = width.Value; morphFromH = height.Value;
                    morphToW = size.Width; morphToH = size.Height;
                }
            }
            // Alerts arrive with a slightly bigger overshoot, everything else settles gently
            if (view == View.Banner || view == View.Toast || view == View.Permission) { width.Tune(320, 0.66); height.Tune(320, 0.66); }
            else { width.Tune(280, 0.74); height.Tune(280, 0.74); }
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
            else if (thinGauges)
            {
                if (!monitorTimer.IsEnabled) monitorTimer.Start();
                UpdateThinGauges();
            }
            else monitorTimer.Stop();
            thinGaugesOpacity.Target = thinGauges ? 1 : 0;
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
            bubbleScale.Target = hasBubble ? (view == View.Minimal ? 0.42 : 1) : 0.3;
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
            return value == View.MusicExpanded || value == View.ClaudeExpanded || value == View.TimerExpanded || value == View.QuickExpanded || value == View.ClipboardExpanded || value == View.ReminderExpanded || value == View.SystemExpanded || value == View.ExternalExpanded || value == View.Banner || value == View.Permission;
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
            double tallest = Math.Max(Math.Max(Math.Max(Sizes[View.MusicExpanded].Height, Sizes[View.TimerExpanded].Height), Sizes[View.SystemExpanded].Height), 248 + 22 + 20); // The Claude page at its tallest: Codex, the forecast line and the day line
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
            ThinGauges.VerticalAlignment = align;
            ThinGauges.Margin = bottom ? new Thickness(0, 0, 0, settings.OffsetY) : new Thickness(0, settings.OffsetY, 0, 0);
            Pill.Margin = margin;
            Bubble.Margin = margin;
            PrivacyDots.VerticalAlignment = align;
            PrivacyDots.Margin = margin;
            Pill.RenderTransformOrigin = new Point(0.5, bottom ? 1 : 0);
            ApplyFrame();
        }
    }
}
