using System;
using System.Globalization;

namespace WindowsIsland
{
    // Every text the app shows, in English, Portuguese, Spanish and Russian. The language follows Windows
    // unless the user picks one in Settings.
    static class Strings
    {
        static string lang = Detect();

        static string Detect()
        {
            return Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }

        static string Normalize(string code)
        {
            code = (code ?? "").ToLowerInvariant();
            return code == "pt" || code == "es" || code == "ru" ? code : "en";
        }

        // "Auto" (or empty) follows Windows; otherwise a language code: en, pt, es or ru.
        public static void Use(string setting)
        {
            lang = string.IsNullOrWhiteSpace(setting) || setting.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? Detect() : Normalize(setting);
        }

        public static string Current { get { return lang; } }

        static CultureInfo Culture
        {
            get
            {
                try { return CultureInfo.GetCultureInfo(lang); }
                catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
            }
        }

        static string T(string en, string pt, string es, string ru)
        {
            switch (lang)
            {
                case "pt": return pt;
                case "es": return es;
                case "ru": return ru;
                default: return en;
            }
        }

        public static string ClaudeDone => T("Claude finished", "Claude terminou", "Claude terminó", "Claude закончил");
        public static string ClaudeNeedsYou => T("Claude needs you", "Claude precisa de você", "Claude te necesita", "Claude ждёт вас");
        public static string CodexDone => T("Codex finished", "Codex terminou", "Codex terminó", "Codex закончил");
        public static string CodexIdle => T("No task running", "Nenhuma tarefa em andamento", "Ninguna tarea en curso", "Нет активной задачи");
        public static string CodexNoData => T("No data yet. Use Codex once to see its limits",
                                              "Sem dados ainda. Use o Codex uma vez para ver os limites",
                                              "Aún sin datos. Usa Codex una vez para ver sus límites",
                                              "Данных пока нет. Запустите Codex один раз, чтобы увидеть лимиты");
        public static string MenuCodex => T("Show Codex", "Mostrar Codex", "Mostrar Codex", "Показывать Codex");
        public static string MenuCodexBanners => T("Codex alerts", "Avisos do Codex", "Avisos de Codex", "Оповещения Codex");
        public static string Working => T("Working", "Trabalhando", "Trabajando", "Работает");
        public static string Waiting => T("Waiting", "Aguardando", "Esperando", "Ожидает");
        public static string WaitingForYou => T("Waiting for you", "Aguardando você", "Esperándote", "Ждёт вас");
        public static string NoSession => T("No open session", "Nenhuma sessão aberta", "Ninguna sesión abierta", "Нет открытых сессий");
        public static string Session5h => T("Session (5h)", "Sessão (5h)", "Sesión (5 h)", "Сессия (5 ч)");
        public static string Weekly => T("Week", "Semana", "Semana", "Неделя");
        public static string ReadingUsage => T("Reading your limits…", "Lendo seus limites…", "Leyendo tus límites…", "Чтение лимитов…");
        public static string ClaudeNotFound => T("Claude Code not found. Install it and sign in by running claude once.",
                                                 "Claude Code não encontrado. Instale e entre rodando claude uma vez.",
                                                 "No se encontró Claude Code. Instálalo e inicia sesión ejecutando claude una vez.",
                                                 "Claude Code не найден. Установите его и войдите, запустив claude один раз.");
        public static string UnknownTrack => T("Unknown", "Desconhecido", "Desconocido", "Неизвестно");
        public static string SampleNotificationTitle => T("Lunch tomorrow?", "Almoço amanhã?", "¿Almorzamos mañana?", "Обед завтра?");
        public static string SampleNotificationBody => T("Are you free at noon? There is a new place nearby.",
                                                         "Você está livre ao meio-dia? Abriu um lugar novo aqui perto.",
                                                         "¿Estás libre al mediodía? Abrieron un sitio nuevo cerca.",
                                                         "Вы свободны в полдень? Рядом открылось новое место.");
        public static string SampleSessionTwo => T("Fix the login page", "Corrigir a página de login", "Arreglar la página de inicio de sesión", "Исправить страницу входа");
        public static string SampleSession => T("Refactor the settings panel", "Refatorar o painel de ajustes", "Refactorizar el panel de ajustes", "Переработать панель настроек");

