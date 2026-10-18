using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// Motion for UI pieces (the felt HUD, menus, the inventory): slide in and out, pop, jolt when hit, a ring of thread.
    /// Slides and jolts move an element's `translate`, so use them on plain containers, not on elements placed with an
    /// anchor (Ui.Place with ax/ay) or on FeltButtons (their hover lift is a translate too): wrap those in a holder.
    /// One translate tween per element at a time; a new one takes over from where the old one stopped.
    /// </summary>
    public static class UiMotion
    {
        static readonly Dictionary<VisualElement, Anim.Handle> moving = new Dictionary<VisualElement, Anim.Handle>();

        static Vector2 Offset(VisualElement e)
        {
            var t = e.style.translate.value;
            return new Vector2(t.x.value, t.y.value);
        }

        static void Set(VisualElement e, Vector2 at) => e.style.translate = new Translate(at.x, at.y);

        static readonly List<VisualElement> stale = new List<VisualElement>();

        /// <summary>Stop `e`'s translate tween, and forget tweens that ended (a screen left mid-tween cancels them).</summary>
        static void Take(VisualElement e)
        {
            if (moving.TryGetValue(e, out var old)) old.Stop();
            stale.Clear();
            foreach (var kv in moving) if (kv.Value.IsDone) stale.Add(kv.Key);
            foreach (var k in stale) moving.Remove(k);
        }

        /// <summary>Tween `e` from where it is now to `to` (px from its place).</summary>
        public static Anim.Handle Move(VisualElement e, Vector2 to, float seconds, Ease ease = Ease.CubicOut, float delay = 0)
        {
            Take(e);
            var from = Offset(e);
            var h = Anim.Float(0, 1, seconds, t => Set(e, Vector2.LerpUnclamped(from, to, t)), ease, delay);
            moving[e] = h;
            h.OnComplete(() => { if (moving.TryGetValue(e, out var cur) && cur == h) moving.Remove(e); });
            return h;
        }

        /// <summary>Start `from` px off its place and slide home (pieces coming on screen).</summary>
        public static Anim.Handle SlideIn(VisualElement e, Vector2 from, float seconds, float delay = 0, Ease ease = Ease.BackOut)
        {
            Set(e, from);
            return Move(e, Vector2.zero, seconds, ease, delay);
        }

        /// <summary>Slide `by` px away from its place (pieces leaving).</summary>
        public static Anim.Handle SlideOut(VisualElement e, Vector2 by, float seconds, float delay = 0, Ease ease = Ease.CubicIn) =>
            Move(e, by, seconds, ease, delay);

        /// <summary>Fade in from 0 (after `delay`).</summary>
        public static Anim.Handle FadeIn(VisualElement e, float seconds, float delay = 0, float to = 1)
        {
            e.style.opacity = 0;
            return Anim.Float(0, to, seconds, v => e.style.opacity = v, Ease.Linear, delay);
        }

        /// <summary>Grow from `from` to full size with an overshoot (around its transform origin).</summary>
        public static Anim.Handle Pop(VisualElement e, float from = 0.3f, float seconds = 0.3f, float delay = 0, Ease ease = Ease.BackOut)
        {
            e.style.scale = new Scale(Vector3.one * from);
            return Anim.Float(from, 1, seconds, s => e.style.scale = new Scale(new Vector3(s, s, 1)), ease, delay);
        }

        /// <summary>A quick sideways shake that dies down (a HUD piece taking a hit). `px` is the first swing.</summary>
        public static Anim.Handle Jolt(VisualElement e, float px, float seconds = 0.28f)
        {
            Take(e);
            var h = Anim.Float(0, 1, seconds, t =>
            {
                var fade = 1 - t;
                Set(e, new Vector2(Mathf.Sin(t * Mathf.PI * 7) * px * fade * fade, Mathf.Sin(t * Mathf.PI * 5) * px * 0.3f * fade));
            }, Ease.Linear);
            moving[e] = h;
            h.OnComplete(() =>
            {
                Set(e, Vector2.zero);
                if (moving.TryGetValue(e, out var cur) && cur == h) moving.Remove(e);
            });
            return h;
        }

        /// <summary>A ring of thread that bursts out from (cx, cy) and fades (Attack's turn pop, an item landing in its slot).</summary>
        public static void Ring(VisualElement parent, float cx, float cy, float radius, Color color, float width = 4, float seconds = 0.45f, float delay = 0)
        {
            var ring = new VisualElement { pickingMode = PickingMode.Ignore, name = "ring" };
            Ui.Place(ring, cx - radius, cy - radius);
            Ui.Size(ring, radius * 2, radius * 2);
            Ui.Round(ring, radius);
            Ui.Border(ring, width, color);
            ring.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            ring.style.opacity = 0;
            parent.Add(ring);
            Anim.Float(0, 1, seconds, t =>
            {
                var s = Mathf.Lerp(0.55f, 1.35f, t);
                ring.style.scale = new Scale(new Vector3(s, s, 1));
                ring.style.opacity = 0.9f * (1 - t);
            }, Ease.CubicOut, delay).OnComplete(ring.RemoveFromHierarchy);
        }

        /// <summary>A full-screen layer (1280 x 720, clicks pass through) to group pieces that move together.</summary>
        public static VisualElement Layer(VisualElement parent, string name)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, name = name };
            Ui.Place(e, 0, 0);
            Ui.Size(e, Design.Width, Design.Height);
            parent.Add(e);
            return e;
        }
    }
}
