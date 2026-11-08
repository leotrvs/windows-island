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

        // The lengths offered by the - and + buttons in the island, in minutes.
        public static readonly int[] Steps = { 1, 2, 3, 5, 10, 15, 20, 25, 30, 45, 60, 90, 120 };

        public Kind Mode { get; private set; }
        public bool Paused { get; private set; }
        public int ChosenMinutes { get; set; } = 5; // What Start uses
        public event Action Changed;
        public event Action Finished;

        DateTime started;
        TimeSpan before; // Time counted before the last pause
        TimeSpan length;
        readonly DispatcherTimer tick;

        public TimerService(Dispatcher dispatcher)
        {
            tick = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
            tick.Tick += (s, e) =>
            {
                if (Mode == Kind.Countdown && !Paused && length - Elapsed <= TimeSpan.Zero)
                {
                    Stop();
                    Finished?.Invoke();
                }
            };
        }

        public bool Running { get { return Mode != Kind.None; } }

        TimeSpan Elapsed { get { return before + (Paused ? TimeSpan.Zero : DateTime.UtcNow - started); } }

        // Time left on a countdown, or time elapsed on a stopwatch.
        public TimeSpan Shown
        {
            get
            {
                if (Mode == Kind.Countdown) { TimeSpan left = length - Elapsed; return left < TimeSpan.Zero ? TimeSpan.Zero : left; }
                if (Mode == Kind.Stopwatch) return Elapsed;
                return TimeSpan.Zero;
            }
        }

        public void StartCountdown(int minutes)
        {
            Mode = Kind.Countdown;
            length = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 600));
            Begin();
        }

        public void StartStopwatch()
        {
            Mode = Kind.Stopwatch;
            Begin();
        }

        void Begin()
        {
            before = TimeSpan.Zero;
            Paused = false;
            started = DateTime.UtcNow;
            tick.Start();
            Changed?.Invoke();
        }

        public void PauseOrResume()
        {
            if (Mode == Kind.None) return;
            if (Paused) { started = DateTime.UtcNow; Paused = false; }
            else { before = Elapsed; Paused = true; }
            Changed?.Invoke();
        }

        public void Stop()
        {
            if (Mode == Kind.None) return;
            Mode = Kind.None;
            Paused = false;
            tick.Stop();
            Changed?.Invoke();
        }

        // Moves the chosen length to the next or previous step.
        public void Nudge(int direction)
        {
            int index = Array.FindIndex(Steps, m => m >= ChosenMinutes);
            if (index < 0) index = Steps.Length - 1;
            if (Steps[index] != ChosenMinutes && direction < 0) direction = 0; // Between steps: snap down
            ChosenMinutes = Steps[Math.Clamp(index + direction, 0, Steps.Length - 1)];
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
            bool mic = InUse("microphone") || AudioCapture.Active(), cam = InUse("webcam");
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

        public event Action<string, string, string> Alert; // title, body, kind

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
                if (onAc && !wasOnAc) Alert?.Invoke(Strings.BatteryCharging, percent + "%", "charging");
                else if (!onAc && wasOnAc) Alert?.Invoke(Strings.BatteryUnplugged, percent + "%", "unplugged");
                else if (full && !wasFull) Alert?.Invoke(Strings.BatteryFull, "100%", "full");
                if (!onAc)
                {
                    int level = percent <= 10 ? 10 : percent <= 20 ? 20 : 100;
                    if (level < lowNotified) Alert?.Invoke(Strings.BatteryLow, percent + "%", "low");
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

    // Whether any app is recording from a microphone right now, asked of Windows' audio sessions. This catches
    // apps the privacy record in the registry misses (some web and Electron apps). Only the "active" state of
    // each capture session is read: no audio is opened or heard.
    static class AudioCapture
    {
        const int eCapture = 1, DEVICE_STATE_ACTIVE = 1, AudioSessionStateActive = 1, CLSCTX_ALL = 23;

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            void GetCount(out int count);
            void Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            void GetAudioSessionControl_(); // Unused slots keep the vtable order
            void GetSimpleAudioVolume_();
            void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            void GetCount(out int count);
            void GetSession(int index, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            void GetState(out int state);
            void GetDisplayName_();
            void SetDisplayName_();
            void GetIconPath_();
            void SetIconPath_();
            void GetGroupingParam_();
            void SetGroupingParam_();
            void RegisterAudioSessionNotification_();
            void UnregisterAudioSessionNotification_();
            void GetSessionIdentifier_();
            void GetSessionInstanceIdentifier_();
            void GetProcessId(out uint processId);
        }

        public static bool Active()
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDeviceCollection devices;
                enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, out devices);
                int count;
                devices.GetCount(out count);
                Guid managerId = typeof(IAudioSessionManager2).GUID;
                for (int i = 0; i < count; i++)
                {
                    IMMDevice device;
                    devices.Item(i, out device);
                    object manager;
                    device.Activate(ref managerId, CLSCTX_ALL, IntPtr.Zero, out manager);
                    IAudioSessionEnumerator sessions;
                    ((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions);
                    int sessionCount;
                    sessions.GetCount(out sessionCount);
                    for (int j = 0; j < sessionCount; j++)
                    {
                        IAudioSessionControl2 session;
                        sessions.GetSession(j, out session);
                        int state;
                        session.GetState(out state);
                        uint pid;
                        session.GetProcessId(out pid);
                        Marshal.ReleaseComObject(session);
                        if (state == AudioSessionStateActive && pid != 0) return true;
                    }
                    Marshal.ReleaseComObject(sessions);
                    Marshal.ReleaseComObject(manager);
                    Marshal.ReleaseComObject(device);
                }
                Marshal.ReleaseComObject(devices);
                Marshal.ReleaseComObject(enumerator);
            }
            catch (Exception)
            {
            }
            return false;
        }
    }
}
