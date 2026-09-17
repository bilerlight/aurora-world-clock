using System;
using System.Windows;
using System.Windows.Media;
using AuroraClock.Models;

namespace AuroraClock.Services
{
    /// <summary>
    /// Owns the cel-shaded palette and pushes it into the application resources, so every window
    /// (dialogs, tiles, the dock) picks the colours up through <c>DynamicResource</c>.
    /// </summary>
    public static class ThemeService
    {
        public static ThemePalette Current { get; private set; } = new();

        public static void Use(ThemePalette? palette)
            => Current = palette?.Clone() ?? new ThemePalette();

        public static Color Parse(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                var value = hex!.Trim();
                if (value.StartsWith("#", StringComparison.Ordinal))
                    return (Color)ColorConverter.ConvertFromString(value);
            }
            catch { /* fall through to the fallback */ }
            return fallback;
        }

        private static Color C(string? hex, string fallback)
            => Parse(hex, (Color)ColorConverter.ConvertFromString(fallback));

        public static Color Panel => C(Current.Panel, "#E8F9FC");
        public static Color PanelEdge => C(Current.PanelEdge, "#6ECFDA");
        public static Color Item => C(Current.Item, "#F4FEFD");
        public static Color ItemEdge => C(Current.ItemEdge, "#B4E8E6");
        public static Color Text => C(Current.Text, "#1F4B64");
        public static Color Muted => C(Current.Muted, "#5E93A8");
        public static Color Accent => C(Current.Accent, "#17B3CD");
        public static Color Accent2 => C(Current.Accent2, "#3FD2B4");
        public static Color Warn => C(Current.Warn, "#FF8577");

        public static Color WithAlpha(Color c, double alpha)
            => Color.FromArgb((byte)Math.Clamp(alpha * 255.0, 0, 255), c.R, c.G, c.B);

        /// <summary>The card fill, already faded by the configured panel transparency.</summary>
        public static Color PanelFill => WithAlpha(Panel, Current.PanelAlpha);

        public static Color PanelSoftFill => WithAlpha(Panel, Current.PanelAlpha * 0.45);

        public static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Builds the resource set consumed by XAML.</summary>
        public static ResourceDictionary Build()
        {
            var d = new ResourceDictionary();

            d["Cel.Panel"] = PanelFill;
            d["Cel.PanelSoft"] = PanelSoftFill;
            d["Cel.PanelSolid"] = Panel;
            d["Cel.Edge"] = PanelEdge;
            d["Cel.Item"] = WithAlpha(Item, Math.Min(1.0, Current.PanelAlpha + 0.22));
            d["Cel.ItemEdge"] = ItemEdge;
            d["Cel.Text"] = Text;
            d["Cel.Muted"] = Muted;
            d["Cel.Accent"] = Accent;
            d["Cel.Accent2"] = Accent2;
            d["Cel.Warn"] = Warn;
            d["Cel.Shadow"] = WithAlpha(PanelEdge, 0.38);
            d["Cel.Halo"] = WithAlpha(Accent, 0.30);
            d["Cel.Highlight"] = WithAlpha(Colors.White, 0.55);

            d["Cel.PanelBrush"] = Brush(PanelFill);
            d["Cel.PanelSoftBrush"] = Brush(PanelSoftFill);
            d["Cel.EdgeBrush"] = Brush(PanelEdge);
            d["Cel.ItemBrush"] = Brush(WithAlpha(Item, Math.Min(1.0, Current.PanelAlpha + 0.22)));
            d["Cel.ItemEdgeBrush"] = Brush(ItemEdge);
            d["Cel.TextBrush"] = Brush(Text);
            d["Cel.MutedBrush"] = Brush(Muted);
            d["Cel.AccentBrush"] = Brush(Accent);
            d["Cel.Accent2Brush"] = Brush(Accent2);
            d["Cel.WarnBrush"] = Brush(Warn);
            d["Cel.HighlightBrush"] = Brush(WithAlpha(Colors.White, 0.55));

            d["Cel.CornerRadius"] = new CornerRadius(Current.CornerRadius);
            d["Cel.CornerRadiusSmall"] = new CornerRadius(Math.Max(6, Current.CornerRadius - 8));
            d["Cel.EdgeThickness"] = new Thickness(Current.CelOutline ? Current.EdgeWidth : 0);

            // ---- legacy keys: every existing window restyles through these ----
            d["AccentColor"] = Accent;
            d["AccentBrush"] = Brush(Accent);
            d["AccentSoftBrush"] = Brush(WithAlpha(Accent, 0.25));
            d["TextBrush"] = Brush(Text);
            d["SubTextBrush"] = Brush(Muted);
            d["GlassFillBrush"] = Brush(PanelFill);
            d["GlassStrokeBrush"] = Brush(PanelEdge);
            d["RowBrush"] = Brush(WithAlpha(Item, 0.20));
            d["RowHoverBrush"] = Brush(WithAlpha(Accent, 0.16));
            d["RowSelBrush"] = Brush(WithAlpha(Accent, 0.30));
            d["DangerBrush"] = Brush(Warn);

            return d;
        }

        /// <summary>Pushes the palette into the running application.</summary>
        public static void Apply()
        {
            var app = Application.Current;
            if (app == null) return;

            var built = Build();
            foreach (var key in built.Keys) app.Resources[key] = built[key];
        }
    }
}
