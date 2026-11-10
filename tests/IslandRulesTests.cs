using WindowsIsland;
using Xunit;

namespace WindowsIsland.Tests
{
    public class IslandRulesTests
    {
        [Fact]
        public void OldSoundOffBecomesNone() { Assert.Equal("None", IslandRules.MigrateSound(false, "Chime")); }

        [Fact]
        public void OldSoundOnKeepsChoice() { Assert.Equal("Bell", IslandRules.MigrateSound(true, "bell")); }

        [Fact]
        public void NoOldSwitchKeepsChoice() { Assert.Equal("Beep", IslandRules.MigrateSound(null, "Beep")); }

        [Fact]
        public void UnknownSoundFallsBackToChime()
        {
            Assert.Equal("Chime", IslandRules.NormalizeSound("Gong"));
            Assert.Equal("Chime", IslandRules.NormalizeSound(null));
        }

        static readonly double[][] Two = { new[] { 0.0, 0, 1920, 1080 }, new[] { 1920.0, 0, 3840, 1080 } };

        [Fact]
        public void PointPicksItsMonitor()
        {
            Assert.Equal(0, IslandRules.MonitorIndexAt(Two, 100, 100));
            Assert.Equal(1, IslandRules.MonitorIndexAt(Two, 2000, 500));
        }

        [Fact]
        public void PointOutsideGoesToNearest()
        {
            Assert.Equal(1, IslandRules.MonitorIndexAt(Two, 5000, 500));
            Assert.Equal(0, IslandRules.MonitorIndexAt(Two, -50, -50));
        }

        [Fact]
        public void NoMonitorsGivesMinusOne() { Assert.Equal(-1, IslandRules.MonitorIndexAt(new double[0][], 1, 1)); }

        [Fact]
        public void AlertsGoToActiveIslandOnly()
        {
            Assert.True(IslandRules.GetsAlert(false, false, true));
            Assert.True(IslandRules.GetsAlert(true, true, true));
            Assert.False(IslandRules.GetsAlert(true, false, true));
            Assert.True(IslandRules.GetsAlert(true, false, false)); // Nobody active: nobody is left out
        }

        [Fact]
        public void PresentingNeedsBoth()
        {
            Assert.True(IslandRules.Presenting(true, true));
            Assert.False(IslandRules.Presenting(true, false));
            Assert.False(IslandRules.Presenting(false, true));
        }
    }
}
