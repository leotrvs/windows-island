using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The music page: the song, its cover sliding in and out when it changes, the progress bar and the equalizer.
    public partial class IslandWindow
    {
        void OnMediaChanged() { UpdateMusic(); MusicPeekCheck(); Recompute(); }

        // While the island is a thin line, a change in the music (pause, play, next, previous) makes it grow for a moment,
        // with the cover, a symbol for what happened and the song's name.
        bool musicPrimed, musicWasPlaying;
        string musicTitle = "";
        readonly List<string> musicHistory = new List<string>(); // The songs heard just before this one, newest last
        DateTime musicPeekUntil = DateTime.MinValue;
        DispatcherTimer peekTimer;

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
            bool songChanged = false;
            if (title.Length > 0 && title != musicTitle)
            {
                songChanged = true;
                // The command that was just given says which way; without one, a song heard a moment ago means back
                bool recent = DateTime.Now - media.LastWayAt < TimeSpan.FromSeconds(4);
                bool back = recent ? media.LastWay == MediaService.Way.Previous : musicHistory.Contains(title);
                glyph = back ? "\uE892" : "\uE893";
                if (musicTitle.Length > 0) musicHistory.Add(musicTitle);
                if (musicHistory.Count > 8) musicHistory.RemoveAt(0);
                musicTitle = title;
            }
            else if (playing != musicWasPlaying) glyph = playing ? "\uE768" : "\uE769";
            musicWasPlaying = playing;
            if (glyph == null || !settings.ShowMusic) return;
            if (!ThinActive) return; // The normal island shows the name in its own room (see UpdateMusic)
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

        bool MusicActive
        {
            get
            {
                return settings.ShowMusic && media.HasSession && !string.IsNullOrEmpty(media.Title) &&
                       (media.IsPlaying || DateTime.Now - media.LastPlayingAt < TimeSpan.FromSeconds(settings.PausedMusicSeconds));
            }
        }

        // ---------- Content ----------

        string songShown; // The title on screen: when it changes, the cover and the name slide out and the new ones slide in

        // -1 when the song went back, 1 when it moved on
        int SongDirection(string title)
        {
            if (DateTime.Now - media.LastWayAt < TimeSpan.FromSeconds(4)) return media.LastWay == MediaService.Way.Previous ? -1 : 1;
            return musicHistory.Contains(title) ? -1 : 1;
        }

        // What is on screen now, read when a slide reaches its middle (a cover or a title that arrives while it slides is never overwritten by an older one)
        Brush artNow;
        string titleNow, artistNow;
        DateTime songNameUntil = DateTime.MinValue;
        DispatcherTimer songNameTimer;
        readonly Dictionary<FrameworkElement, int> swapToken = new Dictionary<FrameworkElement, int>();

        bool SongNameShown { get { return settings.SongNameAlways || DateTime.Now < songNameUntil; } }

        // The compact island has room for the name while it is always shown, or for a moment after the song changed
        void ApplyCompactName()
        {
            bool show = SongNameShown;
            CompactTitle.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        TranslateTransform MoveOf(FrameworkElement element)
        {
            TranslateTransform move = element.RenderTransform as TranslateTransform;
            if (move == null && (element.RenderTransform == null || element.RenderTransform.Value.IsIdentity))
                element.RenderTransform = move = new TranslateTransform();
            return move;
        }

        // The old content goes up (down when the song went back) while the new comes in from the other side, both at once and one piece after
        // the other: a copy of the old one (a picture of it) leaves while the real one arrives, so there is never a moment with nothing there.
        static ImageSource Snapshot(FrameworkElement element)
        {
            const double scale = 2;
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
                context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        void SlideSwap(FrameworkElement element, int direction, Action apply, int delayMs = 0)
        {
            TranslateTransform move = MoveOf(element);
            double distance = element.ActualHeight > 30 ? 18 : 12;
            Rect where = Rect.Empty;
            try
            {
                if (element.IsVisible && element.ActualWidth > 1 && element.ActualHeight > 1)
                    where = new Rect(element.TransformToAncestor(Stage).Transform(new Point(0, 0)), new Size(element.ActualWidth, element.ActualHeight));
            }
            catch (InvalidOperationException) { }
            if (move == null || where.IsEmpty || where.Width > 400) { apply(); return; }

            // The old content stays where it was, as a picture
            System.Windows.Shapes.Rectangle ghost = new System.Windows.Shapes.Rectangle
            {
                Width = where.Width, Height = where.Height, IsHitTestVisible = false,
                Fill = new ImageBrush(Snapshot(element)) { Stretch = Stretch.Fill },
                RenderTransform = new TranslateTransform(),
                RadiusX = (element as Border)?.CornerRadius.TopLeft ?? 0, RadiusY = (element as Border)?.CornerRadius.TopLeft ?? 0,
            };
            Canvas.SetLeft(ghost, where.X);
            Canvas.SetTop(ghost, where.Y);
            Stage.Children.Add(ghost);

            apply(); // The real one now has the new content and arrives from the other side
            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            CubicEase inOut = new CubicEase { EasingMode = EasingMode.EaseInOut }, fast = new CubicEase { EasingMode = EasingMode.EaseOut }, slow = new CubicEase { EasingMode = EasingMode.EaseIn };

            DoubleAnimation up = new DoubleAnimation(0, -distance * direction, TimeSpan.FromMilliseconds(300)) { BeginTime = delay, EasingFunction = inOut, FillBehavior = FillBehavior.HoldEnd };
            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { BeginTime = delay, EasingFunction = slow, FillBehavior = FillBehavior.HoldEnd };
            fade.Completed += (s, e) => Stage.Children.Remove(ghost);
            ((TranslateTransform)ghost.RenderTransform).BeginAnimation(TranslateTransform.YProperty, up);
            ghost.BeginAnimation(OpacityProperty, fade);

            // Until its turn comes the real one waits out of sight, so the old and the new never show together in the same place
            element.Opacity = 0;
            move.Y = distance * direction;
            DoubleAnimation arrive = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = delay, EasingFunction = fast, FillBehavior = FillBehavior.HoldEnd };
            DoubleAnimation settle = new DoubleAnimation(distance * direction, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = delay, EasingFunction = inOut, FillBehavior = FillBehavior.HoldEnd };
            settle.Completed += (s, e) =>
            {
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = 1;
                move.BeginAnimation(TranslateTransform.YProperty, null);
                move.Y = 0;
            };
            move.BeginAnimation(TranslateTransform.YProperty, settle);
            element.BeginAnimation(OpacityProperty, arrive);
        }

        // Comes in from below when the song moves on, from above when it went back
        void SlideIn(FrameworkElement element, int direction, int delayMs = 0)
        {
            TranslateTransform move = MoveOf(element);
            if (move == null) return;
            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            element.Opacity = 0; // Out of sight until its turn: no flash of the finished thing before it comes in
            move.Y = 12 * direction;
            DoubleAnimation arrive = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd };
            DoubleAnimation settle = new DoubleAnimation(12 * direction, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.HoldEnd };
            settle.Completed += (s, e) =>
            {
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = 1;
                move.BeginAnimation(TranslateTransform.YProperty, null);
                move.Y = 0;
            };
            move.BeginAnimation(TranslateTransform.YProperty, settle);
            element.BeginAnimation(OpacityProperty, arrive);
        }

        // After a moment the name goes away and the island is as small as before
        void StartSongNameTimer()
        {
            if (songNameTimer == null)
            {
                songNameTimer = new DispatcherTimer();
                songNameTimer.Tick += (s, e) =>
                {
                    songNameTimer.Stop();
                    if (settings.SongNameAlways) return;
                    DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160)) { FillBehavior = FillBehavior.HoldEnd };
                    fade.Completed += (s2, e2) =>
                    {
                        songNameUntil = DateTime.MinValue;
                        CompactTitle.BeginAnimation(OpacityProperty, null);
                        CompactTitle.Opacity = 1;
                        ApplyCompactName();
                        Recompute();
                    };
                    CompactTitle.BeginAnimation(OpacityProperty, fade);
                };
            }
            songNameTimer.Stop();
            songNameTimer.Interval = TimeSpan.FromSeconds(2.8);
            songNameTimer.Start();
        }

        void UpdateMusic()
        {
            string shownTitle = string.IsNullOrEmpty(media.Title) ? Strings.UnknownTrack : media.Title;
            bool songChanged = songShown != null && songShown != shownTitle && !string.IsNullOrEmpty(media.Title);
            songShown = shownTitle;
            int way = songChanged ? SongDirection(media.Title) : 1;
            bool expandedSeen = songChanged && presence[MusicExpanded].Value > 0.5;
            bool compactSeen = songChanged && presence[MusicCompact].Value > 0.5;

            ImageBrush artBrush = null;
            if (media.Art != null)
            {
                artBrush = new ImageBrush(media.Art) { Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapScalingMode(artBrush, BitmapScalingMode.HighQuality); // Small icons stay smooth, not blocky
            }
            artNow = artBrush != null ? Freeze(artBrush) : (Brush)FindResource("ArtPlaceholder");
            titleNow = shownTitle;
            artistNow = media.Artist;

            // A new song on the normal island: the name opens beside the cover for a moment, in the room the island already has
            bool reveal = compactSeen && settings.ShowMusic && !ThinActive && !settings.SongNameAlways && !settings.EmptyIsland;
            if (reveal)
            {
                songNameUntil = DateTime.Now.AddSeconds(2.8);
                StartSongNameTimer();
            }
            ApplyCompactName();

            if (compactSeen)
            {
                SlideSwap(CompactArt, way, () => CompactArt.Background = artNow);
                if (reveal)
                {
                    CompactTitle.Text = titleNow;
                    CompactTitle.BeginAnimation(OpacityProperty, null);
                    SlideIn(CompactTitle, way, 60);
                }
                else if (settings.SongNameAlways) SlideSwap(CompactTitle, way, () => CompactTitle.Text = titleNow, 30);
                else CompactTitle.Text = titleNow;
            }
            else
            {
                CompactArt.Background = artNow;
                CompactTitle.Text = titleNow;
            }
            if (expandedSeen)
            {
                SlideSwap(ExpandedArt, way, () => ExpandedArt.Background = artNow);
                SlideSwap(TrackTitle, way, () => TrackTitle.Text = titleNow, 30);
                SlideSwap(TrackArtist, way, () => TrackArtist.Text = artistNow, 60);
            }
            else
            {
                ExpandedArt.Background = artNow;
                TrackTitle.Text = titleNow;
                TrackArtist.Text = artistNow;
            }
            BubbleArt.Fill = artNow;
            PeekArt.Background = artNow;
            if (songChanged && presence[MusicCompact].Value > 0.5)
            {
                // The island gives a small nudge when the song changes
                DoubleAnimation nudge = new DoubleAnimation(0, 0.05, TimeSpan.FromMilliseconds(150)) { AutoReverse = true, IsAdditive = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, nudge);
                PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, nudge);
            }

            Brush accent = Freeze(new SolidColorBrush(media.Accent));
            foreach (Rectangle bar in compactBars) bar.Fill = accent;
            foreach (Rectangle bar in expandedBars) bar.Fill = accent;
            PlayGlyphPath.Data = (Geometry)FindResource(media.IsPlaying ? "PauseGlyph" : "PlayGlyph");
            if (presence[MusicExpanded].Value > 0.01) UpdateProgress();
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
    }
}
