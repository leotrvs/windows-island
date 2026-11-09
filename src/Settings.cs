using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace WindowsIsland
{
    public sealed class Settings
    {
        public string Position { get; set; } = "Top"; // Top or Bottom
        public string Monitor { get; set; } = "Primary"; // Primary, Active (follows the mouse), All, or a device name
        public double OffsetY { get; set; } = 5;
        public double OffsetX { get; set; } = 0;             // From the screen's center, in DIPs
        public double CollapsedScale { get; set; } = 0.9;    // Idle and compact island
        public double ExpandedScale { get; set; } = 0.95;     // Expanded island and alerts
        public bool ShowIdlePill { get; set; } = true;
        public bool EmptyIsland { get; set; } = false; // Plain pill only; activities show when it expands
        public int AlertSeconds { get; set; } = 3;       // Claude alerts, notifications and app messages
        public int PausedMusicSeconds { get; set; } = 0; // How long a paused song stays on the island
        public int AutoHideSeconds { get; set; } = 0;    // Hide the idle island after this long; 0 = never
        public bool ExpandOnHover { get; set; } = true;
        public bool ShowMusic { get; set; } = true;
        public bool ShowClaude { get; set; } = true;
        public bool ClaudeBanners { get; set; } = true;
        public bool PermissionPrompts { get; set; } = false; // Allow or deny Claude's permission requests on the island (installs a hook)
        public string Theme { get; set; } = "Auto"; // Colors of the Settings window: Auto (follows Windows), Dark or Light
        public System.Collections.Generic.List<string> PrivacyIgnored { get; set; } = new System.Collections.Generic.List<string>(); // Apps whose microphone, camera or screen use shows no dot
        public System.Collections.Generic.List<string> PrivacySeen { get; set; } = new System.Collections.Generic.List<string>();    // Apps seen using them, offered in Settings to ignore
        public bool DiscordEnabled { get; set; } = false;   // Show when you are muted or deafened in Discord (needs your own Discord app, see docs/DISCORD.md)
        public string DiscordClientId { get; set; } = "";
        public bool MonitorKeepsPlace { get; set; } = true; // With the system monitor on, an activity (music, Claude...) is a bubble beside it instead of replacing it
        public bool ShowSteamDownloads { get; set; } = true; // Progress of Steam downloads and updates, read from Steam's manifest files
        public string IdleStyle { get; set; } = "Line"; // When nothing is happening: Pill (the small pill) or Line (a thin line)
        public string MaximizedMode { get; set; } = "Thin"; // With a maximized window in front: Thin (a thin line), Normal (as always) or Hidden
        public bool ShowFootball { get; set; } = false;  // The live score of one team, from ESPN's public scoreboard (asks the internet, so it starts off)
        public string FootballTeam { get; set; } = "";   // A name or an abbreviation
        public string FootballLeagues { get; set; } = "bra.1"; // ESPN league codes, separated by commas
        public bool ShowObs { get; set; } = true;        // OBS recording or streaming, read from OBS's own log
        public bool ShowWindowsUpdate { get; set; } = true; // Windows installing updates, and the restart that follows
        public bool AlwaysThin { get; set; } = false; // The collapsed island is the thin line with a tiny icon of what matters most; hover to open it
        public const int CurrentVersion = 4;
        public int SettingsVersion { get; set; } = 0;        // Settings from an older version are started again, with the welcome
        public bool OnboardingDone { get; set; } = true;     // False until the welcome steps were gone through or skipped
        public string SettingsMode { get; set; } = "Essentials"; // Essentials (a few switches) or Advanced (every setting)
        public string WhatsNewSeen { get; set; } = "";       // The release whose news card was dismissed
        public int PomodoroFocus { get; set; } = 25;   // Minutes
        public int PomodoroBreak { get; set; } = 5;
        public int PomodoroLong { get; set; } = 15;
        public bool MinimalMusicPosition { get; set; } = false; // The thin line fills along the song while music plays
        public bool ThinDefaultsApplied { get; set; } = false; // From 4.5.1: copies from before get "thin line with a maximized window" and "activities too" switched on once
        public bool MaximizedActivitiesThin { get; set; } = true; // With a maximized window and the thin line: activities too become the thin line (hover to see them)
        public bool ShowScreenShare { get; set; } = true; // A blue dot while the screen is shared or recorded (needs the privacy dots)
        public bool LimitAt75 { get; set; } = true;     // Which limit alerts to show
        public bool LimitAt90 { get; set; } = true;
        public bool LimitAt100 { get; set; } = true;
        public bool LimitRenewed { get; set; } = true;
        public int LimitAlertSeconds { get; set; } = 4;     // How long a limit alert stays
        public int NotificationSeconds { get; set; } = 3;   // How long a Windows notification stays
        public bool ShowLimitAlerts { get; set; } = true; // Claude 5-hour and weekly limits at 75, 90 and 100% and when they renew
        public bool ShowCodex { get; set; } = true;     // Only shows anything once Codex is installed
        public bool CodexBanners { get; set; } = true;
        public string Language { get; set; } = "Auto";   // Auto follows Windows; or en, pt, es, ru
        public string BannerSize { get; set; } = "Small"; // Small or Normal
        public string RingColor { get; set; } = "Blue";  // Usage rings: Blue, Green or Purple
        // Which pages the expanded island offers (scroll or click the dots to switch between them)
        public bool PageMusic { get; set; } = true;
        public bool PageClaude { get; set; } = true;
        public bool PageTimer { get; set; } = true;
        public bool PageSystem { get; set; } = true;
        public bool PageQuick { get; set; } = true;     // The shortcuts page: mute the microphone or speakers, screenshot, volume
        public string PageOrder { get; set; } = "Music,Claude,System,Timer,Quick"; // The order of the pages when the island is open

        public static readonly string[] PageNames = { "Music", "Claude", "System", "Timer", "Quick" };

        // The saved order, with anything missing or unknown fixed: every page exactly once.
        public System.Collections.Generic.List<string> PageOrderList()
        {
            System.Collections.Generic.List<string> order = new System.Collections.Generic.List<string>();
            foreach (string name in (PageOrder ?? "").Split(','))
            {
                string known = Array.Find(PageNames, n => string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (known != null && !order.Contains(known)) order.Add(known);
            }
            foreach (string name in PageNames) if (!order.Contains(name)) order.Add(name);
            return order;
        }
        public bool ShowDeviceAlerts { get; set; } = true;  // Bluetooth and USB devices connecting
        public bool ShowNetworkAlerts { get; set; } = true; // Going offline and coming back
        public bool HotkeyEnabled { get; set; } = false;   // A global shortcut that opens and closes the island: off by default
        public string HotkeyCombo { get; set; } = "Ctrl+Alt+I";
        public bool IslandQuiet { get; set; } = false; // Do not disturb for the island only: banners stay away (the tray menu turns it on)
        public bool ShowFocus { get; set; } = true; // A small moon while Do not disturb is on
        // Where the Settings window was left (size and place), so it opens the same way next time
        public double? SettingsLeft { get; set; }
        public double? SettingsTop { get; set; }
        public double? SettingsWidth { get; set; }
        public double? SettingsHeight { get; set; }
        public bool SystemMonitor { get; set; } = false; // CPU and memory on the collapsed island when nothing else is happening
        public string MonitorStyle { get; set; } = "Rings"; // Rings, Bars or Numbers
        public bool HideWindowsOsd { get; set; } = false; // Experimental: hide Windows' own volume popup
        public bool ShowLevels { get; set; } = true; // Volume and brightness while they change
        public bool ShowPrivacyDots { get; set; } = true;
        public int PrivacyNameSeconds { get; set; } = 0; // How long the app's name shows by itself when an app starts recording (0 = only on hover)
        public bool BatteryAlerts { get; set; } = true;
        public bool BatteryActivity { get; set; } = true; // Charging shows in the island like any other activity
        public bool ShowScreenshotAlert { get; set; } = true;
        public bool ShowNotifications { get; set; } = true; // Reads notification content: on by default, asked again in the welcome
        public bool HideInFullscreen { get; set; } = true;
        public bool StartWithWindows { get; set; } = true;
        public bool AutoUpdate { get; set; } = true;
        public bool ApiEnabled { get; set; } = false; // Local API for scripts and apps, on 127.0.0.1 only: off by default
        public int ApiPort { get; set; } = LocalApi.DefaultPort;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "WindowsIsland";

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsIsland"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "settings.json"); }
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Settings loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                    if (loaded.SettingsVersion < CurrentVersion) loaded = Started(loaded);
                    if (!loaded.ThinDefaultsApplied)
                    {
                        // Once: the thin line stays out of the way of the tabs when a window is maximized, and activities become the line too
                        loaded.MaximizedMode = "Thin";
                        loaded.MaximizedActivitiesThin = true;
                        loaded.ThinDefaultsApplied = true;
                        loaded.Save();
                    }
                    return loaded.Sanitized();
                }
            }
            catch (Exception)
            {
                // A broken file falls back to the defaults instead of stopping the app.
            }
            return Started(null);
        }

        // The settings of someone new, or of someone from before the welcome existed: the defaults of this version, and the
        // welcome. What is a login, a key or a list the person built is kept (language, Discord, the API, the hook, the apps
        // hidden from the privacy dots and where the Settings window sits).
        static Settings Started(Settings old)
        {
            Settings fresh = new Settings { SettingsVersion = CurrentVersion, OnboardingDone = false, WhatsNewSeen = WhatsNew.Key }; // Someone starting out has the welcome, not the news
            if (old != null)
            {
                fresh.Language = old.Language;
                fresh.DiscordEnabled = old.DiscordEnabled;
                fresh.DiscordClientId = old.DiscordClientId;
                fresh.ApiEnabled = old.ApiEnabled;
                fresh.ApiPort = old.ApiPort;
                fresh.PermissionPrompts = old.PermissionPrompts;
                fresh.PrivacyIgnored = old.PrivacyIgnored ?? new System.Collections.Generic.List<string>();
                fresh.PrivacySeen = old.PrivacySeen ?? new System.Collections.Generic.List<string>();
                fresh.SettingsLeft = old.SettingsLeft;
                fresh.SettingsTop = old.SettingsTop;
                fresh.SettingsWidth = old.SettingsWidth;
                fresh.SettingsHeight = old.SettingsHeight;
            }
            return fresh;
        }

        // The file is hand-editable, so values are kept within sensible bounds.
        Settings Sanitized()
        {
            if (!string.Equals(IdleStyle, "Line", StringComparison.OrdinalIgnoreCase)) IdleStyle = "Pill";
            if (!string.Equals(MaximizedMode, "Normal", StringComparison.OrdinalIgnoreCase) && !string.Equals(MaximizedMode, "Hidden", StringComparison.OrdinalIgnoreCase)) MaximizedMode = "Thin";
            if (!string.Equals(SettingsMode, "Advanced", StringComparison.OrdinalIgnoreCase)) SettingsMode = "Essentials";
            AlertSeconds = Math.Clamp(AlertSeconds, 1, 60);
            LimitAlertSeconds = Math.Clamp(LimitAlertSeconds, 1, 60);
            NotificationSeconds = Math.Clamp(NotificationSeconds, 1, 60);
            PausedMusicSeconds = Math.Clamp(PausedMusicSeconds, 0, 600);
            AutoHideSeconds = Math.Clamp(AutoHideSeconds, 0, 3600);
            OffsetY = Math.Clamp(OffsetY, MinOffsetY, MaxOffsetY);
            OffsetX = Math.Clamp(OffsetX, -MaxOffsetX, MaxOffsetX);
            CollapsedScale = Math.Clamp(CollapsedScale, MinScale, MaxScale);
            ExpandedScale = Math.Clamp(ExpandedScale, MinScale, MaxScale);
            ApiPort = Math.Clamp(ApiPort, LocalApi.MinPort, LocalApi.MaxPort);
            return this;
        }

        public const double MinScale = 0.8, MaxScale = 1.3;
        public const double MinOffsetY = 0, MaxOffsetY = 40;
        public const double MaxOffsetX = 600;

        public void ResetAppearance()
        {
            OffsetY = 5;
            OffsetX = 0;
            CollapsedScale = 0.9;
            ExpandedScale = 0.95;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
            }
            ApplyStartup();
        }

        void ApplyStartup()
        {
            if (!StartWithWindows)
            {
                RemoveStartup();
                return;
            }
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    key?.SetValue(RunValue, "\"" + Installer.StartupTarget + "\"");
            }
            catch (Exception)
            {
            }
        }

        public static void RemoveStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    key?.DeleteValue(RunValue, false);
            }
            catch (Exception)
            {
            }
        }

        public static void DeleteAll()
        {
            try
            {
                if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
