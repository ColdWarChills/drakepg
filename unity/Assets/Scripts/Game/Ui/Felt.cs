using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The paper UI kit (style A of the round in art/ui/styles.html, picked by the designer 2026-10-27; it replaced the
    /// felt kit of 2026-10-18): the backgrounds' own medium. Sheets of cream paper cut with scissors (straight cuts of
    /// uneven length, each a hair off the line, the corners snipped) laid on a green sheet, soft shadows between the
    /// layers, no outlines and no stitches. Buttons are two sheets thick (sand; Attack red), key numbers are ink dots, a
    /// green runner lies along the bottom of the battle. Text on paper is dark ink.
    /// Sheets are drawn as vector shapes (UI Toolkit's painter): crisp at any size and resolution, no textures to bake
    /// or stretch (baked cloth textures read as grain, designer: "still too grainy"). Only world sprites (class cards,
    /// the result panel) and key dots are baked. Everything is drawn in code, designed by hand, never generated
    /// (docs/PLANS.md, Art). The kit keeps its felt-era names (Felt, FeltButton, FeltBar, FeltPass).
    /// </summary>
    public static class Felt
    {
        // The paper.
        /// <summary>The cream sheet panels, tags and the log are cut from.</summary>
        public static readonly Color Paper = Design.Hex(0xf6ecd6);
        /// <summary>The green sheet under menus and the HUD, and the battle's runner.</summary>
        public static readonly Color Leaf = Design.Hex(0x5b8a57);
        /// <summary>Buttons.</summary>
        public static readonly Color Sand = Design.Hex(0xe3c88f);
        /// <summary>The main button (Attack), the boss banner, hearts.</summary>
        public static readonly Color Red = Design.Hex(0xc8473a);
        /// <summary>The track a bar runs in.</summary>
        public static readonly Color Groove = Design.Hex(0x4a3a2b);
        /// <summary>Small brown marks on paper: rims, pointers.</summary>
        public static readonly Color Trim = Design.Hex(0x9a7a50);
        /// <summary>Light thread and a dark disc for the map, which is drawn on its own picture, not on paper.</summary>
        public static readonly Color Stitch = Design.Hex(0xeadbb5);
        public static readonly Color NavyDark = Design.Hex(0x1f2738);
        public static readonly Color Hp = Design.Hex(0xc8452f);
        public static readonly Color Mp = Design.Hex(0x3e6fb5);
        /// <summary>Text on paper: dark ink, and darker shades of the colors that mean something.</summary>
        public static readonly Color Text = Design.Hex(0x3a2b1f);
        public static readonly Color TextDim = Design.Hex(0x7d6a52);
        public static readonly Color Gold = Design.Hex(0x9a6512);
        public static readonly Color Good = Design.Hex(0x3f7d3a);
        public static readonly Color Bad = Design.Hex(0xb3402f);
        public static readonly Color Magic = Design.Hex(0x2f62a8);
        public static readonly Color Trait = Design.Hex(0x7a4f9e);
        /// <summary>The monster's next move.</summary>
        public static readonly Color Intent = Design.Hex(0xb04a22);
        /// <summary>Text on sand (buttons).</summary>
        public static readonly Color Brown = Design.Hex(0x3a2a1a);
        public static readonly Color BrownDim = Design.Hex(0x6b5338);
        public static readonly Color Ink = Design.Hex(0x2a1d16);
        /// <summary>Text on strong colors (bar numbers, Attack, chips).</summary>
        public static readonly Color Cream = Design.Hex(0xfff4dc);

        static readonly Color ShadowColor = Design.Hex(0x2a1d10);

        /// <summary>A color made dark enough to read as text on paper: light and grey ones become ink, colors keep their hue, darker.</summary>
        public static Color Readable(Color c)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            Color o;
            if (s < 0.18f) o = v > 0.8f ? Text : v > 0.45f ? TextDim : c;
            else
            {
                // Dark yellow reads as olive: yellows lean to amber.
                if (h > 0.1f && h < 0.19f) h = 0.1f;
                o = Color.HSVToRGB(h, Mathf.Max(s, 0.55f), Mathf.Min(v, 0.6f));
            }
            o.a = c.a;
            return o;
        }

        /// <summary>Whether a paper is dark enough to take cream text (its brightness as the eye sees it).</summary>
        public static bool IsDark(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b < 0.6f;

        /// <summary>A darker sheet of the same paper (under a thick button).</summary>
        static Color Under(Color c) => Color.Lerp(c, new Color(0.25f, 0.13f, 0.05f), 0.33f);

        /// <summary>Texels per design px for baked pictures: the UI's screen scale, at least 1.</summary>
        public static int Density
        {
            get
            {
                var s = GameRoot.I != null && GameRoot.I.DesignRoot != null ? GameRoot.I.DesignRoot.resolvedStyle.scale.value.x : 1;
                return Mathf.Clamp(Mathf.CeilToInt(s), 1, 3);
            }
        }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D Cached(string key, Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            cache[key] = t;
            return t;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        /// <summary>The mock's seeded random (a Lehmer generator), so a seed always gives the same edge.</summary>
        internal sealed class Rand
        {
            long s;
            public Rand(int seed) => s = (long)seed * 9301 + 49297;
            public float Next()
            {
                s = s * 16807 % 2147483647;
                return s / 2147483647f;
            }
        }

        // --- How a sheet looks ---

        /// <summary>How a sheet is cut and laid: its paper, a sheet behind it, how thick it is.</summary>
        public class Patch
        {
            public Color Cloth = Paper;
            /// <summary>A sheet of this color behind it, `BackPad` px bigger all round (the green under menus and the HUD).</summary>
            public Color? Back;
            public float BackPad = 6;
            /// <summary>A soft shadow under each sheet.</summary>
            public bool Shadow = true;
            /// <summary>Two sheets thick (buttons): the darker sheet under it shows this many px below.</summary>
            public float Thick;
            /// <summary>A darker sheet laid over it from this many px down (the runner's two greens; 0: none).</summary>
            public float Band;
            /// <summary>How far each scissor cut wanders off the line, and how much of each corner is snipped off (px).</summary>
            public float Wobble = 1.2f, Snip = 3;

            public string Key => $"{Hex(Cloth)}|{(Back.HasValue ? Hex(Back.Value) : "-")}|{BackPad}|{Shadow}|{Thick}|{Band}|{Wobble}|{Snip}";

            /// <summary>A cream sheet on a green one (the battle sheets, the HUD, panels).</summary>
            public static Patch Panel() => new Patch { Back = Leaf };

            /// <summary>A big menu: more of the green shows.</summary>
            public static Patch Menu() => new Patch { Back = Leaf, BackPad = 9, Wobble = 1.4f };

            /// <summary>A small cream sheet (the next move, the quest tracker, prompts).</summary>
            public static Patch Tag() => new Patch();

            /// <summary>A sand button two sheets thick; any paper (red Attack, a skill's color).</summary>
            public static Patch Button(Color? cloth = null) => new Patch { Cloth = cloth ?? Sand, Thick = 5 };

            /// <summary>The runner along the bottom of the battle screen: green, a darker green from 14 px down.</summary>
            public static Patch Runner() => new Patch { Cloth = Leaf, Band = 14, Wobble = 2 };

            /// <summary>The cream sheet the battle log is written on.</summary>
            public static Patch Log() => new Patch();
        }

        // --- Vector drawing ---

        /// <summary>A closed rounded rectangle path.</summary>
        internal static void RoundRect(Painter2D p, float x, float y, float w, float h, float r)
        {
            r = Mathf.Max(0, Mathf.Min(r, w / 2, h / 2));
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            if (r > 0) p.Arc(new Vector2(x + w - r, y + r), r, Angle.Degrees(-90), Angle.Degrees(0));
            p.LineTo(new Vector2(x + w, y + h - r));
            if (r > 0) p.Arc(new Vector2(x + w - r, y + h - r), r, Angle.Degrees(0), Angle.Degrees(90));
            p.LineTo(new Vector2(x + r, y + h));
            if (r > 0) p.Arc(new Vector2(x + r, y + h - r), r, Angle.Degrees(90), Angle.Degrees(180));
            p.LineTo(new Vector2(x, y + r));
            if (r > 0) p.Arc(new Vector2(x + r, y + r), r, Angle.Degrees(180), Angle.Degrees(270));
            p.ClosePath();
        }

        /// <summary>
        /// A scissor cut around a w x h box (the mock's cut()): straight cuts of uneven length, about 40 px, each end a hair
        /// (`wobble` px) off the line; the corners snipped off. The same seed always cuts the same way.
        /// </summary>
        internal static void Cut(Painter2D p, float x, float y, float w, float h, int seed, float wobble, float snip)
        {
            var rnd = new Rand(seed);
            var first = true;
            void Edge(float ax, float ay, float bx, float by)
            {
                var len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (len < 0.01f) return;
                float nx = -(by - ay) / len, ny = (bx - ax) / len;
                for (float t = 0; t < 1;)
                {
                    var o = (rnd.Next() - 0.5f) * 2 * wobble;
                    var pt = new Vector2(ax + (bx - ax) * t + nx * o, ay + (by - ay) * t + ny * o);
                    if (first) p.MoveTo(pt);
                    else p.LineTo(pt);
                    first = false;
                    t += 40 * (0.6f + rnd.Next() * 0.9f) / len;
                }
            }
            snip = Mathf.Min(snip, w / 4, h / 4);
            p.BeginPath();
            Edge(x + snip, y, x + w - snip, y);
            Edge(x + w, y + snip, x + w, y + h - snip);
            Edge(x + w - snip, y + h, x + snip, y + h);
            Edge(x, y + h - snip, x, y + snip);
            p.ClosePath();
        }

        /// <summary>A soft shadow under a sheet, (dx, dy) off: a few faint layers, each a little bigger, `spread` px in all.</summary>
        static void SoftShadow(Painter2D p, float x, float y, float w, float h, float dx, float dy, float spread, float alpha)
        {
            for (int i = 0; i < 4; i++)
            {
                var g = i * spread / 3;
                p.fillColor = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, alpha / 4);
                RoundRect(p, x + dx - g, y + dy - g, w + 2 * g, h + 2 * g, 3 + g);
                p.Fill();
            }
        }

        /// <summary>
        /// Draws a sheet filling `rect`: the sheet behind it (with its shadow), the sheet's own shadow, the darker sheet
        /// under a thick one, the cut paper, and a darker band over its lower part.
        /// </summary>
        internal static void DrawPatch(Painter2D p, Rect rect, Patch look, int seed)
        {
            float w = rect.width, h = rect.height, x0 = rect.x, y0 = rect.y;
            if (w < 2 || h < 2) return;
            // Small sheets (chips, keys) get gentler cuts.
            var wobble = Mathf.Min(look.Wobble, h / 16, w / 16);
            if (look.Back is Color back)
            {
                var b = look.BackPad;
                if (look.Shadow) SoftShadow(p, x0 - b, y0 - b, w + 2 * b, h + 2 * b, 4, 8, 8, 0.4f);
                p.fillColor = back;
                Cut(p, x0 - b, y0 - b, w + 2 * b, h + 2 * b, seed + 7, wobble * 1.3f, look.Snip);
                p.Fill();
            }
            if (look.Shadow) SoftShadow(p, x0, y0, w, h, 2, 3 + look.Thick, 3, 0.35f);
            if (look.Thick > 0)
            {
                p.fillColor = Under(look.Cloth);
                Cut(p, x0, y0 + look.Thick, w, h, seed + 1, wobble, look.Snip);
                p.Fill();
            }
            p.fillColor = look.Cloth;
            Cut(p, x0, y0, w, h, seed, wobble, look.Snip);
            p.Fill();
            if (look.Band > 0 && h > look.Band)
            {
                p.fillColor = Color.Lerp(look.Cloth, new Color(0.1f, 0.15f, 0.08f), 0.25f);
                Cut(p, x0, y0 + look.Band, w, h - look.Band, seed + 3, wobble, look.Snip);
                p.Fill();
            }
        }

        /// <summary>A sheet as an element: drawn to fill its own box (it may be sized by layout), repainted when that changes.</summary>
        public class PatchView : VisualElement
        {
            Patch look;
            readonly int seed;

            public PatchView(Patch look, int seed)
            {
                this.look = look;
                this.seed = seed;
                pickingMode = PickingMode.Ignore;
                name = "paper sheet";
                generateVisualContent += ctx => DrawPatch(ctx.painter2D, contentRect, this.look, this.seed);
                RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            }

            public Patch Look
            {
                get => look;
                set
                {
                    if (value.Key == look.Key) return;
                    look = value;
                    MarkDirtyRepaint();
                }
            }

            /// <summary>Stretch over the whole parent.</summary>
            public PatchView Fill()
            {
                style.position = Position.Absolute;
                style.left = style.top = style.right = style.bottom = 0;
                return this;
            }
        }

        /// <summary>A sheet on screen. `Root` is the sheet; put content in `Content`.</summary>
        public class Sheet
        {
            public VisualElement Root, Content;
            public float Width, Height;
        }

        /// <summary>A sheet at (x, y) top-left, w x h design px, tilted by `rotate` degrees.</summary>
        public static Sheet MakePatch(VisualElement parent, float x, float y, float w, float h, int seed, Patch patch = null, float rotate = 0,
            bool blocksClicks = true)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "paper" };
            Ui.Place(root, x, y);
            Ui.Size(root, w, h);
            root.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            if (rotate != 0) root.style.rotate = new Rotate(rotate);
            root.Add(new PatchView(patch ?? Patch.Panel(), seed).Fill());
            var content = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore };
            Ui.Place(content, 0, 0);
            Ui.Size(content, w, h);
            root.Add(content);
            parent?.Add(root);
            return new Sheet { Root = root, Content = content, Width = w, Height = h };
        }

        /// <summary>
        /// Paper behind an element that sizes itself (a quest tracker, a prompt): a tag sheet stretched over it. The
        /// element's colors are themed for paper (FeltPass).
        /// </summary>
        public static VisualElement CardBehind(VisualElement e, int seed, Patch patch = null)
        {
            var bg = new PatchView(patch ?? Patch.Tag(), seed).Fill();
            bg.name = "card";
            e.Insert(0, bg);
            e.AddToClassList(FeltPass.Class);
            e.style.backgroundColor = Color.clear;
            return bg;
        }

        // --- Tokens: big numbers on paper shapes (Hearthstone-style HP and MP) ---

        /// <summary>Points along cubic curves: `start`, then (control, control, end) triples, in a 100 x 100 box.</summary>
        static Vector2[] Curve(Vector2 start, params Vector2[] segs)
        {
            var pts = new List<Vector2>();
            var p0 = start;
            for (int i = 0; i + 2 < segs.Length; i += 3)
            {
                Vector2 c1 = segs[i], c2 = segs[i + 1], p3 = segs[i + 2];
                for (int k = 0; k < 24; k++)
                {
                    var t = k / 24f;
                    var u = 1 - t;
                    pts.Add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p3);
                }
                p0 = p3;
            }
            return pts.ToArray();
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>A heart (HP), drawn in a 100 x 100 box.</summary>
        public static readonly Vector2[] Heart = Curve(V(50, 92), V(22, 72), V(4, 54), V(4, 32), V(4, 15), V(16, 4), V(30, 4), V(40, 4), V(47, 10), V(50, 18),
            V(53, 10), V(60, 4), V(70, 4), V(84, 4), V(96, 15), V(96, 32), V(96, 54), V(78, 72), V(50, 92));

        /// <summary>A drop (MP), drawn in a 100 x 100 box.</summary>
        public static readonly Vector2[] Drop = Curve(V(50, 4), V(58, 18), V(88, 46), V(88, 66), V(88, 84), V(71, 96), V(50, 96), V(29, 96), V(12, 84),
            V(12, 66), V(12, 46), V(42, 18), V(50, 4));

        /// <summary>
        /// A token: a shape (Heart, Drop) cut from paper two sheets thick, a big number on it. The number pops when it
        /// changes. Placed by its top-left; `size` px square.
        /// </summary>
        public class TokenView : VisualElement
        {
            readonly Label number;
            readonly float baseFont;
            Anim.Handle pop;

            public TokenView(Vector2[] shape, Color cloth, float size, float fontSize, Color outline)
            {
                baseFont = fontSize;
                pickingMode = PickingMode.Ignore;
                name = "token";
                AddToClassList(FeltPass.Keep);
                Ui.Size(this, size, size);
                var k = size / 100f;
                Vector2 At(Vector2 q, float scale, Vector2 shift) => (new Vector2(50, 50) + (q - new Vector2(50, 50)) * scale) * k + shift;
                void Shape(Painter2D p, float scale, Vector2 shift)
                {
                    p.BeginPath();
                    for (int i = 0; i < shape.Length; i++)
                    {
                        if (i == 0) p.MoveTo(At(shape[i], scale, shift));
                        else p.LineTo(At(shape[i], scale, shift));
                    }
                    p.ClosePath();
                }
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    for (int i = 0; i < 3; i++)
                    {
                        p.fillColor = new Color(0.04f, 0.03f, 0.02f, 0.14f);
                        Shape(p, 1 + i * 0.03f, new Vector2(2, 6));
                        p.Fill();
                    }
                    p.fillColor = Under(cloth);
                    Shape(p, 1, new Vector2(0, 4));
                    p.Fill();
                    p.fillColor = cloth;
                    Shape(p, 1, Vector2.zero);
                    p.Fill();
                };
                number = new Label { pickingMode = PickingMode.Ignore };
                number.style.position = Position.Absolute;
                number.style.left = number.style.right = 0;
                number.style.top = 0;
                number.style.bottom = shape == Heart ? size * 0.1f : -size * 0.08f;
                number.style.fontSize = fontSize;
                number.style.color = Cream;
                number.style.unityTextAlign = TextAnchor.MiddleCenter;
                number.style.marginLeft = number.style.marginRight = number.style.marginTop = number.style.marginBottom = 0;
                number.style.paddingLeft = number.style.paddingRight = number.style.paddingTop = number.style.paddingBottom = 0;
                number.style.unityTextOutlineWidth = 0.25f;
                number.style.unityTextOutlineColor = outline;
                Fonts.Title(number);
                number.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                Add(number);
            }

            /// <summary>Show a number; `animate`: a little pop when it changed.</summary>
            public void Set(string text, bool animate = true)
            {
                if (number.text == text) return;
                var had = !string.IsNullOrEmpty(number.text);
                number.text = text;
                // Long numbers (a boss's HP) shrink to fit the shape.
                number.style.fontSize = text.Length > 3 ? baseFont * 3 / text.Length : baseFont;
                number.style.scale = new Scale(Vector3.one);
                if (!animate || !had) return;
                pop?.Stop();
                pop = Anim.Float(1.3f, 1, 0.3f, v => number.style.scale = new Scale(new Vector3(v, v, 1)), Ease.BackOut);
            }
        }

        /// <summary>A token centered at (cx, cy).</summary>
        public static TokenView Token(VisualElement parent, float cx, float cy, float size, Vector2[] shape, Color cloth, float fontSize, Color outline)
        {
            var t = new TokenView(shape, cloth, size, fontSize, outline);
            Ui.Place(t, cx - size / 2, cy - size / 2);
            parent?.Add(t);
            return t;
        }

        // --- Baked pictures: world sprites and key dots ---

        static float Smooth(float a, float b, float v)
        {
            var t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        static float RoundBoxDist(Vector2 p, float x, float y, float w, float h, float r)
        {
            var qx = Mathf.Abs(p.x - (x + w / 2)) - (w / 2 - r);
            var qy = Mathf.Abs(p.y - (y + h / 2)) - (h / 2 - r);
            var ox = Mathf.Max(qx, 0);
            var oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static Color Over(Color dst, Color src, float a)
        {
            if (a <= 0) return dst;
            var outA = a + dst.a * (1 - a);
            if (outA <= 0) return new Color(0, 0, 0, 0);
            var k = dst.a * (1 - a);
            return new Color((src.r * a + dst.r * k) / outA, (src.g * a + dst.g * k) / outA, (src.b * a + dst.b * k) / outA, outA);
        }

        static Texture2D ToTexture(Color[] px, int W, int H, string name)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            // Rows were built top-down; textures go bottom-up. Unity's Color -> Color32 rounds through Mathf (slow).
            var flipped = new Color32[px.Length];
            for (int y = 0; y < H; y++)
            {
                var src = y * W;
                var dst = (H - 1 - y) * W;
                for (int x = 0; x < W; x++)
                {
                    var c = px[src + x];
                    flipped[dst + x] = new Color32((byte)(c.r * 255f + 0.5f), (byte)(c.g * 255f + 0.5f), (byte)(c.b * 255f + 0.5f), (byte)(c.a * 255f + 0.5f));
                }
            }
            tex.SetPixels32(flipped);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Room around a baked sheet for its shadow and the sheet behind it (design px).</summary>
        const float BakePad = 20;

        /// <summary>
        /// A sheet baked into a picture, for the world, where the painter can't draw: the same layers as PatchView (the
        /// sheet behind, soft shadows, the sheet; edges cut straight), worked out per texel.
        /// </summary>
        static Texture2D PatchTexture(float w, float h, int seed, Patch p)
        {
            var k = Density;
            return Cached($"patch|{w}|{h}|{seed}|{p.Key}|{k}", () =>
            {
                const float pad = BakePad, r = 3;
                int W = Mathf.CeilToInt((w + pad * 2) * k), H = Mathf.CeilToInt((h + pad * 2) * k);
                var b = p.Back.HasValue ? p.BackPad : 0;
                Color Shade(float a) => new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, a);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var q = new Vector2((x + 0.5f) / k - pad, (y + 0.5f) / k - pad);
                        var c = new Color(0, 0, 0, 0);
                        if (p.Back is Color back)
                        {
                            if (p.Shadow) c = Shade(0.4f * (1 - Smooth(-4, 8, RoundBoxDist(q - new Vector2(4, 8), -b, -b, w + 2 * b, h + 2 * b, r))));
                            c = Over(c, back, Mathf.Clamp01(-RoundBoxDist(q, -b, -b, w + 2 * b, h + 2 * b, r) * k + 0.5f));
                        }
                        if (p.Shadow) c = Over(c, ShadowColor, 0.35f * (1 - Smooth(-2, 3, RoundBoxDist(q - new Vector2(2, 3), 0, 0, w, h, r))));
                        c = Over(c, p.Cloth, Mathf.Clamp01(-RoundBoxDist(q, 0, 0, w, h, r) * k + 0.5f));
                        px[y * W + x] = c;
                    }
                }
                return ToTexture(px, W, H, "paper sheet");
            });
        }

        /// <summary>A sheet as a world sprite, centered on its transform (cards the world's figures stand on).</summary>
        public static Sprite PatchSprite(float w, float h, int seed, Patch patch = null)
        {
            var tex = PatchTexture(w, h, seed, patch ?? Patch.Panel());
            var ppu = Design.PixelsPerUnit * tex.width / (w + BakePad * 2);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        // --- Chips and key dots ---

        /// <summary>A word on a small chip of paper in its color, cream text (ink on a light color): elements, statuses, the hero's weapon.</summary>
        public static Label Chip(string rich, Color color, float fontSize = 15, int seed = 1)
        {
            var l = new Label(rich) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = fontSize;
            var paper = Color.Lerp(color, Ink, 0.15f);
            l.style.color = IsDark(paper) ? Cream : Ink;
            Fonts.Heavy(l);
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = 9;
            l.style.paddingTop = 0;
            l.style.paddingBottom = 1;
            l.style.height = Mathf.Round(fontSize + 9);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.backgroundColor = paper;
            Ui.Round(l, 4);
            l.AddToClassList(FeltPass.Keep);
            return l;
        }

        /// <summary>A key number's dot: an ink disc with a soft shadow, 3 px of room for it.</summary>
        public static Texture2D ButtonTexture(float size)
        {
            var k = Density;
            return Cached($"keydot|{size}|{k}", () =>
            {
                const float pad = 3;
                int W = Mathf.CeilToInt((size + pad * 2) * k);
                var c = pad + size / 2;
                var r = size * 12f / 26f;
                var px = new Color[W * W];
                for (int y = 0; y < W; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var p = new Vector2((x + 0.5f) / k, (y + 0.5f) / k);
                        var d = Vector2.Distance(p, new Vector2(c, c));
                        var sd = Vector2.Distance(p, new Vector2(c + 0.8f, c + 1.8f));
                        var col = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0.4f * (1 - Smooth(r - 1.5f, r + 2f, sd)));
                        px[y * W + x] = Over(col, Ink, Mathf.Clamp01((r - d) * k + 0.5f));
                    }
                }
                return ToTexture(px, W, W, "key dot");
            });
        }

        /// <summary>A key dot with a number on it, its top-left at (x, y) (`size` px across, plus shadow room).</summary>
        public static VisualElement SewnButton(VisualElement parent, float x, float y, string text, float size = 26, float fontSize = 14)
        {
            const float pad = 3;
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = "key" };
            e.AddToClassList(FeltPass.Keep);
            Ui.Place(e, x - pad, y - pad);
            Ui.Size(e, size + pad * 2, size + pad * 2);
            e.style.backgroundImage = new StyleBackground(ButtonTexture(size));
            var n = Ui.Text(e, pad + size / 2, pad + size / 2, text, fontSize, Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(n);
            parent?.Add(e);
            return e;
        }

        /// <summary>The green runner across the bottom of the screen, its top edge at y (it runs off the sides and bottom).</summary>
        public static Sheet Runner(VisualElement parent, float y, int seed) =>
            MakePatch(parent, -12, y, Design.Width + 24, 200, seed, Patch.Runner());

        static readonly Dictionary<string, bool> iconExists = new Dictionary<string, bool>();

        /// <summary>Whether Resources/UiIcons has a drawing by this name (skills and statuses: `skill_<id>`, `status_<id>`).</summary>
        public static bool HasIcon(string name)
        {
            if (name == null) return false;
            if (!iconExists.TryGetValue(name, out var has))
                iconExists[name] = has = Resources.Load<Texture2D>("UiIcons/" + name) != null;
            return has;
        }

        /// <summary>A status chip with its icon: the chip holds the icon (cream ink) and then the text.</summary>
        public static VisualElement IconChip(string rich, Color color, string icon, float fontSize = 15, int seed = 1)
        {
            var label = Chip(rich, color, fontSize, seed);
            if (!HasIcon(icon)) return label;
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.height = label.style.height;
            chip.style.backgroundColor = label.style.backgroundColor;
            chip.style.paddingLeft = 4;
            Ui.Round(chip, 4);
            chip.AddToClassList(FeltPass.Keep);
            label.style.backgroundColor = Color.clear;
            label.style.paddingLeft = 3;
            var size = Mathf.Round(fontSize + 5);
            var ic = Icon(null, 0, 0, size, icon, IsDark(chip.style.backgroundColor.value));
            ic.style.position = Position.Relative;
            chip.Add(ic);
            chip.Add(label);
            return chip;
        }

        /// <summary>An icon from Resources/UiIcons (hand-drawn, scripts/ui_icons.py); `light`: the cream-ink set, for dark or colored ground.</summary>
        public static VisualElement Icon(VisualElement parent, float x, float y, float size, string name, bool light = false)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = "icon " + name };
            Ui.Place(e, x, y);
            Ui.Size(e, size, size);
            var tex = Resources.Load<Texture2D>((light ? "UiIconsLight/" : "UiIcons/") + name) ?? Resources.Load<Texture2D>("UiIcons/" + name);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            parent?.Add(e);
            return e;
        }
    }

    /// <summary>
    /// The UI fonts (OFL, Resources/Fonts): Alegreya for titles and buttons, Alegreya Sans for text. Their digits are
    /// lining figures (scripts/lining_figures.py), so numbers sit evenly.
    /// </summary>
    public static class Fonts
    {
        static Font title, body, bold, heavy;
        static Font Load(ref Font f, string name) => f != null ? f : f = Resources.Load<Font>("Fonts/" + name);

        public static void Title(VisualElement e) => Set(e, Load(ref title, "Alegreya-ExtraBold"));
        public static void Body(VisualElement e) => Set(e, Load(ref body, "AlegreyaSans-Medium"));
        public static void Bold(VisualElement e) => Set(e, Load(ref bold, "AlegreyaSans-Bold"));
        public static void Heavy(VisualElement e) => Set(e, Load(ref heavy, "AlegreyaSans-ExtraBold"));

        static void Set(VisualElement e, Font f)
        {
            if (f == null) return;
            e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
            e.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
    }

    /// <summary>
    /// A health or mana bar: a strip of colored paper in a dark brown groove. (x, y) = left edge, middle.
    /// It can preview a coming hit as a pale chunk at the end of the strip (Preview).
    /// </summary>
    public class FeltBar : VisualElement
    {
        readonly VisualElement strip, clip, trail;
        VisualElement preview;
        Anim.Handle previewPulse;
        readonly Label label;
        readonly float inner;
        readonly string prefix;
        float shown = -1, trailShown = -1;
        Anim.Handle tween, trailTween;
        /// <summary>The pale paper left where a hit took HP away: it lingers a moment, then drains after the strip.</summary>
        static readonly Color TrailColor = Design.Hex(0xf3d6a4);

        public FeltBar(VisualElement parent, float x, float y, float width, float height, Color color, string prefix, int seed, float fontSize = 18)
        {
            this.prefix = prefix;
            pickingMode = PickingMode.Ignore;
            AddToClassList(FeltPass.Keep);
            Ui.Place(this, x, y - height / 2);
            Ui.Size(this, width, height);
            Ui.Rect(this, 0, 0, width, height, Felt.Groove, 3);
            // The strip slides in from the left, so its right end always shows; the clip rounds its left end.
            inner = width - 6;
            clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 3, 3);
            Ui.Size(clip, inner, height - 6);
            clip.style.overflow = Overflow.Hidden;
            Ui.Round(clip, 2);
            Add(clip);
            trail = Ui.Rect(clip, 0, 0, inner, height - 6, TrailColor, 2);
            strip = Ui.Rect(clip, 0, 0, inner, height - 6, color, 2);
            label = Ui.Text(this, width / 2, height / 2, "", fontSize, Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(label);
            label.style.unityTextOutlineWidth = 0.2f;
            label.style.unityTextOutlineColor = Design.Hex(0x1a1410);
            parent?.Add(this);
        }

        void Show(float w) => strip.style.left = w - inner;

        void ShowTrail(float w)
        {
            trail.style.left = w - inner;
            trailShown = w;
        }

        /// <summary>
        /// The coming hit as a pale chunk at the end of the strip, from (current - damage) to current, pulsing; all that's
        /// left when the hit would kill (pulsing faster). No damage: hidden.
        /// </summary>
        public void Preview(double current, double max, double? damage)
        {
            if (preview == null)
            {
                preview = Ui.Rect(clip, 0, 0, 0, inner > 0 ? clip.style.height.value.value : 0, new Color(1, 0.96f, 0.86f), 2);
                preview.name = "preview";
            }
            previewPulse?.Stop();
            if (damage == null || damage <= 0 || max <= 0 || current <= 0)
            {
                preview.style.display = DisplayStyle.None;
                return;
            }
            var hi = Mathf.Clamp01((float)(current / max));
            var lo = Mathf.Clamp01((float)((current - damage.Value) / max));
            preview.style.left = inner * lo;
            preview.style.width = Mathf.Max(6, inner * (hi - lo));
            preview.style.display = DisplayStyle.Flex;
            var lethal = damage.Value >= current;
            previewPulse = Anim.Float(0.25f, lethal ? 0.8f : 0.6f, lethal ? 0.35f : 0.7f, a => preview.style.opacity = a, Ease.SineInOut, repeat: -1, yoyo: true);
        }

        public FeltBar SetValue(double current, double max, bool animate = true)
        {
            var ratio = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0;
            var target = inner * ratio;
            var from = shown < 0 ? target : shown;
            tween?.Stop();
            if (!animate || shown < 0)
            {
                Show(target);
                trailTween?.Stop();
                ShowTrail(target);
            }
            else if (target < from)
            {
                // A hit: the strip snaps down, the lost part stays pale for a beat and then drains away.
                tween = Anim.Float(from, target, 0.12f, Show, Ease.CubicOut);
                var trailFrom = Mathf.Max(trailShown, from);
                trailTween?.Stop();
                ShowTrail(trailFrom);
                trailTween = Anim.Float(trailFrom, target, 0.4f, ShowTrail, Ease.QuadIn, 0.35f);
            }
            else if (target > from)
            {
                // Healing: the pale paper jumps ahead and the strip fills up to it.
                trailTween?.Stop();
                ShowTrail(target);
                tween = Anim.Float(from, target, 0.4f, Show);
            }
            // The same value again (the screen refreshes bars after every event): settle the strip, let a trail drain on.
            else Show(target);
            shown = target;
            label.text = $"{prefix}{Math.Max(0, Math.Round(current))} / {max}";
            return this;
        }
    }

    /// <summary>
    /// A battle button: sand paper two sheets thick, tilted a little, an icon and an Alegreya label, an optional second
    /// line, and an ink dot with its key number at the top-left corner. `cloth` changes the paper (Attack is red; skills
    /// get a paper of their color). (x, y) is its center.
    /// </summary>
    public class FeltButton : VisualElement
    {
        readonly Action onClick;
        bool enabled = true, hover, down;

        public FeltButton(VisualElement parent, float x, float y, string text, Action onClick, float width = 196, float height = 64,
            string sublabel = null, string icon = null, Color? cloth = null, int seed = 1, float rotate = 0, string key = null,
            float fontSize = 24, float iconSize = 26, float subSize = 15)
        {
            this.onClick = onClick;
            var face = cloth ?? Felt.Sand;
            // Dark paper (Attack's red) takes cream text and the cream-ink icons; sand and light papers take brown.
            var dark = Felt.IsDark(face);
            var ink = dark ? Felt.Cream : Felt.Brown;
            Ui.Place(this, x - width / 2, y - height / 2);
            Ui.Size(this, width, height);
            pickingMode = PickingMode.Position;
            AddToClassList(FeltPass.Keep);
            style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            if (rotate != 0) style.rotate = new Rotate(rotate);
            Add(new Felt.PatchView(Felt.Patch.Button(face), seed).Fill());

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(column, 0, 0);
            Ui.Size(column, width, height);
            column.style.flexDirection = FlexDirection.Column;
            column.style.justifyContent = Justify.Center;
            column.style.alignItems = Align.Center;
            Add(column);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            column.Add(row);
            if (icon != null)
            {
                var ic = Felt.Icon(null, 0, 0, iconSize, icon, dark);
                ic.style.position = Position.Relative;
                ic.style.marginRight = 8;
                row.Add(ic);
            }
            var label = Flow(text, fontSize, ink);
            Fonts.Title(label);
            row.Add(label);
            if (sublabel != null)
            {
                var sub = Flow(sublabel, subSize, dark ? Felt.Cream : Felt.BrownDim);
                Fonts.Bold(sub);
                sub.style.marginTop = 2;
                sub.style.opacity = dark ? 0.9f : 1;
                column.Add(sub);
            }
            if (key != null) Felt.SewnButton(this, -7, -9, key);

            RegisterCallback<PointerEnterEvent>(_ => { hover = true; Paint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { hover = down = false; Paint(); });
            RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!enabled) return;
                down = true;
                Paint();
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                var was = down;
                down = false;
                Paint();
                if (!enabled || !was || GameRoot.I.Screen?.InputEnabled == false) return;
                Sfx.Play("click");
                e.StopPropagation();
                this.onClick?.Invoke();
            });
            parent?.Add(this);
        }

        static Label Flow(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        /// <summary>Lift on hover, press down onto the sheet under it, fade when disabled.</summary>
        void Paint()
        {
            var lift = !enabled ? 0 : down ? 3 : hover ? -3 : 0;
            style.translate = new Translate(0, lift);
            style.scale = new Scale(Vector3.one * (down && enabled ? 0.97f : 1));
            style.opacity = enabled ? 1 : 0.55f;
        }

        public new FeltButton SetEnabled(bool on)
        {
            enabled = on;
            Paint();
            return this;
        }

        public bool Enabled => enabled;

        public void Click()
        {
            if (enabled) onClick?.Invoke();
        }
    }
}
