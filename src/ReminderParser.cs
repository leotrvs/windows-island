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

        // The word for a day ("depois de amanhã" before "amanhã", so the longer one wins)
        static readonly Regex DayWord = new Regex(@"(?<![\p{L}\d])(?<d>depois\s+de\s+amanh[ãa]|(?:the\s+)?day\s+after\s+tomorrow|pasado\s+ma[ñn]ana|послезавтра|amanh[ãa]|tomorrow|ma[ñn]ana|завтра|hoje|today|hoy|сегодня)(?![\p{L}\d])", Opts);

        // A day of the week, with the preposition that goes with it ("na sexta", "on friday", "el viernes", "в пятницу")
        static readonly Regex Weekday = new Regex(@"(?<![\p{L}\d])(?:(?:na|no|on|el|в|во)\s+)?(?<w>segunda|ter[çc]a|quarta|quinta|sexta|s[áa]bado|domingo|monday|tuesday|wednesday|thursday|friday|saturday|sunday|lunes|martes|mi[ée]rcoles|jueves|viernes|понедельник|вторник|сред[ау]|четверг|пятниц[ау]|суббот[ау]|воскресенье)(?:-feira)?(?![\p{L}\d])(?!\s+(?:via|vez|parte|temporada|chance|op[çc][ãa]o|m[ãa]o|s[ée]rie|edi[çc][ãa]o)(?![\p{L}]))", Opts);

        // A date: "28/11", "28/11/2026", "dia 28/11" (day first, as the list shows it)
        static readonly Regex DateNum = new Regex(@"(?<![\p{L}\d/])(?:(?:dia|day|el|on)\s+)?(?<d>\d{1,2})/(?<mo>\d{1,2})(?:/(?<y>\d{2}|\d{4}))?(?![\d/])", Opts);

        const int DefaultHour = 9; // A day without a time ("amanhã reunião", "sexta dentista"): in the morning

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
                    return Done(text, rel.Index, rel.Length, when);
                }
            }

            // The day: a word ("amanhã"), a weekday ("sexta") or a date ("28/11"). It is blanked out (same length, so the
            // indexes stay right) before looking for the time, so "28/11" is never read as a time.
            Match day = null;
            DateTime? date = null;
            bool explicitDay = false; // A day other than today was named: a time already gone is not pushed to tomorrow
            Match word = DayWord.Match(text);
            Match weekday = Weekday.Match(text);
            Match numeric = DateNum.Match(text);
            if (word.Success)
            {
                string d = word.Groups["d"].Value.ToLowerInvariant();
                int add = d == "hoje" || d == "today" || d == "hoy" || d == "сегодня" ? 0
                    : d.StartsWith("depois", StringComparison.Ordinal) || d.Contains("after") || d.StartsWith("pasado", StringComparison.Ordinal) || d == "послезавтра" ? 2 : 1;
                day = word; date = now.Date.AddDays(add); explicitDay = add > 0;
            }
            else if (numeric.Success)
            {
                if (!TryDate(numeric, now, out DateTime on)) return fail; // "31/02", or a day already gone: not today at that hour
                day = numeric; date = on; explicitDay = on > now.Date;
            }
            else if (weekday.Success)
            {
                int target = WeekdayNumber(weekday.Groups["w"].Value.ToLowerInvariant());
                int ahead = ((target - (int)now.DayOfWeek) + 7) % 7;
                day = weekday; date = now.Date.AddDays(ahead); explicitDay = ahead > 0;
            }

            string search = day == null ? text : text.Substring(0, day.Index) + new string(' ', day.Length) + text.Substring(day.Index + day.Length);
            foreach (Regex pattern in new[] { Colon, HourH, AmPm, Bare })
            {
                foreach (Match m in pattern.Matches(search))
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

                    DateTime when = (date ?? now.Date).AddHours(hour).AddMinutes(minute);
                    if (when <= now)
                    {
                        if (!explicitDay && (day == null || day == word)) when = when.AddDays(1);      // A time already gone today means tomorrow
                        else if (day == weekday) when = when.AddDays(7);                                // "sexta 10h" on a Friday at 11: next Friday
                        else continue;                                                                  // A date in the past: not a reminder
                    }
                    return Done(search, m.Index, m.Length, when, text);
                }
            }

            // A day was named but no time: in the morning of that day (not today: "hoje" alone says nothing)
            if (day != null && explicitDay) return Done(search, 0, 0, date.Value.AddHours(DefaultHour), text);
            return fail;
        }

        static bool TryDate(Match m, DateTime now, out DateTime date)
        {
            date = default;
            int d = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
            int mo = int.Parse(m.Groups["mo"].Value, CultureInfo.InvariantCulture);
            int y = now.Year;
            bool yearGiven = m.Groups["y"].Success && m.Groups["y"].Length > 0;
            if (yearGiven) { y = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture); if (y < 100) y += 2000; }
            if (mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo)) return false;
            date = new DateTime(y, mo, d);
            if (!yearGiven && date < now.Date) // "05/01" in November: next January
            {
                if (d > DateTime.DaysInMonth(y + 1, mo)) return false;
                date = new DateTime(y + 1, mo, d);
            }
            return date >= now.Date;
        }

        static int WeekdayNumber(string w)
        {
            if (w.StartsWith("seg") || w.StartsWith("mon") || w.StartsWith("lun") || w.StartsWith("пон")) return 1;
            if (w.StartsWith("ter") || w.StartsWith("tue") || w.StartsWith("mar") || w.StartsWith("вто")) return 2;
            if (w.StartsWith("qua") || w.StartsWith("wed") || w.StartsWith("mi") || w.StartsWith("сре")) return 3;
            if (w.StartsWith("qui") || w.StartsWith("thu") || w.StartsWith("jue") || w.StartsWith("чет")) return 4;
            if (w.StartsWith("sex") || w.StartsWith("fri") || w.StartsWith("vie") || w.StartsWith("пят")) return 5;
            if (w.StartsWith("s") && w.Length > 1 && (w[1] == 'á' || w[1] == 'a') || w.StartsWith("суб")) return 6;
            return 0; // domingo, sunday, воскресенье
        }

        static Result Done(string text, int index, int length, DateTime when, string original = null)
        {
            string rest = text.Remove(index, length); // The day was already blanked out by the caller
            return new Result { Ok = true, Text = Clean(rest, original ?? text), When = when };
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
