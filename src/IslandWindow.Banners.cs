using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Banners, notifications, the volume and brightness levels, and toasts.
    public partial class IslandWindow
    {
        // Yes to the question a banner asks (download the update): the banner goes and the action runs.
        void AnswerBannerQuestion()
        {
            Action answer = currentBanner?.OnClick;
            if (answer == null) return;
            currentBanner.OnClick = null;
            bannerUntil = DateTime.MinValue;
            if (banners.Count > 0) NextBanner(); else Recompute();
            answer();
        }

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
            public Action OnClick;     // A banner that asks: a click answers yes
            public bool Spin;          // The glyph turns (looking, downloading)
            public bool Pop;           // The glyph pops in (done)
            public Action OnTap;       // Run when the banner is clicked (it still just closes otherwise)
            public bool Urgent;        // A timer that ended: it shows over a fullscreen window, past quiet mode, and goes first
        }

        readonly Queue<BannerItem> banners = new Queue<BannerItem>();
        BannerItem currentBanner;

        void ShowBanner(bool waiting, string name)
        {
            if (!settings.ShowClaude || !settings.ClaudeBanners) return;
            lastClaudeBanner = DateTime.Now;
            if (!waiting) { toastWaitingUntil = DateTime.MinValue; FlashThin(true); } // Done: nothing waits any more, and the line flashes green
            Enqueue(new BannerItem
            {
                FromClaude = true,
                Title = waiting ? Strings.ClaudeNeedsYou : Strings.ClaudeDone,
                Subtitle = waiting && !string.IsNullOrWhiteSpace(claude.LastQuestion) ? claude.LastQuestion : name,
                Seconds = settings.AlertSeconds + (waiting ? 1 : 0), // A question waits a little longer
            });
        }

        DateTime lastClaudeBanner = DateTime.MinValue; // When the island last announced Claude by itself

        internal void ShowNotification(IslandNotification notification)
        {
            if (!settings.ShowNotifications) return;
            // The island announces Claude itself, so the Claude app's own toasts would repeat it. But when the island did not
            // notice (a question it could not read, for example), the app's own notice is the only one: it is shown after a moment.
            if (settings.ShowClaude && settings.ClaudeBanners && notification.App == "Claude")
            {
                DateTime arrived = DateTime.Now;
                DispatcherTimer wait = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                wait.Tick += (s, e) =>
                {
                    wait.Stop();
                    if (lastClaudeBanner < arrived.AddSeconds(-8))
                    {
                        EnqueueNotification(notification);
                        if (AsksForYou(notification)) { toastWaitingUntil = DateTime.Now.AddMinutes(3); Recompute(); } // The line pulses until you look
                    }
                };
                wait.Start();
                return;
            }
            EnqueueNotification(notification);
        }

        void EnqueueNotification(IslandNotification notification)
        {
            unreadAt = DateTime.Now;
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
            if (!settings.ShowLevels || Presenting) return;
            if (fullscreen && settings.HideInFullscreen) return;
            percent = Math.Clamp(percent, 0, 100);
            LevelGlyph.Text = kind == LevelService.Kind.Brightness ? "\uE706" : muted || percent == 0 ? "\uE74F" : percent < 34 ? "\uE992" : percent < 67 ? "\uE993" : "\uE767";
            // Brightness is a sun that lights up: its rays grow with the level. Volume is a speaker whose waves grow, with a cross when muted.
            bool sun = kind == LevelService.Kind.Brightness;
            LevelGlyph.Visibility = sun ? Visibility.Collapsed : Visibility.Visible;
            LevelSun.Visibility = sun ? Visibility.Visible : Visibility.Collapsed;
            LevelMiniSun.Visibility = sun ? Visibility.Visible : Visibility.Collapsed;
            if (sun)
            {
                Geometry rays = SunGeometry(percent);
                Brush body = SunBody(percent);
                LevelSunPath.Data = LevelMiniSunPath.Data = rays;
                LevelSunPath.Fill = LevelMiniSunPath.Fill = body;
            }
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
            LevelMiniGlyph.Text = LevelGlyph.Text;
            LevelMiniGlyph.Visibility = sun ? Visibility.Collapsed : Visibility.Visible;
            LevelMiniFill.Width = 64.0 * (muted ? 0 : percent) / 100;
            levelUntil = DateTime.Now.AddSeconds(1.4);
            Recompute();
        }

        // A sun in a 16 x 16 box: a disc and eight rays that grow from nothing (switched off) to full length (brightest)
        static Geometry SunGeometry(double percent)
        {
            double level = Math.Clamp(percent, 0, 100) / 100;
            GeometryGroup group = new GeometryGroup();
            group.Children.Add(new EllipseGeometry(new Point(8, 8), 3.3, 3.3));
            double length = 3.1 * level;
            if (length > 0.25)
            {
                StreamGeometry rays = new StreamGeometry();
                using (StreamGeometryContext context = rays.Open())
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4;
                        double dx = Math.Cos(angle), dy = Math.Sin(angle);
                        context.BeginFigure(new Point(8 + dx * 5.1, 8 + dy * 5.1), false, false);
                        context.LineTo(new Point(8 + dx * (5.1 + length), 8 + dy * (5.1 + length)), true, false);
                    }
                }
                rays.Freeze();
                group.Children.Add(rays);
            }
            group.Freeze();
            return group;
        }

        // The disc is hollow when it is dim and fills in as it brightens
        static Brush SunBody(double percent)
        {
            double level = Math.Clamp(percent, 0, 100) / 100;
            return new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Clamp((level - 0.15) / 0.6, 0, 1)), 255, 255, 255));
        }

        // A short note that never queues: a new one just restarts the time.
        internal void ShowToast(string text, string glyph, double seconds)
        {
            if (Presenting) return;
            ToastText.Text = text;
            ToastGlyph.Text = glyph;
            toastUntil = DateTime.Now.AddSeconds(seconds);
            Recompute();
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

        // At the current pace the 5-hour or weekly limit would run out before it renews (announced once per window).
        internal void ShowLimitForecast(bool weekly, DateTime when, DateTime? resets)
        {
            if (!settings.ShowClaude || !settings.ShowLimitForecast) return;
            BannerItem item = new BannerItem { FromClaude = true, Seconds = Math.Max(settings.LimitAlertSeconds, 6) };
            item.Title = Strings.ForecastPace;
            item.Subtitle = weekly ? Strings.ForecastWeeklyEnds(when) : Strings.ForecastFiveHourEnds(when);
            item.GlyphBrush = Amber;
            Enqueue(item);
        }

        // A banner that waits for a tap: yes runs the action, anything else lets it go.
        internal void ShowQuestion(string title, string body, Action yes)
        {
            BannerItem item = new BannerItem { App = Installer.Name, Title = title, Subtitle = body, Seconds = 30, Glyph = "\uE896", GlyphBrush = Green, OnClick = yes };
            Enqueue(item, true);
        }

        // The update status on the island, one at a time: each replaces the one before instead of queueing behind it.
        // kind: checking, downloading (a turning arrow), found (a green arrow down), uptodate (a green check that pops), failed (an amber warning)
        internal void ShowStatus(string kind, string text, string body = null, double seconds = 0)
        {
            BannerItem item = new BannerItem { App = Installer.Name, Title = text, Subtitle = body, Seconds = seconds > 0 ? seconds : kind == "checking" ? 30 : kind == "downloading" ? 60 : Math.Max(3, settings.AlertSeconds) };
            switch (kind)
            {
                case "checking": item.Glyph = "\uE895"; item.GlyphBrush = White; item.Spin = true; break;
                case "downloading": item.Glyph = "\uE895"; item.GlyphBrush = Green; item.Spin = true; break;
                case "found": item.Glyph = "\uE896"; item.GlyphBrush = Green; break;
                case "uptodate": item.Glyph = "\uE73E"; item.GlyphBrush = Green; item.Pop = true; break;
                default: item.Glyph = "\uE7BA"; item.GlyphBrush = Amber; break;
            }
            banners.Clear();
            bannerUntil = DateTime.MinValue;
            Enqueue(item, true);
        }

        internal void ShowSystemBanner(string title, string body, string kind = null, bool urgent = false)
        {
            BannerItem item = new BannerItem { App = Installer.Name, Title = title, Subtitle = body, Seconds = urgent ? Math.Max(settings.AlertSeconds, 10) : settings.AlertSeconds, Urgent = urgent };
            ApplyKind(item, kind);
            if (kind != null && kind != "timer" && kind != "reminder") { item.Side = item.Subtitle; item.Subtitle = null; } // Battery: the percentage goes big on the right
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
                case "timer": item.Glyph = "\uE916"; item.GlyphBrush = Amber; break;
                case "reminder": item.Glyph = "\uE823"; item.GlyphBrush = Amber; break;
            }
        }

        // A big file finished downloading: a small note with the name and size on the right; a click shows it in Explorer.
        internal void ShowDownloadBanner(string title, string name, string size, Action onClick)
        {
            BannerItem item = new BannerItem { Title = title, Subtitle = name, Side = size, Seconds = settings.AlertSeconds, Glyph = "\uE896", GlyphBrush = Green, OnTap = onClick };
            Enqueue(item);
        }

        // True while the user is looking at or using the island, so an update restart waits.
        internal bool IsBusy
        {
            get { return hovering || view == View.Banner || view == View.Permission || view == View.MusicExpanded || view == View.ClaudeExpanded || view == View.TimerExpanded || view == View.QuickExpanded || view == View.ClipboardExpanded || view == View.ReminderExpanded || view == View.SystemExpanded || view == View.ExternalExpanded; }
        }

        void Enqueue(BannerItem item, bool always = false)
        {
            if (Presenting && !item.Urgent) return; // Sharing the screen: only the timer's alert
            if (muted && !item.Urgent && !always) return; // Another monitor is the active one
            if (item.Urgent) always = true;
            else if (fullscreen && settings.HideInFullscreen) return;
            if (settings.IslandQuiet && !always && !item.FromClaude && !item.FromCodex) return; // Do not disturb for the island: only what Claude asks still shows
            bool questionWaiting = DateTime.Now < bannerUntil && currentBanner?.OnClick != null; // A question keeps its place
            if (item.Urgent && !questionWaiting)
            {
                // A timer that ended does not wait behind other banners
                List<BannerItem> rest = new List<BannerItem>(banners);
                banners.Clear();
                banners.Enqueue(item);
                foreach (BannerItem other in rest) banners.Enqueue(other);
                bannerUntil = DateTime.MinValue;
                NextBanner();
                return;
            }
            if (banners.Count >= 5) banners.Dequeue(); // A burst of notifications never piles up for minutes
            banners.Enqueue(item);
            if (DateTime.Now >= bannerUntil) NextBanner();
        }

        // True while an urgent banner is on screen, so a fullscreen window does not hide it.
        bool UrgentShowing { get { return DateTime.Now < bannerUntil && currentBanner != null && currentBanner.Urgent; } }

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
                BannerGlyph.RenderTransformOrigin = new Point(0.5, 0.5);
                BannerGlyph.RenderTransform = Transform.Identity;
                if (item.Spin)
                {
                    RotateTransform turn = new RotateTransform();
                    turn.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                    BannerGlyph.RenderTransform = turn;
                }
                else if (item.Pop)
                {
                    ScaleTransform pop = new ScaleTransform(0.2, 0.2);
                    System.Windows.Media.Animation.DoubleAnimation grow = new System.Windows.Media.Animation.DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(420))
                    { EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.8 } };
                    pop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                    pop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                    BannerGlyph.RenderTransform = pop;
                }
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
            BannerAction.Visibility = item.OnClick != null ? Visibility.Visible : Visibility.Collapsed;
            BannerActionText.Text = Strings.UpdateAskButton;
            BannerSide.Text = item.Side ?? "";
            BannerSide.Foreground = item.GlyphBrush ?? White;
            BannerSide.Visibility = string.IsNullOrEmpty(item.Side) ? Visibility.Collapsed : Visibility.Visible;

            bannerUntil = DateTime.Now.AddSeconds(item.Seconds);
            Recompute();
        }
    }
}
