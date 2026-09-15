using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    public partial class SettingsWindow : Window
    {
        private readonly AppController _app;
        private bool _loading;

        public SettingsWindow(AppController app)
        {
            _app = app;
            InitializeComponent();
            Loaded += OnLoaded;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AcrylicHelper.Enable(this, System.Windows.Media.Color.FromRgb(9, 13, 20), 0.55);
            ClockList.ItemsSource = _app.Clocks;
            VersionText.Text = $"Aurora World Clock  v{Version}   ·   .NET {Environment.Version.ToString(3)}   ·   配置目录 %AppData%\\AuroraClock";
            HotkeyText.Text =
                $"{_app.LabelToggleVisible}　显示 / 隐藏全部时钟\n" +
                $"{_app.LabelClickThrough}　指针穿透开关\n" +
                $"{_app.LabelAlwaysOnTop}　始终置顶开关\n" +
                $"{_app.LabelSettings}　打开本设置窗口";

            _loading = true;
            SwitchTop.IsChecked = _app.Config.AlwaysOnTop;
            SwitchPass.IsChecked = _app.Config.ClickThrough;
            SwitchAuto.IsChecked = _app.Config.StartWithWindows;
            SwitchSnap.IsChecked = _app.Config.SnapToEdges;
            SwitchSeconds.IsChecked = _app.Config.ShowSeconds;
            SwitchSmooth.IsChecked = _app.Config.SmoothSecondHand;
            OpacitySlider.Value = Math.Round(_app.Config.Opacity * 100);
            TintSlider.Value = Math.Round(_app.Config.GlassTint * 100);
            _loading = false;
            UpdateLabels();
        }

        public void Refresh()
        {
            if (!IsLoaded) return;
            _loading = true;
            SwitchTop.IsChecked = _app.Config.AlwaysOnTop;
            SwitchPass.IsChecked = _app.Config.ClickThrough;
            SwitchAuto.IsChecked = _app.Config.StartWithWindows;
            SwitchSnap.IsChecked = _app.Config.SnapToEdges;
            SwitchSeconds.IsChecked = _app.Config.ShowSeconds;
            SwitchSmooth.IsChecked = _app.Config.SmoothSecondHand;
            OpacitySlider.Value = Math.Round(_app.Config.Opacity * 100);
            TintSlider.Value = Math.Round(_app.Config.GlassTint * 100);
            _loading = false;
            UpdateLabels();
            SubTitle.Text = $"{_app.Clocks.Count} 个时钟正在运行  ·  {_app.Clocks.Count} clocks live";
        }

        private static string Version =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        private void UpdateLabels()
        {
            OpacityValue.Text = $"{OpacitySlider.Value:0}%";
            TintValue.Text = $"{TintSlider.Value:0}%";
        }

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

        private void ToggleShape_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClockItem item) return;
            var next = item.Shape == WidgetShape.Circle ? WidgetShape.Square : WidgetShape.Circle;
            var widget = _app.Widget(item.Id);
            if (widget != null) widget.SetShape(next);
            else { item.Shape = next; _app.Save(); }
            // refresh the button label binding
            ClockList.Items.Refresh();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            _app.Config.AlwaysOnTop = SwitchTop.IsChecked == true;
            _app.Config.SnapToEdges = SwitchSnap.IsChecked == true;
            _app.Config.ShowSeconds = SwitchSeconds.IsChecked == true;
            _app.Config.SmoothSecondHand = SwitchSmooth.IsChecked == true;
            _app.Config.ClickThrough = SwitchPass.IsChecked == true;

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
            if (_loading) return;
            _app.Config.Opacity = OpacitySlider.Value / 100.0;
            UpdateLabels();
            _app.ApplyGlobalSettings();
        }

        private void Tint_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _app.Config.GlassTint = TintSlider.Value / 100.0;
            UpdateLabels();
            _app.ApplyGlobalSettings();
        }

        private void Tile_Click(object sender, RoutedEventArgs e) => _app.TileAll();

        private void ShowAll_Click(object sender, RoutedEventArgs e) => _app.ShowAll();
    }
}
