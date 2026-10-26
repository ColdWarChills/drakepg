using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// Item names, grade colors, item card text, compare lines and item icons, a port of src/ui/itemUi.ts. Text helpers
    /// are plain strings (Phaser-free like the TS ones); icons are UI Toolkit elements in design pixels.
    /// </summary>
    public static class ItemUi
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // --- Colors ---

        /// <summary>A border/bar color (`color`, the TS number) and a text color (`css`).</summary>
        public sealed class ColorPair
        {
            /// <summary>Frames and bars (the TS `color` number).</summary>
            public readonly Color Color;
            /// <summary>Text, as a hex string for rich text tags (the TS `css`).</summary>
            public readonly string Css;
            /// <summary>`Css` as a Color, for labels.</summary>
            public readonly Color TextColor;

            public ColorPair(long color, string css)
            {
                Color = Design.Hex(color);
                Css = css;
                TextColor = Design.Css(css);
            }
        }

        /// <summary>Border and text color per grade tier (GRADE_COLORS).</summary>
        public static readonly Dictionary<string, ColorPair> GradeColors = new Dictionary<string, ColorPair>
        {
            ["standard"] = new ColorPair(0x8c8577, "#b3ab9a"),
            ["fair"] = new ColorPair(0xe8e2d4, "#f3e9d2"),
            ["fine"] = new ColorPair(0x5cc85c, "#7ddc7d"),
            ["superior"] = new ColorPair(0x4a9cf0, "#7fb8ff"),
            ["masterwork"] = new ColorPair(0xf1c40f, "#ffd34d"),
        };

        /// <summary>Sigil glow and name colors (SIGIL_COLORS). Kept apart from the grade colors (grey, white, green, blue, gold).</summary>
        public static readonly Dictionary<string, ColorPair> SigilColors = new Dictionary<string, ColorPair>
        {
            ["dawn"] = new ColorPair(0xff7a2e, "#ff9550"),
            ["dusk"] = new ColorPair(0xb070ff, "#c79bff"),
            ["eclipse"] = new ColorPair(0xff3358, "#ff6680"),
        };

        /// <summary>Particle colors and pace (ms) per Sigil tier (SIGIL_FX): rarer tiers sparkle faster.</summary>
        public sealed class SigilFxData
        {
            public Color[] Tint;
            public float MoteEvery, TwinkleEvery;
        }

        public static readonly Dictionary<string, SigilFxData> SigilFx = new Dictionary<string, SigilFxData>
        {
            ["dawn"] = new SigilFxData { Tint = new[] { Design.Hex(0xff7a2e), Design.Hex(0xffb070), Design.Hex(0xffe0b0) }, MoteEvery = 160, TwinkleEvery = 520 },
            ["dusk"] = new SigilFxData { Tint = new[] { Design.Hex(0xb070ff), Design.Hex(0xd4b0ff), Design.Hex(0xffffff) }, MoteEvery = 95, TwinkleEvery = 340 },
            ["eclipse"] = new SigilFxData { Tint = new[] { Design.Hex(0xff3358), Design.Hex(0xff8a9c), Design.Hex(0xffd0d8) }, MoteEvery = 55, TwinkleEvery = 220 },
        };

        /// <summary>Blue lines, like the mods themselves (MOD_CSS).</summary>
        public const string ModCss = "#7fb8ff";
        public static readonly Color ModColor = Design.Css(ModCss);
        /// <summary>The fill of a mod's quality bar.</summary>
        public static readonly Color ModBarColor = Design.Hex(0x4a9cf0);
        /// <summary>The dark inside of an item slot.</summary>
        public static readonly Color SlotBg = Design.Hex(0x2a2236);

        /// <summary>The grade color of a 0-100 quality (roll bars).</summary>
        public static Color QualityColor(double quality) => GradeColors[Items.GradeTier(quality).tier].Color;

        /// <summary>Grade, tier and colors of one item (GradeInfo in itemUi.ts).</summary>
        public sealed class GradeData
        {
            public double Grade;
            public string Tier, Name;
            /// <summary>The grade's border color.</summary>
            public Color Color;
            /// <summary>The grade's text color, hex and Color.</summary>
            public string Css;
            public Color TextColor;
            public string Sigil;
            /// <summary>Name color: the Sigil's if it has one, else the grade's.</summary>
            public string NameCss;
            public Color NameColor;
            /// <summary>Border color: the Sigil's if it has one, else the grade's.</summary>
            public Color Frame;
        }

        /// <summary>Grade, tier and colors of an item. Consumables count as Standard.</summary>
        public static GradeData GradeInfo(ItemDef def, GearTraits g = null)
        {
            var gr = def.IsGear && g != null ? Items.Grade(def, g.Rolls ?? new Dictionary<string, double>(), g.Sigil) : 0;
            var (tier, name) = Items.GradeTier(gr);
            var colors = GradeColors[tier];
            var sigil = def.IsGear ? g?.Sigil : null;
            ColorPair sc = null;
            if (sigil != null) SigilColors.TryGetValue(sigil, out sc);
            return new GradeData
            {
                Grade = gr,
                Tier = tier,
                Name = name,
                Color = colors.Color,
                Css = colors.Css,
                TextColor = colors.TextColor,
                Sigil = sigil,
                NameCss = sc?.Css ?? colors.Css,
                NameColor = sc != null ? sc.TextColor : colors.TextColor,
                Frame = sc != null ? sc.Color : colors.Color,
            };
        }

        /// <summary>"Fine 52%" or "Standard".</summary>
        public static string GradeLabel(GradeData info) => info.Tier == "standard" ? "Standard" : $"{info.Name} {N(info.Grade)}%";

        /// <summary>"+3 Iron Sword" for reinforced gear, else just the name.</summary>
        public static string ItemName(ItemDef def, GearTraits g = null) => g?.Plus is int plus && plus != 0 ? $"+{plus} {def.Name}" : def.Name;

        /// <summary>Item name plus its Sigil and grade, e.g. "Cinder Fang (Sigil of Dusk, Fine 52%)"; consumables are just the name.</summary>
        public static string LootName(ItemDef def, GearTraits g = null)
        {
            if (!def.IsGear || g == null) return def.Name;
            var info = GradeInfo(def, g);
            var sigil = info.Sigil != null && Items.SigilNames.TryGetValue(info.Sigil, out var s) ? s + ", " : "";
            return $"{ItemName(def, g)} ({sigil}{GradeLabel(info)})";
        }

        /// <summary>"✦ Sigil of Dusk"</summary>
        public static string SigilLabel(string tier) => "✦ " + (Items.SigilNames.TryGetValue(tier, out var n) ? n : tier);

        // --- Sigil glow ---

        /// <summary>
        /// A Sigil item's icon comes alive: a soft glow in the Sigil's color pulses behind it and the icon swells a little
        /// (animateSigilIcon). `icon` is the square element from `DrawItemIcon`, `size` px wide. The particle twinkles of
        /// the TS version are ported separately; this is the pulse only. Runs while the icon is on screen.
        /// </summary>
        public static void AnimateSigilIcon(VisualElement icon, string tier, float size)
        {
            if (icon == null || tier == null || !SigilColors.TryGetValue(tier, out var colors)) return;
            var glowSize = size * 1.5f;
            var glow = new VisualElement { pickingMode = PickingMode.Ignore, name = "sigil-glow" };
            Ui.Place(glow, (size - glowSize) / 2, (size - glowSize) / 2);
            Ui.Size(glow, glowSize, glowSize);
            glow.style.backgroundImage = new StyleBackground(Art.Soft);
            glow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            glow.style.unityBackgroundImageTintColor = colors.Color;
            glow.style.opacity = 0.3f;
            icon.Insert(0, glow);

            var peak = tier == "eclipse" ? 0.95f : tier == "dusk" ? 0.8f : 0.65f;
            var duration = tier == "eclipse" ? 0.7f : 1.1f;
            Anim.Handle handle = null;
            void Start()
            {
                if (handle != null && !handle.IsDone) return;
                handle = Anim.Float(0, 1, duration, v =>
                {
                    glow.style.opacity = Mathf.Lerp(0.25f, peak, v);
                    icon.style.scale = new Scale(Vector3.one * (1 + 0.07f * v));
                }, Ease.SineInOut, 0, -1, true);
            }
            void Stop()
            {
                handle?.Stop();
                handle = null;
            }
            icon.RegisterCallback<AttachToPanelEvent>(_ => Start());
            icon.RegisterCallback<DetachFromPanelEvent>(_ => Stop());
            if (icon.panel != null) Start();
        }

        // --- Icons ---

        /// <summary>
        /// A square item icon `size` px wide, centered on (x, y) (drawItemIcon). Uses `icon_&lt;id&gt;` art if it exists,
        /// else the weapon's art laid diagonally, else a drawn shape. Not clickable.
        /// </summary>
        public static VisualElement DrawItemIcon(VisualElement parent, float x, float y, float size, ItemDef def)
        {
            var c = new VisualElement { pickingMode = PickingMode.Ignore, name = "item-icon" };
            Ui.Place(c, x - size / 2, y - size / 2);
            Ui.Size(c, size, size);
            parent?.Add(c);
            var iconKey = "icon_" + def.Id;
            if (Art.Has(iconKey))
            {
                Ui.Image(c, size * 0.025f, size * 0.025f, size * 0.95f, size * 0.95f, iconKey);
                return c;
            }
            if (def.IsWeapon && def.Art != null && Art.Has(def.Art))
            {
                // Upright art laid diagonally, like most inventory icons.
                var art = Art.Size(Art.Get(def.Art));
                if (art.x + art.y > 0)
                {
                    var k = size * 1.3f / (art.x + art.y);
                    float w = art.x * k, h = art.y * k;
                    var img = Ui.Image(c, (size - w) / 2, (size - h) / 2, w, h, def.Art);
                    img.style.rotate = new Rotate(Angle.Degrees(45));
                    return c;
                }
            }
            Paint(c, 0, 0, size, size, pen => DrawShape(pen, def, size));
            // A skill book wears its skill's drawing on the cover, so books tell apart at a glance.
            if (def.Type == "book" && Books.BookSkill(def) is SkillDef skill)
            {
                var s = size / 64f;
                var ink = Felt.Icon(c, size / 2 - 3 * s - 13 * s, size / 2 - 1 * s - 13 * s, 26 * s, SkillUi.Icon(skill), true);
                ink.style.unityBackgroundImageTintColor = new Color(1, 0.97f, 0.88f, 0.95f);
            }
            return c;
        }

        /// <summary>
        /// A framed item icon centered on (x, y): the slot's dark square with the grade (or Sigil) border, the icon inside,
        /// and the Sigil pulse. `gear` is the piece's rolls and Sigil (null for consumables and shop stock).
        /// </summary>
        public static VisualElement Icon(VisualElement parent, float x, float y, float size, ItemDef def, GearTraits gear = null)
        {
            var info = GradeInfo(def, gear);
            var box = new VisualElement { pickingMode = PickingMode.Ignore, name = "item-box" };
            Ui.Place(box, x - size / 2, y - size / 2);
            Ui.Size(box, size, size);
            parent?.Add(box);
            Ui.Rect(box, 0, 0, size, size, SlotBg, 8, 3, info.Frame);
            var iconSize = Mathf.Round(size * 0.75f);
            var icon = DrawItemIcon(box, size / 2, size / 2, iconSize, def);
            if (info.Sigil != null) AnimateSigilIcon(icon, info.Sigil, iconSize);
            return box;
        }

        /// <summary>An element drawn with vector shapes; the pen's (0, 0) is the element's center.</summary>
        internal static VisualElement Paint(VisualElement parent, float x, float y, float w, float h, Action<Pen> draw)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(e, x, y);
            Ui.Size(e, w, h);
            e.generateVisualContent += ctx => draw(new Pen(ctx.painter2D, new Vector2(w / 2, h / 2)));
            parent?.Add(e);
            return e;
        }

        /// <summary>Darken a color.</summary>
        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        static Color Alpha(long rgb, float a) => Design.Hex(rgb, a);

        static Vector2[] Pts(float s, params float[] xy)
        {
            var output = new Vector2[xy.Length / 2];
            for (int i = 0; i < output.Length; i++) output[i] = new Vector2(xy[i * 2] * s, xy[i * 2 + 1] * s);
            return output;
        }

        /// <summary>The drawn fallback icon (drawItemIcon's Graphics), on a 64 px grid scaled to `size`.</summary>
        static void DrawShape(Pen g, ItemDef def, float size)
        {
            var color = Design.Hex(def.IconColor ?? 0x9a8a6a);
            var s = size / 64f;
            if (def.IsWeapon)
            {
                g.Rotate(45);
                g.FillRect(Design.Hex(0xd8dde3), -4 * s, -28 * s, 8 * s, 40 * s);
                var hilt = Design.Hex(0x8a6d3b);
                g.FillRect(hilt, -14 * s, 12 * s, 28 * s, 5 * s);
                g.FillRect(hilt, -3 * s, 17 * s, 6 * s, 12 * s);
            }
            else if (def.IsArmor && Items.ArmorSlot(def) != "body")
            {
                DrawArmorPiece(g, Items.ArmorSlot(def), color, s);
            }
            else if (def.IsAccessory)
            {
                if (def.Slot == "ring")
                {
                    g.StrokeCircle(color, 7 * s, 0, 6 * s, 15 * s);
                    g.StrokeCircle(Shade(color, 0.5f), 2 * s, 0, 6 * s, 19 * s);
                    g.StrokeCircle(Shade(color, 0.5f), 2 * s, 0, 6 * s, 11 * s);
                    g.FillCircle(Design.Hex(0xbfe9ff), 0, -13 * s, 7 * s);
                    g.FillCircle(Alpha(0xffffff, 0.6f), -2 * s, -15 * s, 2.5f * s);
                }
                else
                {
                    g.StrokeArc(Design.Hex(0xc9a84a), 3 * s, 0, -14 * s, 20 * s, 200, 340);
                    g.FillCircle(color, 0, 12 * s, 12 * s);
                    g.StrokeCircle(Shade(color, 0.5f), 2 * s, 0, 12 * s, 12 * s);
                    g.FillCircle(Alpha(0xffffff, 0.45f), -4 * s, 8 * s, 3.5f * s);
                }
            }
            else if (def.IsArmor)
            {
                var pts = Pts(s, -12, -24, -4, -18, 4, -18, 12, -24, 26, -16, 22, -4, 16, -6, 16, 24, -16, 24, -16, -6, -22, -4, -26, -16);
                g.FillPoly(color, pts);
                g.StrokePoly(Shade(color, 0.5f), 2, pts);
                g.FillRect(Shade(color, 0.6f), -16 * s, 6 * s, 32 * s, 5 * s);
                g.FillRect(Alpha(0xffffff, 0.18f), -12 * s, -14 * s, 6 * s, 18 * s);
            }
            else if (def.Type == "scroll")
            {
                // A rolled parchment with a wax seal.
                g.FillEllipse(Shade(color, 0.75f), -20 * s, 0, 10 * s, 40 * s);
                g.FillEllipse(Shade(color, 0.75f), 20 * s, 0, 10 * s, 40 * s);
                g.FillRect(color, -20 * s, -18 * s, 40 * s, 36 * s);
                g.StrokeRect(Shade(color, 0.5f), 2, -20 * s, -18 * s, 40 * s, 36 * s);
                g.Line(Shade(color, 0.55f), 2 * s, -12 * s, -8 * s, 12 * s, -8 * s);
                g.Line(Shade(color, 0.55f), 2 * s, -12 * s, 0, 8 * s, 0);
                g.FillCircle(Design.Hex(0xb03a2e), 8 * s, 10 * s, 7 * s);
                g.FillCircle(Alpha(0xffffff, 0.35f), 6 * s, 8 * s, 2 * s);
            }
            else if (def.Type == "book")
            {
                // A closed skill book: cover in its element's color, a page edge, a clasp.
                g.FillRect(Design.Hex(0xf1e6c8), -16 * s, -22 * s, 34 * s, 44 * s);
                g.FillRect(color, -20 * s, -24 * s, 34 * s, 46 * s);
                g.StrokeRect(Shade(color, 0.5f), 2, -20 * s, -24 * s, 34 * s, 46 * s);
                g.FillRect(Shade(color, 0.6f), -20 * s, -24 * s, 6 * s, 46 * s);
                g.FillRect(Design.Hex(0xc9a84a), 12 * s, -4 * s, 6 * s, 8 * s);
            }
            else if (def.Type == "material" || def.Type == "stone")
            {
                // A faceted chunk.
                var pts = Pts(s, 0, -24, 20, -8, 14, 20, -14, 20, -20, -8);
                g.FillPoly(color, pts);
                g.StrokePoly(Shade(color, 0.5f), 2, pts);
                g.FillTriangle(Alpha(0xffffff, 0.35f), 0, -24 * s, -20 * s, -8 * s, -4 * s, -4 * s);
                g.FillTriangle(Shade(color, 0.7f), 14 * s, 20 * s, 20 * s, -8 * s, 2 * s, 4 * s);
            }
            else
            {
                g.FillRect(Alpha(0xdfe8f0, 0.55f), -6 * s, -24 * s, 12 * s, 14 * s);
                g.FillRect(Design.Hex(0x8a6d3b), -7 * s, -28 * s, 14 * s, 6 * s);
                g.FillCircle(Alpha(0xdfe8f0, 0.55f), 0, 6 * s, 20 * s);
                g.FillCircle(color, 0, 9 * s, 16 * s);
                g.FillCircle(Alpha(0xffffff, 0.45f), -7 * s, 2 * s, 5 * s);
            }
        }

        /// <summary>Drawn helm, gloves or boots (until `icon_&lt;id&gt;` art exists).</summary>
        static void DrawArmorPiece(Pen g, string slot, Color color, float s)
        {
            var dark = Shade(color, 0.5f);
            void Poly(params float[] xy)
            {
                var v = Pts(s, xy);
                g.FillPoly(color, v);
                g.StrokePoly(dark, 2, v);
            }
            if (slot == "helm")
            {
                Poly(-20, 18, -22, -2, -14, -18, 0, -24, 14, -18, 22, -2, 20, 18, 10, 18, 10, 4, -10, 4, -10, 18);
                g.FillRect(dark, -12 * s, -2 * s, 24 * s, 5 * s);
                g.FillRect(Alpha(0xffffff, 0.2f), -12 * s, -16 * s, 5 * s, 12 * s);
            }
            else if (slot == "gloves")
            {
                foreach (var dx in new float[] { -12, 12 })
                {
                    Poly(dx - 9, 22, dx - 9, -2, dx - 10, -14, dx - 5, -16, dx - 3, -6, dx - 1, -20, dx + 3, -20, dx + 4, -6, dx + 6, -16, dx + 10, -12, dx + 9, 22);
                    g.FillRect(dark, (dx - 9) * s, 14 * s, 18 * s, 4 * s);
                }
            }
            else
            {
                foreach (var dx in new float[] { -11, 11 })
                {
                    Poly(dx - 7, -22, dx + 5, -22, dx + 5, 10, dx + 14, 14, dx + 14, 22, dx - 7, 22);
                    g.FillRect(dark, (dx - 7) * s, 18 * s, 21 * s, 4 * s);
                }
            }
        }

        /// <summary>Phaser-Graphics-like drawing on a Painter2D, centered and optionally rotated (degrees, clockwise).</summary>
        internal sealed class Pen
        {
            readonly Painter2D p;
            readonly Vector2 center;
            float cos = 1, sin;

            public Pen(Painter2D painter, Vector2 center)
            {
                p = painter;
                this.center = center;
            }

            public void Rotate(float degrees)
            {
                var r = degrees * Mathf.Deg2Rad;
                cos = Mathf.Cos(r);
                sin = Mathf.Sin(r);
            }

            Vector2 T(float x, float y) => center + new Vector2(x * cos - y * sin, x * sin + y * cos);

            void Path(IList<Vector2> pts)
            {
                p.BeginPath();
                p.MoveTo(T(pts[0].x, pts[0].y));
                for (int i = 1; i < pts.Count; i++) p.LineTo(T(pts[i].x, pts[i].y));
                p.ClosePath();
            }

            public void FillPoly(Color c, IList<Vector2> pts)
            {
                if (pts.Count < 3) return;
                Path(pts);
                p.fillColor = c;
                p.Fill(FillRule.NonZero);
            }

            public void StrokePoly(Color c, float width, IList<Vector2> pts)
            {
                if (pts.Count < 2) return;
                Path(pts);
                p.strokeColor = c;
                p.lineWidth = width;
                p.Stroke();
            }

            static Vector2[] RectPts(float x, float y, float w, float h) =>
                new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) };

            public void FillRect(Color c, float x, float y, float w, float h) => FillPoly(c, RectPts(x, y, w, h));

            public void StrokeRect(Color c, float width, float x, float y, float w, float h) => StrokePoly(c, width, RectPts(x, y, w, h));

            public void FillTriangle(Color c, float x1, float y1, float x2, float y2, float x3, float y3) =>
                FillPoly(c, new[] { new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3) });

            public void FillEllipse(Color c, float x, float y, float w, float h)
            {
                const int n = 28;
                var pts = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    var a = i * Mathf.PI * 2 / n;
                    pts[i] = new Vector2(x + Mathf.Cos(a) * w / 2, y + Mathf.Sin(a) * h / 2);
                }
                FillPoly(c, pts);
            }

            public void FillCircle(Color c, float x, float y, float r)
            {
                p.BeginPath();
                p.Arc(T(x, y), r, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                p.ClosePath();
                p.fillColor = c;
                p.Fill(FillRule.NonZero);
            }

            public void StrokeCircle(Color c, float width, float x, float y, float r)
            {
                p.BeginPath();
                p.Arc(T(x, y), r, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                p.ClosePath();
                p.strokeColor = c;
                p.lineWidth = width;
                p.Stroke();
            }

            /// <summary>An open arc, angles in degrees clockwise from +x (like Phaser's arc).</summary>
            public void StrokeArc(Color c, float width, float x, float y, float r, float fromDeg, float toDeg)
            {
                var turn = Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;
                p.BeginPath();
                p.Arc(T(x, y), r, Angle.Degrees(fromDeg + turn), Angle.Degrees(toDeg + turn), ArcDirection.Clockwise);
                p.strokeColor = c;
                p.lineWidth = width;
                p.Stroke();
            }

            public void Line(Color c, float width, float x1, float y1, float x2, float y2)
            {
                p.BeginPath();
                p.MoveTo(T(x1, y1));
                p.LineTo(T(x2, y2));
                p.strokeColor = c;
                p.lineWidth = width;
                p.Stroke();
            }
        }

        // --- Text ---

        /// <summary>A number printed like JavaScript (`${n}`).</summary>
        static string N(double v) => JsMath.Str(v);

        /// <summary>`n.toFixed(digits)`.</summary>
        static string Fixed(double v, int digits) => v.ToString("F" + digits, Inv);

        static double Get(Dictionary<string, double> d, string key) => d != null && d.TryGetValue(key, out var v) ? v : 0;

        public static string DamageTypeLabel(string type) => type != null && Db.Data.DamageTypeLabels.TryGetValue(type, out var l) ? l : type;

        public static string ElementLabel(string element) => element != null && Db.Data.ElementLabels.TryGetValue(element, out var l) ? l : element;

        static string StatLabel(string key) => Player.StatLabels.TryGetValue(key, out var l) ? l : key;

        /// <summary>"+2 STR  +1 END" (bonusText).</summary>
        public static string BonusText(Dictionary<string, double> stats)
        {
            if (stats == null) return "";
            var parts = new List<string>();
            foreach (var k in Stat.Keys)
                if (stats.TryGetValue(k, out var v) && v != 0) parts.Add($"+{N(v)} {StatLabel(k)}");
            return string.Join("  ", parts);
        }

        /// <summary>"DEF Melee 9 · Pierce 10 · Magic 4"</summary>
        public static string DefenseText(double melee, double pierce, double magic) =>
            $"DEF {DamageTypeLabel("melee")} {N(melee)} · {DamageTypeLabel("pierce")} {N(pierce)} · {DamageTypeLabel("magic")} {N(magic)}";

        public static string DefenseText(Dictionary<string, double> d) => DefenseText(Get(d, "melee"), Get(d, "pierce"), Get(d, "magic"));

        /// <summary>"Fire +15, Ice -10" after `prefix`, or "" if none. An "all" entry counts for every element.</summary>
        public static string ResistText(Dictionary<string, double> r, string prefix = "")
        {
            if (r == null) return "";
            var all = Get(r, "all");
            var parts = new List<string>();
            foreach (var e in Db.Data.Elements)
            {
                var v = Get(r, e) + all;
                if (v == 0) continue;
                parts.Add($"{ElementLabel(e)} {(v > 0 ? "+" : "")}{N(v)}");
            }
            return parts.Count > 0 ? prefix + string.Join(", ", parts) : "";
        }

        /// <summary>The item's numbers with its rolls applied, one fact per line. Mods are listed separately (`ModLines`).</summary>
        public static List<string> ItemLines(ItemDef def, GearTraits g = null)
        {
            g ??= new GearTraits();
            var lines = new List<string>();
            switch (def.Type)
            {
                case "weapon":
                {
                    var d = Items.RolledDamage(def, g);
                    lines.Add($"{N(d[0])}-{N(d[1])} {DamageTypeLabel(def.DamageType)} damage");
                    lines.Add(def.Element != null ? $"Element: {ElementLabel(def.Element)}" : "Element: Neutral");
                    if (def.Stats != null) lines.Add(BonusText(def.Stats));
                    if (def.Proc != null) lines.Add($"Special: {def.Proc.Name}, {N(JsMath.Round(Items.RolledProcChance(def, g) * 100))}% on Attack");
                    break;
                }
                case "armor":
                {
                    lines.Add(DefenseText(Items.RolledDefense(def, g, "melee"), Items.RolledDefense(def, g, "pierce"), Items.RolledDefense(def, g, "magic")));
                    var res = ResistText(def.Resists, "Resists ");
                    if (res != "") lines.Add(res);
                    if (def.Stats != null) lines.Add(BonusText(def.Stats));
                    break;
                }
                case "accessory":
                {
                    // Everything scales with the Power roll (and a Sigil or +).
                    var stats = new Dictionary<string, double>();
                    foreach (var k in Stat.Keys)
                        if (def.Stats != null && def.Stats.TryGetValue(k, out var v) && v != 0) stats[k] = Items.AccessoryValue(v, g);
                    if (stats.Count > 0) lines.Add(BonusText(stats));
                    if (def.Hp is double hp && hp != 0) lines.Add($"+{N(Items.AccessoryValue(hp, g))} Max HP");
                    if (def.Mp is double mp && mp != 0) lines.Add($"+{N(Items.AccessoryValue(mp, g))} Max MP");
                    if (def.Crit is double crit && crit != 0) lines.Add($"+{Fixed(Items.AccessoryValue(crit, g, 1), 1)}% Crit chance");
                    var res = new Dictionary<string, double>();
                    if (def.Resists != null) foreach (var kv in def.Resists) res[kv.Key] = Items.AccessoryValue(kv.Value, g);
                    var rt = ResistText(res, "Resists ");
                    if (rt != "") lines.Add(rt);
                    break;
                }
                case "consumable":
                case "material":
                case "stone":
                    lines.Add(def.Description ?? "");
                    break;
                case "book":
                    // What it teaches is the description; the inventory card adds whether you can read it.
                    lines.Add(Books.BookSkill(def) is SkillDef skill ? $"{skill.Name}: {JsMath.Str(skill.MpCost)} MP{(skill.Cooldown > 0 ? $", cooldown {skill.Cooldown}" : "")}" : "");
                    break;
                case "scroll":
                    // The card's description says it all.
                    break;
            }
            return lines;
        }

        /// <summary>What a roll line adds at its quality, e.g. "+11%".</summary>
        public static string RollEffectText(ItemDef def, string key, Dictionary<string, double> rolls)
        {
            var b = Items.LineBonus(def, key, rolls) * 100;
            return $"+{(key == "crit" ? Fixed(b, 1) : N(JsMath.Round(b)))}%";
        }

        public sealed class RollLine
        {
            public string Key, Label, Effect;
            /// <summary>0-100</summary>
            public double Quality;
        }

        /// <summary>"Damage", "Crit chance"... (ROLL_LABELS).</summary>
        public static string RollLabel(string key) => Items.RollLabels.TryGetValue(key, out var l) ? l : key;

        public static List<RollLine> RollLines(ItemDef def, Dictionary<string, double> rolls, string sigil = null) =>
            Items.RollKeys(def, sigil).Select(key => new RollLine
            {
                Key = key,
                Label = RollLabel(key),
                Effect = RollEffectText(def, key, rolls),
                Quality = Get(rolls, key),
            }).ToList();

        /// <summary>"+3 STR", "+2.5% Crit chance", "+14 Fire resist"</summary>
        public static string ModText(Mod mod)
        {
            var v = Items.ModValue(mod);
            switch (mod.Key)
            {
                case "crit": return $"+{Fixed(v, 1)}% Crit chance";
                case "hp": return $"+{N(v)} Max HP";
                case "mp": return $"+{N(v)} Max MP";
                case "resist": return $"+{N(v)} {(mod.Element != null ? ElementLabel(mod.Element) : "")} resist";
                default: return $"+{N(v)} {StatLabel(mod.Key)}";
            }
        }

        public sealed class ModLine
        {
            public string Text;
            /// <summary>0-100</summary>
            public double Quality;
        }

        public static List<ModLine> ModLines(GearTraits g) =>
            (g?.Mods ?? new List<Mod>()).Select(m => new ModLine { Text = ModText(m), Quality = m.Roll }).ToList();

        /// <summary>"+8% Max HP, Earth +15"</summary>
        public static string SetBonusText(SetBonus b)
        {
            var parts = new List<string>();
            var stats = BonusText(b.Stats);
            if (stats != "") parts.Add(stats);
            if (b.HpPct is double hp && hp != 0) parts.Add($"+{N(hp)}% Max HP");
            if (b.DefensePct is double def && def != 0) parts.Add($"+{N(def)}% defense");
            if (b.DamagePct is double dmg && dmg != 0) parts.Add($"+{N(dmg)}% damage");
            if (b.Crit is double crit && crit != 0) parts.Add($"+{N(crit)}% Crit chance");
            var res = ResistText(b.Resists, "Resists ");
            if (res != "") parts.Add(res);
            return string.Join(", ", parts);
        }

        public sealed class SetCardLine
        {
            public string Text;
            /// <summary>Worn piece or active bonus: drawn bright; the rest dim.</summary>
            public bool On;
        }

        public sealed class SetCardData
        {
            public SetDef Set;
            public string Title;
            public List<SetCardLine> Lines;
        }

        static string ReplaceFirst(string text, string find, string with)
        {
            var i = text.IndexOf(find, StringComparison.Ordinal);
            return i < 0 ? text : text.Substring(0, i) + with + text.Substring(i + find.Length);
        }

        /// <summary>The item's set for its card: name with pieces worn, the pieces, and each bonus. Null if it has no set.</summary>
        public static SetCardData SetCard(string itemId, Dictionary<string, Gear> equipment)
        {
            var set = Sets.SetOfItem(itemId);
            if (set == null) return null;
            var count = Sets.SetCounts(equipment).Where(x => x.id == set.Id).Select(x => x.count).FirstOrDefault();
            var worn = new HashSet<string>(equipment.Values.Where(g => g != null).Select(g => g.ItemId));
            var lines = new List<SetCardLine>
            {
                // "Helm ✓ · Gloves · Boots ✓ · Band": worn pieces ticked.
                new SetCardLine
                {
                    Text = string.Join(" · ", set.Pieces.Select(id => ReplaceFirst(Db.Item(id).Name, set.Name + " ", "") + (worn.Contains(id) ? " ✓" : ""))),
                    On = false,
                },
            };
            foreach (var b in set.Bonuses) lines.Add(new SetCardLine { Text = $"({b.Pieces}) {SetBonusText(b)}", On = count >= b.Pieces });
            return new SetCardData { Set = set, Title = $"{set.Name} set ({count}/{set.Pieces.Count})", Lines = lines };
        }

        public sealed class CompareLine
        {
            public string Text;
            /// <summary>"good", "bad", or "neutral" (just different: damage type, element).</summary>
            public string Tone;
        }

        /// <summary>Set bonuses that wearing `piece` in `slot` would switch on (good) or off (bad).</summary>
        public static List<CompareLine> SetCompareLines(Dictionary<string, Gear> equipment, string slot, Gear piece)
        {
            string Key((SetDef set, SetBonus bonus) x) => x.set.Id + ":" + x.bonus.Pieces;
            var before = Sets.ActiveSetBonuses(equipment);
            var after = Sets.ActiveSetBonuses(new Dictionary<string, Gear>(equipment) { [slot] = piece });
            var was = new HashSet<string>(before.Select(Key));
            var now = new HashSet<string>(after.Select(Key));
            var lines = new List<CompareLine>();
            foreach (var x in after)
                if (!was.Contains(Key(x))) lines.Add(new CompareLine { Text = $"{x.set.Name} ({x.bonus.Pieces}): {SetBonusText(x.bonus)}", Tone = "good" });
            foreach (var x in before)
                if (!now.Contains(Key(x))) lines.Add(new CompareLine { Text = $"Loses {x.set.Name} ({x.bonus.Pieces})", Tone = "bad" });
            return lines;
        }

        /// <summary>"+40 HP, +20 MP" (what a potion restored).</summary>
        public static string RestoreText(double hp, double mp)
        {
            var parts = new List<string>();
            if (hp != 0) parts.Add($"+{N(hp)} HP");
            if (mp != 0) parts.Add($"+{N(mp)} MP");
            return parts.Count > 0 ? string.Join(", ", parts) : "No effect";
        }

        // --- Compare ---

        sealed class GearNumbers
        {
            public double AvgDamage, Crit, Special, Hp, HpFlat, Mp;
            public readonly Dictionary<string, double> Defense = new Dictionary<string, double> { ["melee"] = 0, ["pierce"] = 0, ["magic"] = 0 };
            public Dictionary<string, double> Stats = new Dictionary<string, double>();
            public Dictionary<string, double> Resists = new Dictionary<string, double>();
        }

        static readonly string[] DamageTypes = { "melee", "pierce", "magic" };

        /// <summary>`statBonus` is the hero's stat damage for a damage type, so weapons compare by real attack.</summary>
        static GearNumbers Numbers(ItemDef def, GearTraits g, Func<string, double> statBonus)
        {
            var o = new GearNumbers();
            if (def == null) return o;
            g ??= new GearTraits();
            var pieces = new[] { g };
            o.Stats = def.Stats != null ? new Dictionary<string, double>(def.Stats) : new Dictionary<string, double>();
            foreach (var k in Stat.Keys) o.Stats[k] = Get(o.Stats, k) + Items.ModTotal(pieces, k);
            o.Crit = Items.ModTotal(pieces, "crit");
            o.HpFlat = Items.ModTotal(pieces, "hp");
            o.Mp = Items.ModTotal(pieces, "mp");
            o.Resists = def.IsArmor && def.Resists != null ? new Dictionary<string, double>(def.Resists) : new Dictionary<string, double>();
            if (g.Mods != null)
                foreach (var m in g.Mods)
                    if (m.Key == "resist" && m.Element != null) o.Resists[m.Element] = Get(o.Resists, m.Element) + Items.ModValue(m);
            if (def.IsWeapon)
            {
                var d = Items.RolledDamage(def, g);
                o.AvgDamage = (d[0] + d[1]) / 2 + statBonus(def.DamageType);
                o.Crit += Items.RollBonus("crit", g.Rolls) * 100;
                o.Special = Items.RolledProcChance(def, g) * 100;
            }
            else if (def.IsArmor)
            {
                foreach (var t in DamageTypes) o.Defense[t] = Items.RolledDefense(def, g, t);
                o.Hp = Items.LineBonus(def, "hp", g.Rolls) * 100;
            }
            else
            {
                // Accessories: every number scales with Power.
                foreach (var k in Stat.Keys) o.Stats[k] = Get(o.Stats, k) - def.StatOf(k) + Items.AccessoryValue(def.StatOf(k), g);
                o.HpFlat += Items.AccessoryValue(def.Hp ?? 0, g);
                o.Mp += Items.AccessoryValue(def.Mp ?? 0, g);
                o.Crit += Items.AccessoryValue(def.Crit ?? 0, g, 1);
                if (def.Resists != null)
                    foreach (var kv in def.Resists) o.Resists[kv.Key] = Get(o.Resists, kv.Key) + Items.AccessoryValue(kv.Value, g);
            }
            return o;
        }

        /// <summary>
        /// Differences if `def` replaced what is equipped in its slot (`equippedDef`/`equippedGear`, null if empty).
        /// Empty = no change. Attack includes the hero's stat damage, so a sword compares honestly against a staff for a mage.
        /// </summary>
        public static List<CompareLine> CompareGear(ItemDef def, GearTraits g, ItemDef equippedDef, GearTraits equippedGear, Func<string, double> statBonus)
        {
            statBonus ??= _ => 0;
            var a = Numbers(def, g, statBonus);
            var b = Numbers(equippedDef, equippedGear ?? new GearTraits(), statBonus);
            var lines = new List<CompareLine>();
            void Add(string label, double delta, string unit = "", int digits = 0)
            {
                if (double.IsNaN(delta) || double.IsInfinity(delta)) return;
                var rounded = double.Parse(Fixed(delta, digits), Inv);
                if (rounded == 0) return;
                lines.Add(new CompareLine { Text = $"{label} {(rounded > 0 ? "+" : "")}{Fixed(rounded, digits)}{unit}", Tone = rounded > 0 ? "good" : "bad" });
            }
            Add("Attack (avg)", a.AvgDamage - b.AvgDamage, "", 1);
            Add("Crit", a.Crit - b.Crit, "%", 1);
            Add("Special chance", a.Special - b.Special, "%");
            foreach (var t in DamageTypes) Add($"DEF {DamageTypeLabel(t)}", a.Defense[t] - b.Defense[t]);
            Add("Max HP", a.Hp - b.Hp, "%");
            Add("Max HP", a.HpFlat - b.HpFlat);
            Add("Max MP", a.Mp - b.Mp);
            foreach (var k in Stat.Keys) Add(StatLabel(k), Get(a.Stats, k) - Get(b.Stats, k));
            foreach (var e in Db.Data.Elements) Add($"{ElementLabel(e)} resist", Get(a.Resists, e) - Get(b.Resists, e));
            if (def.IsWeapon && equippedDef != null && equippedDef.IsWeapon)
            {
                var was = equippedDef;
                if (was.DamageType != def.DamageType)
                    lines.Add(new CompareLine { Text = $"Damage type: {DamageTypeLabel(def.DamageType)} (was {DamageTypeLabel(was.DamageType)})", Tone = "neutral" });
                if (was.Element != def.Element)
                {
                    lines.Add(new CompareLine
                    {
                        Text = $"Element: {(def.Element != null ? ElementLabel(def.Element) : "Neutral")} (was {(was.Element != null ? ElementLabel(was.Element) : "Neutral")})",
                        Tone = "neutral",
                    });
                }
            }
            return lines;
        }

        /// <summary>The text color of a compare line's tone.</summary>
        public static Color ToneColor(string tone) => tone == "good" ? Palette.TextGood : tone == "bad" ? Palette.TextBad : Palette.TextMagic;
    }
}
