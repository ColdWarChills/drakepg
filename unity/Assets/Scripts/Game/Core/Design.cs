using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// The 1280x720 design space every screen lays out in (like DESIGN in src/config.ts): x right, y down, from the
    /// top-left. The camera shows exactly this area (letterboxed), 100 design pixels to a world unit; UI elements use
    /// design pixels directly.
    /// </summary>
    public static class Design
    {
        public const float Width = 1280, Height = 720;
        public const float PixelsPerUnit = 100;

        /// <summary>World position of a design point (z keeps sprites in front of the background).</summary>
        public static Vector3 World(float x, float y, float z = 0) => new Vector3((x - Width / 2) / PixelsPerUnit, (Height / 2 - y) / PixelsPerUnit, z);

        public static Vector2 FromWorld(Vector3 w) => new Vector2(w.x * PixelsPerUnit + Width / 2, Height / 2 - w.y * PixelsPerUnit);

        /// <summary>Sorting order from the feet's y: lower on screen draws in front.</summary>
        public static int Depth(float feetY) => Mathf.RoundToInt(feetY * 4);

        public static Color Hex(long rgb, float alpha = 1) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);

        public static Color Css(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }

    /// <summary>COLORS in src/config.ts.</summary>
    public static class Palette
    {
        public static readonly Color Bg = Design.Hex(0x0d0b12);
        public static readonly Color Panel = Design.Hex(0x1d1826);
        public static readonly Color PanelBorder = Design.Hex(0x8a6d3b);
        public static readonly Color Button = Design.Hex(0x3a2e4a);
        public static readonly Color ButtonHover = Design.Hex(0x4d3d63);
        public static readonly Color ButtonDown = Design.Hex(0x2a2136);
        public static readonly Color ButtonDisabled = Design.Hex(0x2a2630);
        public static readonly Color Hp = Design.Hex(0xc0392b);
        public static readonly Color Mp = Design.Hex(0x2e86de);
        public static readonly Color Xp = Design.Hex(0xd4ac0d);
        public static readonly Color BarBg = Design.Hex(0x111111);
        public static readonly Color Gold = Design.Hex(0xf1c40f);
        public static readonly Color Text = Design.Css("#f3e9d2");
        public static readonly Color TextDim = Design.Css("#a89f8c");
        public static readonly Color TextGold = Design.Css("#f1c40f");
        public static readonly Color TextGood = Design.Css("#7ddc7d");
        public static readonly Color TextBad = Design.Css("#ff7066");
        public static readonly Color TextMagic = Design.Css("#8ec5ff");

        /// <summary>ELEMENT_TEXT: element names and damage numbers.</summary>
        public static Color ElementText(string element) =>
            element != null && Rules.Db.Data.ElementText.TryGetValue(element, out var css) ? Design.Css(css) : Text;

        /// <summary>ELEMENT_COLORS: particle palettes, bright to dark (neutral when null).</summary>
        public static Color[] ElementColors(string element)
        {
            var list = element != null && Rules.Db.Data.ElementColors.TryGetValue(element, out var c) ? c : Rules.Db.Data.NeutralColors;
            var output = new Color[list.Length];
            for (int i = 0; i < list.Length; i++) output[i] = Design.Hex(list[i]);
            return output;
        }
    }
}
