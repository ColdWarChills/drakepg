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

        // --- Job 1 (2026-11-25): our hero on the knight's skeleton, running in before the swing ---
        /// <summary>Dress the knight as today's hero (DcHero.Dress) once it is measured.</summary>
        public static bool Hero;
        /// <summary>
        /// Run in, then swing: from the arm moment the knight plays the shared run clip from home to the spot today's
        /// hero strikes from (StandDx design px ahead), moved at the clip's stride speed; there it blends into its own
        /// attack, untouched, in place; when the attack ends it runs home backwards (TurnBack: turned round) into its idle clip.
        /// Off: test 1's behaviour (the attack played in place wherever today's hero is).
        /// </summary>
        public static bool RunIn;
        public static float StandDx = 450f;
        /// <summary>Battle-clock seconds the run in takes (and the run home).</summary>
        public static float RunSeconds = 1.0f;
        /// <summary>Battle-clock seconds of the run home (shorter: home before the foe's turn).</summary>
        public static float RunBackSeconds = 0.7f;
        /// <summary>The run clip's ground covered per cycle (1 s), in the knight's own units: sets its playback rate.</summary>
        public static float StrideUnits = 7.1f;
        /// <summary>Off (default): he runs home backwards, still facing the foe (the run clip played in reverse), so the
        /// sword stays in his right hand as in today's game. On: he turns round and runs home (sword in his left).</summary>
        public static bool TurnBack;
        public static float Blend = 0.08f;
        public const string RunPath = Dir + "Animations/Characters/AnimClips/Shared_Bipeds/Anim_Character_Bipedal_Run.anim";
        public const string IdlePath = Dir + "Animations/Characters/AnimClips/Knight/Anim_Character_Knight_Idle.anim";
        /// <summary>The run-in phase now: wait, run, attack, back, home (for the track).</summary>
        public static string Phase { get; private set; } = "";
        /// <summary>Frames on which a body part of the dressed knight showed a knight drawing (DcHero.Strays), and the last ones.</summary>
        public static int StrayFrames { get; private set; }
        public static string LastStrays { get; private set; } = "";
        public static string Dressed { get; private set; } = "";
        static AnimationMixerPlayable mixer;
        static AnimationClipPlayable runPlay, idlePlay;
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
            if (RunIn)
            {
                // three of the sample's own clips, mixed by weight: idle, run, attack (no pose is made here)
                mixer = AnimationMixerPlayable.Create(graph, 3);
                idlePlay = AnimationClipPlayable.Create(graph, AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath));
                runPlay = AnimationClipPlayable.Create(graph, AssetDatabase.LoadAssetAtPath<AnimationClip>(RunPath));
                graph.Connect(idlePlay, 0, mixer, 0);
                graph.Connect(runPlay, 0, mixer, 1);
                graph.Connect(play, 0, mixer, 2);
                mixer.SetInputWeight(0, 1);
                output.SetSourcePlayable(mixer);
            }
            else output.SetSourcePlayable(play);
            play.SetTime(0);
            graph.Evaluate(0);
            StrayFrames = 0;
            LastStrays = "";
            Dressed = "";
            Phase = "";

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
            if (Hero) Dressed = DcHero.Dress(knight, knightH * SizeFix / Mathf.Abs(knight.transform.lossyScale.y));
            float k = heroH / (knightH * parent.lossyScale.y) * SizeFix;
            knight.transform.localScale = new Vector3(k, k, 1);
            yield return null;
            yield return new WaitForEndOfFrame();
            if (knight == null) yield break;
            var kb = Body(knight, new Transform[0]).Value;
            var off = new Vector3((heroBody.HasValue ? heroBody.Value.center.x : fig.transform.position.x) - kb.center.x, feetY - kb.min.y, 0);
            var standOffset = parent.InverseTransformVector(off);
            Info = $"hero body {heroH:0.000} u (box {heroBody}), knight body {knightH:0.000} u, scale {k:0.0000}, offset {standOffset}"
                + (Hero ? "; " + Dressed : "");
            if (RunIn)
            {
                var home = fig.transform.localPosition;
                GameRoot.I.StartCoroutine(RunInLoop(fig, knight, home, standOffset, k, length));
                yield break;
            }
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

        /// <summary>
        /// RunIn: the knight's own path (home -> stand point -> home) on the battle clock, independent of today's hero's
        /// dash; only the sample's clips are played (idle, run, attack), mixed by weight, and the root is moved and turned.
        /// Today's hero stays hidden, his afterimages too; the knight gets its own copy of his ground shadow.
        /// </summary>
        static IEnumerator RunInLoop(Figure fig, GameObject go, Vector3 home, Vector3 standOffset, float k, float attackLength)
        {
            var parent = fig.transform.parent;
            float dx = parent.InverseTransformVector(Design.World(StandDx, 0) - Design.World(0, 0)).x;
            var stand = home + new Vector3(dx, 0, 0);
            float distUnits = Mathf.Abs(dx) / k;
            float rate = distUnits / RunSeconds / StrideUnits;
            float runLen = runPlay.GetAnimationClip().length;
            Info += $"; run in {StandDx} px = {distUnits:0.00} knight units in {RunSeconds} s, run clip x{rate:0.00}";
            // the knight's own shadow: a copy of today's hero's, moved with the knight
            SpriteRenderer heroShadow = null;
            foreach (var r in fig.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name == "shadow") heroShadow = r;
            Transform shadow = null;
            Vector3 shadowRest = Vector3.zero;
            if (heroShadow != null)
            {
                shadowRest = heroShadow.transform.position;
                shadow = Object.Instantiate(heroShadow.gameObject, parent).transform;
                shadow.name = "dc knight shadow";
                shadow.position = shadowRest;
                shadow.localScale = Vector3.Scale(heroShadow.transform.lossyScale, new Vector3(1 / parent.lossyScale.x, 1 / parent.lossyScale.y, 1));
                ghosts[shadow.gameObject] = shadow.gameObject;   // removed with the knight
            }
            var homeWorld = parent.TransformPoint(home);
            float s = -1, idleT = 0;
            bool inStop = false;
            armAt = -1;
            ClipTime = -1;
            Phase = "wait";
            while (go != null && fig != null)
            {
                foreach (var r in fig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                HideGhosts(fig, go);
                bool stop = Anim.TimeScale < 0.5f;
                float dt = Time.deltaTime * (stop && !inStop ? 1 : Anim.TimeScale) * Anim.Speed;
                inStop = stop;
                bool start = armAt >= 0 ? Time.time >= armAt - 0.0001f : fig.PoseName != null && fig.PoseName.StartsWith("sword_wind");
                if (s < 0 && start) { s = 0; armAt = -1; }
                else if (s >= 0) s += dt;
                idleT += dt;
                float wIdle = 0, wRun = 0, wAtk = 0, runT = 0, atkT = 0;
                Vector3 at = home;   // where today's hero would be ("fig-equivalent"); the knight stands at this + offset
                bool back = false;   // turned round (TurnBack only)
                float R = RunSeconds;
                if (s < 0) { wIdle = 1; Phase = "wait"; }
                else if (s < R)
                {
                    Phase = "run";
                    float u = s / R;
                    at = Vector3.LerpUnclamped(home, stand, u);
                    runT = s * rate;
                    wRun = Mathf.Clamp01(s / Blend);
                    wIdle = 1 - wRun;
                }
                else if (s - R < attackLength)
                {
                    Phase = "attack";
                    atkT = s - R;
                    at = stand;
                    runT = s * rate;
                    wAtk = Mathf.Clamp01(atkT / Blend);
                    wRun = 1 - wAtk;
                    ClipTime = atkT;
                }
                else if (s - R - attackLength < RunBackSeconds)
                {
                    Phase = "back";
                    float b = s - R - attackLength;
                    back = TurnBack;
                    at = Vector3.LerpUnclamped(stand, home, b / RunBackSeconds);
                    runT = (TurnBack ? 1 : -1) * b * rate * R / RunBackSeconds;
                    atkT = attackLength;
                    wRun = Mathf.Clamp01(b / Blend);
                    wAtk = 1 - wRun;
                    ClipTime = attackLength;
                }
                else
                {
                    Phase = "home";
                    float h = s - R - RunBackSeconds - attackLength;
                    wIdle = Mathf.Clamp01(h / Blend);
                    wRun = 1 - wIdle;
                    runT = (TurnBack ? 1 : -1) * (RunBackSeconds + h) * rate * R / RunBackSeconds;
                    ClipTime = attackLength;
                }
                // turned round on the way home: mirrored about the body's centre, so the turn does not jump
                var off = back ? new Vector3(-standOffset.x, standOffset.y, standOffset.z) : standOffset;
                go.transform.localPosition = at + off;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(back ? -k : k, k, 1);
                idlePlay.SetTime(idleT % idlePlay.GetAnimationClip().length);
                runPlay.SetTime(((runT % runLen) + runLen) % runLen);
                play.SetTime(Mathf.Clamp(atkT, 0, attackLength));
                mixer.SetInputWeight(0, wIdle);
                mixer.SetInputWeight(1, wRun);
                mixer.SetInputWeight(2, wAtk);
                graph.Evaluate(0);
                if (shadow != null)
                {
                    var eq = parent.TransformPoint(at);
                    shadow.position = new Vector3(shadowRest.x + (eq.x - homeWorld.x), shadowRest.y, shadowRest.z);
                }
                if (Hero)
                {
                    var strays = DcHero.Strays(go);
                    if (strays.Count > 0) { StrayFrames++; LastStrays = string.Join(", ", strays.ToArray()); }
                }
                yield return null;
            }
        }

        /// <summary>RunIn: today's hero's afterimages (Figure copies made after the knight came in) are hidden, not copied.</summary>
        static void HideGhosts(Figure fig, GameObject kn)
        {
            foreach (Transform c in fig.transform.parent)
            {
                if (known.Contains(c.gameObject) || c.gameObject == kn || c.name.StartsWith("dc knight")) continue;
                if (c.GetComponent<Figure>() == null) continue;
                if (Mathf.Sign(c.localScale.x) != Mathf.Sign(fig.transform.localScale.x)) continue;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
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
            DcHero.Clear();
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
            BonesCapture.Watch("phase", () => Phase);
            return BonesCapture.Follow(knight, "hand=hand_R; feet=dc knight");
        }
    }
}
