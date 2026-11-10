using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowsIsland
{
    // Turns what the user types ("call John 3pm", "em 20 min tomar remédio", "завтра 9:00 встреча") into a text and a moment.
    // Pure logic, no window and no clock of its own: the caller gives "now". Understands English, Portuguese, Spanish and Russian.
    static class ReminderParser
    {
        public sealed class Result
        {
            public bool Ok;
            public string Text;   // What to be reminded of, without the time words
            public DateTime When; // Local time
        }

        const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // "em 20 min", "daqui a 2 horas", "in 20 minutes", "en 20 minutos", "через 20 минут", "in an hour"
        static readonly Regex Relative = new Regex(
            @"(?<![\p{L}\d])(?:daqui\s+a|dentro\s+de|em|in|en|через)\s+(?<n>\d{1,3}|um|uma|una|an|a|один|одну)?\s*(?<u>minutos?|minutes?|mins?|мин(?:уту|уты|ут)?|horas?|hours?|hrs?|h|час(?:а|ов)?)(?![\p{L}\d])", Opts);

        // The word for a day
        static readonly Regex DayWord = new Regex(@"(?<![\p{L}\d])(?<d>amanh[ãa]|tomorrow|ma[ñn]ana|завтра|hoje|today|hoy|сегодня)(?![\p{L}\d])", Opts);

        const string Prep = @"(?:(?<![\p{L}\d])(?:às|as|à|at|a\s+las?|a|в|во|к)\s+)?";

        static readonly Regex Colon = new Regex(Prep + @"(?<![\d:])(?<h>\d{1,2}):(?<m>\d{2})(?![\d:])(?:\s*(?<ap>am|pm)(?![\p{L}]))?", Opts);
        static readonly Regex HourH = new Regex(Prep + @"(?<![\d:])(?<h>\d{1,2})\s?(?:h(?:rs?|s)?|час(?:а|ов)?)(?:(?<m>\d{2})(?!\d)|(?![\p{L}\d]))", Opts);
        static readonly Regex AmPm = new Regex(Prep + @"(?<![\d:])(?<h>\d{1,2})\s*(?<ap>a\.?m\.?|p\.?m\.?)(?![\p{L}])", Opts);
        static readonly Regex Bare = new Regex(@"(?<![\p{L}\d])(?:às|as|à|at|a\s+las?|в|к)\s+(?<h>\d{1,2})(?![\d:\p{L}])", Opts);

        // "remind me to", "lembre-me de"...: not part of what is to be remembered
        static readonly Regex Lead = new Regex(@"^\s*(?:remind\s+me\s+(?:to\s+)?|me\s+lembre\s+(?:de\s+)?|lembre-me\s+(?:de\s+)?|lembrar\s+(?:de\s+)?|recu[ée]rdame\s+(?:que\s+)?|recordarme\s+|напомни(?:\s+мне)?\s+)", Opts);

        public static Result Parse(string input, DateTime now)
        {
            Result fail = new Result();
            if (string.IsNullOrWhiteSpace(input)) return fail;
            string text = input.Trim();

            Match rel = Relative.Match(text);
            if (rel.Success)
            {
                string n = rel.Groups["n"].Value;
                int amount = 1;
                if (n.Length > 0 && char.IsDigit(n[0])) amount = int.Parse(n, CultureInfo.InvariantCulture);
                else if (n.Length == 0) amount = 0; // "em min": no number, no article
                string unit = rel.Groups["u"].Value.ToLowerInvariant();
                bool minutes = unit.StartsWith("min", StringComparison.Ordinal) || unit.StartsWith("мин", StringComparison.Ordinal);
                if (amount > 0 && amount <= (minutes ? 24 * 60 : 24 * 7))
                {
                    DateTime when = minutes ? now.AddMinutes(amount) : now.AddHours(amount);
                    return Done(text, rel.Index, rel.Length, null, when);
                }
            }

            Match day = DayWord.Match(text);
            int addDays = 0;
            if (day.Success)
            {
                string d = day.Groups["d"].Value.ToLowerInvariant();
                addDays = d == "hoje" || d == "today" || d == "hoy" || d == "сегодня" ? 0 : 1;
            }

            foreach (Regex pattern in new[] { Colon, HourH, AmPm, Bare })
            {
                foreach (Match m in pattern.Matches(text))
                {
                    int hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
                    int minute = m.Groups["m"].Success && m.Groups["m"].Length > 0 ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
                    string ap = m.Groups["ap"].Success ? m.Groups["ap"].Value.Replace(".", "").ToLowerInvariant() : "";
                    if (minute > 59) continue;
                    if (ap.Length > 0)
                    {
                        if (hour < 1 || hour > 12) continue;
                        if (ap == "pm" && hour < 12) hour += 12;
                        else if (ap == "am" && hour == 12) hour = 0;
                    }
                    else if (hour > 23) continue;

                    DateTime date = now.Date.AddDays(addDays).AddHours(hour).AddMinutes(minute);
                    if (!day.Success || addDays == 0) { if (date <= now) date = date.AddDays(1); } // A time already gone today means tomorrow
                    return Done(text, m.Index, m.Length, day.Success ? day : null, date);
                }
            }
            return fail;
        }

        static Result Done(string text, int index, int length, Match day, DateTime when)
        {
            // Cut the time and the day word out (the later one first so the indexes stay right)
            string rest = text;
            if (day != null && day.Index > index) { rest = rest.Remove(day.Index, day.Length); rest = rest.Remove(index, length); }
            else if (day != null) { rest = rest.Remove(index, length); rest = rest.Remove(day.Index, day.Length); }
            else rest = rest.Remove(index, length);
            return new Result { Ok = true, Text = Clean(rest, text), When = when };
        }

        static string Clean(string rest, string original)
        {
            rest = Lead.Replace(rest, "");
            rest = Regex.Replace(rest, @"\s+", " ");
            rest = Regex.Replace(rest, @"\s+([,;.])", "$1").Trim();
            rest = rest.Trim(' ', ',', '-', '–', '—', ':', ';', '.');
            // A preposition left hanging at either end ("ligar pro João às")
            rest = Regex.Replace(rest, @"(?:^|\s)(?:às|as|à|at|em|in|en|в|во|к)$", "", Opts).Trim(' ', ',', '-', ':', ';');
            return rest.Length > 0 ? rest : original.Trim();
        }
    }
}
