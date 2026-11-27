using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsIsland
{
    // The first-run steps, shown instead of the settings.
    public partial class SettingsWindow
    {
        // ---- The first-run welcome: how it looks, what it shows, done ----

        void BuildWelcome()
        {
            Heading.Text = Strings.WelcomeTitle;
            string[] titles = { Strings.WelcomeLookTitle, Strings.WelcomeShowTitle, Strings.WelcomeDoneTitle };
            StackPanel steps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 10) };
            for (int i = 0; i < 3; i++)
                steps.Children.Add(new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 0, 5, 0), Background = new SolidColorBrush(i <= welcomeStep ? SettingsTheme.Accent : SettingsTheme.SwitchOff) });
            Content.Children.Add(steps);
            Content.Children.Add(new TextBlock { Text = titles[welcomeStep], FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
            Content.Children.Add(new TextBlock { Text = welcomeStep == 2 ? Strings.WelcomeDoneText : Strings.WelcomeChangeLater, Foreground = (Brush)FindResource("Secondary"), FontSize = 13, Margin = new Thickness(4, 0, 0, 14), TextWrapping = TextWrapping.Wrap });

            if (welcomeStep == 0)
            {
                // Three looks; the one picked has a blue edge
                int pick = settings.AlwaysThin ? 1 : string.Equals(settings.IdleStyle, "Pill", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
                Grid cards = new Grid();
                for (int i = 0; i < 3; i++) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                string[] looks = { Strings.LookIdleLine, Strings.LookAlwaysLine, Strings.LookPill };
                string[] hints = { Strings.LookIdleLineHint, Strings.LookAlwaysLineHint, Strings.LookPillHint };
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    Border preview = new Border { Height = 46, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(0x55, 0x80, 0x80, 0x80)), Margin = new Thickness(0, 0, 0, 8) }; // Grey, so the black shapes show on it
                    Border shape = new Border { Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(SettingsTheme.Separator), BorderThickness = new Thickness(0.5) };
                    if (i == 2) { shape.Width = 62; shape.Height = 18; shape.CornerRadius = new CornerRadius(9); }
                    else { shape.Width = 58; shape.Height = 7; shape.CornerRadius = new CornerRadius(3.5); }
                    preview.Child = shape;
                    StackPanel body = new StackPanel { Margin = new Thickness(8, 8, 8, 10) };
                    body.Children.Add(preview);
                    body.Children.Add(new TextBlock { Text = looks[i], FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
                    body.Children.Add(new TextBlock { Text = hints[i], FontSize = 11, Foreground = (Brush)FindResource("Secondary"), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                    Border card = new Border
                    {
                        Child = body,
                        Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 0),
                        CornerRadius = new CornerRadius(12),
                        Background = (Brush)FindResource("Card"),
                        BorderThickness = new Thickness(1.5),
                        BorderBrush = i == pick ? new SolidColorBrush(SettingsTheme.Accent) : new SolidColorBrush(SettingsTheme.Separator),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        settings.AlwaysThin = index == 1;
                        settings.IdleStyle = index == 2 ? "Pill" : "Line";
                        Apply();
                        Rebuild();
                    };
                    Grid.SetColumn(card, i);
                    cards.Children.Add(card);
                }
                Content.Children.Add(cards);
            }
            else if (welcomeStep == 1)
            {
                Section(Strings.SectionActivities);
                Toggle("\uE8D6", Strings.MenuMusic, null, () => settings.ShowMusic, v => settings.ShowMusic = v);
                Toggle("img:claude", Strings.MenuClaude, null, () => settings.ShowClaude, v => settings.ShowClaude = v);
                Toggle("\uE7E7", Strings.MenuNotifications, Strings.HintNotifications, () => settings.ShowNotifications, v => settings.ShowNotifications = v);
                Toggle("\uE720", Strings.MenuPrivacyDots, Strings.HintPrivacyDots, () => settings.ShowPrivacyDots, v => settings.ShowPrivacyDots = v);
                Toggle("\uE72E", Strings.MenuPermissionPrompts, Strings.HintPermissionPrompts, () => settings.PermissionPrompts, SetPermissionPrompts);
                Toggle("\uE7E8", Strings.MenuStartup, null, () => settings.StartWithWindows, v => settings.StartWithWindows = v);
            }

            // The buttons: the blue one goes on, the plain ones go back or skip
            StackPanel buttons = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            Border next = new Border
            {
                Background = new SolidColorBrush(SettingsTheme.Accent),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0, 9, 0, 9),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = welcomeStep == 2 ? Strings.WelcomeFinish : Strings.WelcomeContinue, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center },
            };
            next.MouseLeftButtonUp += (s, e) =>
            {
                if (welcomeStep < 2) { welcomeStep++; Rebuild(); }
                else FinishWelcome();
            };
            buttons.Children.Add(next);
            Border plain = new Border
            {
                Padding = new Thickness(0, 10, 0, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new TextBlock { Text = welcomeStep == 0 ? Strings.WelcomeSkip : Strings.WelcomeBack, Foreground = (Brush)FindResource("Secondary"), TextAlignment = TextAlignment.Center, FontSize = 12.5 },
            };
            plain.MouseLeftButtonUp += (s, e) =>
            {
                if (welcomeStep == 0) FinishWelcome();
                else { welcomeStep--; Rebuild(); }
            };
            buttons.Children.Add(plain);
            Content.Children.Add(buttons);
        }

        internal void WelcomeStep(int step) { welcomeStep = step; }

        void FinishWelcome()
        {
            settings.OnboardingDone = true;
            settings.Save();
            changed();
            Close();
        }
    }
}
