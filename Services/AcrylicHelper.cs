using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AuroraClock.Services
{
    /// <summary>
    /// Applies the Windows "acrylic" / blur-behind backdrop to a borderless WPF window.
    /// Because the window uses per-pixel alpha (AllowsTransparency), the blur is masked by
    /// the window's alpha channel - so a circular widget gets a circular frosted disc.
    /// </summary>
    public static class AcrylicHelper
    {
        private enum AccentState
        {
            Disabled = 0,
            EnableGradient = 1,
            EnableTransparentGradient = 2,
            EnableBlurBehind = 3,
            EnableAcrylicBlurBehind = 4,
            EnableHostBackdrop = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor; // 0xAABBGGRR
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int WCA_ACCENT_POLICY = 19;

        /// <summary>
        /// Windows' acrylic fills the whole window rectangle, so a circular window would get a
        /// square frosted block behind it. Clipping the window region to the widget silhouette
        /// keeps the blur inside the circle / rounded square.
        /// </summary>
        public static void ApplyShapeRegion(Window window, double widthDip, double heightDip, double radiusDip)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero || widthDip < 2 || heightDip < 2) return;

            var dpi = VisualTreeHelper.GetDpi(window);
            int w = (int)Math.Round(widthDip * dpi.DpiScaleX);
            int h = (int)Math.Round(heightDip * dpi.DpiScaleY);
            int r = (int)Math.Round(radiusDip * 2 * dpi.DpiScaleX);
            r = Math.Max(1, Math.Min(r, Math.Min(w, h)));

            IntPtr region = CreateRoundRectRgn(0, 0, w + 2, h + 2, r, r);
            if (SetWindowRgn(hwnd, region, true) == 0)
                DeleteObject(region);
        }

        private static bool _disabledByUser; // Windows 7 or acrylic unavailable

        /// <summary>
        /// Enables acrylic behind the window. <paramref name="tintA"/> is the alpha (0..1) of the tint,
        /// <paramref name="color"/> the RGB tint colour.
        /// </summary>
        public static void Enable(Window window, Color color, double tintA)
        {
            if (_disabledByUser) return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            byte a = (byte)Math.Clamp(tintA * 255.0, 0, 255);
            // GradientColor is ABGR
            int gradient = (a << 24) | (color.R << 16) | (color.G << 8) | color.B;

            var accent = new AccentPolicy
            {
                AccentState = AccentState.EnableAcrylicBlurBehind,
                AccentFlags = 2,
                GradientColor = gradient,
                AnimationId = 0
            };

            var size = Marshal.SizeOf<AccentPolicy>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                if (SetWindowCompositionAttribute(hwnd, ref data) == 0)
                    _disabledByUser = true;
            }
            catch
            {
                _disabledByUser = true;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>Cheap blur used while dragging (acrylic lags on layered windows).</summary>
        public static void EnableFastBlur(Window window, Color color, double tintA)
        {
            if (_disabledByUser) return;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            byte a = (byte)Math.Clamp(tintA * 255.0, 0, 255);
            int gradient = (a << 24) | (color.R << 16) | (color.G << 8) | color.B;
            var accent = new AccentPolicy
            {
                AccentState = AccentState.EnableBlurBehind,
                AccentFlags = 2,
                GradientColor = gradient,
                AnimationId = 0
            };
            Apply(hwnd, accent);
        }

        public static void Disable(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            Apply(hwnd, new AccentPolicy { AccentState = AccentState.Disabled });
        }

        private static void Apply(IntPtr hwnd, AccentPolicy accent)
        {
            var size = Marshal.SizeOf<AccentPolicy>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            catch { /* ignored */ }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        /// <summary>Darkens/lightens the system "light mode" acrylic; not strictly needed here.</summary>
        public static bool IsSupported => !_disabledByUser;
    }
}