        public static string IdleSessions(int count)
        {
            switch (lang)
            {
                case "pt": return count == 1 ? "1 sessão ociosa" : count + " sessões ociosas";
                case "es": return count == 1 ? "1 sesión inactiva" : count + " sesiones inactivas";
                case "ru":
                    int last = count % 10, tens = count % 100;
                    string word = last == 1 && tens != 11 ? "неактивная сессия" : last >= 2 && last <= 4 && (tens < 12 || tens > 14) ? "неактивные сессии" : "неактивных сессий";
                    return count + " " + word;
                default: return count == 1 ? "1 idle session" : count + " idle sessions";
            }
        }

        public static string Resets(DateTime? when)
        {
            if (!when.HasValue) return "";
            string time = when.Value - DateTime.Now < TimeSpan.FromHours(20)
                ? when.Value.ToString("HH:mm", Culture)
                : when.Value.ToString("ddd HH:mm", Culture);
            return T("resets ", "renova ", "se renueva ", "сброс ") + time;
        }

        // Install
        public static string InstallTitle => T("Install Windows Island", "Instalar o Windows Island", "Instalar Windows Island", "Установить Windows Island");
        public static string InstallPrompt => T(
            "Install Windows Island for your Windows user?\n\nIt adds Start menu and Desktop shortcuts and starts with Windows. You can uninstall it from Settings > Apps.",
            "Instalar o Windows Island para o seu usuário do Windows?\n\nCria atalhos no menu Iniciar e na Área de Trabalho e inicia com o Windows. Dá para desinstalar em Configurações > Aplicativos.",
            "¿Instalar Windows Island para tu usuario de Windows?\n\nCrea accesos directos en el menú Inicio y el Escritorio y se inicia con Windows. Puedes desinstalarlo en Configuración > Aplicaciones.",
            "Установить Windows Island для вашей учётной записи Windows?\n\nБудут созданы ярлыки в меню «Пуск» и на рабочем столе, приложение будет запускаться вместе с Windows. Удалить его можно в Параметры > Приложения.");
        public static string InstallFailed => T("The installation failed. Try again, or run with --portable.",
                                                "A instalação falhou. Tente de novo ou rode com --portable.",
                                                "La instalación falló. Inténtalo de nuevo o ejecútalo con --portable.",
                                                "Не удалось установить. Повторите попытку или запустите с --portable.");
        public static string UninstallTitle => T("Uninstall Windows Island", "Desinstalar o Windows Island", "Desinstalar Windows Island", "Удалить Windows Island");
        public static string UninstallPrompt => T("Remove Windows Island, its shortcuts and its settings?",
                                                  "Remover o Windows Island, os atalhos e as configurações?",
                                                  "¿Quitar Windows Island, sus accesos directos y su configuración?",
                                                  "Удалить Windows Island, его ярлыки и настройки?");

