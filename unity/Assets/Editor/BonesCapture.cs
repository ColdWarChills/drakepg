using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Drakepg.Rules;
using Drakepg.Test;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// The judging kit's recorder (scripts/bones_sheets.py makes the pictures). Works on any GameObject from outside,
    /// with no change to the code that animates it: follow named transforms (weapon tip, grip, hands, joints) and log
    /// them per recorded frame through BonesTrack; watch values and log an event when they change; record a clean
    /// pass (the actor alone on a flat key colour, everything else hidden and put back after). Frames come from
    /// Drive.Record. Typical use, all in one execute_code call (play mode):
    /// <code>
    /// BonesCapture.Reset(); BonesCapture.Bones(hero); BonesCapture.Take(dir, 105, 30);
    /// </code>
    /// then, after the take is done (BonesCapture.Busy false), the same setup again with Clean(dir + "/clean", hero, 105, 30).
    /// </summary>
    public static class BonesCapture
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        /// <summary>The clean pass's key colour (magenta: not in the hero's skin, cloth, metal or wood).</summary>
        public static readonly Color Key = new Color(1, 0, 1, 1);

        static readonly List<KeyValuePair<string, Func<Vector3?>>> points = new List<KeyValuePair<string, Func<Vector3?>>>();
        static readonly List<KeyValuePair<string, Func<Bounds?>>> boxes = new List<KeyValuePair<string, Func<Bounds?>>>();
        static readonly List<KeyValuePair<string, Func<object>>> values = new List<KeyValuePair<string, Func<object>>>();
        static readonly List<KeyValuePair<string, Func<string>>> watches = new List<KeyValuePair<string, Func<string>>>();
        static readonly List<KeyValuePair<string, float>> schedule = new List<KeyValuePair<string, float>>();

        /// <summary>A take is recording.</summary>
        public static bool Busy { get; private set; }
        /// <summary>What the last take reported (frames, file).</summary>
        public static string Last { get; private set; }

        /// <summary>Forget every point, box, value, watch and scheduled event.</summary>
        public static string Reset()
        {
            points.Clear();
            boxes.Clear();
            values.Clear();
            watches.Clear();
            schedule.Clear();
            return "reset";
        }

        public static void Point(string name, Func<Vector3?> at) => points.Add(new KeyValuePair<string, Func<Vector3?>>(name, at));
        public static void Box(string name, Func<Bounds?> box) => boxes.Add(new KeyValuePair<string, Func<Bounds?>>(name, box));
        public static void Value(string name, Func<object> value) => values.Add(new KeyValuePair<string, Func<object>>(name, value));
        /// <summary>Log an event "name:value" on the first frame, and on every frame the value differs from the frame before.</summary>
        public static void Watch(string name, Func<string> value) => watches.Add(new KeyValuePair<string, Func<string>>(name, value));

        /// <summary>Events at game times from the take's start, e.g. "wind@0.6 strike@1.04 back@1.76".</summary>
        public static void At(string steps)
        {
            foreach (var step in steps.Split(' '))
            {
                var p = step.Split('@');
                if (p.Length == 2) schedule.Add(new KeyValuePair<string, float>(p[0], float.Parse(p[1], C)));
            }
        }

        /// <summary>A transform under `root` by name (depth first; the first one found).</summary>
        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// Follow transforms under `root`: "name=transform; name2=transform@x,y; tip=near weapon#tip; ..." (a local
        /// offset in the transform's own units; #tip: the opaque pixel of that transform's sprite furthest from the
        /// transform it hangs on, for a weapon whose pivot is the grip). Returns what was found.
        /// </summary>
        public static string Follow(GameObject root, string spec)
        {
            var found = new List<string>();
            foreach (var raw in spec.Split(';'))
            {
                var part = raw.Trim();
                if (part == "") continue;
                var kv = part.Split('=');
                var name = kv[0].Trim();
                var target = kv.Length > 1 ? kv[1].Trim() : name;
                bool tip = target.EndsWith("#tip");
                if (tip) target = target.Substring(0, target.Length - 4);
                var local = Vector3.zero;
                var at = target.IndexOf('@');
                if (at >= 0)
                {
                    var n = target.Substring(at + 1).Split(',');
                    local = new Vector3(float.Parse(n[0], C), float.Parse(n[1], C), 0);
                    target = target.Substring(0, at);
                }
                var t = Find(root.transform, target);
                if (t == null) { found.Add(name + "=MISSING"); continue; }
                if (tip)
                {
                    var sr = t.GetComponent<SpriteRenderer>();
                    var tl = sr != null ? SpriteTip(sr, t.position) : (Vector3?)null;
                    if (tl == null) { found.Add(name + "=NO SPRITE"); continue; }
                    local = tl.Value;
                }
                var off = local;
                Point(name, () => t == null ? (Vector3?)null : t.TransformPoint(off));
                found.Add(name);
            }
            return string.Join(", ", found);
        }

        /// <summary>
        /// The opaque pixel of a sprite furthest from `from` (world), in the sprite renderer's local space: the far end
        /// of a weapon whose grip is at `from`. Reads the texture through the GPU, so non-readable art works too.
        /// </summary>
        public static Vector3? SpriteTip(SpriteRenderer sr, Vector3 from)
        {
            var s = sr.sprite;
            if (s == null) return null;
            var tex = s.texture;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32();
            UnityEngine.Object.DestroyImmediate(copy);
            var r = s.textureRect;
            var fromLocal = sr.transform.InverseTransformPoint(from);
            float best = -1;
            Vector3 tip = Vector3.zero;
            for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                {
                    if (px[y * tex.width + x].a < 128) continue;
                    var l = new Vector3((x - r.xMin + 0.5f - s.pivot.x) / s.pixelsPerUnit, (y - r.yMin + 0.5f - s.pivot.y) / s.pixelsPerUnit, 0);
                    var d = (l - fromLocal).sqrMagnitude;
                    if (d > best) { best = d; tip = l; }
                }
            return best < 0 ? (Vector3?)null : tip;
        }

        /// <summary>The union of a root's enabled renderers' bounds (skipping shadows and trails).</summary>
        public static Bounds? BoundsOf(GameObject root)
        {
            if (root == null) return null;
            Bounds? all = null;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || Skipped(r)) continue;
                if (r is SpriteRenderer sr && sr.sprite == null) continue;
                if (all == null) all = r.bounds;
                else { var b = all.Value; b.Encapsulate(r.bounds); all = b; }
            }
            return all;
        }

        static bool Skipped(Renderer r) => r.name == "shadow" || r.name.Contains("trail");

        /// <summary>The clocks: game time, the hit-stop scale and the battle speed (Anim runs on Time.deltaTime x both).</summary>
        static void Clocks()
        {
            Value("time", () => Time.time);
            Value("anim_scale", () => Anim.TimeScale);
            Value("anim_speed", () => Anim.Speed);
            Watch("hitstop", () => Anim.TimeScale < 0.5f ? "on" : "off");
        }

        /// <summary>
        /// The hero on bones (LayeredHero, SkeletonSwing): weapon tip, the weapon's grip, the hand's own grip point (from
        /// the hand bone and the rig's grip, so it does not just repeat where the weapon is put), every joint (each part
        /// sits on its joint), the hero's box.
        /// </summary>
        public static string Bones(GameObject hero)
        {
            var rig = LayeredHero.RigJson;
            var j = rig["joints"];
            var g = rig["grips"]["n"]["at"];
            float u = LayeredHero.Unit;
            float gx = ((float)g[0] - (float)j["n_wrist"][0]) * u, gy = -((float)g[1] - (float)j["n_wrist"][1]) * u;
            var gf = rig["grips"]["f"]["at"];
            float fx = ((float)gf[0] - (float)j["f_wrist"][0]) * u, fy = -((float)gf[1] - (float)j["f_wrist"][1]) * u;
            var spec = "tip=near weapon#tip; grip=near weapon; hand_grip=n_hand@" + gx.ToString(C) + "," + gy.ToString(C)
                + "; far_grip=f_hand@" + fx.ToString(C) + "," + fy.ToString(C);
            foreach (var p in rig["parts"])
                spec += "; j_" + (string)p["joint"] + "=" + (string)p["name"];
            var found = Follow(hero, spec);
            Box("hero", () => BoundsOf(hero));
            Clocks();
            var face = Find(hero.transform, "face");
            if (face != null)
            {
                var sr = face.GetComponent<SpriteRenderer>();
                Watch("face", () => sr != null && sr.enabled && sr.sprite != null ? sr.sprite.texture.name + sr.sprite.rect.width : "none");
            }
            return found;
        }

        /// <summary>
        /// The hero on bones that SkeletonSwingDemo.Stage() made last (read from the demo, since a Find by name can
        /// still meet the one being destroyed); also the drawn hero beside it.
        /// </summary>
        public static GameObject SwingHero() =>
            (typeof(SkeletonSwingDemo).GetField("hero", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Component)?.gameObject;

        public static GameObject SwingToday() =>
            (typeof(SkeletonSwingDemo).GetField("today", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Component)?.gameObject;

        static object Screen => GameRoot.I != null ? GameRoot.I.Screen : null;
        static object F(object o, string name) => o?.GetType().GetField(name, Any)?.GetValue(o);
        static float Num(object o, string name) => o != null && F(o, name) is float v ? v : float.NaN;

        /// <summary>
        /// A fight on the battle screen (today's drawn hero): the weapon pivot (grip), the drawn weapon picture's far end
        /// (tip), the game's own glint point (tip_game: pivot + up x reach, BattleScreen.BladeTip), the feet, boxes for hero and
        /// foe; values for the hero's place, tilt and squash, the foe's HP; events when the drawn pose or the foe's HP
        /// changes.
        /// </summary>
        public static string Battle()
        {
            var hero = F(Screen, "hero");
            var foe = F(Screen, "foe");
            var fig = F(hero, "Fig") as Figure;
            var foeFig = F(foe, "Fig") as Figure;
            if (fig == null) return "no battle hero";
            var found = new List<string>();
            if (fig.Weapons.Count > 0 && fig.Weapons[0] != null)
            {
                var pivot = fig.Weapons[0];
                Point("grip", () => pivot == null ? (Vector3?)null : pivot.position);
                // The game's own glint point (shorter than the drawn blade); "tip" below is the picture's real far end.
                Point("tip_game", () =>
                {
                    if (pivot == null) return null;
                    var reach = fig.DrawnHeight * Num(hero, "Base") * 0.38f / Design.PixelsPerUnit;
                    return pivot.position + pivot.up * reach;
                });
                // The weapon picture behind the body (order 5 under "held"): its far end from the grip.
                foreach (var sr in pivot.GetComponentsInChildren<SpriteRenderer>(true))
                    if (!sr.name.EndsWith(" grip"))
                    {
                        var tl = SpriteTip(sr, pivot.position);
                        var s = sr;
                        if (tl != null) Point("tip", () => s == null ? (Vector3?)null : s.transform.TransformPoint(tl.Value));
                        found.Add("tip");
                        break;
                    }
                found.Add("grip, tip_game");
            }
            Point("feet", () => fig == null ? (Vector3?)null : fig.transform.position);
            Box("hero", () => fig == null ? null : BoundsOf(fig.gameObject));
            if (foeFig != null) Box("foe", () => foeFig == null ? null : BoundsOf(foeFig.gameObject));
            Clocks();
            Value("hero_x", () => Num(hero, "X"));
            Value("hero_y", () => Num(hero, "Y"));
            Value("hero_angle", () => Num(hero, "Angle"));
            Value("hero_sx", () => Num(hero, "Sx"));
            Value("hero_sy", () => Num(hero, "Sy"));
            Value("foe_x", () => Num(foe, "X"));
            Watch("pose", () => fig == null ? "gone" : fig.PoseName ?? "rest");
            Func<string> foeHp = () => F(Screen, "battle") is Drakepg.Rules.Battle b ? b.Enemy.Hp.ToString() : "?";
            Watch("foe_hp", foeHp);
            Value("foe_hp", () => F(Screen, "battle") is Drakepg.Rules.Battle b ? (object)b.Enemy.Hp : null);
            Watch("state", () => F(Screen, "battle") is Drakepg.Rules.Battle b ? b.State : "?");
            found.Add("feet, boxes hero/foe, values, watches pose/foe_hp/state");
            return string.Join(", ", found);
        }

        /// <summary>
        /// Record `frames` pictures 1/fps s of game time apart into `dir` (Drive.Record: f000.jpg ...) and, frame for
        /// frame, the track file dir/track.json with everything set up by Follow/Point/Box/Value/Watch/At. `presses`:
        /// battle buttons to press at game times from the start, "0@0.2" or "1@0.2 0@0.5" (Skills, then its first);
        /// a press shows from the frame it is logged on.
        /// </summary>
        public static string Take(string dir, int frames, int fps = 30, int width = 1280, string presses = null)
        {
            if (Busy) return "busy";
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f*.jpg")) File.Delete(f);
            Busy = true;
            var list = new List<KeyValuePair<int, int>>();
            if (!string.IsNullOrEmpty(presses))
                foreach (var step in presses.Split(' '))
                {
                    var p = step.Split('@');
                    if (p.Length == 2) list.Add(new KeyValuePair<int, int>(Mathf.RoundToInt(float.Parse(p[1], C) * fps), int.Parse(p[0])));
                }
            Drive.Record(dir, frames, fps, width);
            GameRoot.I.StartCoroutine(TrackLoop(dir, frames, fps, width, width * 9 / 16, list));
            return $"recording {frames} frames to {dir}";
        }

        public static string Take(string dir, int frames, int fps, int width, int press, float pressAt) =>
            Take(dir, frames, fps, width, press >= 0 ? press + "@" + pressAt.ToString(C) : null);

        /// <summary>
        /// The clean pass: the same take with only `actor` drawn, on the key colour (every other renderer and the UI
        /// hidden while it records, then put back exactly). Start the move again the same way before calling it.
        /// </summary>
        public static string Clean(string dir, GameObject actor, int frames, int fps = 30, int width = 1280, string presses = null)
        {
            if (Busy) return "busy";
            Hide(actor);
            var r = Take(dir, frames, fps, width, presses);
            GameRoot.I.StartCoroutine(RestoreAfter());
            return r + " (clean)";
        }

        static IEnumerator RestoreAfter()
        {
            while (Busy) yield return null;
            Restore();
        }

        static IEnumerator TrackLoop(string dir, int frames, int fps, int width, int height, List<KeyValuePair<int, int>> presses)
        {
            BonesTrack.Begin(Path.Combine(dir, "track.json"), fps, width, height);
            var last = new Dictionary<string, string>();
            var fired = new HashSet<int>();
            float t0 = Time.time;
            for (int i = 0; i < frames; i++)
            {
                // Pressed after the frame before was captured and logged: it shows (and is logged) from frame i.
                foreach (var p in presses)
                    if (p.Key == i)
                    {
                        BonesTrack.Event("press:" + p.Value + (Drive.Press(p.Value) ? "" : " (refused)"));
                    }
                yield return new WaitForEndOfFrame();
                float t = Time.time - t0;
                for (int k = 0; k < schedule.Count; k++)
                    if (!fired.Contains(k) && t + 0.5f / fps >= schedule[k].Value)
                    {
                        fired.Add(k);
                        BonesTrack.Event(schedule[k].Key);
                    }
                foreach (var p in points)
                {
                    Vector3? at = null;
                    try { at = p.Value(); } catch (Exception) { }
                    if (at.HasValue) BonesTrack.Point(p.Key, at.Value);
                }
                foreach (var b in boxes)
                {
                    Bounds? box = null;
                    try { box = b.Value(); } catch (Exception) { }
                    if (box.HasValue) BonesTrack.Box(b.Key, box.Value);
                }
                foreach (var v in values)
                {
                    object o = null;
                    try { o = v.Value(); } catch (Exception) { }
                    if (o is float f) { if (!float.IsNaN(f)) BonesTrack.Value(v.Key, f); }
                    else if (o is int n) BonesTrack.Value(v.Key, n);
                    else if (o != null) BonesTrack.Value(v.Key, o.ToString());
                }
                foreach (var w in watches)
                {
                    string s = null;
                    try { s = w.Value(); } catch (Exception) { }
                    if (!last.TryGetValue(w.Key, out var was) || was != s) BonesTrack.Event(w.Key + ":" + s);
                    last[w.Key] = s;
                }
                BonesTrack.Next(t);
            }
            var file = BonesTrack.End();
            Last = $"{frames} frames, track {file}";
            Debug.Log("drakepg: bones capture " + Last);
            Busy = false;
        }

        // --- The clean pass: hide everything but the actor, per camera render (so things made mid-take hide too) ---

        static GameObject cleanActor;
        static readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        static DisplayStyle uiWas;
        static UIDocument ui;
        static CameraClearFlags clearWas;
        static Color backWas;
        static bool hiding;

        /// <summary>Show only `actor` (minus its shadow and trails) on the key colour until Restore().</summary>
        public static string Hide(GameObject actor)
        {
            if (hiding) Restore();
            cleanActor = actor;
            hiding = true;
            var cam = Camera.main;
            clearWas = cam.clearFlags;
            backWas = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Key;
            ui = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            if (ui != null && ui.rootVisualElement != null)
            {
                uiWas = ui.rootVisualElement.style.display.value;
                ui.rootVisualElement.style.display = DisplayStyle.None;
            }
            HideNow();
            RenderPipelineManager.beginContextRendering += BeforeRender;
            return "hiding all but " + actor.name;
        }

        static void BeforeRender(ScriptableRenderContext ctx, List<Camera> cams) => HideNow();

        static void HideNow()
        {
            if (!hiding) return;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool keep = cleanActor != null && r.transform.IsChildOf(cleanActor.transform) && !Skipped(r);
                if (keep) continue;
                if (!hidden.ContainsKey(r)) hidden[r] = r.enabled;
                r.enabled = false;
            }
        }

        /// <summary>Put back everything Hide changed (renderers, UI, camera background).</summary>
        public static string Restore()
        {
            RenderPipelineManager.beginContextRendering -= BeforeRender;
            int n = 0;
            foreach (var kv in hidden)
                if (kv.Key != null) { kv.Key.enabled = kv.Value; n++; }
            hidden.Clear();
            if (hiding)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = clearWas;
                    cam.backgroundColor = backWas;
                }
                if (ui != null && ui.rootVisualElement != null) ui.rootVisualElement.style.display = uiWas;
            }
            hiding = false;
            cleanActor = null;
            Time.captureFramerate = 0;
            return $"restored {n} renderers";
        }
    }
}
