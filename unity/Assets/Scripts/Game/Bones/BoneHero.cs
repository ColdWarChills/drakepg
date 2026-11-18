#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg.Bones
{
    /// <summary>
    /// The hero on bones, played from data (docs/PLANS.md "the bone system"; the format in unity/Bones/HOWTO.md):
    /// stiff part pictures on joints from rig.json, a draw order, legs and arms that reach for a point. A pose is
    /// solved in rig canvas px (Solve, pure) and then put on the pictures (Apply). Moves are files (BoneMove) played on
    /// the game's Anim clock, so x2 and the hit pause reach them. Nothing in the game uses this yet.
    ///
    /// Objects (the judging kit reads them by name): "puppet" (squash, root turn, facing) holds a picture per part
    /// named as the part, each sitting on its joint (j_&lt;joint&gt; = that part's position); "near weapon" / "far
    /// weapon" turn on their own grip point; "n_hand_grip" / "f_hand_grip" hang on the hands where the hand picture
    /// holds (from the picture's own grip data, not from the weapon); "face" hangs on the head.
    /// </summary>
    public class BoneHero : MonoBehaviour
    {
        static readonly string[] Sides = { "n", "f" };

        public BoneRig Rig { get; private set; }
        public IBoneSource Source { get; private set; }
        /// <summary>Where it stands (design px, feet). Travel goes from here toward the target.</summary>
        public Vector2 Home;
        /// <summary>The target given to Play (design px, the monster's feet); null: Home + 620 px forward.</summary>
        public Vector2? Target;
        /// <summary>Sets of part pictures that replace bare ones (swaps.json "sets", e.g. "bare_v2"), later ones win.</summary>
        public readonly List<string> BaseSets = new List<string>();
        /// <summary>Weapons in hand (rig weapon ids), [0] near, [1] far; null: the move's own.</summary>
        public readonly string[] Held = new string[2];
        /// <summary>
        /// The sword hand (HOWTO "Grip hands"): false = the old hand exactly (one fist, turned with the blade, hung on
        /// the forearm's end). True = while the near hand holds a weapon with its blade keyed and no hand: event says
        /// otherwise, the grip hand drawn for the blade-to-forearm angle shows (swaps.json swaps with a "relation", or
        /// the rig's own fist), pinned to the weapon by its own grip; the arm then reaches for that hand's wrist, its
        /// elbow folding at most FoldCap degrees more than before (the rest of the way the forearm's end slides under
        /// the fist). The weapon's grip and blade are the same either way.
        /// </summary>
        public bool NewHands;
        /// <summary>How many degrees more the near elbow may fold to reach a grip hand's wrist (NewHands).</summary>
        public float FoldCap = 30;
        readonly string[] eventHand = new string[2];

        Transform puppet;
        readonly Dictionary<string, SpriteRenderer> parts = new Dictionary<string, SpriteRenderer>();
        readonly SpriteRenderer[] weapons = new SpriteRenderer[2];
        readonly Transform[] handGrips = new Transform[2];
        SpriteRenderer face;
        BoneTrail trail;
        readonly Dictionary<string, string> worn = new Dictionary<string, string>();

        // slots now shown (swap names, face, order, weapons)
        string torsoSwap, faceNow, orderNow;
        readonly string[] handSwap = new string[2], weaponNow = new string[2];
        bool dressed;

        BoneMove playing;
        Anim.Handle play;
        float playedTo = -1;
        IBoneEvents handler;
        BoneFrame lastFrame;

        public float Unit => Rig.Unit;
        public int Facing => lastFrame != null && lastFrame.Facing < 0 ? -1 : 1;
        public BoneFrame Frame => lastFrame;
        public bool Playing => play != null && !play.IsDone;
        public BoneMove Move => playing;
        public float MoveTime => playedTo;

        /// <summary>Where travel = 1 brings the hero: the move's reach short of the target.</summary>
        public Vector2 TargetSpot
        {
            get
            {
                var t = Target ?? Home + new Vector2(620, 0);
                float reach = playing != null ? playing.Reach : 170;
                return new Vector2(t.x - reach * Mathf.Sign(t.x - Home.x == 0 ? 1 : t.x - Home.x), Home.y);
            }
        }

        // --- building ---------------------------------------------------------------------------------------------

        /// <summary>A hero at (x, y) design px (feet), `scale` x the rig's size, in the rest layout.</summary>
        public static BoneHero Create(Transform parent, IBoneSource source, float x, float y, float scale = 1)
        {
            var rig = BoneRig.Of(source);
            var go = new GameObject("bone hero");
            go.transform.SetParent(parent, false);
            go.transform.position = Design.World(x, y);
            go.transform.localScale = new Vector3(scale, scale, 1);
            go.AddComponent<SortingGroup>().sortingOrder = Design.Depth(y);
            var h = go.AddComponent<BoneHero>();
            h.Rig = rig;
            h.Source = source;
            h.Home = new Vector2(x, y);
            h.puppet = new GameObject("puppet").transform;
            h.puppet.SetParent(go.transform, false);
            foreach (var p in rig.Parts)
            {
                var sr = new GameObject(p.Name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(h.puppet, false);
                h.parts[p.Name] = sr;
            }
            for (int i = 0; i < 2; i++)
            {
                h.weapons[i] = new GameObject(i == 0 ? "near weapon" : "far weapon").AddComponent<SpriteRenderer>();
                h.weapons[i].transform.SetParent(h.puppet, false);
                h.weapons[i].enabled = false;
                h.handGrips[i] = new GameObject(Sides[i] + "_hand_grip").transform;
                h.handGrips[i].SetParent(h.parts[Sides[i] + "_hand"].transform, false);
            }
            h.face = new GameObject("face").AddComponent<SpriteRenderer>();
            h.face.transform.SetParent(h.parts["head"].transform, false);
            h.face.enabled = false;
            h.Rest();
            return h;
        }

        public int Order { get => GetComponent<SortingGroup>().sortingOrder; set => GetComponent<SortingGroup>().sortingOrder = value; }

        /// <summary>Re-read rig, faces and swaps if their files changed, and redraw the pictures.</summary>
        public void Reload()
        {
            Rig = BoneRig.Of(Source);
            dressed = false;
        }

        /// <summary>Gear on a slot (chest, legs, helm, boots): a gear id with a part set in rig.json, or null for none.</summary>
        public void Wear(string slot, string gear)
        {
            if (slot == "body") slot = "chest";
            if (!Rig.Slots.ContainsKey(slot))
            {
                Debug.LogWarning($"bones: no gear slot '{slot}' on the rig (rig.json slots: {string.Join(", ", Rig.Slots.Keys)})");
                return;
            }
            if (gear != null && !Rig.HasSet(gear))
            {
                Debug.LogWarning($"bones: no part pictures for '{gear}' (rig.json sets)");
                return;
            }
            worn[slot] = gear;
            dressed = false;
            if (lastFrame != null) Redraw();
        }

        public string Worn(string slot) => worn.TryGetValue(slot, out var g) ? g : null;

        /// <summary>Weapons in hand (rig weapon ids, null for none).</summary>
        public void Hold(string near, string far = null)
        {
            Held[0] = near;
            Held[1] = far;
            if (lastFrame != null) Redraw();
        }

        // which gear set draws a part: boots over legs over chest over helm, else the base sets, else bare
        static readonly string[] SlotOrder = { "boots", "legs", "chest", "helm" };

        string SetFor(string part)
        {
            foreach (var slot in SlotOrder)
            {
                var g = Worn(slot);
                if (g != null && Rig.Slots.TryGetValue(slot, out var list) && list.Contains(part) && Rig.SetPicture(g, part) != null) return g;
            }
            for (int i = BaseSets.Count - 1; i >= 0; i--)
                if (Rig.SetPicture(BaseSets[i], part) != null) return BaseSets[i];
            return "bare";
        }

        BoneRig.Swap SwapOn(string part)
        {
            if (part == "torso" && torsoSwap != null) return Rig.SwapPics.TryGetValue(torsoSwap, out var s) ? s : null;
            for (int i = 0; i < 2; i++)
                if (part == Sides[i] + "_hand" && handSwap[i] != null) return Rig.SwapPics.TryGetValue(handSwap[i], out var s) ? s : null;
            return null;
        }

        /// <summary>Where `joint` is in the picture `part` shows now (a swap brings its own joints), rest layout px.</summary>
        public Vector2 JointIn(string part, string joint)
        {
            var s = SwapOn(part);
            if (s != null && s.Joints.TryGetValue(joint, out var at)) return at;
            return Rig.J[joint];
        }

        /// <summary>The grip point and angle of the hand picture shown now on side i (a swap hand's own, else the rig's).</summary>
        public (Vector2 at, float angle) GripOf(int i)
        {
            var s = SwapOn(Sides[i] + "_hand");
            if (s != null && s.GripAt.HasValue) return (s.GripAt.Value, s.GripAngle);
            return Rig.Grip(Sides[i]);
        }

        void Dress()
        {
            foreach (var p in Rig.Parts)
            {
                var sr = parts[p.Name];
                var s = SwapOn(p.Name);
                sr.sprite = s != null
                    ? Rig.Picture(s.File, JointIn(p.Name, p.Joint) - s.Corner)
                    : Rig.Picture(Rig.SetPicture(SetFor(p.Name), p.Name) ?? Rig.SetPicture("bare", p.Name), Rig.J[p.Joint]);
            }
            if (faceNow != null)
            {
                var e = Rig.Face(faceNow, Worn("helm"));
                if (e == null) Debug.LogWarning("bones: no face '" + faceNow + "'");
                face.sprite = Rig.Picture(e, JointIn("head", "neck"));
                face.enabled = face.sprite != null;
                face.sortingOrder = Mathf.RoundToInt(Z("head") * 10) + 10;
            }
            else face.enabled = false;
            for (int i = 0; i < 2; i++)
            {
                var w = Rig.Weapon(weaponNow[i]);
                weapons[i].enabled = w != null;
                if (w != null) weapons[i].sprite = Rig.Picture((string)w["file"], new Vector2((float)w["gx"], (float)w["gy"]));
                var (gat, _) = GripOf(i);
                var wrist = JointIn(Sides[i] + "_hand", Sides[i] + "_wrist");
                handGrips[i].localPosition = new Vector3((gat.x - wrist.x) * Unit, -(gat.y - wrist.y) * Unit, 0);
            }
            dressed = true;
        }

        /// <summary>Show the slots (swaps, face, order, weapons) for this moment; redress only on a change.</summary>
        void Slots(string torso, string nHand, string fHand, string faceName, string order, string near, string far)
        {
            if (!dressed || torso != torsoSwap || nHand != handSwap[0] || fHand != handSwap[1] || faceName != faceNow || near != weaponNow[0] || far != weaponNow[1])
            {
                torsoSwap = torso;
                handSwap[0] = nHand;
                handSwap[1] = fHand;
                faceNow = faceName;
                weaponNow[0] = near;
                weaponNow[1] = far;
                Dress();
            }
            orderNow = order;
        }

        float Z(string part)
        {
            float z = part == "n_weapon" ? (SwapOn("n_hand")?.WeaponZ ?? Rig.WeaponZ("n"))
                : part == "f_weapon" ? (SwapOn("f_hand")?.WeaponZ ?? Rig.WeaponZ("f")) : Rig.PartOf[part].Z;
            if (orderNow == null || orderNow == "rest") return z;
            if (orderNow == "far_front") return Rig.FarFront(part) ?? z;
            if (playing != null && playing.Orders.TryGetValue(orderNow, out var map) && map.TryGetValue(part, out var oz)) return oz;
            return z;
        }

        // --- solving ----------------------------------------------------------------------------------------------

        /// <summary>A solved pose: where each part's joint is (canvas px), its turn, scale and shade; the weapons.</summary>
        public sealed class BoneFrame
        {
            public Vector2 Waist;
            public readonly Dictionary<string, Vector2> At = new Dictionary<string, Vector2>();
            public readonly Dictionary<string, float> Turn = new Dictionary<string, float>();
            public readonly Dictionary<string, Vector2> Scale = new Dictionary<string, Vector2>();
            public readonly Dictionary<string, float> Shade = new Dictionary<string, float>();
            public readonly Vector2[] Grip = new Vector2[2];
            /// <summary>The weapon's direction on screen (deg, 0 = forward): where the blade points.</summary>
            public readonly float[] Blade = new float[2];
            public readonly bool[] Armed = new bool[2];
            /// <summary>NewHands: the grip hand chosen (a swap name, null = the rig's fist), whether it was chosen
            /// here, the blade-to-forearm angle it was chosen by, and how far the forearm's end is from that hand's
            /// wrist (canvas px; hidden under the fist when the elbow may not fold enough).</summary>
            public readonly string[] HandPic = new string[2];
            public readonly bool[] HandAuto = new bool[2];
            public readonly float[] Relation = new float[2];
            public readonly float[] WristGap = new float[2];
            public Vector2 Squash = Vector2.one;
            public float RootTurn, Facing = 1, Travel;
        }

        static Vector2 Rot(Vector2 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float Ang(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        static float Norm(float a) => Mathf.Repeat(a + 180, 360) - 180;

        /// <summary>
        /// Two bones from a to the target: the direction of each. `bend` is the side the middle joint goes to (1 or -1).
        /// In between, both turns are blended from the two solutions (the upper bone through the line to the target, the
        /// lower one mirrored), so the joint moves smoothly through the straight arm and never flips; while |bend| &lt; 1
        /// the end reaches a little past the target. Key the bend's change short and on a fast moment.
        /// </summary>
        static void Ik(Vector2 a, Vector2 target, float l1, float l2, float bend, out float d1, out float d2)
        {
            var dv = target - a;
            float d = Mathf.Max(Mathf.Abs(l1 - l2) + 0.5f, Mathf.Min(dv.magnitude, l1 + l2 - 0.5f));
            float b = Mathf.Atan2(dv.y, dv.x);
            float inner = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2 * l1 * d), -1, 1));
            // the solution on the +1 side: its lower bone's angle off the line, gamma
            var mid = a + l1 * new Vector2(Mathf.Cos(b + inner), Mathf.Sin(b + inner));
            var end = a + d * new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            float bd = b * Mathf.Rad2Deg;
            float gamma = Norm(bd - Ang(end - mid));
            float k = Mathf.Clamp(bend, -1, 1);
            d1 = bd + k * inner * Mathf.Rad2Deg;
            d2 = bd - k * gamma;
        }

        Vector2 Place(BoneFrame f, BonePose p, string part)
        {
            var info = Rig.PartOf[part];
            if (info.Parent == null) return f.Waist;
            var parent = Rig.PartOf[info.Parent];
            var off = JointIn(parent.Name, info.Joint) - JointIn(parent.Name, parent.Joint);
            off = Vector2.Scale(off, f.Scale.TryGetValue(parent.Name, out var s) ? s : Vector2.one) + p.V("slide." + info.Joint);
            return f.At[parent.Name] + Rot(off, f.Turn[parent.Name]);
        }

        void Put(BoneFrame f, BonePose p, string part, float turn)
        {
            f.Turn[part] = turn;
            f.At[part] = Place(f, p, part);
        }

        /// <summary>Solve a pose (pure: touches no picture). Weapons per side: rig ids or null.</summary>
        public BoneFrame Solve(BonePose p, string near, string far)
        {
            var f = new BoneFrame { Squash = p.V("squash"), RootTurn = p.F("rootTurn"), Facing = p.F("facing"), Travel = p.F("travel") };
            var J = Rig.J;
            foreach (var part in Rig.Parts)
            {
                f.Scale[part.Name] = p.V("scale." + part.Name);
                f.Shade[part.Name] = p.F("shade." + part.Name);
            }
            float plant = p.F("plant");
            var root = p.V("root");
            f.Waist = J["waist"] + root;
            Put(f, p, "hips", p.F("hips"));
            Put(f, p, "torso", p.F("torso"));
            Put(f, p, "head", p.F("head"));
            foreach (var s in Sides)
            {
                // Legs: the ankle stays where the foot is planted (rest layout + foot), or rides with the root (plant 0).
                var hip = Place(f, p, s + "_thigh");
                var ankle = J[s + "_ankle"] + p.V(s + ".foot") + (1 - plant) * root;
                float l1 = (J[s + "_knee"] - J[s + "_hip"]).magnitude, l2 = (J[s + "_ankle"] - J[s + "_knee"]).magnitude;
                Ik(hip, ankle, l1, l2, p.F(s + ".knee"), out var d1, out var d2);
                Ik(J[s + "_hip"], J[s + "_ankle"], l1, l2, -1, out var r1, out var r2);
                Put(f, p, s + "_thigh", Norm(d1 - r1));
                Put(f, p, s + "_shin", Norm(d2 - r2));
                Put(f, p, s + "_foot", p.F(s + ".toe"));
            }
            string[] held = { near, far };
            for (int i = 0; i < 2; i++)
            {
                string s = Sides[i];
                bool armed = held[i] != null && Rig.Weapon(held[i]) != null;
                var shoulder = Place(f, p, s + "_upper");
                var elbowJ = J[s + "_elbow"];
                // NewHands: the first pass is always the old fist's (so the grip, the blade and the hand chosen never
                // depend on the picture shown now); GripHand then swaps the hand and re-aims the arm.
                bool auto = NewHands && i == 0 && eventHand[i] == null;
                var wristJ = auto ? J[s + "_wrist"] : JointIn(s + "_hand", s + "_wrist");
                Vector2 up = elbowJ - J[s + "_shoulder"], fore = J[s + "_wrist"] - elbowJ;
                float upper = p.F(s + ".upper"), foreTurn = p.F(s + ".fore"), ik = Mathf.Clamp01(p.F(s + ".ik"));
                float upperFK = upper, foreFK = foreTurn;
                bool targeted = false;
                var (gat, gang) = auto ? Rig.Grip(s) : GripOf(i);
                float inhand = p.F(s + ".inhand");
                bool bladeKeyed = armed && p.Has(s + ".blade");
                float handTurn = bladeKeyed ? p.F(s + ".blade") - gang - inhand : 0;
                var handScale = f.Scale[s + "_hand"];
                bool aimed = p.Has(s + ".aim");
                if (ik > 0 && (aimed || p.Has(s + ".hand")))
                {
                    targeted = true;
                    float aim = p.F(s + ".aim") * Mathf.Deg2Rad;
                    var target = aimed ? shoulder + p.F(s + ".reach") * new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)) : f.Waist + p.V(s + ".hand");
                    var wristTarget = bladeKeyed ? target - Rot(Vector2.Scale(gat - wristJ, handScale), handTurn) : target;
                    Ik(shoulder, wristTarget, up.magnitude, fore.magnitude, p.F(s + ".bend"), out var a1, out var a2);
                    upper = Mathf.LerpAngle(upper, Norm(a1 - Ang(up)), ik);
                    foreTurn = Mathf.LerpAngle(foreTurn, Norm(a2 - Ang(fore)), ik);
                }
                if (!bladeKeyed) handTurn = foreTurn + p.F(s + ".wrist");
                // Two hands on one weapon: the far fist on the near weapon's shaft, `onT` px along it (old Pose.On).
                float on = i == 1 ? Mathf.Clamp01(p.F("f.on")) : 0;
                if (on > 0 && f.Armed[0])
                {
                    var (fat, fang) = GripOf(1);
                    float wdir = f.Blade[0];
                    var hold = f.Grip[0] + Rot(new Vector2(p.F("f.onT"), 0), wdir);
                    float hand = Norm(wdir - fang);
                    if (Mathf.Abs(hand) > 90) hand = Norm(hand + 180);
                    var target = hold - Rot(Vector2.Scale(fat - wristJ, handScale), hand);
                    if (ik >= 0.999f)
                    {
                        // Blend where the wrist goes, then solve once: blending the two arms' angles flips the arm
                        // the short way round when they are about 180 degrees apart.
                        float ru = (Ang(up) + upper) * Mathf.Deg2Rad, rf = (Ang(fore) + foreTurn) * Mathf.Deg2Rad;
                        var free = shoulder + up.magnitude * new Vector2(Mathf.Cos(ru), Mathf.Sin(ru))
                                            + fore.magnitude * new Vector2(Mathf.Cos(rf), Mathf.Sin(rf));
                        Ik(shoulder, Vector2.Lerp(free, target, on), up.magnitude, fore.magnitude, p.F("f.bend"), out var b1, out var b2);
                        upper = Norm(b1 - Ang(up));
                        foreTurn = Norm(b2 - Ang(fore));
                    }
                    else
                    {
                        Ik(shoulder, target, up.magnitude, fore.magnitude, p.F("f.bend"), out var a1, out var a2);
                        upper = Mathf.LerpAngle(upper, Norm(a1 - Ang(up)), on);
                        foreTurn = Mathf.LerpAngle(foreTurn, Norm(a2 - Ang(fore)), on);
                    }
                    handTurn = Mathf.LerpAngle(handTurn, hand, on);
                }
                Put(f, p, s + "_upper", upper);
                Put(f, p, s + "_fore", foreTurn);
                Put(f, p, s + "_hand", handTurn);
                f.Armed[i] = armed;
                if (armed)
                {
                    f.Grip[i] = f.At[s + "_hand"] + Rot(Vector2.Scale(gat - wristJ, handScale), handTurn);
                    f.Blade[i] = handTurn + gang + inhand;
                }
                if (auto && armed && bladeKeyed && on <= 0)
                    GripHand(f, p, i, shoulder, up, fore, upperFK, foreFK, targeted ? ik : 0, inhand, handScale, gat - wristJ, gang);
            }
            return f;
        }

        /// <summary>
        /// NewHands, after the first pass (the old fist): pick the grip hand drawn nearest to this blade-to-forearm angle
        /// (the rig's fist or a swap with a "relation"), pin it to the weapon by its own grip (so the grip and blade stay
        /// exactly the first pass's), and aim the arm at its wrist. The arm's aim blends between the two nearest hands'
        /// wrists over Rig.GripBand degrees round the angle where the picture changes (no jump of the arm), and the elbow
        /// folds at most FoldCap more than in the first pass; what is left, the forearm's end slides under the fist.
        /// </summary>
        void GripHand(BoneFrame f, BonePose p, int i, Vector2 shoulder, Vector2 up, Vector2 fore, float upperFK, float foreFK,
            float ik, float inhand, Vector2 handScale, Vector2 oldV, float oldAngle)
        {
            string s = Sides[i];
            float blade = f.Blade[i];
            var grip = f.Grip[i];
            float rel = Norm(Ang(fore) + f.Turn[s + "_fore"] - blade);
            var names = new List<string> { null };
            var rels = new List<float> { Norm(Ang(fore) - oldAngle) };
            var vs = new List<Vector2> { oldV };
            var angles = new List<float> { oldAngle };
            foreach (var kv in Rig.SwapPics)
            {
                var sw = kv.Value;
                if (sw.Part != s + "_hand" || !sw.Relation.HasValue || !sw.GripAt.HasValue || !sw.Joints.TryGetValue(s + "_wrist", out var w)) continue;
                names.Add(kv.Key);
                rels.Add(sw.Relation.Value);
                vs.Add(sw.GripAt.Value - w);
                angles.Add(sw.GripAngle);
            }
            int k1 = 0, k2 = -1;
            for (int k = 1; k < names.Count; k++)
                if (Mathf.Abs(Norm(rel - rels[k])) < Mathf.Abs(Norm(rel - rels[k1]))) k1 = k;
            for (int k = 0; k < names.Count; k++)
                if (k != k1 && (k2 < 0 || Mathf.Abs(Norm(rel - rels[k])) < Mathf.Abs(Norm(rel - rels[k2])))) k2 = k;
            float t = k2 < 0 ? 0 : Mathf.Clamp01(0.5f - (Mathf.Abs(Norm(rel - rels[k2])) - Mathf.Abs(Norm(rel - rels[k1]))) / (2 * Mathf.Max(1, Rig.GripBand)));
            f.HandPic[i] = names[k1];
            f.HandAuto[i] = true;
            f.Relation[i] = rel;
            f.WristGap[i] = 0;
            if (k1 == 0 && t <= 0) return; // the old fist, as before
            Vector2 Off(int k) => Rot(Vector2.Scale(vs[k], handScale), blade - angles[k] - inhand);
            var u1 = Off(k1);
            var u2 = k2 >= 0 ? Off(k2) : u1;
            float ua = Mathf.LerpAngle(Ang(u1), Ang(u2), t) * Mathf.Deg2Rad, ul = Mathf.Lerp(u1.magnitude, u2.magnitude, t);
            var wristTarget = grip - ul * new Vector2(Mathf.Cos(ua), Mathf.Sin(ua));
            if (ik > 0)
            {
                float l1 = up.magnitude, l2 = fore.magnitude;
                float fold = Mathf.Abs(Norm(f.Turn[s + "_fore"] + Ang(fore) - f.Turn[s + "_upper"] - Ang(up)));
                float maxFold = Mathf.Min(179, fold + Mathf.Max(0, FoldCap)) * Mathf.Deg2Rad;
                float dMin = Mathf.Sqrt(Mathf.Max(0, l1 * l1 + l2 * l2 + 2 * l1 * l2 * Mathf.Cos(maxFold)));
                var to = wristTarget - shoulder;
                var aimAt = to.magnitude < dMin ? shoulder + (to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right) * dMin : wristTarget;
                // While the elbow changes side (|bend| < 1) the two-bone blend lands past its target: aim again by the
                // miss, twice, so the forearm's end meets the pinned hand.
                var aim = aimAt;
                for (int pass = 0; pass < 3; pass++)
                {
                    Ik(shoulder, aim, l1, l2, p.F(s + ".bend"), out var a1, out var a2);
                    Put(f, p, s + "_upper", Mathf.LerpAngle(upperFK, Norm(a1 - Ang(up)), ik));
                    Put(f, p, s + "_fore", Mathf.LerpAngle(foreFK, Norm(a2 - Ang(fore)), ik));
                    aim -= Place(f, p, s + "_hand") - aimAt;
                }
            }
            var end = Place(f, p, s + "_hand");
            float turn = blade - angles[k1] - inhand;
            f.Turn[s + "_hand"] = turn;
            f.At[s + "_hand"] = grip - Rot(Vector2.Scale(vs[k1], handScale), turn);
            f.WristGap[i] = (end - f.At[s + "_hand"]).magnitude;
        }

        /// <summary>Where the shoulder of a side is, from the waist, in this pose (BoneMove.Compile's arcs).</summary>
        Vector2 ShoulderFromWaist(BonePose p, string side, float ms)
        {
            var keep = torsoSwap;
            if (playing != null) torsoSwap = playing.Last("torso", ms) is string t && t != "none" ? t : null;
            var f = new BoneFrame { Waist = Rig.J["waist"] + p.V("root") };
            foreach (var part in Rig.Parts) f.Scale[part.Name] = p.V("scale." + part.Name);
            Put(f, p, "hips", p.F("hips"));
            Put(f, p, "torso", p.F("torso"));
            var at = Place(f, p, side + "_upper") - f.Waist;
            torsoSwap = keep;
            return at;
        }

        // --- showing ----------------------------------------------------------------------------------------------

        Vector3 Local(Vector2 canvas) => new Vector3((canvas.x - BoneRig.Centre) * Unit, (BoneRig.Ground - canvas.y) * Unit, 0);

        void PutPicture(Transform t, Vector2 canvas, float deg, Vector2 scale)
        {
            t.localPosition = Local(canvas);
            t.localRotation = Quaternion.Euler(0, 0, -deg);
            t.localScale = new Vector3(scale.x, scale.y, 1);
        }

        /// <summary>Put a solved frame on the pictures, and the hero where travel says.</summary>
        public void Apply(BoneFrame f)
        {
            if (!dressed) Dress();
            lastFrame = f;
            foreach (var p in Rig.Parts)
            {
                var sr = parts[p.Name];
                PutPicture(sr.transform, f.At[p.Name], f.Turn[p.Name], f.Scale[p.Name]);
                sr.sortingOrder = Mathf.RoundToInt(Z(p.Name) * 10);
                float shade = 1 - Mathf.Clamp(f.Shade[p.Name], -1, 1);
                sr.color = shade <= 1 ? new Color(shade, shade, shade, 1) : Color.Lerp(Color.white, new Color(2, 2, 2, 1), shade - 1);
            }
            for (int i = 0; i < 2; i++)
            {
                weapons[i].enabled = f.Armed[i] && weapons[i].sprite != null;
                if (!f.Armed[i]) continue;
                PutPicture(weapons[i].transform, f.Grip[i], f.Blade[i] + 90, Vector2.one);
                weapons[i].sortingOrder = Mathf.RoundToInt(Z(Sides[i] + "_weapon") * 10);
            }
            puppet.localScale = new Vector3(f.Squash.x * (f.Facing < 0 ? -1 : 1), f.Squash.y, 1);
            puppet.localRotation = Quaternion.Euler(0, 0, -f.RootTurn);
            transform.position = HeroWorld(f.Travel);
        }

        Vector3 HeroWorld(float travel)
        {
            var spot = TargetSpot;
            return Design.World(Mathf.LerpUnclamped(Home.x, spot.x, travel), Home.y);
        }

        /// <summary>A canvas point of a frame in world space, as the pictures would show it (also between frames).</summary>
        public Vector3 World(BoneFrame f, Vector2 canvas)
        {
            var l = Local(canvas);
            l = new Vector3(l.x * f.Squash.x * (f.Facing < 0 ? -1 : 1), l.y * f.Squash.y, 0);
            l = Quaternion.Euler(0, 0, -f.RootTurn) * l;
            var s = transform.lossyScale;
            return HeroWorld(f.Travel) + new Vector3(l.x * s.x, l.y * s.y, 0);
        }

        /// <summary>The weapon tip and a point down the blade (canvas px) of side i in a frame.</summary>
        public bool Blade(BoneFrame f, int i, out Vector2 tip, out Vector2 inner)
        {
            tip = inner = Vector2.zero;
            if (!f.Armed[i]) return false;
            var w = Rig.Weapon(weaponNow[i] ?? Held[i]);
            float len = w != null ? (float)w["gy"] * 0.97f : 400;
            var dir = new Vector2(Mathf.Cos(f.Blade[i] * Mathf.Deg2Rad), Mathf.Sin(f.Blade[i] * Mathf.Deg2Rad));
            tip = f.Grip[i] + dir * len;
            inner = f.Grip[i] + dir * len * 0.45f;
            return true;
        }

        /// <summary>The rest layout (rig.json as drawn), no weapon unless held, no move.</summary>
        public void Rest()
        {
            Stop();
            playing = null;
            eventHand[0] = eventHand[1] = null;
            Slots(null, null, null, null, null, Held[0], Held[1]);
            Apply(Solve(new BonePose(), Held[0], Held[1]));
        }

        void Redraw()
        {
            dressed = false;
            if (playing != null && playedTo >= 0) Show(playing, playedTo);
            else Rest();
        }

        /// <summary>The weapon in hand i at `ms`: a "weapon:n=&lt;id&gt;" / "weapon:f=none" event so far, else the hero's, else the move's.</summary>
        string Weapon(BoneMove m, int i, float ms = -1)
        {
            if (m != null && ms >= 0)
            {
                string got = null;
                bool any = false;
                foreach (var a in m.All("weapon", ms))
                    if (a != null && a.Length > 2 && a[1] == '=' && a[0] == Sides[i][0])
                    {
                        any = true;
                        got = a.Substring(2) == "none" ? null : a.Substring(2);
                    }
                if (any) return got;
            }
            return Held[i] ?? (m != null && m.Weapons.TryGetValue(Sides[i], out var w) ? w : null);
        }

        void Show(BoneMove m, float ms)
        {
            string NoneIsNull(string s) => s == "none" ? null : s;
            string nHand = null, fHand = null;
            foreach (var a in m.All("hand", ms))
            {
                if (a == null || a == "none") { nHand = fHand = null; continue; }
                if (a.StartsWith("n_hand=") || a.StartsWith("f_hand="))
                {
                    var v = NoneIsNull(a.Substring(7));
                    if (a[0] == 'n') nHand = v; else fHand = v;
                    continue;
                }
                if (!Rig.SwapPics.TryGetValue(a, out var sw)) continue;
                if (sw.Part == "n_hand") nHand = a; else if (sw.Part == "f_hand") fHand = a;
            }
            string torso = NoneIsNull(m.Last("torso", ms));
            if (torso != null && !Rig.SwapPics.ContainsKey(torso)) torso = null;
            eventHand[0] = nHand;
            eventHand[1] = fHand;
            string face = NoneIsNull(m.Last("face", ms)), order = m.Last("order", ms);
            // NewHands: the solve never reads the near hand shown when it chooses it, so keep the one shown until then
            Slots(torso, nHand ?? (NewHands ? handSwap[0] : null), fHand, face, order, Weapon(m, 0, ms), Weapon(m, 1, ms));
            var f = Solve(m.Sample(ms), weaponNow[0], weaponNow[1]);
            // the grip hand the solve chose shows (the automatic rule; a hand: event wins)
            var want = f.HandAuto[0] ? f.HandPic[0] : nHand;
            if (want != handSwap[0]) Slots(torso, want, fHand, face, order, weaponNow[0], weaponNow[1]);
            Apply(f);
        }

        void Prepare(BoneMove m)
        {
            Rig = BoneRig.Of(Source);
            if (m.StandIns != null)
                foreach (var kv in m.StandIns)
                    if (!Rig.SwapPics.ContainsKey(kv.Key) || Rig.SwapPics[kv.Key].StandIn)
                    {
                        var sw = Rig.ParseSwap(kv.Key, kv.Value as Newtonsoft.Json.Linq.JObject, m.File);
                        if (sw == null) continue;
                        sw.StandIn = true;
                        Rig.SwapPics[kv.Key] = sw;
                    }
            playing = m;
            m.Compile(Rig, ShoulderFromWaist);
        }

        /// <summary>Put the hero in the move's pose at `ms` (x1) without playing: sheets, checks, scrubbing.</summary>
        public void Sample(BoneMove m, float ms)
        {
            Stop();
            Prepare(m);
            playedTo = ms;
            Show(m, ms);
            if (trail != null) trail.Clear();
        }

        /// <summary>
        /// Play a move on the game's Anim clock (x2 and the hit pause reach it) toward `target` (design px, the
        /// monster's feet; null: straight ahead). Every event goes to `events` when its time is passed (face, swaps,
        /// order and trail the hero shows itself). Awaitable like the battle's blows; ends on the move's last pose.
        /// </summary>
        public Task Play(BoneMove m, Vector2? target, IBoneEvents events)
        {
            Stop();
            Target = target;
            Prepare(m);
            handler = events;
            playedTo = -1;
            if (trail != null) trail.Clear();
            Step(0);
            float seconds = Mathf.Max(0.001f, m.Length / 1000f);
            play = Anim.Float(0, m.Length, seconds, Step, Ease.Linear, owner: this);
            return play.Task;
        }

        public void Stop()
        {
            if (play != null && !play.IsDone) play.Stop();
            play = null;
        }

        void Step(float ms)
        {
            var m = playing;
            if (m == null) return;
            float from = playedTo;
            foreach (var e in m.Events)
                if (e.T > from && e.T <= ms) handler?.OnBoneEvent(this, e);
            // ghost:<alpha>,<ms>[,<at>]: a fading pale copy of the pose at move time `at` (default: this frame)
            foreach (var e in m.Events)
                if (e.T > from && e.T <= ms && e.Name == "ghost")
                {
                    float at = BoneGameEvents.Num(e, 2, -1);
                    if (at >= 0) Show(m, at);
                    Ghost(BoneGameEvents.Num(e, 0, 0.3f), BoneGameEvents.Num(e, 1, 200));
                }
            Show(m, ms);
            string mode = m.Last("trail", ms);
            bool on = mode == "on" || mode == "smear";
            if (on || (trail != null && trail.Busy))
            {
                if (trail == null) trail = BoneTrail.Create(this);
                trail.Order = Order + 1;
                if (on) trail.Smear = mode == "smear";
                // The arc between two drawn frames, from poses a few ms apart (a quarter turn inside one frame still
                // draws round).
                float start = Mathf.Max(0, from);
                for (float t = start + BoneTrail.StepMs; t < ms; t += BoneTrail.StepMs)
                {
                    var mt = m.Last("trail", t);
                    if (mt == "on" || mt == "smear") AddTrail(m, t);
                }
                if (on) AddTrail(m, ms);
                trail.Age(ms);
            }
            playedTo = ms;
        }

        /// <summary>
        /// A pale copy of the hero as it is drawn now (every shown picture, filled with `color`), just behind it, fading
        /// out over `ms` on the Anim clock (x2 reaches it) and then destroyed. Beside the hero, named "bone ghost", so the
        /// kit's clean pass and box skip it. Like the battle's pose ghost and afterimages (BattleScreen Ghost).
        /// </summary>
        public void Ghost(float alpha, float ms, Color? color = null)
        {
            var c = color ?? Color.white;
            var root = new GameObject("bone ghost");
            root.transform.SetParent(transform.parent, false);
            root.transform.localPosition = transform.localPosition;
            root.transform.localRotation = transform.localRotation;
            root.transform.localScale = transform.localScale;
            root.AddComponent<SortingGroup>().sortingOrder = Order - 1;
            var copy = Instantiate(puppet.gameObject, root.transform, false);
            copy.name = "puppet";
            var rs = new List<SpriteRenderer>();
            foreach (var sr in copy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr.enabled || sr.sprite == null) { sr.enabled = false; continue; }
                sr.sharedMaterial = Fx.FillMaterial;
                sr.color = new Color(c.r, c.g, c.b, alpha);
                rs.Add(sr);
            }
            Anim.Float(1, 0, Mathf.Max(0.01f, ms / 1000f), k =>
            {
                foreach (var sr in rs) if (sr != null) sr.color = new Color(c.r, c.g, c.b, alpha * k);
            }, Ease.QuadIn).OnComplete(() => { if (root != null) Destroy(root); });
        }

        void AddTrail(BoneMove m, float t)
        {
            var f = Solve(m.Sample(t), weaponNow[0], weaponNow[1]);
            for (int i = 0; i < 2; i++)
                if (Blade(f, i, out var tip, out var inner)) trail.Add(i, World(f, tip), World(f, inner), t);
        }

        void OnDestroy()
        {
            Anim.Clear(this);
            if (trail != null) Destroy(trail.gameObject);
        }
    }
}
#endif
