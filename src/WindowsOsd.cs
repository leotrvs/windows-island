using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIsland
{
    // EXPERIMENTAL. While on, hides Windows' own volume and brightness popup so only the island's bar shows.
    // Windows has no setting for this: the popup is a small window of the Explorer shell (class
    // XamlExplorerHostIslandWindow) that becomes visible for a moment. A window-event hook (the same call
    // screen readers use) tells this app the moment such a window is shown, and it is hidden at once, so there
    // is no polling and no visible flash even while a key is held. Only small windows of that class are touched,
    // and the island gives the popup back when this is turned off, when it quits and at its next start, even
    // after a crash. A Windows update may change the window and stop this from working.
    sealed class WindowsOsd
    {
        const string ClassName = "XamlExplorerHostIslandWindow";
        const uint EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_UNCLOAKED = 0x8018, EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        const uint WINEVENT_OUTOFCONTEXT = 0;
        const int OBJID_WINDOW = 0;

        delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
        const int GWL_EXSTYLE = -20, WS_EX_LAYERED = 0x80000;
        const uint LWA_ALPHA = 2;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

        [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        const uint WM_QUIT = 0x0012;

        readonly WinEventProc callback; // Kept in a field so it is not collected while hooked
        System.Threading.Thread thread;
        uint threadId;

        readonly System.Collections.Generic.HashSet<IntPtr> seeded = new System.Collections.Generic.HashSet<IntPtr>();

        public WindowsOsd()
        {
            callback = OnEvent;
            RestoreStale();
        }

        // If a earlier run of the island ended without turning this off (a crash, or being closed from the Task
        // Manager), its popup window may still be fully transparent. Any such window is given back at start-up.
        void RestoreStale()
        {
            try
            {
                for (IntPtr found = FindWindowEx(IntPtr.Zero, IntPtr.Zero, ClassName, null); found != IntPtr.Zero; found = FindWindowEx(IntPtr.Zero, found, ClassName, null))
                {
                    RECT box;
                    if (!IsPopup(found, out box)) continue;
                    int style = GetWindowLong(found, GWL_EXSTYLE);
                    uint key, flags;
                    byte alpha;
                    if ((style & WS_EX_LAYERED) != 0 && GetLayeredWindowAttributes(found, out key, out alpha, out flags) && alpha == 0)
                    {
                        SetLayeredWindowAttributes(found, 0, 255, LWA_ALPHA);
                        SetWindowLong(found, GWL_EXSTYLE, style & ~WS_EX_LAYERED);
                    }
                }
            }
            catch (Exception) { }
        }

        // The popup's window stays around between uses. Making it fully transparent ahead of time means that even
        // the instant between Windows showing it and this app hiding it shows nothing, so there is no flash.
        void Seed(IntPtr hwnd)
        {
            lock (seeded)
            {
                if (seeded.Contains(hwnd)) return;
                int style = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((style & WS_EX_LAYERED) != 0) return; // Already layered by something else: leave it alone
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
                SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
                seeded.Add(hwnd);
            }
        }

        // Gives the popup back exactly as Windows made it.
        void Restore()
        {
            lock (seeded)
            {
                foreach (IntPtr hwnd in seeded)
                {
                    try
                    {
                        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_LAYERED);
                    }
                    catch (Exception) { }
                }
                seeded.Clear();
            }
        }

        static bool IsPopup(IntPtr hwnd, out RECT r)
        {
            r = new RECT();
            StringBuilder name = new StringBuilder(64);
            if (GetClassName(hwnd, name, 64) == 0 || name.ToString() != ClassName) return false;
            if (!GetWindowRect(hwnd, out r)) return false;
            int w = r.R - r.L, h = r.B - r.T;
            return w > 0 && w < 500 && h > 0 && h < 200;
        }

        // The hook lives on its own thread with its own message loop, so the island's animations on the
        // main thread can never delay the moment the popup is hidden.
        public void Apply(bool on)
        {
            if (on && thread == null)
            {
                System.Threading.ManualResetEventSlim ready = new System.Threading.ManualResetEventSlim(false);
                thread = new System.Threading.Thread(() =>
                {
                    threadId = GetCurrentThreadId();
                    IntPtr show = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, callback, 0, 0, WINEVENT_OUTOFCONTEXT);
                    IntPtr uncloak = SetWinEventHook(EVENT_OBJECT_UNCLOAKED, EVENT_OBJECT_UNCLOAKED, IntPtr.Zero, callback, 0, 0, WINEVENT_OUTOFCONTEXT);
                    IntPtr move = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, callback, 0, 0, WINEVENT_OUTOFCONTEXT);
                    for (IntPtr found = FindWindowEx(IntPtr.Zero, IntPtr.Zero, ClassName, null); found != IntPtr.Zero; found = FindWindowEx(IntPtr.Zero, found, ClassName, null))
                    {
                        RECT box;
                        if (IsPopup(found, out box)) Seed(found);
                    }
                    ready.Set();
                    MSG msg;
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref msg); DispatchMessage(ref msg); }
                    UnhookWinEvent(show); UnhookWinEvent(uncloak); UnhookWinEvent(move);
                }) { IsBackground = true, Name = "WindowsOsd" };
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                ready.Wait(2000);
            }
            else if (!on && thread != null)
            {
                PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                thread.Join(2000); // The hook is gone before the window is given back, so it cannot be hidden again
                thread = null;
                Restore();
            }
        }

        void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero) return;
            try
            {
                RECT r;
                if (!IsPopup(hwnd, out r)) return;
                Seed(hwnd);
                if (IsWindowVisible(hwnd)) ShowWindow(hwnd, 0);
            }
            catch (Exception) { }
        }
    }
}
