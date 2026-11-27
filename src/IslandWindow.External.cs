using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindowsIsland
{
    // Activities that other apps send through the local API.
    public partial class IslandWindow
    {
        // ---- Live activities pushed by scripts and apps through the local API ----
        LocalApi api;
        void OnApiActivity(ApiActivity activity) { UpdateExternal(); UpdatePrivacy(); Recompute(); }
        void OnApiEnded(string id) { UpdateExternal(); UpdatePrivacy(); Recompute(); }

        // A screen recording (OBS or the Game Bar) is also a red dot, like the microphone's, so it stays in sight
        // when music and Claude take the island's two places
        bool RecordingOn { get { return (renderThin && minimalKind == Thin.Recording) || (api != null && api.Activities.Any(a => a.State == "running" && IsRecording(a))); } }
        bool ApiActive { get { return api != null && api.Activities.Count > 0; } }

        static Brush StateBrush(string state) { return state == "done" ? Green : state == "error" ? Red : SystemBlue; }
        static bool IsRecording(ApiActivity a) { return a.Id.StartsWith("obs-", StringComparison.Ordinal) || a.Id.StartsWith("gamebar-", StringComparison.Ordinal); }
        static Brush ActivityBrush(ApiActivity a) { return a.State == "running" && IsRecording(a) ? Red : StateBrush(a.State); } // A recording is red, not blue
        static string SideOf(ApiActivity a) { return a.State == "done" ? "\u2713" : a.State == "error" ? "\u2715" : a.Progress >= 0 ? a.Progress + "%" : "\u2026"; }

        void UpdateExternal()
        {
            if (api == null) return;
            List<ApiActivity> list = api.Activities.OrderByDescending(a => a.Updated).ToList();
            if (list.Count == 0) return;
            BuildExternal(list);
        }

        void BuildExternal(List<ApiActivity> list)
        {
            ApiActivity first = list[0];
            ExternalGlyph.Text = first.IconGlyph;
            ExternalGlyph.Foreground = ActivityBrush(first);
            ExternalTitle.Text = first.Title;
            ExternalSide.Text = SideOf(first);
            ExternalSide.Foreground = first.State == "running" ? White : StateBrush(first.State);
            BubbleExternal.Text = first.IconGlyph;
            BubbleExternal.Foreground = ActivityBrush(first);

            // The progress bar under the main activity
            bool running = first.State == "running" && first.Progress >= 0;
            ExternalBarTrack.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            ExternalBar.Width = 158.0 * Math.Clamp(first.Progress, 0, 100) / 100;
            ExternalBar.Background = StateBrush(first.State);

            // As the second activity: a small icon inside a ring that fills; when it is done, a green disc with a check that pops in
            bool done = first.State == "done";
            BubbleRingTrack.Visibility = BubbleRing.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            BubbleRing.Data = Arc(first.Progress, 28, 2.5);
            BubbleExternal.FontSize = running ? 11 : 16;
            BubbleExternal.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
            if (done && BubbleDone.Visibility != Visibility.Visible)
            {
                BubbleDone.Visibility = Visibility.Visible;
                DoubleAnimation pop = new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 } };
                BubbleDoneScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                BubbleDoneScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            }
            else if (!done) BubbleDone.Visibility = Visibility.Collapsed;

            // The page: up to three rows, each with who sent it
            ExternalList.Children.Clear();
            foreach (ApiActivity a in list.Take(3))
            {
                Grid row = new Grid { Height = 38 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = a.IconGlyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = ActivityBrush(a), VerticalAlignment = VerticalAlignment.Center });
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = a.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                string caption = string.IsNullOrEmpty(a.Text) ? a.App : a.App + " \u00B7 " + a.Text;
                text.Children.Add(new TextBlock { Text = caption, FontSize = 11, Foreground = (Brush)FindResource("Secondary"), TextTrimming = TextTrimming.CharacterEllipsis });
                if (a.Progress >= 0)
                {
                    Grid track = new Grid { Height = 3, Margin = new Thickness(0, 3, 0, 0) };
                    track.Children.Add(new Border { CornerRadius = new CornerRadius(1.5), Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) });
                    Border fill = new Border { CornerRadius = new CornerRadius(1.5), Background = StateBrush(a.State), HorizontalAlignment = HorizontalAlignment.Left, Width = 292.0 * a.Progress / 100 };
                    track.Children.Add(fill);
                    text.Children.Add(track);
                }
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                TextBlock side = new TextBlock { Text = SideOf(a), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = a.State == "running" ? White : StateBrush(a.State) };
                Grid.SetColumn(side, 2);
                row.Children.Add(side);
                ExternalList.Children.Add(row);
            }
            if (list.Count > 3)
                ExternalList.Children.Add(new TextBlock { Text = "+" + (list.Count - 3), FontSize = 11, Foreground = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Right });
        }

        // An alert from a script or app: a banner that names who sent it.
        internal void ShowApiAlert(ApiAlert alert)
        {
            if (fullscreen && settings.HideInFullscreen) return;
            Enqueue(new BannerItem { App = alert.App, Title = alert.Title, Subtitle = alert.Text, Glyph = alert.IconGlyph, Seconds = alert.Seconds });
        }
    }
}
