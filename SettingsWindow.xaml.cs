using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    public partial class SettingsWindow : Window
    {
        private readonly AppController _app;
        private readonly DispatcherTimer _themeDebounce;
        private bool _loading;

        public SettingsWindow(AppController app)
        {
            _app = app;
            InitializeComponent();
            Loaded += OnLoaded;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            _themeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            _themeDebounce.Tick += (_, _) =>
            {
                _themeDebounce.Stop();
                _app.ApplyTheme();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ClockList.ItemsSource = _app.Clocks;
            VersionText.Text = $"Aurora World Clock  v{Version}   ·   .NET {Environment.Version.ToString(3)}   ·   配置目录 %AppData%\\AuroraClock";
            HotkeyText.Text =
                $"{_app.LabelToggleVisible}　显示 / 隐藏全部时钟\n" +
                $"{_app.LabelClickThrough}　指针穿透开关\n" +
                $"{_app.LabelAlwaysOnTop}　始终置顶开关\n" +
                $"{_app.LabelDock}　右侧盒子显示 / 隐藏\n" +
                $"{_app.LabelSettings}　打开本设置窗口";

            BuildPanelSwatches();
            BuildAccentSwatches();

            EdgeRightBtn.Click += (_, _) => SetEdge("Right");
            EdgeLeftBtn.Click += (_, _) => SetEdge("Left");
            EdgeTopBtn.Click += (_, _) => SetEdge("Top");
            EdgeBottomBtn.Click += (_, _) => SetEdge("Bottom");

            Refresh();
        }

        // ------------------------------------------------------------------- edge
        private void SetEdge(string edge)
        {
            _app.Config.DockEdge = edge;
            _app.Config.DockFullHeight = true;
            _app.SyncDock();
            _app.Dock?.ApplyAutoHide();
            _app.Save();
            UpdateEdgeButtons();
        }

        private void UpdateEdgeButtons()
        {
            if (EdgeRightBtn == null) return;
            string edge = (_app.Config.DockEdge ?? "Right").Trim();
            Segment(EdgeRightBtn, edge.Equals("Right", StringComparison.OrdinalIgnoreCase));
            Segment(EdgeLeftBtn, edge.Equals("Left", StringComparison.OrdinalIgnoreCase));
            Segment(EdgeTopBtn, edge.Equals("Top", StringComparison.OrdinalIgnoreCase));
            Segment(EdgeBottomBtn, edge.Equals("Bottom", StringComparison.OrdinalIgnoreCase));
        }

        private void Segment(Button button, bool on)
            => button.Style = (Style)FindResource(on ? "AccentButton" : "GhostButton");

        private void DockDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            _app.Config.DockAutoHideDelay = DockDelaySlider.Value;
            UpdateLabels();
            _app.Save();
        }

        public void Refresh()
        {
            if (!IsLoaded) return;

            var theme = _app.Config.Theme ??= new ThemePalette();
            var cfg = _app.Config;

            _loading = true;
            SwitchTop.IsChecked = cfg.AlwaysOnTop;
            SwitchPass.IsChecked = cfg.ClickThrough;
            SwitchAuto.IsChecked = cfg.StartWithWindows;
            SwitchSnap.IsChecked = cfg.SnapToEdges;
            SwitchFloating.IsChecked = cfg.ShowFloatingClocks;
            SwitchSeconds.IsChecked = cfg.ShowSeconds;
            SwitchSmooth.IsChecked = cfg.SmoothSecondHand;
            SwitchBlur.IsChecked = cfg.BackdropBlur;
            SwitchCapture.IsChecked = !cfg.ExcludeFromCapture;
            OpacitySlider.Value = Math.Round(cfg.Opacity * 100);
            TintSlider.Value = Math.Round(cfg.GlassTint * 100);

            AlphaSlider.Value = Math.Round(theme.PanelAlpha * 100);
            RadiusSlider.Value = Math.Round(theme.CornerRadius);
            SwitchCel.IsChecked = theme.CelOutline;
            SwitchShadow.IsChecked = theme.StickerShadow;

            SwitchDock.IsChecked = cfg.DockEnabled;
            SwitchDockFull.IsChecked = cfg.DockFullHeight;
            SwitchDockAuto.IsChecked = cfg.DockAutoHide;
            SwitchDockFrost.IsChecked = cfg.FrostOnDock;
            SwitchBump.IsChecked = cfg.DockBumpToReveal;
            DockWidthSlider.Value = Math.Round(cfg.DockWidth);
            DockDelaySlider.Value = Math.Round(Math.Clamp(cfg.DockAutoHideDelay, 0.5, 15) * 2) / 2.0;
            UpdateEdgeButtons();
            _loading = false;

            UpdateLabels();
            SubTitle.Text = $"{_app.Clocks.Count} 个时钟 · {cfg.Launchers?.Count ?? 0} 个启动项 · 赛璐璐高透主题";
        }

        private static string Version =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";

        private void UpdateLabels()
        {
            // These fields are null while InitializeComponent() is still parsing the XAML,
            // and setting Slider Minimum/Maximum raises ValueChanged at that point.
            if (OpacityValue == null || TintValue == null) return;
            if (AlphaValue == null || RadiusValue == null || DockWidthValue == null) return;
            if (DockDelayValue == null) return;

            OpacityValue.Text = $"{OpacitySlider.Value:0}%";
            TintValue.Text = $"{TintSlider.Value:0}%";
            AlphaValue.Text = $"{AlphaSlider.Value:0}%";
            RadiusValue.Text = $"{RadiusSlider.Value:0}";
            DockWidthValue.Text = $"{DockWidthSlider.Value:0} px";
            DockDelayValue.Text = $"{DockDelaySlider.Value:0.#} 秒";
        }

        // ---------------------------------------------------------------- swatches
        private sealed record PanelPreset(string Name, string Panel, string Edge, string Item,
            string ItemEdge, string Text, string Muted);

        private static readonly PanelPreset[] Panels =
        {
            new("雪青 Frost", "#E8F9FC", "#6ECFDA", "#F4FEFD", "#B4E8E6", "#1F4B64", "#5E93A8"),
            new("薄荷 Mint", "#E7FBF3", "#5FD3B4", "#F3FEFB", "#B0E9D8", "#14524A", "#4E8C82"),
            new("奶油 Cream", "#FFF7E6", "#E0C078", "#FFFCF3", "#EFDCB0", "#5A4416", "#8E7A4E"),
            new("樱粉 Sakura", "#FDEDF5", "#E4A0C4", "#FEF7FB", "#F2C6DC", "#6B2749", "#A06884"),
            new("天蓝 Sky", "#EAF4FF", "#7FB6E8", "#F5FAFF", "#BFDCF5", "#1B3F66", "#5A7E9E"),
            new("墨夜 Ink", "#22303F", "#6E93AD", "#2C3C4E", "#52708A", "#EAF4FA", "#9FB6C6")
        };

        private static readonly (string Name, string Hex)[] Accents =
        {
            ("青", "#17B3CD"),
            ("薄荷", "#3FD2B4"),
            ("珊瑚", "#FF8577"),
            ("紫", "#8B7CF6"),
            ("琥珀", "#F5A623"),
            ("玫红", "#F06292"),
            ("靛蓝", "#4F8DF7"),
            ("石墨", "#5E93A8")
        };

        private void BuildPanelSwatches()
        {
            PanelSwatches.Children.Clear();
            foreach (var preset in Panels)
            {
                var chip = new Border
                {
                    Width = 34,
                    Height = 30,
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(0, 0, 9, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = preset.Name,
                    Background = ThemeService.Brush(ThemeService.Parse(preset.Panel, Colors.White)),
                    BorderBrush = ThemeService.Brush(ThemeService.Parse(preset.Edge, Colors.Gray)),
                    BorderThickness = new Thickness(2)
                };
                chip.MouseLeftButtonUp += (_, _) => ApplyPanel(preset);
                PanelSwatches.Children.Add(chip);
            }
        }

        private void BuildAccentSwatches()
        {
            AccentSwatches.Children.Clear();
            foreach (var (name, hex) in Accents)
            {
                var chip = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Margin = new Thickness(0, 0, 9, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = name,
                    Background = ThemeService.Brush(ThemeService.Parse(hex, Colors.Cyan))
                };
                chip.MouseLeftButtonUp += (_, _) => ApplyAccent(hex);
                AccentSwatches.Children.Add(chip);
            }
        }

        private void ApplyPanel(PanelPreset preset)
        {
            var theme = _app.Config.Theme ??= new ThemePalette();
            theme.Panel = preset.Panel;
            theme.PanelEdge = preset.Edge;
            theme.Item = preset.Item;
            theme.ItemEdge = preset.ItemEdge;
            theme.Text = preset.Text;
            theme.Muted = preset.Muted;

            _app.ApplyTheme();
            BuildPanelSwatches();
            Refresh();
        }

        private void ApplyAccent(string hex)
        {
            var theme = _app.Config.Theme ??= new ThemePalette();
            theme.Accent = hex;
            _app.ApplyTheme();
            BuildAccentSwatches();
            Refresh();
        }

        private void ResetTheme_Click(object sender, RoutedEventArgs e)
        {
            _app.Config.Theme = new ThemePalette();
            _app.ApplyTheme();
            BuildPanelSwatches();
            BuildAccentSwatches();
            Refresh();
        }

        // ------------------------------------------------------------------ theme
        private void Alpha_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            (_app.Config.Theme ??= new ThemePalette()).PanelAlpha = AlphaSlider.Value / 100.0;
            UpdateLabels();
            _themeDebounce.Stop();
            _themeDebounce.Start();
        }

        private void Radius_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            (_app.Config.Theme ??= new ThemePalette()).CornerRadius = RadiusSlider.Value;
            UpdateLabels();
            _themeDebounce.Stop();
            _themeDebounce.Start();
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _loading) return;
            var theme = _app.Config.Theme ??= new ThemePalette();
            theme.CelOutline = SwitchCel.IsChecked == true;
            theme.StickerShadow = SwitchShadow.IsChecked == true;
            _app.ApplyTheme();
        }

        // ------------------------------------------------------------------- dock
        private void Dock_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _loading) return;

            var cfg = _app.Config;
            bool was = cfg.DockEnabled;

            cfg.DockEnabled = SwitchDock.IsChecked == true;
            cfg.DockFullHeight = SwitchDockFull.IsChecked == true;
            cfg.DockAutoHide = SwitchDockAuto.IsChecked == true;
            cfg.FrostOnDock = SwitchDockFrost.IsChecked == true;
            cfg.DockBumpToReveal = SwitchBump.IsChecked == true;

            _app.SyncDock();
            _app.Dock?.ApplyAutoHide();
            _app.Save();
            _app.SyncTray();

            if (was != cfg.DockEnabled || cfg.DockAutoHide) _app.Dock?.Reload();
        }

        private void DockWidth_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            _app.Config.DockWidth = DockWidthSlider.Value;
            UpdateLabels();
            _themeDebounce.Stop();
            _themeDebounce.Start();
        }

        private void AddLauncher_Click(object sender, RoutedEventArgs e)
            => _app.AddLauncherInteractive();

        private void DockToggle_Click(object sender, RoutedEventArgs e)
        {
            _app.ToggleDock();
            Refresh();
        }

        // ----------------------------------------------------------------- clocks
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CityPickerWindow { Owner = this, Topmost = true };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
                _app.AddClock(dlg.Selected);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ClockItem item)
                _app.RemoveClock(item);
        }

        private void ChangeZone_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClockItem item) return;
            var dlg = new CityPickerWindow { Owner = this, Topmost = true };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
            {
                item.City = dlg.Selected.Name;
                item.TimeZoneId = string.IsNullOrEmpty(dlg.Selected.Id)
                    ? TimeZoneInfo.Local.Id
                    : dlg.Selected.Id;
                _app.Widget(item.Id)?.RefreshItem();
                _app.Save();
                ClockList.Items.Refresh();
            }
        }

        private void FaceImage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClockItem item) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择表底图片",
                Filter = "图片 Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件 All files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                item.FaceImage = dlg.FileName;
                _app.Widget(item.Id)?.RefreshItem();
                _app.Save();
            }
        }

        private void ToggleShape_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClockItem item) return;
            var next = item.Shape == WidgetShape.Circle ? WidgetShape.Square : WidgetShape.Circle;
            var widget = _app.Widget(item.Id);
            if (widget != null) widget.SetShape(next);
            else { item.Shape = next; _app.Save(); }
            ClockList.Items.Refresh();
        }

        // ------------------------------------------------------------- appearance
        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _loading) return;

            _app.Config.AlwaysOnTop = SwitchTop.IsChecked == true;
            _app.Config.SnapToEdges = SwitchSnap.IsChecked == true;
            _app.Config.ShowSeconds = SwitchSeconds.IsChecked == true;
            _app.Config.SmoothSecondHand = SwitchSmooth.IsChecked == true;
            _app.Config.ClickThrough = SwitchPass.IsChecked == true;
            _app.Config.BackdropBlur = SwitchBlur.IsChecked == true;
            _app.Config.ExcludeFromCapture = SwitchCapture.IsChecked != true;

            bool auto = SwitchAuto.IsChecked == true;
            if (auto != _app.Config.StartWithWindows)
            {
                _app.Config.StartWithWindows = auto;
                AutostartService.Apply(auto);
            }

            foreach (var item in _app.Clocks) item.ShowSeconds = _app.Config.ShowSeconds;
            _app.ApplyGlobalSettings();
            _app.ApplyClickThrough();
            foreach (var w in _app.Widgets) w.RefreshItem();
        }

        private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            _app.Config.Opacity = OpacitySlider.Value / 100.0;
            UpdateLabels();
            _app.ApplyGlobalSettings();
        }

        private void Tint_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _loading) return;
            _app.Config.GlassTint = TintSlider.Value / 100.0;
            UpdateLabels();
            _app.ApplyGlobalSettings();
        }

        private void Floating_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _loading) return;
            _app.Config.ShowFloatingClocks = SwitchFloating.IsChecked == true;
            if (_app.Config.ShowFloatingClocks) _app.ShowAll();
            else
            {
                foreach (var w in _app.Widgets.ToList()) w.Close();
            }
            _app.Save();
            _app.SyncTray();
        }

        private void Tile_Click(object sender, RoutedEventArgs e) => _app.TileAll();
        private void ShowAll_Click(object sender, RoutedEventArgs e) => _app.ShowAll();
    }
}
