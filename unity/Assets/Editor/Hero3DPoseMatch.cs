using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Drakepg.Hero3D;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Do clips made in Blender land in Unity exactly? <c>Hero3DPoseMatch.Run(prefabs...)</c> (edit mode) plays each
    /// imported model's clips at the frames listed in its clip_joints.json (Blender's bone heads in armature space, Z up)
    /// and compares Unity's bone positions with them in the armature node's own space (Blender (x, y, z) = Unity
    /// armature-local (x, y, -z), checked on the rest pose first). Reports the largest error per frame in cm and the bone.
    /// Models are hidden, unsaved copies: the open scene is not touched.
    /// </summary>
    public static class Hero3DPoseMatch
    {
        public static string Run(string dir = null, params string[] prefabs)
        {
            dir ??= Hero3DTest.DefaultDir;
            if (prefabs == null || prefabs.Length == 0)
                prefabs = new[] { "hero_c1", "hero_u2_h" }.Select(n => $"{Hero3DImport.Root}/{n}/{n}.prefab").ToArray();
            var sb = new StringBuilder();
            foreach (var p in prefabs) sb.AppendLine(One(p));
            var text = sb.ToString();
            File.WriteAllText(Path.Combine(dir, "pose_match.txt"), text);
            Debug.Log("drakepg: hero3d pose match\n" + text);
            return text;
        }

        static Vector3 ToUnity(JToken v) => new Vector3((float)v[0], (float)v[1], -(float)v[2]);

        static string One(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) return prefabPath + ": missing";
            var folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            var jointsPath = Path.GetFullPath(folder + "/clip_joints.json");
            if (!File.Exists(jointsPath)) return prefabPath + ": no clip_joints.json";
            var joints = JObject.Parse(File.ReadAllText(jointsPath));
            var fps = (float?)joints["fps"] ?? 24;
            var sb = new StringBuilder();
            var inst = Object.Instantiate(asset);
            inst.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                var anim = inst.GetComponent<Animator>();
                var avatar = anim != null ? anim.avatar : null;
                var kind = avatar != null && avatar.isHuman ? "Humanoid" : "Generic";
                var bones = inst.GetComponentsInChildren<Transform>(true).Where(t => t.GetComponent<SkinnedMeshRenderer>() == null)
                    .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var rest = (JObject)joints["rest"];
                var rig = bones.TryGetValue("Hips", out var hips) ? hips.parent : inst.transform;
                sb.Append($"{Path.GetFileNameWithoutExtension(prefabPath)} ({kind}, avatar {(avatar == null ? "none" : $"{avatar.name} valid {avatar.isValid}")}, armature node {rig.name}): ");

                // Rest pose (the bind pose) checks the axis conversion.
                Hero3DImport.ToBindPose(inst);
                sb.Append("rest " + Compare(rest, bones, rig, out _) + "; ");

                // The clips are in the model file next to the prefab.
                var fbx = Directory.GetFiles(Path.GetFullPath(folder), "*.fbx").FirstOrDefault();
                var fileClips = fbx == null ? new List<AnimationClip>() :
                    AssetDatabase.LoadAllAssetsAtPath(folder + "/" + Path.GetFileName(fbx)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
                if (anim == null || fileClips.Count == 0)
                    return sb.Append($"no clips in Unity ({(anim == null ? "no Animator" : "the import made none")})").ToString();
                var overall = 0f;
                foreach (var clipEntry in ((JObject)joints["clips"]).Properties())
                {
                    var clip = fileClips.FirstOrDefault(c => c.name == clipEntry.Name || c.name.EndsWith("|" + clipEntry.Name));
                    if (clip == null) { sb.Append($"{clipEntry.Name}: no such clip; "); continue; }
                    using (var player = new Hero3DClip(anim, clip))
                    {
                        var frames = ((JObject)clipEntry.Value).Properties().Select(p => int.Parse(p.Name)).OrderBy(f => f).ToList();
                        // Blender frame numbers: the take starts at the first frame (1), Unity's clip time 0.
                        var first = (int?)joints["frame_start"] ?? 1;
                        sb.Append($"{clipEntry.Name} ({clip.length:0.###} s in Unity): ");
                        foreach (var f in frames)
                        {
                            player.SetTime((f - first) / fps);
                            var e = Compare((JObject)clipEntry.Value[f.ToString()], bones, rig, out var err);
                            overall = Mathf.Max(overall, err);
                            sb.Append($"f{f} {e}; ");
                        }
                    }
                }
                sb.Append($"LARGEST {overall * 10:0.0000} mm");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
            return sb.ToString();
        }

        /// <summary>Largest distance (cm) between Unity's bones and Blender's heads, and the bone it is on.</summary>
        static string Compare(JObject blender, Dictionary<string, Transform> bones, Transform rig, out float worst)
        {
            worst = 0;
            string worstBone = "-";
            int n = 0;
            foreach (var p in blender.Properties())
            {
                if (!bones.TryGetValue(p.Name, out var t)) continue;
                var want = ToUnity(p.Value["head"]);
                var have = rig.InverseTransformPoint(t.position);
                var d = (want - have).magnitude * 100;
                n++;
                if (d > worst) { worst = d; worstBone = p.Name; }
            }
            return $"{worst * 10:0.0000} mm ({worstBone})";
        }
    }
}
