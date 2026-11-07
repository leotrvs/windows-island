using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WindowsIsland
{
    public partial class App : Application
    {
        Mutex singleInstance;
        Tray tray;

        // Arguments:
        //   --preview=<State>   shows one state with sample data (Idle, MusicCompact, ClaudeExpanded...)
        //   --render <folder>   writes every state to PNG files with sample data, then quits
        //   --lang=en|pt        forces the language
        //   --portable          runs without installing
        //   --uninstall         removes the app, its shortcuts and settings
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // The island is tiny, so drawing it on the CPU costs nothing and spares the ~120 MB
            // that a Direct3D device takes. Measured: 166 MB private memory with GPU, 43 MB without.
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            string preview = null, render = null;
            bool portable = false, uninstall = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg.StartsWith("--preview=", StringComparison.OrdinalIgnoreCase)) preview = arg.Substring(10);
                else if (arg.Equals("--render", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length) render = e.Args[++i];
                else if (arg.Equals("--portable", StringComparison.OrdinalIgnoreCase)) portable = true;
                else if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
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
            if (!sample && !portable && !Installer.IsInstalledCopy)
            {
                Installer.RunInteractive();
                Shutdown();
                return;
            }

            bool created;
            singleInstance = new Mutex(true, sample ? @"Local\WindowsIsland.Preview." + Guid.NewGuid() : @"Local\WindowsIsland.SingleInstance", out created);
            if (!created)
            {
                Shutdown();
                return;
            }

            Settings settings = sample ? new Settings() : Settings.Load();
            MediaService media = new MediaService(Dispatcher);
            ClaudeService claude = new ClaudeService(Dispatcher);
            if (sample)
            {
                media.UseSample();
                claude.UseSample();
            }

            IslandWindow window = new IslandWindow(settings, media, claude);
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

            tray = new Tray(settings, applySettings, () => Shutdown());
            if (!sample)
            {
                _ = media.StartAsync();
                claude.Start();
                if (settings.ShowNotifications) notifications.Start();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            tray?.Dispose();
            singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
