using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AuroraClock.Services
{
    /// <summary>Native window tweaks: click-through, no-activate and top-most banding.</summary>
    public static class WindowFx
    {
        private const int GWL_EXSTYLE = -20;

        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Makes the window ignore the mouse so clicks land on the window underneath.
        /// Requires WS_EX_LAYERED (which WPF already sets for transparent windows).
        /// </summary>
        public static void SetClickThrough(Window window, bool enable)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (enable)
                ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            else
                ex &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

            SetWindowLong(hwnd, GWL_EXSTYLE, ex);
        }

        /// <summary>Hides the window from Alt+Tab / task switcher.</summary>
        public static void HideFromAltTab(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        }
    }
}
