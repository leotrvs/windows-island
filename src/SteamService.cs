using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowsIsland
{
    // Follows Steam's downloads and updates by reading the small manifest files Steam keeps beside the games
    // (steamapps\appmanifest_*.acf in each library folder): the game's name and how many bytes are done. Nothing is sent
    // anywhere, nothing is written, and Steam itself is not touched. Each game being worked on is posted to the island
    // as an activity with a progress bar.
    sealed class SteamService
    {
        public event Action<string, string, int, bool> Progress; // app id, game name, percent (-1 unknown), finished

        readonly DispatcherTimer timer;
        readonly Dictionary<string, DateTime> seen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase); // manifest path -> last write read
        readonly Dictionary<string, string> active = new Dictionary<string, string>(); // app id -> name, while it works
        readonly Dictionary<string, SteamManifest> last = new Dictionary<string, SteamManifest>();
        List<string> libraries = new List<string>();
        DateTime librariesAt = DateTime.MinValue;

        public SteamService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            seen.Clear();
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            foreach (KeyValuePair<string, string> app in active.ToArray()) Progress?.Invoke(app.Key, app.Value, -1, true);
            active.Clear();
            last.Clear();
        }

        static string SteamPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string path = key == null ? null : key.GetValue("SteamPath") as string;
                    return string.IsNullOrEmpty(path) ? null : path.Replace('/', Path.DirectorySeparatorChar);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        void FindLibraries()
        {
            if (DateTime.Now - librariesAt < TimeSpan.FromMinutes(2)) return;
            librariesAt = DateTime.Now;
            List<string> found = new List<string>();
            string steam = SteamPath();
            if (steam != null)
            {
                found.Add(steam);
                try
                {
                    string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (string library in SteamManifest.Libraries(ReadShared(vdf)))
                            if (!found.Contains(library, StringComparer.OrdinalIgnoreCase)) found.Add(library);
                }
                catch (Exception) { }
            }
            libraries = found;
        }

        static string ReadShared(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        void Poll()
        {
            try
            {
                FindLibraries();
                HashSet<string> now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string library in libraries)
                {
                    string apps = Path.Combine(library, "steamapps");
                    if (!Directory.Exists(apps)) continue;
                    foreach (string file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                    {
                        DateTime write = File.GetLastWriteTimeUtc(file);
                        string appId = Path.GetFileNameWithoutExtension(file).Substring("appmanifest_".Length);
                        DateTime before;
                        bool changed = !seen.TryGetValue(file, out before) || before != write;
                        // A game that is being worked on is read every time (Steam may change it in place); others only when they change
                        if (!changed && !active.ContainsKey(appId)) continue;
                        seen[file] = write;
                        SteamManifest manifest = SteamManifest.Parse(ReadShared(file));
                        if (manifest == null) continue;
                        last[manifest.AppId] = manifest;
                        if (manifest.IsWorking || manifest.IsPaused)
                        {
                            now.Add(manifest.AppId);
                            active[manifest.AppId] = string.IsNullOrEmpty(manifest.Name) ? "Steam" : manifest.Name;
                            Progress?.Invoke(manifest.AppId, active[manifest.AppId], manifest.Progress, false);
                        }
                    }
                }
                // A game that was working and no longer is: finished (or cancelled; the manifest then says it is not installed)
                foreach (KeyValuePair<string, string> app in active.ToArray())
                {
                    SteamManifest manifest;
                    bool stillWorking = last.TryGetValue(app.Key, out manifest) && (manifest.IsWorking || manifest.IsPaused);
                    if (stillWorking) continue;
                    active.Remove(app.Key);
                    bool installed = manifest != null && (manifest.StateFlags & 4) != 0;
                    Progress?.Invoke(app.Key, app.Value, installed ? 100 : -1, true); // 100 and finished: done; -1 and finished: gone
                }
            }
            catch (Exception)
            {
                // Steam is not installed, or a file was being written: tried again on the next tick
            }
        }
    }
}
