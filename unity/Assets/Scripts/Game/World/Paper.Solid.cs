// The paper death fold on a solver (docs/PLANS.md, "Step 1b, the fold redone").
//
// The hinge force (FoldBy / Step: the dihedral angle, its unwrap, the force on the two wing tips and its reaction on the
// crease's ends) follows the model of Origami Simulator (A. Ghassaei, E. Demaine, N. Gershenfeld, "Fast, Interactive
// Origami Simulation using GPU Computation", 7OSME), ported from its shaders:
//
// MIT License. Copyright (c) 2018 Amanda Ghassaei.
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
// persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice
// and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg
{
    public partial class Paper
    {
        /// <summary>The fold redone on a solver (off: today's fold, which stays in the game until this beats it).</summary>
        public static bool Solid;

        /// <summary>Nonzero: the next death folds with this seed (the paper check, for like-for-like stills); used once.</summary>
        public static int NextSeed;

        /// <summary>Testing: the solver stops at this many seconds and holds there (stills). Below 0: off.</summary>
        public static float HoldAt = -1;

        /// <summary>Testing: no solver; the first crease's side turned by this many degrees (the renderer alone).</summary>
        public static float HandAngle = float.NaN;

        /// <summary>Testing (step 1c, gate A): no solver; the paper is crumpled into a fixed lump (the look alone) by folding
        /// the whole sheet across planes, again and again: `CrumpleBig` folds through its middle across its longest spread in 3D (each halves it, so it balls up), then
        /// `CrumpleRim` smaller ones near its edge (corners turned in). Each fold tilts `CrumpleTilt` degrees (min, max)
        /// out of the sheet, so a flap lies 180 minus twice that from the paper it folds onto. The lump is turned to show
        /// about `CrumpleBack` of paper back.</summary>
        public static bool Crumple;
        public static int CrumpleBig = 4, CrumpleRim = 6;
        public static Vector2 CrumpleTilt = new Vector2(12, 32);
        public static float CrumpleBack = 0.25f;
        /// <summary>How far out a corner fold's plane sits, as a share of the lump's reach that way (min, max).</summary>
        public static Vector2 CrumpleRimAt = new Vector2(0.45f, 0.7f);
        /// <summary>The lump drawn this much bigger than the folds leave it (a crumpled sheet is a third of the picture,
        /// too small for its facets to read at game size), and a dark line this many canvas px round its outline.</summary>
        public static float CrumpleScale = 1.5f, CrumpleOutline = 2.5f;

        /// <summary>Testing: which passes draw (1 the paper, 2 its shadows), and a file the paper's texture is saved to once
        /// the solver reaches `DumpAt` seconds (then cleared).</summary>
        public static int Passes = 3;
        /// <summary>Testing: log where the paper is every tenth of a second.</summary>
        public static bool Trace;
        float traceAt, groundPush;
        /// <summary>Testing: no ground.</summary>
        public static bool NoGround;
        /// <summary>Testing: at most this many creases (-1: as many as the fight gives).</summary>
        public static int CreaseLimit = -1;
        /// <summary>Creases never cross on the paper (two crossing lines can't both fold part way with flat paper
        /// between: the solver would crumple it).</summary>
        public static bool NoCross = true;
        int[] pickSat;
        public static string DumpRt;
        public static float DumpAt;

        /// <summary>The last solid fold, for the paper check's log: seed, mesh size, creases, time, CPU cost.</summary>
        public static string LastSolid = "";

        // --- Tuning (found by eye) ---

        /// <summary>Grid cells across the paper's longer side.</summary>
        public static int GridCells = 15;
        /// <summary>Fold stiffness (per grid cell of crease, by the paper's size squared, so any size folds alike).</summary>
        public static float FoldK = 20f;
        /// <summary>Between creases: how stiffly the paper stays flat (a share of the fold stiffness).</summary>
        public static float FacetShare = 1f;
        /// <summary>Seconds over which a crease's rest angle goes from flat to folded (Origami Simulator's fold percent);
        /// the paper follows it on its springs, late and past it.</summary>
        public static float FoldTime = 0.3f;
        /// <summary>Velocity kept per second (the rest is lost to the air).</summary>
        public static float AirDamp = 0.85f;
        /// <summary>Of the paper's motion against itself (folding, not falling), the share kept per second: the folds settle.</summary>
        public static float InnerDamp = 0.05f;
        /// <summary>Damping on each hinge's turning, a share of critical for the points next to it.</summary>
        public static float HingeDamp = 0.3f;
        /// <summary>Gravity, design px per second squared (the old drop's).</summary>
        public static float Gravity = 3000;
        /// <summary>How much of a landing's speed comes back up, and how much sideways speed the ground takes.</summary>
        public static float Bounce = 0.32f, Friction = 0.45f;
        /// <summary>The killing blow pops the paper up this high (a share of its size), spinning up to this fast
        /// (radians a second) and pushed away from the hero this fast (sizes a second); it folds in the air and lands.</summary>
        public static float Hop = 0.14f, HopSpin = 1.2f, HopPush = 0.1f;
        /// <summary>The perspective on the paper: the eye is this many paper heights away.</summary>
        public static float Eye = 2.6f;
        /// <summary>The battle is seen from a little above: what stands out toward the viewer is drawn this much lower
        /// per px (what goes behind, higher), so a flap folded back shows its top over the paper's edge.</summary>
        public static float Tilt = 0.3f;
        /// <summary>The paper's back: plain paper, this much toward the picture's own colour.</summary>
        public static float BackTint = 0.35f;
        const float SubStep = 1 / 600f;
        const int MaxSubSteps = 60;

        // --- The mesh (canvas px, y up, z toward the viewer) ---

        Vector2[] flat;
        Vector3[] pos, prev, force, normal;
        int[] tri;
        int vCount, tCount;
        struct Bar { public int A, B; public float L; }
        Bar[] bars;
        struct Hinge
        {
            /// <summary>The edge (A to B as it runs in T1), the wing tips (C in T1, D in T2), the triangles.</summary>
            public int A, B, C, D, T1, T2;
            /// <summary>Which crease line (-1: paper between creases), target angle (radians), stiffness, damping, angle now.</summary>
            public int Line;
            public float Target, K, Damp, Theta, Last;
            /// <summary>The narrower wing's height, floored (forces and damping use at least this: thin triangles stay stable).</summary>
            public float HF;
        }
        float hFloor;
        Hinge[] hinges;
        /// <summary>Per triangle, the hinge on each edge (opposite corner 0, 1, 2), -1 on the paper's rim.</summary>
        int[] triHinge;
        /// <summary>Per triangle, the flat panel it is in (the paper between creases); its tone is smooth across the
        /// panel and steps only at a crease.</summary>
        int[] triPanel;
        int panelCount;
        Vector3[] nAcc, rNormal;
        /// <summary>Points with the picture under them (or next to it): only these touch the ground, so the clear margin
        /// round the picture doesn't hold it up.</summary>
        bool[] inked;
        /// <summary>The face panel's vertices.</summary>
        int[] facePanel;
        /// <summary>When each crease line starts folding (seconds), and its target.</summary>
        float[] lineStart;

        struct Line
        {
            public Vector2 P, N;
            /// <summary>The fight's crease (true) or one made up.</summary>
            public bool Fight;
            /// <summary>A crease on another's flap, parallel to it (an accordion), or -1.</summary>
            public int Parent;
            /// <summary>+1: the flap folds toward the viewer (its back shows); -1: behind.</summary>
            public float Sign;
            /// <summary>How far a flap toward the viewer may fold (degrees) before it lies over the face.</summary>
            public float MaxDeg;
        }
        readonly List<Line> lines = new List<Line>();

        RenderTexture solidCanvas, solidRt;
        Material solidMat, showMat;
        Mesh paperMesh, shadowMesh;
        CommandBuffer cmd;
        Vector3[] rv, sv;
        Vector2[] ruv;
        Vector3[] rWorld, rBary, rLines;
        float simTime, groundC, sheetSize, zHold;
        Vector3 gravityC, lightC;
        /// <summary>Up from the ground in canvas px (the figure can lean, so not always the canvas's own up).</summary>
        Vector2 upC;
        bool landedOnce, solidOn;
        float startWidthPx;
        SpriteRenderer shadowSr;
        float shadowAlpha, shadowHeight;
        Renderer quadRenderer;
        bool shadowLogged;
        int seed;

        static readonly int ColorIdS = Shader.PropertyToID("_Color"), LightId = Shader.PropertyToID("_Light"), ShadeId = Shader.PropertyToID("_Shade"),
            OutlineId = Shader.PropertyToID("_Outline");

        /// <summary>
        /// The paper folds on the solver: every crease at once toward its target (seeded mountain or valley, so no two
        /// deaths match), the paper between them bending a little, under gravity, onto the ground; the face panel is
        /// held facing the viewer. Ends once it lies still. `landed` as in <see cref="Fold"/>.
        /// </summary>
        async Task FoldSolid(float groundY, Action<float, float> landed)
        {
            folding = true;
            solidOn = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            seed = NextSeed != 0 ? NextSeed : rng.Next(1, int.MaxValue);
            NextSeed = 0;
            var srng = new System.Random(seed);
            StartSolid();
            var scan = ScanSolid();
            FindFace(scan);
            // The solver's flaps reach much further than the old fold's: the face is guarded as a wider disc, from
            // about two-fifths of an upright picture's height up (eyes often sit in its middle, not its top).
            {
                var hh = scan.Max.y - scan.Min.y;
                if (scan.Max.x - scan.Min.x <= 1.3f * hh) face.y = Mathf.Min(face.y, scan.Min.y + 0.67f * hh);
                faceRadius = Mathf.Max(faceRadius, 0.28f * hh);
            }
            if (Crumple) lines.Clear();
            else PickLines(scan, srng);
            BuildMesh(scan, srng);
            SetupSolver(scan, srng, groundY);
            var buildMs = (float)watch.Elapsed.TotalMilliseconds;
            Took(watch);
            Draw();
            float worstMs = 0;
            int frames = 0;
            var handle = Anim.Float(0, 30, 30, time =>
            {
                var w = System.Diagnostics.Stopwatch.StartNew();
                Advance(time, landed);
                Draw();
                var ms = (float)w.Elapsed.TotalMilliseconds;
                worstMs = Mathf.Max(worstMs, ms);
                frames++;
            }, Ease.Linear);
            var settled = false;
            handle.OnComplete(() => settled = true);
            while (!settled && !handle.Done)
            {
                if (Settled()) handle.Stop();
                await Task.Yield();
                if (this == null) return;
            }
            if (!landedOnce)
            {
                landedOnce = true;
                var (mid, width, _) = Packet();
                landed?.Invoke(mid.x, width);
            }
            if (worstMs > SlowestMs) SlowestMs = worstMs;
            LastSolid = $"seed {seed}, {vCount} points, {tCount} triangles, {lines.Count} creases ({CountFight()} from the fight), "
                + $"{simTime:0.00} s to lie still, build {buildMs:0.0} ms, worst frame {worstMs:0.00} ms over {frames} frames";
            Debug.Log($"drakepg: paper fold (solver), {LastSolid}, face at the {faceNote}");
        }

        int CountFight()
        {
            int n = 0;
            foreach (var l in lines) if (l.Fight) n++;
            return n;
        }

        /// <summary>The living picture with its creases baked onto a canvas (with mips, for soft shadows), and the quad that shows the paper.</summary>
        void StartSolid()
        {
            var k = Mathf.Min(1, Mathf.Max(64, heightPx) / size.y);
            W = Mathf.Max(8, Mathf.RoundToInt(size.x * k));
            H = Mathf.Max(8, Mathf.RoundToInt(size.y * k));
            pad = Mathf.RoundToInt(Mathf.Max(W, H) * 0.3f);
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
            solidCanvas = new RenderTexture(FW, FH, 0, RenderTextureFormat.ARGB32)
            {
                name = "paper canvas", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = true, autoGenerateMips = true,
            };
            solidCanvas.Create();
            Graphics.Blit(body.sprite.texture, solidCanvas, mat, 2);
            // `sheet` is what the scan reads.
            sheet = solidCanvas;
            tint = new Color(body.color.r, body.color.g, body.color.b, 1);

            var shader = Resources.Load<Shader>("Fx/PaperSolid");
            solidMat = new Material(shader) { name = "Paper solid" };
            solidMat.mainTexture = solidCanvas;
            showMat = new Material(shader) { name = "Paper fold" };
            solidRt = new RenderTexture(new RenderTextureDescriptor(FW, FH, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4 })
            {
                name = "paper canvas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            solidRt.Create();
            showMat.mainTexture = solidRt;
            showMat.SetColor(ColorIdS, tint);

            canvasPpu = ppu * k;
            origin = pivot * k + new Vector2(pad, pad);
            quad = new GameObject("paper fold");
            quad.transform.SetParent(body.transform, false);
            mesh = new Mesh { name = "paper fold" };
            float hx = FW / 2f / canvasPpu, hy = FH / 2f / canvasPpu;
            mesh.vertices = new[] { new Vector3(-hx, -hy), new Vector3(hx, -hy), new Vector3(-hx, hy), new Vector3(hx, hy) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = showMat;
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
                        shadowHeight = c.localScale.y;
                        shadowSr = c.GetComponent<SpriteRenderer>();
                        if (shadowSr != null) shadowAlpha = shadowSr.color.a;
                    }
            paperMesh = new Mesh { name = "paper fold" };
            paperMesh.MarkDynamic();
            shadowMesh = new Mesh { name = "paper fold" };
            shadowMesh.MarkDynamic();
            cmd = new CommandBuffer { name = "paper fold" };
        }

        /// <summary>The canvas read back (a quarter of its size), with its opaque cells kept for the mesh.</summary>
        Scan ScanSolid()
        {
            var s = ScanCanvas();
            sheetSize = Mathf.Max(s.Max.x - s.Min.x, s.Max.y - s.Min.y, 8);
            return s;
        }

        // --- Creases ---

        /// <summary>
        /// The lines it folds along, on the flat paper: the fight's newest creases first, then made-up ones (a one-hit
        /// kill has none), 3 (5 when the fight left 7 or more). Each must cut the paper with a fair share on both sides,
        /// keep off the face, and not lie close along another.
        /// </summary>
        void PickLines(Scan scan, System.Random srng)
        {
            lines.Clear();
            pickSat = Integral(scan);
            var want = creases.Count >= 7 ? 5 : creases.Count >= 4 ? 4 : 3;
            if (CreaseLimit >= 0) want = Mathf.Min(want, CreaseLimit);
            // A flap that can fold toward the viewer (its back shows on every death), made on purpose and first (the
            // fight's creases could fill its place): on a round picture with the face in the middle, a line that can is
            // a thin band (far enough off the face, yet cutting off a fair share), which random lines rarely hit. In a
            // random direction, slid out from the face until one fits: the biggest flap that can still stand well past
            // upright.
            if (want > 0)
            {
                var a0 = (float)srng.NextDouble() * 2 * Mathf.PI;
                for (int k = 0; k < 16 && !HasFrontRoom(scan); k++)
                {
                    var a = a0 + k * (2 * Mathf.PI / 16) * 7;  // stepping round the circle in a scattered order
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float far = 0;
                    foreach (var q in scan.Pts) far = Mathf.Max(far, Vector2.Dot(q - face, n));
                    for (float d = 0.75f * faceRadius; d < far; d += 0.25f * sheetSize / GridCells)
                        if (TryLine(scan, face + n * d, n, false, -1, FrontMin)) break;
                }
            }
            // Then the fight's own creases, newest first.
            for (int i = creases.Count - 1; i >= 0 && lines.Count < want; i--)
            {
                var c = creases[i];
                TryLine(scan, new Vector2(c.x * H + pad, c.y * H + pad), new Vector2(c.z, c.w), true);
            }
            for (int tries = 0; tries < 60 && lines.Count < want; tries++)
            {
                // Sometimes a second crease on a flap, parallel to the first: the flap folds like a fan.
                if (lines.Count > 0 && srng.NextDouble() < 0.4)
                {
                    var pi = srng.Next(lines.Count);
                    var l = lines[pi];
                    var off = (0.12f + 0.1f * (float)srng.NextDouble()) * sheetSize;
                    TryLine(scan, l.P + l.N * off, l.N, false, pi);
                    continue;
                }
                var a = (float)srng.NextDouble() * Mathf.PI;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // Through a point away from the face, somewhere on the paper. The last one must be able to fold toward
                // the viewer if none can yet (the paper's back shows on every death).
                var q = scan.Pts[srng.Next(scan.Pts.Count)];
                TryLine(scan, q, n, false, -1, lines.Count == want - 1 && !HasFrontRoom(scan) ? FrontMin : 0);
            }
            // None can fold forward yet (the fight's creases filled every place, or none fitted): one more that can, if any.
            for (int tries = 0; tries < 60 && !HasFrontRoom(scan); tries++)
            {
                var a = (float)srng.NextDouble() * Mathf.PI;
                var q = scan.Pts[srng.Next(scan.Pts.Count)];
                TryLine(scan, q, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), false, -1, FrontMin);
            }
            // Which way each flap goes: toward the viewer (its back shows) where it won't land on the face, else behind;
            // a fan's creases alternate.
            bool anyFront = false;
            int canFront = -1;
            // (Only a crease on the paper itself counts here: a fan's second crease folding forward mostly hides again.)
            for (int k = 0; k < lines.Count; k++)
            {
                var l = lines[k];
                if (l.Parent >= 0)
                    l.Sign = -lines[l.Parent].Sign;
                else
                {
                    // Folded by an angle a toward the viewer, the flap reaches back over the picture its length times
                    // -cos(a): it may fold until that reaches the face's edge (a flap that can't stand at least
                    // halfway past upright folds behind instead).
                    l.MaxDeg = FrontRoom(scan, l.P, l.N);
                    var clear = l.MaxDeg >= FrontMin;
                    if (clear && (canFront < 0 || l.MaxDeg > lines[canFront].MaxDeg)) canFront = k;
                    l.Sign = clear && srng.NextDouble() < 0.6 ? 1 : -1;
                }
                anyFront |= l.Sign > 0 && l.Parent < 0;
                lines[k] = l;
            }
            if (!anyFront && canFront >= 0)
            {
                var l = lines[canFront];
                l.Sign = 1;
                lines[canFront] = l;
                for (int k = canFront + 1; k < lines.Count; k++)
                    if (lines[k].Parent >= 0)
                    {
                        var c = lines[k];
                        c.Sign = -lines[c.Parent].Sign;
                        lines[k] = c;
                    }
            }
        }

        /// <summary>The least a flap must be able to fold toward the viewer (degrees) to fold that way: past upright, so
        /// its back shows over the picture.</summary>
        const float FrontMin = 115;

        /// <summary>How far (degrees) the flap past a line may fold toward the viewer before it lies over the face.</summary>
        float FrontRoom(Scan scan, Vector2 p, Vector2 n)
        {
            var dFace = -Vector2.Dot(face - p, n);
            float reach = 0;
            foreach (var q in scan.Pts) reach = Mathf.Max(reach, Vector2.Dot(q - p, n));
            var room = Mathf.Clamp01((dFace - 0.65f * faceRadius) / Mathf.Max(1, reach));
            // Less the 12 degrees a flap may swing past its aim and rest at.
            return 168 - Mathf.Acos(Mathf.Min(0.97f, room)) * Mathf.Rad2Deg;
        }

        bool HasFrontRoom(Scan scan)
        {
            foreach (var l in lines)
                if (l.Parent < 0 && FrontRoom(scan, l.P, l.N) >= FrontMin) return true;
            return false;
        }

        bool TryLine(Scan scan, Vector2 p, Vector2 n, bool fight, int parent = -1, float needDeg = 0)
        {
            n = n.normalized;
            if (Vector2.Dot(face - p, n) > 0) n = -n;
            if (needDeg > 0 && FrontRoom(scan, p, n) < needDeg) return false;
            // Off the face.
            if (Mathf.Abs(Vector2.Dot(face - p, n)) < 0.75f * faceRadius) return false;
            int side = 0;
            foreach (var q in scan.Pts)
                if (Vector2.Dot(q - p, n) > 0) side++;
            var share = side / (float)Mathf.Max(1, scan.Pts.Count);
            if (share < (parent >= 0 ? 0.05f : 0.12f) || share > 0.55f) return false;
            foreach (var l in lines)
            {
                var cos = Mathf.Abs(Vector2.Dot(l.N, n));
                if (cos > 0.94f && Mathf.Abs(Vector2.Dot(p - l.P, l.N)) < 0.12f * sheetSize) return false;
                // Crossing very near the face: no.
                var det = l.N.x * n.y - l.N.y * n.x;
                if (Mathf.Abs(det) > 1e-3f)
                {
                    var c1 = Vector2.Dot(l.N, l.P);
                    var c2 = Vector2.Dot(n, p);
                    var x = new Vector2((c1 * n.y - c2 * l.N.y) / det, (l.N.x * c2 - n.x * c1) / det);
                    if ((x - face).magnitude < 1.1f * faceRadius) return false;
                    // On the paper (or within two cells of it): no.
                    var room = Vector2.one * (2f * sheetSize / GridCells);
                    if (NoCross && Count(pickSat, scan, x - room, x + room) > 0) return false;
                }
            }
            // Inside another's flap (all of its own flap is on that one's far side): a pleat of that one, so the two
            // fold opposite ways (two nested flaps folding the same way just carry each other through the body). One
            // that would swallow a line already there is left out.
            if (parent < 0)
                for (int k = 0; k < lines.Count; k++)
                {
                    if (Inside(scan, p, n, lines[k].P, lines[k].N)) { parent = k; break; }
                    if (Inside(scan, lines[k].P, lines[k].N, p, n)) return false;
                }
            lines.Add(new Line { P = p, N = n, Fight = fight, Parent = parent, Sign = 1 });
            return true;
        }

        /// <summary>Whether the flap past line (p, n) lies within the flap past line (po, no) (a few points either way
        /// forgiven).</summary>
        static bool Inside(Scan scan, Vector2 p, Vector2 n, Vector2 po, Vector2 no)
        {
            int flap = 0, outside = 0;
            foreach (var q in scan.Pts)
                if (Vector2.Dot(q - p, n) > 0)
                {
                    flap++;
                    if (Vector2.Dot(q - po, no) <= 0) outside++;
                }
            return flap > 0 && outside <= 0.05f * flap;
        }

        // --- The mesh ---

        List<Vector2> bFlat;
        List<int> bTri;
        List<int> bMask;
        /// <summary>The crumple (testing): each point's place in the lump, beside `bFlat` while building, then fixed.</summary>
        List<Vector3> bPos;
        Vector3[] crumplePos;
        Scan bScan;
        int[] bSat;

        /// <summary>
        /// A triangle grid over the paper (the picture's own alpha is the outline), kept where the picture is, then cut
        /// along every crease line (grid points near a line snapped onto it first, so no slivers).
        /// </summary>
        void BuildMesh(Scan scan, System.Random srng)
        {
            var s = sheetSize / GridCells;
            var rowH = s * 0.8660254f;
            var x0 = scan.Min.x - s;
            var y0 = scan.Min.y - s;
            var cols = Mathf.CeilToInt((scan.Max.x - x0) / s) + 2;
            var rows = Mathf.CeilToInt((scan.Max.y - y0) / rowH) + 2;
            var sat = Integral(scan);
            bScan = scan;
            bSat = sat;
            bFlat = new List<Vector2>();
            bTri = new List<int>();
            bMask = new List<int>();
            var index = new int[rows * cols];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            Func<int, int, int> V = (r, c) =>
            {
                var k = r * cols + c;
                if (index[k] < 0)
                {
                    index[k] = bFlat.Count;
                    bFlat.Add(new Vector2(x0 + c * s + (r % 2 == 1 ? s / 2 : 0), y0 + r * rowH));
                    bMask.Add(0);
                }
                return index[k];
            };
            Func<Vector2, Vector2, Vector2, bool> covered = (a, b, c) =>
            {
                var mn = Vector2.Min(a, Vector2.Min(b, c)) - Vector2.one * (0.2f * s);
                var mx = Vector2.Max(a, Vector2.Max(b, c)) + Vector2.one * (0.2f * s);
                return Count(sat, scan, mn, mx) > 0;
            };
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int[] up, down;
                if (r % 2 == 0)
                {
                    up = new[] { r, c, r, c + 1, r + 1, c };
                    down = new[] { r, c + 1, r + 1, c + 1, r + 1, c };
                }
                else
                {
                    up = new[] { r, c, r, c + 1, r + 1, c + 1 };
                    down = new[] { r, c, r + 1, c + 1, r + 1, c };
                }
                foreach (var t in new[] { up, down })
                {
                    if (t[1] >= cols || t[3] >= cols || t[5] >= cols) continue;
                    var pa = GridPt(x0, y0, s, rowH, t[0], t[1]);
                    var pb = GridPt(x0, y0, s, rowH, t[2], t[3]);
                    var pc = GridPt(x0, y0, s, rowH, t[4], t[5]);
                    if (!covered(pa, pb, pc)) continue;
                    bTri.Add(V(t[0], t[1]));
                    bTri.Add(V(t[2], t[3]));
                    bTri.Add(V(t[4], t[5]));
                }
            }
            for (int k = 0; k < lines.Count; k++) Cut(k, s);
            if (Crumple) CrumpleFolds(srng, s);
            flat = bFlat.ToArray();
            tri = bTri.ToArray();
            vCount = flat.Length;
            tCount = tri.Length / 3;
            inked = new bool[vCount];
            var near = Vector2.one * (0.3f * s);
            for (int i = 0; i < vCount; i++) inked[i] = Count(sat, scan, flat[i] - near, flat[i] + near) > 0;
        }

        /// <summary>A triangle's smallest height (twice its area over its longest side).</summary>
        float Alt(int a, int b, int c)
        {
            Vector2 pa = bFlat[a], pb = bFlat[b], pc = bFlat[c];
            var area2 = Mathf.Abs((pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y));
            var longest = Mathf.Max((pb - pa).magnitude, Mathf.Max((pc - pb).magnitude, (pa - pc).magnitude));
            return area2 / Mathf.Max(1e-6f, longest);
        }

        static Vector2 GridPt(float x0, float y0, float s, float rowH, int r, int c) => new Vector2(x0 + c * s + (r % 2 == 1 ? s / 2 : 0), y0 + r * rowH);

        /// <summary>A summed-area table of the scan's opaque cells.</summary>
        static int[] Integral(Scan scan)
        {
            int w = scan.SW, h = scan.SH;
            var sat = new int[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                int row = 0;
                for (int x = 0; x < w; x++)
                {
                    row += scan.Solid[y * w + x] ? 1 : 0;
                    sat[(y + 1) * (w + 1) + x + 1] = sat[y * (w + 1) + x + 1] + row;
                }
            }
            return sat;
        }

        /// <summary>Opaque cells in a box of canvas px.</summary>
        static int Count(int[] sat, Scan scan, Vector2 mn, Vector2 mx)
        {
            int w = scan.SW;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(mn.x / scan.FX), 0, scan.SW), x1 = Mathf.Clamp(Mathf.CeilToInt(mx.x / scan.FX), 0, scan.SW);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(mn.y / scan.FY), 0, scan.SH), y1 = Mathf.Clamp(Mathf.CeilToInt(mx.y / scan.FY), 0, scan.SH);
            if (x1 <= x0 || y1 <= y0) return 0;
            return sat[y1 * (w + 1) + x1] - sat[y0 * (w + 1) + x1] - sat[y1 * (w + 1) + x0] + sat[y0 * (w + 1) + x0];
        }

        /// <summary>
        /// The crumple (testing): the sheet folded across planes, every layer at once. Each fold cuts the triangles the
        /// plane crosses (a point on each edge it crosses, shared) and mirrors everything on the plane's far side
        /// through it, so the paper keeps every length, the layers stack, and the mirrored part shows its other face.
        /// </summary>
        void CrumpleFolds(System.Random srng, float s)
        {
            bPos = new List<Vector3>(bFlat.Count * 3);
            foreach (var f in bFlat) bPos.Add(new Vector3(f.x, f.y, 0));
            float U(float a, float b) => a + (b - a) * (float)srng.NextDouble();
            for (int k = 0; k < CrumpleBig + CrumpleRim; k++)
            {
                var cen = Vector3.zero;
                foreach (var q in bPos) cen += q;
                cen /= Mathf.Max(1, bPos.Count);
                if (k < CrumpleBig)
                {
                    // Across its longest spread in 3D (the covariance's main axis), the plane tilted a little about a
                    // random line across it, through near its middle: it halves, and the lump grows round, not flat.
                    float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
                    foreach (var q in bPos)
                    {
                        var r = q - cen;
                        xx += r.x * r.x; xy += r.x * r.y; xz += r.x * r.z;
                        yy += r.y * r.y; yz += r.y * r.z; zz += r.z * r.z;
                    }
                    var ax3 = new Vector3(1, 0.3f, 0.1f).normalized;
                    for (int it = 0; it < 24; it++)
                    {
                        ax3 = new Vector3(xx * ax3.x + xy * ax3.y + xz * ax3.z, xy * ax3.x + yy * ax3.y + yz * ax3.z,
                            xz * ax3.x + yz * ax3.y + zz * ax3.z);
                        var m = ax3.magnitude;
                        if (m < 1e-9f) { ax3 = Vector3.right; break; }
                        ax3 /= m;
                    }
                    var across = Vector3.Cross(ax3, new Vector3(U(-1, 1), U(-1, 1), U(-1, 1)));
                    if (across.sqrMagnitude < 1e-8f) across = Vector3.Cross(ax3, Vector3.forward);
                    across.Normalize();
                    var tilt3 = U(CrumpleTilt.x, CrumpleTilt.y) * Mathf.Deg2Rad * (srng.NextDouble() < 0.5 ? -1 : 1);
                    var n3 = ax3 * Mathf.Cos(tilt3) + across * Mathf.Sin(tilt3);
                    float ext3 = 0;
                    foreach (var q in bPos) ext3 = Mathf.Max(ext3, Mathf.Abs(Vector3.Dot(q - cen, n3)));
                    FoldAcross(cen + n3 * (U(-0.15f, 0.15f) * ext3), n3, 0.15f * s);
                    continue;
                }
                {
                    // A corner turned in: a plane part way out toward the edge, any way round.
                    var a = U(0, 2 * Mathf.PI);
                    var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float ext = 0;
                    foreach (var q in bPos) ext = Mathf.Max(ext, Vector2.Dot((Vector2)q - (Vector2)cen, ax));
                    var c2 = (Vector2)cen + ax * (U(CrumpleRimAt.x, CrumpleRimAt.y) * ext);
                    var tilt = U(CrumpleTilt.x, CrumpleTilt.y) * Mathf.Deg2Rad * (srng.NextDouble() < 0.5 ? -1 : 1);
                    var n = new Vector3(ax.x * Mathf.Cos(tilt), ax.y * Mathf.Cos(tilt), Mathf.Sin(tilt));
                    FoldAcross(new Vector3(c2.x, c2.y, cen.z), n, 0.15f * s);
                }
            }
            crumplePos = bPos.ToArray();
        }

        void FoldAcross(Vector3 c, Vector3 n, float snap)
        {
            var d = new List<float>(bPos.Count + 64);
            for (int i = 0; i < bPos.Count; i++)
            {
                var x = Vector3.Dot(bPos[i] - c, n);
                // Points very near the plane go onto it (no slivers; the paper stretches by far less than a pixel's worth).
                if (Mathf.Abs(x) < snap)
                {
                    bPos[i] -= n * x;
                    x = 0;
                }
                d.Add(x);
            }
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                var key = Math.Min(a, b) * 1000003L + Math.Max(a, b);
                if (!cache.TryGetValue(key, out var m))
                {
                    var u = d[a] / (d[a] - d[b]);
                    m = bFlat.Count;
                    bFlat.Add(Vector2.Lerp(bFlat[a], bFlat[b], u));
                    bPos.Add(Vector3.Lerp(bPos[a], bPos[b], u));
                    bMask.Add(0);
                    d.Add(0);
                    cache[key] = m;
                }
                return m;
            }
            var outTri = new List<int>(bTri.Count + 256);
            var poly = new List<int>(5);
            for (int t = 0; t < bTri.Count; t += 3)
            {
                float d0 = d[bTri[t]], d1 = d[bTri[t + 1]], d2 = d[bTri[t + 2]];
                if (Mathf.Min(d0, Mathf.Min(d1, d2)) >= 0 || Mathf.Max(d0, Mathf.Max(d1, d2)) <= 0)
                {
                    outTri.Add(bTri[t]);
                    outTri.Add(bTri[t + 1]);
                    outTri.Add(bTri[t + 2]);
                    continue;
                }
                for (int side = 1; side >= -1; side -= 2)
                {
                    poly.Clear();
                    for (int j = 0; j < 3; j++)
                    {
                        int a = bTri[t + j], b = bTri[t + (j + 1) % 3];
                        float da = side * d[a], db = side * d[b];
                        if (da >= 0) poly.Add(a);
                        if ((da > 0 && db < 0) || (da < 0 && db > 0)) poly.Add(Mid(a, b));
                    }
                    int o = 0;
                    if (poly.Count == 4)
                    {
                        var a0 = Mathf.Min(Alt(poly[0], poly[1], poly[2]), Alt(poly[0], poly[2], poly[3]));
                        var a1 = Mathf.Min(Alt(poly[1], poly[2], poly[3]), Alt(poly[1], poly[3], poly[0]));
                        if (a1 > a0) o = 1;
                    }
                    for (int j = 1; j + 1 < poly.Count; j++)
                    {
                        outTri.Add(poly[o]);
                        outTri.Add(poly[(o + j) % poly.Count]);
                        outTri.Add(poly[(o + j + 1) % poly.Count]);
                    }
                }
            }
            bTri = outTri;
            // The far side mirrored through the plane (it lands on the near side, over or under the paper there).
            for (int i = 0; i < bPos.Count; i++)
                if (d[i] > 0) bPos[i] -= 2 * d[i] * n;
        }

        /// <summary>Of what the viewer sees of the lump (a coarse depth buffer), the share that is the paper's back.</summary>
        float BackShare(Vector3[] p)
        {
            const int G = 48;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < vCount; i++)
            {
                mn = Vector2.Min(mn, p[i]);
                mx = Vector2.Max(mx, p[i]);
            }
            var cell = Mathf.Max(mx.x - mn.x, mx.y - mn.y) / G + 1e-4f;
            var zb = new float[G * G];
            var bk = new bool[G * G];
            for (int i = 0; i < zb.Length; i++) zb[i] = float.MinValue;
            for (int t = 0; t < tCount; t++)
            {
                int i0 = tri[t * 3], i1 = tri[t * 3 + 1], i2 = tri[t * 3 + 2];
                Vector2 a = (Vector2)(p[i0] - (Vector3)mn) / cell, b = (Vector2)(p[i1] - (Vector3)mn) / cell, c = (Vector2)(p[i2] - (Vector3)mn) / cell;
                var den = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(den) < 1e-6f) continue;
                Vector2 fa = flat[i0], fb = flat[i1], fc = flat[i2];
                var fden = (fb.x - fa.x) * (fc.y - fa.y) - (fc.x - fa.x) * (fb.y - fa.y);
                var isBack = den * fden < 0;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(G - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(G - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    var l1 = ((px - a.x) * (c.y - a.y) - (c.x - a.x) * (py - a.y)) / den;
                    var l2 = ((b.x - a.x) * (py - a.y) - (px - a.x) * (b.y - a.y)) / den;
                    var l0 = 1 - l1 - l2;
                    if (l0 < 0 || l1 < 0 || l2 < 0) continue;
                    var z = l0 * p[i0].z + l1 * p[i1].z + l2 * p[i2].z;
                    var k = y * G + x;
                    if (z <= zb[k]) continue;
                    // Only where the picture is (the sheet's margin round it isn't drawn).
                    var fp = l0 * fa + l1 * fb + l2 * fc;
                    if (Count(bSat, bScan, fp - Vector2.one, fp + Vector2.one) == 0) continue;
                    zb[k] = z;
                    bk[k] = isBack;
                }
            }
            int seen = 0, back = 0;
            for (int k = 0; k < zb.Length; k++)
                if (zb[k] > float.MinValue)
                {
                    seen++;
                    if (bk[k]) back++;
                }
            return seen > 0 ? (float)back / seen : 0;
        }

        /// <summary>Cut every triangle the line `k` crosses in two (a point on each edge it crosses, shared).</summary>
        void Cut(int k, float s)
        {
            var l = lines[k];
            var bit = 1 << k;
            const float eps = 1e-3f;
            // Snap grid points near the line onto it (not points already on an earlier line).
            for (int i = 0; i < bFlat.Count; i++)
            {
                var d = Vector2.Dot(bFlat[i] - l.P, l.N);
                if (Mathf.Abs(d) < 0.33f * s && bMask[i] == 0)
                {
                    bFlat[i] -= l.N * d;
                    bMask[i] |= bit;
                }
                else if (Mathf.Abs(d) <= eps) bMask[i] |= bit;
            }
            var cache = new Dictionary<long, int>();
            var outTri = new List<int>(bTri.Count + 64);
            var ring = new List<int>(5);
            var dd = new float[3];
            for (int t = 0; t < bTri.Count; t += 3)
            {
                bool pos = false, neg = false;
                for (int j = 0; j < 3; j++)
                {
                    var v = bTri[t + j];
                    dd[j] = (bMask[v] & bit) != 0 ? 0 : Vector2.Dot(bFlat[v] - l.P, l.N);
                    if (dd[j] > eps) pos = true;
                    if (dd[j] < -eps) neg = true;
                }
                if (!(pos && neg))
                {
                    outTri.Add(bTri[t]);
                    outTri.Add(bTri[t + 1]);
                    outTri.Add(bTri[t + 2]);
                    continue;
                }
                ring.Clear();
                var side = new List<int>(5);
                for (int j = 0; j < 3; j++)
                {
                    int a = bTri[t + j], b = bTri[t + (j + 1) % 3];
                    float da = dd[j], db = dd[(j + 1) % 3];
                    ring.Add(a);
                    side.Add(da > eps ? 1 : da < -eps ? -1 : 0);
                    if ((da > eps && db < -eps) || (da < -eps && db > eps))
                    {
                        var key = Math.Min(a, b) * 1000003L + Math.Max(a, b);
                        if (!cache.TryGetValue(key, out var m))
                        {
                            var u = da / (da - db);
                            m = bFlat.Count;
                            bFlat.Add(Vector2.Lerp(bFlat[a], bFlat[b], u));
                            bMask.Add(bit | (bMask[a] & bMask[b]));
                            cache[key] = m;
                        }
                        ring.Add(m);
                        side.Add(0);
                    }
                }
                foreach (var want in new[] { 1, -1 })
                {
                    var poly = new List<int>(4);
                    for (int j = 0; j < ring.Count; j++)
                        if (side[j] == want || side[j] == 0) poly.Add(ring[j]);
                    // A quad: split along the diagonal that leaves the fatter triangles.
                    int o = 0;
                    if (poly.Count == 4)
                    {
                        var a0 = Mathf.Min(Alt(poly[0], poly[1], poly[2]), Alt(poly[0], poly[2], poly[3]));
                        var a1 = Mathf.Min(Alt(poly[1], poly[2], poly[3]), Alt(poly[1], poly[3], poly[0]));
                        if (a1 > a0) o = 1;
                    }
                    for (int j = 1; j + 1 < poly.Count; j++)
                    {
                        outTri.Add(poly[o]);
                        outTri.Add(poly[(o + j) % poly.Count]);
                        outTri.Add(poly[(o + j + 1) % poly.Count]);
                    }
                }
            }
            bTri = outTri;
        }

        // --- The solver ---

        void SetupSolver(Scan scan, System.Random srng, float groundY)
        {
            pos = new Vector3[vCount];
            prev = new Vector3[vCount];
            force = new Vector3[vCount];
            normal = new Vector3[tCount];
            for (int i = 0; i < vCount; i++) pos[i] = prev[i] = flat[i];
            // Bars on every edge; hinges on every edge two triangles share.
            var edges = new Dictionary<long, int>();
            var barList = new List<Bar>();
            var firstTri = new List<int>();
            var firstCorner = new List<int>();
            var hingeList = new List<Hinge>();
            triHinge = new int[tCount * 3];
            for (int i = 0; i < triHinge.Length; i++) triHinge[i] = -1;
            var edgeHinge = new Dictionary<long, int>();
            for (int t = 0; t < tCount; t++)
            for (int j = 0; j < 3; j++)
            {
                int a = tri[t * 3 + j], b = tri[t * 3 + (j + 1) % 3], c = tri[t * 3 + (j + 2) % 3];
                var key = Math.Min(a, b) * 1000003L + Math.Max(a, b);
                if (!edges.TryGetValue(key, out var e))
                {
                    edges[key] = barList.Count;
                    barList.Add(new Bar { A = a, B = b, L = (flat[a] - flat[b]).magnitude });
                    firstTri.Add(t);
                    firstCorner.Add((j + 2) % 3);
                    continue;
                }
                // The second triangle on this edge (it runs b to a here): a hinge.
                int t1 = firstTri[e];
                int c1 = tri[t1 * 3 + firstCorner[e]];
                var bar = barList[e];
                var h = new Hinge { A = bar.A, B = bar.B, C = c1, D = c, T1 = t1, T2 = t, Line = -1 };
                // Which crease line it lies on, if any.
                for (int k = 0; k < lines.Count; k++)
                    if ((bMask[a] & bMask[b] & (1 << k)) != 0) h.Line = k;
                triHinge[t1 * 3 + firstCorner[e]] = hingeList.Count;
                triHinge[t * 3 + (j + 2) % 3] = hingeList.Count;
                hingeList.Add(h);
            }
            bars = barList.ToArray();
            hinges = hingeList.ToArray();
            // Panels: triangles joined across every hinge that isn't on a crease.
            var parent = new int[tCount];
            for (int t = 0; t < tCount; t++) parent[t] = t;
            Func<int, int> root = null;
            root = t => parent[t] == t ? t : (parent[t] = root(parent[t]));
            foreach (var h in hinges)
                if (h.Line < 0) parent[root(h.T1)] = root(h.T2);
            triPanel = new int[tCount];
            var ids = new Dictionary<int, int>();
            for (int t = 0; t < tCount; t++)
            {
                var r = root(t);
                if (!ids.TryGetValue(r, out var id)) ids[r] = id = ids.Count;
                triPanel[t] = id;
            }
            panelCount = ids.Count;
            nAcc = new Vector3[vCount * panelCount];
            rNormal = new Vector3[tCount * 3];

            // Each crease line's sign: the first keeps one sign all along; a later one flips each time it crosses an
            // earlier one (so every crossing folds like a map fold: three one way, one the other).
            var baseSign = new float[lines.Count];
            var mag = new float[lines.Count];
            lineStart = new float[lines.Count];
            for (int k = 0; k < lines.Count; k++)
            {
                baseSign[k] = lines[k].Sign;
                // A flap toward the viewer lies well past upright over the picture (its back and its shadow show). One
                // behind (or a pleat) turns only part way: still in sight, foreshortened and toned by its angle (folded
                // flat behind, the camera straight on, it would just vanish).
                var r = (float)srng.NextDouble();
                // (A flap hanging down that folds behind turns least: the camera looks down a little, so the further it
                // turns, the more of it hides behind the picture.)
                var down = lines[k].N.y < -0.5f;
                mag[k] = Mathf.Deg2Rad * (lines[k].Sign > 0 && lines[k].Parent < 0 ? Mathf.Min(125 + 35 * r, lines[k].MaxDeg)
                    : lines[k].Parent < 0 ? (down ? 30 + 25 * r : 45 + 55 * r) : 50 + 60 * r);
                lineStart[k] = 0.03f * k + 0.05f * (float)srng.NextDouble();
            }
            // Stiffness per edge, by its length in grid cells and the paper's size squared, so any size folds at
            // about the same speed (the swing's frequency squared is about 1.4 FoldK); damping a share of critical.
            var cell = Mathf.Max(1, sheetSize / GridCells);
            // Damping by the points next to the hinge (unit mass, at about a cell's height from it): stable at any size.
            var arm = 0.87f * cell;
            for (int i = 0; i < hinges.Length; i++)
            {
                ref var h = ref hinges[i];
                var len = bars[edges[Math.Min(h.A, h.B) * 1000003L + Math.Max(h.A, h.B)]].L;
                var k = FoldK * sheetSize * sheetSize * len / cell;
                if (h.Line < 0)
                {
                    h.K = k * FacetShare;
                    h.Damp = 2 * HingeDamp * Mathf.Sqrt(h.K) * arm;
                    h.Target = 0;
                    continue;
                }
                var mid = (flat[h.A] + flat[h.B]) / 2;
                var sign = baseSign[h.Line];
                for (int j = 0; j < h.Line; j++)
                    if (!NoCross && Vector2.Dot(mid - lines[j].P, lines[j].N) > 0) sign = -sign;
                h.K = k;
                h.Damp = 2 * HingeDamp * Mathf.Sqrt(h.K) * arm;
                h.Target = sign * mag[h.Line];
            }
            // Thin triangles (where a crease cuts the grid): their hinges push through a short lever, so stiffness and
            // damping are capped by the narrower wing's height, keeping every step stable.
            hFloor = 0.4f * cell;
            for (int i = 0; i < hinges.Length; i++)
            {
                ref var h = ref hinges[i];
                var e = flat[h.B] - flat[h.A];
                var len2 = Mathf.Max(1e-6f, e.sqrMagnitude);
                float hmin = float.MaxValue;
                foreach (var w in new[] { h.C, h.D })
                {
                    var t = Vector2.Dot(flat[w] - flat[h.A], e) / len2;
                    hmin = Mathf.Min(hmin, (flat[w] - flat[h.A] - t * e).magnitude);
                }
                h.HF = Mathf.Max(hmin, hFloor);
                h.K = Mathf.Min(h.K, 0.02f * h.HF * h.HF / (SubStep * SubStep));
                h.Damp = Mathf.Min(2 * HingeDamp * Mathf.Sqrt(h.K) * h.HF, 0.035f * h.HF * h.HF / SubStep);
            }
            // Testing: how near the explicit steps are to blowing up (each point's stiffness and damping from the hinges
            // round it, times the step; near 1 or more is trouble) and the thinnest triangle.
            if (Trace)
            {
                var stiff = new float[vCount];
                var damp = new float[vCount];
                float minH = float.MaxValue;
                foreach (var h in hinges)
                {
                    var e = flat[h.B] - flat[h.A];
                    var len2 = Mathf.Max(1e-6f, e.sqrMagnitude);
                    foreach (var w in new[] { h.C, h.D })
                    {
                        var t = Vector2.Dot(flat[w] - flat[h.A], e) / len2;
                        var hh = (flat[w] - flat[h.A] - t * e).magnitude;
                        minH = Mathf.Min(minH, hh);
                        hh = Mathf.Max(hh, hFloor);
                        var k2 = h.K / Mathf.Max(1e-6f, hh * hh);
                        var d2 = h.Damp / Mathf.Max(1e-6f, hh * hh);
                        stiff[w] += k2;
                        damp[w] += d2;
                        stiff[h.A] += k2 * (1 - t) * (1 - t);
                        damp[h.A] += d2 * (1 - t) * (1 - t);
                        stiff[h.B] += k2 * t * t;
                        damp[h.B] += d2 * t * t;
                    }
                }
                float worstK = 0, worstD = 0;
                for (int i = 0; i < vCount; i++)
                {
                    worstK = Mathf.Max(worstK, Mathf.Sqrt(stiff[i]) * SubStep);
                    worstD = Mathf.Max(worstD, damp[i] * SubStep);
                }
                var signs = "";
                foreach (var l in lines) signs += (l.Sign > 0 ? "front" : "back") + (l.Parent >= 0 ? "(fan)" : "") + " ";
                Debug.Log($"drakepg paper trace: setup, {lines.Count} creases ({signs}), thinnest triangle {minH / cell:0.00} cells, worst stiffness x step {worstK:0.00}, worst damping x step {worstD:0.00}");
            }
            // The face panel: the whole of the paper on the face's side of every crease, clear of the creases themselves
            // (held flat like a card on a stand, so the flaps turn about it rather than bending it; a point held on a
            // crease would pin the flap next to it).
            var panel = new List<int>();
            var clearOf = 0.6f * cell;
            for (int i = 0; i < vCount; i++)
            {
                var inside = true;
                foreach (var l in lines)
                    if (Vector2.Dot(flat[i] - l.P, l.N) > -clearOf) inside = false;
                if (inside) panel.Add(i);
            }
            if (panel.Count < 3)
            {
                var order = new List<int>();
                for (int i = 0; i < vCount; i++) order.Add(i);
                order.Sort((p, q) => (flat[p] - face).sqrMagnitude.CompareTo((flat[q] - face).sqrMagnitude));
                panel = order.GetRange(0, Mathf.Min(6, order.Count));
            }
            facePanel = panel.ToArray();
            zHold = 0;

            // The ground, gravity and the light in canvas px.
            var gw = Design.World(Design.FromWorld(body.transform.position).x, groundY);
            var gl = body.transform.InverseTransformPoint(gw);
            var gc = new Vector2(gl.x * canvasPpu + origin.x, gl.y * canvasPpu + origin.y);
            // World down in canvas px (scale and mirroring counted: the canvas follows the body's transform).
            var downL = body.transform.InverseTransformVector(Vector3.down);
            upC = -new Vector2(downL.x, downL.y).normalized;
            groundC = Vector2.Dot(gc, upC);
            // Nothing starts under the ground (the feet can sit a little below it): pushed out, it would be flung up.
            for (int i = 0; i < vCount; i++)
                if (inked[i]) groundC = Mathf.Min(groundC, Vector2.Dot(flat[i], upC));
            var designPerCanvas = (Design.FromWorld(body.transform.TransformPoint(new Vector3(1 / canvasPpu, 0, 0))) - Design.FromWorld(body.transform.position)).magnitude;
            gravityC = -(Vector3)upC * (Gravity / Mathf.Max(1e-3f, designPerCanvas));
            var flip = body.transform.lossyScale.x < 0 ? -1 : 1;
            lightC = new Vector3(-0.5f * flip, 0.7f, 0.55f).normalized;
            solidMat.SetVector(LightId, lightC);
            var back = Crumple ? Color.Lerp(Back[0], scan.Mean, 0.1f) : Color.Lerp(Back[0] * BackLight, scan.Mean, BackTint);
            back.a = 0.04f;
            solidMat.SetColor(BackId, back);
            showMat.SetFloat(OutlineId, Crumple ? CrumpleOutline : 0);
            simTime = 0;
            landedOnce = false;
            startWidthPx = 0;
            rv = new Vector3[tCount * 3];
            sv = new Vector3[tCount * 3];
            ruv = new Vector2[tCount * 3];
            rWorld = new Vector3[tCount * 3];
            rBary = new Vector3[tCount * 3];
            rLines = new Vector3[tCount * 3];
            var idx = new int[tCount * 3];
            for (int i = 0; i < idx.Length; i++)
            {
                idx[i] = i;
                ruv[i] = new Vector2(flat[tri[i]].x / FW, flat[tri[i]].y / FH);
                rBary[i] = i % 3 == 0 ? new Vector3(1, 0, 0) : i % 3 == 1 ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
            }
            paperMesh.vertices = rv;
            paperMesh.uv = ruv;
            paperMesh.SetUVs(1, rWorld);
            paperMesh.SetUVs(2, rBary);
            paperMesh.SetUVs(3, rLines);
            paperMesh.SetIndices(idx, MeshTopology.Triangles, 0);
            shadowMesh.vertices = sv;
            shadowMesh.uv = ruv;
            shadowMesh.SetUVs(1, rWorld);
            shadowMesh.SetUVs(2, rBary);
            shadowMesh.SetUVs(3, rLines);
            shadowMesh.SetIndices(idx, MeshTopology.Triangles, 0);
            if (Crumple)
            {
                CrumplePose();
                Angles();
            }
            else if (!float.IsNaN(HandAngle))
            {
                HandPose(HandAngle);
                Angles();
            }
            else
            {
                // The pop: up, away from the hero, turning (seeded).
                var mid = Vector2.zero;
                for (int i = 0; i < vCount; i++) mid += flat[i];
                mid /= Mathf.Max(1, vCount);
                var rightL = body.transform.InverseTransformVector(Vector3.right);
                var away = new Vector2(rightL.x, rightL.y).normalized;
                var vUp = upC * Mathf.Sqrt(2 * gravityC.magnitude * Hop * sheetSize) + away * (HopPush * sheetSize);
                var spin = HopSpin * (0.5f + 0.5f * (float)srng.NextDouble()) * (srng.NextDouble() < 0.5 ? -1 : 1);
                for (int i = 0; i < vCount; i++)
                {
                    var r = flat[i] - mid;
                    var v = vUp + spin * new Vector2(-r.y, r.x);
                    prev[i] = pos[i] - new Vector3(v.x, v.y, 0) * SubStep;
                }
            }
        }

        /// <summary>
        /// Testing (step 1c, gate A): the folded lump (<see cref="CrumpleFolds"/>) at rest: its broad side to the viewer,
        /// its longest spread along the ground, the side showing nearer `CrumpleBack` of paper back; set where the picture
        /// stood, on the ground.
        /// </summary>
        void CrumplePose()
        {
            var mid = Vector3.zero;
            for (int i = 0; i < vCount; i++) mid += (Vector3)flat[i];
            mid /= Mathf.Max(1, vCount);
            var cen = Vector3.zero;
            for (int i = 0; i < vCount; i++) cen += crumplePos[i];
            cen /= Mathf.Max(1, vCount);
            // Its broad side to the viewer and its longest spread along the ground (as a lump comes to rest): the
            // covariance's axes, longest (e1) to the ground's direction, the thinnest (e3) toward the viewer.
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int i = 0; i < vCount; i++)
            {
                var r = crumplePos[i] - cen;
                xx += r.x * r.x; xy += r.x * r.y; xz += r.x * r.z;
                yy += r.y * r.y; yz += r.y * r.z; zz += r.z * r.z;
            }
            Vector3 Mul(Vector3 v) => new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
            Vector3 Main(Vector3 v, Vector3 without)
            {
                for (int it = 0; it < 40; it++)
                {
                    v = Mul(v);
                    v -= Vector3.Dot(v, without) * without;
                    var m = v.magnitude;
                    if (m < 1e-9f) return Vector3.Cross(without, Vector3.forward).normalized;
                    v /= m;
                }
                return v;
            }
            var e1 = Main(new Vector3(1, 0.31f, 0.17f).normalized, Vector3.zero);
            var e2 = Main(Vector3.Cross(e1, new Vector3(0.2f, 0.3f, 1)).normalized, e1);
            var e3 = Vector3.Cross(e1, e2);
            var up3 = new Vector3(upC.x, upC.y, 0).normalized;
            var side3 = new Vector3(upC.y, -upC.x, 0).normalized;
            var test = new Vector3[vCount];
            float bestScore = float.MaxValue;
            int bestFace = 1;
            // Its two broad sides (the other one: half a turn about the up axis); the one nearer `CrumpleBack` of back.
            for (int face = 1; face >= -1; face -= 2)
            {
                for (int i = 0; i < vCount; i++)
                {
                    var r = crumplePos[i] - cen;
                    test[i] = side3 * (face * Vector3.Dot(r, e1)) + up3 * Vector3.Dot(r, e2) + Vector3.forward * (face * Vector3.Dot(r, e3));
                }
                var share = BackShare(test);
                var score = Mathf.Abs(share - CrumpleBack);
                if (Trace) Debug.Log($"[Paper] crumple side {face}: back {share:P0}");
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFace = face;
                }
            }
            for (int i = 0; i < vCount; i++)
            {
                var r = crumplePos[i] - cen;
                pos[i] = prev[i] = mid + CrumpleScale * (side3 * (bestFace * Vector3.Dot(r, e1)) + up3 * Vector3.Dot(r, e2) + Vector3.forward * (bestFace * Vector3.Dot(r, e3)));
            }
            // On the ground: its lowest inked point there.
            float low = float.MaxValue;
            for (int i = 0; i < vCount; i++)
                if (inked[i]) low = Mathf.Min(low, Vector2.Dot(pos[i], upC));
            var lift = (Vector3)(upC * (groundC - low));
            for (int i = 0; i < vCount; i++) pos[i] = prev[i] = pos[i] + lift;
            // The shadow narrows from the flat picture's width (design units, as PacketSolid measures) to the lump's.
            float w0 = float.MaxValue, w1 = float.MinValue;
            for (int i = 0; i < vCount; i++)
            {
                var x = CanvasToDesign(flat[i]).x;
                w0 = Mathf.Min(w0, x);
                w1 = Mathf.Max(w1, x);
            }
            startWidthPx = Mathf.Max(1, w1 - w0);
        }

        /// <summary>Testing: the first line's far side turned by `deg` about the line (the renderer alone).</summary>
        void HandPose(float deg)
        {
            if (lines.Count == 0) return;
            var l = lines[0];
            var a = deg * Mathf.Deg2Rad;
            var tdir = new Vector2(-l.N.y, l.N.x);
            for (int i = 0; i < vCount; i++)
            {
                var q = flat[i] - l.P;
                var d = Vector2.Dot(q, l.N);
                if (d <= 0) continue;
                var along = Vector2.Dot(q, tdir);
                var p2 = l.P + tdir * along + l.N * (d * Mathf.Cos(a));
                pos[i] = prev[i] = new Vector3(p2.x, p2.y, d * Mathf.Sin(a));
            }
        }

        /// <summary>Step the solver to `time` (Anim seconds) in fixed steps, a cap a frame.</summary>
        void Advance(float time, Action<float, float> landed)
        {
            if (!float.IsNaN(HandAngle) || Crumple)
            {
                simTime = time;
                return;
            }
            int n = 0;
            while (simTime + SubStep <= time && n < MaxSubSteps)
            {
                if (HoldAt >= 0 && simTime >= HoldAt) return;
                Step(SubStep, landed);
                simTime += SubStep;
                n++;
            }
            // Behind (a slow frame): the time it couldn't catch up is let go, not saved up.
            if (n == MaxSubSteps) simTime = time;
            if (Trace && simTime >= traceAt)
            {
                traceAt = simTime + 0.1f;
                float low = float.MaxValue, fast = 0;
                for (int i = 0; i < vCount; i++)
                {
                    if (inked[i]) low = Mathf.Min(low, Vector2.Dot(pos[i], upC));
                    fast = Mathf.Max(fast, (pos[i] - prev[i]).magnitude / SubStep);
                }
                var c = Centre();
                var mv = Vector3.zero;
                for (int i = 0; i < vCount; i++) mv += (pos[i] - prev[i]) / SubStep;
                mv /= vCount;
                float facet = 0;
                foreach (var h in hinges)
                    if (h.Line < 0) facet = Mathf.Max(facet, Mathf.Abs(h.Theta));
                var flapZ = "";
                for (int k = 0; k < lines.Count; k++)
                {
                    var l = lines[k];
                    float zs = 0; int zn = 0;
                    for (int i = 0; i < vCount; i++)
                        if (Vector2.Dot(flat[i] - l.P, l.N) > 0.3f * sheetSize / GridCells) { zs += pos[i].z; zn++; }
                    float th = 0, tg = 0, tmin = float.MaxValue, tmax = float.MinValue; int hn = 0;
                    foreach (var h in hinges)
                        if (h.Line == k)
                        {
                            th += h.Theta; tg += h.Target; hn++;
                            tmin = Mathf.Min(tmin, h.Theta); tmax = Mathf.Max(tmax, h.Theta);
                        }
                    var d = Mathf.Rad2Deg;
                    flapZ += $"{(l.Sign > 0 ? "front" : "back")} z {(zn > 0 ? zs / zn : 0):0} angle {(hn > 0 ? th / hn * d : 0):0} ({tmin * d:0}..{tmax * d:0}) of {(hn > 0 ? tg / hn * d : 0):0}, {hn} hinges, {zn} pts; ";
                }
                Debug.Log($"drakepg paper trace: {simTime:0.00} s, centre {c}, lowest {low:0}, fastest {fast:0} px/s, worst facet {facet * Mathf.Rad2Deg:0} deg, flaps: {flapZ}steps {n}");
            }
        }

        /// <summary>Every triangle's normal and every hinge's angle (unwrapped: it runs on past +-180 rather than jumping).</summary>
        void Angles()
        {
            for (int t = 0; t < tCount; t++)
            {
                var a = pos[tri[t * 3]];
                normal[t] = Vector3.Cross(pos[tri[t * 3 + 1]] - a, pos[tri[t * 3 + 2]] - a).normalized;
            }
            for (int i = 0; i < hinges.Length; i++)
            {
                ref var h = ref hinges[i];
                var n1 = normal[h.T1];
                var n2 = normal[h.T2];
                var e = pos[h.B] - pos[h.A];
                var len2 = e.sqrMagnitude;
                h.Last = h.Theta;
                if (len2 < 1e-8f) continue;
                var ehat = e / Mathf.Sqrt(len2);
                var theta = Mathf.Atan2(Vector3.Dot(Vector3.Cross(n1, ehat), n2), Vector3.Dot(n1, n2));
                var diff = theta - h.Theta;
                if (diff < -5) diff += 2 * Mathf.PI;
                else if (diff > 5) diff -= 2 * Mathf.PI;
                h.Theta += diff;
            }
        }

        void Step(float dt, Action<float, float> landed)
        {
            Angles();
            for (int i = 0; i < vCount; i++) force[i] = gravityC;
            for (int i = 0; i < hinges.Length; i++)
            {
                ref var h = ref hinges[i];
                var n1 = normal[h.T1];
                var n2 = normal[h.T2];
                var e = pos[h.B] - pos[h.A];
                var len2 = e.sqrMagnitude;
                if (len2 < 1e-8f) continue;
                var t1 = Vector3.Dot(pos[h.C] - pos[h.A], e) / len2;
                var t2 = Vector3.Dot(pos[h.D] - pos[h.A], e) / len2;
                var h1 = (pos[h.C] - pos[h.A] - t1 * e).magnitude;
                var h2 = (pos[h.D] - pos[h.A] - t2 * e).magnitude;
                // A crease squashed too thin does nothing (Origami Simulator's guard).
                if (h1 < 1e-3f || h2 < 1e-3f) continue;
                h1 = Mathf.Max(h1, hFloor);
                h2 = Mathf.Max(h2, hFloor);
                var target = h.Line >= 0 ? h.Target * Mathf.Clamp01((simTime - lineStart[h.Line]) / FoldTime) : h.Target;
                var ang = h.K * (target - h.Theta) - h.Damp * (h.Theta - h.Last) / dt;
                var f1 = n1 * (ang / h1);
                var f2 = n2 * (ang / h2);
                force[h.C] += f1;
                force[h.D] += f2;
                force[h.A] -= (1 - t1) * f1 + (1 - t2) * f2;
                force[h.B] -= t1 * f1 + t2 * f2;
            }
            // Verlet, with air damping, and the paper's motion against its own middle damped harder (the folds settle;
            // the fall isn't slowed).
            var keep = Mathf.Pow(AirDamp, dt);
            var inner = Mathf.Pow(InnerDamp, dt);
            var dt2 = dt * dt;
            var mean = Vector3.zero;
            for (int i = 0; i < vCount; i++) mean += pos[i] - prev[i];
            mean /= vCount;
            for (int i = 0; i < vCount; i++)
            {
                var p = pos[i];
                var v = (mean + (p - prev[i] - mean) * inner) * keep;
                pos[i] = p + v + force[i] * dt2;
                prev[i] = p;
            }
            // The ground: a bounce off it, friction along it, and the first hard landing reported.
            float hardest = 0;
            var up3 = (Vector3)upC;
            for (int i = 0; i < vCount; i++)
            {
                if (NoGround || !inked[i]) continue;
                var below = groundC - Vector3.Dot(pos[i], up3);
                if (below <= 0) continue;
                var v = (pos[i] - prev[i]) / dt;
                var vn = Vector3.Dot(v, up3);
                hardest = Mathf.Max(hardest, -vn);
                if (Trace) groundPush += below / dt / vCount;
                pos[i] += up3 * below;
                // Off the ground at a share of the speed it hit with; friction along it.
                var vt = v - up3 * vn;
                prev[i] = pos[i] - (vt * (1 - Friction) + up3 * (-Mathf.Min(0, vn) * Bounce)) * dt;
            }
            // A hard landing: as fast as a fall of a few hundredths of its size.
            if (!landedOnce && simTime > 0.08f && hardest > Mathf.Sqrt(2 * gravityC.magnitude * 0.03f * sheetSize))
            {
                landedOnce = true;
                var (mid, width, _) = Packet();
                landed?.Invoke(mid.x, width);
            }
            // The paper doesn't stretch: each edge back to its length.
            for (int it = 0; it < 4; it++)
            {
                for (int b = 0; b < bars.Length; b++)
                {
                    var bar = bars[b];
                    var d = pos[bar.B] - pos[bar.A];
                    var l = d.magnitude;
                    if (l < 1e-6f) continue;
                    var corr = d * (0.5f * (l - bar.L) / l);
                    pos[bar.A] += corr;
                    pos[bar.B] -= corr;
                }
                // The face panel stands facing the viewer, like a paper figure on its stand: it slides, falls and turns in
                // the picture, never over (the face stays in sight).
                foreach (var v in facePanel)
                {
                    pos[v].z = zHold;
                    prev[v].z = zHold;
                }
                // Held on the ground without the push counting as speed (a deep push would fling it).
                if (NoGround) continue;
                for (int i = 0; i < vCount; i++)
                {
                    if (!inked[i]) continue;
                    var below = groundC - Vector3.Dot(pos[i], up3);
                    if (below <= 0) continue;
                    if (Trace) groundPush += below / dt / vCount;
                    pos[i] += up3 * below;
                    prev[i] += up3 * below;
                }
            }
        }

        /// <summary>Lying still: on the ground (or never leaving it), hardly moving, after the creases had time.</summary>
        bool Settled()
        {
            if (!float.IsNaN(HandAngle) || Crumple) return simTime > 1.2f;
            if (HoldAt >= 0) return false;
            if (simTime < 0.5f) return false;
            // The paper's mean speed (one point buzzing on a thin triangle doesn't keep it awake); once still, it sleeps.
            float v = 0;
            for (int i = 0; i < vCount; i++) v += (pos[i] - prev[i]).magnitude / SubStep;
            v /= Mathf.Max(1, vCount);
            return v < 0.03f * sheetSize || simTime > 3;
        }

        /// <summary>The middle of the paper now (canvas px): where the picture's perspective and the texture are centred.</summary>
        Vector2 Centre()
        {
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < vCount; i++)
            {
                mn = Vector2.Min(mn, pos[i]);
                mx = Vector2.Max(mx, pos[i]);
            }
            return (mn + mx) / 2;
        }

        Vector3 Project(Vector3 p, Vector2 c, float eye)
        {
            var k = eye / Mathf.Max(0.2f * eye, eye - p.z);
            return new Vector3(c.x + (p.x - c.x) * k, c.y + (p.y - c.y) * k - Tilt * p.z, p.z);
        }

        /// <summary>The paper drawn into its texture (the paper, then its shadows), and the quad moved to it.</summary>
        void Draw()
        {
            var c = Centre();
            var eye = Eye * sheetSize;
            float zPanel = 0;
            foreach (var v in facePanel) zPanel += pos[v].z;
            zPanel /= facePanel.Length;
            var lz = Mathf.Max(0.2f, lightC.z);
            for (int t = 0; t < tCount; t++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var v = tri[t * 3 + j];
                    var p = pos[v];
                    rv[t * 3 + j] = Project(p, c, eye);
                    rWorld[t * 3 + j] = p;
                    // Its shadow: moved along the light by its height above the face panel, a little behind it.
                    var hgt = Mathf.Max(0, p.z - zPanel);
                    var q = new Vector3(p.x - lightC.x * hgt / lz, p.y - lightC.y * hgt / lz, p.z);
                    var sp = Project(q, c, eye);
                    sp.z = p.z - 0.004f * sheetSize - 0.02f * hgt;
                    sv[t * 3 + j] = sp;
                }
                // The fold lines: a crease line once it has turned; the paper between, only where it bends sharply.
                var w = Vector3.zero;
                for (int j = 0; j < 3; j++)
                {
                    var hi = triHinge[t * 3 + j];
                    if (hi < 0) continue;
                    var h = hinges[hi];
                    var deg = Mathf.Abs(h.Theta) * Mathf.Rad2Deg;
                    w[j] = h.Line >= 0 ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 35, deg))
                        : Crumple ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14, 40, deg))
                        : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(35, 70, deg)) * 0.6f;
                }
                rLines[t * 3] = rLines[t * 3 + 1] = rLines[t * 3 + 2] = w;
            }
            // Smooth normals within a panel (each corner: the mean of its panel's triangles round that point).
            Array.Clear(nAcc, 0, nAcc.Length);
            for (int t = 0; t < tCount; t++)
            for (int j = 0; j < 3; j++)
                nAcc[tri[t * 3 + j] * panelCount + triPanel[t]] += normal[t];
            for (int t = 0; t < tCount; t++)
            for (int j = 0; j < 3; j++)
            {
                var nn = nAcc[tri[t * 3 + j] * panelCount + triPanel[t]];
                rNormal[t * 3 + j] = Crumple ? normal[t] : nn.sqrMagnitude > 1e-12f ? nn.normalized : normal[t];
            }
            paperMesh.vertices = rv;
            paperMesh.normals = rNormal;
            paperMesh.SetUVs(1, rWorld);
            paperMesh.SetUVs(3, rLines);
            paperMesh.RecalculateBounds();
            shadowMesh.vertices = sv;
            shadowMesh.RecalculateBounds();
            var zc = 4f * Mathf.Max(FW, FH);
            void Render(int passes)
            {
                cmd.Clear();
                cmd.SetRenderTarget(solidRt);
                cmd.ClearRenderTarget(true, true, Color.clear);
                cmd.SetViewProjectionMatrices(Matrix4x4.Translate(new Vector3(0, 0, -zc)),
                    Matrix4x4.Ortho(c.x - FW / 2f, c.x + FW / 2f, c.y - FH / 2f, c.y + FH / 2f, 1, 2 * zc));
                if ((passes & 1) != 0) cmd.DrawMesh(paperMesh, Matrix4x4.identity, solidMat, 0, 1);
                if ((passes & 2) != 0) cmd.DrawMesh(shadowMesh, Matrix4x4.identity, solidMat, 0, 2);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            // Testing: the texture saved, and the same frame without its shadows beside it.
            if (DumpRt != null && simTime >= DumpAt)
            {
                Render(1);
                SaveRt(DumpRt.Replace(".png", "_noshadow.png"));
                Render(Passes);
                SaveRt(DumpRt);
                DumpRt = null;
            }
            else
                Render(Passes);
            quad.transform.localPosition = new Vector3((c.x - origin.x) / canvasPpu, (c.y - origin.y) / canvasPpu, 0);
            if (quadRenderer == null) quadRenderer = quad.GetComponent<Renderer>();
            // The shadow on the ground follows the paper along it, narrows with it and pales as it rises.
            if (Trace && Crumple && shadow == null && !shadowLogged)
            {
                shadowLogged = true;
                Debug.Log("[Paper] shadow probe: no shadow found");
            }
            if (shadow != null)
            {
                var (mid, width, _) = PacketSolid();
                if (startWidthPx <= 0) startWidthPx = Mathf.Max(1, width);
                float low = float.MaxValue;
                for (int i = 0; i < vCount; i++)
                    if (inked[i]) low = Mathf.Min(low, Vector2.Dot(pos[i], upC));
                var rise = Mathf.Clamp01((low - groundC) / (0.6f * sheetSize));
                var sp = shadow.position;
                shadow.position = new Vector3(Design.World(mid.x, 0).x, sp.y, sp.z);
                // A crumpled lump sits on a small, dark contact shadow (a soft blob as wide as the monster reads as nothing).
                var tight = Crumple ? 0.7f : 1;
                shadow.localScale = new Vector3(shadowWidth * Mathf.Clamp(width / startWidthPx, 0.3f, 1) * (1 - 0.3f * rise), shadowHeight * tight, 1);
                if (shadowSr != null)
                {
                    var col = shadowSr.color;
                    col.a = (Crumple ? Mathf.Max(shadowAlpha, 0.7f) : shadowAlpha) * (1 - 0.6f * rise);
                    shadowSr.color = col;
                    // The figure's shadow sits under the stage's ground art; the lump's is drawn just under the paper.
                    if (Crumple && quadRenderer != null)
                    {
                        shadowSr.sortingLayerID = quadRenderer.sortingLayerID;
                        shadowSr.sortingOrder = quadRenderer.sortingOrder - 1;
                    }
                }
                if (Trace && Crumple && simTime >= 0.5f && !shadowLogged)
                {
                    shadowLogged = true;
                    Debug.Log($"[Paper] shadow probe: sr {(shadowSr != null)}, enabled {(shadowSr != null && shadowSr.enabled)}, active {shadow.gameObject.activeInHierarchy}, " +
                        $"colour {(shadowSr != null ? shadowSr.color.ToString() : "-")}, layer {(shadowSr != null ? shadowSr.sortingLayerName : "-")} order {(shadowSr != null ? shadowSr.sortingOrder : 0)}, " +
                        $"paper order {quad.GetComponent<Renderer>().sortingOrder}, pos {shadow.position}, scale {shadow.lossyScale}, quad pos {quad.transform.position}, width {width:0} start {startWidthPx:0}");
                }
            }
        }

        /// <summary>Testing: the paper's texture to a PNG.</summary>
        void SaveRt(string path)
        {
            var tmp = RenderTexture.GetTemporary(FW, FH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(solidRt, tmp);
            var was = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, FW, FH), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(tmp);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"drakepg: paper texture saved to {path} at {simTime:0.00} s");
        }

        /// <summary>The packet's middle and size in design px (solver path).</summary>
        (Vector2 mid, float width, float height) PacketSolid()
        {
            var c = Centre();
            var eye = Eye * sheetSize;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < vCount; i++)
            {
                var p = Project(pos[i], c, eye);
                mn = Vector2.Min(mn, p);
                mx = Vector2.Max(mx, p);
            }
            var a = CanvasToDesign(mn);
            var b = CanvasToDesign(mx);
            return ((a + b) / 2, Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        Vector2 CanvasToDesign(Vector2 px) => Design.FromWorld(body.transform.TransformPoint(new Vector3((px.x - origin.x) / canvasPpu, (px.y - origin.y) / canvasPpu, 0)));

        void DestroySolid()
        {
            if (solidRt != null)
            {
                solidRt.Release();
                Destroy(solidRt);
            }
            if (solidCanvas != null)
            {
                solidCanvas.Release();
                Destroy(solidCanvas);
            }
            if (solidMat != null) Destroy(solidMat);
            if (showMat != null) Destroy(showMat);
            if (paperMesh != null) Destroy(paperMesh);
            if (shadowMesh != null) Destroy(shadowMesh);
            cmd?.Release();
        }
    }
}
