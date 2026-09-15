using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    public partial class ClockWidget : Window
    {
        private readonly ClockItem _item;
        private readonly AppController _app;
        private readonly Stopwatch _frameClock = Stopwatch.StartNew();

        private bool _dragging;
        private Point _dragStartDevice;
        private Point _windowStart;
        private long _lastFrameMs = -100;
        private long _radiusTrackUntil;
        private int _lastRenderedSecond = -1;
        private bool _ready;

        private static readonly Color TintColor = Color.FromRgb(9, 13, 20);

        public ClockWidget(ClockItem item, AppController app)
        {
            _item = item;
            _app = app;

            InitializeComponent();

            Loaded += OnLoaded;
            Closing += OnClosing;
            SizeChanged += (_, _) => UpdateGeometry();
            DpiChanged += (_, _) => UpdateGeometry();
            MouseLeftButtonDown += OnMouseLeftDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftUp;
            MouseEnter += OnEnter;
            MouseLeave += OnLeave;
            MouseWheel += OnWheel;
            PreviewKeyDown += OnKeyDown;
        }

        public ClockItem Item => _item;

        // ------------------------------------------------------------------ init
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            ApplyClockProperties();

            double size = Clamp(_item.Size, 120, 460);
            _item.Size = size;
            Width = Height = size;
            LayoutForSize(size);

            InitShape(_item.Shape);
            RestorePosition();

            Noise.Background = NoiseBrush();
            Noise.Opacity = 0.55;

            Opacity = Clamp(_app.Config.Opacity, 0.25, 1.0);
            Topmost = _app.Config.AlwaysOnTop;
            ContextMenu = BuildMenu();

            _ready = true;
            UpdateGeometry();
            ApplyClickThrough();
            CompositionTarget.Rendering += OnRendering;
        }

        private static ImageBrush? _noise;

        /// <summary>A tiny tiled grain bitmap - the detail that sells a real frosted surface.</summary>
        private static ImageBrush NoiseBrush()
        {
            if (_noise != null) return _noise;

            const int n = 256;
            var rnd = new Random(20240914);
            var wb = new WriteableBitmap(n, n, 96, 96, PixelFormats.Bgra32, null);
            var buf = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++)
            {
                byte v = (byte)rnd.Next(110, 205);
                buf[i * 4 + 0] = v;                          // B
                buf[i * 4 + 1] = v;                          // G
                buf[i * 4 + 2] = v;                          // R
                buf[i * 4 + 3] = (byte)rnd.Next(0, 16);      // A - very subtle grain
            }
            wb.WritePixels(new Int32Rect(0, 0, n, n), buf, n * 4, 0);
            wb.Freeze();

            _noise = new ImageBrush(wb)
            {
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, n, n),
                Stretch = Stretch.None
            };
            _noise.Freeze();
            return _noise;
        }

        public void ApplyClickThrough()
        {
            if (!_ready) return;
            WindowFx.SetClickThrough(this, _app.Config.ClickThrough);
            PassRim.BeginAnimation(OpacityProperty, new DoubleAnimation(
                PassRim.Opacity, _app.Config.ClickThrough ? 0.9 : 0.0, TimeSpan.FromMilliseconds(220)));
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void ApplyTheme()
        {
            double tint = Clamp(_app.Config.GlassTint, 0, 1);
            double a = 0.13 + tint * 0.34;
            Glass.Background = new SolidColorBrush(Color.FromArgb((byte)(a * 255), TintColor.R, TintColor.G, TintColor.B));

            var accent = ParseColor(_item.Accent, Color.FromRgb(0x7C, 0xC4, 0xFF));
            GlowRim.BorderBrush = new SolidColorBrush(accent);

            AcrylicHelper.Enable(this, TintColor, 0.14 + tint * 0.24);
        }

        private double AcrylicTint => 0.14 + Clamp(_app.Config.GlassTint, 0, 1) * 0.24;

        public void ApplyConfig()
        {
            if (!_ready) return;
            ApplyTheme();
            Opacity = Clamp(_app.Config.Opacity, 0.25, 1.0);
            Topmost = _app.Config.AlwaysOnTop;
            ApplyClockProperties();
            ApplyClickThrough();
        }

        public void RefreshItem()
        {
            ApplyClockProperties();
            var tz = TimeZoneCatalog.Resolve(_item.TimeZoneId);
            UpdateDigital(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            BigClock.InvalidateVisual();
            MiniClock.InvalidateVisual();
        }

        private void ApplyClockProperties()
        {
            var accent = ParseColor(_item.Accent, Color.FromRgb(0x7C, 0xC4, 0xFF));
            Configure(BigClock);
            Configure(MiniClock);

            void Configure(Controls.AnalogClock c)
            {
                c.TimeZoneId = _item.TimeZoneId;
                c.City = ShortCity(_item.City);
                c.ShowSecondHand = _item.ShowSeconds;
                c.SweepSeconds = _app.Config.SmoothSecondHand;
                c.AccentColor = accent;
            }
        }

        private static string ShortCity(string city)
        {
            if (string.IsNullOrWhiteSpace(city)) return "";
            int idx = city.IndexOf('·');
            return idx > 0 ? city[..idx].Trim() : city;
        }

        // -------------------------------------------------------------- layout
        private void LayoutForSize(double size)
        {
            BigClock.Width = size;
            BigClock.Height = size;

            SquareCard.Margin = new Thickness(Math.Round(size * 0.078));

            bool showMini = size >= 185;
            MiniColumn.Width = showMini ? GridLength.Auto : new GridLength(0);
            MiniClock.Visibility = showMini ? Visibility.Visible : Visibility.Collapsed;
            double mini = Math.Round(size * 0.300);
            MiniClock.Width = mini;
            MiniClock.Height = mini;
            MiniClock.Margin = showMini ? new Thickness(0, 0, size * 0.055, 0) : new Thickness(0);

            CityText.FontSize = Math.Round(Math.Max(10, size * 0.072));
            TimeText.FontSize = Math.Round(Math.Max(19, size * 0.200));
            SecondsText.FontSize = Math.Round(Math.Max(9.5, size * 0.070));
            DateText.FontSize = Math.Round(Math.Max(9, size * 0.050));
            SecondsText.Margin = new Thickness(0, -size * 0.036, 0, size * 0.010);
        }

        private CornerRadius TargetRadius(WidgetShape shape, double size)
            => new(shape == WidgetShape.Circle ? size / 2 : size * 0.20);

        private void InitShape(WidgetShape shape)
        {
            UpdateGeometry();

            bool circle = shape == WidgetShape.Circle;
            BigClock.Visibility = circle ? Visibility.Visible : Visibility.Collapsed;
            BigClock.Opacity = circle ? 1 : 0;
            SquareCard.Visibility = circle ? Visibility.Collapsed : Visibility.Visible;
            SquareCard.Opacity = circle ? 0 : 1;
        }

        /// <summary>
        /// Corner radius follows the live size (WPF clamps a huge radius into a perfect circle) and
        /// the native window region is clipped to the same silhouette so the acrylic blur stays inside.
        /// </summary>
        private void UpdateGeometry()
        {
            double w = ActualWidth > 1 ? ActualWidth : Width;
            double h = ActualHeight > 1 ? ActualHeight : Height;
            bool circle = _item.Shape == WidgetShape.Circle;
            double radius = circle ? w / 2 : w * 0.20;

            var cr = new CornerRadius(circle ? 9999 : radius);
            Glass.CornerRadius = Sheen.CornerRadius = Rim.CornerRadius = GlowRim.CornerRadius = cr;
            Bloom.CornerRadius = PassRim.CornerRadius = Noise.CornerRadius = cr;

            AcrylicHelper.ApplyShapeRegion(this, w, h, radius);
        }

        private void SwapContent(WidgetShape shape)
        {
            bool circle = shape == WidgetShape.Circle;
            BigClock.Visibility = circle ? Visibility.Visible : Visibility.Collapsed;
            BigClock.Opacity = circle ? 1 : 0;
            SquareCard.Visibility = circle ? Visibility.Collapsed : Visibility.Visible;
            SquareCard.Opacity = circle ? 0 : 1;
        }

        // ------------------------------------------------------------ animation
        public void AnimateIn()
        {
            Opacity = 0;
            EntranceScale.ScaleX = EntranceScale.ScaleY = 0.80;
            EntranceMove.Y = 22;

            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 };
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, Clamp(_app.Config.Opacity, 0.25, 1.0), TimeSpan.FromMilliseconds(360)));

            var sc = new DoubleAnimation(0.80, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease };
            EntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, sc);
            EntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, sc);

            var mv = new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            EntranceMove.BeginAnimation(TranslateTransform.YProperty, mv);
        }

        private void OnEnter(object sender, MouseEventArgs e)
        {
            GlowRim.BeginAnimation(OpacityProperty, new DoubleAnimation(GlowRim.Opacity, 0.85, TimeSpan.FromMilliseconds(180)));
            Rim.BeginAnimation(OpacityProperty, new DoubleAnimation(Rim.Opacity, 1.0, TimeSpan.FromMilliseconds(180)));
        }

        private void OnLeave(object sender, MouseEventArgs e)
        {
            GlowRim.BeginAnimation(OpacityProperty, new DoubleAnimation(GlowRim.Opacity, 0.0, TimeSpan.FromMilliseconds(220)));
        }

        // --------------------------------------------------------------- render
        private void OnRendering(object? sender, EventArgs e)
        {
            long now = _frameClock.ElapsedMilliseconds;
            if (now - _lastFrameMs < 33) return; // ~30 fps
            _lastFrameMs = now;

            if (now < _radiusTrackUntil) UpdateGeometry();

            bool circle = BigClock.Visibility == Visibility.Visible;
            if (circle) BigClock.InvalidateVisual(); else MiniClock.InvalidateVisual();

            var tz = TimeZoneCatalog.Resolve(_item.TimeZoneId);
            var t = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            if (t.Second != _lastRenderedSecond)
            {
                _lastRenderedSecond = t.Second;
                UpdateDigital(t);
                if (circle) BigClock.InvalidateVisual();
            }
        }

        private void UpdateDigital(DateTime t)
        {
            if (SquareCard.Visibility != Visibility.Visible) return;
            CityText.Text = ShortCity(_item.City);
            TimeText.Text = t.ToString("HH:mm", CultureInfo.InvariantCulture);
            SecondsText.Text = t.ToString("ss", CultureInfo.InvariantCulture) + (t.Hour < 12 ? "  AM" : "  PM");
            DateText.Text = t.ToString("ddd, MMM d", CultureInfo.CurrentCulture);
        }

        // ----------------------------------------------------------- positioning
        public void SetPosition(double x, double y, bool persist = true, bool snap = true)
        {
            if (snap && _app.Config.SnapToEdges)
            {
                const double margin = 14;
                var wa = SystemParameters.WorkArea;
                if (Math.Abs(x - wa.Left) < margin) x = wa.Left;
                if (Math.Abs(x + Width - wa.Right) < margin) x = wa.Right - Width;
                if (Math.Abs(y - wa.Top) < margin) y = wa.Top;
                if (Math.Abs(y + Height - wa.Bottom) < margin) y = wa.Bottom - Height;
            }

            Left = x;
            Top = y;
            _item.X = x;
            _item.Y = y;
            if (persist) _app.Save();
        }

        private void RestorePosition()
        {
            var wa = SystemParameters.WorkArea;
            double x = _item.X, y = _item.Y;

            if (double.IsNaN(x) || double.IsNaN(y) || !IsOnAnyScreen(x, y))
            {
                x = wa.Right - Width - 36;
                y = wa.Top + 80;
            }

            SetPosition(x, y, persist: false, snap: false);
        }

        private bool IsOnAnyScreen(double x, double y)
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var b = screen.WorkingArea;
                if (x + Width > b.Left && x < b.Right && y + Height > b.Top && y < b.Bottom) return true;
            }
            return false;
        }

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            if (_app.Config.ClickThrough) return;
            if (_item.Locked) return;
            if (e.ButtonState != MouseButtonState.Pressed) return;

            _dragging = true;
            _dragStartDevice = PointToScreen(e.GetPosition(this));
            _windowStart = new Point(Left, Top);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            AcrylicHelper.EnableFastBlur(this, TintColor, AcrylicTint);
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var now = PointToScreen(e.GetPosition(this));
            var dpi = VisualTreeHelper.GetDpi(this);
            double dx = (now.X - _dragStartDevice.X) / dpi.DpiScaleX;
            double dy = (now.Y - _dragStartDevice.Y) / dpi.DpiScaleY;
            SetPosition(_windowStart.X + dx, _windowStart.Y + dy, persist: false, snap: true);
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            AcrylicHelper.Enable(this, TintColor, AcrylicTint);
            _app.Save();
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                double o = Clamp(_app.Config.Opacity + (e.Delta > 0 ? 0.05 : -0.05), 0.25, 1.0);
                _app.Config.Opacity = o;
                Opacity = o;
                _app.Save();
            }
            else
            {
                Resize(_item.Size + (e.Delta > 0 ? 16 : -16));
            }
            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                double step = 1;
                double dx = 0, dy = 0;
                switch (e.Key)
                {
                    case Key.Left: dx = -step; break;
                    case Key.Right: dx = step; break;
                    case Key.Up: dy = -step; break;
                    case Key.Down: dy = step; break;
                    default: return;
                }
                SetPosition(Left + dx, Top + dy, persist: true, snap: false);
                e.Handled = true;
            }
        }

        public void Resize(double newSize)
        {
            newSize = Clamp(newSize, 120, 460);
            if (Math.Abs(newSize - _item.Size) < 0.5) return;

            double from = Width;
            _item.Size = newSize;

            Width = Height = newSize; // base value (animation overrides then settles)
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(180);
            BeginAnimation(WidthProperty, new DoubleAnimation(from, newSize, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            BeginAnimation(HeightProperty, new DoubleAnimation(from, newSize, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });

            LayoutForSize(newSize);
            _radiusTrackUntil = _frameClock.ElapsedMilliseconds + 240;
            UpdateGeometry();
            _app.Save();
        }

        /// <summary>Circle &lt;-&gt; Square, with a squash-and-pop morph.</summary>
        public async void SetShape(WidgetShape shape)
        {
            if (_item.Shape == shape) return;
            _item.Shape = shape;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            double target = Clamp(_app.Config.Opacity, 0.25, 1.0);

            var down = new DoubleAnimation(1, 0.90, TimeSpan.FromMilliseconds(130)) { EasingFunction = easeOut };
            EntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, down);
            EntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, down);
            BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, Math.Max(0.3, target * 0.5), TimeSpan.FromMilliseconds(130)) { EasingFunction = easeOut });

            await Task.Delay(130);

            UpdateGeometry();
            SwapContent(shape);

            var up = new DoubleAnimation(0.90, 1, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };
            EntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, up);
            EntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, up);
            BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, target, TimeSpan.FromMilliseconds(220)));

            if (shape == WidgetShape.Square)
                UpdateDigital(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneCatalog.Resolve(_item.TimeZoneId)));
            _app.Save();
        }

        // ------------------------------------------------------------ menu
        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu { PlacementTarget = this };
            var accent = ParseColor(_item.Accent, Color.FromRgb(0x7C, 0xC4, 0xFF));

            MenuItem Mi(string header, string? glyph, Action? action, bool check = false, bool enabled = true)
            {
                var mi = new MenuItem { Header = header, IsEnabled = enabled };
                if (check)
                    mi.Icon = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Foreground = new SolidColorBrush(accent) };
                else if (glyph != null)
                    mi.Icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Opacity = 0.9 };
                if (action != null) mi.Click += (_, _) => action();
                return mi;
            }

            menu.Items.Add(Mi("添加时钟…", "\uE710", PickAndAdd));
            menu.Items.Add(Mi("删除此时钟", "\uE74D", () => _app.RemoveClock(_item)));

            menu.Items.Add(new Separator());

            var shape = new MenuItem { Header = "形状 Shape" };
            shape.Items.Add(Mi("圆形 Circle", null, () => SetShape(WidgetShape.Circle), _item.Shape == WidgetShape.Circle));
            shape.Items.Add(Mi("方形 Square", null, () => SetShape(WidgetShape.Square), _item.Shape == WidgetShape.Square));
            menu.Items.Add(shape);

            var size = new MenuItem { Header = "大小 Size" };
            size.Items.Add(Mi("小 Small · 150", null, () => Resize(150), Math.Abs(_item.Size - 150) < 6));
            size.Items.Add(Mi("中 Medium · 210", null, () => Resize(210), Math.Abs(_item.Size - 210) < 6));
            size.Items.Add(Mi("大 Large · 280", null, () => Resize(280), Math.Abs(_item.Size - 280) < 6));
            size.Items.Add(Mi("特大 XL · 360", null, () => Resize(360), Math.Abs(_item.Size - 360) < 6));
            menu.Items.Add(size);

            menu.Items.Add(Mi("显示秒针 Seconds", null, () =>
            {
                _item.ShowSeconds = !_item.ShowSeconds;
                ApplyClockProperties();
                _app.Save();
            }, _item.ShowSeconds));

            menu.Items.Add(new Separator());

            menu.Items.Add(Mi("始终置顶 Always on top", null, () =>
            {
                _app.Config.AlwaysOnTop = !_app.Config.AlwaysOnTop;
                _app.ApplyGlobalSettings();
            }, _app.Config.AlwaysOnTop));

            menu.Items.Add(Mi("锁定位置 Lock", null, () => { _item.Locked = !_item.Locked; _app.Save(); }, _item.Locked));

            menu.Items.Add(Mi($"指针穿透 Click-through  {_app.LabelClickThrough}", null, _app.ToggleClickThrough, _app.Config.ClickThrough));
            menu.Items.Add(Mi($"隐藏全部时钟 Hide all  {_app.LabelToggleVisible}", null, _app.HideAll));

            menu.Items.Add(Mi("开机自启 Startup", null, () =>
            {
                _app.Config.StartWithWindows = !_app.Config.StartWithWindows;
                AutostartService.Apply(_app.Config.StartWithWindows);
                _app.Save();
            }, _app.Config.StartWithWindows));

            menu.Items.Add(new Separator());

            menu.Items.Add(Mi("设置…", "\uE713", _app.ShowSettings));
            menu.Items.Add(Mi("排列全部时钟", "\uE8FD", _app.TileAll));
            menu.Items.Add(Mi("显示全部时钟", "\uE7C4", _app.ShowAll));

            menu.Items.Add(new Separator());
            menu.Items.Add(Mi("退出 Aurora Clock", "\uE7E8", _app.Quit));

            return menu;
        }

        private void PickAndAdd()
        {
            var dlg = new CityPickerWindow { Owner = this, Topmost = true };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
                _app.AddClock(dlg.Selected, _item.Shape);
        }

        // --------------------------------------------------------------- helpers
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        private static Color ParseColor(string? hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return fallback;
                return (Color)ColorConverter.ConvertFromString(hex)!;
            }
            catch { return fallback; }
        }
    }
}
