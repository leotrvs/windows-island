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

        // What to announce when a limit goes from the level it had (-1 = not read yet) to the one it has now:
        // the new level when it went up, 0 when it renewed after reaching 75% or more, -1 for nothing.
        public static int Announce(int before, int level)
        {
            if (before < 0 || level == before) return -1;
            if (level > before) return level;
            if (level == 0 && before >= 75) return 0;
            return -1;
        }
    }
}
