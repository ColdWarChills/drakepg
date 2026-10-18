using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The felt UI kit (the mock art/ui/battle_felt.html, picked by the designer 2026-10-18): patches of felt with
    /// fuzzy edges, a soft nap and running stitches; sand-felt buttons with brown stitches (Attack in red felt), sewing
    /// buttons for key numbers, a felt runner along the bottom and a linen patch for the log. Everything is drawn in code
    /// (designed by hand, never generated: docs/PLANS.md, Art) and baked into textures at the screen's resolution,
    /// cached by size, colors and seed.
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
        static readonly Color WeaveColor = new Color(0.35f, 0.27f, 0.18f);

        /// <summary>A color made light enough to read as text on navy felt.</summary>
        public static Color OnFelt(Color c)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (s < 0.15f) return v < 0.6f ? TextDim : c;
            return Color.HSVToRGB(h, Mathf.Min(s, 0.55f), Mathf.Max(v, 0.88f));
        }

        /// <summary>Texels per design px for baked felt: the UI's screen scale, at least 1.</summary>
        public static int Density
        {
            get
            {
                var s = GameRoot.I != null && GameRoot.I.DesignRoot != null ? GameRoot.I.DesignRoot.resolvedStyle.scale.value.x : 1;
                return Mathf.Clamp(Mathf.CeilToInt(s), 1, 3);
            }
        }

        /// <summary>Texels per design px for a baked picture this big: big menu patches bake at 1 (soft felt hides it).</summary>
        static int DensityFor(float w, float h) => w * h > 250000 ? 1 : Density;

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

        const int TileSize = 256;
        static readonly Dictionary<string, float[]> tiles = new Dictionary<string, float[]>();

        /// <summary>Fine texture (felt nap, linen weave, edge fuzz) baked once per density on a 256-texel tile and repeated.</summary>
        static float[] Tile(string name, int k, Func<float, float, float> f)
        {
            var key = name + k;
            if (tiles.TryGetValue(key, out var t)) return t;
            t = new float[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    t[y * TileSize + x] = f(x / (float)k, y / (float)k);
            tiles[key] = t;
            return t;
        }

        static float TileAt(float[] t, int x, int y) => t[(y & (TileSize - 1)) * TileSize + (x & (TileSize - 1))];

        /// <summary>
        /// The felt's nap: light fibers in white. The mock's (0.5 n - 0.18 of turbulence at 0.9 per px) read as pepper
        /// noise in the game (designer: "super noisy"), so it is softer (0.45 per px) and about a third as strong.
        /// </summary>
        static float[] NapTile(int k) => Tile("nap2", k, (x, y) => Mathf.Clamp01(0.5f * Fractal(x * 0.45f, y * 0.45f, 31, 2) - 0.18f) * 0.35f);

        /// <summary>
        /// The mock's linen: turbulence stretched along x (0.02 x 0.9 per px), 0.45 n - 0.12 in brown, at half strength.
        /// Worked out per texel, not tiled: its long streaks would show the tile's seams.
        /// </summary>
        static float Weave(float x, float y) => Mathf.Clamp01(0.45f * Fractal(x * 0.02f, y * 0.9f, 41, 2) - 0.12f) * 0.45f;

        /// <summary>The mock's fuzzy edge: a displacement of up to 1.1 px from turbulence at 0.7 per px (two tiles, x and y).</summary>
        static float[] FuzzTile(int k, int axis) => Tile("fuzz" + axis, k, (x, y) => Noise(x * 0.7f, y * 0.7f, 51 + axis));

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

        /// <summary>Straight-alpha "over".</summary>
        static Color Over(Color dst, Color src, float a)
        {
            if (a <= 0) return dst;
            var outA = a + dst.a * (1 - a);
            if (outA <= 0) return new Color(0, 0, 0, 0);
            var k = dst.a * (1 - a);
            return new Color((src.r * a + dst.r * k) / outA, (src.g * a + dst.g * k) / outA, (src.b * a + dst.b * k) / outA, outA);
        }

        /// <summary>0 below a, 1 above b, smooth between.</summary>
        static float Smooth(float a, float b, float v)
        {
            var t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Stroke coverage of a line `width` px wide at distance `d` (design px), anti-aliased at `k` texels per px.</summary>
        static float Stroke(float d, float width, int k) => Mathf.Clamp01((width / 2 - Mathf.Abs(d)) * k + 0.5f);

        /// <summary>Signed distance to a rounded box (negative inside).</summary>
        static float RoundBoxDist(Vector2 p, float x, float y, float w, float h, float r)
        {
            var qx = Mathf.Abs(p.x - (x + w / 2)) - (w / 2 - r);
            var qy = Mathf.Abs(p.y - (y + h / 2)) - (h / 2 - r);
            var ox = Mathf.Max(qx, 0);
            var oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        /// <summary>A gentle hand wobble for thread lines (the mock's 0.06 per px displacement, smaller).</summary>
        static Vector2 Wobble(Vector2 p, int seed) =>
            new Vector2((Noise(p.x * 0.06f, p.y * 0.06f, seed) - 0.5f) * 0.8f, (Noise(p.x * 0.06f, p.y * 0.06f, seed + 1) - 0.5f) * 0.8f);

        // --- Stitches ---

        /// <summary>
        /// The nearest point on a rounded rectangle's outline: its distance `dn` and how far along the outline it is
        /// (`s`, clockwise from the top edge's start), so dashes can be laid along it.
        /// </summary>
        static void Nearest(Vector2 q, float x, float y, float w, float h, float r, out float dn, out float s)
        {
            float l1 = w - 2 * r, l2 = h - 2 * r, a = Mathf.PI * r / 2;
            var best = float.MaxValue;
            var bestS = 0f;
            void Seg(float ax, float ay, float bx, float by, float s0, float len)
            {
                var vx = bx - ax;
                var vy = by - ay;
                var len2 = vx * vx + vy * vy;
                var t = len2 > 0 ? Mathf.Clamp01(((q.x - ax) * vx + (q.y - ay) * vy) / len2) : 0;
                var dx = q.x - (ax + vx * t);
                var dy = q.y - (ay + vy * t);
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < best)
                {
                    best = d;
                    bestS = s0 + t * len;
                }
            }
            void Arc(float cx, float cy, float a0, float s0)
            {
                if (r <= 0) return;
                var ang = Mathf.Atan2(q.y - cy, q.x - cx) * Mathf.Rad2Deg - a0;
                while (ang > 180) ang -= 360;
                while (ang <= -180) ang += 360;
                ang = Mathf.Clamp(ang, 0, 90);
                var rad = (a0 + ang) * Mathf.Deg2Rad;
                var dx = q.x - (cx + Mathf.Cos(rad) * r);
                var dy = q.y - (cy + Mathf.Sin(rad) * r);
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < best)
                {
                    best = d;
                    bestS = s0 + ang * Mathf.Deg2Rad * r;
                }
            }
            Seg(x + r, y, x + w - r, y, 0, l1);
            Arc(x + w - r, y + r, -90, l1);
            Seg(x + w, y + r, x + w, y + h - r, l1 + a, l2);
            Arc(x + w - r, y + h - r, 0, l1 + a + l2);
            Seg(x + w - r, y + h, x + r, y + h, l1 + 2 * a + l2, l1);
            Arc(x + r, y + h - r, 90, 2 * l1 + 2 * a + l2);
            Seg(x, y + h - r, x, y + r, 2 * l1 + 3 * a + l2, l2);
            Arc(x + r, y + r, 180, 2 * l1 + 3 * a + 2 * l2);
            dn = best;
            s = bestS;
        }

        /// <summary>Distance to the nearest dash of a dashed line with round ends: `along` is how far outside a dash a point lies.</summary>
        static float DashDist(float dn, float s, float dash, float gap)
        {
            var period = dash + gap;
            var phase = s % period;
            if (phase < 0) phase += period;
            var along = phase <= dash ? 0 : Mathf.Min(phase - dash, period - phase);
            return Mathf.Sqrt(dn * dn + along * along);
        }

        /// <summary>
        /// Running stitches around a rounded rectangle (x, y, w, h in design px from the texture's `origin`): a thread
        /// shadow (0.8, 1.2 px down-right, like thread pulled through) then the thread, dashes `dash` long, `gap` apart.
        /// </summary>
        static void Stitches(Color[] px, int W, int H, int k, Vector2 origin, float x, float y, float w, float h, float r, float dash, float gap,
            float width, Color color, int seed)
        {
            r = Mathf.Min(r, w / 2, h / 2);
            var reach = width / 2 + 2.5f;
            int y0 = Mathf.Max(0, Mathf.FloorToInt((origin.y + y - reach) * k)), y1 = Mathf.Min(H, Mathf.CeilToInt((origin.y + y + h + reach) * k));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((origin.x + x - reach) * k)), x1 = Mathf.Min(W, Mathf.CeilToInt((origin.x + x + w + reach) * k));
            var shade = new Vector2(0.8f, 1.2f);
            for (int py = y0; py < y1; py++)
            {
                for (int pxi = x0; pxi < x1; pxi++)
                {
                    var p = new Vector2((pxi + 0.5f) / k - origin.x, (py + 0.5f) / k - origin.y);
                    if (Mathf.Abs(RoundBoxDist(p, x, y, w, h, r)) > reach) continue;
                    var q = p + Wobble(p, seed);
                    var i = py * W + pxi;
                    if (px[i].a <= 0) continue;
                    Nearest(q - shade, x, y, w, h, r, out var dn, out var s);
                    var a = Stroke(DashDist(dn, s, dash, gap), width * 1.1f, k) * 0.35f;
                    var c = px[i];
                    if (a > 0) c = Over(c, Color.black, a * c.a);
                    Nearest(q, x, y, w, h, r, out dn, out s);
                    a = Stroke(DashDist(dn, s, dash, gap), width, k) * color.a;
                    if (a > 0) c = Over(c, color, a * px[i].a);
                    px[i] = c;
                }
            }
        }

        /// <summary>A straight row of stitches from x0 to x1 at y (the stitch along a bar's felt strip).</summary>
        static void StitchLine(Color[] px, int W, int H, int k, float x0, float x1, float y, float dash, float gap, float width, Color color)
        {
            int ya = Mathf.Max(0, Mathf.FloorToInt((y - width) * k)), yb = Mathf.Min(H, Mathf.CeilToInt((y + width) * k));
            for (int py = ya; py < yb; py++)
            {
                for (int pxi = 0; pxi < W; pxi++)
                {
                    var p = new Vector2((pxi + 0.5f) / k, (py + 0.5f) / k);
                    float dn = Mathf.Abs(p.y - y), along;
                    if (p.x < x0) along = x0 - p.x;
                    else if (p.x > x1) along = p.x - x1;
                    else
                    {
                        var period = dash + gap;
                        var phase = (p.x - x0) % period;
                        along = phase <= dash ? 0 : Mathf.Min(phase - dash, period - phase);
                    }
                    var a = Stroke(Mathf.Sqrt(dn * dn + along * along), width, k) * color.a;
                    var i = py * W + pxi;
                    if (a > 0 && px[i].a > 0) px[i] = Over(px[i], color, a * px[i].a);
                }
            }
        }

        // --- Patches ---

        /// <summary>How a felt patch is made: its felt, its thread and where the stitches run.</summary>
        public class Patch
        {
            public Color Cloth = Navy;
            public Color? Thread = Felt.Stitch;
            public float Inset = 7, Radius = 16, Dash = 7, Gap = 5, ThreadWidth = 2.2f;
            /// <summary>Room around the picture for the drop shadow (design px); 0-2 for pieces without one.</summary>
            public float Pad = 10;
            public bool Shadow = true, Woven, Fuzzy = true;
            public float Nap = 1, Amp = 1.3f;
            /// <summary>A row of light stitches along the top (a bar's felt strip).</summary>
            public bool TopStitch;

            public string Key =>
                $"{Hex(Cloth)}|{(Thread.HasValue ? Hex(Thread.Value) : "-")}|{Inset}|{Radius}|{Dash}|{Gap}|{ThreadWidth}|{Pad}|{Shadow}|{Woven}|{Fuzzy}|{Nap}|{Amp}|{TopStitch}";

            /// <summary>A navy panel with cream stitches (the battle sheets, the HUD).</summary>
            public static Patch Panel() => new Patch();

            /// <summary>A big navy menu with a wider hem.</summary>
            public static Patch Menu() => new Patch { Inset = 10, Radius = 18, Dash = 9, Gap = 6, ThreadWidth = 2.4f };

            /// <summary>A small navy tag (the next move, the quest tracker, prompts).</summary>
            public static Patch Tag() => new Patch { Inset = 5, Radius = 10, Dash = 5, Gap = 4, ThreadWidth = 1.8f };

            /// <summary>A sand button with brown stitches; any cloth (red Attack, a skill's color) with its own thread.</summary>
            public static Patch Button(Color? cloth = null, Color? thread = null) =>
                new Patch { Cloth = cloth ?? Sand, Thread = thread ?? SandStitch, Inset = 5, Radius = 12, Dash = 5, Gap = 4 };

            /// <summary>The runner along the bottom of the battle screen.</summary>
            public static Patch Runner() => new Patch { Cloth = NavyDark, Thread = Hem, Inset = 9, Radius = 22, Dash = 9, Gap = 6 };

            /// <summary>The linen patch the battle log is written on.</summary>
            public static Patch LinenLog() =>
                new Patch { Cloth = Linen, Thread = LinenStitch, Inset = 7, Radius = 10, Dash = 6, Gap = 5, ThreadWidth = 2, Woven = true, Fuzzy = false, Nap = 0, Amp = 1.2f };
        }

        /// <summary>
        /// A felt patch's picture: drop shadow, fuzzy edge (the cloth's outline displaced by noise, like the mock's
        /// filter), soft nap, then running stitches inset `Inset` px. `Pad` px of room around it for the shadow.
        /// </summary>
        public static Texture2D PatchTexture(float w, float h, int seed, Patch p)
        {
            var k = DensityFor(w, h);
            return Cached($"patch|{w}|{h}|{seed}|{p.Key}|{k}", () =>
            {
                var pad = p.Pad;
                int W = Mathf.CeilToInt((w + pad * 2) * k), H = Mathf.CeilToInt((h + pad * 2) * k);
                var origin = new Vector2(pad, pad);
                var cov = Fill(WavyRect(0, 0, w, h, seed, p.Radius, p.Amp, 80), W, H, k, origin);
                var fuzzX = FuzzTile(k, 0);
                var fuzzY = FuzzTile(k, 1);
                var nap = NapTile(k);
                var maxShift = p.Fuzzy ? Mathf.RoundToInt(1.1f * k) : 0;
                float cr = p.Cloth.r, cg = p.Cloth.g, cb = p.Cloth.b, sr = ShadowColor.r, sg = ShadowColor.g, sb = ShadowColor.b;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    var dy = y / (float)k - pad;
                    for (int x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        var dx = x / (float)k - pad;
                        // Fuzzy edge: read the cloth's coverage a little off to the side, by noise.
                        var ox = Mathf.Clamp(x + Mathf.RoundToInt((TileAt(fuzzX, x + seed * 7, y) - 0.5f) * 2 * maxShift), 0, W - 1);
                        var oy = Mathf.Clamp(y + Mathf.RoundToInt((TileAt(fuzzY, x, y + seed * 5) - 0.5f) * 2 * maxShift), 0, H - 1);
                        var fc = cov[oy * W + ox];
                        // Its drop shadow (2 px right, 6 down, soft ~4 px), worked out from the distance to its box.
                        var sa = p.Shadow && fc < 1 ? 0.5f * (1 - Smooth(-4, 5, RoundBoxDist(new Vector2(dx - 2, dy - 6), 0, 0, w, h, p.Radius))) : 0;
                        if (fc <= 0)
                        {
                            px[i] = new Color(sr, sg, sb, sa);
                            continue;
                        }
                        float r = cr, g = cg, b = cb;
                        if (p.Woven)
                        {
                            var t = Weave(dx + seed * 37, dy);
                            r += (WeaveColor.r - r) * t;
                            g += (WeaveColor.g - g) * t;
                            b += (WeaveColor.b - b) * t;
                        }
                        if (p.Nap > 0)
                        {
                            // The nap: light fibers catching the light.
                            var t = TileAt(nap, x + seed * 31, y + seed * 17) * p.Nap;
                            r += (1 - r) * t;
                            g += (1 - g) * t;
                            b += (1 - b) * t;
                        }
                        if (fc >= 1 || sa <= 0)
                        {
                            px[i] = new Color(r, g, b, fc);
                            continue;
                        }
                        var outA = fc + sa * (1 - fc);
                        var kk = sa * (1 - fc);
                        px[i] = new Color((r * fc + sr * kk) / outA, (g * fc + sg * kk) / outA, (b * fc + sb * kk) / outA, outA);
                    }
                }
                if (p.Thread.HasValue)
                    Stitches(px, W, H, k, origin, p.Inset, p.Inset, w - p.Inset * 2, h - p.Inset * 2, Mathf.Max(4, p.Radius - p.Inset), p.Dash, p.Gap,
                        p.ThreadWidth, p.Thread.Value, seed + 77);
                if (p.TopStitch) StitchLine(px, W, H, k, pad + 7, pad + w - 8, pad + 3, 5, 4, 1.4f, new Color(1, 240 / 255f, 215 / 255f, 0.45f));
                return ToTexture(px, W, H, "felt patch");
            });
        }

        /// <summary>A patch on screen. `Root` is the whole picture (shadow room included); put content in `Content`.</summary>
        public class Sheet
        {
            public VisualElement Root, Content;
            public float Width, Height;
        }

        /// <summary>A felt patch at (x, y) top-left, w x h design px, tilted by `rotate` degrees.</summary>
        public static Sheet MakePatch(VisualElement parent, float x, float y, float w, float h, int seed, Patch patch = null, float rotate = 0,
            bool blocksClicks = true)
        {
            patch ??= Patch.Panel();
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "felt" };
            Ui.Place(root, x - patch.Pad, y - patch.Pad);
            Ui.Size(root, w + patch.Pad * 2, h + patch.Pad * 2);
            root.style.backgroundImage = new StyleBackground(PatchTexture(w, h, seed, patch));
            if (rotate != 0) root.style.rotate = new Rotate(rotate);
            var content = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore };
            Ui.Place(content, patch.Pad, patch.Pad);
            Ui.Size(content, w, h);
            root.Add(content);
            parent?.Add(root);
            return new Sheet { Root = root, Content = content, Width = w, Height = h };
        }

        /// <summary>A felt patch as a world sprite, centered on its transform (cards the world's figures stand on).</summary>
        public static Sprite PatchSprite(float w, float h, int seed, Patch patch = null)
        {
            var tex = PatchTexture(w, h, seed, patch ?? Patch.Panel());
            var ppu = Design.PixelsPerUnit * tex.width / (w + (patch?.Pad ?? 10) * 2);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// Felt behind an element that sizes itself (a quest tracker, a prompt): a small stitched patch, 9-sliced to
        /// fit, its shadow spilling out by its pad. The element's colors are themed for felt (FeltPass).
        /// </summary>
        public static VisualElement CardBehind(VisualElement e, int seed, Patch patch = null)
        {
            patch ??= Patch.Tag();
            var k = Density;
            var bg = new VisualElement { pickingMode = PickingMode.Ignore, name = "card" };
            bg.style.position = Position.Absolute;
            bg.style.left = bg.style.top = bg.style.right = bg.style.bottom = -patch.Pad;
            bg.style.backgroundImage = new StyleBackground(PatchTexture(160, 80, seed, patch));
            bg.style.unitySliceLeft = bg.style.unitySliceRight = bg.style.unitySliceTop = bg.style.unitySliceBottom =
                Mathf.RoundToInt((patch.Pad + patch.Inset + patch.Radius) * k);
            bg.style.unitySliceScale = 1f / k;
            e.Insert(0, bg);
            e.AddToClassList(FeltPass.Class);
            e.style.backgroundColor = Color.clear;
            return bg;
        }

        // --- Chips and sewing buttons ---

        /// <summary>A small felt chip in a color, 64 x 24 (2 px room), drawn as a 9-slice behind a word ("Water", "Burn 2").</summary>
        public static Texture2D ChipTexture(Color color, int seed) =>
            PatchTexture(64, 24, seed, new Patch { Cloth = Color.Lerp(NavyDark, color, 0.45f), Thread = null, Radius = 7, Pad = 2, Shadow = false, Nap = 0.6f, Amp = 0.4f });

        /// <summary>A word on a small felt chip of its color, light text in that color (elements, statuses, the hero's weapon).</summary>
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
            var k = Density;
            l.style.backgroundImage = new StyleBackground(ChipTexture(color, seed));
            l.style.unitySliceLeft = l.style.unitySliceRight = l.style.unitySliceTop = l.style.unitySliceBottom = 10 * k;
            l.style.unitySliceScale = 1f / k;
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

        // --- The battle board ---

        /// <summary>The felt runner across the bottom of the screen, its top edge at y (it runs off the sides and bottom).</summary>
        public static Sheet Runner(VisualElement parent, float y, int seed) =>
            MakePatch(parent, -12, y, Design.Width + 24, 200, seed, Patch.Runner());

        // --- Bars ---

        /// <summary>A bar's groove: a dark felt slot, rounded, 2 px room.</summary>
        public static Texture2D GrooveTexture(float w, float h, int seed) =>
            PatchTexture(w, h, seed, new Patch { Cloth = Groove, Thread = null, Radius = h / 2, Pad = 2, Shadow = false, Nap = 0.4f, Amp = 0.3f });

        /// <summary>A bar's felt strip, w x h, with a row of light stitches along its top.</summary>
        public static Texture2D StripTexture(float w, float h, Color color, int seed) =>
            PatchTexture(w, h, seed, new Patch { Cloth = color, Thread = null, Radius = h / 2, Pad = 0, Shadow = false, Amp = 0.3f, TopStitch = true });

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

    /// <summary>A health or mana bar: a felt strip in a dark felt groove (the mock's bars). (x, y) = left edge, middle.</summary>
    public class FeltBar : VisualElement
    {
        readonly VisualElement strip;
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
            var groove = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(groove, -2, -2);
            Ui.Size(groove, width + 4, height + 4);
            groove.style.backgroundImage = new StyleBackground(Felt.GrooveTexture(width, height, seed));
            Add(groove);
            // The strip slides in from the left, so its rounded right end always shows.
            inner = width - 6;
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 3, 3);
            Ui.Size(clip, inner, height - 6);
            clip.style.overflow = Overflow.Hidden;
            Ui.Round(clip, (height - 6) / 2);
            Add(clip);
            strip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(strip, 0, 0);
            Ui.Size(strip, inner, height - 6);
            strip.style.backgroundImage = new StyleBackground(Felt.StripTexture(inner, height - 6, color, seed + 1));
            clip.Add(strip);
            label = Ui.Text(this, width / 2, height / 2, "", fontSize, Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(label);
            label.style.unityTextOutlineWidth = 0.2f;
            label.style.unityTextOutlineColor = Design.Hex(0x1a1410);
            parent?.Add(this);
        }

        void Show(float w) => strip.style.left = w - inner;

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
            float fontSize = 24)
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
            Felt.MakePatch(this, 0, 0, width, height, seed, Felt.Patch.Button(face, thread), blocksClicks: false);

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
                var ic = Felt.Icon(null, 0, 0, 26, icon, dark);
                ic.style.position = Position.Relative;
                ic.style.marginRight = 8;
                row.Add(ic);
            }
            var label = Flow(text, fontSize, ink);
            Fonts.Title(label);
            row.Add(label);
            if (sublabel != null)
            {
                var sub = Flow(sublabel, 15, dark ? Felt.Cream : Felt.BrownDim);
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

        /// <summary>Lift on hover, press down, fade when disabled (the patch's shadow stays put, like cloth lifting).</summary>
        void Paint()
        {
            var lift = !enabled ? 0 : down ? 1 : hover ? -3 : 0;
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
