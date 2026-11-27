using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WindowsIsland
{
    // Small shared helpers: buttons, arcs, brushes and text formats.
    public partial class IslandWindow
    {
        static void BuildBars(Canvas canvas, Rectangle[] bars, double barWidth, double gap)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                Rectangle bar = new Rectangle { Width = barWidth, Height = barWidth, RadiusX = barWidth / 2, RadiusY = barWidth / 2, Fill = White };
                Canvas.SetLeft(bar, i * (barWidth + gap));
                canvas.Children.Add(bar);
                bars[i] = bar;
            }
        }

        static void SetupButton(Border button, Action action)
        {
            ScaleTransform scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                button.CaptureMouse();
                AnimateScale(scale, 0.8, 90, new QuadraticEase { EasingMode = EasingMode.EaseOut });
            };
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                bool inside = button.IsMouseOver;
                button.ReleaseMouseCapture();
                AnimateScale(scale, 1, 320, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 });
                if (inside) action();
            };
        }

        static void AnimateScale(ScaleTransform scale, double to, int milliseconds, IEasingFunction easing)
        {
            DoubleAnimation animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        static Geometry Arc(double percent, double size, double thickness)
        {
            double fraction = Math.Clamp(percent / 100, 0, 1);
            if (fraction <= 0.001) return Geometry.Empty;
            double r = (size - thickness) / 2;
            Point center = new Point(size / 2, size / 2);
            if (fraction >= 0.999)
            {
                EllipseGeometry full = new EllipseGeometry(center, r, r);
                full.Freeze();
                return full;
            }

            double angle = fraction * 2 * Math.PI;
            Point start = new Point(center.X, center.Y - r);
            Point end = new Point(center.X + r * Math.Sin(angle), center.Y - r * Math.Cos(angle));
            PathFigure figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        // The usage rings use the chosen color, then turn amber and red as the limit gets close.
        Brush RingBrush(int percent)
        {
            if (percent >= 90) return Red;
            if (percent >= 70) return Amber;
            string choice = settings.RingColor ?? "Blue";
            return choice.Equals("Green", StringComparison.OrdinalIgnoreCase) ? Green
                 : choice.Equals("Purple", StringComparison.OrdinalIgnoreCase) ? Purple
                 : Blue;
        }

        static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0}:{1:00}", time.Minutes, time.Seconds);
        }

        static string Join(string first, string second)
        {
            return string.IsNullOrEmpty(second) ? first : first + " · " + second;
        }

        static T Freeze<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
