using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AuroraClock.Models;

namespace AuroraClock.Services
{
    /// <summary>Central hub: owns the configuration, the live clock list and the widget windows.</summary>
    public sealed class AppController
    {
        public static AppController Instance { get; } = new();

        private readonly Dictionary<string, ClockWidget> _widgets = new();
        private readonly Dictionary<string, NoteWindow> _noteWindows = new();
        private TrayIcon? _tray;
        private HotkeyManager? _hotkeys;
        private SettingsWindow? _settings;
        private DashboardWindow? _dashboard;
        private UsageWidget? _usageWidget;
        private DockBoxWindow? _dock;
        private ReminderService? _reminders;
        private bool _shuttingDown;

        public AppConfig Config { get; private set; } = new();

        public ObservableCollection<ClockItem> Clocks { get; } = new();

        public IEnumerable<ClockWidget> Widgets => _widgets.Values;

        public static void RunOnUi(Action action)
        {
            var app = Application.Current;
            if (app == null) { action(); return; }
            if (app.Dispatcher.CheckAccess()) action();
            else app.Dispatcher.Invoke(action);
        }

        // ------------------------------------------------------------------ boot
        public void Initialize()
        {
            Config = ConfigService.Load();

            // the palette must be in place before the first window is built
            ThemeService.Use(Config.Theme);
            ThemeService.Apply();

            Clocks.Clear();
            foreach (var c in Config.Clocks) Clocks.Add(c);

            // "--hidden" starts tucked away in the tray (useful for a manual autostart entry).
            // "--settings" opens the settings window straight away.
            bool wantSettings = false;
            bool wantCenter = false;
            bool wantPopup = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "--hidden", StringComparison.OrdinalIgnoreCase))
                    Config.Hidden = true;
                else if (string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase))
                    wantSettings = true;
                else if (string.Equals(arg, "--center", StringComparison.OrdinalIgnoreCase))
                    wantCenter = true;
                else if (string.Equals(arg, "--testpopup", StringComparison.OrdinalIgnoreCase))
                    wantPopup = true;
            }

            // keep registry in sync with the saved preference
            if (Config.StartWithWindows != AutostartService.IsEnabled())
                AutostartService.Apply(Config.StartWithWindows);

            // hotkeys first, so the tray menu can show the ones that actually bound
            _hotkeys = new HotkeyManager(OnHotkey);
            _hotkeys.Start();
            LabelToggleVisible = Bind(HotkeyManager.IdToggleVisible, "D", "H", "V");
            LabelClickThrough = Bind(HotkeyManager.IdClickThrough, "P", "O", "U", "K");
            LabelAlwaysOnTop = Bind(HotkeyManager.IdAlwaysOnTop, "T", "Y", "G");
            LabelSettings = Bind(HotkeyManager.IdSettings, "S", "I", "W");
            LabelDock = Bind(HotkeyManager.IdDock, "B", "J", "N");

            _tray = new TrayIcon(this);
            _tray.Show();

            foreach (var item in Clocks) OpenWidget(item);
            if (Config.ClickThrough) ApplyClickThrough();

            // reminders / notes / usage
            _reminders = new ReminderService(this);
            _reminders.Due += r => ShowReminderPopup(r);
            _reminders.Start();

            foreach (var n in Config.Notes) ShowNoteWindow(n);
            SyncUsageWidget();
            SyncDock();

            // Write the config on first run so it is discoverable/editable straight away.
            Save();

            if (wantSettings) ShowSettings();
            if (wantCenter) ShowDashboard();
            if (wantPopup) ShowReminderPopup(new Models.Reminder
            {
                Text = "喝水！站起来活动一下",
                Time = DateTime.Now.ToString("HH:mm"),
                Repeat = Models.RepeatMode.Daily
            }, sticky: true);
        }

        // Labels reflect the key that actually got registered (some may be taken by other apps).
        public string LabelToggleVisible { get; private set; } = "Ctrl+Alt+D";
        public string LabelClickThrough { get; private set; } = "Ctrl+Alt+P";
        public string LabelAlwaysOnTop { get; private set; } = "Ctrl+Alt+T";
        public string LabelSettings { get; private set; } = "Ctrl+Alt+S";
        public string LabelDock { get; private set; } = "Ctrl+Alt+B";

        private string Bind(int id, params string[] candidates)
        {
            if (_hotkeys == null) return "(未绑定)";
            foreach (var key in candidates)
            {
                uint vk = key.ToUpperInvariant()[0];
                if (_hotkeys.Register(id, HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT, vk))
                    return "Ctrl+Alt+" + key;
            }
            return "(未绑定)";
        }

        private void OnHotkey(int id)
        {
            switch (id)
            {
                case HotkeyManager.IdClickThrough: ToggleClickThrough(); break;
                case HotkeyManager.IdToggleVisible: ToggleVisible(); break;
                case HotkeyManager.IdSettings: ShowSettings(); break;
                case HotkeyManager.IdAlwaysOnTop: ToggleAlwaysOnTop(); break;
                case HotkeyManager.IdDock: ToggleDock(); break;
            }
        }

        public void ToggleClickThrough()
        {
            Config.ClickThrough = !Config.ClickThrough;
            ApplyClickThrough();
            Save();
            _tray?.Sync();
            _settings?.Refresh();
            if (Config.ClickThrough)
                _tray?.Notify("指针穿透已开启 Click-through ON", $"鼠标点击会穿过时钟。按 {LabelClickThrough} 或右键托盘图标可关闭。");
        }

        public void ApplyClickThrough()
        {
            foreach (var w in _widgets.Values) w.ApplyClickThrough();
        }

        public void ToggleAlwaysOnTop()
        {
            Config.AlwaysOnTop = !Config.AlwaysOnTop;
            ApplyGlobalSettings();
            _tray?.Sync();
            _settings?.Refresh();
        }

        public void ToggleVisible()
        {
            if (Config.Hidden) ShowAll();
            else HideAll();
        }

        public void HideAll()
        {
            Config.Hidden = true;
            foreach (var w in _widgets.Values) w.Hide();
            _dock?.Hide();
            Save();
            _tray?.Sync();
            _settings?.Refresh();
        }

        /// <summary>
        /// The floating dials are optional: the dock already shows every clock. Turning them on
        /// opens one window per city, turning them off closes them again.
        /// </summary>
        public void ToggleFloatingClocks()
        {
            Config.ShowFloatingClocks = !Config.ShowFloatingClocks;

            if (Config.ShowFloatingClocks)
            {
                foreach (var item in Clocks) OpenWidget(item);
            }
            else
            {
                foreach (var w in _widgets.Values.ToList()) w.Close();
                _widgets.Clear();
            }

            Save();
            _tray?.Sync();
            _settings?.Refresh();
        }

        public void Shutdown()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;
            Save();
            _reminders?.Dispose();
            _reminders = null;
            _hotkeys?.Dispose();
            _hotkeys = null;
            _tray?.Dispose();
            _tray = null;
        }

        // -------------------------------------------------- reminders / notes
        private void ShowReminderPopup(Models.Reminder r, bool sticky = false)
        {
            try
            {
                var popup = new ReminderPopup(r, sticky);
                popup.Show();
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
            _dashboard?.RefreshAll();
        }

        public void ShowDashboard()
        {
            if (_dashboard == null)
            {
                _dashboard = new DashboardWindow(this);
                _dashboard.Closed += (_, _) => _dashboard = null;
            }
            _dashboard.RefreshAll();
            _dashboard.Show();
            _dashboard.Activate();
            _dashboard.Topmost = Config.AlwaysOnTop;
        }

        public void ShowNoteWindow(Models.NoteItem note)
        {
            if (_noteWindows.TryGetValue(note.Id, out var existing))
            {
                existing.Show();
                existing.Activate();
                return;
            }
            var w = new NoteWindow(note, this);
            _noteWindows[note.Id] = w;
            w.Closed += (_, _) => _noteWindows.Remove(note.Id);
            w.Show();
        }

        public void HideNoteWindow(Models.NoteItem note)
        {
            if (_noteWindows.TryGetValue(note.Id, out var w)) w.Close();
        }

        /// <summary>Called when a note's text is edited in its desktop window.</summary>
        public void NotesChanged()
        {
            Save();
            _dashboard?.RefreshAll();
        }

        public void SyncUsageWidget()
        {
            bool wanted = Config.ShowUsageWidget && CcSwitchUsage.IsRunning();
            if (wanted && _usageWidget == null)
            {
                _usageWidget = new UsageWidget(this);
                _usageWidget.Closed += (_, _) => _usageWidget = null;
                _usageWidget.Show();
            }
            else if (_usageWidget != null)
            {
                if (!Config.ShowUsageWidget) _usageWidget.Hide();
                else _usageWidget.ApplyPosition();
            }
        }

        // ------------------------------------------------------------------ dock
        /// <summary>The right-hand global box. Created lazily and kept alive while enabled.</summary>
        public void SyncDock()
        {
            if (Config.DockEnabled)
            {
                if (_dock == null)
                {
                    _dock = new DockBoxWindow(this);
                    _dock.Closed += (_, _) => _dock = null;
                    _dock.Show();
                }
                else
                {
                    _dock.Show();
                }
                _dock.Topmost = Config.AlwaysOnTop;
                return;
            }

            _dock?.Close();
            _dock = null;
        }

        public void ToggleDock()
        {
            Config.DockEnabled = !Config.DockEnabled;
            SyncDock();
            Save();
            _tray?.Sync();
            _settings?.Refresh();
        }

        public DockBoxWindow? Dock => _dock;

        /// <summary>Refreshes the tray check marks (used by the settings window).</summary>
        public void SyncTray() => _tray?.Sync();

        /// <summary>Opens the launcher editor for a brand new tile.</summary>
        public void AddLauncherInteractive()
        {
            var item = new Models.LauncherItem { Kind = Models.LauncherKind.App };
            var dlg = new LauncherEditWindow(item) { Topmost = Config.AlwaysOnTop };
            if (_dock != null && _dock.IsVisible) dlg.Owner = _dock;

            if (dlg.ShowDialog() != true) return;

            Config.Launchers ??= new List<Models.LauncherItem>();
            Config.Launchers.Add(item);
            Save();
            _dock?.Reload();
        }

        /// <summary>Re-applies the palette after the user edited it in Settings.</summary>
        public void ApplyTheme()
        {
            ThemeService.Use(Config.Theme);
            ThemeService.Apply();

            foreach (var w in _widgets.Values) w.ApplyConfig();

            if (_dock != null)
            {
                _dock.ApplyLook();
                _dock.Reload();
            }

            _usageWidget?.ApplyLook();
            Save();
        }

        public void Quit()        {
            Save();
            _shuttingDown = true;
            Application.Current?.Shutdown();
        }

        public void Save()
        {
            Config.Clocks = Clocks.ToList();
            ConfigService.Save(Config);
        }

        // -------------------------------------------------------------- widgets
        public void OpenWidget(ClockItem item)
        {
            // with the dock box in place the floating dials are opt-in
            if (!Config.ShowFloatingClocks) return;

            if (_widgets.TryGetValue(item.Id, out var existing))
            {
                existing.Show();
                existing.Activate();
                return;
            }

            var w = new ClockWidget(item, this);
            _widgets[item.Id] = w;
            w.Closed += (_, _) => _widgets.Remove(item.Id);
            if (!Config.Hidden)
            {
                w.Show();
                w.AnimateIn();
            }
        }

        public void CloseWidget(ClockItem item)
        {
            if (_widgets.TryGetValue(item.Id, out var w)) w.Close();
        }

        public ClockWidget? Widget(string id)
            => _widgets.TryGetValue(id, out var w) ? w : null;

        public void ApplyGlobalSettings()
        {
            foreach (var w in _widgets.Values) w.ApplyConfig();
            Save();
        }

        // ---------------------------------------------------------------- clocks
        public ClockItem AddClock(CityEntry entry, WidgetShape? shape = null, bool offset = true)
        {
            var id = string.IsNullOrEmpty(entry.Id) ? TimeZoneInfo.Local.Id : entry.Id;
            var item = new ClockItem
            {
                City = entry.Name,
                TimeZoneId = id,
                Shape = shape ?? Config.DefaultShape,
                Size = Config.DefaultSize,
                ShowSeconds = Config.ShowSeconds
            };

            if (offset)
            {
                var pt = NextFreePosition(item.Size);
                item.X = pt.X;
                item.Y = pt.Y;
            }

            Clocks.Add(item);
            Save();
            OpenWidget(item);
            _tray?.Sync();

            if (_settings != null) _settings.Refresh();
            return item;
        }

        public void RemoveClock(ClockItem item)
        {
            CloseWidget(item);
            Clocks.Remove(item);
            Save();
            _tray?.Sync();
            if (_settings != null) _settings.Refresh();
        }

        public void ShowAll()
        {
            Config.Hidden = false;
            foreach (var item in Clocks) if (!_widgets.ContainsKey(item.Id)) OpenWidget(item);
            foreach (var w in _widgets.Values) { w.Show(); }
            SyncDock();
            Save();
            _tray?.Sync();
            _settings?.Refresh();
        }

        public void TileAll()
        {
            var wa = SystemParameters.WorkArea;
            double x = wa.Left + 28, y = wa.Top + 28, rowH = 0;
            foreach (var item in Clocks)
            {
                if (!_widgets.TryGetValue(item.Id, out var w)) continue;
                if (x + item.Size > wa.Right - 20 && x > wa.Left + 28)
                {
                    x = wa.Left + 28;
                    y += rowH + 18;
                    rowH = 0;
                }
                w.SetPosition(x, y, persist: false);
                x += item.Size + 18;
                rowH = Math.Max(rowH, item.Size);
            }
            Save();
        }

        private Point NextFreePosition(double size)
        {
            var wa = SystemParameters.WorkArea;
            double step = 26, x0 = wa.Right - size - 36, y0 = wa.Top + 80;
            for (int i = 0; i < 24; i++)
            {
                double x = x0 - i * step;
                double y = y0 + i * step;
                bool clash = _widgets.Values.Any(w =>
                    Math.Abs(w.Left - x) < size * 0.6 && Math.Abs(w.Top - y) < size * 0.6);
                if (!clash && x > wa.Left && y + size < wa.Bottom) return new Point(x, y);
            }
            return new Point(Math.Max(wa.Left + 20, x0), y0);
        }

        // -------------------------------------------------------------- settings
        public void ShowSettings()
        {
            if (_settings == null)
            {
                _settings = new SettingsWindow(this);
                _settings.Closed += (_, _) => _settings = null;
            }
            _settings.Refresh();
            _settings.Show();
            if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
            _settings.Topmost = Config.AlwaysOnTop;
            _settings.Activate();
        }

        public void ToggleAutoStart()
        {
            Config.StartWithWindows = !Config.StartWithWindows;
            AutostartService.Apply(Config.StartWithWindows);
            ApplyGlobalSettings();
        }
    }
}
