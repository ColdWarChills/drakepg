#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg.Bones
{
    /// <summary>
    /// The blade's trail: a strip between the weapon tip and a point down the blade, from poses sampled every few ms
    /// of the move (not once per drawn frame), so a fast cut leaves a round arc. Bright at the tip, clear at the inner
    /// edge, fading with age. Lives beside the hero (not under it), named "blade trail" so the kit's clean pass and
    /// box skip it.
    /// </summary>
    public class BoneTrail : MonoBehaviour
    {
        /// <summary>Move ms between two sampled poses.</summary>
        public const float StepMs = 4;
        /// <summary>How long a sampled edge stays (move ms).</summary>
        public float LifeMs = 110;
        public float Alpha = 0.55f;
        public Color Tint = new Color(1, 0.98f, 0.92f);
        /// <summary>
        /// Smear mode ("trail:smear"): a short solid crescent instead of a fan. Each sampled edge spans only the outer
        /// part of the blade (SmearInner..tip), older edges shrink toward the tip (a tapered tail), and the inner edge
        /// keeps some alpha, so it reads as the blade's own blur on the fastest frames, not a pale sheet.
        /// </summary>
        public bool Smear;
        public float SmearLifeMs = 60, SmearAlpha = 0.85f, SmearInner = 0.55f;

        struct Edge
        {
            public Vector3 Tip, Inner;
            public float T;
            public int Side;
        }

        readonly List<Edge> edges = new List<Edge>();
        Mesh mesh;
        MeshRenderer mr;
        float now;
        static Material material;

        public bool Busy => edges.Count > 0;
        public int Order { set { if (mr != null) mr.sortingOrder = value; } }

        public static BoneTrail Create(BoneHero hero)
        {
            var go = new GameObject("blade trail");
            go.transform.SetParent(hero.transform.parent, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var t = go.AddComponent<BoneTrail>();
            t.mesh = new Mesh { name = "blade trail" };
            t.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
            t.mr = go.AddComponent<MeshRenderer>();
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                material = shader != null ? new Material(shader) { name = "blade trail" } : null;
            }
            t.mr.sharedMaterial = material;
            t.mr.sortingOrder = hero.Order + 1;
            return t;
        }

        public void Clear()
        {
            edges.Clear();
            if (mesh != null) mesh.Clear();
        }

        public void Add(int side, Vector3 tip, Vector3 inner, float t)
        {
            // keep them in time order (sub-frame samples come in order; a repeat of the same time is skipped)
            for (int i = edges.Count - 1; i >= 0; i--)
                if (edges[i].Side == side && Mathf.Abs(edges[i].T - t) < 0.01f) return;
            edges.Add(new Edge { Tip = transform.InverseTransformPoint(tip), Inner = transform.InverseTransformPoint(inner), T = t, Side = side });
        }

        /// <summary>Drop edges older than LifeMs at move time `t` and rebuild the strip.</summary>
        public void Age(float t)
        {
            now = t;
            float life = Smear ? SmearLifeMs : LifeMs;
            edges.RemoveAll(e => now - e.T > life);
            Build();
        }

        void Build()
        {
            mesh.Clear();
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count, n = 0;
                foreach (var e in edges)
                {
                    if (e.Side != side) continue;
                    if (Smear)
                    {
                        float ks = Mathf.Clamp01(1 - (now - e.T) / SmearLifeMs);
                        // the band near the tip, narrowing to the tip as it ages
                        var inner = Vector3.Lerp(e.Tip, Vector3.Lerp(e.Inner, e.Tip, SmearInner), ks);
                        verts.Add(e.Tip);
                        verts.Add(inner);
                        var cs = Tint;
                        cs.a = SmearAlpha * ks;
                        cols.Add(cs);
                        cs.a = SmearAlpha * ks * 0.45f;
                        cols.Add(cs);
                        n++;
                        continue;
                    }
                    float k = Mathf.Clamp01(1 - (now - e.T) / LifeMs);
                    verts.Add(e.Tip);
                    verts.Add(e.Inner);
                    var c = Tint;
                    c.a = Alpha * k * k;
                    cols.Add(c);
                    c.a = 0;
                    cols.Add(c);
                    n++;
                }
                for (int i = 0; i + 1 < n; i++)
                {
                    int a = start + i * 2;
                    tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
#endif
