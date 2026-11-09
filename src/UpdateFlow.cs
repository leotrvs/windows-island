using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WindowsIsland
{
    // What the user sees of updating: checking, "download" when one is found, downloading, up to date.
    // The trust checks (HTTPS, signature, hash, version) all stay in Updater; this only sequences them.
    sealed class UpdateFlow
    {
        public enum Phase { Idle, Checking, Available, Downloading, UpToDate, Failed }

        public Phase State { get; private set; }
        public string Version { get; private set; }
        public event Action Changed;

        readonly Dispatcher dispatcher;
        readonly Func<bool> islandBusy; // Never restart under the user's mouse
        readonly Action quit;
        readonly Action<string> say;    // A message on the island
        readonly DispatcherTimer settle;
        UpdateInfo found;
        bool working;

        public UpdateFlow(Dispatcher dispatcher, Func<bool> islandBusy, Action quit, Action<string> say)
        {
            this.dispatcher = dispatcher;
            this.islandBusy = islandBusy;
            this.quit = quit;
            this.say = say;
            settle = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(4) };
            settle.Tick += (s, e) => { settle.Stop(); if (State == Phase.UpToDate || State == Phase.Failed) Set(Phase.Idle); };
        }

        void Set(Phase phase)
        {
            State = phase;
            Changed?.Invoke();
            settle.Stop();
            if (phase == Phase.UpToDate || phase == Phase.Failed) settle.Start();
        }

        // manual = the user asked: a found update waits for a click. Otherwise it installs by itself.
        public async Task CheckAsync(bool manual)
        {
            if (working) return;
            if (State == Phase.Available && manual) return;
            working = true;
            try
            {
                if (!Updater.CanUpdate)
                {
                    if (manual) say(Strings.UpdatesNeedInstall);
                    return;
                }
                if (manual) Set(Phase.Checking);

                Tuple<UpdateResult, UpdateInfo> check = await Updater.CheckAsync();
                if (check.Item1 == UpdateResult.Available)
                {
                    found = check.Item2;
                    Version = check.Item2.Version;
                    if (manual)
                    {
                        Set(Phase.Available);
                        return;
                    }
                    say(Strings.UpdateFoundAuto(Version)); // The island says so, then it downloads
                    working = false;
                    await InstallAsync();
                    return;
                }
                if (manual) Set(check.Item1 == UpdateResult.UpToDate ? Phase.UpToDate : Phase.Failed);
            }
            finally
            {
                working = false;
            }
        }

        // Downloads the update that was found, checks it, and restarts into it.
        public async Task InstallAsync()
        {
            if (working || found == null) return;
            working = true;
            try
            {
                Set(Phase.Downloading);
                if (await Updater.DownloadVerifiedAsync(found))
                {
                    while (islandBusy()) await Task.Delay(2000);
                    if (Updater.ApplyAndRestart())
                    {
                        quit();
                        return;
                    }
                }
                Set(Phase.Failed);
            }
            finally
            {
                working = false;
            }
        }
    }
}
