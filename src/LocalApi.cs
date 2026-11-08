using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WindowsIsland
{
    // A live activity pushed by a script or app. Each update replaces the object, so a reference never changes under you.
    sealed class ApiActivity
    {
        public string Id { get; init; }
        public string App { get; init; }       // Who sent it, never blank
        public string Title { get; init; }
        public string Text { get; init; }      // "" when none
        public int Progress { get; init; }     // 0-100, or -1 when none
        public string IconGlyph { get; init; } // A Segoe Fluent Icons character
        public string State { get; init; }     // running, done or error
        public DateTime Updated { get; init; } // UTC
    }

    sealed class ApiAlert
    {
        public string App { get; init; }
        public string Title { get; init; }
        public string Text { get; init; }
        public string IconGlyph { get; init; }
        public double Seconds { get; init; }   // 2-15
    }

    // A browser extension saying which tab is playing.
    sealed class ApiMediaHint
    {
        public string App { get; init; }
        public string Title { get; init; }
        public string Host { get; init; }      // null when none
        public ImageSource Favicon { get; init; } // Frozen, at most 128 x 128; null when none
    }

    // A small HTTP server on 127.0.0.1 only, so scripts and apps can show their own activities and alerts. Off by default.
    // Any web page and any local program can reach localhost, so every request needs the secret token from a file only
    // this Windows user can read, the Host header must name the loopback address (against DNS rebinding), web pages are
    // refused by their Origin header, and bodies must be small JSON. Text is only ever shown as plain text.
    sealed class LocalApi
    {
        public const int DefaultPort = 47613;
        public const int MinPort = 1024, MaxPort = 65535;
        const int MaxActivities = 8;
        const int MaxConnections = 16;
        const int MaxHeaderBytes = 8 * 1024;
        const int MaxHeaderCount = 32;
        const int MaxBodyBytes = 8 * 1024;
        const int MaxHintBodyBytes = 48 * 1024; // A favicon of up to 32 KB is about 43 KB as base64
        const int MaxFaviconBytes = 32 * 1024;
        const int MaxFaviconPixels = 128;
        const int TitleLength = 60, TextLength = 120, AppLength = 24, HostLength = 100;
        const int FailedAuthDelayMs = 250;
        static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
        static readonly TimeSpan ActivityTtl = TimeSpan.FromMinutes(10);
        static readonly TimeSpan FinishedLinger = TimeSpan.FromSeconds(6);
        static readonly Regex IdPattern = new Regex("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);
        static readonly Regex HostPattern = new Regex("^[a-z0-9.-]{1,253}(:[0-9]{1,5})?$", RegexOptions.CultureInvariant);
        static readonly Regex TokenPattern = new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

        // The only icons a sender can pick, by name. Anything else shows as "info".
        public const string InfoGlyph = "";
        static readonly Dictionary<string, string> Icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "info", InfoGlyph }, { "check", "" }, { "error", "" }, { "warning", "" },
            { "build", "" }, { "download", "" }, { "upload", "" }, { "sync", "" },
            { "mail", "" }, { "music", "" }, { "code", "" }, { "terminal", "" },
            { "bell", "" }, { "heart", "" }, { "star", "" }, { "clock", "" },
            { "calendar", "" }, { "lock", "" }, { "shield", "" }, { "wifi", "" },
            { "bluetooth", "" }, { "battery", "" }, { "camera", "" }, { "mic", "" },
            { "cloud", "" }, { "globe", "" }, { "chat", "" }, { "play", "" },
        };

        public static IReadOnlyCollection<string> IconNames { get { return Icons.Keys; } }

        public event Action<ApiActivity> ActivityUpdated;
        public event Action<string> ActivityEnded;
        public event Action<ApiAlert> AlertReceived;
        public event Action<ApiMediaHint> MediaHint;
        public event Action<ApiPermission> PermissionAsked;
        const int MaxPendingPermissions = 4;
        int pendingPermissions; // Only touched on the UI thread

        public bool Running { get; private set; }
        public bool LastStartFailed { get; private set; }
        public int Port { get; private set; }
        public int FailedAuthCount { get { return Volatile.Read(ref failedAuth); } }

        readonly Dispatcher dispatcher;
        readonly List<Entry> activities = new List<Entry>(); // Only touched on the UI thread
        readonly DispatcherTimer sweep;
        readonly object rateLock = new object();
        TcpListener listener;
        CancellationTokenSource stopping;
        SemaphoreSlim slots;
        volatile byte[] expected; // The token as ASCII bytes while running
        int generation, failedAuth;
        double requestBudget = 20, alertBudget = 5;
        long budgetTime = Stopwatch.GetTimestamp();

        sealed class Entry
        {
            public ApiActivity Activity;
            public DateTime Expires;
        }

        public LocalApi(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            sweep = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            sweep.Tick += (s, e) => Sweep();
        }

        public IReadOnlyList<ApiActivity> Activities
        {
            get { return activities.Select(a => a.Activity).ToList(); }
        }

        // ---------- Token ----------

        static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsIsland"); }
        }

        public static string TokenPath
        {
            get { return Path.Combine(Folder, "api-token.txt"); }
        }

        // The token from its file, or null when it was never created (or the file is not a valid token).
        public string Token { get { return ReadToken(); } }

        public static string ReadToken()
        {
            try
            {
                if (!File.Exists(TokenPath)) return null;
                string token = File.ReadAllText(TokenPath).Trim();
                return TokenPattern.IsMatch(token) ? token : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // A new token replaces the old one at once: callers using the old one are refused from now on.
        public string RegenerateToken()
        {
            string token = WriteNewToken();
            if (Running) expected = Encoding.ASCII.GetBytes(token);
            return token;
        }

        static string WriteNewToken()
        {
            byte[] raw = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToHexString(raw).ToLowerInvariant();
            CryptographicOperations.ZeroMemory(raw);
            Directory.CreateDirectory(Folder);
            // Written beside the file with its final permissions, then moved over it, so it is never readable by others.
            string temp = TokenPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream file = new FileInfo(temp).Create(FileMode.CreateNew, FileSystemRights.Write | FileSystemRights.ReadData,
                                                                   FileShare.None, 4096, FileOptions.None, OwnerOnly()))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(token);
                    file.Write(bytes, 0, bytes.Length);
                }
                File.Move(temp, TokenPath, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (Exception) { }
            }
            return token;
        }

        // Only the current Windows user, with nothing inherited from the folder.
        static FileSecurity OwnerOnly()
        {
            FileSecurity security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            using (WindowsIdentity me = WindowsIdentity.GetCurrent())
                security.AddAccessRule(new FileSystemAccessRule(me.User, FileSystemRights.FullControl, AccessControlType.Allow));
            return security;
        }

        // ---------- Start and stop ----------

        public void Start(int port)
        {
            port = Math.Clamp(port, MinPort, MaxPort);
            if (Running && port == Port) return;
            Stop();
            try
            {
                string token = ReadToken();
                if (token == null) token = WriteNewToken();
                else
                {
                    try { new FileInfo(TokenPath).SetAccessControl(OwnerOnly()); } // In case the file was replaced by hand
                    catch (Exception) { }
                }
                TcpListener server = new TcpListener(IPAddress.Loopback, port);
                server.ExclusiveAddressUse = true; // No other program can bind the same address and port while it runs
                server.Start(32);
                listener = server;
                expected = Encoding.ASCII.GetBytes(token);
                stopping = new CancellationTokenSource();
                slots = new SemaphoreSlim(MaxConnections, MaxConnections);
                Port = port;
                Running = true;
                LastStartFailed = false;
                generation++;
                CancellationToken stop = stopping.Token;
                SemaphoreSlim connectionSlots = slots;
                Task.Run(() => AcceptLoop(server, connectionSlots, stop));
            }
            catch (Exception)
            {
                // Usually the port is taken by another program.
                listener = null;
                expected = null;
                Running = false;
                LastStartFailed = true;
            }
        }

        public void Stop()
        {
            if (Running)
            {
                Running = false;
                generation++;
                expected = null;
                try { stopping?.Cancel(); }
                catch (Exception) { }
                try { listener?.Stop(); }
                catch (Exception) { }
                listener = null;
                stopping = null;
            }
            EndAll();
        }

        void EndAll()
        {
            if (activities.Count == 0) return;
            List<Entry> ended = new List<Entry>(activities);
            activities.Clear();
            sweep.Stop();
            foreach (Entry entry in ended) ActivityEnded?.Invoke(entry.Activity.Id);
        }

        void Sweep()
        {
            DateTime now = DateTime.UtcNow;
            List<Entry> expired = activities.FindAll(a => a.Expires <= now);
            foreach (Entry entry in expired)
            {
                activities.Remove(entry);
                ActivityEnded?.Invoke(entry.Activity.Id);
            }
            if (activities.Count == 0) sweep.Stop();
        }

        // ---------- Connections ----------

        async Task AcceptLoop(TcpListener server, SemaphoreSlim connectionSlots, CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await server.AcceptTcpClientAsync(stop);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException)
                {
                    if (stop.IsCancellationRequested) break;
                    continue;
                }
                if (!connectionSlots.Wait(0))
                {
                    // Too many at once: drop it without reading anything.
                    try { client.Client.LingerState = new LingerOption(true, 0); }
                    catch (Exception) { }
                    client.Dispose();
                    continue;
                }
                _ = Task.Run(() => Serve(client, connectionSlots, stop));
            }
        }

        async Task Serve(TcpClient client, SemaphoreSlim connectionSlots, CancellationToken stop)
        {
            try
            {
                using (client)
                using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(stop))
                {
                    deadline.CancelAfter(RequestTimeout);
                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();
                    Reply reply;
                    try
                    {
                        reply = await Handle(client, stream, deadline.Token, deadline);
                    }
                    catch (OperationCanceledException)
                    {
                        reply = stop.IsCancellationRequested ? null : Reply.Error(408, "timeout");
                    }
                    catch (Exception)
                    {
                        reply = Reply.Error(400, "bad_request");
                    }
                    if (reply == null) return;
                    try
                    {
                        byte[] bytes = reply.ToBytes();
                        using (CancellationTokenSource write = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                            await stream.WriteAsync(bytes, 0, bytes.Length, write.Token);
                        // Read what the client may still be sending, so closing does not reset the connection
                        // before it reads the answer.
                        client.Client.Shutdown(SocketShutdown.Send);
                        using (CancellationTokenSource drain = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
                        {
                            byte[] sink = new byte[4096];
                            int total = 0;
                            while (total < 64 * 1024)
                            {
                                int n = await stream.ReadAsync(sink, 0, sink.Length, drain.Token);
                                if (n <= 0) break;
                                total += n;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                connectionSlots.Release();
            }
        }

        sealed class Request
        {
            public string Method, Path;
            public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        async Task<Reply> Handle(TcpClient client, NetworkStream stream, CancellationToken token, CancellationTokenSource deadlineSource)
        {
            IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
            if (remote == null || !IPAddress.IsLoopback(remote.Address)) return null;

            // Headers, up to a blank line, within a fixed size.
            byte[] buffer = new byte[MaxHeaderBytes];
            int filled = 0, headerEnd = -1;
            while (headerEnd < 0)
            {
                if (filled == buffer.Length) return Reply.Error(431, "headers_too_large");
                int n = await stream.ReadAsync(buffer, filled, buffer.Length - filled, token);
                if (n <= 0) return null;
                int from = Math.Max(0, filled - 3);
                filled += n;
                headerEnd = IndexOfBlankLine(buffer, from, filled);
            }

            Request request = Parse(buffer, headerEnd);
            if (request == null) return Reply.Error(400, "bad_request");

            // Against DNS rebinding: a web page on another name that resolves to 127.0.0.1 still sends its own Host.
            string host;
            if (!request.Headers.TryGetValue("Host", out host) ||
                !(host.Equals("127.0.0.1:" + Port, StringComparison.OrdinalIgnoreCase) || host.Equals("localhost:" + Port, StringComparison.OrdinalIgnoreCase)))
                return Reply.Error(400, "bad_request");
            // Web pages always send their Origin; only browser extensions are allowed to call.
            string origin;
            if (request.Headers.TryGetValue("Origin", out origin) &&
                !(origin.StartsWith("chrome-extension://", StringComparison.Ordinal) || origin.StartsWith("moz-extension://", StringComparison.Ordinal)))
                return Reply.Error(403, "forbidden");
            if (request.Method != "GET" && request.Method != "POST") return Reply.Error(405, "method_not_allowed");
            // After the checks above, so requests from web pages cannot use up the budget of real callers.
            if (!TakeBudget(false)) return Reply.Error(429, "too_many_requests");

            if (!Authorized(request))
            {
                Interlocked.Increment(ref failedAuth);
                await Task.Delay(FailedAuthDelayMs, token);
                return Reply.Error(401, "unauthorized");
            }

            string route = request.Path;
            bool known = route == "/v1/ping" || route == "/v1/activity" || route == "/v1/activity/end" || route == "/v1/alert" || route == "/v1/media-hint" || route == "/v1/permission";
            if (!known) return Reply.Error(404, "not_found");
            if (route == "/v1/ping")
            {
                if (request.Method != "GET") return Reply.Error(405, "method_not_allowed");
                if (request.Headers.ContainsKey("Transfer-Encoding") || (request.Headers.TryGetValue("Content-Length", out string pingLength) && pingLength != "0"))
                    return Reply.Error(400, "bad_request");
                return new Reply(200, "{\"ok\":true,\"app\":\"WindowsIsland\",\"version\":\"" + Installer.Version + "\"}");
            }
            if (request.Method != "POST") return Reply.Error(405, "method_not_allowed");

            // Body: JSON only (a plain web form cannot send that), with a known length, small.
            if (request.Headers.ContainsKey("Transfer-Encoding")) return Reply.Error(501, "not_implemented");
            string type;
            if (!request.Headers.TryGetValue("Content-Type", out type) || !IsJson(type)) return Reply.Error(415, "unsupported_media_type");
            string lengthText;
            if (!request.Headers.TryGetValue("Content-Length", out lengthText)) return Reply.Error(411, "length_required");
            int length;
            if (lengthText.Length == 0 || lengthText.Length > 7 || !lengthText.All(c => c >= '0' && c <= '9') || !int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out length))
                return Reply.Error(400, "bad_request");
            int limit = route == "/v1/media-hint" ? MaxHintBodyBytes : MaxBodyBytes;
            if (length > limit) return Reply.Error(413, "too_large");

            int already = filled - (headerEnd + 4);
            if (already > length) return Reply.Error(400, "bad_request"); // One request per connection
            string expect;
            if (request.Headers.TryGetValue("Expect", out expect))
            {
                if (!expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase)) return Reply.Error(417, "expectation_failed");
                if (already < length)
                {
                    byte[] proceed = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                    await stream.WriteAsync(proceed, 0, proceed.Length, token);
                }
            }
            byte[] body = new byte[length];
            Buffer.BlockCopy(buffer, headerEnd + 4, body, 0, already);
            int got = already;
            while (got < length)
            {
                int n = await stream.ReadAsync(body, got, length - got, token);
                if (n <= 0) return null;
                got += n;
            }

            Dictionary<string, JsonElement> fields;
            using (JsonDocument document = ParseJson(body))
            {
                if (document == null || (fields = Fields(document.RootElement)) == null) return Reply.Error(400, "invalid_json");
                if (route == "/v1/alert" && !TakeBudget(true)) return Reply.Error(429, "too_many_requests");
                string senderPath = SenderPath(remote, Port);
                int ticket = generation;
                if (route == "/v1/permission") return await PermissionCall(fields, stream, token, deadlineSource, ticket);
                Func<Reply> work;
                if (route == "/v1/activity") work = ActivityCall(fields, senderPath);
                else if (route == "/v1/activity/end") work = EndCall(fields);
                else if (route == "/v1/alert") work = AlertCall(fields, senderPath);
                else work = HintCall(fields, senderPath);
                if (work == null) return Reply.Error(400, "invalid_field");
                // State lives on the UI thread, so events fire there and the island can use them directly.
                return await dispatcher.InvokeAsync(() => ticket == generation && Running ? work() : Reply.Error(503, "unavailable"),
                                                    DispatcherPriority.Normal, token).Task;
            }
        }

        // Claude Code's permission hook: the island shows Allow and Deny, and the answer goes back on this same
        // connection. A click, the time running out, or the caller hanging up (Claude's own prompt was answered first)
        // ends the wait. "none" means no decision, so Claude Code shows its own prompt.
        async Task<Reply> PermissionCall(Dictionary<string, JsonElement> fields, NetworkStream stream, CancellationToken token, CancellationTokenSource deadlineSource, int ticket)
        {
            bool bad = false;
            string session = Text(fields, "session", 64, ref bad);
            string tool = Text(fields, "tool", 100, ref bad);
            string summary = Text(fields, "summary", 300, ref bad);
            double? wait = Number(fields, "wait", ref bad);
            if (bad || string.IsNullOrEmpty(tool)) return Reply.Error(400, "invalid_field");
            int seconds = (int)Math.Clamp(wait ?? 60, 5, 110);

            ApiPermission permission = new ApiPermission { Session = session, Tool = tool, Summary = summary ?? "" };
            bool shown = await dispatcher.InvokeAsync(() =>
            {
                if (ticket != generation || !Running || PermissionAsked == null || pendingPermissions >= MaxPendingPermissions) return false;
                pendingPermissions++;
                PermissionAsked(permission);
                return true;
            }, DispatcherPriority.Normal, token).Task;
            if (!shown) return new Reply(200, "{\"decision\":\"none\"}");

            string decision = "none";
            try
            {
                deadlineSource.CancelAfter(TimeSpan.FromSeconds(seconds + 5)); // This route is the one that waits
                Task<int> hangup = stream.ReadAsync(new byte[1], 0, 1, token);
                Task finished = await Task.WhenAny(permission.Answer, Task.Delay(TimeSpan.FromSeconds(seconds), token), hangup);
                if (finished == permission.Answer) decision = permission.Answer.Result;
            }
            catch (Exception)
            {
                // Cancelled or the connection broke: no decision
            }
            finally
            {
                try { await dispatcher.InvokeAsync(() => { pendingPermissions--; permission.Cancel(); }); }
                catch (Exception) { }
            }
            return new Reply(200, "{\"decision\":\"" + decision + "\"}");
        }

        bool Authorized(Request request)
        {
            byte[] secret = expected;
            string header;
            if (secret == null || !request.Headers.TryGetValue("Authorization", out header)) return false;
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            byte[] given = Encoding.ASCII.GetBytes(header.Substring(7).Trim());
            // FixedTimeEquals only leaks the length, which is public (64 hex characters).
            return CryptographicOperations.FixedTimeEquals(given, secret);
        }

        // About 20 requests a second in all, and a few alerts a second, so a loop gone wrong cannot flood the island.
        bool TakeBudget(bool alert)
        {
            lock (rateLock)
            {
                long now = Stopwatch.GetTimestamp();
                double seconds = (now - budgetTime) / (double)Stopwatch.Frequency;
                budgetTime = now;
                requestBudget = Math.Min(20, requestBudget + seconds * 20);
                alertBudget = Math.Min(5, alertBudget + seconds * 1);
                if (alert)
                {
                    if (alertBudget < 1) return false;
                    alertBudget -= 1;
                    return true;
                }
                if (requestBudget < 1) return false;
                requestBudget -= 1;
                return true;
            }
        }

        static int IndexOfBlankLine(byte[] data, int from, int end)
        {
            for (int i = from; i + 3 < end; i++)
                if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n') return i;
            return -1;
        }

        // A strict reading of the request line and headers: plain ASCII, CRLF line ends, no repeated headers.
        static Request Parse(byte[] data, int end)
        {
            for (int i = 0; i < end; i++)
            {
                byte b = data[i];
                if (b == '\r')
                {
                    if (i + 1 >= end || data[i + 1] != '\n') return null;
                }
                else if (b == '\n')
                {
                    if (i == 0 || data[i - 1] != '\r') return null;
                }
                else if (b != '\t' && (b < 0x20 || b > 0x7E)) return null;
            }
            string[] lines = Encoding.ASCII.GetString(data, 0, end).Split("\r\n");
            if (lines.Length < 2 || lines.Length > MaxHeaderCount + 1) return null;

            string[] first = lines[0].Split(' ');
            if (first.Length != 3) return null;
            if (first[0].Length == 0 || first[0].Length > 10 || !first[0].All(c => c >= 'A' && c <= 'Z')) return null;
            if (first[1].Length == 0 || first[1].Length > 64 || first[1][0] != '/') return null;
            if (first[2] != "HTTP/1.1" && first[2] != "HTTP/1.0") return null;
            Request request = new Request { Method = first[0], Path = first[1] };

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon <= 0) return null;
                string name = line.Substring(0, colon);
                if (!name.All(IsTokenChar)) return null;
                string value = line.Substring(colon + 1).Trim(' ', '\t');
                if (request.Headers.ContainsKey(name)) return null;
                request.Headers[name] = value;
            }
            return request;
        }

        static bool IsTokenChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || "!#$%&'*+-.^_`|~".IndexOf(c) >= 0;
        }

        // application/json, optionally with charset=utf-8.
        static bool IsJson(string type)
        {
            string[] parts = type.Split(';');
            if (!parts[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 1; i < parts.Length; i++)
            {
                string parameter = parts[i].Trim().Replace(" ", "");
                if (!parameter.Equals("charset=utf-8", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        static JsonDocument ParseJson(byte[] body)
        {
            try
            {
                return JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 4, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The top-level object's fields; null when it is not an object or a name repeats.
        static Dictionary<string, JsonElement> Fields(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return null;
            Dictionary<string, JsonElement> fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (fields.Count >= 32 || fields.ContainsKey(property.Name)) return null;
                fields[property.Name] = property.Value.Clone();
            }
            return fields;
        }

        // ---------- Calls ----------
        // Each returns null when a field has the wrong type or value; the returned work runs on the UI thread.

        Func<Reply> ActivityCall(Dictionary<string, JsonElement> fields, string senderPath)
        {
            string id = Id(fields);
            bool bad = id == null;
            string title = Text(fields, "title", TitleLength, ref bad);
            string text = Text(fields, "text", TextLength, ref bad);
            string icon = Text(fields, "icon", 32, ref bad);
            string claimed = Text(fields, "app", AppLength, ref bad);
            string state = Text(fields, "state", 16, ref bad);
            double? progress = Number(fields, "progress", ref bad);
            if (state != null) state = state.ToLowerInvariant();
            if (state != null && state != "running" && state != "done" && state != "error") bad = true;
            if (bad) return null;
            if (title == "") title = null;

            return () =>
            {
                Entry entry = activities.Find(a => a.Activity.Id == id);
                if (entry == null)
                {
                    if (title == null) return Reply.Error(400, "title_required");
                    if (activities.Count >= MaxActivities) return Reply.Error(409, "too_many_activities");
                }
                ApiActivity before = entry?.Activity;
                string finalState = state ?? "running";
                ApiActivity activity = new ApiActivity
                {
                    Id = id,
                    App = SenderName(senderPath, claimed ?? before?.App),
                    Title = title ?? before.Title,
                    Text = text ?? before?.Text ?? "",
                    Progress = progress.HasValue ? (int)Math.Round(Math.Clamp(progress.Value, 0, 100)) : before?.Progress ?? -1,
                    IconGlyph = icon != null ? Glyph(icon) : before?.IconGlyph ?? InfoGlyph,
                    State = finalState,
                    Updated = DateTime.UtcNow,
                };
                if (entry == null)
                {
                    entry = new Entry();
                    activities.Add(entry);
                }
                entry.Activity = activity;
                entry.Expires = DateTime.UtcNow + (finalState == "running" ? ActivityTtl : FinishedLinger);
                if (!sweep.IsEnabled) sweep.Start();
                ActivityUpdated?.Invoke(activity);
                return new Reply(200, "{\"ok\":true}");
            };
        }

        Func<Reply> EndCall(Dictionary<string, JsonElement> fields)
        {
            string id = Id(fields);
            if (id == null) return null;
            return () =>
            {
                Entry entry = activities.Find(a => a.Activity.Id == id);
                if (entry != null)
                {
                    activities.Remove(entry);
                    if (activities.Count == 0) sweep.Stop();
                    ActivityEnded?.Invoke(id);
                }
                return new Reply(200, "{\"ok\":true}"); // Ending one that is gone is not an error
            };
        }

        Func<Reply> AlertCall(Dictionary<string, JsonElement> fields, string senderPath)
        {
            bool bad = false;
            string title = Text(fields, "title", TitleLength, ref bad);
            string text = Text(fields, "text", TextLength, ref bad);
            string icon = Text(fields, "icon", 32, ref bad);
            string claimed = Text(fields, "app", AppLength, ref bad);
            double? seconds = Number(fields, "seconds", ref bad);
            if (bad) return null;
            return () =>
            {
                if (string.IsNullOrEmpty(title)) return Reply.Error(400, "title_required");
                AlertReceived?.Invoke(new ApiAlert
                {
                    App = SenderName(senderPath, claimed),
                    Title = title,
                    Text = text ?? "",
                    IconGlyph = Glyph(icon),
                    Seconds = Math.Clamp(seconds ?? 5, 2, 15),
                });
                return new Reply(200, "{\"ok\":true}");
            };
        }

        Func<Reply> HintCall(Dictionary<string, JsonElement> fields, string senderPath)
        {
            bool bad = false;
            string title = Text(fields, "title", TitleLength, ref bad);
            string host = Text(fields, "host", HostLength, ref bad);
            string claimed = Text(fields, "app", AppLength, ref bad);
            string favicon = Raw(fields, "favicon", ref bad);
            if (host == "") host = null;
            if (host != null)
            {
                host = host.ToLowerInvariant();
                if (!HostPattern.IsMatch(host)) bad = true;
            }
            byte[] image = null;
            if (!string.IsNullOrEmpty(favicon))
            {
                image = FromBase64(favicon);
                if (image == null) bad = true;
            }
            if (bad) return null;
            return () =>
            {
                if (string.IsNullOrEmpty(title)) return Reply.Error(400, "title_required");
                ImageSource icon = null;
                if (image != null)
                {
                    icon = DecodeFavicon(image); // On the UI thread: WPF images belong to a dispatcher until frozen
                    if (icon == null) return Reply.Error(400, "invalid_favicon");
                }
                MediaHint?.Invoke(new ApiMediaHint { App = SenderName(senderPath, claimed), Title = title, Host = host, Favicon = icon });
                return new Reply(200, "{\"ok\":true}");
            };
        }

        static string Id(Dictionary<string, JsonElement> fields)
        {
            JsonElement value;
            if (!fields.TryGetValue("id", out value) || value.ValueKind != JsonValueKind.String) return null;
            string id = value.GetString();
            return id != null && IdPattern.IsMatch(id) ? id : null;
        }

        // A cleaned string field; null when absent or null. A value of another type marks the call as bad.
        static string Text(Dictionary<string, JsonElement> fields, string name, int max, ref bool bad)
        {
            string raw = Raw(fields, name, ref bad);
            return raw == null ? null : Clean(raw, max);
        }

        static string Raw(Dictionary<string, JsonElement> fields, string name, ref bool bad)
        {
            JsonElement value;
            if (!fields.TryGetValue(name, out value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.String) { bad = true; return null; }
            return value.GetString();
        }

        static double? Number(Dictionary<string, JsonElement> fields, string name, ref bool bad)
        {
            JsonElement value;
            if (!fields.TryGetValue(name, out value) || value.ValueKind == JsonValueKind.Null) return null;
            double number;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out number) || double.IsNaN(number) || double.IsInfinity(number))
            {
                bad = true;
                return null;
            }
            return number;
        }

        public static string Glyph(string name)
        {
            string glyph;
            return name != null && Icons.TryGetValue(name.Trim(), out glyph) ? glyph : InfoGlyph;
        }

        // Plain, single-line text: no control, formatting or direction-changing characters, no private-use
        // characters (which could pass for icons), runs of spaces collapsed, at most max characters as people count them.
        public static string Clean(string value, int max)
        {
            if (value == null) return null;
            StringBuilder kept = new StringBuilder(Math.Min(value.Length, max * 4));
            bool space = false;
            for (int i = 0; i < value.Length && kept.Length < max * 8; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    space = kept.Length > 0;
                    continue;
                }
                UnicodeCategory category;
                int width = 1;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) continue; // A broken pair
                    category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertToUtf32(c, value[i + 1]));
                    width = 2;
                }
                else if (char.IsLowSurrogate(c)) continue;
                else category = CharUnicodeInfo.GetUnicodeCategory(c);

                bool drop = category == UnicodeCategory.Control || category == UnicodeCategory.LineSeparator ||
                            category == UnicodeCategory.ParagraphSeparator || category == UnicodeCategory.PrivateUse ||
                            category == UnicodeCategory.Surrogate ||
                            (category == UnicodeCategory.Format && c != '‍'); // Keeps the joiner inside emoji sequences
                if (!drop)
                {
                    if (space) kept.Append(' ');
                    space = false;
                    kept.Append(value, i, width);
                }
                i += width - 1;
            }

            string text = kept.ToString().Trim();
            StringBuilder result = new StringBuilder(text.Length);
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
            int count = 0;
            while (count < max && elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (element.Length > 16) continue; // Piles of combining marks
                if (element.Length == 1 && element[0] == '‍') continue; // A joiner on its own
                result.Append(element);
                count++;
            }
            return result.ToString().Trim();
        }

        static byte[] FromBase64(string value)
        {
            int comma = value.IndexOf(',');
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                if (value.Substring(0, comma).IndexOf(";base64", StringComparison.OrdinalIgnoreCase) < 0) return null;
                value = value.Substring(comma + 1);
            }
            if (value.Length > (MaxFaviconBytes + 2) / 3 * 4) return null;
            byte[] buffer = new byte[MaxFaviconBytes + 3];
            int written;
            if (!Convert.TryFromBase64String(value, buffer, out written) || written == 0 || written > MaxFaviconBytes) return null;
            byte[] data = new byte[written];
            Buffer.BlockCopy(buffer, 0, data, 0, written);
            return data;
        }

        // PNG, JPEG or ICO only, at most 128 x 128 pixels, checked before the pixels are decoded.
        static ImageSource DecodeFavicon(byte[] data)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(data, false))
                {
                    const BitmapCreateOptions options = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
                    BitmapDecoder decoder;
                    bool png = data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
                    bool jpeg = data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
                    bool ico = data.Length > 6 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0 && (data[4] | data[5]) != 0;
                    if (png) decoder = new PngBitmapDecoder(stream, options, BitmapCacheOption.None);
                    else if (jpeg) decoder = new JpegBitmapDecoder(stream, options, BitmapCacheOption.None);
                    else if (ico) decoder = new IconBitmapDecoder(stream, options, BitmapCacheOption.None);
                    else return null;

                    BitmapFrame chosen = null;
                    foreach (BitmapFrame frame in decoder.Frames)
                    {
                        if (frame.PixelWidth < 1 || frame.PixelHeight < 1 || frame.PixelWidth > MaxFaviconPixels || frame.PixelHeight > MaxFaviconPixels) continue;
                        if (chosen == null || frame.PixelWidth > chosen.PixelWidth) chosen = frame;
                        if (!ico) break;
                    }
                    if (chosen == null) return null;
                    WriteableBitmap bitmap = new WriteableBitmap(new FormatConvertedBitmap(chosen, PixelFormats.Pbgra32, null, 0));
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------- Who is calling ----------

        // The name of the program on the other end, from the owner of its loopback connection. Runs on the UI thread.
        static string SenderName(string path, string claimed)
        {
            string name = null;
            if (path != null)
            {
                try { name = Clean(AppNames.FromPath(path), AppLength); }
                catch (Exception) { }
            }
            if (string.IsNullOrEmpty(name)) name = claimed;
            return string.IsNullOrEmpty(name) ? "Local app" : name;
        }

        // The program that owns the client's end of the connection. The island's own command line tool sends for
        // whatever started it (often through island.cmd), so that program is named instead.
        static string SenderPath(IPEndPoint remote, int serverPort)
        {
            try
            {
                uint address = BitConverter.ToUInt32(remote.Address.MapToIPv4().GetAddressBytes(), 0);
                int pid = OwnerOf(address, remote.Port, serverPort);
                if (pid <= 0) return null;
                string path = ImagePath(pid);
                if (path == null || !IsNamed(path, "WindowsIsland.exe")) return path;
                int child = pid;
                for (int hop = 0; hop < 4; hop++)
                {
                    int parent = ParentOf(child);
                    if (parent <= 0) return null;
                    long childStart = StartTime(child), parentStart = StartTime(parent);
                    if (childStart == 0 || parentStart == 0 || parentStart > childStart) return null; // The parent is gone and its id reused
                    string parentPath = ImagePath(parent);
                    if (parentPath == null) return null;
                    if (!IsNamed(parentPath, "WindowsIsland.exe") && !IsNamed(parentPath, "cmd.exe")) return parentPath;
                    child = parent;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool IsNamed(string path, string file)
        {
            return string.Equals(Path.GetFileName(path), file, StringComparison.OrdinalIgnoreCase);
        }

        // ---------- Windows ----------

        const int AF_INET = 2, TCP_TABLE_OWNER_PID_ALL = 5, ERROR_INSUFFICIENT_BUFFER = 122;
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, TH32CS_SNAPPROCESS = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCPROW_OWNER_PID
        {
            public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort, OwningPid;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32W
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

        static int NetworkPort(uint value)
        {
            return (int)(((value & 0xFF) << 8) | ((value >> 8) & 0xFF));
        }

        // The process owning the IPv4 connection whose local end is address:localPort and remote port is remotePort.
        internal static int OwnerOf(uint address, int localPort, int remotePort)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                size += 4096;
                IntPtr table = Marshal.AllocHGlobal(size);
                try
                {
                    uint result = GetExtendedTcpTable(table, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                    if (result == ERROR_INSUFFICIENT_BUFFER) continue;
                    if (result != 0) return -1;
                    int count = Marshal.ReadInt32(table);
                    int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                    for (int i = 0; i < count; i++)
                    {
                        MIB_TCPROW_OWNER_PID row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(table + 4 + i * rowSize);
                        if (row.LocalAddr == address && NetworkPort(row.LocalPort) == localPort && NetworkPort(row.RemotePort) == remotePort)
                            return (int)row.OwningPid;
                    }
                    return -1;
                }
                finally
                {
                    Marshal.FreeHGlobal(table);
                }
            }
            return -1;
        }

        internal static string ImagePath(int pid)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero) return null;
            try
            {
                StringBuilder name = new StringBuilder(1024);
                int size = name.Capacity;
                return QueryFullProcessImageName(process, 0, name, ref size) ? name.ToString(0, size) : null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        static long StartTime(int pid)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero) return 0;
            try
            {
                long creation, exit, kernel, user;
                return GetProcessTimes(process, out creation, out exit, out kernel, out user) ? creation : 0;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        static int ParentOf(int pid)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return -1;
            try
            {
                PROCESSENTRY32W entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
                if (!Process32FirstW(snapshot, ref entry)) return -1;
                do
                {
                    if (entry.th32ProcessID == pid) return (int)entry.th32ParentProcessID;
                } while (Process32NextW(snapshot, ref entry));
                return -1;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        // ---------- Replies ----------

        sealed class Reply
        {
            public readonly int Status;
            public readonly string Json;

            public Reply(int status, string json)
            {
                Status = status;
                Json = json;
            }

            // Fixed, generic bodies: nothing about the request, the token or the app's internals.
            public static Reply Error(int status, string code)
            {
                return new Reply(status, "{\"ok\":false,\"error\":\"" + code + "\"}");
            }

            public byte[] ToBytes()
            {
                byte[] body = Encoding.UTF8.GetBytes(Json);
                string head = "HTTP/1.1 " + Status + " " + Reason(Status) + "\r\n" +
                              "Content-Type: application/json; charset=utf-8\r\n" +
                              "Content-Length: " + body.Length + "\r\n" +
                              "Cache-Control: no-store\r\n" +
                              "X-Content-Type-Options: nosniff\r\n" +
                              "Connection: close\r\n\r\n";
                byte[] header = Encoding.ASCII.GetBytes(head);
                byte[] all = new byte[header.Length + body.Length];
                Buffer.BlockCopy(header, 0, all, 0, header.Length);
                Buffer.BlockCopy(body, 0, all, header.Length, body.Length);
                return all;
            }

            static string Reason(int status)
            {
                switch (status)
                {
                    case 200: return "OK";
                    case 400: return "Bad Request";
                    case 401: return "Unauthorized";
                    case 403: return "Forbidden";
                    case 404: return "Not Found";
                    case 405: return "Method Not Allowed";
                    case 408: return "Request Timeout";
                    case 409: return "Conflict";
                    case 411: return "Length Required";
                    case 413: return "Content Too Large";
                    case 415: return "Unsupported Media Type";
                    case 417: return "Expectation Failed";
                    case 429: return "Too Many Requests";
                    case 431: return "Request Header Fields Too Large";
                    case 501: return "Not Implemented";
                    default: return "Service Unavailable";
                }
            }
        }
    }
}
