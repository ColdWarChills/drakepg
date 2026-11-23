using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Drakepg.Hero3D;
using UnityEditor;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// The real (imported) hero in a fight, play mode: <c>Hero3DTest.RunReal()</c> starts the same fight as Run(), hides the
    /// drawn hero and shows the imported model (Hero3DImport's prefab) at his spot and size through Hero3DView, in the
    /// game view of its look file (yaw, pitch), posed by its "Stance" clip if it has one (else the rest pose). Saves
    /// real_without.png / real_hidden.png / real_fight.png / real_3x.png / real_left.png (facing left by mirroring the
    /// picture) and, with a "TestSwing" clip, swing_sheet.png; checks the weapon hand is the near hand, the pixels outside
    /// the hero's box, the materials' colours in the picture and that nothing touches the picture's border; the lines go
    /// to the log and LastReport.
    /// </summary>
    public static partial class Hero3DTest
    {
        public const string RealPrefab = "Assets/Hero3D/Models/hero_c1/hero_c1.prefab";
        static Hero3DClip realClip;
        static Hero3DModel realModel;
        static string realPrefab;

        public static string RunReal(string dir = null, string prefab = RealPrefab, string poseClip = "Stance", string swingClip = "TestSwing")
        {
            if (!Application.isPlaying || GameRoot.I == null) return "enter play mode first";
            GameRoot.I.StartCoroutine(RunRealLoop(dir ?? DefaultDir, prefab, poseClip, swingClip));
            return "started";
        }

        static IEnumerator RunRealLoop(string dir, string prefab, string poseClip, string swingClip)
        {
            Directory.CreateDirectory(dir);
            var report = new List<string>();
            yield return StartFight("warrior");
            Freeze(true);
            yield return Frames(3);
            var without = Path.Combine(dir, "real_without.png");
            GameRoot.I.Snapshot(without);
            yield return Frames(3);
            var fig = HeroFigure();
            fig.gameObject.SetActive(false);
            yield return Frames(3);
            var hidden = Path.Combine(dir, "real_hidden.png");
            GameRoot.I.Snapshot(hidden);
            yield return Frames(3);

            report.Add(ShowReal(fig, prefab, poseClip));
            yield return Frames(4);
            var with = Path.Combine(dir, "real_fight.png");
            GameRoot.I.Snapshot(with);
            yield return Frames(4);
            report.Add(Check(without, hidden, with, dir, "real_3x.png"));
            report.Add(NearHand());
            report.Add(Colours());
            report.Add(Border("pose"));

            // Swing frames: the clip's frames 0..n, grounded once on its first frame (the feet stay planted).
            var swing = Clip(realModel, swingClip);
            if (swing != null)
            {
                realClip?.Dispose();
                realClip = new Hero3DClip(realModel.GetComponent<Animator>(), swing);
                realClip.SetTime(0);
                view.GroundToLowest();
                var frames = Mathf.RoundToInt(swing.length * swing.frameRate);
                var shots = new List<string>();
                var borders = new List<string>();
                for (int f = 0; f <= frames; f++)
                {
                    realClip.SetFrame(f);
                    yield return Frames(2);
                    var p = Path.Combine(dir, $"swing_f{f:00}.png");
                    GameRoot.I.Snapshot(p);
                    yield return Frames(2);
                    shots.Add(p);
                    var b = Border("f" + f);
                    if (!b.Contains("clear")) borders.Add(b);
                }
                Sheet(shots, Path.Combine(dir, "swing_sheet.png"), frames);
                // Blender frames 8, 12 and 16 (clip frames 7, 11, 15) at 3x.
                foreach (var f in new[] { 7, 11, 15 })
                    if (f < shots.Count) Crop3x(shots[f], Path.Combine(dir, $"swing_f{f + 1:00}_3x.png"), view.Picture.bounds.min, view.Picture.bounds.max);
                foreach (var p in shots) File.Delete(p);
                report.Add($"swing {swing.name}: {frames + 1} frames ({swing.length:0.###} s at {swing.frameRate} fps) in swing_sheet.png; border: {(borders.Count == 0 ? "clear in every frame" : string.Join("; ", borders))}");
                // Back to the pose for the left still.
                realClip.Dispose();
                realClip = null;
                var pose = Clip(realModel, poseClip);
                if (pose != null) { realClip = new Hero3DClip(realModel.GetComponent<Animator>(), pose); realClip.SetTime(0); }
                view.GroundToLowest();
            }
            else report.Add($"no clip {swingClip}: no swing sheet");

            // Facing left: what turning the model round would do to his hands (not shown), then the default: the picture mirrored.
            view.Follow = null;
            view.FlipPicture = false;
            view.SetFacing(-1);
            var turned = NearHand(false);
            view.FlipPicture = true;
            view.SetFacing(-1);
            yield return Frames(4);
            var left = Path.Combine(dir, "real_left.png");
            GameRoot.I.Snapshot(left);
            yield return Frames(4);
            Crop3x(left, Path.Combine(dir, "real_left_3x.png"), view.Picture.bounds.min, view.Picture.bounds.max);
            report.Add("left (Hero3DView.Apply sets FlipPicture): picture mirrored (the light and his features mirror with it: the light comes from the upper left); " + NearHand() + ". Turning the model round instead: " + turned);
            view.SetFacing(1);
            view.Follow = fig.transform;
            yield return Frames(2);

            // Show / hide by name (what armor will do), one still each, then back.
            var hide = new List<string>();
            foreach (var part in new[] { "Shirt", "Head" })
            {
                var ok = realModel.SetVisible(part, false);
                yield return Frames(3);
                var p = Path.Combine(dir, $"real_no{part.ToLowerInvariant()}.png");
                GameRoot.I.Snapshot(p);
                yield return Frames(3);
                Crop3x(p, Path.Combine(dir, $"real_no{part.ToLowerInvariant()}_3x.png"), view.Picture.bounds.min, view.Picture.bounds.max);
                File.Delete(p);
                realModel.SetVisible(part, true);
                hide.Add($"{part}: {(ok ? "hidden" : "NOT FOUND")}");
            }
            var headBone = realModel.Bone("Head");
            hide.Add($"Bone(\"Head\") = {(headBone == null ? "none" : headBone.name + (headBone.GetComponent<Renderer>() != null ? " (a MESH: wrong)" : " (the bone)"))}");
            report.Add("show/hide by name: " + string.Join("; ", hide) + " (stills real_noshirt_3x.png, real_nohead_3x.png)");

            LastReport = string.Join("\n", report);
            Debug.Log("drakepg: hero3d real\n" + LastReport);
            File.WriteAllText(Path.Combine(dir, "real_report.txt"), LastReport);
        }

        /// <summary>The imported hero through Hero3DView where `fig` stands, posed by `poseClip` (if the model has it) and grounded.</summary>
        static string ShowReal(Figure fig, string prefab, string poseClip)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (asset == null) return "no prefab " + prefab;
            realPrefab = prefab;
            var inst = Object.Instantiate(asset);
            realModel = inst.GetComponent<Hero3DModel>();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.quality = SkinQuality.Bone4;
            view = Hero3DView.Create(Screen.World, inst.transform, Design.Depth(GroundY));
            view.PixelsPerUnit = Design.PixelsPerUnit;
            view.DrawnHeight = Figure.ArtHeight;
            view.ScreenCamera = GameRoot.I.Camera;
            view.Apply(realModel);
            view.OutlineColor = OutlineOf(inst);
            var pose = Clip(realModel, poseClip);
            realClip?.Dispose();
            realClip = null;
            if (pose != null)
            {
                realClip = new Hero3DClip(inst.GetComponent<Animator>(), pose);
                realClip.SetTime(0);
            }
            view.GroundToLowest();
            view.Follow = fig.transform;
            return $"real hero {prefab}: {(pose != null ? "pose " + pose.name : "rest pose (no " + poseClip + " clip)")}; yaw {view.Turn} pitch {view.Pitch} front {view.FrontYaw}; {view.ModelHeight} m = {view.DrawnHeight} px; ground {view.GroundY:0.000} m";
        }

        static Color OutlineOf(GameObject inst)
        {
            var m = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.sharedMaterials).FirstOrDefault(x => x != null);
            return m != null ? m.GetColor("_OutlineColor") : new Color(0.13f, 0.1f, 0.14f, 1);
        }

        static AnimationClip Clip(Hero3DModel m, string name)
        {
            if (m == null || string.IsNullOrEmpty(name)) return null;
            // The clips live in the model file next to the prefab.
            if (string.IsNullOrEmpty(realPrefab)) return null;
            var folder = Path.GetDirectoryName(realPrefab).Replace('\\', '/');
            var fbx = Directory.GetFiles(Path.GetFullPath(folder), "*.fbx").FirstOrDefault();
            if (fbx == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(folder + "/" + Path.GetFileName(fbx)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name));
        }

        /// <summary>socket_hand_R must be nearer the camera than socket_hand_L (his right = the near arm), by Hero3DView's own view.</summary>
        static string NearHand(bool assert = true)
        {
            var r = realModel.Bone("socket_hand_R");
            var l = realModel.Bone("socket_hand_L");
            if (r == null || l == null) return "near hand: no sockets";
            float dr = view.Depth(r.position), dl = view.Depth(l.position);
            var ok = dr < dl;
            if (!ok && assert) Debug.LogError("drakepg: hero3d the weapon hand (socket_hand_R) is not the near hand");
            return $"near hand {(ok ? "OK" : "FAILED")}: socket_hand_R depth {dr:0.000} m, socket_hand_L {dl:0.000} m (smaller = nearer)";
        }

        static Texture2D ReadView()
        {
            var rt = view.Texture;
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            t.Apply();
            RenderTexture.active = was;
            return t;
        }

        /// <summary>Each material's flat lit and shaded colour, counted in the hero's picture (mip 0, solid pixels).</summary>
        static string Colours()
        {
            var t = ReadView();
            var px = t.GetPixels32();
            Object.Destroy(t);
            var solid = px.Where(p => p.a == 255).ToArray();
            var sb = new StringBuilder($"colours in the picture ({solid.Length} solid px): ");
            var mats = realModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.sharedMaterials).Where(m => m != null).Distinct();
            foreach (var m in mats)
            {
                var b = m.GetColor("_Color");
                var s = m.GetColor("_ShadeColor");
                var lit = (Color32)b;
                var shade = (Color32)new Color(b.r * s.r, b.g * s.g, b.b * s.b);
                int nl = solid.Count(p => Near(p, lit)), ns = solid.Count(p => Near(p, shade));
                sb.Append($"{m.name} lit #{ColorUtility.ToHtmlStringRGB(lit)} {nl} / shade #{ColorUtility.ToHtmlStringRGB(shade)} {ns}; ");
            }
            return sb.ToString();
        }

        static bool Near(Color32 p, Color32 q) => Mathf.Abs(p.r - q.r) <= 2 && Mathf.Abs(p.g - q.g) <= 2 && Mathf.Abs(p.b - q.b) <= 2;

        /// <summary>Whether any drawn pixel touches the picture's border (the box would cut the hero off).</summary>
        static string Border(string label)
        {
            var t = ReadView();
            int w = t.width, h = t.height, hits = 0;
            var px = t.GetPixels32();
            Object.Destroy(t);
            for (int x = 0; x < w; x++) { if (px[x].a > 0) hits++; if (px[(h - 1) * w + x].a > 0) hits++; }
            for (int y = 0; y < h; y++) { if (px[y * w].a > 0) hits++; if (px[y * w + w - 1].a > 0) hits++; }
            return hits == 0 ? $"border {label}: clear" : $"border {label}: {hits} px touch the edge of the {w}x{h} picture";
        }

        /// <summary>Like Check, for the real hero: pixels changed outside its box, where the drawn and the 3D hero are, and the 3x crop.</summary>
        static string Check(string without, string hidden, string with, string dir, string crop)
        {
            var a = Load(without);
            var h = Load(hidden);
            var w = Load(with);
            if (a == null || h == null || w == null) return "shots missing";
            var cam = GameRoot.I.Camera;
            var pb = view.Picture.bounds;
            var lo = Vector3.Min(cam.WorldToScreenPoint(pb.min), cam.WorldToScreenPoint(view.Shadow.bounds.min));
            var hi = Vector3.Max(cam.WorldToScreenPoint(pb.max), cam.WorldToScreenPoint(view.Shadow.bounds.max));
            int outside = 0, outsideVsWithout = 0;
            RectInt drawn = default, hero = default;
            bool hasDrawn = false, hasHero = false;
            for (int y = 0; y < a.height; y++)
                for (int x = 0; x < a.width; x++)
                {
                    var i = y * a.width + x;
                    bool inBox = x >= lo.x - 1 && x <= hi.x + 1 && y >= lo.y - 1 && y <= hi.y + 1;
                    if (Diff(a.px[i], h.px[i])) Grow(ref drawn, ref hasDrawn, x, y);
                    if (Diff(h.px[i], w.px[i]))
                    {
                        Grow(ref hero, ref hasHero, x, y);
                        if (!inBox) outside++;
                    }
                    if (!inBox && Diff(a.px[i], w.px[i])) outsideVsWithout++;
                }
            Crop3x(with, Path.Combine(dir, crop), pb.min, pb.max);
            string Rows(RectInt r) => $"x {r.xMin}..{r.xMax}, top {a.height - 1 - r.yMax}, feet {a.height - 1 - r.yMin} ({r.yMax - r.yMin + 1} px tall)";
            return $"shot {a.width}x{a.height}; box x {lo.x:0}..{hi.x:0}; changed pixels outside the hero's box: {outside} (vs the fight with the drawn hero: {outsideVsWithout}, which includes the drawn hero's own pixels outside the box); " +
                   $"drawn hero {(hasDrawn ? Rows(drawn) : "none")}; 3D hero {(hasHero ? Rows(hero) : "none")} (includes its shadow)";
        }

        /// <summary>The picture's area of each shot side by side (cols x rows), 2x.</summary>
        static void Sheet(List<string> shots, string dst, int lastFrame)
        {
            var cam = GameRoot.I.Camera;
            var pb = view.Picture.bounds;
            var lo = cam.WorldToScreenPoint(pb.min);
            var hi = cam.WorldToScreenPoint(pb.max);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.x, hi.x))), x1 = Mathf.CeilToInt(Mathf.Max(lo.x, hi.x));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(lo.y, hi.y))), y1 = Mathf.CeilToInt(Mathf.Max(lo.y, hi.y));
            int cw = x1 - x0, ch = y1 - y0, k = 2;
            int cols = Mathf.Min(7, shots.Count), rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cw * k, rows * ch * k, TextureFormat.RGBA32, false);
            var outPx = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < shots.Count; i++)
            {
                var s = Load(shots[i]);
                if (s == null) continue;
                int cx = (i % cols) * cw * k, cy = (rows - 1 - i / cols) * ch * k;
                for (int y = 0; y < ch * k; y++)
                    for (int x = 0; x < cw * k; x++)
                    {
                        int sx = Mathf.Min(s.width - 1, x0 + x / k), sy = Mathf.Min(s.height - 1, y0 + y / k);
                        var c = s.px[sy * s.width + sx];
                        // A thin frame between cells and a frame number mark (dots) in the corner.
                        if (x == 0 || y == 0) c = new Color32(30, 30, 30, 255);
                        outPx[(cy + y) * sheet.width + cx + x] = c;
                    }
                for (int d = 0; d <= i; d++)
                {
                    int dx = cx + 6 + (d % 12) * 6, dy = cy + ch * k - 8 - (d / 12) * 6;
                    for (int yy = 0; yy < 4; yy++) for (int xx = 0; xx < 4; xx++) outPx[(dy + yy) * sheet.width + dx + xx] = new Color32(255, 255, 255, 255);
                }
            }
            sheet.SetPixels32(outPx);
            sheet.Apply();
            File.WriteAllBytes(dst, sheet.EncodeToPNG());
            Object.Destroy(sheet);
        }
    }
}
