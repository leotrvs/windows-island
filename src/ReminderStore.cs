using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowsIsland
{
    // The pending reminders, kept in a small JSON file on this PC (text and a UTC moment). Pure logic with an injectable
    // path so it can be tested; errors are swallowed (a reminder that cannot be saved must never break the island).
    sealed class ReminderStore
    {
        public const int MaxItems = 50;
        public const int MaxLength = 200;

        public sealed class Item
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public DateTime DueUtc { get; set; }
        }

        readonly string path;
        readonly List<Item> items = new List<Item>();

        public ReminderStore(string path)
        {
            this.path = path;
        }

        // Soonest first
        public IReadOnlyList<Item> Items { get { return items; } }

        public void Load()
        {
            items.Clear();
            try
            {
                if (!File.Exists(path)) return;
                List<Item> read = JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(path));
                if (read == null) return;
                foreach (Item item in read)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Text)) continue;
                    if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString("N");
                    item.DueUtc = DateTime.SpecifyKind(item.DueUtc, DateTimeKind.Utc);
                    if (item.Text.Length > MaxLength) item.Text = item.Text.Substring(0, MaxLength);
                    items.Add(item);
                }
                Sort();
                if (items.Count > MaxItems) items.RemoveRange(MaxItems, items.Count - MaxItems);
            }
            catch { items.Clear(); }
        }

        public Item Add(string text, DateTime dueUtc)
        {
            if (string.IsNullOrWhiteSpace(text) || items.Count >= MaxItems) return null;
            text = text.Trim();
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            Item item = new Item { Id = Guid.NewGuid().ToString("N"), Text = text, DueUtc = DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc) };
            items.Add(item);
            Sort();
            Save();
            return item;
        }

        public bool Remove(string id)
        {
            int removed = items.RemoveAll(i => i.Id == id);
            if (removed > 0) Save();
            return removed > 0;
        }

        // The reminders whose time has come, taken out of the list (and the file)
        public List<Item> TakeDue(DateTime nowUtc)
        {
            List<Item> due = items.Where(i => i.DueUtc <= nowUtc).ToList();
            if (due.Count == 0) return due;
            items.RemoveAll(i => i.DueUtc <= nowUtc);
            Save();
            return due;
        }

        void Sort()
        {
            List<Item> sorted = items.OrderBy(i => i.DueUtc).ToList();
            items.Clear();
            items.AddRange(sorted);
        }

        // Written to a temporary file, then swapped in, so a crash never leaves half a file
        void Save()
        {
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(items));
                File.Move(temp, path, true);
            }
            catch { }
        }
    }
}
