using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The parchment UI kit (the mock art/ui/battle_parchment.html): parchment sheets with deckled, lightly scorched
    /// edges, stains and grain, and a ruled ink border with cut-in corners; watercolor-wash bars and chips; red wax seals
    /// for key numbers; a tooled leather band and a small scroll. Everything is drawn in code (designed by hand, never
    /// generated: docs/PLANS.md, Art) and baked into textures at the screen's resolution, cached by size, colors and seed.
    /// </summary>
    public static class Paper
    {
        // The mock's palette.
        public static readonly Color Ink = Design.Hex(0x2b1a0e);
        public static readonly Color InkDim = Design.Hex(0x7a5a38);
        public static readonly Color Parch = Design.Hex(0xead7ac);
        public static readonly Color ParchDark = Design.Hex(0xcdb07a);
        public static readonly Color Leather = Design.Hex(0x3f2615);
        public static readonly Color Gilt = Design.Hex(0xc39a52);
        /// <summary>Text on a strong wash (Attack, bar numbers).</summary>
        public static readonly Color Cream = Design.Hex(0xfff3dc);
        public static readonly Color Red = Design.Hex(0xb8392a);
        public static readonly Color RedWash = Design.Hex(0xb5321f);
        public static readonly Color Blue = Design.Hex(0x3a68a6);
        /// <summary>Text colors that read on parchment.</summary>
        public static readonly Color InkGood = Design.Hex(0x2f7a32);
        public static readonly Color InkBad = Design.Hex(0xa8281c);
        public static readonly Color InkGold = Design.Hex(0x8f6210);
        public static readonly Color InkMagic = Design.Hex(0x2f5f9e);
        public static readonly Color InkTrait = Design.Hex(0x5a3a8a);
        /// <summary>The monster's next move.</summary>
        public static readonly Color InkIntent = Design.Hex(0x8a2414);

        static readonly Color ShadowColor = Design.Hex(0x190c04);
        static readonly Color StainColor = new Color(0.55f, 0.36f, 0.14f);
        static readonly Color GrainColor = new Color(0.35f, 0.22f, 0.08f);
        static readonly Color BurnColor = Design.Hex(0x5c2f0e);
        static readonly Color EdgeColor = Design.Hex(0x6b3d17);

        /// <summary>A color made dark enough to read as text on parchment (element colors are pastel on the web).</summary>
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

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        // --- Shapes (ports of the mock's mocklib.js) ---

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

        /// <summary>A deckled (hand-torn) rectangle: many tiny ragged steps (deckled()).</summary>
        public static List<Vector2> Deckled(float x, float y, float w, float h, int seed, float amp = 1.4f, float step = 3)
        {
            var r = new Rand(seed);
            var pts = new List<Vector2>();
            void Side(float x0, float y0, float x1, float y1)
            {
                var len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                var nx = -(y1 - y0) / len;
                var ny = (x1 - x0) / len;
                for (float d = 0; d < len; d += step + r.Next() * step)
                {
                    var t = d / len;
                    var o = (r.Next() - 0.5f) * 2 * amp;
                    pts.Add(new Vector2(x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o));
                }
            }
            Side(x, y, x + w, y);
            Side(x + w, y, x + w, y + h);
            Side(x + w, y + h, x, y + h);
            Side(x, y + h, x, y);
            return pts;
        }

        /// <summary>A rounded rectangle whose edges wave gently (wavyRect()): `amp` px, `wave` px per bump.</summary>
        public static List<Vector2> WavyRect(float x, float y, float w, float h, int seed, float r = 14, float amp = 2.2f, float wave = 70, float step = 6)
        {
            r = Mathf.Min(r, w / 2, h / 2);
            var R = new Rand(seed);
            var pts = new List<Vector2>();
            var phase = new[] { R.Next() * 6.28f, R.Next() * 6.28f, R.Next() * 6.28f, R.Next() * 6.28f };
            void Side(float x0, float y0, float x1, float y1, float ph)
            {
                var len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                if (len < 0.01f) return;
                var nx = -(y1 - y0) / len;
                var ny = (x1 - x0) / len;
                for (float d = 0; d < len; d += step)
                {
                    var t = d / len;
                    var o = Mathf.Sin(ph + d / wave * 6.28f) * amp * Mathf.Sin(t * Mathf.PI);
                    pts.Add(new Vector2(x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o));
                }
            }
            void Arc(float cx, float cy, float a0)
            {
                for (int i = 0; i <= 6; i++)
                {
                    var a = a0 + i / 6f * Mathf.PI / 2;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Side(x + r, y, x + w - r, y, phase[0]);
            Arc(x + w - r, y + r, -Mathf.PI / 2);
            Side(x + w, y + r, x + w, y + h - r, phase[1]);
            Arc(x + w - r, y + h - r, 0);
            Side(x + w - r, y + h, x + r, y + h, phase[2]);
            Arc(x + r, y + h - r, Mathf.PI / 2);
            Side(x, y + h - r, x, y + r, phase[3]);
            Arc(x + r, y + r, Mathf.PI);
            return pts;
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
            for (int i = 0; i < cov.Length; i++) cov[i] = Mathf.Clamp01(cov[i]);
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

        static float[] Blurred(float[] a, int W, int H, int radius)
        {
            var b = (float[])a.Clone();
            Blur(b, W, H, radius);
            return b;
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

        /// <summary>Value noise is flatter than SVG turbulence: spread it around 0.5 so stains and specks show.</summary>
        static float Contrast(float n, float k = 1.8f) => Mathf.Clamp01(0.5f + (n - 0.5f) * k);

        /// <summary>The mock's ink wobble (an SVG displacement of +-1.2 px), so ruled lines look drawn by hand.</summary>
        static Vector2 Wobble(Vector2 p, int seed) =>
            new Vector2((Noise(p.x * 0.06f, p.y * 0.06f, seed) - 0.5f) * 2.4f, (Noise(p.x * 0.06f, p.y * 0.06f, seed + 1) - 0.5f) * 2.4f);

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

        /// <summary>Stroke coverage of a line `width` px wide at distance `d` (design px), anti-aliased at `k` texels per px.</summary>
        static float Stroke(float d, float width, int k) => Mathf.Clamp01((width / 2 - Mathf.Abs(d)) * k + 0.5f);

        static float SegDist(Vector2 p, float ax, float ay, float bx, float by)
        {
            var abx = bx - ax;
            var aby = by - ay;
            var len2 = abx * abx + aby * aby;
            var t = len2 > 0 ? Mathf.Clamp01(((p.x - ax) * abx + (p.y - ay) * aby) / len2) : 0;
            var dx = p.x - (ax + abx * t);
            var dy = p.y - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance to a quarter circle around a corner, the quarter that lies inside the box (sx, sy point inward).</summary>
        static float ArcDist(Vector2 p, float cx, float cy, float sx, float sy, float r)
        {
            var dx = (p.x - cx) * sx;
            var dy = (p.y - cy) * sy;
            if (dx < 0 || dy < 0) return float.MaxValue;
            return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
        }

        /// <summary>Distance to a ruled border whose corners are cut in (quarter circles around each corner).</summary>
        static float ConcaveDist(Vector2 p, float x, float y, float w, float h, float r)
        {
            var d = SegDist(p, x + r, y, x + w - r, y);
            d = Mathf.Min(d, SegDist(p, x + w, y + r, x + w, y + h - r));
            d = Mathf.Min(d, SegDist(p, x + r, y + h, x + w - r, y + h));
            d = Mathf.Min(d, SegDist(p, x, y + r, x, y + h - r));
            d = Mathf.Min(d, ArcDist(p, x, y, 1, 1, r));
            d = Mathf.Min(d, ArcDist(p, x + w, y, -1, 1, r));
            d = Mathf.Min(d, ArcDist(p, x, y + h, 1, -1, r));
            d = Mathf.Min(d, ArcDist(p, x + w, y + h, -1, -1, r));
            return d;
        }

        /// <summary>Signed distance to a rounded box (negative inside).</summary>
        static float RoundBoxDist(Vector2 p, float x, float y, float w, float h, float r)
        {
            var qx = Mathf.Abs(p.x - (x + w / 2)) - (w / 2 - r);
            var qy = Mathf.Abs(p.y - (y + h / 2)) - (h / 2 - r);
            var ox = Mathf.Max(qx, 0);
            var oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        /// <summary>Signed distance to an ellipse (a first-order estimate, fine for edges a few px wide).</summary>
        static float EllipseDist(Vector2 p, float cx, float cy, float rx, float ry)
        {
            var dx = p.x - cx;
            var dy = p.y - cy;
            var f = Mathf.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry));
            if (f < 1e-4f) return -Mathf.Min(rx, ry);
            var gx = dx / (rx * rx) / f;
            var gy = dy / (ry * ry) / f;
            return (f - 1) / Mathf.Max(Mathf.Sqrt(gx * gx + gy * gy), 1e-4f);
        }

        /// <summary>
        /// Paint a ruled ink border (cut-in corners, an ink dot in each corner) into `px`; (x, y, w, h) in design px from
        /// the texture's design origin `origin`.
        /// </summary>
        static void RuleBorder(Color[] px, int W, int H, int k, Vector2 origin, float x, float y, float w, float h, float r, float width, int seed)
        {
            var reach = width + 3;
            int y0 = Mathf.Max(0, Mathf.FloorToInt((origin.y + y - reach) * k)), y1 = Mathf.Min(H, Mathf.CeilToInt((origin.y + y + h + reach) * k));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((origin.x + x - reach) * k)), x1 = Mathf.Min(W, Mathf.CeilToInt((origin.x + x + w + reach) * k));
            for (int py = y0; py < y1; py++)
            {
                for (int pxi = x0; pxi < x1; pxi++)
                {
                    var p = new Vector2((pxi + 0.5f) / k - origin.x, (py + 0.5f) / k - origin.y);
                    // Far from every edge: nothing to draw.
                    if (p.x > x + r + reach && p.x < x + w - r - reach && p.y > y + reach && p.y < y + h - reach) continue;
                    if (p.y > y + r + reach && p.y < y + h - r - reach && p.x > x + reach && p.x < x + w - reach) continue;
                    var q = p + Wobble(p, seed);
                    var a = Stroke(ConcaveDist(q, x, y, w, h, r), width, k);
                    foreach (var c in new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x, y + h), new Vector2(x + w, y + h) })
                        a = Mathf.Max(a, Mathf.Clamp01((1.8f - Vector2.Distance(q, c)) * k + 0.5f));
                    if (a <= 0) continue;
                    var i = py * W + pxi;
                    px[i] = Over(px[i], Ink, a * px[i].a);
                }
            }
        }

        // --- Sheets ---

        /// <summary>Room around a sheet's texture for its drop shadow (design px).</summary>
        public const float Pad = 10;

        /// <summary>
        /// A parchment sheet's picture: drop shadow, deckled edge, stains, grain, a scorched rim and a thin dark edge; an
        /// optional watercolor wash (`wash` at `washAlpha`, inset 4 px) and a ruled border inset `rule` px (0: none).
        /// </summary>
        public static Texture2D SheetTexture(float w, float h, int seed, float burn = 0.4f, bool shadow = true, float rule = 8, float ruleR = 9,
            float ruleWidth = 1.7f, Color? wash = null, float washAlpha = 0.95f)
        {
            var k = Density;
            var key = $"sheet|{w}|{h}|{seed}|{burn}|{shadow}|{rule}|{ruleR}|{ruleWidth}|{(wash.HasValue ? Hex(wash.Value) : "-")}|{washAlpha}|{k}";
            return Cached(key, () =>
            {
                int W = Mathf.CeilToInt((w + Pad * 2) * k), H = Mathf.CeilToInt((h + Pad * 2) * k);
                var origin = new Vector2(Pad, Pad);
                var poly = Deckled(0, 0, w, h, seed);
                var cov = Fill(poly, W, H, k, origin);
                float[] shadowCov = null;
                if (shadow)
                {
                    shadowCov = Fill(poly, W, H, k, origin + new Vector2(2, 6));
                    Blur(shadowCov, W, H, Mathf.Max(1, Mathf.RoundToInt(3 * k)));
                }
                var burnCov = burn > 0 ? Blurred(cov, W, H, Mathf.RoundToInt(7 * k)) : null;
                var edgeCov = Blurred(cov, W, H, Mathf.Max(1, k / 2));
                float[] washCov = null, washInner = null;
                if (wash.HasValue)
                {
                    washCov = Fill(WavyRect(4, 4, w - 8, h - 8, seed + 40, 4, 1.2f, 50), W, H, k, origin);
                    washInner = Fill(WavyRect(5.2f, 5.2f, w - 10.4f, h - 10.4f, seed + 40, 4, 1.2f, 50), W, H, k, origin);
                }
                var washEdge = wash.HasValue ? Color.Lerp(wash.Value, Ink, 0.25f) : default;
                // Every sheet gets its own patch of the stain noise.
                var off = seed * 137 % 900;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var c = new Color(0, 0, 0, 0);
                        if (shadowCov != null) c = Over(c, ShadowColor, shadowCov[i] * 0.5f);
                        var fc = cov[i];
                        if (fc > 0)
                        {
                            float dx = x / (float)k - Pad, dy = y / (float)k - Pad;
                            // The mock's filters: stains 1.6 n - 0.75 (turbulence 0.011), grain 0.5 n - 0.2 (turbulence 0.9).
                            // Value noise spreads differently from turbulence: tuned by eye against the mock.
                            var stain = Mathf.Clamp01(1.6f * Contrast(Fractal((dx + off) * 0.011f, (dy + off / 2) * 0.011f, 2, 4), 1.5f) - 0.8f) * 0.8f;
                            var grain = Mathf.Clamp01(0.5f * Contrast(Fractal(dx * 0.9f, dy * 0.9f, seed + 3, 2)) - 0.2f);
                            var f = Color.Lerp(Parch, StainColor, stain);
                            f = Color.Lerp(f, GrainColor, grain);
                            if (burnCov != null)
                            {
                                var b = Mathf.Clamp01((1 - burnCov[i]) * 1.7f);
                                f = Color.Lerp(f, BurnColor, b * b * burn);
                            }
                            var e = edgeCov[i];
                            f = Color.Lerp(f, EdgeColor, Mathf.Clamp01(4 * e * (1 - e)) * 0.55f);
                            if (washCov != null && washCov[i] > 0)
                            {
                                // Granulation (the wash pools in the paper's tooth) and a darker rim where it dried.
                                var gran = 0.86f + 0.14f * Fractal(dx * 0.55f, dy * 0.55f, seed + 9, 2);
                                f = Color.Lerp(f, wash.Value, washCov[i] * gran * washAlpha);
                                f = Color.Lerp(f, washEdge, Mathf.Clamp01(washCov[i] - washInner[i]) * washAlpha * 0.8f);
                            }
                            c = Over(c, f, fc);
                        }
                        px[i] = c;
                    }
                }
                if (rule > 0) RuleBorder(px, W, H, k, origin, rule, rule, w - rule * 2, h - rule * 2, ruleR, ruleWidth, seed + 77);
                return ToTexture(px, W, H, "parchment sheet");
            });
        }

        /// <summary>A sheet on screen. `Root` is the whole picture (shadow room included); put content in `Content`.</summary>
        public class Sheet
        {
            public VisualElement Root, Content;
            public float Width, Height;
        }

        /// <summary>A parchment sheet at (x, y) top-left, w x h design px, tilted by `rotate` degrees.</summary>
        public static Sheet MakeSheet(VisualElement parent, float x, float y, float w, float h, int seed, float rotate = 0, bool shadow = true,
            float burn = 0.4f, float rule = 8, float ruleR = 9, float ruleWidth = 1.7f, Color? wash = null, float washAlpha = 0.95f, bool blocksClicks = true)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "paper" };
            Ui.Place(root, x - Pad, y - Pad);
            Ui.Size(root, w + Pad * 2, h + Pad * 2);
            root.style.backgroundImage = new StyleBackground(SheetTexture(w, h, seed, burn, shadow, rule, ruleR, ruleWidth, wash, washAlpha));
            if (rotate != 0) root.style.rotate = new Rotate(rotate);
            var content = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore };
            Ui.Place(content, Pad, Pad);
            Ui.Size(content, w, h);
            root.Add(content);
            parent?.Add(root);
            return new Sheet { Root = root, Content = content, Width = w, Height = h };
        }

        // --- Washes, chips, seals ---

        /// <summary>A watercolor wash, w x h: wavy edges, granulation, a darker dried rim (bar fills).</summary>
        public static Texture2D WashTexture(float w, float h, Color color, int seed, float alpha = 0.95f)
        {
            var k = Density;
            return Cached($"wash|{w}|{h}|{Hex(color)}|{seed}|{alpha}|{k}", () =>
            {
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt(h * k);
                var cov = Fill(WavyRect(0, 0, w, h, seed, 3, 0.8f, 40), W, H, k, Vector2.zero);
                var inner = Fill(WavyRect(1.2f, 1.2f, w - 2.4f, h - 2.4f, seed, 3, 0.8f, 40), W, H, k, Vector2.zero);
                var rim = Color.Lerp(color, Ink, 0.25f);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var gran = 0.86f + 0.14f * Fractal(x / (float)k * 0.55f, y / (float)k * 0.55f, seed + 9, 2);
                        var c = new Color(color.r, color.g, color.b, cov[i] * gran * alpha);
                        px[i] = Over(c, rim, Mathf.Clamp01(cov[i] - inner[i]) * alpha * 0.8f);
                    }
                }
                return ToTexture(px, W, H, "wash");
            });
        }

        /// <summary>A small wash with a thin ink rim, 64 x 24, drawn as a 9-slice behind chips ("Water", "Burn 2"). `solid`: on parchment.</summary>
        public static Texture2D ChipTexture(Color wash, Color ink, int seed, bool solid = false)
        {
            var k = Density;
            return Cached($"chip|{Hex(wash)}|{Hex(ink)}|{seed}|{solid}|{k}", () =>
            {
                const float w = 64, h = 24;
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt(h * k);
                var cov = Fill(WavyRect(0.5f, 0.5f, w - 1, h - 1, seed, 4, 0.7f, 30), W, H, k, Vector2.zero);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var p = new Vector2((x + 0.5f) / k, (y + 0.5f) / k);
                        var gran = 0.86f + 0.14f * Fractal(p.x * 0.55f, p.y * 0.55f, seed + 9, 2);
                        // On the scene (status chips) the wash sits on a scrap of parchment; on a sheet it's see-through.
                        var c = solid ? new Color(Parch.r, Parch.g, Parch.b, cov[i]) : new Color(0, 0, 0, 0);
                        c = Over(c, wash, cov[i] * gran * (solid ? 0.38f : 0.32f));
                        var q = p + Wobble(p, seed) * 0.4f;
                        c = Over(c, ink, Stroke(RoundBoxDist(q, 1, 1, w - 2, h - 2, 4), 1, k) * 0.45f);
                        px[i] = c;
                    }
                }
                return ToTexture(px, W, H, "chip");
            });
        }

        /// <summary>A word on a wash of its own color, sized to its text (elements, statuses, the hero's weapon).</summary>
        public static Label Chip(string rich, Color wash, Color ink, float fontSize = 15, int seed = 1, bool solid = false)
        {
            var l = new Label(rich) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = fontSize;
            l.style.color = ink;
            Fonts.Heavy(l);
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = 8;
            l.style.paddingTop = 0;
            l.style.paddingBottom = 1;
            l.style.height = Mathf.Round(fontSize + 8);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            var k = Density;
            l.style.backgroundImage = new StyleBackground(ChipTexture(wash, ink, seed, solid));
            l.style.unitySliceLeft = l.style.unitySliceRight = l.style.unitySliceTop = l.style.unitySliceBottom = 8 * k;
            l.style.unitySliceScale = 1f / k;
            return l;
        }

        /// <summary>A red wax seal (the key numbers on buttons): a lumpy disc with a pressed ring, `size` + 3 px shadow room each side.</summary>
        public static Texture2D SealTexture(float size, int seed)
        {
            var k = Density;
            return Cached($"seal|{size}|{seed}|{k}", () =>
            {
                const float pad = 3;
                int W = Mathf.CeilToInt((size + pad * 2) * k);
                var r = new Rand(seed);
                var c = size / 2;
                var poly = new List<Vector2>();
                for (int i = 0; i < 30; i++)
                {
                    var a = i / 30f * Mathf.PI * 2;
                    var rr = size * (13.4f + (r.Next() - 0.5f) * 2.4f) / 30f;
                    poly.Add(new Vector2(c + Mathf.Cos(a) * rr, c + Mathf.Sin(a) * rr));
                }
                var cov = Fill(poly, W, W, k, new Vector2(pad, pad));
                var sh = Fill(poly, W, W, k, new Vector2(pad + 1, pad + 2.5f));
                Blur(sh, W, W, Mathf.Max(1, Mathf.RoundToInt(2 * k)));
                Color g0 = Design.Hex(0xe05a44), g1 = Design.Hex(0xa8261a), g2 = Design.Hex(0x6a120c);
                Color press = Design.Hex(0x460804), shine = Design.Hex(0xffaa8c);
                var light = new Vector2(pad + size * 0.38f, pad + size * 0.32f);
                var mid = new Vector2(pad + c, pad + c);
                var ringR = size * 9.2f / 30f;
                var px = new Color[W * W];
                for (int y = 0; y < W; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var p = new Vector2((x + 0.5f) / k, (y + 0.5f) / k);
                        var col = Over(new Color(0, 0, 0, 0), Design.Hex(0x280602), sh[i] * 0.55f);
                        if (cov[i] > 0)
                        {
                            var t = Vector2.Distance(p, light) / (size * 0.7f);
                            var f = t < 0.45f ? Color.Lerp(g0, g1, t / 0.45f) : Color.Lerp(g1, g2, Mathf.Clamp01((t - 0.45f) / 0.55f));
                            f = Color.Lerp(f, press, Stroke(Vector2.Distance(p, mid) - ringR, 1.6f, k) * 0.6f);
                            f = Color.Lerp(f, shine, Stroke(Vector2.Distance(p, mid + new Vector2(0, size * 0.02f)) - ringR, 0.8f, k) * 0.35f);
                            col = Over(col, f, cov[i]);
                        }
                        px[i] = col;
                    }
                }
                return ToTexture(px, W, W, "wax seal");
            });
        }

        /// <summary>A wax seal with a number on it, its center at (x + 15, y + 15) (the 30 px seal's top-left at x, y).</summary>
        public static VisualElement Seal(VisualElement parent, float x, float y, string text, int seed, float size = 30)
        {
            const float pad = 3;
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = "seal" };
            Ui.Place(e, x - pad, y - pad);
            Ui.Size(e, size + pad * 2, size + pad * 2);
            e.style.backgroundImage = new StyleBackground(SealTexture(size, seed));
            var n = Ui.Text(e, pad + size / 2, pad + size / 2, text, 16, Design.Hex(0xfbe6cc), false, 0.5f, 0.5f);
            Fonts.Heavy(n);
            n.style.unityTextOutlineWidth = 0.15f;
            n.style.unityTextOutlineColor = Design.Hex(0x5a0f0a);
            parent?.Add(e);
            return e;
        }

        // --- The battle board: leather and a scroll ---

        /// <summary>A tooled leather band w x h with a gently cut top edge and its shadow above (10 px room on top).</summary>
        public static Texture2D LeatherTexture(float w, float h, int seed)
        {
            var k = Density;
            return Cached($"leather|{w}|{h}|{seed}|{k}", () =>
            {
                const float top = 10;
                int W = Mathf.CeilToInt(w * k), H = Mathf.CeilToInt((h + top) * k);
                var shape = WavyRect(-20, 0, w + 40, h + 40, seed, 2, 1.2f, 300);
                var cov = Fill(shape, W, H, k, new Vector2(0, top));
                var sh = Fill(shape, W, H, k, new Vector2(0, top - 5));
                Blur(sh, W, H, Mathf.Max(1, Mathf.RoundToInt(6 * k)));
                Color hide = new Color(0.55f, 0.36f, 0.2f), groove = new Color(0, 0, 0), rimTop = Design.Hex(0x7a5232);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var c = Over(new Color(0, 0, 0, 0), Design.Hex(0x0a0602), sh[i] * 0.55f);
                        if (cov[i] > 0)
                        {
                            float dx = x / (float)k, dy = y / (float)k - top;
                            // The mock's "hide" (big soft patches, stretched sideways) and fine leather grain.
                            var patch = Mathf.Clamp01(0.9f * Contrast(Fractal(dx * 0.006f, dy * 0.02f, seed, 3), 2.2f) - 0.42f);
                            var grain = Mathf.Clamp01(0.9f * Contrast(Fractal(dx * 0.38f, dy * 0.38f, seed + 5, 3)) - 0.33f) * 0.5f;
                            var f = Color.Lerp(Leather, hide, patch);
                            f = Color.Lerp(f, groove, grain);
                            // Two tooled lines (a dark groove with gilt pressed in under it) and a light rim on top.
                            f = Color.Lerp(f, groove, Stroke(dy - 9, 1.6f, k) * 0.5f);
                            f = Color.Lerp(f, Gilt, Stroke(dy - 10.4f, 1f, k) * 0.75f);
                            f = Color.Lerp(f, groove, Stroke(dy - 15, 1f, k) * 0.5f);
                            f = Color.Lerp(f, Gilt, Stroke(dy - 16.4f, 0.7f, k) * 0.75f);
                            f = Color.Lerp(f, rimTop, Stroke(dy - 1, 1.2f, k));
                            c = Over(c, f, cov[i]);
                        }
                        px[i] = c;
                    }
                }
                return ToTexture(px, W, H, "leather");
            });
        }

        /// <summary>The leather band across the bottom of the screen, its top edge at y.</summary>
        public static VisualElement Band(VisualElement parent, float y, float h, int seed)
        {
            var e = new VisualElement { pickingMode = PickingMode.Position, name = "leather" };
            Ui.Place(e, 0, y - 10);
            Ui.Size(e, Design.Width, h + 10);
            e.style.backgroundImage = new StyleBackground(LeatherTexture(Design.Width, h, seed));
            parent?.Add(e);
            return e;
        }

        /// <summary>One rolled end of a scroll, 26 px wide and `length` tall, with its wooden knobs (10 px room around).</summary>
        public static Texture2D RollTexture(float length)
        {
            var k = Density;
            return Cached($"roll|{length}|{k}", () =>
            {
                const float pad = 10, w = 26;
                int W = Mathf.CeilToInt((w + pad * 2) * k), H = Mathf.CeilToInt((length + pad * 2) * k);
                var sh = new float[W * H];
                Color[] stops = { Design.Hex(0x7a5630), Design.Hex(0xe9d3a2), Design.Hex(0xf3e2b8), Design.Hex(0xbf9a5e), Design.Hex(0x6e4a26) };
                float[] at = { 0, 0.3f, 0.55f, 0.8f, 1 };
                Color Grad(float t)
                {
                    for (int s = 1; s < at.Length; s++)
                        if (t <= at[s]) return Color.Lerp(stops[s - 1], stops[s], (t - at[s - 1]) / (at[s] - at[s - 1]));
                    return stops[stops.Length - 1];
                }
                // Shadow of the body, down-right.
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        var p = new Vector2((x + 0.5f) / k - pad - 1, (y + 0.5f) / k - pad - 2);
                        sh[y * W + x] = Mathf.Clamp01(-RoundBoxDist(p, 0, 0, w, length, 11) * k + 0.5f);
                    }
                Blur(sh, W, H, Mathf.Max(1, Mathf.RoundToInt(3 * k)));
                Color outline = Design.Hex(0x5a3a1c), knob = Design.Hex(0x3e2412), cap = Design.Hex(0x6b4422), capRim = Design.Hex(0x4a2c14), ring = Design.Hex(0xd9bd86);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var p = new Vector2((x + 0.5f) / k - pad, (y + 0.5f) / k - pad);
                        var c = Over(new Color(0, 0, 0, 0), ShadowColor, sh[i] * 0.45f);
                        var d = RoundBoxDist(p, 0, 0, w, length, 11);
                        c = Over(c, Grad(Mathf.Clamp01(p.x / w)), Mathf.Clamp01(-d * k + 0.5f));
                        c = Over(c, outline, Stroke(d, 1, k));
                        foreach (var cy in new[] { 0f, length })
                        {
                            var kd = RoundBoxDist(p, 8, cy < 1 ? cy - 8 : cy, 10, 8, 3);
                            c = Over(c, knob, Mathf.Clamp01(-kd * k + 0.5f));
                            var ed = EllipseDist(p, 13, cy, 12.5f, 4);
                            c = Over(c, cap, Mathf.Clamp01(-ed * k + 0.5f));
                            c = Over(c, capRim, Stroke(ed, 1, k));
                            c = Over(c, ring, Stroke(EllipseDist(p, 13, cy, 7, 2.2f), 0.9f, k));
                            c = Over(c, ring, Stroke(EllipseDist(p, 13, cy, 3, 1), 0.8f, k));
                        }
                        px[i] = c;
                    }
                }
                return ToTexture(px, W, H, "scroll roll");
            });
        }

        /// <summary>
        /// A small scroll at (x, y), w x h overall: a parchment sheet between two rolled ends. Text goes in `Content`
        /// (the open paper, from x + 26 to x + w - 26).
        /// </summary>
        public static Sheet Scroll(VisualElement parent, float x, float y, float w, float h, int seed, float rotate = 0)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "scroll" };
            Ui.Place(root, x, y);
            Ui.Size(root, w, h);
            if (rotate != 0) root.style.rotate = new Rotate(rotate);
            parent?.Add(root);
            MakeSheet(root, 14, 6, w - 28, h - 12, seed, burn: 0.3f, rule: 0, blocksClicks: false);
            foreach (var rx in new[] { 0f, w - 26 })
            {
                var roll = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(roll, rx - 10, -3 - 10);
                Ui.Size(roll, 26 + 20, h + 6 + 20);
                roll.style.backgroundImage = new StyleBackground(RollTexture(h + 6));
                root.Add(roll);
            }
            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(content, 26, 6);
            Ui.Size(content, w - 52, h - 12);
            root.Add(content);
            return new Sheet { Root = root, Content = content, Width = w - 52, Height = h - 12 };
        }

        // --- Bars ---

        /// <summary>A bar's frame: two sketchy ink outlines (a heavy one and a light one a little off), w x h plus 2 px room.</summary>
        public static Texture2D BarFrameTexture(float w, float h, int seed)
        {
            var k = Density;
            return Cached($"barframe|{w}|{h}|{seed}|{k}", () =>
            {
                const float pad = 2;
                int W = Mathf.CeilToInt((w + pad * 2) * k), H = Mathf.CeilToInt((h + pad * 2) * k);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var p = new Vector2((x + 0.5f) / k - pad, (y + 0.5f) / k - pad);
                        var q = p + Wobble(p, seed);
                        var a = Stroke(RoundBoxDist(q, 0, 0, w, h, 4), 1.6f, k);
                        var b = Stroke(RoundBoxDist(q, 0.9f, -0.7f, w - 1, h + 0.4f, 4), 0.7f, k) * 0.45f;
                        px[y * W + x] = new Color(Ink.r, Ink.g, Ink.b, Mathf.Max(a, b));
                    }
                }
                return ToTexture(px, W, H, "bar frame");
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

    /// <summary>A health or mana bar: a watercolor wash in a ruled ink frame (the mock's bars). (x, y) = left edge, middle.</summary>
    public class PaperBar : VisualElement
    {
        readonly VisualElement strip;
        readonly Label label;
        readonly float inner;
        readonly string prefix;
        float shown = -1;
        Anim.Handle tween;

        public PaperBar(VisualElement parent, float x, float y, float width, float height, Color color, string prefix, int seed, float fontSize = 18)
        {
            this.prefix = prefix;
            pickingMode = PickingMode.Ignore;
            Ui.Place(this, x, y - height / 2);
            Ui.Size(this, width, height);
            // The slot: a faint brown tint inside the frame.
            Ui.Rect(this, 0, 0, width, height, new Color(110 / 255f, 70 / 255f, 25 / 255f, 0.16f), 4);
            // The wash slides in from the left, so its painted right end always shows.
            inner = width - 4;
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 2, 2);
            Ui.Size(clip, inner, height - 4);
            clip.style.overflow = Overflow.Hidden;
            Add(clip);
            strip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(strip, 0, 0);
            Ui.Size(strip, inner, height - 4);
            strip.style.backgroundImage = new StyleBackground(Paper.WashTexture(inner, height - 4, color, seed));
            clip.Add(strip);
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(frame, -2, -2);
            Ui.Size(frame, width + 4, height + 4);
            frame.style.backgroundImage = new StyleBackground(Paper.BarFrameTexture(width, height, seed + 3));
            Add(frame);
            label = Ui.Text(this, width / 2, height / 2, "", fontSize, Paper.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(label);
            label.style.unityTextOutlineWidth = 0.2f;
            label.style.unityTextOutlineColor = Paper.Ink;
            parent?.Add(this);
        }

        void Show(float w) => strip.style.left = w - inner;

        public PaperBar SetValue(double current, double max, bool animate = true)
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
    /// A parchment tag button (the mock's battle buttons): a deckled sheet with a ruled border, tilted a little, an ink
    /// icon and an Alegreya label, an optional second line, and a wax seal with its key number at the top-left corner.
    /// `wash` tints the tag (Attack is soaked in red; skills get a light wash of their color). (x, y) is its center.
    /// </summary>
    public class PaperButton : VisualElement
    {
        readonly Action onClick;
        bool enabled = true, hover, down;

        public PaperButton(VisualElement parent, float x, float y, string text, Action onClick, float width = 196, float height = 64,
            string sublabel = null, string icon = null, Color? wash = null, float washAlpha = 0.32f, int seed = 1, float rotate = 0, string key = null,
            float fontSize = 24)
        {
            this.onClick = onClick;
            // A strong dark wash takes cream text; everything else is ink.
            var light = false;
            if (wash.HasValue)
            {
                Color.RGBToHSV(wash.Value, out _, out _, out var v);
                light = washAlpha >= 0.6f && v < 0.8f;
            }
            var ink = light ? Paper.Cream : Paper.Ink;
            Ui.Place(this, x - width / 2, y - height / 2);
            Ui.Size(this, width, height);
            pickingMode = PickingMode.Position;
            style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            if (rotate != 0) style.rotate = new Rotate(rotate);
            Paper.MakeSheet(this, 0, 0, width, height, seed, burn: 0.32f, rule: 5, ruleR: 6, ruleWidth: 1.4f, wash: wash, washAlpha: washAlpha,
                blocksClicks: false);

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
            var label = Flow(text, fontSize, ink);
            Fonts.Title(label);
            row.Add(label);
            if (light) Outline(label);
            if (sublabel != null)
            {
                var sub = Flow(sublabel, 15, light ? Paper.Cream : Design.Hex(0x6b4a2a));
                Fonts.Bold(sub);
                sub.style.marginTop = 2;
                column.Add(sub);
                if (light) Outline(sub);
            }
            if (key != null) Paper.Seal(this, -12, -12, key, seed + 30);

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

        static void Outline(Label l)
        {
            l.style.unityTextOutlineWidth = 0.18f;
            l.style.unityTextOutlineColor = Design.Hex(0x5a160c);
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
