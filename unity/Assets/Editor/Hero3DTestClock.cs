using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Drakepg.Hero3D;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// The real hero's clip on the battle's own clock, and what drawing him costs (play mode, after RunReal):
    /// <c>Hero3DTest.Clock()</c> plays TestSwing through an Anim tween (so Anim.Speed, slow motion and hit-stops scale it)
    /// at x1, x2, in the game's own "grand" finishing-blow chain (HitStop 170 + SlowMo 0.3 for 950 ms), in steady slow
    /// motion, and across a full stop, and writes clock.txt. <c>Hero3DTest.Cost()</c> times BakeMesh against
    /// cmd.DrawRenderer, checks DrawRenderer's picture against BakeMesh's on a moving pose, and times frames with and
    /// without the hero (cost.txt). <c>Hero3DTest.RunAll()</c>: RunReal, then Clock, then Cost.
    /// </summary>
    public static partial class Hero3DTest
    {
        static string clockReport = "", costReport = "";

        public static string RunAll(string dir = null)
        {
            if (!Application.isPlaying || GameRoot.I == null) return "enter play mode first";
            GameRoot.I.StartCoroutine(RunAllLoop(dir ?? DefaultDir));
            return "started";
        }

        static IEnumerator RunAllLoop(string dir)
        {
            yield return RunRealLoop(dir, RealPrefab, "Stance", "TestSwing");
            yield return ClockLoop(dir);
            yield return CostLoop(dir);
            Debug.Log("drakepg: hero3d all done");
        }

        public static string Clock(string dir = null)
        {
            if (view == null || realModel == null) return "run RunReal first";
            GameRoot.I.StartCoroutine(ClockLoop(dir ?? DefaultDir));
            return "started";
        }

        public static string Cost(string dir = null)
        {
            if (view == null || realModel == null) return "run RunReal first";
            GameRoot.I.StartCoroutine(CostLoop(dir ?? DefaultDir));
            return "started";
        }

        /// <summary>
        /// Play `clip` on the battle clock: an Anim tween over frames / fps seconds of battle time (each frame shown
        /// 1 / fps), setting the clip's time. The tween belongs to the current screen, so it stops with it.
        /// </summary>
        public static Anim.Handle PlayOnBattleClock(Hero3DClip clip, int repeat = 0)
        {
            var fps = clip.Clip.frameRate;
            var frames = Mathf.RoundToInt(clip.Length * fps) + 1;
            var duration = frames / fps;
            return Anim.Float(0, duration, duration, t => clip.SetTime(Mathf.Min(t, clip.Length)), Ease.Linear, repeat: repeat);
        }

        class ClockRun
        {
            public string Name;
            public float Real, SumDt, BattleExpected;
            public int Frames;
            public readonly List<string> Marks = new List<string>();
        }

        static IEnumerator ClockLoop(string dir)
        {
            Freeze(false);
            var swing = Clip(realModel, "TestSwing");
            if (swing == null) { clockReport = "no TestSwing clip"; yield break; }
            realClip?.Dispose();
            realClip = new Hero3DClip(realModel.GetComponent<Animator>(), swing);
            var speedWas = Anim.Speed;
            var runs = new List<ClockRun>();
            var lines = new List<string>();
            var fps = swing.frameRate;
            var frames = Mathf.RoundToInt(swing.length * fps) + 1;
            lines.Add($"TestSwing: {frames} frames at {fps} fps = {frames / fps:0.000} s of battle time (Unity clip length {swing.length:0.000} s = frames 1..{frames}; the last frame is held its 1/{fps} s).");
            lines.Add("Driver: Anim.Float(0, D, D, t => clip.SetTime(min(t, length)), Linear), D = frames / fps; the PlayableGraph is in manual mode, so nothing else moves the pose.");

            IEnumerator Play(string name, System.Action atStart, System.Func<float, bool> midCheck = null)
            {
                yield return Frames(3);
                var r = new ClockRun { Name = name };
                var done = false;
                realClip.SetTime(0);
                atStart?.Invoke();
                var h = PlayOnBattleClock(realClip).OnComplete(() => done = true);
                var t0 = Time.realtimeSinceStartup;
                float nextMark = 0.25f;
                while (!done)
                {
                    yield return null;
                    r.Frames++;
                    r.SumDt += Time.deltaTime;
                    var real = Time.realtimeSinceStartup - t0;
                    if (real >= nextMark)
                    {
                        r.Marks.Add($"{real:0.00}s real -> clip {realClip.Time:0.000}s");
                        nextMark += 0.25f;
                    }
                    if (real > 15) { r.Marks.Add("TIMEOUT"); break; }
                }
                r.Real = Time.realtimeSinceStartup - t0;
                runs.Add(r);
                lines.Add($"{name}: {r.Real:0.000} s real ({r.Frames} frames, sum of Time.deltaTime {r.SumDt:0.000} s); clip at the end {realClip.Time:0.000} s. Marks: {string.Join(", ", r.Marks)}");
            }

            Anim.Speed = 1;
            yield return Play("x1 (Anim.Speed 1)", null);
            Anim.Speed = 2;
            yield return Play("x2 (Anim.Speed 2)", null);
            Anim.Speed = 1;
            yield return Play("grand finishing blow at the start (Fx.HitStop(170) then Fx.SlowMo(0.3, 950), as BattleScreen.FinishingBlow does; expected ~0.17 + 0.78 + 0.76 = 1.71 s)",
                () => { Fx.HitStop(170); Fx.SlowMo(0.3f, 950); });
            yield return Play("steady slow motion (Fx.SlowMo(0.3, 6000); expected 1 / 0.3 = 3.33 s)", () => Fx.SlowMo(0.3f, 6000));
            // Let the long slow motion run out before the next test.
            for (float w = 0; w < 3.5f; w += Time.unscaledDeltaTime) yield return null;
            Anim.TimeScale = 1;

            // A full stop mid-swing: the battle clock at 0 (as a pause or Hero3DTest.Freeze sets it) for 0.5 s real.
            {
                yield return Frames(3);
                realClip.SetTime(0);
                var h = PlayOnBattleClock(realClip);
                for (float w = 0; w < 0.3f; w += Time.unscaledDeltaTime) yield return null;
                Anim.TimeScale = 0;
                var before = realClip.Time;
                var t0 = Time.realtimeSinceStartup;
                int n = 0;
                while (Time.realtimeSinceStartup - t0 < 0.5f) { yield return null; n++; }
                var stopReal = Time.realtimeSinceStartup - t0;
                var after = realClip.Time;
                Anim.TimeScale = 1;
                // And a game hit-stop (Anim.TimeScale 0.02) of 500 ms real.
                for (float w = 0; w < 0.1f; w += Time.unscaledDeltaTime) yield return null;
                Fx.HitStop(500);
                var hs0 = realClip.Time;
                var t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 0.45f) yield return null;
                var hs1 = realClip.Time;
                lines.Add($"stop: Anim.TimeScale = 0 for {stopReal:0.00} s real ({n} frames): clip {before:0.0000} -> {after:0.0000} s (moved {after - before:0.0000} s).");
                lines.Add($"hit-stop: Fx.HitStop(500) (Anim.TimeScale 0.02) for 0.45 s real: clip {hs0:0.0000} -> {hs1:0.0000} s (moved {hs1 - hs0:0.0000} s, expected ~0.009).");
                h.Stop();
            }
            Anim.Speed = speedWas;
            Anim.TimeScale = 1;
            lines.Add("Unity's own Animator time is not used: the graph is manual (DirectorUpdateMode.Manual) and evaluated only in Hero3DClip.SetTime.");
            clockReport = string.Join("\n", lines);
            File.WriteAllText(Path.Combine(dir, "clock.txt"), clockReport + "\n");
            Debug.Log("drakepg: hero3d clock\n" + clockReport);
            // Back to the stance pose.
            var pose = Clip(realModel, "Stance");
            if (pose != null) { realClip.Dispose(); realClip = new Hero3DClip(realModel.GetComponent<Animator>(), pose); realClip.SetTime(0); }
        }

        static IEnumerator CostLoop(string dir)
        {
            Freeze(false);
            var lines = new List<string>();
            var smrs = realModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var verts = smrs.Sum(s => s.sharedMesh.vertexCount);
            var tris = smrs.Sum(s => s.sharedMesh.triangles.Length / 3);
            lines.Add($"model: {smrs.Length} skinned meshes, {verts} vertices, {tris} triangles as imported (each drawn twice: toon + outline hull)");
            var swing = Clip(realModel, "TestSwing");
            realClip?.Dispose();
            realClip = new Hero3DClip(realModel.GetComponent<Animator>(), swing);
            var frames = Mathf.RoundToInt(swing.length * swing.frameRate) + 1;

            // 1. The picture on a moving pose: BakeMesh, then DrawRenderer, each frame posed and drawn in the same frame.
            IEnumerator Capture(List<Color32[]> into)
            {
                for (int f = 0; f < frames; f++)
                {
                    realClip.SetFrame(f);
                    yield return new WaitForEndOfFrame();
                    var t = ReadView();
                    into.Add(t.GetPixels32());
                    Object.Destroy(t);
                    yield return null;
                }
            }
            view.UseDrawRenderer = false;
            var bake = new List<Color32[]>();
            yield return Capture(bake);
            view.UseDrawRenderer = true;
            yield return Frames(2);
            var draw = new List<Color32[]>();
            yield return Capture(draw);
            int Differ(Color32[] a, Color32[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].a - b[i].a) > 8 || (a[i].a > 0 && Diff(a[i], b[i]))) n++; return n; }
            var same = new List<int>();
            var prev = new List<int>();
            for (int f = 0; f < frames; f++)
            {
                same.Add(Differ(bake[f], draw[f]));
                if (f > 0) prev.Add(Differ(bake[f - 1], draw[f]));
            }
            var drawnPx = draw.Select(p => p.Count(c => c.a > 0)).ToList();
            lines.Add($"DrawRenderer vs BakeMesh, same frame, {frames} frames: pixels that differ per frame max {same.Max()}, mean {same.Average():0} (of ~{drawnPx.Average():0} drawn px); vs BakeMesh's previous frame: mean {prev.Average():0}. Drawn px with DrawRenderer: min {drawnPx.Min()}, max {drawnPx.Max()}.");
            var drawOk = same.Max() < 200 && drawnPx.Min() > 1000;

            // 2. CPU per frame, pose moving on the battle clock.
            var loop = PlayOnBattleClock(realClip, repeat: -1);
            IEnumerator Cpu(List<float> acc, int n) { for (int i = 0; i < n; i++) { yield return null; acc.Add(view.LastCpuMs); } }
            view.UseDrawRenderer = false;
            yield return Frames(10);
            var bakeMs = new List<float>();
            yield return Cpu(bakeMs, 240);
            view.UseDrawRenderer = true;
            yield return Frames(10);
            var drawMs = new List<float>();
            yield return Cpu(drawMs, 240);
            lines.Add($"Hero3DView.LastCpuMs (pose moving): BakeMesh path {bakeMs.Average():0.000} ms (worst {bakeMs.Max():0.00}: bake + queue + execute); DrawRenderer path {drawMs.Average():0.000} ms (worst {drawMs.Max():0.00}: queueing only; its execute runs at the start of rendering and Unity's skinning is untimed in both). Whole-frame CPU below.");

            // 3. Keep the correct, cheaper path; frame time with and without the hero.
            view.UseDrawRenderer = drawOk && drawMs.Average() < bakeMs.Average();
            lines.Add($"kept: {(view.UseDrawRenderer ? "DrawRenderer" : "BakeMesh")} ({(drawOk ? "DrawRenderer's picture matches BakeMesh's" : "DrawRenderer's picture does NOT match BakeMesh's")}).");
            var keep = view.UseDrawRenderer;
            view.UseDrawRenderer = false;
            yield return Frames(30);
            var onBake = new Timing();
            yield return onBake.Measure(240, view);
            view.UseDrawRenderer = true;
            yield return Frames(30);
            var onDraw = new Timing();
            yield return onDraw.Measure(240, view);
            view.UseDrawRenderer = keep;
            var on = keep ? onDraw : onBake;
            view.gameObject.SetActive(false);
            yield return Frames(30);
            var off = new Timing();
            yield return off.Measure(240, null);
            view.gameObject.SetActive(true);
            loop.Stop();
            lines.Add($"frame with the hero, BakeMesh: {onBake}");
            lines.Add($"frame with the hero, DrawRenderer: {onDraw}");
            lines.Add($"frame without the hero: {off}");
            lines.Add($"RenderTexture {view.Texture.width}x{view.Texture.height} ARGB32 + 24-bit depth, mipmapped (Supersample {view.Supersample}, box {view.BoxWidth}x{view.BoxHeight} design px); about {view.Texture.width * view.Texture.height * 8 * 4 / 3 / 1048576f:0.0} MB.");
            costReport = string.Join("\n", lines);
            File.WriteAllText(Path.Combine(dir, "cost.txt"), costReport + "\n");
            Debug.Log("drakepg: hero3d cost\n" + costReport);
            var pose = Clip(realModel, "Stance");
            if (pose != null) { realClip.Dispose(); realClip = new Hero3DClip(realModel.GetComponent<Animator>(), pose); realClip.SetTime(0); }
        }
    }
}
