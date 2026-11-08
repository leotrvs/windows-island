using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace WindowsIsland
{
    // Tells when a Bluetooth device connects or disconnects, when a USB storage drive or phone is plugged in or
    // removed, and when the network goes away or comes back. Only names and states are read; no device is opened.
    sealed class DeviceService
    {
        public enum Kind { Bluetooth, Usb, Network }

        public event Action<Kind, string, bool> Changed; // kind, name, connected

        readonly Dispatcher dispatcher;
        DeviceWatcher bluetooth, usb;
        readonly HashSet<string> seen = new HashSet<string>();
        bool bluetoothReady, usbReady;
        bool? online;
        DispatcherTimer networkDebounce;

        public DeviceService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public void Start(bool devices, bool network)
        {
            if (devices) StartDevices(); else StopDevices();
            if (network) StartNetwork(); else StopNetwork();
        }

        public void Stop()
        {
            StopDevices();
            StopNetwork();
        }

        // ---------- Bluetooth and USB ----------

        void StartDevices()
        {
            if (bluetooth != null) return;
            try
            {
                string connected = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                bluetooth = Watch(connected, Kind.Bluetooth, () => bluetoothReady = true, () => bluetoothReady);
                // Portable devices (phones, cameras) and removable drives
                usb = Watch("System.Devices.InterfaceClassGuid:=\"{6AC27878-A6FA-4155-BA85-F98F491D4F33}\" OR System.Devices.InterfaceClassGuid:=\"{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}\"",
                            Kind.Usb, () => usbReady = true, () => usbReady);
            }
            catch (Exception)
            {
                StopDevices();
            }
        }

        DeviceWatcher Watch(string selector, Kind kind, Action ready, Func<bool> isReady)
        {
            DeviceWatcher watcher = DeviceInformation.CreateWatcher(selector);
            watcher.Added += (w, info) => Raise(kind, info.Name, true, isReady(), info.Id);
            watcher.Removed += (w, update) => Raise(kind, null, false, isReady(), update.Id);
            watcher.EnumerationCompleted += (w, o) => ready();
            watcher.Start();
            return watcher;
        }

        readonly Dictionary<string, string> names = new Dictionary<string, string>();

        void Raise(Kind kind, string name, bool connected, bool ready, string id)
        {
            lock (names)
            {
                if (connected) names[id] = name ?? "";
                else if (!names.TryGetValue(id, out name)) name = null;
                else names.Remove(id);
            }
            if (!ready || string.IsNullOrWhiteSpace(name)) return; // Devices already there at startup are not news
            string shown = name.Length > 40 ? name.Substring(0, 39) + "…" : name;
            dispatcher.BeginInvoke(new Action(() => Changed?.Invoke(kind, shown, connected)));
        }

        void StopDevices()
        {
            foreach (DeviceWatcher watcher in new[] { bluetooth, usb })
            {
                try
                {
                    if (watcher != null && (watcher.Status == DeviceWatcherStatus.Started || watcher.Status == DeviceWatcherStatus.EnumerationCompleted)) watcher.Stop();
                }
                catch (Exception)
                {
                }
            }
            bluetooth = usb = null;
            bluetoothReady = usbReady = false;
            lock (names) names.Clear();
        }

        // ---------- Network ----------

        void StartNetwork()
        {
            if (networkDebounce != null) return;
            online = IsOnline();
            networkDebounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(2) };
            networkDebounce.Tick += (s, e) =>
            {
                networkDebounce.Stop();
                bool now = IsOnline();
                if (online.HasValue && now != online.Value) Changed?.Invoke(Kind.Network, "", now);
                online = now;
            };
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        }

        void StopNetwork()
        {
            if (networkDebounce == null) return;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            networkDebounce.Stop();
            networkDebounce = null;
        }

        // The events come on another thread and in bursts; one check settles it two seconds after the last one.
        void OnNetworkChanged(object sender, EventArgs e)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (networkDebounce == null) return;
                networkDebounce.Stop();
                networkDebounce.Start();
            }));
        }

        static bool IsOnline()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces().Any(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any)));
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    // Whether Windows is in "Do not disturb" (focus assist). Windows has no way to read the Clock app's focus
    // session itself, but while one runs, or Do not disturb is on, this state is set. Only that one state is read.
    sealed class FocusService
    {
        const int QUNS_QUIET_TIME = 6;

        [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        public bool On { get; private set; }
        public event Action Changed;

        readonly DispatcherTimer timer;

        public FocusService(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => Poll(false);
        }

        public void Start()
        {
            if (timer.IsEnabled) return;
            Poll(true);
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (On) { On = false; Changed?.Invoke(); }
        }

        void Poll(bool first)
        {
            int state;
            bool now = SHQueryUserNotificationState(out state) == 0 && state == QUNS_QUIET_TIME;
            if (now == On) return;
            On = now;
            Changed?.Invoke();
        }
    }
}
