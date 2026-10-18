using System.Collections.Generic;
using Drakepg.Rules;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>
    /// What spills from a struck monster (its look's `Blood`): liquid drops that arc out and splat on the ground, bits of
    /// stone, bark or shell, or a spirit's essence leaking up; pools under the fallen. Flat two-tone shapes like the rest.
    /// </summary>
    public static partial class Fx
    {
        /// <summary>Blood when a look names none: a deep cartoon red.</summary>
        public const long BloodRed = 0xa3161f;
        /// <summary>Splats kept on the ground at once (the oldest go first).</summary>
        const int MaxSplats = 70;
        const float BloodGravity = 1700;
        static readonly List<Transform> splats = new List<Transform>();

        /// <summary>
        /// A hit's spill at (x, y). `dir` 1 = the blow travels right (most of it flies that way). `amount` about 0.15 (a
        /// scratch) to 1 (a heavy blow), up to 2 for a finishing blow. Liquid lands around `floorY` and leaves splats on
        /// the ground at sorting order `groundOrder` (under the fighters).
        /// </summary>
        public static void Bleed(Transform parent, float x, float y, int dir, Blood blood, float amount, float floorY, int groundOrder)
        {
            var root = Root(parent);
            if (root == null || amount <= 0) return;
            dir = dir < 0 ? -1 : 1;
            var kind = blood?.Kind ?? "liquid";
            var color = blood != null ? blood.Color : BloodRed;
            var dark = Shade(color, 0.62f);
            if (kind == "chips")
            {
                Burst(parent, x, y, Tex.Rock, Mathf.RoundToInt(5 + amount * 12), new Emit
                {
                    Speed = Val.Rand(220, 480 + 200 * amount),
                    Angle = dir > 0 ? Val.Rand(-75, 15) : Val.Rand(165, 255),
                    GravityY = 1500,
                    Rotate = Val.Rand(0, 360),
                    Scale = Val.Life(0.75f + 0.25f * amount, 0.5f),
                    Lifespan = 720,
                    Tints = new[] { color, dark },
                });
                Burst(parent, x, y, Tex.Soft, 4, new Emit
                {
                    Speed = Val.Rand(30, 120),
                    Scale = Val.Life(1.4f, 2.6f),
                    Alpha = Val.Life(0.45f, 0),
                    Lifespan = 420,
                    Tint = Shade(color, 1.15f),
                });
                return;
            }
            if (kind == "wisps")
            {
                // Plain (not added) so dark essences keep their color on bright ground.
                Burst(parent, x, y, Tex.Soft, Mathf.RoundToInt(6 + amount * 12), new Emit
                {
                    SpeedX = Val.Rand(40 * dir, 240 * dir),
                    SpeedY = Val.Rand(-240, -50),
                    Scale = Val.Life(0.9f + 0.3f * Mathf.Min(1, amount), 0),
                    Alpha = Val.Life(0.75f, 0),
                    Lifespan = 700,
                    Tints = new[] { color, dark },
                });
                Burst(parent, x, y, Tex.Star, Mathf.RoundToInt(2 + amount * 5), new Emit
                {
                    SpeedX = Val.Rand(20 * dir, 160 * dir),
                    SpeedY = Val.Rand(-200, -40),
                    Rotate = Val.Rand(0, 180),
                    Scale = Val.Life(0.7f, 0),
                    Lifespan = 600,
                    Tint = 0xffffff,
                    Add = true,
                });
                return;
            }

            // Liquid: a puff of spray, then drops that arc out and splat where they land.
            Burst(parent, x, y, Tex.Soft, Mathf.RoundToInt(3 + amount * 5), new Emit
            {
                Speed = Val.Rand(40, 180 + 120 * amount),
                Angle = dir > 0 ? Val.Rand(-50, 30) : Val.Rand(150, 230),
                Scale = Val.Life(0.9f, 1.8f + 0.6f * amount),
                Alpha = Val.Life(0.6f, 0),
                Lifespan = 260,
                Tint = color,
            });
            int count = Mathf.Min(54, Mathf.RoundToInt(8 + amount * 24));
            for (int i = 0; i < count; i++)
            {
                // Most fly on with the blow; a few spatter back toward the attacker.
                var back = Random.value < 0.22f;
                var vx = dir * Random.Range(110, 440 + 300 * amount) * (back ? -0.45f : 1);
                var vy = -Random.Range(80, 520) * (0.65f + 0.35f * Mathf.Min(1, amount));
                var size = Random.Range(0.7f, 1.5f) * (0.85f + 0.3f * Mathf.Min(1.5f, amount));
                var x0 = x + Random.Range(-10f, 10f);
                var y0 = y + Random.Range(-26f, 26f);
                var land = floorY + Random.Range(-16f, 36f);
                // Time to fall to `land`: y0 + vy t + g t^2 / 2 = land.
                var fall = Mathf.Max(0, land - y0);
                var secs = (-vy + Mathf.Sqrt(vy * vy + 2 * BloodGravity * fall)) / BloodGravity;
                var tint = Random.value < 0.35f ? dark : color;
                var drop = Img(root, Get(Tex.Drop), x0, y0, tint, 1, OrderFx, false);
                ScaleTo(drop, size * 0.8f, size);
                var splatChance = 0.65f;
                Tw(secs * 1000, Lin, t =>
                {
                    if (drop == null) return;
                    var s = t * secs;
                    var px = x0 + vx * s;
                    var py = y0 + vy * s + BloodGravity * s * s / 2;
                    drop.transform.localPosition = P(px, py);
                    // Long along its flight, faster = longer.
                    var fy = vy + BloodGravity * s;
                    AngleTo(drop, Mathf.Atan2(fy, vx) * Mathf.Rad2Deg + 90);
                    var speed = Mathf.Sqrt(vx * vx + fy * fy);
                    ScaleTo(drop, size * 0.8f, size * (1 + speed / 1100));
                }, () =>
                {
                    if (drop == null) return;
                    var at = drop.transform.localPosition;
                    Kill(drop);
                    if (Random.value < splatChance) Splat(root, at.x * Design.PixelsPerUnit, -at.y * Design.PixelsPerUnit, size * 0.85f, tint, groundOrder);
                });
            }
        }

        /// <summary>A splat on the ground (flattened for the floor's angle) that pops in and dries darker over a few seconds.</summary>
        static void Splat(Transform root, float x, float y, float size, long color, int order)
        {
            var holder = Holder(root, x, y, "splat");
            holder.localScale = new Vector3(1, 0.36f, 1);
            var r = Img(holder, Get(Tex.Splat), 0, 0, color, 0.95f, order, false);
            AngleTo(r, Random.Range(0, 360));
            Tw(110, BackOut, t =>
            {
                if (r != null) ScaleTo(r, size * Mathf.LerpUnclamped(0.3f, 1, t), size * Mathf.LerpUnclamped(0.3f, 1, t));
            });
            Tw(3000, Lin, t =>
            {
                if (r != null) SetAlpha(r, Mathf.Lerp(0.95f, 0.7f, t));
            }, null, 400);
            splats.RemoveAll(s => s == null);
            splats.Add(holder);
            while (splats.Count > MaxSplats)
            {
                if (splats[0] != null) Object.Destroy(splats[0].gameObject);
                splats.RemoveAt(0);
            }
        }

        /// <summary>
        /// A pool spreading under a fallen body, centered at (x, y) on the ground, `width` design px across: a dark rim
        /// and the blood's color inside. Liquid blood only.
        /// </summary>
        public static void Pool(Transform parent, float x, float y, float width, Blood blood, int groundOrder, float ms = 1100)
        {
            var root = Root(parent);
            if (root == null || (blood?.Kind ?? "liquid") != "liquid") return;
            var color = blood != null ? blood.Color : BloodRed;
            var holder = Holder(root, x, y, "pool");
            holder.localScale = new Vector3(1, 0.3f, 1);
            // The splat texture is 48 px across.
            var full = width / 48f;
            var rim = Img(holder, Get(Tex.Splat), 0, 0, Shade(color, 0.62f), 0.95f, groundOrder, false);
            var inner = Img(holder, Get(Tex.Splat), 0, 0, color, 0.95f, groundOrder + 1, false);
            var spin = Random.Range(0, 360);
            AngleTo(rim, spin);
            AngleTo(inner, spin + 25);
            Tw(ms, CubicOut, t =>
            {
                if (rim != null) ScaleTo(rim, full * t, full * t);
                if (inner != null) ScaleTo(inner, full * 0.78f * t, full * 0.78f * t);
            });
        }

        static long Shade(long rgb, float k)
        {
            int r = (int)((rgb >> 16) & 0xff), g = (int)((rgb >> 8) & 0xff), b = (int)(rgb & 0xff);
            r = Mathf.Clamp(Mathf.RoundToInt(r * k), 0, 255);
            g = Mathf.Clamp(Mathf.RoundToInt(g * k), 0, 255);
            b = Mathf.Clamp(Mathf.RoundToInt(b * k), 0, 255);
            return ((long)r << 16) | ((long)g << 8) | (long)b;
        }

        // --- Time ---

        /// <summary>
        /// Slow motion: game time runs at `scale` for `ms` of real time (a hit-stop inside it still freezes, then the
        /// slow motion carries on). Finishing blows.
        /// </summary>
        public static void SlowMo(float scale, float ms)
        {
            FxRunner.SlowFor(scale, ms / 1000f);
            if (Anim.TimeScale > scale) Anim.TimeScale = scale;
        }

        /// <summary>A glint running up a blade (or a flare at a hand): a star that pops and spins out. Charge-ups.</summary>
        public static void Glint(Transform parent, float x, float y, long color = 0xffffff, float size = 1.6f)
        {
            var root = Root(parent);
            if (root == null) return;
            var star = Img(root, Get(Tex.Star), x, y, color, 1, Depth(905), true);
            var glow = Img(root, Get(Tex.Soft), x, y, color, 0.8f, Depth(904), true);
            Tw(320, Lin, t =>
            {
                var pop = Mathf.Sin(t * Mathf.PI);
                if (star != null)
                {
                    ScaleTo(star, size * pop, size * pop);
                    AngleTo(star, t * 140);
                }
                if (glow != null)
                {
                    ScaleTo(glow, size * 1.4f * pop, size * 1.4f * pop);
                    SetAlpha(glow, 0.8f * pop);
                }
            }, () =>
            {
                Kill(star);
                Kill(glow);
            });
        }
    }
}
