using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
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
        readonly Dictionary<string, DateTime> lastWrites = new Dictionary<string, DateTime>(); // When each manifest was last written (UTC)
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
        Dictionary<string, DateTime> logTimes = new Dictionary<string, DateTime>();

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
                        string text = reader.ReadToEnd();
                        Dictionary<string, string> found = SteamManifest.States(text);
                        Dictionary<string, DateTime> foundTimes = SteamManifest.StateTimes(text);
                        // Keep what an older read knew about games that did not change in this tail
                        foreach (KeyValuePair<string, string> old in logStates) if (!found.ContainsKey(old.Key)) found[old.Key] = old.Value;
                        foreach (KeyValuePair<string, DateTime> old in logTimes) if (!foundTimes.ContainsKey(old.Key)) foundTimes[old.Key] = old.Value;
                        logStates = found;
                        logTimes = foundTimes;
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

        // ---- Bytes Steam has written to disk (its process counters): the progress between two manifest writes ----

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct IoCounters { public ulong Reads, Writes, Others, ReadBytes, WriteBytes, OtherBytes; }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

        static long SteamWrites()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("steam"))
                {
                    using (process)
                    {
                        IntPtr handle = OpenProcess(0x0400, false, process.Id); // Query information
                        if (handle == IntPtr.Zero) continue;
                        try
                        {
                            IoCounters counters;
                            if (GetProcessIoCounters(handle, out counters)) return (long)Math.Min(counters.WriteBytes, (ulong)long.MaxValue);
                        }
                        finally
                        {
                            CloseHandle(handle);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // No counters: the manifest numbers are used
            }
            return -1;
        }

        sealed class Anchor { public long Staged; public long Writes; public bool Valid; public long ManifestStaged = -1; }
        readonly Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>();

        // Progress from the bytes written since the manifest was last written (at the start of the run), or -1 when it cannot be told.
        int FromWrites(SteamManifest manifest, DateTime manifestWrite)
        {
            if (manifest.BytesToStage <= 0) return -2; // Nothing to count against: use the manifest
            long writes = SteamWrites();
            if (writes < 0) return -2;
            Anchor anchor;
            if (!anchors.TryGetValue(manifest.AppId, out anchor) || manifest.BytesStaged != anchor.ManifestStaged)
            {
                // The manifest was just written (the run started, or Steam saved where it was): count from here. It only counts if
                // it was written when the run started; joining a run in progress leaves it unknown
                DateTime stateAt;
                bool fresh = logTimes.TryGetValue(manifest.AppId, out stateAt) && manifestWrite.ToLocalTime() >= stateAt - TimeSpan.FromSeconds(3);
                if (anchor == null) anchor = anchors[manifest.AppId] = new Anchor { Valid = fresh };
                else if (fresh) anchor.Valid = true;
                anchor.Staged = manifest.BytesStaged;
                anchor.ManifestStaged = manifest.BytesStaged;
                anchor.Writes = writes;
            }
            if (!anchor.Valid) return -1;
            return SteamManifest.EstimateFromWrites(anchor.Staged, writes - anchor.Writes, manifest.BytesToStage);
        }

        int Percent(SteamManifest manifest)
        {
            int written = FromWrites(manifest, lastWrites.TryGetValue(manifest.AppId, out DateTime stamp) ? stamp : DateTime.MinValue);
            if (written != -2) return written;
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
                        lastWrites[manifest.AppId] = write;
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
                        anchors.Remove(manifest.AppId);
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
