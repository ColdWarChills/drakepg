using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakepg
{
    public enum Ease { Linear, QuadOut, QuadIn, CubicOut, CubicIn, CubicInOut, BackOut, BackIn, SineInOut, BounceOut, ElasticOut }

    /// <summary>
    /// A small tween runner (what Phaser's scene.tweens did): interpolate a value over time and await it. Tweens
    /// belong to an owner (a screen) and stop when it is cleared, so nothing animates a destroyed object.
    /// `TimeScale` slows everything (hit-stop); `Paused` freezes it.
    /// </summary>
    public class Anim
    {
        public class Handle
        {
            internal float Delay, Duration, Elapsed;
            internal Action<float> Update;
            internal Ease Ease;
            internal int Repeat;
            internal bool Yoyo, Forward = true, Done, Cancelled;
            internal object Owner;
            internal TaskCompletionSource<bool> Tcs;
            internal Action Complete;

            public bool IsDone => Done;
            public Task Task => (Tcs ??= new TaskCompletionSource<bool>()).Task;
            public void Stop() => Done = true;
            public Handle OnComplete(Action a)
            {
                Complete += a;
                return this;
            }
        }

        static readonly List<Handle> active = new List<Handle>();
        static readonly List<Handle> adding = new List<Handle>();
        public static float TimeScale = 1;
        /// <summary>The battle speed option (x2): every tween runs this much faster. The battle sets it; any screen change puts it back to 1.</summary>
        public static float Speed = 1;

        /// <summary>The owner new tweens get when none is given: the current screen.</summary>
        public static object DefaultOwner;

        public static Handle Float(float from, float to, float duration, Action<float> update, Ease ease = Ease.CubicOut, float delay = 0,
            int repeat = 0, bool yoyo = false, object owner = null)
        {
            var h = new Handle
            {
                Duration = Mathf.Max(0.0001f, duration), Delay = delay, Ease = ease, Repeat = repeat, Yoyo = yoyo, Owner = owner ?? DefaultOwner,
                Update = t => update(Mathf.LerpUnclamped(from, to, t)),
            };
            adding.Add(h);
            return h;
        }

        /// <summary>Repeat forever (-1) is allowed: `repeat: -1`.</summary>
        public static Handle Move(Transform target, Vector3 to, float duration, Ease ease = Ease.CubicOut, float delay = 0, int repeat = 0, bool yoyo = false)
        {
            Vector3 from = default;
            bool started = false;
            return Float(0, 1, duration, t =>
            {
                if (target == null) return;
                if (!started)
                {
                    from = target.position;
                    started = true;
                }
                target.position = Vector3.LerpUnclamped(from, to, t);
            }, ease, delay, repeat, yoyo);
        }

        public static Handle Scale(Transform target, Vector3 to, float duration, Ease ease = Ease.CubicOut, float delay = 0, int repeat = 0, bool yoyo = false)
        {
            Vector3 from = default;
            bool started = false;
            return Float(0, 1, duration, t =>
            {
                if (target == null) return;
                if (!started)
                {
                    from = target.localScale;
                    started = true;
                }
                target.localScale = Vector3.LerpUnclamped(from, to, t);
            }, ease, delay, repeat, yoyo);
        }

        public static Handle Fade(SpriteRenderer target, float to, float duration, Ease ease = Ease.Linear, float delay = 0)
        {
            float from = 0;
            bool started = false;
            return Float(0, 1, duration, t =>
            {
                if (target == null) return;
                if (!started)
                {
                    from = target.color.a;
                    started = true;
                }
                var c = target.color;
                c.a = Mathf.LerpUnclamped(from, to, t);
                target.color = c;
            }, ease, delay);
        }

        /// <summary>Wait in game time (slowed by TimeScale).</summary>
        public static Task Wait(float seconds) => Float(0, 1, seconds, _ => { }, Ease.Linear).Task;

        public static Handle After(float seconds, Action a) => Float(0, 1, seconds, _ => { }, Ease.Linear).OnComplete(a);

        /// <summary>Stop every tween of this owner (a screen being left).</summary>
        public static void Clear(object owner)
        {
            // Cancelled tweens never finish their Task, so code awaiting them on a screen that is gone stops there.
            foreach (var h in active) if (h.Owner == owner) h.Done = h.Cancelled = true;
            foreach (var h in adding) if (h.Owner == owner) h.Done = h.Cancelled = true;
        }

        public static void Tick(float dt)
        {
            active.AddRange(adding);
            adding.Clear();
            dt *= TimeScale * Speed;
            for (int i = 0; i < active.Count; i++)
            {
                var h = active[i];
                if (h.Done) continue;
                if (h.Delay > 0)
                {
                    h.Delay -= dt;
                    if (h.Delay > 0) continue;
                }
                h.Elapsed += dt;
                var t = Mathf.Clamp01(h.Elapsed / h.Duration);
                try { h.Update(Apply(h.Ease, h.Forward ? t : 1 - t)); }
                catch (Exception e) { Debug.LogException(e); h.Done = true; }
                if (t < 1) continue;
                if (h.Yoyo && h.Forward)
                {
                    h.Forward = false;
                    h.Elapsed = 0;
                    continue;
                }
                if (h.Repeat != 0)
                {
                    if (h.Repeat > 0) h.Repeat--;
                    h.Forward = true;
                    h.Elapsed = 0;
                    continue;
                }
                h.Done = true;
                try { h.Complete?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (!active[i].Done) continue;
                if (!active[i].Cancelled) active[i].Tcs?.TrySetResult(true);
                active.RemoveAt(i);
            }
        }

        public static float Apply(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.QuadOut: return 1 - (1 - t) * (1 - t);
                case Ease.QuadIn: return t * t;
                case Ease.CubicOut: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.CubicIn: return t * t * t;
                case Ease.CubicInOut: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case Ease.BackOut:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                }
                case Ease.BackIn:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Ease.SineInOut: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case Ease.BounceOut:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1 / d1) return n1 * t * t;
                    if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
                    if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
                    return n1 * (t -= 2.625f / d1) * t + 0.984375f;
                }
                case Ease.ElasticOut:
                    return t == 0 ? 0 : t == 1 ? 1 : Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
                default: return t;
            }
        }
    }
}
