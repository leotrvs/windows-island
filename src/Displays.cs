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

        public static List<Display> All()
        {
            // Monitors report physical pixels; WPF positions windows in DIPs at the system DPI.
            List<MONITORINFOEX> found = new List<MONITORINFOEX>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, dc, rect, data) =>
            {
                MONITORINFOEX info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfo(monitor, ref info)) found.Add(info);
                return true;
            }, IntPtr.Zero);

            double toDip = 1;
            foreach (MONITORINFOEX info in found)
            {
                int width = info.rcMonitor.Right - info.rcMonitor.Left;
                if ((info.dwFlags & MONITORINFOF_PRIMARY) != 0 && width > 0)
                    toDip = SystemParameters.PrimaryScreenWidth / width;
            }

            List<Display> displays = new List<Display>();
            foreach (MONITORINFOEX info in found)
            {
                Match number = Digits.Match(info.szDevice ?? "");
                displays.Add(new Display
                {
                    Id = info.szDevice,
                    Number = number.Success ? int.Parse(number.Value) : displays.Count + 1,
                    Primary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    Bounds = Scale(info.rcMonitor, toDip),
                    WorkArea = Scale(info.rcWork, toDip),
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
