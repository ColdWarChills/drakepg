using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// The effects' textures and materials: the particle textures of createFxTextures (fx.ts) drawn in code at first
    /// use (no art needed), the shapes Phaser drew with Graphics (slash arcs, the cast circle), and the FxSprite materials.
    /// Sizes are design px, drawn at `Density` texels per design px so they stay crisp on big screens.
    /// </summary>
    public static partial class Fx
    {
        /// <summary>The particle textures (fx_soft, fx_spark, ... in fx.ts).</summary>
        public enum Tex { Soft, Spark, Star, Leaf, Coin, Ring, Rock, Plus, Chevron, Streak, Drop, Splat, Splash, Impact, Spatter }

        const float Density = 2;
        static Sprite[] sprites;

        /// <summary>
        /// Draws the particle textures now (createFxTextures). Optional: they are drawn on first use anyway; calling this
        /// at boot moves the few ms of drawing out of the first hit.
        /// </summary>
        public static void CreateTextures()
        {
            if (sprites != null)
            {
                bool ok = true;
                foreach (var s in sprites) ok &= s != null;
                if (ok) return;
            }
            sprites = new Sprite[Enum.GetValues(typeof(Tex)).Length];

            // Soft glowing dot.
            var c = new Canvas(0, 0, 32, 32);
            for (int r = 16; r > 0; r--) c.Fill(Disc(16, 16, r, c.D), 0xffffff, 0.12f + (1 - r / 16f) * 0.25f);
            sprites[(int)Tex.Soft] = c.ToSprite("fx_soft");

            // Spark: thin diamond.
            c = new Canvas(0, 0, 8, 24);
            c.Fill(Inside(c.D, Poly(4, 0, 8, 12, 0, 12)), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(0, 12, 8, 12, 4, 24)), 0xffffff, 1);
            sprites[(int)Tex.Spark] = c.ToSprite("fx_spark");

            // Four-point star.
            c = new Canvas(0, 0, 24, 24);
            c.Fill(Inside(c.D, Poly(12, 0, 15, 12, 9, 12)), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(12, 24, 15, 12, 9, 12)), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(0, 12, 12, 9, 12, 15)), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(24, 12, 12, 9, 12, 15)), 0xffffff, 1);
            c.Fill(Disc(12, 12, 3, c.D), 0xffffff, 1);
            sprites[(int)Tex.Star] = c.ToSprite("fx_star");

            // Leaf.
            c = new Canvas(0, 0, 18, 12);
            c.Fill(Inside(c.D, Ellipse(9, 6, 9, 4.5f)), 0xffffff, 1);
            sprites[(int)Tex.Leaf] = c.ToSprite("fx_leaf");

            // Coin.
            c = new Canvas(0, 0, 20, 20);
            c.Fill(Disc(10, 10, 10, c.D), 0x9a6b00, 1);
            c.Fill(Disc(10, 10, 8, c.D), 0xf1c40f, 1);
            c.Fill(Disc(7, 7, 3, c.D), 0xfff3a0, 1);
            sprites[(int)Tex.Coin] = c.ToSprite("fx_coin");

            // Ring for shockwaves (a 6 px line around radius 28).
            c = new Canvas(0, 0, 64, 64);
            c.Fill(Annulus(32, 32, 25, 31, c.D), 0xffffff, 1);
            sprites[(int)Tex.Ring] = c.ToSprite("fx_ring");

            // Rock chunk (earth hits), lit from the top left.
            c = new Canvas(0, 0, 20, 20);
            c.Fill(Inside(c.D, Poly(3, 6, 9, 1, 16, 3, 19, 11, 13, 18, 4, 16)), 0xbbbbbb, 1);
            c.Fill(Inside(c.D, Poly(4, 7, 9, 3, 14, 5, 9, 10)), 0xffffff, 1);
            sprites[(int)Tex.Rock] = c.ToSprite("fx_rock");

            // Plus sign (healing).
            c = new Canvas(0, 0, 22, 22);
            c.Fill(Inside(c.D, RoundRect(7, 0, 8, 22, 3)), 0xffffff, 1);
            c.Fill(Inside(c.D, RoundRect(0, 7, 22, 8, 3)), 0xffffff, 1);
            sprites[(int)Tex.Plus] = c.ToSprite("fx_plus");

            // Chevron pointing up (buffs rise, debuffs sink flipped).
            c = new Canvas(0, 0, 28, 22);
            c.Fill(Inside(c.D, Poly(0, 14, 14, 0, 28, 14, 28, 22, 14, 8, 0, 22)), 0xffffff, 1);
            sprites[(int)Tex.Chevron] = c.ToSprite("fx_chevron");

            // Long thin streak (speed lines, crit rays).
            c = new Canvas(0, 0, 96, 6);
            for (int i = 0; i < 4; i++) c.Fill(Inside(c.D, Ellipse(48, 3, (96 - i * 20) / 2f, (6 - i) / 2f)), 0xffffff, 0.25f + i * 0.25f);
            sprites[(int)Tex.Streak] = c.ToSprite("fx_streak");

            // A drop of liquid in flight: a round head on top and a tail that tapers off below (turned along its flight,
            // head first).
            c = new Canvas(0, 0, 10, 26);
            c.Fill(Disc(5, 5.5f, 5, c.D), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(0.4f, 6, 9.6f, 6, 5, 26)), 0xffffff, 1);
            sprites[(int)Tex.Drop] = c.ToSprite("fx_drop");

            // A splat on the ground: a blob with lobes and a few flecks around it (squashed flat when drawn).
            c = new Canvas(0, 0, 48, 48);
            float[] blobs = { 24, 24, 13, 13, 20, 6.5f, 35, 27, 7, 26, 37, 5.5f, 20, 12, 5, 37, 15, 4.5f, 7, 31, 2.5f, 43, 39, 2.5f, 31, 5, 2, 9, 43, 2 };
            for (int i = 0; i < blobs.Length; i += 3) c.Fill(Disc(blobs[i], blobs[i + 1], blobs[i + 2], c.D), 0xffffff, 1);
            sprites[(int)Tex.Splat] = c.ToSprite("fx_splat");

            // A splash on the ground from a drop that landed moving right: a ragged blob, then a tongue and a run of
            // droplets splattering on to the right (flipped for drops moving left, squashed flat when drawn).
            c = new Canvas(0, 0, 72, 36);
            float[] splash = { 20, 18, 12, 10, 12, 6, 12, 26, 5.5f, 27, 10, 5, 29, 25, 5, 56, 18, 3.4f, 63, 16.5f, 2.4f, 69, 19.5f, 1.6f, 36, 6, 2, 45, 30, 1.8f, 4, 28, 1.6f, 49, 7, 1.4f };
            for (int i = 0; i < splash.Length; i += 3) c.Fill(Disc(splash[i], splash[i + 1], splash[i + 2], c.D), 0xffffff, 1);
            c.Fill(Inside(c.D, Poly(26, 13, 53, 16.5f, 53, 19.5f, 26, 24)), 0xffffff, 1);
            sprites[(int)Tex.Splash] = c.ToSprite("fx_splash");

            // A comic impact: a jagged burst of uneven spikes (flat, no glow), drawn where a blow lands.
            c = new Canvas(0, 0, 64, 64);
            float[] spikes = { 31, 12, 27, 14, 30, 11, 25, 13, 31, 12, 28, 14 };
            var burst = new float[spikes.Length * 2];
            for (int i = 0; i < spikes.Length; i++)
            {
                float a = i / (float)spikes.Length * Mathf.PI * 2 + (i % 2 == 0 ? 0 : 0.08f);
                burst[i * 2] = 32 + Mathf.Cos(a) * spikes[i];
                burst[i * 2 + 1] = 32 + Mathf.Sin(a) * spikes[i];
            }
            c.Fill(Inside(c.D, Poly(burst)), 0xffffff, 1);
            sprites[(int)Tex.Impact] = c.ToSprite("fx_impact");

            // A blood spatter where a blow lands: a small ragged core and tapered spikes flung out of it, longest the way
            // the blow travels (+x), each ending in a droplet.
            c = new Canvas(0, 0, 96, 64);
            const float cx = 30, cy = 32;
            c.Fill(Disc(cx, cy, 10, c.D), 0xffffff, 1);
            c.Fill(Disc(cx - 5, cy - 6, 6, c.D), 0xffffff, 1);
            c.Fill(Disc(cx + 4, cy + 7, 6, c.D), 0xffffff, 1);
            // (angle in degrees from +x, length, width at the core)
            float[] arms = { 0, 46, 8, 17, 38, 7, -19, 40, 7, 38, 28, 6, -41, 30, 6, 62, 19, 5, -66, 21, 5, 98, 15, 4, -102, 14, 4, 138, 13, 4, -150, 12, 4, 178, 15, 5 };
            for (int i = 0; i < arms.Length; i += 3)
            {
                float a = arms[i] * Mathf.Deg2Rad, len = arms[i + 1], wd = arms[i + 2];
                float dx = Mathf.Cos(a), dy = Mathf.Sin(a), nx = -dy, ny = dx;
                float bx = cx + dx * 6, by = cy + dy * 6, tx = cx + dx * len, ty = cy + dy * len;
                c.Fill(Inside(c.D, Poly(bx + nx * wd / 2, by + ny * wd / 2, tx, ty, bx - nx * wd / 2, by - ny * wd / 2)), 0xffffff, 1);
                c.Fill(Disc(cx + dx * (len + wd * 0.35f), cy + dy * (len + wd * 0.35f), wd * 0.42f, c.D), 0xffffff, 1);
            }
            sprites[(int)Tex.Spatter] = c.ToSprite("fx_spatter");
        }

        /// <summary>A particle texture as a sprite: scale 1 draws it at its design size (fx_soft is 32 px).</summary>
        public static Sprite Get(Tex t)
        {
            if (sprites == null || sprites[(int)t] == null) CreateTextures();
            return sprites[(int)t];
        }

        /// <summary>The web's texture keys ("fx_star") to Tex.</summary>
        public static Tex TexOf(string key)
        {
            switch (key)
            {
                case "fx_spark": return Tex.Spark;
                case "fx_star": return Tex.Star;
                case "fx_leaf": return Tex.Leaf;
                case "fx_coin": return Tex.Coin;
                case "fx_ring": return Tex.Ring;
                case "fx_rock": return Tex.Rock;
                case "fx_plus": return Tex.Plus;
                case "fx_chevron": return Tex.Chevron;
                case "fx_streak": return Tex.Streak;
                default: return Tex.Soft;
            }
        }

        // --- Shapes Phaser drew with Graphics ---

        /// <summary>A generated shape and where its center sits relative to the shape's origin (design px, y down).</summary>
        struct Shape
        {
            public Sprite Sprite;
            public Vector2 Center;
        }

        static readonly Dictionary<string, Shape> shapes = new Dictionary<string, Shape>();

        /// <summary>A stroked arc around (0, 0) from -`half` to +`half` radians (centered on +x), `width` thick, butt ends.</summary>
        static Shape ArcShape(float r, float width, float half)
        {
            var key = "arc" + r + "/" + width + "/" + half;
            if (shapes.TryGetValue(key, out var s) && s.Sprite != null) return s;
            float outer = r + width / 2, inner = r - width / 2;
            float x0 = Mathf.Floor(inner * Mathf.Cos(half) - 2), y0 = Mathf.Floor(-outer * Mathf.Sin(half) - 2);
            var c = new Canvas(x0, y0, Mathf.Ceil(outer + 2), -y0);
            c.Fill(Arc(0, 0, r, width, -half, half, c.D), 0xffffff, 1);
            s = new Shape { Sprite = c.ToSprite(key), Center = c.Center };
            shapes[key] = s;
            return s;
        }

        /// <summary>The magic circle of castCircle: two rings, six rune dots and a hexagram, white (tinted when drawn).</summary>
        static Shape CircleShape()
        {
            const string key = "cast circle";
            if (shapes.TryGetValue(key, out var s) && s.Sprite != null) return s;
            // Drawn additively like the Graphics it replaces: where lines cross they add up.
            var c = new Canvas(-84, -84, 84, 84) { Additive = true };
            c.Fill(Annulus(0, 0, 78, 82, c.D), 0xffffff, 0.9f);
            c.Fill(Annulus(0, 0, 63, 65, c.D), 0xffffff, 0.7f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2, dx = Mathf.Cos(a) * 72, dy = Mathf.Sin(a) * 72;
                c.Fill(Disc(dx, dy, 4, c.D), 0xffffff, 1, dx - 5, dy - 5, dx + 5, dy + 5);
            }
            foreach (var rot in new[] { -Mathf.PI / 2, Mathf.PI / 2 })
            {
                for (int k = 0; k < 3; k++)
                {
                    float a = rot + k / 3f * Mathf.PI * 2, b = rot + (k + 1) / 3f * Mathf.PI * 2;
                    float ax = Mathf.Cos(a) * 60, ay = Mathf.Sin(a) * 60, bx = Mathf.Cos(b) * 60, by = Mathf.Sin(b) * 60;
                    c.Fill(Segment(ax, ay, bx, by, 2, c.D), 0xffffff, 0.6f,
                        Mathf.Min(ax, bx) - 2, Mathf.Min(ay, by) - 2, Mathf.Max(ax, bx) + 2, Mathf.Max(ay, by) + 2);
                }
            }
            s = new Shape { Sprite = c.ToSprite(key), Center = c.Center };
            shapes[key] = s;
            return s;
        }

        // --- Materials ---

        static Shader shader;
        static bool shaderMissing;
        static Material matAlpha, matAdd, matFill, matFillAdd;

        /// <summary>Unlit alpha-blended sprites (smoke, dust, confetti).</summary>
        public static Material AlphaMaterial => matAlpha != null ? matAlpha : matAlpha = NewMaterial(false, false);

        /// <summary>Additive sprites (Phaser's blendMode 'ADD': sparks, glows, rings).</summary>
        public static Material AdditiveMaterial => matAdd != null ? matAdd : matAdd = NewMaterial(true, false);

        /// <summary>Paints a sprite one solid color (its renderer color) through its alpha: hit flashes, ghosts.</summary>
        public static Material FillMaterial => matFill != null ? matFill : matFill = NewMaterial(false, true);

        /// <summary>Like FillMaterial but additive: Sigil glow halos.</summary>
        public static Material FillAdditiveMaterial => matFillAdd != null ? matFillAdd : matFillAdd = NewMaterial(true, true);

        static Material NewMaterial(bool add, bool fill)
        {
            if (shader == null && !shaderMissing)
            {
                shader = Resources.Load<Shader>("Fx/FxSprite");
                if (shader == null) shader = Shader.Find("Drakepg/FxSprite");
                if (shader == null)
                {
                    shaderMissing = true;
                    Debug.LogWarning("Fx: Resources/Fx/FxSprite.shader is missing; effects fall back to the default sprite material.");
                }
            }
            if (shader == null) return null;
            var m = new Material(shader) { name = "Fx " + (fill ? "fill " : "") + (add ? "additive" : "alpha") };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Fill", fill ? 1 : 0);
            return m;
        }

        /// <summary>Sets a renderer's material when there is one (without the shader, sprites keep their default).</summary>
        static MaterialPropertyBlock texBlock;
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Pin a sprite renderer's texture in its own property block. Effect sprites share a few materials, and without
        /// this Unity now and then drew one with another's texture (a crisp blood spatter came out as the soft glow dot:
        /// "circles instead of splats"). Call after setting the sprite.
        /// </summary>
        public static void BindTexture(SpriteRenderer r)
        {
            if (r == null || r.sprite == null) return;
            texBlock ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(texBlock);
            texBlock.SetTexture(MainTexId, r.sprite.texture);
            r.SetPropertyBlock(texBlock);
        }

        static void Use(Renderer r, Material m)
        {
            if (m != null) r.sharedMaterial = m;
        }

        // --- Drawing (coverage functions in design px, y down) ---

        static Func<float, float, float> Disc(float cx, float cy, float r, float d) => (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) * d + 0.5f);
        };

        static Func<float, float, float> Annulus(float cx, float cy, float rIn, float rOut, float d) => (x, y) =>
        {
            float dx = x - cx, dy = y - cy, dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(Mathf.Min(rOut - dist, dist - rIn) * d + 0.5f);
        };

        static Func<float, float, float> Segment(float ax, float ay, float bx, float by, float width, float d) => (x, y) =>
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / Mathf.Max(0.0001f, dx * dx + dy * dy));
            float qx = ax + dx * t - x, qy = ay + dy * t - y;
            return Mathf.Clamp01((width / 2 - Mathf.Sqrt(qx * qx + qy * qy)) * d + 0.5f);
        };

        static Func<float, float, float> Arc(float cx, float cy, float r, float width, float a0, float a1, float d) => (x, y) =>
        {
            float dx = x - cx, dy = y - cy, rho = Mathf.Sqrt(dx * dx + dy * dy);
            float radial = Mathf.Clamp01((width / 2 - Mathf.Abs(rho - r)) * d + 0.5f);
            if (radial <= 0) return 0;
            float a = Mathf.Atan2(dy, dx);
            return radial * Mathf.Clamp01(Mathf.Min(a - a0, a1 - a) * rho * d + 0.5f);
        };

        static Func<float, float, bool> Ellipse(float cx, float cy, float rx, float ry) => (x, y) =>
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1;
        };

        static Func<float, float, bool> RoundRect(float rx, float ry, float w, float h, float rad) => (x, y) =>
        {
            if (x < rx || x > rx + w || y < ry || y > ry + h) return false;
            float cx = Mathf.Clamp(x, rx + rad, rx + w - rad), cy = Mathf.Clamp(y, ry + rad, ry + h - rad);
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= rad * rad;
        };

        /// <summary>A filled polygon (x, y pairs), even-odd like a simple polygon fill.</summary>
        static Func<float, float, bool> Poly(params float[] p) => (x, y) =>
        {
            bool inside = false;
            int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        };

        /// <summary>Coverage of an inside test by 4x4 samples over the texel around (x, y).</summary>
        static Func<float, float, float> Inside(float d, Func<float, float, bool> inside) => (x, y) =>
        {
            int n = 0;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (inside(x + ((sx + 0.5f) / 4 - 0.5f) / d, y + ((sy + 0.5f) / 4 - 0.5f) / d)) n++;
            return n / 16f;
        };

        /// <summary>A small RGBA canvas in design px (y down) that bakes into a sprite with a center pivot.</summary>
        sealed class Canvas
        {
            public readonly int W, H;
            public readonly float D, Ox, Oy;
            /// <summary>Layers add up (alpha sums) instead of covering each other.</summary>
            public bool Additive;
            readonly Color[] px;

            public Canvas(float x0, float y0, float x1, float y1, float density = Density)
            {
                D = density;
                Ox = x0;
                Oy = y0;
                W = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) * D));
                H = Mathf.Max(1, Mathf.CeilToInt((y1 - y0) * D));
                px = new Color[W * H];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
            }

            /// <summary>The middle of the canvas in its design coordinates.</summary>
            public Vector2 Center => new Vector2(Ox + W / D / 2, Oy + H / D / 2);

            /// <summary>Paints `coverage` (0-1 per texel) in a color; (x0, y0)-(x1, y1) limits the texels looked at.</summary>
            public void Fill(Func<float, float, float> coverage, long rgb, float alpha, float x0 = -1e6f, float y0 = -1e6f, float x1 = 1e6f, float y1 = 1e6f)
            {
                var c = Design.Hex(rgb);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((x0 - Ox) * D) - 1), i1 = Mathf.Min(W - 1, Mathf.CeilToInt((x1 - Ox) * D) + 1);
                int j0 = Mathf.Max(0, Mathf.FloorToInt((y0 - Oy) * D) - 1), j1 = Mathf.Min(H - 1, Mathf.CeilToInt((y1 - Oy) * D) + 1);
                for (int j = j0; j <= j1; j++)
                {
                    float y = Oy + (j + 0.5f) / D;
                    for (int i = i0; i <= i1; i++)
                    {
                        float cov = coverage(Ox + (i + 0.5f) / D, y);
                        if (cov <= 0) continue;
                        float sa = alpha * cov;
                        int k = j * W + i;
                        var dst = px[k];
                        if (Additive)
                        {
                            dst.r = c.r;
                            dst.g = c.g;
                            dst.b = c.b;
                            dst.a = Mathf.Min(1, dst.a + sa);
                        }
                        else
                        {
                            float oa = sa + dst.a * (1 - sa);
                            if (oa > 0)
                            {
                                dst.r = (c.r * sa + dst.r * dst.a * (1 - sa)) / oa;
                                dst.g = (c.g * sa + dst.g * dst.a * (1 - sa)) / oa;
                                dst.b = (c.b * sa + dst.b * dst.a * (1 - sa)) / oa;
                            }
                            dst.a = oa;
                        }
                        px[k] = dst;
                    }
                }
            }

            public Sprite ToSprite(string name)
            {
                // Rows run bottom-up in a texture; the canvas is y down like the web.
                var flipped = new Color[W * H];
                for (int j = 0; j < H; j++) Array.Copy(px, j * W, flipped, (H - 1 - j) * W, W);
                // No mipmaps: effects are drawn at their own size or bigger, and a smaller copy got sampled now and then
                // (a crisp spatter flickering into a soft red blob).
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                tex.SetPixels(flipped);
                tex.Apply(false, true);
                var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), D * Design.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                s.name = name;
                return s;
            }
        }
    }
}
