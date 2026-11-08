using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace WindowsIsland
{
    // Claude Code in a terminal marks a session "waiting" when it asks for permission. The Claude Desktop app
    // keeps such a session "busy", so the island works it out from the session's own transcript: a tool call
    // that has been asked for but has no result yet, for a few seconds, without the tool visibly running, is a
    // question waiting for an answer (a permission, or a question Claude asks). Only the transcript's tool
    // names and times are read, never what was said.
    static class ClaudeWaiting
    {
        const int TailBytes = 256 * 1024;
        static readonly TimeSpan QuestionAfter = TimeSpan.FromSeconds(1);
        static readonly TimeSpan PermissionAfter = TimeSpan.FromSeconds(4);

        // Questions to the user: waiting as soon as they are asked.
        static readonly HashSet<string> Questions = new HashSet<string>(StringComparer.Ordinal) { "AskUserQuestion", "ExitPlanMode" };
        // Tools that run a program of their own: waiting only when that program has not been started.
        static readonly HashSet<string> Shells = new HashSet<string>(StringComparer.Ordinal) { "Bash", "PowerShell" };
        // Tools that may legitimately take long without a child process: never taken as waiting.
        static readonly HashSet<string> Slow = new HashSet<string>(StringComparer.Ordinal)
            { "Task", "Agent", "WebFetch", "WebSearch", "Workflow", "Monitor", "ScheduleWakeup", "Skill", "TaskOutput", "ToolSearch" };

        sealed class Pending
        {
            public string Name;
            public DateTime Since; // UTC
        }

        sealed class Cached
        {
            public string Path;
            public long Length;
            public DateTime Write;
            public Pending Oldest;
            public DateTime SearchedAt; // When the transcript was last looked for in vain
        }

        static readonly Dictionary<string, Cached> cache = new Dictionary<string, Cached>();

        // True when the session is waiting for the user, by the rules above.
        public static bool IsWaiting(string sessionId, int processId)
        {
            try
            {
                Pending pending = OldestPending(sessionId);
                if (pending == null) return false;
                TimeSpan age = DateTime.UtcNow - pending.Since;
                if (Questions.Contains(pending.Name)) return age >= QuestionAfter;
                if (age < PermissionAfter) return false;
                if (Slow.Contains(pending.Name) || pending.Name.StartsWith("mcp__", StringComparison.Ordinal)) return false;
                if (Shells.Contains(pending.Name)) return !StartedChildSince(processId, pending.Since.AddSeconds(-1));
                return true; // Edit, Write, Read and the like finish at once when allowed
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string FindTranscript(string sessionId)
        {
            string root = Path.Combine(ConfigDirectory, "projects");
            if (!Directory.Exists(root)) return null;
            foreach (string folder in Directory.GetDirectories(root))
            {
                string candidate = Path.Combine(folder, sessionId + ".jsonl");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        static string ConfigDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                return !string.IsNullOrEmpty(configured) ? configured : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            }
        }

        static Pending OldestPending(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || sessionId.IndexOfAny(new[] { '/', '\\', ':', '.' }) >= 0) return null;
            Cached entry;
            if (!cache.TryGetValue(sessionId, out entry)) { entry = new Cached(); cache[sessionId] = entry; }
            if (entry.Path == null || !File.Exists(entry.Path))
            {
                if (entry.Path == null && DateTime.Now - entry.SearchedAt < TimeSpan.FromSeconds(30)) return null; // Not looked for every second
                entry.Path = FindTranscript(sessionId);
                entry.Length = -1;
                if (entry.Path == null) { entry.SearchedAt = DateTime.Now; return null; }
            }

            FileInfo info = new FileInfo(entry.Path);
            if (info.Length == entry.Length && info.LastWriteTimeUtc == entry.Write) return entry.Oldest;
            entry.Length = info.Length;
            entry.Write = info.LastWriteTimeUtc;
            entry.Oldest = Scan(entry.Path, info.Length);
            return entry.Oldest;
        }

        static Pending Scan(string path, long length)
        {
            byte[] bytes;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, length - TailBytes);
                stream.Seek(start, SeekOrigin.Begin);
                bytes = new byte[length - start];
                int read = 0;
                while (read < bytes.Length) { int n = stream.Read(bytes, read, bytes.Length - read); if (n <= 0) break; read += n; }
                if (read < bytes.Length) Array.Resize(ref bytes, read);
                if (start > 0)
                {
                    int newline = Array.IndexOf(bytes, (byte)'\n'); // The first line is cut: skip it
                    if (newline < 0) return null;
                    bytes = new ArraySegment<byte>(bytes, newline + 1, bytes.Length - newline - 1).ToArray();
                }
            }

            Dictionary<string, Pending> open = new Dictionary<string, Pending>();
            foreach (string line in Encoding.UTF8.GetString(bytes).Split('\n'))
            {
                if (line.Length < 20) continue;
                bool assistant = line.Contains("\"type\":\"assistant\"", StringComparison.Ordinal);
                bool result = line.Contains("\"tool_result\"", StringComparison.Ordinal);
                if (!assistant && !result) continue;
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(line))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement side;
                        if (root.TryGetProperty("isSidechain", out side) && side.ValueKind == JsonValueKind.True) continue; // A sub-agent's own calls
                        JsonElement message, content;
                        if (!root.TryGetProperty("message", out message) || !message.TryGetProperty("content", out content) || content.ValueKind != JsonValueKind.Array) continue;
                        DateTime when = DateTime.UtcNow;
                        JsonElement stamp;
                        if (root.TryGetProperty("timestamp", out stamp) && stamp.ValueKind == JsonValueKind.String)
                            DateTime.TryParse(stamp.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out when);
                        foreach (JsonElement block in content.EnumerateArray())
                        {
                            JsonElement type;
                            if (!block.TryGetProperty("type", out type)) continue;
                            string kind = type.GetString();
                            if (kind == "tool_use")
                            {
                                JsonElement id, name;
                                if (block.TryGetProperty("id", out id) && block.TryGetProperty("name", out name) && id.ValueKind == JsonValueKind.String)
                                    open[id.GetString()] = new Pending { Name = name.GetString() ?? "", Since = when };
                            }
                            else if (kind == "tool_result")
                            {
                                JsonElement id;
                                if (block.TryGetProperty("tool_use_id", out id) && id.ValueKind == JsonValueKind.String) open.Remove(id.GetString());
                            }
                        }
                    }
                }
                catch (JsonException) { } // A line still being written
            }

            Pending oldest = null;
            foreach (Pending p in open.Values) if (oldest == null || p.Since < oldest.Since) oldest = p;
            return oldest;
        }

        // ---------- Did the tool start a program? ----------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32
        {
            public int dwSize, cntUsage, th32ProcessID;
            public UIntPtr th32DefaultHeapID;
            public int th32ModuleID, cntThreads, th32ParentProcessID, pcPriClassBase, dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr process, out long created, out long exit, out long kernel, out long user);

        // Whether a direct child of the session's process was created after the tool was asked for. A shell
        // command that is running has one; a command held back for permission has not (the long-lived children
        // such as tool servers started earlier do not count).
        static bool StartedChildSince(int processId, DateTime sinceUtc)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return true; // Cannot tell: do not claim a wait
            try
            {
                PROCESSENTRY32 entry = new PROCESSENTRY32 { dwSize = Marshal.SizeOf<PROCESSENTRY32>() };
                if (!Process32First(snapshot, ref entry)) return true;
                long limit = sinceUtc.ToFileTimeUtc();
                do
                {
                    if (entry.th32ParentProcessID != processId) continue;
                    IntPtr handle = OpenProcess(0x1000, false, entry.th32ProcessID); // PROCESS_QUERY_LIMITED_INFORMATION
                    if (handle == IntPtr.Zero) continue;
                    try
                    {
                        long created, exit, kernel, user;
                        if (GetProcessTimes(handle, out created, out exit, out kernel, out user) && created >= limit) return true;
                    }
                    finally { CloseHandle(handle); }
                } while (Process32Next(snapshot, ref entry));
                return false;
            }
            finally { CloseHandle(snapshot); }
        }
    }
}
