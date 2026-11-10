using System.Media;

namespace WindowsIsland
{
    // The sound for a timer that ends, a Pomodoro phase or a reminder: one of three Windows sounds, or none.
    static class TimerSounds
    {
        public static void Play(Settings settings)
        {
            Play(settings.TimerSoundChoice);
        }

        public static void Play(string choice)
        {
            switch (IslandRules.NormalizeSound(choice))
            {
                case "Chime": SystemSounds.Asterisk.Play(); break;
                case "Bell": SystemSounds.Exclamation.Play(); break;
                case "Beep": SystemSounds.Beep.Play(); break;
            }
        }
    }
}
