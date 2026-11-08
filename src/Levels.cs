using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;

namespace WindowsIsland
{
    // Tells when the speaker volume or the screen brightness changes, so the island can show it like a phone does.
    // Volume comes from the default speakers' master level in Windows' audio system; brightness from the built-in
    // laptop screen's driver (external monitors have no brightness Windows can read, so nothing shows for them).
    // Only two numbers are read; nothing is changed.
    sealed class LevelService
    {
        public enum Kind { Volume, Brightness }

        const int eRender = 0, eMultimedia = 1, CLSCTX_ALL = 23;

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints_(); // Unused slots keep the vtable order
            void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            void RegisterControlChangeNotify_();
            void UnregisterControlChangeNotify_();
            void GetChannelCount_();
            void SetMasterVolumeLevel_();
            void SetMasterVolumeLevelScalar_();
            void GetMasterVolumeLevel_();
            void GetMasterVolumeLevelScalar(out float level);
            void SetChannelVolumeLevel_();
            void SetChannelVolumeLevelScalar_();
            void GetChannelVolumeLevel_();
            void GetChannelVolumeLevelScalar_();
            void SetMute_();
            void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        }

        // The laptop panel's brightness, asked of the display driver.
        const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_SHARE_READ_WRITE = 3, OPEN_EXISTING = 3;
        const uint IOCTL_VIDEO_QUERY_DISPLAY_BRIGHTNESS = 0x23049C;

        [StructLayout(LayoutKind.Sequential)]
        struct DISPLAY_BRIGHTNESS { public byte Policy, AcLevel, DcLevel; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, int inputSize, out DISPLAY_BRIGHTNESS output, int outputSize, out int returned, IntPtr overlapped);

        public event Action<Kind, int, bool> Changed; // kind, percent, muted

        readonly DispatcherTimer timer;
        int lastVolume = -1, lastBrightness = -1;
        bool lastMuted;
        bool noBrightness;
        int brightnessTick;

        public LevelService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            lastVolume = lastBrightness = -1; // The first reading is only a starting point
            noBrightness = false;
            timer.Start();
        }

        public void Stop() { timer.Stop(); }

        void Poll()
        {
            int volume;
            bool muted;
            if (ReadVolume(out volume, out muted))
            {
                if (lastVolume >= 0 && (volume != lastVolume || muted != lastMuted)) Changed?.Invoke(Kind.Volume, volume, muted);
                lastVolume = volume;
                lastMuted = muted;
            }

            if (!noBrightness && ++brightnessTick % 4 == 0) // About once a second
            {
                int brightness = ReadBrightness();
                if (brightness < 0) { if (lastBrightness < 0) noBrightness = true; }
                else
                {
                    if (lastBrightness >= 0 && brightness != lastBrightness) Changed?.Invoke(Kind.Brightness, brightness, false);
                    lastBrightness = brightness;
                }
            }
        }

        static bool ReadVolume(out int percent, out bool muted)
        {
            percent = 0;
            muted = false;
            object endpoint = null;
            IMMDevice device = null;
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out device);
                Guid id = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref id, CLSCTX_ALL, IntPtr.Zero, out endpoint);
                IAudioEndpointVolume volume = (IAudioEndpointVolume)endpoint;
                float level;
                volume.GetMasterVolumeLevelScalar(out level);
                volume.GetMute(out muted);
                percent = (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (endpoint != null) Marshal.ReleaseComObject(endpoint);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        // The current level in percent, or -1 when this PC has no built-in screen to read.
        static int ReadBrightness()
        {
            try
            {
                using (SafeFileHandle device = CreateFile(@"\\.\LCD", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                {
                    if (device.IsInvalid) return -1;
                    DISPLAY_BRIGHTNESS result;
                    int returned;
                    if (!DeviceIoControl(device, IOCTL_VIDEO_QUERY_DISPLAY_BRIGHTNESS, IntPtr.Zero, 0, out result, Marshal.SizeOf<DISPLAY_BRIGHTNESS>(), out returned, IntPtr.Zero)) return -1;
                    bool onBattery = SystemInformationOnBattery();
                    return Math.Clamp(onBattery ? result.DcLevel : result.AcLevel, (byte)0, (byte)100);
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        static bool SystemInformationOnBattery()
        {
            return System.Windows.SystemParameters.PowerLineStatus == System.Windows.PowerLineStatus.Offline;
        }
    }
}
