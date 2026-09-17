using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Standalone floating clock widgets. Off by default: the dock box shows the clocks, so the
        /// free-floating dials are only for people who want them on top of everything.
        /// </summary>
        public bool ShowFloatingClocks { get; set; }

        public bool SmoothSecondHand { get; set; } = true;

        public bool SnapToEdges { get; set; } = true;

        public double Opacity { get; set; } = 1.0;

        public double GlassTint { get; set; } = 0.60;

        public WidgetShape DefaultShape { get; set; } = WidgetShape.Circle;

        public double DefaultSize { get; set; } = 210;

        public List<ClockItem> Clocks { get; set; } = new();

        // ---- extras -------------------------------------------------------
        public List<Reminder> Reminders { get; set; } = new();
        public List<NoteItem> Notes { get; set; } = new();
        public List<TodoItem> Todos { get; set; } = new();

        public bool ShowUsageWidget { get; set; } = true;
        public double UsageX { get; set; } = double.NaN;
        public double UsageY { get; set; } = double.NaN;

        // ---- right-hand dock box ------------------------------------------
        public bool DockEnabled { get; set; } = true;

        public bool DockFullHeight { get; set; } = true;

        public bool DockAutoHide { get; set; }

        public double DockWidth { get; set; } = 344;

        public double DockX { get; set; } = double.NaN;

        public double DockY { get; set; } = double.NaN;

        public double DockOpacity { get; set; } = 1.0;

        /// <summary>Frost the screen behind the dock (capture + blur).</summary>
        public bool FrostOnDock { get; set; } = true;

        /// <summary>Which screen edge the dock sticks to: Right, Left, Top, Bottom.</summary>
        public string DockEdge { get; set; } = "Right";

        /// <summary>Thickness when docked to the top or bottom edge.</summary>
        public double DockHeight { get; set; } = 300;

        /// <summary>Length along the docked edge when not filling it. NaN = pick a sensible one.</summary>
        public double DockSpan { get; set; } = double.NaN;

        /// <summary>Seconds to wait after the pointer leaves before sliding away.</summary>
        public double DockAutoHideDelay { get; set; } = 2.5;

        /// <summary>Reveal the dock when the pointer bumps the screen edge it hides behind.</summary>
        public bool DockBumpToReveal { get; set; } = true;

        /// <summary>
        /// Pixels of the dock left on screen while it is hidden. 0 = hidden completely
        /// (the edge-bump watcher still brings it back).
        /// </summary>
        public double DockHidePeek { get; set; }

        /// <summary>Ordered section ids: clock, launcher, usage, todo, note.</summary>
        public List<string> DockSections { get; set; } = new() { "clock", "launcher", "usage", "todo", "note" };

        public List<LauncherItem> Launchers { get; set; } = new();

        // ---- look ---------------------------------------------------------
        public ThemePalette Theme { get; set; } = new();
    }

    public static class ConfigService
    {
        /// <summary>
        /// Positions are stored as NaN until they are first laid out. System.Text.Json refuses to
        /// write NaN and cannot read null into a double, so both are normalised here.
        /// </summary>
        private sealed class LooseDoubleConverter : JsonConverter<double>
        {
            public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        return double.NaN;
                    case JsonTokenType.Number:
                        return reader.GetDouble();
                    case JsonTokenType.String:
                        return double.TryParse(reader.GetString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
                    default:
                        return double.NaN;
                }
            }

            public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) writer.WriteNullValue();
                else writer.WriteNumberValue(value);
            }
        }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(), new LooseDoubleConverter() }
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
