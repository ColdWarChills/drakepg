#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Drakepg.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg.Test
{
    /// <summary>
    /// TEST, not part of the game (docs/PLANS.md "How the hero is put together"): the hero as separate parts on joints,
    /// gear as a swap of pictures per part, the weapon between the forearm and the fist. Pictures and numbers come from
    /// art/redo/layers/parts (scripts/hero_layers_rig.py: rig.json, poses.json), read from disk in the editor only;
    /// nothing here is in a build. A pose is a turn per part plus where the hips and ankles are; the legs are solved
    /// from hip to ankle every frame, so the feet stay planted while the body moves between poses.
    /// Angles as in the rig script: clockwise on screen, 0 = forward, 90 = down, in rig canvas px.
    /// </summary>
    public class LayeredHero : MonoBehaviour
    {
        const float Ground = 1345, Centre = 760;
        static readonly string[] Arms = { "n_upper", "n_fore", "n_hand", "f_upper", "f_fore", "f_hand" };

        class Rig
        {
            public JObject Json, Poses;
            public float Scale;
            public Dictionary<string, Vector2> J = new Dictionary<string, Vector2>();
            public List<(string name, string parent, string joint, float z)> Parts = new List<(string, string, string, float)>();
            public Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
            public string Dir;
        }

        public class Pose
        {
            public float Hips, Torso, Head;
            public Dictionary<string, float> Arm = new Dictionary<string, float>();
            public Vector2 Root, NAnkle, FAnkle;
            public bool FarFront, On;
            public float OnT, OnBend;
            public string[] Weapon = new string[2];
        }

        static Rig rig;
        Pose pose;
        readonly Dictionary<string, SpriteRenderer> parts = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, string> wear = new Dictionary<string, string>();
        readonly SpriteRenderer[] weapons = new SpriteRenderer[2];
        float unit;

        /// <summary>Where the parts are. Set it by hand when the editor opens the project through a link (C:\Users\..\dk).</summary>
        public static string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/redo/layers/parts"));

        /// <summary>Forget the loaded pictures and numbers (after the rig script wrote new ones).</summary>
        public static void Reload() => rig = null;

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static Rig Load()
        {
            if (rig != null) return rig;
            var r = new Rig { Dir = Folder };
            r.Json = JObject.Parse(File.ReadAllText(Path.Combine(r.Dir, "rig.json")));
            r.Poses = JObject.Parse(File.ReadAllText(Path.Combine(r.Dir, "poses.json")));
            r.Scale = (float)r.Json["scale"];
            foreach (var j in (JObject)r.Json["joints"]) r.J[j.Key] = V(j.Value);
            foreach (var p in r.Json["parts"])
                r.Parts.Add(((string)p["name"], (string)p["parent"], (string)p["joint"], (float)p["z"]));
            return rig = r;
        }

        static Sprite Picture(string file, Vector2 pivot)
        {
            var r = Load();
            if (r.Sprites.TryGetValue(file, out var s)) return s;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadImage(File.ReadAllBytes(Path.Combine(r.Dir, file)));
            // Straight alpha: bleed the colour into the clear pixels so turned edges get no dark fringe.
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(pivot.x / tex.width, 1 - pivot.y / tex.height),
                Design.PixelsPerUnit / r.Scale, 0, SpriteMeshType.FullRect);
            return r.Sprites[file] = s;
        }

        public static LayeredHero Create(Transform parent, float x, float y, float scale = 1, int facing = 1)
        {
            var r = Load();
            var go = new GameObject("layered hero");
            go.transform.SetParent(parent, false);
            go.transform.position = Design.World(x, y);
            go.transform.localScale = new Vector3(scale * facing, scale, 1);
            go.AddComponent<SortingGroup>().sortingOrder = Design.Depth(y);
            var h = go.AddComponent<LayeredHero>();
            h.unit = r.Scale / Design.PixelsPerUnit;
            foreach (var p in r.Parts)
            {
                var sr = new GameObject(p.name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(go.transform, false);
                h.parts[p.name] = sr;
            }
            for (int i = 0; i < 2; i++)
            {
                h.weapons[i] = new GameObject(i == 0 ? "near weapon" : "far weapon").AddComponent<SpriteRenderer>();
                h.weapons[i].transform.SetParent(go.transform, false);
            }
            h.Dress();
            h.Set("rest");
            return h;
        }

        public int Order { set => GetComponent<SortingGroup>().sortingOrder = value; }

        /// <summary>Gear per slot (chest, legs, helm): the name of a cut set, or null for the bare hero.</summary>
        public void Wear(string slot, string set)
        {
            wear[slot] = set;
            Dress();
        }

        void Dress()
        {
            var r = Load();
            var slotOf = new Dictionary<string, string>();
            foreach (var s in (JObject)r.Json["slots"])
                foreach (var p in s.Value) slotOf[(string)p] = s.Key;
            foreach (var p in r.Parts)
            {
                string set = "bare";
                if (slotOf.TryGetValue(p.name, out var slot) && wear.TryGetValue(slot, out var w) && w != null) set = w;
                var e = r.Json["sets"][set][p.name];
                parts[p.name].sprite = Picture((string)e["file"], r.J[p.joint] - new Vector2((float)e["x"], (float)e["y"]));
            }
        }

        public static Pose Named(string name)
        {
            var j = Load().Poses[name];
            if (j == null) throw new ArgumentException("no pose " + name);
            var p = new Pose
            {
                Hips = (float)j["hips"], Torso = (float)j["torso"], Head = (float)j["head"], Root = V(j["root"]),
                NAnkle = V(j["n_ankle"]), FAnkle = V(j["f_ankle"]), FarFront = (bool)j["far_front"],
            };
            foreach (var a in Arms) p.Arm[a] = (float)j[a];
            if (j["on"].Type != JTokenType.Null)
            {
                p.On = true;
                p.OnT = (float)j["on"]["t"];
                p.OnBend = (float)j["on"]["bend"];
            }
            int i = 0;
            foreach (var w in j["weapon"]) p.Weapon[i++] = w.Type == JTokenType.Null ? null : (string)w;
            return p;
        }

        public void Set(string name) => Apply(pose = Named(name));

        // For the skeleton swing (SkeletonSwing.cs): poses made in code, and the rig's numbers and pictures.
        public void Show(Pose p) => Apply(pose = p);
        public static Vector2 Joint(string name) => Load().J[name];
        public static JObject RigJson => Load().Json;
        public static Sprite PartSprite(string file, Vector2 pivot) => Picture(file, pivot);
        public static float Unit => Load().Scale / Design.PixelsPerUnit;
        public static Vector2 Canvas(Vector2 c) => new Vector2((c.x - Centre) * Unit, (Ground - c.y) * Unit);

        /// <summary>Move from the pose it is in to another one.</summary>
        public Anim.Handle To(string name, float seconds, Ease ease = Ease.CubicOut)
        {
            Pose a = pose, b = Named(name);
            return Anim.Float(0, 1, seconds, k => Apply(pose = Mix(a, b, k)), ease, owner: this);
        }

        static Pose Mix(Pose a, Pose b, float k)
        {
            var p = new Pose
            {
                Hips = Mathf.LerpAngle(a.Hips, b.Hips, k), Torso = Mathf.LerpAngle(a.Torso, b.Torso, k), Head = Mathf.LerpAngle(a.Head, b.Head, k),
                Root = Vector2.LerpUnclamped(a.Root, b.Root, k), NAnkle = Vector2.LerpUnclamped(a.NAnkle, b.NAnkle, k),
                FAnkle = Vector2.LerpUnclamped(a.FAnkle, b.FAnkle, k),
                FarFront = k < 0.5f ? a.FarFront : b.FarFront, Weapon = b.Weapon,
                // Both poses hold the near weapon with the far fist: it stays on the shaft all the way.
                On = a.On && b.On, OnT = Mathf.LerpUnclamped(a.OnT, b.OnT, k), OnBend = b.OnBend,
            };
            foreach (var arm in Arms) p.Arm[arm] = Mathf.LerpAngle(a.Arm[arm], b.Arm[arm], k);
            return p;
        }

        static Vector2 Rot(Vector2 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float Ang(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        static float Norm(float a) => Mathf.Repeat(a + 180, 360) - 180;

        /// <summary>Two bones from a to the target: the direction of each, the middle joint on the bend side.</summary>
        static (float, float) Ik(Vector2 a, Vector2 target, float l1, float l2, float bend)
        {
            var dv = target - a;
            float d = Mathf.Max(Mathf.Abs(l1 - l2) + 0.5f, Mathf.Min(dv.magnitude, l1 + l2 - 0.5f));
            float b = Mathf.Atan2(dv.y, dv.x);
            float inner = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1));
            float d1 = b + bend * inner;
            var mid = a + l1 * new Vector2(Mathf.Cos(d1), Mathf.Sin(d1));
            var end = a + d * new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            return (d1 * Mathf.Rad2Deg, Ang(end - mid));
        }

        void Apply(Pose p)
        {
            var r = Load();
            var j = r.J;
            var g = r.Json["grips"];
            var turn = new Dictionary<string, float> { ["hips"] = p.Hips, ["torso"] = p.Torso, ["head"] = p.Head };
            foreach (var a in Arms) turn[a] = p.Arm[a];
            var waist = j["waist"] + p.Root;
            foreach (var s in new[] { "n", "f" })
            {
                var hip = waist + Rot(j[s + "_hip"] - j["waist"], p.Hips);
                var ankle = j[s + "_ankle"] + (s == "n" ? p.NAnkle : p.FAnkle);
                float l1 = (j[s + "_knee"] - j[s + "_hip"]).magnitude, l2 = (j[s + "_ankle"] - j[s + "_knee"]).magnitude;
                var (d1, d2) = Ik(hip, ankle, l1, l2, -1);
                var (r1, r2) = Ik(j[s + "_hip"], j[s + "_ankle"], l1, l2, -1);
                turn[s + "_thigh"] = Norm(d1 - r1);
                turn[s + "_shin"] = Norm(d2 - r2);
                turn[s + "_foot"] = 0;
            }
            var at = Places(r, turn, waist);
            Vector2 Grip(string s) => at[s + "_hand"] + Rot(V(g[s]["at"]) - j[s + "_wrist"], turn[s + "_hand"]);
            if (p.On)
            {
                float wdir = turn["n_hand"] + (float)g["n"]["angle"];
                var hold = Grip("n") + Rot(new Vector2(p.OnT, 0), wdir);
                float hand = Norm(wdir - (float)g["f"]["angle"]);
                if (Mathf.Abs(hand) > 90) hand = Norm(hand + 180);
                var target = hold - Rot(V(g["f"]["at"]) - j["f_wrist"], hand);
                float l1 = (j["f_elbow"] - j["f_shoulder"]).magnitude, l2 = (j["f_wrist"] - j["f_elbow"]).magnitude;
                var (d1, d2) = Ik(at["f_upper"], target, l1, l2, p.OnBend);
                turn["f_upper"] = Norm(d1 - Ang(j["f_elbow"] - j["f_shoulder"]));
                turn["f_fore"] = Norm(d2 - Ang(j["f_wrist"] - j["f_elbow"]));
                turn["f_hand"] = hand;
                at = Places(r, turn, waist);
            }
            foreach (var part in r.Parts)
            {
                float z = part.z;
                if (p.FarFront && r.Json["farFront"][part.name] != null) z = (float)r.Json["farFront"][part.name];
                Put(parts[part.name], at[part.name], turn[part.name], z);
            }
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? "n" : "f", id = p.Weapon[i];
                weapons[i].enabled = id != null;
                if (id == null) continue;
                var w = r.Json["weapons"][id];
                weapons[i].sprite = Picture((string)w["file"], new Vector2((float)w["gx"], (float)w["gy"]));
                float z = i == 1 && p.FarFront ? (float)r.Json["farFront"]["f_weapon"] : (float)r.Json["weaponZ"][s];
                Put(weapons[i], Grip(s), turn[s + "_hand"] + (float)g[s]["angle"] + 90, z);
            }
        }

        static Dictionary<string, Vector2> Places(Rig r, Dictionary<string, float> turn, Vector2 waist)
        {
            var at = new Dictionary<string, Vector2>();
            var jointOf = new Dictionary<string, string>();
            foreach (var p in r.Parts)
            {
                jointOf[p.name] = p.joint;
                at[p.name] = p.parent == null ? waist : at[p.parent] + Rot(r.J[p.joint] - r.J[jointOf[p.parent]], turn[p.parent]);
            }
            return at;
        }

        void Put(SpriteRenderer sr, Vector2 canvas, float deg, float z)
        {
            sr.transform.localPosition = new Vector3((canvas.x - Centre) * unit, (Ground - canvas.y) * unit, 0);
            sr.transform.localRotation = Quaternion.Euler(0, 0, -deg);
            sr.sortingOrder = Mathf.RoundToInt(z * 10);
        }

        void OnDestroy() => Anim.Clear(this);
    }

    /// <summary>The test's stage: the layered hero beside today's hero on a plain plate. Called from the editor.</summary>
    public static class LayeredHeroDemo
    {
        static GameObject root;
        static readonly List<(LayeredHero hero, string kind)> layered = new List<(LayeredHero, string)>();
        static readonly List<(Figure fig, string kind)> drawn = new List<(Figure, string)>();
        static readonly (string kind, string cls, string item)[] Kits =
        {
            ("sword", "warrior", "iron_sword"), ("staff", "mage", "oak_staff"), ("dagger", "rogue", "rusty_dagger"),
        };

        public static string Clear()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            layered.Clear();
            drawn.Clear();
            return "cleared";
        }

        static void Stage()
        {
            Clear();
            LayeredHero.Reload();
            root = new GameObject("layered hero test");
            root.transform.SetParent(GameRoot.I.Screen.World, false);
            var plate = new GameObject("plate").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(root.transform, false);
            plate.sprite = Art.White;
            plate.color = new Color(0.9f, 0.88f, 0.83f);
            plate.sortingOrder = 29000;
            plate.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
            plate.transform.localScale = new Vector3(Design.Width * 2, Design.Height * 2, 1);
        }

        /// <summary>Today's hero and the layered hero in the ready stance of each weapon, in pairs.</summary>
        public static string Compare(float y = 560, float size = 1)
        {
            Stage();
            for (int k = 0; k < Kits.Length; k++)
            {
                var (kind, cls, item) = Kits[k];
                var look = Db.Class(cls).Look;
                float x = (k + 0.5f) * Design.Width / Kits.Length;
                var fig = Figure.Create(root.transform, x - 95, y, look, 1, Db.Item(item));
                fig.Order = 30000;
                fig.SetPose(kind + "_ready");
                drawn.Add((fig, kind));
                var hero = LayeredHero.Create(root.transform, x + 95, y, (float)look.Scale * size);
                hero.Order = 30000;
                hero.Set(kind + "_ready");
                layered.Add((hero, kind));
            }
            return "compare";
        }

        /// <summary>One layered hero per entry "pose|chest|legs|helm" (empty = bare), in rows.</summary>
        public static string Grid(string cells, int cols, float top = 330, float rowHeight = 310, float size = 1)
        {
            Stage();
            var list = cells.Split(';');
            float scale = (float)Db.Class("warrior").Look.Scale * size;
            for (int k = 0; k < list.Length; k++)
            {
                var c = list[k].Split('|');
                var hero = LayeredHero.Create(root.transform, (k % cols + 0.5f) * Design.Width / cols, top + (k / cols) * rowHeight, scale);
                hero.Order = 30000;
                string[] slots = { "chest", "legs", "helm" };
                for (int s = 0; s < 3; s++)
                    if (c.Length > s + 1 && c[s + 1] != "") hero.Wear(slots[s], c[s + 1]);
                hero.Set(c[0]);
                layered.Add((hero, c[0].Split('_')[0]));
            }
            return list.Length + " heroes";
        }

        /// <summary>One blow from every hero on the stage: the layered ones move between their poses, today's swap pictures.</summary>
        public static string Swing(float lunge = 70)
        {
            foreach (var (hero, kind) in layered)
            {
                var h = hero;
                var k = kind;
                Lunge(h.transform, lunge);
                h.To(k + "_wind", 0.26f, Ease.CubicOut);
                Anim.After(0.34f, () => h.To(k + "_strike", 0.13f, Ease.QuadIn));
                Anim.After(0.80f, () => h.To(k + "_ready", 0.34f, Ease.CubicInOut));
            }
            foreach (var (fig, kind) in drawn)
            {
                var f = fig;
                var k = kind;
                Lunge(f.transform, lunge);
                f.SetPose(k + "_wind");
                Anim.After(0.34f, () => f.SetPose(k + "_step"));
                Anim.After(0.42f, () => f.SetPose(k + "_strike"));
                Anim.After(0.80f, () => f.SetPose(k + "_ready"));
            }
            return "swing";
        }

        static void Lunge(Transform t, float lunge)
        {
            float home = t.position.x, far = home + lunge / Design.PixelsPerUnit;
            void X(float x) { if (t != null) t.position = new Vector3(x, t.position.y, t.position.z); }
            Anim.Float(home, home - 0.12f, 0.26f, X, Ease.CubicOut);
            Anim.After(0.34f, () => Anim.Float(home - 0.12f, far, 0.13f, X, Ease.QuadIn));
            Anim.After(0.80f, () => Anim.Float(far, home, 0.34f, X, Ease.CubicInOut));
        }
    }
}
#endif
