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
        public Action<string, string> Status; // The update status on the island: a kind (checking, downloading, found, uptodate, failed) and a text
        public Action<string, Action> Ask; // A question on the island: the version, and what to do when the user says yes
        readonly DispatcherTimer settle;
        UpdateInfo found;
        bool installing;                                      // A download is under way: never two at once (they would write the same file)
        Task<Tuple<UpdateResult, UpdateInfo>> inflight;       // The look for an update that is under way: whoever asks meanwhile joins it

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

        // One look at a time: the tray menu, the Settings window and the hourly check that ask while it runs all wait for the same answer
        async Task<Tuple<UpdateResult, UpdateInfo>> SharedCheckAsync()
        {
            if (inflight == null) inflight = Updater.CheckAsync();
            Task<Tuple<UpdateResult, UpdateInfo>> task = inflight;
            try { return await task; }
            finally { if (inflight == task) inflight = null; }
        }

        // manual = the user asked: a found update waits for a click. Otherwise it installs by itself.
        public async Task CheckAsync(bool manual)
        {
            if (installing) return;
            if (State == Phase.Available && manual) return;
            if (!Updater.CanUpdate)
            {
                if (manual) say(Strings.UpdatesNeedInstall);
                return;
            }
            if (manual) Set(Phase.Checking);

            Tuple<UpdateResult, UpdateInfo> check = await SharedCheckAsync();
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
                await InstallAsync();
                return;
            }
            if (manual) Set(check.Item1 == UpdateResult.UpToDate ? Phase.UpToDate : Phase.Failed);
        }

        void Tell(string kind, string text)
        {
            if (Status != null) Status(kind, text); else say(text);
        }

        // From the tray menu: look in the background and, when there is a new version, ask on the island before downloading.
        // The Settings window shows the same state (looking, then the Download button).
        public async Task CheckFromMenuAsync()
        {
            if (installing) return;
            if (!Updater.CanUpdate) { say(Strings.UpdatesNeedInstall); return; }
            if (State == Phase.Available && found != null) { Ask?.Invoke(Version, () => { _ = InstallAsync(true); }); return; }

            Set(Phase.Checking);
            Tell("checking", Strings.UpdateChecking);
            Tuple<UpdateResult, UpdateInfo> check = await SharedCheckAsync();
            if (check.Item1 == UpdateResult.Available)
            {
                found = check.Item2;
                Version = check.Item2.Version;
                Set(Phase.Available);
                if (Ask != null) Ask(Version, () => { _ = InstallAsync(true); });
                else await InstallAsync(true);
            }
            else if (check.Item1 == UpdateResult.UpToDate) { Set(Phase.UpToDate); Tell("uptodate", Strings.UpToDate); }
            else { Set(Phase.Failed); Tell("failed", Strings.UpdateFailed); }
        }

        // Downloads the update that was found, checks it, and restarts into it.
        // announce = someone asked for it (the island or the Settings window): the island says so, and the restart does not wait for the mouse to leave.
        public async Task InstallAsync(bool announce = false)
        {
            if (installing || found == null) return;
            installing = true;
            try
            {
                Set(Phase.Downloading);
                if (announce) Tell("downloading", Strings.UpdateDownloading);
                if (await Updater.DownloadVerifiedAsync(found))
                {
                    if (!announce) while (islandBusy()) await Task.Delay(2000);
                    if (Updater.ApplyAndRestart())
                    {
                        quit();
                        return;
                    }
                }
                Set(Phase.Failed);
                if (announce) Tell("failed", Strings.UpdateFailed);
            }
            finally
            {
                installing = false;
            }
        }
    }
}
