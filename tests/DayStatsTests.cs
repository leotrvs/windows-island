using System;
using System.IO;
using Xunit;

namespace WindowsIsland.Tests
{
    public class DayStatsTests
    {
        static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "day-stats-test-" + Guid.NewGuid().ToString("N") + ".json");
        }

        [Fact]
        public void OverlappingChatsCountTheTimeOnce()
        {
            DayStats stats = new DayStats(TempFile());
            DateTime t = DateTime.Now;
            stats.Tick(t, new[] { "a", "b" });
            stats.Tick(t.AddSeconds(1), new[] { "a", "b" });
            stats.Tick(t.AddSeconds(2), new[] { "a", "b" });
            Assert.Equal(2, stats.Sessions);
            Assert.Equal(2, (int)stats.Worked.TotalSeconds);
        }

        [Fact]
        public void IdleTimeAndLongGapsAreNotCounted()
        {
            DayStats stats = new DayStats(TempFile());
            DateTime t = DateTime.Now;
            stats.Tick(t, new string[0]);
            stats.Tick(t.AddSeconds(1), new string[0]);
            Assert.Equal(0, stats.Sessions);
            Assert.Equal(0, (int)stats.Worked.TotalSeconds);
            stats.Tick(t.AddSeconds(2), new[] { "a" });
            stats.Tick(t.AddMinutes(30), new[] { "a" }); // The computer slept
            Assert.True(stats.Worked.TotalSeconds <= 6);
        }

        [Fact]
        public void TheDayIsRememberedAcrossARestartAndStoresNoNames()
        {
            string file = TempFile();
            DayStats first = new DayStats(file);
            DateTime t = DateTime.Now;
            first.Tick(t, new[] { "secret-session-id" });
            first.Tick(t.AddSeconds(1), new[] { "secret-session-id" });
            first.Save(t);

            Assert.DoesNotContain("secret", File.ReadAllText(file));
            DayStats second = new DayStats(file);
            Assert.Equal(1, second.Sessions);
            Assert.Equal(1, (int)second.Worked.TotalSeconds);
            second.Tick(t.AddSeconds(2), new[] { "secret-session-id" }); // The same session is not counted again
            Assert.Equal(1, second.Sessions);
            File.Delete(file);
        }

        [Fact]
        public void AnotherDayStartsAgainAtZero()
        {
            DayStats stats = new DayStats(TempFile());
            DateTime t = DateTime.Now;
            stats.Tick(t, new[] { "a" });
            stats.Tick(t.AddSeconds(1), new[] { "a" });
            stats.Tick(t.AddDays(1), new string[0]);
            Assert.Equal(0, stats.Sessions);
            Assert.Equal(0, (int)stats.Worked.TotalSeconds);
        }
    }
}
