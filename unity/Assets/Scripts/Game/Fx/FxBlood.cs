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

            // Liquid: a splat on the body where the blow landed, a fine spray of specks, a jet that arcs out of the wound (drops leaving one after another along
            // nearly the same path, slowing, so they draw a curve), and heavier globs. Landing drops splash on the way
            // they were going.
            WoundSplat(root, x, y, dir, color, dark, 0.7f + 0.35f * Mathf.Min(1.5f, amount));
            Burst(parent, x, y, Tex.Drop, Mathf.Min(40, Mathf.RoundToInt(10 + amount * 14)), new Emit
            {
                Speed = Val.Rand(260, 720 + 200 * Mathf.Min(1, amount)),
                Angle = dir > 0 ? Val.Rand(-55, 25) : Val.Rand(155, 235),
                GravityY = 1100,
                FaceVelocity = 90,
                Scale = Val.Life(0.42f, 0.16f),
                Alpha = Val.Life(1, 0),
                Lifespan = 300,
                Tints = new[] { color, dark },
            });
            if (amount >= 0.35f)
            {
                int jet = Mathf.Min(16, Mathf.RoundToInt(5 + amount * 6));
                var angle = Random.Range(18f, 42f) * Mathf.Deg2Rad;
                var speed = Random.Range(520f, 680f) + 120 * Mathf.Min(1, amount);
                for (int i = 0; i < jet; i++)
                {
                    var k = i / (float)jet;
                    var sp = speed * (1 - 0.45f * k);
                    var a = angle + Random.Range(-0.05f, 0.05f);
                    Drop(parent, root, x, y, dir * Mathf.Cos(a) * sp, -Mathf.Sin(a) * sp, Mathf.Lerp(1.15f, 0.55f, k) * Random.Range(0.9f, 1.1f),
                        k < 0.5f ? color : dark, floorY, groundOrder, i * 0.012f, 0.8f);
                }
            }
            int globs = Mathf.Min(16, Mathf.RoundToInt(3 + amount * 6));
            for (int i = 0; i < globs; i++)
            {
                // Most fly on with the blow; a few spatter back toward the attacker.
                var back = Random.value < 0.2f;
                var vx = dir * Random.Range(120, 420 + 260 * amount) * (back ? -0.45f : 1);
                var vy = -Random.Range(120, 520) * (0.65f + 0.35f * Mathf.Min(1, amount));
                Drop(parent, root, x + Random.Range(-10f, 10f), y + Random.Range(-24f, 24f), vx, vy,
                    Random.Range(0.7f, 1.4f) * (0.85f + 0.25f * Mathf.Min(1.5f, amount)), Random.value < 0.35f ? dark : color, floorY, groundOrder, 0, 0.7f);
            }
        }

        /// <summary>
        /// The splat where a blow lands on a body: a spatter flung the way the blow travels (dark rim, the blood's color
        /// inside) that bursts out, holds a moment, then runs down and fades.
        /// </summary>
        static void WoundSplat(Transform root, float x, float y, int dir, long color, long dark, float size)
        {
            var holder = Holder(root, x, y, "wound splat");
            holder.localScale = new Vector3(dir, 1, 1);
            var tilt = Random.Range(-25f, 15f);
            // The spatter's core sits left of its middle: shift it onto the hit.
            var rim = Img(holder, Get(Tex.Spatter), 18 * size, 0, dark, 1, OrderFx - 2, false);
            var inside = Img(holder, Get(Tex.Spatter), 18 * size, 0, color, 1, OrderFx - 1, false);
            AngleTo(rim, tilt);
            AngleTo(inside, tilt);
            Tw(520, Lin, t =>
            {
                // Slaps on in 60 ms, holds, then runs down and fades.
                var on = Mathf.Min(1, t / 0.12f);
                var s = size * Mathf.Lerp(0.4f, 1, 1 - (1 - on) * (1 - on));
                var run = Mathf.Max(0, (t - 0.45f) / 0.55f);
                var a = 1 - run;
                if (rim != null)
                {
                    ScaleTo(rim, s, s * (1 + 0.25f * run));
                    SetAlpha(rim, a);
                }
                if (inside != null)
                {
                    ScaleTo(inside, s * 0.82f, s * (0.82f + 0.2f * run));
                    SetAlpha(inside, a);
                }
                if (holder != null) holder.localPosition = P(x, y + 18 * run * run);
            }, () =>
            {
                if (holder != null) Object.Destroy(holder.gameObject);
            });
        }

        /// <summary>
        /// One drop flying from (x, y) at (vx, vy) px/s under gravity after `delay` s, turned along its flight (head
        /// first, tail behind, longer the faster), landing around `floorY`; it splashes there `splatChance` of the time,
        /// and a big one throws up a few specks.
        /// </summary>
        static void Drop(Transform parent, Transform root, float x, float y, float vx, float vy, float size, long tint, float floorY, int groundOrder,
            float delay, float splatChance)
        {
            var land = floorY + Random.Range(-16f, 36f);
            // Time to fall to `land`: y + vy t + g t^2 / 2 = land.
            var fall = Mathf.Max(0, land - y);
            var secs = (-vy + Mathf.Sqrt(vy * vy + 2 * BloodGravity * fall)) / BloodGravity;
            SpriteRenderer drop = null;
            bool started = false;
            Tw(secs * 1000, Lin, t =>
            {
                if (!started)
                {
                    started = true;
                    drop = Img(root, Get(Tex.Drop), x, y, tint, 1, OrderFx, false);
                }
                if (drop == null) return;
                var s = t * secs;
                drop.transform.localPosition = P(x + vx * s, y + vy * s + BloodGravity * s * s / 2);
                var fy = vy + BloodGravity * s;
                AngleTo(drop, Mathf.Atan2(fy, vx) * Mathf.Rad2Deg + 90);
                var speed = Mathf.Sqrt(vx * vx + fy * fy);
                ScaleTo(drop, size * 0.85f, size * (0.75f + speed / 1300));
            }, () =>
            {
                if (drop == null) return;
                var at = drop.transform.localPosition;
                Kill(drop);
                float lx = at.x * Design.PixelsPerUnit, ly = -at.y * Design.PixelsPerUnit;
                if (Random.value < splatChance) Splat(root, lx, ly, size * (0.55f + Mathf.Min(0.5f, Mathf.Abs(vx) / 1200)), tint, groundOrder, vx);
                if (size > 1.05f)
                    Burst(parent, lx, ly, Tex.Drop, 3, new Emit
                    {
                        Speed = Val.Rand(90, 200),
                        Angle = Val.Rand(-150, -30),
                        GravityY = 1500,
                        FaceVelocity = 90,
                        Scale = Val.Life(0.32f, 0.14f),
                        Lifespan = 220,
                        Tint = tint,
                    });
            }, delay * 1000);
        }

        /// <summary>
        /// A splat on the ground (flattened for the floor's angle) that spreads out from where the drop hit and dries
        /// darker over a few seconds. A drop that was moving splashes on the way it was going (`vx`'s sign); a slow one
        /// leaves a round blot.
        /// </summary>
        static void Splat(Transform root, float x, float y, float size, long color, int order, float vx = 0)
        {
            var holder = Holder(root, x, y, "splat");
            // Every drop splashes on (a slow one a little smaller), so no round blots.
            var moving = true;
            if (Mathf.Abs(vx) < 60) size *= 0.8f;
            holder.localScale = new Vector3(vx < 0 ? -1 : vx > 0 ? 1 : (Random.value < 0.5f ? -1 : 1), 0.38f, 1);
            // The splash texture's blob sits left of its middle: shift it so the blob is where the drop landed.
            var scale = moving ? size * 0.75f : size;
            var r = Img(holder, Get(moving ? Tex.Splash : Tex.Splat), moving ? 16 * scale : 0, 0, color, 0.95f, order, false);
            AngleTo(r, moving ? Random.Range(-12f, 12f) : Random.Range(0, 360));
            Tw(90, CubicOut, t =>
            {
                if (r != null) ScaleTo(r, scale * Mathf.LerpUnclamped(0.35f, 1, t), scale * Mathf.LerpUnclamped(0.6f, 1, t));
            });
            Tw(3000, Lin, t =>
            {
                if (r != null) SetAlpha(r, Mathf.Lerp(0.95f, 0.7f, t));
            }, null, 400);
            splats.RemoveAll(sp => sp == null);
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
