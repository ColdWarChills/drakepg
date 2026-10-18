using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The felt UI kit (the mock art/ui/battle_felt.html, picked by the designer 2026-10-18): patches of felt with a
    /// slightly hairy edge, a padded rim and running stitches; sand-felt buttons with brown stitches (Attack in red
    /// felt), sewing buttons for key numbers, a felt runner along the bottom and a linen patch for the log.
    /// Patches are drawn as vector shapes (UI Toolkit's painter): crisp at any size and resolution, no textures to bake
    /// or stretch. The first version baked noisy cloth textures, and big menus (baked small, shown 2x) read as grain
    /// (designer: "still too grainy"). Only world sprites (class cards, the result panel) and sewing buttons are baked.
    /// Everything is drawn in code, designed by hand, never generated (docs/PLANS.md, Art).
    /// </summary>
    public static class Felt
    {
        // The mock's felt and thread.
        public static readonly Color Navy = Design.Hex(0x2b3650);
        public static readonly Color NavyDark = Design.Hex(0x1f2738);
        public static readonly Color Groove = Design.Hex(0x141a28);
        public static readonly Color Stitch = Design.Hex(0xeadbb5);
        public static readonly Color Hem = Design.Hex(0xc9b48c);
        public static readonly Color Sand = Design.Hex(0xd8c59c);
        public static readonly Color SandStitch = Design.Hex(0x7a5636);
        public static readonly Color RedFelt = Design.Hex(0xb8402c);
        public static readonly Color RedStitch = Design.Hex(0xf6e3c0);
        public static readonly Color Linen = Design.Hex(0xece2c9);
        public static readonly Color LinenStitch = Design.Hex(0x8a5a3a);
        public static readonly Color Hp = Design.Hex(0xc8452f);
        public static readonly Color Mp = Design.Hex(0x4a7cc0);
        /// <summary>Text on navy felt.</summary>
        public static readonly Color Text = Design.Hex(0xf1e6cc);
        public static readonly Color TextDim = Design.Hex(0xc4b48f);
        public static readonly Color Gold = Design.Hex(0xf0c75a);
        public static readonly Color Good = Design.Hex(0x9ad48a);
        public static readonly Color Bad = Design.Hex(0xff8f7a);
        public static readonly Color Magic = Design.Hex(0x9fc4ff);
        public static readonly Color Trait = Design.Hex(0xd9bdf0);
        /// <summary>The monster's next move.</summary>
        public static readonly Color Intent = Design.Hex(0xffb08a);
        /// <summary>Text on sand felt (buttons) and on linen (the log).</summary>
        public static readonly Color Brown = Design.Hex(0x3a2a1a);
        public static readonly Color BrownDim = Design.Hex(0x5f4a33);
        public static readonly Color Ink = Design.Hex(0x2a1d16);
        /// <summary>Text on strong colors (bar numbers, Attack).</summary>
        public static readonly Color Cream = Design.Hex(0xfff4dc);

        static readonly Color ShadowColor = Design.Hex(0x0a0806);

        /// <summary>A color made light enough to read as text on navy felt.</summary>
        public static Color OnFelt(Color c)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (s < 0.15f) return v < 0.6f ? TextDim : c;
            return Color.HSVToRGB(h, Mathf.Min(s, 0.55f), Mathf.Max(v, 0.88f));
        }

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

        // --- How a patch looks ---

        /// <summary>How a felt patch is made: its cloth, its thread and where the stitches run.</summary>
        public class Patch
        {
            public Color Cloth = Navy;
            public Color? Thread = Felt.Stitch;
            public float Inset = 7, Radius = 16, Dash = 7, Gap = 5, ThreadWidth = 2.2f;
            /// <summary>A soft drop shadow (2 px right, 5 down).</summary>
            public bool Shadow = true;
            /// <summary>How hairy the edge is: px the outline wanders in and out (0: a clean cut, like linen).</summary>
            public float Fuzz = 1.3f;
            /// <summary>The darker band just inside the edge that makes the patch look padded (its opacity).</summary>
            public float Rim = 0.13f;
            /// <summary>Thick felt (buttons): its cut edge shows this many px below it, in a darker shade.</summary>
            public float Thick;

            public string Key =>
                $"{Hex(Cloth)}|{(Thread.HasValue ? Hex(Thread.Value) : "-")}|{Inset}|{Radius}|{Dash}|{Gap}|{ThreadWidth}|{Shadow}|{Fuzz}|{Rim}|{Thick}";

            /// <summary>A navy panel with cream stitches (the battle sheets, the HUD).</summary>
            public static Patch Panel() => new Patch();

            /// <summary>A big navy menu with a wider hem.</summary>
            public static Patch Menu() => new Patch { Inset = 10, Radius = 18, Dash = 9, Gap = 6, ThreadWidth = 2.4f };

            /// <summary>A small navy tag (the next move, the quest tracker, prompts).</summary>
            public static Patch Tag() => new Patch { Inset = 5, Radius = 10, Dash = 5, Gap = 4, ThreadWidth = 1.8f };

            /// <summary>A sand button with brown stitches; any cloth (red Attack, a skill's color) with its own thread.</summary>
            public static Patch Button(Color? cloth = null, Color? thread = null) =>
                new Patch { Cloth = cloth ?? Sand, Thread = thread ?? SandStitch, Inset = 5, Radius = 12, Dash = 5, Gap = 4, Rim = 0.1f, Thick = 4 };

            /// <summary>The runner along the bottom of the battle screen.</summary>
            public static Patch Runner() => new Patch { Cloth = NavyDark, Thread = Hem, Inset = 9, Radius = 22, Dash = 9, Gap = 6 };

            /// <summary>The linen patch the battle log is written on: a clean cut edge, brown stitches.</summary>
            public static Patch LinenLog() =>
                new Patch { Cloth = Linen, Thread = LinenStitch, Inset = 7, Radius = 10, Dash = 6, Gap = 5, ThreadWidth = 2, Fuzz = 0, Rim = 0.06f };
        }

        // --- Vector drawing ---

        /// <summary>Walks a rounded rectangle's outline clockwise from the top edge's start: the point and its outward normal at length s.</summary>
        internal readonly struct Outline
        {
            readonly float x, y, w, h, r, l1, l2, a;
            public readonly float Length;

            public Outline(float x, float y, float w, float h, float r)
            {
                r = Mathf.Max(0, Mathf.Min(r, w / 2, h / 2));
                this.x = x;
                this.y = y;
                this.w = w;
                this.h = h;
                this.r = r;
                l1 = Mathf.Max(0, w - 2 * r);
                l2 = Mathf.Max(0, h - 2 * r);
                a = Mathf.PI * r / 2;
                Length = 2 * l1 + 2 * l2 + 4 * a;
            }

            Vector2 Arc(float cx, float cy, float deg, out Vector2 n)
            {
                var rad = deg * Mathf.Deg2Rad;
                n = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                return new Vector2(cx + n.x * r, cy + n.y * r);
            }

            public Vector2 At(float s, out Vector2 n)
            {
                if (Length <= 0)
                {
                    n = Vector2.up;
                    return new Vector2(x, y);
                }
                s %= Length;
                if (s < 0) s += Length;
                if (s < l1)
                {
                    n = Vector2.down;
                    return new Vector2(x + r + s, y);
                }
                s -= l1;
                if (s < a) return Arc(x + w - r, y + r, -90 + s / a * 90, out n);
                s -= a;
                if (s < l2)
                {
                    n = Vector2.right;
                    return new Vector2(x + w, y + r + s);
                }
                s -= l2;
                if (s < a) return Arc(x + w - r, y + h - r, s / a * 90, out n);
                s -= a;
                if (s < l1)
                {
                    n = Vector2.up;
                    return new Vector2(x + w - r - s, y + h);
                }
                s -= l1;
                if (s < a) return Arc(x + r, y + h - r, 90 + s / a * 90, out n);
                s -= a;
                if (s < l2)
                {
                    n = Vector2.left;
                    return new Vector2(x, y + h - r - s);
                }
                s -= l2;
                return Arc(x + r, y + r, 180 + s / Mathf.Max(a, 1e-4f) * 90, out n);
            }
        }

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
        /// Running stitches along a rounded rectangle: a whole number of dashes (so the seam closes evenly), round ends,
        /// a thread shadow down-right first (like thread pulled through cloth), then the thread.
        /// </summary>
        internal static void Stitches(Painter2D p, float x, float y, float w, float h, float r, float dash, float gap, float width, Color color)
        {
            var o = new Outline(x, y, w, h, r);
            if (o.Length <= 0) return;
            var period = dash + gap;
            var count = Mathf.Max(1, Mathf.RoundToInt(o.Length / period));
            var step = o.Length / count;
            var len = dash * step / period;
            var parts = Mathf.Max(1, Mathf.CeilToInt(len / 2));
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            for (int pass = 0; pass < 2; pass++)
            {
                var shift = pass == 0 ? new Vector2(0.8f, 1.2f) : Vector2.zero;
                p.lineWidth = pass == 0 ? width * 1.1f : width;
                p.strokeColor = pass == 0 ? new Color(0, 0, 0, 0.35f * color.a) : color;
                p.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    var s0 = i * step;
                    for (int j = 0; j <= parts; j++)
                    {
                        var pt = o.At(s0 + len * j / parts, out _) + shift;
                        if (j == 0) p.MoveTo(pt);
                        else p.LineTo(pt);
                    }
                }
                p.Stroke();
            }
        }

        /// <summary>
        /// Draws a patch filling `rect`: a soft shadow, the cloth with a slightly hairy edge, a darker padded rim and
        /// running stitches.
        /// </summary>
        internal static void DrawPatch(Painter2D p, Rect rect, Patch look, int seed)
        {
            float w = rect.width, h = rect.height, x0 = rect.x, y0 = rect.y;
            if (w < 2 || h < 2) return;
            var r = Mathf.Min(look.Radius, w / 2, h / 2);
            if (look.Shadow)
            {
                // A soft shadow: a few faint layers, each a little bigger.
                for (int i = 0; i < 4; i++)
                {
                    var g = i * 1.6f;
                    p.fillColor = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0.1f);
                    RoundRect(p, x0 + 2 - g, y0 + 5 + look.Thick - g, w + 2 * g, h + 2 * g, r + g);
                    p.Fill();
                }
            }
            // The cloth. Felt's edge isn't a clean cut: the outline wanders in and out a little, every 1.5 px.
            void Cloth(float dy, int s)
            {
                if (look.Fuzz <= 0)
                {
                    RoundRect(p, x0, y0 + dy, w, h, r);
                    return;
                }
                var o = new Outline(x0, y0 + dy, w, h, r);
                var rnd = new Rand(s);
                var n = Mathf.Max(8, Mathf.RoundToInt(o.Length / 1.5f));
                p.BeginPath();
                for (int i = 0; i < n; i++)
                {
                    var pt = o.At(i * o.Length / n, out var normal);
                    pt += normal * ((rnd.Next() - 0.5f) * look.Fuzz);
                    if (i == 0) p.MoveTo(pt);
                    else p.LineTo(pt);
                }
                p.ClosePath();
            }
            if (look.Thick > 0)
            {
                // Thick felt: its cut edge, a darker shade, shows below it.
                p.fillColor = Color.Lerp(look.Cloth, Color.black, 0.4f);
                Cloth(look.Thick, seed + 5);
                p.Fill();
            }
            p.fillColor = look.Cloth;
            Cloth(0, seed);
            p.Fill();
            // A darker band just inside the edge: the patch looks padded, not printed.
            if (look.Rim > 0 && w > 12 && h > 12)
            {
                p.strokeColor = new Color(0, 0, 0, look.Rim);
                p.lineWidth = 5;
                p.lineJoin = LineJoin.Round;
                RoundRect(p, x0 + 3, y0 + 3, w - 6, h - 6, Mathf.Max(0, r - 3));
                p.Stroke();
            }
            if (look.Thread.HasValue && w > look.Inset * 2 + 4 && h > look.Inset * 2 + 4)
                Stitches(p, x0 + look.Inset, y0 + look.Inset, w - look.Inset * 2, h - look.Inset * 2, Mathf.Max(3, r - look.Inset),
                    look.Dash, look.Gap, look.ThreadWidth, look.Thread.Value);
        }

        /// <summary>A felt patch as an element: drawn to fill its own box (it may be sized by layout), repainted when that changes.</summary>
        public class PatchView : VisualElement
        {
            Patch look;
            readonly int seed;

            public PatchView(Patch look, int seed)
            {
                this.look = look;
                this.seed = seed;
                pickingMode = PickingMode.Ignore;
                name = "felt patch";
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

        /// <summary>A patch on screen. `Root` is the patch; put content in `Content`.</summary>
        public class Sheet
        {
            public VisualElement Root, Content;
            public float Width, Height;
        }

        /// <summary>A felt patch at (x, y) top-left, w x h design px, tilted by `rotate` degrees.</summary>
        public static Sheet MakePatch(VisualElement parent, float x, float y, float w, float h, int seed, Patch patch = null, float rotate = 0,
            bool blocksClicks = true)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "felt" };
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
        /// Felt behind an element that sizes itself (a quest tracker, a prompt): a tag patch stretched over it. The
        /// element's colors are themed for felt (FeltPass).
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

        /// <summary>A row of light stitches from x0 to x1 at y inside an element (along a bar's strip).</summary>
        public class StitchRow : VisualElement
        {
            public StitchRow(float x0, float x1, float y, Color color, float dash = 5, float gap = 4, float width = 1.4f)
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.top = style.right = style.bottom = 0;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    p.lineCap = LineCap.Round;
                    p.lineWidth = width;
                    p.strokeColor = color;
                    p.BeginPath();
                    for (var s = x0; s + dash <= x1; s += dash + gap)
                    {
                        p.MoveTo(new Vector2(s, y));
                        p.LineTo(new Vector2(s + dash, y));
                    }
                    p.Stroke();
                };
            }
        }

        // --- Tokens: big numbers on felt shapes (Hearthstone-style HP and MP) ---

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
        /// A felt token: a shape (Heart, Drop) cut from thick felt with stitches just inside its edge and a big number on
        /// it. The number pops when it changes. Placed by its top-left; `size` px square.
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
                    p.fillColor = Color.Lerp(cloth, Color.black, 0.42f);
                    Shape(p, 1, new Vector2(0, 4));
                    p.Fill();
                    p.fillColor = cloth;
                    Shape(p, 1, Vector2.zero);
                    p.Fill();
                    // Stitches along the shape, 80% of its size: dashes laid by length along the outline.
                    var seam = new Vector2[shape.Length];
                    for (int i = 0; i < shape.Length; i++) seam[i] = At(shape[i], 0.8f, Vector2.zero);
                    var lengths = new float[seam.Length + 1];
                    for (int i = 0; i < seam.Length; i++) lengths[i + 1] = lengths[i] + Vector2.Distance(seam[i], seam[(i + 1) % seam.Length]);
                    var total = lengths[seam.Length];
                    Vector2 Along(float s)
                    {
                        s = Mathf.Repeat(s, total);
                        var j = Array.BinarySearch(lengths, s);
                        if (j < 0) j = ~j - 1;
                        j = Mathf.Clamp(j, 0, seam.Length - 1);
                        var seg = lengths[j + 1] - lengths[j];
                        return Vector2.Lerp(seam[j], seam[(j + 1) % seam.Length], seg > 0 ? (s - lengths[j]) / seg : 0);
                    }
                    var count = Mathf.Max(3, Mathf.RoundToInt(total / 10.5f));
                    var step = total / count;
                    var dash = step * 0.57f;
                    p.lineCap = LineCap.Round;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var shift = pass == 0 ? new Vector2(0.8f, 1.2f) : Vector2.zero;
                        p.lineWidth = pass == 0 ? 2.2f : 2f;
                        p.strokeColor = pass == 0 ? new Color(0, 0, 0, 0.35f) : RedStitch;
                        p.BeginPath();
                        for (int i = 0; i < count; i++)
                            for (int j = 0; j <= 3; j++)
                            {
                                var pt = Along(i * step + dash * j / 3) + shift;
                                if (j == 0) p.MoveTo(pt);
                                else p.LineTo(pt);
                            }
                        p.Stroke();
                    }
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

        // --- Baked pictures: world sprites and sewing buttons ---

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

        static float Stroke(float d, float width, int k) => Mathf.Clamp01((width / 2 - Mathf.Abs(d)) * k + 0.5f);

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

        /// <summary>
        /// A patch baked into a picture, for the world, where the painter can't draw: the same look as PatchView (a clean
        /// edge), worked out per texel, with 10 px of room for the shadow.
        /// </summary>
        static Texture2D PatchTexture(float w, float h, int seed, Patch p)
        {
            var k = Density;
            return Cached($"patch|{w}|{h}|{seed}|{p.Key}|{k}", () =>
            {
                const float pad = 10;
                int W = Mathf.CeilToInt((w + pad * 2) * k), H = Mathf.CeilToInt((h + pad * 2) * k);
                var r = Mathf.Min(p.Radius, w / 2, h / 2);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var q = new Vector2((x + 0.5f) / k - pad, (y + 0.5f) / k - pad);
                        var d = RoundBoxDist(q, 0, 0, w, h, r);
                        var c = p.Shadow
                            ? new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0.4f * (1 - Smooth(-3, 5, RoundBoxDist(q - new Vector2(2, 5), 0, 0, w, h, r))))
                            : new Color(0, 0, 0, 0);
                        var cover = Mathf.Clamp01(-d * k + 0.5f);
                        if (cover > 0)
                        {
                            var f = p.Cloth;
                            if (p.Rim > 0) f = Color.Lerp(f, Color.black, p.Rim * Stroke(d + 3, 5, k));
                            c = Over(c, f, cover);
                        }
                        px[y * W + x] = c;
                    }
                }
                if (p.Thread.HasValue)
                {
                    // The stitches: points every half px along the seam; each dash covers the texels near its points.
                    var o = new Outline(p.Inset, p.Inset, w - p.Inset * 2, h - p.Inset * 2, Mathf.Max(3, r - p.Inset));
                    var period = p.Dash + p.Gap;
                    var count = Mathf.Max(1, Mathf.RoundToInt(o.Length / period));
                    var step = o.Length / count;
                    var len = p.Dash * step / period;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var shift = pass == 0 ? new Vector2(0.8f, 1.2f) : Vector2.zero;
                        var width = pass == 0 ? p.ThreadWidth * 1.1f : p.ThreadWidth;
                        var col = pass == 0 ? new Color(0, 0, 0, 0.35f) : p.Thread.Value;
                        var pts = new List<Vector2>();
                        for (int i = 0; i < count; i++)
                        {
                            pts.Clear();
                            for (float s = 0; s <= len; s += 0.5f) pts.Add(o.At(i * step + s, out _) + shift);
                            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                            foreach (var pt in pts)
                            {
                                minX = Mathf.Min(minX, pt.x);
                                maxX = Mathf.Max(maxX, pt.x);
                                minY = Mathf.Min(minY, pt.y);
                                maxY = Mathf.Max(maxY, pt.y);
                            }
                            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX + pad - width) * k)), x1 = Mathf.Min(W, Mathf.CeilToInt((maxX + pad + width) * k));
                            int y0 = Mathf.Max(0, Mathf.FloorToInt((minY + pad - width) * k)), y1 = Mathf.Min(H, Mathf.CeilToInt((maxY + pad + width) * k));
                            for (int y = y0; y < y1; y++)
                            {
                                for (int x = x0; x < x1; x++)
                                {
                                    var q = new Vector2((x + 0.5f) / k - pad, (y + 0.5f) / k - pad);
                                    var best = float.MaxValue;
                                    foreach (var pt in pts) best = Mathf.Min(best, (pt - q).sqrMagnitude);
                                    var i2 = y * W + x;
                                    var cov = Stroke(Mathf.Sqrt(best), width, k) * col.a;
                                    if (cov > 0) px[i2] = Over(px[i2], col, cov * px[i2].a);
                                }
                            }
                        }
                    }
                }
                return ToTexture(px, W, H, "felt patch");
            });
        }

        /// <summary>A felt patch as a world sprite, centered on its transform (cards the world's figures stand on).</summary>
        public static Sprite PatchSprite(float w, float h, int seed, Patch patch = null)
        {
            var tex = PatchTexture(w, h, seed, patch ?? Patch.Panel());
            var ppu = Design.PixelsPerUnit * tex.width / (w + 20);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        // --- Chips and sewing buttons ---

        /// <summary>A word on a small rounded chip of felt in its color, light text in that color (elements, statuses, the hero's weapon).</summary>
        public static Label Chip(string rich, Color color, float fontSize = 15, int seed = 1)
        {
            var l = new Label(rich) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = fontSize;
            l.style.color = Color.Lerp(OnFelt(color), Color.white, 0.25f);
            Fonts.Heavy(l);
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = 9;
            l.style.paddingTop = 0;
            l.style.paddingBottom = 1;
            l.style.height = Mathf.Round(fontSize + 9);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.backgroundColor = Color.Lerp(NavyDark, color, 0.45f);
            Ui.Round(l, 7);
            l.AddToClassList(FeltPass.Keep);
            return l;
        }

        /// <summary>A cream sewing button (the key numbers): a disc with a brown rim and a pressed inner ring, 3 px shadow room.</summary>
        public static Texture2D ButtonTexture(float size)
        {
            var k = Density;
            return Cached($"sewbtn|{size}|{k}", () =>
            {
                const float pad = 3;
                int W = Mathf.CeilToInt((size + pad * 2) * k);
                var c = pad + size / 2;
                var rOut = size * 12f / 26f;
                var rIn = size * 8.5f / 26f;
                Color face = Design.Hex(0xefe2c2), rim = SandStitch, ring = Hem;
                var px = new Color[W * W];
                for (int y = 0; y < W; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var p = new Vector2((x + 0.5f) / k, (y + 0.5f) / k);
                        var d = Vector2.Distance(p, new Vector2(c, c));
                        var sd = Vector2.Distance(p, new Vector2(c + 0.8f, c + 1.8f));
                        var col = new Color(0, 0, 0, 0.45f * (1 - Smooth(rOut - 1.5f, rOut + 2f, sd)));
                        col = Over(col, face, Mathf.Clamp01((rOut - d) * k + 0.5f));
                        col = Over(col, rim, Stroke(d - rOut + 0.8f, 1.6f, k));
                        col = Over(col, ring, Stroke(d - rIn, 1f, k));
                        px[y * W + x] = col;
                    }
                }
                return ToTexture(px, W, W, "sewing button");
            });
        }

        /// <summary>A sewing button with a number on it, its top-left at (x, y) (`size` px across, plus shadow room).</summary>
        public static VisualElement SewnButton(VisualElement parent, float x, float y, string text, float size = 26, float fontSize = 14)
        {
            const float pad = 3;
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = "sewn" };
            e.AddToClassList(FeltPass.Keep);
            Ui.Place(e, x - pad, y - pad);
            Ui.Size(e, size + pad * 2, size + pad * 2);
            e.style.backgroundImage = new StyleBackground(ButtonTexture(size));
            var n = Ui.Text(e, pad + size / 2, pad + size / 2, text, fontSize, Brown, false, 0.5f, 0.5f);
            Fonts.Heavy(n);
            parent?.Add(e);
            return e;
        }

        /// <summary>The felt runner across the bottom of the screen, its top edge at y (it runs off the sides and bottom).</summary>
        public static Sheet Runner(VisualElement parent, float y, int seed) =>
            MakePatch(parent, -12, y, Design.Width + 24, 200, seed, Patch.Runner());

        /// <summary>An icon from Resources/UiIcons (hand-drawn, scripts/ui_icons.py); `light`: the cream-ink set for dark felt.</summary>
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
    /// A health or mana bar: a felt strip with a row of light stitches in a dark felt groove. (x, y) = left edge, middle.
    /// It can preview a coming hit as a pale chunk at the end of the strip (Preview).
    /// </summary>
    public class FeltBar : VisualElement
    {
        readonly VisualElement strip, clip;
        VisualElement preview;
        Anim.Handle previewPulse;
        readonly Label label;
        readonly float inner;
        readonly string prefix;
        float shown = -1;
        Anim.Handle tween;

        public FeltBar(VisualElement parent, float x, float y, float width, float height, Color color, string prefix, int seed, float fontSize = 18)
        {
            this.prefix = prefix;
            pickingMode = PickingMode.Ignore;
            AddToClassList(FeltPass.Keep);
            Ui.Place(this, x, y - height / 2);
            Ui.Size(this, width, height);
            Ui.Rect(this, 0, 0, width, height, Felt.Groove, height / 2);
            // The strip slides in from the left, so its rounded right end always shows; the clip rounds its left end.
            inner = width - 6;
            clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 3, 3);
            Ui.Size(clip, inner, height - 6);
            clip.style.overflow = Overflow.Hidden;
            Ui.Round(clip, (height - 6) / 2);
            Add(clip);
            strip = Ui.Rect(clip, 0, 0, inner, height - 6, color, (height - 6) / 2);
            strip.Add(new Felt.StitchRow(7, inner - 8, 3, new Color(1, 240 / 255f, 215 / 255f, 0.45f)));
            label = Ui.Text(this, width / 2, height / 2, "", fontSize, Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(label);
            label.style.unityTextOutlineWidth = 0.2f;
            label.style.unityTextOutlineColor = Design.Hex(0x1a1410);
            parent?.Add(this);
        }

        void Show(float w) => strip.style.left = w - inner;

        /// <summary>
        /// The coming hit as a pale chunk at the end of the strip, from (current - damage) to current, pulsing; all that's
        /// left when the hit would kill (pulsing faster). No damage: hidden.
        /// </summary>
        public void Preview(double current, double max, double? damage)
        {
            if (preview == null)
            {
                preview = Ui.Rect(clip, 0, 0, 0, inner > 0 ? clip.style.height.value.value : 0, new Color(1, 0.96f, 0.86f), clip.style.height.value.value / 2);
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
            if (!animate || shown < 0) Show(target);
            else tween = Anim.Float(from, target, target < from ? 0.25f : 0.4f, Show);
            shown = target;
            label.text = $"{prefix}{Math.Max(0, Math.Round(current))} / {max}";
            return this;
        }
    }

    /// <summary>
    /// A felt button (the mock's battle buttons): a sand patch with brown running stitches, tilted a little, an icon and
    /// an Alegreya label, an optional second line, and a sewing button with its key number at the top-left corner.
    /// `cloth` changes the felt (Attack is red with cream thread; skills get a felt of their color). (x, y) is its center.
    /// </summary>
    public class FeltButton : VisualElement
    {
        readonly Action onClick;
        bool enabled = true, hover, down;

        public FeltButton(VisualElement parent, float x, float y, string text, Action onClick, float width = 196, float height = 64,
            string sublabel = null, string icon = null, Color? cloth = null, Color? thread = null, int seed = 1, float rotate = 0, string key = null,
            float fontSize = 24, float iconSize = 26, float subSize = 15)
        {
            this.onClick = onClick;
            var face = cloth ?? Felt.Sand;
            Color.RGBToHSV(face, out _, out _, out var v);
            // Dark felt (Attack's red) takes cream text and the cream-ink icons; sand and light felts take brown.
            var dark = v < 0.75f;
            var ink = dark ? Felt.Cream : Felt.Brown;
            Ui.Place(this, x - width / 2, y - height / 2);
            Ui.Size(this, width, height);
            pickingMode = PickingMode.Position;
            AddToClassList(FeltPass.Keep);
            style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            if (rotate != 0) style.rotate = new Rotate(rotate);
            Add(new Felt.PatchView(Felt.Patch.Button(face, thread), seed).Fill());

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

        /// <summary>Lift on hover, press down onto its thick edge, fade when disabled.</summary>
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