        // Tray menu and Settings
        public static string MenuMusic => T("Show music", "Mostrar música", "Mostrar música", "Показывать музыку");
        public static string MenuClaude => T("Show Claude", "Mostrar Claude", "Mostrar Claude", "Показывать Claude");
        public static string MenuBanners => T("Claude alerts", "Avisos do Claude", "Avisos de Claude", "Оповещения Claude");
        public static string MenuNotifications => T("Show Windows notifications", "Mostrar notificações do Windows", "Mostrar notificaciones de Windows", "Показывать уведомления Windows");
        public static string MenuSettings => T("Settings…", "Ajustes…", "Ajustes…", "Настройки…");
        public static string SettingsTitle => T("Settings", "Ajustes", "Ajustes", "Настройки");
        public static string SectionActivities => T("Activities", "Atividades", "Actividades", "Активности");
        public static string SectionBehavior => T("Behavior", "Comportamento", "Comportamiento", "Поведение");
        public static string SectionAppearance => T("Appearance", "Aparência", "Apariencia", "Внешний вид");
        public static string SectionSystem => T("System", "Sistema", "Sistema", "Система");
        public static string HintNotifications => T("Reads the text of new notifications to show them here",
                                                    "Lê o texto das notificações novas para mostrá-las aqui",
                                                    "Lee el texto de las notificaciones nuevas para mostrarlas aquí",
                                                    "Читает текст новых уведомлений, чтобы показать их здесь");
        public static string HintEmptyIsland => T("Stays a plain pill; hover to see activities",
                                                  "Fica só a pílula; passe o mouse para ver as atividades",
                                                  "Queda solo la píldora; pasa el ratón para ver las actividades",
                                                  "Остаётся простая капсула; наведите мышь, чтобы увидеть активности");
        public static string HintAutoHide => T("Comes back with new activity or when the mouse reaches its spot",
                                               "Volta com uma atividade nova ou quando o mouse chega ao lugar dela",
                                               "Vuelve con una actividad nueva o cuando el ratón llega a su lugar",
                                               "Возвращается при новой активности или когда мышь подходит к её месту");
        public static string MonitorLabel => T("Monitor", "Monitor", "Monitor", "Монитор");
        public static string HintMonitor => T("With the mouse: the island moves to the screen the cursor is on",
                                              "Com o mouse: a ilha vai para a tela onde está o cursor",
                                              "Con el ratón: la isla va a la pantalla donde está el cursor",
                                              "С мышью: остров переходит на экран, где находится курсор");
        public static string MonitorPrimary => T("Primary", "Principal", "Principal", "Основной");
        public static string MonitorActive => T("With the mouse", "Com o mouse", "Con el ratón", "С мышью");
        public static string MonitorAll => T("All", "Todos", "Todos", "Все");
        public static string MonitorNumber(int number) => T("Screen ", "Tela ", "Pantalla ", "Экран ") + number;
        public static string CollapsedSize => T("Collapsed size", "Tamanho recolhida", "Tamaño contraída", "Размер в свёрнутом виде");
        public static string ExpandedSize => T("Expanded size", "Tamanho expandida", "Tamaño expandida", "Размер в развёрнутом виде");
        public static string DistanceFromEdge => T("Distance from the edge", "Distância da borda", "Distancia del borde", "Отступ от края");
        public static string HorizontalPosition => T("Horizontal position", "Posição horizontal", "Posición horizontal", "Положение по горизонтали");
        public static string Centered => T("Centered", "Centralizada", "Centrada", "По центру");
        public static string ResetAppearance => T("Reset appearance", "Restaurar aparência", "Restablecer apariencia", "Сбросить внешний вид");

        public static string MenuAutoHide => T("Auto-hide when idle", "Esconder sozinha quando ociosa", "Ocultar sola cuando está inactiva", "Скрывать в простое");
        public static string MenuAlertDuration => T("Alert duration", "Duração dos avisos", "Duración de los avisos", "Длительность оповещений");
        public static string MenuPausedMusic => T("Hide paused music after", "Esconder música pausada após", "Ocultar música en pausa tras", "Скрывать музыку на паузе через");

        // 0 means "never" for auto-hide and "at once" for the others.
        public static string Duration(int seconds, bool zeroIsNever)
        {
            if (seconds == 0) return zeroIsNever ? T("Never", "Nunca", "Nunca", "Никогда") : T("At once", "Na hora", "Al instante", "Сразу");
            string minute = T("min", "min", "min", "мин"), second = T("s", "s", "s", "с");
            if (seconds >= 60 && seconds % 60 == 0) return (seconds / 60) + " " + minute;
            return seconds + " " + second;
        }

