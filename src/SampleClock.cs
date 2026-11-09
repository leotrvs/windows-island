using System;

namespace WindowsIsland
{
    // The clock the live times (a chat's time so far, the timer) are read from. It is the real clock, except while the README
    // animations are drawn, when it is moved forward by hand so the times in them count up.
    static class SampleClock
    {
        public static TimeSpan Offset = TimeSpan.Zero;
        public static DateTime Now { get { return DateTime.Now + Offset; } }
        public static DateTime UtcNow { get { return DateTime.UtcNow + Offset; } }
    }
}
