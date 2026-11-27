using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The README images, animation and clips: made-up states drawn to files, never seen by the user.
    public partial class IslandWindow
    {
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
            cascadeOff = true;
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

            // The quick clipboard page, with made-up texts (the real clipboard is never read for the images)
            clipboardSample = true;
            clipboardSampleItems = new List<ClipboardHistory.Item>
            {
                new ClipboardHistory.Item(Strings.SampleClip1, DateTime.Now.AddSeconds(-12)),
                new ClipboardHistory.Item(Strings.SampleClip2, DateTime.Now.AddMinutes(-2)),
                new ClipboardHistory.Item(Strings.SampleClip3, DateTime.Now.AddMinutes(-4)),
            };
            settings.ClipboardHistory = true;
            UpdateClipboard(true);
            forced = View.ClipboardExpanded;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "clipboardexpanded" + suffix + ".png"));
            settings.ClipboardHistory = false;

            // The reminders page and a finished download, with made-up data (nothing is read or saved)
            reminderSample = true;
            DateTime sampleDay = DateTime.Today;
            reminderSampleItems = new List<ReminderStore.Item>
            {
                new ReminderStore.Item { Id = "a", Text = Strings.SampleReminder1, DueUtc = sampleDay.AddHours(15).ToUniversalTime() },
                new ReminderStore.Item { Id = "b", Text = Strings.SampleReminder2, DueUtc = sampleDay.AddHours(18).AddMinutes(30).ToUniversalTime() },
                new ReminderStore.Item { Id = "c", Text = Strings.SampleReminder3, DueUtc = sampleDay.AddDays(1).AddHours(9).ToUniversalTime() },
            };
            UpdateReminders(true);
            forced = View.ReminderExpanded;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "reminderexpanded" + suffix + ".png"));
            reminderSampleItems = new List<ReminderStore.Item>();
            UpdateReminders(true);
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "reminderempty" + suffix + ".png"));
            reminderSample = false;

            banners.Clear();
            bannerUntil = DateTime.MinValue;
            ShowDownloadBanner(Strings.DownloadDone, Strings.SampleDownloadName, DownloadTracker.FormatSize(230L * 1024 * 1024), null);
            forced = View.Banner;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "download" + suffix + ".png"));
            forced = null;

            RenderThinStates(folder, suffix);
        }

        // Puts the thin line into one state (for the README images and animation), with made-up data.
        void SetThin(Thin kind, Color color, double fraction, bool gauges = false, Activity bubbleActivity = Activity.None, bool privacyDots = false)
        {
            renderThin = true;
            renderGauges = gauges;
            renderBubble = bubbleActivity;
            minimalFixed = true;
            minimalKind = kind;
            minimalColor = color;
            minimalFraction = fraction;
            minimalHasContent = kind == Thin.Focus;
            ApplyTargets();
            if (gauges)
            {
                ThinCpuRing.Data = Arc(34, 14, 1.6);
                ThinMemRing.Data = Arc(61, 14, 1.6);
                ThinCpuRing.Stroke = MonitorBlue;
                ThinMemRing.Stroke = MonitorGreen;
            }
            if (privacyDots)
            {
                ThinMic.Visibility = Visibility.Visible;
                ThinCam.Visibility = Visibility.Visible;
            }
        }

        void ThinShot(string folder, string suffix, string name, Thin kind, Color color, double fraction, double at, bool gauges = false, Activity bubbleActivity = Activity.None, bool privacyDots = false)
        {
            renderThin = true;
            renderGauges = gauges;
            renderBubble = bubbleActivity;
            forced = View.Minimal;
            Recompute();
            SetThin(kind, color, fraction, gauges, bubbleActivity, privacyDots);
            clock = at;
            SnapAll();
            ApplyFrame();
            bubbleShift.Snap(bubbleShift.Target);
            AnimateMinimal();
            ApplyFrame();
            Root.UpdateLayout();
            AnimateMinimal();
            ApplyFrame();
            Root.UpdateLayout();
            SaveSnapshot(System.IO.Path.Combine(folder, "thin-" + name + suffix + ".png"));
        }

        // Every state of the thin line, one image each
        void RenderThinStates(string folder, string suffix)
        {
            ThinShot(folder, suffix, "idle", Thin.None, Colors.White, 0, 0);
            ThinShot(folder, suffix, "claude", Thin.ClaudeWorking, ThinClay, 0, 0.55);
            ThinShot(folder, suffix, "waiting", Thin.ClaudeWaiting, ThinClay, 0, 0);
            ThinShot(folder, suffix, "music", Thin.Music, media.Accent, 0, 0.3);
            ThinShot(folder, suffix, "timer", Thin.Timer, ThinYellow, 0.58, 0);
            ThinShot(folder, suffix, "progress", Thin.Progress, ThinBlue, 0.64, 0);
            ThinShot(folder, suffix, "charging", Thin.Charging, ThinGreen, 0.54, 0);
            ThinShot(folder, suffix, "alert", Thin.Alert, ThinRed, 0, 0);
            ThinShot(folder, suffix, "recording", Thin.Recording, ThinRed, 0, 0);
            ThinShot(folder, suffix, "offline", Thin.Offline, ThinRed, 0, 0);
            ThinShot(folder, suffix, "done", Thin.Done, ThinGreen, 0, 0);
            ThinShot(folder, suffix, "focus", Thin.Focus, Colors.White, 0, 0);
            ThinShot(folder, suffix, "gauges", Thin.None, Colors.White, 0, 0, true);
            ThinShot(folder, suffix, "privacy", Thin.None, Colors.White, 0, 0, false, Activity.None, true);
            ThinShot(folder, suffix, "bubble", Thin.ClaudeWorking, ThinClay, 0, 0.55, false, Activity.Music);

            // A change in the music, and the volume, on the thin line
            PeekGlyph.Text = "\uE893";
            PeekTitle.Text = media.Title;
            forced = View.MusicPeek;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            SaveSnapshot(System.IO.Path.Combine(folder, "thin-peek" + suffix + ".png"));

            LevelMiniGlyph.Text = "\uE767";
            LevelMiniFill.Width = 64.0 * 0.65;
            forced = View.LevelMini;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            ApplyFrame();
            SaveSnapshot(System.IO.Path.Combine(folder, "thin-level" + suffix + ".png"));
            renderThin = false;
            minimalFixed = false;
        }

        void SnapAll()
        {
            foreach (Spring spring in new[] { width, height, shown, stateScale, bubbleScale, bubbleOpacity, privacyOpacity, thinGaugesOpacity, sessionArc, weeklyArc })
                spring.Snap(spring.Target);
            foreach (Spring spring in presence.Values) spring.Snap(spring.Target);
        }

        // Records the README animation: the island going through its states, one PNG per frame, from sample data.
        public void RenderAnimation(string folder)
        {
            cascadeOff = true;
            Directory.CreateDirectory(folder);
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;
            const int fps = 20, substeps = 3;
            (View view, double seconds, Action setup)[] script =
            {
                (View.Idle, 0.8, null), (View.MusicCompact, 2.2, null), (View.MusicExpanded, 2.4, null), (View.ClaudeExpanded, 2.6, null),
                (View.TimerExpanded, 1.8, null), (View.ClaudeCompact, 2.4, null), (View.Banner, 2.4, null),
                // The thin line: Claude working, music, a change of song, the monitor's rings, the volume and a download
                (View.Minimal, 2.2, () => SetThin(Thin.ClaudeWorking, ThinClay, 0)),
                (View.Minimal, 1.8, () => SetThin(Thin.Music, media.Accent, 0)),
                (View.MusicPeek, 1.8, () => { PeekGlyph.Text = "\uE893"; PeekTitle.Text = media.Title; }),
                (View.Minimal, 1.6, () => SetThin(Thin.None, Colors.White, 0, true)),
                (View.LevelMini, 1.4, () => { LevelMiniGlyph.Text = "\uE767"; LevelMiniFill.Width = 64.0 * 0.65; }),
                (View.Minimal, 1.4, () => SetThin(Thin.Progress, ThinBlue, 0.64)),
                (View.Idle, 1.0, null),
            };
            forced = View.Idle;
            Recompute();
            SnapAll();
            ApplyFrame();
            Root.UpdateLayout();
            int frame = 0;
            foreach ((View view, double seconds, Action setup) step in script)
            {
                renderThin = step.setup != null;
                if (!renderThin) { minimalFixed = false; renderGauges = false; renderBubble = Activity.None; }
                forced = step.view;
                Recompute();
                step.setup?.Invoke();
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

        // One short animation per state for the README: the island at rest, the state appearing, held, and back to rest.
        // Each clip is a folder of numbered PNG frames, cropped to the same size, from sample data.
        public void RenderClips(string folder)
        {
            Directory.CreateDirectory(folder);
            settings.MaximizedActivitiesThin = false; // Whatever window is in front while the clips are made must not turn the island into the thin line
            double[] levels = { 0.55, 0.95, 0.4, 0.75 };
            Array.Copy(levels, eqLevel, levels.Length);
            BuildExternal(new List<ApiActivity>
            {
                new ApiActivity { Id = "build", App = "PowerShell", Title = "Build", Text = "compiling", Progress = 40, IconGlyph = LocalApi.Glyph("build"), State = "running", Updated = DateTime.UtcNow },
                new ApiActivity { Id = "dl", App = "curl", Title = "Download", Text = "", Progress = 72, IconGlyph = LocalApi.Glyph("download"), State = "running", Updated = DateTime.UtcNow },
            });
            BatteryCompactText.Text = "54%";
            LevelGlyph.Text = ""; LevelFill.Width = 84.5; LevelText.Text = "65%";
            LevelMiniGlyph.Text = "\uE767"; LevelMiniFill.Width = 64.0 * 0.65;
            UpdateSystemPage();
            BannerTitle.Text = Strings.ClaudeDone;
            BannerSubtitle.Text = Strings.SampleSession;
            LayoutBars(compactBars, CompactEq.Height);
            LayoutBars(expandedBars, ExpandedEq.Height);

            List<(string name, View rest, Action apply, double seconds)> clips = new List<(string, View, Action, double)>();
            Dictionary<string, Action<double>> steps = new Dictionary<string, Action<double>>(); // What changes on every frame of a clip, by the time held (0 to 1)
            Action<View> viewClip = v => clips.Add((v.ToString().ToLowerInvariant(), View.Idle, () => { forced = v; Recompute(); }, 3.0));
            foreach (View v in new[] { View.MusicCompact, View.ClaudeCompact, View.CodexCompact, View.BatteryCompact, View.ExternalCompact, View.SystemCompact,
                                       View.ExternalExpanded, View.MusicExpanded, View.ClaudeExpanded, View.TimerExpanded, View.SystemExpanded })
                viewClip(v);

            // Volume and brightness, each going up and down (the volume ends muted), in the island and on the thin line
            Func<double, double> ease = t => t * t * (3 - 2 * t);
            Func<double, double> volumeAt = t => t < 0.4 ? 15 + 70 * ease(t / 0.4) : t < 0.7 ? 85 - 40 * ((t - 0.4) / 0.3) : 45;
            Func<double, double> brightAt = t => t < 0.5 ? 5 + 90 * ease(t / 0.5) : 95 - 55 * ((t - 0.5) / 0.5);
            Action<string, View, View, LevelService.Kind> levelClip = (name, rest, view, kind) =>
            {
                clips.Add((name, rest, () => { forced = view; Recompute(); }, 3.6));
                steps[name] = t => ShowLevel(kind, (int)Math.Round(kind == LevelService.Kind.Volume ? volumeAt(t) : brightAt(t)), kind == LevelService.Kind.Volume && t > 0.82);
            };
            levelClip("level-volume", View.Idle, View.Level, LevelService.Kind.Volume);
            levelClip("level-brightness", View.Idle, View.Level, LevelService.Kind.Brightness);
            levelClip("thin-level-volume", View.Minimal, View.LevelMini, LevelService.Kind.Volume);
            levelClip("thin-level-brightness", View.Minimal, View.LevelMini, LevelService.Kind.Brightness);

            // A pomodoro: the time counts down, the phase shows
            clips.Add(("quick", View.Idle, () => { quickSample = true; quickMicMuted = true; quickSpeakerMuted = false; quickVolume = 62; UpdateQuick(true); forced = View.QuickExpanded; Recompute(); }, 3.2));
            clips.Add(("pomodoro", View.Idle, () => { timer.FocusMinutes = 25; timer.BreakMinutes = 5; timer.LongBreakMinutes = 15; timer.StartPomodoro(); UpdateTimerPage(); forced = View.TimerExpanded; Recompute(); }, 3.4));
            clips.Add(("pomodoro-compact", View.Idle, () => { timer.StartPomodoro(); UpdateTimerPage(); forced = View.TimerCompact; Recompute(); }, 3.0));

            // Banners: a Claude alert, a Windows notification, Claude needing you, the battery and the limits
            Action<BannerItem> bannerClip = null;
            clips.Add(("banner", View.Idle, () => { forced = View.Banner; Recompute(); }, 3.2));
            clips.Add(("notification", View.Idle, () => { forced = View.Banner; Recompute(); banners.Enqueue(new BannerItem { App = "Mail", Title = Strings.SampleNotificationTitle, Subtitle = Strings.SampleNotificationBody, Seconds = 5 }); NextBanner(); }, 3.4));
            clips.Add(("banner-waiting", View.Idle, () => { forced = View.Banner; Recompute(); banners.Enqueue(new BannerItem { FromClaude = true, Title = Strings.ClaudeNeedsYou, Subtitle = Strings.SampleQuestion, Seconds = 5 }); NextBanner(); }, 3.4));
            // The update on the island: looking (a turning arrow), a new version (with its button), up to date (a check that pops)
            clips.Add(("update-checking", View.Idle, () => { forced = View.Banner; Recompute(); ShowStatus("checking", Strings.UpdateChecking); }, 3.0));
            steps["update-checking"] = tt => { BannerGlyph.RenderTransformOrigin = new Point(0.5, 0.5); BannerGlyph.RenderTransform = new RotateTransform(360 * 2 * tt); };
            clips.Add(("update-found", View.Idle, () => { forced = View.Banner; Recompute(); ShowQuestion(Strings.UpdateAskTitle("4.5.0"), Strings.UpdateAskBody, () => { }); }, 3.0));
            clips.Add(("update-uptodate", View.Idle, () => { forced = View.Banner; Recompute(); ShowStatus("uptodate", Strings.UpToDate); }, 3.0));
            steps["update-uptodate"] = tt =>
            {
                double grow = Math.Min(1, tt / 0.2);
                double scale = 0.2 + 0.8 * grow + (grow >= 1 ? 0 : 0.15 * Math.Sin(grow * Math.PI));
                BannerGlyph.RenderTransformOrigin = new Point(0.5, 0.5);
                BannerGlyph.RenderTransform = new ScaleTransform(scale, scale);
            };
            foreach (string kind in new[] { "charging", "low" })
            {
                string k = kind;
                clips.Add(("battery-" + k, View.Idle, () =>
                {
                    forced = View.Banner; Recompute();
                    BannerItem alert = new BannerItem { App = Installer.Name, Title = k == "low" ? Strings.BatteryLow : Strings.BatteryCharging, Side = k == "low" ? "18%" : "54%", Seconds = 5 };
                    ApplyKind(alert, k);
                    banners.Enqueue(alert);
                    NextBanner();
                }, 3.2));
            }
            DateTime fiveReset = DateTime.Now.AddHours(2.5), weekReset = DateTime.Now.Date.AddDays(3).AddHours(9);
            (string key, string title, string sub, string side, Brush color)[] specs =
            {
                ("75", Strings.LimitFiveHour, Strings.Resets(fiveReset), "75%", RingBrush(75)),
                ("90", Strings.LimitFiveHour, Strings.Resets(fiveReset), "90%", RingBrush(90)),
                ("100", Strings.LimitReachedTitle, Strings.WindowFiveHour + " · " + Strings.Resets(fiveReset), "100%", RingBrush(100)),
                ("reset5", Strings.LimitRenewed(Strings.LimitFiveHour), Strings.LimitFull, "0%", Green),
            };
            foreach (var spec in specs)
            {
                var sp = spec;
                clips.Add(("usage-" + sp.key, View.Idle, () =>
                {
                    forced = View.Banner; Recompute();
                    banners.Enqueue(new BannerItem { FromClaude = true, Title = sp.title, Subtitle = sp.sub, Side = sp.side, GlyphBrush = sp.color, Seconds = 5 });
                    NextBanner();
                }, 3.2));
            }
            foreach (string style in new[] { "Bars", "Numbers" })
            {
                string st = style;
                clips.Add(("systemcompact-" + st.ToLowerInvariant(), View.Idle, () => { settings.MonitorStyle = st; UpdateSystemMonitor(); forced = View.SystemCompact; Recompute(); }, 2.6));
            }

            // The thin line: from a plain line, into each state
            View line = View.Minimal;
            Action<string, Thin, Color, double, bool, Activity> thinClip = (name, kind, color, fraction, gauges, bubbleActivity) =>
                clips.Add(("thin-" + name, line, () => { renderThin = true; renderGauges = gauges; renderBubble = bubbleActivity; forced = View.Minimal; Recompute(); SetThin(kind, color, fraction, gauges, bubbleActivity); }, 2.8));
            thinClip("claude", Thin.ClaudeWorking, ThinClay, 0, false, Activity.None);
            thinClip("waiting", Thin.ClaudeWaiting, ThinClay, 0, false, Activity.None);
            thinClip("music", Thin.Music, media.Accent, 0, false, Activity.None);
            thinClip("timer", Thin.Timer, ThinYellow, 1.0, false, Activity.None);
            thinClip("progress", Thin.Progress, ThinBlue, 0.0, false, Activity.None);
            thinClip("charging", Thin.Charging, ThinGreen, 0.1, false, Activity.None);
            steps["thin-timer"] = t => minimalFraction = 1.0 - 0.7 * t;     // The line empties
            steps["thin-progress"] = t => minimalFraction = 0.02 + 0.95 * t; // The line fills
            steps["thin-charging"] = t => minimalFraction = 0.1 + 0.6 * t;
            thinClip("alert", Thin.Alert, ThinRed, 0, false, Activity.None);
            thinClip("recording", Thin.Recording, ThinRed, 0, false, Activity.None);
            thinClip("offline", Thin.Offline, ThinRed, 0, false, Activity.None);
            thinClip("done", Thin.Done, ThinGreen, 0, false, Activity.None);
            thinClip("focus", Thin.Focus, Colors.White, 0, false, Activity.None);
            thinClip("gauges", Thin.None, Colors.White, 0, true, Activity.None);
            thinClip("bubble", Thin.ClaudeWorking, ThinClay, 0, false, Activity.Music);
            clips.Add(("thin-peek", line, () => { PeekGlyph.Text = "\uE893"; PeekTitle.Text = media.Title; forced = View.MusicPeek; Recompute(); }, 2.6));
            clips.Add(("thin-level", line, () => { forced = View.LevelMini; Recompute(); }, 2.4));

            // Several chats, a chat that waits, and a permission request
            for (int i = 0; i < 2; i++)
            {
                int chats = i;
                string chatName = chats == 0 ? "chats-working" : "chats-waiting";
                foreach (View chatView in new[] { View.ClaudeCompact, View.ClaudeExpanded })
                {
                    View cv = chatView;
                    clips.Add((chatName + (cv == View.ClaudeCompact ? "-compact" : "-expanded"), View.Idle, () =>
                    {
                        claude.UseSampleChats(chats);
                        lastCompactText = null; lastStatus = null;
                        forced = cv;
                        UpdateClaude();
                        UpdateClaudeLive();
                        Recompute();
                    }, 3.0));
                }
            }
            clips.Add(("permission", View.Idle, () =>
            {
                settings.PermissionPrompts = true;
                ApiPermission sample = new ApiPermission { Tool = "mcp__supabase__apply_migration", Summary = "create table invoices (id uuid primary key default gen_random_uuid(), amount numeric not null, paid boolean default false)" };
                sample.Closed += () => { };
                ShowPermission(sample);
                forced = View.Permission;
                Recompute();
            }, 3.4));

            // A song change: the cover and the name go up and the new ones come in from below; on the normal island the name opens for a moment
            bool songChangeDone = false;
            Action<bool> songChange = (wide) =>
            {
                media.UseSample();
                songShown = null; // The first song is not a change
                UpdateMusic();
                songChangeDone = false;
                forced = wide ? View.MusicExpanded : View.MusicCompact;
                Recompute();
            };
            clips.Add(("song-change", View.MusicCompact, () => songChange(false), 4.6));
            clips.Add(("song-change-expanded", View.MusicExpanded, () => songChange(true), 2.4));
            Action<double> songStep = tt =>
            {
                if (songChangeDone || tt < 0.12) return;
                songChangeDone = true;
                media.UseSampleNext();
                UpdateMusic();
                Recompute();
            };
            steps["song-change"] = songStep;
            steps["song-change-expanded"] = songStep;

            const int fps = 20, substeps = 3;
            string only = Environment.GetEnvironmentVariable("ISLAND_CLIPS_ONLY"); // For trying one clip: a name prefix
            foreach ((string name, View rest, Action apply, double seconds) clip in clips)
            {
                if (!string.IsNullOrEmpty(only) && !clip.name.StartsWith(only)) continue;
                string dir = System.IO.Path.Combine(folder, clip.name);
                Directory.CreateDirectory(dir);

                // Pass one: where the island ends up, to cut every frame of the clip at the same size
                ClipRest(clip.rest);
                clip.apply();
                SnapAll();
                ApplyFrame();
                bubbleShift.Snap(bubbleShift.Target);
                Root.UpdateLayout();
                ApplyFrame();
                Root.UpdateLayout();
                Rect crop = ClipBounds();
                crop.Inflate(clip.name.StartsWith("song-") ? 60 : 24, 20);
                crop.Intersect(new Rect(Root.RenderSize));
                crop = new Rect(Math.Floor(crop.X), Math.Floor(crop.Y), Math.Ceiling(crop.Width / 2) * 2, Math.Ceiling(crop.Height / 2) * 2);

                // Pass two: rest, the state arriving, held, and back to rest
                ClipRest(clip.rest);
                SampleClock.Offset = TimeSpan.Zero;
                int frame = 0;
                Action<double, Action<double>> run = (seconds, step) =>
                {
                    int count = (int)Math.Round(seconds * fps);
                    for (int i = 0; i < count; i++)
                    {
                        SampleClock.Offset += TimeSpan.FromSeconds(1.0 / fps); // The live times in the clip count up
                        step?.Invoke(count > 1 ? (double)i / (count - 1) : 1);
                        for (int k = 0; k < substeps; k++)
                        {
                            bool moving, continuous;
                            Advance(1.0 / (fps * substeps), out moving, out continuous);
                        }
                        if (clip.name.StartsWith("song-"))
                        {
                            // These clips use WPF's own animations, which run on the clock: let it run, one frame at a time
                            System.Threading.Thread.Sleep(1000 / fps - 4);
                            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                        }
                        Root.UpdateLayout();
                        SaveClipFrame(System.IO.Path.Combine(dir, "frame" + frame++.ToString("0000") + ".png"), crop);
                    }
                };
                Action<double> step;
                steps.TryGetValue(clip.name, out step);
                run(0.4, null);
                clip.apply();
                run(clip.seconds, step);
                ClipRest(clip.rest, false); // Back to rest, animated
                run(0.7, null);
            }
            StopLoop();
        }

        // The island at rest between clips: nothing queued, nothing forced but the resting view.
        // A clip starts from a page that has not come in yet
        void CascadeReset()
        {
            foreach (FrameworkElement panel in cascadeStart.Keys.ToList())
            {
                Panel host = panel as Panel;
                if (host != null && host.Children.Count == 1 && host.Children[0] is Panel inner) host = inner;
                if (host != null) CascadeRestore(host);
            }
            cascadeStart.Clear();
        }

        void ClipRest(View rest, bool snap = true)
        {
            CascadeReset();
            songNameUntil = DateTime.MinValue;
            if (songNameTimer != null) songNameTimer.Stop();
            ApplyCompactName();
            if (timer.Running) timer.Stop();
            banners.Clear();
            bannerUntil = DateTime.MinValue;
            toastUntil = DateTime.MinValue;
            levelUntil = DateTime.MinValue;
            musicPeekUntil = DateTime.MinValue;
            renderThin = rest == View.Minimal;
            renderGauges = false;
            renderBubble = Activity.None;
            minimalFixed = renderThin;
            forced = rest;
            Recompute();
            if (rest == View.Minimal) SetThin(Thin.None, Colors.White, 0);
            if (!snap) return;
            SnapAll();
            ApplyFrame();
            bubbleShift.Snap(bubbleShift.Target);
            Root.UpdateLayout();
        }

        Rect ClipBounds()
        {
            Rect bounds = Pill.TransformToAncestor(Root).TransformBounds(new Rect(Pill.RenderSize));
            if (Bubble.Opacity > 0.01) bounds.Union(Bubble.TransformToAncestor(Root).TransformBounds(new Rect(Bubble.RenderSize)));
            if (ThinGauges.Opacity > 0.01) bounds.Union(ThinGauges.TransformToAncestor(Root).TransformBounds(new Rect(ThinGauges.RenderSize)));
            return bounds;
        }

        void SaveClipFrame(string file, Rect crop)
        {
            const double scale = 1.5;
            RenderTargetBitmap full = new RenderTargetBitmap((int)(Root.ActualWidth * scale), (int)(Root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            full.Render(Root);
            Int32Rect area = new Int32Rect((int)(crop.X * scale), (int)(crop.Y * scale), (int)(crop.Width * scale), (int)(crop.Height * scale));
            DrawingVisual card = new DrawingVisual();
            using (DrawingContext context = card.RenderOpen())
            {
                Rect rect = new Rect(0, 0, area.Width, area.Height);
                context.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xEE, 0xEC, 0xF6), Color.FromRgb(0xD5, 0xD9, 0xEA), 90), null, rect);
                context.DrawImage(new CroppedBitmap(full, area), rect);
            }
            RenderTargetBitmap output = new RenderTargetBitmap(area.Width, area.Height, 96, 96, PixelFormats.Pbgra32);
            output.Render(card);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using (FileStream stream = File.Create(file)) encoder.Save(stream);
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
            if (ThinGauges.Opacity > 0.01)
                bounds.Union(ThinGauges.TransformToAncestor(Root).TransformBounds(new Rect(ThinGauges.RenderSize)));
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
    }
}
