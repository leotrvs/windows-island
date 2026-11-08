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
        public int AlertSeconds { get; set; } = 5;       // Claude alerts, notifications and app messages
        public int PausedMusicSeconds { get; set; } = 5; // How long a paused song stays on the island
        public int AutoHideSeconds { get; set; } = 0;    // Hide the idle island after this long; 0 = never
        public bool ExpandOnHover { get; set; } = true;
        public bool ShowMusic { get; set; } = true;
        public bool ShowClaude { get; set; } = true;
        public bool ClaudeBanners { get; set; } = true;
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
        public bool ShowDeviceAlerts { get; set; } = true;  // Bluetooth and USB devices connecting
        public bool ShowNetworkAlerts { get; set; } = true; // Going offline and coming back
        public bool HotkeyEnabled { get; set; } = false;   // A global shortcut that opens and closes the island: off by default
        public string HotkeyCombo { get; set; } = "Ctrl+Alt+I";
        public bool ShowFocus { get; set; } = true; // A small moon while Do not disturb is on
        public bool HideWindowsOsd { get; set; } = false; // Experimental: hide Windows' own volume popup
        public bool ShowLevels { get; set; } = true; // Volume and brightness while they change
        public bool ShowPrivacyDots { get; set; } = true;
        public int PrivacyNameSeconds { get; set; } = 0; // How long the app's name shows by itself when an app starts recording (0 = only on hover)
        public bool BatteryAlerts { get; set; } = true;
        public bool BatteryActivity { get; set; } = true; // Charging shows in the island like any other activity
        public bool ShowScreenshotAlert { get; set; } = true;
        public bool ShowNotifications { get; set; } = false; // Reads notification content, so it is opt-in
        public bool HideInFullscreen { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool AutoUpdate { get; set; } = true;
        public bool ApiEnabled { get; set; } = false; // Local API for scripts and apps, on 127.0.0.1 only: off by default
        public int ApiPort { get; set; } = LocalApi.DefaultPort;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "WindowsIsland";

        static string Folder
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
                    return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings()).Sanitized();
            }
            catch (Exception)
            {
                // A broken file falls back to the defaults instead of stopping the app.
            }
            return new Settings();
        }

        // The file is hand-editable, so values are kept within sensible bounds.
        Settings Sanitized()
        {
            AlertSeconds = Math.Clamp(AlertSeconds, 1, 60);
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
