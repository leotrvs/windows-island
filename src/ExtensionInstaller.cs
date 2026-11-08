using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;


namespace WindowsIsland
{
    // Unpacks the browser extension that is embedded in the exe, with the island's port and token in a
    // config.json beside it, and opens the browser's extensions page. The user still has to load the folder
    // there (Chrome allows nothing else outside its store), but needs no copy and paste.
    public static class ExtensionInstaller
    {
        const string Prefix = "extension/";

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsIsland", "extension"); }
        }

        // Writes the extension files and returns the folder. The token is validated hex and the port a number,
        // so the config file can be built by hand.
        public static string Extract(string token, int port)
        {
            string dir = Folder;
            Directory.CreateDirectory(dir);
            Assembly assembly = typeof(ExtensionInstaller).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string file = name.Substring(Prefix.Length);
                if (file.Length == 0 || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || file.Contains("..")) continue;
                using (Stream source = assembly.GetManifestResourceStream(name))
                using (FileStream target = File.Create(Path.Combine(dir, file)))
                    source.CopyTo(target);
            }
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"port\":" + port + ",\"token\":\"" + token + "\"}");
            return dir;
        }

        // Opens chrome://extensions or edge://extensions in that browser. False when it is not installed.
        public static bool OpenExtensionsPage(bool edge)
        {
            string exe = FindBrowser(edge ? "msedge.exe" : "chrome.exe");
            if (exe == null) return false;
            try
            {
                // Without a profile, a browser with several profiles opens its "who is using Chrome?" page and
                // drops the address. The profile used last is the one the user is working in.
                string profile = LastUsedProfile(edge);
                string args = (profile == null ? "" : "--profile-directory=" + (char)34 + profile + (char)34 + " ") + (edge ? "edge://extensions" : "chrome://extensions");
                Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
                // Browsers ignore chrome:// and edge:// addresses that come from the command line and open a blank
                // tab instead, so the address is typed into that tab once it is in front.
                string address = edge ? "edge://extensions" : "chrome://extensions";
                string process = edge ? "msedge" : "chrome";
                ThreadPool.QueueUserWorkItem(_ => TypeAddress(process, address));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int max);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT key; [FieldOffset(0)] public MOUSEINPUT mouse; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }

        const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;
        const ushort VK_CONTROL = 0x11, VK_L = 0x4C, VK_RETURN = 0x0D;

        static readonly string[] NewTabTitles = { "New tab", "Nova guia", "Novo separador", "Nueva pestaña", "Новая вкладка", "Neuer Tab", "Nouvel onglet", "Nuova scheda" };

        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

        // Types the address into the browser's new tab. It only ever types when the window in front is that
        // browser showing a blank new tab, so it can never replace a page you were reading.
        static void TypeAddress(string processName, string address)
        {
            try
            {
                for (int i = 0; i < 40; i++)
                {
                    Thread.Sleep(150);
                    IntPtr window = FindNewTabWindow(processName);
                    if (window == IntPtr.Zero) continue;
                    if (GetForegroundWindow() != window)
                    {
                        if (IsIconic(window)) ShowWindow(window, 9);
                        SetForegroundWindow(window);
                        Thread.Sleep(250);
                    }
                    if (!NewTabInFront(processName)) continue;
                    Thread.Sleep(350); // The address bar needs a moment to take the focus
                    if (!NewTabInFront(processName)) return;
                    Press(VK_CONTROL, VK_L); // Select the address bar
                    foreach (char ch in address) Unicode(ch);
                    Tap(VK_RETURN);
                    return;
                }
            }
            catch (Exception) { }
        }

        // A visible window of that browser whose title says it shows a blank new tab.
        static IntPtr FindNewTabWindow(string processName)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder title = new StringBuilder(256);
                GetWindowText(h, title, 256);
                bool newTab = false;
                foreach (string name in NewTabTitles)
                    if (title.ToString().StartsWith(name, StringComparison.OrdinalIgnoreCase)) { newTab = true; break; }
                if (!newTab) return true;
                GetWindowThreadProcessId(h, out uint pid);
                try
                {
                    using (Process owner = Process.GetProcessById((int)pid))
                        if (string.Equals(owner.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
                }
                catch (Exception) { }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        static bool NewTabInFront(string processName)
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint pid);
            try
            {
                using (Process owner = Process.GetProcessById((int)pid))
                {
                    if (!string.Equals(owner.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            catch (Exception) { return false; }
            StringBuilder title = new StringBuilder(256);
            GetWindowText(window, title, 256);
            string text = title.ToString();
            foreach (string name in NewTabTitles)
                if (text.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static INPUT Key(ushort vk, ushort scan, uint flags)
        {
            INPUT input = new INPUT { type = 1 };
            input.u.key = new KEYBDINPUT { vk = vk, scan = scan, flags = flags };
            return input;
        }

        static void Tap(ushort vk)
        {
            SendInput(2, new[] { Key(vk, 0, 0), Key(vk, 0, KEYEVENTF_KEYUP) }, Marshal.SizeOf(typeof(INPUT)));
        }

        static void Press(ushort modifier, ushort vk)
        {
            SendInput(4, new[] { Key(modifier, 0, 0), Key(vk, 0, 0), Key(vk, 0, KEYEVENTF_KEYUP), Key(modifier, 0, KEYEVENTF_KEYUP) }, Marshal.SizeOf(typeof(INPUT)));
        }

        static void Unicode(char ch)
        {
            SendInput(2, new[] { Key(0, ch, KEYEVENTF_UNICODE), Key(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP) }, Marshal.SizeOf(typeof(INPUT)));
        }

        // The folder name of the profile the browser used last ("Default", "Profile 2"), from its Local State file.
        static string LastUsedProfile(bool edge)
        {
            try
            {
                string state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                            edge ? "Microsoft" : "Google", edge ? "Edge" : "Chrome", "User Data", "Local State");
                if (!File.Exists(state)) return null;
                using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(state)))
                {
                    if (!doc.RootElement.TryGetProperty("profile", out System.Text.Json.JsonElement profile)) return null;
                    if (!profile.TryGetProperty("last_used", out System.Text.Json.JsonElement last)) return null;
                    string name = last.GetString();
                    // Only a plain folder name: it goes on a command line
                    return !string.IsNullOrEmpty(name) && System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9 ]{1,40}$") ? name : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string FindBrowser(string exe)
        {
            foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                    {
                        string path = key == null ? null : key.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                    }
                }
                catch (Exception) { }
            }
            return null;
        }
    }
}