        public static string MenuEmptyIsland => T("Empty island (no live activities)", "Ilha vazia (sem atividades)", "Isla vacía (sin actividades)", "Пустой остров (без активностей)");
        public static string MenuIdlePill => T("Show pill when idle", "Mostrar pílula ociosa", "Mostrar píldora en reposo", "Показывать капсулу в простое");
        public static string MenuHover => T("Expand on hover", "Expandir ao passar o mouse", "Expandir al pasar el ratón", "Раскрывать при наведении");
        public static string MenuFullscreen => T("Hide in fullscreen", "Esconder em tela cheia", "Ocultar en pantalla completa", "Скрывать в полноэкранном режиме");
        public static string MenuPosition => T("Position", "Posição", "Posición", "Положение");
        public static string MenuTop => T("Top", "Topo", "Arriba", "Сверху");
        public static string MenuBottom => T("Bottom", "Embaixo", "Abajo", "Снизу");
        public static string MenuStartup => T("Start with Windows", "Iniciar com o Windows", "Iniciar con Windows", "Запускать с Windows");
        public static string MenuAutoUpdate => T("Automatic updates", "Atualizações automáticas", "Actualizaciones automáticas", "Автоматические обновления");
        public static string MenuCheckUpdates => T("Check for updates", "Procurar atualizações", "Buscar actualizaciones", "Проверить обновления");
        public static string UpdateChecking => T("Checking for updates…", "Procurando atualizações…", "Buscando actualizaciones…", "Поиск обновлений…");
        public static string UpdateDownload(string version) => T("Download update ", "Baixar a atualização ", "Descargar la actualización ", "Скачать обновление ") + version;
        public static string UpdateDownloading => T("Downloading the update…", "Baixando a atualização…", "Descargando la actualización…", "Загрузка обновления…");
        public static string UpToDate => T("You have the latest version", "Você está na versão mais recente", "Tienes la versión más reciente", "У вас последняя версия");
        public static string UpdateFailed => T("Could not check for updates", "Não foi possível procurar atualizações", "No se pudo buscar actualizaciones", "Не удалось проверить обновления");
        public static string UpdatesNeedInstall => T("Updates work in the installed copy", "As atualizações funcionam na cópia instalada", "Las actualizaciones funcionan en la copia instalada", "Обновления работают в установленной копии");
        public static string UpdatedTo(string version) => T("Updated to version ", "Atualizado para a versão ", "Actualizado a la versión ", "Обновлено до версии ") + version;
        public static string MenuUninstall => T("Uninstall Windows Island", "Desinstalar o Windows Island", "Desinstalar Windows Island", "Удалить Windows Island");
        public static string MenuOpenSettings => T("Open settings file", "Abrir arquivo de configurações", "Abrir archivo de configuración", "Открыть файл настроек");
        public static string MenuQuit => T("Quit", "Sair", "Salir", "Выход");
        public static string MenuTimer => T("Timer", "Timer", "Temporizador", "Таймер");
        public static string MenuTimerMinutes(int minutes) => minutes < 60 ? minutes + " " + T("min", "min", "min", "мин") : (minutes / 60) + " " + T("h", "h", "h", "ч");
        public static string MenuStopwatch => T("Stopwatch", "Cronômetro", "Cronómetro", "Секундомер");
        public static string MenuStopTimer => T("Stop", "Parar", "Detener", "Стоп");
        public static string RingColorLabel => T("Usage ring color", "Cor dos anéis de uso", "Color de los anillos de uso", "Цвет колец лимитов");
        public static string ColorBlue => T("Blue", "Azul", "Azul", "Синий");
        public static string ColorGreen => T("Green", "Verde", "Verde", "Зелёный");
        public static string ColorPurple => T("Purple", "Roxo", "Morado", "Фиолетовый");
        public static string TimerStart => T("Start", "Iniciar", "Iniciar", "Старт");
        public static string TimerPause => T("Pause", "Pausar", "Pausar", "Пауза");
        public static string TimerResume => T("Resume", "Continuar", "Continuar", "Продолжить");
        public static string TimerPaused => T("Paused", "Pausado", "En pausa", "На паузе");
        public static string TimerPickLength => T("Set a length", "Escolha a duração", "Elige la duración", "Выберите время");
        public static string TimerFinished => T("Timer finished", "Timer terminou", "El temporizador terminó", "Таймер завершён");
        public static string TimerLabel => T("Timer", "Timer", "Temporizador", "Таймер");
        public static string StopwatchLabel => T("Stopwatch", "Cronômetro", "Cronómetro", "Секундомер");
        public static string MenuPrivacyDots => T("Microphone and camera dots", "Pontos de microfone e câmera", "Puntos de micrófono y cámara", "Точки микрофона и камеры");
        public static string HintPrivacyDots => T("Orange when the microphone is in use, green for the camera",
                                                  "Laranja com o microfone em uso, verde com a câmera",
                                                  "Naranja con el micrófono en uso, verde con la cámara",
                                                  "Оранжевая, пока используется микрофон, зелёная для камеры");
        public static string MenuPrivacyNames => T("App name when recording starts", "Nome do app ao começar a gravar", "Nombre de la app al empezar a grabar", "Название приложения при начале записи");
        public static string HintPrivacyNames => T("Hover the dots to see which app uses the microphone or camera. Here, choose how long its name also shows by itself when an app starts",
                                                   "Passe o mouse nos pontos para ver qual app usa o microfone ou a câmera. Aqui, escolha por quanto tempo o nome também aparece sozinho quando um app começa",
                                                   "Pasa el ratón por los puntos para ver qué app usa el micrófono o la cámara. Aquí elige cuánto tiempo su nombre aparece también solo cuando una app empieza",
                                                   "Наведите мышь на точки, чтобы увидеть, какое приложение использует микрофон или камеру. Здесь выберите, сколько секунд название показывается само, когда приложение начинает");
        public static string Off => T("Off", "Desligado", "Desactivado", "Выкл");
        public static string GroupPrivacy => T("Privacy (microphone and camera)", "Privacidade (microfone e câmera)", "Privacidad (micrófono y cámara)", "Конфиденциальность (микрофон и камера)");
        public static string GroupBattery => T("Battery", "Bateria", "Batería", "Батарея");
        public static string GroupAlerts => T("System alerts", "Avisos do sistema", "Avisos del sistema", "Системные уведомления");
        public static string GroupLevels => T("Volume and brightness", "Volume e brilho", "Volumen y brillo", "Громкость и яркость");
        public static string GroupTimes => T("Times", "Tempos", "Tiempos", "Время");
        public static string GroupSizePos => T("Size and position", "Tamanho e posição", "Tamaño y posición", "Размер и положение");
        public static string GroupApiMore => T("Token, guide and browser extension", "Token, guia e extensão do navegador", "Token, guía y extensión del navegador", "Токен, руководство и расширение браузера");
        public static string LimitFiveHour => T("5-hour limit", "Limite de 5 horas", "Límite de 5 horas", "Лимит 5 часов");
        public static string LimitWeekly => T("Weekly limit", "Limite semanal", "Límite semanal", "Недельный лимит");
        public static string LimitReachedTitle => T("Limit reached", "Limite atingido", "Límite alcanzado", "Лимит исчерпан");
        public static string WindowFiveHour => T("5 hours", "5 horas", "5 horas", "5 часов");
        public static string WindowWeekly => T("Weekly", "Semanal", "Semanal", "Недельный");
        public static string MenuLimitAlerts => T("Limit alerts", "Avisos de limite", "Avisos de límite", "Оповещения о лимитах");
        public static string HintLimitAlerts => T("Warns when the 5-hour or weekly limit reaches 75%, 90% and 100%, and when it renews",
                                                  "Avisa quando o limite de 5 horas ou o semanal chega a 75%, 90% e 100%, e quando renova",
                                                  "Avisa cuando el límite de 5 horas o el semanal llega al 75%, 90% y 100%, y cuando se renueva",
                                                  "Сообщает, когда лимит за 5 часов или недельный достигает 75%, 90% и 100%, и когда он обновляется");
        public static string LimitReached(string window) => T(window + " reached", window + " atingido", window + " alcanzado", window + ": достигнут");
        public static string LimitRenewed(string window) => T(window + " renewed", window + " renovado", window + " renovado", window + ": обновлён");
        public static string LimitFull => T("Your full limit is available again", "Seu limite está cheio de novo", "Tu límite está completo otra vez", "Лимит снова полный");
        public static string SectionPages => T("Pages", "Páginas", "Páginas", "Страницы");
        public static string HintPages => T("The pages you can switch between when the island is open",
                                            "As páginas entre as quais você alterna com a ilha aberta",
                                            "Las páginas entre las que cambias con la isla abierta",
                                            "Страницы, между которыми можно переключаться, когда остров открыт");
        public static string PageMusicLabel => T("Music page", "Página de música", "Página de música", "Страница музыки");
        public static string PageClaudeLabel => T("Claude and Codex page", "Página do Claude e Codex", "Página de Claude y Codex", "Страница Claude и Codex");
        public static string PageTimerLabel => T("Timer page", "Página do timer", "Página del temporizador", "Страница таймера");
        public static string MenuDevices => T("Bluetooth and USB devices", "Dispositivos Bluetooth e USB", "Dispositivos Bluetooth y USB", "Устройства Bluetooth и USB");
        public static string HintDevices => T("A short note when headphones, drives and other devices connect or disconnect",
                                              "Um aviso curto quando fones, pendrives e outros dispositivos conectam ou desconectam",
                                              "Un aviso corto cuando se conectan o desconectan auriculares, unidades y otros dispositivos",
                                              "Короткое уведомление, когда наушники, диски и другие устройства подключаются или отключаются");
        public static string MenuNetworkAlerts => T("Network connection", "Conexão de rede", "Conexión de red", "Подключение к сети");
        public static string HintNetworkAlerts => T("A short note when the internet goes away or comes back",
                                                    "Um aviso curto quando a internet cai ou volta",
                                                    "Un aviso corto cuando se cae o vuelve internet",
                                                    "Короткое уведомление, когда интернет пропадает или возвращается");
        public static string DeviceConnected(string name) => string.Format(T("{0} connected", "{0} conectado", "{0} conectado", "{0}: подключено"), name);
        public static string DeviceDisconnected(string name) => string.Format(T("{0} disconnected", "{0} desconectado", "{0} desconectado", "{0}: отключено"), name);
        public static string NetworkOnline => T("Back online", "Internet de volta", "Conexión restablecida", "Интернет снова есть");
        public static string NetworkOffline => T("No internet", "Sem internet", "Sin internet", "Нет интернета");
        public static string MenuHotkey => T("Keyboard shortcut", "Atalho de teclado", "Atajo de teclado", "Сочетание клавиш");
        public static string HintHotkey => T("Opens and closes the island from anywhere. Off by default",
                                             "Abre e fecha a ilha de qualquer lugar. Desligado por padrão",
                                             "Abre y cierra la isla desde cualquier lugar. Desactivado por defecto",
                                             "Открывает и закрывает остров из любого места. По умолчанию выключено");
        public static string HotkeyChange => T("Change shortcut", "Mudar atalho", "Cambiar atajo", "Изменить сочетание");
        public static string HotkeyPress => T("Press the new keys… (Esc cancels)", "Aperte as novas teclas… (Esc cancela)", "Pulsa las nuevas teclas… (Esc cancela)", "Нажмите новые клавиши… (Esc — отмена)");
        public static string HotkeyTaken => T("Another program already uses this shortcut", "Outro programa já usa este atalho", "Otro programa ya usa este atajo", "Это сочетание уже занято другой программой");
        public static string MenuFocus => T("Do not disturb", "Não perturbe", "No molestar", "Не беспокоить");
        public static string HintFocus => T("A small moon on the island while Do not disturb or a focus session is on",
                                            "Uma luazinha na ilha enquanto o Não perturbe ou uma sessão de foco está ligada",
                                            "Una lunita en la isla mientras No molestar o una sesión de enfoque está activa",
                                            "Маленькая луна на острове, пока включён режим «Не беспокоить» или сеанс фокусировки");
        public static string FocusOn => T("Do not disturb on", "Não perturbe ligado", "No molestar activado", "«Не беспокоить» включён");
        public static string FocusOff => T("Do not disturb off", "Não perturbe desligado", "No molestar desactivado", "«Не беспокоить» выключен");
        public static string SectionApi => T("Scripts and apps", "Scripts e apps", "Scripts y apps", "Скрипты и приложения");
        public static string MenuApi => T("Local API", "API local", "API local", "Локальный API");
        public static string HintApi => T("Lets scripts and apps on this PC show activities and alerts in the island. The island command and the browser extension already work with it, no token needed",
                                          "Deixa scripts e apps deste PC mostrarem atividades e avisos na ilha. O comando island e a extensão do navegador já funcionam com ela, sem token",
                                          "Permite que scripts y apps de este PC muestren actividades y avisos en la isla. El comando island y la extensión del navegador ya funcionan con ella, sin token",
                                          "Позволяет скриптам и приложениям на этом ПК показывать действия и уведомления на острове. Команда island и расширение для браузера уже работают с ней, токен не нужен");
        public static string ApiTokenLabel => T("Token for your own scripts", "Token para seus próprios scripts", "Token para tus propios scripts", "Токен для ваших скриптов");
        public static string ApiTokenHint => T("Only if you write your own script or app: send it in the header Authorization: Bearer <token>",
                                               "Só se você criar seu próprio script ou app: envie no cabeçalho Authorization: Bearer <token>",
                                               "Solo si creas tu propio script o app: envíalo en el encabezado Authorization: Bearer <token>",
                                               "Только если вы пишете свой скрипт или приложение: передавайте в заголовке Authorization: Bearer <token>");
        public static string ApiGuide => T("How to use the API (examples)", "Como usar a API (exemplos)", "Cómo usar la API (ejemplos)", "Как пользоваться API (примеры)");
        public static string ApiCopyToken => T("Copy token", "Copiar token", "Copiar token", "Скопировать токен");
        public static string ApiTokenCopied => T("Token copied", "Token copiado", "Token copiado", "Токен скопирован");
        public static string ApiNewToken => T("Make a new token", "Gerar um novo token", "Generar un token nuevo", "Создать новый токен");
        public static string ApiNewTokenDone => T("New token made. Apps using the old one must be updated", "Novo token gerado. Apps que usavam o antigo precisam ser atualizados", "Token nuevo generado. Las apps con el anterior deben actualizarse", "Токен создан. Приложения со старым токеном нужно обновить");
        public static string ExtInstall => T("Install browser extension", "Instalar extensão do navegador", "Instalar extensión del navegador", "Установить расширение для браузера");
        public static string ExtHint => T("Gives web audio the site's icon. Opens the browser's extensions page", "Dá ao áudio da web o ícone do site. Abre a página de extensões do navegador", "Da al audio web el icono del sitio. Abre la página de extensiones del navegador", "Показывает значок сайта для веб-аудио. Открывает страницу расширений браузера");
        public static string ExtSteps(string folder) => T("The folder path is copied. In the browser: 1) turn on Developer mode, 2) click Load unpacked, 3) paste the path (Ctrl+V) and confirm.",
                                                          "O caminho da pasta foi copiado. No navegador: 1) ligue o Modo do desenvolvedor (ou Modo de programador), 2) clique em Carregar sem compactação (ou Carregar descompactada), 3) cole o caminho (Ctrl+V) e confirme.",
                                                          "La ruta de la carpeta está copiada. En el navegador: 1) active el Modo desarrollador, 2) pulse Cargar descomprimida, 3) pegue la ruta (Ctrl+V) y confirma.",
                                                          "Путь к папке скопирован. В браузере: 1) включите режим разработчика, 2) нажмите «Загрузить распакованное», 3) вставьте путь (Ctrl+V) и подтвердите.");
        public static string ExtStepsNoCopy(string folder) => T("Could not copy the path, so its folder is open. In the browser: 1) turn on Developer mode, 2) click Load unpacked, 3) pick this folder: " + folder,
                                                               "Não consegui copiar o caminho, então a pasta foi aberta. No navegador: 1) ligue o Modo do desenvolvedor (ou Modo de programador), 2) clique em Carregar sem compactação (ou Carregar descompactada), 3) escolha esta pasta: " + folder,
                                                               "No pude copiar la ruta, así que la carpeta está abierta. En el navegador: 1) active el Modo desarrollador, 2) pulse Cargar descomprimida, 3) elige esta carpeta: " + folder,
                                                               "Не удалось скопировать путь, поэтому папка открыта. В браузере: 1) включите режим разработчика, 2) нажмите «Загрузить распакованное», 3) выберите папку: " + folder);
        public static string ExtNoBrowser => T("That browser was not found. The folder is ready:", "Esse navegador não foi encontrado. A pasta está pronta:", "No se encontró ese navegador. La carpeta está lista:", "Браузер не найден. Папка готова:");
        public static string ExtFailed => T("Could not prepare the extension", "Não foi possível preparar a extensão", "No se pudo preparar la extensión", "Не удалось подготовить расширение");
        public static string ApiPortBusy => T("The port is in use by another program", "A porta está em uso por outro programa", "El puerto está en uso por otro programa", "Порт занят другой программой");
        public static string MenuSystemMonitor => T("CPU and memory on the island", "CPU e memória na ilha", "CPU y memoria en la isla", "ЦП и память на острове");
        public static string HintSystemMonitor => T("Keeps a small monitor on the island when nothing else is happening", "Mantém um pequeno monitor na ilha quando nada mais está acontecendo", "Mantiene un pequeño monitor en la isla cuando no pasa nada más", "Показывает маленький монитор на острове, когда больше ничего не происходит");
        public static string MonitorStyleLabel => T("Monitor style", "Estilo do monitor", "Estilo del monitor", "Стиль монитора");
        public static string StyleRings => T("Rings", "Anéis", "Anillos", "Кольца");
        public static string StyleBars => T("Bars", "Barras", "Barras", "Полосы");
        public static string StyleNumbers => T("Numbers", "Números", "Números", "Числа");
        public static string MenuHideOsd => T("Hide Windows' volume popup (experimental)", "Esconder a barra de volume do Windows (experimental)", "Ocultar la barra de volumen de Windows (experimental)", "Скрывать окно громкости Windows (экспериментально)");
        public static string HintHideOsd => T("Shows only the island's bar. Needs the volume bar above. A Windows update can stop it from working; turn it off to get the popup back",
                                              "Mostra só a barra da ilha. Precisa da barra de volume acima. Uma atualização do Windows pode quebrar isso; desligue para a barra voltar",
                                              "Muestra solo la barra de la isla. Requiere la barra de volumen de arriba. Una actualización de Windows puede romperlo; desactívelo para recuperar la barra",
                                              "Показывает только полосу острова. Нужна полоса громкости выше. Обновление Windows может это сломать; выключите, чтобы вернуть окно");
        public static string MenuLevels => T("Volume and brightness", "Volume e brilho", "Volumen y brillo", "Громкость и яркость");
        public static string HintLevels => T("A small bar while you change the volume or the screen brightness (laptop screens only)",
                                             "Uma barrinha enquanto você muda o volume ou o brilho da tela (só telas de notebook)",
                                             "Una barrita mientras cambias el volumen o el brillo de la pantalla (solo pantallas de portátil)",
                                             "Небольшая полоска при изменении громкости или яркости экрана (только экраны ноутбуков)");
        public static string PageSystemLabel => T("Clock and system page", "Página de relógio e sistema", "Página de reloj y sistema", "Страница часов и системы");
        public static string CpuLabel => T("Processor", "Processador", "Procesador", "Процессор");
        public static string MemoryLabel => T("Memory", "Memória", "Memoria", "Память");
        public static string MenuBatteryActivity => T("Battery while charging", "Bateria ao carregar", "Batería al cargar", "Батарея при зарядке");
        public static string HintBatteryActivity => T("Shows the charge level in the island while the charger is plugged in",
                                                      "Mostra o nível de carga na ilha enquanto o carregador está ligado",
                                                      "Muestra el nivel de carga en la isla mientras el cargador está conectado",
                                                      "Показывает уровень заряда на острове, пока подключена зарядка");
        public static string MenuBatteryAlerts => T("Battery alerts", "Avisos de bateria", "Avisos de batería", "Оповещения о батарее");
        public static string HintBatteryAlerts => T("Charger and low battery, on laptops",
                                                    "Carregador e bateria baixa, em notebooks",
                                                    "Cargador y batería baja, en portátiles",
                                                    "Зарядное устройство и низкий заряд, на ноутбуках");
        public static string BatteryCharging => T("Charging", "Carregando", "Cargando", "Зарядка");
        public static string BatteryUnplugged => T("Charger disconnected", "Carregador desconectado", "Cargador desconectado", "Зарядное отключено");
        public static string BatteryFull => T("Battery full", "Bateria carregada", "Batería cargada", "Батарея заряжена");
        public static string BatteryLow => T("Battery low", "Bateria baixa", "Batería baja", "Низкий заряд батареи");

        public static string MenuScreenshot => T("Screenshot alert", "Aviso de captura de tela", "Aviso de captura de pantalla", "Оповещение о скриншоте");
        public static string HintScreenshot => T("A small note when a screenshot is taken with Print Screen or Win+Shift+S",
                                                 "Um aviso pequeno quando você tira um print com Print Screen ou Win+Shift+S",
                                                 "Un aviso pequeño cuando haces una captura con Impr Pant o Win+Mayús+S",
                                                 "Небольшое сообщение, когда делается скриншот клавишей Print Screen или Win+Shift+S");
        public static string ScreenshotTaken => T("Screenshot taken", "Tela capturada", "Captura hecha", "Скриншот сделан");
        public static string LanguageLabel => T("Language", "Idioma", "Idioma", "Язык");
        public static string LanguageAuto => T("Auto", "Auto", "Auto", "Авто");
        public static string BannerSizeLabel => T("Alert size", "Tamanho dos avisos", "Tamaño de los avisos", "Размер оповещений");
        public static string BannerSmall => T("Small", "Pequeno", "Pequeño", "Малый");
        public static string BannerNormal => T("Normal", "Normal", "Normal", "Обычный");
    }
}
