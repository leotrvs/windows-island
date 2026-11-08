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
        enum View { Hidden, Minimal, Idle, MusicCompact, ClaudeCompact, CodexCompact, TimerCompact, BatteryCompact, MusicExpanded, ClaudeExpanded, TimerExpanded, SystemExpanded, Banner, Toast, Level }
        enum Activity { None, Music, Claude, Codex, Timer, Battery, System }

        static readonly Dictionary<View, Size> Sizes = new Dictionary<View, Size>
        {
            [View.Hidden] = new Size(90, 24),
            [View.Minimal] = new Size(76, 6),
            [View.Idle] = new Size(112, 30),
            [View.MusicCompact] = new Size(204, 34),
            [View.ClaudeCompact] = new Size(176, 34),
            [View.CodexCompact] = new Size(176, 34),
            [View.TimerCompact] = new Size(150, 34),
            [View.BatteryCompact] = new Size(150, 34),
            [View.MusicExpanded] = new Size(372, 152),
            [View.TimerExpanded] = new Size(372, 150),
            [View.SystemExpanded] = new Size(372, 150),
            [View.ClaudeExpanded] = new Size(372, 156),
            [View.Banner] = new Size(352, 74),
            [View.Toast] = new Size(184, 36),
            [View.Level] = new Size(220, 36),
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
            panels[View.MusicCompact] = MusicCompact;
            panels[View.ClaudeCompact] = ClaudeCompact;
            panels[View.CodexCompact] = CodexCompact;
            panels[View.TimerCompact] = TimerCompact;
            panels[View.BatteryCompact] = BatteryCompact;
            panels[View.TimerExpanded] = TimerExpanded;
            panels[View.SystemExpanded] = SystemExpanded;
            panels[View.MusicExpanded] = MusicExpanded;
            panels[View.ClaudeExpanded] = ClaudeExpanded;
            panels[View.Banner] = Banner;
            panels[View.Toast] = Toast;
            panels[View.Level] = Level;
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

            Pill.MouseEnter += OnPillEnter;
            Pill.MouseLeave += OnPillLeave;
            Pill.MouseLeftButtonUp += OnPillClick;
            enterTimer.Tick += (s, e) => { enterTimer.Stop(); SetHovering(true); };
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
            battery = (Application.Current as App)?.Battery;
            if (battery != null) battery.Changed += OnBatteryChanged;
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

        void OnMediaChanged() { UpdateMusic(); Recompute(); }
        void OnClaudeChanged() { UpdateClaude(); Recompute(); }

        void OnTimerChanged() { UpdateTimerPage(); Recompute(); }
        void OnPrivacyChanged() { UpdatePrivacy(); Recompute(); }
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
        void OnFocusChanged() { UpdateFocusMoon(); }

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

        bool PrivacyOn { get { return settings.ShowPrivacyDots && (privacy.Microphone || privacy.Camera); } }

        string privacyLabel = "";
        DateTime privacyNameUntil;
        DispatcherTimer privacyNameTimer;

        // "Zoom", or "Zoom +1" when several apps are recording.
        string PrivacyLabel()
        {
            List<string> names = new List<string>();
            if (privacy.Microphone) names.AddRange(privacy.MicrophoneApps);
            if (privacy.Camera) names.AddRange(privacy.CameraApps);
            names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0) return "";
            string first = names[0].Length > 20 ? names[0].Substring(0, 19) + "…" : names[0];
            return names.Count > 1 ? first + " +" + (names.Count - 1) : first;
        }

        void UpdatePrivacy()
        {
            UpdateFocusMoon();
            bool mic = privacy.Microphone && settings.ShowPrivacyDots, cam = privacy.Camera && settings.ShowPrivacyDots;
            MicDot.Visibility = mic ? Visibility.Visible : Visibility.Collapsed;
            CamDot.Visibility = cam ? Visibility.Visible : Visibility.Collapsed;

            // A new app using the microphone or camera shows its name for a few seconds; hovering shows it again.
            string label = mic || cam ? PrivacyLabel() : "";
            if (label != privacyLabel)
            {
                bool added = label.Length > 0;
                privacyLabel = label;
                if (added)
                {
                    privacyNameUntil = DateTime.Now.AddSeconds(5);
                    if (privacyNameTimer == null)
                    {
                        privacyNameTimer = new DispatcherTimer();
                        privacyNameTimer.Tick += (s, e) => { privacyNameTimer.Stop(); UpdatePrivacy(); };
                    }
                    privacyNameTimer.Interval = TimeSpan.FromSeconds(5.1);
                    privacyNameTimer.Stop();
                    privacyNameTimer.Start();
                }
            }
            bool showName = settings.ShowPrivacyNames && label.Length > 0 && (hovering || DateTime.Now < privacyNameUntil);
            double dotsWidth = mic && cam ? 30 : 20, nameWidth = 0;
            PrivacyName.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;
            if (showName)
            {
                if (PrivacyName.Text != label) PrivacyName.Text = label;
                PrivacyName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                nameWidth = PrivacyName.DesiredSize.Width + PrivacyName.Margin.Left + PrivacyName.Margin.Right;
            }
            PrivacyDots.Width = dotsWidth + nameWidth;
            privacyOpacity.Target = mic || cam ? 1 : 0;
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
            media.Changed -= OnMediaChanged;
            claude.Changed -= OnClaudeChanged;
            claude.Alert -= ShowBanner;
            codex.Changed -= OnCodexChanged;
            codex.Finished -= OnCodexFinished;
            timer.Changed -= OnTimerChanged;
            privacy.Changed -= OnPrivacyChanged;
            if (battery != null) battery.Changed -= OnBatteryChanged;
            if (focusState != null) focusState.Changed -= OnFocusChanged;
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
            View[] states = { View.Idle, View.MusicCompact, View.ClaudeCompact, View.CodexCompact, View.BatteryCompact, View.MusicExpanded, View.ClaudeExpanded, View.TimerExpanded, View.SystemExpanded, View.Level, View.Banner };
            double[] levels = { 0.55, 0.95, 0.4, 0.75 };
            Array.Copy(levels, eqLevel, levels.Length);
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
            banners.Enqueue(new BannerItem { App = "Mail", Title = Strings.SampleNotificationTitle, Subtitle = Strings.SampleNotificationBody, Seconds = 5 });
            NextBanner();
            forced = View.Banner;
            Root.UpdateLayout();
            ApplyFrame();
            SaveSnapshot(System.IO.Path.Combine(folder, "notification" + suffix + ".png"));

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
                Root.UpdateLayout();
                ApplyFrame();
                SaveSnapshot(System.IO.Path.Combine(folder, "battery-" + kind + suffix + ".png"));
            }
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
            if (BatteryActive) active.Add(Activity.Battery);
            if (timer.Running) active.Insert(working && claude.Waiting > 0 ? 1 : 0, Activity.Timer);
            Activity primary = active.Count > 0 ? active[0] : Activity.None;
            Activity second = active.Count > 1 ? active[1] : Activity.None;

            View next;
            if (forced.HasValue) next = forced.Value;
            else if (fullscreen && settings.HideInFullscreen) next = View.Hidden;
            else if (hovering || DateTime.Now < peekUntil)
                next = PickedPage() ?? DefaultPage(primary);
            else if (DateTime.Now < bannerUntil) next = View.Banner;
            else if (DateTime.Now < toastUntil) next = View.Toast;
            else if (DateTime.Now < levelUntil) next = View.Level;
            // Empty island: the collapsed island stays a plain pill. Activities only show when it expands.
            else if (primary == Activity.Music && !settings.EmptyIsland) next = View.MusicCompact;
            else if (primary == Activity.Claude && !settings.EmptyIsland) next = View.ClaudeCompact;
            else if (primary == Activity.Codex && !settings.EmptyIsland) next = View.CodexCompact;
            else if (primary == Activity.Battery && !settings.EmptyIsland) next = View.BatteryCompact;
            else if (primary == Activity.Timer) next = View.TimerCompact;
            else if (!settings.ShowIdlePill && !PrivacyOn) next = View.Hidden;
            else if (maximized && !IsBottom) next = View.Minimal; // Stay out of the way of tab strips
            else next = View.Idle;

            // Auto-hide: an island that has shown nothing for a while steps away until something happens
            // or the mouse comes to its spot.
            bool idle = next == View.Idle || next == View.Minimal;
            if (!idle || hovering || PrivacyOn) lastActive = DateTime.Now;
            autoHidden = idle && !forced.HasValue && settings.AutoHideSeconds > 0 &&
                         DateTime.Now - lastActive > TimeSpan.FromSeconds(settings.AutoHideSeconds);
            if (autoHidden) next = View.Hidden;
            if (autoHidden != revealTimer.IsEnabled) revealTimer.IsEnabled = autoHidden;

            Activity shownActivity = next == View.MusicCompact ? Activity.Music
                                   : next == View.ClaudeCompact ? Activity.Claude
                                   : next == View.CodexCompact ? Activity.Codex
                                   : next == View.TimerCompact ? Activity.Timer
                                   : next == View.BatteryCompact ? Activity.Battery
                                   : Activity.None;
            Activity other = primary == shownActivity ? second : primary;
            Activity nextBubble = shownActivity != Activity.None && other != shownActivity ? other : Activity.None;
            if (next != view || nextBubble != bubble)
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
            Size size = SizeOf(view);
            width.Target = size.Width;
            height.Target = size.Height;
            shown.Target = view == View.Hidden ? 0 : 1;
            stateScale.Target = IsExpandedView(view) ? settings.ExpandedScale : settings.CollapsedScale;
            Pill.IsHitTestVisible = view != View.Hidden;
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
            }
            bubbleOpacity.Target = hasBubble ? 1 : 0;
            bubbleScale.Target = hasBubble ? 1 : 0.4;
        }

        // ---------- Pages ----------

        Activity? page; // The page picked with the wheel or the dots; null = the default one

        List<Activity> Pages()
        {
            List<Activity> pages = new List<Activity>();
            if (settings.PageMusic && settings.ShowMusic && media.HasSession) pages.Add(Activity.Music);
            if (settings.PageClaude && (settings.ShowClaude || CodexSectionVisible)) pages.Add(Activity.Claude);
            if (settings.PageSystem) pages.Add(Activity.System);
            if (settings.PageTimer) pages.Add(Activity.Timer);
            return pages;
        }

        // The page the island opens on: the one for what is happening, if it is on, else the first page that is.
        View DefaultPage(Activity primary)
        {
            Activity wanted = primary == Activity.Music ? Activity.Music
                            : primary == Activity.Timer ? Activity.Timer
                            : settings.ShowClaude || CodexSectionVisible ? Activity.Claude
                            : settings.ShowMusic && media.HasSession ? Activity.Music
                            : Activity.None;
            List<Activity> pages = Pages();
            if (wanted != Activity.None && pages.Contains(wanted)) return ExpandedFor(wanted);
            return pages.Count > 0 ? ExpandedFor(pages[0]) : View.Idle;
        }

        static View ExpandedFor(Activity activity)
        {
            return activity == Activity.Music ? View.MusicExpanded : activity == Activity.Timer ? View.TimerExpanded : activity == Activity.System ? View.SystemExpanded : View.ClaudeExpanded;
        }

        static Activity ActivityOf(View value)
        {
            return value == View.MusicExpanded ? Activity.Music : value == View.TimerExpanded ? Activity.Timer : value == View.SystemExpanded ? Activity.System : Activity.Claude;
        }

        View? PickedPage()
        {
            if (!page.HasValue || !Pages().Contains(page.Value)) return null;
            return ExpandedFor(page.Value);
        }

        DateTime lastWheel;
        void OnPillWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsExpandedView(view) || view == View.Banner) return;
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
            bool show = IsExpandedView(view) && view != View.Banner && pages.Count > 1;
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
                Subtitle = name,
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
                Seconds = settings.AlertSeconds,
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
            LevelFill.Width = Math.Max(0, 130.0 * (muted ? 0 : percent) / 100);
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
            get { return hovering || view == View.Banner || view == View.MusicExpanded || view == View.ClaudeExpanded || view == View.TimerExpanded || view == View.SystemExpanded; }
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
            BannerIcon.Background = item.FromClaude ? ClaudeTint : item.FromCodex ? CodexTint : NotificationTint;
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
            if (!insideX || !insideY) return;

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
            return value == View.MusicExpanded || value == View.ClaudeExpanded || value == View.TimerExpanded || value == View.SystemExpanded || value == View.Banner;
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

            Brush art = media.Art != null
                ? Freeze(new ImageBrush(media.Art) { Stretch = Stretch.UniformToFill })
                : (Brush)FindResource("ArtPlaceholder");
            CompactArt.Background = art;
            ExpandedArt.Background = art;
            BubbleArt.Fill = art;

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

            string compact = waiting ? Strings.Waiting : elapsed;
            if (compact != lastCompactText)
            {
                lastCompactText = compact;
                ClaudeCompactText.Text = compact;
                ClaudeCompactText.Foreground = waiting ? Orange : White;
            }

            string status = waiting ? Join(Strings.WaitingForYou, claude.WaitingName)
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

            privacyShift.Target = -(w / 2 + BubbleGap + PrivacyDots.Width / 2) * s;
            PrivacyShift.X = privacyShift.Value;
            PrivacyShift.Y = (bottom ? -1 : 1) * (Math.Min(h, 34) * s - PrivacyDots.Height) / 2; // Level with a compact island
            PrivacyDots.Opacity = Math.Clamp(privacyOpacity.Value, 0, 1) * visible;

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
