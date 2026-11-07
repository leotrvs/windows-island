using System;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowsIsland
{
    // The tray icon is the way back to the island's settings, even while the island is hidden.
    // A click opens the settings window; the right-click menu keeps only the quick actions.
    sealed class Tray : IDisposable
    {
        readonly Forms.NotifyIcon icon;
        readonly Settings settings;
        readonly Action changed;

        public Tray(Settings settings, Action changed, Action openSettings, Action checkForUpdates, Action quit)
        {
            this.settings = settings;
            this.changed = changed;

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            Forms.ToolStripMenuItem open = new Forms.ToolStripMenuItem(Strings.MenuSettings, null, (s, e) => openSettings());
            open.Font = new Drawing.Font(open.Font, Drawing.FontStyle.Bold);
            menu.Items.Add(open);
            Forms.ToolStripMenuItem empty = Toggle(Strings.MenuEmptyIsland, () => settings.EmptyIsland, v => settings.EmptyIsland = v);
            menu.Items.Add(empty);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Strings.MenuCheckUpdates, null, (s, e) => checkForUpdates());
            menu.Items.Add(Strings.MenuQuit, null, (s, e) => quit());
            menu.Opening += (s, e) => empty.Checked = settings.EmptyIsland; // It can change in the settings window

            icon = new Forms.NotifyIcon
            {
                Icon = MakeIcon(),
                Text = Installer.Name,
                ContextMenuStrip = menu,
                Visible = true,
            };
            icon.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) openSettings(); };
        }

        Forms.ToolStripMenuItem Toggle(string text, Func<bool> get, Action<bool> set)
        {
            Forms.ToolStripMenuItem item = new Forms.ToolStripMenuItem(text) { CheckOnClick = true, Checked = get() };
            item.CheckedChanged += (s, e) =>
            {
                if (item.Checked == get()) return; // Only a refresh from the settings window
                set(item.Checked);
                settings.Save();
                changed();
            };
            return item;
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
