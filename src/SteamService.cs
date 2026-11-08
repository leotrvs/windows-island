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

        // Steam's own log is the quickest to say what runs and what is paused; the manifest follows later
        string logPath;
        long logLength = -1;
        DateTime logWrite;
        Dictionary<string, string> logStates = new Dictionary<string, string>();

        void ReadLog()
        {
            try
            {
                string steam = SteamPath();
                if (steam == null) return;
                FileInfo info = new FileInfo(Path.Combine(steam, "logs", "content_log.txt"));
                if (!info.Exists) { logStates = new Dictionary<string, string>(); return; }
                if (info.Length == logLength && info.LastWriteTimeUtc == logWrite) return;
                logLength = info.Length;
                logWrite = info.LastWriteTimeUtc;
                using (FileStream stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    const int Tail = 512 * 1024;
                    if (stream.Length > Tail) stream.Seek(-Tail, SeekOrigin.End);
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        Dictionary<string, string> found = SteamManifest.States(reader.ReadToEnd());
                        // Keep what an older read knew about games that did not change in this tail
                        foreach (KeyValuePair<string, string> old in logStates) if (!found.ContainsKey(old.Key)) found[old.Key] = old.Value;
                        logStates = found;
                    }
                }
            }
            catch (Exception)
            {
                // The log is a help, not a need
            }
        }

        // For the pace between two manifest writes: the value and when it was seen, per game and phase
        sealed class Sample { public long Value; public DateTime At; public double Rate; public bool Downloading; }
        readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();

        int Percent(SteamManifest manifest)
        {
            Sample sample;
            DateTime now = DateTime.UtcNow;
            if (!samples.TryGetValue(manifest.AppId, out sample) || sample.Downloading != manifest.IsDownloading || manifest.Current < sample.Value)
            {
                samples[manifest.AppId] = new Sample { Value = manifest.Current, At = now, Rate = 0, Downloading = manifest.IsDownloading };
                return manifest.Progress;
            }
            if (manifest.Current != sample.Value)
            {
                double seconds = (now - sample.At).TotalSeconds;
                if (seconds > 0.5 && seconds < 180) sample.Rate = (manifest.Current - sample.Value) / seconds;
                sample.Value = manifest.Current;
                sample.At = now;
            }
            return SteamManifest.Estimate(sample.Value, manifest.Total, sample.Rate, (now - sample.At).TotalSeconds);
        }

        void Poll()
        {
            try
            {
                FindLibraries();
                ReadLog();
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
                    }
                }

                foreach (SteamManifest manifest in last.Values.ToArray())
                {
                    string state;
                    bool running = logStates.TryGetValue(manifest.AppId, out state) ? SteamManifest.StateIsRunning(state) : manifest.IsRunning;
                    if (running)
                    {
                        string name = string.IsNullOrEmpty(manifest.Name) ? "Steam" : manifest.Name;
                        active[manifest.AppId] = name;
                        Progress?.Invoke(manifest.AppId, name, Percent(manifest), false);
                    }
                    else
                    {
                        string name;
                        if (!active.TryGetValue(manifest.AppId, out name)) continue;
                        active.Remove(manifest.AppId);
                        samples.Remove(manifest.AppId);
                        bool done = logStates.TryGetValue(manifest.AppId, out state) ? SteamManifest.StateIsDone(state) : (manifest.StateFlags & 6) == 4;
                        Progress?.Invoke(manifest.AppId, name, done ? 100 : -1, true); // 100 and finished: done; -1 and finished: gone (paused or cancelled)
                    }
                }
            }
            catch (Exception)
            {
                // Steam is not installed, or a file was being written: tried again on the next tick
            }
        }
    }
}
