using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace WindowsIsland
{
    // The colors of the Settings window, dark or light. The island itself is always black; only this window follows
    // the choice (Auto follows the Windows app theme).
    static class SettingsTheme
    {
        public static bool Light { get; private set; }

        public static Color WindowBack, Text, Card, Separator, Secondary, Accent, On, SwitchOff, TabTrack, SegTrack,
                            Pill, Hover, Icon, SliderTrack, ChipHover, Thumb, Border;

        public static void Choose(string setting)
        {
            Light = string.Equals(setting, "Light", StringComparison.OrdinalIgnoreCase) ||
                    (!string.Equals(setting, "Dark", StringComparison.OrdinalIgnoreCase) && WindowsUsesLight());
            if (Light)
            {
                WindowBack = Color.FromRgb(0xF2, 0xF2, 0xF7);
                Text = Colors.Black;
                Card = Colors.White;
                Separator = Color.FromRgb(0xE5, 0xE5, 0xEA);
                Secondary = Color.FromRgb(0x6C, 0x6C, 0x70);
                Accent = Color.FromRgb(0x00, 0x7A, 0xFF);
                On = Color.FromRgb(0x34, 0xC7, 0x59);
                SwitchOff = Color.FromRgb(0xE5, 0xE5, 0xEA);
                TabTrack = Color.FromRgb(0xE3, 0xE3, 0xE8);
                SegTrack = Color.FromRgb(0xE3, 0xE3, 0xE8);
                Pill = Colors.White;
                Hover = Color.FromRgb(0xE5, 0xE5, 0xEA);
                Icon = Color.FromRgb(0x63, 0x63, 0x66);
                SliderTrack = Color.FromRgb(0xD1, 0xD1, 0xD6);
                ChipHover = Colors.White;
                Thumb = Color.FromArgb(0x40, 0, 0, 0);
                Border = Color.FromRgb(0xD1, 0xD1, 0xD6);
            }
            else
            {
                WindowBack = Colors.Black;
                Text = Colors.White;
                Card = Color.FromRgb(0x1C, 0x1C, 0x1E);
                Separator = Color.FromRgb(0x38, 0x38, 0x3A);
                Secondary = Color.FromRgb(0x8E, 0x8E, 0x93);
                Accent = Color.FromRgb(0x0A, 0x84, 0xFF);
                On = Color.FromRgb(0x30, 0xD1, 0x58);
                SwitchOff = Color.FromRgb(0x39, 0x39, 0x3D);
                TabTrack = Color.FromRgb(0x1C, 0x1C, 0x1E);
                SegTrack = Color.FromRgb(0x2C, 0x2C, 0x2E);
                Pill = Color.FromRgb(0x63, 0x63, 0x66);
                Hover = Color.FromRgb(0x2C, 0x2C, 0x2E);
                Icon = Color.FromRgb(0xB4, 0xB4, 0xBC);
                SliderTrack = Color.FromRgb(0x3A, 0x3A, 0x3C);
                ChipHover = Color.FromRgb(0x63, 0x63, 0x66);
                Thumb = Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);
                Border = Color.FromRgb(0x2E, 0x2E, 0x38);
            }
        }

        // The Windows setting "Choose your default app mode".
        static bool WindowsUsesLight()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 1;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The window border color as DWM wants it: 0x00BBGGRR.
        public static int BorderColorRef
        {
            get { return (Border.B << 16) | (Border.G << 8) | Border.R; }
        }
    }
}
