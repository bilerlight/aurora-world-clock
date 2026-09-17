using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AuroraClock.Services
{
    /// <summary>Draws the app / tray icon at runtime so no .ico asset is required.</summary>
    public static class IconFactory
    {
        private static Icon? _cached;

        public static Icon AppIcon(int size = 32)
        {
            if (_cached != null) return _cached;

            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var pad = size * 0.06f;
                var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);

                using (var bg = new LinearGradientBrush(rect,
                           Color.FromArgb(255, 90, 150, 235),
                           Color.FromArgb(255, 40, 70, 130),
                           LinearGradientMode.ForwardDiagonal))
                {
                    g.FillEllipse(bg, rect);
                }

                var cx = size / 2f;
                var cy = size / 2f;
                var r = (size - pad * 2) / 2f;

                using (var ring = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1f, size * 0.045f)))
                    g.DrawEllipse(ring, cx - r * 0.72f, cy - r * 0.72f, r * 1.44f, r * 1.44f);

                using var handPen = new Pen(Color.White, Math.Max(1.5f, size * 0.075f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                // hour hand -> 10 o'clock, minute hand -> 2 o'clock
                g.DrawLine(handPen, cx, cy, cx - r * 0.38f, cy - r * 0.30f);
                g.DrawLine(handPen, cx, cy, cx + r * 0.42f, cy - r * 0.42f);

                using (var center = new SolidBrush(Color.White))
                    g.FillEllipse(center, cx - size * 0.05f, cy - size * 0.05f, size * 0.10f, size * 0.10f);
            }

            var hIcon = bmp.GetHicon();
            using var tmp = Icon.FromHandle(hIcon);
            _cached = (Icon)tmp.Clone();
            return _cached;
        }

        private static readonly System.Collections.Generic.Dictionary<string, System.Windows.Media.ImageSource?> IconCache = new();

        /// <summary>
        /// Pulls the real shell icon out of an executable / shortcut / document so launcher tiles
        /// look like the ones on the desktop. Returns null when the file cannot provide one.
        /// </summary>
        public static System.Windows.Media.ImageSource? FileIcon(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var key = path!.Trim().Trim('"');
            if (IconCache.TryGetValue(key, out var cached)) return cached;

            System.Windows.Media.ImageSource? result = null;
            try
            {
                var target = Environment.ExpandEnvironmentVariables(key);
                if (System.IO.File.Exists(target))
                {
                    using var ico = Icon.ExtractAssociatedIcon(target);
                    if (ico != null)
                    {
                        var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            ico.Handle,
                            System.Windows.Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        result = src;
                    }
                }
            }
            catch
            {
                // a folder, a shell alias such as "code", or a broken path - the dock falls back to a letter chip
            }

            IconCache[key] = result;
            return result;
        }
    }
}
