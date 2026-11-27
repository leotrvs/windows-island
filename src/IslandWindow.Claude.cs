using System;
using System.Windows;
using System.Windows.Input;

namespace WindowsIsland
{
    // Claude and Codex: the compact pill, the expanded page with the limit rings and the chats.
    public partial class IslandWindow
    {
        void OnClaudeChanged() { UpdateClaude(); Recompute(); }

        void OnCodexChanged()
        {
            UpdateCodex();
            ApplyTargets(); // The Claude panel grows or shrinks with the Codex section
            Recompute();
        }

        void OnCodexFinished()
        {
            if (!settings.ShowCodex || !settings.CodexBanners) return;
            Enqueue(new BannerItem { FromCodex = true, Title = Strings.CodexDone, Subtitle = "Codex", Seconds = settings.AlertSeconds });
        }

        bool CodexActive
        {
            get { return settings.ShowCodex && codex.Working; }
        }

        // The Codex part of the expanded panel shows only while Codex is working (and turned on).
        bool CodexSectionVisible
        {
            get { return settings.ShowCodex && codex.Working; }
        }

        double ClaudePanelHeight
        {
            get { return (CodexSectionVisible ? 248 : 156) + (forecastLine ? 22 : 0) + (dayLine ? 20 : 0); }
        }

        bool forecastLine, dayLine; // The two small lines under the rings, when there is something to say

        // The chat line under the mouse: a click brings that chat's window forward
        ClaudeService.ActiveSession ChatAt(MouseEventArgs e, out double top, out double height)
        {
            top = height = 0;
            int count = claude.Active.Count;
            if (count == 0 || ClaudeStatus.ActualHeight <= 0) return null;
            int lines = count > 1 ? Math.Min(3, count) + (count > 3 ? 1 : 0) : 1;
            double each = ClaudeStatus.ActualHeight / lines;
            int index = (int)(e.GetPosition(ClaudeStatus).Y / each);
            int chats = count > 1 ? Math.Min(3, count) : 1;
            if (index < 0 || index >= chats) return null;
            top = index * each;
            height = each;
            return claude.Active[index];
        }

        void WireChatLines()
        {
            ClaudeStatusHost.MouseMove += (s, e) =>
            {
                double top, height;
                ClaudeService.ActiveSession chat = ChatAt(e, out top, out height);
                ClaudeStatusHost.Cursor = chat != null ? Cursors.Hand : null;
                ChatHover.Visibility = chat != null ? Visibility.Visible : Visibility.Collapsed;
                if (chat != null) { ChatHover.Height = height; ChatHover.Margin = new Thickness(-4, top, -4, 0); }
            };
            ClaudeStatusHost.MouseLeave += (s, e) => ChatHover.Visibility = Visibility.Collapsed;
            ClaudeStatusHost.MouseLeftButtonDown += (s, e) => e.Handled = true;
            ClaudeStatusHost.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                double top, height;
                ClaudeService.ActiveSession chat = ChatAt(e, out top, out height);
                if (chat != null) ClaudeWindow.FocusSession(chat.Pid, chat.Desktop);
            };
        }

        void UpdateCodex()
        {
            bool visible = CodexSectionVisible;
            CodexSection.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ClaudeExpanded.Height = ClaudePanelHeight;
            if (!visible) return;

            UpdateCodexLive();
            UsageWindow session = codex.Session;
            UsageWindow weekly = codex.Weekly;
            int sessionPercent = session?.EffectivePercent ?? 0;
            int weeklyPercent = weekly?.EffectivePercent ?? 0;
            CodexSessionPercent.Text = session?.Percent != null ? sessionPercent + "%" : "–";
            CodexWeeklyPercent.Text = weekly?.Percent != null ? weeklyPercent + "%" : "–";
            CodexSessionArc.Data = Arc(sessionPercent, 36, 4);
            CodexWeeklyArc.Data = Arc(weeklyPercent, 36, 4);
            CodexSessionArc.Stroke = RingBrush(sessionPercent);
            CodexWeeklyArc.Stroke = RingBrush(weeklyPercent);
        }

        // The parts that change every second while Codex works.
        void UpdateCodexLive()
        {
            string elapsed = codex.WorkingSince.HasValue ? FormatTime(SampleClock.Now - codex.WorkingSince.Value) : "";
            if (CodexCompactText.Text != elapsed) CodexCompactText.Text = elapsed;
            string status = codex.Working ? (Strings.Working + " " + elapsed).Trim()
                          : codex.Session?.Percent != null ? Strings.CodexIdle
                          : Strings.CodexNoData;
            if (CodexStatus.Text != status) CodexStatus.Text = status;
        }

        // The Claude app said it needs you (its own Windows notice) and the island had not noticed by itself: it counts as waiting for a while
        DateTime toastWaitingUntil = DateTime.MinValue;
        bool ToastWaiting { get { return DateTime.Now < toastWaitingUntil; } }
        bool ClaudeWaitingNow { get { return claude.Waiting > 0 || ToastWaiting; } }

        static bool AsksForYou(IslandNotification n)
        {
            string text = ((n.Title ?? "") + " " + (n.Body ?? "")).ToLowerInvariant();
            foreach (string word in new[] { "contribui", "precisa", "permiss", "aprova", "pergunt", "aguard", "esperando", "input", "needs", "permission", "approve", "question", "waiting", "your answer" })
                if (text.Contains(word)) return true;
            return false;
        }

