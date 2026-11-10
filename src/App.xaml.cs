using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WindowsIsland
{
    public partial class App : Application
    {
#if UPDATE_TEST
        static readonly TimeSpan FirstUpdateCheck = TimeSpan.FromSeconds(3);
#else
        static readonly TimeSpan FirstUpdateCheck = TimeSpan.FromSeconds(30);
#endif
        static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(1);
        const int UpdateHandoverMs = 8000;

        Mutex singleInstance;
        bool ownsMutex;
        Tray tray;
        Settings settings;
        MediaService media;
        ClaudeService claude;
        CodexService codex;
        TimerService timer;
        PrivacyService privacy;
        BatteryService battery;
        LevelService levels;
        WindowsOsd osd;
        DeviceService devices;
        HotkeyService hotkey;
        FocusService focus;
        internal FocusService Focus { get { return focus; } }
        internal static bool HotkeyFailed; // The chosen shortcut could not be registered
        internal BatteryService Battery { get { return battery; } }
        LocalApi api;
        internal LocalApi Api { get { return api; } } // Activities and alerts from scripts and apps
        SteamService steam;
        ObsService obs;
        GameBarService gameBar;
        FootballService football;
        string footballKey = "";

        // Starts, restarts (the team or the leagues changed) or stops the football score
        void ApplyFootball(bool sample)
        {
            string key = settings.ShowFootball && !sample ? settings.FootballTeam + "|" + settings.FootballLeagues : "";
            if (key == footballKey) return;
            footballKey = key;
            if (key.Length > 0) football.Start(settings.FootballTeam, settings.FootballLeagues);
            else football.Stop();
        }
        WindowsUpdateService windowsUpdate;
        DiscordService discord;
        internal DiscordService Discord { get { return discord; } } // Muted or deafened in Discord
        ScreenshotService screenshots;
        ClipboardService clipboard;
        DownloadWatcher downloads;
        ReminderService reminders;
        internal ReminderService Reminders { get { return reminders; } } // The reminders: text and time kept in reminders.json on this PC
        internal ClipboardService ClipboardList { get { return clipboard; } } // The quick clipboard: the last texts copied, in memory only
        readonly List<IslandWindow> islands = new List<IslandWindow>();
        readonly DispatcherTimer followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        readonly DispatcherTimer muteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) }; // Which island is active, only with "only on the active monitor"
        DispatcherTimer updateTimer;
        UpdateFlow updates;
        SettingsWindow settingsWindow;
        EventWaitHandle showSettings;
        const string ShowSettingsEvent = @"Local\WindowsIsland.ShowSettings";

        // Arguments:
        //   --preview=<State>   shows one state with sample data (Idle, MusicCompact, ClaudeExpanded...)
        //   --clips <folder>    writes one short animation per state (as numbered PNG frames in a folder each), then quits
        //   --frames <folder>   writes the README animation as numbered PNG frames with sample data, then quits
        //   --dev               runs beside the installed copy (its own instance), for testing a build
        //   --render <folder>   writes every state to PNG files with sample data, then quits
        //   --lang=en|pt        forces the language
        //   --portable          runs without installing
        //   --uninstall         removes the app, its shortcuts and settings
        //   --after-update      started by the previous version right after it replaced itself
        //   show, done, end, alert, token, help   the command line for the local API (see docs/API.md), then quits
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (ApiCli.IsCommand(e.Args))
            {
                Shutdown(ApiCli.Run(e.Args));
                return;
            }
            // The island is tiny, so drawing it on the CPU costs nothing and spares the ~120 MB
            // that a Direct3D device takes. Measured: 166 MB private memory with GPU, 43 MB without.
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            string preview = null, render = null, frames = null, clips = null;
            bool portable = false, uninstall = false, afterUpdate = false, afterCrash = false, installNow = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg.StartsWith("--preview=", StringComparison.OrdinalIgnoreCase)) preview = arg.Substring(10);
                else if (arg.Equals("--render", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) render = e.Args[++i];
                else if (arg.Equals("--frames", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) frames = e.Args[++i];
                else if (arg.Equals("--clips", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) clips = e.Args[++i];
                else if (arg.Equals("--portable", StringComparison.OrdinalIgnoreCase)) portable = true;
                else if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                else if (arg.Equals("--after-update", StringComparison.OrdinalIgnoreCase)) afterUpdate = true;
                else if (arg.Equals("--after-crash", StringComparison.OrdinalIgnoreCase)) afterCrash = true;
                else if (arg.Equals("--install-now", StringComparison.OrdinalIgnoreCase)) installNow = true;
                else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(arg.Substring(7));
            }
            bool sample = preview != null || render != null || frames != null || clips != null;

            int report = Array.IndexOf(e.Args, "--report"); // Writes the report that Settings copies for a problem, then quits
            if (report >= 0 && report + 1 < e.Args.Length)
            {
                System.IO.File.WriteAllText(e.Args[report + 1], Diagnostics.Report(Settings.Load()));
                Shutdown();
                return;
            }

            int diagnose = Array.IndexOf(e.Args, "--diagnose-codex");
            if (diagnose >= 0 && diagnose + 1 < e.Args.Length)
            {
                System.IO.File.WriteAllText(e.Args[diagnose + 1], new CodexService(Dispatcher).Diagnose());
                Shutdown();
                return;
            }
            if (Array.IndexOf(e.Args, "--install-silent") >= 0)
            {
                Installer.Install();
                Shutdown();
                return;
            }
            if (uninstall)
            {
                Installer.RunUninstall();
                Shutdown();
                return;
            }
            // A downloaded copy offers to install itself; --portable runs it in place.
            if (!sample && !portable && !afterUpdate && Array.IndexOf(e.Args, "--dev") < 0 && !Installer.IsInstalledCopy)
            {
                if (installNow)
                {
                    try { Installer.Install(); }
                    catch (Exception) { MessageBox.Show(Strings.InstallFailed, Installer.Name, MessageBoxButton.OK, MessageBoxImage.Warning); }
                }
                else Installer.RunInteractive();
                Shutdown();
                return;
            }

            bool dev = Array.IndexOf(e.Args, "--dev") >= 0; // A development copy that can run beside the installed one
            if (!sample) Diagnostics.Install(this, dev);
            singleInstance = new Mutex(true, sample || dev ? @"Local\WindowsIsland.Preview." + Guid.NewGuid() : @"Local\WindowsIsland.SingleInstance", out ownsMutex);
            if (!ownsMutex && (afterUpdate || afterCrash)) ownsMutex = WaitForPreviousVersion();
            if (!ownsMutex)
            {
                // Opening the shortcut while the island runs brings up its settings.
                try
                {
                    using (EventWaitHandle show = EventWaitHandle.OpenExisting(ShowSettingsEvent)) show.Set();
                }
                catch (Exception)
                {
                }
                Shutdown();
                return;
            }

            settings = sample ? new Settings() : Settings.Load();
            if (!sample && !settings.OnboardingDone)
                settings.Save(); // Started again (new, or from before the welcome): saved at once, so it is not started again next time
            if (!sample) Strings.Use(settings.Language);
            media = new MediaService(Dispatcher);
            claude = new ClaudeService(Dispatcher);
            codex = new CodexService(Dispatcher);
            timer = new TimerService(Dispatcher);
            Action<WatchedActivity> watched = w =>
            {
                if (api == null) return;
                if (w.Gone) { api.End(w.Id); return; }
                api.Post(new ApiActivity
                {
                    Id = w.Id,
                    App = w.App,
                    Title = w.Title,
                    Text = w.Text,
                    Progress = w.Progress,
                    IconGlyph = w.Glyph,
                    State = w.Done ? "done" : "running",
                    Updated = DateTime.UtcNow,
                });
            };
            football = new FootballService(Dispatcher);
            football.Updated += watched;
            football.Goal += (title, text) => MainIsland?.ShowApiAlert(new ApiAlert { App = Strings.MenuFootball, Title = title, Text = text, IconGlyph = LocalApi.Glyph("star"), Seconds = 6 });
            obs = new ObsService(Dispatcher);
            obs.Updated += watched;
            gameBar = new GameBarService(Dispatcher);
            gameBar.Updated += watched;
            windowsUpdate = new WindowsUpdateService(Dispatcher);
            windowsUpdate.Updated += watched;
            steam = new SteamService(Dispatcher);
            steam.Progress += (appId, name, percent, finished) =>
            {
                if (api == null) return;
                string id = "steam-" + appId;
                if (finished && percent < 0) { api.End(id); return; }
                api.Post(new ApiActivity
                {
                    Id = id,
                    App = "Steam",
                    Title = name,
                    Text = finished ? Strings.SteamDone : (percent >= 0 ? "" : Strings.SteamPreparing),
                    Progress = finished ? 100 : percent,
                    IconGlyph = LocalApi.Glyph("download"),
                    State = finished ? "done" : "running",
                    Updated = DateTime.UtcNow,
                });
            };
            discord = new DiscordService(Dispatcher);
            privacy = new PrivacyService(Dispatcher);
            privacy.IsIgnored = name => settings.PrivacyIgnored.Contains(name, StringComparer.OrdinalIgnoreCase);
            privacy.AppSeen += name =>
            {
                if (settings.PrivacySeen.Contains(name, StringComparer.OrdinalIgnoreCase) || name == "?") return;
                settings.PrivacySeen.Add(name);
                if (settings.PrivacySeen.Count > 12) settings.PrivacySeen.RemoveAt(0);
                settings.Save();
            };
            battery = new BatteryService(Dispatcher);
            levels = new LevelService(Dispatcher);
            osd = new WindowsOsd();
            devices = new DeviceService(Dispatcher);
            focus = new FocusService(Dispatcher);
            focus.Changed += () => { if (settings.ShowFocus) MainIsland?.ShowToast(focus.On ? Strings.FocusOn : Strings.FocusOff, "\uE708", 2.2); };
            hotkey = new HotkeyService();
            hotkey.Pressed += () => MainIsland?.ToggleOpen();
            screenshots = new ScreenshotService();
            clipboard = new ClipboardService(Dispatcher);
            downloads = new DownloadWatcher();
            reminders = new ReminderService(Dispatcher);
            api = new LocalApi(Dispatcher);
            if (sample)
            {
                media.UseSample();
                claude.UseSample();
                codex.UseSample();
            }

            if (sample)
            {
                // Previews and README images use a single island on the primary monitor.
                IslandWindow window = new IslandWindow(settings, media, claude, codex, timer, privacy);
                islands.Add(window);
                if (string.Equals(preview, "Settings", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Theme = Environment.GetEnvironmentVariable("ISLAND_PREVIEW_THEME") ?? "Dark";
                    new SettingsWindow(settings, () => { }, null, () => { }).Show(); // A look at the Settings window with sample data
                    window.Show();
                    return;
                }
                if (preview != null) window.Preview(preview);
                if (clips != null)
                {
                    settings.OffsetY = 28;
                    window.Opacity = 0;
                    window.Loaded += (s, a) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        window.RenderClips(clips);
                        Shutdown();
                    }));
                    window.Show();
                    return;
                }
                if (frames != null)
                {
                    settings.OffsetY = 28;
                    window.Opacity = 0;
                    window.Loaded += (s, a) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        window.RenderAnimation(frames);
                        Shutdown();
                    }));
                    window.Show();
                    return;
                }
                if (render != null)
                {
                    settings.OffsetY = 28; // Even padding around the island in the images
                    window.Opacity = 0;
                    window.Loaded += (s, a) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        string suffix = "-" + CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                        window.RenderStates(render, suffix);
                        SettingsWindow page = new SettingsWindow(new Settings { Theme = Environment.GetEnvironmentVariable("ISLAND_PREVIEW_THEME") ?? "Dark" }, () => { }, null, () => { });
                        page.SaveSnapshot(System.IO.Path.Combine(render, "settings" + suffix + ".png"));
                        // The search box must never break the page: try a few words (and empty it again)
                        foreach (string term in new[] { "a", "di", "steam", "zzz", "linha", "thin", "Claude", "é", "  ", "(" }) { page.SearchBox.Text = term; page.RunSearch(); }
                        page.SearchBox.Text = "";
                        page.RunSearch();
                        if (Environment.GetEnvironmentVariable("ISLAND_RENDER_WINDOWS") == "1")
                        {
                            new SettingsWindow(new Settings { Theme = "Dark" }, () => { }, null, () => { }).SaveWindowShot(System.IO.Path.Combine(render, "win-essentials" + suffix + ".png"), "Essentials", 0);
                            new SettingsWindow(new Settings { Theme = "Dark" }, () => { }, null, () => { }).SaveWindowShot(System.IO.Path.Combine(render, "win-system" + suffix + ".png"), "Advanced", 3);
                        }
                        // The tray menu, in both themes
                        foreach (string menuTheme in new[] { "Dark", "Light" })
                            TrayMenu.SaveImage(Tray.BuildItems(new Settings { AlwaysThin = false, IslandQuiet = true }, () => { }, () => { }, () => { }, () => { }), menuTheme,
                                System.IO.Path.Combine(render, "tray-menu-" + menuTheme.ToLowerInvariant() + suffix + ".png"));
                        // The welcome steps, as images
                        for (int step = 0; step < 3; step++)
                        {
                            SettingsWindow welcomePage = new SettingsWindow(new Settings { Theme = Environment.GetEnvironmentVariable("ISLAND_PREVIEW_THEME") ?? "Dark" }, () => { }, null, () => { }, true);
                            welcomePage.WelcomeStep(step);
                            welcomePage.SaveSnapshot(System.IO.Path.Combine(render, "welcome-" + (step + 1) + suffix + ".png"));
                        }
                        Shutdown();
                    }));
                    window.Show();
                    return;
                }
                window.Show();
            }
            else
            {
                ReconcileIslands();
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, a) => Dispatcher.BeginInvoke(new Action(ReconcileIslands));
                followTimer.Tick += (s, a) => FollowMouse();
                muteTimer.Tick += (s, a) => UpdateMuted();
            }

            NotificationService notifications = new NotificationService(Dispatcher);
            notifications.Arrived += n => { foreach (IslandWindow island in Targets()) island.ShowNotification(n); };
            timer.PhaseChanged += phase =>
            {
                foreach (IslandWindow island in Targets())
                {
                    island.ShowSystemBanner(phase == TimerService.Phase.Focus ? Strings.PomodoroFocusBanner : Strings.PomodoroBreakBanner, Strings.PhaseName(phase), "timer", true);
                    island.FlashThin(phase != TimerService.Phase.Focus);
                }
                if (!sample) TimerSounds.Play(settings);
            };
            timer.Finished += () =>
            {
                foreach (IslandWindow island in Targets()) island.ShowSystemBanner(Strings.TimerFinished, "", "timer", true);
                if (!sample) TimerSounds.Play(settings);
            };
            devices.Changed += (kind, name, connected) =>
            {
                if (kind == DeviceService.Kind.Network)
                    foreach (IslandWindow island in islands) island.SetOnline(connected);
                if (kind == DeviceService.Kind.Network)
                    Notifier?.ShowToast(connected ? Strings.NetworkOnline : Strings.NetworkOffline, connected ? "\uE701" : "\uF384", 2.6);
                else
                    Notifier?.ShowToast(connected ? Strings.DeviceConnected(name) : Strings.DeviceDisconnected(name), kind == DeviceService.Kind.Bluetooth ? "\uE702" : "\uE88E", 2.6);
            };
            if (api != null)
            {
                api.AlertReceived += a => Notifier?.ShowApiAlert(a);
                api.MediaHint += h => media.ApplyHint(h);
            }
            levels.Changed += (kind, percent, muted) => Notifier?.ShowLevel(kind, percent, muted);
            claude.LimitAlert += (weekly, level, percent, resets) => Notifier?.ShowLimitAlert(weekly, level, percent, resets);
            claude.Forecast += (weekly, when, resets) => Notifier?.ShowLimitForecast(weekly, when, resets);
            if (api != null) api.PermissionAsked += p => { if (Notifier != null) Notifier.ShowPermission(p); else p.Resolve("none"); };
            screenshots.Taken += () => Notifier?.ShowToast(Strings.ScreenshotTaken, "\uE722", 2.2);
            downloads.Completed += (path, size) => Dispatcher.BeginInvoke(new Action(() =>
            {
                // While the screen is shared the file name stays out of sight
                string name = privacy.Screen ? "" : DownloadTracker.ShortName(System.IO.Path.GetFileName(path), 36);
                Notifier?.ShowDownloadBanner(privacy.Screen ? Strings.DownloadDoneHidden : Strings.DownloadDone, name, DownloadTracker.FormatSize(size), () => DownloadWatcher.Reveal(path));
            }));
            reminders.Due += (text, late) =>
            {
                foreach (IslandWindow island in Targets()) island.ShowSystemBanner(Strings.ReminderBanner, late ? text + " " + Strings.ReminderLate : text, "reminder", true);
                if (!sample) TimerSounds.Play(settings);
            };
            battery.Alert += (title, body, kind) => Notifier?.ShowSystemBanner(title, body, kind);
            Action applySettings = () =>
            {
                if (!sample) Strings.Use(settings.Language);
                // The quick clipboard also needs to know when the screen is shared, to hide its texts then
                if ((settings.ShowPrivacyDots || settings.ClipboardHistory || settings.ShowDownloadAlerts || settings.PresentationMode) && !sample) { privacy.Start(); privacy.Refresh(); }
                else privacy.Stop();
                downloads.MinMb = settings.DownloadMinMb;
                if (settings.ShowDownloadAlerts && !sample) downloads.Start();
                else downloads.Stop();
                if (settings.ShowReminders && !sample) reminders.Start();
                else reminders.Stop();
                clipboard.SetKeep(settings.ClipboardForgetMinutes);
                if (settings.ClipboardHistory && !sample) clipboard.Start();
                else clipboard.Stop(); // Turned off: the list is emptied
                battery.AlertsOn = settings.BatteryAlerts;
                if ((settings.BatteryAlerts || settings.BatteryActivity) && !sample) battery.Start();
                else battery.Stop();
                if (!sample) HotkeyFailed = settings.HotkeyEnabled && !hotkey.Apply(true, settings.HotkeyCombo);
                if (!sample && !settings.HotkeyEnabled) hotkey.Apply(false, null);
                if (settings.ShowFocus && !sample) focus.Start();
                else focus.Stop();
                if (!sample) devices.Start(settings.ShowDeviceAlerts, settings.ShowNetworkAlerts);
                if (settings.ShowLevels && !sample) levels.Start();
                else levels.Stop();
                osd.Apply(settings.HideWindowsOsd && settings.ShowLevels && !sample);
                if (settings.ShowScreenshotAlert && !sample) screenshots.Start();
                else screenshots.Stop();
                if (settings.ApiEnabled && !sample) api.Start(settings.ApiPort);
                else api.Stop();
                if (settings.DiscordEnabled && !sample) discord.Start(settings.DiscordClientId);
                else discord.Stop();
                if (settings.ShowSteamDownloads && !sample) steam.Start();
                else steam.Stop();
                ApplyFootball(sample);
                if (settings.ShowObs && !sample) { obs.Start(); gameBar.Start(); }
                else { obs.Stop(); gameBar.Stop(); }
                if (settings.ShowWindowsUpdate && !sample) windowsUpdate.Start();
                else windowsUpdate.Stop();
                if (!sample) ReconcileIslands();
                foreach (IslandWindow island in islands) island.SettingsChanged();
                if (settings.ShowNotifications && !sample) notifications.Start();
                else notifications.Stop();
            };

            updates = new UpdateFlow(Dispatcher, () => islands.Exists(i => i.IsBusy), () => Shutdown(),
                message => MainIsland.ShowSystemBanner(Installer.Name, message));
            Action openSettings = () =>
            {
                if (settingsWindow == null)
                {
                    settingsWindow = new SettingsWindow(settings, applySettings, updates,
                        () => { foreach (IslandWindow island in islands) island.PeekExpanded(); });
                    settingsWindow.Closed += (s, a) => settingsWindow = null;
                    settingsWindow.Show();
                }
                if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
                settingsWindow.Activate();
            };

            Action toggleSettings = () =>
            {
                if (settingsWindow != null) settingsWindow.Close();
                else openSettings();
            };

            tray = new Tray(settings, applySettings, openSettings, toggleSettings, () =>
            {
                _ = updates.CheckFromMenuAsync();
            }, () => Shutdown(), timer);
            updates.Status = (kind, text) => MainIsland?.ShowStatus(kind, text);
            updates.Ask = (version, yes) => MainIsland.ShowQuestion(Strings.UpdateAskTitle(version), Strings.UpdateAskBody, yes);
            if (dev && Array.IndexOf(e.Args, "--show-menu") >= 0) Dispatcher.BeginInvoke(new Action(() => tray.ShowMenu()), DispatcherPriority.Background); // For looking at the tray menu
            if (sample) return;
            if (settings.ShowPrivacyDots || settings.ClipboardHistory || settings.ShowDownloadAlerts || settings.PresentationMode) privacy.Start();
            clipboard.SetKeep(settings.ClipboardForgetMinutes);
            if (settings.ClipboardHistory) clipboard.Start();
            downloads.MinMb = settings.DownloadMinMb;
            if (settings.ShowDownloadAlerts) downloads.Start();
            if (settings.ShowReminders) reminders.Start();
            battery.AlertsOn = settings.BatteryAlerts;
            if (settings.BatteryAlerts || settings.BatteryActivity) battery.Start();
            if (settings.HotkeyEnabled) HotkeyFailed = !hotkey.Apply(true, settings.HotkeyCombo);
            if (settings.ShowFocus) focus.Start();
            devices.Start(settings.ShowDeviceAlerts, settings.ShowNetworkAlerts);
            if (settings.ShowLevels) levels.Start();
            osd.Apply(settings.HideWindowsOsd && settings.ShowLevels);
            if (settings.ShowScreenshotAlert) screenshots.Start();
            if (settings.ApiEnabled) api.Start(settings.ApiPort);
            if (settings.DiscordEnabled) discord.Start(settings.DiscordClientId);
            if (settings.ShowSteamDownloads) steam.Start();
            ApplyFootball(false);
            if (settings.ShowObs) { obs.Start(); gameBar.Start(); }
            if (settings.ShowWindowsUpdate) windowsUpdate.Start();
            if (Installer.IsInstalledCopy) { Installer.EnsureCommandShim(); Installer.RefreshAppsEntry(); }
            if (settings.PermissionPrompts && !dev) { string hookError; ClaudeHook.Apply(true, out hookError); } // Keeps the hook pointing at this exe // Copies updated from before 2.0 get island.cmd too

            if (!dev)
            {
                showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
                ThreadPool.RegisterWaitForSingleObject(showSettings,
                    (state, timedOut) => Dispatcher.BeginInvoke(openSettings), null, Timeout.Infinite, false);
            }

            // First run, or the first run after the settings were started again: the welcome steps, once
            if (!settings.OnboardingDone && !dev)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (settingsWindow != null) return;
                    settingsWindow = new SettingsWindow(settings, applySettings, updates, () => { foreach (IslandWindow island in islands) island.PeekExpanded(); }, true);
                    settingsWindow.Closed += (s, a) =>
                    {
                        settingsWindow = null;
                        if (!settings.OnboardingDone) { settings.OnboardingDone = true; settings.Save(); } // Closed with the X: counts as skipped
                    };
                    settingsWindow.Show();
                }), DispatcherPriority.ApplicationIdle);

            _ = media.StartAsync();
            claude.Start();
            codex.Start();
            if (settings.ShowNotifications) notifications.Start();

            Updater.CleanUp();
            // The notice waits a moment after the island is up (it was lost when the app was still starting), then stays for a few seconds
            // (The island's window is usually loaded already by now, so "Loaded" alone would never fire: this is why the notice never showed)
            if (afterUpdate)
                AfterStart(() => MainIsland.ShowStatus("uptodate", Strings.UpdatedTo(Installer.Version), settings.WhatsNewSeen != WhatsNew.Key ? Strings.WhatsNewHint : null, 6)); // The green check, as when the update is looked for
            if (afterCrash)
                AfterStart(() => MainIsland.ShowStatus("failed", Strings.ReopenedAfterFailure, null, 6));
            updateTimer = new DispatcherTimer { Interval = FirstUpdateCheck };
            updateTimer.Tick += (s, a) =>
            {
                updateTimer.Interval = UpdateInterval;
                if (settings.AutoUpdate) _ = updates.CheckAsync(false);
            };
            updateTimer.Start();
        }

        void AfterStart(Action show)
        {
            DispatcherTimer wait = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.3) };
            wait.Tick += (s, e) => { wait.Stop(); show(); };
            wait.Start();
        }

        // "Only on the monitor you are using": with one island per monitor, a new alert goes to the island of the monitor
        // with the mouse. Nobody is left out when none matches.
        bool ActiveOnly
        {
            get { return settings.ActiveMonitorOnly && islands.Count > 1 && string.Equals(settings.Monitor, "All", StringComparison.OrdinalIgnoreCase); }
        }

        bool IsActiveIsland(IslandWindow island, string cursorId)
        {
            return island.Target?.Id == cursorId;
        }

        // The islands that get a new alert.
        IEnumerable<IslandWindow> Targets()
        {
            if (!ActiveOnly) return islands.ToArray();
            string id = Displays.UnderCursor().Id;
            bool any = islands.Exists(i => IsActiveIsland(i, id));
            return islands.Where(i => IslandRules.GetsAlert(true, IsActiveIsland(i, id), any)).ToArray();
        }

        // The island that gets a single alert (a toast, a level, a limit): the active one, else the main one.
        IslandWindow Notifier
        {
            get { return Targets().FirstOrDefault() ?? (islands.Count > 0 ? MainIsland : null); }
        }

        // The other islands stay quiet (no music, no Claude) while the option is on.
        void UpdateMuted()
        {
            bool on = ActiveOnly;
            string id = on ? Displays.UnderCursor().Id : null;
            bool any = on && islands.Exists(i => IsActiveIsland(i, id));
            foreach (IslandWindow island in islands) island.SetMuted(on && !IslandRules.GetsAlert(true, IsActiveIsland(island, id), any));
            muteTimer.IsEnabled = on;
        }

        // The island on the primary monitor (or the only one) speaks for the app, such as update messages.
        IslandWindow MainIsland
        {
            get { return islands[0]; }
        }

        // Makes the set of islands match the Monitor setting: one per monitor for "All", otherwise a
        // single island on the primary monitor, a chosen one, or the one under the mouse.
        void ReconcileIslands()
        {
            string mode = settings.Monitor ?? "Primary";
            bool all = mode.Equals("All", StringComparison.OrdinalIgnoreCase);
            bool follow = mode.Equals("Active", StringComparison.OrdinalIgnoreCase);

            List<Display> wanted;
            if (all) wanted = Displays.All();
            else if (follow) wanted = new List<Display> { Displays.UnderCursor() };
            else if (mode.Equals("Primary", StringComparison.OrdinalIgnoreCase)) wanted = new List<Display> { Displays.Primary() };
            else wanted = new List<Display> { Displays.ById(mode) ?? Displays.Primary() }; // A monitor that was unplugged falls back
            if (wanted.Count == 0) wanted.Add(Displays.Primary());

            if (!all)
            {
                while (islands.Count > 1) CloseIsland(islands[islands.Count - 1]);
                if (islands.Count == 0) OpenIsland(wanted[0]);
                else islands[0].MoveTo(wanted[0], islands[0].Target?.Id != wanted[0].Id);
            }
            else
            {
                foreach (IslandWindow island in islands.ToArray())
                {
                    Display same = wanted.Find(d => d.Id == island.Target?.Id);
                    if (same == null) CloseIsland(island);
                    else island.MoveTo(same, false); // Refreshes its bounds after a display change
                }
                foreach (Display display in wanted)
                    if (!islands.Exists(i => i.Target?.Id == display.Id)) OpenIsland(display);
                islands.Sort((a, b) => wanted.FindIndex(d => d.Id == a.Target?.Id).CompareTo(wanted.FindIndex(d => d.Id == b.Target?.Id)));
            }
            followTimer.IsEnabled = follow;
            UpdateMuted();
        }

        void OpenIsland(Display display)
        {
            IslandWindow island = new IslandWindow(settings, media, claude, codex, timer, privacy);
            island.MoveTo(display, false);
            islands.Add(island);
            island.Show();
        }

        void CloseIsland(IslandWindow island)
        {
            island.Detach();
            islands.Remove(island);
            island.Close();
        }

        // "Active" mode: the island hops to the monitor the mouse is on.
        void FollowMouse()
        {
            if (islands.Count == 0) return;
            Display display = Displays.UnderCursor();
            if (islands[0].Target?.Id != display.Id) islands[0].MoveTo(display, true);
        }

        // The previous version is quitting after replacing itself; take over its single-instance lock.
        bool WaitForPreviousVersion()
        {
            try { return singleInstance.WaitOne(UpdateHandoverMs); }
            catch (AbandonedMutexException) { return true; }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            tray?.Dispose();
            screenshots?.Dispose();
            clipboard?.Dispose(); // Forgets the copied texts
            downloads?.Dispose();
            reminders?.Dispose();
            api?.Stop();
            discord?.Stop();
            steam?.Stop();
            obs?.Stop();
            gameBar?.Stop();
            football?.Stop();
            windowsUpdate?.Stop();
            osd?.Apply(false); // Gives Windows its volume popup back
            if (ownsMutex)
            {
                try { singleInstance.ReleaseMutex(); }
                catch (Exception) { }
            }
            singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
