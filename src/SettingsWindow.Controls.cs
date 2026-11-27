using System;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The building blocks every page is made of: sections, groups, rows, switches, choices, sliders and buttons.
    public partial class SettingsWindow
    {
        // Choices in a rounded track, with a pill that slides to the one picked.
        FrameworkElement SlidingBar(string[] names, int current, Action<int> pick, Color track, HorizontalAlignment align, Thickness margin)
        {
            Grid host = new Grid();
            Border pill = new Border
            {
                Background = new SolidColorBrush(SettingsTheme.Pill),
                CornerRadius = new CornerRadius(7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 1.5, 0, 1.5),
                Width = 0,
            };
            TranslateTransform move = new TranslateTransform();
            pill.RenderTransform = move;
            StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
            int selected = Math.Clamp(current, 0, names.Length - 1);

            Action<bool> place = animate =>
            {
                Border chip = (Border)items.Children[selected];
                if (chip.ActualWidth <= 0) return;
                double x = chip.TranslatePoint(new Point(0, 0), host).X, w = chip.ActualWidth;
                if (!animate)
                {
                    move.BeginAnimation(TranslateTransform.XProperty, null);
                    pill.BeginAnimation(FrameworkElement.WidthProperty, null);
                    move.X = x;
                    pill.Width = w;
                    return;
                }
                System.Windows.Media.Animation.CubicEase ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                move.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(x, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
                pill.BeginAnimation(FrameworkElement.WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(w, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            };

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                Border chip = new Border
                {
                    Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(1.5),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock { Text = names[i], FontSize = 12, Foreground = new SolidColorBrush(SettingsTheme.Text) },
                };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    if (index == selected) return;
                    selected = index;
                    place(true);
                    pick(index);
                };
                items.Children.Add(chip);
            }
            host.Children.Add(pill);
            host.Children.Add(items);
            host.Loaded += (s, e) => { host.UpdateLayout(); place(false); };
            host.SizeChanged += (s, e) => place(false);

            return new Border
            {
                Background = new SolidColorBrush(track),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(1),
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Child = host,
            };
        }

        Border Chip(string text, bool solid, Action click)
        {
            Border chip = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(solid ? Color.FromArgb(0x40, 0x0A, 0x84, 0xFF) : Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
                Child = new TextBlock { Text = text, FontSize = 12.5, Foreground = solid ? (Brush)IconBrush : (Brush)FindResource("Secondary") },
            };
            chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
            return chip;
        }

        void MegabytesRow(string icon, string label, int[] values, Func<int> get, Action<int> set)
        {
            string[] names = values.Select(v => v + " MB").ToArray();
            Row(icon, label, null, null, SegmentBar(names, Math.Max(0, Array.IndexOf(values, get())), i => set(values[i])));
        }

        void MinutesRow(string icon, string label, int[] values, Func<int> get, Action<int> set)
        {
            string[] names = values.Select(v => v + " min").ToArray();
            Row(icon, label, null, null, SegmentBar(names, Math.Max(0, Array.IndexOf(values, get())), i => set(values[i])));
        }

        // ---------- Building blocks ----------

        void Section(string title)
        {
            if (showTitles)
                Content.Children.Add(new TextBlock
                {
                    Text = title.ToUpper(CultureInfo.CurrentCulture),
                    Foreground = (Brush)FindResource("Secondary"),
                    FontSize = 12,
                    Margin = new Thickness(16, 18, 0, 6),
                });
            card = new StackPanel();
            Border border = new Border
            {
                Background = (Brush)FindResource("Card"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, !showTitles && Content.Children.Count > 0 ? 14 : 0, 0, 0), // Cards stacked without a title keep a gap
                Child = card,
            };
            Content.Children.Add(border);
        }

        static readonly System.Collections.Generic.HashSet<string> openGroups = new System.Collections.Generic.HashSet<string>(); // Remembered while the app runs

        // A row with an arrow that folds away the rows inside it, so a tab shows the main choices first.
        static readonly System.Collections.Generic.HashSet<string> openedOnce = new System.Collections.Generic.HashSet<string>();

        void Group(string icon, string title, Action build, bool openByDefault = false)
        {
            if (openByDefault && openedOnce.Add(title)) openGroups.Add(title);
            bool open = openGroups.Contains(title) || filter.Length > 0; // Searching opens every fold; the rows that do not match are hidden
            TextBlock arrow = new TextBlock
            {
                Text = "\uE70D",
                FontFamily = IconFont,
                FontSize = 12,
                Foreground = (Brush)FindResource("Secondary"),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(open ? 180 : 0),
            };
            Row(icon, title, null, arrow);
            StackPanel header = (StackPanel)card.Children[card.Children.Count - 1];
            header.Background = Brushes.Transparent;
            header.Cursor = System.Windows.Input.Cursors.Hand;

            // What is inside is set apart: darker, indented and with a blue edge, so it never reads as more of the same list
            StackPanel body = new StackPanel();
            Border inset = new Border
            {
                Child = body,
                Visibility = open ? Visibility.Visible : Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)),
                BorderThickness = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(0, 9, 9, 0),
                Margin = new Thickness(18, 0, 10, 10),
            };
            card.Children.Add(inset);
            header.MouseLeftButtonUp += (s, e) =>
            {
                bool now = inset.Visibility != Visibility.Visible;
                inset.Visibility = now ? Visibility.Visible : Visibility.Collapsed;
                if (now) openGroups.Add(title); else openGroups.Remove(title);
                ((RotateTransform)arrow.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(now ? 180 : 0, TimeSpan.FromMilliseconds(180)));
                FitSoon();
            };

            StackPanel outer = card;
            card = body;
            try { build(); }
            finally { card = outer; }
        }

        // One row: label (and hint) on the left, a control on the right, a hairline above all but the first.
        void Row(string icon, string label, string hint, UIElement control, UIElement below = null, UIElement left = null)
        {

            Grid grid = new Grid { Margin = new Thickness(14, 10, 14, 10), MinHeight = 30 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (icon != null && icon.StartsWith("img:"))
            {
                // The mark is a white shape: its outline cuts a rectangle of the icon color, so it fits a light window too
                ImageBrush shape = new ImageBrush(new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/" + icon.Substring(4) + "-mark.png")));
                RenderOptions.SetBitmapScalingMode(shape, BitmapScalingMode.HighQuality);
                System.Windows.Shapes.Rectangle mark = new System.Windows.Shapes.Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = IconBrush,
                    OpacityMask = shape,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                grid.Children.Add(mark);
            }
            else if (icon != null)
            {
                grid.Children.Add(new TextBlock
                {
                    Text = icon,
                    FontFamily = IconFont,
                    FontSize = 16,
                    Foreground = IconBrush,
                    Width = 30,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            if (left != null)
            {
                Grid.SetColumn(left, icon != null ? 1 : 0);
                Grid.SetColumnSpan(left, icon != null ? 1 : 2);
                grid.Children.Add(left);
            }
            else
            {
                StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(text, 1);
                text.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
                if (hint != null)
                    text.Children.Add(new TextBlock { Text = hint, FontSize = 12, Foreground = (Brush)FindResource("Secondary"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                grid.Children.Add(text);
            }

            if (control != null)
            {
                Grid.SetColumn(control, 2);
                grid.Children.Add(control);
            }

            StackPanel row = new StackPanel { Tag = (label ?? "") + "\n" + (hint ?? "") };
            if (card.Children.Count > 0)
                row.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Separator"), Margin = new Thickness(16, 0, 0, 0), SnapsToDevicePixels = true });
            row.Children.Add(grid);
            if (below != null) row.Children.Add(below);
            card.Children.Add(row);
        }

        void Toggle(string icon, string label, string hint, Func<bool> get, Action<bool> set, UIElement before = null)
        {
            CheckBox toggle = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = get(), VerticalAlignment = VerticalAlignment.Center };
            toggle.ApplyTemplate();
            Border track = (Border)toggle.Template.FindName("Track", toggle);
            // Template parts are frozen, so the knob gets a transform of its own that can move.
            TranslateTransform knob = new TranslateTransform();
            ((FrameworkElement)toggle.Template.FindName("Knob", toggle)).RenderTransform = knob;
            SolidColorBrush color = new SolidColorBrush(get() ? SwitchOn : SwitchOff);
            track.Background = color;
            knob.X = get() ? KnobTravel : 0;

            toggle.Checked += (s, e) => { AnimateSwitch(color, knob, true); set(true); Apply(); };
            toggle.Unchecked += (s, e) => { AnimateSwitch(color, knob, false); set(false); Apply(); };
            if (before == null) { Row(icon, label, hint, toggle); return; }
            StackPanel both = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            both.Children.Add(before);
            both.Children.Add(toggle);
            Row(icon, label, hint, both);
        }

        // A grip (two columns of dots) that drags a page row up or down; the other rows slide out of the way and the order is saved on release.
        UIElement PageGrip(string name, int index, System.Collections.Generic.List<UIElement> rows, StackPanel host)
        {
            Grid grip = new Grid
            {
                Width = 28, Height = 32, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNS,
            };
            System.Windows.Controls.Primitives.UniformGrid dots = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Rows = 3, Width = 10, Height = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            for (int i = 0; i < 6; i++)
                dots.Children.Add(new System.Windows.Shapes.Ellipse { Width = 3, Height = 3, Fill = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            grip.Children.Add(dots);

            double startY = 0, dragged = 0;
            double[] tops = null, heights = null;
            int target = index;

            grip.MouseLeftButtonDown += (s, e) =>
            {
                tops = new double[rows.Count];
                heights = new double[rows.Count];
                for (int i = 0; i < rows.Count; i++)
                {
                    FrameworkElement r = (FrameworkElement)rows[i];
                    tops[i] = r.TranslatePoint(new Point(0, 0), host).Y;
                    heights[i] = r.ActualHeight;
                    r.RenderTransform = new TranslateTransform();
                }
                startY = e.GetPosition(host).Y;
                target = index;
                Panel.SetZIndex(rows[index], 10);
                rows[index].Opacity = 0.85;
                grip.CaptureMouse();
                e.Handled = true;
            };
            grip.MouseMove += (s, e) =>
            {
                if (!grip.IsMouseCaptured || tops == null) return;
                double minDy = -tops[index] + tops[0], maxDy = tops[rows.Count - 1] + heights[rows.Count - 1] - tops[index] - heights[index];
                dragged = Math.Max(minDy, Math.Min(maxDy, e.GetPosition(host).Y - startY));
                ((TranslateTransform)rows[index].RenderTransform).Y = dragged;
                double center = tops[index] + heights[index] / 2 + dragged;
                int t = 0;
                for (int j = 0; j < rows.Count; j++)
                    if (j != index && tops[j] + heights[j] / 2 < center) t++;
                target = t;
                for (int j = 0; j < rows.Count; j++)
                {
                    if (j == index) continue;
                    double shift = 0;
                    if (j < index && j >= target) shift = heights[index];
                    else if (j > index && j <= target) shift = -heights[index];
                    ((TranslateTransform)rows[j].RenderTransform).Y = shift;
                }
            };
            grip.MouseLeftButtonUp += (s, e) =>
            {
                if (!grip.IsMouseCaptured) return;
                grip.ReleaseMouseCapture();
                tops = null;
                if (target == index)
                {
                    foreach (UIElement r in rows) r.RenderTransform = Transform.Identity;
                    rows[index].Opacity = 1;
                    Panel.SetZIndex(rows[index], 0);
                    return;
                }
                System.Collections.Generic.List<string> order = settings.PageOrderList();
                order.RemoveAt(index);
                order.Insert(target, name);
                settings.PageOrder = string.Join(",", order);
                Apply();
                Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
            };
            grip.LostMouseCapture += (s, e) =>
            {
                if (tops == null) return;
                tops = null;
                foreach (UIElement r in rows) r.RenderTransform = Transform.Identity;
                rows[index].Opacity = 1;
                Panel.SetZIndex(rows[index], 0);
            };
            return grip;
        }

        static Color SwitchOn { get { return SettingsTheme.On; } }
        static Color SwitchOff { get { return SettingsTheme.SwitchOff; } }
        const double KnobTravel = 15;

        static void AnimateSwitch(SolidColorBrush color, TranslateTransform knob, bool on)
        {
            color.BeginAnimation(SolidColorBrush.ColorProperty,
                new System.Windows.Media.Animation.ColorAnimation(on ? SwitchOn : SwitchOff, TimeSpan.FromMilliseconds(180)));
            knob.BeginAnimation(TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(on ? KnobTravel : 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.4 },
                });
        }

        // Duration choices as a segmented control under the label.
        void Choice(string icon, string label, string hint, int[] seconds, bool zeroIsNever, Func<int> get, Action<int> set)
        {
            string[] names = new string[seconds.Length];
            for (int i = 0; i < seconds.Length; i++) names[i] = Strings.Duration(seconds[i], zeroIsNever);
            int current = Array.IndexOf(seconds, get());
            Row(icon, label, hint, null, SegmentBar(names, current, i => set(seconds[i])));
        }

        void Segmented(string icon, string label, string[] names, Func<int> get, Action<int> set)
        {
            Row(icon, label, null, SegmentBar(names, get(), set, true));
        }

        FrameworkElement SegmentBar(string[] names, int current, Action<int> set, bool inline = false)
        {
            return SlidingBar(names, current, i => { set(i); Apply(); }, SettingsTheme.SegTrack,
                inline ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                inline ? new Thickness(0) : new Thickness(14, 0, 14, 12));
        }

        void SliderRow(string icon, string label, double min, double max, double step, Func<double, string> format,
                       Func<double> get, Action<double> set, bool peek)
        {
            TextBlock value = new TextBlock { Text = format(get()), Foreground = (Brush)FindResource("Secondary"), VerticalAlignment = VerticalAlignment.Center };
            Slider slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = get(),
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                SmallChange = step,
                LargeChange = step * 2,
                Margin = new Thickness(16, 0, 16, 10),
            };
            slider.ValueChanged += (s, e) =>
            {
                set(Math.Round(e.NewValue, 3));
                value.Text = format(e.NewValue);
                if (peek) peekExpanded();
                Apply();
            };
            Row(icon, label, null, value, slider);
        }

        static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        void ActionRow(string icon, string label, Action action, bool danger = false, bool external = false)
        {
            Button button = new Button { Content = external ? ExternalLabel(label) : (object)label, Style = (Style)FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            if (danger) button.Foreground = DangerBrush;
            button.Click += (s, e) => action();
            Row(icon, null, null, null, null, button);
            if (danger)
            {
                // The icon in red too
                StackPanel row = (StackPanel)card.Children[card.Children.Count - 1];
                Grid grid = row.Children[row.Children.Count - 1] as Grid;
                TextBlock mark = grid != null && grid.Children.Count > 0 ? grid.Children[0] as TextBlock : null;
                if (mark != null) mark.Foreground = DangerBrush;
            }
        }

        static string Percent(double value) { return Math.Round(value * 100) + "%"; }
        static string Pixels(double value) { return Math.Round(value) + " px"; }

        static string Horizontal(double value)
        {
            if (Math.Abs(value) < 1) return Strings.Centered;
            return (value > 0 ? "+" : "−") + Math.Round(Math.Abs(value)) + " px";
        }
    }
}
