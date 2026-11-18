#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Drakepg.Bones
{
    /// <summary>Who hears a move's events while it plays (the test stage tonight, the battle screen later).</summary>
    public interface IBoneEvents
    {
        void OnBoneEvent(BoneHero hero, BoneEvent e);
    }

    /// <summary>
    /// The game's own tools for a move's events: freeze -> Fx.HitStop, shake -> GameRoot.Shake, kick -> GameRoot.Kick
    /// (along the hero's facing), zoom -> GameRoot.View (clamped so no edge shows, like the battle's ViewIn), sound ->
    /// Sfx.Play. "contact" and "dim" are the caller's (the battle's Impact and Dim are its own): set Contact and Dim.
    /// face / torso / hand / order / trail / key / mark are the hero's own; they come here too, for logs.
    /// </summary>
    public class BoneGameEvents : IBoneEvents
    {
        public Action<BoneHero, BoneEvent> Contact;
        /// <summary>dim:&lt;alpha&gt;[,ms]: darken the backdrop to alpha over ms.</summary>
        public Action<float, float> Dim;
        /// <summary>Every event, before it is handled (logging).</summary>
        public Action<BoneHero, BoneEvent> Heard;
        /// <summary>Where a zoom leans in (design px); default between the hero and its target.</summary>
        public Func<BoneHero, Vector2> ZoomAt;
        static readonly HashSet<string> warned = new HashSet<string>();

        public static float Num(BoneEvent e, int i, float fallback = 0)
        {
            if (e.Arg == null) return fallback;
            var p = e.Arg.Split(',');
            return i < p.Length && float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        public virtual void OnBoneEvent(BoneHero hero, BoneEvent e)
        {
            Heard?.Invoke(hero, e);
            switch (e.Name)
            {
                case "contact": Contact?.Invoke(hero, e); break;
                case "freeze": Fx.HitStop(Num(e, 0, 70)); break;
                case "shake": GameRoot.I.Shake(Num(e, 0, 90) / 1000f, Num(e, 1, 4)); break;
                case "kick": GameRoot.I.Kick(Num(e, 0) * hero.Facing, Num(e, 1)); break;
                case "zoom":
                {
                    float z = Mathf.Max(1, Num(e, 0, 1)), s = Num(e, 1, 200) / 1000f;
                    var at = z <= 1.0001f ? new Vector2(Design.Width / 2, Design.Height / 2)
                        : ZoomAt != null ? ZoomAt(hero) : (hero.Home + hero.TargetSpot) / 2 + new Vector2(0, -150);
                    float halfW = Design.Width / 2 / z, halfH = Design.Height / 2 / z;
                    GameRoot.I.View(Mathf.Clamp(at.x, halfW, Design.Width - halfW), Mathf.Clamp(at.y, halfH, Design.Height - halfH), z, s, Ease.QuadOut);
                    break;
                }
                case "sound": if (e.Arg != null) Sfx.Play(e.Arg); break;
                case "dim": Dim?.Invoke(Num(e, 0, 0.35f), Num(e, 1, 180)); break;
                case "lines":
                {
                    // speed lines streaking behind the hero where it is now (the battle's lunge: Fx.SpeedLines)
                    var t = hero.Frame != null ? hero.Frame.Travel : 0;
                    float x = Mathf.LerpUnclamped(hero.Home.x, hero.TargetSpot.x, t);
                    Fx.SpeedLines(hero.transform.parent, x, hero.Home.y, hero.Facing);
                    break;
                }
                case "face": case "torso": case "hand": case "order": case "trail": case "key": case "mark": case "weapon": case "ghost": case "grip": break;
                default:
                    if (warned.Add(e.Name)) Debug.LogWarning("bones: no one handles the event '" + e.Raw + "'");
                    break;
            }
        }
    }
}
#endif
