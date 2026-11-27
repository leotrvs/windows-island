using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // System pages: the monitor (processor, memory), the battery, Discord and focus.
    public partial class IslandWindow
    {
        // Muting or unmuting in Discord brings the island back for a moment, even if it was put away or auto-hidden,
        // so the change is noticed; the crossed icon stays beside it while you are muted.
        bool discordWasMuted, discordWasDeaf;
        DateTime discordChangedAt = DateTime.MinValue;
        void OnDiscordChanged()
        {
            bool changed = discord != null && (discord.Muted != discordWasMuted || discord.Deafened != discordWasDeaf);
            if (discord != null) { discordWasMuted = discord.Muted; discordWasDeaf = discord.Deafened; }
            if (changed && settings.DiscordEnabled)
            {
                tucked = false;
                lastActive = DateTime.Now; // The auto-hide time starts again
                discordChangedAt = DateTime.Now;
            }
            UpdatePrivacy();
            Recompute();
        }
        void OnBatteryChanged() { UpdateBattery(); Recompute(); }

        // The clock and system page: the time, and the processor and memory load, refreshed once a second.
        readonly SystemStats systemStats = new SystemStats();
        DateTime lastSystemUpdate = DateTime.MinValue;
        void UpdateSystemPage()
        {
            DateTime now = DateTime.Now;
            string time = now.ToString("t");
            if (ClockBig.Text != time) ClockBig.Text = time;
            if (now - lastSystemUpdate < TimeSpan.FromSeconds(1) && lastSystemUpdate != DateTime.MinValue) return;
            if (now - lastSystemUpdate > TimeSpan.FromSeconds(3)) systemStats.Reset();
            lastSystemUpdate = now;
            systemStats.Update();
            ClockDate.Text = now.ToString("dddd, d MMMM");
            CpuLabel.Text = Strings.CpuLabel;
            MemoryLabel.Text = Strings.MemoryLabel;
            CpuText.Text = systemStats.Cpu + "%";
            MemoryText.Text = systemStats.Memory + "%";
            CpuFill.Width = 132.0 * systemStats.Cpu / 100;
            MemoryFill.Width = 132.0 * systemStats.Memory / 100;
        }
        // The system monitor on the collapsed island: processor and memory as rings, bars or numbers, once a second.
        static readonly Brush MonitorBlue = Freeze(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
        static readonly Brush MonitorGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)));
        readonly DispatcherTimer monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        double monitorWidth = 140;
        string monitorStyleDrawn;

        string MonitorStyle
        {
            get
            {
                string style = settings.MonitorStyle ?? "";
                return style.Equals("Bars", StringComparison.OrdinalIgnoreCase) ? "Bars" : style.Equals("Numbers", StringComparison.OrdinalIgnoreCase) ? "Numbers" : "Rings";
            }
        }

        bool thinGauges; // Processor and memory as two tiny rings beside the thin line, while nothing else is happening
        readonly Spring thinGaugesOpacity = new Spring(0, 0.002, 500, 1);

        void UpdateThinGauges()
        {
            if (!thinGauges) return;
            UpdateSystemPage(); // Reads the processor and memory again (once a second)
            int cpu = systemStats.Cpu, memory = systemStats.Memory;
            ThinCpuRing.Data = Arc(Math.Max(cpu, 3), 14, 1.6);
            ThinMemRing.Data = Arc(Math.Max(memory, 3), 14, 1.6);
            ThinCpuRing.Stroke = cpu >= 90 ? Red : cpu >= 70 ? Amber : MonitorBlue;
            ThinMemRing.Stroke = memory >= 90 ? Red : memory >= 70 ? Amber : MonitorGreen;
        }

        void UpdateSystemMonitor()
        {
            UpdateSystemPage();
            int cpu = systemStats.Cpu, memory = systemStats.Memory;
            string style = MonitorStyle;
            if (style != monitorStyleDrawn)
            {
                monitorStyleDrawn = style;
                SysRings.Visibility = style == "Rings" ? Visibility.Visible : Visibility.Collapsed;
                SysBars.Visibility = style == "Bars" ? Visibility.Visible : Visibility.Collapsed;
                SysNumbers.Visibility = style == "Numbers" ? Visibility.Visible : Visibility.Collapsed;
                FrameworkElement active = style == "Rings" ? (FrameworkElement)SysRings : style == "Bars" ? SysBars : SysNumbers;
                active.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                monitorWidth = Math.Ceiling(active.DesiredSize.Width) + 34; // The same breathing room on both sides
                SystemCompact.Width = monitorWidth;
                if (view == View.SystemCompact) ApplyTargets();
            }
            Brush cpuBrush = cpu >= 90 ? Red : cpu >= 70 ? Amber : MonitorBlue;
            Brush memoryBrush = memory >= 90 ? Red : memory >= 70 ? Amber : MonitorGreen;
            if (style == "Rings")
            {
                SysCpuRing.Data = Arc(Math.Max(cpu, 2), 20, 3);
                SysMemRing.Data = Arc(Math.Max(memory, 2), 20, 3);
                SysCpuRing.Stroke = cpuBrush;
                SysMemRing.Stroke = memoryBrush;
                SysCpuRingText.Text = cpu + "%";
                SysMemRingText.Text = memory + "%";
            }
            else if (style == "Bars")
            {
                SysCpuBar.Width = 42.0 * cpu / 100;
                SysMemBar.Width = 42.0 * memory / 100;
                SysCpuBar.Background = cpuBrush;
                SysMemBar.Background = memoryBrush;
            }
            else
            {
                SysCpuNumber.Text = cpu + "%";
                SysMemNumber.Text = memory + "%";
            }
        }

        void OnFocusChanged() { UpdateFocusMoon(); }
        DiscordService discord;

        FocusService focusState;
        void UpdateFocusMoon()
        {
            FocusMoon.Visibility = settings.ShowFocus && focusState != null && focusState.On ? Visibility.Visible : Visibility.Collapsed;
        }

        BatteryService battery;
        bool BatteryActive { get { return settings.BatteryActivity && battery != null && battery.Charging; } }

        void UpdateBattery()
        {
            if (battery != null && battery.HasBattery) BatteryCompactText.Text = battery.Percent + "%";
        }
    }
}
