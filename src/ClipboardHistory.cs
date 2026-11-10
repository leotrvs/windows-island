using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsIsland
{
    // The quick clipboard: the last few texts copied, so one can be copied again with a click. What is copied can be a
    // password, so the list lives in memory only: it is never written to disk, a log, the settings, a problem report or the
    // local API, it forgets each text after a while, and it is emptied when the option is turned off or the app closes.
    // This part has no window and no Windows calls, so what is kept, what is refused and when it is forgotten can be tested.
    // ClipboardService reads the clipboard and feeds it.
    sealed class ClipboardHistory
    {
        public const int MaxItems = 5;
        public const int MaxLength = 4000;  // Longer texts are not kept
        public const int LineLength = 48;   // What one line on the island shows
        public const string Masked = "••••••"; // Shown instead of the text while the screen is shared
        public static readonly int[] ForgetMinutes = { 1, 5, 15, 60 };

        // Password managers, by the start of their process name (lower case, without spaces, dashes, dots or underscores).
        // Whatever they put on the clipboard is left alone, even when they do not mark it as private.
        static readonly string[] PasswordManagers =
        {
            "1password", "bitwarden", "keepass", "lastpass", "dashlane", "nordpass", "protonpass", "enpass", "roboform", "robotaskbaricon",
        };

        public sealed class Item
        {
            public Item(string text, DateTime copied)
            {
                Text = text;
                Copied = copied;
            }

            public string Text { get; }
            public DateTime Copied { get; internal set; }
        }

        readonly List<Item> items = new List<Item>();

        // Newest first
        public IReadOnlyList<Item> Items { get { return items; } }

        // A text that was just copied. Returns true when the list changed.
        public bool Add(string text, DateTime now)
        {
            if (!Acceptable(text)) return false;
            if (items.Count > 0 && items[0].Text == text) return false; // The same as the newest one
            int found = items.FindIndex(i => i.Text == text);
            if (found >= 0) items.RemoveAt(found); // Copied again: it moves up instead of showing twice
            items.Insert(0, new Item(text, now));
            while (items.Count > MaxItems) items.RemoveAt(items.Count - 1);
            return true;
        }

        // A text the island put back on the clipboard: it moves to the top (and its time starts again), never twice.
        public bool Promote(string text, DateTime now)
        {
            int found = items.FindIndex(i => i.Text == text);
            if (found < 0) return false;
            Item item = items[found];
            items.RemoveAt(found);
            item.Copied = now;
            items.Insert(0, item);
            return true;
        }

        // Forgets the texts copied longer ago than "keep". Returns true when any was forgotten.
        public bool Expire(DateTime now, TimeSpan keep)
        {
            return items.RemoveAll(i => now - i.Copied >= keep) > 0;
        }

        public bool Clear()
        {
            if (items.Count == 0) return false;
            items.Clear();
            return true;
        }

        // Empty, only spaces, or too long: not kept.
        public static bool Acceptable(string text)
        {
            return text != null && text.Length <= MaxLength && !string.IsNullOrWhiteSpace(text);
        }

        // Windows and password managers mark private copies with registered clipboard formats: the copy is left alone when
        // "ExcludeClipboardContentFromMonitorProcessing" (or the older "Clipboard Viewer Ignore") is there, or when
        // "CanIncludeInClipboardHistory" or "CanUploadToCloudClipboard" holds 0. A null value means the format is not there.
        public static bool Refused(bool excludeFromMonitors, bool viewerIgnore, int? canIncludeInHistory, int? canUploadToCloud)
        {
            return excludeFromMonitors || viewerIgnore || canIncludeInHistory == 0 || canUploadToCloud == 0;
        }

        public static bool IsPasswordManager(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            StringBuilder key = new StringBuilder(processName.Length);
            foreach (char c in processName.ToLowerInvariant())
                if (c != ' ' && c != '-' && c != '_' && c != '.') key.Append(c);
            string name = key.ToString();
            foreach (string manager in PasswordManagers)
                if (name.StartsWith(manager, StringComparison.Ordinal)) return true;
            return false;
        }

        // One line for the island: line breaks and tabs become one space, and a long text ends in "…".
        public static string OneLine(string text, int max = LineLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder line = new StringBuilder(Math.Min(text.Length, max + 8));
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && line.Length > 0) line.Append(' ');
                space = false;
                line.Append(c);
                if (line.Length > max + 1) break; // Enough to know it must be cut
            }
            if (line.Length <= max) return line.ToString();
            int cut = max - 1;
            if (char.IsHighSurrogate(line[cut - 1])) cut--; // Never half of an emoji
            return line.ToString(0, cut).TrimEnd() + "…";
        }
    }
}
