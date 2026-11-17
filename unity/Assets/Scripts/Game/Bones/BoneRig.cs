#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Drakepg.Bones
{
    /// <summary>
    /// The hero's bones as data: joints, parts (parent, joint, draw order), part pictures per set (bare and gear),
    /// weapons and their grip points, faces (fight.json) and swap pictures (swaps.json, optional). Rig canvas px,
    /// y down; angles clockwise on screen, 0 = forward. Read through an IBoneSource; read again when rig.json,
    /// fight.json or swaps.json changes on disk. A missing or half-written swaps.json gives a warning, never a crash.
    /// </summary>
    public sealed class BoneRig
    {
        /// <summary>The canvas point that sits on the hero's own position (between the feet, on the ground).</summary>
        public const float Ground = 1345, Centre = 760;

        public sealed class Part
        {
            public string Name, Parent, Joint;
            public float Z;
            public int Index;
        }

        /// <summary>A picture that replaces a part's picture for a while (swaps.json "swaps"), with its own joints.</summary>
        public sealed class Swap
        {
            public string Name, Part, File, Note;
            /// <summary>Brought by a move as a stand-in (not from swaps.json).</summary>
            public bool StandIn;
            public Vector2 Corner;
            public Dictionary<string, Vector2> Joints = new Dictionary<string, Vector2>();
            /// <summary>A swap hand's own grip point and angle (null: the rig's).</summary>
            public Vector2? GripAt;
            public float GripAngle;
        }

        public readonly IBoneSource Source;
        public float Scale;
        public readonly Dictionary<string, Vector2> J = new Dictionary<string, Vector2>();
        public readonly Dictionary<string, float> Radius = new Dictionary<string, float>();
        public readonly List<Part> Parts = new List<Part>();
        public readonly Dictionary<string, Part> PartOf = new Dictionary<string, Part>();
        public JObject Json, Fight, Swaps;
        public readonly Dictionary<string, Swap> SwapPics = new Dictionary<string, Swap>();
        /// <summary>Parts each gear slot covers (rig.json "slots").</summary>
        public readonly Dictionary<string, List<string>> Slots = new Dictionary<string, List<string>>();
        long stampRig, stampFight, stampSwaps;

        /// <summary>World units per rig px at hero scale 1.</summary>
        public float Unit => Scale / Design.PixelsPerUnit;

        static readonly Dictionary<string, BoneRig> loaded = new Dictionary<string, BoneRig>();

        /// <summary>The rig of a source (read once, read again when its files change).</summary>
        public static BoneRig Of(IBoneSource source)
        {
            var key = source.Key("rig.json");
            if (loaded.TryGetValue(key, out var r) && !r.Stale()) return r;
            r = new BoneRig(source);
            loaded[key] = r;
            return r;
        }

        /// <summary>Forget every loaded rig (the next Of reads the files again).</summary>
        public static void Forget() => loaded.Clear();

        bool Stale() =>
            Source.Stamp("rig.json") != stampRig || Source.Stamp("fight.json") != stampFight || Source.Stamp("swaps.json") != stampSwaps;

        public static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        BoneRig(IBoneSource source)
        {
            Source = source;
            stampRig = source.Stamp("rig.json");
            stampFight = source.Stamp("fight.json");
            stampSwaps = source.Stamp("swaps.json");
            var text = source.Text("rig.json");
            if (text == null) throw new InvalidOperationException("bones: no rig.json at " + source.Key("rig.json"));
            Json = JObject.Parse(text);
            Scale = (float)Json["scale"];
            foreach (var j in (JObject)Json["joints"]) J[j.Key] = V(j.Value);
            if (Json["radius"] is JObject rad) foreach (var j in rad) Radius[j.Key] = (float)j.Value;
            foreach (var p in Json["parts"])
            {
                var part = new Part
                {
                    Name = (string)p["name"], Parent = (string)p["parent"], Joint = (string)p["joint"], Z = (float)p["z"], Index = Parts.Count,
                };
                Parts.Add(part);
                PartOf[part.Name] = part;
            }
            if (Json["slots"] is JObject slots)
                foreach (var s in slots)
                {
                    var list = new List<string>();
                    foreach (var p in s.Value) list.Add((string)p);
                    Slots[s.Key] = list;
                }
            var fight = source.Text("fight.json");
            Fight = fight != null ? JObject.Parse(fight) : new JObject();
            ReadSwaps();
        }

        /// <summary>swaps.json (the picture worker's file): tolerated missing, half-written or odd.</summary>
        void ReadSwaps()
        {
            Swaps = new JObject();
            var text = Source.Text("swaps.json");
            if (text == null) return;
            try { Swaps = JObject.Parse(text); }
            catch (Exception e)
            {
                Debug.LogWarning("bones: swaps.json can't be read yet (" + e.Message + "); going on without it");
                Swaps = new JObject();
                return;
            }
            if (Swaps["radius"] is JObject rad)
                foreach (var j in rad)
                    try { Radius[j.Key] = (float)j.Value; }
                    catch (Exception) { Debug.LogWarning("bones: swaps.json radius." + j.Key + " is not a number"); }
            if (!(Swaps["swaps"] is JObject list)) return;
            foreach (var s in list)
            {
                var sw = ParseSwap(s.Key, s.Value as JObject, "swaps.json");
                if (sw != null) SwapPics[s.Key] = sw;
            }
        }

        /// <summary>One swap entry (swaps.json "swaps", or a move's own "swaps" stand-ins); null and a warning when unusable.</summary>
        public Swap ParseSwap(string name, JObject o, string from)
        {
            try
            {
                if (o == null) throw new FormatException("not an object");
                var sw = new Swap
                {
                    Name = name, Part = (string)o["part"], File = (string)o["file"], Note = (string)o["note"],
                    Corner = new Vector2(o["x"] != null ? (float)o["x"] : 0, o["y"] != null ? (float)o["y"] : 0),
                };
                if (sw.Part == null || sw.File == null || !PartOf.ContainsKey(sw.Part))
                {
                    Debug.LogWarning($"bones: {from} swap '{name}' has no known part or no file; skipped");
                    return null;
                }
                if (o["joints"] is JObject js) foreach (var j in js) sw.Joints[j.Key] = V(j.Value);
                if (o["grip"] is JObject g && g["at"] != null)
                {
                    sw.GripAt = V(g["at"]);
                    sw.GripAngle = g["angle"] != null ? (float)g["angle"] : 0;
                }
                return sw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"bones: {from} swap '{name}' can't be read ({e.Message}); skipped");
                return null;
            }
        }

        /// <summary>A part's picture entry in a set: rig.json sets, then swaps.json sets (null: the set lacks it).</summary>
        public JToken SetPicture(string set, string part)
        {
            var e = Json["sets"]?[set]?[part];
            if (e != null) return e;
            return Swaps["sets"]?[set]?[part];
        }

        public bool HasSet(string set) => Json["sets"]?[set] != null || Swaps["sets"]?[set] != null;

        /// <summary>A face picture entry (fight.json faces, then swaps.json faces); for a helm its own one first.</summary>
        public JToken Face(string name, string helm)
        {
            if (helm != null)
            {
                var e = Fight["faces"]?[name + "_" + helm] ?? Swaps["faces"]?[name + "_" + helm];
                if (e != null) return e;
            }
            return Fight["faces"]?[name] ?? Swaps["faces"]?[name];
        }

        public JToken Weapon(string id) => id == null ? null : Json["weapons"]?[id];

        /// <summary>The rig's grip point and angle of a hand ("n" or "f").</summary>
        public (Vector2 at, float angle) Grip(string side) => (V(Json["grips"][side]["at"]), (float)Json["grips"][side]["angle"]);

        public float WeaponZ(string side) => (float)Json["weaponZ"][side];

        /// <summary>The far-front order (rig.json farFront): part or "f_weapon" -> z.</summary>
        public float? FarFront(string name) => Json["farFront"]?[name] != null ? (float)Json["farFront"][name] : (float?)null;

        // --- pictures ---------------------------------------------------------------------------------------------

        static readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();

        /// <summary>
        /// A picture with its turning point at `pivot` (picture px, y down). Cached by the file's full name, its write
        /// time and the pivot, so a redrawn or new picture shows on the next look without a restart.
        /// </summary>
        public Sprite Picture(string file, Vector2 pivot)
        {
            long stamp = Source.Stamp(file);
            string key = Source.Key(file) + "|" + stamp + "|" + pivot.x.ToString("0.##") + "|" + pivot.y.ToString("0.##");
            if (pictures.TryGetValue(key, out var s) && s != null) return s;
            var bytes = Source.Bytes(file);
            if (bytes == null)
            {
                Debug.LogWarning("bones: picture missing: " + Source.Key(file));
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadImage(bytes);
            var name = file.Substring(file.LastIndexOf('/') + 1);
            tex.name = name.Contains(".") ? name.Substring(0, name.LastIndexOf('.')) : name;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(pivot.x / tex.width, 1 - pivot.y / tex.height),
                Design.PixelsPerUnit / Scale, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            return pictures[key] = s;
        }

        /// <summary>A set entry's picture turning on the rig joint `joint` (the entry's corner at x, y on the canvas).</summary>
        public Sprite Picture(JToken entry, Vector2 joint) =>
            entry == null ? null : Picture((string)entry["file"], joint - new Vector2((float)entry["x"], (float)entry["y"]));
    }
}
#endif
