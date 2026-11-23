using System.Collections.Generic;
using UnityEngine;

namespace Drakepg.Hero3D
{
    /// <summary>
    /// A stand-in rigged hero made in code (no downloads): one SkinnedMeshRenderer over 11 bones (hips, spine, head, two
    /// arms with elbows, two legs with knees), built from capsules in flat vertex colours, about 1.75 units tall, facing
    /// +Z with his right on +X. Posed to test self-occlusion: the right forearm across the chest, the right leg forward.
    /// The head carries face UVs (a planar front projection) for the face texture.
    /// </summary>
    public static class StandinBuilder
    {
        static readonly Color Skin = new Color(0.96f, 0.78f, 0.62f), Tunic = new Color(0.22f, 0.42f, 0.72f),
            Sleeve = new Color(0.18f, 0.34f, 0.6f), Belt = new Color(0.42f, 0.26f, 0.14f), Pants = new Color(0.5f, 0.38f, 0.26f),
            Boots = new Color(0.27f, 0.17f, 0.11f), Hair = new Color(0.36f, 0.2f, 0.1f);

        enum B { Hips, Spine, Head, UpperArmR, LowerArmR, UpperArmL, LowerArmL, UpperLegR, LowerLegR, UpperLegL, LowerLegL }

        /// <summary>Build the rig under `parent` (its local origin = the feet). Returns the renderer; the rig root is its transform.</summary>
        public static SkinnedMeshRenderer Build(Transform parent, Material material, bool posed = true)
        {
            var root = new GameObject("standin").transform;
            root.SetParent(parent, false);
            // Bind pose, in root space (no rotations, so root space = each bone's frame before posing).
            var at = new Dictionary<B, (Vector3 pos, B? parent)>
            {
                [B.Hips] = (new Vector3(0, 0.95f, 0), null),
                [B.Spine] = (new Vector3(0, 1.1f, 0), B.Hips),
                [B.Head] = (new Vector3(0, 1.44f, 0), B.Spine),
                [B.UpperArmR] = (new Vector3(0.22f, 1.38f, 0), B.Spine),
                [B.LowerArmR] = (new Vector3(0.22f, 1.12f, 0), B.UpperArmR),
                [B.UpperArmL] = (new Vector3(-0.22f, 1.38f, 0), B.Spine),
                [B.LowerArmL] = (new Vector3(-0.22f, 1.12f, 0), B.UpperArmL),
                [B.UpperLegR] = (new Vector3(0.1f, 0.9f, 0), B.Hips),
                [B.LowerLegR] = (new Vector3(0.1f, 0.5f, 0), B.UpperLegR),
                [B.UpperLegL] = (new Vector3(-0.1f, 0.9f, 0), B.Hips),
                [B.LowerLegL] = (new Vector3(-0.1f, 0.5f, 0), B.UpperLegL),
            };
            var count = at.Count;
            var bones = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var b = (B)i;
                var t = new GameObject(b.ToString()).transform;
                var p = at[b].parent;
                t.SetParent(p.HasValue ? bones[(int)p.Value] : root, false);
                t.position = root.TransformPoint(at[b].pos);
                bones[i] = t;
            }

