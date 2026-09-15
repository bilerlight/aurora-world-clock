using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AuroraClock.Services
{
    /// <summary>
    /// Renders a real frosted backdrop by grabbing the screen area behind the widget and blurring it.
    /// This is the only way to get a *shaped* (circular) frosted-glass widget on Windows: the DWM
    /// acrylic backdrop always fills the whole window rectangle and cannot be region-clipped.
    ///
    /// The widget window is marked WDA_EXCLUDEFROMCAPTURE so it never captures itself.
    /// </summary>
    public sealed class BackdropService : IDisposable
    {
        private const int WDA_NONE = 0x00000000;
        private const int WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, int affinity);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private readonly Window _window;
        private readonly Action<BitmapSource> _onFrame;
        private readonly DispatcherTimer _timer;
        private readonly bool _excludeFromCapture;
        private bool _busy;
        private bool _excluded;

        /// <summary>Downscale factor for the capture - small = fast, and the blur hides the loss.</summary>
        private const int Downscale = 5;

        public BackdropService(Window window, Action<BitmapSource> onFrame,
            bool excludeFromCapture = true, int intervalMs = 110)
        {
            _window = window;
            _onFrame = onFrame;
            _excludeFromCapture = excludeFromCapture;
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _timer.Tick += (_, _) => Refresh();
        }

        public void Start()
        {
            // Keep our own widget out of the capture so the blur doesn't feed back on itself.
            var hwnd = new WindowInteropHelper(_window).Handle;
            if (_excludeFromCapture && hwnd != IntPtr.Zero)
                _excluded = SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);

            _timer.Start();
            Refresh();
        }

        public void Stop() => _timer.Stop();

        /// <summary>Force an immediate re-capture (used after the widget moves).</summary>
        public void Refresh()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var frame = Capture();
                if (frame != null) _onFrame(frame);
            }
            catch
            {
                // Capture can fail while the desktop is switching/locking; keep the last frame.
            }
            finally
            {
                _busy = false;
            }
        }

        private BitmapSource? Capture()
        {
            double w = _window.ActualWidth, h = _window.ActualHeight;
            if (w < 8 || h < 8 || !_window.IsVisible) return null;

            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_window);
            int px = (int)Math.Round(_window.Left * dpi.DpiScaleX);
            int py = (int)Math.Round(_window.Top * dpi.DpiScaleY);
            int pw = (int)Math.Round(w * dpi.DpiScaleX);
            int ph = (int)Math.Round(h * dpi.DpiScaleY);
            if (pw < 8 || ph < 8) return null;

            int sw = Math.Max(6, pw / Downscale);
            int sh = Math.Max(6, ph / Downscale);

            using var full = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(full))
            {
                g.CopyFromScreen(px, py, 0, 0, new System.Drawing.Size(pw, ph), CopyPixelOperation.SourceCopy);
            }

            using var small = new Bitmap(sw, sh, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(full, new Rectangle(0, 0, sw, sh));
            }

            Frost(small);
            return ToBitmapSource(small);
        }

        /// <summary>Separable box blur (x3 ≈ Gaussian) plus a touch of desaturation and lift - the "frost".</summary>
        private static void Frost(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int count = w * h;
                var buf = new int[count];
                Marshal.Copy(data.Scan0, buf, 0, count);

                int radius = Math.Max(3, Math.Min(w, h) / 8);
                for (int pass = 0; pass < 3; pass++)
                {
                    BoxBlur(buf, w, h, radius, horizontal: true);
                    BoxBlur(buf, w, h, radius, horizontal: false);
                }

                for (int i = 0; i < count; i++)
                {
                    int c = buf[i];
                    int b = c & 0xFF, g = (c >> 8) & 0xFF, r = (c >> 16) & 0xFF;

                    // pull ~18% toward luminance: frosted glass is never as saturated as the source
                    int lum = (r * 77 + g * 151 + b * 28) >> 8;
                    r += (lum - r) * 18 / 100;
                    g += (lum - g) * 18 / 100;
                    b += (lum - b) * 18 / 100;

                    // gentle lift so dark backgrounds still read as glass
                    r = Math.Min(255, r + 8);
                    g = Math.Min(255, g + 8);
                    b = Math.Min(255, b + 8);

                    buf[i] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
                }

                Marshal.Copy(buf, 0, data.Scan0, count);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static void BoxBlur(int[] buf, int w, int h, int r, bool horizontal)
        {
            if (horizontal)
            {
                var line = new int[w];
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    Array.Copy(buf, row, line, 0, w);
                    for (int x = 0; x < w; x++)
                    {
                        int b = 0, g = 0, rr = 0, n = 0;
                        for (int k = -r; k <= r; k++)
                        {
                            int xx = x + k;
                            if (xx < 0) xx = 0; else if (xx >= w) xx = w - 1;
                            int c = line[xx];
                            b += c & 0xFF; g += (c >> 8) & 0xFF; rr += (c >> 16) & 0xFF;
                            n++;
                        }
                        buf[row + x] = unchecked((int)0xFF000000) | ((rr / n) << 16) | ((g / n) << 8) | (b / n);
                    }
                }
            }
            else
            {
                var col = new int[h];
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++) col[y] = buf[y * w + x];
                    for (int y = 0; y < h; y++)
                    {
                        int b = 0, g = 0, rr = 0, n = 0;
                        for (int k = -r; k <= r; k++)
                        {
                            int yy = y + k;
                            if (yy < 0) yy = 0; else if (yy >= h) yy = h - 1;
                            int c = col[yy];
                            b += c & 0xFF; g += (c >> 8) & 0xFF; rr += (c >> 16) & 0xFF;
                            n++;
                        }
                        buf[y * w + x] = unchecked((int)0xFF000000) | ((rr / n) << 16) | ((g / n) << 8) | (b / n);
                    }
                }
            }
        }

        private static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            IntPtr hb = bmp.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                DeleteObject(hb);
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            var hwnd = new WindowInteropHelper(_window).Handle;
            if (_excluded && hwnd != IntPtr.Zero)
                SetWindowDisplayAffinity(hwnd, WDA_NONE);
        }
    }
}
