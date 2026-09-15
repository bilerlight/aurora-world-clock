using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuroraClock.Models;

namespace AuroraClock.Services
{
    public sealed class AppConfig
    {
        /// <summary>Live frosted backdrop (capture + blur behind the widget).</summary>
        public bool BackdropBlur { get; set; } = true;

        /// <summary>Keep the widget out of screen captures - hides it from your own screenshots
        /// too, but gives a slightly cleaner blur (no self-capture feedback).</summary>
        public bool ExcludeFromCapture { get; set; }

        public bool AlwaysOnTop { get; set; } = true;

        public bool StartWithWindows { get; set; }

        /// <summary>When on, mouse clicks fall through the widgets to whatever is behind them.</summary>
        public bool ClickThrough { get; set; }

        /// <summary>Widgets are hidden (still running, controllable from the tray).</summary>
        public bool Hidden { get; set; }

        public bool ShowSeconds { get; set; } = true;

        public bool SmoothSecondHand { get; set; } = true;

        public bool SnapToEdges { get; set; } = true;

        public double Opacity { get; set; } = 1.0;

        public double GlassTint { get; set; } = 0.60;

        public WidgetShape DefaultShape { get; set; } = WidgetShape.Circle;

        public double DefaultSize { get; set; } = 210;

        public List<ClockItem> Clocks { get; set; } = new();
    }

    public static class ConfigService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string Dir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuroraClock");

        public static string FilePath { get; } = Path.Combine(Dir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, Options);
                    if (cfg != null)
                    {
                        foreach (var c in cfg.Clocks)
                        {
                            if (string.IsNullOrWhiteSpace(c.City)) c.City = "Local";
                            if (string.IsNullOrWhiteSpace(c.TimeZoneId)) c.TimeZoneId = TimeZoneInfo.Local.Id;
                        }

                        // Never end up with no widgets at all.
                        if (cfg.Clocks.Count == 0) cfg.Clocks.Add(DefaultLocalClock());

                        return cfg;
                    }
                }
            }
            catch
            {
                // Corrupt config -> start fresh but keep a backup for the curious.
                try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }
            }

            var fresh = new AppConfig();
            fresh.Clocks.Add(DefaultLocalClock());
            return fresh;
        }

        private static ClockItem DefaultLocalClock() => new()
        {
            City = "Local · 本地",
            TimeZoneId = TimeZoneInfo.Local.Id,
            Shape = WidgetShape.Circle,
            Size = 210
        };

        public static void Save(AppConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(cfg, Options);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch
            {
                // never crash the UI because of IO
            }
        }
    }
}
