using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowsIsland
{
    // An activity the island notices by itself, to be shown like one a script posted.
    sealed class WatchedActivity
    {
        public string Id = "", App = "", Title = "", Text = "", Glyph = "";
        public int Progress = -1;
        public bool Done, Gone;
    }

    // OBS recording or streaming, from the lines OBS writes in its own log (read only; OBS is not touched).
    sealed class ObsService
    {
        public event Action<WatchedActivity> Updated;

        readonly DispatcherTimer timer;
        bool recording, streaming;

        public ObsService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            Report("obs-rec", false, TimeSpan.Zero, ref recording);
            Report("obs-live", false, TimeSpan.Zero, ref streaming);
        }

        static string LatestLog()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "logs");
            if (!Directory.Exists(folder)) return null;
            return new DirectoryInfo(folder).GetFiles("*.txt").OrderByDescending(f => f.LastWriteTimeUtc).Select(f => f.FullName).FirstOrDefault();
        }

        void Poll()
        {
            try
            {
                TimeSpan? rec = null, live = null;
                if (Process.GetProcessesByName("obs64").Length > 0)
                {
                    string path = LatestLog();
                    if (path != null)
                    {
                        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            const int Tail = 256 * 1024;
                            if (stream.Length > Tail) stream.Seek(-Tail, SeekOrigin.End);
                            using (StreamReader reader = new StreamReader(stream)) ObsLog.Read(reader.ReadToEnd(), out rec, out live);
                        }
                    }
                }
                TimeSpan now = DateTime.Now.TimeOfDay;
                Report("obs-rec", rec.HasValue, rec.HasValue ? ObsLog.Since(rec.Value, now) : TimeSpan.Zero, ref recording);
                Report("obs-live", live.HasValue, live.HasValue ? ObsLog.Since(live.Value, now) : TimeSpan.Zero, ref streaming);
            }
            catch (Exception)
            {
                // The log is a help: tried again on the next tick
            }
        }

        void Report(string id, bool on, TimeSpan elapsed, ref bool was)
        {
            if (!on)
            {
                if (was) Updated?.Invoke(new WatchedActivity { Id = id, Gone = true });
                was = false;
                return;
            }
            was = true;
            Updated?.Invoke(new WatchedActivity
            {
                Id = id,
                App = "OBS",
                Title = id == "obs-rec" ? Strings.ObsRecording : Strings.ObsStreaming,
                Text = ObsLog.Clock(elapsed),
                Glyph = "",
            });
        }
    }

    // The Windows Game Bar's screen recording (Win+Alt+R): it writes a video in Videos\Captures while it records, a file that grows
    // every second and that no other program can open until it stops. Only that folder is looked at, only for the newest video.
    sealed class GameBarService
    {
        public event Action<WatchedActivity> Updated;

        readonly DispatcherTimer timer;
        bool recording;
        DateTime seenSince = DateTime.MinValue;

        public GameBarService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (recording) Updated?.Invoke(new WatchedActivity { Id = "gamebar-rec", Gone = true });
            recording = false;
        }

        void Poll()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
                FileInfo newest = Directory.Exists(folder)
                    ? new DirectoryInfo(folder).GetFiles("*.mp4").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
                    : null;
                bool on = false;
                if (newest != null && DateTime.UtcNow - newest.LastWriteTimeUtc < TimeSpan.FromSeconds(6))
                {
                    try
                    {
                        using (new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                    }
                    catch (IOException)
                    {
                        on = true; // Still being written
                    }
                }
                if (!on)
                {
                    if (recording) Updated?.Invoke(new WatchedActivity { Id = "gamebar-rec", Gone = true });
                    recording = false;
                    seenSince = DateTime.MinValue;
                    return;
                }
                DateTime start = ObsLog.StartFromName(newest.Name) ?? (seenSince == DateTime.MinValue ? (seenSince = DateTime.Now) : seenSince);
                TimeSpan elapsed = DateTime.Now - start;
                if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                recording = true;
                Updated?.Invoke(new WatchedActivity { Id = "gamebar-rec", App = "Xbox Game Bar", Title = Strings.ObsRecording, Text = ObsLog.Clock(elapsed), Glyph = "\uE7C8" });
            }
            catch (Exception)
            {
                // Tried again on the next tick
            }
        }
    }

    // Windows Update: that it is installing (the servicing process is busy for a while) and that a restart is needed.
    // Both only as notices: the island cannot read a percentage that Windows keeps to itself.
    sealed class WindowsUpdateService
    {
        public event Action<WatchedActivity> Updated;

        readonly DispatcherTimer timer;
        DateTime busySince = DateTime.MinValue;
        bool installing, rebootShown;
        DateTime rebootAt = DateTime.MinValue;

        public WindowsUpdateService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (installing) Updated?.Invoke(new WatchedActivity { Id = "wu-install", Gone = true });
            installing = false;
        }

        static bool RebootNeeded()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) return key != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void Poll()
        {
            try
            {
                // TiWorker is Windows' servicing process: busy for a good while means updates are going in
                bool busy = Process.GetProcessesByName("TiWorker").Length > 0;
                if (busy && busySince == DateTime.MinValue) busySince = DateTime.Now;
                if (!busy) busySince = DateTime.MinValue;
                bool now = busy && DateTime.Now - busySince > TimeSpan.FromSeconds(30);
                if (now)
                    Updated?.Invoke(new WatchedActivity { Id = "wu-install", App = "Windows", Title = Strings.WuInstalling, Glyph = "" });
                else if (installing)
                    Updated?.Invoke(new WatchedActivity { Id = "wu-install", Gone = true });
                installing = now;

                // The restart notice shows once per run of the island, for a minute and a half
                if (!rebootShown && !busy && RebootNeeded())
                {
                    rebootShown = true;
                    rebootAt = DateTime.Now;
                }
                if (rebootShown && DateTime.Now - rebootAt < TimeSpan.FromSeconds(90))
                    Updated?.Invoke(new WatchedActivity { Id = "wu-reboot", App = "Windows", Title = Strings.WuReboot, Glyph = "" });
                else if (rebootShown && rebootAt != DateTime.MinValue)
                {
                    Updated?.Invoke(new WatchedActivity { Id = "wu-reboot", Gone = true });
                    rebootAt = DateTime.MinValue;
                }
            }
            catch (Exception)
            {
                // Tried again on the next tick
            }
        }
    }
}
