using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsIsland
{
    // The permission card: Claude asks to run a tool, the user allows or denies.
    public partial class IslandWindow
    {
        static readonly Brush SupabaseTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0x3E, 0xCF, 0x8E)));
        static readonly Brush AmberTint = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x9F, 0x0A)));
        Border permissionPressed;
        DateTime permissionArmedAt;
        DispatcherTimer permissionArmTimer;
        double permissionHeight = 138;

        void AnswerPermission(Border button, string decision, MouseButtonEventArgs e)
        {
            e.Handled = true;
            bool pressedHere = permissionPressed == button;
            permissionPressed = null;
            button.ReleaseMouseCapture();
            if (!pressedHere || permission == null || DateTime.Now < permissionArmedAt) return;
            Point at = e.GetPosition(button);
            if (at.X < 0 || at.Y < 0 || at.X > button.ActualWidth || at.Y > button.ActualHeight) return; // Released somewhere else
            permission.Resolve(decision);
        }

        ApiPermission permission;
        readonly Queue<ApiPermission> permissionQueue = new Queue<ApiPermission>();

        internal void ShowPermission(ApiPermission request)
        {
            // Not shown at all (off, or hidden for a full-screen window): no decision, so Claude asks as usual
            if (!settings.PermissionPrompts || Presenting || (fullscreen && settings.HideInFullscreen)) { request.Resolve("none"); return; }
            request.Closed += () => OnPermissionClosed(request);
            permissionQueue.Enqueue(request);
            if (permission == null) NextPermission();
            else PermQueue.Text = PendingCount() > 0 ? "+" + PendingCount() : "";
        }

        int PendingCount()
        {
            int count = 0;
            foreach (ApiPermission p in permissionQueue) if (!p.IsClosed) count++;
            return count;
        }

        // The island is going away: whatever waits for an answer gets none.
        void CancelPermissions()
        {
            foreach (ApiPermission p in permissionQueue.ToArray()) p.Cancel();
            permissionQueue.Clear();
            if (permission != null) permission.Cancel();
        }

        void NextPermission()
        {
            permission = null;
            while (permissionQueue.Count > 0 && permission == null)
            {
                ApiPermission next = permissionQueue.Dequeue();
                if (!next.IsClosed) permission = next;
            }
            if (permission != null)
            {
                PermTitle.Text = Strings.PermissionAsks;
                PermTool.Text = ToolName(permission.Tool) + (string.IsNullOrEmpty(permission.Project) ? "" : " \u00B7 " + permission.Project);
                bool supabase = permission.Tool != null && permission.Tool.StartsWith("mcp__", StringComparison.Ordinal) &&
                                permission.Tool.IndexOf("supabase", StringComparison.OrdinalIgnoreCase) >= 0;
                PermSupabase.Visibility = supabase ? Visibility.Visible : Visibility.Collapsed;
                PermLock.Visibility = supabase ? Visibility.Collapsed : Visibility.Visible;
                PermIconBack.Background = supabase ? SupabaseTint : AmberTint;
                PermSummary.Text = permission.Summary;
                PermQueue.Text = PendingCount() > 0 ? "+" + PendingCount() : "";
                // A summary that is cut, or is only the raw arguments, is not enough to approve on: Deny stays, Allow is replaced by a link to Claude
                PermAllow.Visibility = permission.Partial ? Visibility.Collapsed : Visibility.Visible;
                PermOpen.Visibility = permission.Partial ? Visibility.Visible : Visibility.Collapsed;
                PermOpenText.Text = Strings.OpenClaude;
                int lines = Math.Clamp((permission.Summary.Length + 51) / 52, 2, 6);
                bool scrolls = permission.Summary.Length > 6 * 52;
                PermCheck.Text = permission.Partial ? Strings.PermissionPartial : scrolls ? Strings.PermissionScroll : Strings.PermissionCheck;
                permissionHeight = 138 + (lines - 2) * 15;
                PermissionPanel.Height = permissionHeight;
                PermScroll.MaxHeight = lines * 15 + 4;
                PermScroll.ScrollToTop();
                // The buttons wake up a moment after the prompt shows
                permissionArmedAt = DateTime.Now.AddMilliseconds(650);
                PermAllow.Opacity = PermDeny.Opacity = 0.45;
                if (permissionArmTimer == null)
                {
                    permissionArmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(660) };
                    permissionArmTimer.Tick += (s, e) => { permissionArmTimer.Stop(); PermAllow.Opacity = PermDeny.Opacity = 1; };
                }
                permissionArmTimer.Stop();
                permissionArmTimer.Start();
                PermAllowText.Text = Strings.Allow;
                PermDenyText.Text = Strings.Deny;
                tucked = false;
                lastActive = DateTime.Now;
            }
            Recompute();
        }

        // Claude's own link to the chat that needs you (the app registers claude:// and goes straight to it); when it cannot be
        // opened, the app is only brought to the front
        void OpenClaudeNeedsInput()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("claude://code/needs-input?source=desktop_action") { UseShellExecute = true })?.Dispose();
                return;
            }
            catch (Exception)
            {
                // Not registered: the old way
            }
            ClaudeWindow.Focus();
        }

        void OnPermissionClosed(ApiPermission request)
        {
            if (permission == request) NextPermission();
        }

        // "mcp__supabase__execute_sql" -> "supabase · execute_sql"
        static string ToolName(string tool)
        {
            if (tool != null && tool.StartsWith("mcp__", StringComparison.Ordinal))
            {
                string[] parts = tool.Substring(5).Split(new[] { "__" }, 2, StringSplitOptions.None);
                return parts.Length == 2 ? parts[0] + " \u00B7 " + parts[1] : parts[0];
            }
            return tool ?? "";
        }
    }
}
