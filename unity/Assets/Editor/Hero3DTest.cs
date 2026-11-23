using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Drakepg.Hero3D;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Test of the 3D hero in the fight (Assets/Hero3D): in play mode,
    /// <c>Drakepg.EditorTools.Hero3DTest.Run()</c> starts a fight through Drive, freezes it, saves the fight as it is
    /// (fight_without.png), hides the drawn hero (at runtime only), shows the code-made stand-in at his spot through
    /// Hero3DView, and saves standin_fight.png, standin_3x.png (the figure's area at 3x) and a short report in the log
    /// (pixels changed outside the figure's box, where the two heroes' feet and heads are, frame times with and without).
    /// <c>Direct()</c> instead puts the stand-in straight into the scene through the 2D renderer (no RenderTexture), for
    /// comparison. <c>Clear()</c> takes the test away and shows the drawn hero again. Nothing is saved into a scene.
    /// </summary>
    public static partial class Hero3DTest
    {
        public const string DefaultDir = "C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port/art/tests/hero3d_v2/unity";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const float GroundY = 500;

        static Hero3DView view;
        static GameObject direct;
        static Material material;
        static Texture2D face;
        public static string LastReport = "";

        static GameScreen Screen => GameRoot.I != null ? GameRoot.I.Screen : null;

        static Figure HeroFigure()
        {
            var hero = Drive.Field("hero");
            return hero?.GetType().GetField("Fig", Any)?.GetValue(hero) as Figure;
        }

        static Material ToonMaterial()
        {
            if (material != null) return material;
            // An instance of the material asset (Resources, so builds include it and its shader); the face goes on the copy.
            material = new Material(Resources.Load<Material>("Hero3D/Hero3DToon")) { name = "Hero3DToon (test)" };
            face = StandinBuilder.Face();
            material.mainTexture = face;
            return material;
        }

        /// <summary>The whole test (play mode).</summary>
        public static string Run(string dir = null, string classId = "warrior")
        {
            if (!Application.isPlaying || GameRoot.I == null) return "enter play mode first";
            GameRoot.I.StartCoroutine(RunLoop(dir ?? DefaultDir, classId));
            return "started";
        }

        static IEnumerator RunLoop(string dir, string classId)
        {
            Directory.CreateDirectory(dir);
            yield return StartFight(classId);
            Freeze(true);
            yield return Frames(3);
            var without = Path.Combine(dir, "fight_without.png");
            GameRoot.I.Snapshot(without);
            yield return Frames(3);

            var fig = HeroFigure();
            fig.gameObject.SetActive(false);
            yield return Frames(3);
            var hidden = Path.Combine(dir, "fight_hidden.png");
            GameRoot.I.Snapshot(hidden);
            yield return Frames(3);

            Show(fig);
            yield return Frames(4);
            var with = Path.Combine(dir, "standin_fight.png");
            GameRoot.I.Snapshot(with);
            yield return Frames(4);

            var report = new List<string>();
            report.Add(Check(without, hidden, with, dir));
            // Frame cost: unfrozen, 240 frames each way.
            Freeze(false);
            yield return Frames(30);
            var on = new Timing();
            yield return on.Measure(240, view);
            view.gameObject.SetActive(false);
            yield return Frames(30);
            var off = new Timing();
            yield return off.Measure(240, null);
            view.gameObject.SetActive(true);
            report.Add($"frame with figure: {on}; without: {off}; Hero3DView CPU (bake + queue) {on.ViewMs:0.000} ms; texture {view.Texture.width}x{view.Texture.height}");
            LastReport = string.Join("\n", report);
            Debug.Log("drakepg: hero3d " + LastReport);
            File.WriteAllText(Path.Combine(dir, "report.txt"), LastReport);
        }

        /// <summary>The stand-in in the scene, drawn by the main camera through the 2D renderer (play mode, in a fight).</summary>
        public static string Direct(string dir = null)
        {
            if (!Application.isPlaying || GameRoot.I == null) return "enter play mode first";
            GameRoot.I.StartCoroutine(DirectLoop(dir ?? DefaultDir));
            return "started";
        }

        static IEnumerator DirectLoop(string dir)
        {
            Directory.CreateDirectory(dir);
            if (!(Screen is BattleScreen)) yield return StartFight("warrior");
            Freeze(true);
            Clear();
            var fig = HeroFigure();
            fig.gameObject.SetActive(false);
            direct = new GameObject("Hero3D direct test");
            direct.transform.SetParent(Screen.World, false);
            var holder = new GameObject("model").transform;
            holder.SetParent(direct.transform, false);
            var smr = StandinBuilder.Build(holder, ToonMaterial());
            smr.updateWhenOffscreen = true;
            // Turned three-quarters toward the right like Hero3DView, 240 design px tall times the figure's scale, its
            // depth behind the sprites' z = 0 so later sprites (effects) still pass the depth test over it.
            holder.localRotation = Quaternion.Euler(0, 130, 0);
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var top = 0f;
            foreach (var v in mesh.vertices) top = Mathf.Max(top, v.y);
            Object.Destroy(mesh);
            var scale = Mathf.Abs(fig.transform.lossyScale.y) * 240 / Design.PixelsPerUnit / Mathf.Max(top, 1);
            direct.transform.position = new Vector3(fig.transform.position.x, fig.transform.position.y, 3);
            direct.transform.localScale = Vector3.one * scale;
            smr.sortingOrder = Design.Depth(GroundY);
            Shader.SetGlobalVector("_Hero3DPixel", Vector4.zero);
            yield return Frames(4);
            var shot = Path.Combine(dir, "direct_1x.png");
            GameRoot.I.Snapshot(shot);
            yield return Frames(4);
            var b = smr.bounds;
            Crop3x(shot, Path.Combine(dir, "direct_3x.png"), b.min, b.max);
            Debug.Log("drakepg: hero3d direct shot saved");
        }

        /// <summary>The stand-in through Hero3DView, standing where `fig` stands (which stays hidden).</summary>
        static void Show(Figure fig)
        {
            var holder = new GameObject("standin holder").transform;
            StandinBuilder.Build(holder, ToonMaterial());
            view = Hero3DView.Create(Screen.World, holder, Design.Depth(GroundY));
            view.PixelsPerUnit = Design.PixelsPerUnit;
            view.DrawnHeight = Figure.ArtHeight;
            view.ScreenCamera = GameRoot.I.Camera;
            view.Follow = fig.transform;
        }

        /// <summary>Take the test away and show the drawn hero again.</summary>
        public static string Clear()
        {
            if (view != null) Object.Destroy(view.gameObject);
            if (direct != null) Object.Destroy(direct);
            view = null;
            direct = null;
            realClip?.Dispose();
            realClip = null;
            realModel = null;
            var fig = Screen is BattleScreen ? HeroFigure() : null;
            if (fig != null) fig.gameObject.SetActive(true);
            Freeze(false);
            return "cleared";
        }

        /// <summary>Flash the view (for checking the hook): solid `color` for a moment.</summary>
        public static string Flash(float r = 1, float g = 1, float b = 1)
        {
            if (view == null) return "no view";
            view.SetTint(new Color(r, g, b));
            view.SetFlash(1);
            return "flashing";
        }

        static IEnumerator StartFight(string classId)
        {
            if (!(Screen is BattleScreen))
            {
                if (!Drakepg.Rules.Session.HasPlayer) Drive.NewGame(classId, 1);
                yield return new WaitForSecondsRealtime(1f);
                Drive.Fight("burrow_rat", "road", "road_mill");
            }
            // Until the intro is over (the hero walked in, the menu is up).
            for (int i = 0; i < 600; i++)
            {
                if (Screen is BattleScreen && Drive.Field("busy") is bool busy && !busy) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.8f);
        }

        static void Freeze(bool on)
        {
            Anim.TimeScale = on ? 0 : 1;
            Time.timeScale = on ? 0 : 1;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForEndOfFrame();
            yield return null;
        }

        class Timing
        {
            public float AvgMs, WorstMs, GpuMs, ViewMs, MainMs, RenderMs;
            public IEnumerator Measure(int frames, Hero3DView v)
            {
                float sum = 0, worst = 0, gpu = 0, viewSum = 0, main = 0, render = 0;
                int gpuN = 0, cpuN = 0;
                var ft = new FrameTiming[1];
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    var dt = Time.unscaledDeltaTime * 1000;
                    sum += dt;
                    worst = Mathf.Max(worst, dt);
                    if (v != null) viewSum += v.LastCpuMs;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, ft) > 0)
                    {
                        if (ft[0].gpuFrameTime > 0) { gpu += (float)ft[0].gpuFrameTime; gpuN++; }
                        if (ft[0].cpuMainThreadFrameTime > 0) { main += (float)ft[0].cpuMainThreadFrameTime; render += (float)ft[0].cpuRenderThreadFrameTime; cpuN++; }
                    }
                }
                AvgMs = sum / frames;
                WorstMs = worst;
                GpuMs = gpuN > 0 ? gpu / gpuN : -1;
                ViewMs = viewSum / frames;
                MainMs = cpuN > 0 ? main / cpuN : -1;
                RenderMs = cpuN > 0 ? render / cpuN : -1;
            }
            public override string ToString() => $"avg {AvgMs:0.00} ms (worst {WorstMs:0.0}), GPU {(GpuMs < 0 ? "n/a" : GpuMs.ToString("0.00") + " ms")}, CPU main thread {(MainMs < 0 ? "n/a" : MainMs.ToString("0.00") + " ms")}, render thread {(RenderMs < 0 ? "n/a" : RenderMs.ToString("0.00") + " ms")}";
        }

        /// <summary>
        /// Compare the shots: pixels changed outside the figure's box (should be none), where the drawn hero and the
        /// stand-in are (bounding boxes of what changed when each was hidden or shown), and the 3x crop.
        /// </summary>
        static string Check(string without, string hidden, string with, string dir)
        {
            var a = Load(without);
            var h = Load(hidden);
            var w = Load(with);
            if (a == null || h == null || w == null) return "shots missing";
            var cam = GameRoot.I.Camera;
            var pb = view.Picture.bounds;
            var lo = cam.WorldToScreenPoint(pb.min);
            var hi = cam.WorldToScreenPoint(pb.max);
            var sb = view.Shadow.bounds;
            lo = Vector3.Min(lo, cam.WorldToScreenPoint(sb.min));
            hi = Vector3.Max(hi, cam.WorldToScreenPoint(sb.max));
            int outside = 0;
            var drawn = new RectInt();
            var standin = new RectInt();
            bool hasDrawn = false, hasStandin = false;
            for (int y = 0; y < a.height; y++)
                for (int x = 0; x < a.width; x++)
                {
                    var i = y * a.width + x;
                    var ca = a.px[i];
                    var ch = h.px[i];
                    var cw = w.px[i];
                    bool inBox = x >= lo.x - 1 && x <= hi.x + 1 && y >= lo.y - 1 && y <= hi.y + 1;
                    if (Diff(ca, ch)) Grow(ref drawn, ref hasDrawn, x, y);
                    if (Diff(ch, cw))
                    {
                        Grow(ref standin, ref hasStandin, x, y);
                        if (!inBox) outside++;
                    }
                }
            Crop3x(with, Path.Combine(dir, "standin_3x.png"), pb.min, pb.max);
            // Rows from the top, like the design space.
            string Rows(RectInt r) => $"x {r.xMin}..{r.xMax}, top {a.height - 1 - r.yMax}, feet {a.height - 1 - r.yMin}";
            return $"shot {a.width}x{a.height}; changed pixels outside the figure's box: {outside}; drawn hero {(hasDrawn ? Rows(drawn) : "none")}; stand-in {(hasStandin ? Rows(standin) : "none")} (includes its shadow)";
        }

        static bool Diff(Color32 p, Color32 q) => Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b) > 6;

        static void Grow(ref RectInt r, ref bool has, int x, int y)
        {
            if (!has) { r = new RectInt(x, y, 0, 0); has = true; return; }
            r.xMin = Mathf.Min(r.xMin, x);
            r.yMin = Mathf.Min(r.yMin, y);
            r.xMax = Mathf.Max(r.xMax, x);
            r.yMax = Mathf.Max(r.yMax, y);
        }

        class Shot { public int width, height; public Color32[] px; }

        static Shot Load(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            var s = new Shot { width = t.width, height = t.height, px = t.GetPixels32() };
            Object.Destroy(t);
            return s;
        }

        /// <summary>The screen area of world box min..max (plus a margin) from `src`, scaled up 3x with hard pixels.</summary>
        static void Crop3x(string src, string dst, Vector3 min, Vector3 max)
        {
            var s = Load(src);
            if (s == null) return;
            var cam = GameRoot.I.Camera;
            var lo = cam.WorldToScreenPoint(min);
            var hi = cam.WorldToScreenPoint(max);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)) - 8, 0, s.width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)) + 8, 0, s.width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.y, hi.y)) - 8, 0, s.height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lo.y, hi.y)) + 8, 0, s.height);
            int w = x1 - x0, h = y1 - y0;
            var big = new Texture2D(w * 3, h * 3, TextureFormat.RGBA32, false);
            var px = new Color32[w * 3 * h * 3];
            for (int y = 0; y < h * 3; y++)
                for (int x = 0; x < w * 3; x++)
                    px[y * w * 3 + x] = s.px[(y0 + y / 3) * s.width + x0 + x / 3];
            big.SetPixels32(px);
            big.Apply();
            File.WriteAllBytes(dst, big.EncodeToPNG());
            Object.Destroy(big);
        }
    }
}
