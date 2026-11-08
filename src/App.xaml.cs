using System;
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
        internal BatteryService Battery { get { return battery; } }
        ScreenshotService screenshots;
        readonly List<IslandWindow> islands = new List<IslandWindow>();
        readonly DispatcherTimer followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        DispatcherTimer updateTimer;
        UpdateFlow updates;
        SettingsWindow settingsWindow;
        EventWaitHandle showSettings;
        const string ShowSettingsEvent = @"Local\WindowsIsland.ShowSettings";

        // Arguments:
        //   --preview=<State>   shows one state with sample data (Idle, MusicCompact, ClaudeExpanded...)
        //   --frames <folder>   writes the README animation as numbered PNG frames with sample data, then quits
        //   --dev               runs beside the installed copy (its own instance), for testing a build
        //   --render <folder>   writes every state to PNG files with sample data, then quits
        //   --lang=en|pt        forces the language
        //   --portable          runs without installing
        //   --uninstall         removes the app, its shortcuts and settings
        //   --after-update      started by the previous version right after it replaced itself
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // The island is tiny, so drawing it on the CPU costs nothing and spares the ~120 MB
            // that a Direct3D device takes. Measured: 166 MB private memory with GPU, 43 MB without.
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            string preview = null, render = null, frames = null;
            bool portable = false, uninstall = false, afterUpdate = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg.StartsWith("--preview=", StringComparison.OrdinalIgnoreCase)) preview = arg.Substring(10);
                else if (arg.Equals("--render", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) render = e.Args[++i];
                else if (arg.Equals("--frames", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) frames = e.Args[++i];
                else if (arg.Equals("--portable", StringComparison.OrdinalIgnoreCase)) portable = true;
                else if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                else if (arg.Equals("--after-update", StringComparison.OrdinalIgnoreCase)) afterUpdate = true;
                else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(arg.Substring(7));
            }
            bool sample = preview != null || render != null || frames != null;

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
                Installer.RunInteractive();
                Shutdown();
                return;
            }

            bool dev = Array.IndexOf(e.Args, "--dev") >= 0; // A development copy that can run beside the installed one
            singleInstance = new Mutex(true, sample || dev ? @"Local\WindowsIsland.Preview." + Guid.NewGuid() : @"Local\WindowsIsland.SingleInstance", out ownsMutex);
            if (!ownsMutex && afterUpdate) ownsMutex = WaitForPreviousVersion();
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
            if (!sample) Strings.Use(settings.Language);
            media = new MediaService(Dispatcher);
            claude = new ClaudeService(Dispatcher);
            codex = new CodexService(Dispatcher);
            timer = new TimerService(Dispatcher);
            privacy = new PrivacyService(Dispatcher);
            battery = new BatteryService(Dispatcher);
            screenshots = new ScreenshotService();
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
                    new SettingsWindow(settings, () => { }, null, () => { }).Show(); // A look at the Settings window with sample data
                    window.Show();
                    return;
                }
                if (preview != null) window.Preview(preview);
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
                        SettingsWindow page = new SettingsWindow(new Settings(), () => { }, null, () => { });
                        page.SaveSnapshot(System.IO.Path.Combine(render, "settings" + suffix + ".png"));
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
            }

            NotificationService notifications = new NotificationService(Dispatcher);
            notifications.Arrived += n => { foreach (IslandWindow island in islands) island.ShowNotification(n); };
            timer.Finished += () =>
            {
                foreach (IslandWindow island in islands) island.ShowSystemBanner(Strings.TimerFinished, "");
            };
            screenshots.Taken += () => MainIsland?.ShowToast(Strings.ScreenshotTaken, "\uE722", 2.2);
            battery.Alert += (title, body, kind) => MainIsland?.ShowSystemBanner(title, body, kind);
            Action applySettings = () =>
            {
                if (!sample) Strings.Use(settings.Language);
                if (settings.ShowPrivacyDots && !sample) privacy.Start();
                else privacy.Stop();
                battery.AlertsOn = settings.BatteryAlerts;
                if ((settings.BatteryAlerts || settings.BatteryActivity) && !sample) battery.Start();
                else battery.Stop();
                if (settings.ShowScreenshotAlert && !sample) screenshots.Start();
                else screenshots.Stop();
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

            tray = new Tray(settings, applySettings, openSettings, () =>
            {
                openSettings();
                if (settingsWindow != null) settingsWindow.GoToSystem();
                _ = updates.CheckAsync(true);
            }, () => Shutdown(), timer);
            if (sample) return;
            if (settings.ShowPrivacyDots) privacy.Start();
            battery.AlertsOn = settings.BatteryAlerts;
            if (settings.BatteryAlerts || settings.BatteryActivity) battery.Start();
            if (settings.ShowScreenshotAlert) screenshots.Start();

            if (!dev)
            {
                showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
                ThreadPool.RegisterWaitForSingleObject(showSettings,
                    (state, timedOut) => Dispatcher.BeginInvoke(openSettings), null, Timeout.Infinite, false);
            }

            _ = media.StartAsync();
            claude.Start();
            codex.Start();
            if (settings.ShowNotifications) notifications.Start();

            Updater.CleanUp();
            if (afterUpdate)
                MainIsland.Loaded += (s, a) => MainIsland.ShowSystemBanner(Installer.Name, Strings.UpdatedTo(Installer.Version));
            updateTimer = new DispatcherTimer { Interval = FirstUpdateCheck };
            updateTimer.Tick += (s, a) =>
            {
                updateTimer.Interval = UpdateInterval;
                if (settings.AutoUpdate) _ = updates.CheckAsync(false);
            };
            updateTimer.Start();
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
