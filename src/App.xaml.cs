using System;
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
        IslandWindow window;
        DispatcherTimer updateTimer;
        bool updating;

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
                Shutdown();
                return;
            }

            settings = sample ? new Settings() : Settings.Load();
            MediaService media = new MediaService(Dispatcher);
            ClaudeService claude = new ClaudeService(Dispatcher);
            if (sample)
            {
                media.UseSample();
                claude.UseSample();
            }

            window = new IslandWindow(settings, media, claude);
            if (preview != null) window.Preview(preview);
            if (render != null)
            {
                settings.OffsetY = 28; // Even padding around the island in the images
                window.Opacity = 0;
                window.Loaded += (s, a) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    string suffix = "-" + CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    window.RenderStates(render, suffix);
                    Shutdown();
                }));
                window.Show();
                return;
            }
            window.Show();

            NotificationService notifications = new NotificationService(Dispatcher);
            notifications.Arrived += window.ShowNotification;
            Action applySettings = () =>
            {
                window.SettingsChanged();
                if (settings.ShowNotifications && !sample) notifications.Start();
                else notifications.Stop();
            };

            tray = new Tray(settings, applySettings, () => _ = CheckForUpdatesAsync(true), () => Shutdown());
            if (sample) return;

            _ = media.StartAsync();
            claude.Start();
            if (settings.ShowNotifications) notifications.Start();

            Updater.CleanUp();
            if (afterUpdate)
                window.Loaded += (s, a) => window.ShowSystemBanner(Installer.Name, Strings.UpdatedTo(Installer.Version));
            updateTimer = new DispatcherTimer { Interval = FirstUpdateCheck };
            updateTimer.Tick += (s, a) =>
            {
                updateTimer.Interval = UpdateInterval;
                if (settings.AutoUpdate) _ = CheckForUpdatesAsync(false);
            };
            updateTimer.Start();
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
                    if (manual) window.ShowSystemBanner(Installer.Name, Strings.UpdatesNeedInstall);
                    return;
                }

                Tuple<UpdateResult, UpdateInfo> check = await Updater.CheckAsync();
                if (check.Item1 == UpdateResult.Available && await Updater.DownloadVerifiedAsync(check.Item2))
                {
                    while (window.IsBusy) await Task.Delay(2000); // Never restart under the user's mouse
                    if (Updater.ApplyAndRestart())
                    {
                        Shutdown();
                        return;
                    }
                }
                if (manual)
                    window.ShowSystemBanner(Installer.Name, check.Item1 == UpdateResult.UpToDate ? Strings.UpToDate : Strings.UpdateFailed);
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
