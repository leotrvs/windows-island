using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace WindowsIsland
{
    public sealed class Settings
    {
        public string Position { get; set; } = "Top"; // Top or Bottom
        public double OffsetY { get; set; } = 8;
        public bool ShowIdlePill { get; set; } = true;
        public bool ExpandOnHover { get; set; } = true;
        public bool ShowMusic { get; set; } = true;
        public bool ShowClaude { get; set; } = true;
        public bool ClaudeBanners { get; set; } = true;
        public bool HideInFullscreen { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;

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
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch (Exception)
            {
                // A broken file falls back to the defaults instead of stopping the app.
            }
            return new Settings();
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
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (StartWithWindows && Environment.ProcessPath != null)
                        key.SetValue(RunValue, "\"" + Environment.ProcessPath + "\"");
                    else
                        key.DeleteValue(RunValue, false);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
