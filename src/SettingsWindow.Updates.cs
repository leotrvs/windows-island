using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The update row: checking, the new version, up to date.
    public partial class SettingsWindow
    {
        TextBlock updateGlyph, updateText;
        RotateTransform spin;

        void UpdateRow()
        {
            Row(null, null, null, null, null, MakeUpdateLine());
        }

        bool updateCompact; // The narrow version in Essentials: short texts, one line, so both halves of the row look the same

        FrameworkElement MakeUpdateLine(bool compact = false)
        {
            updateCompact = compact;
            spin = new RotateTransform();
            updateGlyph = new TextBlock { FontFamily = IconFont, FontSize = 16, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, RenderTransform = spin, RenderTransformOrigin = new Point(0.5, 0.5) };
            updateText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = compact ? TextWrapping.NoWrap : TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis };
            // The text wraps inside the space it has, so a long message is never cut off
            Grid line = new Grid { Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(updateText, 1);
            line.Children.Add(updateGlyph);
            line.Children.Add(updateText);
            line.MouseLeftButtonUp += (s, e) =>
            {
                if (updates == null) return;
                if (updates.State == UpdateFlow.Phase.Available) _ = updates.InstallAsync(true);
                else if (updates.State == UpdateFlow.Phase.Idle || updates.State == UpdateFlow.Phase.UpToDate || updates.State == UpdateFlow.Phase.Failed) _ = updates.CheckAsync(true);
            };
            if (updates != null)
            {
                updates.Changed -= OnUpdateChanged;
                updates.Changed += OnUpdateChanged;
            }
            RefreshUpdateRow();
            return line;
        }

        void OnUpdateChanged()
        {
            Dispatcher.BeginInvoke(new Action(RefreshUpdateRow));
        }

        void RefreshUpdateRow()
        {
            if (updateText == null) return;
            UpdateFlow.Phase phase = updates?.State ?? UpdateFlow.Phase.Idle;
            string glyph = "\uE895";
            string text = updateCompact ? Strings.UpdateIdleShort : Strings.MenuCheckUpdates;
            Brush color = (Brush)FindResource("Accent");
            bool spinning = false;
            switch (phase)
            {
                case UpdateFlow.Phase.Checking: glyph = "\uE895"; text = updateCompact ? Strings.UpdateCheckingShort : Strings.UpdateChecking; color = (Brush)FindResource("Secondary"); spinning = true; break;
                case UpdateFlow.Phase.Available: glyph = "\uE896"; text = updateCompact ? Strings.UpdateAskButton + " " + updates.Version : Strings.UpdateDownload(updates.Version); color = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)); break; // A new version: green
                case UpdateFlow.Phase.Downloading: glyph = "\uE895"; text = updateCompact ? Strings.UpdateDownloadingShort : Strings.UpdateDownloading; color = (Brush)FindResource("Secondary"); spinning = true; break;
                case UpdateFlow.Phase.UpToDate: glyph = "\uE73E"; text = updateCompact ? Strings.UpToDateShort : Strings.UpToDate; color = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)); break;
                case UpdateFlow.Phase.Failed: glyph = "\uE7BA"; text = updateCompact ? Strings.UpdateFailedShort : Strings.UpdateFailed; color = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)); break;
            }
            updateGlyph.Text = glyph;
            updateGlyph.Foreground = color;
            updateText.Text = text;
            updateText.Foreground = color;
            if (spinning)
                spin.BeginAnimation(RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
            else
                spin.BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }
}
