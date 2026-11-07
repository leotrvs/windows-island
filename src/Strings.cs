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
        public static string ReadingUsage => T("Reading your limits…", "Lendo seus limites…");
        public static string ClaudeNotFound => T("Claude Code not found. Install it and sign in by running claude once.",
                                                 "Claude Code não encontrado. Instale e entre rodando claude uma vez.");
        public static string UnknownTrack => T("Unknown", "Desconhecido");
        public static string SampleNotificationTitle => T("Lunch tomorrow?", "Almoço amanhã?");
        public static string SampleNotificationBody => T("Are you free at noon? There is a new place nearby.", "Você está livre ao meio-dia? Abriu um lugar novo aqui perto.");
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

        // Install
        public static string InstallTitle => T("Install Windows Island", "Instalar o Windows Island");
        public static string InstallPrompt => T(
            "Install Windows Island for your Windows user?\n\nIt adds Start menu and Desktop shortcuts and starts with Windows. You can uninstall it from Settings > Apps.",
            "Instalar o Windows Island para o seu usuário do Windows?\n\nCria atalhos no menu Iniciar e na Área de Trabalho e inicia com o Windows. Dá para desinstalar em Configurações > Aplicativos.");
        public static string InstallFailed => T("The installation failed. Try again, or run with --portable.",
                                                "A instalação falhou. Tente de novo ou rode com --portable.");
        public static string UninstallTitle => T("Uninstall Windows Island", "Desinstalar o Windows Island");
        public static string UninstallPrompt => T("Remove Windows Island, its shortcuts and its settings?",
                                                  "Remover o Windows Island, os atalhos e as configurações?");

        // Tray menu
        public static string MenuMusic => T("Show music", "Mostrar música");
        public static string MenuClaude => T("Show Claude", "Mostrar Claude");
        public static string MenuBanners => T("Claude alerts", "Avisos do Claude");
        public static string MenuNotifications => T("Show Windows notifications", "Mostrar notificações do Windows");
        public static string MenuIdlePill => T("Show pill when idle", "Mostrar pílula ociosa");
        public static string MenuHover => T("Expand on hover", "Expandir ao passar o mouse");
        public static string MenuFullscreen => T("Hide in fullscreen", "Esconder em tela cheia");
        public static string MenuPosition => T("Position", "Posição");
        public static string MenuTop => T("Top", "Topo");
        public static string MenuBottom => T("Bottom", "Embaixo");
        public static string MenuStartup => T("Start with Windows", "Iniciar com o Windows");
        public static string MenuAutoUpdate => T("Automatic updates", "Atualizações automáticas");
        public static string MenuCheckUpdates => T("Check for updates", "Procurar atualizações");
        public static string UpToDate => T("You have the latest version", "Você está na versão mais recente");
        public static string UpdateFailed => T("Could not check for updates", "Não foi possível procurar atualizações");
        public static string UpdatesNeedInstall => T("Updates work in the installed copy", "As atualizações funcionam na cópia instalada");
        public static string UpdatedTo(string version) => T("Updated to version ", "Atualizado para a versão ") + version;
        public static string MenuOpenSettings => T("Open settings file", "Abrir arquivo de configurações");
        public static string MenuQuit => T("Quit", "Sair");
    }
}
