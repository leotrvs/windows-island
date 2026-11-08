using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Knows whether the user is muted or deafened in Discord, by talking to the Discord app on this computer through its
    // local RPC pipe. It needs a Discord application of the user's own (client id and secret from the developer portal),
    // and the user approves it once inside Discord. See docs/DISCORD.md. Nothing is sent anywhere except the Discord
    // token endpoint (https://discord.com/api/oauth2/token), once per authorization and again to renew it.
    sealed class DiscordService
    {
        public enum State { Off, NeedsSetup, WaitingForDiscord, Approve, Connected, Refused, Failed }

        public State Status { get; private set; } = State.Off;
        public string Detail { get; private set; } = ""; // What Discord said, when it refused (shortened)
        public bool Muted { get; private set; }
        public bool Deafened { get; private set; }
        public event Action Changed;

        const string RedirectUri = "http://127.0.0.1";
        const int MaxDetail = 120;
        static readonly string[] Scopes = { "rpc", "rpc.voice.read" };
        static readonly HttpClient web = CreateClient();

        static HttpClient CreateClient()
        {
            // No redirects (a redirect would send the secret on), a small answer, and a name
            HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 16 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsIsland (https://github.com/leotrvs/windows-island)");
            return client;
        }

        [DllImport("kernel32.dll")] static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint processId);
        [DllImport("kernel32.dll")] static extern bool GetNamedPipeServerSessionId(IntPtr pipe, out uint sessionId);
        [DllImport("kernel32.dll")] static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

        readonly Dispatcher dispatcher;
        readonly object gate = new object();
        CancellationTokenSource stopping;
        string runningFor;

        public DiscordService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public void Start(string clientId)
        {
            clientId = (clientId ?? "").Trim();
            CancellationToken token;
            lock (gate)
            {
                if (stopping != null && runningFor == clientId) return;
            }
            Stop();
            if (clientId.Length == 0) { Set(CancellationToken.None, State.NeedsSetup, "", false, false); return; }
            lock (gate)
            {
                stopping = new CancellationTokenSource();
                runningFor = clientId;
                token = stopping.Token;
            }
            _ = Task.Run(() => Loop(clientId, token));
        }

        // After the client secret changed: connect again with it.
        public void Restart(string clientId)
        {
            Stop();
            Start(clientId);
        }

        public void Stop()
        {
            lock (gate)
            {
                if (stopping != null) { stopping.Cancel(); stopping.Dispose(); stopping = null; }
                runningFor = null;
            }
            Set(CancellationToken.None, State.Off, "", false, false);
        }

        // A run that was cancelled (stopped, or replaced by a restart) can no longer change what is shown.
        void Set(CancellationToken run, State status, string detail, bool muted, bool deafened)
        {
            if (detail != null && detail.Length > MaxDetail) detail = detail.Substring(0, MaxDetail) + "…";
            lock (gate)
            {
                if (run.IsCancellationRequested) return;
                if (Status == status && Detail == detail && Muted == muted && Deafened == deafened) return;
                Status = status;
                Detail = detail ?? "";
                Muted = muted;
                Deafened = deafened;
            }
            dispatcher.BeginInvoke(new Action(() => Changed?.Invoke()));
        }

        async Task Loop(string clientId, CancellationToken stop)
        {
            int wait = 6; // Seconds; doubles after a failure up to five minutes, and starts over once connected
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await Session(clientId, stop);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    if (Status != State.Refused && Status != State.NeedsSetup) Set(stop, State.Failed, "", false, false);
                }

                // Refused or not set up: nothing to try again until the id or the secret changes (that restarts this loop),
                // so Discord is not asked to show its approval window over and over
                if (Status == State.Refused || Status == State.NeedsSetup)
                {
                    try { await Task.Delay(Timeout.Infinite, stop); }
                    catch (OperationCanceledException) { return; }
                }

                if (Status == State.Connected) wait = 6;
                else wait = Math.Min(wait * 2, 300);
                if (Status != State.Failed) Set(stop, State.WaitingForDiscord, "", false, false);
                try { await Task.Delay(TimeSpan.FromSeconds(wait), stop); }
                catch (OperationCanceledException) { return; }
            }
        }

        // ---------- One connection ----------

        sealed class Link : IDisposable
        {
            readonly NamedPipeClientStream pipe;
            byte[] pending = new byte[0];
            public string ClosedWith; // What Discord said when it ended the connection

            public Link(NamedPipeClientStream pipe) { this.pipe = pipe; }

            public async Task Send(int opcode, string json, CancellationToken ct)
            {
                byte[] frame = DiscordRpc.Encode(opcode, json);
                await pipe.WriteAsync(frame, 0, frame.Length, ct);
                await pipe.FlushAsync(ct);
            }

            public async Task<KeyValuePair<int, string>> Receive(CancellationToken ct)
            {
                byte[] chunk = new byte[4096];
                while (true)
                {
                    int opcode, consumed;
                    string json;
                    if (DiscordRpc.TryDecode(pending, pending.Length, out opcode, out json, out consumed))
                    {
                        byte[] rest = new byte[pending.Length - consumed];
                        Buffer.BlockCopy(pending, consumed, rest, 0, rest.Length);
                        pending = rest;
                        return new KeyValuePair<int, string>(opcode, json);
                    }
                    if (consumed < 0) throw new IOException("Bad frame");
                    int n = await pipe.ReadAsync(chunk, 0, chunk.Length, ct);
                    if (n <= 0) throw new IOException("Closed");
                    byte[] joined = new byte[pending.Length + n];
                    Buffer.BlockCopy(pending, 0, joined, 0, pending.Length);
                    Buffer.BlockCopy(chunk, 0, joined, pending.Length, n);
                    pending = joined;
                }
            }

            public void Dispose() { pipe.Dispose(); }
        }

        // Only a pipe served by the Discord app of this user gets the token: another program could create a pipe of the same
        // name. The server must run in this Windows session and be an executable under this user's Discord folders
        // (Discord, DiscordPTB or DiscordCanary in %LOCALAPPDATA%), not just something called Discord.
        static bool ServedByDiscord(NamedPipeClientStream pipe)
        {
            try
            {
                IntPtr handle = pipe.SafePipeHandle.DangerousGetHandle();
                uint pid, serverSession, mySession;
                if (!GetNamedPipeServerProcessId(handle, out pid) || !GetNamedPipeServerSessionId(handle, out serverSession)) return false;
                if (!ProcessIdToSessionId((uint)Process.GetCurrentProcess().Id, out mySession) || serverSession != mySession) return false;
                using (Process server = Process.GetProcessById((int)pid))
                {
                    string path = server.MainModule.FileName;
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    foreach (string folder in new[] { "Discord", "DiscordPTB", "DiscordCanary" })
                    {
                        string root = Path.GetFullPath(Path.Combine(local, folder)) + Path.DirectorySeparatorChar;
                        if (Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        static async Task<Link> Connect(CancellationToken ct)
        {
            for (int n = 0; n < 10; n++)
            {
                // Identification only: a program that created the pipe cannot act as this user through it
                NamedPipeClientStream pipe = new NamedPipeClientStream(".", "discord-ipc-" + n, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                try
                {
                    await pipe.ConnectAsync(250, ct);
                    if (ServedByDiscord(pipe)) return new Link(pipe);
                }
                catch (OperationCanceledException) { pipe.Dispose(); throw; }
                catch (Exception) { }
                pipe.Dispose();
            }
            return null;
        }

        async Task Session(string clientId, CancellationToken ct)
        {
            using (Link link = await Connect(ct))
            {
                if (link == null) { Set(ct, State.WaitingForDiscord, "", false, false); return; }
                try
                {
                    await link.Send(DiscordRpc.OpHandshake, DiscordRpc.HandshakeJson(clientId), ct);
                    DiscordRpc.Message ready = await WaitFor(link, m => m.Evt == "READY" || m.IsError, 8000, ct); // Discord answers with its READY event
                    if (ready == null || ready.IsError)
                    {
                        Set(ct, State.Refused, ready == null ? "" : (ready.Text ?? ""), false, false);
                        return;
                    }

                    DiscordSecrets secrets = DiscordSecrets.Load();
                    if (string.IsNullOrEmpty(secrets.ClientSecret)) { Set(ct, State.NeedsSetup, "", false, false); return; }
                    if (secrets.ClientId != clientId) { secrets.AccessToken = secrets.RefreshToken = ""; } // Tokens of another application

                    if (!await Authenticate(link, clientId, secrets, ct)) return;

                    // The current state, then every change
                    DiscordRpc.Message first = await Ask(link, "GET_VOICE_SETTINGS", null, null, 8000, ct);
                    Apply(ct, first);
                    await Ask(link, "SUBSCRIBE", null, "VOICE_SETTINGS_UPDATE", 8000, ct);
                    Set(ct, State.Connected, "", Muted, Deafened);

                    while (true)
                    {
                        KeyValuePair<int, string> frame = await link.Receive(ct);
                        if (frame.Key == DiscordRpc.OpClose) return;
                        if (frame.Key == DiscordRpc.OpPing) { await link.Send(DiscordRpc.OpPong, frame.Value, ct); continue; }
                        DiscordRpc.Message message = DiscordRpc.Parse(frame.Value);
                        if (message == null) continue;
                        if (message.Evt == "VOICE_SETTINGS_UPDATE") Apply(ct, message);
                    }
                }
                catch (IOException) when (!string.IsNullOrEmpty(link.ClosedWith))
                {
                    Set(ct, State.Refused, link.ClosedWith, false, false); // Such as a wrong client id
                }
            }
        }

        void Apply(CancellationToken run, DiscordRpc.Message message)
        {
            if (message == null || message.RawJson == null) return;
            bool mute, deaf;
            if (DiscordRpc.TryVoice(message.RawJson, out mute, out deaf)) Set(run, Status == State.Off ? State.Connected : Status, "", mute, deaf);
        }

        // ---------- Authorization ----------

        async Task<bool> Authenticate(Link link, string clientId, DiscordSecrets secrets, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(secrets.AccessToken) && await TryToken(link, secrets.AccessToken, ct)) return true;

            // The token may only have expired: renew it
            if (!string.IsNullOrEmpty(secrets.RefreshToken))
            {
                try
                {
                    int status = await Exchange(clientId, secrets, "refresh_token", "refresh_token", secrets.RefreshToken, ct);
                    if (status == 200 && await TryToken(link, secrets.AccessToken, ct)) return true;
                    if (status == 400) DiscordSecrets.SaveTokens(clientId, "", ""); // A refresh token Discord rejects is dropped, not tried again
                }
                catch (HttpRequestException) { }
            }

            // Not authorized yet: Discord shows its own "approve" window
            Set(ct, State.Approve, "", false, false);
            DiscordRpc.Message reply = await Ask(link, "AUTHORIZE", w =>
            {
                w.WriteString("client_id", clientId);
                w.WriteStartArray("scopes");
                foreach (string scope in Scopes) w.WriteStringValue(scope);
                w.WriteEndArray();
            }, null, 180000, ct);
            if (reply == null || reply.IsError || string.IsNullOrEmpty(reply.Code))
            {
                Set(ct, State.Refused, reply == null ? "" : (reply.Text ?? ""), false, false);
                return false;
            }
            try
            {
                if (await Exchange(clientId, secrets, "authorization_code", "code", reply.Code, ct) != 200) { Set(ct, State.Refused, "", false, false); return false; }
            }
            catch (HttpRequestException)
            {
                Set(ct, State.Failed, "", false, false);
                return false;
            }
            if (await TryToken(link, secrets.AccessToken, ct)) return true;
            Set(ct, State.Refused, "", false, false);
            return false;
        }

        async Task<bool> TryToken(Link link, string token, CancellationToken ct)
        {
            DiscordRpc.Message reply = await Ask(link, "AUTHENTICATE", w => w.WriteString("access_token", token), null, 8000, ct);
            return reply != null && !reply.IsError;
        }

        // Trades a code (or a refresh token) for tokens at Discord and keeps them, encrypted. Returns the HTTP status.
        static async Task<int> Exchange(string clientId, DiscordSecrets secrets, string grant, string field, string value, CancellationToken ct)
        {
            List<KeyValuePair<string, string>> form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", secrets.ClientSecret),
                new KeyValuePair<string, string>("grant_type", grant),
                new KeyValuePair<string, string>(field, value),
            };
            if (grant == "authorization_code") form.Add(new KeyValuePair<string, string>("redirect_uri", RedirectUri));
            using (HttpResponseMessage response = await web.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(form), ct))
            {
                int status = (int)response.StatusCode;
                if (status != 200) return status;
                string text = await response.Content.ReadAsStringAsync(ct);
                using (JsonDocument document = JsonDocument.Parse(text))
                {
                    JsonElement access, refresh, type;
                    if (!document.RootElement.TryGetProperty("access_token", out access) || access.ValueKind != JsonValueKind.String) return 0;
                    if (document.RootElement.TryGetProperty("token_type", out type) && type.ValueKind == JsonValueKind.String && !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)) return 0;
                    secrets.AccessToken = access.GetString();
                    if (document.RootElement.TryGetProperty("refresh_token", out refresh) && refresh.ValueKind == JsonValueKind.String) secrets.RefreshToken = refresh.GetString();
                    secrets.ClientId = clientId;
                    if (!ct.IsCancellationRequested) DiscordSecrets.SaveTokens(clientId, secrets.AccessToken, secrets.RefreshToken); // Only the tokens: a secret typed meanwhile stays
                    return 200;
                }
            }
        }

        // ---------- Requests ----------

        // Sends a command and waits for its answer (matched by nonce); events that arrive meanwhile are applied.
        async Task<DiscordRpc.Message> Ask(Link link, string cmd, Action<Utf8JsonWriter> args, string evt, int timeoutMs, CancellationToken ct)
        {
            string nonce = Guid.NewGuid().ToString("N");
            await link.Send(DiscordRpc.OpFrame, DiscordRpc.Command(cmd, args, evt, nonce), ct);
            return await WaitFor(link, m => m.Nonce == nonce, timeoutMs, ct);
        }

        async Task<DiscordRpc.Message> WaitFor(Link link, Func<DiscordRpc.Message, bool> match, int timeoutMs, CancellationToken ct)
        {
            using (CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                limit.CancelAfter(timeoutMs);
                try
                {
                    while (true)
                    {
                        KeyValuePair<int, string> frame = await link.Receive(limit.Token);
                        if (frame.Key == DiscordRpc.OpClose) { link.ClosedWith = DiscordRpc.CloseText(frame.Value); throw new IOException("Closed"); }
                        if (frame.Key == DiscordRpc.OpPing) { await link.Send(DiscordRpc.OpPong, frame.Value, limit.Token); continue; }
                        DiscordRpc.Message message = DiscordRpc.Parse(frame.Value);
                        if (message == null) continue;
                        if (message.Evt == "VOICE_SETTINGS_UPDATE") Apply(ct, message);
                        if (match(message)) return message;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return null; // No answer in time
                }
            }
        }
    }
}
