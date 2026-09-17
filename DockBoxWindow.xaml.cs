using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuroraClock.Controls;
using AuroraClock.Models;
using AuroraClock.Services;

namespace AuroraClock
{
    /// <summary>
    /// The right-hand "global box": one docked, cel-shaded, highly transparent panel hosting the
    /// clocks, the quick launcher (apps and cmd commands bound to a working folder), the CC Switch
    /// usage readout, the todo list and the sticky notes.
    /// </summary>
    public partial class DockBoxWindow : Window
    {
        private enum Edge { Left, Top, Right, Bottom }

        /// <summary>How many pixels stay on screen while the dock is hidden (0 = fully hidden).</summary>
        private double Peek => Math.Clamp(_app.Config.DockHidePeek, 0, 30);

        private const double EdgePad = 10;

        private readonly AppController _app;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _usageTimer;
        private readonly DispatcherTimer _hintTimer;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _bumpTimer;
        private readonly List<AnalogClock> _dials = new();

        private BackdropService? _backdrop;

        private bool _collapsed;
        private bool _dragging;
        private bool _resizing;
        private bool _autoHidden;

        private Point _dragOrigin;
        private double _dragLeft;
        private double _dragTop;
        private double _resizeCross;

        private Edge CurrentEdge => (_app.Config.DockEdge ?? "Right").Trim().ToLowerInvariant() switch
        {
            "left" => Edge.Left,
            "top" => Edge.Top,
            "bottom" => Edge.Bottom,
            _ => Edge.Right
        };

        private bool VerticalSpan => CurrentEdge == Edge.Left || CurrentEdge == Edge.Right;

        public DockBoxWindow(AppController app)
        {
            _app = app;
            InitializeComponent();

            Loaded += OnLoaded;
            MouseRightButtonUp += OnBackgroundMenu;
            MouseEnter += (_, _) => OnPointerEnter();
            MouseLeave += (_, _) => OnPointerLeave();

            HeaderBar.MouseLeftButtonDown += OnHeaderDrag;
            HeaderBar.MouseMove += OnHeaderMove;
            HeaderBar.MouseLeftButtonUp += OnHeaderRelease;
            GripLeft.MouseLeftButtonDown += OnResizeStart;
            GripLeft.MouseMove += OnResizeMove;
            GripLeft.MouseLeftButtonUp += OnResizeEnd;

            CollapseBtn.Click += (_, _) => ToggleCollapse();
            DockSettingsBtn.Click += (_, _) => _app.ShowSettings();
            AddMenuBtn.Click += (_, _) => ShowAddMenu();
            TodoAddBtn.Click += (_, _) => AddTodo();
            NoteAddBtn.Click += (_, _) => AddNote();
            TodoInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { AddTodo(); e.Handled = true; }
            };

            _clockTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _clockTimer.Tick += (_, _) => UpdateClock();

            _usageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _usageTimer.Tick += (_, _) => UpdateUsage();

            _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _hintTimer.Tick += (_, _) => { _hintTimer.Stop(); RefreshFooter(); };

            // slides away a moment after the pointer has left
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                // never yank the box away while the pointer is on it, near its edge, or typing in it
                if (IsMouseOver || IsKeyboardFocusWithin) return;
                if (PointerInEdgeZone(18)) return;
                if (_dragging || _resizing) return;
                HideNow();
            };

            // edge bump detection, so a fully hidden dock still reacts to the mouse
            _bumpTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _bumpTimer.Tick += (_, _) => BumpTick();

