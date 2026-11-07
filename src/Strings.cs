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
        public static string CodexDone => T("Codex finished", "Codex terminou");
        public static string CodexIdle => T("No task running", "Nenhuma tarefa em andamento");
        public static string CodexNoData => T("No data yet. Use Codex once to see its limits", "Sem dados ainda. Use o Codex uma vez para ver os limites");
        public static string MenuCodex => T("Show Codex", "Mostrar Codex");
        public static string MenuCodexBanners => T("Codex alerts", "Avisos do Codex");
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
        // Settings window
        public static string MenuSettings => T("Settings…", "Ajustes…");
        public static string SettingsTitle => T("Settings", "Ajustes");
        public static string SectionActivities => T("Activities", "Atividades");
        public static string SectionBehavior => T("Behavior", "Comportamento");
        public static string SectionAppearance => T("Appearance", "Aparência");
        public static string SectionSystem => T("System", "Sistema");
        public static string HintNotifications => T("Reads the text of new notifications to show them here", "Lê o texto das notificações novas para mostrá-las aqui");
        public static string HintEmptyIsland => T("Stays a plain pill; hover to see activities", "Fica só a pílula; passe o mouse para ver as atividades");
        public static string HintAutoHide => T("Comes back with new activity or when the mouse reaches its spot", "Volta com uma atividade nova ou quando o mouse chega ao lugar dela");
        public static string MonitorLabel => T("Monitor", "Monitor");
        public static string HintMonitor => T("With the mouse: the island moves to the screen the cursor is on", "Com o mouse: a ilha vai para a tela onde está o cursor");
        public static string MonitorPrimary => T("Primary", "Principal");
        public static string MonitorActive => T("With the mouse", "Com o mouse");
        public static string MonitorAll => T("All", "Todos");
        public static string MonitorNumber(int number) => T("Screen ", "Tela ") + number;
        public static string CollapsedSize => T("Collapsed size", "Tamanho recolhida");
        public static string ExpandedSize => T("Expanded size", "Tamanho expandida");
        public static string DistanceFromEdge => T("Distance from the edge", "Distância da borda");
        public static string HorizontalPosition => T("Horizontal position", "Posição horizontal");
        public static string Centered => T("Centered", "Centralizada");
        public static string ResetAppearance => T("Reset appearance", "Restaurar aparência");

        public static string MenuAutoHide => T("Auto-hide when idle", "Esconder sozinha quando ociosa");
        public static string MenuAlertDuration => T("Alert duration", "Duração dos avisos");
        public static string MenuPausedMusic => T("Hide paused music after", "Esconder música pausada após");

        // 0 means "never" for auto-hide and "at once" for the others.
        public static string Duration(int seconds, bool zeroIsNever)
        {
            if (seconds == 0) return zeroIsNever ? T("Never", "Nunca") : T("At once", "Na hora");
            if (seconds >= 60 && seconds % 60 == 0) return (seconds / 60) + " min";
            return seconds + " s";
        }

        public static string MenuEmptyIsland => T("Empty island (no live activities)", "Ilha vazia (sem atividades)");
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
