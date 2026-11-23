using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Drakepg.Hero3D;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Brings a hero model made in Blender (scripts/hero3d_v2) into the project, driven by its look file (hero_look.json):
    /// <c>Drakepg.EditorTools.Hero3DImport.Run(sourceFolder, name)</c> copies the folder's .fbx, .json and .png files into
    /// Assets/Hero3D/Models/&lt;name&gt;/ (overwriting in place, so GUIDs survive a re-run), sets the model importer (1 unit =
    /// 1 m, no stray root scale or rotation, animation on and uncompressed, imported smooth normals, Generic or Humanoid),
    /// sets the face texture's importer, makes one Hero3DToon material per material the look file names (remapped onto
    /// the model by name; a slot the file doesn't name is left empty and not drawn), and saves &lt;name&gt;.prefab: the model
    /// with a Hero3DModel component holding the rest of the look file (game view, light, size, parts, gear, rim, shadow
    /// mask). Missing fields fall back to defaults; unknown fields are listed in the result and otherwise ignored.
    /// No AssetPostprocessor: the settings are applied here, so nothing else in the project reimports.
    /// </summary>
    public static class Hero3DImport
    {
        public const string Root = "Assets/Hero3D/Models";
        /// <summary>Game size the outline width in metres is converted with: the hero's rest height = 240 design px.</summary>
        const float DrawnHeightPx = 240;
        static readonly string[] Known =
        {
            "shader", "light_dir_blender_world", "light_note", "cut", "materials", "face", "outline", "proportions", "units",
            "light", "shadow_mask", "rim", "parts", "gear", "clips", "game_view", "version", "height_m", "front_yaw",
        };

        /// <summary>
        /// Import `sourceFolder` (absolute, or relative to the repo) as Models/`name`. `lookFolder`: where hero_look.json and
        /// the face come from when the source has none (a clip export of the same model). `humanoid`: build an Avatar
        /// from this model and import its clips as Humanoid (default Generic).
        /// </summary>
        public static string Run(string sourceFolder, string name, string lookFolder = null, bool humanoid = false)
        {
            var log = new StringBuilder();
            if (!Directory.Exists(sourceFolder)) return "no folder " + sourceFolder;
            var dest = Root + "/" + name;
            var destAbs = Path.GetFullPath(dest);
            Directory.CreateDirectory(destAbs);

            // 1. Copy (overwrite in place; the .meta files stay, so a re-run keeps GUIDs).
            var copied = new List<string>();
            void CopyFrom(string folder, bool onlyMissing)
            {
                foreach (var f in Directory.GetFiles(folder))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".fbx" && ext != ".json" && ext != ".png") continue;
                    var to = Path.Combine(destAbs, Path.GetFileName(f));
                    if (onlyMissing && copied.Contains(Path.GetFileName(f))) continue;
                    File.Copy(f, to, true);
                    copied.Add(Path.GetFileName(f));
                }
            }
            CopyFrom(sourceFolder, false);
            if (!string.IsNullOrEmpty(lookFolder) && Directory.Exists(lookFolder)) CopyFrom(lookFolder, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("copied: " + string.Join(", ", copied));

            // 2. The look file.
            var lookName = copied.FirstOrDefault(f => f.EndsWith("_look.json")) ?? copied.FirstOrDefault(f => f.EndsWith(".json") && f.Contains("look"));
            var look = lookName != null ? JObject.Parse(File.ReadAllText(Path.Combine(destAbs, lookName))) : new JObject();
            if (lookName == null) log.AppendLine("no look file: defaults");
            var unknown = look.Properties().Select(p => p.Name).Where(n => !Known.Contains(n)).ToList();
            if (unknown.Count > 0) log.AppendLine("unknown look fields (ignored): " + string.Join(", ", unknown));

            // 3. Face texture.
            var faceInfo = look["face"] as JObject;
            var faceFile = Str(faceInfo?["texture"], "face.png");
            Texture2D faceTex = null;
            if (copied.Contains(faceFile))
            {
                var fp = dest + "/" + faceFile;
                var ti = (TextureImporter)AssetImporter.GetAtPath(fp);
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
                faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(fp);
            }

            // 4. Materials, one per material the look file names.
            var lightView = LightView(look, log);
            var cut = Num(look["light"]?["cut"], Num(look["cut"], 0));
            var outline = look["outline"] as JObject;
            var outlineColor = Col(outline?["colour"] ?? outline?["color"], new Color(0.165f, 0.102f, 0.071f));
            var heightM = Num(look["height_m"], 0);
            float outlinePx;
            if (outline?["width_px_720p"] != null) outlinePx = Num(outline["width_px_720p"], 1);
            else if (outline?["width_px"] != null) outlinePx = Num(outline["width_px"], 1);
            else outlinePx = Num(outline?["width_m"], 0.007f) * DrawnHeightPx / (heightM > 0 ? heightM : 1.75f);
            var outlineChannel = ChannelIndex(outline?["width_vertex_channel"] ?? outline?["per_vertex"]?["channel"] ?? outline?["vertex_width"]?["channel"]);
            var mask = look["shadow_mask"] as JObject;
            var maskChannel = mask != null && Bool(mask["enabled"], true) ? ChannelIndex(mask["channel"] ?? "r") : -1;
            var maskThreshold = Num(mask?["threshold"], 0.5f);

            var matDir = dest + "/Materials";
            Directory.CreateDirectory(Path.GetFullPath(matDir));
            var shader = Shader.Find("Drakepg/Hero3DToon");
            var mats = new Dictionary<string, Material>();
            Material Make(string matName, Color baseC, Color shadeC, Texture tex)
            {
                var path = matDir + "/" + matName + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader) { name = matName };
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetColor("_Color", baseC);
                // The shader multiplies the colour by _ShadeColor on the dark side: shade / base per channel.
                m.SetColor("_ShadeColor", new Color(Ratio(shadeC.r, baseC.r), Ratio(shadeC.g, baseC.g), Ratio(shadeC.b, baseC.b), 1));
                m.SetTexture("_MainTex", tex);
                // Only the face material samples its texture (the other meshes have no UVs).
                m.SetFloat("_HasFace", tex != null ? 1 : 0);
                m.SetFloat("_ShadeStep", cut);
                // Blender's light is fixed in the world of its level camera (light_dir_blender_world = the camera-space
                // vector before the 5 deg tilt), so it goes in Hero3DView's world: camera looking +Z, z toward the viewer = -Z.
                m.SetVector("_LightDir", new Vector4(lightView.x, lightView.y, -lightView.z, 0));
                m.SetFloat("_LightSpace", 0);
                m.SetFloat("_UseVertexColor", 0);
                m.SetColor("_OutlineColor", outlineColor);
                m.SetFloat("_OutlinePx", outlinePx);
                m.SetFloat("_OutlineVertexChannel", outlineChannel);
                m.SetFloat("_ShadowMaskChannel", maskChannel);
                m.SetFloat("_ShadowMaskThreshold", maskThreshold);
                EditorUtility.SetDirty(m);
                mats[matName] = m;
                return m;
            }
            if (look["materials"] is JObject matList)
                foreach (var p in matList.Properties())
                    Make(p.Name, Col(p.Value["base"], Color.white), Col(p.Value["shade"], Color.gray), null);
            if (faceInfo != null)
            {
                // The face: the texture is the albedo; shaded like the material it "colours as" (its base shows where the texture is clear).
                var asName = Str(faceInfo["colours_as"], "Skin");
                var asMat = look["materials"]?[asName];
                Make(Str(faceInfo["material"], "Face"), Col(asMat?["base"], Color.white), Col(asMat?["shade"], Color.gray), faceTex);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"materials: {string.Join(", ", mats.Keys)}; light (view) {lightView.x:0.###},{lightView.y:0.###},{lightView.z:0.###}; cut {cut}; outline {outlinePx:0.##} px");

            // 5. The model importer.
            var fbxName = copied.FirstOrDefault(f => f.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));
            if (fbxName == null) return log.Append("no .fbx").ToString();
            var fbxPath = dest + "/" + fbxName;
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            mi.globalScale = 1;
            mi.useFileScale = true;
            mi.useFileUnits = true;
            mi.bakeAxisConversion = true;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.addCollider = false;
            mi.isReadable = true;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeMeshVertices = false;
            mi.optimizeMeshPolygons = false;
            mi.weldVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Off;
            mi.resampleCurves = true;
            mi.optimizeGameObjects = false;
            mi.sortHierarchyByName = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.SaveAndReimport();
            if (humanoid)
            {
                // Unity's Humanoid setup fails (no message) when a mesh object has a bone's name: round 1's "Head" mesh
                // and "Head" bone. Name the clash; if the avatar fails, keep Generic.
                var g = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath).GetComponentsInChildren<Transform>(true);
                var boneNames = new HashSet<string>(g.Where(t => t.GetComponent<SkinnedMeshRenderer>() == null).Select(t => t.name));
                var clash = g.Where(t => t.GetComponent<SkinnedMeshRenderer>() != null && boneNames.Contains(t.name)).Select(t => t.name).ToList();
                if (clash.Count > 0) log.AppendLine("mesh objects named like bones (Unity's Humanoid setup fails on these; rename them in the model): " + string.Join(", ", clash));
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.SaveAndReimport();
                var av = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
                if (av == null || !av.isValid || !av.isHuman)
                {
                    log.AppendLine("Humanoid avatar failed: kept Generic");
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.SaveAndReimport();
                }
            }
            // Clips: keep each take's own range; root kept exactly as in the file (matters for Humanoid).
            var defaults = mi.defaultClipAnimations;
            if (defaults.Length > 0)
            {
                foreach (var c in defaults)
                {
                    c.lockRootRotation = false;
                    c.lockRootHeightY = false;
                    c.lockRootPositionXZ = false;
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = true;
                    c.heightFromFeet = false;
                    c.loopTime = false;
                }
                mi.clipAnimations = defaults;
                mi.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            log.AppendLine($"model {fbxPath}: root pos {model.transform.localPosition} rot {model.transform.localEulerAngles} scale {model.transform.localScale}; " +
                           $"clips {(clips.Count == 0 ? "none" : string.Join(", ", clips.Select(c => $"{c.name} {c.length:0.###}s @{c.frameRate}fps")))}; " +
                           $"avatar {(avatar == null ? "none" : $"{avatar.name} valid {avatar.isValid} human {avatar.isHuman}")}");
            foreach (Transform t in model.transform)
                log.AppendLine($"  child {t.name}: pos {t.localPosition} rot {t.localEulerAngles} scale {t.localScale}");

            // 6. The prefab: the model with its look data; unknown material slots emptied (not drawn).
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                inst.hideFlags = HideFlags.HideAndDontSave;
                var verts = 0;
                var emptied = new List<string>();
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.quality = SkinQuality.Bone4;
                    smr.updateWhenOffscreen = true;
                    var sm = smr.sharedMaterials;
                    for (int i = 0; i < sm.Length; i++)
                        if (sm[i] == null || !mats.ContainsValue(sm[i]))
                        {
                            emptied.Add($"{smr.name}[{i}] {(sm[i] != null ? sm[i].name : "none")} ({smr.sharedMesh.GetSubMesh(i).indexCount / 3} tris)");
                            sm[i] = null;
                        }
                    smr.sharedMaterials = sm;
                    verts += smr.sharedMesh.vertexCount;
                }
                log.AppendLine($"vertices as imported: {verts}; slots not in the look file (not drawn): {(emptied.Count == 0 ? "none" : string.Join("; ", emptied))}");
                // Hard edges (one position, several normals) open gaps in the hull outline: count them.
                var hard = new List<string>();
                var noColours = new List<string>();
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var me = smr.sharedMesh;
                    var vs = me.vertices;
                    var ns = me.normals;
                    var seen = new Dictionary<Vector3, Vector3>();
                    int split = 0;
                    for (int i = 0; i < vs.Length && i < ns.Length; i++)
                    {
                        var key = new Vector3(Mathf.Round(vs[i].x * 1e5f), Mathf.Round(vs[i].y * 1e5f), Mathf.Round(vs[i].z * 1e5f));
                        if (seen.TryGetValue(key, out var n0)) { if (Vector3.Angle(n0, ns[i]) > 1) split++; }
                        else seen[key] = ns[i];
                    }
                    if (split > 0) hard.Add($"{smr.name} {split}");
                    if (me.colors32 == null || me.colors32.Length == 0) noColours.Add(smr.name);
                }
                log.AppendLine("hard-edge vertices (same place, another normal; the hull outline can gap there): " + (hard.Count == 0 ? "none" : string.Join(", ", hard)));
                // A vertex-colour channel the meshes don't have would read as a constant: switch those features off.
                if (noColours.Count > 0 && (maskChannel >= 0 || outlineChannel >= 0))
                {
                    log.AppendLine($"no vertex colours in {string.Join(", ", noColours)}: shadow mask / per-vertex outline width asked for but switched off");
                    foreach (var m in mats.Values)
                    {
                        m.SetFloat("_ShadowMaskChannel", -1);
                        m.SetFloat("_OutlineVertexChannel", -1);
                        EditorUtility.SetDirty(m);
                    }
                    maskChannel = -1;
                }

                var hm = inst.GetComponent<Hero3DModel>() ?? inst.AddComponent<Hero3DModel>();
                hm.lookVersion = Str(look["version"], "1");
                var rest = RestHeight(inst);
                hm.restHeightM = rest;
                // Blender's game scale (render.py GAME_PPM = 240 / 1.75): 1.75 m = the drawn hero's 240 px, unless the file says otherwise.
                hm.heightM = heightM > 0 ? heightM : 1.75f;
                hm.frontYaw = FrontYaw(look, inst, log);
                var gv = look["game_view"];
                hm.gameYaw = Num(gv?["yaw"], 40);
                hm.gamePitch = Num(gv?["pitch"], 5);
                hm.lightView = lightView;
                hm.cut = cut;
                var rim = look["rim"] as JObject;
                hm.rim = rim != null && Bool(rim["enabled"], true);
                hm.rimColor = Col(rim?["colour"] ?? rim?["color"], hm.rimColor);
                hm.rimWidthPx = Num(rim?["width_px_720p"] ?? rim?["width_px"], 1);
                hm.shadowMask = maskChannel >= 0;
                hm.shadowMaskLayer = Str(mask?["layer"], "");
                hm.shadowMaskChannel = maskChannel;
                hm.shadowMaskThreshold = maskThreshold;
                hm.parts = Names(look["parts"]);
                if (hm.parts.Count == 0)
                    hm.parts = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.name).ToList();
                hm.gear = new List<Hero3DModel.Gear>();
                if (look["gear"] is JArray gearList)
                    foreach (var g in gearList.OfType<JObject>())
                        hm.gear.Add(new Hero3DModel.Gear
                        {
                            id = Str(g["id"], ""), slot = Str(g["slot"], ""), bone = Str(g["bone"], ""), socket = Str(g["socket"], ""),
                            objects = Names(g["objects"]).ToArray(), hides = Names(g["hides"]).ToArray(),
                        });
                hm.clips = Names(look["clips"]);
                // Start bare: gear objects off.
                foreach (var g in hm.gear)
                    foreach (var o in g.objects)
                    {
                        var t = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == o);
                        if (t != null) t.gameObject.SetActive(false);
                        else log.AppendLine($"gear {g.id}: no object {o}");
                    }
                foreach (var p in hm.parts)
                    if (inst.GetComponentsInChildren<Transform>(true).All(x => x.name != p)) log.AppendLine($"part {p}: no such object");
                inst.hideFlags = HideFlags.None;
                var prefabPath = dest + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath, out var ok);
                log.AppendLine($"prefab {prefabPath} saved {ok}; height of the file's default pose {rest:0.0000} m (scale: {hm.heightM:0.0000} m = 240 px); view yaw {hm.gameYaw} pitch {hm.gamePitch}; parts {hm.parts.Count}, gear {hm.gear.Count}; rim {hm.rim}; shadow mask {hm.shadowMask}");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets();
            var result = log.ToString();
            Debug.Log("drakepg: hero3d import " + name + "\n" + result);
            return result;
        }

        /// <summary>Put every skinned bone back where the meshes were bound to it (the rest pose), parents first.</summary>
        public static void ToBindPose(GameObject root)
        {
            var world = new Dictionary<Transform, Matrix4x4>();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                var bind = smr.sharedMesh.bindposes;
                for (int i = 0; i < bones.Length && i < bind.Length; i++)
                    if (bones[i] != null && !world.ContainsKey(bones[i]))
                        world[bones[i]] = smr.transform.localToWorldMatrix * bind[i].inverse;
            }
            int Depth(Transform t) { int d = 0; for (; t != null; t = t.parent) d++; return d; }
            foreach (var kv in world.OrderBy(kv => Depth(kv.Key)))
            {
                kv.Key.position = kv.Value.GetColumn(3);
                kv.Key.rotation = kv.Value.rotation;
            }
        }

        /// <summary>Top of the model above its origin in the pose it has (the file's rest pose), from a bake of every skinned mesh.</summary>
        public static float RestHeight(GameObject root)
        {
            float top = float.MinValue, low = float.MaxValue;
            var mesh = new Mesh();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(mesh, true);
                var m = Matrix4x4.TRS(smr.transform.position - root.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                {
                    var y = m.MultiplyPoint3x4(v).y;
                    top = Mathf.Max(top, y);
                    low = Mathf.Min(low, y);
                }
            }
            Object.DestroyImmediate(mesh);
            return top - Mathf.Min(0, low);
        }

        /// <summary>
        /// Which way the model faces, as a turn from +Z: the look file's "front_yaw" if given, else from the feet (the
        /// average of toe bone minus foot bone, for bones whose names end in "Toes" and "Foot"), else 0.
        /// </summary>
        static float FrontYaw(JObject look, GameObject root, StringBuilder log)
        {
            if (look["front_yaw"] != null) return Num(look["front_yaw"], 0);
            var bones = root.GetComponentsInChildren<Transform>(true);
            var sum = Vector3.zero;
            foreach (var toe in bones.Where(b => b.name.EndsWith("Toes")))
            {
                var foot = bones.FirstOrDefault(b => b.name == toe.name.Substring(0, toe.name.Length - 4) + "Foot");
                if (foot != null) sum += toe.position - foot.position;
            }
            sum.y = 0;
            if (sum.sqrMagnitude < 1e-6f)
            {
                log.AppendLine("front: no Foot/Toes bones, assumed +Z");
                return 0;
            }
            var yaw = Mathf.Atan2(sum.x, sum.z) * Mathf.Rad2Deg;
            // Snap to a quarter turn: the rest pose's feet may point a little out.
            yaw = Mathf.Round(yaw / 90) * 90;
            log.AppendLine($"front from the feet: {sum.normalized} -> yaw {yaw}");
            return yaw;
        }

        /// <summary>The light in camera space (x right, y up, z toward the viewer): light.dir_view, else the vector in light_note, else a default.</summary>
        static Vector3 LightView(JObject look, StringBuilder log)
        {
            var v = Vec(look["light"]?["dir_view"]) ?? Vec(look["light_dir_view"]);
            if (v == null && look["light_note"] != null)
            {
                var m = Regex.Match(look["light_note"].ToString(), @"\(\s*([-0-9.eE]+)\s*,\s*([-0-9.eE]+)\s*,\s*([-0-9.eE]+)\s*\)");
                if (m.Success)
                    v = new Vector3(P(m.Groups[1].Value), P(m.Groups[2].Value), P(m.Groups[3].Value));
            }
            if (v == null)
            {
                log.AppendLine("no camera-space light: default (0.55, 0.65, 0.52)");
                v = new Vector3(0.55f, 0.65f, 0.52f);
            }
            return v.Value.normalized;
        }

        static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static float Ratio(float a, float b) => b > 1e-4f ? Mathf.Min(1, a / b) : 1;

        static Vector3? Vec(JToken t)
        {
            if (t is JArray a && a.Count >= 3) return new Vector3((float)a[0], (float)a[1], (float)a[2]);
            if (t is JObject o && o["x"] != null) return new Vector3(Num(o["x"], 0), Num(o["y"], 0), Num(o["z"], 0));
            return null;
        }

        static float Num(JToken t, float d)
        {
            if (t == null || t.Type == JTokenType.Null) return d;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (float)t;
            return float.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : d;
        }

        static string Str(JToken t, string d) => t == null || t.Type == JTokenType.Null ? d : t.ToString();
        static bool Bool(JToken t, bool d) => t == null || t.Type != JTokenType.Boolean ? d : (bool)t;

        /// <summary>"#rrggbb", "#rrggbbaa", or [r, g, b(, a)] in 0-1 or 0-255.</summary>
        static Color Col(JToken t, Color d)
        {
            if (t == null) return d;
            if (t.Type == JTokenType.String && ColorUtility.TryParseHtmlString(t.ToString(), out var c)) return c;
            if (t is JArray a && a.Count >= 3)
            {
                var v = a.Select(x => (float)x).ToArray();
                var k = v.Take(3).Any(x => x > 1) ? 1 / 255f : 1;
                return new Color(v[0] * k, v[1] * k, v[2] * k, v.Length > 3 ? v[3] * k : 1);
            }
            return d;
        }

        /// <summary>"r"/"g"/"b"/"a" or 0-3; -1 when absent.</summary>
        static int ChannelIndex(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return -1;
            if (t.Type == JTokenType.Integer) return Mathf.Clamp((int)t, 0, 3);
            var s = t.ToString().ToLowerInvariant();
            return s.StartsWith("r") ? 0 : s.StartsWith("g") ? 1 : s.StartsWith("b") ? 2 : s.StartsWith("a") ? 3 : -1;
        }

        /// <summary>A list of names: strings, objects with a name/object/id, or an object's keys.</summary>
        static List<string> Names(JToken t)
        {
            var list = new List<string>();
            if (t is JArray a)
                foreach (var x in a)
                {
                    if (x.Type == JTokenType.String) list.Add(x.ToString());
                    else if (x is JObject o) { var n = o["name"] ?? o["object"] ?? o["id"]; if (n != null) list.Add(n.ToString()); }
                }
            else if (t is JObject obj) list.AddRange(obj.Properties().Select(p => p.Name));
            else if (t != null && t.Type == JTokenType.String) list.Add(t.ToString());
            return list;
        }
    }
}
