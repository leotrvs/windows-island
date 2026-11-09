using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace WindowsIsland
{
    // The island's command line: "island show build --title Building --progress 40" and friends. It sends the request to
    // the local API of the running island, so it needs the API turned on in Settings. Runs before any window opens.
    static class ApiCli
    {
        static readonly string[] Commands = { "show", "done", "end", "alert", "token", "help", "run", "permission-hook" };
        const int STD_OUTPUT_HANDLE = -11, STD_ERROR_HANDLE = -12, ATTACH_PARENT_PROCESS = -1;
        const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
        const int ConnectTimeoutMs = 2000, ReplyTimeoutMs = 6000, MaxReplyBytes = 16 * 1024;

        [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int which);
        [DllImport("kernel32.dll")] static extern bool SetStdHandle(int which, IntPtr handle);
        [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        const string Usage =
            "Windows Island command line. Turn on the local API in Settings first.\n" +
            "\n" +
            "  island show <id> [--title T] [--text T] [--progress 0-100] [--icon NAME] [--state running|done|error]\n" +
            "  island done <id> [--text T]\n" +
            "  island end <id>\n" +
            "  island alert <title> [--text T] [--icon NAME] [--seconds 2-15]\n" +
            "  island run [--title T] -- <command...>   runs the command and shows it in the island until it ends\n" +
            "  island token      prints the API token (keep it secret)\n" +
            "  island help\n" +
            "\n" +
            "A new activity needs --title. Ids use letters, digits, dot, dash and underscore.\n" +
            "Icons: ";

        public static bool IsCommand(string[] args)
        {
            return args.Length > 0 && Array.IndexOf(Commands, args[0].ToLowerInvariant()) >= 0;
        }

        // Returns the exit code: 0 when it worked, 1 otherwise.
        public static int Run(string[] args)
        {
            ConnectConsole();
            try
            {
                return Execute(args);
            }
            catch (Exception)
            {
                Fail("Something went wrong.");
                return 1;
            }
            finally
            {
                try { Console.Out.Flush(); Console.Error.Flush(); }
                catch (Exception) { }
            }
        }

        // The exe is a Windows app, so it has no console of its own: it writes to the terminal that started it, or to
        // wherever its output was redirected. Started from Explorer, there is nowhere to write and nothing is shown.
        static void ConnectConsole()
        {
            bool output = Usable(GetStdHandle(STD_OUTPUT_HANDLE)), errors = Usable(GetStdHandle(STD_ERROR_HANDLE));
            if (output && errors) return;
            if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;
            if (!output && !Usable(GetStdHandle(STD_OUTPUT_HANDLE))) SetStdHandle(STD_OUTPUT_HANDLE, OpenConsoleOutput());
            if (!errors && !Usable(GetStdHandle(STD_ERROR_HANDLE))) SetStdHandle(STD_ERROR_HANDLE, OpenConsoleOutput());
        }

        static bool Usable(IntPtr handle)
        {
            return handle != IntPtr.Zero && handle != new IntPtr(-1) && GetFileType(handle) != 0;
        }

        static IntPtr OpenConsoleOutput()
        {
            return CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        }

        static bool quiet; // "island run" says nothing of its own: the command's output is what counts

        static void Print(string text)
        {
            if (!quiet) Console.Out.WriteLine(text);
        }

        static void Fail(string text)
        {
            if (!quiet) Console.Error.WriteLine("island: " + text);
        }

        // island run [--title T] -- npm test: the command runs here, with its own output, and the island shows it
        // as an activity until it ends (done, or error with the exit code). The exit code is the command's own.
        static int RunCommand(string[] args)
        {
            string title = null;
            int start = 1;
            while (start < args.Length && args[start].StartsWith("--", StringComparison.Ordinal) && args[start] != "--")
            {
                if (!args[start].Equals("--title", StringComparison.OrdinalIgnoreCase) || start + 1 >= args.Length) return Wrong("Unknown or incomplete option: " + args[start] + ".");
                title = args[start + 1];
                start += 2;
            }
            if (start < args.Length && args[start] == "--") start++;
            if (start >= args.Length) return Wrong("The run command needs a command to run.");

            string[] command = new string[args.Length - start];
            Array.Copy(args, start, command, 0, command.Length);
            if (string.IsNullOrWhiteSpace(title)) title = string.Join(" ", command);
            if (title.Length > 60) title = title.Substring(0, 57) + "...";
            string id = "run-" + Environment.ProcessId;

            quiet = true;
            SendActivity(id, title, "running", null);
            int code;
            try
            {
                // Through cmd, so npm, dotnet and other .cmd tools are found like in a terminal
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false };
                StringBuilder line = new StringBuilder("/d /s /c \"");
                foreach (string part in command) line.Append(part.IndexOfAny(new[] { ' ', '\t' }) >= 0 ? "\"" + part + "\"" : part).Append(' ');
                info.Arguments = line.ToString().TrimEnd() + "\"";
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    process.WaitForExit();
                    code = process.ExitCode;
                }
            }
            catch (Exception)
            {
                code = 1;
            }
            SendActivity(id, title, code == 0 ? "done" : "error", code == 0 ? "Done" : "Failed (code " + code + ")");
            return code;
        }

        static void SendActivity(string id, string title, string state, string text)
        {
            try
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Utf8JsonWriter json = new Utf8JsonWriter(buffer))
                    {
                        json.WriteStartObject();
                        json.WriteString("id", id);
                        json.WriteString("title", title);
                        json.WriteString("icon", "terminal");
                        json.WriteString("state", state);
                        if (text != null) json.WriteString("text", text);
                        json.WriteEndObject();
                    }
                    Send("/v1/activity", buffer.ToArray());
                }
            }
            catch (Exception)
            {
                // The island is not running: the command goes on without it
            }
        }

        static int Execute(string[] args)
        {
            string command = args[0].ToLowerInvariant();
            if (command == "permission-hook") return PermissionHook.Run(); // Claude Code's hook: always exit code 0
            if (command == "run") return RunCommand(args);
            if (command == "help")
            {
                Print(Usage + string.Join(", ", LocalApi.IconNames));
                return 0;
            }
            if (command == "token")
            {
                string token = LocalApi.ReadToken();
                if (token == null)
                {
                    Fail("There is no token yet. Turn on the local API in Settings first.");
                    return 1;
                }
                Print(token);
                return 0;
            }

            string[] allowed;
            if (command == "show") allowed = new[] { "title", "text", "progress", "icon", "state" };
            else if (command == "done") allowed = new[] { "text" };
            else if (command == "alert") allowed = new[] { "text", "icon", "seconds" };
            else allowed = new string[0];

            string positional = null;
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    string name = arg.Substring(2), value = null;
                    int equals = name.IndexOf('=');
                    if (equals >= 0)
                    {
                        value = name.Substring(equals + 1);
                        name = name.Substring(0, equals);
                    }
                    else if (i + 1 < args.Length) value = args[++i];
                    if (Array.IndexOf(allowed, name.ToLowerInvariant()) < 0 || value == null || options.ContainsKey(name))
                        return Wrong("Unknown or incomplete option: " + arg + ".");
                    options[name] = value;
                }
                else if (positional == null) positional = arg;
                else return Wrong("Too many values: " + arg + ".");
            }
            if (string.IsNullOrEmpty(positional)) return Wrong(command == "alert" ? "The alert needs a title." : "The activity needs an id.");

            using (MemoryStream buffer = new MemoryStream())
            {
                string path;
                using (Utf8JsonWriter json = new Utf8JsonWriter(buffer))
                {
                    json.WriteStartObject();
                    if (command == "alert")
                    {
                        path = "/v1/alert";
                        json.WriteString("title", positional);
                    }
                    else
                    {
                        path = command == "end" ? "/v1/activity/end" : "/v1/activity";
                        json.WriteString("id", positional);
                    }
                    if (command == "done") json.WriteString("state", "done");
                    foreach (KeyValuePair<string, string> option in options)
                    {
                        string name = option.Key.ToLowerInvariant();
                        if (name == "progress" || name == "seconds")
                        {
                            double number;
                            if (!double.TryParse(option.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number))
                                return Wrong("--" + name + " needs a number.");
                            json.WriteNumber(name, number);
                        }
                        else json.WriteString(name, option.Value);
                    }
                    json.WriteEndObject();
                }
                return Send(path, buffer.ToArray());
            }
        }

        static int Wrong(string message)
        {
            Fail(message + " Run \"island help\".");
            return 1;
        }

        static int Send(string path, byte[] body)
        {
            string token = LocalApi.ReadToken();
            if (token == null)
            {
                Fail("There is no token yet. Turn on the local API in Settings first.");
                return 1;
            }
            int port = Settings.Load().ApiPort;
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                try
                {
                    if (!client.ConnectAsync(IPAddress.Loopback, port).Wait(ConnectTimeoutMs)) return NotRunning();
                }
                catch (Exception)
                {
                    return NotRunning();
                }
                // The token only goes to the island itself, never to another program that took the port.
                if (!AnswersAsIsland(client, port))
                {
                    Fail("Another program is using port " + port + ", so the request was not sent.");
                    return 1;
                }

                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = ReplyTimeoutMs;
                stream.WriteTimeout = ReplyTimeoutMs;
                string head = "POST " + path + " HTTP/1.1\r\n" +
                              "Host: 127.0.0.1:" + port + "\r\n" +
                              "Authorization: Bearer " + token + "\r\n" +
                              "Content-Type: application/json\r\n" +
                              "Content-Length: " + body.Length + "\r\n" +
                              "Connection: close\r\n\r\n";
                byte[] headBytes = Encoding.ASCII.GetBytes(head);
                stream.Write(headBytes, 0, headBytes.Length);
                stream.Write(body, 0, body.Length);

                byte[] reply = new byte[MaxReplyBytes];
                int filled = 0;
                try
                {
                    int n;
                    while (filled < reply.Length && (n = stream.Read(reply, filled, reply.Length - filled)) > 0) filled += n;
                }
                catch (IOException)
                {
                    if (filled == 0) return NotRunning();
                }
                return Report(Encoding.UTF8.GetString(reply, 0, filled));
            }
        }

        internal static bool AnswersAsIsland(TcpClient client, int port)
        {
            try
            {
                IPEndPoint local = (IPEndPoint)client.Client.LocalEndPoint;
                uint loopback = BitConverter.ToUInt32(IPAddress.Loopback.GetAddressBytes(), 0);
                int pid = LocalApi.OwnerOf(loopback, port, local.Port);
                if (pid <= 0) return false;
                string server = LocalApi.ImagePath(pid);
                if (server == null) return false;
                server = Path.GetFullPath(server);
                return string.Equals(server, Path.GetFullPath(Installer.InstalledExe), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(server, Path.GetFullPath(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        static int NotRunning()
        {
            Fail("Windows Island is not running, or its local API is off (Settings).");
            return 1;
        }

        static int Report(string reply)
        {
            int status = 0;
            string code = null;
            int space = reply.IndexOf(' ');
            if (reply.StartsWith("HTTP/1.", StringComparison.Ordinal) && space > 0 && reply.Length >= space + 4)
                int.TryParse(reply.Substring(space + 1, 3), NumberStyles.None, CultureInfo.InvariantCulture, out status);
            int bodyStart = reply.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (bodyStart >= 0)
            {
                try
                {
                    using (JsonDocument document = JsonDocument.Parse(reply.Substring(bodyStart + 4)))
                    {
                        JsonElement error;
                        if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out error) && error.ValueKind == JsonValueKind.String)
                            code = error.GetString();
                    }
                }
                catch (Exception)
                {
                }
            }

            if (status == 200)
            {
                Print("OK");
                return 0;
            }
            if (status == 400 && code == "title_required") Fail("A new activity needs --title.");
            else if (status == 400) Fail("The island refused the request (" + (code ?? "bad_request") + ").");
            else if (status == 401) Fail("The island refused the token. Check that the local API is on in Settings.");
            else if (status == 409) Fail("There are already 8 live activities. End one first.");
            else if (status == 413) Fail("The request is too large.");
            else if (status == 429) Fail("Too many requests. Try again in a moment.");
            else if (status == 0) Fail("The island did not answer.");
            else Fail("The island answered " + status + ".");
            return 1;
        }
    }
}
