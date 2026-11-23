using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Plays hero 4's clip (Assets/Test2D) and records it, in play mode (through the Unity MCP's execute_code):
    /// Studio(dir, ...) renders the clip frame by frame on a flat backdrop at the drawing's own scale (1 px = 1 drawing px)
    /// with the joint numbers per frame (studio.json); Sweep(dir) rotates each joint through its range; Battle(...) puts
    /// hero 4 in today's hero's place in a fight (his feet, tilt, squash and lunge) and plays the clip on the blow.
    /// </summary>
    public static class Test2DRun
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        public static bool Busy { get; private set; }
        public static string Last { get; private set; }
        static GameObject studio;
        static Camera cam;

        static AnimationClip Clip => AssetDatabase.LoadAssetAtPath<AnimationClip>(Test2DBuild.ClipPath);

        /// <summary>The hero on his own, far from the game, with a camera that sees the drawing canvas window.</summary>
        static GameObject Make(Vector3 at, out Camera camera, int w = 1800, int h = 1000, float ox = -150, float oy = -110)
        {
            if (studio != null) Object.Destroy(studio);
            studio = new GameObject("test2d studio");
            studio.transform.position = at;
            var hero = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Test2DBuild.PrefabPath), studio.transform);
            hero.name = "hero4";
            hero.transform.localPosition = Vector3.zero;
            var cgo = new GameObject("test2d cam");
            cgo.transform.SetParent(studio.transform, false);
            camera = cgo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = h / 2f / 100f;
            var c = Test2DBuild.C2R(new Vector2(ox + w / 2f, oy + h / 2f));
            cgo.transform.localPosition = new Vector3(c.x, c.y, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.58f, 1);
            camera.enabled = false;
            camera.targetTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            return hero;
        }

        static void Save(Camera camera, string file)
        {
            var rt = camera.targetTexture;
            camera.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static Transform Find(Transform root, string name) => BonesCapture.Find(root, name);
        static string F(float v) => v.ToString("0.###", C);
        static string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "]";

        /// <summary>World angle of a transform's +x (Unity degrees).</summary>
        static float Ang(Transform t) => Mathf.Atan2(t.right.y, t.right.x) * Mathf.Rad2Deg;

        /// <summary>One line of joint numbers (hero-root space, drawing px, y down): what the tests read.</summary>
        static string Joints(GameObject hero, int f)
        {
            var r = hero.transform;
            Transform T(string n) => Find(r, n);
            Vector3 P(string n) { var p = r.InverseTransformPoint(T(n).position) * 100; return new Vector3(p.x, -p.y, 0); }
            float up = -Ang(T("upper")), fo = -Ang(T("fore")), hand = -Ang(T("hand")), torso = -Ang(T("torso"));
            float b = Mathf.DeltaAngle(fo, up);   // drawing convention: forearm = upper - b
            var tgt = P("target");
            var wrist = P("hand");
            var sr = new StringBuilder();
            sr.Append("{\"f\":" + f + ",\"upper\":" + F(up) + ",\"fore\":" + F(fo) + ",\"b\":" + F(b) + ",\"hand\":" + F(hand) + ",\"torso\":" + F(torso));
            sr.Append(",\"upper_rel_torso\":" + F(Mathf.DeltaAngle(torso, up)));
            sr.Append(",\"S\":" + V(P("upper")) + ",\"elbow\":" + V(P("fore")) + ",\"wrist\":" + V(wrist) + ",\"target\":" + V(tgt));
            sr.Append(",\"reach_miss\":" + F((wrist - tgt).magnitude));
            sr.Append(",\"flip\":" + (T("ik_arm").GetComponent<LimbSolver2D>().flip ? 1 : 0));
            foreach (var n in new[] { "torso_pic", "head_pic", "far_arm_pic", "armhole_pic", "sleeve", "shoulder_pic", "hips_pic", "tails_pic" })
                sr.Append(",\"" + n + "\":\"" + T(n).GetComponent<SpriteRenderer>().sprite?.name + "\"");
            sr.Append(",\"tails_order\":" + T("tails_pic").GetComponent<SpriteRenderer>().sortingOrder);
            sr.Append(",\"waist\":" + V(P("hips")) + ",\"neck\":" + V(P("neck")));
            int sm = 0;
            foreach (var s in T("smears").GetComponentsInChildren<SpriteRenderer>()) if (s.enabled) sm++;
            sr.Append(",\"smear\":" + sm);
            var sk = T("sleeve").GetComponent<SpriteSkin>();
            sr.Append(",\"skin_valid\":" + (sk != null && sk.isActiveAndEnabled ? 1 : 0));
            var sb = T("sleeve").GetComponent<SpriteRenderer>().bounds;
            sr.Append(",\"sleeve_bounds\":" + V(sb.size * 100));
            var tip = T("sword_pic").GetComponent<SpriteRenderer>();
            sr.Append("}");
            return sr.ToString();
        }

        /// <summary>Render every clip frame (fps of the clip) to dir/s%03d.png on a flat backdrop + dir/studio.json.</summary>
        public static string Studio(string dir) => Studio(dir, 1800, 1000, -150, -110);

        /// <summary>As Studio, with the canvas window to render (w x h drawing px from ox, oy).</summary>
        public static string Studio(string dir, int w, int h, float ox, float oy) { if (Busy) return "busy"; Busy = true; GameRoot.I.StartCoroutine(StudioLoop(dir, w, h, ox, oy)); return "studio started"; }

        static IEnumerator StudioLoop(string dir, int w, int h, float ox, float oy)
        {
            Directory.CreateDirectory(dir);
            var hero = Make(new Vector3(500, 0, 0), out cam, w, h, ox, oy);
            var clip = Clip;
            int n = Mathf.RoundToInt(clip.length * clip.frameRate) + 1;
            var lines = new List<string>();
            for (int i = 0; i < n; i++)
            {
                clip.SampleAnimation(hero, i / clip.frameRate);
                yield return new WaitForEndOfFrame();
                lines.Add(Joints(hero, i));
                Save(cam, Path.Combine(dir, $"s{i:000}.png"));
            }
            File.WriteAllText(Path.Combine(dir, "studio.json"), "[\n" + string.Join(",\n", lines) + "\n]");
            Last = $"studio: {n} frames in {dir}";
            Debug.Log("drakepg: test2d " + Last);
            Busy = false;
        }

        /// <summary>
        /// Joint sweep: from the ready pose, the shoulder (upper arm) through -180..+180 relative, the elbow through the
        /// solver's bend range on its fixed side, the wrist (fist) through -90..+90; slow (every 3 degrees), on the
        /// flat backdrop: dir/w%03d.png + sweep.json.
        /// </summary>
        public static string Sweep(string dir) { if (Busy) return "busy"; Busy = true; GameRoot.I.StartCoroutine(SweepLoop(dir)); return "sweep started"; }

        static IEnumerator SweepLoop(string dir)
        {
            Directory.CreateDirectory(dir);
            var hero = Make(new Vector3(500, 0, 0), out cam);
            var clip = Clip;
            clip.SampleAnimation(hero, 0);
            var r = hero.transform;
            var ik = r.GetComponent<IKManager2D>();
            ik.enabled = false;   // the sweep drives the joints directly (forward), so each joint moves alone
            var upper = Find(r, "upper");
            var fore = Find(r, "fore");
            var hand = Find(r, "hand");
            yield return new WaitForEndOfFrame();
            // the rest pose as the clip's frame 0 solved: take it from the IK once
            ik.enabled = true;
            clip.SampleAnimation(hero, 0);
            yield return new WaitForEndOfFrame();
            ik.enabled = false;
            var u0 = upper.localRotation; var f0 = fore.localRotation; var h0 = hand.rotation;
            var lines = new List<string>();
            int k = 0;
            IEnumerator Shot(string joint, float deg)
            {
                yield return new WaitForEndOfFrame();
                lines.Add("{\"joint\":\"" + joint + "\",\"deg\":" + F(deg) + ",\"j\":" + Joints(hero, k) + "}");
                Save(cam, Path.Combine(dir, $"w{k:000}.png"));
                k++;
            }
            for (float d = -180; d <= 180; d += 6) { upper.localRotation = u0 * Quaternion.Euler(0, 0, d); fore.localRotation = f0; hand.rotation = h0 * Quaternion.Euler(0, 0, d); yield return Shot("shoulder", d); }
            upper.localRotation = u0;
            // the elbow on the ready side: forearm from straight (0) to folded 140 degrees
            float bend0 = Mathf.DeltaAngle(0, f0.eulerAngles.z);
            for (float d = 0; d <= 140; d += 4) { fore.localRotation = Quaternion.Euler(0, 0, d); hand.rotation = h0 * Quaternion.Euler(0, 0, d - bend0); yield return Shot("elbow", d); }
            fore.localRotation = f0;
            for (float d = -90; d <= 90; d += 4) { hand.rotation = h0 * Quaternion.Euler(0, 0, d); yield return Shot("wrist", d); }
            hand.rotation = h0;
            ik.enabled = true;
            File.WriteAllText(Path.Combine(dir, "sweep.json"), "[\n" + string.Join(",\n", lines) + "\n]");
            Last = $"sweep: {k} frames in {dir}";
            Debug.Log("drakepg: test2d " + Last);
            Busy = false;
        }

        // ------------------------------------------------------------------ in a fight

        static GameObject battleHero;
        public static GameObject BattleHero => battleHero;
        static object Screen => GameRoot.I != null ? GameRoot.I.Screen : null;
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        static object Fld(object o, string n) => o?.GetType().GetField(n, Any)?.GetValue(o);
        static float Num(object o, string n) => Fld(o, n) is float v ? v : 0;

        /// <summary>
        /// Put hero 4 where today's hero stands in this fight (same feet, size, tilt, squash, lunge; today's hero hidden
        /// except his ground shadow) and play sword_cut when today's hero starts his blow (his wind pose), on the
        /// battle's clock (hit stop included). `startFrame`: the clip frame shown on that first frame. Call before
        /// BonesCapture.Take; Remove() puts today's hero back.
        /// </summary>
        public static string Battle(int startFrame = 1)
        {
            var hero = Fld(Screen, "hero");
            var fig = Fld(hero, "Fig") as Figure;
            if (fig == null) return "no battle hero";
            if (battleHero != null) Object.Destroy(battleHero);
            battleHero = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Test2DBuild.PrefabPath), fig.transform.parent);
            battleHero.name = "hero4 battle";
            // size: today's hero's standing height (his drawn box, measured before he is hidden) = hero 4's 1027 px
            var old = BonesCapture.BoundsOf(fig.gameObject);
            float oldH = old.HasValue ? old.Value.size.y : 4.8f;
            float k = oldH / (10.27f * fig.transform.parent.lossyScale.y);
            var sg = battleHero.GetComponent<UnityEngine.Rendering.SortingGroup>();
            int order = 0; string layer = "Default";
            foreach (var r in fig.GetComponentsInChildren<SpriteRenderer>(true)) if (r.sortingOrder > order) { order = r.sortingOrder; layer = r.sortingLayerName; }
            // the figure's own sorting group decides where it draws among the fight's figures and their afterimages
            var figSg = fig.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (figSg != null) { order = figSg.sortingOrder; layer = figSg.sortingLayerName; }
            sg.sortingLayerName = layer;
            sg.sortingOrder = order;
            known.Clear();
            foreach (Transform c in fig.transform.parent) known.Add(c.gameObject);
            ghosts.Clear();
            GameRoot.I.StartCoroutine(Follow(hero, fig, k, startFrame));
            return $"hero 4 in the fight: old height {oldH:0.000} u, scale {k:0.0000}, order {order}";
        }

        public static float ClipTime { get; private set; } = -1;

        /// <summary>
        /// Hero 4 stands this many drawing px further from the foe than today's hero (his whole stand, every frame; his
        /// ground shadow and afterimages with him): his strike plants the back foot, so the lunge lands nearer the foe
        /// than today's one drawing, and the blade would run through it.
        /// </summary>
        public static float StandBack = 267f;   // measured: the held strike's tip just reaches the rat's front (takes 13-15)
        static Vector3 standOffset;
        static Transform shadow;
        static Vector3 shadowBase;

        static IEnumerator Follow(object hero, Figure fig, float k, int startFrame)
        {
            var clip = Clip;
            float t = -1;
            bool inStop = false;
            var go = battleHero;
            standOffset = Vector3.left * (StandBack / 100f * k);
            shadow = null;
            foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) if (r.name == "shadow") { shadow = r.transform; shadowBase = shadow.localPosition; break; }
            while (go != null && fig != null)
            {
                var tr = go.transform;
                tr.localPosition = fig.transform.localPosition + standOffset;
                if (shadow != null)
                {
                    shadow.localPosition = shadowBase;
                    shadow.position += tr.parent.TransformVector(standOffset);
                }
                // Not the battle's tilt (today's one drawing leans back -8.4 in the wind-up, forward +5 in the strike):
                // tilting hero 4 about his feet would lift the lunge's planted foot off the ground; his clip leans his
                // body itself (stance, torso, the lunge drawings).
                tr.localRotation = Quaternion.identity;
                // Size only: today's squash and stretch (Sx, Sy, Breath) scale x and y apart, which on a jointed rig shears
                // every turned part (the fist, the sleeve) and bends the solver's straight arm; hero 4 is not squashed.
                tr.localScale = new Vector3(k, k, 1);
                foreach (var r in fig.GetComponentsInChildren<Renderer>(true))
                    if (r.name != "shadow") r.enabled = false;
                if (t < 0 && fig.PoseName != null && fig.PoseName.StartsWith("sword_wind")) t = startFrame / clip.frameRate;
                else if (t >= 0)
                {
                    // the battle's clock; the hit stop's first frame still advances, so the strike shows on the contact
                    // frame and is then held for the stop
                    bool stop = Anim.TimeScale < 0.5f;
                    t += Time.deltaTime * (stop && !inStop ? 1 : Anim.TimeScale);
                    inStop = stop;
                }
                ClipTime = t;
                clip.SampleAnimation(go, Mathf.Clamp(t, 0, clip.length));
                Ghosts(fig, go);
                yield return null;
            }
        }

        static readonly Dictionary<GameObject, GameObject> ghosts = new Dictionary<GameObject, GameObject>();
        static readonly HashSet<GameObject> known = new HashSet<GameObject>();

        /// <summary>
        /// The afterimages: the game copies today's hero (BattleScreen.Ghost / Fx.Ghost: a new Figure, flat colour,
        /// fading, one order under him); each such copy is hidden and the same is done with hero 4 as he stands now (same
        /// colour, material, fade, draw order under him). New figures under the fight's parent after Battle() are copies.
        /// </summary>
        static void Ghosts(Figure fig, GameObject hero4)
        {
            foreach (Transform c in fig.transform.parent)
            {
                if (known.Contains(c.gameObject) || c.gameObject == hero4 || c.name.StartsWith("hero4")) continue;
                if (c.GetComponent<Figure>() == null || ghosts.ContainsKey(c.gameObject)) continue;
                if (Mathf.Sign(c.localScale.x) != Mathf.Sign(fig.transform.localScale.x)) continue;   // the foe's copies
                SpriteRenderer src = null;
                foreach (var r in c.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name != "shadow" && r.color.a > 0) { src = r; break; }
                if (src == null) continue;
                var copy = Object.Instantiate(hero4, hero4.transform.parent);
                copy.name = "hero4 ghost";
                copy.transform.localPosition = c.localPosition + standOffset;
                copy.transform.localRotation = Quaternion.identity;   // as hero 4 himself (no battle tilt)
                copy.transform.localScale = hero4.transform.localScale;
                copy.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = hero4.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder - 1;
                // own materials, tinted: the game's flat fill material (Fx.FillMaterial) draws only scraps of hero 4's
                // sprites (tested 2026-11-23), so his afterimages are tinted see-through copies instead of flat ones
                foreach (var r in copy.GetComponentsInChildren<SpriteRenderer>(true)) r.color = src.color;
                ghosts[c.gameObject] = copy;
            }
            var gone = new List<GameObject>();
            foreach (var kv in ghosts)
            {
                if (kv.Key == null) { if (kv.Value != null) Object.Destroy(kv.Value); gone.Add(kv.Key); continue; }
                SpriteRenderer src = null;
                foreach (var r in kv.Key.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name != "shadow") { if (src == null && r.color.a > 0) src = r; r.enabled = false; }
                if (src != null && kv.Value != null)
                    foreach (var r in kv.Value.GetComponentsInChildren<SpriteRenderer>(true)) r.color = src.color;
            }
            foreach (var g in gone) ghosts.Remove(g);
        }

        /// <summary>Remove hero 4 from the fight and show today's hero again.</summary>
        public static string Remove()
        {
            var fig = Fld(Fld(Screen, "hero"), "Fig") as Figure;
            if (battleHero != null) Object.Destroy(battleHero);
            battleHero = null;
            if (shadow != null) shadow.localPosition = shadowBase;
            shadow = null;
            if (fig != null) foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            return "removed";
        }

        /// <summary>BonesCapture points on hero 4 in the fight (the same names as today's take where they mean the same).</summary>
        public static string Track()
        {
            if (battleHero == null) return "no hero 4";
            var f = BonesCapture.Follow(battleHero, "grip=sword_pic; S=upper; elbow=fore; wrist=hand; neck=neck; waist=hips; feet=hero4 battle");
            BonesCapture.Box("hero", () => battleHero == null ? null : BonesCapture.BoundsOf(battleHero));
            BonesCapture.Value("clip_t", () => ClipTime);
            BonesCapture.Value("anim_scale", () => Anim.TimeScale);
            BonesCapture.Watch("torso_pic", () => Find(battleHero.transform, "torso_pic").GetComponent<SpriteRenderer>().sprite?.name);
            BonesCapture.Watch("hips_pic", () => Find(battleHero.transform, "hips_pic").GetComponent<SpriteRenderer>().sprite?.name);
            BonesCapture.Watch("tails_order", () => Find(battleHero.transform, "tails_pic").GetComponent<SpriteRenderer>().sortingOrder.ToString());
            BonesCapture.Follow(battleHero, "hip=hips; knot=knot; far=far");
            var solver = Find(battleHero.transform, "ik_arm").GetComponent<LimbSolver2D>();
            BonesCapture.Watch("flip", () => solver.flip ? "1" : "0");
            BonesCapture.Value("upper", () => -Ang(Find(battleHero.transform, "upper")));
            BonesCapture.Value("fore", () => -Ang(Find(battleHero.transform, "fore")));
            BonesCapture.Value("hand", () => -Ang(Find(battleHero.transform, "hand")));
            BonesCapture.Value("root_angle", () => -battleHero.transform.eulerAngles.z);
            return f;
        }

        /// <summary>
        /// Lossless frames beside a BonesCapture.Take started in the same call (same frames: both capture at the end of
        /// each frame on the fixed clock it sets): dir/p%03d.png for frames from..to of the take, full game size.
        /// </summary>
        public static string Png(string dir, int from, int to)
        {
            Directory.CreateDirectory(dir);
            GameRoot.I.StartCoroutine(PngLoop(dir, from, to));
            return $"png frames {from}..{to} to {dir}";
        }

        static IEnumerator PngLoop(string dir, int from, int to)
        {
            for (int i = 0; i <= to; i++)
            {
                yield return new WaitForEndOfFrame();
                if (i < from) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"p{i:000}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            Debug.Log("drakepg: test2d png frames done " + dir);
        }

        public static string Clear()
        {
            if (studio != null) Object.Destroy(studio);
            return "cleared";
        }
    }
}
