using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg
{
    /// <summary>
    /// Paper damage, on every monster (docs/PLANS.md, roadmap job 4 and "paper damage for every monster"; the idea and its behaviour come from
    /// art/tests/paper_damage_mock.html). The figure is a sheet of paper and the damage happens to the paper: each blow
    /// leaves a crease across its picture (drawn by Fx/PaperSprite.shader on the art itself, so nothing is copied while
    /// it lives), it tears when badly hurt (at a third of its HP and again at 15%), fire burns holes in it, and when it dies it folds up along the creases the fight left, so no two deaths match, then drops to
    /// the ground as a folded packet (Fx/PaperFold.shader, on the GPU: the picture is baked onto a canvas with room
    /// around it, one fold at a time). One set of rules for any picture, nothing drawn per monster. Switch: `On`.
    /// "Sheet space": the picture with its height 1, y up. Canvas px: the fold canvas, y up.
    /// </summary>
    public partial class Paper : MonoBehaviour
    {
        /// <summary>Every monster is paper (the designer, 2026-11-25: "Yes, every monster"). The cheat menu turns it off for
        /// this session, for testing: monsters made while it's off bleed and slump as before.</summary>
        public static bool On = true;

        /// <summary>Milliseconds the slowest step on the CPU took (a fold's bake and read-back): the cost check.</summary>
        public static float SlowestMs;

        const int MaxCreases = 16, MaxTears = 2, MaxHoles = 4;
        static readonly int RectId = Shader.PropertyToID("_PaperRect"), InfoId = Shader.PropertyToID("_PaperInfo");
        static readonly int CreasesId = Shader.PropertyToID("_PaperCreases"), CanvasId = Shader.PropertyToID("_Canvas");
        static readonly int PadId = Shader.PropertyToID("_Pad"), LineId = Shader.PropertyToID("_FoldLine"), FoldId = Shader.PropertyToID("_Fold");
        static readonly int BackId = Shader.PropertyToID("_Back"), ShadowId = Shader.PropertyToID("_Shadow"), ColorId = Shader.PropertyToID("_Color");
        static readonly int TearsId = Shader.PropertyToID("_PaperTears"), HolesId = Shader.PropertyToID("_PaperHoles");

        static Material live, liveFill;
        static Shader foldShader;

        /// <summary>The one-colour version of a paper picture's material (hit flashes keep its holes); null for any other.</summary>
        public static Material FillFor(Material m)
        {
            if (m == null || m != live) return null;
            if (liveFill == null)
            {
                liveFill = new Material(live) { name = "Paper fill" };
                liveFill.SetFloat("_Fill", 1);
            }
            return liveFill;
        }

        SpriteRenderer body;
        Bend bend;
        /// <summary>The picture's rect in its texture (UV), its size in texels, width / height, px per unit, feet.</summary>
        Vector4 rect;
        Vector2 size, pivot;
        float aspect, ppu, heightPx;
        /// <summary>Creases in sheet space: a point on it (xy) and its unit normal (zw).</summary>
        readonly List<Vector4> creases = new List<Vector4>();
        readonly Vector4[] creaseArray = new Vector4[MaxCreases];
        /// <summary>Tears in sheet space: where each starts at the edge (xy) and its tip (zw).</summary>
        readonly Vector4[] tears = new Vector4[MaxTears];
        int tearCount;
        /// <summary>Burn holes: middle (xy), radius now (z), glow 0-1 (w); and each one's full radius and age in seconds.</summary>
        readonly Vector4[] holes = new Vector4[MaxHoles];
        readonly float[] holeSize = new float[MaxHoles], holeAge = new float[MaxHoles];
        int holeCount;
        MaterialPropertyBlock block;
        System.Random rng;
        bool folding;

        /// <summary>
        /// Make `fig`'s picture paper. `heightPx`: the biggest it is ever drawn on screen (the fold canvas is made that
        /// big). Null for a figure without art, or where the shaders can't run.
        /// </summary>
        public static Paper Attach(Figure fig, float heightPx)
        {
            var r = fig != null ? fig.Body : null;
            if (r == null || r.sprite == null || r.sprite.texture == null) return null;
            if (live == null)
            {
                var shader = Resources.Load<Shader>("Fx/PaperSprite");
                if (shader == null || !shader.isSupported) return null;
                live = new Material(shader) { name = "Paper" };
            }
            if (foldShader == null) foldShader = Resources.Load<Shader>("Fx/PaperFold");
            if (foldShader == null || !foldShader.isSupported) return null;
            var p = fig.gameObject.AddComponent<Paper>();
            p.Init(r, fig.Bend, heightPx);
            return p;
        }

        void Init(SpriteRenderer r, Bend b, float h)
        {
            body = r;
            bend = b;
            heightPx = h;
            var s = r.sprite;
            var tex = s.texture;
            var rr = s.rect;
            rect = new Vector4(rr.x / tex.width, rr.y / tex.height, rr.width / tex.width, rr.height / tex.height);
            size = rr.size;
            aspect = rr.width / rr.height;
            ppu = s.pixelsPerUnit;
            pivot = s.pivot;
            block = new MaterialPropertyBlock();
            rng = new System.Random(Environment.TickCount ^ GetInstanceID());
            r.sharedMaterial = live;
            Push();
            StartPrebuild();
        }

        float R() => (float)rng.NextDouble();

        /// <summary>The damage onto the body's property block (the bones keep their own values in the same block).</summary>
        void Push()
        {
            for (int i = 0; i < MaxCreases; i++) creaseArray[i] = i < creases.Count ? creases[i] : Vector4.zero;
            body.GetPropertyBlock(block);
            block.SetVector(RectId, rect);
            block.SetVector(InfoId, Info);
            block.SetVectorArray(CreasesId, creaseArray);
            block.SetVectorArray(TearsId, tears);
            block.SetVectorArray(HolesId, holes);
            body.SetPropertyBlock(block);
        }

        Vector4 Info => new Vector4(aspect, creases.Count, tearCount, holeCount);

        /// <summary>Burn holes eat outward for a moment, and their edges cool from a glow to scorch.</summary>
        void Update()
        {
            PrebuildTick();
            if (folding || holeCount == 0) return;
            var dt = Time.deltaTime * Anim.TimeScale * Anim.Speed;
            var changed = false;
            for (int i = 0; i < holeCount; i++)
            {
                if (holeAge[i] > 2) continue;
                holeAge[i] += dt;
                var a = holeAge[i];
                var grow = 1 - Mathf.Pow(1 - Mathf.Clamp01(a / 0.35f), 3);
                holes[i] = new Vector4(holes[i].x, holes[i].y, holeSize[i] * grow, Mathf.Clamp01(1 - (a - 0.3f) / 1.6f));
                changed = true;
            }
            if (changed) Push();
        }

        /// <summary>A point in the world on the picture, in sheet space (follows the figure's facing; ignores the bend).</summary>
        Vector2 ToSheet(Vector3 world)
        {
            var local = body.transform.InverseTransformPoint(world);
            return new Vector2((local.x * ppu + pivot.x) / size.y, (local.y * ppu + pivot.y) / size.y);
        }

        // --- Damage ---

        /// <summary>
        /// A blow landed at design (x, y): a crease across the sheet, through a spot anywhere on its middle pulled a
        /// little toward where it landed, at any angle (crumpled paper creases every way). `weight` 0-1. A fire blow
        /// also burns a hole where it landed (pulled onto the picture's middle).
        /// </summary>
        public void Hit(float x, float y, float weight, string element = null)
        {
            if (folding) return;
            var at = ToSheet(Design.World(x, y));
            if (element == "fire") Burn(at, weight);
            var spot = new Vector2(aspect * (0.2f + R() * 0.6f), 0.2f + R() * 0.6f);
            var p = Vector2.Lerp(spot, new Vector2(Mathf.Clamp(at.x, 0, aspect), Mathf.Clamp01(at.y)), 0.25f);
            var a = R() * Mathf.PI;
            creases.Add(new Vector4(p.x, p.y, -Mathf.Sin(a), Mathf.Cos(a)));
            if (creases.Count > MaxCreases) creases.RemoveAt(0);
            Push();
        }

        /// <summary>A hole burnt at sheet point `at` (the oldest goes when there are too many).</summary>
        void Burn(Vector2 at, float weight)
        {
            var c = new Vector2(Mathf.Lerp(Mathf.Clamp(at.x, 0, aspect), aspect / 2, 0.45f), Mathf.Lerp(Mathf.Clamp01(at.y), 0.5f, 0.45f));
            c += new Vector2(R() - 0.5f, R() - 0.5f) * 0.12f;
            if (holeCount == MaxHoles)
            {
                for (int i = 1; i < MaxHoles; i++)
                {
                    holes[i - 1] = holes[i];
                    holeSize[i - 1] = holeSize[i];
                    holeAge[i - 1] = holeAge[i];
                }
                holeCount--;
            }
            holes[holeCount] = new Vector4(c.x, c.y, 0, 1);
            holeSize[holeCount] = 0.055f + 0.04f * Mathf.Clamp01(weight) + 0.015f * R();
            holeAge[holeCount] = 0;
            holeCount++;
            Push();
        }

        /// <summary>
        /// The monster's HP is now `fraction` of its most: at a third it tears, and again at 15%. True when a new tear
        /// opened (for the rip sound and scraps). Tears run up and down, clear of the face (the top of the picture, a
        /// little toward its front, as for the fold): one down from the top behind the face, one up from the bottom
        /// under its front. Across the face, a jagged tear reads as a grinning mouth.
        /// </summary>
        public bool Wounded(float fraction)
        {
            if (folding || tearCount >= MaxTears) return false;
            var due = fraction <= 0.15f ? 2 : fraction <= 1 / 3f ? 1 : 0;
            if (due <= tearCount) return false;
            while (tearCount < due)
            {
                Vector2 start, tip;
                if (tearCount == 0)
                {
                    start = new Vector2(aspect * (0.14f + 0.16f * R()), 1);
                    tip = new Vector2(start.x + aspect * (0.08f + 0.1f * R()), 0.42f + 0.1f * R());
                }
                else
                {
                    start = new Vector2(aspect * (0.52f + 0.2f * R()), 0);
                    tip = new Vector2(start.x + aspect * (R() - 0.5f) * 0.16f, 0.36f + 0.08f * R());
                }
                tears[tearCount++] = new Vector4(start.x, start.y, tip.x, tip.y);
            }
            Push();
            return true;
        }

        // --- The death fold (the mock's startFold / nextLine / tickFold, on the GPU) ---

        /// <summary>The paper's back, a shade deeper with every fold (the mock's BACK).</summary>
        static readonly Color[] Back = { Design.Hex(0xefe7d2), Design.Hex(0xe4dac1), Design.Hex(0xd9ceb3), Design.Hex(0xcfc3a6) };

        /// <summary>How the battle stages light the paper's back (they are painted dim; full white glows).</summary>
        const float BackLight = 0.88f;

        Material mat;
        RenderTexture sheet, next;
        Texture2D scanTex;
        GameObject quad;
        Mesh mesh;
        int W, H, FW, FH, pad;
        /// <summary>Canvas px per unit of the body's space, and the canvas px at the quad's origin.</summary>
        float canvasPpu;
        Vector2 origin;
        Transform shadow;
        float shadowWidth;
        /// <summary>The picture's tint as the fold starts (a boss's new form), kept on the folding paper.</summary>
        Color tint = Color.white;
        /// <summary>The face (canvas px) and how far around it folds keep clear.</summary>
        Vector2 face;
        float faceRadius;

        struct Scan
        {
            public List<Vector2> Pts;
            public Vector2 Mid, Min, Max;
            /// <summary>The read-back grid: opaque cells, its size, and canvas px per cell.</summary>
            public bool[] Solid;
            public int SW, SH;
            public float FX, FY;
            /// <summary>The picture's average colour (its opaque cells).</summary>
            public Color Mean;
        }

        struct FoldLine
        {
            /// <summary>A point on it and the unit normal toward the side that folds (canvas px).</summary>
            public Vector2 P, N;
        }

        /// <summary>
        /// Fold the picture up: the fight's creases first (when one cuts off a good share), else lines across the
        /// middle of what is left, square to its long side. Folds take turns coming toward the viewer (the paper's back
        /// on top, a shadow under it) and going behind, so some of the picture stays in sight. Few creases: three folds
        /// into a neat packet; seven or more: five quick, rough ones. It sinks as what it stands on folds away (`Sink`),
        /// then drops the rest of the way to `groundY` and bounces; `landed` runs as it first hits (design x of its
        /// middle, its width in design px).
        /// </summary>
        public async Task Fold(float groundY, Action<float, float> landed)
        {
            if (Solid)
            {
                await FoldSolid(groundY, landed);
                return;
            }
            folding = true;
            var foldStart = Time.time;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            StartCanvas();
            var scan = ScanCanvas();
            Took(watch);
            var rough = creases.Count >= 7;
            var take = Mathf.Min(creases.Count, rough ? 8 : 4);
            var queue = new Queue<Vector4>(creases.GetRange(creases.Count - take, take));
            int folds = rough ? 5 : 3, done = 0;
            // Timed so the folds and the drop together take no longer than today's slump (about 0.95 s).
            float dur = rough ? 0.095f : 0.16f, settle = rough ? 0.018f : 0.025f, end = rough ? -0.85f : -1;
            var startWidth = scan.Max.x - scan.Min.x;
            FindFace(scan);
            while (done < folds && scan.Pts.Count >= 12)
            {
                var level = done;
                var valley = level % 2 == 0;
                if (!NextLine(scan, queue, valley, out var line)) break;
                Sfx.Play("crinkle");
                // What it stands on folds away under it, so it sinks as it folds (most of the lift; the drop does the rest),
                // rather than hanging in the air at chest height and falling only at the end.
                var from = quad.transform.position;
                var sink = Sink(scan, line, end, groundY) / Design.PixelsPerUnit;
                // A fold going behind is only seen until it stands edge-on: it spends most of its time there (QuadIn).
                await Anim.Float(0, 1, dur, t =>
                {
                    SetFold(line, Mathf.Lerp(1, end * 1.08f, t), valley, level, rough);
                    if (sink > 0) quad.transform.position = from + Vector3.down * (sink * Mathf.Clamp01(t));
                }, valley ? Ease.CubicInOut : Ease.QuadIn).Task;
                await Anim.Float(1.08f, 1, settle, k => SetFold(line, end * k, valley, level, rough), Ease.QuadOut).Task;
                watch.Restart();
                SetFold(line, end, valley, level, rough);
                Graphics.Blit(sheet, next, mat, 1);
                (sheet, next) = (next, sheet);
                mat.mainTexture = sheet;
                SetFlat();
                scan = ScanCanvas();
                Took(watch);
                // The shadow under it narrows with the paper.
                if (shadow != null && startWidth > 0)
                    shadow.localScale = new Vector3(shadowWidth * Mathf.Max(0.3f, (scan.Max.x - scan.Min.x) / startWidth), shadow.localScale.y, 1);
                done++;
                dur = Mathf.Max(rough ? 0.07f : 0.11f, dur * 0.82f);
            }
            await Drop(scan, groundY, landed);
            Debug.Log($"drakepg: paper fold, {done} folds ({creases.Count} creases), face at the {faceNote}, slowest CPU step {SlowestMs:0.0} ms, took {Time.time - foldStart:0.00} s");
        }

        RenderTexture NewCanvas()
        {
            var t = new RenderTexture(FW, FH, 0, RenderTextureFormat.ARGB32) { name = "paper canvas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.Create();
            return t;
        }

        /// <summary>The living picture with its creases baked onto a canvas, shown on a quad in the body's place.</summary>
        void StartCanvas()
        {
            var k = Mathf.Min(1, Mathf.Max(64, heightPx) / size.y);
            W = Mathf.Max(8, Mathf.RoundToInt(size.x * k));
            H = Mathf.Max(8, Mathf.RoundToInt(size.y * k));
            pad = Mathf.RoundToInt(Mathf.Max(W, H) * 0.25f);
            FW = W + 2 * pad;
            FH = H + 2 * pad;
            mat = new Material(foldShader) { name = "Paper fold" };
            mat.SetVector(CanvasId, new Vector4(FW, FH, 0, 0));
            mat.SetVector(PadId, new Vector4(pad, pad, W, H));
            mat.SetVector(RectId, rect);
            mat.SetVector(InfoId, Info);
            for (int i = 0; i < MaxCreases; i++) creaseArray[i] = i < creases.Count ? creases[i] : Vector4.zero;
            mat.SetVectorArray(CreasesId, creaseArray);
            mat.SetVectorArray(TearsId, tears);
            mat.SetVectorArray(HolesId, holes);
            sheet = NewCanvas();
            next = NewCanvas();
            Graphics.Blit(body.sprite.texture, sheet, mat, 2);
            mat.mainTexture = sheet;
            tint = new Color(body.color.r, body.color.g, body.color.b, 1);
            mat.SetColor(ColorId, tint);
            SetFlat();

            canvasPpu = ppu * k;
            origin = pivot * k + new Vector2(pad, pad);
            quad = new GameObject("paper fold");
            quad.transform.SetParent(body.transform, false);
            mesh = new Mesh { name = "paper fold" };
            Rebuild();
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingLayerID = body.sortingLayerID;
            mr.sortingOrder = body.sortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            body.enabled = false;
            if (bend != null) bend.enabled = false;
            if (body.transform.parent != null)
                foreach (Transform c in body.transform.parent)
                    if (c.name == "shadow")
                    {
                        shadow = c;
                        shadowWidth = c.localScale.x;
                    }
        }

        /// <summary>The quad covering the canvas, its origin at canvas px `origin`.</summary>
        void Rebuild()
        {
            float x0 = -origin.x / canvasPpu, y0 = -origin.y / canvasPpu, x1 = (FW - origin.x) / canvasPpu, y1 = (FH - origin.y) / canvasPpu;
            mesh.vertices = new[] { new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x0, y1), new Vector3(x1, y1) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
        }

        /// <summary>Show the canvas as it is (no fold under way).</summary>
        void SetFlat()
        {
            mat.SetVector(LineId, new Vector4(99999, 0, 1, 0));
            mat.SetVector(FoldId, new Vector4(1, 0, 0, 0));
        }

        /// <summary>One moment of a fold: squash `s` (1 flat, 0 edge-on, -1 over), shading, the back's tone and the shadow.</summary>
        void SetFold(FoldLine l, float s, bool valley, int level, bool rough)
        {
            var over = s < 0;
            var shade = 0.42f * (1 - Mathf.Min(1, Mathf.Abs(s))) + (rough && over ? 0.1f : 0);
            var u = H / 360f;
            mat.SetVector(LineId, new Vector4(l.P.x, l.P.y, l.N.x, l.N.y));
            mat.SetVector(FoldId, new Vector4(s, valley ? 1 : 0, shade, Mathf.Max(1.2f, 1.2f * u)));
            // The back as the scene lights it (the stages are dim), a shade deeper with every fold.
            var back = Back[level % Back.Length] * BackLight;
            back.a = level == 0 ? 0.12f : 0;
            mat.SetColor(BackId, back);
            var lift = over ? 1 - Mathf.Min(1, Mathf.Abs(s)) : 0;
            mat.SetVector(ShadowId, new Vector4((3 + 9 * lift) * u, -(4 + 10 * lift) * u, 0.32f * (1 - 0.5f * lift), 0.6f * H));
        }

        /// <summary>What is on the canvas, read back small (a quarter of its size): opaque points, their middle and box.</summary>
        Scan ScanCanvas()
        {
            int sw = Mathf.Max(4, FW / 4), sh = Mathf.Max(4, FH / 4);
            var small = RenderTexture.GetTemporary(sw, sh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sheet, small);
            var was = RenderTexture.active;
            RenderTexture.active = small;
            if (scanTex == null || scanTex.width != sw || scanTex.height != sh)
            {
                if (scanTex != null) Destroy(scanTex);
                scanTex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            }
            scanTex.ReadPixels(new Rect(0, 0, sw, sh), 0, 0, false);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(small);
            var px = scanTex.GetPixels32();
            float fx = FW / (float)sw, fy = FH / (float)sh;
            var s = new Scan
            {
                Pts = new List<Vector2>(), Min = new Vector2(float.MaxValue, float.MaxValue), Max = new Vector2(float.MinValue, float.MinValue),
                Solid = new bool[sw * sh], SW = sw, SH = sh, FX = fx, FY = fy,
            };
            var sum = Vector2.zero;
            var colSum = Vector4.zero;
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                if (px[y * sw + x].a <= 60) continue;
                Color cc = px[y * sw + x];
                colSum += new Vector4(cc.r, cc.g, cc.b, 1);
                s.Solid[y * sw + x] = true;
                var p = new Vector2((x + 0.5f) * fx, (y + 0.5f) * fy);
                s.Pts.Add(p);
                sum += p;
                s.Min = Vector2.Min(s.Min, p - new Vector2(fx / 2, fy / 2));
                s.Max = Vector2.Max(s.Max, p + new Vector2(fx / 2, fy / 2));
            }
            s.Mid = s.Pts.Count > 0 ? sum / s.Pts.Count : new Vector2(FW / 2f, FH / 2f);
            s.Mean = colSum.w > 0 ? new Color(colSum.x / colSum.w, colSum.y / colSum.w, colSum.z / colSum.w, 1) : Color.gray;
            return s;
        }

        /// <summary>
        /// Where the face is, which no fold may cover, so the packet stays the monster: the middle of the top part of
        /// the picture, a little toward its front (pictures face right); for a wide one, its front above the legs.
        /// Generic, nothing marked per monster.
        /// </summary>
        void FindFace(Scan scan)
        {
            var w = scan.Max.x - scan.Min.x;
            var h = scan.Max.y - scan.Min.y;
            // Upright (a blob, a bird, a man): the top part. Wide (a boar, a wolf): the front, above the legs.
            var wide = w > 1.3f * h;
            var top = scan.Min.y + (wide ? 0.35f : 0.55f) * h;
            var front = wide ? scan.Max.x - 0.3f * w : float.MinValue;
            var sum = Vector2.zero;
            var n = 0;
            foreach (var q in scan.Pts)
                if (q.y >= top && q.x >= front)
                {
                    sum += q;
                    n++;
                }
            face = (n > 0 ? sum / n : scan.Mid) + new Vector2(wide ? 0 : 0.08f * W, 0);
            faceRadius = (wide ? 0.25f : 0.2f) * H;
            faceNote = $"{(wide ? "front" : "top")} (paper {w / Mathf.Max(1, h):0.00} wide to its height)";
        }

        /// <summary>Which face rule the fold used, for the log.</summary>
        string faceNote;

        /// <summary>
        /// The next fold. A crease of the fight's first (newest last), folding the side away from the face, when it
        /// cuts off 12-50% and keeps the face in sight; else a line square to some direction away from the face,
        /// cutting off as much as it can (up to about 40%) while the face stays in sight.
        /// </summary>
        bool NextLine(Scan scan, Queue<Vector4> queue, bool valley, out FoldLine line)
        {
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                var p = new Vector2(c.x * H + pad, c.y * H + pad);
                var n = new Vector2(c.z, c.w);
                if (Vector2.Dot(face - p, n) > 0) n = -n;
                line = new FoldLine { P = p, N = n };
                var share = Share(scan, line);
                if (share >= 0.12f && share <= 0.5f && Clear(scan, line, valley)) return true;
            }
            var targets = valley ? new[] { 0.35f, 0.28f, 0.2f, 0.14f, 0.1f } : new[] { 0.42f, 0.34f, 0.26f, 0.18f, 0.12f };
            var along = new float[scan.Pts.Count];
            foreach (var target in targets)
            {
                var start = R() * Mathf.PI * 2;
                for (int i = 0; i < 12; i++)
                {
                    var a = start + i * Mathf.PI / 6;
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    for (int k = 0; k < along.Length; k++) along[k] = Vector2.Dot(scan.Pts[k] - face, n);
                    Array.Sort(along);
                    var cut = along[Mathf.Clamp(Mathf.RoundToInt(along.Length * (1 - target)), 0, along.Length - 1)];
                    line = new FoldLine { P = face + n * cut, N = n };
                    if (Clear(scan, line, valley)) return true;
                }
            }
            line = default;
            return false;
        }

        /// <summary>How much of the paper is on the side of `l` that folds.</summary>
        static float Share(Scan scan, FoldLine l)
        {
            int a = 0;
            foreach (var q in scan.Pts)
                if (Vector2.Dot(q - l.P, l.N) > 0) a++;
            return scan.Pts.Count > 0 ? a / (float)scan.Pts.Count : 0;
        }

        /// <summary>
        /// The face stays in sight: on the side that stays (by its radius; half that for a fold going behind, which
        /// only trims the outline), and, for a fold toward the viewer, not under the flap.
        /// </summary>
        bool Clear(Scan scan, FoldLine l, bool valley)
        {
            if (Vector2.Dot(face - l.P, l.N) > -(valley ? faceRadius : 0.5f * faceRadius)) return false;
            if (!valley) return true;
            var r2 = 0.85f * faceRadius * 0.85f * faceRadius;
            foreach (var q in scan.Pts)
            {
                var d = Vector2.Dot(q - l.P, l.N);
                if (d <= 0) continue;
                if ((q - 2 * d * l.N - face).sqrMagnitude < r2) return false;
            }
            return true;
        }

        static void Took(System.Diagnostics.Stopwatch w)
        {
            var ms = (float)w.Elapsed.TotalMilliseconds;
            if (ms > SlowestMs) SlowestMs = ms;
        }

        /// <summary>A canvas px in design px (through the quad, so after it moved and turned too).</summary>
        Vector2 ToDesign(Vector2 px) => Design.FromWorld(quad.transform.TransformPoint(new Vector3((px.x - origin.x) / canvasPpu, (px.y - origin.y) / canvasPpu, 0)));

        /// <summary>Of a fold's lift (how far it raises the paper's lowest point), the share the paper sinks while folding.</summary>
        const float SinkShare = 0.75f;

        /// <summary>
        /// How far (design px) the paper sinks during the fold along `l`: `SinkShare` of how much the fold raises its lowest
        /// point, never past the ground. A fold across a wide monster raises nothing, and a flyer keeps its height.
        /// </summary>
        float Sink(Scan scan, FoldLine l, float end, float groundY)
        {
            if (scan.Pts.Count == 0) return 0;
            float low = float.MaxValue, lowAfter = float.MaxValue;
            foreach (var q in scan.Pts)
            {
                low = Mathf.Min(low, q.y);
                // The folding side ends up mirrored over the line (scaled by `end`).
                var d = Vector2.Dot(q - l.P, l.N);
                lowAfter = Mathf.Min(lowAfter, d > 0 ? (q - (1 - end) * d * l.N).y : q.y);
            }
            if (lowAfter <= low) return 0;
            var x = scan.Mid.x;
            var bottom = ToDesign(new Vector2(x, low)).y;
            var lift = bottom - ToDesign(new Vector2(x, lowAfter)).y;
            // Never below the ground: the gap under it once folded.
            var gap = groundY - (bottom - lift);
            return Mathf.Clamp(SinkShare * lift, 0, Mathf.Max(0, gap));
        }

        /// <summary>The folded packet falls to `groundY`, turning a little, bounces once and lies still.</summary>
        async Task Drop(Scan scan, float groundY, Action<float, float> landed)
        {
            if (scan.Pts.Count == 0) return;
            var bottom = ToDesign(new Vector2(scan.Mid.x, scan.Min.y)).y;
            var center = ToDesign(scan.Mid);
            var width = Mathf.Abs(ToDesign(new Vector2(scan.Max.x, scan.Mid.y)).x - ToDesign(new Vector2(scan.Min.x, scan.Mid.y)).x);
            var fall = Mathf.Max(0, groundY - bottom);
            // Turn about the packet's middle: the quad's origin moves there without a jump.
            var t = quad.transform;
            t.localPosition += new Vector3((scan.Mid.x - origin.x) / canvasPpu, (scan.Mid.y - origin.y) / canvasPpu, 0);
            origin = scan.Mid;
            Rebuild();
            var start = t.position;
            // The path worked out ahead and played on Anim time, so the x2 battle speed and recordings keep it.
            const float gravity = 3000, step = 1 / 240f;
            var ys = new List<float>();
            float y = 0, v = 0, hitAt = -1;
            int bounces = 0;
            while (ys.Count < 2400)
            {
                v += gravity * step;
                y += v * step;
                if (y >= fall)
                {
                    y = fall;
                    if (hitAt < 0) hitAt = ys.Count * step;
                    if (v > 170 && bounces < 1)
                    {
                        v = -v * 0.22f;
                        bounces++;
                    }
                    else
                    {
                        ys.Add(y);
                        break;
                    }
                }
                ys.Add(y);
            }
            var total = ys.Count * step;
            var spin = (R() - 0.5f) * 150;
            if (hitAt < 0) hitAt = total;
            Anim.After(Mathf.Max(0.001f, hitAt), () => landed?.Invoke(center.x, width));
            await Anim.Float(0, total, Mathf.Max(0.001f, total), time =>
            {
                var i = Mathf.Clamp((int)(time / step), 0, ys.Count - 1);
                t.position = start + Vector3.down * (ys[i] / Design.PixelsPerUnit);
                // It turns as it falls, and much less once it has hit.
                var turn = spin * Mathf.Min(time, hitAt) + 0.4f * spin * Mathf.Max(0, time - hitAt);
                t.localRotation = Quaternion.Euler(0, 0, turn);
            }, Ease.Linear).Task;
        }

        /// <summary>The packet's middle and size in design px (after Fold), for the smoke and the gold.</summary>
        public (Vector2 mid, float width, float height) Packet()
        {
            if (quad == null) return (Design.FromWorld(body.transform.position), 60, 40);
            if (solidOn && pos != null) return PacketSolid();
            var scan = ScanCanvas();
            if (scan.Pts.Count == 0) return (ToDesign(origin), 60, 40);
            var a = ToDesign(scan.Min);
            var b = ToDesign(scan.Max);
            return ((a + b) / 2, Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        /// <summary>Fade the folded paper (0 gone).</summary>
        public void SetAlpha(float a)
        {
            if (showMat != null) showMat.SetColor(ColorId, new Color(tint.r, tint.g, tint.b, a));
            else if (mat != null) mat.SetColor(ColorId, new Color(tint.r, tint.g, tint.b, a));
            if (shadow != null)
            {
                var r = shadow.GetComponent<SpriteRenderer>();
                // The crumple's own flat shadow fades with it (the figure's soft one is gone already).
                if (r != null) r.color = new Color(0, 0, 0, flatShadowSr != null ? 0 : 0.45f * a);
                if (flatShadowSr != null) flatShadowSr.color = new Color(0, 0, 0, flatShadowA * a);
            }
        }

        void OnDestroy()
        {
            var freeing = System.Diagnostics.Stopwatch.StartNew();
            FreeAll();
            if (Solid && freeing.Elapsed.TotalMilliseconds > 2)
                Debug.Log($"drakepg: paper freed in {freeing.Elapsed.TotalMilliseconds:0.0} ms");
        }

        void FreeAll()
        {
            // Also what the crumple made as the fight started, for a paper that never folded.
            if (solidOn || solidMat != null)
            {
                DestroySolid();
                sheet = null;
            }
            else DestroyCrumple();
            if (sheet != null) sheet.Release();
            if (next != null) next.Release();
            if (sheet != null) Destroy(sheet);
            if (next != null) Destroy(next);
            if (mat != null) Destroy(mat);
            if (mesh != null) Destroy(mesh);
            if (scanTex != null) Destroy(scanTex);
        }
    }
}
