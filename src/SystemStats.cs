using System;
using System.Runtime.InteropServices;

namespace WindowsIsland
{
    // How busy the processor is and how much memory is in use, for the clock and system page. Two numbers read from
    // Windows each second while that page is open; nothing is stored.
    sealed class SystemStats
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

        ulong lastIdle, lastTotal;

        public int Cpu { get; private set; }
        public int Memory { get; private set; }

        public void Update()
        {
            FILETIME idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user))
            {
                ulong total = kernel.Value + user.Value; // Kernel time already includes idle time
                if (lastTotal != 0 && total > lastTotal)
                {
                    double busy = 1 - (double)(idle.Value - lastIdle) / (total - lastTotal);
                    Cpu = (int)Math.Round(Math.Clamp(busy, 0, 1) * 100);
                }
                lastIdle = idle.Value;
                lastTotal = total;
            }
            MEMORYSTATUSEX memory = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memory)) Memory = (int)memory.MemoryLoad;
        }

        // Forget the last sample, so the first reading after a pause is not an average over the whole pause.
        public void Reset() { lastIdle = lastTotal = 0; }
    }
}
