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

        /// <summary>Which crumple (testing): 1 the folds across planes above (try 1), 2 the sheet folded flat, then given a
        /// coarse relief (try 2, <see cref="CrumpleFlat"/>): it can't pass through itself, and its facets are counted.</summary>
        public static int CrumpleKind = 2;
        /// <summary>Try 2: corners tucked behind (along its longest spread), tapered pleats (the picture breaks into pieces
        /// turned against each other), flaps turned over onto the front (the back shows there), and how many relief
        /// points inside (the facets are the triangles between them and the outline's).</summary>
        public static int CrumpleTucks = 4, CrumplePleats = 2, CrumpleFlaps = 2, CrumpleInner = 8, CrumpleTries = 5;
        /// <summary>Try 2: where tucks and flaps fold (share of its reach that way), the paper back share wanted (a death
        /// re-rolls up to `CrumpleTries` times to land in it).</summary>
        public static Vector2 CrumpleTuckAt = new Vector2(0.3f, 0.5f), CrumpleFlapAt = new Vector2(0.4f, 0.6f), CrumpleBackRange = new Vector2(0.25f, 0.4f);
        /// <summary>Try 2: the relief's dome and random lift (shares of its reach), the rim pulled in, the outline smoothed
        /// (closing radius, share of the picture's height: a furry outline would leave specks), lines along cut edges.</summary>
        public static float CrumpleDome = 0.55f, CrumpleRelief = 0.14f, CrumpleShrink = 0.22f, CrumpleClose = 0.05f, CrumpleEdgeInk = 1, CrumpleStep = 0.1f;
        /// <summary>
        /// Gate B: the crumple's grid, cells across the sheet (coarser than the solver's: the folds cut it where it bends,
        /// and the panels between stay flat); points this near a fold's line (a share of a cell) count as on it; the
        /// roundness (the lump's outline as seen, 4 pi area / perimeter squared) a try must reach before it costs.
        /// </summary>
        public static int CrumpleGrid = 8;
        public static float CrumpleEps = 0.002f, CrumpleRound = 0.9f;
        /// <summary>Gate B: how long a lump may be for its width as seen (the spread of its outline's points, long way over
        /// short way) before a try costs, and how much each step past it costs. Off (0): tried at 1 on 11 deaths, it made
        /// two rounder and two worse (a standing slab, an open V), so only logged.</summary>
        public static float CrumpleLong = 1.25f, CrumpleLongCost = 0f;
        /// <summary>Testing: the fold program checked after every crumple build (logged).</summary>
        public static bool CheckChain;

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
        /// <summary>Testing: the paper's back in this colour (a loud one, to count it in the saved texture).</summary>
        public static Color? BackTest;
        bool dumpedRt;
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
            OutlineId = Shader.PropertyToID("_Outline"), EdgeInkId = Shader.PropertyToID("_EdgeInk");

        /// <summary>
        /// The paper folds on the solver: every crease at once toward its target (seeded mountain or valley, so no two
        /// deaths match), the paper between them bending a little, under gravity, onto the ground; the face panel is
        /// held facing the viewer. Ends once it lies still. `landed` as in <see cref="Fold"/>.
        /// </summary>
        async Task FoldSolid(float groundY, Action<float, float> landed)
        {
            if (Crumple && CrumpleKind == 2 && CrumpleMotion)
            {
                await FoldCrumple(groundY, landed);
                return;
            }
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
            if (Crumple) LastSolid += $", crumple {CrumpleKind}: {crumpleNote}, outline closing {closeMs:0} ms, crumple shape {crumpleMs:0} ms";
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
            MakeCanvas();
            closeMs = crumpleMs = 0;
            if (Crumple && CrumpleKind == 2 && CrumpleClose > 0)
            {
                var cw = System.Diagnostics.Stopwatch.StartNew();
                CloseCanvas();
                closeMs = (float)cw.Elapsed.TotalMilliseconds;
            }
            StartScene();
        }

        /// <summary>The canvas: the living picture with the fight's damage on it, padded round; `sheet` is what the scan reads.</summary>
        void MakeCanvas()
        {
            var k = Mathf.Min(1, Mathf.Max(64, heightPx) / size.y);
            W = Mathf.Max(8, Mathf.RoundToInt(size.x * k));
            H = Mathf.Max(8, Mathf.RoundToInt(size.y * k));
            pad = Mathf.RoundToInt(Mathf.Max(W, H) * 0.3f);
            FW = W + 2 * pad;
            FH = H + 2 * pad;
            canvasK = k;
            mat = new Material(foldShader) { name = "Paper fold" };
            mat.SetVector(CanvasId, new Vector4(FW, FH, 0, 0));
            mat.SetVector(PadId, new Vector4(pad, pad, W, H));
            mat.SetVector(RectId, rect);
            solidCanvas = new RenderTexture(FW, FH, 0, RenderTextureFormat.ARGB32)
            {
                name = "paper canvas", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = true, autoGenerateMips = true,
            };
            solidCanvas.Create();
            BakeDamage();
            sheet = solidCanvas;
        }

        float canvasK;

        /// <summary>The picture with the fight's creases, tears and burns as they are now, onto the canvas (the crumple's
        /// canvas is made as the fight starts and baked again at the death).</summary>
        void BakeDamage()
        {
            mat.SetVector(InfoId, Info);
            for (int i = 0; i < MaxCreases; i++) creaseArray[i] = i < creases.Count ? creases[i] : Vector4.zero;
            mat.SetVectorArray(CreasesId, creaseArray);
            mat.SetVectorArray(TearsId, tears);
            mat.SetVectorArray(HolesId, holes);
            Graphics.Blit(body.sprite.texture, solidCanvas, mat, 2);
        }

        /// <summary>The paper's materials and render texture, the quad that shows it in the figure's place, its shadow.</summary>
        void StartScene()
        {
            PrepareScene();
            ShowScene();
        }

        /// <summary>The materials, the render texture, the quad (hidden), the meshes and the command buffer: made once,
        /// before anything is shown (the crumple makes them as the fight starts).</summary>
        void PrepareScene()
        {
            if (solidMat != null) return;
            var k = canvasK;
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

            canvasPpu = ppu * k;
            origin = pivot * k + new Vector2(pad, pad);
            quad = new GameObject("paper fold");
            quad.SetActive(false);
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
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            paperMesh = new Mesh { name = "paper fold" };
            paperMesh.MarkDynamic();
            shadowMesh = new Mesh { name = "paper fold" };
            shadowMesh.MarkDynamic();
            cmd = new CommandBuffer { name = "paper fold" };
        }

        /// <summary>The paper in the figure's place: its colour and order as they are now, the figure hidden, its shadow found.</summary>
        void ShowScene()
        {
            tint = new Color(body.color.r, body.color.g, body.color.b, 1);
            showMat.SetColor(ColorIdS, tint);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sortingLayerID = body.sortingLayerID;
            mr.sortingOrder = body.sortingOrder;
            quad.SetActive(true);
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
            var s = sheetSize / (Crumple && CrumpleKind == 2 ? CrumpleGrid : GridCells);
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
            if (Crumple && CrumpleKind == 1) CrumpleFolds(srng, s);
            if (Crumple && CrumpleKind == 2)
            {
                var cw = System.Diagnostics.Stopwatch.StartNew();
                CrumpleFlatTries(srng, s);
                BuildChain(shape);
                crumpleMs = (float)cw.Elapsed.TotalMilliseconds;
            }
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

        // --- Crumple, try 2: folded flat, then a coarse relief ---

        string crumpleNote = "";
        float closeMs, crumpleMs;

        /// <summary>
        /// Up to `CrumpleTries` crumples of the same grid (each from the seed's next number); the first whose paper back
        /// share lands in `CrumpleBackRange` is kept, else the nearest.
        /// </summary>
        void CrumpleFlatTries(System.Random srng, float s)
        {
            var f0 = new List<Vector2>(bFlat);
            var t0 = new List<int>(bTri);
            var m0 = new List<int>(bMask);
            float bestScore = float.MaxValue;
            int bestTry = 0;
            CrumpleShape best = null;
            var tries = "";
            // Every try is made (each from the seed's next number) and the best kept: its back share inside the range,
            // its outline as seen round enough (a box or a crystal reads less as a paper ball).
            for (int a = 0; a < Mathf.Max(1, CrumpleTries); a++)
            {
                bFlat = new List<Vector2>(f0);
                bTri = new List<int>(t0);
                bMask = new List<int>(m0);
                var sh = CrumpleFlat(new System.Random(srng.Next()), s);
                flat = bFlat.ToArray();
                tri = bTri.ToArray();
                vCount = flat.Length;
                tCount = tri.Length / 3;
                // Seen as drawn: the look down (`Tilt`) shows more of its top.
                var view = new Vector3[vCount];
                for (int i = 0; i < vCount; i++) view[i] = new Vector3(crumplePos[i].x, crumplePos[i].y - Tilt * crumplePos[i].z, crumplePos[i].z);
                sh.Back = BackShare(view);
                sh.Round = Roundness(view, s, out sh.Long);
                var score = (sh.Back < CrumpleBackRange.x ? CrumpleBackRange.x - sh.Back : sh.Back > CrumpleBackRange.y ? sh.Back - CrumpleBackRange.y : 0)
                    + Mathf.Max(0, CrumpleRound - sh.Round) + CrumpleLongCost * Mathf.Max(0, sh.Long - CrumpleLong);
                tries += $"{(a > 0 ? " " : "")}{sh.Back:P0}/{sh.Round:0.00}/{sh.Long:0.00}";
                if (Trace) Debug.Log($"[Paper] crumple try {a}: back {sh.Back:P0}, round {sh.Round:0.00}, long {sh.Long:0.00}, {sh.Note}");
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTry = a;
                    best = sh;
                }
            }
            bFlat = best.Flat;
            bTri = best.Tri;
            bMask = best.Mask;
            crumplePos = best.Still;
            shape = best;
            crumpleNote = $"try {bestTry} of back/round/long {tries}, back {best.Back:P0}, round {best.Round:0.00}, long {best.Long:0.00}, {best.Note}";
        }

        /// <summary>How round the lump's outline is as seen (`view`, its inked points): 4 pi area / perimeter squared of
        /// their hull (1 a circle, 0.79 a square); `along`: how long it is for its width (the spread of those points the
        /// long way over the short way, 1 for a round lump).</summary>
        float Roundness(Vector3[] view, float s, out float along)
        {
            var pts = new List<Vector2>(vCount);
            var near = Vector2.one * (0.3f * s);
            for (int i = 0; i < vCount; i++)
                if (Count(bSat, bScan, flat[i] - near, flat[i] + near) > 0) pts.Add(view[i]);
            along = 1;
            if (pts.Count < 3) return 0;
            var mean = Vector2.zero;
            foreach (var p in pts) mean += p;
            mean /= pts.Count;
            float sxx = 0, syy = 0, sxy = 0;
            foreach (var p in pts)
            {
                var d = p - mean;
                sxx += d.x * d.x;
                syy += d.y * d.y;
                sxy += d.x * d.y;
            }
            float half = 0.5f * (sxx + syy), gap = Mathf.Sqrt(0.25f * (sxx - syy) * (sxx - syy) + sxy * sxy);
            along = Mathf.Sqrt((half + gap) / Mathf.Max(1e-6f, half - gap));
            var hull = HullIdx(pts);
            float area = 0, per = 0;
            for (int j = 0; j < hull.Count; j++)
            {
                Vector2 a = pts[hull[j]], b = pts[hull[(j + 1) % hull.Count]];
                area += a.x * b.y - b.x * a.y;
                per += (b - a).magnitude;
            }
            return per > 1e-4f ? 4 * Mathf.PI * Mathf.Abs(area / 2) / (per * per) : 0;
        }

        /// <summary>
        /// The sheet folded FLAT first, every fold a half turn about its hinge with the turned layers stacked in order
        /// above (or below) everything: corners tucked behind (straight fold edges in the outline), tapered pleats (the
        /// part beyond comes back over, turned, so the picture breaks into pieces that run different ways), flaps turned
        /// over onto the front (its back shows there). Then the rim pulled in, and a coarse crease network (Delaunay over
        /// the outline's points and `CrumpleInner` inside) lifted as a dome with random heights: one height field added
        /// to every layer, so nothing passes through anything. `crumplePos`: the lump about its middle, posed.
        /// </summary>
        CrumpleShape CrumpleFlat(System.Random srng, float s)
        {
            bPos = new List<Vector3>(bFlat.Count * 4);
            foreach (var f in bFlat) bPos.Add(new Vector3(f.x, f.y, 0));
            var gridN = bFlat.Count;
            bGridN = gridN;
            bBorn.Clear();
            bStrayed = false;
            // The fold program (gate B): each fold, which points it turned, and the stack's depths after it.
            var stages = CrumpleTucks + 2 * CrumplePleats + CrumpleFlaps;
            bFolds = new List<FoldRec>();
            bMoves = new List<long>(bFlat.Count * 4);
            for (int i = 0; i < bFlat.Count; i++) bMoves.Add(0);
            bZ = new List<float>[stages + 1];
            for (int k = 0; k <= stages; k++)
            {
                bZ[k] = new List<float>(bFlat.Count * 4);
                for (int i = 0; i < bFlat.Count; i++) bZ[k].Add(0);
            }
            float U(float a, float b) => a + (b - a) * (float)srng.NextDouble();
            Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            // Only points right on a fold's line count as on it: one left a little off it would stay put while its
            // neighbours turn, and the paper there would stretch as the fold turns (gate B animates the folds).
            var eps = CrumpleEps * s;
            int tucks = 0, pleats = 0, flaps = 0;
            // 1. Corners tucked behind, along its longest spread (either end, a little off), so tall or wide sheets ball up.
            for (int k = 0; k < CrumpleTucks; k++)
            {
                var c = Mean2();
                var a0 = MainAngle2(c) + (srng.NextDouble() < 0.5 ? Mathf.PI : 0);
                var n = Dir(a0 + U(-35, 35) * Mathf.Deg2Rad);
                FlatFold(c + n * (U(CrumpleTuckAt.x, CrumpleTuckAt.y) * Reach(c, n)), n, eps, false, null);
                tucks++;
            }
            // 2. Tapered pleats: folded over, then the part beyond folded back (turned twice the taper about where the
            // two creases meet, off the sheet); the strip between hides under it.
            for (int k = 0; k < CrumplePleats; k++)
            {
                var c = Mean2();
                var a = U(0, 2 * Mathf.PI);
                var n = Dir(a);
                var r = Reach(c, n);
                var c1 = c + n * (U(-0.1f, 0.25f) * r);
                var along = new Vector2(-n.y, n.x);
                var alpha = U(8, 18) * Mathf.Deg2Rad;
                var wid = U(0.08f, 0.16f) * r;
                var side = srng.NextDouble() < 0.5 ? -1 : 1;
                // The creases meet off the paper (folded flat, two creases meeting on it would have to tear it there, and
                // the second fold would drag the first crease's points out of the paper beside it): at least as far
                // along the first crease as the paper reaches on that side; the taper eases to fit.
                float far = 0;
                foreach (var p in bPos) far = Mathf.Max(far, side * Vector2.Dot((Vector2)p - c1, along));
                var dist = Mathf.Max(wid / Mathf.Tan(alpha), far + s);
                alpha = Mathf.Atan(wid / dist);
                var q = c1 + along * (side * dist);
                var sgn = Vector2.Dot(c1 - q, along) > 0 ? 1 : -1;
                var n2 = Dir(a + sgn * alpha);
                if (Vector2.Dot(c1 - q, n2) > 0) n2 = Dir(a - sgn * alpha);
                var moved = FlatFold(c1, n, eps, true, null);
                var n2m = n2 - 2 * Vector2.Dot(n2, n) * n;
                FlatFold(q, n2m, eps, true, moved);
                pleats++;
            }
            // 3. Flaps turned over onto the front.
            for (int k = 0; k < CrumpleFlaps; k++)
            {
                var c = Mean2();
                var n = Dir(U(0, 2 * Mathf.PI));
                FlatFold(c + n * (U(CrumpleFlapAt.x, CrumpleFlapAt.y) * Reach(c, n)), n, eps, true, null);
                flaps++;
            }
            // The stack's depth doubles with each fold (as a stack does); only the layers' order is kept: each distinct
            // depth becomes the next small step (one increasing map for every point, so the order holds). The same for
            // the depths after each earlier fold (the folds turning one by one pass through those).
            var levelCount = 0;
            for (int k = 1; k < bZ.Length; k++)
            {
                var lc = Compress(bZ[k]);
                if (k == bFolds.Count) levelCount = lc;
            }
            for (int i = 0; i < bPos.Count; i++) bPos[i] = new Vector3(bPos[i].x, bPos[i].y, bZ[Mathf.Min(bFolds.Count, bZ.Length - 1)][i]);
            var foldedN = bPos.Count;
            var foldedXY = new Vector2[foldedN];
            for (int i = 0; i < foldedN; i++) foldedXY[i] = bPos[i];
            // 4. The rim pulled in while still flat (monotonic in the distance, so nothing crosses; before the relief,
            // so stacked layers stay parallel).
            {
                var hull = Hull2();
                var c0 = Vector2.zero;
                foreach (var h in hull) c0 += h;
                c0 /= Mathf.Max(1, hull.Count);
                float r0 = 1e-3f;
                foreach (var p in bPos) r0 = Mathf.Max(r0, ((Vector2)p - c0).magnitude);
                for (int i = 0; i < bPos.Count; i++)
                {
                    var p = bPos[i];
                    var d = (Vector2)p - c0;
                    var rr = d.magnitude / r0;
                    var q = c0 + d * (1 - CrumpleShrink * rr * rr);
                    bPos[i] = new Vector3(q.x, q.y, p.z);
                }
            }
            // 5. The relief: control points on the outline (about one per 0.45 of its reach) and inside (darts, kept
            // apart), a Delaunay net over them, a height for each (a dome plus a random lift), the sheet cut along every
            // net edge (crisp creases) and lifted by the net's height field.
            var hp = Hull2();
            var cen = Vector2.zero;
            foreach (var h in hp) cen += h;
            cen /= Mathf.Max(1, hp.Count);
            float R = 1e-3f;
            foreach (var p in bPos) R = Mathf.Max(R, ((Vector2)p - cen).magnitude);
            var ctrl = new List<Vector2>();
            foreach (var h in hp)
                if (ctrl.Count == 0 || (h - ctrl[ctrl.Count - 1]).magnitude > 0.45f * R) ctrl.Add(h);
            var hullN = ctrl.Count;
            var minD = R * Mathf.Sqrt(Mathf.PI / Mathf.Max(1, CrumpleInner)) * 0.4f;
            for (int tries = 0, got = 0; got < CrumpleInner && tries < 4000; tries++)
            {
                var p = cen + new Vector2(U(-R, R), U(-R, R));
                if (!InConvex(hp, p)) continue;
                var ok = true;
                foreach (var q in ctrl)
                    if ((p - q).sqrMagnitude < minD * minD) { ok = false; break; }
                if (!ok) continue;
                ctrl.Add(p);
                got++;
            }
            var net = Delaunay(ctrl);
            var hz = new float[ctrl.Count];
            for (int i = 0; i < ctrl.Count; i++)
            {
                var rr = (ctrl[i] - cen).magnitude / R;
                hz[i] = CrumpleDome * R * (1 - rr * rr) + U(-CrumpleRelief, CrumpleRelief) * R;
            }
            // A flat skirt round the net, at its rim's heights, out far enough to hold the whole sheet: the paper
            // beyond the rim points takes its height from inside a facet too (the same plane for every layer there),
            // so no layer pokes through the one above it, and the net inside stays as it was.
            var facets = net.Count / 3;
            float skirtK = 1;
            {
                var outer = HullIdx(ctrl);
                var n0 = ctrl.Count;
                for (int j = 0; j < outer.Count; j++)
                {
                    Vector2 a = ctrl[outer[j]], b = ctrl[outer[(j + 1) % outer.Count]];
                    var e = b - a;
                    var nOut = new Vector2(e.y, -e.x).normalized;
                    var h0 = Vector2.Dot(nOut, a - cen);
                    if (h0 <= 1e-4f) continue;
                    foreach (var p in hp) skirtK = Mathf.Max(skirtK, Vector2.Dot(nOut, p - cen) / h0);
                }
                skirtK = Mathf.Min(skirtK * 1.02f, 4);
                var hzl = new List<float>(hz);
                for (int j = 0; j < outer.Count; j++)
                {
                    ctrl.Add(cen + (ctrl[outer[j]] - cen) * skirtK);
                    hzl.Add(hz[outer[j]]);
                }
                for (int j = 0; j < outer.Count; j++)
                {
                    int a = outer[j], b = outer[(j + 1) % outer.Count], sa = n0 + j, sb = n0 + (j + 1) % outer.Count;
                    net.Add(a); net.Add(b); net.Add(sb);
                    net.Add(a); net.Add(sb); net.Add(sa);
                }
                hz = hzl.ToArray();
            }
            var netEdges = new HashSet<long>();
            for (int t = 0; t < net.Count; t += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = net[t + j], b = net[t + (j + 1) % 3];
                    netEdges.Add(Math.Min(a, b) * 100000L + Math.Max(a, b));
                }
            foreach (var e in netEdges)
            {
                Vector2 p0 = ctrl[(int)(e / 100000L)], p1 = ctrl[(int)(e % 100000L)];
                var u = p1 - p0;
                if (u.sqrMagnitude < 1e-6f) continue;
                var nn = new Vector2(-u.y, u.x).normalized;
                // A tiny snap only: a point left just off a crease would bend its triangle across it, out of plane.
                SplitLine(p0, nn, 0.002f * s, null, true, p0, p1, out _);
            }
            var relief = new float[bPos.Count];
            for (int i = 0; i < bPos.Count; i++)
            {
                var p = (Vector2)bPos[i];
                int best = 0;
                Vector3 bb = Vector3.zero;
                float bestMin = float.MinValue;
                for (int t = 0; t < net.Count; t += 3)
                {
                    var b = Bary(ctrl[net[t]], ctrl[net[t + 1]], ctrl[net[t + 2]], p);
                    var m = Mathf.Min(b.x, Mathf.Min(b.y, b.z));
                    if (m > bestMin)
                    {
                        bestMin = m;
                        best = t;
                        bb = b;
                    }
                }
                bb = Vector3.Max(bb, Vector3.zero);
                bb /= Mathf.Max(1e-6f, bb.x + bb.y + bb.z);
                var add = bb.x * hz[net[best]] + bb.y * hz[net[best + 1]] + bb.z * hz[net[best + 2]];
                relief[i] = add;
                bPos[i] += new Vector3(0, 0, add);
            }
            // 6. Posed: about its middle, leaning back a little, turned a little.
            var tb = U(10, 25) * Mathf.Deg2Rad;
            var rl = U(-25, 25) * Mathf.Deg2Rad;
            var yw = U(-20, 20) * Mathf.Deg2Rad;
            crumplePos = new Vector3[bPos.Count];
            for (int i = 0; i < bPos.Count; i++) crumplePos[i] = PoseRot(bPos[i] - new Vector3(cen.x, cen.y, 0), rl, tb, yw);
            var note = $"{tucks} tucks, {pleats} pleats, {flaps} flaps, {levelCount} depths, {ctrl.Count} relief points ({hullN} on the outline, skirt to x{skirtK:0.00}), {facets} facets";
            var sh = new CrumpleShape
            {
                Flat = bFlat, Tri = bTri, Mask = bMask, Folds = bFolds, Moves = bMoves, Z = bZ, Final = bPos.ToArray(), Relief = relief, Cen = cen,
                Roll = rl, Lean = tb, Turn = yw, Still = crumplePos, FoldedXY = foldedXY, GridN = gridN, Note = note,
            };
            bMoves = null;
            bZ = null;
            bFolds = null;
            crumpleNote = note;
            return sh;
        }

        /// <summary>The crumple's pose: about z (roll), then x (lean back), then y (turn); z toward the viewer.</summary>
        static Vector3 PoseRot(Vector3 p, float rl, float tb, float yw)
        {
            p = new Vector3(Mathf.Cos(rl) * p.x - Mathf.Sin(rl) * p.y, Mathf.Sin(rl) * p.x + Mathf.Cos(rl) * p.y, p.z);
            p = new Vector3(p.x, Mathf.Cos(tb) * p.y - Mathf.Sin(tb) * p.z, Mathf.Sin(tb) * p.y + Mathf.Cos(tb) * p.z);
            return new Vector3(Mathf.Cos(yw) * p.x + Mathf.Sin(yw) * p.z, p.y, -Mathf.Sin(yw) * p.x + Mathf.Cos(yw) * p.z);
        }

        /// <summary>The stack's depths as steps: each distinct depth the next `CrumpleStep` up, the flat sheet's level at 0
        /// (one increasing map, so the order holds). Returns how many depths.</summary>
        static int Compress(List<float> z)
        {
            var levels = new SortedSet<float>();
            foreach (var v in z) levels.Add(Mathf.Round(v * 1e4f) / 1e4f);
            var rank = new Dictionary<float, int>();
            int ri = 0, zero = 0;
            foreach (var l in levels)
            {
                if (l <= 0) zero = ri;
                rank[l] = ri++;
            }
            for (int i = 0; i < z.Count; i++) z[i] = (rank[Mathf.Round(z[i] * 1e4f) / 1e4f] - zero) * CrumpleStep;
            return levels.Count;
        }

        // --- Crumple, gate B: the fold program, played as hinges turning ---

        /// <summary>One fold as it was made: its line (a point, and the normal toward the side that turned, in the sheet
        /// as folded so far) and which way it turned (up: over the front, the paper's back showing).</summary>
        struct FoldRec
        {
            public Vector2 C, N;
            public bool Up;
        }

        /// <summary>
        /// A crumple as built: the flat sheet and its triangles; the folds; per point, which folds turned it (bit k) and
        /// its depth in the stack after each fold (`Z[k]`, stage 0 the flat sheet); where the folds, the rim pull and the
        /// relief leave it (`Final`, about nothing: canvas px) and the relief's share of that; the pose; the still.
        /// </summary>
        class CrumpleShape
        {
            public List<Vector2> Flat;
            public List<int> Tri, Mask;
            public List<FoldRec> Folds;
            public List<long> Moves;
            public List<float>[] Z;
            public Vector3[] Final, Still;
            public float[] Relief;
            /// <summary>Testing: each point's place after the folds (the points there then).</summary>
            public Vector2[] FoldedXY;
            public int GridN;
            public Vector2 Cen;
            public float Roll, Lean, Turn, Back, Round, Long;
            public string Note;
        }

        CrumpleShape shape;
        /// <summary>Testing: for each point cut in, the fold then and its two ends (the grid's points first: `bGridN`).</summary>
        List<Vector3Int> bBorn = new List<Vector3Int>();
        int bGridN;
        bool bStrayed;
        string Bits(long m)
        {
            var b = "";
            for (int k = 0; k < 10; k++) b += (m & (1L << k)) != 0 ? "1" : "0";
            return b;
        }
        List<long> bMoves;
        List<float>[] bZ;
        List<FoldRec> bFolds;

        /// <summary>A hinge of a point's chain: which fold, a point on its line and its direction, in the flat sheet.</summary>
        struct ChainHinge
        {
            public int Fold;
            public Vector3 A, U;
        }

        /// <summary>Per group of points turned by the same folds, their hinges (in fold order); each point's group; each
        /// point's depth gained with each fold (`chainK` a point).</summary>
        ChainHinge[][] chainGroups;
        int[] chainGroup;
        float[] chainDz;
        int chainK;
        Quaternion[] chainQ;
        Vector3[] chainT;

        /// <summary>
        /// The fold program as hinges: a point turned by folds k1 &lt; k2 &lt; ... turns about each one's line taken back
        /// into the flat sheet through the folds before it (each a mirror across its line on the flat: a half turn about a
        /// line in the sheet). Played last fold first, each about its own line, any angles keep every length; all at a
        /// half turn they give the folded sheet.
        /// </summary>
        void BuildChain(CrumpleShape sh)
        {
            var K = sh.Folds.Count;
            chainK = K;
            var ids = new Dictionary<long, int>();
            var groups = new List<ChainHinge[]>();
            var n = sh.Flat.Count;
            chainGroup = new int[n];
            var list = new List<ChainHinge>();
            for (int v = 0; v < n; v++)
            {
                var m = sh.Moves[v];
                if (!ids.TryGetValue(m, out var g))
                {
                    ids[m] = g = groups.Count;
                    list.Clear();
                    for (int k = 0; k < K; k++)
                    {
                        if ((m & (1L << k)) == 0) continue;
                        var f = sh.Folds[k];
                        Vector2 c = f.C, d = new Vector2(-f.N.y, f.N.x);
                        for (int j = list.Count - 1; j >= 0; j--)
                        {
                            var fj = sh.Folds[list[j].Fold];
                            c -= 2 * Vector2.Dot(c - fj.C, fj.N) * fj.N;
                            d -= 2 * Vector2.Dot(d, fj.N) * fj.N;
                        }
                        list.Add(new ChainHinge { Fold = k, A = c, U = new Vector3(d.x, d.y, 0).normalized });
                    }
                    groups.Add(list.ToArray());
                }
                chainGroup[v] = g;
            }
            chainGroups = groups.ToArray();
            chainQ = new Quaternion[chainGroups.Length];
            chainT = new Vector3[chainGroups.Length];
            chainDz = new float[n * Mathf.Max(1, K)];
            for (int v = 0; v < n; v++)
                for (int k = 0; k < K; k++)
                    chainDz[v * K + k] = sh.Z[k + 1][v] - sh.Z[k][v];
            // Which folds must wait for which: where two points on one edge were both turned by fold k but only one of them
            // by an earlier fold j (the edge crosses j's line inside k's flap), k turns layers on both sides of j, which
            // lie in one plane only once j is down; before that, turning them together would stretch the paper.
            chainWait = new long[K];
            var tri = sh.Tri;
            for (int t = 0; t < tri.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    long ma = sh.Moves[tri[t + e]], mb = sh.Moves[tri[t + (e + 1) % 3]];
                    long diff = ma ^ mb, both = ma & mb;
                    if (diff == 0 || both == 0) continue;
                    for (int k = 0; k < K; k++)
                    {
                        if ((both & (1L << k)) == 0) continue;
                        chainWait[k] |= diff & ((1L << k) - 1);
                    }
                }
        }

        /// <summary>Per fold, the earlier folds (bits) that must be down before it starts turning.</summary>
        long[] chainWait;

        /// <summary>The waits for the log: "3<1,2" (fold 3 after 1 and 2).</summary>
        string WaitNote()
        {
            var w = "";
            for (int k = 0; k < chainK; k++)
            {
                if (chainWait[k] == 0) continue;
                w += (w.Length > 0 ? " " : "") + k + "<";
                var first = true;
                for (int j = 0; j < k; j++)
                    if ((chainWait[k] & (1L << j)) != 0)
                    {
                        w += (first ? "" : ",") + j;
                        first = false;
                    }
            }
            return w.Length > 0 ? w : "none";
        }

        /// <summary>
        /// The sheet with fold k turned `ang[k]` (radians: 0 flat, pi folded; up folds turn toward the viewer) and its
        /// stack's depth `prog[k]` of the way there (null: none), into `outPos` (canvas px).
        /// </summary>
        void ChainPose(float[] ang, float[] prog, Vector3[] outPos)
        {
            var folds = shape.Folds;
            for (int g = 0; g < chainGroups.Length; g++)
            {
                var q = Quaternion.identity;
                var t = Vector3.zero;
                var hs = chainGroups[g];
                for (int j = hs.Length - 1; j >= 0; j--)
                {
                    var h = hs[j];
                    var phi = (folds[h.Fold].Up ? -1 : 1) * ang[h.Fold];
                    var r = Quaternion.AngleAxis(phi * Mathf.Rad2Deg, h.U);
                    // About the line through A: p' = r (p - A) + A, after what came before.
                    q = r * q;
                    t = r * t + (h.A - r * h.A);
                }
                chainQ[g] = q;
                chainT[g] = t;
            }
            var flatL = shape.Flat;
            var K = chainK;
            for (int v = 0; v < outPos.Length; v++)
            {
                var g = chainGroup[v];
                var p = chainQ[g] * new Vector3(flatL[v].x, flatL[v].y, 0) + chainT[g];
                if (prog != null)
                    for (int k = 0; k < K; k++) p.z += prog[k] * chainDz[v * K + k];
                outPos[v] = p;
            }
        }

        /// <summary>
        /// Testing (`CheckChain`): the fold program against the build. All folds at a half turn: the folded sheet; at
        /// random angles: every edge its flat length; each fold alone part way: up folds toward the viewer; the folds,
        /// the squeeze and the pose all the way: the still. Logged, in cells.
        /// </summary>
        void CheckChainNow(float s)
        {
            var n = shape.Flat.Count;
            var K = chainK;
            var p = new Vector3[n];
            var ang = new float[K];
            var prog = new float[K];
            for (int k = 0; k < K; k++)
            {
                ang[k] = Mathf.PI;
                prog[k] = 1;
            }
            ChainPose(ang, null, p);
            float foldedErr = 0, foldedZ = 0;
            int worst = 0, off = 0;
            for (int v = 0; v < shape.FoldedXY.Length; v++)
            {
                var e = ((Vector2)p[v] - shape.FoldedXY[v]).magnitude;
                if (e > 0.01f * s) off++;
                if (e > foldedErr)
                {
                    foldedErr = e;
                    worst = v;
                }
                foldedZ = Mathf.Max(foldedZ, Mathf.Abs(p[v].z));
            }
            if (foldedErr > 0.01f * s)
            {
                // The worst point's mirrors replayed from its flat place, fold by fold (its bits): the bits wrong, or the chain?
                Vector2 x = shape.Flat[worst];
                var bits = "";
                for (int k = 0; k < K; k++)
                {
                    var on = (shape.Moves[worst] & (1L << k)) != 0;
                    bits += on ? "1" : "0";
                    if (on) x -= 2 * Vector2.Dot(x - shape.Folds[k].C, shape.Folds[k].N) * shape.Folds[k].N;
                }
                Debug.Log($"drakepg paper chain check: {off} points off; the worst #{worst} (of {shape.GridN} grid points), folds {bits}, flat {shape.Flat[worst]}, "
                    + $"built {shape.FoldedXY[worst]}, mirrors replayed {x}, chain {(Vector2)p[worst]}");
            }
            var rnd = new System.Random(7);
            float stretch = 0;
            for (int trial = 0; trial < 20; trial++)
            {
                // Random angles that keep the waits: a fold part way (or flat) holds every fold waiting on it flat.
                for (int k = 0; k < K; k++)
                {
                    var r = rnd.NextDouble();
                    ang[k] = r < 0.4 ? Mathf.PI : (float)rnd.NextDouble() * Mathf.PI;
                    for (int j = 0; j < k; j++)
                        if ((chainWait[k] & (1L << j)) != 0 && ang[j] < Mathf.PI) ang[k] = 0;
                }
                ChainPose(ang, null, p);
                for (int t = 0; t < shape.Tri.Count; t += 3)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = shape.Tri[t + j], b = shape.Tri[t + (j + 1) % 3];
                        stretch = Mathf.Max(stretch, Mathf.Abs((p[a] - p[b]).magnitude - (shape.Flat[a] - shape.Flat[b]).magnitude));
                    }
            }
            var wrongWay = 0;
            for (int k = 0; k < K; k++)
            {
                for (int j = 0; j < K; j++) ang[j] = j < k ? Mathf.PI : j == k ? Mathf.PI / 2 : 0;
                ChainPose(ang, null, p);
                float zs = 0;
                var bit = 1L << k;
                for (int v = 0; v < n; v++)
                    if ((shape.Moves[v] & bit) != 0) zs += p[v].z;
                if (zs * (shape.Folds[k].Up ? 1 : -1) < 0) wrongWay++;
            }
            for (int k = 0; k < K; k++) ang[k] = Mathf.PI;
            ChainPose(ang, prog, p);
            float stillErr = 0;
            for (int v = 0; v < n; v++)
            {
                // The squeeze all the way: the rim pull (xy) and the relief (z) added, then posed about the middle.
                var q = new Vector3(shape.Final[v].x, shape.Final[v].y, p[v].z + shape.Relief[v]);
                var posed = PoseRot(q - new Vector3(shape.Cen.x, shape.Cen.y, 0), shape.Roll, shape.Lean, shape.Turn);
                stillErr = Mathf.Max(stillErr, (posed - shape.Still[v]).magnitude);
            }
            Debug.Log($"drakepg paper chain check: {n} points, {chainGroups.Length} groups, {K} folds; folded: off by {foldedErr / s:0.0000} cells (z {foldedZ / s:0.0000}); "
                + $"part way: edges off by up to {stretch / s:0.0000} cells; folds turning the wrong way: {wrongWay}; the still: off by {stillErr / s:0.0000} cells; waits {WaitNote()}");
        }

        Vector2 Mean2()
        {
            var c = Vector2.zero;
            foreach (var p in bPos) c += (Vector2)p;
            return c / Mathf.Max(1, bPos.Count);
        }

        /// <summary>How far the sheet reaches from `c` along `n`.</summary>
        float Reach(Vector2 c, Vector2 n)
        {
            float r = 0;
            foreach (var p in bPos) r = Mathf.Max(r, Vector2.Dot((Vector2)p - c, n));
            return r;
        }

        /// <summary>The angle of its longest spread (on the flat).</summary>
        float MainAngle2(Vector2 c)
        {
            float xx = 0, xy = 0, yy = 0;
            foreach (var p in bPos)
            {
                var d = (Vector2)p - c;
                xx += d.x * d.x;
                xy += d.x * d.y;
                yy += d.y * d.y;
            }
            return 0.5f * Mathf.Atan2(2 * xy, xx - yy);
        }

        /// <summary>The convex hull of the points (on the flat), anticlockwise.</summary>
        List<Vector2> Hull2()
        {
            var pts = new List<Vector2>(bPos.Count);
            foreach (var p in bPos) pts.Add(p);
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (int pass = 0; pass < 2; pass++)
            {
                var start = h.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[pass == 0 ? i : pts.Count - 1 - i];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h;
        }

        /// <summary>The convex hull of the points as their indices, anticlockwise.</summary>
        static List<int> HullIdx(List<Vector2> pts)
        {
            var idx = new List<int>(pts.Count);
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            idx.Sort((a, b) => pts[a].x != pts[b].x ? pts[a].x.CompareTo(pts[b].x) : pts[a].y.CompareTo(pts[b].y));
            var h = new List<int>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (int pass = 0; pass < 2; pass++)
            {
                var start = h.Count;
                for (int i = 0; i < idx.Count; i++)
                {
                    var p = idx[pass == 0 ? i : idx.Count - 1 - i];
                    while (h.Count >= start + 2 && Cross(pts[h[h.Count - 2]], pts[h[h.Count - 1]], pts[p]) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h;
        }

        static bool InConvex(List<Vector2> hull, Vector2 p)
        {
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
                if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) < 0) return false;
            }
            return true;
        }

        static Vector3 Bary(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            var den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(den) < 1e-9f) return new Vector3(-1, -1, -1);
            var l0 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / den;
            var l1 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / den;
            return new Vector3(l0, l1, 1 - l0 - l1);
        }

        /// <summary>A Delaunay triangulation (Bowyer-Watson; a few dozen points): triangles as index triples.</summary>
        static List<int> Delaunay(List<Vector2> pts)
        {
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in pts)
            {
                mn = Vector2.Min(mn, p);
                mx = Vector2.Max(mx, p);
            }
            var span = Mathf.Max(mx.x - mn.x, mx.y - mn.y) + 1;
            var mid = (mn + mx) / 2;
            var all = new List<Vector2>(pts)
            {
                mid + new Vector2(-20 * span, -20 * span), mid + new Vector2(20 * span, -20 * span), mid + new Vector2(0, 20 * span),
            };
            int n = pts.Count;
            var tris = new List<int[]> { new[] { n, n + 1, n + 2 } };
            bool InCircle(int[] t, Vector2 p)
            {
                Vector2 a = all[t[0]] - p, b = all[t[1]] - p, c = all[t[2]] - p;
                var det = (a.x * a.x + a.y * a.y) * (b.x * c.y - c.x * b.y) - (b.x * b.x + b.y * b.y) * (a.x * c.y - c.x * a.y)
                    + (c.x * c.x + c.y * c.y) * (a.x * b.y - b.x * a.y);
                var orient = (all[t[1]].x - all[t[0]].x) * (all[t[2]].y - all[t[0]].y) - (all[t[2]].x - all[t[0]].x) * (all[t[1]].y - all[t[0]].y);
                return orient > 0 ? det > 0 : det < 0;
            }
            for (int i = 0; i < n; i++)
            {
                var p = all[i];
                var bad = tris.FindAll(t => InCircle(t, p));
                var edges = new Dictionary<long, (int, int)>();
                foreach (var t in bad)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = t[j], b = t[(j + 1) % 3];
                        var key = Math.Min(a, b) * 100000L + Math.Max(a, b);
                        if (edges.ContainsKey(key)) edges.Remove(key);
                        else edges[key] = (a, b);
                    }
                tris.RemoveAll(t => bad.Contains(t));
                foreach (var e in edges.Values) tris.Add(new[] { e.Item1, e.Item2, i });
            }
            var outT = new List<int>();
            foreach (var t in tris)
                if (t[0] < n && t[1] < n && t[2] < n) outT.AddRange(t);
            return outT;
        }

        /// <summary>
        /// A half turn of the part on `n`'s side of the line through `c` about it (lengths kept): onto the top (`up`, its
        /// back shows) or under; the turned layers stack in reverse order above (or below) everything. Only the triangles
        /// in `sel` turn when it is given. Returns the turned triangles.
        /// </summary>
        HashSet<int> FlatFold(Vector2 c, Vector2 n, float eps, bool up, HashSet<int> sel)
        {
            var d = SplitLine(c, n, eps, sel, false, default, default, out var flags);
            float zmax = float.MinValue, zmin = float.MaxValue;
            foreach (var p in bPos)
            {
                zmax = Mathf.Max(zmax, p.z);
                zmin = Mathf.Min(zmin, p.z);
            }
            var h = up ? zmax + 0.3f : zmin - 0.3f;
            var movedT = new HashSet<int>();
            var mv = new HashSet<int>();
            for (int t = 0; t < bTri.Count / 3; t++)
            {
                if (!flags[t]) continue;
                int i0 = bTri[t * 3], i1 = bTri[t * 3 + 1], i2 = bTri[t * 3 + 2];
                if (Mathf.Max(d[i0], Mathf.Max(d[i1], d[i2])) <= 0) continue;
                movedT.Add(t);
                if (d[i0] > 0) mv.Add(i0);
                if (d[i1] > 0) mv.Add(i1);
                if (d[i2] > 0) mv.Add(i2);
            }
            foreach (var v in mv)
            {
                var p = bPos[v];
                bPos[v] = new Vector3(p.x - 2 * d[v] * n.x, p.y - 2 * d[v] * n.y, 2 * h - p.z);
            }
            // The fold program: this fold, the points it turned, and every point's depth after it.
            if (bMoves != null && bFolds.Count < 63)
            {
                var bit = 1L << bFolds.Count;
                bFolds.Add(new FoldRec { C = c, N = n, Up = up });
                foreach (var v in mv) bMoves[v] |= bit;
                if (bFolds.Count < bZ.Length)
                {
                    var zl = bZ[bFolds.Count];
                    for (int i = 0; i < bPos.Count; i++) zl[i] = bPos[i].z;
                }
                // Testing: every point's mirrors replayed from its flat place; the first that strays, and how it was made.
                if (CheckChain && !bStrayed)
                    for (int i = 0; i < bPos.Count; i++)
                    {
                        Vector2 x = bFlat[i];
                        for (int k = 0; k < bFolds.Count; k++)
                            if ((bMoves[i] & (1L << k)) != 0) x -= 2 * Vector2.Dot(x - bFolds[k].C, bFolds[k].N) * bFolds[k].N;
                        var off = (x - (Vector2)bPos[i]).magnitude;
                        if (off < 5 * eps) continue;
                        bStrayed = true;
                        var born = i < bGridN ? "a grid point" : $"cut in fold {bBorn[i - bGridN].x} between #{bBorn[i - bGridN].y} (bits {Bits(bMoves[bBorn[i - bGridN].y])}) and #{bBorn[i - bGridN].z} (bits {Bits(bMoves[bBorn[i - bGridN].z])})";
                        var inSel = 0;
                        var inMoved = 0;
                        for (int t = 0; t < bTri.Count / 3; t++)
                            if (bTri[t * 3] == i || bTri[t * 3 + 1] == i || bTri[t * 3 + 2] == i)
                            {
                                if (flags[t]) inSel++;
                                if (movedT.Contains(t)) inMoved++;
                            }
                        Debug.Log($"drakepg paper chain check: after fold {bFolds.Count - 1} ({(sel == null ? "all" : "a pleat's second")}), #{i} strays {off:0.00} px: bits {Bits(bMoves[i])}, "
                            + $"d {d[i]:0.000}, in {inSel} selected and {inMoved} turned triangles, {born}");
                        break;
                    }
            }
            return movedT;
        }

        /// <summary>
        /// Cut the triangles (all, or those in `sel`) that the line through `c` (normal `n`, on the flat) crosses; with
        /// `seg`, only those reaching the stretch `p0`-`p1`. Points within `eps` count as on it. Returns each point's
        /// signed distance; `flags`: per triangle after the cut, whether it was selected.
        /// </summary>
        List<float> SplitLine(Vector2 c, Vector2 n, float eps, HashSet<int> sel, bool seg, Vector2 p0, Vector2 p1, out List<bool> flags)
        {
            var d = new List<float>(bPos.Count + 64);
            foreach (var p in bPos)
            {
                var x = Vector2.Dot((Vector2)p - c, n);
                d.Add(Mathf.Abs(x) < eps ? 0 : x);
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
                    // The fold program: a new point was turned by whatever turned either end (one end can lie on an
                    // earlier fold's line), and lay between them in the stack at every stage.
                    if (bMoves != null)
                    {
                        bMoves.Add(bMoves[a] | bMoves[b]);
                        foreach (var zl in bZ) zl.Add(zl[a] + (zl[b] - zl[a]) * u);
                        if (CheckChain) bBorn.Add(new Vector3Int(bFolds.Count, a, b));
                    }
                    cache[key] = m;
                }
                return m;
            }
            var su = p1 - p0;
            var sl = Mathf.Max(1e-6f, su.sqrMagnitude);
            var outTri = new List<int>(bTri.Count + 256);
            flags = new List<bool>(bTri.Count / 3 + 64);
            var poly = new List<int>(5);
            for (int t = 0; t < bTri.Count; t += 3)
            {
                var s = sel == null || sel.Contains(t / 3);
                float d0 = d[bTri[t]], d1 = d[bTri[t + 1]], d2 = d[bTri[t + 2]];
                var keep = !s || Mathf.Min(d0, Mathf.Min(d1, d2)) >= 0 || Mathf.Max(d0, Mathf.Max(d1, d2)) <= 0;
                if (!keep && seg)
                {
                    float t0 = Vector2.Dot((Vector2)bPos[bTri[t]] - p0, su) / sl, t1 = Vector2.Dot((Vector2)bPos[bTri[t + 1]] - p0, su) / sl,
                        t2 = Vector2.Dot((Vector2)bPos[bTri[t + 2]] - p0, su) / sl;
                    if (Mathf.Max(t0, Mathf.Max(t1, t2)) < -0.02f || Mathf.Min(t0, Mathf.Min(t1, t2)) > 1.02f) keep = true;
                }
                if (keep)
                {
                    outTri.Add(bTri[t]);
                    outTri.Add(bTri[t + 1]);
                    outTri.Add(bTri[t + 2]);
                    flags.Add(s);
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
                        flags.Add(s);
                    }
                }
            }
            bTri = outTri;
            return d;
        }

        /// <summary>
        /// Try 2: the canvas's outline smoothed (a closing: grown, then shrunk, by `CrumpleClose` of the picture's
        /// height; chamfer distances), the new pixels taking the colour of the picture next to them, so a furry outline
        /// folded inside the lump doesn't leave specks of the layer under it.
        /// </summary>
        void CloseCanvas()
        {
            int w = FW, h = FH;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = solidCanvas;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = was;
            var px = tex.GetPixels32();
            CloseAdd(px, w, h, H);
            tex.SetPixels32(px);
            tex.Apply(false);
            Graphics.Blit(tex, solidCanvas);
            Destroy(tex);
        }

        /// <summary>
        /// The closing on the canvas's pixels `px` (changed in place); returns what it adds, premultiplied (clear where it
        /// adds nothing: inside the picture, so a burn made later in the fight stays open). No Unity calls (a worker
        /// thread runs it).
        /// </summary>
        static Color32[] CloseAdd(Color32[] px, int w, int h, int picH)
        {
            var inside = new bool[w * h];
            for (int i = 0; i < inside.Length; i++) inside[i] = px[i].a > 100;
            var r3 = Math.Max(3, (int)Math.Round(CrumpleClose * picH * 3));
            var near = new int[w * h];
            var d1 = Chamfer(inside, w, h, near);
            var outside = new bool[w * h];
            for (int i = 0; i < outside.Length; i++) outside[i] = d1[i] > r3;
            var d2 = Chamfer(outside, w, h, null);
            var add = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var a = Mathf.Clamp01((d2[i] - r3) / 3f + 0.5f);
                if (a <= 0) continue;
                var p = px[i];
                var was = p.a;
                if (p.a < 128 && near[i] >= 0)
                {
                    var q = px[near[i]];
                    p.r = q.r;
                    p.g = q.g;
                    p.b = q.b;
                }
                var ca = (byte)Math.Round(a * 255);
                p.a = (byte)Math.Max(p.a, ca);
                px[i] = p;
                if (was < 128 || ca > was) add[i] = new Color32((byte)(p.r * ca / 255), (byte)(p.g * ca / 255), (byte)(p.b * ca / 255), ca);
            }
            return add;
        }

        /// <summary>Chamfer distance (3 a step, 4 a diagonal) to the nearest set point; `near` gets that point's index.</summary>
        static int[] Chamfer(bool[] src, int w, int h, int[] near)
        {
            const int Inf = 1 << 28;
            var d = new int[w * h];
            for (int i = 0; i < d.Length; i++)
            {
                d[i] = src[i] ? 0 : Inf;
                if (near != null) near[i] = src[i] ? i : -1;
            }
            void Relax(int i, int x, int y, int cost)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                var j = y * w + x;
                if (d[j] + cost < d[i])
                {
                    d[i] = d[j] + cost;
                    if (near != null) near[i] = near[j];
                }
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var i = y * w + x;
                Relax(i, x - 1, y, 3);
                Relax(i, x - 1, y - 1, 4);
                Relax(i, x, y - 1, 3);
                Relax(i, x + 1, y - 1, 4);
            }
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                var i = y * w + x;
                Relax(i, x + 1, y, 3);
                Relax(i, x + 1, y + 1, 4);
                Relax(i, x, y + 1, 3);
                Relax(i, x - 1, y + 1, 4);
            }
            return d;
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
            SetupTopology(srng);
            SetupScene(scan, groundY);
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

        /// <summary>The mesh's bars, hinges and panels, and the face panel: the canvas only (no Unity calls; the crumple
        /// makes them on a worker thread as the fight starts).</summary>
        void SetupTopology(System.Random srng)
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
        }

        /// <summary>The ground, gravity and the light where the figure stands now, the materials' colours, the meshes.</summary>
        void SetupScene(Scan scan, float groundY)
        {
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
            var back = BackTest ?? (Crumple ? Color.Lerp(Back[0], scan.Mean, 0.1f) : Color.Lerp(Back[0] * BackLight, scan.Mean, BackTint));
            back.a = 0.04f;
            solidMat.SetColor(BackId, back);
            showMat.SetFloat(OutlineId, Crumple ? CrumpleOutline : 0);
            solidMat.SetFloat(EdgeInkId, Crumple && CrumpleKind == 2 ? CrumpleEdgeInk : 0);
            simTime = 0;
            landedOnce = false;
            startWidthPx = 0;
            SetupBuffers();
        }

        /// <summary>The meshes' buffers, their corners' texture places and indices: made once (the crumple makes them as the fight starts).</summary>
        void SetupBuffers()
        {
            if (rv != null && rv.Length == tCount * 3) return;
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
            if (CrumpleKind == 2)
            {
                // Try 2 is posed already (about its middle): set where the picture stood, upright to the ground.
                var u3 = new Vector3(upC.x, upC.y, 0).normalized;
                var s3 = new Vector3(upC.y, -upC.x, 0).normalized;
                for (int i = 0; i < vCount; i++)
                {
                    var r = crumplePos[i];
                    pos[i] = prev[i] = mid + s3 * r.x + u3 * r.y + Vector3.forward * r.z;
                }
                CrumpleGround();
                return;
            }
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
            CrumpleGround();
        }

        /// <summary>The lump set on the ground, and the shadow's starting width.</summary>
        void CrumpleGround()
        {
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
            if (Crumple)
            {
                // The crumple's facets are flat (each triangle its own normal).
                for (int t = 0; t < tCount; t++) rNormal[t * 3] = rNormal[t * 3 + 1] = rNormal[t * 3 + 2] = normal[t];
            }
            else
            {
                Array.Clear(nAcc, 0, nAcc.Length);
                for (int t = 0; t < tCount; t++)
                for (int j = 0; j < 3; j++)
                    nAcc[tri[t * 3 + j] * panelCount + triPanel[t]] += normal[t];
                for (int t = 0; t < tCount; t++)
                for (int j = 0; j < 3; j++)
                {
                    var nn = nAcc[tri[t * 3 + j] * panelCount + triPanel[t]];
                    rNormal[t * 3 + j] = nn.sqrMagnitude > 1e-12f ? nn.normalized : normal[t];
                }
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
            if (DumpRt != null && simTime >= DumpAt && !dumpedRt)
            {
                // "{seed}" in the path: every death saves its own (else only the next one).
                var path = DumpRt.Replace("{seed}", seed.ToString());
                Render(1);
                SaveRt(path.Replace(".png", "_noshadow.png"));
                Render(Passes);
                SaveRt(path);
                if (DumpRt.Contains("{seed}")) dumpedRt = true;
                else DumpRt = null;
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
                if (crumpleMoving)
                {
                    CrumpleShadow(width, rise);
                    return;
                }
                // A crumpled lump sits on a small, dark contact shadow (a soft blob as wide as the monster reads as nothing).
                var tight = Crumple ? 0.7f : 1;
                // Try 2: a flat cut-paper shadow (a hard ellipse a little wider than the lump; a soft one vanishes on the
                // stages' dark ground).
                var flatShadow = Crumple && CrumpleKind == 2;
                if (flatShadow) tight = 0.6f;
                shadow.localScale = flatShadow
                    ? new Vector3(shadowWidth * Mathf.Clamp(width / startWidthPx, 0.2f, 1) * 1.57f * (1 - 0.3f * rise), shadowHeight * tight, 1)
                    : new Vector3(shadowWidth * Mathf.Clamp(width / startWidthPx, 0.3f, 1) * (1 - 0.3f * rise), shadowHeight * tight, 1);
                if (shadowSr != null)
                {
                    if (flatShadow && shadowSr.sprite != Art.Circle) shadowSr.sprite = Art.Circle;
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
            DestroyCrumple();
        }
    }
}
