using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>
    /// Juice: particles, slashes, projectiles, flashes, hit-stop (src/ui/fx.ts). Every effect is drawn from textures made
    /// in code (FxArt.cs), so nothing needs art.
    ///
    /// Conventions: `parent` is the screen's World (GameRoot.I.Screen.World; null picks it), positions are design px (x
    /// right, y down), colors are 0xRRGGBB, times are ms like the web. Effects draw above characters (sorting order
    /// 20000 + the web's depth, so 900 is 20900) unless the web put them behind. They are owned by the screen: its tweens
    /// stop and its objects go when it changes.
    /// </summary>
    public static partial class Fx
    {
        /// <summary>Sorting order of most effects (the web's depth 900).</summary>
        public const int OrderFx = 20900;
        /// <summary>Room ambience behind the characters (dust, mist) and in front of everything else (rain, leaves...).</summary>
        public const int OrderAmbientBack = 1, OrderAmbientFront = 21500;
        /// <summary>The title's stars and embers: just above the backdrop.</summary>
        public const int OrderSky = -30000;

        static int Depth(int webDepth) => 20000 + webDepth;

        // --- Particles ---

        /// <summary>One-shot particle burst that cleans itself up.</summary>
        public static void Burst(Transform parent, float x, float y, Tex tex, int count, Emit cfg)
        {
            var sys = WorldSystem(parent);
            if (sys == null || cfg == null) return;
            var sprite = Get(tex);
            for (int i = 0; i < count; i++) sys.Spawn(cfg, sprite, x, y, i, null);
        }

        /// <summary>Burst with a web texture key ("fx_star").</summary>
        public static void Burst(Transform parent, float x, float y, string texture, int count, Emit cfg) =>
            Burst(parent, x, y, TexOf(texture), count, cfg);

        /// <summary>
        /// A simple star/spark burst: flying out all around at `speedMin`-`speedMax` px/s, shrinking from `scale` to 0 over
        /// `lifespan` seconds.
        /// </summary>
        public static void Burst(Transform parent, float x, float y, string texture, int count, float speedMin, float speedMax, float scale,
            float lifespan, Color tint, bool add = false) =>
            Burst(parent, x, y, TexOf(texture), count, new Emit
            {
                Speed = Val.Rand(speedMin, speedMax),
                Scale = Val.Life(scale, 0),
                Lifespan = lifespan * 1000,
                Tint = Hex(tint),
                Add = add,
            });

        /// <summary>
        /// A looping particle stream at (x, y) (scene.add.particles with a frequency). Its particles live in the stream's
        /// own space, so moving it (`SetPosition`) carries them along like a Phaser emitter. `prewarmMs` runs it ahead.
        /// </summary>
        public static Flow Stream(Transform parent, float x, float y, Tex tex, Emit cfg, float prewarmMs = 0)
        {
            if (parent == null) parent = DefaultParent();
            if (parent == null || cfg == null) return null;
            var go = new GameObject("fx stream");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Design.World(x, y), Quaternion.identity);
            var sys = new FxSystem(go.transform) { Temporary = true };
            var f = sys.AddFlow(cfg, Get(tex));
            if (prewarmMs > 0) sys.Prewarm(f, prewarmMs);
            return f;
        }

        /// <summary>A single effect image (an fx_soft glow, a star): center at (x, y), destroyed with the screen.</summary>
        public static SpriteRenderer Image(Transform parent, Tex tex, float x, float y, long color = 0xffffff, float alpha = 1, float scale = 1,
            bool add = false, int order = OrderFx)
        {
            var root = Root(parent);
            if (root == null) return null;
            var r = Img(root, Get(tex), x, y, color, alpha, order, add);
            ScaleTo(r, scale, scale);
            return r;
        }

        // --- Hits ---

        /// <summary>Expanding ring.</summary>
        public static void Shockwave(Transform parent, float x, float y, long color, float size = 1.6f, float squash = 0.45f)
        {
            var root = Root(parent);
            if (root == null) return;
            var ring = Img(root, Get(Tex.Ring), x, y, color, 1, Depth(899), false);
            ScaleTo(ring, 0.2f, 0.2f * squash);
            Tw(320, CubicOut, t =>
            {
                if (ring == null) return;
                var s = Mathf.LerpUnclamped(0.2f, size, t);
                ScaleTo(ring, s, s * squash);
                SetAlpha(ring, 1 - t);
            }, () => Kill(ring));
        }

        /// <summary>Sparks where a hit lands.</summary>
        public static void HitSparks(Transform parent, float x, float y, bool magic, bool crit, long[] palette = null)
        {
            var colors = palette ?? (magic ? new long[] { 0xffffff, 0x9fd3ff, 0x6c63ff } : new long[] { 0xffffff, 0xfff1a8, 0xffb347 });
            long mid = colors.Length > 1 ? colors[1] : colors[0];
            Burst(parent, x, y, Tex.Spark, crit ? 26 : 14, new Emit
            {
                Speed = Val.Rand(180, crit ? 560 : 420),
                Angle = Val.Rand(0, 360),
                Rotate = Val.Rand(0, 360),
                Scale = Val.Life(crit ? 1.2f : 0.9f, 0),
                Lifespan = 420,
                Colors = colors,
                Add = true,
            });
            Impact(parent, x, y, palette != null ? mid : magic ? 0x9fd3ff : 0xffe7a0, crit ? 1.5f : 1.1f);
        }

        /// <summary>A dagger's stab: thin streaks shooting through the target the way the blade went, and a small impact.</summary>
        public static void Stab(Transform parent, float x, float y, int dir, long color = 0xffffff)
        {
            dir = dir < 0 ? -1 : 1;
            Burst(parent, x - 30 * dir, y, Tex.Streak, 4, new Emit
            {
                Zone = new Rect(-10, -14, 20, 28),
                SpeedX = Val.Rand(900 * dir, 1300 * dir),
                FaceVelocity = 0,
                ScaleX = Val.Life(1.3f, 0.4f),
                ScaleY = 1.2f,
                Alpha = Val.Life(1, 0),
                Lifespan = 140,
                Tint = color,
            });
            Impact(parent, x + 10 * dir, y, color, 0.8f);
        }

        /// <summary>
        /// A flat comic impact where a blow lands: a jagged burst in `color` with a white one inside, popping out in a
        /// blink and gone (no glow, no rings).
        /// </summary>
        public static void Impact(Transform parent, float x, float y, long color, float size = 1.1f)
        {
            var root = Root(parent);
            if (root == null) return;
            var spin = Random.Range(0f, 360f);
            var outer = Img(root, Get(Tex.Impact), x, y, color, 1, Depth(903), false);
            var inner = Img(root, Get(Tex.Impact), x, y, 0xffffff, 1, Depth(904), false);
            AngleTo(outer, spin);
            AngleTo(inner, spin + 20);
            Tw(170, Lin, t =>
            {
                // Out fast (most of it in the first third), then thinning away.
                var grow = Mathf.Min(1, t * 3);
                var s = size * Mathf.Lerp(0.35f, 1.15f, 1 - (1 - grow) * (1 - grow));
                if (outer != null)
                {
                    ScaleTo(outer, s, s * 0.9f);
                    SetAlpha(outer, t < 0.4f ? 1 : 1 - (t - 0.4f) / 0.6f);
                }
                if (inner != null)
                {
                    ScaleTo(inner, s * 0.55f, s * 0.5f);
                    SetAlpha(inner, t < 0.3f ? 1 : Mathf.Max(0, 1 - (t - 0.3f) / 0.4f));
                }
            }, () =>
            {
                Kill(outer);
                Kill(inner);
            });
        }

        /// <summary>A crescent sweep across a target. `dir` 1 = swinging from the left.</summary>
        public static void Slash(Transform parent, float x, float y, int dir, long color = 0xffffff, float tilt = 0, bool big = false)
        {
            var root = Root(parent);
            if (root == null) return;
            dir = dir < 0 ? -1 : 1;
            float r = big ? 120 : 90;
            var holder = Holder(root, x, y, "slash");
            var g = new Group();
            g.Add(ShapeChild(holder, ArcShape(r, big ? 22 : 14, 1.1f), color, 0.9f, Depth(901), true));
            g.Add(ShapeChild(holder, ArcShape(r - 4, big ? 8 : 5, 0.9f), 0xffffff, 1, Depth(901), true));
            float rot0 = -0.5f * dir + tilt, rot1 = 0.5f * dir + tilt;
            void Pose(float s, float rot)
            {
                holder.localScale = new Vector3(s * dir, s, 1);
                holder.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg);
            }
            // Starts near full size: a hit-stop freezes tweens right after contact, and the frozen frame should show the cut.
            Pose(0.8f, rot0);
            Tw(240, CubicOut, t =>
            {
                if (holder == null) return;
                Pose(Mathf.LerpUnclamped(0.8f, 1.15f, t), Mathf.LerpUnclamped(rot0, rot1, t));
                g.Alpha(1 - t);
            }, () => Kill(holder));
        }

        /// <summary>Three parallel claw rakes (monster melee). `dir` 1 = raking from the left.</summary>
        public static void ClawMarks(Transform parent, float x, float y, int dir, long color)
        {
            var root = Root(parent);
            if (root == null) return;
            dir = dir < 0 ? -1 : 1;
            for (int i = 0; i < 3; i++)
            {
                var holder = Holder(root, x + (i - 1) * 26 * dir, y + (i - 1) * 8, "claw");
                var g = new Group();
                g.Add(Line(holder, -50 * dir, -70, 50 * dir, 70, 12, color, 0.75f, Depth(901), true));
                g.Add(Line(holder, -44 * dir, -60, 44 * dir, 60, 4, 0xffffff, 1, Depth(901), true));
                holder.localScale = new Vector3(0.75f, 0.75f, 1);
                Tw(230, CubicOut, t =>
                {
                    if (holder == null) return;
                    var s = Mathf.LerpUnclamped(0.75f, 1.05f, t);
                    holder.localScale = new Vector3(s, s, 1);
                    g.Alpha(1 - t);
                }, () => Kill(holder), i * 30);
            }
        }

        /// <summary>Critical hit: a bright star flash with rays bursting out around it.</summary>
        public static void CritFlare(Transform parent, float x, float y, long color)
        {
            var root = Root(parent);
            if (root == null) return;
            var flash = Img(root, Get(Tex.Star), x, y, 0xffffff, 1, Depth(903), true);
            float a0 = Between(0, 45);
            ScaleTo(flash, 3, 3);
            AngleTo(flash, a0);
            Tw(260, CubicOut, t =>
            {
                if (flash == null) return;
                var s = Mathf.LerpUnclamped(3, 7, t);
                ScaleTo(flash, s, s);
                AngleTo(flash, a0 + 30 * t);
                SetAlpha(flash, 1 - t);
            }, () => Kill(flash));
            Burst(parent, x, y, Tex.Streak, 14, new Emit
            {
                Speed = Val.Rand(700, 1100),
                Angle = Val.Rand(0, 360),
                FaceVelocity = 0,
                ScaleX = Val.Life(1.4f, 0.4f),
                ScaleY = 1.4f,
                Alpha = Val.Life(1, 0),
                Lifespan = 220,
                Colors = new[] { 0xffffff, color },
                Add = true,
            });
        }

        /// <summary>Sparks glancing off a raised guard (a Defend that soaks a hit). `dir` 1 = the blow came from the left.</summary>
        public static void Deflect(Transform parent, float x, float y, int dir, long color)
        {
            Burst(parent, x, y, Tex.Spark, 16, new Emit
            {
                Speed = Val.Rand(260, 560),
                Angle = dir >= 0 ? Val.Rand(150, 260) : Val.Rand(-80, 30),
                FaceVelocity = 90,
                Scale = Val.Life(0.9f, 0),
                GravityY = 500,
                Lifespan = 380,
                Colors = new[] { 0xffffff, color },
                Add = true,
            });
        }

        /// <summary>
        /// Hit-stop: freeze tweens (and particles) for a moment so impacts land with weight. Restored on real time, since
        /// game time is what gets frozen.
        /// </summary>
        public static void HitStop(float ms)
        {
            Anim.TimeScale = 0.02f;
            FxRunner.EndHitStopIn(ms / 1000f);
        }

        // --- Spells ---

        /// <summary>
        /// A spell flying from `from` to `to`; completes when it lands. Its shape follows the element: a glowing orb (fire,
        /// water, light, dark, neutral), an ice shard, a tumbling rock, a spinning gust, or an instant lightning bolt.
        /// </summary>
        public static Task Projectile(Transform parent, Vector2 from, Vector2 to, long[] colors, float duration = 420, string element = null)
        {
            var root = Root(parent);
            if (root == null || colors == null || colors.Length == 0) return Task.CompletedTask;
            long c1 = colors.Length > 1 ? colors[1] : colors[0];
            long c2 = colors.Length > 2 ? colors[2] : c1;
            if (element == "energy")
            {
                // Lightning doesn't fly: it cracks across in a blink.
                Lightning(parent, from, to, c1, 1);
                Anim.After(0.07f, () => Lightning(parent, from, to, c2, 0.6f));
                return Anim.After(Mathf.Min(150, duration / 2) / 1000f, () => Explosion(parent, to.x, to.y, colors)).Task;
            }
            string kind = element == "ice" ? "shard" : element == "earth" ? "rock" : element == "wind" ? "gust" : "orb";
            var tex = kind == "shard" ? Tex.Spark : kind == "rock" ? Tex.Rock : kind == "gust" ? Tex.Ring : Tex.Soft;
            bool rock = kind == "rock";
            var core = Img(root, Get(tex), from.x, from.y, c1, 1, Depth(902), !rock);
            float s0 = rock ? 2.4f : kind == "shard" ? 1.8f : kind == "gust" ? 1.2f : 2.2f;
            ScaleTo(core, s0, s0);
            var at = from;
            var trail = Stream(parent, 0, 0, Tex.Soft, new Emit
            {
                Lifespan = rock ? 260 : 320,
                Speed = Val.Rand(5, 40),
                Scale = Val.Life(kind == "shard" ? 0.9f : 1.6f, 0),
                Alpha = Val.Life(rock ? 0.5f : 0.9f, 0),
                Colors = rock ? new long[] { 0xd9c8a9, 0x8a6a2c } : colors,
                Add = !rock,
                Frequency = 12,
                Order = Depth(901),
            });
            if (trail != null) trail.Pos = () => at;
            // Rocks are lobbed high, shards and gusts fly nearly straight.
            float lift = rock ? 160 : kind == "orb" ? 70 : 20;
            return Tw(duration, rock ? Lin : SineIn, t =>
            {
                if (core == null) return;
                var prev = at;
                at = new Vector2(Mathf.LerpUnclamped(from.x, to.x, t), Mathf.LerpUnclamped(from.y, to.y, t) - Mathf.Sin(t * Mathf.PI) * lift);
                core.transform.localPosition = P(at.x, at.y);
                if (kind == "shard")
                {
                    if ((at - prev).sqrMagnitude > 0.0001f) AngleTo(core, Mathf.Atan2(at.y - prev.y, at.x - prev.x) * Mathf.Rad2Deg + 90);
                }
                else if (rock) AngleTo(core, t * 540);
                else if (kind == "gust")
                {
                    AngleTo(core, t * 1080);
                    ScaleTo(core, 1.2f + t * 0.6f, (1.2f + t * 0.6f) * 0.45f);
                }
                else
                {
                    var s = 2.2f + Mathf.Sin(t * 40) * 0.25f;
                    ScaleTo(core, s, s);
                }
            }, () =>
            {
                Kill(core);
                trail?.Stop();
                Explosion(parent, to.x, to.y, colors);
            }).Task;
        }

        /// <summary>A jagged lightning bolt between two points that flickers out. `width` 1 = a full bolt.</summary>
        public static void Lightning(Transform parent, Vector2 from, Vector2 to, long color, float width = 1)
        {
            var root = Root(parent);
            if (root == null) return;
            float dx = to.x - from.x, dy = to.y - from.y;
            float len = Mathf.Max(0.001f, Mathf.Sqrt(dx * dx + dy * dy));
            float nx = -dy / len, ny = dx / len;
            int steps = Mathf.Max(5, RoundJs(len / 40));
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float off = i == 0 || i == steps ? 0 : Between(-26, 26);
                pts[i] = new Vector2(from.x + dx * t + nx * off, from.y + dy * t + ny * off);
            }
            var holder = Holder(root, 0, 0, "lightning");
            var g = new Group();
            Polyline(holder, pts, 14 * width, color, 0.35f, g, false);
            Polyline(holder, pts, 6 * width, color, 0.9f, g, true);
            Polyline(holder, pts, 2.5f * width, 0xffffff, 1, g, true);
            // Flickers out in three steps (Phaser's Stepped ease).
            Tw(200, Lin, t =>
            {
                if (holder == null) return;
                g.Alpha(1 - Stepped(t, 3));
            }, () => Kill(holder));
        }

        public static void Explosion(Transform parent, float x, float y, long[] colors)
        {
            if (colors == null || colors.Length == 0) return;
            Burst(parent, x, y, Tex.Soft, 24, new Emit
            {
                Speed = Val.Rand(60, 320),
                Scale = Val.Life(2.2f, 0),
                Alpha = Val.Life(1, 0),
                Lifespan = 520,
                Colors = colors,
                Add = true,
            });
            Burst(parent, x, y, Tex.Spark, 12, new Emit
            {
                Speed = Val.Rand(200, 500),
                Rotate = Val.Rand(0, 360),
                Scale = Val.Life(0.9f, 0),
                Lifespan = 380,
                Tint = colors[0],
                Add = true,
            });
            Shockwave(parent, x, y, colors[0], 2.4f, 1);
        }

        /// <summary>Cast-up glow at a caster's hands; completes when it peaks.</summary>
        public static Task ChargeUp(Transform parent, float x, float y, long color, float duration = 300)
        {
            var root = Root(parent);
            if (root == null) return Task.CompletedTask;
            var glow = Img(root, Get(Tex.Soft), x, y, color, 0.4f, Depth(902), true);
            ScaleTo(glow, 0.3f, 0.3f);
            Burst(parent, x, y, Tex.Soft, 10, new Emit
            {
                EdgeRadius = 70,
                EdgeCount = 10,
                MoveTo = Vector2.zero,
                Lifespan = duration,
                Scale = Val.Life(0.9f, 0.2f),
                Tint = color,
                Add = true,
            });
            return Tw(duration, CubicIn, t =>
            {
                if (glow == null) return;
                var s = Mathf.LerpUnclamped(0.3f, 3, t);
                ScaleTo(glow, s, s);
                SetAlpha(glow, Mathf.LerpUnclamped(0.4f, 1, t));
            }, () => Kill(glow)).Task;
        }

        /// <summary>
        /// Magic circle on the ground under a caster, spinning while the spell charges. Drawn just behind a figure whose
        /// feet are at `y` unless `order` says otherwise. Returns its transform.
        /// </summary>
        public static Transform CastCircle(Transform parent, float x, float y, long color, float ms = 400, int? order = null)
        {
            var root = Root(parent);
            if (root == null) return null;
            // The holder squashes it flat onto the ground; the circle spins inside.
            var holder = Holder(root, x, y, "cast circle");
            var spin = Holder(holder, 0, 0, "spin");
            var shape = CircleShape();
            var r = ShapeChild(spin, shape, color, 0, order ?? Design.Depth(y) - 1, true);
            holder.localScale = new Vector3(0.2f, 0.08f, 1);
            Tw(Mathf.Min(180, ms / 2), BackOut, t =>
            {
                if (holder == null) return;
                holder.localScale = new Vector3(Mathf.LerpUnclamped(0.2f, 1, t), Mathf.LerpUnclamped(0.08f, 0.32f, t), 1);
                SetAlpha(r, t);
            });
            Tw(ms + 250, Lin, t =>
            {
                if (spin == null) return;
                spin.localRotation = Quaternion.Euler(0, 0, -120 * t);
            });
            Tw(250, Lin, t =>
            {
                if (r == null) return;
                SetAlpha(r, 1 - t);
            }, () => Kill(holder), ms);
            return holder;
        }

        /// <summary>Element pulled into a target that absorbs it (the reverse of a burst).</summary>
        public static void AbsorbInto(Transform parent, float x, float y, long[] colors)
        {
            Burst(parent, x, y, Tex.Soft, 18, new Emit
            {
                EdgeRadius = 130,
                EdgeCount = 18,
                MoveTo = Vector2.zero,
                Scale = Val.Life(1.2f, 0.3f),
                Lifespan = 380,
                Colors = colors,
                Add = true,
            });
        }

        /// <summary>Motes streaming from one point to another (lifesteal: life pulled out of the target).</summary>
        public static void Drain(Transform parent, Vector2 from, Vector2 to, long color)
        {
            Burst(parent, from.x, from.y, Tex.Soft, 16, new Emit
            {
                Zone = new Rect(-40, -50, 80, 100),
                MoveTo = to - from,
                Delay = Val.Rand(0, 160),
                Scale = Val.Life(1.1f, 0.4f),
                Alpha = Val.Life(1, 0.6f),
                Lifespan = 320,
                Colors = new[] { color, 0xffffff },
                Add = true,
            });
        }

        /// <summary>
        /// What an element looks like when it lands, on top of the normal sparks. `power` 0-1 scales the size
        /// (a small Burn tick is about 0.3, a crit or weak hit 1).
        /// </summary>
        public static void ElementBurst(Transform parent, float x, float y, string element, float power = 0.6f)
        {
            if (element == null || Root(parent) == null) return;
            var c = Colors(element);
            if (c.Length < 4) return;
            int N(float k) => Mathf.Max(2, RoundJs(k * (0.5f + power)));
            switch (element)
            {
                case "fire":
                    // Flames licking upward, then a little smoke.
                    Burst(parent, x, y + 20, Tex.Soft, N(16), new Emit
                    {
                        Zone = new Rect(-50, -20, 100, 40),
                        SpeedY = Val.Rand(-320, -120),
                        SpeedX = Val.Rand(-50, 50),
                        Scale = Val.Life(1.6f, 0),
                        Alpha = Val.Life(0.9f, 0),
                        Lifespan = 520,
                        Colors = new[] { c[1], c[2], c[3] },
                    });
                    Burst(parent, x, y + 10, Tex.Soft, N(6), new Emit
                    {
                        SpeedY = Val.Rand(-200, -80),
                        SpeedX = Val.Rand(-30, 30),
                        Scale = Val.Life(1, 0),
                        Lifespan = 360,
                        Colors = new[] { c[0], c[1] },
                        Add = true,
                    });
                    Burst(parent, x, y - 40, Tex.Soft, N(5), new Emit
                    {
                        SpeedY = Val.Rand(-90, -40),
                        SpeedX = Val.Rand(-30, 30),
                        Scale = Val.Life(1, 2.4f),
                        Alpha = Val.Life(0.35f, 0),
                        Lifespan = 800,
                        Tint = 0x3a2a24,
                    });
                    break;
                case "ice":
                {
                    // Shards burst out and a frost star flashes.
                    Burst(parent, x, y, Tex.Spark, N(14), new Emit
                    {
                        Speed = Val.Rand(220, 520),
                        Angle = Val.Rand(0, 360),
                        FaceVelocity = 90,
                        ScaleX = Val.Life(0.9f, 0.2f),
                        ScaleY = Val.Life(1.6f, 0.4f),
                        GravityY = 600,
                        Lifespan = 520,
                        Colors = new[] { 0xffffff, c[1], c[2] },
                    });
                    var star = Img(Root(parent), Get(Tex.Star), x, y, c[1], 1, Depth(902), true);
                    float peak = 4 + power * 3;
                    Tw(380, CubicOut, t =>
                    {
                        if (star == null) return;
                        var s = Mathf.LerpUnclamped(1, peak, t);
                        ScaleTo(star, s, s);
                        AngleTo(star, 45 * t);
                        SetAlpha(star, 1 - t);
                    }, () => Kill(star));
                    Burst(parent, x, y, Tex.Soft, N(8), new Emit
                    {
                        Zone = new Rect(-70, -70, 140, 140),
                        SpeedY = Val.Rand(10, 60),
                        Scale = Val.Life(0.5f, 0),
                        Lifespan = 900,
                        Tint = 0xffffff,
                        Add = true,
                    });
                    break;
                }
                case "water":
                    // A splash of droplets that fall back down, and a ripple on the ground.
                    Burst(parent, x, y, Tex.Spark, N(18), new Emit
                    {
                        Speed = Val.Rand(200, 460),
                        Angle = Val.Rand(200, 340),
                        FaceVelocity = 90,
                        ScaleX = 0.7f,
                        ScaleY = Val.Life(1, 0.5f),
                        GravityY = 1300,
                        Lifespan = 650,
                        Colors = new[] { c[1], c[2], c[2] },
                        Alpha = Val.Life(1, 0.3f),
                    });
                    Burst(parent, x, y, Tex.Ring, N(5), new Emit
                    {
                        Speed = Val.Rand(40, 160),
                        Scale = Val.Life(0.15f, 0.45f),
                        Alpha = Val.Life(0.9f, 0),
                        Lifespan = 500,
                        Tint = c[0],
                    });
                    Shockwave(parent, x, y + 120, c[1], 2.2f + power, 0.3f);
                    break;
                case "wind":
                {
                    // Leaves and air whipping around the target.
                    Burst(parent, x, y, Tex.Leaf, N(10), new Emit
                    {
                        EdgeRadius = 60,
                        EdgeCount = N(10),
                        SpeedX = Val.Rand(-260, 260),
                        SpeedY = Val.Rand(-260, 60),
                        Rotate = Val.Life(0, 720),
                        Scale = Val.Life(1.1f, 0.3f),
                        Lifespan = 600,
                        Colors = new[] { c[1], c[2], 0xffffff },
                    });
                    var root = Root(parent);
                    for (int i = 0; i < 2; i++)
                    {
                        var ring = Img(root, Get(Tex.Ring), x, y - 10 + i * 30, c[1], 0.9f, Depth(901), true);
                        ScaleTo(ring, 0.6f, 0.25f);
                        float sx = 3 + power * 1.5f, sy = 1 + power * 0.5f, turn = i == 1 ? -40 : 40;
                        Tw(360, CubicOut, t =>
                        {
                            if (ring == null) return;
                            ScaleTo(ring, Mathf.LerpUnclamped(0.6f, sx, t), Mathf.LerpUnclamped(0.25f, sy, t));
                            AngleTo(ring, turn * t);
                            SetAlpha(ring, 0.9f * (1 - t));
                        }, () => Kill(ring), i * 60);
                    }
                    break;
                }
                case "earth":
                    // Rock chunks thrown up, a dust cloud and a ground thump.
                    Burst(parent, x, y + 40, Tex.Rock, N(10), new Emit
                    {
                        Speed = Val.Rand(220, 480),
                        Angle = Val.Rand(215, 325),
                        Rotate = Val.Life(0, 540),
                        Scale = Val.Rand(0.8f, 1.6f),
                        GravityY = 1500,
                        Lifespan = 700,
                        Colors = new[] { c[0], c[1], c[2] },
                    });
                    Burst(parent, x, y + 110, Tex.Soft, N(10), new Emit
                    {
                        Zone = new Rect(-80, -10, 160, 20),
                        SpeedX = Val.Rand(-120, 120),
                        SpeedY = Val.Rand(-60, -10),
                        Scale = Val.Life(1.6f, 3),
                        Alpha = Val.Life(0.45f, 0),
                        Lifespan = 700,
                        Tint = c[1],
                    });
                    Shockwave(parent, x, y + 120, c[1], 2.6f + power, 0.3f);
                    break;
                case "energy":
                {
                    // Little arcs crackling over the target.
                    int arcs = RoundJs(2 + power * 2);
                    long arcColor = c[2];
                    for (int i = 0; i < arcs; i++)
                    {
                        Anim.After(i * 0.045f, () =>
                        {
                            float a = Random.Range(0, Mathf.PI * 2);
                            float r = Between(50, 90);
                            Lightning(parent, new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r),
                                new Vector2(x + Between(-15, 15), y + Between(-15, 15)), arcColor, 0.45f);
                        });
                    }
                    Burst(parent, x, y, Tex.Spark, N(12), new Emit
                    {
                        Speed = Val.Rand(300, 700),
                        Angle = Val.Rand(0, 360),
                        FaceVelocity = 90,
                        Scale = Val.Life(0.8f, 0),
                        Lifespan = 240,
                        Colors = new[] { 0xffffff, c[1], c[3] },
                        Add = true,
                    });
                    break;
                }
                case "light":
                {
                    // A pillar of light falls on the target and stars ring out.
                    var pillar = Img(Root(parent), Get(Tex.Soft), x, y - 40, c[2], 0, Depth(898), true);
                    ScaleTo(pillar, 1.5f, 14);
                    float wide = 2.5f + power * 1.5f;
                    Tw(90, CubicOut, t =>
                    {
                        if (pillar == null) return;
                        SetAlpha(pillar, 0.9f * t);
                        ScaleTo(pillar, Mathf.LerpUnclamped(1.5f, wide, t), 14);
                    });
                    Tw(380, SineIn, t =>
                    {
                        if (pillar == null) return;
                        SetAlpha(pillar, 0.9f * (1 - t));
                        ScaleTo(pillar, Mathf.LerpUnclamped(wide, 0.5f, t), 14);
                    }, () => Kill(pillar), 90);
                    Burst(parent, x, y, Tex.Star, N(10), new Emit
                    {
                        Speed = Val.Rand(120, 320),
                        Angle = Val.Rand(0, 360),
                        Rotate = Val.Rand(0, 180),
                        Scale = Val.Life(1, 0),
                        Lifespan = 520,
                        Colors = new[] { 0xffffff, c[1], c[2] },
                        Add = true,
                    });
                    break;
                }
                case "dark":
                {
                    // Shadow wisps pulled into the target, then a dark pop.
                    Burst(parent, x, y, Tex.Soft, N(16), new Emit
                    {
                        EdgeRadius = 110,
                        EdgeCount = N(16),
                        MoveTo = Vector2.zero,
                        Scale = Val.Life(1.4f, 0.2f),
                        Alpha = Val.Life(0.2f, 1),
                        Lifespan = 260,
                        Colors = new[] { c[1], c[2], c[3] },
                    });
                    var ring = Img(Root(parent), Get(Tex.Ring), x, y, c[1], 0.2f, Depth(901), false);
                    ScaleTo(ring, 3.4f, 3.4f);
                    Tw(240, CubicIn, t =>
                    {
                        if (ring == null) return;
                        var s = Mathf.LerpUnclamped(3.4f, 0.3f, t);
                        ScaleTo(ring, s, s);
                        SetAlpha(ring, Mathf.LerpUnclamped(0.2f, 1, t));
                    }, () => Kill(ring));
                    var pop = new Emit
                    {
                        Speed = Val.Rand(80, 260),
                        Scale = Val.Life(1.6f, 0),
                        Alpha = Val.Life(0.9f, 0),
                        Lifespan = 420,
                        Colors = new[] { c[0], c[1], c[2] },
                        Add = true,
                    };
                    int popCount = N(10);
                    Anim.After(0.24f, () => Burst(parent, x, y, Tex.Soft, popCount, pop));
                    break;
                }
            }
        }

        // --- Healing, buffs, statuses ---

        /// <summary>Sparkles rising from a character's feet.</summary>
        public static void HealSparkles(Transform parent, float x, float y, long color = 0x7dff9a)
        {
            Burst(parent, x, y, Tex.Soft, 26, new Emit
            {
                Zone = new Rect(-60, -20, 120, 30),
                SpeedY = Val.Rand(-220, -80),
                SpeedX = Val.Rand(-20, 20),
                Scale = Val.Life(1.1f, 0),
                Alpha = Val.Life(1, 0),
                Lifespan = 900,
                Tint = color,
                Add = true,
            });
            Burst(parent, x, y - 80, Tex.Star, 8, new Emit
            {
                Zone = new Rect(-60, -80, 120, 140),
                Speed = Val.Rand(10, 40),
                Scale = Val.Life(0.9f, 0),
                Rotate = Val.Rand(0, 180),
                Lifespan = 800,
                Tint = 0xffffff,
                Add = true,
            });
            Shockwave(parent, x, y, color, 2, 0.35f);
        }

        /// <summary>Plus signs floating up (healing), in `color`.</summary>
        public static void HealPluses(Transform parent, float x, float feetY, long color)
        {
            Burst(parent, x, feetY - 60, Tex.Plus, 8, new Emit
            {
                Zone = new Rect(-70, -120, 140, 130),
                SpeedY = Val.Rand(-140, -60),
                Scale = Val.Life(0.9f, 0.2f),
                Alpha = Val.Life(1, 0),
                Lifespan = 800,
                Tints = new[] { color, 0xffffff },
            });
        }

        /// <summary>Golden pillar + star burst.</summary>
        public static void LevelUpBurst(Transform parent, float x, float y)
        {
            var root = Root(parent);
            if (root == null) return;
            var pillar = Img(root, Get(Tex.Soft), x, y - 180, 0xffd75e, 0, Depth(898), true);
            ScaleTo(pillar, 0.5f, 0.5f);
            Tw(260, CubicOut, t =>
            {
                if (pillar == null) return;
                ScaleTo(pillar, Mathf.LerpUnclamped(0.5f, 4, t), Mathf.LerpUnclamped(0.5f, 18, t));
                SetAlpha(pillar, 0.9f * t);
            });
            Tw(900, SineIn, t =>
            {
                if (pillar == null) return;
                ScaleTo(pillar, Mathf.LerpUnclamped(4, 2.5f, t), 18);
                SetAlpha(pillar, 0.9f * (1 - t));
            }, () => Kill(pillar), 260);
            Burst(parent, x, y - 120, Tex.Star, 30, new Emit
            {
                Speed = Val.Rand(120, 420),
                Angle = Val.Rand(200, 340),
                GravityY = 300,
                Rotate = Val.Rand(0, 360),
                Scale = Val.Life(1.2f, 0),
                Lifespan = 1200,
                Colors = new long[] { 0xffffff, 0xffe066, 0xffb300 },
                Add = true,
            });
            Shockwave(parent, x, y, 0xffd75e, 3, 0.35f);
        }

        /// <summary>Rising rings and a column of glow around a figure (buffs, war cries).</summary>
        public static void Aura(Transform parent, float x, float feetY, long color)
        {
            var root = Root(parent);
            if (root == null) return;
            for (int i = 0; i < 3; i++)
            {
                var ring = Img(root, Get(Tex.Ring), x, feetY - 10, color, 0, Depth(901), true);
                ScaleTo(ring, 2.6f, 0.8f);
                Tw(520, SineOut, t =>
                {
                    if (ring == null) return;
                    ring.transform.localPosition = P(x, Mathf.LerpUnclamped(feetY - 10, feetY - 200, t));
                    ScaleTo(ring, Mathf.LerpUnclamped(2.6f, 1.6f, t), Mathf.LerpUnclamped(0.8f, 0.5f, t));
                    SetAlpha(ring, 0.9f * (1 - t));
                }, () => Kill(ring), i * 90);
            }
            var column = Img(root, Get(Tex.Soft), x, feetY - 110, color, 0, Depth(898), true);
            ScaleTo(column, 4, 9);
            Tw(650, SineIn, t =>
            {
                if (column == null) return;
                SetAlpha(column, 0.5f * (1 - t));
                ScaleTo(column, Mathf.LerpUnclamped(4, 2, t), 9);
            }, () => Kill(column));
        }

        /// <summary>Chevrons over a head: rising for a buff, sinking for a debuff.</summary>
        public static void StatusMark(Transform parent, float x, float y, long color, bool up)
        {
            var root = Root(parent);
            if (root == null) return;
            for (int i = 0; i < 3; i++)
            {
                float cx = x + (i - 1) * 34, cy = y + (up ? 30 : -30), cy1 = cy + (up ? -60 : 60);
                var c = Img(root, Get(Tex.Chevron), cx, cy, color, 0, Depth(902), false);
                ScaleTo(c, 1.1f, up ? 1.1f : -1.1f);
                Tw(90, Lin, t =>
                {
                    if (c != null) SetAlpha(c, t);
                }, null, i * 70);
                Tw(520, SineIn, t =>
                {
                    if (c == null) return;
                    c.transform.localPosition = P(cx, Mathf.LerpUnclamped(cy, cy1, t));
                    SetAlpha(c, 1 - t);
                }, () => Kill(c), i * 70 + 90);
            }
        }

        /// <summary>Stars circling over a stunned head.</summary>
        public static void StunStars(Transform parent, float x, float y, float ms = 900)
        {
            var root = Root(parent);
            if (root == null) return;
            for (int i = 0; i < 3; i++)
            {
                var star = Img(root, Get(Tex.Star), x, y, 0xffe066, 1, Depth(902), true);
                float a0 = i / 3f * Mathf.PI * 2;
                void Orbit(float a)
                {
                    star.transform.localPosition = P(x + Mathf.Cos(a) * 50, y + Mathf.Sin(a) * 14);
                    var s = Mathf.Sin(a) > 0 ? 1.2f : 0.8f;
                    ScaleTo(star, s, s);
                }
                Orbit(a0);
                Tw(ms, Lin, t =>
                {
                    if (star != null) Orbit(a0 + t * Mathf.PI * 4);
                }, () => Kill(star));
                Tw(200, Lin, t =>
                {
                    if (star != null) SetAlpha(star, 1 - t);
                }, null, Mathf.Max(0, ms - 200));
            }
        }

        /// <summary>A status taking hold (or ticking): a cloud and bubbles in its color around the body.</summary>
        public static void StatusPuff(Transform parent, float x, float y, long color)
        {
            // Flat: a ring of diamonds in the status's color flung out from the body, and a few drifting up (no soft
            // blobs or rings, which read as circles).
            Burst(parent, x, y, Tex.Spark, 10, new Emit
            {
                EdgeRadius = 18,
                EdgeCount = 10,
                Speed = Val.Rand(140, 260),
                FaceVelocity = 90,
                Scale = Val.Life(1.1f, 0.3f),
                Alpha = Val.Life(1, 0),
                Lifespan = 340,
                Tint = color,
            });
            Burst(parent, x, y, Tex.Spark, 5, new Emit
            {
                Zone = new Rect(-50, -30, 100, 60),
                SpeedY = Val.Rand(-140, -60),
                SpeedX = Val.Rand(-15, 15),
                FaceVelocity = 90,
                Scale = Val.Life(0.7f, 0.2f),
                Alpha = Val.Life(1, 0),
                Lifespan = 520,
                Tint = color,
            });
        }

        // --- Bodies ---

        /// <summary>Puff of smoke + chunks in the monster's color.</summary>
        public static void DeathPoof(Transform parent, float x, float y, long color)
        {
            Burst(parent, x, y, Tex.Soft, 22, new Emit
            {
                Zone = new Rect(-70, -60, 140, 120),
                // The web's config (speed + speedY) made Phaser drop speedY and drift the smoke down-right; it rises here.
                Speed = Val.Rand(20, 90),
                SpeedY = Val.Rand(-120, -30),
                Scale = Val.Life(2.6f, 0.4f),
                Alpha = Val.Life(0.7f, 0),
                Lifespan = 900,
                Tints = new long[] { 0xdddddd, 0xbbbbbb, 0x999999 },
            });
            Burst(parent, x, y, Tex.Soft, 16, new Emit
            {
                Speed = Val.Rand(150, 380),
                Angle = Val.Rand(200, 340),
                GravityY = 900,
                Scale = Val.Life(0.8f, 0.3f),
                Lifespan = 800,
                Tint = color,
            });
        }

        /// <summary>A body breaking apart into motes that drift up (monster deaths). `w`, `h`: the body's size.</summary>
        public static void Dissolve(Transform parent, float x, float feetY, float w, float h, long[] colors)
        {
            Burst(parent, x, feetY, Tex.Soft, 34, new Emit
            {
                Zone = new Rect(-w / 2, -h, w, h),
                SpeedY = Val.Rand(-180, -40),
                SpeedX = Val.Rand(-40, 40),
                Scale = Val.Life(0.9f, 0),
                Alpha = Val.Life(1, 0),
                Lifespan = 900,
                Colors = colors,
                Add = true,
            });
            Burst(parent, x, feetY, Tex.Star, 8, new Emit
            {
                Zone = new Rect(-w / 2, -h, w, h),
                SpeedY = Val.Rand(-120, -30),
                Scale = Val.Life(0.7f, 0),
                Rotate = Val.Rand(0, 180),
                Lifespan = 700,
                Tint = 0xffffff,
                Add = true,
            });
        }

        /// <summary>Little dust puff at the feet (footsteps, landings). Drawn just behind a figure standing at `y` unless `order` says otherwise.</summary>
        public static void Dust(Transform parent, float x, float y, int amount = 3, int? order = null)
        {
            Burst(parent, x, y, Tex.Soft, amount, new Emit
            {
                SpeedX = Val.Rand(-50, 50),
                SpeedY = Val.Rand(-40, -10),
                Scale = Val.Life(0.9f, 0.2f),
                Alpha = Val.Life(0.45f, 0),
                Lifespan = 450,
                Tint = 0xd9c8a9,
                Order = order ?? Design.Depth(y) - 1,
            });
        }

        /// <summary>Wind lines streaking behind a fast-moving figure. `dir` 1 = moving right.</summary>
        public static void SpeedLines(Transform parent, float x, float y, int dir, long color = 0xffffff)
        {
            dir = dir < 0 ? -1 : 1;
            Burst(parent, x, y, Tex.Streak, 7, new Emit
            {
                Zone = new Rect(-30, -150, 60, 140),
                SpeedX = Val.Rand(-900 * dir, -500 * dir),
                ScaleX = Val.Life(1.1f, 0.3f),
                ScaleY = 0.8f,
                Alpha = Val.Life(0.55f, 0),
                Lifespan = 200,
                Tint = color,
                Add = true,
            });
        }

        /// <summary>
        /// Flash a figure one solid color for a moment: white by default, or `then` right after the white (an element's
        /// color, a heal's green). Overlapping flashes keep the look from before the first and only the last restores it.
        /// </summary>
        public static void WhiteFlash(Figure fig, float ms = 90, long color = 0xffffff, long? then = null)
        {
            if (fig == null) return;
            PurgeFlashes();
            if (!flashes.TryGetValue(fig, out var st))
            {
                st = new FlashState();
                foreach (var r in fig.Renderers)
                {
                    if (r == null || r.name == "shadow") continue;
                    st.Rs.Add(r);
                    st.Mats.Add(r.sharedMaterial);
                    st.Cols.Add(r.color);
                }
                flashes[fig] = st;
            }
            int token = ++st.Token;
            Paint(st, color);
            if (then.HasValue)
            {
                long next = then.Value;
                Anim.After(ms / 2000f, () =>
                {
                    if (st.Token == token) Paint(st, next);
                });
            }
            Anim.After(ms / 1000f, () =>
            {
                if (st.Token != token) return;
                flashes.Remove(fig);
                for (int i = 0; i < st.Rs.Count; i++)
                {
                    var r = st.Rs[i];
                    if (r == null) continue;
                    if (st.Mats[i] != null) r.sharedMaterial = st.Mats[i];
                    var c = st.Cols[i];
                    r.color = new Color(c.r, c.g, c.b, r.color.a);
                }
            });
        }

        /// <summary>
        /// A see-through silhouette of a figure that fades out and is destroyed (lunges, dodges). The caller draws the copy
        /// (Figure.Create) and places it; this paints and fades it. `Ghost` does both.
        /// </summary>
        public static void FadeGhost(Figure ghost, long color, float alpha = 0.45f, float ms = 260)
        {
            if (ghost == null) return;
            // Sigil sparkles and halos aren't part of the silhouette.
            foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
            {
                if (t == ghost.transform || (t.name != "sigil fx" && t.name != "sigil glow")) continue;
                t.gameObject.SetActive(false);
                Object.Destroy(t.gameObject);
            }
            var c = Design.Hex(color);
            var g = new Group();
            foreach (var r in ghost.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r.name == "shadow")
                {
                    r.enabled = false;
                    continue;
                }
                Use(r, FillMaterial);
                r.color = new Color(c.r, c.g, c.b, alpha * r.color.a);
                g.Add(r);
            }
            var go = ghost.gameObject;
            Tw(ms, QuadIn, t => g.Alpha(1 - t), () =>
            {
                if (go != null) Object.Destroy(go);
            });
        }

        /// <summary>A fading silhouette of `of` where it stands now, just behind it (BattleScene's ghost).</summary>
        public static Figure Ghost(Figure of, long color, float alpha = 0.4f, float ms = 240)
        {
            if (of == null) return null;
            var go = Object.Instantiate(of.gameObject, of.transform.parent);
            go.name = "ghost";
            go.transform.SetPositionAndRotation(of.transform.position, of.transform.rotation);
            go.transform.localScale = of.transform.localScale;
            var group = go.GetComponent<SortingGroup>();
            var original = of.GetComponent<SortingGroup>();
            if (group != null && original != null) group.sortingOrder = original.sortingOrder - 1;
            var ghost = go.GetComponent<Figure>();
            if (ghost == null)
            {
                Object.Destroy(go);
                return null;
            }
            FadeGhost(ghost, color, alpha, ms);
            return ghost;
        }

        // --- Rewards ---

        /// <summary>Coins pop out of `from` then fly to `to`. Completes when the last one lands; `onLand` runs per coin.</summary>
        public static Task CoinShower(Transform parent, Vector2 from, Vector2 to, int count, Action onLand = null)
        {
            var root = Root(parent);
            if (root == null) return Task.CompletedTask;
            int n = Mathf.Clamp(count, 1, 14);
            var flights = new Task[n];
            for (int i = 0; i < n; i++)
            {
                var coin = Img(root, Get(Tex.Coin), from.x, from.y, 0xffffff, 1, Depth(950), false);
                ScaleTo(coin, 0, 0);
                float popX = from.x + Between(-90, 90), popY = from.y + Between(-140, -40);
                Tw(260, BackOut, t =>
                {
                    if (coin == null) return;
                    coin.transform.localPosition = P(Mathf.LerpUnclamped(from.x, popX, t), Mathf.LerpUnclamped(from.y, popY, t));
                    var s = Mathf.LerpUnclamped(0, 1.3f, t);
                    ScaleTo(coin, s, s);
                });
                flights[i] = Tw(480, CubicIn, t =>
                {
                    if (coin == null) return;
                    coin.transform.localPosition = P(Mathf.LerpUnclamped(popX, to.x, t), Mathf.LerpUnclamped(popY, to.y, t));
                    var s = Mathf.LerpUnclamped(1.3f, 0.7f, t);
                    ScaleTo(coin, s, s);
                }, () =>
                {
                    Kill(coin);
                    Burst(parent, to.x, to.y, Tex.Star, 3, new Emit
                    {
                        Speed = Val.Rand(40, 120),
                        Scale = Val.Life(0.6f, 0),
                        Lifespan = 300,
                        Tint = 0xfff3a0,
                        Add = true,
                    });
                    onLand?.Invoke();
                }, 260 + 120 + i * 45).Task;
            }
            return Task.WhenAll(flights);
        }

        /// <summary>Confetti rain over the whole screen.</summary>
        public static void Confetti(Transform parent)
        {
            const float w = Design.Width;
            Burst(parent, w / 2, -20, Tex.Leaf, 60, new Emit
            {
                Zone = new Rect(-w / 2, 0, w, 10),
                SpeedY = Val.Rand(120, 320),
                SpeedX = Val.Rand(-60, 60),
                Rotate = Val.Life(0, 720),
                Scale = Val.Rand(0.6f, 1.1f),
                Lifespan = 2600,
                Tints = new long[] { 0xf1c40f, 0xe74c3c, 0x3498db, 0x2ecc71, 0x9b59b6, 0xffffff },
            });
        }

        // --- Screens ---

        /// <summary>
        /// Looping background particles for a room: dust, leaves, fireflies, rain, snow, embers, mist. Dust and mist sit
        /// behind the characters, the rest in front, unless `order` says otherwise. Pre-warmed so the room isn't empty.
        /// </summary>
        public static List<Flow> Ambient(Transform parent, IEnumerable<string> kinds, int? order = null)
        {
            const float w = Design.Width, h = Design.Height;
            var flows = new List<Flow>();
            if (kinds == null) return flows;
            foreach (var kind in kinds)
            {
                Emit e;
                var tex = Tex.Soft;
                switch (kind)
                {
                    case "dust":
                        e = new Emit
                        {
                            Zone = new Rect(0, 0, w, h),
                            SpeedX = Val.Rand(-12, 12),
                            SpeedY = Val.Rand(-18, -4),
                            Scale = Val.Rand(0.15f, 0.4f),
                            AlphaOverLife = (t, _) => Mathf.Sin(t * Mathf.PI) * 0.6f,
                            Lifespan = 6000,
                            Frequency = 220,
                            Tint = 0xfff6dd,
                            Add = true,
                        };
                        break;
                    case "leaves":
                        tex = Tex.Leaf;
                        e = new Emit
                        {
                            Zone = new Rect(-100, -30, w + 200, 10),
                            SpeedY = Val.Rand(35, 80),
                            SpeedX = Val.Rand(-40, 20),
                            AccelX = Val.Rand(-12, 12),
                            Rotate = Val.Life(0, 540),
                            Scale = Val.Rand(0.6f, 1.2f),
                            Alpha = Val.Life(0.95f, 0.6f),
                            Lifespan = 11000,
                            Frequency = 420,
                            Tints = new long[] { 0x6b8e23, 0x8fbc3f, 0xc0a040, 0xb5651d },
                        };
                        break;
                    case "fireflies":
                        e = new Emit
                        {
                            Zone = new Rect(0, 120, w, h - 200),
                            SpeedX = Val.Rand(-25, 25),
                            SpeedY = Val.Rand(-25, 25),
                            Scale = Val.Rand(0.3f, 0.6f),
                            AlphaOverLife = (t, px) => Mathf.Sin(t * Mathf.PI) * (0.55f + 0.45f * Mathf.Sin(t * 30 + px)),
                            Lifespan = 5000,
                            Frequency = 260,
                            Tint = 0xd8ff7a,
                            Add = true,
                        };
                        break;
                    case "rain":
                        tex = Tex.Spark;
                        e = new Emit
                        {
                            Zone = new Rect(-200, -40, w + 300, 10),
                            SpeedY = Val.Rand(620, 760),
                            SpeedX = Val.Rand(-150, -120),
                            Rotate = 12,
                            ScaleX = 0.25f,
                            ScaleY = Val.Rand(0.8f, 1.3f),
                            Alpha = Val.Rand(0.15f, 0.35f),
                            Lifespan = 1400,
                            Frequency = 12,
                            Tint = 0xbfd8ff,
                        };
                        break;
                    case "snow":
                        e = new Emit
                        {
                            Zone = new Rect(-100, -30, w + 200, 10),
                            SpeedY = Val.Rand(40, 90),
                            SpeedX = Val.Rand(-30, 10),
                            Scale = Val.Rand(0.12f, 0.3f),
                            Alpha = Val.Rand(0.5f, 0.9f),
                            Lifespan = 12000,
                            Frequency = 70,
                            Tint = 0xffffff,
                        };
                        break;
                    case "embers":
                        e = new Emit
                        {
                            Zone = new Rect(0, h - 160, w, 120),
                            SpeedY = Val.Rand(-70, -30),
                            SpeedX = Val.Rand(-15, 15),
                            Scale = Val.Rand(0.15f, 0.35f),
                            AlphaOverLife = (t, _) => Mathf.Sin(t * Mathf.PI) * 0.8f,
                            Lifespan = 5000,
                            Frequency = 220,
                            Tint = 0xffa040,
                            Add = true,
                        };
                        break;
                    case "mist":
                        e = new Emit
                        {
                            Zone = new Rect(-200, h - 330, w + 200, 200),
                            SpeedX = Val.Rand(8, 22),
                            SpeedY = Val.Rand(-3, 3),
                            Scale = Val.Rand(6, 11),
                            AlphaOverLife = (t, _) => Mathf.Sin(t * Mathf.PI) * 0.12f,
                            Lifespan = 14000,
                            Frequency = 700,
                            Tint = 0xcfe3dc,
                        };
                        break;
                    default:
                        continue;
                }
                e.Order = order ?? (kind == "dust" || kind == "mist" ? OrderAmbientBack : OrderAmbientFront);
                // Pre-warm so the screen isn't empty on entry.
                var f = Stream(parent, 0, 0, tex, e, 6000);
                if (f != null) flows.Add(f);
            }
            return flows;
        }

        /// <summary>Twinkling star field (title and character screens), `count` stars above `maxY`.</summary>
        public static void StarField(Transform parent, int count, float maxY)
        {
            var root = Root(parent);
            if (root == null) return;
            for (int i = 0; i < count; i++)
            {
                bool big = i % 6 == 0;
                float a0 = Random.Range(0.2f, 0.9f);
                var star = Img(root, Get(big ? Tex.Star : Tex.Soft), Between(0, (int)Design.Width), Between(0, (int)maxY), 0xffffff, a0, OrderSky, true);
                var s = Random.Range(0.1f, big ? 0.5f : 0.25f);
                ScaleTo(star, s, s);
                Anim.Handle h = null;
                h = Tw(Between(800, 2600), Lin, t =>
                {
                    if (star == null)
                    {
                        h?.Stop();
                        return;
                    }
                    SetAlpha(star, Mathf.Lerp(a0, 0.1f, t));
                }, null, Between(0, 2000), -1, true);
            }
        }

        /// <summary>Embers drifting up from the bottom of the title screen.</summary>
        public static Flow Embers(Transform parent) =>
            Stream(parent, 0, 0, Tex.Soft, new Emit
            {
                X = Val.Rand(0, Design.Width),
                Y = Design.Height + 10,
                SpeedY = Val.Rand(-70, -30),
                SpeedX = Val.Rand(-15, 15),
                Scale = Val.Life(0.35f, 0),
                Alpha = Val.Life(0.9f, 0),
                Lifespan = 7000,
                Frequency = 140,
                Tints = new long[] { 0xffb347, 0xff7b39, 0xffe066 },
                Add = true,
                Order = OrderSky + 10,
            }, 7000);

        // --- Colors ---

        static readonly long[] Neutral = { 0xe6f0ff, 0x9fb7ff, 0x7a5cff };

        /// <summary>An element's particle palette, bright to dark (ELEMENT_COLORS; NEUTRAL_COLORS for null).</summary>
        public static long[] Colors(string element)
        {
            var data = Rules.Db.Data;
            if (data != null)
            {
                if (element != null && data.ElementColors != null && data.ElementColors.TryGetValue(element, out var c) && c != null) return c;
                if (data.NeutralColors != null && data.NeutralColors.Length > 0) return data.NeutralColors;
            }
            return Neutral;
        }

        /// <summary>A Color as 0xRRGGBB.</summary>
        public static long Hex(Color c) =>
            ((long)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255) << 16) | ((long)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255) << 8) |
            (long)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255);

        internal static Color RampAt(Color[] ramp, float t)
        {
            if (ramp.Length == 1) return ramp[0];
            int m = ramp.Length - 1;
            float f = m * Mathf.Clamp01(t);
            int i = Mathf.Min(m - 1, Mathf.FloorToInt(f));
            return Color.LerpUnclamped(ramp[i], ramp[i + 1], f - i);
        }

        // --- Plumbing ---

        static readonly Dictionary<Transform, FxSystem> worlds = new Dictionary<Transform, FxSystem>();

        static Transform DefaultParent()
        {
            var game = GameRoot.I;
            return game != null && game.Screen != null ? game.Screen.World : null;
        }

        /// <summary>The shared burst system under `parent`: its root sits on the design origin (0, 0).</summary>
        static FxSystem WorldSystem(Transform parent)
        {
            if (parent == null) parent = DefaultParent();
            if (parent == null) return null;
            if (worlds.TryGetValue(parent, out var s) && s.Root != null) return s;
            var go = new GameObject("fx");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Design.World(0, 0), Quaternion.identity);
            s = new FxSystem(go.transform) { Key = parent };
            worlds[parent] = s;
            return s;
        }

        static Transform Root(Transform parent) => WorldSystem(parent)?.Root;

        /// <summary>Drops a dead system from the cache.</summary>
        internal static void Forget(FxSystem s)
        {
            if (s.Key is object && worlds.TryGetValue(s.Key, out var cur) && cur == s) worlds.Remove(s.Key);
        }

        internal static void ResetStatics()
        {
            worlds.Clear();
            flashes.Clear();
        }

        static Vector3 P(float x, float y) => new Vector3(x / Design.PixelsPerUnit, -y / Design.PixelsPerUnit, 0);

        static Transform Holder(Transform under, float x, float y, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(under, false);
            t.localPosition = P(x, y);
            return t;
        }

        static SpriteRenderer Img(Transform under, Sprite sprite, float x, float y, long color, float alpha, int order, bool add)
        {
            var t = Holder(under, x, y, sprite != null ? sprite.name : "fx");
            var r = t.gameObject.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            BindTexture(r);
            Use(r, add ? AdditiveMaterial : AlphaMaterial);
            r.sortingOrder = order;
            r.color = Design.Hex(color, alpha);
            return r;
        }

        /// <summary>A generated shape under `holder`, placed so the shape's own origin sits on the holder.</summary>
        static SpriteRenderer ShapeChild(Transform holder, Shape shape, long color, float alpha, int order, bool add) =>
            Img(holder, shape.Sprite, shape.Center.x, shape.Center.y, color, alpha, order, add);

        /// <summary>A straight line `width` thick from a to b (Graphics.lineBetween), under `under`.</summary>
        static SpriteRenderer Line(Transform under, float ax, float ay, float bx, float by, float width, long color, float alpha, int order, bool add)
        {
            float dx = bx - ax, dy = by - ay;
            var r = Img(under, Art.White, (ax + bx) / 2, (ay + by) / 2, color, alpha, order, add);
            r.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(Mathf.Sqrt(dx * dx + dy * dy), width, 1);
            return r;
        }

        /// <summary>A stroked polyline (Graphics.strokePoints): segments, with round joints when `joints`.</summary>
        static void Polyline(Transform under, Vector2[] pts, float width, long color, float alpha, Group g, bool joints)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
                g.Add(Line(under, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width, color, alpha, Depth(903), true));
            if (!joints) return;
            for (int i = 1; i + 1 < pts.Length; i++)
            {
                var dot = Img(under, Art.Circle, pts[i].x, pts[i].y, color, alpha, Depth(903), true);
                ScaleTo(dot, width / 64f, width / 64f);
                g.Add(dot);
            }
        }

        static void ScaleTo(Component c, float sx, float sy)
        {
            if (c != null) c.transform.localScale = new Vector3(sx, sy, 1);
        }

        /// <summary>Phaser angle: degrees clockwise.</summary>
        static void AngleTo(Component c, float deg)
        {
            if (c != null) c.transform.localRotation = Quaternion.Euler(0, 0, -deg);
        }

        static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            var c = r.color;
            c.a = Mathf.Clamp01(a);
            r.color = c;
        }

        static void Kill(Component c)
        {
            if (c != null) Object.Destroy(c.gameObject);
        }

        /// <summary>A tween of t 0-1 over `ms` (game time, so hit-stops slow it), eased by `ease`.</summary>
        static Anim.Handle Tw(float ms, Func<float, float> ease, Action<float> step, Action done = null, float delayMs = 0, int repeat = 0,
            bool yoyo = false)
        {
            var h = Anim.Float(0, 1, Mathf.Max(0.001f, ms / 1000f), t => step(ease(t)), Ease.Linear, Mathf.Max(0, delayMs) / 1000f, repeat, yoyo);
            if (done != null) h.OnComplete(done);
            return h;
        }

        static readonly Func<float, float> Lin = t => t;
        static readonly Func<float, float> QuadIn = t => t * t;
        static readonly Func<float, float> CubicIn = t => t * t * t;
        static readonly Func<float, float> CubicOut = t => 1 - (1 - t) * (1 - t) * (1 - t);
        static readonly Func<float, float> SineIn = t => 1 - Mathf.Cos(t * Mathf.PI / 2);
        static readonly Func<float, float> SineOut = t => Mathf.Sin(t * Mathf.PI / 2);
        static readonly Func<float, float> SineInOut = t => -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
        static readonly Func<float, float> BackOut = t => Anim.Apply(Ease.BackOut, t);

        /// <summary>Phaser's Stepped ease.</summary>
        static float Stepped(float t, int steps) => t <= 0 ? 0 : t >= 1 ? 1 : (Mathf.Floor(steps * t) + 1) / steps;

        /// <summary>Phaser.Math.Between: a whole number from a to b, both included.</summary>
        static int Between(int a, int b) => Random.Range(a, b + 1);

        /// <summary>JavaScript's Math.round (halves round up; Mathf.Round rounds them to even).</summary>
        static int RoundJs(float v) => Mathf.FloorToInt(v + 0.5f);

        /// <summary>Renderers faded together (a Graphics object's alpha).</summary>
        sealed class Group
        {
            readonly List<SpriteRenderer> rs = new List<SpriteRenderer>();
            readonly List<float> baseAlpha = new List<float>();

            public SpriteRenderer Add(SpriteRenderer r)
            {
                rs.Add(r);
                baseAlpha.Add(r.color.a);
                return r;
            }

            public void Alpha(float a)
            {
                a = Mathf.Clamp01(a);
                for (int i = 0; i < rs.Count; i++)
                {
                    var r = rs[i];
                    if (r == null) continue;
                    var c = r.color;
                    c.a = baseAlpha[i] * a;
                    r.color = c;
                }
            }
        }

        sealed class FlashState
        {
            public readonly List<SpriteRenderer> Rs = new List<SpriteRenderer>();
            public readonly List<Material> Mats = new List<Material>();
            public readonly List<Color> Cols = new List<Color>();
            public int Token;
        }

        static readonly Dictionary<Figure, FlashState> flashes = new Dictionary<Figure, FlashState>();

        static void Paint(FlashState st, long color)
        {
            var c = Design.Hex(color);
            var m = FillMaterial;
            foreach (var r in st.Rs)
            {
                if (r == null) continue;
                Use(r, m);
                r.color = new Color(c.r, c.g, c.b, r.color.a);
            }
        }

        /// <summary>Forgets flashes of figures destroyed mid-flash (their screen ended).</summary>
        static void PurgeFlashes()
        {
            if (flashes.Count == 0) return;
            List<Figure> dead = null;
            foreach (var kv in flashes)
                if (kv.Key == null) (dead ??= new List<Figure>()).Add(kv.Key);
            if (dead == null) return;
            foreach (var f in dead) flashes.Remove(f);
        }
    }
}
