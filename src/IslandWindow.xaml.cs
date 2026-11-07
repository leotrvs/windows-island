using System;
using System.Collections.Generic;
using System.IO;
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
        enum View { Hidden, Minimal, Idle, MusicCompact, ClaudeCompact, MusicExpanded, ClaudeExpanded, Banner }
        enum Activity { None, Music, Claude }

        static readonly Dictionary<View, Size> Sizes = new Dictionary<View, Size>
        {
            [View.Hidden] = new Size(90, 24),
            [View.Minimal] = new Size(76, 6),
            [View.Idle] = new Size(112, 30),
            [View.MusicCompact] = new Size(236, 34),
            [View.ClaudeCompact] = new Size(204, 34),
            [View.MusicExpanded] = new Size(372, 172),
            [View.ClaudeExpanded] = new Size(372, 156),
            [View.Banner] = new Size(352, 74),
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
        static readonly Brush NotificationTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));

        readonly Settings settings;
        readonly MediaService media;
        readonly ClaudeService claude;

        // Shape: slightly bouncy, like the real island.
        readonly Spring width = new Spring(112, 0.05, 280, 0.72);
        readonly Spring height = new Spring(30, 0.05, 280, 0.72);
        readonly Spring shown = new Spring(0, 0.002, 200, 0.7);
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
        readonly DispatcherTimer revealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        DateTime bannerUntil = DateTime.MinValue;
        TimeSpan lastFrame;
        double clock, eqClock;
        double drawnSessionArc = -1, drawnWeeklyArc = -1;
        string lastElapsed, lastRemaining, lastCompactText, lastStatus;
        IntPtr hwnd;

        internal IslandWindow(Settings settings, MediaService media, ClaudeService claude)
        {
            this.settings = settings;
            this.media = media;
            this.claude = claude;
            InitializeComponent();

            panels[View.Idle] = IdlePanel;
            panels[View.MusicCompact] = MusicCompact;
            panels[View.ClaudeCompact] = ClaudeCompact;
            panels[View.MusicExpanded] = MusicExpanded;
            panels[View.ClaudeExpanded] = ClaudeExpanded;
            panels[View.Banner] = Banner;
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
            ProgressHit.MouseLeftButtonUp += Seek;
            ProgressHit.MouseLeftButtonDown += (s, e) => e.Handled = true;

            SessionLabel.Text = Strings.Session5h;
            WeeklyLabel.Text = Strings.Weekly;

            Pill.MouseEnter += OnPillEnter;
            Pill.MouseLeave += OnPillLeave;
            Pill.MouseLeftButtonUp += OnPillClick;
            enterTimer.Tick += (s, e) => { enterTimer.Stop(); SetHovering(true); };
            leaveTimer.Tick += (s, e) => { leaveTimer.Stop(); SetHovering(false); };
            pollTimer.Tick += (s, e) => Poll();
            revealTimer.Tick += (s, e) => CheckReveal();

            media.Changed += () => { UpdateMusic(); Recompute(); };
            claude.Changed += () => { UpdateClaude(); Recompute(); };
            claude.Alert += ShowBanner;

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
                shown.Target = 1;
                Recompute();
                StartLoop();
                pollTimer.Start();
            };
            SystemEvents.DisplaySettingsChanged += (s, e) => Dispatcher.BeginInvoke(new Action(PlaceWindow));
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
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;
        }

        // Draws every state to PNG files for the README, from sample data and without touching the screen.
        public void RenderStates(string folder, string suffix)
        {
            Directory.CreateDirectory(folder);
            View[] states = { View.Idle, View.MusicCompact, View.ClaudeCompact, View.MusicExpanded, View.ClaudeExpanded, View.Banner };
            double[] levels = { 0.55, 0.95, 0.4, 0.75 };
            Array.Copy(levels, eqLevel, levels.Length);
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
        }

        void SnapAll()
        {
            foreach (Spring spring in new[] { width, height, shown, bubbleScale, bubbleOpacity, sessionArc, weeklyArc })
                spring.Snap(spring.Target);
            foreach (Spring spring in presence.Values) spring.Snap(spring.Target);
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

        public void SettingsChanged()
        {
            PlaceWindow();
            UpdateMusic();
            UpdateClaude();
            Recompute();
        }

        // ---------- State ----------

        void Recompute()
        {
            bool music = MusicActive;
            bool working = ClaudeActive;
            Activity primary = Activity.None, second = Activity.None;
            if (working && claude.Waiting > 0) { primary = Activity.Claude; if (music) second = Activity.Music; }
            else if (music) { primary = Activity.Music; if (working) second = Activity.Claude; }
            else if (working) primary = Activity.Claude;

            View next;
            if (forced.HasValue) next = forced.Value;
            else if (fullscreen && settings.HideInFullscreen) next = View.Hidden;
            else if (hovering)
                next = primary == Activity.Music ? View.MusicExpanded
                     : settings.ShowClaude ? View.ClaudeExpanded
                     : settings.ShowMusic && media.HasSession ? View.MusicExpanded
                     : View.Idle;
            else if (DateTime.Now < bannerUntil) next = View.Banner;
            // Empty island: the collapsed island stays a plain pill. Activities only show when it expands.
            else if (primary == Activity.Music && !settings.EmptyIsland) next = View.MusicCompact;
            else if (primary == Activity.Claude && !settings.EmptyIsland) next = View.ClaudeCompact;
            else if (!settings.ShowIdlePill) next = View.Hidden;
            else if (maximized && !IsBottom) next = View.Minimal; // Stay out of the way of tab strips
            else next = View.Idle;

            // Auto-hide: an island that has shown nothing for a while steps away until something happens
            // or the mouse comes to its spot.
            bool idle = next == View.Idle || next == View.Minimal;
            if (!idle || hovering) lastActive = DateTime.Now;
            autoHidden = idle && !forced.HasValue && settings.AutoHideSeconds > 0 &&
                         DateTime.Now - lastActive > TimeSpan.FromSeconds(settings.AutoHideSeconds);
            if (autoHidden) next = View.Hidden;
            if (autoHidden != revealTimer.IsEnabled) revealTimer.IsEnabled = autoHidden;

            Activity shownActivity = next == View.MusicCompact ? Activity.Music : next == View.ClaudeCompact ? Activity.Claude : Activity.None;
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
            Size size = Sizes[view];
            width.Target = size.Width;
            height.Target = size.Height;
            shown.Target = view == View.Hidden ? 0 : 1;
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
            }
            bubbleOpacity.Target = hasBubble ? 1 : 0;
            bubbleScale.Target = hasBubble ? 1 : 0.4;
        }

        void SetHovering(bool value)
        {
            if (hovering == value) return;
            hovering = value;
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
                bool fromClaude = currentBanner == null || currentBanner.FromClaude;
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
                Title = notification.Title,
                Subtitle = notification.Body,
                Seconds = settings.AlertSeconds,
            });
        }

        // Messages from the app itself, such as update results. Not gated by the notifications option.
        internal void ShowSystemBanner(string title, string body)
        {
            Enqueue(new BannerItem { App = Installer.Name, Title = title, Subtitle = body, Seconds = settings.AlertSeconds });
        }

        // True while the user is looking at or using the island, so an update restart waits.
        internal bool IsBusy
        {
            get { return hovering || view == View.Banner || view == View.MusicExpanded || view == View.ClaudeExpanded; }
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
            BannerBell.Visibility = item.FromClaude ? Visibility.Collapsed : Visibility.Visible;
            BannerIcon.Background = item.FromClaude ? ClaudeTint : NotificationTint;
            BannerApp.Text = item.App ?? "";
            BannerApp.Visibility = item.FromClaude || string.IsNullOrEmpty(item.App) ? Visibility.Collapsed : Visibility.Visible;
            BannerTitle.Text = item.Title;
            BannerSubtitle.Text = item.Subtitle ?? "";
            BannerSubtitle.Visibility = string.IsNullOrEmpty(item.Subtitle) ? Visibility.Collapsed : Visibility.Visible;

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

            const double halfWidth = 150, reach = 36;
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

        void PlaceWindow()
        {
            Rect work = SystemParameters.WorkArea;
            bool bottom = IsBottom;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = bottom ? work.Bottom - Height : work.Top;

            VerticalAlignment align = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            Thickness margin = bottom ? new Thickness(0, 0, 0, settings.OffsetY) : new Thickness(0, settings.OffsetY, 0, 0);
            Pill.VerticalAlignment = align;
            Bubble.VerticalAlignment = align;
            Pill.Margin = margin;
            Bubble.Margin = margin;
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
            clock += dt;

            bool moving = false;
            moving |= width.Step(dt);
            moving |= height.Step(dt);
            moving |= shown.Step(dt);
            moving |= bubbleShift.Step(dt);
            moving |= bubbleScale.Step(dt);
            moving |= bubbleOpacity.Step(dt);
            moving |= sessionArc.Step(dt);
            moving |= weeklyArc.Step(dt);
            foreach (Spring spring in presence.Values) moving |= spring.Step(dt);

            ApplyFrame();
            bool continuous = AnimateLive(dt);
            if (!moving && !continuous) StopLoop();
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
            PillScale.ScaleX = PillScale.ScaleY = Math.Max(0, 0.7 + 0.3 * shown.Value);

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
            bubbleShift.Target = bubble != Activity.None ? w / 2 + BubbleGap + BubbleSize / 2 : w / 2 - BubbleSize / 2 - 2;
            BubbleShift.X = bubbleShift.Value;
            BubbleScale.ScaleX = BubbleScale.ScaleY = Math.Max(0, bubbleScale.Value);
            Bubble.Opacity = Math.Clamp(bubbleOpacity.Value, 0, 1) * visible;

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

        static Brush RingBrush(int percent)
        {
            return percent >= 90 ? Red : percent >= 70 ? Amber : Green;
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
