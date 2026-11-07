using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Follows OpenAI Codex without touching its login. Codex writes each session as a JSON-lines file in
    // ~/.codex/sessions/YYYY/MM/DD. From the end of the newest one the island takes only two things:
    // whether the last task started or completed, and the rate-limit percentages Codex records. Prompts,
    // replies and file contents are never kept.
    sealed class CodexService
    {
        const int PollMs = 2000;
        const int TailBytes = 512 * 1024;
        const int MaxScanBytes = 16 * 1024 * 1024;
        const int RecentDays = 10;
        static readonly TimeSpan StaleTask = TimeSpan.FromMinutes(30); // A task with no writes for this long has died

        public event Action Changed;
        public event Action Finished;

        public bool Installed { get; private set; }
        public bool Working { get; private set; }
        public DateTime? WorkingSince { get; private set; }
        public UsageWindow Session { get; private set; }
        public UsageWindow Weekly { get; private set; }

        readonly Dispatcher dispatcher;
        Timer timer;
        int polling;
        string lastFile;
        long lastLength = -1;
        DateTime lastWrite;
        bool primed;
        string signature;

        sealed class Snapshot
        {
            public bool Installed;
            public bool Working;
            public DateTime? WorkingSince;
            public UsageWindow Session;
            public UsageWindow Weekly;
            public bool Unchanged;
        }

        public CodexService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        static string Home
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("CODEX_HOME");
                return !string.IsNullOrEmpty(configured)
                    ? configured
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            }
        }

        // Made-up activity and limits for previews and documentation images.
        public void UseSample()
        {
            Installed = true;
            Working = true;
            WorkingSince = DateTime.Now.AddSeconds(-125);
            Session = new UsageWindow { Percent = 23, ResetsAt = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 3) };
            Weekly = new UsageWindow { Percent = 9, ResetsAt = DateTime.Now.Date.AddDays(4).AddHours(14) };
        }

        // Support aid (--diagnose-codex): reads once and describes what the island sees, numbers only.
        public string Diagnose()
        {
            Snapshot snapshot = Read();
            return "installed=" + snapshot.Installed + " file=" + (lastFile == null ? "none" : Path.GetFileName(lastFile)) +
                   " working=" + snapshot.Working + " since=" + snapshot.WorkingSince +
                   " session=" + snapshot.Session?.Percent + "% resets=" + snapshot.Session?.ResetsAt +
                   " weekly=" + snapshot.Weekly?.Percent + "% resets=" + snapshot.Weekly?.ResetsAt;
        }

        public void Start()
        {
            timer = new Timer(_ => Poll(), null, 500, PollMs);
        }

        void Poll()
        {
            if (Interlocked.Exchange(ref polling, 1) == 1) return;
            try
            {
                Snapshot snapshot = Read();
                if (!snapshot.Unchanged) dispatcher.BeginInvoke(new Action(() => Apply(snapshot)));
            }
            catch (Exception)
            {
                // Tried again on the next tick.
            }
            finally
            {
                Interlocked.Exchange(ref polling, 0);
            }
        }

        void Apply(Snapshot snapshot)
        {
            bool wasWorking = Working;
            Installed = snapshot.Installed;
            Working = snapshot.Working;
            WorkingSince = snapshot.WorkingSince;
            if (snapshot.Session != null) Session = snapshot.Session;
            if (snapshot.Weekly != null) Weekly = snapshot.Weekly;

            if (primed && wasWorking && !Working) Finished?.Invoke();
            primed = true;

            string next = Installed + "|" + Working + "|" + WorkingSince + "|" + Session?.Percent + "|" + Session?.ResetsAt + "|" + Weekly?.Percent;
            if (next == signature) return;
            signature = next;
            Changed?.Invoke();
        }

        Snapshot Read()
        {
            Snapshot snapshot = new Snapshot { Installed = Directory.Exists(Home) };
            if (!snapshot.Installed) return snapshot;

            FileInfo newest = NewestSession();
            if (newest == null) return snapshot;

            // Nothing new was written: only the "task went stale" clock can change the answer.
            bool sameFile = newest.FullName == lastFile && newest.Length == lastLength && newest.LastWriteTimeUtc == lastWrite;
            if (sameFile && !(Working && DateTime.UtcNow - lastWrite > StaleTask))
            {
                snapshot.Unchanged = true;
                return snapshot;
            }
            bool newFile = newest.FullName != lastFile;
            lastFile = newest.FullName;
            lastLength = newest.Length;
            lastWrite = newest.LastWriteTimeUtc;

            string lastLifecycle = null;
            DateTime? lifecycleTime = null;
            string limits = null;
            ScanBackwards(newest.FullName, newest.Length, newFile || Session == null, ref lastLifecycle, ref lifecycleTime, ref limits);

            snapshot.Working = lastLifecycle == "task_started" && DateTime.UtcNow - newest.LastWriteTimeUtc < StaleTask;
            snapshot.WorkingSince = snapshot.Working ? lifecycleTime : null;
            if (limits != null) ParseLimits(limits, snapshot);
            return snapshot;
        }

        // The session files of the last few days, newest first by write time.
        static FileInfo NewestSession()
        {
            string root = Path.Combine(Home, "sessions");
            if (!Directory.Exists(root)) return null;

            FileInfo newest = null;
            DateTime today = DateTime.Now.Date;
            for (int day = 0; day < RecentDays; day++)
            {
                DateTime date = today.AddDays(-day);
                string folder = Path.Combine(root, date.ToString("yyyy", CultureInfo.InvariantCulture),
                    date.ToString("MM", CultureInfo.InvariantCulture), date.ToString("dd", CultureInfo.InvariantCulture));
                if (!Directory.Exists(folder)) continue;
                foreach (FileInfo file in new DirectoryInfo(folder).EnumerateFiles("*.jsonl"))
                    if (newest == null || file.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = file;
            }
            if (newest != null) return newest;

            // Nothing recent: fall back to the newest file anywhere, for the last known limits.
            foreach (FileInfo file in new DirectoryInfo(root).EnumerateFiles("*.jsonl", SearchOption.AllDirectories))
                if (newest == null || file.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = file;
            return newest;
        }

        // Reads the file from the end in chunks until it has seen the last task event and the last
        // rate-limit record (or gives up after a few MB, or after one chunk when limits are not needed).
        static void ScanBackwards(string path, long length, bool needLimits, ref string lifecycle, ref DateTime? lifecycleTime, ref string limits)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long end = stream.Length;
                long scanned = 0;
                string carry = "";
                while (end > 0 && scanned < MaxScanBytes)
                {
                    int size = (int)Math.Min(TailBytes, end);
                    long start = end - size;
                    byte[] buffer = new byte[size];
                    stream.Position = start;
                    int read = 0;
                    while (read < size)
                    {
                        int n = stream.Read(buffer, read, size - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    string text = Encoding.UTF8.GetString(buffer, 0, read) + carry;
                    int firstBreak = start > 0 ? text.IndexOf('\n') : -1;
                    carry = firstBreak >= 0 ? text.Substring(0, firstBreak) : "";
                    string body = firstBreak >= 0 ? text.Substring(firstBreak + 1) : text;

                    string[] lines = body.Split('\n');
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        string line = lines[i];
                        if (line.IndexOf("\"event_msg\"", StringComparison.Ordinal) < 0) continue;
                        if (lifecycle == null)
                        {
                            if (line.IndexOf("\"task_started\"", StringComparison.Ordinal) >= 0) { lifecycle = "task_started"; lifecycleTime = Timestamp(line); }
                            else if (line.IndexOf("\"task_complete\"", StringComparison.Ordinal) >= 0 ||
                                     line.IndexOf("\"turn_aborted\"", StringComparison.Ordinal) >= 0) lifecycle = "task_complete";
                        }
                        if (limits == null && line.IndexOf("\"rate_limits\"", StringComparison.Ordinal) >= 0 &&
                            line.IndexOf("\"used_percent\"", StringComparison.Ordinal) >= 0) limits = line;
                        if (lifecycle != null && (limits != null || !needLimits)) return;
                    }
                    scanned += size;
                    end = start;
                    if (!needLimits && lifecycle != null) return;
                }
            }
        }

        static DateTime? Timestamp(string line)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(line))
                {
                    JsonElement ts;
                    DateTimeOffset parsed;
                    if (document.RootElement.TryGetProperty("timestamp", out ts) && ts.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                        return parsed.LocalDateTime;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        // Only the numbers: used percentage and reset time of the 5-hour (primary) and weekly (secondary) windows.
        static void ParseLimits(string line, Snapshot snapshot)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(line))
                {
                    JsonElement payload, info, limits;
                    if (!document.RootElement.TryGetProperty("payload", out payload)) return;
                    if (!payload.TryGetProperty("rate_limits", out limits) &&
                        !(payload.TryGetProperty("info", out info) && info.ValueKind == JsonValueKind.Object && info.TryGetProperty("rate_limits", out limits)))
                        return;
                    if (limits.ValueKind != JsonValueKind.Object) return;

                    UsageWindow primary = Window(limits, "primary");
                    UsageWindow secondary = Window(limits, "secondary");
                    if (primary != null) snapshot.Session = primary;
                    if (secondary != null) snapshot.Weekly = secondary;
                }
            }
            catch (Exception)
            {
            }
        }

        static UsageWindow Window(JsonElement limits, string name)
        {
            JsonElement window, used, resets;
            if (!limits.TryGetProperty(name, out window) || window.ValueKind != JsonValueKind.Object) return null;
            UsageWindow result = new UsageWindow();
            if (window.TryGetProperty("used_percent", out used) && used.ValueKind == JsonValueKind.Number)
                result.Percent = (int)Math.Round(used.GetDouble());
            long seconds;
            if (window.TryGetProperty("resets_at", out resets) && resets.ValueKind == JsonValueKind.Number && resets.TryGetInt64(out seconds))
                result.ResetsAt = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
            return result.Percent.HasValue ? result : null;
        }
    }
}
