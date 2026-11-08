using System;
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
    }
}
