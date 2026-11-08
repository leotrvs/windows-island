using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace WindowsIsland
{
    // One monitor, with its bounds in the same units WPF uses for window positions (DIPs).
    sealed class Display
    {
        public string Id;      // Windows device name, such as \\.\DISPLAY2
        public int Number;     // The number Windows shows in Display settings
        public bool Primary;
        public Rect Bounds;
        public Rect WorkArea;  // Without the taskbar
        public Rect PxBounds;  // Same two rectangles in physical pixels
        public Rect PxWorkArea;
        public double Scale = 1; // This monitor's DPI scale: 1.0 = 100%, 1.5 = 150%
    }

    static class Displays
    {
        static readonly Regex Digits = new Regex(@"(\d+)$");

        const uint MONITOR_DEFAULTTONEAREST = 2;
        const uint MONITORINFOF_PRIMARY = 1;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT point, uint flags);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);

        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        static double ScaleOf(IntPtr monitor)
        {
            try
            {
                uint x, y;
                if (GetDpiForMonitor(monitor, 0, out x, out y) == 0 && x > 0) return x / 96.0;
            }
            catch { }
            return 1;
        }

        public static List<Display> All()
        {
            // Monitors report physical pixels. Each also gets its own scale, because the app is per-monitor DPI aware.
            List<KeyValuePair<IntPtr, MONITORINFOEX>> found = new List<KeyValuePair<IntPtr, MONITORINFOEX>>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, dc, rect, data) =>
            {
                MONITORINFOEX info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfo(monitor, ref info)) found.Add(new KeyValuePair<IntPtr, MONITORINFOEX>(monitor, info));
                return true;
            }, IntPtr.Zero);

            List<Display> displays = new List<Display>();
            foreach (KeyValuePair<IntPtr, MONITORINFOEX> pair in found)
            {
                MONITORINFOEX info = pair.Value;
                double scale = ScaleOf(pair.Key);
                Match number = Digits.Match(info.szDevice ?? "");
                displays.Add(new Display
                {
                    Id = info.szDevice,
                    Number = number.Success ? int.Parse(number.Value) : displays.Count + 1,
                    Primary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    Scale = scale,
                    PxBounds = Scale(info.rcMonitor, 1),
                    PxWorkArea = Scale(info.rcWork, 1),
                    Bounds = Scale(info.rcMonitor, 1 / scale),
                    WorkArea = Scale(info.rcWork, 1 / scale),
                });
            }
            displays.Sort((a, b) => a.Primary != b.Primary ? (a.Primary ? -1 : 1) : a.Number.CompareTo(b.Number));
            return displays;
        }

        public static Display Primary()
        {
            List<Display> all = All();
            return all.Count > 0 ? all[0] : new Display
            {
                Id = "",
                Number = 1,
                Primary = true,
                Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                WorkArea = SystemParameters.WorkArea,
                PxBounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                PxWorkArea = SystemParameters.WorkArea,
            };
        }

        public static Display ById(string id)
        {
            foreach (Display display in All())
                if (string.Equals(display.Id, id, StringComparison.OrdinalIgnoreCase)) return display;
            return null;
        }

        // The monitor under the mouse cursor.
        public static Display UnderCursor()
        {
            POINT cursor;
            if (!GetCursorPos(out cursor)) return Primary();
            IntPtr monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
            MONITORINFOEX info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info) ? ById(info.szDevice) ?? Primary() : Primary();
        }

        static Rect Scale(RECT r, double factor)
        {
            return new Rect(r.Left * factor, r.Top * factor, (r.Right - r.Left) * factor, (r.Bottom - r.Top) * factor);
        }
    }
}
