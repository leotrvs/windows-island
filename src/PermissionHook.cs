using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace WindowsIsland
{
    // "WindowsIsland.exe permission-hook": the command Claude Code runs before it would ask for a permission. It reads
    // the request from stdin, asks the running island to show Allow and Deny, and prints the answer in the format
    // Claude Code expects. When the island is not running, the API is off, nothing is clicked in time, or anything
    // goes wrong, it prints nothing and exits 0, so Claude Code simply shows its own prompt as it always did.
    static class PermissionHook
    {
        const int MaxInput = 256 * 1024;
        const int ConnectTimeoutMs = 1500;
        // Only the technical fields are shown as the summary: a description is text the model wrote for itself
        static readonly string[] SummaryKeys = { "command", "file_path", "path", "url", "query", "sql", "notebook_path", "pattern" };
        const int ShownLength = 1800; // A long command is shown whole (it scrolls in the island) up to here

        public static int Run()
        {
            try
            {
                byte[] input = ReadInput();
                if (input == null) return 0;
                string tool, session, summary, project, transcript;
                bool partial;
                if (!Parse(input, out tool, out session, out summary, out project, out partial, out transcript)) return 0;

                string decision = Ask(tool, session, summary, project, partial, transcript);
                if (decision == "allow")
                    Console.Out.Write("{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}");
                else if (decision == "deny")
                    Console.Out.Write("{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"deny\",\"message\":\"Denied from Windows Island\"}}}");
                Console.Out.Flush();
            }
            catch (Exception)
            {
            }
            return 0;
        }

        static byte[] ReadInput()
        {
            using (Stream stdin = Console.OpenStandardInput())
            using (MemoryStream all = new MemoryStream())
            {
                byte[] chunk = new byte[8192];
                int n;
                while ((n = stdin.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (all.Length + n > MaxInput) return null;
                    all.Write(chunk, 0, n);
                }
                return all.ToArray();
            }
        }

        // Claude Code's own folder for transcripts: the only place a transcript path is believed to be in
        static string TranscriptRoot()
        {
            string configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            string root = !string.IsNullOrEmpty(configured) ? configured : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.GetFullPath(Path.Combine(root, "projects")) + Path.DirectorySeparatorChar;
        }

        static bool Parse(byte[] input, out string tool, out string session, out string summary, out string project, out bool partial, out string transcript)
        {
            tool = session = summary = project = transcript = null;
            partial = false;
            using (JsonDocument document = JsonDocument.Parse(input))
            {
                JsonElement root = document.RootElement, value;
                if (root.ValueKind != JsonValueKind.Object) return false;
                if (root.TryGetProperty("tool_name", out value) && value.ValueKind == JsonValueKind.String) tool = value.GetString();
                if (root.TryGetProperty("session_id", out value) && value.ValueKind == JsonValueKind.String) session = value.GetString();
                if (string.IsNullOrEmpty(tool)) return false;
                if (root.TryGetProperty("transcript_path", out value) && value.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        string full = Path.GetFullPath(value.GetString());
                        if (full.StartsWith(TranscriptRoot(), StringComparison.OrdinalIgnoreCase) && full.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) transcript = full;
                    }
                    catch (Exception) { }
                }
                if (root.TryGetProperty("cwd", out value) && value.ValueKind == JsonValueKind.String)
                {
                    try { project = Path.GetFileName(value.GetString().TrimEnd('\\', '/')); } catch (Exception) { }
                }
                JsonElement args;
                bool technical = true;
                string text = root.TryGetProperty("tool_input", out args) ? Summarize(args, out technical) : "";
                // One line and short enough to read whole: line breaks are shown, runs of spaces are folded before anything is cut
                text = System.Text.RegularExpressions.Regex.Replace(text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", " \u23CE "), @"[ \t]{2,}", " ").Trim();
                partial = !technical; // A long command is still allowed: it is shown whole and scrolls
                summary = text.Length > ShownLength ? text.Substring(0, ShownLength - 1) + "\u2026" : text;
                return true;
            }
        }

        // What the user needs to see to decide: the command, the file, the address or the query.
        static string Summarize(JsonElement args, out bool technical)
        {
            technical = true;
            if (args.ValueKind != JsonValueKind.Object) return args.ValueKind == JsonValueKind.String ? args.GetString() : args.GetRawText();
            foreach (string key in SummaryKeys)
            {
                JsonElement value;
                if (args.TryGetProperty(key, out value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
            }
            technical = false; // The arguments as they are, which may not be the whole story: look in Claude
            return args.GetRawText();
        }

        static long LengthOf(string path)
        {
            try { return path == null ? -1 : new FileInfo(path).Length; }
            catch (Exception) { return -1; }
        }

        // True when something was added to the transcript with a tool result in it: the request was answered somewhere else
        // (in Claude itself), so the prompt on the island is out of date.
        static bool AnsweredElsewhere(string path, ref long seen)
        {
            try
            {
                long length = LengthOf(path);
                if (length <= seen) return false;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long from = Math.Max(seen, length - 256 * 1024);
                    stream.Seek(from, SeekOrigin.Begin);
                    byte[] chunk = new byte[length - from];
                    int read = stream.Read(chunk, 0, chunk.Length);
                    seen = length;
                    return Encoding.UTF8.GetString(chunk, 0, read).Contains("\"tool_result\"", StringComparison.Ordinal);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string Ask(string tool, string session, string summary, string project, bool partial, string transcript)
        {
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            string token = LocalApi.ReadToken();
            if (token == null) return "none";
            int port = Settings.Load().ApiPort;
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                try
                {
                    if (!client.ConnectAsync(IPAddress.Loopback, port).Wait(ConnectTimeoutMs)) return "none";
                }
                catch (Exception)
                {
                    return "none";
                }
                // The token only goes to the island itself, never to another program that took the port.
                if (!ApiCli.AnswersAsIsland(client, port)) return "none";

                byte[] body;
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Utf8JsonWriter json = new Utf8JsonWriter(buffer))
                    {
                        json.WriteStartObject();
                        json.WriteString("tool", tool.Length > 100 ? tool.Substring(0, 100) : tool);
                        if (!string.IsNullOrEmpty(session) && session.Length <= 64) json.WriteString("session", session);
                        json.WriteString("summary", summary ?? "");
                        if (!string.IsNullOrEmpty(project) && project.Length <= 60) json.WriteString("project", project);
                        json.WriteBoolean("partial", partial);
                        json.WriteNumber("wait", ClaudeHook.WaitSeconds);
                        json.WriteEndObject();
                    }
                    body = buffer.ToArray();
                }

                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = (ClaudeHook.WaitSeconds + 15) * 1000;
                stream.WriteTimeout = 5000;
                string head = "POST /v1/permission HTTP/1.1\r\n" +
                              "Host: 127.0.0.1:" + port + "\r\n" +
                              "Authorization: Bearer " + token + "\r\n" +
                              "Content-Type: application/json\r\n" +
                              "Content-Length: " + body.Length + "\r\n" +
                              "Connection: close\r\n\r\n";
                byte[] headBytes = Encoding.ASCII.GetBytes(head);
                stream.Write(headBytes, 0, headBytes.Length);
                stream.Write(body, 0, body.Length);

                // While it waits, the transcript is watched: a tool result that appears means it was answered in Claude, and the
                // prompt is taken down (the connection closes, which the island sees) instead of waiting out its minute
                bool elsewhere = false;
                bool finished = false;
                long seenLength = LengthOf(transcript);
                if (transcript != null && seenLength >= 0)
                {
                    System.Threading.Thread watcher = new System.Threading.Thread(() =>
                    {
                        while (!finished)
                        {
                            System.Threading.Thread.Sleep(700);
                            if (finished) return;
                            if (AnsweredElsewhere(transcript, ref seenLength))
                            {
                                elsewhere = true;
                                try { client.Close(); } catch (Exception) { }
                                return;
                            }
                        }
                    }) { IsBackground = true };
                    watcher.Start();
                }

                byte[] reply = new byte[4096];
                int filled = 0, n;
                try
                {
                    while (filled < reply.Length)
                    {
                        // The whole wait is capped, not each read, so the hook always ends before Claude Code gives up on it
                        int left = (ClaudeHook.WaitSeconds + 8) * 1000 - (int)total.ElapsedMilliseconds;
                        if (left <= 0) return "none";
                        stream.ReadTimeout = left;
                        n = stream.Read(reply, filled, reply.Length - filled);
                        if (n <= 0) break;
                        filled += n;
                    }
                }
                catch (Exception)
                {
                    if (filled == 0 || elsewhere) { finished = true; return "none"; }
                }
                finished = true;
                if (elsewhere) return "none";
                string text = Encoding.UTF8.GetString(reply, 0, filled);
                if (!text.StartsWith("HTTP/1.1 200", StringComparison.Ordinal)) return "none";
                if (text.Contains("\"decision\":\"allow\"", StringComparison.Ordinal)) return "allow";
                if (text.Contains("\"decision\":\"deny\"", StringComparison.Ordinal)) return "deny";
                return "none";
            }
        }
    }
}