            Closing += (_, _) =>
            {
                _backdrop?.Dispose();
                _clockTimer.Stop();
                _usageTimer.Stop();
                _hintTimer.Stop();
                _hideTimer.Stop();
                _bumpTimer.Stop();
            };
        }

        // ------------------------------------------------------------------ boot
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLook();
            OrderSections();

            UpdateClock();
            UpdateUsage();
            BuildTiles();
            BuildTodos();
            BuildNotes();
            RefreshFooter();

            if (_app.Config.BackdropBlur && _app.Config.FrostOnDock)
            {
                _backdrop = new BackdropService(this,
                    frame => BackdropBrush.ImageSource = frame,
                    _app.Config.ExcludeFromCapture,
                    600);
                _backdrop.Start();
            }
            else
            {
                Backdrop.Visibility = Visibility.Collapsed;
            }

            _clockTimer.Start();
            _usageTimer.Start();
            _bumpTimer.Start();

            ApplyGeometry(animate: false);
            if (_app.Config.DockAutoHide) HideNow(animate: false);

            // Never touch Window.Opacity on a layered window - fade the glass layer instead.
            Glass.Opacity = 0;
            Glass.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, Math.Clamp(_app.Config.DockOpacity, 0.25, 1.0),
                    TimeSpan.FromMilliseconds(340))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            EntranceSlide();
        }

        // -------------------------------------------------------------- geometry
        /// <summary>Where the dock sits when it is on screen, for the current edge.</summary>
        private Rect ShownBounds()
        {
            var wa = SystemParameters.WorkArea;
            var edge = CurrentEdge;

            double thick = _collapsed
                ? 62
                : Math.Max(180, VerticalSpan ? _app.Config.DockWidth : _app.Config.DockHeight);

            if (VerticalSpan)
            {
                double span = _app.Config.DockFullHeight
                    ? Math.Max(320, wa.Height - EdgePad * 2)
                    : Math.Min(SavedSpan(), Math.Max(220, wa.Height - EdgePad * 2));

                double top = _app.Config.DockFullHeight ? wa.Top + EdgePad : _app.Config.DockY;
                if (double.IsNaN(top) || top < wa.Top || top > wa.Bottom - 80) top = wa.Top + 40;
                top = Math.Clamp(top, wa.Top + EdgePad, Math.Max(wa.Top + EdgePad, wa.Bottom - EdgePad - span));

                double left = edge == Edge.Right ? wa.Right - thick - EdgePad : wa.Left + EdgePad;
                return new Rect(left, top, thick, span);
            }
            else
            {
                double span = _app.Config.DockFullHeight
                    ? Math.Max(320, wa.Width - EdgePad * 2)
                    : Math.Min(SavedSpan(), Math.Max(220, wa.Width - EdgePad * 2));

                double left = _app.Config.DockFullHeight ? wa.Left + EdgePad : _app.Config.DockX;
                if (double.IsNaN(left) || left < wa.Left || left > wa.Right - 80) left = wa.Left + 40;
                left = Math.Clamp(left, wa.Left + EdgePad, Math.Max(wa.Left + EdgePad, wa.Right - EdgePad - span));

                double top = edge == Edge.Bottom ? wa.Bottom - thick - EdgePad : wa.Top + EdgePad;
                return new Rect(left, top, span, thick);
            }
        }

        /// <summary>Length along the edge, remembered from the last manual resize.</summary>
        private double SavedSpan()
        {
            var wa = SystemParameters.WorkArea;
            double span = _app.Config.DockSpan;
            if (double.IsNaN(span) || span < 200)
                span = VerticalSpan
                    ? Math.Min(760, Math.Max(360, wa.Height - 80))
                    : Math.Min(720, Math.Max(320, wa.Width - 80));
            return span;
        }

        /// <summary>The off-screen coordinate the dock retreats to.</summary>
        private double HiddenCoordinate()
        {
            var mon = MonitorBounds();
            var b = ShownBounds();
            double peek = Math.Clamp(_app.Config.DockHidePeek, 0, 30);

            return CurrentEdge switch
            {
                Edge.Left => mon.Left - b.Width + peek,
                Edge.Right => mon.Right - peek,
                Edge.Top => mon.Top - b.Height + peek,
                _ => mon.Bottom - peek
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public int Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        private const uint MonitorDefaultToNearest = 2;

        /// <summary>
        /// The full bounds of the monitor the dock is on, in DIPs. Hiding uses the monitor rather
        /// than the work area, so a bottom-docked box does not park itself on top of the taskbar.
        /// </summary>
        private Rect MonitorBounds()
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                    if (GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
                    {
                        var dpi = VisualTreeHelper.GetDpi(this);
                        double left = info.Monitor.Left / dpi.DpiScaleX;
                        double top = info.Monitor.Top / dpi.DpiScaleY;
                        double right = info.Monitor.Right / dpi.DpiScaleX;
                        double bottom = info.Monitor.Bottom / dpi.DpiScaleY;
                        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
                    }
                }
            }
            catch
            {
                // fall through to the primary screen
            }

            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        /// <summary>Sizes, positions and orients the dock for its current edge.</summary>
        private void ApplyGeometry(bool animate)
        {
            var b = ShownBounds();

            Width = b.Width;
            Height = b.Height;

            // the gap (and the shadow) always faces the inside of the screen
            Glass.Margin = CurrentEdge switch
            {
                Edge.Left => new Thickness(0, 0, 8, 0),
                Edge.Top => new Thickness(0, 0, 0, 8),
                Edge.Bottom => new Thickness(0, 8, 0, 0),
                _ => new Thickness(8, 0, 0, 0)
            };

            PlaceGrip();
            ApplyOrientation();

            if (_autoHidden)
            {
                SetHiddenPosition(animate);
                return;
            }

            MoveTo(b.Left, b.Top, animate ? 260 : 0, EasingMode.EaseOut);
            _backdrop?.Refresh();
        }

        /// <summary>Puts the resize grip on the side that faces the middle of the screen.</summary>
        private void PlaceGrip()
        {
            GripLeft.Width = double.NaN;
            GripLeft.Height = double.NaN;
            GripLeft.HorizontalAlignment = HorizontalAlignment.Stretch;
            GripLeft.VerticalAlignment = VerticalAlignment.Stretch;
            GripLeft.ClearValue(WidthProperty);
            GripLeft.ClearValue(HeightProperty);

            switch (CurrentEdge)
            {
                case Edge.Left:
                    GripLeft.HorizontalAlignment = HorizontalAlignment.Right;
                    GripLeft.Width = 8;
                    GripLeft.Cursor = Cursors.SizeWE;
                    break;

                case Edge.Top:
                    GripLeft.VerticalAlignment = VerticalAlignment.Bottom;
                    GripLeft.Height = 8;
                    GripLeft.Cursor = Cursors.SizeNS;
                    break;

                case Edge.Bottom:
                    GripLeft.VerticalAlignment = VerticalAlignment.Top;
                    GripLeft.Height = 8;
                    GripLeft.Cursor = Cursors.SizeNS;
                    break;

                default:
                    GripLeft.HorizontalAlignment = HorizontalAlignment.Left;
                    GripLeft.Width = 8;
                    GripLeft.Cursor = Cursors.SizeWE;
                    break;
            }
        }

        /// <summary>A top / bottom dock flows its sections sideways instead of downwards.</summary>
        private void ApplyOrientation()
        {
            bool horizontal = CurrentEdge == Edge.Top || CurrentEdge == Edge.Bottom;

            Sections.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
            Scroller.HorizontalScrollBarVisibility = horizontal
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
            Scroller.VerticalScrollBarVisibility = horizontal
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto;

            foreach (var card in Sections.Children.OfType<FrameworkElement>())
                card.Width = horizontal ? 310 : double.NaN;
        }

        /// <summary>Animates the dock in from whichever edge it lives on.</summary>
        private void EntranceSlide()
        {
            double from = CurrentEdge switch
            {
                Edge.Left => -34,
                Edge.Right => 34,
                Edge.Top => -34,
                _ => 34
            };

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var axis = VerticalSpan ? TranslateTransform.XProperty : TranslateTransform.YProperty;
            SlideTransform.BeginAnimation(axis,
                new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(400)) { EasingFunction = ease });
        }

        // ------------------------------------------------------------------ boot

        /// <summary>Applies the cel palette to the parts XAML cannot bind directly.</summary>
        public void ApplyLook()
        {
            var palette = ThemeService.Current;
            Tint.Background = ThemeService.Brush(ThemeService.PanelFill);
            Sticker.Color = ThemeService.PanelEdge;
            Sticker.Opacity = palette.StickerShadow ? 0.5 : 0;

            foreach (var dial in _dials)
            {
                dial.AccentColor = ThemeService.Accent;
                dial.TextColor = ThemeService.Text;
            }
        }

        /// <summary>Applies the configured section order and visibility.</summary>
        private void OrderSections()
        {
            var wanted = _app.Config.DockSections ?? new List<string>();
            var cards = Sections.Children.OfType<FrameworkElement>()
                .Where(c => c.Tag is string)
                .ToList();

            foreach (var card in cards)
                card.Visibility = wanted.Contains((string)card.Tag!)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Sections.Children.Clear();
            foreach (var id in wanted)
            {
                var card = cards.FirstOrDefault(c => (string)c.Tag! == id);
                if (card != null) Sections.Children.Add(card);
            }
            foreach (var card in cards.Where(c => !Sections.Children.Contains(c)))
                Sections.Children.Add(card);
        }

        // ----------------------------------------------------------------- clock
        private void UpdateClock()
        {
            try
            {
                if (_app.Clocks.Count > 0)
                {
                    var first = _app.Clocks[0];
                    var tz = FindZone(first.TimeZoneId);
                    var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz);

                    HeroTime.Text = _app.Config.ShowSeconds
                        ? now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                        : now.ToString("HH:mm", CultureInfo.InvariantCulture);

                    HeroDate.Text = now.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")) +
                                    " · " + Workday.WeekLabel(now.DateTime);

                    ZoneText.Text = ShortCity(first.City) + " · " + tz.BaseUtcOffset.ToString(@"hh\:mm");

                    double track = SecTrack.ActualWidth;
                    if (track > 1)
                        SecBar.Width = track * ((now.Second + now.Millisecond / 1000.0) / 60.0);

                    SyncDials();
                }
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }

        private static TimeZoneInfo FindZone(string? id)
        {
            try
            {
                return string.IsNullOrWhiteSpace(id)
                    ? TimeZoneInfo.Local
                    : TimeZoneInfo.FindSystemTimeZoneById(id!);
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        /// <summary>One small analog dial per configured city.</summary>
        private void SyncDials()
        {
            if (_dials.Count != _app.Clocks.Count)
            {
                DialHost.Children.Clear();
                _dials.Clear();

                foreach (var item in _app.Clocks)
                {
                    var dial = new AnalogClock
                    {
                        Width = 86,
                        Height = 86,
                        TimeZoneId = item.TimeZoneId,
                        ShowDigital = false,
                        ShowCity = false,
                        ShowDate = false,
                        ShowNumbers = false,
                        ShowFacePlate = true,
                        ShowSecondHand = item.ShowSeconds,
                        SweepSeconds = _app.Config.SmoothSecondHand,
                        AccentColor = ThemeService.Accent,
                        TextColor = ThemeService.Text
                    };

                    var caption = new TextBlock
                    {
                        Text = ShortCity(item.City),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0),
                        MaxWidth = 86,
                        FontSize = 10,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = ThemeService.Brush(ThemeService.Muted)
                    };

                    var stack = new StackPanel { Width = 86, Margin = new Thickness(0, 0, 8, 10) };
                    stack.Children.Add(dial);
                    stack.Children.Add(caption);
                    DialHost.Children.Add(stack);
                    _dials.Add(dial);
                }
            }

            foreach (var dial in _dials) dial.InvalidateVisual();
        }

        private static string ShortCity(string? city)
        {
            if (string.IsNullOrWhiteSpace(city)) return "本地";
            int cut = city!.IndexOfAny(new[] { '·', '(', '（', ',', '/' });
            return cut > 0 ? city.Substring(0, cut).Trim() : city.Trim();
        }

        // -------------------------------------------------------------- launchers
        public void BuildTiles()
        {
            TileHost.Children.Clear();
            var items = _app.Config.Launchers ??= new List<LauncherItem>();

            foreach (var item in items)
            {
                var stack = new StackPanel();
                stack.Children.Add(BuildIcon(item));
                stack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Name) ? item.KindLabel : item.Name,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 5, 0, 0),
                    MaxWidth = 64,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = ThemeService.Brush(ThemeService.Text)
                });

                var tile = new Button
                {
                    Style = (Style)FindResource("CelTile"),
                    Width = 70,
                    Margin = new Thickness(0, 0, 8, 8),
                    ToolTip = item.Tooltip,
                    Content = stack
                };

                tile.Click += (_, _) => RunTile(item);
                tile.MouseRightButtonUp += (_, e) => { ShowTileMenu(item); e.Handled = true; };

                TileHost.Children.Add(tile);
            }

            LaunchEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LaunchHint.Text = items.Count == 0 ? "" : items.Count + " 项";
        }

        /// <summary>A real shell icon when one can be read, otherwise a flat letter chip.</summary>
        private FrameworkElement BuildIcon(LauncherItem item)
        {
            string? look = null;
            if (!string.IsNullOrWhiteSpace(item.IconPath)) look = item.IconPath;
            else if (item.Kind != LauncherKind.Command) look = item.Target;

            var source = IconFactory.FileIcon(look);
            if (source != null)
            {
                return new Image
                {
                    Source = source,
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }

            var accent = string.IsNullOrWhiteSpace(item.Accent)
                ? ThemeService.Accent
                : ThemeService.Parse(item.Accent, ThemeService.Accent);

            var initial = string.IsNullOrWhiteSpace(item.Name)
                ? item.Kind == LauncherKind.Command ? ">" : "?"
                : item.Name.Trim().Substring(0, 1).ToUpperInvariant();

            var chip = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(9),
                Background = ThemeService.Brush(accent),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            chip.Child = new TextBlock
            {
                Text = initial,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return chip;
        }

        private void RunTile(LauncherItem item)
        {
            var error = LaunchService.Run(item);
            Hint(string.IsNullOrEmpty(error)
                ? "已启动 " + (string.IsNullOrWhiteSpace(item.Name) ? item.KindLabel : item.Name)
                : "启动失败: " + error,
                warn: !string.IsNullOrEmpty(error));
        }

        private void ShowTileMenu(LauncherItem item)
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("启动", () => RunTile(item)));
            menu.Items.Add(Item("编辑…", () => EditLauncher(item)));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("上移", () => MoveLauncher(item, -1)));
            menu.Items.Add(Item("下移", () => MoveLauncher(item, +1)));
            menu.Items.Add(Item("打开所在位置", () => OpenContaining(item)));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("删除", () => RemoveLauncher(item)));
            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private static void OpenContaining(LauncherItem item)
        {
            string? dir = item.Kind == LauncherKind.Folder
                ? item.Target
                : System.IO.Path.GetDirectoryName(item.Target);
            if (string.IsNullOrWhiteSpace(dir)) return;
            LaunchService.Run(new LauncherItem { Kind = LauncherKind.Folder, Target = dir! });
        }

        private void ShowAddMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("添加程序 / 快捷方式…",
                () => EditLauncher(new LauncherItem { Kind = LauncherKind.App })));
            menu.Items.Add(Item("添加命令启动（cmd + 目录）…",
                () => EditLauncher(new LauncherItem
                {
                    Kind = LauncherKind.Command,
                    Terminal = "cmd",
                    KeepOpen = true,
                    WorkingDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                })));
            menu.Items.Add(Item("添加文件夹…", () => EditLauncher(new LauncherItem { Kind = LauncherKind.Folder })));
            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private void EditLauncher(LauncherItem item)
        {
            _app.Config.Launchers ??= new List<LauncherItem>();
            bool isNew = !_app.Config.Launchers.Contains(item);

            var dlg = new LauncherEditWindow(item) { Owner = this, Topmost = true };
            if (dlg.ShowDialog() != true) return;

            if (isNew) _app.Config.Launchers.Add(item);
            _app.Save();
            BuildTiles();
        }

        private void MoveLauncher(LauncherItem item, int delta)
        {
            var list = _app.Config.Launchers;
            int i = list.IndexOf(item);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count) return;
            list.RemoveAt(i);
            list.Insert(j, item);
            _app.Save();
            BuildTiles();
        }

        private void RemoveLauncher(LauncherItem item)
        {
            _app.Config.Launchers.Remove(item);
            _app.Save();
            BuildTiles();
        }

        // ----------------------------------------------------------------- usage
        private void UpdateUsage()
        {
            try
            {
                if (!CcSwitchUsage.IsRunning())
                {
                    UsageOffline.Visibility = Visibility.Visible;
                    UsageBars.Children.Clear();
                    LiveDot.Fill = ThemeService.Brush(ThemeService.Muted);
                    UsageCost.Text = "$0.00";
                    UsageReqs.Text = "";
                    UsageTokens.Text = "";
                    UsageUpdated.Text = "未运行";
                    return;
                }

                var rows = CcSwitchUsage.Day(DateTime.Today);
                decimal cost = rows.Sum(r => r.Cost);
                int requests = rows.Sum(r => r.Requests);
                long input = rows.Sum(r => r.Input);
                long output = rows.Sum(r => r.Output);
                long cache = rows.Sum(r => r.CacheRead);

                UsageOffline.Visibility = Visibility.Collapsed;
                LiveDot.Fill = ThemeService.Brush(ThemeService.Accent2);
                UsageCost.Text = "$" + cost.ToString("0.00", CultureInfo.InvariantCulture);
                UsageReqs.Text = requests + " 次";
                UsageTokens.Text = "输入 " + CcSwitchUsage.FormatTokens(input) +
                                   " · 输出 " + CcSwitchUsage.FormatTokens(output) +
                                   " · 缓存 " + CcSwitchUsage.FormatTokens(cache);
                UsageUpdated.Text = DateTime.Now.ToString("HH:mm:ss");

                UsageBars.Children.Clear();
                double total = Math.Max(0.0001, (double)cost);
                foreach (var row in rows.Take(4))
                    UsageBars.Children.Add(BuildBar(row.App, (double)row.Cost, row.Requests, total));

                if (rows.Count == 0)
                {
                    UsageBars.Children.Add(new TextBlock
                    {
                        Text = "今天还没有请求",
                        FontSize = 11,
                        Foreground = ThemeService.Brush(ThemeService.Muted)
                    });
                }
            }
            catch (Exception ex)
            {
                App.Log(ex);
            }
        }

        private FrameworkElement BuildBar(string name, double cost, int requests, double total)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var track = new Border
            {
                Height = 17,
                CornerRadius = new CornerRadius(7),
                Background = ThemeService.Brush(ThemeService.WithAlpha(ThemeService.Item, 0.28)),
                ClipToBounds = true
            };

            var fill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(7),
                Background = ThemeService.Brush(ThemeService.Accent),
                Width = 0
            };
            track.Child = fill;
            track.SizeChanged += (_, _) =>
                fill.Width = Math.Max(6, track.ActualWidth * Math.Min(1.0, cost / total));

            var inner = new Grid();
            inner.Children.Add(track);
            inner.Children.Add(new TextBlock
            {
                Text = name,
                Margin = new Thickness(9, 0, 8, 0),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeService.Brush(ThemeService.Text)
            });
            row.Children.Add(inner);

            var value = new TextBlock
            {
                Text = "$" + cost.ToString("0.00", CultureInfo.InvariantCulture) + " · " + requests,
                FontSize = 10.5,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeService.Brush(ThemeService.Muted)
            };
            Grid.SetColumn(value, 1);
            row.Children.Add(value);

            return row;
        }

        // ------------------------------------------------------------------ todo
        public void BuildTodos()
        {
            TodoList.Children.Clear();
            var todos = _app.Config.Todos ??= new List<TodoItem>();
            TodoCount.Text = todos.Count == 0 ? "" : todos.Count(t => !t.Done) + " / " + todos.Count;

            foreach (var todo in todos)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new Button
                {
                    Style = (Style)FindResource("CelCheck"),
                    Content = todo.Done ? "\uE73E" : ""
                };
                check.Click += (_, _) =>
                {
                    todo.Done = !todo.Done;
                    _app.Save();
                    BuildTodos();
                };
                row.Children.Add(check);

                var text = new TextBlock
                {
                    Text = todo.Text,
                    FontSize = 11.5,
                    Margin = new Thickness(8, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = ThemeService.Brush(todo.Done ? ThemeService.Muted : ThemeService.Text),
                    TextDecorations = todo.Done ? TextDecorations.Strikethrough : null
                };
                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                var del = new Button
                {
                    Style = (Style)FindResource("CelIconButton"),
                    Content = "\uE711",
                    FontSize = 10,
                    Width = 24,
                    Height = 24,
                    Opacity = 0.7
                };
                del.Click += (_, _) =>
                {
                    _app.Config.Todos.Remove(todo);
                    _app.Save();
                    BuildTodos();
                };
                Grid.SetColumn(del, 2);
                row.Children.Add(del);

                TodoList.Children.Add(row);
            }
        }

        private void AddTodo()
        {
            var text = TodoInput.Text.Trim();
            if (text.Length == 0) return;

            _app.Config.Todos ??= new List<TodoItem>();
            _app.Config.Todos.Add(new TodoItem { Text = text });
            TodoInput.Text = "";
            _app.Save();
            BuildTodos();
            Hint("已添加待办");
        }

        // ------------------------------------------------------------------ note
        public void BuildNotes()
        {
            NoteList.Children.Clear();
            var notes = _app.Config.Notes ??= new List<NoteItem>();
            NoteEmpty.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var note in notes)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var dot = new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = ThemeService.Brush(ThemeService.Accent2)
                };
                row.Children.Add(dot);

                var stack = new StackPanel { Margin = new Thickness(9, 0, 6, 0) };
                stack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(note.Text) ? "(空便签)" : note.Text,
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = ThemeService.Brush(ThemeService.Text)
                });
                stack.Children.Add(new TextBlock
                {
                    Text = NoteCountdown(note),
                    FontSize = 10,
                    Foreground = ThemeService.Brush(ThemeService.Muted)
                });
                Grid.SetColumn(stack, 1);
                row.Children.Add(stack);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal };
                var show = new Button
                {
                    Style = (Style)FindResource("CelIconButton"),
                    Content = "\uE8A7",
                    FontSize = 10,
                    Width = 24,
                    Height = 24,
                    Opacity = 0.8
                };
                show.Click += (_, _) => _app.ShowNoteWindow(note);
                buttons.Children.Add(show);

                var del = new Button
                {
                    Style = (Style)FindResource("CelIconButton"),
                    Content = "\uE711",
                    FontSize = 10,
                    Width = 24,
                    Height = 24,
                    Opacity = 0.7
                };
                del.Click += (_, _) =>
                {
                    _app.HideNoteWindow(note);
                    _app.Config.Notes.Remove(note);
                    _app.Save();
                    BuildNotes();
                };
                buttons.Children.Add(del);

                Grid.SetColumn(buttons, 2);
                row.Children.Add(buttons);

                NoteList.Children.Add(row);
            }
        }

        private static string NoteCountdown(NoteItem note)
        {
            var expiry = note.Expiry;
            if (expiry == null) return "永久";
            var left = expiry.Value - DateTime.Now;
            if (left <= TimeSpan.Zero) return "即将消失";
            if (left.TotalHours >= 1) return $"{(int)left.TotalHours}h {left.Minutes}m 后消失";
            if (left.TotalMinutes >= 1) return $"{(int)left.TotalMinutes}m 后消失";
            return $"{left.Seconds}s 后消失";
        }

        private void AddNote()
        {
            _app.Config.Notes ??= new List<NoteItem>();
            var note = new NoteItem
            {
                Text = "新便签",
                ExpiresAt = DateTime.Now.AddHours(1).ToString("o")
            };
            _app.Config.Notes.Add(note);
            _app.Save();
            BuildNotes();
            _app.ShowNoteWindow(note);
        }

        // ----------------------------------------------------- drag / resize / fx
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            _dragging = true;
            _dragOrigin = PointToScreen(e.GetPosition(this));
            _dragLeft = Left;
            _dragTop = Top;
            HeaderBar.CaptureMouse();
            e.Handled = true;
        }

        private void OnHeaderMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var now = PointToScreen(e.GetPosition(this));
            Left = _dragLeft + (now.X - _dragOrigin.X);
            Top = _dragTop + (now.Y - _dragOrigin.Y);
        }

        /// <summary>Drops the dock onto whichever of the four edges it was let go nearest to.</summary>
        private void OnHeaderRelease(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            HeaderBar.ReleaseMouseCapture();

            var wa = SystemParameters.WorkArea;
            double cx = Left + Width / 2, cy = Top + Height / 2;
            double dLeft = cx - wa.Left, dRight = wa.Right - cx;
            double dTop = cy - wa.Top, dBottom = wa.Bottom - cy;

            double best = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
            var edge = Math.Abs(best - dLeft) < 0.001 ? Edge.Left
                : Math.Abs(best - dRight) < 0.001 ? Edge.Right
                : Math.Abs(best - dTop) < 0.001 ? Edge.Top
                : Edge.Bottom;

            _app.Config.DockEdge = edge.ToString();
            _app.Config.DockFullHeight = false;

            if (edge == Edge.Left || edge == Edge.Right)
            {
                _app.Config.DockY = Top;
                _app.Config.DockSpan = Height;
            }
            else
            {
                _app.Config.DockX = Left;
                _app.Config.DockSpan = Width;
            }

            _app.Save();
            ApplyGeometry(animate: true);
        }

        private void OnResizeStart(object sender, MouseButtonEventArgs e)
        {
            _resizing = true;
            _resizeCross = VerticalSpan ? Width : Height;
            _dragOrigin = PointToScreen(e.GetPosition(this));
            GripLeft.CaptureMouse();
            e.Handled = true;
        }

        private void OnResizeMove(object sender, MouseEventArgs e)
        {
            if (!_resizing) return;

            var now = PointToScreen(e.GetPosition(this));
            var wa = SystemParameters.WorkArea;

            double delta = VerticalSpan
                ? CurrentEdge == Edge.Left ? now.X - _dragOrigin.X : _dragOrigin.X - now.X
                : CurrentEdge == Edge.Top ? now.Y - _dragOrigin.Y : _dragOrigin.Y - now.Y;

            double cross = Math.Clamp(_resizeCross + delta, VerticalSpan ? 240 : 150, 640);

            if (VerticalSpan)
            {
                Width = cross;
                Left = CurrentEdge == Edge.Right ? wa.Right - cross - EdgePad : wa.Left + EdgePad;
            }
            else
            {
                Height = cross;
                Top = CurrentEdge == Edge.Bottom ? wa.Bottom - cross - EdgePad : wa.Top + EdgePad;
            }
        }

        private void OnResizeEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_resizing) return;
            _resizing = false;
            GripLeft.ReleaseMouseCapture();

            if (VerticalSpan) _app.Config.DockWidth = Width;
            else _app.Config.DockHeight = Height;
            _app.Save();

            _backdrop?.Refresh();
        }

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            bool show = !_collapsed;

            Sections.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Scroller.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            FooterBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            HeaderSub.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            AddMenuBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DockSettingsBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CollapseBtn.Content = show ? "\uE76B" : "\uE76C";

            ApplyGeometry(animate: true);
        }

        // ------------------------------------------------------------- auto-hide
        public void ApplyAutoHide()
        {
            _hideTimer.Stop();
            _autoHidden = false;
            ApplyGeometry(animate: true);
            if (_app.Config.DockAutoHide && !IsMouseOver) HideNow();
            RefreshFooter();
        }

        private void ClearAnimations()
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
        }

        /// <summary>
        /// Moves the dock without leaving a holding animation behind: the base values are assigned
        /// straight away and the animation runs from the old values with <c>FillBehavior.Stop</c>.
        /// </summary>
        private void MoveTo(double left, double top, int ms, EasingMode mode)
        {
            double fromLeft = Left, fromTop = Top;
            bool animateLeft = ms > 0 && Math.Abs(fromLeft - left) > 1.0;
            bool animateTop = ms > 0 && Math.Abs(fromTop - top) > 1.0;

            ClearAnimations();
            Left = left;
            Top = top;

            if (!animateLeft && !animateTop)
            {
                _backdrop?.Refresh();
                return;
            }

            var ease = new CubicEase { EasingMode = mode };
            if (animateLeft)
                BeginAnimation(LeftProperty,
                    new DoubleAnimation(fromLeft, left, TimeSpan.FromMilliseconds(ms))
                    { FillBehavior = FillBehavior.Stop, EasingFunction = ease });
            if (animateTop)
                BeginAnimation(TopProperty,
                    new DoubleAnimation(fromTop, top, TimeSpan.FromMilliseconds(ms))
                    { FillBehavior = FillBehavior.Stop, EasingFunction = ease });

            var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms + 40) };
            settle.Tick += (_, _) => { settle.Stop(); _backdrop?.Refresh(); };
            settle.Start();
        }

        private void SetHiddenPosition(bool animate)
        {
            var b = ShownBounds();
            double coord = HiddenCoordinate();

            // keep the base position correct so a later reveal animates from the right place
            double left = VerticalSpan ? coord : b.Left;
            double top = VerticalSpan ? b.Top : coord;
            MoveTo(left, top, animate ? 240 : 0, EasingMode.EaseIn);
        }

        private void OnPointerEnter()
        {
            _hideTimer.Stop();
            if (_autoHidden) Reveal(animate: true);
            RefreshFooter();
        }

        private void OnPointerLeave()
        {
            if (!_app.Config.DockAutoHide || _dragging || _resizing) return;
            _hideTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.4, _app.Config.DockAutoHideDelay));
            _hideTimer.Stop();
            _hideTimer.Start();
            RefreshFooter();
        }

        private void Reveal(bool animate)
        {
            _autoHidden = false;
            var b = ShownBounds();
            MoveTo(b.Left, b.Top, animate ? 220 : 0, EasingMode.EaseOut);
        }

        private void HideNow(bool animate = true)
        {
            if (!_app.Config.DockAutoHide || _collapsed || _dragging || _resizing) return;
            _hideTimer.Stop();
            _autoHidden = true;
            SetHiddenPosition(animate);
        }

        /// <summary>
        /// Watches for the pointer bumping the screen edge the dock hides behind - the dock then
        /// slides back even if it had retreated completely out of sight.
        /// </summary>
        private void BumpTick()
        {
            if (!_autoHidden || !_app.Config.DockAutoHide || !_app.Config.DockBumpToReveal) return;
            if (_dragging || _resizing) return;

            if (PointerInEdgeZone(3)) Reveal(animate: true);
        }

        /// <summary>
        /// True while the pointer is inside the strip of screen along the docked edge that the dock
        /// owns. Revealing puts the panel a small gap away from the edge, so without this the panel
        /// would hide again the instant the pointer crossed that gap.
        /// </summary>
        private bool PointerInEdgeZone(double zone)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                var cursor = System.Windows.Forms.Cursor.Position;
                double x = cursor.X / dpi.DpiScaleX;
                double y = cursor.Y / dpi.DpiScaleY;

                var wa = SystemParameters.WorkArea;
                var b = ShownBounds();
                const double slack = 40;

                return CurrentEdge switch
                {
                    Edge.Left => x <= wa.Left + zone && y >= b.Top - slack && y <= b.Bottom + slack,
                    Edge.Right => x >= wa.Right - zone && y >= b.Top - slack && y <= b.Bottom + slack,
                    Edge.Top => y <= wa.Top + zone && x >= b.Left - slack && x <= b.Right + slack,
                    _ => y >= wa.Bottom - zone && x >= b.Left - slack && x <= b.Right + slack
                };
            }
            catch
            {
                return false;
            }
        }

        public bool IsAutoHidden => _autoHidden;

        // ----------------------------------------------------------- misc / menu
        private void OnBackgroundMenu(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;

            var menu = new ContextMenu();
            menu.Items.Add(Item("添加启动项…", ShowAddMenu));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("设置…", () => _app.ShowSettings()));
            menu.Items.Add(Item("Aurora 中心…", () => _app.ShowDashboard()));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("刷新用量", () => { UpdateUsage(); Hint("用量已刷新"); }));
            menu.Items.Add(EdgeMenu());
            menu.Items.Add(DelayMenu());
            menu.Items.Add(Item(
                _app.Config.DockBumpToReveal ? "关闭边缘撞击唤醒" : "开启边缘撞击唤醒",
                () =>
                {
                    _app.Config.DockBumpToReveal = !_app.Config.DockBumpToReveal;
                    _app.Save();
                    Hint(_app.Config.DockBumpToReveal ? "边缘撞击唤醒：开" : "边缘撞击唤醒：关");
                }));
            menu.Items.Add(Item(FillLabel, ToggleFullHeight));
            menu.Items.Add(Item(_app.Config.DockAutoHide ? "关闭自动隐藏" : "开启自动隐藏", () =>
            {
                _app.Config.DockAutoHide = !_app.Config.DockAutoHide;
                _app.Save();
                ApplyAutoHide();
            }));
            menu.Items.Add(Item($"隐藏等待 {_app.Config.DockAutoHideDelay:0.#} 秒…", () =>
                _app.ShowSettings()));
            menu.Items.Add(Item(_collapsed ? "展开盒子" : "折叠为小条", ToggleCollapse));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("隐藏盒子", () => _app.ToggleDock()));

            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private string FillLabel => VerticalSpan
            ? _app.Config.DockFullHeight ? "取消铺满高度" : "铺满上下高度"
            : _app.Config.DockFullHeight ? "取消铺满宽度" : "铺满左右宽度";

        /// <summary>Dock to any of the four screen edges.</summary>
        private MenuItem EdgeMenu()
        {
            var root = new MenuItem { Header = "停靠位置  Dock edge" };
            var edge = CurrentEdge;

            foreach (var (label, value) in new (string, Edge)[]
            {
                ("靠右  Right", Edge.Right),
                ("靠左  Left", Edge.Left),
                ("靠上  Top", Edge.Top),
                ("靠下  Bottom", Edge.Bottom)
            })
            {
                var item = new MenuItem
                {
                    Header = (edge == value ? "● " : "    ") + label,
                    IsChecked = edge == value
                };
                var target = value;
                item.Click += (_, _) =>
                {
                    _app.Config.DockEdge = target.ToString();
                    _app.Config.DockFullHeight = true;
                    _app.Save();
                    ApplyGeometry(animate: true);
                    ApplyAutoHide();
                };
                root.Items.Add(item);
            }
            return root;
        }

        /// <summary>How long the pointer may be away before the dock slips out of sight.</summary>
        private MenuItem DelayMenu()
        {
            var root = new MenuItem { Header = "隐藏等待  Hide delay" };
            double current = _app.Config.DockAutoHideDelay;

            foreach (double seconds in new[] { 0.5, 1.0, 2.0, 3.0, 5.0, 8.0, 15.0 })
            {
                var item = new MenuItem
                {
                    Header = (Math.Abs(current - seconds) < 0.01 ? "● " : "    ") + seconds + " 秒",
                    IsChecked = Math.Abs(current - seconds) < 0.01
                };
                var value = seconds;
                item.Click += (_, _) =>
                {
                    _app.Config.DockAutoHideDelay = value;
                    _app.Save();
                    ApplyAutoHide();
                };
                root.Items.Add(item);
            }
            return root;
        }

        private void ToggleFullHeight()
        {
            _app.Config.DockFullHeight = !_app.Config.DockFullHeight;
            _app.Save();
            ApplyGeometry(animate: true);
            _backdrop?.Refresh();
        }

        private static MenuItem Item(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            return item;
        }

        public void RefreshFooter()
        {
            FooterText.Foreground = ThemeService.Brush(ThemeService.Muted);
            if (_app.Config.DockAutoHide)
                FooterText.Text = $"移开 {_app.Config.DockAutoHideDelay:0.#} 秒后自动隐藏 · 靠边撞击唤醒";
            else
                FooterText.Text = "右键更多 · " + _app.LabelDock + " 显示/隐藏";
        }

        private void Hint(string text, bool warn = false)
        {
            FooterText.Foreground = ThemeService.Brush(warn ? ThemeService.Warn : ThemeService.Text);
            FooterText.Text = text;
            _hintTimer.Stop();
            _hintTimer.Start();
        }

        /// <summary>Called by the controller after the palette or the data changed.</summary>
        public void Reload()
        {
            ApplyLook();
            BuildTiles();
            BuildTodos();
            BuildNotes();
            UpdateUsage();
            ApplyGeometry(animate: false);
            RefreshFooter();
        }
    }
}
