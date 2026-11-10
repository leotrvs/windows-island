using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Feeds the quick clipboard (ClipboardHistory). Like ScreenshotService it learns of a change from Windows
    // (AddClipboardFormatListener): it never polls, never watches the keyboard and never reads pictures or files, only plain
    // text. A copy that Windows or a password manager marks as private, or that a password manager makes, is not even read.
    // Everything stays in memory; nothing is written anywhere.
    sealed class ClipboardService : IDisposable
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;
        const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr handle);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr handle);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr handle);

        readonly ClipboardHistory history = new ClipboardHistory();
        readonly DispatcherTimer forgetTimer;
        HwndSource window;
        uint excludeFormat, viewerIgnoreFormat, historyFormat, cloudFormat;
        DateTime ownWriteAt = DateTime.MinValue;
        TimeSpan keep = TimeSpan.FromMinutes(5);

        public event Action Changed;

        public ClipboardService(Dispatcher dispatcher)
        {
            forgetTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            forgetTimer.Tick += (s, e) => Forget();
        }

        // Newest first
        public IReadOnlyList<ClipboardHistory.Item> Items { get { return history.Items; } }

        // How long a text is kept, from the setting (1, 5, 15 or 60 minutes)
        public void SetKeep(int minutes)
        {
            keep = TimeSpan.FromMinutes(Math.Max(1, minutes));
            Forget();
        }

        public void Start()
        {
            if (window != null) return;
            excludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
            viewerIgnoreFormat = RegisterClipboardFormat("Clipboard Viewer Ignore");
            historyFormat = RegisterClipboardFormat("CanIncludeInClipboardHistory");
            cloudFormat = RegisterClipboardFormat("CanUploadToCloudClipboard");
            HwndSourceParameters parameters = new HwndSourceParameters("WindowsIslandClipboardHistory") { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 0, Height = 0 };
            window = new HwndSource(parameters);
            window.AddHook(WndProc);
            AddClipboardFormatListener(window.Handle);
        }

        // Turned off (or the app is closing): stops listening and forgets everything.
        public void Stop()
        {
            if (window != null)
            {
                RemoveClipboardFormatListener(window.Handle);
                window.RemoveHook(WndProc);
                window.Dispose();
                window = null;
            }
            forgetTimer.Stop();
            if (history.Clear()) Changed?.Invoke();
        }

        public void Dispose() { Stop(); }

        // The "Clear" button
        public void Clear()
        {
            forgetTimer.Stop();
            if (history.Clear()) Changed?.Invoke();
        }

        // Puts a text from the list back on the clipboard, for the user to paste with Ctrl+V. Nothing is typed and no other
        // window is touched. Another program can hold the clipboard for a moment, so it is retried without freezing the island.
        public async Task<bool> CopyBack(string text)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    ownWriteAt = DateTime.UtcNow; // The change this causes is the island's own: it is not read back
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    ownWriteAt = DateTime.UtcNow;
                    if (history.Promote(text, DateTime.Now)) Changed?.Invoke();
                    return true;
                }
                catch (Exception)
                {
                }
                await Task.Delay(80);
            }
            return false;
        }

        IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_CLIPBOARDUPDATE) _ = ReadAsync();
            return IntPtr.Zero;
        }

        async Task ReadAsync()
        {
            try
            {
                uint sequence = GetClipboardSequenceNumber();
                if (DateTime.UtcNow - ownWriteAt < TimeSpan.FromSeconds(1)) return;
                if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return; // Pictures, files and the like are never read
                if (OwnerIgnored()) return;
                // The clipboard can be busy for a moment (the program that copied may still hold it): a few short tries
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (attempt > 0) await Task.Delay(30);
                    if (window == null) return; // Turned off meanwhile
                    if (!OpenClipboard(window.Handle)) continue;
                    string text = null;
                    try
                    {
                        if (GetClipboardSequenceNumber() != sequence) return; // Something newer came: its own notice reads it
                        text = ReadText();
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                    if (text != null && history.Add(text, DateTime.Now))
                    {
                        forgetTimer.Start();
                        Changed?.Invoke();
                    }
                    return;
                }
            }
            catch (Exception)
            {
                // The clipboard belongs to everyone; a failure here is ignored
            }
        }

        // The island's own copies (an item copied back, its API token, a problem report), and anything from a password manager.
        // When the owner cannot be told, the copy is skipped too.
        static bool OwnerIgnored()
        {
            IntPtr owner = GetClipboardOwner();
            if (owner == IntPtr.Zero) return false; // Copied without a window: nothing to tell by; the private formats still apply
            uint pid;
            if (GetWindowThreadProcessId(owner, out pid) == 0) return true;
            if (pid == (uint)Environment.ProcessId) return true;
            try
            {
                using (Process process = Process.GetProcessById((int)pid)) return ClipboardHistory.IsPasswordManager(process.ProcessName);
            }
            catch (Exception)
            {
                return true;
            }
        }

        // With the clipboard open: the text, unless the copy is marked private. Null when there is nothing to keep.
        string ReadText()
        {
            bool exclude = excludeFormat != 0 && IsClipboardFormatAvailable(excludeFormat);
            bool viewerIgnore = viewerIgnoreFormat != 0 && IsClipboardFormatAvailable(viewerIgnoreFormat);
            if (ClipboardHistory.Refused(exclude, viewerIgnore, ReadNumber(historyFormat), ReadNumber(cloudFormat))) return null;
            IntPtr handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;
            IntPtr data = GlobalLock(handle);
            if (data == IntPtr.Zero) return null;
            try
            {
                // Only up to one character past the limit is read: a longer text is refused without being copied whole
                long chars = (long)(GlobalSize(handle).ToUInt64() / 2);
                int take = (int)Math.Min(chars, ClipboardHistory.MaxLength + 1);
                if (take <= 0) return null;
                string text = Marshal.PtrToStringUni(data, take);
                int end = text.IndexOf('\0');
                if (end >= 0) text = text.Substring(0, end);
                return ClipboardHistory.Acceptable(text) ? text : null;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }

        // A DWORD format such as "CanIncludeInClipboardHistory", or null when it is not there. A format that is there but
        // cannot be read counts as 0 ("no"), so a private copy is never kept by mistake.
        static int? ReadNumber(uint format)
        {
            if (format == 0 || !IsClipboardFormatAvailable(format)) return null;
            IntPtr handle = GetClipboardData(format);
            if (handle == IntPtr.Zero) return 0;
            IntPtr data = GlobalLock(handle);
            if (data == IntPtr.Zero) return 0;
            try
            {
                return GlobalSize(handle).ToUInt64() >= 4 ? Marshal.ReadInt32(data) : 0;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }

        void Forget()
        {
            if (history.Expire(DateTime.Now, keep)) Changed?.Invoke();
            if (history.Items.Count == 0) forgetTimer.Stop();
        }
    }
}
