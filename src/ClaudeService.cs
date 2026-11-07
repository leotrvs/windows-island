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
    // small files Claude Code keeps per session, and the limits from Claude Code's own /usage command.
    // When Claude Usage Taskbar is installed and has fresher numbers, its file is used instead.
    sealed class ClaudeService
    {
        sealed class SessionInfo
        {
            public string Id;
            public string Name;
            public string Status;
            public DateTime StatusSince;
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
        long usageTakenTicks;  // When the numbers on screen were read (UTC)
        long lastProbeTicks;
        int probing;

        static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(2);
        static readonly TimeSpan ProbeWhenLooking = TimeSpan.FromSeconds(45);
        static readonly TimeSpan ProbeFloor = TimeSpan.FromSeconds(20);

        public bool ClaudeFound
        {
            get { return UsageProbe.ClaudeFound; }
        }

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
                UsageSnapshot usage = ReadUsage();
                dispatcher.BeginInvoke(new Action(() => Apply(sessions, usage)));
                MaybeProbe(ProbeInterval, ProbeInterval);
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

        void Apply(List<SessionInfo> sessions, UsageSnapshot usage)
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
            if (usage != null) TakeUsage(usage);

            string next = Running + "|" + busy + "|" + waiting + "|" + busySince + "|" + busyName + "|" + waitingName + "|" + usageVersion;
            if (next == signature) return;
            signature = next;
            Changed?.Invoke();
        }

        // Called when the user opens the Claude panel, so the limits are fresh when someone looks.
        public void RefreshUsage()
        {
            MaybeProbe(ProbeWhenLooking, ProbeFloor);
        }

        void MaybeProbe(TimeSpan maxAge, TimeSpan minGap)
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - Interlocked.Read(ref usageTakenTicks) < maxAge.Ticks) return;
            if (now - Interlocked.Read(ref lastProbeTicks) < minGap.Ticks) return;
            if (Interlocked.Exchange(ref probing, 1) == 1) return;

            Interlocked.Exchange(ref lastProbeTicks, now);
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    UsageSnapshot snapshot = UsageProbe.Run();
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (snapshot != null) TakeUsage(snapshot);
                        else Changed?.Invoke(); // Lets the panel explain when Claude Code is missing
                    }));
                }
                finally
                {
                    Interlocked.Exchange(ref probing, 0);
                }
            });
        }

        // Keeps whichever reading is newest: our own /usage run or Claude Usage Taskbar's file.
        void TakeUsage(UsageSnapshot usage)
        {
            if (usage.TakenUtc.Ticks <= Interlocked.Read(ref usageTakenTicks)) return;
            Interlocked.Exchange(ref usageTakenTicks, usage.TakenUtc.Ticks);
            Session = usage.Session;
            Weekly = usage.Weekly;
            usageVersion++;
            signature = null;
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

        UsageSnapshot ReadUsage()
        {
            try
            {
                string path = UsagePath;
                if (!File.Exists(path)) return null;
                DateTime write = File.GetLastWriteTimeUtc(path);
                if (write == usageWrite) return null;

                using (JsonDocument document = JsonDocument.Parse(ReadShared(path)))
                {
                    UsageSnapshot pair = new UsageSnapshot();
                    pair.Session = Window(document.RootElement, "five_hour");
                    pair.Weekly = Window(document.RootElement, "seven_day");
                    pair.TakenUtc = write;
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
