using System;
using System.Globalization;

namespace WindowsIsland
{
    // What is new in the latest release, in a few lines, for the card at the top of Settings. The full history is the changelog.
    // On each release that has something worth telling: change Key and Version and the text below (and CHANGELOG.md and CHANGELOG.pt-BR.md).
    static class WhatsNew
    {
        public const string Key = "4.4";       // The release the card is about: dismissed once per Key
        public const string Version = "4.4.1"; // Shown in the title

        public static string Title { get { return Strings.T("What's new in version " + Version, "Novidades da versão " + Version, "Novedades de la versión " + Version, "Что нового в версии " + Version); } }

        public static string[][] Items
        {
            get
            {
                return new[]
                {
                    new[] { Strings.T("A tray menu with icons", "Um menu da bandeja com ícones", "Un menú de la bandeja con iconos", "Меню в трее с иконками"),
                            Strings.T("It follows the theme, switches the thin line on or off, and has Do not disturb for the island. \"Quit\" is now a red \"Close\".",
                                      "Segue o tema, liga e desliga a linha fina e tem o Não perturbe da ilha. \"Sair\" virou \"Fechar\", em vermelho.",
                                      "Sigue el tema, activa o desactiva la línea fina y tiene No molestar para la isla. \"Salir\" ahora es \"Cerrar\", en rojo.",
                                      "Следует теме, включает тонкую линию и «Не беспокоить» для острова. «Выход» теперь красное «Закрыть».") },
                    new[] { Strings.T("Update from the island", "Atualizar pela ilha", "Actualizar desde la isla", "Обновление через остров"),
                            Strings.T("\"Check for updates\" looks in the background; if there is a new version, tap the island to download and restart.",
                                      "\"Procurar atualizações\" procura em segundo plano; se houver versão nova, toque na ilha para baixar e reiniciar.",
                                      "\"Buscar actualizaciones\" busca en segundo plano; si hay una versión nueva, toca la isla para descargar y reiniciar.",
                                      "«Проверить обновления» ищет в фоне; если есть новая версия, нажмите на остров, чтобы скачать и перезапустить.") },
                    new[] { Strings.T("Drag to reorder pages", "Arraste para ordenar as páginas", "Arrastra para ordenar las páginas", "Перетаскивайте страницы"),
                            Strings.T("In Settings, Pages, hold a page by its handle and drag it to a new place.",
                                      "Em Ajustes, Páginas, segure uma página pela alça e arraste para outro lugar.",
                                      "En Ajustes, Páginas, sujeta una página por el asa y arrástrala a otro lugar.",
                                      "В настройках, Страницы: возьмите страницу за ручку и перетащите.") },
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
