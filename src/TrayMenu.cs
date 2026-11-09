using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace WindowsIsland
{
    // The tray icon's right-click menu, drawn like the Settings window (dark, rounded, with a soft entrance
    // and exit) instead of the plain Windows menu. Closes on Esc or a click anywhere else.
    sealed class TrayMenu : Window
    {
        public sealed class Item
        {
            public string Text;
            public string Icon;   // A Segoe icon glyph
            public bool Danger;   // Drawn in red
            public bool Checked;
            public bool Separator;
            public Action Click;
            public List<Item> Sub;
        }

        const double MenuWidth = 232, Shadow = 16;
        static Brush CardBrush, LineBrush, HoverBrush, TextBrush, DimBrush;
        static readonly Brush DangerBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));

        // Black with white text in the dark theme, white with black text in the light one
        static void ApplyTheme(string setting)
        {
            SettingsTheme.Choose(setting);
            bool light = SettingsTheme.Light;
            CardBrush = Frozen(new SolidColorBrush(light ? Colors.White : Colors.Black));
            LineBrush = Frozen(new SolidColorBrush(light ? Color.FromRgb(0xD1, 0xD1, 0xD6) : Color.FromRgb(0x38, 0x38, 0x3A)));
            HoverBrush = Frozen(new SolidColorBrush(light ? Color.FromRgb(0xEE, 0xEE, 0xF2) : Color.FromRgb(0x2C, 0x2C, 0x2E)));
            TextBrush = Frozen(new SolidColorBrush(light ? Colors.Black : Colors.White));
            DimBrush = Frozen(new SolidColorBrush(light ? Color.FromRgb(0x6C, 0x6C, 0x70) : Color.FromRgb(0x8E, 0x8E, 0x93)));
        }
        static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        static TrayMenu current;

        readonly Border card;
        readonly TranslateTransform slide = new TranslateTransform();
        readonly bool above;
        readonly double anchorY;
        readonly Rect work;
        readonly DateTime shownAt = DateTime.Now;
        bool closing;

        static Brush Frozen(SolidColorBrush brush) { brush.Freeze(); return brush; }

        // Opens the menu with its corner at the cursor (in physical pixels). Opening it again closes it.
        public static void Open(List<Item> items, Point cursorPx, string theme)
        {
            if (current != null) { current.CloseAnimated(); return; }
            ApplyTheme(theme);
            Display display = Displays.UnderCursor();
            Point cursor = new Point(cursorPx.X / display.Scale, cursorPx.Y / display.Scale);
            current = new TrayMenu(items, cursor, display.WorkArea);
            current.Show();
        }

        TrayMenu(List<Item> items, Point cursor, Rect workArea)
        {
            work = workArea;
            above = cursor.Y > workArea.Top + workArea.Height / 2; // A taskbar at the bottom: open upwards
            anchorY = cursor.Y;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = true;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = MenuWidth + Shadow * 2;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            FontSize = 13;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            Left = Math.Clamp(cursor.X - MenuWidth + Shadow - 6, workArea.Left - Shadow + 4, workArea.Right - MenuWidth - Shadow - 4);
            Top = above ? workArea.Bottom - 400 : cursor.Y;

            StackPanel list = new StackPanel();
            foreach (Item item in items) AddItem(list, item, 0);
            card = new Border
            {
                Background = CardBrush,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(5),
                Margin = new Thickness(Shadow),
                Child = list,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, above ? 1 : 0),
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.5, Color = Colors.Black },
            };
            TransformGroup group = new TransformGroup();
            group.Children.Add(slide);
            card.RenderTransform = group;
            Content = card;

            SizeChanged += (s, e) => Reposition();
            ContentRendered += (s, e) =>
            {
                Reposition();
                IntPtr handle = new WindowInteropHelper(this).Handle;
                NativeMethods.SetForegroundWindow(handle);
                Activate();
                if (!SystemParameters.ClientAreaAnimation) { card.Opacity = 1; return; }
                slide.Y = above ? 10 : -10;
                CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(slide.Y, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            };
            Deactivated += (s, e) =>
            {
                if ((DateTime.Now - shownAt).TotalMilliseconds < 250) { Activate(); return; }
                CloseAnimated();
            };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) CloseAnimated(); };
            Closed += (s, e) => { if (current == this) current = null; };
        }

        // The menu as a PNG for the README (sample settings, nothing is shown on screen)
        public static void SaveImage(List<Item> items, string theme, string file)
        {
            ApplyTheme(theme);
            TrayMenu menu = new TrayMenu(items, new Point(600, 600), new Rect(0, 0, 1920, 1040));
            menu.card.Opacity = 1;
            menu.card.RenderTransform = Transform.Identity;
            Size size = new Size(MenuWidth + Shadow * 2, double.PositiveInfinity);
            menu.card.Measure(size);
            Size wanted = menu.card.DesiredSize;
            menu.card.Arrange(new Rect(wanted));
            menu.card.UpdateLayout();

            const double scale = 2, pad = 20;
            double w = wanted.Width - Shadow * 2 + pad * 2, h = wanted.Height - Shadow * 2 + pad * 2;
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                // The same soft wallpaper-like card as the other images, so it reads on both GitHub themes
                LinearGradientBrush wallpaper = new LinearGradientBrush(Color.FromRgb(0xEE, 0xEC, 0xF6), Color.FromRgb(0xD5, 0xD9, 0xEA), 90);
                context.DrawRoundedRectangle(wallpaper, null, new Rect(0, 0, w, h), 18, 18);
                context.PushTransform(new TranslateTransform(pad - Shadow, pad - Shadow));
                context.DrawRectangle(new VisualBrush(menu.card) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, wanted.Width, wanted.Height), ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, wanted.Width, wanted.Height) }, null, new Rect(0, 0, wanted.Width, wanted.Height));
                context.Pop();
            }
            RenderTargetBitmap output = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            output.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));
            using (System.IO.FileStream stream = System.IO.File.Create(file)) encoder.Save(stream);
        }

        // Keeps the menu's edge at the cursor however tall it grows (a submenu opening), inside the screen.
        void Reposition()
        {
            double height = ActualHeight;
            if (height <= 0) return;
            double top = above ? anchorY - height + Shadow - 4 : anchorY - Shadow + 4;
            Top = Math.Clamp(top, work.Top - Shadow + 4, Math.Max(work.Top, work.Bottom - height + Shadow - 4));
        }

        void AddItem(Panel parent, Item item, int depth)
        {
            if (item.Separator)
            {
                parent.Children.Add(new Border { Height = 1, Background = LineBrush, Margin = new Thickness(8, 4, 8, 4) });
                return;
            }

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Brush ink = item.Danger ? DangerBrush : TextBrush;
            if (item.Icon != null)
                row.Children.Add(new TextBlock { Text = item.Icon, FontFamily = IconFont, FontSize = 14, Foreground = item.Danger ? DangerBrush : DimBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBlock label = new TextBlock { Text = item.Text, Foreground = ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            if (item.Checked)
            {
                TextBlock tick = new TextBlock { Text = "\uE73E", FontFamily = IconFont, FontSize = 12, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tick, 2);
                row.Children.Add(tick);
            }

            TextBlock arrow = null;
            if (item.Sub != null)
            {
                arrow = new TextBlock
                {
                    Text = "", FontFamily = IconFont, FontSize = 11, Foreground = DimBrush, VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(0),
                };
                Grid.SetColumn(arrow, 2);
                row.Children.Add(arrow);
            }

            Border box = new Border
            {
                Child = row,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8 + depth * 14, 7, 10, 7),
                Cursor = Cursors.Arrow,
            };
            box.MouseEnter += (s, e) => box.Background = HoverBrush;
            box.MouseLeave += (s, e) => box.Background = Brushes.Transparent;
            parent.Children.Add(box);

            if (item.Sub != null)
            {
                // The timer choices fold open under the row
                StackPanel sub = new StackPanel { Visibility = Visibility.Collapsed };
                foreach (Item child in item.Sub) AddItem(sub, child, depth + 1);
                parent.Children.Add(sub);
                box.MouseLeftButtonUp += (s, e) =>
                {
                    bool open = sub.Visibility != Visibility.Visible;
                    sub.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                    ((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(open ? 180 : 0, TimeSpan.FromMilliseconds(160)));
                };
            }
            else
            {
                box.MouseLeftButtonUp += (s, e) =>
                {
                    CloseAnimated();
                    item.Click?.Invoke();
                };
            }
        }

        void CloseAnimated()
        {
            if (closing) return;
            closing = true;
            if (!SystemParameters.ClientAreaAnimation) { Close(); return; }
            DoubleAnimation fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
            fade.Completed += (s, e) => { try { Close(); } catch (Exception) { } };
            card.BeginAnimation(OpacityProperty, fade);
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(above ? 6 : -6, TimeSpan.FromMilliseconds(110)));
        }

        static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        }
    }
}
