using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsIsland
{
    // The few things the shortcuts page does to the computer: mute or unmute the microphone or the speakers, set the volume
    // and take a screenshot. Only the default devices are touched, and nothing is read from the microphone: only its mute switch.
    static class AudioControl
    {
        public const int Render = 0, Capture = 1;
        const int eMultimedia = 1, CLSCTX_ALL = 23;

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
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float db);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        static IAudioEndpointVolume Endpoint(int flow)
        {
            IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice device;
            enumerator.GetDefaultAudioEndpoint(flow, eMultimedia, out device);
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object endpoint;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out endpoint);
            return (IAudioEndpointVolume)endpoint;
        }

        public static bool IsMuted(int flow)
        {
            try
            {
                bool muted;
                return Endpoint(flow).GetMute(out muted) == 0 && muted;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SetMuted(int flow, bool mute)
        {
            try
            {
                Guid context = Guid.Empty;
                Endpoint(flow).SetMute(mute, ref context);
            }
            catch (Exception)
            {
                // No such device (a PC without a microphone): nothing to do
            }
        }

        // 0-100, or -1 when it cannot be read
        public static int Volume()
        {
            try
            {
                float level;
                return Endpoint(Render).GetMasterVolumeLevelScalar(out level) == 0 ? (int)Math.Round(level * 100) : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static void SetVolume(int percent)
        {
            try
            {
                Guid context = Guid.Empty;
                Endpoint(Render).SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context);
            }
            catch (Exception)
            {
            }
        }

        [DllImport("user32.dll")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        // Win+Shift+S, the same as pressing the keys: Windows' own snipping bar opens
        public static void Screenshot()
        {
            const byte VK_LWIN = 0x5B, VK_SHIFT = 0x10, VK_S = 0x53;
            const uint KEYUP = 2;
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            keybd_event(VK_S, 0, 0, UIntPtr.Zero);
            keybd_event(VK_S, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYUP, UIntPtr.Zero);
        }

        public static void OpenSoundSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true })?.Dispose(); }
            catch (Exception) { }
        }
    }
}
