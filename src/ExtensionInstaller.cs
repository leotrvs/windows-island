using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace WindowsIsland
{
    // Unpacks the browser extension that is embedded in the exe, with the island's port and token in a
    // config.json beside it, and opens the browser's extensions page. The user still has to load the folder
    // there (Chrome allows nothing else outside its store), but needs no copy and paste.
    public static class ExtensionInstaller
    {
        const string Prefix = "extension/";

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsIsland", "extension"); }
        }

        // Writes the extension files and returns the folder. The token is validated hex and the port a number,
        // so the config file can be built by hand.
        public static string Extract(string token, int port)
        {
            string dir = Folder;
            Directory.CreateDirectory(dir);
            Assembly assembly = typeof(ExtensionInstaller).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string file = name.Substring(Prefix.Length);
                if (file.Length == 0 || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || file.Contains("..")) continue;
                using (Stream source = assembly.GetManifestResourceStream(name))
                using (FileStream target = File.Create(Path.Combine(dir, file)))
                    source.CopyTo(target);
            }
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"port\":" + port + ",\"token\":\"" + token + "\"}");
            return dir;
        }

        // Opens chrome://extensions or edge://extensions in that browser. False when it is not installed.
        public static bool OpenExtensionsPage(bool edge)
        {
            string exe = FindBrowser(edge ? "msedge.exe" : "chrome.exe");
            if (exe == null) return false;
            try
            {
                Process.Start(new ProcessStartInfo(exe, edge ? "edge://extensions" : "chrome://extensions") { UseShellExecute = false });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string FindBrowser(string exe)
        {
            foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                    {
                        string path = key == null ? null : key.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                    }
                }
                catch (Exception) { }
            }
            return null;
        }
    }
}
