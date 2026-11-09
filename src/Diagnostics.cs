using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace WindowsIsland
{
    // A record of the app's own failures, kept in a file on this computer and sent nowhere. Settings can copy a short report of it
    // (no names, no keys) for someone to paste into a GitHub issue. After a failure the app reopens itself, unless it keeps failing.
    static class Diagnostics
    {
        const long MaxLogBytes = 256 * 1024;
        const int MaxRestarts = 3; // Within RestartWindow
        static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(5);

        static string Folder { get { return Path.GetDirectoryName(Settings.FilePath); } }
        static string LogPath { get { return Path.Combine(Folder, "crash.log"); } }
        static string RestartsPath { get { return Path.Combine(Folder, "restarts.txt"); } }

        static bool development;

        public static void Install(Application app, bool dev)
        {
            development = dev;
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log("The window thread", e.Exception);
                e.Handled = true; // No Windows crash box: the app reopens itself
                Restart();
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log("A background thread", e.ExceptionObject as Exception);
                if (e.IsTerminating) Restart();
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log("A background task", e.Exception);
                e.SetObserved(); // Not worth closing the app for
            };
        }

        public static void Log(string where, Exception error)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                StringBuilder entry = new StringBuilder();
                entry.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " v" + Installer.Version + " · " + where);
                entry.AppendLine(error == null ? "(no details)" : error.ToString());
                entry.AppendLine();
                File.AppendAllText(LogPath, entry.ToString());
                FileInfo info = new FileInfo(LogPath);
                if (info.Length > MaxLogBytes)
                {
                    string all = File.ReadAllText(LogPath);
                    File.WriteAllText(LogPath, all.Substring(all.Length - (int)(MaxLogBytes / 2)));
                }
            }
            catch (Exception)
            {
                // A failing log must not make things worse
            }
        }

        // Opens the app again, once the old one is gone, unless it has been failing over and over
        static void Restart()
        {
            if (development) return;
            try
            {
                DateTime now = DateTime.Now;
                string[] recent = File.Exists(RestartsPath) ? File.ReadAllLines(RestartsPath) : new string[0];
                var times = recent.Select(l => { DateTime t; return DateTime.TryParse(l, out t) ? t : DateTime.MinValue; }).Where(t => now - t < RestartWindow).ToList();
                if (times.Count >= MaxRestarts) return;
                times.Add(now);
                File.WriteAllLines(RestartsPath, times.Select(t => t.ToString("o")));
                Process.Start(new ProcessStartInfo(Installer.StartupTarget, "--after-crash") { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                // Not reopened: the person opens it again
            }
            Environment.Exit(1);
        }

        // What to paste into an issue: the version, the system, the settings that are not the defaults and the last failures.
        // Names of apps, the Discord id, the team and the window's place are left out, and the user's name is hidden.
        public static string Report(Settings settings)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Windows Island " + Installer.Version + (Installer.IsInstalledCopy ? " (installed)" : ""));
            text.AppendLine("Windows: " + Environment.OSVersion.Version + " · " + RuntimeInformation.OSArchitecture);
            text.AppendLine(".NET: " + RuntimeInformation.FrameworkDescription);
            text.AppendLine("Culture: " + System.Globalization.CultureInfo.CurrentUICulture.Name);
            text.AppendLine();
            text.AppendLine("Settings that differ from the defaults:");
            string[] left = { "SettingsVersion", "OnboardingDone", "PrivacyIgnored", "PrivacySeen", "DiscordClientId", "FootballTeam", "SettingsLeft", "SettingsTop", "SettingsWidth", "SettingsHeight" };
            Settings defaults = new Settings();
            int shown = 0;
            foreach (PropertyInfo property in typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (left.Contains(property.Name) || !property.CanRead || property.GetIndexParameters().Length > 0) continue;
                object value = property.GetValue(settings), original = property.GetValue(defaults);
                if (value is IEnumerable && !(value is string)) continue;
                if (Equals(value, original)) continue;
                text.AppendLine("  " + property.Name + " = " + value);
                shown++;
            }
            if (shown == 0) text.AppendLine("  (none)");
            text.AppendLine();
            text.AppendLine("Last failures:");
            string log = "";
            try { log = File.Exists(LogPath) ? File.ReadAllText(LogPath) : ""; }
            catch (Exception) { }
            if (log.Length == 0) text.AppendLine("  (none)");
            else
            {
                string[] entries = log.Split(new[] { "=== " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string entry in entries.Skip(Math.Max(0, entries.Length - 3))) text.AppendLine("=== " + entry.TrimEnd());
            }
            return Hide(text.ToString());
        }

        // The user's name and folders are not part of a report
        static string Hide(string text)
        {
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile)) text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(Environment.UserName) && Environment.UserName.Length > 2) text = text.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { }
            return text;
        }
    }
}
