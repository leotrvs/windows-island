using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The tabs: what's new, essentials, activities, behavior, appearance and system.
    public partial class SettingsWindow
    {
        // The news of the latest release, on top of Settings until it is dismissed (the cross)
        void AddWhatsNew()
        {
            if (string.Equals(settings.WhatsNewSeen, WhatsNew.Key, StringComparison.Ordinal)) return;
            Grid head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = WhatsNew.Title, FontWeight = FontWeights.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            TextBlock close = new TextBlock
            {
                Text = "\uE711", FontFamily = IconFont, FontSize = 11, Padding = new Thickness(8, 4, 2, 4), Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent, ToolTip = Strings.WhatsNewGotIt, VerticalAlignment = VerticalAlignment.Center,
            };
            close.MouseLeftButtonUp += (s, e) =>
            {
                settings.WhatsNewSeen = WhatsNew.Key;
                Apply();
                Rebuild();
            };
            Grid.SetColumn(close, 1);
            head.Children.Add(close);

            StackPanel body = new StackPanel { Margin = new Thickness(16, 12, 12, 14) };
            body.Children.Add(head);
            foreach (string[] item in WhatsNew.Items)
            {
                body.Children.Add(new TextBlock { Text = item[0], FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });
                body.Children.Add(new TextBlock { Text = item[1], FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Secondary"), Margin = new Thickness(0, 1, 18, 0) });
            }
            Button all = new Button { Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
            all.Content = GitHubLabel(all, Strings.WhatsNewAll);
            all.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WhatsNew.ChangelogUrl(settings.Language)) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            };
            body.Children.Add(all);
            Content.Children.Add(new Border
            {
                Child = body,
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(SettingsTheme.Accent),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14),
            });
        }

        // A link that leaves the app: the text and a small arrow pointing up and to the right
        FrameworkElement ExternalLabel(string text)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = "\u2197", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 12, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        // The same, with the GitHub mark in front, in the color of its button
        FrameworkElement GitHubLabel(Button owner, string text)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            System.Windows.Shapes.Path mark = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"),
                Width = 14, Height = 14, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            mark.SetBinding(System.Windows.Shapes.Shape.FillProperty, new System.Windows.Data.Binding("Foreground") { Source = owner });
            panel.Children.Add(mark);
            panel.Children.Add(ExternalLabel(text));
            return panel;
        }

        // A few switches, for someone who just wants it to work; "More settings" opens all of them.
        void BuildEssentials()
        {
            Section(Strings.SectionEssentials);
            Toggle("\uE740", Strings.MenuAlwaysThin, Strings.HintAlwaysThin, () => settings.AlwaysThin, v => settings.AlwaysThin = v);
            Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle("\uE7E7", Strings.MenuNotifications, null, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
            Toggle("\uE720", Strings.MenuPrivacyDots, null, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
            Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);

            // Not part of the list: a row of its own, split in two (more settings | updates)
            Grid split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            StackPanel more = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 14, 0, 14) };
            more.Children.Add(new TextBlock { Text = "\uE713", FontFamily = IconFont, FontSize = 16, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            more.Children.Add(new TextBlock { Text = Strings.MoreSettings, Foreground = (Brush)FindResource("Accent"), VerticalAlignment = VerticalAlignment.Center });
            more.MouseLeftButtonUp += (s, e) =>
            {
                settings.SettingsMode = "Advanced";
                Apply();
                BuildTabs();
                Rebuild();
            };
            split.Children.Add(more);
            Border divider = new Border { Background = (Brush)FindResource("Separator"), Margin = new Thickness(0, 8, 0, 8) };
            Grid.SetColumn(divider, 1);
            split.Children.Add(divider);
            FrameworkElement update = MakeUpdateLine(true);
            update.HorizontalAlignment = HorizontalAlignment.Center;
            update.VerticalAlignment = VerticalAlignment.Center;
            update.Margin = new Thickness(16, 14, 16, 14); // Room on both sides: the text does not touch the edge
            Grid.SetColumn(update, 2);
            split.Children.Add(update);
            Content.Children.Add(new Border { Child = split, Background = (Brush)FindResource("Card"), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 12, 0, 0) });
        }

        void BuildActivities()
        {
            Section(Strings.SectionActivities);
            Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
            Toggle("\uE8EC", Strings.MenuSongName, Strings.HintSongName, () => settings.SongNameAlways, v => settings.SongNameAlways = v);
            Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
            Toggle("\uEA8F", Strings.MenuBanners, null, () => settings.ClaudeBanners, v => settings.ClaudeBanners = v);
            Toggle("\uE787", Strings.MenuDaySummary, Strings.HintDaySummary, () => settings.ShowDaySummary, v => settings.ShowDaySummary = v);
            Group("\uE9D9", Strings.GroupLimits, () =>
            {
                Toggle("\uE9D9", Strings.MenuLimitAlerts, Strings.HintLimitAlerts, () => settings.ShowLimitAlerts, v => settings.ShowLimitAlerts = v);
                Toggle("\uE823", Strings.MenuLimitForecast, Strings.HintLimitForecast, () => settings.ShowLimitForecast, v => settings.ShowLimitForecast = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(75), null, () => settings.LimitAt75, v => settings.LimitAt75 = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(90), null, () => settings.LimitAt90, v => settings.LimitAt90 = v);
                Toggle("\uE7BA", Strings.MenuLimitAt(100), null, () => settings.LimitAt100, v => settings.LimitAt100 = v);
                Toggle("\uE777", Strings.MenuLimitRenewed, null, () => settings.LimitRenewed, v => settings.LimitRenewed = v);
            });
            Toggle("\uE72E", Strings.MenuPermissionPrompts, Strings.HintPermissionPrompts, () => settings.PermissionPrompts, SetPermissionPrompts);
            Toggle("img:codex", Strings.MenuCodex, null, () => settings.ShowCodex, v => settings.ShowCodex = v);
            Toggle("\uEA8F", Strings.MenuCodexBanners, null, () => settings.CodexBanners, v => settings.CodexBanners = v);
            Group("\uE720", Strings.GroupPrivacy, () =>
            {
                Toggle("\uE720", Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
                Toggle("\uE7F4", Strings.MenuScreenShare, Strings.HintScreenShare, () => settings.ShowScreenShare, v => settings.ShowScreenShare = v);
                // Apps seen using the microphone, camera or screen: one can be turned off here, like a call app that keeps the microphone busy
                if (settings.PrivacySeen.Count > 0 || settings.PrivacyIgnored.Count > 0) HiddenAppsRow();
                int[] nameSeconds = { 0, 1, 2, 3 };
                string[] nameLabels = { Strings.Off, Strings.Duration(1, false), Strings.Duration(2, false), Strings.Duration(3, false) };
                Row("\uE77B", Strings.MenuPrivacyNames, Strings.HintPrivacyNames, null,
                    SegmentBar(nameLabels, Math.Max(0, Array.IndexOf(nameSeconds, settings.PrivacyNameSeconds)), i => settings.PrivacyNameSeconds = nameSeconds[i]));
            });
            Group("\uE8F1", Strings.GroupMore, () =>
            {
            Toggle("\uE9D9", Strings.MenuFootball, Strings.HintFootball, () => settings.ShowFootball, v => { settings.ShowFootball = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
            if (settings.ShowFootball)
            {
                InputRow(Strings.FootballTeamLabel, () => settings.FootballTeam, v => settings.FootballTeam = (v ?? "").Trim(), false);
                InputRow(Strings.FootballLeaguesLabel, () => settings.FootballLeagues, v => settings.FootballLeagues = (v ?? "").Trim(), false);
            }
            Toggle("\uE7C8", Strings.MenuObs, Strings.HintObs, () => settings.ShowObs, v => settings.ShowObs = v);
            Toggle("\uE895", Strings.MenuWindowsUpdate, Strings.HintWindowsUpdate, () => settings.ShowWindowsUpdate, v => settings.ShowWindowsUpdate = v);
            Toggle("\uE896", Strings.MenuSteam, Strings.HintSteam, () => settings.ShowSteamDownloads, v => settings.ShowSteamDownloads = v);
            });
            Group("\uE77F", Strings.GroupClipboard, () =>
            {
                // Sensitive (a copy can be a password): off by default, with the warning under the switch
                Toggle("\uE77F", Strings.MenuClipboardHistory, Strings.HintClipboardHistory, () => settings.ClipboardHistory, v => { settings.ClipboardHistory = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
                if (settings.ClipboardHistory)
                    Choice("\uE916", Strings.MenuClipboardForget, null, new[] { 60, 300, 900, 3600 }, false, () => settings.ClipboardForgetMinutes * 60, v => settings.ClipboardForgetMinutes = v / 60);
            });
            Group("\uE720", Strings.GroupDiscord, DiscordRows);
            Group("\uE83E", Strings.GroupBattery, () =>
            {
                Toggle("\uE83E", Strings.MenuBatteryActivity, Strings.HintBatteryActivity, () => settings.BatteryActivity, v => settings.BatteryActivity = v);
                Toggle("\uE83F", Strings.MenuBatteryAlerts, Strings.HintBatteryAlerts, () => settings.BatteryAlerts, v => settings.BatteryAlerts = v);
            });
            Group("\uEA8F", Strings.GroupAlerts, () =>
            {
                Toggle("\uE702", Strings.MenuDevices, Strings.HintDevices, () => settings.ShowDeviceAlerts, v => settings.ShowDeviceAlerts = v);
                Toggle("\uE701", Strings.MenuNetworkAlerts, Strings.HintNetworkAlerts, () => settings.ShowNetworkAlerts, v => settings.ShowNetworkAlerts = v);
                Toggle("\uE708", Strings.MenuFocus, Strings.HintFocus, () => settings.ShowFocus, v => settings.ShowFocus = v);
                Toggle("\uE722", Strings.MenuScreenshot, Strings.HintScreenshot, () => settings.ShowScreenshotAlert, v => settings.ShowScreenshotAlert = v);
                Toggle("\uE7E7", Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
                Toggle("\uE896", Strings.MenuDownloads, Strings.HintDownloads, () => settings.ShowDownloadAlerts, v => { settings.ShowDownloadAlerts = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
                if (settings.ShowDownloadAlerts)
                    MegabytesRow("\uE896", Strings.MenuDownloadMin, new[] { 10, 50, 100, 500 }, () => settings.DownloadMinMb, v => settings.DownloadMinMb = v);
            });
            Group("\uE767", Strings.GroupLevels, () =>
            {
                Toggle("\uE767", Strings.MenuLevels, Strings.HintLevels, () => settings.ShowLevels, v => settings.ShowLevels = v);
                Toggle("\uE74F", Strings.MenuHideOsd, Strings.HintHideOsd, () => settings.HideWindowsOsd, v => settings.HideWindowsOsd = v);
            });
        }

        // Adds or removes Claude Code's hook that sends permission requests to the island. The hook needs the local API.
        void SetPermissionPrompts(bool on)
        {
            string error;
            if (!ClaudeHook.Apply(on, out error))
            {
                settings.PermissionPrompts = false;
                MessageBox.Show(Strings.PermissionHookFailed + (error == null ? "" : "\n" + error), Installer.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                return;
            }
            settings.PermissionPrompts = on;
            if (on) settings.ApiEnabled = true;
            Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
        }

        // How the island behaves: when it opens, when it steps away, the keyboard shortcut, the times and the pages.
        void BuildBehavior()
        {
            Section(Strings.SectionBehavior);
            Toggle("\uE7C9", Strings.MenuHover, null, () => settings.ExpandOnHover, v => settings.ExpandOnHover = v);
            Toggle("\uE740", Strings.MenuFullscreen, null, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v);
            Toggle("\uE7B3", Strings.MenuEmptyIsland, Strings.HintEmptyIsland, () => settings.EmptyIsland, v => settings.EmptyIsland = v);
            Toggle("\uE7F8", Strings.MenuIdlePill, null, () => settings.ShowIdlePill, v => settings.ShowIdlePill = v);
            Toggle("\uE765", Strings.MenuHotkey, Strings.HintHotkey, () => settings.HotkeyEnabled, v => { settings.HotkeyEnabled = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
            if (settings.HotkeyEnabled) HotkeyRow();
            Group("\uE916", Strings.GroupTimes, () =>
            {
                Choice("\uE916", Strings.MenuAutoHide, Strings.HintAutoHide, new[] { 0, 5, 10, 30, 60, 300 }, true, () => settings.AutoHideSeconds, v => settings.AutoHideSeconds = v);
                Choice("\uE81C", Strings.MenuAlertDuration, null, new[] { 3, 5, 8, 12, 20 }, false, () => settings.AlertSeconds, v => settings.AlertSeconds = v);
                Choice("\uE81C", Strings.MenuNotificationDuration, null, new[] { 2, 3, 5, 8, 12 }, false, () => settings.NotificationSeconds, v => settings.NotificationSeconds = v);
                Choice("\uE81C", Strings.MenuLimitDuration, null, new[] { 3, 4, 6, 8, 12 }, false, () => settings.LimitAlertSeconds, v => settings.LimitAlertSeconds = v);
                Segmented("\uE767", Strings.MenuTimerSound, new[] { Strings.SoundNone, Strings.SoundChime, Strings.SoundBell, Strings.SoundBeep },
                    () => Math.Max(0, Array.FindIndex(IslandRules.SoundChoices, v => string.Equals(v, settings.TimerSoundChoice, StringComparison.OrdinalIgnoreCase))),
                    i => { settings.TimerSoundChoice = IslandRules.SoundChoices[i]; TimerSounds.Play(settings.TimerSoundChoice); }); // Choosing one plays it
                Toggle("\uE7F4", Strings.MenuPresentationMode, Strings.HintPresentationMode, () => settings.PresentationMode, v => settings.PresentationMode = v);
                Choice("\uE769", Strings.MenuPausedMusic, null, new[] { 0, 2, 5, 15, 30, 60 }, false, () => settings.PausedMusicSeconds, v => settings.PausedMusicSeconds = v);
            });

            Group("\uE916", Strings.GroupPomodoro, () =>
            {
                MinutesRow("\uE916", Strings.PomodoroFocusLabel, new[] { 15, 20, 25, 30, 45, 50 }, () => settings.PomodoroFocus, v => settings.PomodoroFocus = v);
                MinutesRow("\uE916", Strings.PomodoroBreakLabel, new[] { 3, 5, 10 }, () => settings.PomodoroBreak, v => settings.PomodoroBreak = v);
                MinutesRow("\uE916", Strings.PomodoroLongLabel, new[] { 10, 15, 20, 30 }, () => settings.PomodoroLong, v => settings.PomodoroLong = v);
            });

            Section(Strings.SectionPages);
            System.Collections.Generic.List<string> pageOrder = settings.PageOrderList();
            System.Collections.Generic.List<UIElement> pageRows = new System.Collections.Generic.List<UIElement>();
            StackPanel pageCard = card;
            for (int i = 0; i < pageOrder.Count; i++)
            {
                string name = pageOrder[i];
                string hint = i == 0 ? Strings.HintPages : null; // Dragging the handle changes the order the pages are shown in
                UIElement grip = PageGrip(name, i, pageRows, pageCard);
                if (name == "Music") Toggle("\uE8D6", Strings.PageMusicLabel, hint, () => settings.PageMusic, v => settings.PageMusic = v, grip);
                else if (name == "Claude") Toggle("img:claude", Strings.PageClaudeLabel, hint, () => settings.PageClaude, v => settings.PageClaude = v, grip);
                else if (name == "System") Toggle("\uE9D9", Strings.PageSystemLabel, hint, () => settings.PageSystem, v => settings.PageSystem = v, grip);
                else if (name == "Quick") Toggle("\uE713", Strings.PageQuickLabel, hint, () => settings.PageQuick, v => settings.PageQuick = v, grip);
                else if (name == "Reminders") Toggle("", Strings.PageRemindersLabel, Strings.HintReminders, () => settings.ShowReminders, v => settings.ShowReminders = v, grip);
                else if (name == "Clipboard") Toggle("\uE77F", Strings.PageClipboardLabel, Strings.HintClipboardHistory, () => settings.ClipboardHistory, v => { settings.ClipboardHistory = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }, grip); // The same switch as the quick clipboard itself
                else Toggle("\uE916", Strings.PageTimerLabel, hint, () => settings.PageTimer, v => settings.PageTimer = v, grip);
                pageRows.Add(pageCard.Children[pageCard.Children.Count - 1]);
            }
        }

        // How the island looks: the theme, where it sits, the thin line when it is collapsed, the system monitor and the sizes.
        void BuildAppearance()
        {
            Section(Strings.SectionAppearance);
            string[] themeValues = { "Auto", "Dark", "Light" };
            Segmented("\uE790", Strings.MenuTheme, new[] { Strings.ThemeAuto, Strings.ThemeDark, Strings.ThemeLight },
                () => Math.Max(0, Array.FindIndex(themeValues, v => string.Equals(v, settings.Theme, StringComparison.OrdinalIgnoreCase))),
                i => { settings.Theme = themeValues[i]; Dispatcher.BeginInvoke(new Action(ChangeTheme), DispatcherPriority.Background); });
            Segmented("\uE745", Strings.MenuPosition, new[] { Strings.MenuTop, Strings.MenuBottom },
                () => string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.Position = i == 1 ? "Bottom" : "Top");
            MonitorRow();
            string[] ringValues = { "Blue", "Green", "Purple" };
            Segmented("\uE790", Strings.RingColorLabel, new[] { Strings.ColorBlue, Strings.ColorGreen, Strings.ColorPurple },
                () => Math.Max(0, Array.FindIndex(ringValues, v => string.Equals(v, settings.RingColor, StringComparison.OrdinalIgnoreCase))),
                i => settings.RingColor = ringValues[i]);
            string[] bannerValues = { "Small", "Normal" };
            Segmented("\uE8A1", Strings.BannerSizeLabel, new[] { Strings.BannerSmall, Strings.BannerNormal },
                () => string.Equals(settings.BannerSize, "Normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                i => settings.BannerSize = bannerValues[i]);

            // The collapsed island: the thin line, when it shows and what it shows
            Group("\uE740", Strings.GroupThinLine, () =>
            {
                Toggle("\uE740", Strings.MenuAlwaysThin, Strings.HintAlwaysThin, () => settings.AlwaysThin, v => { settings.AlwaysThin = v; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); });
                if (!settings.AlwaysThin)
                {
                    string[] idleValues = { "Pill", "Line" };
                    Row("\uE7F8", Strings.MenuIdleStyle, null, null,
                        SegmentBar(new[] { Strings.IdlePill, Strings.IdleLine },
                            string.Equals(settings.IdleStyle, "Line", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                            i => settings.IdleStyle = idleValues[i]));
                    string[] maxValues = { "Thin", "Normal", "Hidden" };
                    Row("\uE740", Strings.MenuMaximized, Strings.HintMaximized, null,
                        SegmentBar(new[] { Strings.MaxThin, Strings.MaxNormal, Strings.MaxHidden },
                            Math.Max(0, Array.FindIndex(maxValues, v => string.Equals(v, settings.MaximizedMode, StringComparison.OrdinalIgnoreCase))),
                            i => { settings.MaximizedMode = maxValues[i]; Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background); }));
                    if (string.Equals(settings.MaximizedMode, "Thin", StringComparison.OrdinalIgnoreCase))
                        Toggle("\uE740", Strings.MenuMaxActivities, Strings.HintMaxActivities, () => settings.MaximizedActivitiesThin, v => settings.MaximizedActivitiesThin = v);
                }
                Toggle("\uE8D6", Strings.MenuMinimalPosition, null, () => settings.MinimalMusicPosition, v => settings.MinimalMusicPosition = v);
            }, true);

            // Processor and memory
            Group("\uE9D9", Strings.GroupMonitor, () =>
            {
                Toggle("\uE9D9", Strings.MenuSystemMonitor, Strings.HintSystemMonitor, () => settings.SystemMonitor, v =>
                {
                    settings.SystemMonitor = v;
                    Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
                });
                if (settings.SystemMonitor)
                {
                    Toggle("\uE7C9", Strings.MenuMonitorKeeps, Strings.HintMonitorKeeps, () => settings.MonitorKeepsPlace, v => settings.MonitorKeepsPlace = v);
                    string[] monitorValues = { "Rings", "Bars", "Numbers" };
                    Segmented("\uE790", Strings.MonitorStyleLabel, new[] { Strings.StyleRings, Strings.StyleBars, Strings.StyleNumbers },
                        () => Math.Max(0, Array.FindIndex(monitorValues, v => string.Equals(v, settings.MonitorStyle, StringComparison.OrdinalIgnoreCase))),
                        i => settings.MonitorStyle = monitorValues[i]);
                }
            });

            Group("\uE740", Strings.GroupSizePos, () =>
            {
                SliderRow("\uE740", Strings.CollapsedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.CollapsedScale, v => settings.CollapsedScale = v, false);
                SliderRow("\uE740", Strings.ExpandedSize, Settings.MinScale, Settings.MaxScale, 0.05, Percent, () => settings.ExpandedScale, v => settings.ExpandedScale = v, true);
                SliderRow("\uE745", Strings.DistanceFromEdge, Settings.MinOffsetY, Settings.MaxOffsetY, 1, Pixels, () => settings.OffsetY, v => settings.OffsetY = v, false);
                SliderRow("\uE7C9", Strings.HorizontalPosition, -Settings.MaxOffsetX, Settings.MaxOffsetX, 10, Horizontal, () => settings.OffsetX, v => settings.OffsetX = v, false);
                ActionRow("\uE777", Strings.ResetAppearance, () =>
                {
                    settings.ResetAppearance();
                    Apply();
                    Rebuild();
                });
            });
        }

        void BuildSystem()
        {
            Section(Strings.SectionSystem);
            Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);
            Toggle("\uE895", Strings.MenuAutoUpdate, null, () => settings.AutoUpdate, v => settings.AutoUpdate = v);
            ApiRows();
            UpdateRow();
            LanguageRow();
            // Help: one button copies a short report (nothing is sent) and opens the page to report the problem
            Button report = new Button { Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            report.Content = GitHubLabel(report, Strings.DiagReport);
            report.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(Diagnostics.Report(settings));
                    report.Content = new TextBlock { Text = Strings.DiagCopied };
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/leotrvs/windows-island/issues/new") { UseShellExecute = true })?.Dispose();
                }
                catch (Exception)
                {
                    report.Content = GitHubLabel(report, Strings.DiagReport);
                }
                DispatcherTimer back = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                back.Tick += (a2, b2) => { back.Stop(); report.Content = GitHubLabel(report, Strings.DiagReport); };
                back.Start();
            };
            Row("\uE897", null, null, null, null, report);
            ActionRow("\uE81C", Strings.MenuChangelog, () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WhatsNew.ChangelogUrl(settings.Language)) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            }, false, true);
            ActionRow("\uE8A5", Strings.MenuOpenSettings, () =>
            {
                settings.Save();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.FilePath) { UseShellExecute = true })?.Dispose(); }
                catch (Exception) { }
            });
            // The last row, in red, since it removes the app: the same uninstaller Windows' apps list runs; it asks first
            if (Installer.IsInstalledCopy)
                ActionRow("\uE74D", Strings.MenuUninstall, () =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Installer.InstalledExe, "--uninstall") { UseShellExecute = true })?.Dispose(); }
                    catch (Exception) { }
                }, true);


        }
    }
}
