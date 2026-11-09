using System;
using System.Globalization;

namespace WindowsIsland
{
    // What is new in the latest release, in a few lines, for the card at the top of Settings. The full history is the changelog.
    // On each release that has something worth telling: change Key and Version and the text below (and CHANGELOG.md and CHANGELOG.pt-BR.md).
    static class WhatsNew
    {
        public const string Key = "4.2";       // The release the card is about: dismissed once per Key
        public const string Version = "4.2.0"; // Shown in the title

        public static string Title { get { return Strings.T("What's new in version " + Version, "Novidades da versão " + Version, "Novedades de la versión " + Version, "Что нового в версии " + Version); } }

        public static string[][] Items
        {
            get
            {
                return new[]
                {
                    new[] { Strings.T("Pomodoro in the timer", "Pomodoro no timer", "Pomodoro en el temporizador", "Помодоро в таймере"),
                            Strings.T("Focus and break cycles on the timer page, with an alert at each change. The lengths are in Behavior.",
                                      "Ciclos de foco e pausa na página do timer, com um aviso a cada mudança. Os tempos ficam em Comportamento.",
                                      "Ciclos de enfoque y pausa en la página del temporizador, con un aviso en cada cambio. Los tiempos están en Comportamiento.",
                                      "Циклы фокуса и перерыва на странице таймера с оповещением при смене. Длительность — в разделе «Поведение».") },
                    new[] { Strings.T("Smoother transitions", "Transições mais fluidas", "Transiciones más fluidas", "Более плавные переходы"),
                            Strings.T("The thin line grows into a pill before its content appears, and what leaves slides up.",
                                      "A linha fina cresce até virar pílula antes de o conteúdo aparecer, e o que sai desliza para cima.",
                                      "La línea fina crece hasta ser píldora antes de que aparezca el contenido, y lo que sale se desliza hacia arriba.",
                                      "Тонкая линия вырастает в капсулу до появления содержимого, а уходящее съезжает вверх.") },
                    new[] { Strings.T("Brightness and volume", "Brilho e volume", "Brillo y volumen", "Яркость и громкость"),
                            Strings.T("Brightness is a sun that lights up; volume gains waves, and a cross when muted.",
                                      "O brilho é um sol que acende; o volume ganha ondas e um xis quando está mudo.",
                                      "El brillo es un sol que se enciende; el volumen gana ondas, y una cruz si está en silencio.",
                                      "Яркость — солнце, которое разгорается; у громкости появляются волны, а при отключении звука — крестик.") },
                };
            }
        }

        public static string ChangelogUrl(string language)
        {
            bool portuguese = string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase) ||
                              (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) && !string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt");
            return "https://github.com/leotrvs/windows-island/blob/main/" + (portuguese ? "CHANGELOG.pt-BR.md" : "CHANGELOG.md");
        }
    }
}
