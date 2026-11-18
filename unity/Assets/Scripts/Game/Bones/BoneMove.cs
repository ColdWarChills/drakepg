#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Drakepg.Bones
{
    /// <summary>A named moment on a move's timeline: "contact", "face:shout", "shake:120,8"...</summary>
    public struct BoneEvent
    {
        /// <summary>ms from the move's start at x1.</summary>
        public float T;
        public string Name, Arg, Raw;

        public static BoneEvent Of(float t, string raw)
        {
            int colon = raw.IndexOf(':');
            return new BoneEvent
            {
                T = t, Raw = raw, Name = colon < 0 ? raw : raw.Substring(0, colon), Arg = colon < 0 ? null : raw.Substring(colon + 1),
            };
        }

        public override string ToString() => Raw;
    }

    /// <summary>
    /// A move read from a file (unity/Bones/hero/moves/&lt;name&gt;.json; the format is in unity/Bones/HOWTO.md): keys
    /// that name only the channels that change, an ease per key (how it arrives there), a lag per channel, holds,
    /// events. Each channel is its own track made of the keys that name it, so `Sample(ms)` gives the pose at any
    /// moment. Unknown channels or events give a warning naming the file and the key, never an exception.
    /// </summary>
    public sealed class BoneMove
    {
        public string Name, File;
        /// <summary>The weapon kind it is made for (sword, dagger, staff ...).</summary>
        public string Weapon;
        /// <summary>everyday, skill or showpiece.</summary>
        public string Kind = "everyday";
        /// <summary>Weapons the move holds when the hero is given none: side ("n", "f") -> rig weapon id.</summary>
        public readonly Dictionary<string, string> Weapons = new Dictionary<string, string>();
        /// <summary>How far short of the target (design px) "travel": 1 stops.</summary>
        public float Reach = 170;
        public readonly Dictionary<string, float> Lag = new Dictionary<string, float>();
        /// <summary>Per side ("n", "f"): the hand swings on an arc around its shoulder between keys (true, default) or goes in a straight line.</summary>
        public readonly Dictionary<string, bool> Arc = new Dictionary<string, bool> { ["n"] = true, ["f"] = true };
        /// <summary>Named draw orders for "order:&lt;name&gt;" events: part (or n_weapon / f_weapon) -> z.</summary>
        public readonly Dictionary<string, Dictionary<string, float>> Orders = new Dictionary<string, Dictionary<string, float>>();
        public readonly List<BoneEvent> Events = new List<BoneEvent>();
        /// <summary>Key names and times (for logs and sheets).</summary>
        public readonly List<KeyValuePair<string, float>> KeyNames = new List<KeyValuePair<string, float>>();
        /// <summary>The move's length in ms at x1: the last key, hold or event plus the largest lag (or the file's own).</summary>
        public float Length;
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Stand-in swap pictures the move brings itself (same shape as swaps.json "swaps"); a real swap of the same
        /// name in swaps.json wins.</summary>
        public JObject StandIns;

        struct Point
        {
            public float T;
            public Vector2 V;
            public string Ease;
            public float Sweep;
        }

        readonly Dictionary<string, List<Point>> tracks = new Dictionary<string, List<Point>>();
        readonly Dictionary<string, List<Point>> handTargets = new Dictionary<string, List<Point>>();
        BoneRig compiledFor;
        /// <summary>The side channels "n.ik"/"f.ik" default to 1 when the move places that hand by a target.</summary>
        float nIk, fIk;

        static readonly HashSet<string> KeyFields = new HashSet<string> { "t", "ease", "name", "hold", "note" };
        static readonly HashSet<string> TopFields = new HashSet<string>
            { "name", "note", "weapon", "kind", "weapons", "reach", "arc", "lag", "orders", "keys", "events", "length", "swaps" };
        static readonly HashSet<string> EventNames = new HashSet<string>
        {
            "contact", "face", "torso", "hand", "order", "trail", "sound", "freeze", "shake", "kick", "zoom", "dim", "mark", "weapon", "key",
            "ghost", "lines",
        };

        void Warn(string text)
        {
            Warnings.Add(text);
            Debug.LogWarning("bones: " + File + ": " + text);
        }

        /// <summary>Read a move file's text. `file` names it in warnings.</summary>
        public static BoneMove Parse(string json, string file, BoneRig rig = null)
        {
            var m = new BoneMove { File = file };
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Exception e)
            {
                m.Warn("not valid JSON: " + e.Message);
                o = new JObject();
            }
            foreach (var f in o) if (!TopFields.Contains(f.Key)) m.Warn($"unknown field '{f.Key}' (ignored)");
            m.Name = (string)o["name"] ?? file;
            m.Weapon = (string)o["weapon"];
            if (o["kind"] != null)
            {
                m.Kind = (string)o["kind"];
                if (m.Kind != "everyday" && m.Kind != "skill" && m.Kind != "showpiece") m.Warn($"kind '{m.Kind}' is not everyday, skill or showpiece");
            }
            if (o["reach"] != null) m.Reach = (float)o["reach"];
            if (o["weapons"] is JObject ws)
                foreach (var w in ws)
                {
                    if (w.Key != "n" && w.Key != "f") m.Warn($"weapons.{w.Key}: a hand is \"n\" or \"f\"");
                    else m.Weapons[w.Key] = w.Value.Type == JTokenType.Null ? null : (string)w.Value;
                    if (rig != null && w.Value.Type != JTokenType.Null && rig.Weapon((string)w.Value) == null)
                        m.Warn($"weapons.{w.Key}: no weapon '{w.Value}' in rig.json");
                }
            if (o["arc"] is JObject arc) foreach (var a in arc) m.Arc[a.Key] = (bool)a.Value;
            StandInsOf(m, o);
            if (o["lag"] is JObject lag) foreach (var l in lag) m.Lag[l.Key] = (float)l.Value;
            if (o["orders"] is JObject orders)
                foreach (var ord in orders)
                {
                    var map = new Dictionary<string, float>();
                    foreach (var p in (JObject)ord.Value) map[p.Key] = (float)p.Value;
                    m.Orders[ord.Key] = map;
                }
            float last = 0;
            if (o["keys"] is JArray keys)
                for (int k = 0; k < keys.Count; k++)
                {
                    if (!(keys[k] is JObject key) || key["t"] == null)
                    {
                        m.Warn($"key #{k} has no time \"t\"; skipped");
                        continue;
                    }
                    float t = (float)key["t"], hold = key["hold"] != null ? (float)key["hold"] : 0;
                    string ease = (string)key["ease"] ?? "SineInOut";
                    if (!BoneEase.Known(ease)) m.Warn($"key #{k} (t {t} ms): unknown ease '{ease}' (Linear used)");
                    string where = $"key #{k} (t {t} ms" + (key["name"] != null ? ", " + key["name"] : "") + ")";
                    if (key["name"] != null) m.KeyNames.Add(new KeyValuePair<string, float>((string)key["name"], t));
                    float nSweep = key["n.sweep"] != null ? (float)key["n.sweep"] : 0, fSweep = key["f.sweep"] != null ? (float)key["f.sweep"] : 0;
                    foreach (var f in key)
                    {
                        if (KeyFields.Contains(f.Key) || BoneChannels.KeyOnly(f.Key)) continue;
                        if (!BoneChannels.IsKnown(f.Key, rig))
                        {
                            m.Warn($"{where}: unknown channel '{f.Key}' (ignored)");
                            continue;
                        }
                        bool pair = BoneChannels.IsPair(f.Key);
                        Vector2 v;
                        try
                        {
                            if (pair != (f.Value is JArray)) throw new FormatException(pair ? "needs [x, y]" : "needs a number");
                            v = pair ? new Vector2((float)f.Value[0], (float)f.Value[1]) : new Vector2((float)f.Value, 0);
                        }
                        catch (Exception e)
                        {
                            m.Warn($"{where}: channel '{f.Key}' {e.Message} (ignored)");
                            continue;
                        }
                        var into = f.Key == "n.hand" || f.Key == "f.hand" ? m.handTargets : m.tracks;
                        if (!into.TryGetValue(f.Key, out var list)) into[f.Key] = list = new List<Point>();
                        float sweep = f.Key == "n.hand" ? nSweep : f.Key == "f.hand" ? fSweep : 0;
                        list.Add(new Point { T = t, V = v, Ease = ease, Sweep = sweep });
                        if (hold > 0) list.Add(new Point { T = t + hold, V = v, Ease = "Linear" });
                    }
                    last = Mathf.Max(last, t + hold);
                }
            else m.Warn("no \"keys\" list");
            if (o["events"] is JArray evs)
                for (int k = 0; k < evs.Count; k++)
                {
                    var e = evs[k];
                    float t;
                    string raw;
                    if (e is JArray a && a.Count >= 2) { t = (float)a[0]; raw = (string)a[1]; }
                    else if (e is JObject eo && eo["t"] != null && eo["e"] != null) { t = (float)eo["t"]; raw = (string)eo["e"]; }
                    else
                    {
                        m.Warn($"event #{k} is not [t, \"name\"]; skipped");
                        continue;
                    }
                    var ev = BoneEvent.Of(t, raw);
                    if (!EventNames.Contains(ev.Name)) m.Warn($"event #{k} (t {t} ms): unknown event '{raw}' (passed on, but nothing in the core knows it)");
                    m.Events.Add(ev);
                    last = Mathf.Max(last, t);
                }
            foreach (var kv in m.KeyNames) m.Events.Add(BoneEvent.Of(kv.Value, "key:" + kv.Key));
            m.Events.Sort((a, b) => a.T.CompareTo(b.T));
            foreach (var list in m.tracks.Values) list.Sort((a, b) => a.T.CompareTo(b.T));
            foreach (var list in m.handTargets.Values) list.Sort((a, b) => a.T.CompareTo(b.T));
            m.nIk = m.handTargets.ContainsKey("n.hand") ? 1 : 0;
            m.fIk = m.handTargets.ContainsKey("f.hand") ? 1 : 0;
            float maxLag = 0;
            foreach (var l in m.Lag.Values) maxLag = Mathf.Max(maxLag, l);
            m.Length = o["length"] != null ? (float)o["length"] : last + maxLag;
            return m;
        }

        static void StandInsOf(BoneMove m, JObject o) => m.StandIns = o["swaps"] as JObject;

        static Vector2 At(List<Point> list, float t)
        {
            if (t <= list[0].T) return list[0].V;
            var end = list[list.Count - 1];
            if (t >= end.T) return end.V;
            for (int i = 0; i + 1 < list.Count; i++)
            {
                var b = list[i + 1];
                if (t > b.T) continue;
                var a = list[i];
                float k = b.T - a.T <= 0.0001f ? 1 : (t - a.T) / (b.T - a.T);
                return Vector2.LerpUnclamped(a.V, b.V, BoneEase.Apply(b.Ease, k));
            }
            return end.V;
        }

        /// <summary>
        /// The hand targets ("n.hand": where the grip goes, or the wrist of an empty hand, in rig px from the waist)
        /// become a direction and a distance from the shoulder at that key, so the hand swings on an arc around the
        /// shoulder between keys. The direction goes the way the key's "sweep" says (+1 clockwise on screen, -1 the
        /// other way, 0 the short way), never decided by a coin flip at play time. `shoulder(pose, side, ms)` gives the
        /// shoulder relative to the waist for a pose (the hero knows its pictures and swaps).
        /// </summary>
        public void Compile(BoneRig rig, Func<BonePose, string, float, Vector2> shoulder)
        {
            if (compiledFor == rig) return;
            compiledFor = rig;
            foreach (var side in new[] { "n", "f" })
            {
                if (!handTargets.TryGetValue(side + ".hand", out var pts)) continue;
                if (Arc.TryGetValue(side, out var arc) && !arc)
                {
                    tracks[side + ".hand"] = pts;
                    continue;
                }
                var aim = new List<Point>();
                var reach = new List<Point>();
                float prev = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    var body = Sample(p.T, false, false);
                    var d = p.V - shoulder(body, side, p.T);
                    float raw = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    float a = raw;
                    if (i > 0)
                    {
                        float delta = Mathf.Repeat(raw - prev + 180, 360) - 180;
                        if (p.Sweep > 0 && delta < 0) delta += 360;
                        if (p.Sweep < 0 && delta > 0) delta -= 360;
                        a = prev + delta;
                    }
                    prev = a;
                    aim.Add(new Point { T = p.T, V = new Vector2(a, 0), Ease = p.Ease });
                    reach.Add(new Point { T = p.T, V = new Vector2(d.magnitude, 0), Ease = p.Ease });
                }
                tracks[side + ".aim"] = aim;
                tracks[side + ".reach"] = reach;
            }
        }

        /// <summary>The pose at `ms` (x1 time from the start). With `lag`, each channel is read its lag earlier.</summary>
        public BonePose Sample(float ms, bool lag = true) => Sample(ms, lag, true);

        BonePose Sample(float ms, bool lag, bool hands)
        {
            var p = new BonePose { NIk = nIk, FIk = fIk };
            foreach (var kv in tracks)
            {
                if (!hands && (kv.Key.EndsWith(".aim") || kv.Key.EndsWith(".reach"))) continue;
                float t = lag ? ms - BoneChannels.Lag(Lag, kv.Key) : ms;
                p.Set(kv.Key, At(kv.Value, Mathf.Max(0, t)));
            }
            return p;
        }

        /// <summary>The last argument of event `name` at or before `ms` (null: none yet).</summary>
        public string Last(string name, float ms)
        {
            string arg = null;
            foreach (var e in Events)
            {
                if (e.T > ms) break;
                if (e.Name == name) arg = e.Arg;
            }
            return arg;
        }

        /// <summary>Every hand swap argument at or before `ms`, in order (each sets its own hand; "none" clears both).</summary>
        public IEnumerable<string> All(string name, float ms)
        {
            foreach (var e in Events)
            {
                if (e.T > ms) yield break;
                if (e.Name == name) yield return e.Arg;
            }
        }

        public bool Names(string channel) => tracks.ContainsKey(channel) || handTargets.ContainsKey(channel);
    }

    /// <summary>Eases by name: the game's (Anim's Ease names) plus QuadInOut and Step (jump on arrival).</summary>
    public static class BoneEase
    {
        public static bool Known(string name) => name == "QuadInOut" || name == "Step" || Enum.TryParse<Ease>(name, out _);

        public static float Apply(string name, float t)
        {
            if (name == "QuadInOut") return t < 0.5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t);
            if (name == "Step") return t < 1 ? 0 : 1;
            return Enum.TryParse<Ease>(name, out var e) ? Anim.Apply(e, t) : t;
        }
    }
}
#endif
