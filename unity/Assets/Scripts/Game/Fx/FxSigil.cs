using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>Sigil visuals: SIGIL_FX (itemUi.ts) and the weapon-in-hand sparkle of drawFigure (sigilMotes, handSparkle in figures.ts).</summary>
    public static partial class Fx
    {
        /// <summary>
        /// A Sigil tier's look: its glow color (SIGIL_COLORS), particle tints and pace (SIGIL_FX; rarer tiers sparkle
        /// faster) and the glow pulse (addSigilGlow). For item icons (animateSigilIcon on the web): stars twinkle inside
        /// 0.42 x the icon size every `TwinkleEvery` ms (life 650 ms, scale sin(t*pi)*0.55, alpha 1 to 0.6, tints white +
        /// `Tint`), Dusk and Eclipse also shed motes every 2 x `MoteEvery` ms (rising 12-26 px/s, scale 0.35 to 0, alpha
        /// 0.9 to 0, life 1100 ms), and the icon swells to 1.07 and back over `PulseMs`.
        /// </summary>
        public sealed class SigilLook
        {
            public long Glow;
            /// <summary>Particle tints, deep to pale.</summary>
            public long[] Tint;
            /// <summary>ms between motes and between twinkles.</summary>
            public float MoteEvery, TwinkleEvery;
            /// <summary>The glow breathes from strength 2 up to this and back, `PulseMs` each way.</summary>
            public float GlowPeak, PulseMs;
        }

        /// <summary>SIGIL_FX by tier ("dawn", "dusk", "eclipse").</summary>
        public static readonly Dictionary<string, SigilLook> SigilFx = new Dictionary<string, SigilLook>
        {
            ["dawn"] = new SigilLook
            {
                Glow = 0xff7a2e, Tint = new long[] { 0xff7a2e, 0xffb070, 0xffe0b0 }, MoteEvery = 160, TwinkleEvery = 520, GlowPeak = 3.5f, PulseMs = 1100,
            },
            ["dusk"] = new SigilLook
            {
                Glow = 0xb070ff, Tint = new long[] { 0xb070ff, 0xd4b0ff, 0xffffff }, MoteEvery = 95, TwinkleEvery = 340, GlowPeak = 4.5f, PulseMs = 1100,
            },
            ["eclipse"] = new SigilLook
            {
                Glow = 0xff3358, Tint = new long[] { 0xff3358, 0xff8a9c, 0xffd0d8 }, MoteEvery = 55, TwinkleEvery = 220, GlowPeak = 6, PulseMs = 700,
            },
        };

        /// <summary>Sorting inside a Figure: the halo just behind the weapon (5), motes between the weapon and the body (10), the hand sparkle above.</summary>
        const int OrderSigilMotes = 7, OrderSigilSparkle = 15;

        /// <summary>
        /// A Sigil weapon in a figure's hands: the weapon glows in the tier's color and sheds motes drifting up and stars
        /// twinkling along the blade. A figure whose art shows its own weapon (no weapon art in hand) sparkles around its
        /// weapon hand instead. Eclipse sheds the most. Call it after Figure.Create, before any swing. Returns the effect's
        /// root under the figure (destroy it to take the effect off), or null when there is nothing to put it on.
        /// </summary>
        public static Transform SigilGlow(Figure f, string tier)
        {
            if (f == null || tier == null || !SigilFx.TryGetValue(tier, out var look)) return null;
            var art = f.ArtRoot;
            if (art == null) return null;
            var holder = Holder(art, 0, 0, "sigil fx");
            bool any = false;
            foreach (var pivot in f.Weapons)
            {
                if (pivot == null) continue;
                var w = pivot.GetComponentInChildren<SpriteRenderer>();
                if (w == null || w.sprite == null) continue;
                WeaponHalo(holder, w, look);
                BladeMotes(holder, pivot, w, tier, look);
                any = true;
            }
            if (!any) any = HandSparkle(holder, f, tier, look);
            if (any) return holder;
            Object.Destroy(holder.gameObject);
            return null;
        }

        /// <summary>
        /// The pulsing outer glow (addSigilGlow, a WebGL filter on the web): copies of the weapon painted in the glow color,
        /// added up in two rings around it, behind it.
        /// </summary>
        static void WeaponHalo(Transform holder, SpriteRenderer w, SigilLook look)
        {
            var copies = new List<SpriteRenderer>();
            var weights = new List<float>();
            float ws = Mathf.Max(0.0001f, Mathf.Abs(w.transform.localScale.x));
            var c = Design.Hex(look.Glow);
            float[] dists = { 3, 6 }, ringWeights = { 1, 0.5f };
            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + ring * 0.5f) / 8f * Mathf.PI * 2;
                    var go = new GameObject("sigil glow");
                    go.transform.SetParent(w.transform, false);
                    go.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (dists[ring] / Design.PixelsPerUnit / ws);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = w.sprite;
                    BindTexture(r);
                    r.flipX = w.flipX;
                    r.flipY = w.flipY;
                    Use(r, FillAdditiveMaterial);
                    r.sortingOrder = w.sortingOrder - 1;
                    r.color = new Color(c.r, c.g, c.b, 0);
                    copies.Add(r);
                    weights.Add(ringWeights[ring]);
                }
            }
            void Glow(float strength)
            {
                // Follows the weapon's own alpha, so a fading figure takes its halo with it.
                float own = w != null ? w.color.a : 0;
                for (int i = 0; i < copies.Count; i++) SetAlpha(copies[i], 0.05f * strength * weights[i] * own);
            }
            Glow(2);
            Anim.Handle h = null;
            h = Tw(look.PulseMs, SineInOut, t =>
            {
                if (holder == null || w == null)
                {
                    foreach (var r in copies) Kill(r);
                    h?.Stop();
                    return;
                }
                Glow(Mathf.LerpUnclamped(2, look.GlowPeak, t));
            }, null, 0, -1, true);
        }

        /// <summary>Motes drifting up off the blade and stars twinkling along it (sigilMotes).</summary>
        static void BladeMotes(Transform holder, Transform pivot, SpriteRenderer w, string tier, SigilLook look)
        {
            // From the grip toward the tip along the weapon's angle (0 = up), in the figure's own design px.
            var b = w.sprite.bounds;
            float reach = (w.transform.localPosition.y + (b.center.y + b.extents.y) * w.transform.localScale.y) * Design.PixelsPerUnit;
            float a = -pivot.localEulerAngles.z * Mathf.Deg2Rad;
            float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
            var root = Holder(holder, 0, 0, "sigil fx");
            root.localPosition = pivot.localPosition;
            var sys = new FxSystem(root) { Temporary = true };
            Vector2 AlongBlade()
            {
                float t = 0.2f + Random.value * 0.8f;
                return new Vector2(sin * reach * t, -cos * reach * t);
            }
            sys.AddFlow(new Emit
            {
                ZoneFn = AlongBlade,
                SpeedY = Val.Rand(-40, -15),
                SpeedX = Val.Rand(-10, 10),
                Scale = Val.Life(tier == "eclipse" ? 0.6f : 0.45f, 0),
                Alpha = Val.Life(1, 0),
                Lifespan = 1000,
                Frequency = look.MoteEvery,
                Tints = look.Tint,
                Add = true,
                Order = OrderSigilMotes,
            }, Get(Tex.Soft));
            sys.AddFlow(new Emit
            {
                ZoneFn = AlongBlade,
                ScaleOverLife = (t, _) => Mathf.Sin(t * Mathf.PI) * 0.7f,
                Rotate = Val.Rand(0, 90),
                Lifespan = 600,
                Frequency = look.TwinkleEvery,
                Tints = WithWhite(look.Tint),
                Add = true,
                Order = OrderSigilMotes,
            }, Get(Tex.Star));
        }

        /// <summary>Sigil sparkle around the weapon hand, for heroes whose art shows its own weapon (handSparkle).</summary>
        static bool HandSparkle(Transform holder, Figure f, string tier, SigilLook look)
        {
            var figLook = f.Look;
            if (figLook == null || figLook.Bare == null || Art.Pick(figLook.Art) == null) return false;
            var bare = figLook.Bare.Find(b => b != null && Art.Has(b.Art));
            if (bare == null || bare.Hands == null || bare.Hands.Count == 0) return false;
            // The bare art's size as drawn: the hand anchors are in its pixels.
            var size = Art.Size(Art.Get(bare.Art));
            if (size.x <= 0 || size.y <= 0) return false;
            float s = Mathf.Min(Figure.ArtHeight / size.y, Figure.ArtMaxWidth / size.x);
            var hand = bare.Hands[0];
            var root = Holder(holder, ((float)hand.X - size.x / 2) * s, ((float)hand.Y - size.y) * s, "sigil fx");

            var glow = Img(root, Get(Tex.Soft), 0, 0, look.Tint[0], 0.35f, OrderSigilSparkle, true);
            ScaleTo(glow, 2.6f, 2.6f);
            Anim.Handle h = null;
            h = Tw(tier == "eclipse" ? 600 : 1000, SineInOut, t =>
            {
                if (glow == null)
                {
                    h?.Stop();
                    return;
                }
                SetAlpha(glow, Mathf.LerpUnclamped(0.35f, 0.65f, t));
                var sc = Mathf.LerpUnclamped(2.6f, 3.2f, t);
                ScaleTo(glow, sc, sc);
            }, null, 0, -1, true);

            var sys = new FxSystem(root) { Temporary = true };
            sys.AddFlow(new Emit
            {
                X = Val.Rand(-30, 30),
                Y = Val.Rand(-60, 20),
                SpeedY = Val.Rand(-40, -15),
                Scale = Val.Life(0.5f, 0),
                Alpha = Val.Life(1, 0),
                Lifespan = 900,
                Frequency = look.MoteEvery,
                Tints = look.Tint,
                Add = true,
                Order = OrderSigilSparkle,
            }, Get(Tex.Soft));
            sys.AddFlow(new Emit
            {
                X = Val.Rand(-40, 40),
                Y = Val.Rand(-80, 20),
                ScaleOverLife = (t, _) => Mathf.Sin(t * Mathf.PI) * 0.7f,
                Rotate = Val.Rand(0, 90),
                Lifespan = 600,
                Frequency = look.TwinkleEvery,
                Tints = WithWhite(look.Tint),
                Add = true,
                Order = OrderSigilSparkle,
            }, Get(Tex.Star));
            return true;
        }

        /// <summary>White plus the tier's tints (the twinkles' colors).</summary>
        static long[] WithWhite(long[] tints)
        {
            var all = new long[tints.Length + 1];
            all[0] = 0xffffff;
            tints.CopyTo(all, 1);
            return all;
        }
    }
}
