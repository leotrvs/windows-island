using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using Forms = System.Windows.Forms;

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

        public static List<Display> All()
        {
            // WPF positions windows in DIPs at the system DPI; screens report physical pixels.
            Forms.Screen primaryScreen = Forms.Screen.PrimaryScreen;
            double toDip = primaryScreen != null && primaryScreen.Bounds.Width > 0
                ? SystemParameters.PrimaryScreenWidth / primaryScreen.Bounds.Width
                : 1;

            List<Display> displays = new List<Display>();
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                Match number = Digits.Match(screen.DeviceName ?? "");
                displays.Add(new Display
                {
                    Id = screen.DeviceName,
                    Number = number.Success ? int.Parse(number.Value) : displays.Count + 1,
                    Primary = screen.Primary,
                    Bounds = Scale(screen.Bounds, toDip),
                    WorkArea = Scale(screen.WorkingArea, toDip),
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
            Forms.Screen screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
            return ById(screen?.DeviceName) ?? Primary();
        }

        static Rect Scale(System.Drawing.Rectangle r, double factor)
        {
            return new Rect(r.X * factor, r.Y * factor, r.Width * factor, r.Height * factor);
        }
    }
}
