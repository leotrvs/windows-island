using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;

namespace WindowsIsland
{
    sealed class UsageWindow
    {
        public int? Percent;
        public DateTime? ResetsAt;

        // Claude Code only reports again after its next response, so a passed window counts as reset.
        public int EffectivePercent
        {
            get { return ResetsAt.HasValue && ResetsAt.Value <= DateTime.Now ? 0 : Percent ?? 0; }
        }

        public DateTime? EffectiveReset
        {
            get { return ResetsAt.HasValue && ResetsAt.Value <= DateTime.Now ? null : ResetsAt; }
        }
    }

    // Follows Claude Code without ever touching its login: the status of running sessions comes from the
    // small files Claude Code keeps per session, and the limits from the file Claude Usage Taskbar saves.
    sealed class ClaudeService
    {
        sealed class SessionInfo
        {
            public string Id;
            public string Name;
            public string Status;
            public DateTime StatusSince;
        }

        sealed class UsagePair
        {
            public UsageWindow Session;
            public UsageWindow Weekly;
        }

        sealed class Seen
        {
            public string Status;
            public DateTime Since;
        }

        static readonly TimeSpan MinimumWorkForAlert = TimeSpan.FromSeconds(5);

        public event Action Changed;
        public event Action<bool, string> Alert; // true when Claude is waiting for the user, false when it finished

        public int Running { get; private set; }
        public int Busy { get; private set; }
        public int Waiting { get; private set; }
        public DateTime? BusySince { get; private set; }
        public string BusyName { get; private set; }
        public string WaitingName { get; private set; }
        public UsageWindow Session { get; private set; }
        public UsageWindow Weekly { get; private set; }

        readonly Dispatcher dispatcher;
        readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();
        Timer timer;
        int polling;
        bool primed;
        string signature;
        DateTime usageWrite = DateTime.MinValue;
        int usageVersion;

