using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsIsland
{
    // The tray icon is the way back to the island's settings, even while the island is hidden.
    // A click opens the settings window; the right-click menu keeps only the quick actions.
    // It talks to the shell directly (no Windows Forms), which keeps the app small.
    sealed class Tray : IDisposable
    {
        const int WM_APP_TRAY = 0x8001;
        const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_NULL = 0;
        const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
        const uint MF_STRING = 0, MF_CHECKED = 8, MF_SEPARATOR = 0x800;
        const uint TPM_RIGHTBUTTON = 2, TPM_BOTTOMALIGN = 0x20, TPM_RETURNCMD = 0x100;
        const uint MF_POPUP = 0x10;
        const int CmdSettings = 1, CmdEmpty = 2, CmdUpdates = 3, CmdQuit = 4, CmdStopwatch = 200, CmdStopTimer = 201, CmdMinutes = 1000;
        static readonly int[] TimerMinutes = { 1, 5, 10, 15, 25, 30, 45, 60 };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr reserved);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref ICONINFO info);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bits, byte[] data);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

        readonly Settings settings;
        readonly Action changed;
        readonly Action openSettings, toggleSettings, checkForUpdates, quit;
        readonly TimerService timer;
        readonly HwndSource window;
        readonly int taskbarCreated;
        IntPtr hIcon;
        bool added;

        public Tray(Settings settings, Action changed, Action openSettings, Action toggleSettings, Action checkForUpdates, Action quit, TimerService timer)
        {
            this.timer = timer;
            this.settings = settings;
            this.changed = changed;
            this.openSettings = openSettings;
            this.toggleSettings = toggleSettings;
            this.checkForUpdates = checkForUpdates;
            this.quit = quit;

            // A hidden message-only window receives the tray icon's mouse events.
            HwndSourceParameters parameters = new HwndSourceParameters("WindowsIslandTray")
            {
                ParentWindow = new IntPtr(-3), // HWND_MESSAGE
                WindowStyle = 0,
                Width = 0,
                Height = 0,
            };
            window = new HwndSource(parameters);
            window.AddHook(WndProc);
            taskbarCreated = RegisterWindowMessage("TaskbarCreated");

            hIcon = MakeIcon();
            Add();
        }

        NOTIFYICONDATA Data()
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = window.Handle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_APP_TRAY,
                hIcon = hIcon,
                szTip = Installer.Name,
                szInfo = "",
                szInfoTitle = "",
            };
        }

        void Add()
        {
            NOTIFYICONDATA data = Data();
            added = Shell_NotifyIcon(NIM_ADD, ref data);        }

        IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_APP_TRAY)
            {
                int mouse = (int)(lParam.ToInt64() & 0xFFFF);
                if (mouse == WM_LBUTTONUP) toggleSettings(); // A second click closes it again
                else if (mouse == WM_RBUTTONUP) ShowMenu();
                handled = true;
            }
            else if (taskbarCreated != 0 && message == taskbarCreated)
            {
                Add(); // Explorer restarted: put the icon back
            }
            return IntPtr.Zero;
        }

        internal void ShowMenu()
        {
            List<TrayMenu.Item> items = new List<TrayMenu.Item>
            {
                new TrayMenu.Item { Icon = "\uE713", Text = Strings.MenuSettings, Click = openSettings },
                new TrayMenu.Item { Separator = true },
                new TrayMenu.Item
                {
                    Icon = "\uE740", Text = Strings.MenuAlwaysThin, Checked = settings.AlwaysThin,
                    Click = () => { settings.AlwaysThin = !settings.AlwaysThin; settings.Save(); changed(); },
                },
                new TrayMenu.Item
                {
                    Icon = "\uE708", Text = Strings.MenuQuietIsland, Checked = settings.IslandQuiet,
                    Click = () => { settings.IslandQuiet = !settings.IslandQuiet; settings.Save(); changed(); },
                },
                new TrayMenu.Item
                {
                    Icon = "\uE7B3", Text = Strings.MenuEmptyIsland, Checked = settings.EmptyIsland,
                    Click = () => { settings.EmptyIsland = !settings.EmptyIsland; settings.Save(); changed(); },
                },
                new TrayMenu.Item { Separator = true },
                new TrayMenu.Item { Icon = "\uE895", Text = Strings.MenuCheckUpdates, Click = checkForUpdates },
                new TrayMenu.Item { Icon = "\uE711", Text = Strings.MenuClose, Danger = true, Click = quit },
            };
            POINT cursor;
            GetCursorPos(out cursor);
            TrayMenu.Open(items, new Point(cursor.X, cursor.Y), settings.Theme);
        }

        // A small dark pill with a light outline, readable on both light and dark taskbars.
        static IntPtr MakeIcon()
        {
            const int size = 32;
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                Pen outline = new Pen(new SolidColorBrush(Color.FromArgb(230, 235, 235, 240)), 2);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(12, 12, 14)), outline, new Rect(3, 10, 26, 12), 6, 6);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(232, 135, 95)), null, new Point(23, 16), 3, 3);
            }
            RenderTargetBitmap target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            byte[] pixels = new byte[size * size * 4];
            target.CopyPixels(pixels, size * 4, 0);

            IntPtr color = CreateBitmap(size, size, 1, 32, pixels);
            IntPtr mask = CreateBitmap(size, size, 1, 1, new byte[size * size / 8]);
            try
            {
                ICONINFO info = new ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };
                return CreateIconIndirect(ref info);
            }
            finally
            {
                DeleteObject(color);
                DeleteObject(mask);
            }
        }

        public void Dispose()
        {
            if (added)
            {
                NOTIFYICONDATA data = Data();
                Shell_NotifyIcon(NIM_DELETE, ref data);
                added = false;
            }
            window.RemoveHook(WndProc);
            window.Dispose();
            if (hIcon != IntPtr.Zero) { DestroyIcon(hIcon); hIcon = IntPtr.Zero; }
        }
    }
}
