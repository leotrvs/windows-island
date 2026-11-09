using System;
using Xunit;

namespace WindowsIsland.Tests
{
    public class ObsLogTests
    {
        [Fact]
        public void ARecordingThatStartedAndHasNotStoppedIsRunning()
        {
            string log = "01:58:22.396: ==== Startup complete ====\n03:10:00.100: ==== Recording Start ====\n03:11:00.000: something else\n";
            TimeSpan? rec, live;
            ObsLog.Read(log, out rec, out live);
            Assert.Equal(new TimeSpan(3, 10, 0), rec);
            Assert.Null(live);
        }

        [Fact]
        public void AStopAfterTheStartEndsIt()
        {
            string log = "03:10:00.100: ==== Recording Start ====\n03:20:00.100: ==== Recording Stop ====\n04:00:00.000: ==== Streaming Start ====\n";
            TimeSpan? rec, live;
            ObsLog.Read(log, out rec, out live);
            Assert.Null(rec);
            Assert.Equal(new TimeSpan(4, 0, 0), live);
        }

        [Fact]
        public void TheReplayBufferIsNotARecording()
        {
            TimeSpan? rec, live;
            ObsLog.Read("01:58:36.003: ==== Replay Buffer Start ====\n", out rec, out live);
            Assert.Null(rec);
            Assert.Null(live);
        }

        [Fact]
        public void TheGameBarRecordingStartIsReadFromItsName()
        {
            Assert.Equal(new DateTime(2026, 11, 8, 23, 40, 41), ObsLog.StartFromName("Claude 2026-11-08 23-40-41.mp4"));
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), ObsLog.StartFromName("My game 2026-01-02 03-04-05.MP4"));
            Assert.Null(ObsLog.StartFromName("holiday.mp4"));
            Assert.Null(ObsLog.StartFromName("x 2026-11-08 25-00-00.mp4"));
            Assert.Null(ObsLog.StartFromName(null));
        }

        [Fact]
        public void TimeSinceCrossesMidnightAndIsWrittenAsAClock()
        {
            Assert.Equal(TimeSpan.FromMinutes(90), ObsLog.Since(new TimeSpan(23, 30, 0), new TimeSpan(1, 0, 0)));
            Assert.Equal("1:05", ObsLog.Clock(TimeSpan.FromSeconds(65)));
            Assert.Equal("1:02:03", ObsLog.Clock(new TimeSpan(1, 2, 3)));
        }
    }
}
