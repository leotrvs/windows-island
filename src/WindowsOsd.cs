using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WindowsIsland
{
    // EXPERIMENTAL. While on, hides Windows' own volume and brightness popup so only the island's bar shows.
    // Windows has no setting for this: the popup is a small window of the Explorer shell (class
    // XamlExplorerHostIslandWindow) that becomes visible for a moment, and this hides it each time it appears.
    // It touches only visible windows of that class that are small (the popup is about 230x85), and the
    // window is shown again by Windows itself the next time, so quitting the island or turning this off
    // restores the normal popup. A Windows update may change the window and stop this from working.
    sealed class WindowsOsd
    {
        const string ClassName = "XamlExplorerHostIslandWindow";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

        readonly DispatcherTimer timer;

        public WindowsOsd(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(60) };
            timer.Tick += (s, e) => Hide();
        }

        public void Apply(bool on)
        {
            if (on && !timer.IsEnabled) timer.Start();
            else if (!on && timer.IsEnabled) timer.Stop();
        }

        static void Hide()
        {
            try
            {
                IntPtr h = IntPtr.Zero;
                int guard = 0;
                while (guard++ < 16 && (h = FindWindowEx(IntPtr.Zero, h, ClassName, null)) != IntPtr.Zero)
                {
                    if (!IsWindowVisible(h) || !GetWindowRect(h, out RECT r)) continue;
                    int w = r.R - r.L, ht = r.B - r.T;
                    if (w > 0 && w < 500 && ht > 0 && ht < 200) ShowWindow(h, 0);
                }
            }
            catch (Exception) { }
        }
    }
}
