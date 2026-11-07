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
        readonly List<IslandWindow> islands = new List<IslandWindow>();
        readonly DispatcherTimer followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        DispatcherTimer updateTimer;
        bool updating;
        SettingsWindow settingsWindow;
        EventWaitHandle showSettings;
        const string ShowSettingsEvent = @"Local\WindowsIsland.ShowSettings";

        // Arguments:
        //   --preview=<State>   shows one state with sample data (Idle, MusicCompact, ClaudeExpanded...)
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

            string preview = null, render = null;
            bool portable = false, uninstall = false, afterUpdate = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg.StartsWith("--preview=", StringComparison.OrdinalIgnoreCase)) preview = arg.Substring(10);
                else if (arg.Equals("--render", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) render = e.Args[++i];
                else if (arg.Equals("--portable", StringComparison.OrdinalIgnoreCase)) portable = true;
                else if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                else if (arg.Equals("--after-update", StringComparison.OrdinalIgnoreCase)) afterUpdate = true;
                else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                    CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(arg.Substring(7));
            }
            bool sample = preview != null || render != null;

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
            if (!sample && !portable && !afterUpdate && !Installer.IsInstalledCopy)
            {
                Installer.RunInteractive();
                Shutdown();
                return;
            }

            singleInstance = new Mutex(true, sample ? @"Local\WindowsIsland.Preview." + Guid.NewGuid() : @"Local\WindowsIsland.SingleInstance", out ownsMutex);
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
            media = new MediaService(Dispatcher);
            claude = new ClaudeService(Dispatcher);
            if (sample)
            {
                media.UseSample();
                claude.UseSample();
            }

            if (sample)
            {
                // Previews and README images use a single island on the primary monitor.
                IslandWindow window = new IslandWindow(settings, media, claude);
                islands.Add(window);
                if (preview != null) window.Preview(preview);
                if (render != null)
                {
                    settings.OffsetY = 28; // Even padding around the island in the images
                    window.Opacity = 0;
                    window.Loaded += (s, a) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        string suffix = "-" + CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                        window.RenderStates(render, suffix);
                        SettingsWindow page = new SettingsWindow(new Settings(), () => { }, () => { }, () => { });
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
            Action applySettings = () =>
            {
                if (!sample) ReconcileIslands();
                foreach (IslandWindow island in islands) island.SettingsChanged();
                if (settings.ShowNotifications && !sample) notifications.Start();
                else notifications.Stop();
            };

            Action openSettings = () =>
            {
                if (settingsWindow == null)
                {
                    settingsWindow = new SettingsWindow(settings, applySettings, () => _ = CheckForUpdatesAsync(true),
                        () => { foreach (IslandWindow island in islands) island.PeekExpanded(); });
                    settingsWindow.Closed += (s, a) => settingsWindow = null;
                    settingsWindow.Show();
                }
                if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
                settingsWindow.Activate();
            };

            tray = new Tray(settings, applySettings, openSettings, () => _ = CheckForUpdatesAsync(true), () => Shutdown());
            if (sample) return;

            showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
            ThreadPool.RegisterWaitForSingleObject(showSettings,
                (state, timedOut) => Dispatcher.BeginInvoke(openSettings), null, Timeout.Infinite, false);

            _ = media.StartAsync();
            claude.Start();
            if (settings.ShowNotifications) notifications.Start();

            Updater.CleanUp();
            if (afterUpdate)
                MainIsland.Loaded += (s, a) => MainIsland.ShowSystemBanner(Installer.Name, Strings.UpdatedTo(Installer.Version));
            updateTimer = new DispatcherTimer { Interval = FirstUpdateCheck };
            updateTimer.Tick += (s, a) =>
            {
                updateTimer.Interval = UpdateInterval;
                if (settings.AutoUpdate) _ = CheckForUpdatesAsync(false);
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
            IslandWindow island = new IslandWindow(settings, media, claude);
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

        async Task CheckForUpdatesAsync(bool manual)
        {
            if (updating) return;
            updating = true;
            try
            {
                if (!Updater.CanUpdate)
                {
                    if (manual) MainIsland.ShowSystemBanner(Installer.Name, Strings.UpdatesNeedInstall);
                    return;
                }

                Tuple<UpdateResult, UpdateInfo> check = await Updater.CheckAsync();
                if (check.Item1 == UpdateResult.Available && await Updater.DownloadVerifiedAsync(check.Item2))
                {
                    while (islands.Exists(i => i.IsBusy)) await Task.Delay(2000); // Never restart under the user's mouse
                    if (Updater.ApplyAndRestart())
                    {
                        Shutdown();
                        return;
                    }
                }
                if (manual)
                    MainIsland.ShowSystemBanner(Installer.Name, check.Item1 == UpdateResult.UpToDate ? Strings.UpToDate : Strings.UpdateFailed);
            }
            finally
            {
                updating = false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            tray?.Dispose();
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
