#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Drakepg.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Drakepg.Test
{
    /// <summary>
    /// TEST (docs/PLANS.md "Layered hero rebuild", the designer's skeleton proposal 2026-11-17): the bare hero on bones,
    /// one sword on the near hand bone, swung by keyframes: ready, wind-up, strike, follow-through, back to ready, then a
    /// hit. The arms are placed by where the hands go (two-bone IK), the legs by where the feet stay; the faces are the
    /// fight's shout and hurt patches on the head bone; the blade leaves fading copies during the strike.
    /// Canvas numbers are rig px, angles clockwise on screen (0 = forward, 90 = down).
    /// </summary>
    public class SkeletonSwing : MonoBehaviour
    {
        public struct Key
        {
            public Vector2 Root, NAnkle, FAnkle, Sword, Far;
            public float Hips, Torso, Head, Blade, NBend, FBend;
            public string Face;
        }

        public static Key Ready = new Key
        {
            Root = new Vector2(0, 30), Torso = 6, Head = -3, NAnkle = new Vector2(-30, 0), FAnkle = new Vector2(40, 0),
            Sword = new Vector2(70, -40), Blade = -45, NBend = 1, Far = new Vector2(150, -150), FBend = 1,
        };
        public static Key Wind = new Key
        {
            Root = new Vector2(-25, 55), Hips = -4, Torso = -14, Head = 6, NAnkle = new Vector2(-60, 0), FAnkle = new Vector2(60, 0),
            Sword = new Vector2(-280, -290), Blade = 205, NBend = -1, Far = new Vector2(170, -170), FBend = 1,
        };
        public static Key Coil = new Key
        {
            Root = new Vector2(-30, 60), Hips = -5, Torso = -17, Head = 7, NAnkle = new Vector2(-60, 0), FAnkle = new Vector2(60, 0),
            Sword = new Vector2(-300, -270), Blade = 195, NBend = -1, Far = new Vector2(175, -160), FBend = 1,
        };
        public static Key Strike = new Key
        {
            Root = new Vector2(110, 130), Hips = 10, Torso = 32, Head = 6, NAnkle = new Vector2(-80, 0), FAnkle = new Vector2(260, 0),
            Sword = new Vector2(290, -120), Blade = 5, NBend = 1, Far = new Vector2(-190, -170), FBend = 1, Face = "shout",
        };
        public static Key Follow = new Key
        {
            Root = new Vector2(120, 140), Hips = 12, Torso = 36, Head = 6, NAnkle = new Vector2(-80, 0), FAnkle = new Vector2(260, 0),
            Sword = new Vector2(260, -20), Blade = 40, NBend = 1, Far = new Vector2(-200, -140), FBend = 1, Face = "shout",
        };
        public static Key Hurt = new Key
        {
            Root = new Vector2(-45, 45), Hips = -6, Torso = -18, Head = -16, NAnkle = new Vector2(-70, 0), FAnkle = new Vector2(30, 0),
            Sword = new Vector2(30, -10), Blade = 115, NBend = 1, Far = new Vector2(120, -260), FBend = 1, Face = "hurt",
        };

        public static Dictionary<string, Key> Keys => new Dictionary<string, Key>
            { ["ready"] = Ready, ["wind"] = Wind, ["coil"] = Coil, ["strike"] = Strike, ["follow"] = Follow, ["hurt"] = Hurt };

        LayeredHero hero;
        SpriteRenderer face, weapon;
        readonly List<(float t, Key k, Func<float, float> ease)> track = new List<(float, Key, Func<float, float>)>();
        readonly List<(SpriteRenderer sr, float born)> ghosts = new List<(SpriteRenderer, float)>();
        float clock, trailFrom = -1, trailTo = -1;
        public bool Breathes = true;

        public static SkeletonSwing Create(Transform parent, float x, float y, float scale)
        {
            var hero = LayeredHero.Create(parent, x, y, scale);
            var s = hero.gameObject.AddComponent<SkeletonSwing>();
            s.hero = hero;
            s.weapon = hero.transform.Find("near weapon").GetComponent<SpriteRenderer>();
            s.face = new GameObject("face").AddComponent<SpriteRenderer>();
            s.face.transform.SetParent(hero.transform.Find("head"), false);
            s.face.sortingOrder = 610;
            s.Pose(Ready);
            return s;
        }

        static float Norm(float a) => Mathf.Repeat(a + 180, 360) - 180;
        static float Ang(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        static Vector2 J(string n) => LayeredHero.Joint(n);
        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static Vector2 Rot(Vector2 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

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

        /// <summary>The arm of side s reaching its wrist to `wrist`: turns of its upper arm and forearm.</summary>
        static void Arm(LayeredHero.Pose p, string s, Vector2 waist, float torso, Vector2 wrist, float bend)
        {
            var shoulder = waist + Rot(J(s + "_shoulder") - J("waist"), torso);
            Vector2 up = J(s + "_elbow") - J(s + "_shoulder"), fore = J(s + "_wrist") - J(s + "_elbow");
            var (d1, d2) = Ik(shoulder, wrist, up.magnitude, fore.magnitude, bend);
            p.Arm[s + "_upper"] = Norm(d1 - Ang(up));
            p.Arm[s + "_fore"] = Norm(d2 - Ang(fore));
        }

        public static LayeredHero.Pose Build(Key k)
        {
            var p = new LayeredHero.Pose { Hips = k.Hips, Torso = k.Torso, Head = k.Head, Root = k.Root, NAnkle = k.NAnkle, FAnkle = k.FAnkle };
            p.Weapon[0] = "iron_sword";
            var waist = J("waist") + k.Root;
            var g = LayeredHero.RigJson["grips"]["n"];
            float hand = Norm(k.Blade - (float)g["angle"]);
            Arm(p, "n", waist, k.Torso, waist + k.Sword - Rot(V(g["at"]) - J("n_wrist"), hand), k.NBend);
            p.Arm["n_hand"] = hand;
            Arm(p, "f", waist, k.Torso, waist + k.Far, k.FBend);
            p.Arm["f_hand"] = p.Arm["f_fore"];
            return p;
        }

        static Key Mix(Key a, Key b, float t) => new Key
        {
            Root = Vector2.LerpUnclamped(a.Root, b.Root, t), NAnkle = Vector2.LerpUnclamped(a.NAnkle, b.NAnkle, t),
            FAnkle = Vector2.LerpUnclamped(a.FAnkle, b.FAnkle, t), Far = Vector2.LerpUnclamped(a.Far, b.Far, t),
            // The sword hand travels on an arc around the shoulder, not straight across the body.
            Sword = Arc(a.Sword, b.Sword, t),
            Hips = Mathf.LerpAngle(a.Hips, b.Hips, t), Torso = Mathf.LerpAngle(a.Torso, b.Torso, t), Head = Mathf.LerpAngle(a.Head, b.Head, t),
            Blade = a.Blade + Norm(b.Blade - a.Blade) * t, NBend = b.NBend, FBend = b.FBend, Face = t < 0.5f ? a.Face : b.Face,
        };

        static Vector2 Arc(Vector2 a, Vector2 b, float t)
        {
            var c = new Vector2(-40, -200); // about the near shoulder, from the waist
            Vector2 da = a - c, db = b - c;
            float ang = Ang(da) + Norm(Ang(db) - Ang(da)) * t;
            float len = Mathf.LerpUnclamped(da.magnitude, db.magnitude, t);
            return c + len * new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
        }

        public void Pose(Key k)
        {
            hero.Show(Build(k));
            if (k.Face == null) face.enabled = false;
            else
            {
                var e = Faces()[k.Face];
                face.enabled = true;
                face.sprite = LayeredHero.PartSprite((string)e["file"], J("neck") - new Vector2((float)e["x"], (float)e["y"]));
            }
        }

        static JObject faces;
        static JObject Faces() => faces ??= (JObject)JObject.Parse(File.ReadAllText(Path.Combine(LayeredHero.Folder, "fight.json")))["faces"];
        public static void Reload() => faces = null;

        public static float In(float x) => x * x;
        public static float Out(float x) => 1 - (1 - x) * (1 - x);
        public static float InOut(float x) => x < 0.5f ? 2 * x * x : 1 - 2 * (1 - x) * (1 - x);

        /// <summary>A blow starting now: wind-up, a short coil, the strike, the follow-through, back to ready.</summary>
        public void Blow()
        {
            float t = clock;
            track.Clear();
            track.Add((t, Ready, InOut));
            track.Add((t + 0.30f, Wind, InOut));
            track.Add((t + 0.36f, Coil, Out));
            track.Add((t + 0.44f, Strike, In));
            track.Add((t + 0.52f, Follow, Out));
            track.Add((t + 0.80f, Follow, InOut));
            track.Add((t + 1.16f, Ready, InOut));
            trailFrom = t + 0.37f;
            trailTo = t + 0.56f;
        }

        /// <summary>A hit taken now: thrown back into the hurt, held, back to ready.</summary>
        public void Hit()
        {
            float t = clock;
            track.Clear();
            track.Add((t, Ready, InOut));
            track.Add((t + 0.08f, Hurt, Out));
            track.Add((t + 0.34f, Hurt, InOut));
            track.Add((t + 0.72f, Ready, InOut));
        }

        void Update()
        {
            clock += Time.deltaTime;
            var k = Ready;
            if (track.Count > 0)
            {
                if (clock >= track[track.Count - 1].t) k = track[track.Count - 1].k;
                else
                    for (int i = 0; i + 1 < track.Count; i++)
                        if (clock < track[i + 1].t)
                        {
                            var (t0, a, _) = track[i];
                            var (t1, b, ease) = track[i + 1];
                            k = Mix(a, b, ease(Mathf.Clamp01((clock - t0) / Mathf.Max(0.0001f, t1 - t0))));
                            break;
                        }
            }
            if (Breathes)
            {
                float w = Mathf.Sin(clock * Mathf.PI * 2 / 2.4f);
                k.Root.y += 3 * w;
                k.Torso += 0.8f * w;
            }
            Pose(k);
            Trail();
        }

        void Trail()
        {
            if (clock >= trailFrom && clock <= trailTo && weapon.enabled)
            {
                var g = new GameObject("blade trail").AddComponent<SpriteRenderer>();
                g.transform.SetParent(transform, true);
                g.transform.SetPositionAndRotation(weapon.transform.position, weapon.transform.rotation);
                g.transform.localScale = weapon.transform.localScale;
                g.sprite = weapon.sprite;
                g.sortingOrder = weapon.sortingOrder - 1;
                ghosts.Add((g, clock));
            }
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                float age = (clock - ghosts[i].born) / 0.12f;
                if (age >= 1) { Destroy(ghosts[i].sr.gameObject); ghosts.RemoveAt(i); continue; }
                ghosts[i].sr.color = new Color(1, 0.97f, 0.9f, 0.45f * (1 - age));
            }
        }
    }

    public static class SkeletonSwingDemo
    {
        static GameObject root;
        static SkeletonSwing hero;
        static Figure today;

        public static string Clear()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            return "cleared";
        }

        /// <summary>Today's drawn hero (left) and the hero on bones (right), on a plain plate.</summary>
        public static string Stage(float left = 400, float right = 840, float feet = 600, float size = 1.15f)
        {
            Clear();
            LayeredHero.Reload();
            SkeletonSwing.Reload();
            root = new GameObject("skeleton swing test");
            root.transform.SetParent(GameRoot.I.Screen.World, false);
            var plate = new GameObject("plate").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(root.transform, false);
            plate.sprite = Art.White;
            plate.color = new Color(0.9f, 0.88f, 0.83f);
            plate.sortingOrder = 29000;
            plate.transform.position = Design.World(Design.Width / 2, Design.Height / 2);
            plate.transform.localScale = new Vector3(Design.Width * 2, Design.Height * 2, 1);
            var look = Db.Class("warrior").Look;
            today = Figure.Create(root.transform, left, feet, look, 1, Db.Item("iron_sword"));
            today.transform.localScale *= size;
            today.Order = 30000;
            today.EnableBend();
            today.SetPose("sword_ready");
            hero = SkeletonSwing.Create(root.transform, right, feet, (float)look.Scale * size * 0.95f);
            hero.GetComponent<LayeredHero>().Order = 30000;
            var shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(hero.transform, false);
            shadow.sprite = Art.Soft;
            shadow.color = new Color(0, 0, 0, 0.45f);
            shadow.sortingOrder = -100;
            shadow.transform.localScale = new Vector3(FightHero.ShadowWidth / 64f / hero.transform.localScale.x, 26 / 64f / hero.transform.localScale.y, 1);
            return "stage";
        }

        /// <summary>Holds one key (ready, wind, coil, strike, follow, hurt) still, for a picture.</summary>
        public static string Hold(string key)
        {
            hero.Breathes = false;
            hero.enabled = false;
            hero.Pose(SkeletonSwing.Keys[key]);
            return key;
        }

        /// <summary>The clip: stand, a blow (both heroes), stand, a hit (both), stand. Timed on game time (the recorder
        /// steps it frame by frame; Anim's timers would run ahead).</summary>
        public static string Play(float blowAt = 0.6f, float hitAt = 2.4f)
        {
            hero.enabled = true;
            hero.Breathes = true;
            var d = root.AddComponent<Director>();
            d.BlowAt = blowAt;
            d.HitAt = hitAt;
            return "play";
        }

        class Director : MonoBehaviour
        {
            public float BlowAt, HitAt;
            float t;
            int done;
            float home = float.NaN;

            static float Seg(float t, float t0, float dur, float a, float b, Func<float, float> ease) =>
                t <= t0 ? a : t >= t0 + dur ? b : Mathf.LerpUnclamped(a, b, ease((t - t0) / dur));

            void Update()
            {
                if (today == null || hero == null) return;
                if (float.IsNaN(home)) home = today.transform.position.x;
                t += Time.deltaTime;
                float b = t - BlowAt, h = t - HitAt;
                void Once(int bit, float at, Action act) { if (at >= 0 && (done & bit) == 0) { done |= bit; act(); } }
                Once(1, b, () => { hero.Blow(); today.SetPose("sword_wind"); });
                Once(2, b - 0.34f, () => today.SetPose("sword_step"));
                Once(4, b - 0.42f, () => today.SetPose("sword_strike"));
                Once(8, b - 0.80f, () => today.SetPose("sword_ready"));
                Once(16, h, () => { hero.Hit(); Fx.WhiteFlash(today, 90); });
                float x = 0, tilt = 0;
                if (b >= 0 && h < 0)
                    x = b < 0.34f ? Seg(b, 0, 0.26f, 0, -12, SkeletonSwing.Out)
                      : b < 0.80f ? Seg(b, 0.34f, 0.13f, -12, 70, SkeletonSwing.In)
                      : Seg(b, 0.80f, 0.34f, 70, 0, SkeletonSwing.InOut);
                if (h >= 0)
                {
                    x = h < 0.11f ? Seg(h, 0, 0.06f, 0, -40, SkeletonSwing.Out) : Seg(h, 0.11f, 0.3f, -40, 0, SkeletonSwing.InOut);
                    tilt = h < 0.11f ? Seg(h, 0, 0.06f, 0, 10, SkeletonSwing.Out) : Seg(h, 0.11f, 0.3f, 10, 0, SkeletonSwing.InOut);
                }
                var p = today.transform.position;
                today.transform.position = new Vector3(home + x / Design.PixelsPerUnit, p.y, p.z);
                today.transform.localRotation = Quaternion.Euler(0, 0, tilt);
            }
        }
    }
}
#endif
