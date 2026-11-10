using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Threading;

namespace WindowsIsland
{
    // Keeps the reminders (ReminderStore, a small file on this PC) and tells when one is due. A timer looks every 15 seconds;
    // reminders that came due while the app was closed fire when it opens, marked late. Nothing is ever sent anywhere.
    sealed class ReminderService : IDisposable
    {
        const double LateSeconds = 90; // Later than the timer's own delay: the app was closed or the PC asleep

        readonly ReminderStore store;
        readonly DispatcherTimer timer;
        bool started;

        public event Action Changed;
        public event Action<string, bool> Due; // The text, and whether it is late

        public ReminderService(Dispatcher dispatcher, string path = null)
        {
            store = new ReminderStore(path ?? Path.Combine(Settings.Folder, "reminders.json"));
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(15) };
            timer.Tick += (s, e) => Check();
        }

        public IReadOnlyList<ReminderStore.Item> Items { get { return store.Items; } }

        public void Start()
        {
            if (started) return;
            started = true;
            store.Load();
            Check();
            timer.Start();
        }

        public void Stop()
        {
            started = false;
            timer.Stop();
        }

        public bool Add(string text, DateTime whenLocal)
        {
            if (store.Add(text, whenLocal.ToUniversalTime()) == null) return false;
            Changed?.Invoke();
            return true;
        }

        public void Remove(string id)
        {
            if (store.Remove(id)) Changed?.Invoke();
        }

        void Check()
        {
            DateTime now = DateTime.UtcNow;
            List<ReminderStore.Item> due = store.TakeDue(now);
            if (due.Count == 0) return;
            Changed?.Invoke();
            foreach (ReminderStore.Item item in due)
                Due?.Invoke(item.Text, (now - item.DueUtc).TotalSeconds > LateSeconds);
        }

        public void Dispose() { Stop(); }
    }
}
