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
    /// TEST, not part of the game (docs/PLANS.md "Layered hero rebuild", step 1): a layered hero that fights.
    /// Each key moment (ready, wind, strike, hurt) has its upper body drawn whole (torso, arms, hands), cut by
    /// scripts/hero_layers_fight.py into art/redo/layers/parts/fight (fight.json); the head is the rig's one head
    /// picture (so a helm is one picture) with a face patch per moment; the weapon lies between the body and the
    /// gripping fist; the legs are the rig's, solved from hip to ankle every frame so the feet stay planted, except
    /// in the lunge, which brings its own drawn legs. Gear is a picture per slot: helm, chest, gloves, legs, boots.
    /// Between moments the legs and the waist move smoothly and the upper body swaps part-way, leaning into the swap.
    /// Angles as in the rig script: clockwise on screen, 0 = forward, 90 = down, in rig canvas px.
    /// </summary>
    public class FightHero : MonoBehaviour
    {
        const float Ground = 1345, Centre = 760;
        static readonly string[] Legs = { "hips", "n_thigh", "n_shin", "n_foot", "f_thigh", "f_shin", "f_foot" };
        static readonly string[] Boots = { "n_shin", "n_foot", "f_shin", "f_foot" };
        static readonly string[] Upper = { "body", "fist", "chest", "hands", "gloves", "glove_fist" };
        static readonly string[] DrawnLegs = { "legs", "legs_set", "boots_set" };

        class Data
        {
            public JObject Rig, Fight;
            public float Scale;
            public string Dir;
            public Dictionary<string, Vector2> J = new Dictionary<string, Vector2>();
            public Dictionary<string, (string joint, float z)> Part = new Dictionary<string, (string, float)>();
            public Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        }

        /// <summary>Where the hero is between moments: whose pictures show, and where the waist and the feet are.</summary>
        public class Stance
        {
            public string Moment;
            /// <summary>The waist from its rest place, the ankles from theirs.</summary>
            public Vector2 Root, NFoot, FFoot;
            /// <summary>Sink: the hips lower than "the legs just reach". Lean: the hips' turn. Turn: the upper body's extra lean.</summary>
            public float Sink, Lean, NTurn, FTurn, Turn;
        }

        static Data data;
        readonly Dictionary<string, SpriteRenderer> layers = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, string> wear = new Dictionary<string, string>();
        readonly List<Material> plain = new List<Material>();
        Stance stance;
        float unit, busyUntil;
        public string Weapon = "iron_sword";
        /// <summary>The standing hero breathes: the hips rise and sink over planted feet.</summary>
        public bool Breathes = true;

        public static void Reload() => data = null;

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static Data Load()
        {
            if (data != null) return data;
            var d = new Data { Dir = LayeredHero.Folder };
            d.Rig = JObject.Parse(File.ReadAllText(Path.Combine(d.Dir, "rig.json")));
            d.Fight = JObject.Parse(File.ReadAllText(Path.Combine(d.Dir, "fight.json")));
            d.Scale = (float)d.Rig["scale"];
            foreach (var j in (JObject)d.Rig["joints"]) d.J[j.Key] = V(j.Value);
            foreach (var p in d.Rig["parts"]) d.Part[(string)p["name"]] = ((string)p["joint"], (float)p["z"]);
            return data = d;
        }

        /// <summary>A picture with its turning point, given in the rig canvas (the picture's corner is at x, y there).</summary>
        static Sprite Picture(JToken e, Vector2 pivot) =>
            Picture((string)e["file"], pivot - new Vector2((float)e["x"], (float)e["y"]));

        static Sprite Picture(string file, Vector2 pivot)
        {
            var d = Load();
            string key = file + "|" + pivot.x.ToString("0.#") + "|" + pivot.y.ToString("0.#");
            if (d.Sprites.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadImage(File.ReadAllBytes(Path.Combine(d.Dir, file)));
            Clean(tex);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(pivot.x / tex.width, 1 - pivot.y / tex.height),
                Design.PixelsPerUnit / d.Scale, 0, SpriteMeshType.FullRect);
            return d.Sprites[key] = s;
        }

        /// <summary>Cut pictures as the screen needs them: stray specks dropped, and the see-through pixels given
        /// their neighbours' colour, so smooth scaling does not pull a pale rim in from outside the shape.</summary>
        static void Clean(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var seen = new bool[px.Length];
            var stack = new Stack<int>();
            var island = new List<int>();
            for (int i = 0; i < px.Length; i++)
            {
                if (seen[i] || px[i].a < 12) continue;
                island.Clear();
                stack.Push(i);
                seen[i] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    island.Add(p);
                    int x = p % w, y = p / w;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int n = ny * w + nx;
                        if (seen[n] || px[n].a < 12) continue;
                        seen[n] = true;
                        stack.Push(n);
                    }
                }
                if (island.Count < 60) foreach (int p in island) px[p] = new Color32(0, 0, 0, 0);
            }
            for (int i = 0; i < px.Length; i++) if (px[i].a < 12) px[i] = new Color32(0, 0, 0, 0);
            var filled = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) filled[i] = px[i].a > 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var next = (bool[])filled.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (filled[i]) continue;
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                int j = ny * w + nx;
                                if (!filled[j]) continue;
                                r += px[j].r; g += px[j].g; b += px[j].b; n++;
                            }
                        if (n == 0) continue;
                        px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                        next[i] = true;
                    }
                filled = next;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        public static FightHero Create(Transform parent, float x, float y, float scale = 1)
        {
            var d = Load();
            var go = new GameObject("layered fighter");
            go.transform.SetParent(parent, false);
            go.transform.position = Design.World(x, y);
            go.transform.localScale = new Vector3(scale, scale, 1);
            go.AddComponent<SortingGroup>().sortingOrder = Design.Depth(y);
            var h = go.AddComponent<FightHero>();
            h.unit = d.Scale / Design.PixelsPerUnit;
            var names = new List<string>(Legs);
            names.AddRange(Upper);
            names.AddRange(DrawnLegs);
            names.AddRange(new[] { "head", "face", "weapon" });
            foreach (var n in names)
            {
                var sr = new GameObject(n).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(go.transform, false);
                h.layers[n] = sr;
                h.plain.Add(sr.sharedMaterial);
                // Its own property block: without one, layers sharing the flash material are drawn in one
                // batch with one picture (a white block instead of the silhouette).
                var block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                block.SetVector("_BendXf", Vector4.zero);
                sr.SetPropertyBlock(block);
            }
            var shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(go.transform, false);
            shadow.sprite = Art.Soft;
            shadow.color = new Color(0, 0, 0, 0.45f);
            shadow.sortingOrder = -100;
            shadow.transform.localScale = new Vector3(ShadowWidth / 64f, 26 / 64f, 1);
            h.shadow = shadow;
            h.Set("ready");
            return h;
        }

        /// <summary>The ground shadow's width, as the drawn hero's (Figure.AddShadow).</summary>
        public static float ShadowWidth = 150;
        SpriteRenderer shadow;

        public int Order { set => GetComponent<SortingGroup>().sortingOrder = value; }

        /// <summary>Gear per slot (helm, chest, gloves, legs, boots): the name of a cut set, or null for none.</summary>
        public void Wear(string slot, string set)
        {
            wear[slot] = set;
            if (stance != null) Apply(stance);
        }

        /// <summary>The whole set of fight.json ("set"), or nothing.</summary>
        public void WearSet(bool on)
        {
            foreach (var s in (JObject)Load().Fight["set"]) wear[s.Key] = on ? (string)s.Value : null;
            if (stance != null) Apply(stance);
        }

        string Worn(string slot) => wear.TryGetValue(slot, out var w) ? w : null;

        static bool Drawn(JToken moment) => moment["stand"]["drawn"] != null && (bool)moment["stand"]["drawn"];

        public static Stance Of(string moment)
        {
            var d = Load();
            var v = d.Fight["moments"][moment];
            if (v == null) throw new ArgumentException("no moment " + moment);
            var st = v["stand"];
            var root = V(st["root"]);
            bool drawn = Drawn(v);
            return new Stance
            {
                Moment = moment,
                // A drawn pair of legs keeps its picture's own height.
                Root = drawn ? new Vector2(root.x, (float)v["pivot"][1] - d.J["waist"].y + root.y) : root,
                Sink = drawn || st["sink"] == null ? 0 : (float)st["sink"],
                NFoot = st["nfoot"] != null ? V(st["nfoot"]) : Vector2.zero,
                FFoot = st["ffoot"] != null ? V(st["ffoot"]) : Vector2.zero,
                Lean = (float)v["lean"],
                NTurn = st["n_foot"] != null ? (float)st["n_foot"] : 0,
                FTurn = st["f_foot"] != null ? (float)st["f_foot"] : 0,
            };
        }

        public void Set(string moment) => Apply(stance = Of(moment));

        /// <summary>
        /// Move to another moment. The legs and the waist go there smoothly; the drawn upper body swaps `swap` of the way,
        /// the old one leaning to `before` degrees until then, the new one arriving at `after` degrees and settling.
        /// </summary>
        public Anim.Handle To(string moment, float seconds, Ease ease = Ease.CubicOut, float swap = 0.5f, float before = 0, float after = 0)
        {
            Stance a = stance, b = Of(moment);
            busyUntil = Time.time + seconds + 0.05f;
            return Anim.Float(0, 1, seconds, k => Apply(stance = Mix(a, b, k, swap, before, after)), ease, owner: this);
        }

        static Stance Mix(Stance a, Stance b, float k, float swap, float before, float after)
        {
            bool late = k >= swap;
            var s = new Stance
            {
                Moment = late ? b.Moment : a.Moment,
                Root = Vector2.LerpUnclamped(a.Root, b.Root, k), Sink = Mathf.LerpUnclamped(a.Sink, b.Sink, k),
                NFoot = Vector2.LerpUnclamped(a.NFoot, b.NFoot, k), FFoot = Vector2.LerpUnclamped(a.FFoot, b.FFoot, k),
                Lean = Mathf.LerpUnclamped(a.Lean, b.Lean, k), NTurn = Mathf.LerpUnclamped(a.NTurn, b.NTurn, k),
                FTurn = Mathf.LerpUnclamped(a.FTurn, b.FTurn, k),
                Turn = late ? Mathf.LerpUnclamped(after, b.Turn, swap >= 1 ? 1 : (k - swap) / (1 - swap))
                    : Mathf.LerpUnclamped(a.Turn, before, swap <= 0 ? 1 : k / swap),
            };
            // A foot that travels is lifted on the way.
            float up = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            s.NFoot.y -= up * Mathf.Min(45, 0.2f * (b.NFoot - a.NFoot).magnitude);
            s.FFoot.y -= up * Mathf.Min(45, 0.2f * (b.FFoot - a.FFoot).magnitude);
            return s;
        }

        void Update()
        {
            if (!Breathes || stance == null || stance.Moment != "ready" || Time.time < busyUntil) return;
            float k = 0.5f - 0.5f * Mathf.Cos((Time.time - busyUntil) * Mathf.PI * 2 / 1.9f);
            var s = Of("ready");
            s.Sink += 14 * k;
            s.Turn = 0.9f * k;
            Apply(stance = s);
        }

        /// <summary>Paint every picture one colour for a moment (a hit).</summary>
        public void Flash(float seconds = 0.09f)
        {
            int i = 0;
            foreach (var sr in layers.Values) sr.sharedMaterial = Fx.FillMaterial;
            Anim.After(seconds, () =>
            {
                if (this == null) return;
                foreach (var sr in layers.Values) sr.sharedMaterial = plain[i++];
            });
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

        void Apply(Stance s)
        {
            var d = Load();
            var j = d.J;
            var v = d.Fight["moments"][s.Moment];
            var f = (JObject)v["files"];
            var z = d.Fight["z"];
            var pivot = V(v["pivot"]);
            bool drawn = Drawn(v);
            var waist = j["waist"] + s.Root;
            foreach (var sr in layers.Values) sr.enabled = false;
            // The ground shadow stays under the feet: their middle, as wide as they stand apart.
            float feetMid = Centre, feetApart = 0;

            void Show(string name, Sprite sprite, Vector2 canvas, float deg, float depth)
            {
                var sr = layers[name];
                sr.enabled = true;
                sr.sprite = sprite;
                sr.transform.localPosition = new Vector3((canvas.x - Centre) * unit, (Ground - canvas.y) * unit, 0);
                sr.transform.localRotation = Quaternion.Euler(0, 0, -deg);
                sr.sortingOrder = Mathf.RoundToInt(depth * 10);
            }

            if (drawn)
            {
                waist.y += s.Sink;
                foreach (var key in DrawnLegs)
                {
                    string slot = key == "legs" ? null : key == "legs_set" ? "legs" : "boots";
                    if (f[key] != null && (slot == null || Worn(slot) != null)) Show(key, Picture(f[key], pivot), waist, s.Turn, (float)z[key]);
                }
                var feet = layers["legs"];
                if (feet.enabled)
                {
                    feetMid = Centre + (feet.transform.localPosition.x + feet.sprite.bounds.center.x) / unit;
                    feetApart = feet.sprite.bounds.size.x / unit;
                }
            }
            else
            {
                Vector2 Hip(string side) => waist + Rot(j[side + "_hip"] - j["waist"], s.Lean);
                Vector2 Ankle(string side) => j[side + "_ankle"] + (side == "n" ? s.NFoot : s.FFoot);
                float Reach(string side) => (j[side + "_knee"] - j[side + "_hip"]).magnitude + (j[side + "_ankle"] - j[side + "_knee"]).magnitude;
                // The hips drop until both legs reach their ankles, then sink a little more.
                for (int i = 0; i < 200; i++)
                {
                    if ((Hip("n") - Ankle("n")).magnitude <= 0.985f * Reach("n") && (Hip("f") - Ankle("f")).magnitude <= 0.985f * Reach("f")) break;
                    waist.y += 2;
                }
                waist.y += s.Sink;
                string legs = Worn("legs") ?? "bare", boots = Worn("boots");
                Show("hips", Picture(d.Fight["hips"][legs], j["waist"]), waist, s.Lean, d.Part["hips"].z);
                foreach (var side in new[] { "n", "f" })
                {
                    Vector2 k = j[side + "_knee"] - j[side + "_hip"], a = j[side + "_ankle"] - j[side + "_knee"];
                    var hip = Hip(side);
                    var (d1, d2) = Ik(hip, Ankle(side), k.magnitude, a.magnitude, -1);
                    float t1 = Norm(d1 - Ang(k)), t2 = Norm(d2 - Ang(a));
                    var knee = hip + Rot(k, t1);
                    var ankle = knee + Rot(a, t2);
                    void Leg(string part, Vector2 at, float turn)
                    {
                        string set = boots != null && Array.IndexOf(Boots, part) >= 0 ? boots : legs;
                        Show(part, Picture(d.Rig["sets"][set][part], j[d.Part[part].joint]), at, turn, d.Part[part].z);
                    }
                    Leg(side + "_thigh", hip, t1);
                    Leg(side + "_shin", knee, t2);
                    Leg(side + "_foot", ankle, side == "n" ? s.NTurn : s.FTurn);
                }
                feetMid = (Ankle("n").x + Ankle("f").x) / 2 + 25;
                feetApart = Mathf.Abs(Ankle("n").x - Ankle("f").x) + 150;
            }

            void Body(string key)
            {
                if (f[key] != null) Show(key, Picture(f[key], pivot), waist, s.Turn, (float)z[key]);
            }
            bool chest = Worn("chest") != null && f["chest"] != null, gloves = Worn("gloves") != null && f["gloves"] != null;
            if (!chest)
            {
                Body("body");
                Body("fist");
            }
            else
            {
                Body("chest");
                if (!gloves)
                {
                    Body("hands");
                    Body("fist");
                }
            }
            if (gloves)
            {
                Body("gloves");
                Body("glove_fist");
            }

            var neck = waist + Rot(V(v["neck"]) - pivot, s.Turn);
            float nod = (float)v["head"] + s.Turn;
            Show("head", Picture(d.Rig["sets"][Worn("helm") ?? "bare"]["head"], j["neck"]), neck, nod, (float)z["head"]);
            if (v["face"].Type != JTokenType.Null)
                Show("face", Picture(d.Fight["faces"][(string)v["face"]], j["neck"]), neck, nod, (float)z["face"]);
            if (Weapon != null)
            {
                var w = d.Rig["weapons"][Weapon];
                var grip = waist + Rot(V(v["grip"]) - pivot, s.Turn);
                Show("weapon", Picture((string)w["file"], new Vector2((float)w["gx"], (float)w["gy"])), grip,
                    (float)v["blade"] + 90 + s.Turn, (float)v["weaponZ"]);
            }
            if (shadow != null)
            {
                shadow.transform.localPosition = new Vector3((feetMid - Centre) * unit, 0, 0);
                float wide = Mathf.Max(ShadowWidth, feetApart * unit * Design.PixelsPerUnit * 0.95f);
                shadow.transform.localScale = new Vector3(wide / 64f, 26 / 64f, 1);
            }
        }

        void OnDestroy() => Anim.Clear(this);
    }

    /// <summary>
    /// The test's stage: today's drawn hero on the left, the layered one on the right, bare above and in a full set
    /// below. Called from the editor (Unity MCP): Stage, Hold(moment), Blow, Hit, Play.
    /// </summary>
    public static class LayeredFightDemo
    {
        static GameObject root;
        static readonly List<FightHero> layered = new List<FightHero>();
        static readonly List<Figure> drawn = new List<Figure>();
        static readonly Dictionary<Transform, Vector3> home = new Dictionary<Transform, Vector3>();

        public static string Clear()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            layered.Clear();
            drawn.Clear();
            home.Clear();
            return "cleared";
        }

        /// <summary>Four heroes: rows bare and dressed (drawn outfit `outfit`), columns drawn and layered (`layeredSize` makes both as tall).</summary>
        public static string Stage(float size = 0.92f, float left = 250, float right = 800, float top = 348, float bottom = 704, string outfit = "scale_mail", float layeredSize = 0.95f)
        {
            Clear();
            LayeredHero.Reload();
            FightHero.Reload();
            root = new GameObject("layered fight test");
            root.transform.SetParent(GameRoot.I.Screen.World, false);
            var plate = new GameObject("plate").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(root.transform, false);
            plate.sprite = Art.White;
            plate.color = new Color(0.9f, 0.88f, 0.83f);
            plate.sortingOrder = 29000;
            plate.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
            plate.transform.localScale = new Vector3(Design.Width * 2, Design.Height * 2, 1);
            var bare = Db.Class("warrior").Look;
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? top : bottom;
                var look = bare;
                if (row == 1)
                {
                    look = bare.Clone();
                    var b = look.Bare[0];
                    look.Bare = new List<BareLook> { new BareLook { Art = "char_hero_" + outfit, Hands = b.Hands, Poses = b.Poses } };
                }
                var fig = Figure.Create(root.transform, left, y, look, 1, Db.Item("iron_sword"));
                fig.transform.localScale *= size;
                fig.Order = 30000;
                fig.EnableBend(); // as in battle; without it the hit flash draws the body with the weapon's picture
                fig.SetPose("sword_ready");
                drawn.Add(fig);
                home[fig.transform] = fig.transform.position;
                var hero = FightHero.Create(root.transform, right, y, (float)bare.Scale * size * layeredSize);
                hero.Order = 30000;
                hero.WearSet(row == 1);
                layered.Add(hero);
                home[hero.transform] = hero.transform.position;
            }
            return "stage";
        }

        static void Still(Transform t)
        {
            t.position = home[t];
            t.localRotation = Quaternion.identity;
        }

        /// <summary>Every hero frozen in one moment (ready, wind, strike, hurt), for a still sheet.</summary>
        public static string Hold(string moment)
        {
            foreach (var h in layered)
            {
                Anim.Clear(h);
                Still(h.transform);
                h.Breathes = false;
                h.Set(moment);
            }
            foreach (var f in drawn)
            {
                Still(f.transform);
                f.SetPose("sword_" + (moment == "hurt" ? "ready" : moment));
                // Today's hero has no hurt picture: it is pushed back and tilted (BattleScreen).
                if (moment == "hurt")
                {
                    f.transform.position += new Vector3(-0.4f, 0, 0);
                    f.transform.localRotation = Quaternion.Euler(0, 0, 10);
                }
            }
            return moment;
        }

        static void X(Transform t, float from, float to, float seconds, Ease ease, float delay = 0)
        {
            float x0 = home[t].x;
            Anim.Float(from, to, seconds, v => { if (t != null) t.position = new Vector3(x0 + v / Design.PixelsPerUnit, t.position.y, t.position.z); }, ease, delay);
        }

        /// <summary>One blow from every hero: today's swaps its pictures, the layered one moves between its moments.</summary>
        public static string Blow(float lunge = 70, float layeredLunge = 30)
        {
            foreach (var hero in layered)
            {
                var h = hero;
                h.Breathes = true;
                X(h.transform, 0, -6, 0.26f, Ease.CubicOut);
                h.To("wind", 0.26f, Ease.CubicOut, 0.35f, -5, 5);
                Anim.After(0.34f, () =>
                {
                    X(h.transform, -6, layeredLunge, 0.13f, Ease.QuadIn);
                    h.To("strike", 0.13f, Ease.QuadIn, 0.5f, 10, -8);
                });
                Anim.After(0.80f, () =>
                {
                    X(h.transform, layeredLunge, 0, 0.34f, Ease.CubicInOut);
                    h.To("ready", 0.34f, Ease.CubicInOut, 0.35f, -5, 9);
                });
            }
            foreach (var fig in drawn)
            {
                var f = fig;
                X(f.transform, 0, -12, 0.26f, Ease.CubicOut);
                f.SetPose("sword_wind");
                Anim.After(0.34f, () =>
                {
                    X(f.transform, -12, lunge, 0.13f, Ease.QuadIn);
                    f.SetPose("sword_step");
                });
                Anim.After(0.42f, () => f.SetPose("sword_strike"));
                Anim.After(0.80f, () =>
                {
                    X(f.transform, lunge, 0, 0.34f, Ease.CubicInOut);
                    f.SetPose("sword_ready");
                });
            }
            return "blow";
        }

        /// <summary>Every hero takes a hit: today's is pushed back, tilted and flashes; the layered one also recoils in its hurt picture.</summary>
        public static string Hit(float push = 40, float tilt = 10)
        {
            foreach (var fig in drawn)
            {
                var f = fig;
                Fx.WhiteFlash(f, 90);
                X(f.transform, 0, -push, 0.06f, Ease.CubicOut);
                Anim.Float(0, tilt, 0.06f, a => { if (f != null) f.transform.localRotation = Quaternion.Euler(0, 0, a); }, Ease.CubicOut);
                Anim.After(0.11f, () =>
                {
                    X(f.transform, -push, 0, 0.3f, Ease.SineInOut);
                    Anim.Float(tilt, 0, 0.3f, a => { if (f != null) f.transform.localRotation = Quaternion.Euler(0, 0, a); }, Ease.SineInOut);
                });
            }
            foreach (var hero in layered)
            {
                var h = hero;
                h.Flash();
                X(h.transform, 0, -push, 0.06f, Ease.CubicOut);
                h.To("hurt", 0.09f, Ease.CubicOut, 0, 0, 7);
                Anim.After(0.34f, () =>
                {
                    X(h.transform, -push, 0, 0.32f, Ease.SineInOut);
                    h.To("ready", 0.32f, Ease.SineInOut, 0.4f, 4, -7);
                });
            }
            return "hit";
        }

        /// <summary>The clip: stand, a blow, stand, a hit, stand (about `seconds` long).</summary>
        public static string Play(float blowAt = 0.7f, float hitAt = 2.5f)
        {
            foreach (var h in layered) h.Breathes = true;
            Anim.After(blowAt, () => Blow());
            Anim.After(hitAt, () => Hit());
            return "play";
        }
    }
}
#endif
