using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WindowsIsland
{
    // A countdown timer or a stopwatch, started from the tray menu. One instance feeds every island.
    sealed class TimerService
    {
        public enum Kind { None, Countdown, Stopwatch }

        public Kind Mode { get; private set; }
        public event Action Changed;
        public event Action Finished;

        DateTime started;
        TimeSpan length;
        readonly DispatcherTimer tick;

        public TimerService(Dispatcher dispatcher)
        {
            tick = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
            tick.Tick += (s, e) =>
            {
                if (Mode == Kind.Countdown && Remaining <= TimeSpan.Zero)
                {
                    Stop();
                    Finished?.Invoke();
                }
            };
        }

        public bool Running { get { return Mode != Kind.None; } }

        // Time left on a countdown, or time elapsed on a stopwatch.
        public TimeSpan Shown
        {
            get
            {
                if (Mode == Kind.Countdown) return Remaining < TimeSpan.Zero ? TimeSpan.Zero : Remaining;
                if (Mode == Kind.Stopwatch) return DateTime.UtcNow - started;
                return TimeSpan.Zero;
            }
        }

        TimeSpan Remaining { get { return length - (DateTime.UtcNow - started); } }

        public void StartCountdown(int minutes)
        {
            Mode = Kind.Countdown;
            length = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 600));
            started = DateTime.UtcNow;
            tick.Start();
            Changed?.Invoke();
        }

        public void StartStopwatch()
        {
            Mode = Kind.Stopwatch;
            started = DateTime.UtcNow;
            tick.Start();
            Changed?.Invoke();
        }

        public void Stop()
        {
            if (Mode == Kind.None) return;
            Mode = Kind.None;
            tick.Stop();
            Changed?.Invoke();
        }

        public static string Format(TimeSpan t)
        {
            int total = (int)Math.Ceiling(t.TotalSeconds);
            if (total < 0) total = 0;
            int h = total / 3600, m = total / 60 % 60, s = total % 60;
            return h > 0 ? string.Format("{0}:{1:00}:{2:00}", h, m, s) : string.Format("{0}:{1:00}", m, s);
        }
    }

    // Whether the microphone or camera is in use right now, from the "recent use" record Windows keeps for
    // the privacy indicators in Settings. Nothing is recorded or listened to; only two timestamps are read.
    sealed class PrivacyService
    {
        const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";

        public bool Microphone { get; private set; }
        public bool Camera { get; private set; }
        public event Action Changed;

        readonly DispatcherTimer timer;

        public PrivacyService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (Microphone || Camera)
            {
                Microphone = Camera = false;
                Changed?.Invoke();
            }
        }

        void Poll()
        {
            bool mic = InUse("microphone"), cam = InUse("webcam");
            if (mic == Microphone && cam == Camera) return;
            Microphone = mic;
            Camera = cam;
            Changed?.Invoke();
        }

        static bool InUse(string capability)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Root + capability))
                {
                    if (key == null) return false;
                    if (AnyInUse(key)) return true;
                    using (RegistryKey desktop = key.OpenSubKey("NonPackaged"))
                        return desktop != null && AnyInUse(desktop);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // In use: the last start is recorded and no stop has been recorded since.
        static bool AnyInUse(RegistryKey parent)
        {
            foreach (string name in parent.GetSubKeyNames())
            {
                if (name == "NonPackaged") continue;
                using (RegistryKey app = parent.OpenSubKey(name))
                {
                    if (app == null) continue;
                    object start = app.GetValue("LastUsedTimeStart"), stop = app.GetValue("LastUsedTimeStop");
                    if (start is long s && stop is long e && s > 0 && e == 0) return true;
                }
            }
            return false;
        }
    }

    // Charger and low-battery alerts on laptops. Does nothing on a PC without a battery.
    sealed class BatteryService
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        public event Action<string, string> Alert; // title, body

        readonly DispatcherTimer timer;
        bool known, wasOnAc, wasFull;
        int lastPercent = 100, lowNotified = 100;

        public BatteryService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(20) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            known = false;
            Poll();
            timer.Start();
        }

        public void Stop() { timer.Stop(); }

        void Poll()
        {
            SYSTEM_POWER_STATUS status;
            if (!GetSystemPowerStatus(out status) || (status.BatteryFlag & 128) != 0 || status.BatteryLifePercent > 100) return; // No battery
            bool onAc = status.ACLineStatus == 1;
            int percent = status.BatteryLifePercent;
            bool full = onAc && percent >= 100;

            if (known)
            {
                if (onAc && !wasOnAc) Alert?.Invoke(Strings.BatteryCharging, percent + "%");
                else if (!onAc && wasOnAc) Alert?.Invoke(Strings.BatteryUnplugged, percent + "%");
                else if (full && !wasFull) Alert?.Invoke(Strings.BatteryFull, "100%");
                if (!onAc)
                {
                    int level = percent <= 10 ? 10 : percent <= 20 ? 20 : 100;
                    if (level < lowNotified) Alert?.Invoke(Strings.BatteryLow, percent + "%");
                    lowNotified = Math.Min(lowNotified, level);
                }
            }
            if (onAc) lowNotified = 100;
            known = true;
            wasOnAc = onAc;
            wasFull = full;
            lastPercent = percent;
        }
    }
}
