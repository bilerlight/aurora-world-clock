using System;
using Microsoft.Win32;

namespace AuroraClock.Services
{
    /// <summary>Manages the HKCU\...\Run entry that launches the app with Windows.</summary>
    public static class AutostartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AuroraWorldClock";

        public static string ExecutablePath
        {
            get
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return path;
                // Single-file friendly fallback (Assembly.Location is empty when bundled).
                return System.IO.Path.Combine(AppContext.BaseDirectory, "AuroraClock.exe");
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(ValueName) is string s && s.Length > 0;
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                key?.SetValue(ValueName, $"\"{ExecutablePath}\"");
            }
            catch { /* ignored */ }
        }

        public static void Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.DeleteValue(ValueName, false);
            }
            catch { /* ignored */ }
        }

        public static void Apply(bool enabled)
        {
            if (enabled) Enable();
            else Disable();
        }
    }
}
