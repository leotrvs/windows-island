using System;
using System.Globalization;

namespace WindowsIsland
{
    static class Strings
    {
        static readonly bool Pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";

        static string T(string en, string pt) { return Pt ? pt : en; }

        public static string ClaudeDone => T("Claude finished", "Claude terminou");
        public static string ClaudeNeedsYou => T("Claude needs you", "Claude precisa de você");
        public static string Working => T("Working", "Trabalhando");
        public static string Waiting => T("Waiting", "Aguardando");
        public static string WaitingForYou => T("Waiting for you", "Aguardando você");
        public static string NoSession => T("No open session", "Nenhuma sessão aberta");
        public static string Session5h => T("Session (5h)", "Sessão (5h)");
        public static string Weekly => T("Week", "Semana");
        public static string NoUsage => T("No usage data yet. Open Claude Usage Taskbar to collect it.",
                                          "Sem dados de uso. Abra o Claude Usage Taskbar para coletar.");
        public static string UnknownTrack => T("Unknown", "Desconhecido");
        public static string SampleSession => T("Refactor the settings panel", "Refatorar o painel de ajustes");

        public static string IdleSessions(int count)
        {
            if (Pt) return count == 1 ? "1 sessão ociosa" : count + " sessões ociosas";
            return count == 1 ? "1 idle session" : count + " idle sessions";
        }

        public static string Resets(DateTime? when)
        {
            if (!when.HasValue) return "";
            string time = when.Value - DateTime.Now < TimeSpan.FromHours(20)
                ? when.Value.ToString("HH:mm", CultureInfo.CurrentCulture)
                : when.Value.ToString("ddd HH:mm", CultureInfo.CurrentCulture);
            return T("resets ", "renova ") + time;
        }

        // Tray menu
        public static string MenuMusic => T("Show music", "Mostrar música");
        public static string MenuClaude => T("Show Claude", "Mostrar Claude");
        public static string MenuBanners => T("Claude alerts", "Avisos do Claude");
        public static string MenuIdlePill => T("Show pill when idle", "Mostrar pílula ociosa");
        public static string MenuHover => T("Expand on hover", "Expandir ao passar o mouse");
        public static string MenuFullscreen => T("Hide in fullscreen", "Esconder em tela cheia");
        public static string MenuPosition => T("Position", "Posição");
        public static string MenuTop => T("Top", "Topo");
        public static string MenuBottom => T("Bottom", "Embaixo");
        public static string MenuStartup => T("Start with Windows", "Iniciar com o Windows");
        public static string MenuOpenSettings => T("Open settings file", "Abrir arquivo de configurações");
        public static string MenuQuit => T("Quit", "Sair");
    }
}