        bool ClaudeActive
        {
            get { return settings.ShowClaude && (claude.Busy > 0 || claude.Waiting > 0 || ToastWaiting); }
        }

        void UpdateClaude()
        {
            UpdateClaudeLive();

            UsageWindow session = claude.Session;
            bool hasUsage = session != null && session.Percent.HasValue;

            // Small lines under the rings: the limit forecast (the sooner of 5 h and weekly) and the day summary
            DateTime? limitAt = claude.SessionLimitAt, weeklyAt = claude.WeeklyLimitAt;
            bool weeklyFirst = weeklyAt.HasValue && (!limitAt.HasValue || weeklyAt < limitAt);
            bool wantForecast = hasUsage && settings.ShowLimitForecast && (limitAt.HasValue || weeklyAt.HasValue);
            ForecastText.Text = !wantForecast ? "" : weeklyFirst ? Strings.ForecastWeekly(weeklyAt.Value) : Strings.ForecastFiveHour(limitAt.Value);
            bool wantDay = hasUsage && settings.ShowDaySummary && claude.Day.Sessions > 0;
            ForecastText.Visibility = wantForecast ? Visibility.Visible : Visibility.Collapsed;
            DaySummaryText.Visibility = wantDay ? Visibility.Visible : Visibility.Collapsed;
            if (wantDay) DaySummaryText.Text = Strings.DaySummary(claude.Day.Sessions, claude.Day.Worked);
            if (wantForecast != forecastLine || wantDay != dayLine)
            {
                forecastLine = wantForecast;
                dayLine = wantDay;
                ClaudeExpanded.Height = ClaudePanelHeight;
                if (IsLoaded) ApplyTargets();
            }

            UsageRow.Visibility = hasUsage ? Visibility.Visible : Visibility.Collapsed;
            NoUsageText.Visibility = hasUsage ? Visibility.Collapsed : Visibility.Visible;
            if (!hasUsage)
            {
                NoUsageText.Text = claude.ClaudeFound ? Strings.ReadingUsage : Strings.ClaudeNotFound;
                return;
            }

            UsageWindow weekly = claude.Weekly ?? new UsageWindow();
            int sessionPercent = session.EffectivePercent;
            int weeklyPercent = weekly.EffectivePercent;
            sessionArc.Target = sessionPercent;
            weeklyArc.Target = weeklyPercent;
            SessionPercent.Text = sessionPercent + "%";
            WeeklyPercent.Text = weekly.Percent.HasValue ? weeklyPercent + "%" : "–";
            SessionArc.Stroke = RingBrush(sessionPercent);
            WeeklyArc.Stroke = RingBrush(weeklyPercent);
            SessionReset.Text = Strings.Resets(session.EffectiveReset);
            WeeklyReset.Text = Strings.Resets(weekly.EffectiveReset);
            StartLoop();
        }

        // The parts that change every second while Claude works.
        void UpdateClaudeLive()
        {
            string elapsed = claude.BusySince.HasValue ? FormatTime(SampleClock.Now - claude.BusySince.Value) : "";
            bool waiting = claude.Waiting > 0;

            // With several chats working, a count comes before the time so it is clear it is more than one
            int many = claude.Active.Count;
            // "1/2 · Waiting" reads as one of two chats waiting; "2 · 0:58" as two chats working
            string compact = waiting ? (many > 1 ? claude.Waiting + "/" + many + " · " : "") + Strings.Waiting
                                     : (many > 1 ? many + " · " : "") + elapsed;
            if (compact != lastCompactText)
            {
                lastCompactText = compact;
                ClaudeCompactText.Text = compact;
                ClaudeCompactText.Foreground = waiting ? Orange : White;
            }

            ClaudeTitle.Visibility = many > 1 ? Visibility.Collapsed : Visibility.Visible; // The lines take its place
            OpenClaudeLink.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed; // A question is answered in the Claude app
            if (waiting && OpenClaudeLink.Text.Length == 0) OpenClaudeLink.Text = Strings.OpenClaude;
            string status;
            if (many > 1)
            {
                // One line per chat (up to three): what it is doing and for how long
                System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                for (int i = 0; i < Math.Min(3, many); i++)
                {
                    ClaudeService.ActiveSession item = claude.Active[i];
                    lines.Add(item.Waiting ? Join(Strings.WaitingForYou, string.IsNullOrWhiteSpace(item.Question) ? item.Name : item.Name + ": " + item.Question) : Join(Strings.Working + " " + FormatTime(SampleClock.Now - item.Since), item.Name));
                }
                if (many > 3) lines.Add("+" + (many - 3));
                status = string.Join(Environment.NewLine, lines);
            }
            else status = waiting ? Join(Strings.WaitingForYou, claude.WaitingName)
                          : claude.Busy > 0 ? Join(Strings.Working + " " + elapsed, claude.BusyName)
                          : claude.Running > 0 ? Strings.IdleSessions(claude.Running)
                          : Strings.NoSession;
            if (status != lastStatus)
            {
                lastStatus = status;
                ClaudeStatus.Text = status;
            }
            if (dayLine)
            {
                string day = Strings.DaySummary(claude.Day.Sessions, claude.Day.Worked);
                if (day != DaySummaryText.Text) DaySummaryText.Text = day;
            }
        }
    }
}
