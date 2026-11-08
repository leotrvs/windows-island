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

        // 0-100 while downloading, then while the downloaded files are put in place; -1 when it cannot tell.
        public int Progress
        {
            get
            {
                if (BytesToDownload > 0 && BytesDownloaded < BytesToDownload)
                    return (int)Math.Clamp(BytesDownloaded * 100 / BytesToDownload, 0, 99);
                if (BytesToStage > 0)
                    return (int)Math.Clamp(BytesStaged * 100 / BytesToStage, 0, 99);
                return -1;
            }
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
