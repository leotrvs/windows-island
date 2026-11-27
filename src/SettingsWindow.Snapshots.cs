using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindowsIsland
{
    // The settings drawn to a file: the README images, and the whole window for checking the layout.
    public partial class SettingsWindow
    {
        // The whole window as it is, for checking the layout (not part of the README images)
        internal void SaveWindowShot(string file, string mode, int tabIndex)
        {
            settings.SettingsMode = mode;
            tab = tabIndex;
            Show();
            BuildTabs();
            Rebuild();
            UpdateLayout();
            FitHeight(false);
            UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ActualWidth * 2), (int)Math.Ceiling(ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render((Visual)VisualTreeHelper.GetChild(this, 0));
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
            Close();
        }

        // Draws the settings to a PNG for the README: each tab as a column, side by side (a single column for the welcome).
        public void SaveSnapshot(string file)
        {
            const double scale = 2, colWidth = 440, gap = 16, margin = 22, partGap = 14;
            string mode = settings.SettingsMode;
            settings.SettingsMode = "Advanced";
            try
            {
                Action[] builds = welcome ? new Action[] { () => BuildWelcome() } : new Action[] { BuildActivities, BuildBehavior, BuildAppearance, BuildSystem };
                System.Collections.Generic.List<System.Windows.Media.Imaging.RenderTargetBitmap> parts = new System.Collections.Generic.List<System.Windows.Media.Imaging.RenderTargetBitmap>();
                System.Collections.Generic.List<double> heights = new System.Collections.Generic.List<double>();
                foreach (Action build in builds)
                {
                    Content.Children.Clear();
                    showTitles = !welcome;
                    Content.Margin = new Thickness(0);
                    Content.Width = colWidth; // Each column is exactly this wide, whatever the window is
                    build();
                    Content.Measure(new Size(colWidth, double.PositiveInfinity));
                    Size size = new Size(colWidth, Content.DesiredSize.Height);
                    Content.Arrange(new Rect(size));
                    Content.UpdateLayout();
                    DrawingVisual part = new DrawingVisual();
                    using (DrawingContext context = part.RenderOpen())
                        context.DrawRectangle(new VisualBrush(Content) { Stretch = Stretch.Fill }, null, new Rect(0, 0, Content.ActualWidth, Content.ActualHeight));
                    System.Windows.Media.Imaging.RenderTargetBitmap piece = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    piece.Render(part);
                    parts.Add(piece);
                    heights.Add(size.Height);
                }

                // Each piece goes to the shortest column, in order
                int columns = welcome ? 1 : 3;
                double[] used = new double[columns];
                double[] top = new double[parts.Count];
                int[] where = new int[parts.Count];
                double title = welcome ? 0 : 46;
                for (int i = 0; i < parts.Count; i++)
                {
                    int c = 0;
                    for (int k = 1; k < columns; k++) if (used[k] < used[c]) c = k;
                    where[i] = c;
                    top[i] = used[c];
                    used[c] += heights[i] + partGap;
                }
                double totalWidth = margin * 2 + columns * colWidth + (columns - 1) * gap;
                double totalHeight = margin * 2 + title + (used.Max() - partGap);
                DrawingVisual page = new DrawingVisual();
                using (DrawingContext context = page.RenderOpen())
                {
                    context.DrawRoundedRectangle(new SolidColorBrush(SettingsTheme.WindowBack), null, new Rect(0, 0, totalWidth, totalHeight), 16, 16);
                    if (!welcome)
                    {
                        FormattedText heading = new FormattedText(Strings.SettingsTitle, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 30, new SolidColorBrush(SettingsTheme.Text), 1.0);
                        context.DrawText(heading, new Point(margin + 4, margin - 4));
                    }
                    for (int i = 0; i < parts.Count; i++)
                        context.DrawImage(parts[i], new Rect(margin + where[i] * (colWidth + gap), margin + title + top[i], colWidth, heights[i]));
                }
                System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    (int)(totalWidth * scale), (int)(totalHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(page);
                System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
            }
            finally
            {
                settings.SettingsMode = mode;
                Content.Width = double.NaN;
            }
        }
    }
}
