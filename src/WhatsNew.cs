using System;
using System.Globalization;

namespace WindowsIsland
{
    // What is new in the latest release, in a few lines, for the card at the top of Settings. The full history is the changelog.
    // On each release that has something worth telling: change Key and Version and the text below (and CHANGELOG.md and CHANGELOG.pt-BR.md).
    static class WhatsNew
    {
        public const string Key = "4.3";       // The release the card is about: dismissed once per Key
        public const string Version = "4.3.0"; // Shown in the title

        public static string Title { get { return Strings.T("What's new in version " + Version, "Novidades da versão " + Version, "Novedades de la versión " + Version, "Что нового в версии " + Version); } }

        public static string[][] Items
        {
            get
            {
                return new[]
                {
                    new[] { Strings.T("Shortcuts page", "Página de atalhos", "Página de atajos", "Страница быстрых действий"),
                            Strings.T("Mute the microphone or the speakers, take a screenshot, open the sound settings and set the volume, right from the island.",
                                      "Silencie o microfone ou o alto-falante, tire uma captura, abra os ajustes de som e regule o volume, direto na ilha.",
                                      "Silencia el micrófono o los altavoces, haz una captura, abre los ajustes de sonido y regula el volumen, desde la propia isla.",
                                      "Отключайте микрофон или динамики, делайте снимок, открывайте настройки звука и меняйте громкость прямо на острове.") },
                    new[] { Strings.T("Pomodoro, clearer", "Pomodoro mais claro", "Pomodoro más claro", "Помодоро нагляднее"),
                            Strings.T("Dots show which cycle you are on, and the thin line flashes when a phase changes or Claude finishes.",
                                      "Pontos mostram em que ciclo você está, e a linha fina pisca quando uma fase muda ou o Claude termina.",
                                      "Unos puntos muestran en qué ciclo estás, y la línea fina parpadea al cambiar de fase o cuando Claude termina.",
                                      "Точки показывают текущий цикл, а тонкая линия мигает при смене фазы или когда Claude закончил.") },
                    new[] { Strings.T("Updates that get through", "Atualizações que chegam", "Actualizaciones que llegan", "Обновления доходят"),
                            Strings.T("If GitHub's download address does not answer, the app tries another way; an old .exe offers the newest version.",
                                      "Se o endereço de download do GitHub não responde, o app tenta outro caminho; um .exe antigo oferece a versão mais nova.",
                                      "Si la dirección de descarga de GitHub no responde, la app prueba otro camino; un .exe antiguo ofrece la versión más nueva.",
                                      "Если адрес загрузки GitHub не отвечает, приложение пробует другой путь; старый .exe предложит новую версию.") },
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
