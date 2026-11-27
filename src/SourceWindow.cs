using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsIsland
{
    // Brings the app that plays the current song (Spotify, a browser, any player in the Windows media controls) to the front.
    // The media session only names the app by its AppUserModelID, so the window is found by matching that id against each
    // window's own id, its process's packaged id, or its executable name. Nothing is read beyond window ids and process names.
    static class SourceWindow
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out int value, int size);

        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref int length, StringBuilder id);

        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            int GetCount(out uint count);
            int GetAt(uint index, out PropertyKey key);
            int GetValue(ref PropertyKey key, out PropVariant value);
            int SetValue(ref PropertyKey key, ref PropVariant value);
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PropertyKey { public Guid FormatId; public uint PropertyId; }

        [StructLayout(LayoutKind.Sequential)]
        struct PropVariant { public ushort Type; public ushort R1, R2, R3; public IntPtr Value; public IntPtr Value2; }

        const uint ProcessQueryLimitedInformation = 0x1000;
        const ushort VtLpwstr = 31;
        static readonly Guid PropertyStoreId = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        static PropertyKey AppUserModelIdKey = new PropertyKey { FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), PropertyId = 5 };

        // Returns false when no window of the app was found, so nothing happened.
        public static bool Focus(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return false;
            try
            {
                IntPtr found = Find(appId);
                if (found != IntPtr.Zero)
                {
                    if (IsIconic(found)) ShowWindow(found, 9); // SW_RESTORE
                    SetForegroundWindow(found);
                    return true;
                }
                return Launch(appId);
            }
            catch (Exception)
            {
                return false; // The click simply does nothing
            }
        }

        // The first matching window in z-order, which is the one of that app used last.
        static IntPtr Find(string appId)
        {
            string exe = ExeName(appId);
            Dictionary<uint, bool> byProcess = new Dictionary<uint, bool>();
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!IsCandidate(h)) return true;
                if (string.Equals(WindowAppId(h), appId, StringComparison.OrdinalIgnoreCase)) { found = h; return false; }
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                bool matches;
                if (!byProcess.TryGetValue(pid, out matches))
                {
                    matches = ProcessMatches(pid, appId, exe);
                    byProcess[pid] = matches;
                }
                if (matches) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        // A visible, titled, unowned, not cloaked top-level window: what shows in Alt+Tab.
        static bool IsCandidate(IntPtr h)
        {
            if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero || GetWindowTextLength(h) == 0) return false; // 4 = GW_OWNER
            int cloaked;
            if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false; // 14 = DWMWA_CLOAKED
            return true;
        }

        // "Spotify.exe" -> "spotify", "C:\...\foo.exe" -> "foo", "Chrome" -> "chrome". Packaged ids ("Family!App") give "".
        static string ExeName(string appId)
        {
            if (appId.IndexOf('!') >= 0) return "";
            string name = appId;
            int slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
            if (slash >= 0) name = name.Substring(slash + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return name.ToLowerInvariant();
        }

        static bool ProcessMatches(uint pid, string appId, string exe)
        {
            if (appId.IndexOf('!') >= 0) return string.Equals(PackagedAppId(pid), appId, StringComparison.OrdinalIgnoreCase);
            if (exe.Length == 0) return false;
            try
            {
                using (Process process = Process.GetProcessById((int)pid))
                    return string.Equals(process.ProcessName, exe, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        static string PackagedAppId(uint pid)
        {
            IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (handle == IntPtr.Zero) return "";
            try
            {
                int length = 256;
                StringBuilder id = new StringBuilder(length);
                return GetApplicationUserModelId(handle, ref length, id) == 0 ? id.ToString() : "";
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        // The id a window sets for itself (Firefox and some players do), which is what the media session reports for them.
        static string WindowAppId(IntPtr h)
        {
            IPropertyStore store = null;
            try
            {
                Guid iid = PropertyStoreId;
                if (SHGetPropertyStoreForWindow(h, ref iid, out store) != 0 || store == null) return "";
                PropVariant value;
                if (store.GetValue(ref AppUserModelIdKey, out value) != 0) return "";
                try { return value.Type == VtLpwstr && value.Value != IntPtr.Zero ? Marshal.PtrToStringUni(value.Value) ?? "" : ""; }
                finally { PropVariantClear(ref value); }
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                if (store != null) Marshal.ReleaseComObject(store);
            }
        }

        // No window to show: Spotify closed to the tray hides its window, so the app is opened again, which brings the
        // running one back. Only for players that do that by themselves; a browser would open a new, empty window.
        static bool Launch(string appId)
        {
            string target = null;
            if (appId.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0) target = "spotify:";
            else if (appId.IndexOf('!') >= 0 && !IsBrowserId(appId)) target = "shell:AppsFolder\\" + appId;
            if (target == null) return false;
            using (Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })) { }
            return true;
        }

        static bool IsBrowserId(string appId)
        {
            string lower = appId.ToLowerInvariant();
            foreach (string browser in new[] { "chrome", "msedge", "edge", "brave", "opera", "firefox", "vivaldi", "arc" })
                if (lower.Contains(browser)) return true;
            return false;
        }
    }
}