            var m = new MeshParts();
            // Torso and pelvis.
            m.Capsule(new Vector3(0, 1.0f, 0), new Vector3(0, 1.32f, 0), 0.17f, 0.72f, Tunic, (int)B.Spine);
            m.Capsule(new Vector3(-0.09f, 0.92f, 0), new Vector3(0.09f, 0.92f, 0), 0.13f, 0.8f, Belt, (int)B.Hips);
            m.Capsule(new Vector3(0, 1.38f, 0), new Vector3(0, 1.5f, 0), 0.055f, 1, Skin, (int)B.Head);
            // Head with the face, and hair: a slightly bigger ball set back and up, so the face shows in front.
            m.Capsule(new Vector3(0, 1.6f, 0), new Vector3(0, 1.6f, 0), 0.15f, 1, Skin, (int)B.Head, face: true);
            m.Capsule(new Vector3(0, 1.63f, -0.035f), new Vector3(0, 1.63f, -0.035f), 0.152f, 1, Hair, (int)B.Head);
            foreach (var s in new[] { 1, -1 })
            {
                var x = 0.22f * s;
                var r = s > 0;
                m.Capsule(new Vector3(x, 1.37f, 0), new Vector3(x, 1.12f, 0), 0.062f, 1, Sleeve, (int)(r ? B.UpperArmR : B.UpperArmL));
                m.Capsule(new Vector3(x, 1.12f, 0), new Vector3(x, 0.9f, 0), 0.05f, 1, Skin, (int)(r ? B.LowerArmR : B.LowerArmL));
                m.Capsule(new Vector3(x, 0.86f, 0.01f), new Vector3(x, 0.86f, 0.01f), 0.062f, 1, Skin, (int)(r ? B.LowerArmR : B.LowerArmL));
                var lx = 0.1f * s;
                m.Capsule(new Vector3(lx, 0.88f, 0), new Vector3(lx, 0.5f, 0), 0.08f, 1, Pants, (int)(r ? B.UpperLegR : B.UpperLegL));
                m.Capsule(new Vector3(lx, 0.5f, 0), new Vector3(lx, 0.1f, 0), 0.068f, 1, Boots, (int)(r ? B.LowerLegR : B.LowerLegL));
                m.Capsule(new Vector3(lx, 0.06f, -0.02f), new Vector3(lx, 0.06f, 0.15f), 0.06f, 1, Boots, (int)(r ? B.LowerLegR : B.LowerLegL));
            }

            var mesh = m.ToMesh();
            var bind = new Matrix4x4[count];
            for (int i = 0; i < count; i++) bind[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            mesh.bindposes = bind;

            var smr = root.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[(int)B.Hips];
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = true;
            if (posed) Pose(bones);
            // Stand on the ground: the lowest point of the posed body at the root's origin.
            var probe = new Mesh();
            smr.BakeMesh(probe, true);
            var low = float.MaxValue;
            foreach (var p in probe.vertices) low = Mathf.Min(low, p.y);
            Object.Destroy(probe);
            bones[(int)B.Hips].localPosition -= new Vector3(0, low, 0);
            return smr;
        }

        /// <summary>The test pose: right (near) forearm across the chest, right leg forward, left leg back.</summary>
        static void Pose(Transform[] bones)
        {
            var root = bones[0].parent;
            void Aim(B b, Vector3 dir)
            {
                // Point the bone's rest direction (down) along `dir` (root space).
                var t = bones[(int)b];
                var want = Quaternion.FromToRotation(Vector3.down, dir.normalized);
                var parentRot = Quaternion.Inverse(root.rotation) * t.parent.rotation;
                t.localRotation = Quaternion.Inverse(parentRot) * want;
            }
            bones[(int)B.Spine].localRotation = Quaternion.Euler(4, -8, 0);
            Aim(B.UpperArmR, new Vector3(-0.05f, -0.6f, 0.8f));
            Aim(B.LowerArmR, new Vector3(-1f, 0.3f, 0.15f));
            Aim(B.UpperArmL, new Vector3(-0.2f, -1f, -0.2f));
            Aim(B.LowerArmL, new Vector3(-0.1f, -1f, 0.2f));
            Aim(B.UpperLegR, new Vector3(0.05f, -0.9f, 0.42f));
            Aim(B.LowerLegR, new Vector3(0, -1f, 0.05f));
            Aim(B.UpperLegL, new Vector3(-0.03f, -0.95f, -0.3f));
            Aim(B.LowerLegL, new Vector3(0, -0.85f, -0.5f));
            bones[(int)B.Head].localRotation = Quaternion.Euler(-4, 6, 0);
        }

