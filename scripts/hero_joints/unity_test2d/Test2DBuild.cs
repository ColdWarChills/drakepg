using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Hero 4 (the jointed hero) on Unity's 2D Animation package, built from data: scripts/hero_joints/r4_export.py
    /// writes the parts (Assets/Test2D/Art/*.png + rig.json), r4_keys.py the key poses (keys.json). This makes the
    /// sprite imports (pivots on the joints), the sleeve's bones and weights (Sprite Skin), the Sprite Library, the prefab
    /// (bones as Transforms, Limb Solver 2D on the near arm, Sprite Resolvers) and the Animation clip sword_cut.
    /// Menu: Drakepg/Test2D/Build hero 4. Nothing here runs in the game.
    /// </summary>
    public static class Test2DBuild
    {
        public const string Dir = "Assets/Test2D";
        public const string ArtDir = Dir + "/Art";
        public const string PrefabPath = Dir + "/hero4.prefab";
        public const string ClipPath = Dir + "/sword_cut.anim";
        public const string LibPath = Dir + "/hero4_library.asset";
        const float Ppu = 100;
        /// <summary>Sleeve weights: the forearm's share ramps from 0 to 1 over the elbow, this far each side (px).</summary>
        public static float ElbowRamp = 36;

        static JObject rig;
        /// <summary>The smear drawings r4_keys.py made: one per strike frame (f, file, pivot at the hero root).</summary>
        static JArray Smears => (JArray)JObject.Parse(File.ReadAllText(ArtDir + "/keys.json"))["smears"];
        static JObject Rig => rig ??= JObject.Parse(File.ReadAllText(ArtDir + "/rig.json"));
        static JObject Geo => (JObject)Rig["geo"];
        static Vector2 G(string k) => new Vector2((float)Geo[k][0], (float)Geo[k][1]);
        static float Gf(string k) => (float)Geo[k];

        /// <summary>Drawing canvas px (y down) -> the hero root's local units (origin at the soles under the waist).</summary>
        public static Vector2 C2R(Vector2 c) => new Vector2((c.x - G("waist").x) / Ppu, (Gf("soles_y") - c.y) / Ppu);
        /// <summary>A canvas offset (y down) -> local units.</summary>
        public static Vector2 D2U(Vector2 d) => new Vector2(d.x / Ppu, -d.y / Ppu);

        [MenuItem("Drakepg/Test2D/Build hero 4")]
        public static string BuildAll()
        {
            rig = null;
            var log = new List<string> { ImportArt(), SkinSleeves(), BuildLibrary(), BuildPrefab(), BuildClip() };
            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        // ---------------------------------------------------------------- sprites

        public static string ImportArt()
        {
            int n = 0;
            foreach (var kv in (JObject)Rig["sprites"])
            {
                var s = (JObject)kv.Value;
                n += ImportOne((string)s["file"], s) ? 1 : 0;
                if (s["rim"] != null) n += ImportOne((string)s["rim"], s) ? 1 : 0;
            }
            foreach (JObject sm in Smears)
                n += ImportOne((string)sm["file"], null, new Vector2((float)sm["pivot"][0], (float)sm["pivot"][1])) ? 1 : 0;
            return $"imported {n} sprites";
        }

        static bool ImportOne(string file, JObject s, Vector2? pivotPx = null)
        {
            var path = ArtDir + "/" + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (ti == null) return false;
            ti.GetSourceTextureWidthAndHeight(out var w, out var h);
            Vector2 pv;
            if (pivotPx.HasValue) pv = pivotPx.Value;
            else if (s != null) pv = new Vector2((float)s["pivot"][0], (float)s["pivot"][1]);
            else pv = new Vector2(w / 2f, h / 2f);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            // As ArtImport (mipmaps, Kaiser, trilinear: drawn 2-4x smaller than the art), but not crunched.
            ti.mipmapEnabled = true;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.filterMode = FilterMode.Trilinear;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.crunchedCompression = false;
            ti.spritePixelsPerUnit = Ppu;
            var set = new TextureImporterSettings();
            ti.ReadTextureSettings(set);
            set.spriteAlignment = (int)SpriteAlignment.Custom;
            set.spritePivot = new Vector2(pv.x / w, 1 - pv.y / h);
            set.spriteMeshType = file.StartsWith("sleeve") ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            set.spriteExtrude = 2;
            ti.SetTextureSettings(set);
            ti.SaveAndReimport();
            return true;
        }

        public static Sprite Spr(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + file);

        /// <summary>
        /// The sleeve sprites (and their rim twins) get two bones, upper and fore, and a grid mesh whose forearm weight
        /// ramps over the elbow: one straight painted sleeve bent by Sprite Skin.
        /// </summary>
        public static string SkinSleeves()
        {
            var log = new List<string>();
            foreach (var file in new[] { "sleeve_lit.png", "sleeve_under.png", "sleeve_lit_rim.png", "sleeve_under_rim.png" })
                log.Add(SkinOne(file));
            return string.Join("; ", log);
        }

        static string SkinOne(string file)
        {
            var path = ArtDir + "/" + file;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.GetSourceTextureWidthAndHeight(out var w, out var h);
            var s = (JObject)Rig["sprites"]["sleeve_lit"];
            var pv = new Vector2((float)s["pivot"][0], h - (float)s["pivot"][1]);   // rect space, y up
            float ue = Gf("UE"), len = Gf("L");
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            var rect = dp.GetSpriteRects()[0];
            var guid = rect.spriteID;
            var bones = new List<SpriteBone>
            {
                new SpriteBone { name = "upper", guid = "hero4_upper", position = new Vector3(pv.x, pv.y, 0), rotation = Quaternion.Euler(0, 0, -90), length = ue, parentId = -1 },
                new SpriteBone { name = "fore", guid = "hero4_fore", position = new Vector3(ue, 0, 0), rotation = Quaternion.identity, length = len - ue, parentId = 0 },
            };
            dp.GetDataProvider<ISpriteBoneDataProvider>().SetBones(guid, bones);
            // grid: columns across, rows along the arm (dense over the elbow)
            var xs = new List<float>();
            for (int i = 0; i <= 10; i++) xs.Add(w * i / 10f);
            var ys = new SortedSet<float> { 0, h };
            for (float y = 0; y < h; y += 16) ys.Add(y);
            for (float u = ue - ElbowRamp - 12; u <= ue + ElbowRamp + 12; u += 4) { var y = pv.y - u; if (y > 0 && y < h) ys.Add(y); }
            var rows = ys.ToList();
            var verts = new List<Vertex2DMetaData>();
            foreach (var y in rows)
                foreach (var x in xs)
                {
                    float u = pv.y - y;
                    float t = Mathf.Clamp01((u - (ue - ElbowRamp)) / (2 * ElbowRamp));
                    float wf = t * t * (3 - 2 * t);
                    var bw = new BoneWeight();
                    if (wf > 0.5f) { bw.boneIndex0 = 1; bw.weight0 = wf; bw.boneIndex1 = 0; bw.weight1 = 1 - wf; }
                    else { bw.boneIndex0 = 0; bw.weight0 = 1 - wf; bw.boneIndex1 = 1; bw.weight1 = wf; }
                    verts.Add(new Vertex2DMetaData { position = new Vector2(x, y), boneWeight = bw });
                }
            int nc = xs.Count, nr = rows.Count;
            var idx = new List<int>();
            for (int r = 0; r < nr - 1; r++)
                for (int c = 0; c < nc - 1; c++)
                {
                    int a = r * nc + c, b = a + 1, d = a + nc, e = d + 1;
                    idx.AddRange(new[] { a, d, b, b, d, e });
                }
            var edges = new List<Vector2Int>();
            for (int c = 0; c < nc - 1; c++) { edges.Add(new Vector2Int(c, c + 1)); edges.Add(new Vector2Int((nr - 1) * nc + c, (nr - 1) * nc + c + 1)); }
            for (int r = 0; r < nr - 1; r++) { edges.Add(new Vector2Int(r * nc, (r + 1) * nc)); edges.Add(new Vector2Int(r * nc + nc - 1, (r + 1) * nc + nc - 1)); }
            var mesh = dp.GetDataProvider<ISpriteMeshDataProvider>();
            mesh.SetVertices(guid, verts.ToArray());
            mesh.SetIndices(guid, idx.ToArray());
            mesh.SetEdges(guid, edges.ToArray());
            dp.Apply();
            ti.SaveAndReimport();
            var spr = Spr(file);
            return $"{file}: {verts.Count} verts, bones {spr.GetBones().Length}";
        }

        // ---------------------------------------------------------------- library

        /// <summary>Category = part, label = drawing.</summary>
        public static readonly (string cat, string label, string file)[] Entries =
        {
            ("torso", "front", "torso_front"), ("torso", "windup", "torso_windup"), ("torso", "strike", "torso_strike"),
            ("head", "front", "head_front"), ("head", "windup", "head_windup"), ("head", "shout", "head_shout"),
            ("far_arm", "front", "far_arm_front"), ("far_arm", "windup", "far_arm_windup"), ("far_arm", "back", "far_arm_back"),
            ("armhole", "front", "armhole_front"), ("armhole", "windup", "armhole_windup"), ("armhole", "strike", "armhole_strike"),
            ("hips", "stand", "hips"), ("hips", "lunge", "hips_lunge"), ("hips", "wind", "hips_wind"),
            ("tails", "stand", "tails"), ("tails", "strike", "tails_strike"),
            ("sleeve", "lit", "sleeve_lit"), ("sleeve", "under", "sleeve_under"),
            ("shoulder", "lit", "shoulder_lit"), ("shoulder", "under", "shoulder_under"),
        };

        public static string BuildLibrary()
        {
            var lib = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            int n = 0;
            foreach (var (cat, label, file) in Entries)
            {
                lib.AddCategoryLabel(Spr(file + ".png"), cat, label); n++;
                var rim = Spr(file + "_rim.png");
                if (rim != null) { lib.AddCategoryLabel(rim, cat + "_rim", label); n++; }
                else if (file == "armhole_front") { lib.AddCategoryLabel(Spr("armhole_front.png"), cat + "_rim", label); n++; }
            }
            AssetDatabase.DeleteAsset(LibPath);
            AssetDatabase.CreateAsset(lib, LibPath);
            return $"library: {n} entries";
        }

        // ---------------------------------------------------------------- prefab

        /// <summary>Draw order inside the hero (a Sorting Group): all rims under all parts.</summary>
        static readonly Dictionary<string, int> Order = new Dictionary<string, int>
        {
            ["tails"] = 10, ["far_arm"] = 20, ["head"] = 30, ["torso"] = 40, ["hips"] = 50, ["shoulder"] = 60,
            ["smear"] = 65, ["sword"] = 70, ["fist"] = 80, ["sleeve"] = 90, ["armhole"] = 100,
        };

        static Transform Bone(string name, Transform parent, Vector2 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static SpriteRenderer Pic(string name, Transform parent, string file, string part, string category = null, string label = null, bool rim = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr(file + ".png");
            sr.sortingOrder = Order[part];
            if (category != null) go.AddComponent<SpriteResolver>().SetCategoryAndLabel(category, label);
            if (rim && Spr(file + "_rim.png") != null || (category == "armhole"))
            {
                var r = new GameObject(name + "_rim");
                r.transform.SetParent(parent, false);
                var rr = r.AddComponent<SpriteRenderer>();
                rr.sprite = Spr(file + (category == "armhole" && label == "front" ? ".png" : "_rim.png"));
                rr.sortingOrder = Order[part] / 10;   // 1..10: under every part
                if (category != null) r.AddComponent<SpriteResolver>().SetCategoryAndLabel(category + "_rim", label);
            }
            return sr;
        }

        public static string BuildPrefab()
        {
            var root = new GameObject("hero4");
            root.AddComponent<SortingGroup>();
            root.AddComponent<SpriteLibrary>().spriteLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(LibPath);
            var waist = G("waist");
            // stance: the whole body tilted about the soles (the clip's own lean of the figure, over the battle's)
            var stance = Bone("stance", root.transform, Vector2.zero);
            var hips = Bone("hips", stance, C2R(waist));
            Pic("hips_pic", hips, "hips", "hips", "hips", "stand");
            var torso = Bone("torso", hips, Vector2.zero);
            Pic("torso_pic", torso, "torso_front", "torso", "torso", "front");
            Pic("armhole_pic", torso, "armhole_front", "armhole", "armhole", "front");
            var neck = Bone("neck", torso, D2U(G("neck") - waist));
            Pic("head_pic", neck, "head_front", "head", "head", "front");
            var knot = Bone("knot", neck, D2U(G("knot") - G("neck")));
            Pic("tails_pic", knot, "tails", "tails", "tails", "stand");
            var far = Bone("far", torso, D2U(G("far_shoulder_front") - waist));
            Pic("far_arm_pic", far, "far_arm_front", "far_arm", "far_arm", "front");
            var shoulder = Bone("shoulder", torso, D2U(G("S_front") - waist));
            float ue = Gf("UE"), len = Gf("L");
            var upper = Bone("upper", shoulder, Vector2.zero);
            upper.localRotation = Quaternion.Euler(0, 0, -70);
            var disc = Pic("shoulder_pic", upper, "shoulder_lit", "shoulder", "shoulder", "lit");
            disc.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var discRim = upper.Find("shoulder_pic_rim");
            discRim.localRotation = Quaternion.Euler(0, 0, 90);
            var fore = Bone("fore", upper, new Vector2(ue / Ppu, 0));
            var hand = Bone("hand", fore, new Vector2((len - ue) / Ppu, 0));
            var fist = Pic("fist_pic", hand, "fist", "fist");
            var grip = G("fist_grip") - G("fist_wrist");
            var sword = Pic("sword_pic", fist.transform, "sword", "sword", rim: false);
            sword.transform.localPosition = D2U(grip);
            sword.transform.localRotation = Quaternion.Euler(0, 0, -(Gf("fist_blade") + 90));
            var target = Bone("target", torso, Vector2.zero);
            // the sleeve: one straight painted sprite bent by the upper and fore bones
            foreach (var (n, file, cat, ord) in new[] { ("sleeve", "sleeve_lit", "sleeve", Order["sleeve"]), ("sleeve_rim", "sleeve_lit_rim", "sleeve_rim", Order["sleeve"] / 10) })
            {
                var go = new GameObject(n);
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr(file + ".png");
                sr.sortingOrder = ord;
                go.AddComponent<SpriteResolver>().SetCategoryAndLabel(cat, "lit");
                var skin = go.AddComponent<SpriteSkin>();
                skin.SetRootBone(upper);
                skin.SetBoneTransforms(new[] { upper, fore });
                skin.alwaysUpdate = true;
            }
            // the smears (each shown on its own strike frame only)
            var smears = new GameObject("smears");
            smears.transform.SetParent(stance, false);
            foreach (JObject sm in Smears)
            {
                var smearGo = new GameObject(Path.GetFileNameWithoutExtension((string)sm["file"]));
                smearGo.transform.SetParent(smears.transform, false);
                var ssr = smearGo.AddComponent<SpriteRenderer>();
                ssr.sprite = Spr((string)sm["file"]);
                ssr.sortingOrder = Order["smear"];
                ssr.enabled = false;
            }
            // IK: Limb Solver 2D on the near arm, the target under the torso so the lean carries the arm
            var ik = root.AddComponent<IKManager2D>();
            var solverGo = new GameObject("ik_arm");
            solverGo.transform.SetParent(root.transform, false);
            var solver = solverGo.AddComponent<LimbSolver2D>();
            var chain = solver.GetChain(0);
            chain.effector = hand;
            chain.target = target;
            solver.constrainRotation = true;
            solver.solveFromDefaultPose = true;
            solver.flip = false;
            solver.Initialize();
            ik.AddSolver(solver);
            ik.alwaysUpdate = true;
            AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return "prefab " + PrefabPath;
        }

        // ---------------------------------------------------------------- clip

        static void Set(AnimationClip clip, string path, System.Type type, string prop, List<(float t, float v)> keys, bool stepped)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                var mode = stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
                AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, prop), curve);
        }

        static Vector2 AngDir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        /// <summary>
        /// The clip from keys.json: a key every frame of the in-betweens r4_keys.py made (joint-space arcs, not straight
        /// target paths), linear between; stepped for the drawing swaps, the flip and the smear.
        /// </summary>
        public static string BuildClip()
        {
            var keys = JObject.Parse(File.ReadAllText(ArtDir + "/keys.json"));
            float fps = (float)keys["fps"];
            float fistBlade = (float)keys["fist_blade"];
            var waist = G("waist");
            float ue = Gf("UE"), len = Gf("L");
            var ch = new Dictionary<string, List<(float, float)>>();
            void K(string name, float t, float v) { if (!ch.TryGetValue(name, out var l)) ch[name] = l = new List<(float, float)>(); l.Add((t, v)); }
            int nFrames = 0;
            foreach (JObject p in (JArray)keys["frames"])
            {
                float t = (int)p["f"] / fps;
                nFrames++;
                float F(string k) => p[k] != null ? (float)p[k] : 0f;
                string body = (string)p["body"];
                // "wind" (the coiled wind-up: hips_wind + the windup upper body) shares the windup set's joints; only its
                // hips drawing differs, pivoted on its own waist and placed by the keyed waist offset (ox, oy)
                bool coil = body == "wind";
                bool wind = body == "windup" || coil, strike = body == "strike";
                // each drawing set's own joints: the strike drawings are pivoted as drawn in the lunge
                var bw = strike ? G("waist_strike") : waist;
                var bNeck = strike ? G("neck_strike") : G("neck");
                var bKnot = strike ? G("knot_strike") : G("knot");
                var bS = strike ? G("S_strike") : G(wind ? "S_windup" : "S_front");
                var bFar = strike ? G("far_shoulder_strike") : G(wind ? "far_shoulder_windup" : "far_shoulder_front");
                // body (drawing convention: y down, + clockwise -> Unity: y up, + counter-clockwise); the hips carry the
                // drawing set's waist offset (ox, oy: the lunge moved so the planted foot stays put)
                var hipsPos = C2R(waist + new Vector2(F("hx") + F("ox"), F("oy")));
                K("hx", t, hipsPos.x); K("hy", t, hipsPos.y);
                var torsoPos = D2U(new Vector2(0, F("dy")));
                K("ty", t, torsoPos.y);
                K("lean", t, -F("lean"));
                K("st", t, -F("st"));
                K("head", t, -F("head"));
                K("tail", t, -F("tail"));
                var nPos = D2U(bNeck - bw); K("nx", t, nPos.x); K("ny", t, nPos.y);
                var kPos = D2U(bKnot - bNeck); K("kx", t, kPos.x); K("ky", t, kPos.y);
                var sPos = D2U(bS - bw) + D2U(new Vector2(F("sx"), F("sy")));
                K("sx", t, sPos.x); K("sy", t, sPos.y);
                var fPos = D2U(bFar - bw) + D2U(new Vector2(F("fx"), F("fy")));
                K("fx", t, fPos.x); K("fy", t, fPos.y);
                // the strike's far arm is behind the tails (drawn so): the tails go up between far arm (20) and head (30)
                K("tails_order", t, strike ? 25 : Order["tails"]);
                K("far", t, -F("far"));
                // the IK target in the torso's space: the wrist of two straight bones at screen angles a1, a1 - b
                float a1 = F("a1"), b = F("b");
                bool straight = (bool)p["straight"];
                var sScreen = new Vector2(sPos.x, sPos.y);     // in torso space before the lean; rotate below
                var arm = AngDir(-a1) * (ue / Ppu) + AngDir(-(a1 - b)) * ((len - ue) / Ppu);
                if (straight) arm = AngDir(-a1) * (len / Ppu * 1.02f);   // out of reach: the solver straightens the arm
                // root space -> torso space: torso = hips * (dy) * R(lean); the arm vector only needs un-rotating
                var armT = (Vector2)(Quaternion.Euler(0, 0, F("lean")) * arm);
                var tgt = sScreen + armT;
                K("tx", t, tgt.x); K("ty_t", t, tgt.y);
                float F2 = a1 - b, blade = F2 - F("turn");
                float rho = blade - fistBlade;                 // the fist's turn from its drawing (drawing convention)
                K("trot", t, -rho + F("lean"));                // target rotation in torso space (Unity degrees)
                K("flip", t, (bool)p["flip"] ? 1 : 0);
                K("smear", t, (bool)p["smear"] ? 1 : 0);
                K("body", t, strike ? 2 : coil ? 3 : wind ? 1 : 0);
                K("band", t, (string)p["sleeve"] == "under" ? 1 : 0);
            }
            var clip = new AnimationClip { frameRate = fps, name = "sword_cut" };
            var tr = typeof(Transform);
            // Every curve is stepped: held key poses, a key per frame, and the battle samples between frames, where a
            // linear bone offset would slide against a stepped drawing swap. A clip writes a whole position: every
            // component it moves must be keyed (the rest would be 0).
            const bool S = true;
            Set(clip, "stance/hips", tr, "m_LocalPosition.x", ch["hx"], S);
            Set(clip, "stance/hips", tr, "m_LocalPosition.y", ch["hy"], S);
            Set(clip, "stance/hips/torso", tr, "m_LocalPosition.y", ch["ty"], S);
            Set(clip, "stance", tr, "localEulerAnglesRaw.z", ch["st"], S);
            Set(clip, "stance/hips/torso", tr, "localEulerAnglesRaw.z", ch["lean"], S);
            Set(clip, "stance/hips/torso/neck", tr, "m_LocalPosition.x", ch["nx"], S);
            Set(clip, "stance/hips/torso/neck", tr, "m_LocalPosition.y", ch["ny"], S);
            Set(clip, "stance/hips/torso/neck", tr, "localEulerAnglesRaw.z", ch["head"], S);
            Set(clip, "stance/hips/torso/neck/knot", tr, "m_LocalPosition.x", ch["kx"], S);
            Set(clip, "stance/hips/torso/neck/knot", tr, "m_LocalPosition.y", ch["ky"], S);
            Set(clip, "stance/hips/torso/neck/knot", tr, "localEulerAnglesRaw.z", ch["tail"], S);
            Set(clip, "stance/hips/torso/far", tr, "m_LocalPosition.x", ch["fx"], S);
            Set(clip, "stance/hips/torso/far", tr, "m_LocalPosition.y", ch["fy"], S);
            Set(clip, "stance/hips/torso/far", tr, "localEulerAnglesRaw.z", ch["far"], S);
            Set(clip, "stance/hips/torso/shoulder", tr, "m_LocalPosition.x", ch["sx"], S);
            Set(clip, "stance/hips/torso/shoulder", tr, "m_LocalPosition.y", ch["sy"], S);
            Set(clip, "stance/hips/torso/target", tr, "m_LocalPosition.x", ch["tx"], S);
            Set(clip, "stance/hips/torso/target", tr, "m_LocalPosition.y", ch["ty_t"], S);
            Set(clip, "stance/hips/torso/target", tr, "localEulerAnglesRaw.z", ch["trot"], S);
            Set(clip, "stance/hips/torso/neck/knot/tails_pic", typeof(SpriteRenderer), "m_SortingOrder", ch["tails_order"], S);
            Set(clip, "ik_arm", typeof(LimbSolver2D), "m_Flip", ch["flip"], true);
            foreach (JObject sm in Smears)
            {
                int f = (int)sm["f"];
                var on = ch["smear"].Select(k => (k.Item1, Mathf.RoundToInt(k.Item1 * fps) == f ? 1f : 0f)).ToList();
                Set(clip, "stance/smears/" + Path.GetFileNameWithoutExtension((string)sm["file"]), typeof(SpriteRenderer), "m_Enabled", on, true);
            }
            // drawing swaps: the resolver's sprite hash, stepped
            // labels by key value: body 0 front | 1 windup | 2 strike | 3 wind (coiled: hips_wind, round 2 head and
            // tails); band 0 lit | 1 under
            var swaps = new (string path, string cat, string[] labels, string key)[]
            {
                ("stance/hips/torso/torso_pic", "torso", new[] { "front", "windup", "strike", "windup" }, "body"),
                ("stance/hips/torso/neck/head_pic", "head", new[] { "front", "windup", "shout", "front" }, "body"),
                ("stance/hips/torso/far/far_arm_pic", "far_arm", new[] { "front", "windup", "back", "windup" }, "body"),
                ("stance/hips/torso/armhole_pic", "armhole", new[] { "front", "windup", "strike", "windup" }, "body"),
                ("stance/hips/hips_pic", "hips", new[] { "stand", "stand", "lunge", "wind" }, "body"),
                ("stance/hips/torso/neck/knot/tails_pic", "tails", new[] { "stand", "stand", "strike", "stand" }, "body"),
                ("sleeve", "sleeve", new[] { "lit", "under" }, "band"),
                ("stance/hips/torso/shoulder/upper/shoulder_pic", "shoulder", new[] { "lit", "under" }, "band"),
            };
            foreach (var s in swaps)
                foreach (var rimTwin in new[] { false, true })
                {
                    var cat = rimTwin ? s.cat + "_rim" : s.cat;
                    var path = rimTwin ? s.path + "_rim" : s.path;
                    var list = ch[s.key].Select(k => (k.Item1, HashValue(LibHash(cat, s.labels[Mathf.RoundToInt(k.Item2)])))).ToList();
                    Set(clip, path, typeof(SpriteResolver), "m_SpriteHash", list, true);
                }
            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return $"clip {ClipPath}: {nFrames} frames, {clip.length:0.000} s";
        }

        /// <summary>How an int hash is stored in a float key (set by the spike: the int's bits or its value).</summary>
        public static bool HashAsBits = true;
        /// <summary>The resolver's key for a category and label (the package's own hash; internal there).</summary>
        public static int LibHash(string cat, string label) =>
            (int)typeof(SpriteLibrary).GetMethod("GetHashForCategoryAndEntry", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Invoke(null, new object[] { cat, label });
        static float HashValue(int h) => HashAsBits ? System.BitConverter.Int32BitsToSingle(h) : h;
    }
}
