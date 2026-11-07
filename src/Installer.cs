using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace WindowsIsland
{
    // Installs for the current Windows user only: no administrator rights, no installer framework.
    static class Installer
    {
        public const string Id = "WindowsIsland";
        public const string Name = "Windows Island";
        const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Id;
        const int ExitWaitMs = 3000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool DeleteFile(string path);

        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Id); }
        }

        public static string InstalledExe
        {
            get { return Path.Combine(InstallDir, Id + ".exe"); }
        }

        public static bool IsInstalledCopy
        {
            get { return string.Equals(Environment.ProcessPath, InstalledExe, StringComparison.OrdinalIgnoreCase); }
        }

        // What the Windows Run key should start: the installed copy when there is one.
        public static string StartupTarget
        {
            get { return File.Exists(InstalledExe) ? InstalledExe : Environment.ProcessPath; }
        }

        static string Version
        {
            get { return typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"; }
        }

        static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk"); }
        }

        static string DesktopShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk"); }
        }

        // Returns true when the island was installed and started, so this copy should quit.
        public static bool RunInteractive()
        {
            MessageBoxResult answer = MessageBox.Show(Strings.InstallPrompt, Strings.InstallTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return false;

            try
            {
                Install();
                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(Strings.InstallFailed, Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        public static void Install()
        {
            StopOtherCopies();
            Directory.CreateDirectory(InstallDir);
            File.Copy(Environment.ProcessPath, InstalledExe, true);
            DeleteFile(InstalledExe + ":Zone.Identifier"); // The user already approved the download.

            CreateShortcut(StartMenuShortcut);
            CreateShortcut(DesktopShortcut);
            RegisterInAppsList();

            Settings settings = Settings.Load();
            settings.StartWithWindows = true;
            settings.Save(); // Writes the Run key, now pointing at the installed copy

            Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true })?.Dispose();
        }

        public static void RunUninstall()
        {
            MessageBoxResult answer = MessageBox.Show(Strings.UninstallPrompt, Strings.UninstallTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            StopOtherCopies();
            Settings.RemoveStartup();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            TryDelete(StartMenuShortcut);
            TryDelete(DesktopShortcut);
            Settings.DeleteAll();
            DeleteInstallDirAfterExit();
        }

        static void RegisterInAppsList()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", Name);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("DisplayIcon", InstalledExe);
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("Publisher", "windows-island contributors");
                key.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        static void StopOtherCopies()
        {
            int self = Environment.ProcessId;
            foreach (Process process in Process.GetProcessesByName(Id))
            {
                using (process)
                {
                    if (process.Id == self) continue;
                    try
                    {
                        process.Kill();
                        process.WaitForExit(ExitWaitMs);
                    }
                    catch (Exception)
                    {
                        // Already gone.
                    }
                }
            }
        }

        static void CreateShortcut(string path)
        {
            try
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(path);
                shortcut.TargetPath = InstalledExe;
                shortcut.WorkingDirectory = InstallDir;
                shortcut.IconLocation = InstalledExe + ",0";
                shortcut.Description = Name;
                shortcut.Save();
            }
            catch (Exception)
            {
                // A missing shortcut is not worth failing the install for.
            }
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception) { }
        }

        // The running exe cannot delete itself, so a short-lived hidden command removes the folder.
        static void DeleteInstallDirAfterExit()
        {
            ProcessStartInfo start = new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + InstallDir + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WorkingDirectory = Path.GetTempPath();
            Process.Start(start)?.Dispose();
        }
    }
}