        /// <summary>A small face texture made in code: eyes, brows, a mouth and blush on clear ground (laid over the skin).</summary>
        public static Texture2D Face()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "standin face", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float Ellipse(float x, float y, float cx, float cy, float rx, float ry) =>
                Mathf.Clamp01((1 - Mathf.Sqrt((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry))) * rx * 1.2f);
            var ink = new Color(0.12f, 0.08f, 0.1f);
            var blush = new Color(0.95f, 0.55f, 0.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x + 0.5f, v = y + 0.5f;
                    var c = new Color(0, 0, 0, 0);
                    var cheek = Mathf.Max(Ellipse(u, v, 17, 25, 6, 3.5f), Ellipse(u, v, 47, 25, 6, 3.5f)) * 0.5f;
                    if (cheek > 0) c = new Color(blush.r, blush.g, blush.b, cheek);
                    var eye = Mathf.Max(Ellipse(u, v, 22, 33, 3.6f, 5.5f), Ellipse(u, v, 42, 33, 3.6f, 5.5f));
                    var brow = Mathf.Max(Ellipse(u, v, 21, 43, 6, 1.6f), Ellipse(u, v, 43, 43, 6, 1.6f));
                    var mouth = Ellipse(u, v, 32, 19, 5.5f, 1.5f);
                    var a = Mathf.Max(eye, Mathf.Max(brow, mouth));
                    if (a > 0) c = Color.Lerp(c, new Color(ink.r, ink.g, ink.b, 1), a);
                    // Light in the eyes.
                    var glint = Mathf.Max(Ellipse(u, v, 23, 35, 1.3f, 1.6f), Ellipse(u, v, 43, 35, 1.3f, 1.6f));
                    if (glint > 0) c = Color.Lerp(c, Color.white, glint);
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Capsules (two hemispheres and a tube, smooth normals) with one bone and one colour each.</summary>
        class MeshParts
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<BoneWeight> w = new List<BoneWeight>();
            readonly List<int> tri = new List<int>();
            const int Around = 20, Cap = 6;

            /// <summary>From `a` to `b` (equal: a ball), radius `r`, flattened front to back by `depth`. `face`: front UVs.</summary>
            public void Capsule(Vector3 a, Vector3 b, float r, float depth, Color color, int bone, bool face = false)
            {
                var axis = b - a;
                axis = axis.sqrMagnitude < 1e-8f ? Vector3.up : axis.normalized;
                var u = Vector3.Cross(axis, Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
                var w2 = Vector3.Cross(axis, u);
                var start = v.Count;
                var rings = 0;
                for (int half = 0; half < 2; half++)
                    for (int i = 0; i <= Cap; i++)
                    {
                        var lat = half == 0 ? -Mathf.PI / 2 + i * (Mathf.PI / 2) / Cap : i * (Mathf.PI / 2) / Cap;
                        var center = half == 0 ? a : b;
                        for (int k = 0; k <= Around; k++)
                        {
                            var phi = k * Mathf.PI * 2 / Around;
                            var radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * w2;
                            var dir = Mathf.Sin(lat) * axis + Mathf.Cos(lat) * radial;
                            var off = dir * r;
                            off.z *= depth;
                            var nn = dir;
                            nn.z /= depth;
                            v.Add(center + off);
                            n.Add(nn.normalized);
                            c.Add(color);
                            w.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                            // Face: a planar projection of the front of the head (the texture covers 1.25 radii).
                            uv.Add(face && dir.z > 0.2f ? new Vector2(0.5f + off.x / (r * 1.25f), 0.5f + off.y / (r * 1.25f)) : new Vector2(-1, -1));
                        }
                        rings++;
                    }
                // Wind so faces point outward: decided once on a quad at the waist (the poles are degenerate).
                int m0 = start + (Cap - 1) * (Around + 1);
                var outward = Vector3.Dot(Vector3.Cross(v[m0 + Around + 1] - v[m0], v[m0 + 1] - v[m0]), n[m0]) >= 0;
                for (int ring = 0; ring < rings - 1; ring++)
                    for (int k = 0; k < Around; k++)
                    {
                        int i0 = start + ring * (Around + 1) + k, i1 = i0 + 1, j0 = i0 + Around + 1, j1 = j0 + 1;
                        if (outward) { tri.Add(i0); tri.Add(j0); tri.Add(i1); tri.Add(i1); tri.Add(j0); tri.Add(j1); }
                        else { tri.Add(i0); tri.Add(i1); tri.Add(j0); tri.Add(i1); tri.Add(j1); tri.Add(j0); }
                    }
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { name = "standin" };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetColors(c);
                mesh.SetUVs(0, uv);
                mesh.boneWeights = w.ToArray();
                mesh.SetTriangles(tri, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
