using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowsIsland
{
    // One global shortcut, such as Ctrl+Alt+I. It is registered with Windows' own hotkey call, so the app never
    // sees any other key press and no keyboard hook is installed. Off unless the user turns it on.
    sealed class HotkeyService : IDisposable
    {
        const int WM_HOTKEY = 0x0312, HotkeyId = 0x4953;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public event Action Pressed;

        HwndSource window;
        string registered;

        // True when the shortcut is now active. False when it is off, invalid, or taken by another program.
        public bool Apply(bool enabled, string combo)
        {
            if (!enabled) { Unregister(); return false; }
            if (window != null && registered == combo) return true;
            Unregister();
            uint modifiers;
            Key key;
            if (!TryParse(combo, out modifiers, out key)) return false;
            if (window == null)
            {
                HwndSourceParameters parameters = new HwndSourceParameters("WindowsIslandHotkey") { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 0, Height = 0 };
                window = new HwndSource(parameters);
                window.AddHook(WndProc);
            }
            if (!RegisterHotKey(window.Handle, HotkeyId, modifiers | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key)))
            {
                return false;
            }
            registered = combo;
            return true;
        }

        void Unregister()
        {
            if (window == null) return;
            UnregisterHotKey(window.Handle, HotkeyId);
            window.RemoveHook(WndProc);
            window.Dispose();
            window = null;
            registered = null;
        }

        public void Dispose() { Unregister(); }

        IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                Pressed?.Invoke();
            }
            return IntPtr.Zero;
        }

        // "Ctrl+Alt+I" -> modifiers and key. At least one modifier is required, so typing never triggers it.
        public static bool TryParse(string combo, out uint modifiers, out Key key)
        {
            modifiers = 0;
            key = Key.None;
            if (string.IsNullOrWhiteSpace(combo)) return false;
            foreach (string part in combo.Split('+'))
            {
                string name = part.Trim();
                if (name.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_CONTROL;
                else if (name.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_ALT;
                else if (name.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_SHIFT;
                else if (name.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_WIN;
                else if (!Enum.TryParse(name, true, out key) || key == Key.None) return false;
            }
            return modifiers != 0 && key != Key.None;
        }

        // The text saved in settings for a pressed combination, or null when it is not a usable shortcut.
        public static string Format(ModifierKeys modifiers, Key key)
        {
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin ||
                key == Key.System || key == Key.None) return null;
            if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) return null; // Shift alone is just typing
            List<string> parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }
}
