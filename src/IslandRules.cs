using System;

namespace WindowsIsland
{
    // Small rules with no window and no Windows services, so tests can run them.
    static class IslandRules
    {
        public static readonly string[] SoundChoices = { "None", "Chime", "Bell", "Beep" };

        // A choice written by hand or by a newer version falls back to the default.
        public static string NormalizeSound(string choice)
        {
            foreach (string known in SoundChoices)
                if (string.Equals(known, choice, StringComparison.OrdinalIgnoreCase)) return known;
            return "Chime";
        }

        // The old on/off switch (TimerSound) becomes a choice: off is "None", on keeps the choice.
        public static string MigrateSound(bool? oldSwitch, string choice)
        {
            if (oldSwitch == false) return "None";
            return NormalizeSound(choice);
        }

        // The index of the monitor (left, top, right, bottom in one shared space) that holds the point; -1 when there are none.
        // A point outside all of them (a gap between monitors) goes to the nearest one.
        public static int MonitorIndexAt(double[][] bounds, double x, double y)
        {
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < bounds.Length; i++)
            {
                double[] b = bounds[i];
                double dx = x < b[0] ? b[0] - x : x > b[2] ? x - b[2] : 0;
                double dy = y < b[1] ? b[1] - y : y > b[3] ? y - b[3] : 0;
                double distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        // The presentation mode only counts while the screen is being shared or recorded.
        public static bool Presenting(bool optionOn, bool screenShared)
        {
            return optionOn && screenShared;
        }

        // Which islands get a new alert: all of them, or only the one on the active monitor (the first when none matches).
        public static bool GetsAlert(bool activeOnly, bool isActive, bool anyActive)
        {
            return !activeOnly || isActive || !anyActive;
        }
    }
}
