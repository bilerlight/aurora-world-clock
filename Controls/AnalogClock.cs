using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AuroraClock.Controls
{
    /// <summary>
    /// A fully code-rendered analog clock face. It draws the dial, ticks, numerals,
    /// sweeping hands and (optionally) a digital readout + city label.
    /// </summary>
    public sealed class AnalogClock : FrameworkElement
    {
        // ---------------------------------------------------------------- DPs
        public static readonly DependencyProperty TimeZoneIdProperty = Reg<string>(nameof(TimeZoneId), null!);
        public static readonly DependencyProperty CityProperty = Reg<string>(nameof(City), "");
        public static readonly DependencyProperty ShowDigitalProperty = Reg<bool>(nameof(ShowDigital), true);
        public static readonly DependencyProperty ShowCityProperty = Reg<bool>(nameof(ShowCity), true);
        public static readonly DependencyProperty ShowDateProperty = Reg<bool>(nameof(ShowDate), true);
        public static readonly DependencyProperty ShowNumbersProperty = Reg<bool>(nameof(ShowNumbers), false);
        public static readonly DependencyProperty ShowSecondHandProperty = Reg<bool>(nameof(ShowSecondHand), true);
        public static readonly DependencyProperty SweepSecondsProperty = Reg<bool>(nameof(SweepSeconds), true);
        public static readonly DependencyProperty AccentColorProperty = Reg(nameof(AccentColor), Color.FromRgb(0x7C, 0xC4, 0xFF));
        public static readonly DependencyProperty TextColorProperty = Reg(nameof(TextColor), Color.FromRgb(0xF2, 0xF6, 0xFC));
        public static readonly DependencyProperty IsDimmedProperty = Reg<bool>(nameof(IsDimmed), false);

        private static DependencyProperty Reg<T>(string name, T def)
            => DependencyProperty.Register(name, typeof(T), typeof(AnalogClock),
                new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

        private static DependencyProperty Reg(string name, Color def)
            => DependencyProperty.Register(name, typeof(Color), typeof(AnalogClock),
                new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

        public string? TimeZoneId { get => (string?)GetValue(TimeZoneIdProperty); set => SetValue(TimeZoneIdProperty, value); }
        public string City { get => (string)GetValue(CityProperty); set => SetValue(CityProperty, value); }
        public bool ShowDigital { get => (bool)GetValue(ShowDigitalProperty); set => SetValue(ShowDigitalProperty, value); }
        public bool ShowCity { get => (bool)GetValue(ShowCityProperty); set => SetValue(ShowCityProperty, value); }
        public bool ShowDate { get => (bool)GetValue(ShowDateProperty); set => SetValue(ShowDateProperty, value); }
        public bool ShowNumbers { get => (bool)GetValue(ShowNumbersProperty); set => SetValue(ShowNumbersProperty, value); }
        public bool ShowSecondHand { get => (bool)GetValue(ShowSecondHandProperty); set => SetValue(ShowSecondHandProperty, value); }
        public bool SweepSeconds { get => (bool)GetValue(SweepSecondsProperty); set => SetValue(SweepSecondsProperty, value); }
        public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }
        public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
        public bool IsDimmed { get => (bool)GetValue(IsDimmedProperty); set => SetValue(IsDimmedProperty, value); }

        private static readonly Typeface Face = new(new FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface FaceBold = new(new FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        protected override Size MeasureOverride(Size availableSize)
        {
            var w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
            var h = double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height;
            return new Size(w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 8 || h < 8) return;

            double size = Math.Min(w, h);
            double cx = w / 2, cy = h / 2;
            double r = size / 2 - size * 0.045;
            if (r <= 2) return;

            var tz = Services.TimeZoneCatalog.Resolve(TimeZoneId);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            double sec = now.Second + (SweepSeconds ? now.Millisecond / 1000.0 : 0);
            double min = now.Minute + sec / 60.0;
            double hour = (now.Hour % 12) + min / 60.0;

            double dim = IsDimmed ? 0.55 : 1.0;
            Color text = TextColor;
            Color accent = AccentColor;

            // ---- face ------------------------------------------------------
            var faceBrush = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.28),
                Center = new Point(0.45, 0.42),
                RadiusX = 0.9,
                RadiusY = 0.9
            };
            faceBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * dim), 255, 255, 255), 0.0));
            faceBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(14 * dim), 255, 255, 255), 0.45));
            faceBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(36 * dim), 0, 0, 0), 1.0));
            faceBrush.Freeze();
            if (r > size * 0.30)
                dc.DrawEllipse(faceBrush, null, new Point(cx, cy), r, r);

            // ---- rim -------------------------------------------------------
            var rimPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 * dim), 255, 255, 255)), Math.Max(1, size * 0.008));
            rimPen.Freeze();
            dc.DrawEllipse(null, rimPen, new Point(cx, cy), r - rimPen.Thickness / 2, r - rimPen.Thickness / 2);

            var accentRing = new Pen(new SolidColorBrush(Color.FromArgb((byte)(30 * dim), accent.R, accent.G, accent.B)), Math.Max(1, size * 0.004));
            accentRing.Freeze();
            dc.DrawEllipse(null, accentRing, new Point(cx, cy), r - size * 0.035, r - size * 0.035);

            bool tiny = size < 52;

            // ---- ticks -----------------------------------------------------
            if (!tiny)
            {
                var majorPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(200 * dim), text.R, text.G, text.B)), Math.Max(1, size * 0.009)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var minorPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 * dim), text.R, text.G, text.B)), Math.Max(1, size * 0.004)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                majorPen.Freeze();
                minorPen.Freeze();

                for (int i = 0; i < 60; i++)
                {
                    bool major = i % 5 == 0;
                    double len = major ? size * 0.055 : size * 0.028;
                    double outer = r - size * 0.055;
                    double ang = i * 6.0 * Math.PI / 180.0;
                    double sx = cx + Math.Sin(ang) * outer;
                    double sy = cy - Math.Cos(ang) * outer;
                    double ex = cx + Math.Sin(ang) * (outer - len);
                    double ey = cy - Math.Cos(ang) * (outer - len);
                    dc.DrawLine(major ? majorPen : minorPen, new Point(sx, sy), new Point(ex, ey));
                }

                // ---- numerals ---------------------------------------------
                if (ShowNumbers)
                {
                    double nr = r - size * 0.185;
                    for (int n = 1; n <= 12; n++)
                    {
                        double ang = n * 30.0 * Math.PI / 180.0;
                        var px = cx + Math.Sin(ang) * nr;
                        var py = cy - Math.Cos(ang) * nr;
                        var ft = new FormattedText(n.ToString(CultureInfo.InvariantCulture),
                            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, FaceBold,
                            size * 0.085, new SolidColorBrush(Color.FromArgb((byte)(215 * dim), text.R, text.G, text.B)),
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(ft, new Point(px - ft.Width / 2, py - ft.Height / 2));
                    }
                }
            }

            // ---- hands -----------------------------------------------------
            double handShadow = Math.Max(1.0, size * 0.006);
            DrawHand(dc, cx, cy, hour * 30.0, r * 0.42, size * 0.045, Math.Max(2, size * 0.026),
                new SolidColorBrush(TextColor), handShadow, dim);
            DrawHand(dc, cx, cy, min * 6.0, r * 0.62, size * 0.055, Math.Max(1.5, size * 0.017),
                new SolidColorBrush(TextColor), handShadow, dim);

            if (ShowSecondHand && !tiny)
            {
                DrawHand(dc, cx, cy, sec * 6.0, r * 0.74, size * 0.09, Math.Max(1, size * 0.0075),
                    new SolidColorBrush(accent), handShadow, dim);
            }

            // ---- centre cap ------------------------------------------------
            var capBrush = new SolidColorBrush(accent);
            capBrush.Freeze();
            dc.DrawEllipse(capBrush, null, new Point(cx, cy), size * 0.017, size * 0.017);
            var capRing = new Pen(new SolidColorBrush(Color.FromArgb((byte)(160 * dim), text.R, text.G, text.B)), Math.Max(1, size * 0.004));
            capRing.Freeze();
            dc.DrawEllipse(null, capRing, new Point(cx, cy), size * 0.017, size * 0.017);

            // ---- glass sheen ----------------------------------------------
            var sheen = new LinearGradientBrush(
                Color.FromArgb((byte)(38 * dim), 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255), 90);
            sheen.Freeze();
            var sheenGeo = new StreamGeometry();
            using (var g = sheenGeo.Open())
            {
                g.BeginFigure(new Point(cx - r * 0.94, cy - r * 0.10), true, true);
                g.ArcTo(new Point(cx + r * 0.10, cy - r * 0.94), new Size(r * 0.94, r * 0.94), 0, false, SweepDirection.Clockwise, true, false);
                g.ArcTo(new Point(cx - r * 0.10, cy + r * 0.94), new Size(r * 0.86, r * 0.86), 0, false, SweepDirection.Clockwise, true, false);
                g.ArcTo(new Point(cx - r * 0.94, cy - r * 0.10), new Size(r * 0.52, r * 0.52), 0, false, SweepDirection.Clockwise, true, false);
            }
            sheenGeo.Freeze();
            dc.DrawGeometry(sheen, null, sheenGeo);

            // ---- labels ----------------------------------------------------
            var pixels = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var textBrush = new SolidColorBrush(Color.FromArgb((byte)(250 * dim), text.R, text.G, text.B));
            var subBrush = new SolidColorBrush(Color.FromArgb((byte)(175 * dim), text.R, text.G, text.B));
            var glowBrush = new SolidColorBrush(Color.FromArgb((byte)(150 * dim), 0, 0, 0));
            textBrush.Freeze();
            subBrush.Freeze();
            glowBrush.Freeze();
            double sh = Math.Max(1.0, size * 0.009);

            if (ShowCity && !string.IsNullOrWhiteSpace(City))
            {
                var ft = new FormattedText(City, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    FaceBold, Math.Max(9, size * 0.060), textBrush, pixels);
                ft.MaxTextWidth = r * 1.42;
                ft.TextAlignment = TextAlignment.Center;
                var p = new Point(cx - r * 0.71, cy - r * 0.76);
                ft.SetForegroundBrush(glowBrush);
                dc.DrawText(ft, new Point(p.X + sh, p.Y + sh));
                ft.SetForegroundBrush(textBrush);
                dc.DrawText(ft, p);
            }

            if (ShowDate && !tiny)
            {
                var ft = new FormattedText(now.ToString("ddd · MMM d", CultureInfo.CurrentCulture),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face,
                    Math.Max(7.5, size * 0.041), subBrush, pixels);
                ft.MaxTextWidth = r * 1.42;
                ft.TextAlignment = TextAlignment.Center;
                var p = new Point(cx - r * 0.71, cy - r * 0.555);
                ft.SetForegroundBrush(glowBrush);
                dc.DrawText(ft, new Point(p.X + sh, p.Y + sh));
                ft.SetForegroundBrush(subBrush);
                dc.DrawText(ft, p);
            }

            if (ShowDigital && !tiny)
            {
                string time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
                var ft = new FormattedText(time, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    FaceBold, Math.Max(11, size * 0.150), textBrush, pixels);
                ft.MaxTextWidth = r * 1.6;
                ft.TextAlignment = TextAlignment.Center;

                FormattedText? ftSec = null;
                if (ShowSecondHand)
                {
                    ftSec = new FormattedText(now.ToString("ss", CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture, FlowDirection.LeftToRight, FaceBold,
                        Math.Max(7.5, size * 0.068), new SolidColorBrush(accent), pixels);
                }

                double baseY = cy + r * 0.30;
                double total = ftSec != null ? ft.Width + size * 0.028 + ftSec.Width : ft.Width;
                var p = new Point(cx - total / 2, baseY);

                ft.SetForegroundBrush(glowBrush);
                dc.DrawText(ft, new Point(p.X + sh, p.Y + sh));
                ft.SetForegroundBrush(textBrush);
                dc.DrawText(ft, p);

                if (ftSec != null)
                {
                    var ps = new Point(p.X + ft.Width + size * 0.028, p.Y + ft.Height - ftSec.Height);
                    ftSec.SetForegroundBrush(glowBrush);
                    dc.DrawText(ftSec, new Point(ps.X + sh, ps.Y + sh));
                    ftSec.SetForegroundBrush(new SolidColorBrush(accent));
                    dc.DrawText(ftSec, ps);
                }
            }
        }

        private static void DrawHand(DrawingContext dc, double cx, double cy, double angle,
            double length, double tail, double thickness, SolidColorBrush brush, double shadow, double dim)
        {
            dc.PushTransform(new RotateTransform(angle, cx, cy));

            var shadowBrush = new SolidColorBrush(Color.FromArgb((byte)(70 * dim), 0, 0, 0));
            shadowBrush.Freeze();
            var sp = new Pen(shadowBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            sp.Freeze();
            dc.DrawLine(sp, new Point(cx + shadow, cy + tail + shadow), new Point(cx + shadow, cy - length + shadow));

            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            dc.DrawLine(pen, new Point(cx, cy + tail), new Point(cx, cy - length));

            dc.Pop();
        }
    }
}
