using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Rows with their own logic: the language, hidden apps, Discord, the monitor, the local API and the hotkey.
    public partial class SettingsWindow
    {
        void LanguageRow()
        {
            string[] values = { "Auto", "en", "pt", "es", "ru" };
            string[] names = { Strings.LanguageAuto, "English", "Português", "Español", "Русский" };
            int current = Math.Max(0, Array.FindIndex(values, v => string.Equals(v, settings.Language, StringComparison.OrdinalIgnoreCase)));
            StackPanel flags = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                Border chip = new Border
                {
                    Width = 34, Height = 28, CornerRadius = new CornerRadius(8), Margin = new Thickness(2, 0, 0, 0),
                    Background = index == current ? new SolidColorBrush(SettingsTheme.Pill) : Brushes.Transparent,
                    BorderBrush = index == current ? new SolidColorBrush(SettingsTheme.Accent) : Brushes.Transparent, BorderThickness = new Thickness(1.2),
                    Cursor = System.Windows.Input.Cursors.Hand, ToolTip = names[i],
                    Child = Flag(values[i]),
                };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    settings.Language = values[index];
                    Strings.Use(settings.Language);
                    Apply();
                    Dispatcher.BeginInvoke(new Action(RefreshTexts)); // After this click has finished
                };
                flags.Children.Add(chip);
            }
            Row("\uE774", Strings.LanguageLabel, null, flags);
        }

        // A small flag drawn in 20 x 14 (the system's fonts have no flag emoji): a globe for "automatic"
        FrameworkElement Flag(string code)
        {
            if (code == "Auto")
                return new TextBlock { Text = "\uE774", FontFamily = IconFont, FontSize = 15, Foreground = IconBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Canvas canvas = new Canvas { Width = 20, Height = 14 };
            Func<string, Brush> brush = hex => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            Action<double, double, double, double, string> rect = (x, y, w, h, hex) =>
            {
                System.Windows.Shapes.Rectangle r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = brush(hex) };
                Canvas.SetLeft(r, x); Canvas.SetTop(r, y); canvas.Children.Add(r);
            };
            Action<double, double, double, double, string, double> line = (x1, y1, x2, y2, hex, width) =>
                canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush(hex), StrokeThickness = width });
            if (code == "pt")
            {
                rect(0, 0, 20, 14, "#009B3A");
                canvas.Children.Add(new System.Windows.Shapes.Polygon { Points = new PointCollection { new Point(10, 1.6), new Point(18.4, 7), new Point(10, 12.4), new Point(1.6, 7) }, Fill = brush("#FEDF00") });
                System.Windows.Shapes.Ellipse disc = new System.Windows.Shapes.Ellipse { Width = 6.6, Height = 6.6, Fill = brush("#002776") };
                Canvas.SetLeft(disc, 6.7); Canvas.SetTop(disc, 3.7); canvas.Children.Add(disc);
            }
            else if (code == "es")
            {
                rect(0, 0, 20, 14, "#AA151B");
                rect(0, 3.5, 20, 7, "#F1BF00");
            }
            else if (code == "ru")
            {
                rect(0, 0, 20, 4.7, "#FFFFFF");
                rect(0, 4.7, 20, 4.7, "#0039A6");
                rect(0, 9.4, 20, 4.6, "#D52B1E");
            }
            else
            {
                rect(0, 0, 20, 14, "#012169");
                line(0, 0, 20, 14, "#FFFFFF", 3); line(20, 0, 0, 14, "#FFFFFF", 3);
                line(0, 0, 20, 14, "#C8102E", 1.1); line(20, 0, 0, 14, "#C8102E", 1.1);
                rect(0, 4.6, 20, 4.8, "#FFFFFF"); rect(8.1, 0, 3.8, 14, "#FFFFFF");
                rect(0, 5.7, 20, 2.6, "#C8102E"); rect(8.9, 0, 2.2, 14, "#C8102E");
            }
            return new Border { Width = 20, Height = 14, CornerRadius = new CornerRadius(2.5), ClipToBounds = true, Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        // One row for every app whose dot is hidden: hidden ones are chips with an x, apps seen lately are chips with a +.
        void HiddenAppsRow()
        {
            WrapPanel chips = new WrapPanel { Margin = new Thickness(44, 0, 14, 10) };
            foreach (string name in settings.PrivacyIgnored.ToArray())
            {
                string app = name;
                chips.Children.Add(Chip(app + "  \u2715", true, () => { settings.PrivacyIgnored.RemoveAll(n => string.Equals(n, app, StringComparison.OrdinalIgnoreCase)); Apply(); Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
            }
            foreach (string name in settings.PrivacySeen.ToArray())
            {
                string app = name;
                if (settings.PrivacyIgnored.Contains(app, StringComparer.OrdinalIgnoreCase)) continue;
                chips.Children.Add(Chip("+  " + app, false, () => { settings.PrivacyIgnored.Add(app); Apply(); Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
            }
            Row("\uE77B", Strings.MenuHiddenApps, Strings.HintHiddenApps, null, chips);
        }

        // The Discord rows: the switch, the app's id and secret, the state of the connection and a guide.
        void DiscordRows()
        {
            DiscordService discord = (Application.Current as App)?.Discord;
            Toggle("\uE720", Strings.MenuDiscord, Strings.HintDiscord, () => settings.DiscordEnabled, v => settings.DiscordEnabled = v);
            InputRow(Strings.DiscordClientIdLabel, () => settings.DiscordClientId, v => settings.DiscordClientId = (v ?? "").Trim(), false);
            InputRow(Strings.DiscordClientSecretLabel, () => DiscordSecrets.Load().ClientSecret, v =>
            {
                DiscordSecrets secrets = DiscordSecrets.Load();
                secrets.ClientSecret = (v ?? "").Trim();
                secrets.AccessToken = secrets.RefreshToken = ""; // A new secret means a new authorization
                secrets.Save();
                if (settings.DiscordEnabled && discord != null) discord.Restart(settings.DiscordClientId);
            }, true);

            TextBlock status = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(60, 0, 14, 10) };
            Action show = () => status.Text = discord == null || !settings.DiscordEnabled ? "" : Strings.DiscordStatus(discord.Status, discord.Detail);
            show();
            if (discord != null)
            {
                Action changed = () => Dispatcher.BeginInvoke(show);
                discord.Changed += changed;
                Closed += (s, e) => discord.Changed -= changed;
            }
            ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(status);

            Button guide = new Button { Content = Strings.DiscordGuide, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            guide.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/blob/main/docs/DISCORD.md") { UseShellExecute = true }); } catch (Exception) { } };
            Row("\uE897", null, null, null, null, guide);
            ActionRow("\uE74D", Strings.DiscordForget, () =>
            {
                // The secret and the tokens are deleted, the id cleared and the connection closed
                DiscordSecrets.Delete();
                settings.DiscordClientId = "";
                settings.DiscordEnabled = false;
                Apply();
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
            }, true);
        }

        // A text box (or a password box) under its label; the value is saved when the box loses the focus.
        void InputRow(string label, Func<string> get, Action<string> set, bool secret)
        {
            Control box;
            string shown = get() ?? "";
            if (secret)
            {
                PasswordBox password = new PasswordBox { Password = shown.Length == 0 ? "" : new string('\u2022', 12) };
                bool touched = false;
                password.PasswordChanged += (s, e) => touched = true;
                password.GotFocus += (s, e) => { if (!touched && shown.Length > 0) password.Clear(); }; // A saved secret is not shown: typing replaces it
                password.LostFocus += (s, e) =>
                {
                    string mask = new string('\u2022', 12);
                    if (touched && password.Password.Length > 0 && password.Password != mask) { set(password.Password); Apply(); }
                    password.Password = (get() ?? "").Length > 0 ? mask : "";
                    touched = false;
                };
                box = password;
            }
            else
            {
                TextBox text = new TextBox { Text = shown };
                text.LostFocus += (s, e) => { set(text.Text); Apply(); };
                text.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) { set(text.Text); Apply(); } };
                box = text;
            }
            box.Background = Brushes.Transparent;
            box.Foreground = new SolidColorBrush(SettingsTheme.Text);
            box.BorderThickness = new Thickness(0);
            box.Padding = new Thickness(10, 7, 10, 7);
            box.FontSize = 13;
            if (box is TextBox) ((TextBox)box).CaretBrush = new SolidColorBrush(SettingsTheme.Text);
            if (box is PasswordBox) ((PasswordBox)box).CaretBrush = new SolidColorBrush(SettingsTheme.Text);
            Border frame = new Border { CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(SettingsTheme.SegTrack), Child = box, Margin = new Thickness(14, 0, 14, 12) };
            Row(null, label, null, null, frame);
        }

        // Primary, the one with the mouse, all, then each other monitor by the number Windows gives it.
        void MonitorRow()
        {
            System.Collections.Generic.List<string> values = new System.Collections.Generic.List<string> { "Primary", "Active", "All" };
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string> { Strings.MonitorPrimary, Strings.MonitorActive, Strings.MonitorAll };
            foreach (Display display in Displays.All())
            {
                if (display.Primary) continue;
                values.Add(display.Id);
                names.Add(Strings.MonitorNumber(display.Number));
            }
            int current = values.FindIndex(v => string.Equals(v, settings.Monitor, StringComparison.OrdinalIgnoreCase));
            Row("\uE7F4", Strings.MonitorLabel, Strings.HintMonitor, null, SegmentBar(names.ToArray(), Math.Max(0, current), i => settings.Monitor = values[i]));
            Toggle("\uE7F4", Strings.MenuActiveMonitorOnly, Strings.HintActiveMonitorOnly, () => settings.ActiveMonitorOnly, v => settings.ActiveMonitorOnly = v);
        }

        // Another program can hold the clipboard for a moment, so it is retried (waiting without freezing the window).
        static async System.Threading.Tasks.Task<bool> CopyText(string text)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    if (Clipboard.ContainsText() && Clipboard.GetText() == text) return true;
                }
                catch (Exception) { }
                await System.Threading.Tasks.Task.Delay(80);
            }
            return false;
        }

        // The local API for scripts and apps: a switch, and while it is on, the token to hand to them.
        void ApiRows()
        {
            Toggle("\uE943", Strings.MenuApi, Strings.HintApi, () => settings.ApiEnabled, v =>
            {
                settings.ApiEnabled = v;
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
            });
            if (!settings.ApiEnabled) return;
            LocalApi api = (Application.Current as App)?.Api;
            if (api == null) return;
            Group("\uE8C8", Strings.GroupApiMore, () => ApiDetails(api));
        }

        void ApiDetails(LocalApi api)
        {

            TextBlock note = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(60, 0, 14, 10) };
            Button copy = new Button { Content = Strings.ApiCopyToken, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            copy.Click += (s, e) =>
            {
                string token = api.Token;
                if (string.IsNullOrEmpty(token)) return;
                try
                {
                    // Out of the Win+V history and the cloud clipboard: the token is a secret
                    DataObject data = new DataObject();
                    data.SetText(token);
                    data.SetData("ExcludeClipboardContentFromMonitorProcessing", new System.IO.MemoryStream(new byte[] { 1, 0, 0, 0 }));
                    data.SetData("CanIncludeInClipboardHistory", new System.IO.MemoryStream(new byte[] { 0, 0, 0, 0 }));
                    data.SetData("CanUploadToCloudClipboard", new System.IO.MemoryStream(new byte[] { 0, 0, 0, 0 }));
                    Clipboard.SetDataObject(data, true);
                }
                catch (Exception) { return; }
                note.Text = Strings.ApiTokenCopied;
                note.Visibility = Visibility.Visible;
            };
            Row("\uE8C8", Strings.ApiTokenLabel, Strings.ApiTokenHint, null, null, copy);
            Button renew = new Button { Content = Strings.ApiNewToken, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            renew.Click += (s, e) =>
            {
                string fresh = api.RegenerateToken();
                // An unpacked extension keeps its own copy of the token: refresh it so it keeps working
                try { if (System.IO.Directory.Exists(ExtensionInstaller.Folder)) ExtensionInstaller.Extract(fresh, api.Port); } catch (Exception) { }
                note.Text = Strings.ApiNewTokenDone;
                note.Visibility = Visibility.Visible;
            };
            Row("\uE72C", null, null, null, null, renew);
            Button guide = new Button { Content = ExternalLabel(Strings.ApiGuide), Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            guide.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/blob/main/docs/API.md") { UseShellExecute = true }); } catch (Exception) { } };
            Row("\uE897", null, null, null, null, guide);

            StackPanel browsers = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (bool edge in new[] { false, true })
            {
                bool isEdge = edge;
                Button install = new Button { Content = isEdge ? "Edge" : "Chrome", Style = (Style)FindResource("LinkButton"), Margin = new Thickness(0, 0, 14, 0) };
                install.Click += async (s, e) =>
                {
                    try
                    {
                        string token = api.Token ?? api.RegenerateToken();
                        string folder = ExtensionInstaller.Extract(token, api.Port > 0 ? api.Port : settings.ApiPort);
                        // When the copy still fails after the retries, the folder opens in Explorer so its address
                        // can be copied from there.
                        bool copied = await CopyText(folder);
                        if (!copied)
                        {
                            try { System.Diagnostics.Process.Start("explorer.exe", (char)34 + folder + (char)34); } catch (Exception) { }
                        }
                        bool opened = ExtensionInstaller.OpenExtensionsPage(isEdge);
                        note.Text = !opened ? Strings.ExtNoBrowser + " " + folder : copied ? Strings.ExtSteps(folder) : Strings.ExtStepsNoCopy(folder);
                    }
                    catch (Exception)
                    {
                        note.Text = Strings.ExtFailed;
                    }
                    note.Visibility = Visibility.Visible;
                };
                browsers.Children.Add(install);
            }
            Row("\uE896", Strings.ExtInstall, Strings.ExtHint, browsers); // The name and what it does on the left, the two browsers on the right
            if (api.LastStartFailed)
                ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(new TextBlock { Text = Strings.ApiPortBusy, FontSize = 12, Foreground = Brushes.IndianRed, Margin = new Thickness(60, 0, 14, 10) });
            ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(note);
        }

        // Shows the current shortcut; a click waits for the next key combination and saves it.
        void HotkeyRow()
        {
            Button button = new Button { Content = settings.HotkeyCombo, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Focusable = true };
            bool capturing = false;
            button.Click += (s, e) =>
            {
                capturing = true;
                button.Content = Strings.HotkeyPress;
                button.Focus();
            };
            button.LostKeyboardFocus += (s, e) =>
            {
                if (!capturing) return;
                capturing = false;
                button.Content = settings.HotkeyCombo;
            };
            button.PreviewKeyDown += (s, e) =>
            {
                if (!capturing) return;
                e.Handled = true;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.Escape)
                {
                    capturing = false;
                    button.Content = settings.HotkeyCombo;
                    return;
                }
                string combo = HotkeyService.Format(Keyboard.Modifiers, key);
                if (combo == null) return; // Only a modifier so far, or no modifier at all
                capturing = false;
                settings.HotkeyCombo = combo;
                Apply();
                Rebuild();
            };
            Row("\uE765", null, null, null, null, button);
            if (App.HotkeyFailed)
            {
                TextBlock warning = new TextBlock { Text = Strings.HotkeyTaken, FontSize = 12, Foreground = Brushes.IndianRed, Margin = new Thickness(60, 0, 14, 10), TextWrapping = TextWrapping.Wrap };
                ((StackPanel)card.Children[card.Children.Count - 1]).Children.Add(warning);
            }
        }
    }
}
