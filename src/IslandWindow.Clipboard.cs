using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsIsland
{
    // The clipboard page: the last copied texts.
    public partial class IslandWindow
    {
        // The last texts copied, one line each; a click puts one back on the clipboard (the user pastes it with Ctrl+V).
        // The texts come from ClipboardService and stay in memory; while the screen is shared they are masked.

        ClipboardService clipboardList;
        bool clipboardSample;                                  // README images: made-up texts, the clipboard is not touched
        List<ClipboardHistory.Item> clipboardSampleItems;
        string clipboardCopiedText;                            // The row that was just copied shows "Copied" for a moment
        DateTime clipboardCopiedUntil = DateTime.MinValue;
        DateTime clipboardRead = DateTime.MinValue;
        string clipboardDrawn;
        static readonly Brush ClipboardHover = Freeze(new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)));

        void OnClipboardChanged() { UpdateClipboard(true); Recompute(); }

        bool ClipboardMasked { get { return privacy.Screen; } } // Any screen capture or share Windows reports, whatever the dot settings

        IReadOnlyList<ClipboardHistory.Item> ClipboardItems
        {
            get
            {
                if (clipboardSample) return clipboardSampleItems;
                return clipboardList != null ? clipboardList.Items : new List<ClipboardHistory.Item>();
            }
        }

        void UpdateClipboard(bool force)
        {
            if (!force && (DateTime.Now - clipboardRead).TotalMilliseconds < 1000) return; // The "12 s ago" texts, once a second
            clipboardRead = DateTime.Now;
            IReadOnlyList<ClipboardHistory.Item> items = ClipboardItems;
            bool masked = ClipboardMasked && !clipboardSample;
            DateTime now = DateTime.Now;
            bool copiedShown = now < clipboardCopiedUntil;
            // Redrawn only when something visible changed
            System.Text.StringBuilder key = new System.Text.StringBuilder(Strings.Current + (masked ? "|m" : "|") + (copiedShown ? clipboardCopiedText?.GetHashCode().ToString() : ""));
            foreach (ClipboardHistory.Item item in items) key.Append('|').Append(item.Text.GetHashCode()).Append(':').Append(Strings.ClipboardAgo(now - item.Copied));
            string drawn = key.ToString();
            if (!force && drawn == clipboardDrawn) return;
            clipboardDrawn = drawn;

            ClipboardTitleText.Text = Strings.ClipboardTitle;
            ClipboardClearText.Text = Strings.ClipboardClear;
            ClipboardHiddenText.Text = "· " + Strings.ClipboardHidden;
            ClipboardHiddenText.Visibility = masked && items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ClipboardClearButton.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ClipboardEmptyText.Text = Strings.ClipboardEmpty;
            ClipboardEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClipboardList.Children.Clear();
            foreach (ClipboardHistory.Item item in items)
            {
                string text = item.Text;
                Border row = new Border { Height = 19, CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(-6, 0, -6, 0), Padding = new Thickness(6, 0, 6, 0) };
                Grid line = new Grid();
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.Children.Add(new TextBlock
                {
                    Text = masked ? ClipboardHistory.Masked : ClipboardHistory.OneLine(text),
                    FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                });
                bool copied = copiedShown && text == clipboardCopiedText;
                TextBlock side = new TextBlock
                {
                    Text = copied ? Strings.ClipboardCopied : Strings.ClipboardAgo(now - item.Copied),
                    FontSize = 10.5, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Foreground = copied ? Green : (Brush)FindResource("Secondary"),
                };
                Grid.SetColumn(side, 1);
                line.Children.Add(side);
                row.Child = line;
                row.MouseEnter += (s, e) => row.Background = ClipboardHover;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonDown += (s, e) => e.Handled = true;
                row.MouseLeftButtonUp += async (s, e) =>
                {
                    e.Handled = true;
                    if (clipboardSample || clipboardList == null) return;
                    if (!await clipboardList.CopyBack(text)) return;
                    clipboardCopiedText = text;
                    clipboardCopiedUntil = DateTime.Now.AddSeconds(1.5);
                    UpdateClipboard(true);
                };
                ClipboardList.Children.Add(row);
            }
        }
    }
}
