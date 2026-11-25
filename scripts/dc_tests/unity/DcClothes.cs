using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Test only (2026-11-24, the designer's test 2 "clothes only"): the Dragon Crashers knight, untouched (skeleton,
    /// weights, other parts and its attack clip), with only its `torso` drawing swapped for a repainted one of the same
    /// canvas, pivot and outline. The new torso is a runtime Sprite with the original's mesh, weights, bones and bind
    /// poses copied, put in through the knight's Sprite Library (AddOverride under Torso/Torso). No rig code, no pose.
    /// Play mode, through the Unity MCP's execute_code: Export(dir) (any mode), Measure(dir), Run(...), read Status;
    /// Clear() removes the test objects.
    /// </summary>
    public static class DcClothes
    {
        const string Dir = "Assets/Test2D/dc/";
        /// <summary>The figure Run/Measure spawn; set to the bare mannequin (PV_Character_Base_Bipedal) to see the skeleton alone.</summary>
        public static string PrefabPath = Dir + "Art/Characters/_Prefabs/PV_Character_Knight.prefab";
        const string ClipPath = Dir + "Animations/Characters/AnimClips/Knight/Anim_Character_Knight_Attack.anim";
        static readonly Color Backdrop = new Color(0.40f, 0.44f, 0.47f, 1f);
        static readonly Vector3 Stage = new Vector3(5000f, 5000f, 0f);

        public static string Status { get; private set; } = "";
        static readonly List<Object> made = new List<Object>();

        static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        static AnimationClip Clip => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);

        static SpriteRenderer Torso(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name == "torso") return r;
            return null;
        }

        static bool IsWeapon(Transform t, Transform root)
        {
            for (var p = t; p != null && p != root; p = p.parent) if (p.name.StartsWith("P_Weapon")) return true;
            return false;
        }

        /// <summary>The torso's own pixels (its rect in the PSB atlas, read through a blit) to dir/torso_orig.png.</summary>
        public static string Export(string dir)
        {
            var s = Torso(Prefab).sprite;
            var tex = s.texture;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var r = s.textureRect;
            var t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "torso_orig.png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            return $"torso {r} pivot {s.pivot} ppu {s.pixelsPerUnit} atlas {tex.width}x{tex.height} filter {tex.filterMode} mips {tex.mipmapCount}";
        }

        /// <summary>
        /// A new torso sprite from a PNG of the torso's rect size: same pivot and pixels per unit, the original's vertex
        /// positions, tangents, blend weights, indices, bones and bind poses; UVs remapped from the atlas rect to the PNG.
        /// </summary>
        public static Sprite MakeTorso(Sprite orig, string png)
        {
            var src = orig.texture;
            var load = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            load.LoadImage(File.ReadAllBytes(png));
            var rect = orig.rect;
            if (load.width != (int)rect.width || load.height != (int)rect.height)
                throw new System.Exception($"{png} is {load.width}x{load.height}, torso rect is {rect.width}x{rect.height}");
            var tex = new Texture2D(load.width, load.height, TextureFormat.RGBA32, src.mipmapCount > 1);
            tex.SetPixels32(load.GetPixels32());
            tex.filterMode = src.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = src.anisoLevel;
            tex.Apply(true);
            Object.Destroy(load);
            made.Add(tex);

            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(orig.pivot.x / rect.width, orig.pivot.y / rect.height), orig.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sp.name = "torso (" + Path.GetFileNameWithoutExtension(png) + ")";
            made.Add(sp);
            int n = orig.GetVertexCount();
            sp.SetVertexCount(n);
            var tr = orig.textureRect;
            using (var pos = new NativeArray<Vector3>(n, Allocator.Temp))
            using (var uv = new NativeArray<Vector2>(n, Allocator.Temp))
            {
                var p0 = orig.GetVertexAttribute<Vector3>(VertexAttribute.Position);
                var u0 = orig.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0);
                var a = pos; var b = uv;
                for (int i = 0; i < n; i++)
                {
                    a[i] = p0[i];
                    b[i] = new Vector2((u0[i].x * src.width - tr.x) / tr.width, (u0[i].y * src.height - tr.y) / tr.height);
                }
                sp.SetVertexAttribute(VertexAttribute.Position, a);
                sp.SetVertexAttribute(VertexAttribute.TexCoord0, b);
            }
            if (orig.HasVertexAttribute(VertexAttribute.Tangent))
                using (var tg = new NativeArray<Vector4>(orig.GetVertexAttribute<Vector4>(VertexAttribute.Tangent).ToArray(), Allocator.Temp))
                    sp.SetVertexAttribute(VertexAttribute.Tangent, tg);
            using (var bw = new NativeArray<BoneWeight>(orig.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray(), Allocator.Temp))
                sp.SetVertexAttribute(VertexAttribute.BlendWeight, bw);
            using (var idx = new NativeArray<ushort>(orig.GetIndices().ToArray(), Allocator.Temp))
                sp.SetIndices(idx);
            sp.SetBones(orig.GetBones());
            using (var bp = new NativeArray<Matrix4x4>(orig.GetBindPoses().ToArray(), Allocator.Temp))
                sp.SetBindPoses(bp);
            return sp;
        }

        class Knight
        {
            public string Name;
            public GameObject Go;
            public PlayableGraph Graph;
            public AnimationClipPlayable Play;
            public SpriteRenderer TorsoR;
            public Sprite Want;
            public int Wrong;
        }

        static Knight Spawn(string name, string png, int slot)
        {
            var k = new Knight { Name = name };
            k.Go = (GameObject)Object.Instantiate(Prefab, Stage + new Vector3(slot * 40f, 0, 0), Quaternion.identity);
            k.Go.name = "dc clothes " + name;
            made.Add(k.Go);
            var animator = k.Go.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            k.Graph = PlayableGraph.Create("dc clothes " + name);
            k.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(k.Graph, "dc", animator);
            k.Play = AnimationClipPlayable.Create(k.Graph, Clip);
            output.SetSourcePlayable(k.Play);
            k.TorsoR = Torso(k.Go);
            if (png != null)
            {
                k.Want = MakeTorso(k.TorsoR.sprite, png);
                var lib = k.Go.GetComponentInChildren<SpriteLibrary>();
                lib.AddOverride(k.Want, "Torso", "Torso");
                lib.RefreshSpriteResolvers();
                var res = k.TorsoR.GetComponent<SpriteResolver>();
                res.SetCategoryAndLabel("Torso", "Torso");
                res.ResolveSpriteToSpriteRenderer();
            }
            else k.Want = k.TorsoR.sprite;
            return k;
        }

        static Camera Cam(string name, int w, int h, float ortho)
        {
            var go = new GameObject(name);
            made.Add(go);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            made.Add(rt);
            cam.targetTexture = rt;
            return cam;
        }

        static void Save(Camera cam, Texture2D buf, string path)
        {
            var rt = cam.targetTexture;
            RenderTexture.active = rt;
            buf.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            buf.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, buf.EncodeToPNG());
        }

        public static string Clear()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            foreach (var g in graphs) if (g.IsValid()) g.Destroy();
            graphs.Clear();
            return "cleared";
        }

        static readonly List<PlayableGraph> graphs = new List<PlayableGraph>();

        /// <summary>
        /// One standing knight (clip frame 0, sword and shield hidden) seen by a camera centred on its root with ortho
        /// size 5 into 2560x1440: dir/measure.png, for the body's pixel height. Then the same with weapons at frames
        /// 0, 20, 33 and 50 (dir/reach_NNN.png) to size the cells.
        /// </summary>
        public static string Measure(string dir, float ortho = 5f, float cy = 0f)
        {
            if (!EditorApplication.isPlaying) return "needs play mode";
            Clear();
            Directory.CreateDirectory(dir);
            GameRoot.I.StartCoroutine(MeasureLoop(dir, ortho, cy));
            return "measuring";
        }

        static IEnumerator MeasureLoop(string dir, float ortho, float cy)
        {
            Status = "measure";
            var k = Spawn("measure", null, 0);
            graphs.Add(k.Graph);
            var cam = Cam("dc clothes cam", 2560, 1440, ortho);
            cam.transform.position = k.Go.transform.position + new Vector3(0, cy, -10);
            var buf = new Texture2D(2560, 1440, TextureFormat.RGB24, false);
            made.Add(buf);
            var weapons = new List<Renderer>();
            foreach (var r in k.Go.GetComponentsInChildren<Renderer>(true)) if (IsWeapon(r.transform, k.Go.transform)) weapons.Add(r);
            int[] frames = { -1, 0, 20, 33, 50 };
            foreach (int f in frames)
            {
                foreach (var r in weapons) r.enabled = f >= 0;
                for (int w = 0; w < 3; w++)
                {
                    yield return null;
                    k.Play.SetTime(Mathf.Max(0, f) / 60.0);
                    k.Graph.Evaluate(0);
                    yield return new WaitForEndOfFrame();
                }
                Save(cam, buf, Path.Combine(dir, f < 0 ? "measure.png" : $"reach_{f:000}.png"));
            }
            Status = "measured";
        }

        /// <summary>
        /// The test: knights side by side, `variants` "name=png" pairs separated by ';' (png empty = as shipped), clip
        /// frames from..to at t = f/60, each knight through its own camera (ppu = screen pixels per unit, cell w x h, centre offset from the
        /// knight's root in units) to dir/&lt;name&gt;_fNNN.png; at the still frames also a 3x camera on the torso
        /// (offset tx,ty from the root, cell sw x sh) to dir/&lt;name&gt;_3x_fNNN.png. Status ends "done ..." with how many
        /// frames a swapped torso renderer did not hold the new sprite.
        /// </summary>
        public static string Run(string dir, string variants, float ppu, int w, int h, float cx, float cy,
            int from, int to, string stillFrames, float tx, float ty, int sw, int sh)
        {
            if (!EditorApplication.isPlaying) return "needs play mode";
            Clear();
            Directory.CreateDirectory(dir);
            var ks = new List<Knight>();
            int slot = 0;
            foreach (var v in variants.Split(';'))
            {
                var kv = v.Split('=');
                var k = Spawn(kv[0], kv.Length > 1 && kv[1].Length > 0 ? kv[1] : null, slot++);
                graphs.Add(k.Graph);
                ks.Add(k);
            }
            var stills = new HashSet<int>();
            foreach (var s in stillFrames.Split(',')) if (s.Length > 0) stills.Add(int.Parse(s));
            GameRoot.I.StartCoroutine(RunLoop(dir, ks, ppu, w, h, cx, cy, from, to, stills, tx, ty, sw, sh));
            var swapped = new List<string>();
            foreach (var k in ks) swapped.Add(k.Name + ": torso sprite " + k.TorsoR.sprite.name + (k.TorsoR.sprite == k.Want ? " (as wanted)" : " (NOT the wanted " + k.Want.name + ")"));
            return "running; " + string.Join("; ", swapped.ToArray());
        }

        static IEnumerator RunLoop(string dir, List<Knight> ks, float ppu, int w, int h, float cx, float cy, int from, int to,
            HashSet<int> stills, float tx, float ty, int sw, int sh)
        {
            var cams = new List<Camera>();
            var zooms = new List<Camera>();
            foreach (var k in ks)
            {
                var c = Cam("dc clothes cam " + k.Name, w, h, h / (2f * ppu));
                c.transform.position = k.Go.transform.position + new Vector3(cx, cy, -10);
                cams.Add(c);
                var z = Cam("dc clothes 3x " + k.Name, sw, sh, sh / (2f * 3f * ppu));
                z.transform.position = k.Go.transform.position + new Vector3(tx, ty, -10);
                z.enabled = false;
                zooms.Add(z);
            }
            var buf = new Texture2D(w, h, TextureFormat.RGB24, false);
            var zbuf = new Texture2D(sw, sh, TextureFormat.RGB24, false);
            made.Add(buf); made.Add(zbuf);
            for (int f = from; f <= to; f++)
            {
                Status = "frame " + f + "/" + to;
                bool still = stills.Contains(f);
                foreach (var z in zooms) z.enabled = still;
                // two warm-up frames on the first one, so the skins are deformed before the first capture
                for (int warm = f == from ? 3 : 1; warm > 0; warm--)
                {
                    yield return null;
                    foreach (var k in ks) { k.Play.SetTime(f / 60.0); k.Graph.Evaluate(0); }
                    yield return new WaitForEndOfFrame();
                }
                for (int i = 0; i < ks.Count; i++)
                {
                    if (ks[i].TorsoR.sprite != ks[i].Want) ks[i].Wrong++;
                    Save(cams[i], buf, Path.Combine(dir, $"{ks[i].Name}_f{f:000}.png"));
                    if (still) Save(zooms[i], zbuf, Path.Combine(dir, $"{ks[i].Name}_3x_f{f:000}.png"));
                }
            }
            var report = new List<string>();
            foreach (var k in ks) report.Add(k.Name + " wrong-sprite frames " + k.Wrong + " (torso " + k.TorsoR.sprite.name + ")");
            Status = "done " + string.Join("; ", report.ToArray());
        }
    }
}
