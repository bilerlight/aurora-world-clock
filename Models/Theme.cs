using System;

namespace AuroraClock.Models
{
    /// <summary>
    /// The cel-shaded palette. Flat fills, clean line-art edges and a hard offset shadow -
    /// high transparency, fresh and light by default, fully user editable.
    /// </summary>
    public sealed class ThemePalette
    {
        /// <summary>Card fill (drawn at <see cref="PanelAlpha"/> so the frost shows through).</summary>
        public string Panel { get; set; } = "#E8F9FC";

        /// <summary>The cel "ink" line around cards, tiles and buttons.</summary>
        public string PanelEdge { get; set; } = "#6ECFDA";

        /// <summary>Inner tiles / rows.</summary>
        public string Item { get; set; } = "#F4FEFD";

        public string ItemEdge { get; set; } = "#B4E8E6";

        /// <summary>Primary text - a deep teal ink rather than plain black.</summary>
        public string Text { get; set; } = "#1F4B64";

        public string Muted { get; set; } = "#5E93A8";

        public string Accent { get; set; } = "#17B3CD";

        public string Accent2 { get; set; } = "#3FD2B4";

        public string Warn { get; set; } = "#FF8577";

        /// <summary>0 = fully transparent, 1 = solid. The "high transparency" knob.</summary>
        public double PanelAlpha { get; set; } = 0.58;

        /// <summary>How much the panel is blurred behind the UI (0..1).</summary>
        public double FrostStrength { get; set; } = 0.85;

        public double CornerRadius { get; set; } = 18;

        public double EdgeWidth { get; set; } = 2;

        /// <summary>Draw the flat cel outline around cards and tiles.</summary>
        public bool CelOutline { get; set; } = true;

        /// <summary>Soft drop shadow under the panel (the "sticker" lift).</summary>
        public bool StickerShadow { get; set; } = true;

        public ThemePalette Clone() => (ThemePalette)MemberwiseClone();
    }
}
