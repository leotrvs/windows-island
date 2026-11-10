using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WindowsIsland
{
    // What Claude did today: how long at least one chat was working (overlapping chats count once) and how many different
    // sessions worked. Only numbers are kept on disk: the date, the seconds, and a short hash of each session id (so a
    // restart does not count the same session twice). No names, no paths. Starts again at local midnight.
    sealed class DayStats
    {
        static readonly TimeSpan LongestStep = TimeSpan.FromSeconds(5); // A pause (sleep, a stalled app) is not work time
        static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(30);

        readonly string path;
        readonly HashSet<string> ids = new HashSet<string>();
        string date;
        double seconds;
        DateTime lastTick = DateTime.MinValue;
        DateTime lastSave = DateTime.MinValue;
        bool dirty;
        bool sample;

        public DayStats(string path)
        {
            this.path = path;
            Load();
        }

        public int Sessions { get { return ids.Count; } }
        public TimeSpan Worked { get { return TimeSpan.FromSeconds(seconds); } }

        // For previews and documentation images.
        public void UseSample()
        {
            sample = true; // Never written to disk, never changed by real sessions
            date = DateTime.Now.ToString("yyyy-MM-dd");
            seconds = 2 * 3600 + 15 * 60;
            ids.Clear();
            for (int i = 0; i < 4; i++) ids.Add("sample" + i);
        }

        // Called about once a second with the ids of the sessions that are working right now.
        public void Tick(DateTime now, IEnumerable<string> busyIds)
        {
            if (sample) return;
            string today = now.ToString("yyyy-MM-dd");
            if (today != date)
            {
                date = today;
                seconds = 0;
                ids.Clear();
                dirty = true;
            }

            bool any = false;
            foreach (string id in busyIds)
            {
                any = true;
                if (ids.Add(Hash(id))) dirty = true;
            }
            if (any && lastTick != DateTime.MinValue)
            {
                TimeSpan step = now - lastTick;
                if (step > TimeSpan.Zero)
                {
                    seconds += Math.Min(step.TotalSeconds, LongestStep.TotalSeconds);
                    dirty = true;
                }
            }
            lastTick = now;

            if (dirty && now - lastSave >= SaveEvery) Save(now);
        }

        public void Save(DateTime now)
        {
            if (sample) return;
            dirty = false;
            lastSave = now;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonSerializer.Serialize(new Stored { Date = date, Seconds = (long)seconds, Ids = new List<string>(ids) });
                File.WriteAllText(path, json);
            }
            catch (Exception)
            {
                // Only a summary line: losing a write is not worth a message.
            }
        }

        sealed class Stored
        {
            public string Date { get; set; }
            public long Seconds { get; set; }
            public List<string> Ids { get; set; }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                Stored stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
                if (stored == null || stored.Date != DateTime.Now.ToString("yyyy-MM-dd")) return;
                date = stored.Date;
                seconds = Math.Max(0, stored.Seconds);
                if (stored.Ids != null) foreach (string id in stored.Ids) ids.Add(id);
            }
            catch (Exception)
            {
                // A damaged file just means a fresh day.
            }
        }

        static string Hash(string id)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(id ?? ""));
            return Convert.ToHexString(bytes, 0, 6);
        }
    }
}
