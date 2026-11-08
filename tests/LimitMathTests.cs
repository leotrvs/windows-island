using Xunit;

namespace WindowsIsland.Tests
{
    public class LimitMathTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(74, 0)]
        [InlineData(75, 75)]
        [InlineData(89, 75)]
        [InlineData(90, 90)]
        [InlineData(99, 90)]
        [InlineData(100, 100)]
        [InlineData(140, 100)]
        public void LevelIsTheHighestThresholdReached(int percent, int level)
        {
            Assert.Equal(level, LimitMath.Level(percent));
        }

        [Fact]
        public void TheFirstReadingIsNeverAnnounced()
        {
            Assert.Equal(-1, LimitMath.Announce(-1, 90));
        }

        [Fact]
        public void GoingUpAnnouncesTheNewLevel()
        {
            Assert.Equal(75, LimitMath.Announce(0, 75));
            Assert.Equal(90, LimitMath.Announce(75, 90));
            Assert.Equal(100, LimitMath.Announce(0, 100));
        }

        [Fact]
        public void StayingAtTheSameLevelAnnouncesNothing()
        {
            Assert.Equal(-1, LimitMath.Announce(90, 90));
        }

        [Fact]
        public void RenewingAfterAWarningIsAnnounced()
        {
            Assert.Equal(0, LimitMath.Announce(100, 0));
            Assert.Equal(0, LimitMath.Announce(75, 0));
        }

        [Fact]
        public void FallingButNotToZeroAnnouncesNothing()
        {
            Assert.Equal(-1, LimitMath.Announce(100, 75));
            Assert.Equal(-1, LimitMath.Announce(90, 75));
        }
    }
}
