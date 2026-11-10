using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowsIsland
{
    // The rules for "a download finished", as pure logic: names, sizes and time only, never the content of a file.
    // DownloadWatcher feeds it what the folder reports.
    sealed class DownloadTracker
    {
        public const int StableSeconds = 3;
        const int MaxTracked = 200;

        static readonly string[] Partial = { ".crdownload", ".part", ".opdownload", ".download", ".tmp", ".partial" };

        sealed class Entry
        {
            public long Size;
            public DateTime Changed;   // When the size last changed
            public bool Grew;          // The size changed after it was first seen: the file is being written
        }

        readonly Dictionary<string, Entry> tracked = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> reported = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public int Count { get { return tracked.Count; } }

        // A name the browser uses while it still downloads
        public static bool IsPartialName(string path)
        {
            string ext = Path.GetExtension(path ?? "");
            return Array.Exists(Partial, p => string.Equals(p, ext, StringComparison.OrdinalIgnoreCase));
        }

        public static bool MeetsMinimum(long bytes, int megabytes)
        {
            return bytes >= (long)megabytes * 1024 * 1024;
        }

        // "230 MB", "1.4 GB", "850 KB"
        public static string FormatSize(long bytes)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", c) + " GB";
            if (bytes >= 1024L * 1024) return Math.Round(bytes / (1024.0 * 1024)).ToString("0", c) + " MB";
            if (bytes >= 1024) return Math.Round(bytes / 1024.0).ToString("0", c) + " KB";
            return bytes + " B";
        }

        // A long name cut in the middle, the extension kept: "very-long-installer-na….exe"
        public static string ShortName(string name, int max)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= max) return name ?? "";
            string ext = Path.GetExtension(name);
            if (ext.Length > 8) ext = "";
            int keep = Math.Max(1, max - ext.Length - 1);
            return name.Substring(0, keep) + "…" + ext;
        }

        // A file was renamed. Returns true when this is a browser finishing: a partial name became the final one.
        public bool Renamed(string oldPath, string newPath, DateTime now)
        {
            tracked.Remove(oldPath ?? "");
            if (string.IsNullOrEmpty(newPath) || IsPartialName(newPath)) return false;
            if (!IsPartialName(oldPath)) return false;
            return MarkReported(newPath, now);
        }

        // A file appeared or changed: its size, now
        public void Changed(string path, long size, DateTime now)
        {
            if (string.IsNullOrEmpty(path) || IsPartialName(path) || size < 0) return;
            DateTime before;
            if (reported.TryGetValue(path, out before) && (now - before).TotalMinutes <= 10) return;
            Entry entry;
            if (!tracked.TryGetValue(path, out entry))
            {
                if (tracked.Count >= MaxTracked) Drop(now);
                tracked[path] = new Entry { Size = size, Changed = now };
                return;
            }
            if (size != entry.Size) { entry.Size = size; entry.Changed = now; entry.Grew = true; }
        }

        // The files that grew and then stopped for StableSeconds. sizeOf gives the current size (-1: gone), canOpen says
        // whether nobody else holds the file any more. A file reported once is not reported again.
        public List<KeyValuePair<string, long>> Stable(DateTime now, Func<string, long> sizeOf, Func<string, bool> canOpen)
        {
            List<KeyValuePair<string, long>> done = new List<KeyValuePair<string, long>>();
            foreach (string path in new List<string>(tracked.Keys))
            {
                Entry entry = tracked[path];
                long size = sizeOf(path);
                if (size < 0) { tracked.Remove(path); continue; }
                if (size != entry.Size) { entry.Size = size; entry.Changed = now; entry.Grew = true; continue; }
                if ((now - entry.Changed).TotalSeconds >= 600) { tracked.Remove(path); continue; } // Locked for ages: let it go
                if (!entry.Grew || size <= 0 || (now - entry.Changed).TotalSeconds < StableSeconds) continue;
                if (!canOpen(path)) continue; // Still held by the program that writes it
                tracked.Remove(path);
                if (MarkReported(path, now)) done.Add(new KeyValuePair<string, long>(path, size));
            }
            return done;
        }

        // False when this file was already reported lately (the same name again within ten minutes)
        bool MarkReported(string path, DateTime now)
        {
            foreach (string key in new List<string>(reported.Keys))
                if ((now - reported[key]).TotalMinutes > 10) reported.Remove(key);
            if (reported.ContainsKey(path)) return false;
            reported[path] = now;
            return true;
        }

        void Drop(DateTime now)
        {
            string oldest = null;
            DateTime when = DateTime.MaxValue;
            foreach (KeyValuePair<string, Entry> pair in tracked)
                if (pair.Value.Changed < when) { when = pair.Value.Changed; oldest = pair.Key; }
            if (oldest != null) tracked.Remove(oldest);
        }
    }
}
