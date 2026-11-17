#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg.Bones
{
    /// <summary>
    /// One pose: a value per channel (a number or a pair). Channels a pose doesn't name have their rest value
    /// (BoneChannels.Default), so the empty pose is the rig's own layout. Units: rig canvas px (y down), degrees
    /// clockwise on screen (0 = forward, 90 = down). Turns of hips, torso, head, arms are absolute on screen, as in
    /// the old test rig.
    /// </summary>
    public sealed class BonePose
    {
        readonly Dictionary<string, Vector2> v = new Dictionary<string, Vector2>();
        /// <summary>Per side ("n", "f") the default of `ik` when the pose doesn't name it.</summary>
        public float NIk, FIk;

        public bool Has(string c) => v.ContainsKey(c);
        public Vector2 V(string c) => v.TryGetValue(c, out var x) ? x : Default(c);
        public float F(string c) => V(c).x;
        public void Set(string c, Vector2 x) => v[c] = x;
        public void Set(string c, float x) => v[c] = new Vector2(x, 0);
        public void Clear(string c) => v.Remove(c);
        public IEnumerable<string> Names => v.Keys;

        Vector2 Default(string c)
        {
            if (c == "n.ik") return new Vector2(NIk, 0);
            if (c == "f.ik") return new Vector2(FIk, 0);
            return BoneChannels.Default(c);
        }

        public BonePose Clone()
        {
            var p = new BonePose { NIk = NIk, FIk = FIk };
            foreach (var kv in v) p.v[kv.Key] = kv.Value;
            return p;
        }
    }

    /// <summary>The channels a move may key, their kind (number or pair) and rest values.</summary>
    public static class BoneChannels
    {
        /// <summary>Numbers. "n."/"f." = near side (the sword arm, on screen-left at rest) / far side.</summary>
        static readonly HashSet<string> Numbers = new HashSet<string>
        {
            "rootTurn", "facing", "travel", "plant", "hips", "torso", "head",
            "n.blade", "n.wrist", "n.bend", "n.ik", "n.upper", "n.fore", "n.inhand", "n.on", "n.onT", "n.knee", "n.toe", "n.sweep",
            "f.blade", "f.wrist", "f.bend", "f.ik", "f.upper", "f.fore", "f.inhand", "f.on", "f.onT", "f.knee", "f.toe", "f.sweep",
            // made from n.hand / f.hand when a move is read (the hand's direction and distance from its shoulder)
            "n.aim", "n.reach", "f.aim", "f.reach",
        };

        /// <summary>Pairs (x, y).</summary>
        static readonly HashSet<string> Pairs = new HashSet<string> { "root", "squash", "n.hand", "f.hand", "n.foot", "f.foot" };

        /// <summary>Per part / per joint channels: scale.&lt;part&gt; (pair), slide.&lt;joint&gt; (pair), shade.&lt;part&gt; (number).</summary>
        public static bool IsKnown(string c, BoneRig rig)
        {
            if (Numbers.Contains(c) || Pairs.Contains(c)) return true;
            if (c.StartsWith("scale.") || c.StartsWith("shade.")) return rig == null || rig.PartOf.ContainsKey(c.Substring(6));
            if (c.StartsWith("slide.")) return rig == null || rig.J.ContainsKey(c.Substring(6));
            return false;
        }

        public static bool IsPair(string c) => Pairs.Contains(c) || c.StartsWith("scale.") || c.StartsWith("slide.");

        /// <summary>Channels only keys carry (not interpolated into the pose).</summary>
        public static bool KeyOnly(string c) => c == "n.sweep" || c == "f.sweep";

        public static Vector2 Default(string c)
        {
            switch (c)
            {
                case "squash": return Vector2.one;
                case "facing": case "plant": case "n.bend": case "f.bend": return new Vector2(1, 0);
                case "n.knee": case "f.knee": return new Vector2(-1, 0);
            }
            if (c.StartsWith("scale.")) return Vector2.one;
            return Vector2.zero;
        }

        /// <summary>The lag group of a channel: "n.blade" -> lag["n.blade"], else lag["n"]; "scale.torso" -> lag["torso"].</summary>
        public static float Lag(Dictionary<string, float> lag, string c)
        {
            if (lag == null || lag.Count == 0) return 0;
            if (lag.TryGetValue(c, out var ms)) return ms;
            // the hand's arc channels follow the hand's lag
            if ((c.EndsWith(".aim") || c.EndsWith(".reach")) && lag.TryGetValue(c.Substring(0, 1) + ".hand", out ms)) return ms;
            int dot = c.IndexOf('.');
            if (dot > 0)
            {
                var head = c.Substring(0, dot);
                var tail = c.Substring(dot + 1);
                if ((head == "scale" || head == "slide" || head == "shade") && lag.TryGetValue(tail, out ms)) return ms;
                if (lag.TryGetValue(head, out ms)) return ms;
            }
            return 0;
        }
    }
}
#endif
