using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowsIsland
{
    // What OBS writes in its log when it starts and stops recording or streaming, apart from any file so it can be tested.
    // The lines look like "01:58:36.003: ==== Recording Start ====" (only the time of day, no date).
    static class ObsLog
    {
        static readonly Regex Marker = new Regex("^(?<h>\\d\\d):(?<m>\\d\\d):(?<s>\\d\\d)\\.\\d+: ==== (?<what>Recording|Streaming) (?<how>Start|Stop)", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        // The time of day each one started, or null when it is not running (its last marker is a Stop, or there is none).
        public static void Read(string log, out TimeSpan? recordingSince, out TimeSpan? streamingSince)
        {
            recordingSince = null;
            streamingSince = null;
            if (string.IsNullOrEmpty(log)) return;
            foreach (Match match in Marker.Matches(log))
            {
                TimeSpan at = new TimeSpan(int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture));
                TimeSpan? value = match.Groups["how"].Value == "Start" ? at : (TimeSpan?)null;
                if (match.Groups["what"].Value == "Recording") recordingSince = value;
                else streamingSince = value;
            }
        }

        // How long since a time of day, when it can be yesterday's evening.
        public static TimeSpan Since(TimeSpan start, TimeSpan now)
        {
            TimeSpan elapsed = now - start;
            return elapsed < TimeSpan.Zero ? elapsed + TimeSpan.FromDays(1) : elapsed;
        }

        public static string Clock(TimeSpan elapsed)
        {
            return elapsed.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", elapsed.Minutes, elapsed.Seconds);
        }
    }
}
