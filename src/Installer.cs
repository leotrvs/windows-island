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

        public static string Version
        {
            get { return typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"; }
        }

        // "island.cmd show build --title Building": a short name for the command line, beside the installed exe.
        // PATH is left alone; scripts call it by its full path.
        static string CommandShim
        {
            get { return Path.Combine(InstallDir, "island.cmd"); }
        }

        const string CommandShimText = "@echo off\r\n\"%~dp0" + Id + ".exe\" %*\r\n";

        public static void EnsureCommandShim()
        {
            try
            {
                if (File.Exists(CommandShim) && File.ReadAllText(CommandShim) == CommandShimText) return;
                File.WriteAllText(CommandShim, CommandShimText);
            }
            catch (Exception)
            {
                // The command line is a convenience; the island works without it.
            }
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
            EnsureCommandShim();

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
            string hookError;
            ClaudeHook.Apply(false, out hookError); // Claude Code must not be left calling a program that is gone
            Settings.RemoveStartup();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            TryDelete(StartMenuShortcut);
            TryDelete(DesktopShortcut);
            TryDelete(CommandShim);
            Settings.DeleteAll();
            DeleteInstallDirAfterExit();
        }

        // Keeps the Windows apps list entry current (the version was only ever written at install time).
        public static void RefreshAppsEntry()
        {
            try { RegisterInAppsList(); }
            catch (Exception) { }
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
                key.SetValue("URLInfoAbout", "https://github.com/leotrvs/windows-island");
                if (key.GetValue("InstallDate") == null) key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                try { key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord); } catch (Exception) { }
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
