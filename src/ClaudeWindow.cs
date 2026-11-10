using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIsland
{
    // Brings the Claude desktop app to the front, so a question it asks can be answered there.
    static class ClaudeWindow
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int max);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

        public static void Focus()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder title = new StringBuilder(128);
                GetWindowText(h, title, 128);
                if (!title.ToString().StartsWith("Claude", StringComparison.Ordinal)) return true;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                try
                {
                    using (Process owner = Process.GetProcessById((int)pid))
                        if (string.Equals(owner.ProcessName, "claude", StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
                }
                catch (Exception) { }
                return true;
            }, IntPtr.Zero);
            if (found == IntPtr.Zero) return;
            if (IsIconic(found)) ShowWindow(found, 9);
            SetForegroundWindow(found);
        }

        // Brings the window of one chat to the front: the Claude app for a desktop chat, otherwise the terminal (or editor)
        // that runs Claude Code, found by going up the chain of parent processes until one has a window. Does nothing when
        // nothing is found.
        public static void FocusSession(int pid, bool desktop)
        {
            try
            {
                if (desktop || pid <= 0) { Focus(); return; }

                Dictionary<int, int> parents = ParentMap();
                Dictionary<int, IntPtr> windows = TopWindows();
                IntPtr found = IntPtr.Zero;
                HashSet<int> visited = new HashSet<int>();
                int current = pid;
                for (int depth = 0; depth < 16 && current > 4 && visited.Add(current) && found == IntPtr.Zero; depth++)
                {
                    if (IsShell(current)) break; // Never the desktop itself
                    if (!windows.TryGetValue(current, out found))
                    {
                        // A classic console window belongs to the console host, which is a child of the shell, not its parent
                        foreach (KeyValuePair<int, int> pair in parents)
                        {
                            IntPtr host;
                            if (pair.Value == current && windows.TryGetValue(pair.Key, out host) && IsConsoleHost(pair.Key)) { found = host; break; }
                        }
                    }
                    int next;
                    if (found != IntPtr.Zero || !parents.TryGetValue(current, out next)) break;
                    current = next;
                }
                if (found == IntPtr.Zero) return;
                if (IsIconic(found)) ShowWindow(found, 9);
                SetForegroundWindow(found);
            }
            catch (Exception)
            {
                // Failing silently is the intended behavior: the click simply does nothing.
            }
        }

        static bool IsShell(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        static bool IsConsoleHost(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    string name = process.ProcessName;
                    return name.Equals("conhost", StringComparison.OrdinalIgnoreCase) || name.Equals("OpenConsole", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception) { return false; }
        }

        // For each process that has a visible, titled, top-level window: that window.
        static Dictionary<int, IntPtr> TopWindows()
        {
            Dictionary<int, IntPtr> result = new Dictionary<int, IntPtr>();
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero) return true; // 4 = GW_OWNER: skip owned popups
                StringBuilder title = new StringBuilder(8);
                if (GetWindowText(h, title, 8) == 0) return true;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (!result.ContainsKey((int)pid)) result[(int)pid] = h;
                return true;
            }, IntPtr.Zero);
            return result;
        }

        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry
        {
            public uint Size, Usage, ProcessId;
            public UIntPtr DefaultHeapId;
            public uint ModuleId, Threads, ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
        }

        // process id -> parent process id
        static Dictionary<int, int> ParentMap()
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return map;
            try
            {
                ProcessEntry entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
                    map[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return map;
        }
    }
}
