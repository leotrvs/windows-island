using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIsland
{
    struct ScreenState
    {
        public bool Fullscreen;
        public bool Maximized;
    }

    static class Native
    {
        const int GWL_EXSTYLE = -20;
        const long WS_EX_TOOLWINDOW = 0x80;
        const long WS_EX_APPWINDOW = 0x40000;
        const long WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200, SWP_NOZORDER = 0x4;
        const uint MONITOR_DEFAULTTONEAREST = 2;
        const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        const int QUNS_PRESENTATION_MODE = 4;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        // The cursor in physical screen pixels.
        public static System.Windows.Point CursorPosition()
        {
            POINT point;
            return GetCursorPos(out point) ? new System.Windows.Point(point.X, point.Y) : new System.Windows.Point(double.NaN, double.NaN);
        }

        // Keeps the island out of Alt+Tab and the taskbar, and stops clicks on it from stealing focus.
        public static void MakeOverlay(IntPtr hwnd)
        {
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            style = (style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        }

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

        public static IntPtr Foreground() { return GetForegroundWindow(); }

        // The island normally never takes focus. To type in it, the no-activate style is lifted and the window comes forward
        // (the click that started this gives the process the right to). Call Release when typing ends.
        public static void TakeFocus(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style & ~WS_EX_NOACTIVATE));
            IntPtr front = GetForegroundWindow();
            if (front == hwnd) return;
            uint frontThread = front == IntPtr.Zero ? 0 : GetWindowThreadProcessId(front, IntPtr.Zero);
            uint mine = GetCurrentThreadId();
            bool attached = frontThread != 0 && frontThread != mine && AttachThreadInput(mine, frontThread, true);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(mine, frontThread, false);
        }

        // Back to the no-activate overlay; the window that had the focus before gets it back.
        public static void ReleaseFocus(IntPtr hwnd, IntPtr previous)
        {
            if (hwnd == IntPtr.Zero) return;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_NOACTIVATE));
            if (previous != IntPtr.Zero && previous != hwnd && IsWindow(previous) && GetForegroundWindow() == hwnd) SetForegroundWindow(previous);
        }

        // Moves a window to physical screen pixels without activating it. Crossing to a monitor with another scale
        // makes Windows send the DPI change right away, so callers set the size after this returns.
        public static void MoveTo(IntPtr hwnd, int x, int y)
        {
            if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER | SWP_NOOWNERZORDER);
        }

        public static void KeepTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        // Looks at the foreground window, but only when it is on the same monitor as this island.
        public static ScreenState GetScreenState(IntPtr self)
        {
            ScreenState state = new ScreenState();
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == self || IsDesktop(foreground)) return state;

            IntPtr monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
            if (self != IntPtr.Zero && monitor != MonitorFromWindow(self, MONITOR_DEFAULTTONEAREST)) return state;
            MONITORINFO info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return state;

            state.Maximized = IsZoomed(foreground);

            int notification;
            if (SHQueryUserNotificationState(out notification) == 0 &&
                (notification == QUNS_RUNNING_D3D_FULL_SCREEN || notification == QUNS_PRESENTATION_MODE))
            {
                state.Fullscreen = true;
                return state;
            }

            RECT rect;
            if (GetWindowRect(foreground, out rect))
            {
                RECT m = info.rcMonitor;
                bool covers = rect.Left <= m.Left && rect.Top <= m.Top && rect.Right >= m.Right && rect.Bottom >= m.Bottom;
                // A game in a borderless window reports itself as maximized too; what tells it from a normal maximized
                // window (which stops at the taskbar and has a title bar) is that it covers the whole screen without one
                if (covers && (!state.Maximized || (GetWindowLong(foreground, GWL_STYLE) & WS_CAPTION) != WS_CAPTION))
                {
                    state.Fullscreen = true;
                    state.Maximized = false;
                }
            }
            return state;
        }

        const int GWL_STYLE = -16, WS_CAPTION = 0x00C00000;
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);

        static bool IsDesktop(IntPtr hwnd)
        {
            StringBuilder name = new StringBuilder(64);
            GetClassName(hwnd, name, name.Capacity);
            string value = name.ToString();
            return value == "Progman" || value == "WorkerW" || value == "Shell_TrayWnd";
        }
    }
}
