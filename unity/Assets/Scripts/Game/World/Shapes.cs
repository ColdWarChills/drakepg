using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// Flat colored shapes in the world, like a Phaser Graphics object: fill rects, ellipses, triangles and polygons,
    /// stroke lines, all in design px relative to the object's position (x right, y down). Call the drawing methods,
    /// they rebuild one mesh. Used for props, blockers, doorways and chests until they get art.
    /// </summary>
    public class Shapes : MonoBehaviour
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Color> baseColors = new List<Color>();
        readonly List<int> tris = new List<int>();
        Mesh mesh;
        MeshRenderer meshRenderer;
        float alpha = 1;
        bool dirty;

        static Material material;

        /// <summary>
        /// Vertex colors, unlit (Resources/Fx/Shape.shader), shared by every shape. Sprite shaders don't work on plain
        /// meshes: they read the sprite renderer's color, which a mesh renderer leaves at zero.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                material = new Material(Resources.Load<Shader>("Fx/Shape")) { name = "Shapes" };
                return material;
            }
        }

        public static Shapes Create(Transform parent, float x, float y, int order, string name = "shapes")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Design.World(x, y, 1);
            var s = go.AddComponent<Shapes>();
            s.mesh = new Mesh { name = name };
            s.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = s.mesh;
            s.meshRenderer = go.AddComponent<MeshRenderer>();
            s.meshRenderer.sharedMaterial = Material;
            s.meshRenderer.sortingOrder = order;
            return s;
        }

        public int Order
        {
            get => meshRenderer.sortingOrder;
            set => meshRenderer.sortingOrder = value;
        }

        /// <summary>Position in design px.</summary>
        public void SetPosition(float x, float y) => transform.position = Design.World(x, y, transform.position.z);

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                dirty = true;
            }
        }

        public Shapes Clear()
        {
            verts.Clear();
            baseColors.Clear();
            tris.Clear();
            dirty = true;
            return this;
        }

        static Vector3 V(float x, float y) => new Vector3(x / Design.PixelsPerUnit, -y / Design.PixelsPerUnit, 0);

        int Add(float x, float y, Color c)
        {
            verts.Add(V(x, y));
            baseColors.Add(c);
            dirty = true;
            return verts.Count - 1;
        }

        /// <summary>A convex polygon (fan from the first point).</summary>
        public Shapes Poly(Color c, params Vector2[] pts)
        {
            if (pts.Length < 3) return this;
            var first = Add(pts[0].x, pts[0].y, c);
            for (int i = 1; i < pts.Length; i++) Add(pts[i].x, pts[i].y, c);
            for (int i = 1; i < pts.Length - 1; i++)
            {
                tris.Add(first);
                tris.Add(first + i);
                tris.Add(first + i + 1);
            }
            return this;
        }

        public Shapes Tri(Color c, float x1, float y1, float x2, float y2, float x3, float y3) =>
            Poly(c, new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3));

        /// <summary>A rectangle by its top-left corner.</summary>
        public Shapes Rect(Color c, float x, float y, float w, float h) =>
            Poly(c, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));

        /// <summary>An ellipse by its center and full width/height.</summary>
        public Shapes Ellipse(Color c, float cx, float cy, float w, float h, int segments = 28)
        {
            var center = Add(cx, cy, c);
            for (int i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * Mathf.PI * 2;
                Add(cx + Mathf.Cos(a) * w / 2, cy + Mathf.Sin(a) * h / 2, c);
                if (i == 0) continue;
                tris.Add(center);
                tris.Add(center + i);
                tris.Add(center + i + 1);
            }
            return this;
        }

        public Shapes Circle(Color c, float cx, float cy, float r) => Ellipse(c, cx, cy, r * 2, r * 2);

        /// <summary>A rectangle with rounded corners (top-left x, y).</summary>
        public Shapes RoundRect(Color c, float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w / 2, h / 2);
            var pts = new List<Vector2>();
            void Corner(float cx, float cy, float start)
            {
                for (int i = 0; i <= 6; i++)
                {
                    var a = (start + i * 15) * Mathf.Deg2Rad;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Corner(x + w - r, y + r, 270);
            Corner(x + w - r, y + h - r, 0);
            Corner(x + r, y + h - r, 90);
            Corner(x + r, y + r, 180);
            return Poly(c, pts.ToArray());
        }

        /// <summary>A straight line `width` px thick.</summary>
        public Shapes Line(Color c, float width, float x1, float y1, float x2, float y2)
        {
            var d = new Vector2(x2 - x1, y2 - y1);
            if (d.sqrMagnitude < 0.0001f) return this;
            var n = new Vector2(-d.y, d.x).normalized * (width / 2);
            return Poly(c, new Vector2(x1 + n.x, y1 + n.y), new Vector2(x2 + n.x, y2 + n.y), new Vector2(x2 - n.x, y2 - n.y), new Vector2(x1 - n.x, y1 - n.y));
        }

        /// <summary>Lines through the points (closed: back to the first).</summary>
        public Shapes Stroke(Color c, float width, bool closed, params Vector2[] pts)
        {
            for (int i = 0; i < pts.Length - 1; i++) Line(c, width, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
            if (closed && pts.Length > 2) Line(c, width, pts[pts.Length - 1].x, pts[pts.Length - 1].y, pts[0].x, pts[0].y);
            // Round the joins a little.
            foreach (var p in pts) Circle(c, p.x, p.y, width / 2);
            return this;
        }

        public Shapes StrokeRect(Color c, float width, float x, float y, float w, float h) =>
            Stroke(c, width, true, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));

        /// <summary>An ellipse outline.</summary>
        public Shapes StrokeEllipse(Color c, float width, float cx, float cy, float w, float h, int segments = 28)
        {
            for (int i = 0; i < segments; i++)
            {
                var a0 = i / (float)segments * Mathf.PI * 2;
                var a1 = (i + 1) / (float)segments * Mathf.PI * 2;
                Line(c, width, cx + Mathf.Cos(a0) * w / 2, cy + Mathf.Sin(a0) * h / 2, cx + Mathf.Cos(a1) * w / 2, cy + Mathf.Sin(a1) * h / 2);
            }
            return this;
        }

        void LateUpdate()
        {
            if (dirty) Rebuild();
        }

        /// <summary>Push the shapes to the mesh now (otherwise it happens before the frame is drawn).</summary>
        public void Rebuild()
        {
            dirty = false;
            colors.Clear();
            foreach (var c in baseColors) colors.Add(new Color(c.r, c.g, c.b, c.a * alpha));
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
