using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Test only (2026-11-24, the designer's test 1 "motion only"): the Dragon Crashers knight, untouched (its own
    /// skeleton, drawings and attack clip, Assets/Test2D/dc), stands where today's hero stands in a fight, at his size,
    /// and plays its attack when today's hero starts his blow, on the battle's clock (hit stop included). Today's hero is
    /// hidden except his ground shadow. Nothing on the knight is posed or edited. Play mode, through the Unity MCP's
    /// execute_code: Drive.Fight(...), then DcFight.Battle(), then BonesCapture.Take(...); Remove() puts the hero back.
    /// </summary>
    public static class DcFight
    {
        const string Dir = "Assets/Test2D/dc/";
        public const string PrefabPath = Dir + "Art/Characters/_Prefabs/PV_Character_Knight.prefab";
        public const string ClipPath = Dir + "Animations/Characters/AnimClips/Knight/Anim_Character_Knight_Attack.anim";

        /// <summary>Clip time (s) shown on the first frame of today's hero's wind-up; the clip then runs on the battle clock.</summary>
        public static float StartAt = 0f;

        /// <summary>
        /// Size correction measured on the screen (2026-11-24): the skinned drawings' boxes read before they are posed
        /// make the knight 568 px tall against today's hero's 472 px (head top to feet, 2560 take); 472 / 568.
        /// </summary>
        public static float SizeFix = 0.831f;
        public static float ClipTime { get; private set; } = -1;
        public static GameObject Knight => knight;

        static GameObject knight;
        static PlayableGraph graph;
        static AnimationClipPlayable play;
        static readonly HashSet<GameObject> known = new HashSet<GameObject>();
        static readonly Dictionary<GameObject, GameObject> ghosts = new Dictionary<GameObject, GameObject>();
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        static object Fld(object o, string n) => o?.GetType().GetField(n, Any)?.GetValue(o);
        static object Screen => GameRoot.I != null ? GameRoot.I.Screen : null;

        static bool IsWeapon(Transform t, Transform root)
        {
            for (var p = t; p != null && p != root; p = p.parent) if (p.name.StartsWith("P_Weapon")) return true;
            return false;
        }

        /// <summary>Body box (no weapon, shield or shadow) of a root's enabled sprite renderers.</summary>
        static Bounds? Body(GameObject root, ICollection<Transform> skip)
        {
            Bounds? all = null;
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r.enabled || r.sprite == null || r.name == "shadow" || r.name.Contains("trail")) continue;
                bool s = IsWeapon(r.transform, root.transform);
                foreach (var w in skip) if (w != null && r.transform.IsChildOf(w)) s = true;
                if (s) continue;
                if (all == null) all = r.bounds; else { var b = all.Value; b.Encapsulate(r.bounds); all = b; }
            }
            return all;
        }

        public static string Battle()
        {
            var hero = Fld(Screen, "hero");
            var fig = Fld(hero, "Fig") as Figure;
            if (fig == null) return "no battle hero";
            Remove();
            var parent = fig.transform.parent;
            knight = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
            knight.name = "dc knight";
            var animator = knight.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            graph = PlayableGraph.Create("dc knight");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "dc", animator);
            play = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(play);
            play.SetTime(0);
            graph.Evaluate(0);

            // the knight keeps its own material (Sprite-Lit-Default, as the game's weapons; the fight has no 2D lights, so
            // it draws unlit-looking); the hero's own "Fx alpha" draws only scraps of a skinned sprite (Test2DRun, 2026-11-23)
            string mat = null;

            knight.transform.localPosition = fig.transform.localPosition;
            knight.transform.localRotation = Quaternion.identity;
            knight.transform.localScale = Vector3.one;
            var sg = knight.GetComponent<UnityEngine.Rendering.SortingGroup>();
            var figSg = fig.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (figSg != null) { sg.sortingLayerName = figSg.sortingLayerName; sg.sortingOrder = figSg.sortingOrder; }
            else
            {
                int order = 0; string layer = "Default";
                foreach (var r in fig.GetComponentsInChildren<SpriteRenderer>(true)) if (r.sortingOrder > order) { order = r.sortingOrder; layer = r.sortingLayerName; }
                sg.sortingLayerName = layer; sg.sortingOrder = order;
            }
            known.Clear();
            foreach (Transform c in parent) known.Add(c.gameObject);
            ghosts.Clear();
            Info = "measuring";
            GameRoot.I.StartCoroutine(Follow(fig, clip.length));
            return "knight in the fight, material " + (mat ?? "own") + "; read DcFight.Info after two frames";
        }

        public static string Info { get; private set; } = "";
        static float armAt = -1;

        /// <summary>
        /// Start the knight's clip this many seconds of game time from now instead of on today's hero's wind-up (call in
        /// the same execute_code as BonesCapture.Take, whose press then comes later by the lead): so the knight's own
        /// contact frame lands on the game's contact frame without retiming the clip.
        /// </summary>
        public static string Arm(float seconds) { armAt = Time.time + seconds; return "armed at " + armAt; }

        static IEnumerator Follow(Figure fig, float length)
        {
            // size, measured once the skinned drawings are deformed: today's hero's body height (feet to the top of his
            // head, weapon left out) = the knight's, both standing; the knight's lowest drawn point on his feet point and
            // its body centre over his
            var parent = fig.transform.parent;
            foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var heroBody = Body(fig.gameObject, fig.Weapons);
            float feetY = fig.transform.position.y;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (knight == null) yield break;
            var kBody = Body(knight, new Transform[0]);
            float heroH = heroBody.HasValue ? heroBody.Value.max.y - feetY : 4.8f;
            float knightH = kBody.HasValue ? kBody.Value.size.y : 7f;
            float k = heroH / (knightH * parent.lossyScale.y) * SizeFix;
            knight.transform.localScale = new Vector3(k, k, 1);
            yield return null;
            yield return new WaitForEndOfFrame();
            if (knight == null) yield break;
            var kb = Body(knight, new Transform[0]).Value;
            var off = new Vector3((heroBody.HasValue ? heroBody.Value.center.x : fig.transform.position.x) - kb.center.x, feetY - kb.min.y, 0);
            var standOffset = parent.InverseTransformVector(off);
            Info = $"hero body {heroH:0.000} u (box {heroBody}), knight body {knightH:0.000} u, scale {k:0.0000}, offset {standOffset}";
            float t = -1;
            bool inStop = false;
            var go = knight;
            armAt = -1;
            ClipTime = -1;
            while (go != null && fig != null)
            {
                go.transform.localPosition = fig.transform.localPosition + standOffset;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(k, k, 1);
                foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) if (r.name != "shadow") r.enabled = false;
                bool start = armAt >= 0 ? Time.time >= armAt - 0.0001f : fig.PoseName != null && fig.PoseName.StartsWith("sword_wind");
                if (t < 0 && start) { t = StartAt; armAt = -1; }
                else if (t >= 0)
                {
                    // the battle's clock: hit stop (TimeScale) and battle speed (Speed); the hit stop's first frame
                    // still advances, so the strike shows on the contact frame and is then held for the stop
                    bool stop = Anim.TimeScale < 0.5f;
                    t += Time.deltaTime * (stop && !inStop ? 1 : Anim.TimeScale) * Anim.Speed;
                    inStop = stop;
                }
                ClipTime = t;
                play.SetTime(Mathf.Clamp(t, 0, length));
                graph.Evaluate(0);
                Ghosts(fig, go, standOffset);
                yield return null;
            }
        }

        /// <summary>The game's afterimages copy today's hero; each copy is hidden and the knight, as it stands now, is copied instead (tinted the same).</summary>
        static void Ghosts(Figure fig, GameObject kn, Vector3 standOffset)
        {
            foreach (Transform c in fig.transform.parent)
            {
                if (known.Contains(c.gameObject) || c.gameObject == kn || c.name.StartsWith("dc knight")) continue;
                if (c.GetComponent<Figure>() == null || ghosts.ContainsKey(c.gameObject)) continue;
                if (Mathf.Sign(c.localScale.x) != Mathf.Sign(fig.transform.localScale.x)) continue;
                var copy = Object.Instantiate(kn, kn.transform.parent);
                copy.name = "dc knight ghost";
                var a = copy.GetComponent<Animator>(); if (a != null) a.enabled = false;
                copy.transform.localPosition = c.localPosition + standOffset;
                copy.transform.localScale = kn.transform.localScale;
                copy.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = kn.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder - 1;
                ghosts[c.gameObject] = copy;
            }
            var gone = new List<GameObject>();
            foreach (var kv in ghosts)
            {
                if (kv.Key == null) { if (kv.Value != null) Object.Destroy(kv.Value); gone.Add(kv.Key); continue; }
                SpriteRenderer src = null;
                foreach (var r in kv.Key.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name != "shadow") { if (src == null && r.color.a > 0) src = r; r.enabled = false; }
                if (src != null && kv.Value != null) foreach (var r in kv.Value.GetComponentsInChildren<SpriteRenderer>(true)) r.color = src.color;
            }
            foreach (var g in gone) ghosts.Remove(g);
        }

        public static string Remove()
        {
            if (graph.IsValid()) graph.Destroy();
            if (knight != null) Object.Destroy(knight);
            knight = null;
            foreach (var g in ghosts.Values) if (g != null) Object.Destroy(g);
            ghosts.Clear();
            var fig = Fld(Fld(Screen, "hero"), "Fig") as Figure;
            if (fig != null) foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            return "removed";
        }

        /// <summary>Lossless game frames beside a BonesCapture.Take started in the same call: dir/p%03d.png for take frames from..to.</summary>
        public static string Png(string dir, int from, int to)
        {
            System.IO.Directory.CreateDirectory(dir);
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
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"p{i:000}.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            PngDone = dir;
        }

        public static string PngDone { get; private set; } = "";

        /// <summary>Capture points on the knight (BonesCapture track): sword tip side, feet, body box.</summary>
        public static string Track()
        {
            if (knight == null) return "no knight";
            BonesCapture.Box("knight", () => knight == null ? null : BonesCapture.BoundsOf(knight));
            BonesCapture.Value("clip_t", () => ClipTime);
            BonesCapture.Value("anim_scale", () => Anim.TimeScale);
            return BonesCapture.Follow(knight, "hand=hand_R; feet=dc knight");
        }
    }
}
