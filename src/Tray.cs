using System;
using System.Diagnostics;
using System.IO;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowsIsland
{
    // The tray icon is the only way back to the settings while the island is hidden.
    sealed class Tray : IDisposable
    {
        readonly Forms.NotifyIcon icon;
        readonly Settings settings;
        readonly Action changed;

        public Tray(Settings settings, Action changed, Action quit)
        {
            this.settings = settings;
            this.changed = changed;

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add(Toggle(Strings.MenuMusic, () => settings.ShowMusic, v => settings.ShowMusic = v));
            menu.Items.Add(Toggle(Strings.MenuClaude, () => settings.ShowClaude, v => settings.ShowClaude = v));
            menu.Items.Add(Toggle(Strings.MenuBanners, () => settings.ClaudeBanners, v => settings.ClaudeBanners = v));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Toggle(Strings.MenuIdlePill, () => settings.ShowIdlePill, v => settings.ShowIdlePill = v));
            menu.Items.Add(Toggle(Strings.MenuHover, () => settings.ExpandOnHover, v => settings.ExpandOnHover = v));
            menu.Items.Add(Toggle(Strings.MenuFullscreen, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v));
            menu.Items.Add(PositionMenu());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Toggle(Strings.MenuStartup, () => settings.StartWithWindows, v => settings.StartWithWindows = v));
            menu.Items.Add(Strings.MenuOpenSettings, null, (s, e) => OpenSettingsFile());
            menu.Items.Add(Strings.MenuQuit, null, (s, e) => quit());

            icon = new Forms.NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "Windows Island",
                ContextMenuStrip = menu,
                Visible = true,
            };
        }

        Forms.ToolStripMenuItem Toggle(string text, Func<bool> get, Action<bool> set)
        {
            Forms.ToolStripMenuItem item = new Forms.ToolStripMenuItem(text) { CheckOnClick = true, Checked = get() };
            item.CheckedChanged += (s, e) =>
            {
                set(item.Checked);
                settings.Save();
                changed();
            };
            return item;
        }

        Forms.ToolStripMenuItem PositionMenu()
        {
            Forms.ToolStripMenuItem parent = new Forms.ToolStripMenuItem(Strings.MenuPosition);
            Forms.ToolStripMenuItem top = new Forms.ToolStripMenuItem(Strings.MenuTop);
            Forms.ToolStripMenuItem bottom = new Forms.ToolStripMenuItem(Strings.MenuBottom);
            Action refresh = () =>
            {
                bool isBottom = string.Equals(settings.Position, "Bottom", StringComparison.OrdinalIgnoreCase);
                top.Checked = !isBottom;
                bottom.Checked = isBottom;
            };
            top.Click += (s, e) => { settings.Position = "Top"; settings.Save(); refresh(); changed(); };
            bottom.Click += (s, e) => { settings.Position = "Bottom"; settings.Save(); refresh(); changed(); };
            parent.DropDownItems.Add(top);
            parent.DropDownItems.Add(bottom);
            refresh();
            return parent;
        }

        void OpenSettingsFile()
        {
            if (!File.Exists(Settings.FilePath)) settings.Save();
            try { Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        // A small dark pill with a light outline, readable on both light and dark taskbars.
        static Drawing.Icon MakeIcon()
        {
            using (Drawing.Bitmap bitmap = new Drawing.Bitmap(32, 32))
            {
                using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
                using (Drawing.Drawing2D.GraphicsPath path = new Drawing.Drawing2D.GraphicsPath())
                using (Drawing.SolidBrush fill = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 12, 12, 14)))
                using (Drawing.Pen outline = new Drawing.Pen(Drawing.Color.FromArgb(230, 235, 235, 240), 2f))
                using (Drawing.SolidBrush dot = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 232, 135, 95)))
                {
                    g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    Drawing.RectangleF pill = new Drawing.RectangleF(2, 9, 28, 14);
                    path.AddArc(pill.X, pill.Y, pill.Height, pill.Height, 90, 180);
                    path.AddArc(pill.Right - pill.Height, pill.Y, pill.Height, pill.Height, 270, 180);
                    path.CloseFigure();
                    g.FillPath(fill, path);
                    g.DrawPath(outline, path);
                    g.FillEllipse(dot, 20, 13, 6, 6);
                }
                return Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
        }
    }
}