        public ClaudeService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        static string ConfigDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                return !string.IsNullOrEmpty(configured)
                    ? configured
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            }
        }

        static string UsagePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClaudeUsageTaskbar", "usage.json");
            }
        }

        // Made-up sessions and limits for previews and documentation images.
        public void UseSample()
        {
            Running = 2;
            Busy = 1;
            BusySince = DateTime.Now.AddSeconds(-84);
            BusyName = Strings.SampleSession;
            Session = new UsageWindow { Percent = 42, ResetsAt = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 2).AddMinutes(30) };
            Weekly = new UsageWindow { Percent = 18, ResetsAt = DateTime.Now.Date.AddDays(3).AddHours(9) };
        }

        public void Start()
        {
            timer = new Timer(_ => Poll(), null, 300, 1000);
        }

        void Poll()
        {
            if (Interlocked.Exchange(ref polling, 1) == 1) return;
            try
            {
                List<SessionInfo> sessions = ReadSessions();
                UsagePair usage = ReadUsage();
                dispatcher.BeginInvoke(new Action(() => Apply(sessions, usage)));
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

        void Apply(List<SessionInfo> sessions, UsagePair usage)
        {
            int busy = 0, waiting = 0;
            DateTime? busySince = null;
            string busyName = null, waitingName = null;
            HashSet<string> present = new HashSet<string>();

            foreach (SessionInfo session in sessions)
            {
                present.Add(session.Id);
                if (session.Status == "busy")
                {
                    busy++;
                    if (busySince == null || session.StatusSince < busySince)
                    {
                        busySince = session.StatusSince;
                        busyName = session.Name;
                    }
                }
                else if (session.Status == "waiting")
                {
                    waiting++;
                    if (waitingName == null) waitingName = session.Name;
                }

                Seen previous;
                if (seen.TryGetValue(session.Id, out previous) && previous.Status != session.Status && primed)
                {
                    if (session.Status == "waiting")
                        Alert?.Invoke(true, session.Name);
                    else if (previous.Status == "busy" && session.Status == "idle" &&
                             session.StatusSince - previous.Since >= MinimumWorkForAlert)
                        Alert?.Invoke(false, session.Name);
                }
                if (previous == null || previous.Status != session.Status)
                    seen[session.Id] = new Seen { Status = session.Status, Since = session.StatusSince };
            }

            foreach (string id in new List<string>(seen.Keys))
                if (!present.Contains(id)) seen.Remove(id);
            primed = true;

            Running = sessions.Count;
            Busy = busy;
            Waiting = waiting;
            BusySince = busySince;
            BusyName = busyName;
            WaitingName = waitingName;
            if (usage != null)
            {
                Session = usage.Session;
                Weekly = usage.Weekly;
                usageVersion++;
            }

            string next = Running + "|" + busy + "|" + waiting + "|" + busySince + "|" + busyName + "|" + waitingName + "|" + usageVersion;
            if (next == signature) return;
            signature = next;
            Changed?.Invoke();
        }

        static List<SessionInfo> ReadSessions()
        {
            List<SessionInfo> sessions = new List<SessionInfo>();
            string folder = Path.Combine(ConfigDirectory, "sessions");
            if (!Directory.Exists(folder)) return sessions;

            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    using (JsonDocument document = JsonDocument.Parse(ReadShared(file)))
                    {
                        JsonElement root = document.RootElement;
                        if (Text(root, "kind") != "interactive") continue; // Background reads are not sessions.

                        JsonElement pid;
                        int processId;
                        if (!root.TryGetProperty("pid", out pid) || !pid.TryGetInt32(out processId) || !IsRunning(processId))
                            continue; // The files outlive their process.

                        SessionInfo session = new SessionInfo();
                        session.Id = Text(root, "sessionId") ?? file;
                        session.Status = Text(root, "status") ?? "idle";
                        session.Name = Text(root, "name");
                        if (string.IsNullOrWhiteSpace(session.Name)) session.Name = "Claude Code";
                        JsonElement since;
                        long milliseconds;
                        session.StatusSince = root.TryGetProperty("statusUpdatedAt", out since) && since.TryGetInt64(out milliseconds)
                            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime
                            : DateTime.Now;
                        sessions.Add(session);
                    }
                }
                catch (Exception)
                {
                    // Files are rewritten while sessions run, so an unreadable one is skipped this time.
                }
            }
            return sessions;
        }

        UsagePair ReadUsage()
        {
            try
            {
                string path = UsagePath;
                if (!File.Exists(path)) return null;
                DateTime write = File.GetLastWriteTimeUtc(path);
                if (write == usageWrite) return null;

                using (JsonDocument document = JsonDocument.Parse(ReadShared(path)))
                {
                    UsagePair pair = new UsagePair();
                    pair.Session = Window(document.RootElement, "five_hour");
                    pair.Weekly = Window(document.RootElement, "seven_day");
                    usageWrite = write;
                    return pair;
                }
            }
            catch (Exception)
            {
                return null; // A partial write is read again on the next tick.
            }
        }

        static UsageWindow Window(JsonElement root, string name)
        {
            UsageWindow window = new UsageWindow();
            JsonElement data;
            if (!root.TryGetProperty(name, out data) || data.ValueKind != JsonValueKind.Object) return window;

            JsonElement percent;
            if (data.TryGetProperty("used_percentage", out percent) && percent.ValueKind == JsonValueKind.Number)
                window.Percent = (int)Math.Round(percent.GetDouble());

            JsonElement resets;
            if (data.TryGetProperty("resets_at", out resets))
            {
                long seconds;
                DateTimeOffset parsed;
                if (resets.ValueKind == JsonValueKind.Number && resets.TryGetInt64(out seconds))
                    window.ResetsAt = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
                else if (resets.ValueKind == JsonValueKind.String &&
                         DateTimeOffset.TryParse(resets.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    window.ResetsAt = parsed.LocalDateTime;
            }
            return window;
        }

        static string ReadShared(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        static string Text(JsonElement root, string name)
        {
            JsonElement value;
            return root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        static bool IsRunning(int processId)
        {
            try
            {
                using (Process.GetProcessById(processId)) return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
