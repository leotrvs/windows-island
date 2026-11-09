using System;
using System.Globalization;

namespace WindowsIsland
{
    // What is new in the latest release, in a few lines, for the card at the top of Settings. The full history is the changelog.
    // On each release that has something worth telling: change Key and the text below (and CHANGELOG.md and CHANGELOG.pt-BR.md).
    static class WhatsNew
    {
        public const string Key = "4.2";

        public static string Title { get { return Strings.T("What's new in " + Key, "Novidades da " + Key, "Novedades de la " + Key, "Что нового в " + Key); } }

        public static string[] Items
        {
            get
            {
                return new[]
                {
                    Strings.T("Pomodoro in the timer: focus and break cycles, with an alert at each change",
                              "Pomodoro no timer: ciclos de foco e pausa, com um aviso a cada mudança",
                              "Pomodoro en el temporizador: ciclos de enfoque y pausa, con un aviso en cada cambio",
                              "Помодоро в таймере: циклы фокуса и перерыва с оповещением при смене"),
                    Strings.T("Smoother transitions: the thin line grows into a pill and content slides in",
                              "Transições mais fluidas: a linha fina cresce até virar pílula e o conteúdo desliza para dentro",
                              "Transiciones más fluidas: la línea fina crece hasta ser píldora y el contenido se desliza",
                              "Более плавные переходы: тонкая линия растёт в капсулу, содержимое выезжает"),
                    Strings.T("Brightness is a sun that lights up; volume has waves that grow, and a cross when muted",
                              "O brilho é um sol que acende; o volume tem ondas que crescem, e um xis quando está mudo",
                              "El brillo es un sol que se enciende; el volumen tiene ondas que crecen, y una cruz si está en silencio",
                              "Яркость — солнце, которое разгорается; у громкости растут волны, а при отключении звука — крестик"),
                    Strings.T("Settings, System: copy a report to paste into a GitHub issue",
                              "Ajustes, Sistema: copie um relatório para colar numa issue do GitHub",
                              "Ajustes, Sistema: copia un informe para pegarlo en un issue de GitHub",
                              "Настройки, Система: скопируйте отчёт для issue на GitHub"),
                };
            }
        }

        // Portuguese readers go to the Portuguese changelog, everyone else to the English one
        public static string ChangelogUrl(string language)
        {
            bool portuguese = string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase) ||
                              (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) && !string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt");
            return "https://github.com/leotrvs/windows-island/blob/main/" + (portuguese ? "CHANGELOG.pt-BR.md" : "CHANGELOG.md");
        }
    }
}
