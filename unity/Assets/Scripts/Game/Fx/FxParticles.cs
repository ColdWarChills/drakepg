using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drakepg
{
    public static partial class Fx
    {
        /// <summary>
        /// True (the default): particles slow with Anim.TimeScale, so a hit-stop freezes them with the tweens. False lets
        /// them fly through hit-stops like on the web, where only tweens and timers froze.
        /// </summary>
        public static bool ParticlesFollowHitStop = true;

        /// <summary>
        /// A particle value, like a Phaser emitter op: a number (fixed), `Rand(min, max)` (picked at birth) or
        /// `Life(start, end)` (eased linearly over the particle's life). Ints and floats convert to a fixed value.
        /// </summary>
        public struct Val
        {
            internal byte Kind; // 0 unset, 1 fixed, 2 random, 3 over life
            internal float A, B;

            public static implicit operator Val(float v) => new Val { Kind = 1, A = v, B = v };

            public static Val Rand(float min, float max) => new Val { Kind = 2, A = min, B = max };

            public static Val Life(float start, float end) => new Val { Kind = 3, A = start, B = end };

            public bool IsSet => Kind != 0;

            /// <summary>The value at birth and at death for one particle.</summary>
            internal void Sample(out float start, out float end)
            {
                if (Kind == 2) start = end = A + (B - A) * Random.value;
                else
                {
                    start = A;
                    end = B;
                }
            }

            internal float Pick()
            {
                Sample(out var s, out _);
                return s;
            }
        }

        /// <summary>
        /// A particle emitter config (Phaser's ParticleEmitterConfig). Positions are design px, y down, relative to the
        /// emitter; angles are degrees clockwise (0 = right, 90 = down); times are ms like the web.
        /// Velocity: `Speed` flies in a random direction within `Angle` (default all around); `SpeedX`/`SpeedY` add a
        /// straight velocity; `MoveTo` instead flies each particle to that point, arriving at the end of its life.
        /// Size: `Scale` for both axes, or `ScaleX`/`ScaleY`. Color: `Tint`/`Tints` (picked at birth) or `Colors`
        /// (blended through over the particle's life, Phaser's `color`).
        /// </summary>
        public sealed class Emit
        {
            public Val Speed, Angle, SpeedX, SpeedY, AccelX, Scale, ScaleX, ScaleY, Alpha, Rotate, Lifespan, Delay, X, Y;
            public float GravityY;
            public long[] Tints, Colors;
            /// <summary>One tint for every particle.</summary>
            public long Tint { set => Tints = new[] { value }; }
            /// <summary>Additive blending (Phaser's blendMode 'ADD').</summary>
            public bool Add;
            /// <summary>Spawn at a random point in this rectangle (x, y, width, height) around the emitter.</summary>
            public Rect? Zone;
            /// <summary>Spawn on evenly spaced points of a circle's edge (Phaser's edge zone with `quantity`).</summary>
            public float EdgeRadius;
            public int EdgeCount;
            /// <summary>Spawn at the point this returns (a custom zone).</summary>
            public Func<Vector2> ZoneFn;
            public Vector2? MoveTo;
            /// <summary>Turn each particle along its flight, plus this many degrees (90 for textures whose long side is vertical).</summary>
            public float? FaceVelocity;
            /// <summary>Custom alpha / scale over life: (t 0-1, particle x) -> value. Override Alpha / Scale.</summary>
            public Func<float, float, float> AlphaOverLife, ScaleOverLife;
            /// <summary>For streams: ms between particles.</summary>
            public float Frequency;
            /// <summary>Sorting order of the particles.</summary>
            public int Order = OrderFx;

            internal Color[] ramp;

            internal Color[] Ramp()
            {
                if (ramp == null || ramp.Length != Colors.Length)
                {
                    ramp = new Color[Colors.Length];
                    for (int i = 0; i < Colors.Length; i++) ramp[i] = Design.Hex(Colors[i]);
                }
                return ramp;
            }
        }

        /// <summary>A running particle stream (an emitter with a frequency). Stop it to let its particles die out.</summary>
        public sealed class Flow
        {
            internal Emit C;
            internal Sprite S;
            internal Func<Vector2> Pos;
            internal float Counter;
            internal int Index;
            internal FxSystem Sys;
            internal bool On = true;

            /// <summary>The stream's own transform: particles live in its space and move with it.</summary>
            public Transform Root => Sys?.Root;

            public bool IsOn => On && Sys != null && Sys.Root != null;

            /// <summary>Stops emitting; the stream's object goes away once its last particle dies.</summary>
            public void Stop() => On = false;

            /// <summary>Moves the stream (and the particles in flight with it, like a moved Phaser emitter) to design px.</summary>
            public void SetPosition(float x, float y)
            {
                var root = Root;
                if (root != null) root.position = Design.World(x, y, root.position.z);
            }
        }
    }

    /// <summary>One live particle: a pooled SpriteRenderer plus its state (design px, y down, in its system's space).</summary>
    internal sealed class FxParticle
    {
        public SpriteRenderer R;
        public Transform T;
        public Fx.Emit C;
        public Fx.Flow Owner;
        public float X, Y, Vx, Vy, Ax, Mx, My, Life, Age, Delay;
        public float Sx0, Sx1, Sy0, Sy1, A0, A1, R0, R1;
        public Color Tint;
    }

    /// <summary>
    /// Particles under one transform (`Root`, whose origin is the emitters' (0, 0)): pooled SpriteRenderers updated by
    /// FxRunner. The root sits under the screen's World (or a figure), so the particles die with it.
    /// </summary>
    internal sealed class FxSystem
    {
        public readonly Transform Root;
        /// <summary>The parent this is the shared burst system of (Fx's cache key), if any.</summary>
        public Transform Key;
        /// <summary>Destroy the root once every stream has stopped and every particle is gone.</summary>
        public bool Temporary;

        readonly List<FxParticle> live = new List<FxParticle>();
        readonly Stack<FxParticle> pool = new Stack<FxParticle>();
        readonly List<Fx.Flow> flows = new List<Fx.Flow>();

        public FxSystem(Transform root)
        {
            Root = root;
            FxRunner.Register(this);
        }

        public bool Finished => Temporary && flows.Count == 0 && live.Count == 0;

        public Fx.Flow AddFlow(Fx.Emit c, Sprite s, Func<Vector2> pos = null)
        {
            var f = new Fx.Flow { C = c, S = s, Pos = pos, Sys = this, Counter = Mathf.Max(1, c.Frequency) };
            flows.Add(f);
            return f;
        }

        /// <summary>Runs a stream ahead so the screen isn't empty on entry (Phaser's fastForward).</summary>
        public void Prewarm(Fx.Flow f, float ms)
        {
            int steps = Mathf.CeilToInt(ms / (1000f / 60));
            for (int i = 0; i < steps; i++) Step(1 / 60f, f, false);
            foreach (var p in live)
                if (p.Owner == f) Apply(p, Mathf.Clamp01(p.Age / p.Life));
        }

        FxParticle NewParticle()
        {
            var go = new GameObject("p");
            go.transform.SetParent(Root, false);
            return new FxParticle { R = go.AddComponent<SpriteRenderer>(), T = go.transform };
        }

        public void Spawn(Fx.Emit c, Sprite sprite, float ex, float ey, int index, Fx.Flow owner, bool render = true)
        {
            if (Root == null) return;
            FxParticle p = null;
            while (pool.Count > 0 && p == null)
            {
                p = pool.Pop();
                if (p.R == null) p = null;
            }
            if (p == null) p = NewParticle();
            p.C = c;
            p.Owner = owner;

            float zx = 0, zy = 0;
            if (c.Zone.HasValue)
            {
                var z = c.Zone.Value;
                zx = z.x + Random.value * z.width;
                zy = z.y + Random.value * z.height;
            }
            else if (c.EdgeCount > 0)
            {
                float a = index % c.EdgeCount / (float)c.EdgeCount * Mathf.PI * 2;
                zx = Mathf.Cos(a) * c.EdgeRadius;
                zy = Mathf.Sin(a) * c.EdgeRadius;
            }
            else if (c.ZoneFn != null)
            {
                var v = c.ZoneFn();
                zx = v.x;
                zy = v.y;
            }
            p.X = ex + zx + (c.X.IsSet ? c.X.Pick() : 0);
            p.Y = ey + zy + (c.Y.IsSet ? c.Y.Pick() : 0);
            p.Life = Mathf.Max(0.001f, (c.Lifespan.IsSet ? c.Lifespan.Pick() : 1000) / 1000f);
            p.Age = 0;
            p.Delay = c.Delay.IsSet ? c.Delay.Pick() / 1000f : 0;

            if (c.Scale.IsSet)
            {
                c.Scale.Sample(out p.Sx0, out p.Sx1);
                p.Sy0 = p.Sx0;
                p.Sy1 = p.Sx1;
            }
            else
            {
                if (c.ScaleX.IsSet) c.ScaleX.Sample(out p.Sx0, out p.Sx1);
                else p.Sx0 = p.Sx1 = 1;
                if (c.ScaleY.IsSet) c.ScaleY.Sample(out p.Sy0, out p.Sy1);
                else p.Sy0 = p.Sy1 = 1;
            }
            if (c.Alpha.IsSet) c.Alpha.Sample(out p.A0, out p.A1);
            else p.A0 = p.A1 = 1;
            if (c.Rotate.IsSet) c.Rotate.Sample(out p.R0, out p.R1);
            else p.R0 = p.R1 = 0;

            p.Vx = p.Vy = 0;
            if (c.MoveTo.HasValue)
            {
                p.Mx = ex + c.MoveTo.Value.x;
                p.My = ey + c.MoveTo.Value.y;
                p.Vx = (p.Mx - p.X) / p.Life;
                p.Vy = (p.My - p.Y) / p.Life;
            }
            else
            {
                if (c.Speed.IsSet)
                {
                    float s = Mathf.Abs(c.Speed.Pick());
                    float a = (c.Angle.IsSet ? c.Angle.Pick() : Random.value * 360) * Mathf.Deg2Rad;
                    p.Vx = Mathf.Cos(a) * s;
                    p.Vy = Mathf.Sin(a) * s;
                }
                if (c.SpeedX.IsSet) p.Vx += c.SpeedX.Pick();
                if (c.SpeedY.IsSet) p.Vy += c.SpeedY.Pick();
            }
            p.Ax = c.AccelX.IsSet ? c.AccelX.Pick() : 0;
            p.Tint = c.Tints != null && c.Tints.Length > 0 ? Design.Hex(c.Tints[Random.Range(0, c.Tints.Length)]) : Color.white;

            var r = p.R;
            r.sprite = sprite;
            var m = c.Add ? Fx.AdditiveMaterial : Fx.AlphaMaterial;
            if (m != null) r.sharedMaterial = m;
            r.sortingOrder = c.Order;
            r.enabled = true;
            live.Add(p);
            if (render) Apply(p, 0);
        }

        /// <summary>Moves every particle on by `dt` seconds, then lets the streams emit. `only` limits it to one stream (prewarm).</summary>
        public void Step(float dt, Fx.Flow only = null, bool render = true)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                if (only != null && p.Owner != only) continue;
                if (p.R == null)
                {
                    live.RemoveAt(i);
                    continue;
                }
                if (p.Delay > 0)
                {
                    p.Delay -= dt;
                    continue;
                }
                float t = Mathf.Clamp01(p.Age / p.Life);
                if (p.C.MoveTo.HasValue)
                {
                    float rem = p.Life - p.Age;
                    if (rem > 0.0001f)
                    {
                        p.Vx = (p.Mx - p.X) / rem;
                        p.Vy = (p.My - p.Y) / rem;
                    }
                }
                p.Vx += p.Ax * dt;
                p.Vy += p.C.GravityY * dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                if (render) Apply(p, t);
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.R.enabled = false;
                    p.C = null;
                    p.Owner = null;
                    pool.Push(p);
                    live[i] = live[live.Count - 1];
                    live.RemoveAt(live.Count - 1);
                }
            }

            for (int i = flows.Count - 1; i >= 0; i--)
            {
                var f = flows[i];
                if (only != null && f != only) continue;
                if (!f.On)
                {
                    flows.RemoveAt(i);
                    continue;
                }
                float every = Mathf.Max(1, f.C.Frequency);
                f.Counter -= dt * 1000;
                int guard = 0;
                while (f.Counter <= 0 && guard++ < 500)
                {
                    var pos = f.Pos != null ? f.Pos() : Vector2.zero;
                    Spawn(f.C, f.S, pos.x, pos.y, f.Index++, f, render);
                    f.Counter += every;
                }
                if (f.Counter <= 0) f.Counter = every;
            }
        }

        static void Apply(FxParticle p, float t)
        {
            var c = p.C;
            float sx, sy;
            if (c.ScaleOverLife != null) sx = sy = c.ScaleOverLife(t, p.X);
            else
            {
                sx = p.Sx0 + (p.Sx1 - p.Sx0) * t;
                sy = p.Sy0 + (p.Sy1 - p.Sy0) * t;
            }
            float a = Mathf.Clamp01(c.AlphaOverLife != null ? c.AlphaOverLife(t, p.X) : p.A0 + (p.A1 - p.A0) * t);
            float rot = c.FaceVelocity.HasValue
                ? Mathf.Atan2(p.Vy, p.Vx) * Mathf.Rad2Deg + c.FaceVelocity.Value
                : p.R0 + (p.R1 - p.R0) * t;
            var col = c.Colors != null && c.Colors.Length > 0 ? Fx.RampAt(c.Ramp(), t) : p.Tint;
            col.a = a;
            p.T.localPosition = new Vector3(p.X / Design.PixelsPerUnit, -p.Y / Design.PixelsPerUnit, 0);
            p.T.localRotation = Quaternion.Euler(0, 0, -rot);
            p.T.localScale = new Vector3(sx, sy, 1);
            p.R.color = col;
        }
    }
}
