using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The live score of one team, from ESPN's public scoreboard (site.api.espn.com). Only the league codes the user typed are
    // asked for; the team's name is used here to pick the match and is never sent. Off until the user turns it on.
    sealed class FootballService
    {
        public event Action<WatchedActivity> Updated;
        public event Action<string, string> Goal; // title, text

        static readonly HttpClient Http = Make();
        readonly Dispatcher dispatcher;
        readonly DispatcherTimer timer;
        readonly Dictionary<string, int> goals = new Dictionary<string, int>();
        readonly HashSet<string> finished = new HashSet<string>();
        string team = "";
        List<string> leagues = new List<string>();
        string activeId;
        int generation;

        static HttpClient Make()
        {
            HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsIsland");
            return client;
        }

        public FootballService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMinutes(3) };
            timer.Tick += (s, e) => Poll();
        }

        public void Start(string teamName, string leagueCodes)
        {
            Stop();
            team = (teamName ?? "").Trim();
            leagues = FootballFeed.Leagues(leagueCodes);
            if (team.Length < 2 || leagues.Count == 0) return;
            Poll();
            timer.Start();
        }

        public void Stop()
        {
            generation++;
            timer.Stop();
            if (activeId != null) Updated?.Invoke(new WatchedActivity { Id = activeId, Gone = true });
            activeId = null;
            goals.Clear();
            finished.Clear();
        }

        async void Poll()
        {
            int mine = generation;
            List<FootballMatch> found = new List<FootballMatch>();
            string[] codes = leagues.ToArray();
            string who = team;
            await Task.Run(async () =>
            {
                foreach (string code in codes)
                {
                    try
                    {
                        using (HttpResponseMessage reply = await Http.GetAsync("https://site.api.espn.com/apis/site/v2/sports/soccer/" + code + "/scoreboard"))
                        {
                            if (!reply.IsSuccessStatusCode) continue;
                            string json = await reply.Content.ReadAsStringAsync();
                            foreach (FootballMatch match in FootballFeed.Parse(json))
                                if (FootballFeed.Has(match, who)) found.Add(match);
                        }
                    }
                    catch (Exception)
                    {
                        // No connection or an odd answer: tried again later
                    }
                }
            });
            if (mine != generation) return; // Stopped or changed while asking

            FootballMatch live = found.Find(m => m.Live);
            FootballMatch recent = found.Find(m => m.Finished && !finished.Contains(m.Id));
            if (live != null)
            {
                int before;
                if (goals.TryGetValue(live.Id, out before) && live.Goals > before)
                    Goal?.Invoke(Strings.FootballGoal, live.Line);
                goals[live.Id] = live.Goals;
                activeId = "football";
                Updated?.Invoke(new WatchedActivity { Id = "football", App = Strings.MenuFootball, Title = live.Line, Text = live.Clock, Glyph = LocalApi.Glyph("star") });
                timer.Interval = TimeSpan.FromSeconds(30);
            }
            else
            {
                if (activeId != null)
                {
                    if (recent != null)
                    {
                        finished.Add(recent.Id);
                        Updated?.Invoke(new WatchedActivity { Id = "football", App = Strings.MenuFootball, Title = recent.Line, Text = Strings.FootballFinal, Glyph = LocalApi.Glyph("star"), Done = true });
                    }
                    else Updated?.Invoke(new WatchedActivity { Id = "football", Gone = true });
                    activeId = null;
                }
                timer.Interval = TimeSpan.FromMinutes(3);
            }
        }
    }
}
