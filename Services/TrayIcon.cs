using System;
using System.Drawing;
using System.Windows.Forms;

namespace AuroraClock.Services
{
    /// <summary>
    /// System tray presence with a dark, glass-matching menu. This is the control surface that
    /// stays reachable when the widgets are hidden or set to pointer click-through.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly AppController _app;
        private NotifyIcon? _notify;
        private ContextMenuStrip? _menu;
        private ToolStripMenuItem? _topItem;
        private ToolStripMenuItem? _autoItem;
        private ToolStripMenuItem? _passItem;
        private ToolStripMenuItem? _visItem;
        private ToolStripMenuItem? _usageItem;

        public TrayIcon(AppController app) => _app = app;

        public void Show()
        {
            _menu = new ContextMenuStrip
            {
                Renderer = new DarkRenderer(),
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(14, 18, 26),
                ForeColor = Color.FromArgb(240, 245, 252)
            };

            _visItem = new ToolStripMenuItem($"显示时钟  Show clocks ({_app.LabelToggleVisible})");
            _visItem.Click += (_, _) => _app.ToggleVisible();
            _menu.Items.Add(_visItem);

            _passItem = new ToolStripMenuItem($"指针穿透  Click-through ({_app.LabelClickThrough})");
            _passItem.Click += (_, _) => _app.ToggleClickThrough();
            _menu.Items.Add(_passItem);

            _topItem = new ToolStripMenuItem($"始终置顶  Always on top ({_app.LabelAlwaysOnTop})");
            _topItem.Click += (_, _) => _app.ToggleAlwaysOnTop();
            _menu.Items.Add(_topItem);

            _autoItem = new ToolStripMenuItem("开机自启  Start with Windows");
            _autoItem.Click += (_, _) =>
            {
                _app.Config.StartWithWindows = !_app.Config.StartWithWindows;
                AutostartService.Apply(_app.Config.StartWithWindows);
                _app.Save();
                Sync();
            };
            _menu.Items.Add(_autoItem);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(Item("Aurora 中心（待办/提醒/便签/用量）", (_, _) => _app.ShowDashboard()));
            var usage = new ToolStripMenuItem("显示用量组件");
            usage.Click += (_, _) =>
            {
                _app.Config.ShowUsageWidget = !_app.Config.ShowUsageWidget;
                _app.Save();
                _app.SyncUsageWidget();
                Sync();
            };
            _usageItem = usage;
            _menu.Items.Add(usage);
            _menu.Items.Add(Item("添加时钟…  Add clock", (_, _) => AddClock()));
            _menu.Items.Add(Item("排列全部时钟  Tile all", (_, _) => _app.TileAll()));
            _menu.Items.Add(Item("设置…  Settings (" + _app.LabelSettings + ")", (_, _) => _app.ShowSettings()));

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(Item("退出  Exit", (_, _) => _app.Quit()));

            _notify = new NotifyIcon
            {
                Icon = IconFactory.AppIcon(),
                Text = "Aurora World Clock",
                Visible = true,
                ContextMenuStrip = _menu
            };
            _notify.DoubleClick += (_, _) => _app.ToggleVisible();

            Sync();
        }

        public void Sync()
        {
            if (_topItem != null) _topItem.Checked = _app.Config.AlwaysOnTop;
            if (_autoItem != null) _autoItem.Checked = _app.Config.StartWithWindows;
            if (_passItem != null) _passItem.Checked = _app.Config.ClickThrough;
            if (_visItem != null) _visItem.Checked = !_app.Config.Hidden;
            if (_usageItem != null) _usageItem.Checked = _app.Config.ShowUsageWidget;
        }

        /// <summary>Balloon tip used to explain how to come back from click-through mode.</summary>
        public void Notify(string title, string text)
        {
            if (_notify == null) return;
            try
            {
                _notify.BalloonTipTitle = title;
                _notify.BalloonTipText = text;
                _notify.BalloonTipIcon = ToolTipIcon.Info;
                _notify.ShowBalloonTip(4000);
            }
            catch { /* ignored */ }
        }

        private void AddClock()
        {
            var dlg = new CityPickerWindow { Topmost = true };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
                _app.AddClock(dlg.Selected);
        }

        private static ToolStripMenuItem Item(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += onClick;
            return item;
        }

        public void Dispose()
        {
            if (_notify != null)
            {
                _notify.Visible = false;
                _notify.Dispose();
                _notify = null;
            }
            _menu?.Dispose();
            _menu = null;
        }

        private sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(238, 243, 250);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(Color.FromArgb(48, 255, 255, 255));
                var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.DrawRectangle(pen, r);
            }

            private sealed class DarkColors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => Color.FromArgb(14, 18, 26);
                public override Color MenuItemSelected => Color.FromArgb(60, 124, 196, 255);
                public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 124, 196, 255);
                public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 124, 196, 255);
                public override Color MenuItemBorder => Color.FromArgb(70, 124, 196, 255);
                public override Color ImageMarginGradientBegin => Color.FromArgb(14, 18, 26);
                public override Color ImageMarginGradientMiddle => Color.FromArgb(14, 18, 26);
                public override Color ImageMarginGradientEnd => Color.FromArgb(14, 18, 26);
                public override Color SeparatorDark => Color.FromArgb(40, 255, 255, 255);
                public override Color SeparatorLight => Color.FromArgb(40, 255, 255, 255);
            }
        }
    }
}
