using System;
using System.Globalization;

namespace WindowsIsland
{
    // What is new in the latest release, in a few lines, for the card at the top of Settings. The full history is the changelog.
    // On each release that has something worth telling: change Key and Version and the text below (and CHANGELOG.md and CHANGELOG.pt-BR.md).
    static class WhatsNew
    {
        public const string Key = "4.6";       // The release the card is about: dismissed once per Key
        public const string Version = "4.6.0"; // Shown in the title

        public static string Title { get { return Strings.T("What's new in version " + Version, "Novidades da versão " + Version, "Novedades de la versión " + Version, "Что нового в версии " + Version); } }

        public static string[][] Items
        {
            get
            {
                return new[]
                {
                    new[] { Strings.T("The song's name on the island", "O nome da música na ilha", "El nombre de la canción en la isla", "Название песни на острове"),
                            Strings.T("When the song changes, the cover and the name slide to the side, and the name shows for a moment. Settings can keep it always visible.",
                                      "Quando a música muda, a capa e o nome deslizam para o lado, e o nome aparece por um momento. Nos Ajustes dá para deixá-lo sempre visível.",
                                      "Cuando cambia la canción, la portada y el nombre se deslizan al lado, y el nombre se muestra un momento. En Ajustes puedes dejarlo siempre visible.",
                                      "При смене песни обложка и название сдвигаются в сторону, и название ненадолго появляется. В настройках его можно оставить всегда.") },
                    new[] { Strings.T("Pages come in one by one", "As páginas entram uma a uma", "Las páginas entran una a una", "Страницы появляются по очереди"),
                            Strings.T("When the island opens, each row of the page comes in a little after the one before.",
                                      "Quando a ilha abre, cada linha da página entra um pouco depois da anterior.",
                                      "Cuando la isla se abre, cada fila de la página entra un poco después de la anterior.",
                                      "Когда остров открывается, каждая строка страницы появляется чуть позже предыдущей.") },
                    new[] { Strings.T("Thin line and a timer ring", "Linha fina e anel do timer", "Línea fina y anillo del temporizador", "Тонкая линия и кольцо таймера"),
                            Strings.T("A maximized window now gets the thin line by default, and the timer pill has a ring that fades as time runs out.",
                                      "Com uma janela maximizada a ilha vira a linha fina por padrão, e a pílula do timer tem um anel que some conforme o tempo acaba.",
                                      "Con una ventana maximizada la isla es la línea fina por defecto, y la píldora del temporizador tiene un anillo que se apaga con el tiempo.",
                                      "С развёрнутым окном остров по умолчанию становится тонкой линией, а у таймера есть кольцо, которое гаснет со временем.") },
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
