using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace WindowsIsland
{
    // Tells when a big file finishes downloading into the user's Downloads folder. It reads only names and sizes from the
    // folder (FileSystemWatcher), never the content of any file: to know a file is free it only tries to open it and closes it
    // again at once. The rules live in DownloadTracker. Completed fires on a background thread.
    sealed class DownloadWatcher : IDisposable
    {
        static readonly Guid DownloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

        readonly object gate = new object();
        readonly DownloadTracker tracker = new DownloadTracker();
        FileSystemWatcher watcher;
        Timer timer;

        public int MinMb = 50;
        public event Action<string, long> Completed; // The full path and the size

        // The real Downloads folder (it can be moved), not %USERPROFILE%\Downloads
        public static string DownloadsFolder()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out buffer) != 0 || buffer == IntPtr.Zero) return null;
                string path = Marshal.PtrToStringUni(buffer);
                return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;
            }
            catch { return null; }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer); }
        }

        public void Start()
        {
            if (watcher != null) return;
            try
            {
                string folder = DownloadsFolder();
                if (folder == null) return;
                watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    InternalBufferSize = 32 * 1024,
                };
                watcher.Created += (s, e) => Seen(e.FullPath);
                watcher.Changed += (s, e) => Seen(e.FullPath);
                watcher.Renamed += OnRenamed;
                watcher.Error += (s, e) => { };
                watcher.EnableRaisingEvents = true;
                timer = new Timer(_ => Check(), null, 2000, 2000);
            }
            catch { Stop(); }
        }

        public void Stop()
        {
            try { timer?.Dispose(); } catch { }
            timer = null;
            try { watcher?.Dispose(); } catch { }
            watcher = null;
        }

        public void Dispose() { Stop(); }

        void Seen(string path)
        {
            try
            {
                if (DownloadTracker.IsPartialName(path)) return;
                long size = SizeOf(path);
                if (size < 0) return;
                lock (gate) tracker.Changed(path, size, DateTime.UtcNow);
            }
            catch { }
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                bool finished;
                lock (gate) finished = tracker.Renamed(e.OldFullPath, e.FullPath, DateTime.UtcNow);
                if (!finished) { Seen(e.FullPath); return; }
                long size = SizeOf(e.FullPath);
                if (size >= 0 && DownloadTracker.MeetsMinimum(size, MinMb)) Completed?.Invoke(e.FullPath, size);
            }
            catch { }
        }

        void Check()
        {
            try
            {
                System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, long>> done;
                lock (gate)
                {
                    if (tracker.Count == 0) return;
                    done = tracker.Stable(DateTime.UtcNow, SizeOf, CanOpen);
                }
                foreach (System.Collections.Generic.KeyValuePair<string, long> file in done)
                    if (DownloadTracker.MeetsMinimum(file.Value, MinMb)) Completed?.Invoke(file.Key, file.Value);
            }
            catch { }
        }

        // The size from the folder entry; -1 for a missing file or a folder
        static long SizeOf(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || (info.Attributes & FileAttributes.Directory) != 0) return -1;
                return info.Length;
            }
            catch { return -1; }
        }

        // True when no program holds the file open any more. Nothing is read: the handle is closed right away.
        static bool CanOpen(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) return true;
            }
            catch { return false; }
        }

        // Opens Explorer with the file selected. Windows file names cannot contain a quote, so the quoted argument is safe.
        public static void Reveal(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || path.IndexOf('"') >= 0 || !Path.IsPathRooted(path) || !File.Exists(path)) return;
                ProcessStartInfo start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
                {
                    UseShellExecute = false,
                    Arguments = "/select,\"" + path + "\"",
                };
                Process.Start(start)?.Dispose();
            }
            catch { }
        }
    }
}
