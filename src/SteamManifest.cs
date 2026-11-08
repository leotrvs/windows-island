using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WindowsIsland
{
    // What Steam keeps in a game's appmanifest_*.acf while it downloads or updates it, apart from any file so it can be tested.
    sealed class SteamManifest
    {
        public string AppId = "", Name = "";
        public long StateFlags, BytesToDownload, BytesDownloaded, BytesToStage, BytesStaged;

        // StateFlags bits that Steam sets while it works on a game
        const long UpdateRunning = 256, UpdatePaused = 512, UpdateStarted = 1024, Preallocating = 524288, Downloading = 1048576,
                   Staging = 2097152, Committing = 4194304, AddingFiles = 262144;
        const long Working = UpdateRunning | UpdateStarted | Preallocating | Downloading | Staging | Committing | AddingFiles;

        static readonly Regex Pair = new Regex("^\\s*\"(?<key>[^\"]+)\"\\s+\"(?<value>[^\"]*)\"\\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public bool IsWorking { get { return (StateFlags & Working) != 0 && (StateFlags & UpdatePaused) == 0; } }
        public bool IsPaused { get { return (StateFlags & Working) != 0 && (StateFlags & UpdatePaused) != 0; } }

        // The manifest itself says it runs (an old copy of the truth: Steam writes the file now and then, the log is faster)
        public bool IsRunning { get { return (StateFlags & UpdateRunning) != 0; } }

        // Downloading the data, or (after that) putting the files in place
        public bool IsDownloading { get { return BytesToDownload > 0 && BytesDownloaded < BytesToDownload; } }
        public long Total { get { return IsDownloading ? BytesToDownload : BytesToStage; } }
        public long Current { get { return IsDownloading ? BytesDownloaded : BytesStaged; } }

        // 0-100 while downloading, then while the downloaded files are put in place; -1 when it cannot tell.
        public int Progress
        {
            get { return Percent(Current, Total); }
        }

        public static int Percent(long current, long total)
        {
            return total > 0 ? (int)Math.Clamp(current * 100 / total, 0, 99) : -1;
        }

        // Steam writes the manifest only now and then, so between two writes the progress is carried on at the pace seen
        // between the last two, but never more than a twentieth of the whole ahead (a pause or a stall cannot run away).
        public static int Estimate(long current, long total, double bytesPerSecond, double secondsSinceWrite)
        {
            if (total <= 0) return -1;
            double ahead = Math.Max(0, bytesPerSecond) * Math.Max(0, secondsSinceWrite);
            ahead = Math.Min(ahead, total / 20.0);
            return Percent((long)Math.Min(current + ahead, total), total);
        }

        // The last state Steam logged for each game in content_log.txt ("AppID 252950 state changed : Update Required,...,").
        static readonly Regex StateLine = new Regex("AppID (?<id>\\d+) state changed : (?<state>[^\\r\\n]*)", RegexOptions.CultureInvariant);

        public static Dictionary<string, string> States(string log)
        {
            Dictionary<string, string> states = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(log)) return states;
            foreach (Match match in StateLine.Matches(log)) states[match.Groups["id"].Value] = match.Groups["state"].Value;
            return states;
        }

        public static bool StateIsRunning(string state)
        {
            return state != null && state.Contains("Update Running") && !state.Contains("Suspended");
        }

        // Installed with nothing left to do
        public static bool StateIsDone(string state)
        {
            return state != null && state.Contains("Fully Installed") && !state.Contains("Update Required") && !state.Contains("Update Queued") &&
                   !state.Contains("Update Running") && !state.Contains("Update Started");
        }

        // True after the download itself is done and the files are being put in place.
        public bool IsInstalling
        {
            get { return BytesToDownload > 0 && BytesDownloaded >= BytesToDownload; }
        }

        // Only the top level of the file matters (the nested depots are skipped by the key names we ask for).
        public static SteamManifest Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 256 * 1024) return null;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Pair.Matches(text))
            {
                string key = match.Groups["key"].Value;
                if (!values.ContainsKey(key)) values[key] = match.Groups["value"].Value;
            }
            if (!values.ContainsKey("appid")) return null;
            return new SteamManifest
            {
                AppId = values["appid"],
                Name = Get(values, "name"),
                StateFlags = Number(values, "StateFlags"),
                BytesToDownload = Number(values, "BytesToDownload"),
                BytesDownloaded = Number(values, "BytesDownloaded"),
                BytesToStage = Number(values, "BytesToStage"),
                BytesStaged = Number(values, "BytesStaged"),
            };
        }

        static string Get(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : "";
        }

        static long Number(Dictionary<string, string> values, string key)
        {
            long number;
            return long.TryParse(Get(values, key), NumberStyles.None, CultureInfo.InvariantCulture, out number) ? number : 0;
        }

        // The folders of the Steam libraries from libraryfolders.vdf (the lines "path"  "D:\\SteamLibrary").
        public static List<string> Libraries(string vdf)
        {
            List<string> paths = new List<string>();
            if (string.IsNullOrEmpty(vdf) || vdf.Length > 256 * 1024) return paths;
            foreach (Match match in Pair.Matches(vdf))
                if (string.Equals(match.Groups["key"].Value, "path", StringComparison.OrdinalIgnoreCase))
                {
                    string path = match.Groups["value"].Value.Replace("\\\\", "\\");
                    if (path.Length > 0 && !paths.Contains(path, StringComparer.OrdinalIgnoreCase)) paths.Add(path);
                }
            return paths;
        }
    }
}
