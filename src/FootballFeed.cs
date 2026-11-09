using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsIsland
{
    // One match from a league's scoreboard, apart from the network so it can be tested.
    sealed class FootballMatch
    {
        public string Id = "", State = "", Clock = "";
        public string HomeName = "", HomeAbbr = "", AwayName = "", AwayAbbr = "";
        public int HomeScore, AwayScore;

        public bool Live { get { return State == "in"; } }
        public bool Finished { get { return State == "post"; } }
        public int Goals { get { return HomeScore + AwayScore; } }
        public string Line { get { return HomeAbbr + " " + HomeScore + " x " + AwayScore + " " + AwayAbbr; } }
    }

    static class FootballFeed
    {
        static readonly Regex LeagueCode = new Regex("^[a-z0-9][a-z0-9.\\-]{1,29}$", RegexOptions.CultureInvariant);

        // The league codes typed by the user ("bra.1, eng.1"): valid ones only, at most five, no repeats.
        public static List<string> Leagues(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (string part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string code = part.Trim().ToLowerInvariant();
                if (LeagueCode.IsMatch(code) && !result.Contains(code) && result.Count < 5) result.Add(code);
            }
            return result;
        }

        public static List<FootballMatch> Parse(string json)
        {
            List<FootballMatch> matches = new List<FootballMatch>();
            if (string.IsNullOrEmpty(json) || json.Length > 2 * 1024 * 1024) return matches;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement events;
                    if (!document.RootElement.TryGetProperty("events", out events) || events.ValueKind != JsonValueKind.Array) return matches;
                    foreach (JsonElement item in events.EnumerateArray())
                    {
                        FootballMatch match = One(item);
                        if (match != null) matches.Add(match);
                    }
                }
            }
            catch (Exception)
            {
                // Not what was expected: no matches
            }
            return matches;
        }

        static FootballMatch One(JsonElement item)
        {
            JsonElement competitions;
            if (!item.TryGetProperty("competitions", out competitions) || competitions.ValueKind != JsonValueKind.Array || competitions.GetArrayLength() == 0) return null;
            JsonElement competition = competitions[0];
            JsonElement teams;
            if (!competition.TryGetProperty("competitors", out teams) || teams.ValueKind != JsonValueKind.Array) return null;

            FootballMatch match = new FootballMatch { Id = Text(item, "id") };
            JsonElement status, type;
            if (item.TryGetProperty("status", out status))
            {
                match.Clock = Text(status, "displayClock");
                if (status.TryGetProperty("type", out type)) match.State = Text(type, "state");
            }
            bool home = false, away = false;
            foreach (JsonElement team in teams.EnumerateArray())
            {
                JsonElement info;
                if (!team.TryGetProperty("team", out info)) continue;
                int score;
                int.TryParse(Text(team, "score"), NumberStyles.None, CultureInfo.InvariantCulture, out score);
                string side = Text(team, "homeAway");
                if (side == "home") { match.HomeName = Text(info, "displayName"); match.HomeAbbr = Text(info, "abbreviation"); match.HomeScore = score; home = true; }
                else if (side == "away") { match.AwayName = Text(info, "displayName"); match.AwayAbbr = Text(info, "abbreviation"); match.AwayScore = score; away = true; }
            }
            return home && away && match.Id.Length > 0 ? match : null;
        }

        static string Text(JsonElement parent, string name)
        {
            JsonElement value;
            if (!parent.TryGetProperty(name, out value)) return "";
            return value.ValueKind == JsonValueKind.String ? (value.GetString() ?? "") : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "";
        }

        // Does the match have the team the user typed (a name or an abbreviation, any case, part of a name is enough)?
        public static bool Has(FootballMatch match, string team)
        {
            team = (team ?? "").Trim();
            if (team.Length < 2) return false;
            return Contains(match.HomeName, team) || Contains(match.AwayName, team) ||
                   string.Equals(match.HomeAbbr, team, StringComparison.OrdinalIgnoreCase) || string.Equals(match.AwayAbbr, team, StringComparison.OrdinalIgnoreCase);
        }

        static bool Contains(string name, string part)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(name ?? "", part, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}
