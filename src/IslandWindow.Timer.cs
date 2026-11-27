using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsIsland
{
    // The timer and the pomodoro: the page, the ring around the island and the live countdown.
    public partial class IslandWindow
    {
        void OnTimerChanged() { UpdateTimerPage(); Recompute(); }

        // The timer's outline: a ring around the pill that is full at the start and fades away as the time runs out
        // (orange for focus, green for a break). A faint rail stays behind it. It shows only on the compact timer.
        readonly System.Windows.Shapes.Path timerRing = new System.Windows.Shapes.Path { StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false, Opacity = 0 };
        readonly System.Windows.Shapes.Path timerRail = new System.Windows.Shapes.Path { StrokeThickness = 2, IsHitTestVisible = false, Opacity = 0, Stroke = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF))) };
        double ringW, ringH, ringLength;

        void UpdateTimerRing(double w, double h)
        {
            double presenceValue = presence[TimerCompact].Value;
            bool on = presenceValue > 0.01 && timer.Running && timer.Mode == TimerService.Kind.Countdown;
            if (!on)
            {
                if (timerRing.Opacity != 0) { timerRing.Opacity = 0; timerRail.Opacity = 0; }
                return;
            }
            const double inset = 2;
            double a = w - 2 * inset, b = h - 2 * inset, r = b / 2;
            if (a <= b || b <= 4) { timerRing.Opacity = 0; timerRail.Opacity = 0; return; }
            if (w != ringW || h != ringH)
            {
                ringW = w; ringH = h;
                PathFigure figure = new PathFigure { StartPoint = new Point(w / 2, inset) };
                figure.Segments.Add(new LineSegment(new Point(w - inset - r, inset), true));
                figure.Segments.Add(new ArcSegment(new Point(w - inset - r, h - inset), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
                figure.Segments.Add(new LineSegment(new Point(inset + r, h - inset), true));
                figure.Segments.Add(new ArcSegment(new Point(inset + r, inset), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
                figure.Segments.Add(new LineSegment(new Point(w / 2, inset), true));
                PathGeometry geometry = new PathGeometry(new[] { figure });
                geometry.Freeze();
                timerRing.Data = geometry;
                timerRail.Data = geometry;
                ringLength = 2 * (a - b) + Math.PI * b;
            }
            double left = Math.Clamp(timer.Fraction, 0, 1);
            double unit = ringLength / timerRing.StrokeThickness;
            // The part that is left ends at the top and shrinks clockwise, like the iPhone's timer
            timerRing.StrokeDashArray = new DoubleCollection { left * unit, unit };
            timerRing.StrokeDashOffset = -(1 - left) * unit;
            timerRing.Stroke = OnBreak ? Green : Amber;
            timerRing.Opacity = timerRail.Opacity = Math.Clamp(presenceValue, 0, 1);
        }

        string lastTimerText;
        void UpdateTimerLive()
        {
            string text = TimerService.Format(timer.Running ? timer.Shown : TimeSpan.FromMinutes(timer.ChosenMinutes));
            if (text != lastTimerText)
            {
                lastTimerText = text;
                TimerCompactText.Text = text;
                TimerBig.Text = text;
            }
            TimerCompactText.Opacity = timer.Paused ? 0.5 : 1;
            Brush phase = OnBreak ? Green : Amber; // Focus is orange, a break is green
            TimerCompactText.Foreground = phase;
            TimerBig.Foreground = OnBreak ? Green : Brushes.White;
            UpdateTimerRing(Pill.Width, Pill.Height);
        }

        // The lengths chosen in Settings go to the timer
        void SyncPomodoro()
        {
            timer.FocusMinutes = settings.PomodoroFocus;
            timer.BreakMinutes = settings.PomodoroBreak;
            timer.LongBreakMinutes = settings.PomodoroLong;
        }

        FontFamily plusFont; // The "+" button's own font, back when it is not the skip button
        bool OnBreak { get { return timer.Pomodoro && timer.Current != TimerService.Phase.Focus; } }

        void UpdateTimerPage()
        {
            bool running = timer.Running;
            bool pomodoro = running && timer.Pomodoro;
            TimerHeader.Text = !running ? Strings.TimerPickLength
                             : pomodoro ? Strings.PhaseName(timer.Current) + " · " + Strings.PomodoroCycle(timer.Cycle, timer.CyclesBeforeLong) + (timer.Paused ? " · " + Strings.TimerPaused : "")
                             : (timer.Mode == TimerService.Kind.Countdown ? Strings.TimerLabel : Strings.StopwatchLabel)
                               + (timer.Paused ? " · " + Strings.TimerPaused : "");
            BtnMinus.Visibility = running ? Visibility.Hidden : Visibility.Visible;
            BtnPlus.Visibility = !running || pomodoro ? Visibility.Visible : Visibility.Hidden; // A running pomodoro skips with it
            TimerDots.Visibility = pomodoro ? Visibility.Visible : Visibility.Collapsed;
            if (pomodoro)
            {
                TimerDots.Children.Clear();
                for (int i = 1; i <= timer.CyclesBeforeLong; i++)
                    TimerDots.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width = 6, Height = 6, Margin = new Thickness(0, 0, 4, 0),
                        Fill = i < timer.Cycle || (i == timer.Cycle && timer.Current == TimerService.Phase.Focus) ? Amber : (Brush)new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)),
                    });
            }
            BtnPlusText.Text = pomodoro ? "\uE893" : "+";
            BtnPlusText.FontFamily = pomodoro ? FluentIcons : plusFont;
            BtnPlusText.FontSize = pomodoro ? 14 : 20;
            BtnStart.Visibility = BtnWatch.Visibility = BtnPomodoro.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
            BtnPause.Visibility = BtnStop.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            BtnPauseText.Text = timer.Paused ? Strings.TimerResume : Strings.TimerPause;
            TimerBig.Opacity = timer.Paused ? 0.5 : 1;
            lastTimerText = null;
            UpdateTimerLive();
        }
    }
}
