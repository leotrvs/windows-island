using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WindowsIsland
{
    // Reads what is playing from the Windows media controls, the same source as the volume flyout,
    // so it works with Spotify, browsers and any player that shows up there.
    sealed class MediaService
    {
        public event Action Changed;

        public bool HasSession { get; private set; }
        public string Title { get; private set; } = "";
        public string Artist { get; private set; } = "";
        public BitmapSource Art { get; private set; }
        public Color Accent { get; private set; } = Colors.White;
        public bool IsPlaying { get; private set; }
        public TimeSpan Duration { get; private set; }
        public DateTime LastPlayingAt { get; private set; } = DateTime.MinValue;

        TimeSpan position;
        DateTimeOffset positionStamp = DateTimeOffset.Now;
        double rate = 1;
        int generation;

        readonly Dispatcher dispatcher;
        GlobalSystemMediaTransportControlsSessionManager manager;
        GlobalSystemMediaTransportControlsSession session;
        readonly List<GlobalSystemMediaTransportControlsSession> watched = new List<GlobalSystemMediaTransportControlsSession>();
        string sourceApp = "";

        static readonly string[] Browsers = { "chrome", "msedge", "edge", "brave", "opera", "firefox", "vivaldi", "arc" };
        static readonly Color[] TileColors =
        {
            Color.FromRgb(0x30, 0xD1, 0x58), Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0xFF, 0x9F, 0x0A),
            Color.FromRgb(0xBF, 0x5A, 0xF2), Color.FromRgb(0xFF, 0x37, 0x5F), Color.FromRgb(0x64, 0xD2, 0xFF),
            Color.FromRgb(0xFF, 0xD6, 0x0A), Color.FromRgb(0x5E, 0x5C, 0xE6),
        };

        public MediaService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        // The reported position only changes on seeks and state changes, so it is moved forward locally.
        public TimeSpan Position
        {
            get
            {
                if (!IsPlaying || Duration <= TimeSpan.Zero) return position;
                TimeSpan current = position + TimeSpan.FromTicks((long)((DateTimeOffset.Now - positionStamp).Ticks * rate));
                if (current < TimeSpan.Zero) return TimeSpan.Zero;
                return current > Duration ? Duration : current;
            }
        }

        // Made-up track for previews and documentation images, so no real listening data is ever shown.
        public void UseSample()
        {
            HasSession = true;
            Title = "Midnight Drive";
            Artist = "Neon Avenue";
            IsPlaying = true;
            LastPlayingAt = DateTime.MaxValue;
            Duration = TimeSpan.FromSeconds(222);
            position = TimeSpan.FromSeconds(78);
            positionStamp = DateTimeOffset.Now;
            rate = 0; // Frozen in place, so every image shows the same moment
            Art = SampleArt();
            Accent = AccentOf(Art);
        }

        static BitmapSource SampleArt()
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                LinearGradientBrush sky = new LinearGradientBrush(Color.FromRgb(0xFF, 0x6A, 0x3D), Color.FromRgb(0x6B, 0x2F, 0xD9), 90);
                context.DrawRectangle(sky, null, new System.Windows.Rect(0, 0, 160, 160));
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(230, 0xFF, 0xD1, 0x6B)), null, new System.Windows.Point(80, 92), 34, 34);
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x24, 0x10, 0x4A)), null, new System.Windows.Rect(0, 104, 160, 56));
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(160, 160, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        public async Task StartAsync()
        {
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch (Exception)
            {
                return; // No media controls on this system: the island simply never shows music.
            }
            manager.CurrentSessionChanged += (sender, args) => Post(Repick);
            manager.SessionsChanged += (sender, args) => Post(Rewatch);
            Rewatch();
        }

        // Windows keeps calling a paused player "current" for a while after another one starts, so the
        // island watches every session and follows whichever is actually playing.
        void Rewatch()
        {
            foreach (GlobalSystemMediaTransportControlsSession watchedSession in watched)
                watchedSession.PlaybackInfoChanged -= OnAnyPlaybackChanged;
            watched.Clear();
            try
            {
                foreach (GlobalSystemMediaTransportControlsSession candidate in manager.GetSessions())
                {
                    candidate.PlaybackInfoChanged += OnAnyPlaybackChanged;
                    watched.Add(candidate);
                }
            }
            catch (Exception)
            {
            }
            Repick();
        }

        void OnAnyPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Post(Repick);
        }

        void Repick()
        {
            GlobalSystemMediaTransportControlsSession best = PickSession();
            if (!ReferenceEquals(best, session)) Attach(best);
        }

        GlobalSystemMediaTransportControlsSession PickSession()
        {
            GlobalSystemMediaTransportControlsSession current = null;
            try { current = manager.GetCurrentSession(); }
            catch (Exception) { }

            if (current != null && IsPlayingNow(current)) return current;
            if (session != null && watched.Contains(session) && IsPlayingNow(session)) return session;
            foreach (GlobalSystemMediaTransportControlsSession candidate in watched)
                if (IsPlayingNow(candidate)) return candidate;

            // Nothing plays: keep the last one while it exists, so a paused song stays for a moment.
            if (session != null && watched.Contains(session)) return session;
            return current ?? (watched.Count > 0 ? watched[0] : null);
        }

        static bool IsPlayingNow(GlobalSystemMediaTransportControlsSession candidate)
        {
            try { return candidate.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
            catch (Exception) { return false; }
        }

        public void TogglePlayPause()
        {
            if (session == null) return;
            position = Position;
            positionStamp = DateTimeOffset.Now;
            IsPlaying = !IsPlaying; // Answer the click at once; the real state follows in an event.
            if (IsPlaying) LastPlayingAt = DateTime.Now;
            Changed?.Invoke();
            Run(() => session.TryTogglePlayPauseAsync());
        }

        public void Next() { Run(() => session.TrySkipNextAsync()); }
        public void Previous() { Run(() => session.TrySkipPreviousAsync()); }

        public void Seek(TimeSpan target)
        {
            if (session == null) return;
            position = target;
            positionStamp = DateTimeOffset.Now;
            Run(() => session.TryChangePlaybackPositionAsync(target.Ticks));
        }

        async void Run(Func<IAsyncOperation<bool>> command)
        {
            if (session == null) return;
            try { await command(); }
            catch (Exception) { }
        }

        void Post(Action action)
        {
            dispatcher.BeginInvoke(action);
        }

        void Attach(GlobalSystemMediaTransportControlsSession next)
        {
            if (session != null)
            {
                session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }

            generation++;
            session = next;
            sourceApp = "";
            try { if (session != null) sourceApp = session.SourceAppUserModelId ?? ""; }
            catch (Exception) { }

            HasSession = session != null;
            if (session == null)
            {
                Title = Artist = "";
                Art = null;
                IsPlaying = false;
                Changed?.Invoke();
                return;
            }

            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            ReadPlayback();
            ReadTimeline();
            Changed?.Invoke();
            _ = ReadPropertiesAsync(generation);
        }

        void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Post(() => { _ = ReadPropertiesAsync(generation); });
        }

        void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Post(() => { ReadPlayback(); Changed?.Invoke(); });
        }

        void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            Post(() => { ReadTimeline(); Changed?.Invoke(); });
        }

        void ReadPlayback()
        {
            if (session == null) return;
            try
            {
                GlobalSystemMediaTransportControlsSessionPlaybackInfo info = session.GetPlaybackInfo();
                bool playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                position = Position;
                positionStamp = DateTimeOffset.Now;
                if (playing || IsPlaying) LastPlayingAt = DateTime.Now;
                IsPlaying = playing;
                rate = info.PlaybackRate ?? 1;
            }
            catch (Exception)
            {
            }
        }

        void ReadTimeline()
        {
            if (session == null) return;
            try
            {
                GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
                Duration = timeline.EndTime - timeline.StartTime;
                position = timeline.Position - timeline.StartTime;
                DateTimeOffset stamp = timeline.LastUpdatedTime;
                DateTimeOffset now = DateTimeOffset.Now;
                positionStamp = stamp.Year < 2000 || stamp > now ? now : stamp;
            }
            catch (Exception)
            {
            }
        }

        // The browser extension's note about the tab that is playing: its small icon replaces the letter tile.
        ApiMediaHint hint;

        public void ApplyHint(ApiMediaHint value)
        {
            hint = value;
            if (session == null || !IsBrowser(sourceApp) || !string.IsNullOrWhiteSpace(Artist)) return;
            if (!HintMatches(Title, value) || !(value.Favicon is BitmapSource icon)) return;
            BitmapSource best = BestIcon(value);
            Art = best;
            Accent = SafeAccent(best, Accent); // The equalizer takes the site icon's color (WhatsApp green)
            Changed?.Invoke();
            if (!siteIcons.ContainsKey(HostKey(value))) _ = FetchSiteIconAsync(value);
        }

        // Some sites draw a badge into their tab icon (WhatsApp's unread count), and tab icons are tiny. The
        // site's own touch icon, when it has one, is sharper and clean; it is fetched once per site.
        readonly Dictionary<string, BitmapSource> siteIcons = new Dictionary<string, BitmapSource>();
        static readonly System.Net.Http.HttpClient web = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(4) };

        static string HostKey(ApiMediaHint h) { return (h.Host ?? "").ToLowerInvariant(); }

        BitmapSource BestIcon(ApiMediaHint h)
        {
            BitmapSource site;
            if (siteIcons.TryGetValue(HostKey(h), out site) && site != null) return site;
            return Smooth((BitmapSource)h.Favicon);
        }

        async Task FetchSiteIconAsync(ApiMediaHint value)
        {
            string host = HostKey(value);
            if (host.Length == 0 || host.IndexOf('.') < 0 || host.EndsWith(".local") || host.EndsWith(".internal") || Regex.IsMatch(host, @"^[0-9.]+$"))
            {
                siteIcons[host] = null;
                return;
            }
            siteIcons[host] = null; // Marks it as tried, so a failure is not retried while it plays
            BitmapSource icon = null;
            try
            {
                icon = await Task.Run(async () =>
                {
                    using (System.Net.Http.HttpResponseMessage response = await web.GetAsync("https://" + host + "/apple-touch-icon.png", System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK) return null;
                        long? length = response.Content.Headers.ContentLength;
                        if (length.HasValue && length.Value > 256 * 1024) return null;
                        byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (data.Length < 100 || data.Length > 256 * 1024) return null;
                        bool png = data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
                        if (!png) return null;
                        using (MemoryStream stream = new MemoryStream(data, false))
                        {
                            BitmapImage image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.DecodePixelWidth = 160;
                            image.StreamSource = stream;
                            image.EndInit();
                            image.Freeze();
                            return (BitmapSource)(image.PixelWidth >= 48 ? image : null);
                        }
                    }
                }).ConfigureAwait(true);
            }
            catch (Exception) { icon = null; }
            if (icon == null) return;
            siteIcons[host] = icon;
            if (hint == null || HostKey(hint) != host || session == null || !string.IsNullOrWhiteSpace(Artist) || !HintMatches(Title, hint)) return;
            Art = icon;
            Accent = SafeAccent(icon, Accent);
            Changed?.Invoke();
        }

        // A small site icon (16 or 32 px) is enlarged once with smooth interpolation, so it shows soft instead of blocky.
        static BitmapSource Smooth(BitmapSource icon)
        {
            try
            {
                if (icon.PixelWidth >= 160) return icon;
                const double size = 160;
                DrawingVisual visual = new DrawingVisual();
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.Fant);
                using (DrawingContext context = visual.RenderOpen())
                    context.DrawImage(icon, new System.Windows.Rect(0, 0, size, size));
                RenderTargetBitmap bitmap = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception) { return icon; }
        }

        static Color SafeAccent(BitmapSource icon, Color fallback)
        {
            try { return AccentOf(icon); } catch (Exception) { return fallback; }
        }

        static bool HintMatches(string title, ApiMediaHint value)
        {
            string a = (title ?? "").Trim(), b = (value.Title ?? "").Trim();
            if (a.Length == 0 || b.Length == 0) return false;
            b = UnreadCount.Replace(b, "");
            return a.Equals(b, StringComparison.OrdinalIgnoreCase) || a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        async Task ReadPropertiesAsync(int expected)
        {
            GlobalSystemMediaTransportControlsSession current = session;
            if (current == null) return;

            GlobalSystemMediaTransportControlsSessionMediaProperties properties;
            try { properties = await current.TryGetMediaPropertiesAsync(); }
            catch (Exception) { return; }
            if (properties == null || expected != generation) return;

            string title = properties.Title ?? "";
            string artist = string.IsNullOrEmpty(properties.Artist) ? properties.AlbumArtist ?? "" : properties.Artist;
            bool fromBrowser = IsBrowser(sourceApp);
            if (fromBrowser) title = UnreadCount.Replace(title, ""); // "(3) WhatsApp" -> "WhatsApp"
            bool trackChanged = title != Title || artist != Artist;
            Title = title;
            Artist = artist;
            Changed?.Invoke();

            // A web page that plays audio without media metadata (a WhatsApp voice message, for example)
            // has no artist, and the browser hands over its own logo as artwork. Windows does not tell
            // which tab or site it is, so the island draws a tile with the page's initial instead.
            if (fromBrowser && string.IsNullOrWhiteSpace(artist))
            {
                Color tile = TileColor(title);
                bool hasIcon = hint != null && HintMatches(title, hint) && hint.Favicon is BitmapSource;
                BitmapSource chosen = hasIcon ? BestIcon(hint) : null;
                Art = hasIcon ? chosen : LetterTile(title, tile);
                Accent = hasIcon ? SafeAccent(chosen, tile) : tile;
                Changed?.Invoke();
                return;
            }

            if (!trackChanged && Art != null) return;
            IRandomAccessStreamReference thumbnail = properties.Thumbnail;
            if (thumbnail == null)
            {
                Art = null;
                Accent = Colors.White;
                Changed?.Invoke();
                return;
            }

            Tuple<BitmapSource, Color> loaded = await Task.Run(() => LoadArtAsync(thumbnail));
            if (expected != generation || title != Title) return;
            Art = loaded?.Item1;
            Accent = loaded?.Item2 ?? Colors.White;
            Changed?.Invoke();
        }

        static readonly Regex UnreadCount = new Regex(@"^\(\d+\+?\)\s*");

        static bool IsBrowser(string app)
        {
            string lower = (app ?? "").ToLowerInvariant();
            foreach (string browser in Browsers)
                if (lower.Contains(browser)) return true;
            return false;
        }

        static Color TileColor(string title)
        {
            int hash = 17;
            foreach (char c in title.ToLowerInvariant()) hash = unchecked(hash * 31 + c);
            return TileColors[(hash & int.MaxValue) % TileColors.Length];
        }

        static BitmapSource LetterTile(string title, Color color)
        {
            string trimmed = title.Trim();
            string letter = trimmed.Length > 0 ? char.ToUpper(trimmed[0], CultureInfo.CurrentCulture).ToString() : "♪";
            const double size = 160;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                Color darker = Color.FromRgb((byte)(color.R * 0.62), (byte)(color.G * 0.62), (byte)(color.B * 0.62));
                context.DrawRectangle(new LinearGradientBrush(color, darker, 90), null, new System.Windows.Rect(0, 0, size, size));
                FormattedText text = new FormattedText(letter, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), System.Windows.FontStyles.Normal,
                        System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal),
                    86, Brushes.White, 1);
                context.DrawText(text, new System.Windows.Point((size - text.Width) / 2, (size - text.Height) / 2));
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        static async Task<Tuple<BitmapSource, Color>> LoadArtAsync(IRandomAccessStreamReference reference)
        {
            try
            {
                MemoryStream memory = new MemoryStream();
                using (IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync())
                using (Stream source = stream.AsStreamForRead())
                    await source.CopyToAsync(memory).ConfigureAwait(false);
                memory.Position = 0;

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 160;
                image.StreamSource = memory;
                image.EndInit();
                image.Freeze();
                return Tuple.Create<BitmapSource, Color>(image, AccentOf(image));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The color the equalizer takes, like iOS does: the most vivid tone in the artwork, kept bright.
        static Color AccentOf(BitmapSource image)
        {
            BitmapSource small = new TransformedBitmap(
                new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0),
                new ScaleTransform(24.0 / image.PixelWidth, 24.0 / image.PixelHeight));
            int width = small.PixelWidth, height = small.PixelHeight;
            byte[] pixels = new byte[width * height * 4];
            small.CopyPixels(pixels, width * 4, 0);

            double r = 0, g = 0, b = 0, total = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double pb = pixels[i], pg = pixels[i + 1], pr = pixels[i + 2];
                double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
                double saturation = max <= 0 ? 0 : (max - min) / max;
                double weight = saturation * saturation * (max / 255) + 0.01;
                r += pr * weight; g += pg * weight; b += pb * weight; total += weight;
            }
            r /= total; g /= total; b /= total;

            double peak = Math.Max(r, Math.Max(g, b));
            double low = Math.Min(r, Math.Min(g, b));
            if (peak < 1 || (peak - low) / peak < 0.18) return Color.FromRgb(235, 235, 240); // Gray artwork
            double scale = Math.Max(1, 235 / peak);
            return Color.FromRgb((byte)Math.Min(255, r * scale), (byte)Math.Min(255, g * scale), (byte)Math.Min(255, b * scale));
        }
    }
}
