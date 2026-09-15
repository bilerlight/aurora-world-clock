using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    /// <summary>
    /// Small glass widget mirroring the usage statistics CC Switch collects.
    /// It hides itself whenever CC Switch is not running.
    /// </summary>
    public partial class UsageWidget : Window
    {
        private readonly AppController _app;
        private readonly DispatcherTimer _refresh;
        private BackdropService? _backdrop;
        private bool _ready;

        private static readonly Color TintColor = Color.FromRgb(9, 13, 20);

        public UsageWidget(AppController app)
        {
            _app = app;
            InitializeComponent();

            Loaded += OnLoaded;
            Closing += (_, _) => _backdrop?.Dispose();
            MouseLeftButtonDown += OnDrag;
            MouseRightButtonDown += (_, e) =>
            {
                var menu = new ContextMenu { PlacementTarget = this };
                var hide = new MenuItem { Header = "隐藏用量组件" };
                hide.Click += (_, _) => { _app.Config.ShowUsageWidget = false; _app.Save(); Hide(); };
                var open = new MenuItem { Header = "打开统计面板…" };
                open.Click += (_, _) => _app.ShowDashboard();
                menu.Items.Add(open);
                menu.Items.Add(hide);
                menu.IsOpen = true;
            };

            _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refresh.Tick += (_, _) => Update();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyPosition();
            ApplyLook();

            _ready = true;
            _backdrop = new BackdropService(this, f => BackdropBrush.ImageSource = f,
                _app.Config.ExcludeFromCapture, 400);
            _backdrop.Start();

            _refresh.Start();
            Update();

            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
        }

        public void ApplyPosition()
        {
            double w = Width, h = Height;
            var wa = SystemParameters.WorkArea;
            double x = _app.Config.UsageX, y = _app.Config.UsageY;
            if (double.IsNaN(x) || double.IsNaN(y) || x < wa.Left - 50 || x > wa.Right - 50)
            {
                x = wa.Left + 40;
                y = wa.Top + 40;
            }
            Left = x;
            Top = y;
        }

        public void ApplyLook()
        {
            double tint = Math.Clamp(_app.Config.GlassTint, 0, 1);
            Tint.Background = new SolidColorBrush(Color.FromArgb((byte)((0.22 + tint * 0.40) * 255),
                TintColor.R, TintColor.G, TintColor.B));

            var cr = new CornerRadius(22);
            Glass.CornerRadius = Backdrop.CornerRadius = Tint.CornerRadius = cr;
            Sheen.CornerRadius = Rim.CornerRadius = cr;
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            DragMove();
            _app.Config.UsageX = Left;
            _app.Config.UsageY = Top;
            _backdrop?.Refresh();
            _app.Save();
        }

        private void Update()
        {
            if (!_ready) return;

            bool show = CcSwitchUsage.IsRunning() && _app.Config.ShowUsageWidget;
            Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;

            var rows = CcSwitchUsage.Day();
            decimal cost = rows.Sum(r => r.Cost);
            long inTok = rows.Sum(r => r.Input);
            long outTok = rows.Sum(r => r.Output);
            long cache = rows.Sum(r => r.CacheRead);
            int req = rows.Sum(r => r.Requests);

            CostText.Text = "$" + cost.ToString("0.00");
            ReqText.Text = req > 0 ? $"{req} 次请求" : "今日暂无请求";
            TokenText.Text = $"输入 {CcSwitchUsage.FormatTokens(inTok)} · 输出 {CcSwitchUsage.FormatTokens(outTok)} · 缓存 {CcSwitchUsage.FormatTokens(cache)}";
            UpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");

            int recent = CcSwitchUsage.RecentRequests(5);
            LiveDot.Fill = recent > 0
                ? new SolidColorBrush(Color.FromRgb(0x57, 0xE0, 0x8A))
                : new SolidColorBrush(Color.FromRgb(0x8F, 0xA6, 0xC2));

            BuildBars(rows, cost);
        }

        private void BuildBars(List<UsageRow> rows, decimal total)
        {
            Bars.Children.Clear();
            var top = rows.Take(3).ToList();
            if (top.Count == 0) return;

            decimal max = Math.Max(0.0001m, top.Max(r => r.Cost));
            foreach (var r in top)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var name = new TextBlock
                {
                    Text = Short(r.App),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xB0, 0xC4)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(name, 0);

                var track = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(track, 1);

                var fill = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Color.FromRgb(0x7C, 0xC4, 0xFF))
                };
                var host = new Grid();
                host.Children.Add(track);
                host.Children.Add(fill);
                Grid.SetColumn(host, 1);

                var val = new TextBlock
                {
                    Text = "$" + r.Cost.ToString("0.00"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xB0, 0xC4)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(val, 2);

                row.Children.Add(name);
                row.Children.Add(host);
                row.Children.Add(val);
                Bars.Children.Add(row);

                // fill width after layout
                double frac = (double)(r.Cost / max);
                fill.Width = 0;
                host.SizeChanged += (_, _) =>
                {
                    fill.Width = Math.Max(2, host.ActualWidth * frac);
                };
            }
        }

        private static string Short(string app)
        {
            if (string.IsNullOrEmpty(app)) return "其他";
            return app.Length <= 8 ? app : app[..8];
        }
    }
}
