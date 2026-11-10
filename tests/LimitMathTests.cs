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
        public void RenewingFromBelowTheFirstLevelIsAnnouncedToo()
        {
            Assert.Equal(0, LimitMath.Announce(0, 0, 73, 0));
            Assert.Equal(-1, LimitMath.Announce(0, 0, 5, 0));
            Assert.Equal(-1, LimitMath.Announce(-1, 0, -1, 0));
        }

        [Fact]
        public void FallingButNotToZeroAnnouncesNothing()
        {
            Assert.Equal(-1, LimitMath.Announce(100, 75));
            Assert.Equal(-1, LimitMath.Announce(90, 75));
        }

        static readonly System.DateTime Now = new System.DateTime(2026, 11, 10, 15, 0, 0);

        // Readings every 5 minutes, ending now, climbing from "from" to "to" percent.
        static System.Collections.Generic.List<LimitMath.Sample> Climb(double from, double to, int minutes)
        {
            var list = new System.Collections.Generic.List<LimitMath.Sample>();
            for (int m = minutes; m >= 0; m -= 5)
                list.Add(new LimitMath.Sample { Time = Now.AddMinutes(-m), Percent = from + (to - from) * (minutes - m) / minutes });
            return list;
        }

        [Fact]
        public void AFastPaceForecastsTheLimitBeforeTheReset()
        {
            // 2 hours into the window at 60%, rising 0.5 point per minute: 40 points left = 80 minutes.
            var samples = Climb(0, 60, 120);
            System.DateTime? when = LimitMath.Forecast(60, Now.AddHours(3), 5, samples, Now);
            Assert.True(when.HasValue);
            Assert.InRange((when.Value - Now).TotalMinutes, 40, 100);
        }

        [Fact]
        public void ASlowPaceThatOutlastsTheWindowForecastsNothing()
        {
            var samples = Climb(0, 20, 120);
            Assert.Null(LimitMath.Forecast(20, Now.AddHours(3), 5, samples, Now));
        }

        [Fact]
        public void TooLittleDataForecastsNothing()
        {
            var few = new System.Collections.Generic.List<LimitMath.Sample>
            {
                new LimitMath.Sample { Time = Now.AddMinutes(-2), Percent = 50 },
                new LimitMath.Sample { Time = Now, Percent = 60 },
            };
            Assert.Null(LimitMath.Forecast(60, Now.AddHours(3), 5, few, Now));
            Assert.Null(LimitMath.Forecast(3, Now.AddHours(3), 5, Climb(0, 3, 120), Now)); // under 5%
            Assert.Null(LimitMath.Forecast(60, Now.AddHours(3), 5, Climb(50, 60, 10), Now)); // under 15 minutes of readings
        }

        [Fact]
        public void AFlatPaceOrAFullLimitForecastsNothing()
        {
            var flat = Climb(40, 40, 120);
            Assert.Null(LimitMath.Forecast(40, Now.AddHours(3), 5, flat, Now));
            Assert.Null(LimitMath.Forecast(100, Now.AddHours(3), 5, Climb(0, 100, 120), Now));
        }

        [Fact]
        public void TheWeeklyWindowUsesItsOwnLength()
        {
            // 3 days into the week at 70%, still rising: the limit comes before the reset 4 days away.
            var samples = new System.Collections.Generic.List<LimitMath.Sample>();
            for (int m = 180; m >= 0; m -= 30)
                samples.Add(new LimitMath.Sample { Time = Now.AddMinutes(-m), Percent = 66 + 4.0 * (180 - m) / 180 });
            System.DateTime? when = LimitMath.Forecast(70, Now.AddDays(4), 168, samples, Now);
            Assert.True(when.HasValue);
            Assert.True(when.Value < Now.AddDays(4));
        }

        [Fact]
        public void AForecastIsAnnouncedOnceUntilItMovesALot()
        {
            System.TimeSpan threshold = System.TimeSpan.FromMinutes(60);
            System.DateTime at = Now.AddHours(2);
            Assert.True(LimitMath.ShouldAnnounceForecast(null, at, threshold));
            Assert.False(LimitMath.ShouldAnnounceForecast(at, at.AddMinutes(20), threshold));
            Assert.False(LimitMath.ShouldAnnounceForecast(at, at.AddMinutes(-59), threshold));
            Assert.True(LimitMath.ShouldAnnounceForecast(at, at.AddMinutes(-60), threshold));
            Assert.True(LimitMath.ShouldAnnounceForecast(at, at.AddHours(2), threshold));
        }
    }
}
