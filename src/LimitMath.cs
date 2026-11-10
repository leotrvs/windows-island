using System;

namespace WindowsIsland
{
    // The rules of the limit alerts, apart from everything else so they can be tested.
    static class LimitMath
    {
        // 0 below 75%, then 75, 90 or 100.
        public static int Level(int percent)
        {
            return percent >= 100 ? 100 : percent >= 90 ? 90 : percent >= 75 ? 75 : 0;
        }

        // A limit that fell by 10 points or more to below 75% was renewed, whatever it had reached before.
        public static int Announce(int before, int level, int beforePercent, int percent)
        {
            int result = Announce(before, level);
            if (result < 0 && before >= 0 && level == 0 && beforePercent >= 0 && beforePercent - percent >= 10) return 0;
            return result;
        }

        // What to announce when a limit goes from the level it had (-1 = not read yet) to the one it has now:
        // the new level when it went up, 0 when it renewed after reaching 75% or more, -1 for nothing.
        public static int Announce(int before, int level)
        {
            if (before < 0 || level == before) return -1;
            if (level > before) return level;
            if (level == 0 && before >= 75) return 0;
            return -1;
        }

        // One reading of a limit: when it was taken and how much was used (0-100).
        public sealed class Sample
        {
            public DateTime Time;
            public double Percent;
        }

        const double MinimumPercent = 5;
        const int MinimumSamples = 3;
        const double MinimumSpanMinutes = 15;
        const double RecentWindowMinutes = 90;
        const double BurstMinutes = 6;
        const double ShortMinutes = 15;
        const double FarFromLimitPercent = 50;
        const double NearLimitPercent = 90;
        const double BurstWeightFar = 0.25;
        const double BurstWeightNear = 0.85;

        // When the limit would reach 100% at the current pace, but only if that happens before the window renews
        // (null when there is too little data, the pace is flat, or the window renews first). The pace blends the
        // average since the window began, the recent trend and the last few minutes; near the limit the last
        // minutes weigh most. Adapted from Claude Usage Taskbar.
        public static DateTime? Forecast(double percent, DateTime resetsAt, double windowHours, System.Collections.Generic.IList<Sample> samples, DateTime now)
        {
            DateTime start = resetsAt.AddHours(-windowHours);
            double elapsed = (now - start).TotalMinutes;
            double remaining = (resetsAt - now).TotalMinutes;
            if (percent < MinimumPercent || percent >= 100 || elapsed <= 0 || remaining <= 0) return null;

            System.Collections.Generic.List<Sample> inWindow = new System.Collections.Generic.List<Sample>();
            foreach (Sample s in samples) if (s.Time >= start && s.Time <= now) inWindow.Add(s);
            if (inWindow.Count < MinimumSamples) return null;
            if ((inWindow[inWindow.Count - 1].Time - inWindow[0].Time).TotalMinutes < MinimumSpanMinutes) return null;

            double average = percent / elapsed;
            DateTime recentStart = now.AddMinutes(-RecentWindowMinutes);
            System.Collections.Generic.List<Sample> recent = inWindow.FindAll(s => s.Time >= recentStart);
            double trend = recent.Count >= MinimumSamples ? Math.Max(0, Slope(recent)) : average;
            double steady = (average + trend) / 2;
            double burst = Math.Max(RateOver(inWindow, now, BurstMinutes), RateOver(inWindow, now, ShortMinutes));
            double closeness = Math.Max(0, Math.Min(1, (percent - FarFromLimitPercent) / (NearLimitPercent - FarFromLimitPercent)));
            double burstWeight = BurstWeightFar + (BurstWeightNear - BurstWeightFar) * closeness;
            double rate = steady * (1 - burstWeight) + burst * burstWeight;
            if (rate <= 0) return null;

            double minutes = (100 - percent) / rate;
            return minutes < remaining ? (DateTime?)now.AddMinutes(minutes) : null;
        }

        // A forecast is announced once per window: again only when the predicted time moved by at least the threshold.
        public static bool ShouldAnnounceForecast(DateTime? announced, DateTime predicted, TimeSpan threshold)
        {
            return !announced.HasValue || (predicted - announced.Value).Duration() >= threshold;
        }

        static double RateOver(System.Collections.Generic.List<Sample> samples, DateTime now, double minutes)
        {
            DateTime start = now.AddMinutes(-minutes);
            System.Collections.Generic.List<Sample> recent = samples.FindAll(s => s.Time >= start);
            if (recent.Count < 2) return 0;
            Sample first = recent[0];
            Sample last = recent[recent.Count - 1];
            double span = (last.Time - first.Time).TotalMinutes;
            return span <= 0 ? 0 : Math.Max(0, (last.Percent - first.Percent) / span);
        }

        // Least-squares slope, in percentage points per minute.
        static double Slope(System.Collections.Generic.List<Sample> samples)
        {
            DateTime origin = samples[0].Time;
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            foreach (Sample sample in samples)
            {
                double x = (sample.Time - origin).TotalMinutes;
                sumX += x;
                sumY += sample.Percent;
                sumXY += x * sample.Percent;
                sumXX += x * x;
            }
            double n = samples.Count;
            double denominator = n * sumXX - sumX * sumX;
            return denominator == 0 ? 0 : (n * sumXY - sumX * sumY) / denominator;
        }
    }
}
