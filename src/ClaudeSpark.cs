using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsIsland
{
    // Claude's spark. At rest it shows the full burst; while Claude works it plays the thinking
    // animation from the sprite sheet in Assets\thinking.png (12 frames of 128 px).
    sealed class ClaudeSpark : FrameworkElement
    {
        const int FrameCount = 12;
        const int FrameSize = 128;
        const int RestFrame = 4;
        const double FrameSeconds = 0.13;

        static readonly BitmapSource[] Frames = LoadFrames();

        const double TurnDegreesPerSecond = 24;

        double time;
        bool thinking;
        int frame = RestFrame;
        double blend;
        double angle;

        public ClaudeSpark()
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        public bool Thinking
        {
            get { return thinking; }
            set
            {
                if (thinking == value) return;
                thinking = value;
                time = 0; // Always start the cycle from the dot
                SetFrame(thinking ? 0 : RestFrame);
            }
        }

        // The source has only 12 frames (about 8 per second). Drawing every display frame as a blend of
        // the current and next source frame, plus a slow turn, turns those steps into continuous motion.
        public void Advance(double dt)
        {
            if (!thinking) return;
            time += dt;
            double position = time / FrameSeconds;
            frame = (int)position % FrameCount;
            double t = position - Math.Floor(position);
            blend = t * t * (3 - 2 * t); // Smoothstep: rests on each drawn frame, glides between them
            angle = time * TurnDegreesPerSecond % 360;
            InvalidateVisual();
        }

        void SetFrame(int value)
        {
            if (value == frame && blend == 0 && angle == 0) return;
            frame = value;
            blend = 0;
            angle = 0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext context)
        {
            if (Frames == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            double size = Math.Min(ActualWidth, ActualHeight);
            Rect area = new Rect((ActualWidth - size) / 2, (ActualHeight - size) / 2, size, size);

            bool turning = angle != 0;
            if (turning) context.PushTransform(new RotateTransform(angle, ActualWidth / 2, ActualHeight / 2));
            if (blend <= 0.001)
            {
                context.DrawImage(Frames[frame], area);
            }
            else
            {
                context.PushOpacity(1 - blend);
                context.DrawImage(Frames[frame], area);
                context.Pop();
                context.PushOpacity(blend);
                context.DrawImage(Frames[(frame + 1) % FrameCount], area);
                context.Pop();
            }
            if (turning) context.Pop();
        }

        static BitmapSource[] LoadFrames()
        {
            try
            {
                BitmapImage sheet = new BitmapImage(new Uri("pack://application:,,,/Assets/thinking.png"));
                sheet.Freeze();
                BitmapSource[] frames = new BitmapSource[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                {
                    CroppedBitmap crop = new CroppedBitmap(sheet, new Int32Rect(i * FrameSize, 0, FrameSize, FrameSize));
                    crop.Freeze();
                    frames[i] = crop;
                }
                return frames;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
