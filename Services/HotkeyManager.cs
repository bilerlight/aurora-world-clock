using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AuroraClock.Services
{
    /// <summary>System-wide hotkeys, delivered through a hidden message-only window.</summary>
    public sealed class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const int IdClickThrough = 1;
        public const int IdToggleVisible = 2;
        public const int IdSettings = 3;
        public const int IdAlwaysOnTop = 4;
        public const int IdTileAll = 5;

        private readonly Action<int> _onPressed;
        private HwndSource? _source;
        private readonly System.Collections.Generic.List<int> _registered = new();

        public HotkeyManager(Action<int> onPressed) => _onPressed = onPressed;

        public void Start()
        {
            if (_source != null) return;

            var p = new HwndSourceParameters("AuroraClockHotkeySink")
            {
                Width = 0,
                Height = 0,
                PositionX = -4000,
                PositionY = -4000,
                WindowStyle = unchecked((int)0x80000000),               // WS_POPUP, never shown
                ExtendedWindowStyle = 0x00000080 | 0x08000000           // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
            };
            _source = new HwndSource(p);
            _source.AddHook(Hook);
        }

        public bool IsStarted => _source != null && _source.Handle != IntPtr.Zero;

        public bool Register(int id, uint modifiers, uint key)
        {
            if (_source == null) return false;
            if (RegisterHotKey(_source.Handle, id, modifiers | MOD_NOREPEAT, key))
            {
                if (!_registered.Contains(id)) _registered.Add(id);
                return true;
            }
            return false;
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                _onPressed(wParam.ToInt32());
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_source != null)
            {
                foreach (var id in _registered) UnregisterHotKey(_source.Handle, id);
                _registered.Clear();
                _source.RemoveHook(Hook);
                _source.Dispose();
                _source = null;
            }
        }
    }
}
