using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The cut-paper UI kit (the round-15 mock, art/ui/battle_craft.html): paper sheets with scissor-cut edges, a backing
    /// sheet, grain, fibers and a soft drop shadow; hand-painted bars; paper buttons with an ink-blot key number.
    /// Everything is drawn in code (designed by hand, never generated: docs/PLANS.md, Art) and baked into textures at the
    /// screen's resolution, cached by size, colors and seed.
    /// </summary>
    public static class Paper
    {
        // The mock's palette.
        public static readonly Color Ink = Design.Hex(0x2a1d16);
        public static readonly Color InkDim = Design.Hex(0x6b5440);
        public static readonly Color Kraft = Design.Hex(0x3a2a1f);
        public static readonly Color KraftDark = Design.Hex(0x241911);
        public static readonly Color Kraft2 = Design.Hex(0x5c4330);
        public static readonly Color Cream = Design.Hex(0xefe2c2);
        public static readonly Color Cream2 = Design.Hex(0xd9c59c);
        public static readonly Color Red = Design.Hex(0xc8452f);
        public static readonly Color Blue = Design.Hex(0x3d6fa8);
        public static readonly Color Amber = Design.Hex(0xe0a93a);
        public static readonly Color Teal = Design.Hex(0x2f7f78);
        /// <summary>Text colors that read on cream paper.</summary>
        public static readonly Color InkGood = Design.Hex(0x2f7a32);
        public static readonly Color InkBad = Design.Hex(0xb0321f);
        public static readonly Color InkGold = Design.Hex(0x8f6210);
        public static readonly Color InkMagic = Design.Hex(0x2f5f9e);

        /// <summary>A color made dark enough to read as text on cream paper (element colors are pastel on the web).</summary>
        public static Color OnPaper(Color c)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (s < 0.15f) return v > 0.55f ? InkDim : c;
            return Color.HSVToRGB(h, Mathf.Max(s, 0.6f), Mathf.Min(v, 0.5f));
        }

        /// <summary>Texels per design px for baked paper: the UI's screen scale, at least 1.</summary>
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

        // --- Shapes (ports of cutPath and friends in the mock) ---

        /// <summary>The mock's seeded random (a Lehmer generator), so a seed always cuts the same edge.</summary>
        sealed class Rand
        {
            long s;
            public Rand(int seed) => s = (long)seed * 9301 + 49297;
            public float Next()
            {
                s = s * 16807 % 2147483647;
                return s / 2147483647f;
            }
        }

        /// <summary>
        /// A scissor-cut outline: straight cuts of uneven length, each end a little off the ideal line, corners clipped.
        /// `torn`: the top edge is torn instead (many small ragged steps).
        /// </summary>
        public static List<Vector2> CutPath(float x, float y, float w, float h, int seed, float jitter = 2.2f, float cut = 46, float corner = 5, bool torn = false)
        {
            var r = new Rand(seed);
            var pts = new List<Vector2>();
            void Edge(float x0, float y0, float x1, float y1, float jit)
            {
                var len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                var nx = -(y1 - y0) / len;
                var ny = (x1 - x0) / len;
                for (float t = 0; t < 1;)
                {
                    var o = (r.Next() - 0.5f) * 2 * jit;
                    pts.Add(new Vector2(x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o));
                    t += cut * (0.55f + r.Next() * 0.9f) / len;
                }
            }
            var c = corner;
            Edge(x + c, y, x + w - c, y, torn ? 5 : jitter);
            Edge(x + w, y + c, x + w, y + h - c, jitter);
            Edge(x + w - c, y + h, x + c, y + h, jitter);
            Edge(x, y + h - c, x, y + c, jitter);
            if (!torn) return pts;
            var top = new List<Vector2>();
            var r2 = new Rand(seed + 99);
            for (var px = x; px <= x + w; px += 5 + r2.Next() * 7) top.Add(new Vector2(px, y + (r2.Next() - 0.5f) * 7));
            top.Add(new Vector2(x + w, y + h));
            top.Add(new Vector2(x, y + h));
            return top;
        }

        // --- Rasterizing ---

        /// <summary>Coverage (0-1) of a polygon on a W x H grid, anti-aliased: 4 sub-rows per row, exact spans in x.</summary>
        static float[] Fill(List<Vector2> poly, int W, int H, float scale, Vector2 offset)
        {
            var cov = new float[W * H];
            const int sub = 4;
            var n = poly.Count;
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = (poly[i] + offset) * scale;
            var xs = new List<float>(16);
            for (int row = 0; row < H; row++)
            {
                for (int s = 0; s < sub; s++)
                {
                    var yy = row + (s + 0.5f) / sub;
                    xs.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        var a = p[i];
                        var b = p[(i + 1) % n];
                        if ((a.y <= yy && b.y > yy) || (b.y <= yy && a.y > yy)) xs.Add(a.x + (yy - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2) Span(cov, row, W, xs[k], xs[k + 1], 1f / sub);
                }
            }
            return cov;
        }

        static void Span(float[] cov, int row, int W, float x0, float x1, float weight)
        {
            x0 = Mathf.Clamp(x0, 0, W);
            x1 = Mathf.Clamp(x1, 0, W);
            if (x1 <= x0) return;
            int i0 = (int)x0, i1 = (int)x1;
            var o = row * W;
            if (i0 == i1)
            {
                if (i0 < W) cov[o + i0] += (x1 - x0) * weight;
                return;
            }
            cov[o + i0] += (i0 + 1 - x0) * weight;
            for (int i = i0 + 1; i < i1; i++) cov[o + i] += weight;
            if (i1 < W) cov[o + i1] += (x1 - i1) * weight;
        }

        /// <summary>Two box-blur passes each way (a soft shadow).</summary>
        static void Blur(float[] a, int W, int H, int radius)
        {
            if (radius < 1) return;
            var tmp = new float[a.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < H; y++)
                {
                    float sum = 0;
                    var o = y * W;
                    for (int x = -radius; x <= radius; x++) sum += a[o + Mathf.Clamp(x, 0, W - 1)];
                    for (int x = 0; x < W; x++)
                    {
                        tmp[o + x] = sum / (radius * 2 + 1);
                        sum += a[o + Mathf.Min(x + radius + 1, W - 1)] - a[o + Mathf.Max(x - radius, 0)];
                    }
                }
                for (int x = 0; x < W; x++)
                {
                    float sum = 0;
                    for (int y = -radius; y <= radius; y++) sum += tmp[Mathf.Clamp(y, 0, H - 1) * W + x];
                    for (int y = 0; y < H; y++)
                    {
                        a[y * W + x] = sum / (radius * 2 + 1);
                        sum += tmp[Mathf.Min(y + radius + 1, H - 1) * W + x] - tmp[Mathf.Max(y - radius, 0) * W + x];
                    }
                }
            }
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        /// <summary>Smooth value noise, 0-1.</summary>
        static float Noise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            var a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx);
            var b = Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fractal(float x, float y, int seed, int octaves)
        {
            float sum = 0, amp = 0.5f, total = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 31) * amp;
                total += amp;
                x *= 2;
                y *= 2;
                amp *= 0.5f;
            }
            return sum / total;
        }

        /// <summary>Value noise is flatter than SVG turbulence: spread it around 0.5 so specks and streaks show.</summary>
        static float Contrast(float n) => Mathf.Clamp01(0.5f + (n - 0.5f) * 1.8f);

        static Texture2D ToTexture(Color[] px, int W, int H, string name)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            // Rows were built top-down; textures go bottom-up.
            var flipped = new Color[px.Length];
            for (int y = 0; y < H; y++) Array.Copy(px, y * W, flipped, (H - 1 - y) * W, W);
            tex.SetPixels(flipped);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Straight-alpha "over".</summary>
        static Color Over(Color dst, Color src, float a)
        {
            if (a <= 0) return dst;
            var outA = a + dst.a * (1 - a);
            if (outA <= 0) return new Color(0, 0, 0, 0);
            var k = dst.a * (1 - a);
            return new Color((src.r * a + dst.r * k) / outA, (src.g * a + dst.g * k) / outA, (src.b * a + dst.b * k) / outA, outA);
        }

        // --- Sheets ---

        /// <summary>Room around a sheet's texture for its drop shadow (design px).</summary>
        public const float Pad = 10;

        /// <summary>
        /// A paper sheet's picture: drop shadow, backing sheet, front sheet inset 5 px and cut on its own, paper grain,
        /// fibers and a darker band along the bottom. `torn`: a board with a torn top edge (no inset).
        /// </summary>
        public static Texture2D SheetTexture(float w, float h, Color front, Color back, int seed, bool torn = false, bool shadow = true, float inset = 5)
        {
            var k = Density;
            var key = $"sheet|{w}|{h}|{ColorUtility.ToHtmlStringRGBA(front)}|{ColorUtility.ToHtmlStringRGBA(back)}|{seed}|{torn}|{shadow}|{inset}|{k}";
            return Cached(key, () =>
            {
                int W = Mathf.CeilToInt((w + Pad * 2) * k), H = Mathf.CeilToInt((h + Pad * 2) * k);
                var origin = new Vector2(Pad, Pad);
                var backPoly = CutPath(0, 0, w, h, seed, torn: torn);
                var frontPoly = torn ? CutPath(0, 6, w, h - 6, seed + 5, torn: true)
                    : inset > 0 ? CutPath(inset, inset, w - inset * 2, h - inset * 2, seed + 5, 1.8f) : backPoly;
                var backCov = Fill(backPoly, W, H, k, origin);
                var frontCov = inset > 0 || torn ? Fill(frontPoly, W, H, k, origin) : backCov;
                float[] shadowCov = null;
                if (shadow)
                {
                    shadowCov = Fill(backPoly, W, H, k, origin + new Vector2(2, 5));
                    Blur(shadowCov, W, H, Mathf.Max(1, Mathf.RoundToInt(3 * k)));
                }
                var shadowColor = new Color(25 / 255f, 16 / 255f, 10 / 255f);
                var grainColor = new Color(0.16f, 0.11f, 0.07f);
                var px = new Color[W * H];
                var bandTop = (Pad + h - 14) * k;
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var c = new Color(0, 0, 0, 0);
                        if (shadowCov != null) c = Over(c, shadowColor, Mathf.Clamp01(shadowCov[i]) * 0.45f);
                        c = Over(c, back, Mathf.Clamp01(backCov[i]));
                        var fc = Mathf.Clamp01(frontCov[i]);
                        if (fc > 0)
                        {
                            // Paper grain (dark specks) and fibers (light streaks), sized in design px.
                            float dx = x / (float)k, dy = y / (float)k;
                            // The mock's feColorMatrix: grain alpha = 0.55 n - 0.12, fibers 0.35 n - 0.1 (at 90% and 50%).
                            // Kept quieter than the SVG: value noise speckles harder than turbulence.
                            var grain = Mathf.Clamp01(0.55f * Contrast(Fractal(dx * 0.6f, dy * 0.6f, seed, 3)) - 0.12f) * 0.35f;
                            var fiber = Mathf.Clamp01(0.35f * Contrast(Fractal(dx * 0.02f, dy * 0.4f, seed + 7, 2)) - 0.1f) * 0.4f;
                            var f = Color.Lerp(front, grainColor, grain);
                            f = Color.Lerp(f, Color.white, fiber);
                            if (y >= bandTop) f = Color.Lerp(f, Ink, 0.08f);
                            c = Over(c, f, fc);
                        }
                        px[i] = c;
                    }
                }
                return ToTexture(px, W, H, "paper sheet");
            });
        }

        /// <summary>A sheet on screen. `Root` is the whole picture (shadow room included); put content in `Content`.</summary>
        public class Sheet
        {
            public VisualElement Root, Content;
            public float Width, Height;
        }

        /// <summary>A paper sheet at (x, y) top-left, w x h design px, tilted by `rotate` degrees.</summary>
        public static Sheet MakeSheet(VisualElement parent, float x, float y, float w, float h, Color front, Color back, int seed,
            bool torn = false, float rotate = 0, bool shadow = true, float inset = 5, bool blocksClicks = true)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "paper" };
            Ui.Place(root, x - Pad, y - Pad);
            Ui.Size(root, w + Pad * 2, h + Pad * 2);
            root.style.backgroundImage = new StyleBackground(SheetTexture(w, h, front, back, seed, torn, shadow, inset));
            if (rotate != 0) root.style.rotate = new Rotate(rotate);
            var content = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore };
            Ui.Place(content, Pad, Pad);
            Ui.Size(content, w, h);
            root.Add(content);
            parent?.Add(root);
            return new Sheet { Root = root, Content = content, Width = w, Height = h };
        }

        // --- Bars ---

        /// <summary>A bar's slot: a narrow cut strip of ink.</summary>
        public static Texture2D SlotTexture(float w, float h, int seed)
        {
            var k = Density;
            return Cached($"slot|{w}|{h}|{seed}|{k}", () =>
            {
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt(h * k);
                var cov = Fill(CutPath(0, 0, w, h, seed, 1.2f, 30, 3), W, H, k, Vector2.zero);
                var px = new Color[W * H];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(Ink.r, Ink.g, Ink.b, Mathf.Clamp01(cov[i]));
                return ToTexture(px, W, H, "bar slot");
            });
        }

        /// <summary>The painted strip of a full bar (uneven top and bottom, a light streak), w x h.</summary>
        public static Texture2D StripTexture(float w, float h, Color color, int seed)
        {
            var k = Density;
            return Cached($"strip|{w}|{h}|{ColorUtility.ToHtmlStringRGBA(color)}|{seed}|{k}", () =>
            {
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt(h * k);
                var r = new Rand(seed);
                var top = new List<Vector2>();
                var bottom = new List<Vector2>();
                for (float x = 0; x < w; x += 14)
                {
                    top.Add(new Vector2(x, (r.Next() - 0.5f) * 1.6f));
                    bottom.Add(new Vector2(x, h + (r.Next() - 0.5f) * 1.6f));
                }
                top.Add(new Vector2(w + 2, 0));
                bottom.Add(new Vector2(w + 2, h));
                bottom.Reverse();
                var poly = new List<Vector2>(top);
                poly.AddRange(bottom);
                var cov = Fill(poly, W, H, k, Vector2.zero);
                var streak = new Color(1, 240 / 255f, 215 / 255f);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var c = color;
                        // A light streak 3 px down, like a brush's dry edge.
                        var dy = Mathf.Abs(y / (float)k - 3);
                        if (dy < 1.25f && x / (float)k > 4) c = Color.Lerp(c, streak, 0.45f * Mathf.Clamp01(1.25f - dy));
                        c.a = Mathf.Clamp01(cov[i]);
                        px[i] = c;
                    }
                }
                return ToTexture(px, W, H, "bar strip");
            });
        }

        /// <summary>The ragged brush end of a bar's paint, 10 x h (straight left side, overlaps the strip).</summary>
        public static Texture2D BrushEndTexture(float h, Color color, int seed)
        {
            var k = Density;
            const float w = 12;
            return Cached($"brush|{h}|{ColorUtility.ToHtmlStringRGBA(color)}|{seed}|{k}", () =>
            {
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt(h * k);
                var r = new Rand(seed);
                var poly = new List<Vector2> { new Vector2(0, 0) };
                for (float y = 0; y <= h; y += 3) poly.Add(new Vector2(5 + (r.Next() - 0.3f) * 6, y));
                poly.Add(new Vector2(0, h));
                var cov = Fill(poly, W, H, k, Vector2.zero);
                var px = new Color[W * H];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(color.r, color.g, color.b, Mathf.Clamp01(cov[i]));
                return ToTexture(px, W, H, "bar brush end");
            });
        }

        // --- Blots ---

        /// <summary>An ink blot circle cut by hand (the key numbers on buttons), size x size.</summary>
        public static Texture2D BlotTexture(float size, Color color, int seed)
        {
            var k = Density;
            return Cached($"blot|{size}|{ColorUtility.ToHtmlStringRGBA(color)}|{seed}|{k}", () =>
            {
                int W = Mathf.CeilToInt(size * k);
                var r = new Rand(seed);
                var poly = new List<Vector2>();
                var c = size / 2;
                for (float a = 0; a < Mathf.PI * 2; a += Mathf.PI / 7)
                {
                    var rad = c * (11.5f + (r.Next() - 0.5f) * 2) / 12;
                    poly.Add(new Vector2(c + Mathf.Cos(a) * rad, c + Mathf.Sin(a) * rad));
                }
                var cov = Fill(poly, W, W, k, Vector2.zero);
                var px = new Color[W * W];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(color.r, color.g, color.b, Mathf.Clamp01(cov[i]));
                return ToTexture(px, W, W, "ink blot");
            });
        }

        /// <summary>An icon from Resources/UiIcons (hand-drawn ink, scripts/ui_icons.py), size x size at (x, y) top-left.</summary>
        public static VisualElement Icon(VisualElement parent, float x, float y, float size, string name)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = "icon " + name };
            Ui.Place(e, x, y);
            Ui.Size(e, size, size);
            var tex = Resources.Load<Texture2D>("UiIcons/" + name);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            parent?.Add(e);
            return e;
        }
    }

    /// <summary>The UI fonts (OFL, Resources/Fonts): Almendra for titles and buttons, Kalam for text.</summary>
    public static class Fonts
    {
        static Font title, body, bold;
        static Font Load(ref Font f, string name) => f != null ? f : f = Resources.Load<Font>("Fonts/" + name);

        public static void Title(VisualElement e) => Set(e, Load(ref title, "Almendra-Bold"));
        public static void Body(VisualElement e) => Set(e, Load(ref body, "Kalam-Regular"));
        public static void Bold(VisualElement e) => Set(e, Load(ref bold, "Kalam-Bold"));

        static void Set(VisualElement e, Font f)
        {
            if (f == null) return;
            e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
            e.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
    }

    /// <summary>A health or mana bar painted on paper (the mock's bars). (x, y) = left edge, middle.</summary>
    public class PaperBar : VisualElement
    {
        readonly VisualElement clip, cap;
        readonly Label label;
        readonly float inner, innerH;
        readonly string prefix;
        float shown = -1;
        Anim.Handle tween;

        public PaperBar(VisualElement parent, float x, float y, float width, float height, Color color, string prefix, int seed, float fontSize = 17)
        {
            this.prefix = prefix;
            pickingMode = PickingMode.Ignore;
            Ui.Place(this, x, y - height / 2);
            Ui.Size(this, width, height);
            style.backgroundImage = new StyleBackground(Paper.SlotTexture(width, height, seed));
            inner = width - 8;
            innerH = height - 8;
            clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 4, 4);
            Ui.Size(clip, inner, innerH);
            clip.style.overflow = Overflow.Hidden;
            Add(clip);
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(strip, 0, 0);
            Ui.Size(strip, inner, innerH);
            strip.style.backgroundImage = new StyleBackground(Paper.StripTexture(inner, innerH, color, seed + 1));
            clip.Add(strip);
            cap = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Size(cap, 12, innerH);
            cap.style.position = Position.Absolute;
            cap.style.top = 4;
            cap.style.backgroundImage = new StyleBackground(Paper.BrushEndTexture(innerH, color, seed + 2));
            Add(cap);
            label = Ui.Text(this, width / 2, height / 2, "", fontSize, Paper.Cream, true, 0.5f, 0.5f);
            Fonts.Bold(label);
            label.style.unityTextOutlineWidth = 0.22f;
            label.style.unityTextOutlineColor = Paper.Ink;
            parent?.Add(this);
        }

        void Show(float w)
        {
            clip.style.width = Mathf.Max(0, w);
            cap.style.left = 4 + w - 3;
            cap.style.display = w > 2 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public PaperBar SetValue(double current, double max, bool animate = true)
        {
            var ratio = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0;
            var target = (inner - 6) * ratio;
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
    /// A paper button (the mock's battle buttons): a cut sheet tilted a little, an ink icon and an Almendra label, an
    /// optional second line, and an ink-blot key number at its top-left corner. (x, y) is its center.
    /// </summary>
    public class PaperButton : VisualElement
    {
        readonly Label label, sub;
        readonly Action onClick;
        bool enabled = true, hover, down;

        public PaperButton(VisualElement parent, float x, float y, string text, Action onClick, float width = 196, float height = 64,
            string sublabel = null, string icon = null, Color? front = null, int seed = 1, float rotate = 0, string key = null, float fontSize = 23)
        {
            this.onClick = onClick;
            var face = front ?? Paper.Cream;
            Color.RGBToHSV(face, out _, out _, out var v);
            var ink = v < 0.62f ? Paper.Cream : Paper.Ink;
            Ui.Place(this, x - width / 2, y - height / 2);
            Ui.Size(this, width, height);
            pickingMode = PickingMode.Position;
            style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            if (rotate != 0) style.rotate = new Rotate(rotate);
            Paper.MakeSheet(this, 0, 0, width, height, face, Paper.Ink, seed, blocksClicks: false);

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
                var ic = Paper.Icon(null, 0, 0, 26, icon);
                ic.style.position = Position.Relative;
                ic.style.marginRight = 8;
                row.Add(ic);
            }
            label = Flow(text, fontSize, ink);
            Fonts.Title(label);
            row.Add(label);
            if (sublabel != null)
            {
                sub = Flow(sublabel, 13, ink);
                Fonts.Bold(sub);
                sub.style.opacity = 0.85f;
                sub.style.marginTop = 1;
                column.Add(sub);
            }
            if (key != null)
            {
                var blot = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(blot, -6, -8);
                Ui.Size(blot, 24, 24);
                blot.style.backgroundImage = new StyleBackground(Paper.BlotTexture(24, Paper.Ink, seed + 10));
                var n = Ui.Text(blot, 12, 12, key, 14, Paper.Cream, true, 0.5f, 0.5f);
                Fonts.Bold(n);
                Add(blot);
            }

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

        /// <summary>Lift on hover, press down, fade when disabled (the sheet's shadow stays put, like paper lifting).</summary>
        void Paint()
        {
            var lift = !enabled ? 0 : down ? 1 : hover ? -3 : 0;
            style.translate = new Translate(0, lift);
            style.scale = new Scale(Vector3.one * (down && enabled ? 0.97f : 1));
            style.opacity = enabled ? 1 : 0.55f;
        }

        public new PaperButton SetEnabled(bool on)
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
