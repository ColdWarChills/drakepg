// The crumple in motion (docs/PLANS.md, step 1c, gate B): the fold program played on damped springs, a squeeze into
// the relief, and a rigid drop that bounces, rolls and goes to sleep. Nothing here is a timed curve: every turn, the
// squeeze and the drop are integrated on Anim time.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg
{
    public partial class Paper
    {
        /// <summary>Gate B: the crumple moves (off: gate A's still lump).</summary>
        public static bool CrumpleMotion = true;
        /// <summary>
        /// Each fold is a damped spring toward a half turn: how fast it swings (Hz, min and max, seeded per fold), its
        /// damping (a share of critical), and the share of its speed that comes back when it slaps onto the paper under
        /// it (only a fold no other waits on bounces: one that others wait on lies flat once down). A fold starts once
        /// the one before it is `FoldLead` of a half turn along (min and max, seeded) and every fold it waits on is down.
        /// 1.3 times the first try's 3.6-4.8 Hz: the folds end at 0.45-0.6 s and still read at x1 and x2.
        /// </summary>
        public static Vector2 FoldHz = new Vector2(4.68f, 6.24f), FoldLead = new Vector2(0.25f, 0.6f);
        public static float FoldDamp = 0.45f, FoldBounce = 0.1f;
        /// <summary>The squeeze (the rim pulled in, the relief, the lean) starts once every fold is `SqueezeAt` of a half
        /// turn along; a spring of `SqueezeHz`, damped `SqueezeDamp` of critical (a little spring-back, as paper has).</summary>
        public static float SqueezeAt = 0.6f, SqueezeHz = 6f, SqueezeDamp = 0.75f;
        /// <summary>The killing blow knocks it away from the hero (sheets a second) and up (as high as a share of the sheet).</summary>
        public static float KnockAway = 0.6f, KnockUp = 0.05f;
        /// <summary>It goes to sleep once slower than `SleepSpeed` (sheets a second) for `SleepFor` seconds, on the
        /// ground, folds and squeeze done; at `MotionCap` seconds it stops anyway (logged as capped).</summary>
        public static float SleepSpeed = 0.04f, SleepFor = 0.12f, MotionCap = 3f;
        /// <summary>How often a death's wad may be tipped off a tip it came to rest on (then the tip counts as held).</summary>
        public static int MaxKicks = 3;
        /// <summary>Rolling resistance: a lumpy wad loses its roll against the ground (a share of what presses it down,
        /// at the wad's mean radius); without it a round one rolls on and on.</summary>
        public static float RollResist = 0.08f;
        /// <summary>The roll lost to its lumps while it touches the ground (a share of the spin a second): each lump that
        /// comes down takes some of it, so a fast roll dies quickly. It never holds the wad still (`RollResist` does).</summary>
        public static float RollDamp = 4f;
        /// <summary>The settling, so a death ends by a set time (about 1.2 s from the fold's start to the dust, never
        /// past 1.4 s): from `SettleFrom` seconds the ground takes more of the wad's slide and spin each second (and its
        /// bounces soften), rising smoothly to `SettleRate` a second at `SettleBy`, and no tip-off after it (0: off).
        /// With the grip, the sleep and the stop below, all 30 monsters sleep by 1.21 s (tm/P1, 2026-11-26).</summary>
        public static float SettleFrom = 0.55f, SettleBy = 0.85f, SettleRate = 10;
        /// <summary>Testing (off): a wad whose centre has passed the edge it stands on tips over that edge freely (no
        /// lumps' damping, no settling while it pivots), instead of creeping over it.</summary>
        public static bool ToppleFree;
        /// <summary>The settling also grips (0: off): the rolling resistance's lever grows smoothly from `SettleFrom`
        /// to (1 + `SettleGrip`) times by `SettleBy`, so the ground holds a leaning wad and stops a roll in a set time (a
        /// steady slowing ends; a share a second only creeps).</summary>
        public static float SettleGrip = 4;
        /// <summary>Once the settling is full (`SettleBy`), slow is enough to sleep, held or not: a wad rocking between
        /// lumps that barely touch the ground is still to the eye, and the held test sees only this step's touches.</summary>
        public static bool SettleSleep = true;
        /// <summary>The settling's stop (0: off). From `SettleBy` the ground also takes the wad's whole motion
        /// (slide any way and spin), a share a second rising smoothly to `SettleStopRate` at `SettleStop` seconds; then a
        /// wad on the ground with its folds down sleeps whatever is left (logged), so no death runs past it. In the air
        /// it is neither slowed nor stopped. Two of the 30 reach it (tm/P1), both nearly still.</summary>
        public static float SettleStop = 1.2f, SettleStopRate = 30;
        /// <summary>The landing (the thud, dust and scraps): the first touch as hard as a fall of this share of the sheet.</summary>
        public static float LandFall = 0.2f;
        /// <summary>Test: log the body and the squeeze each tick (a sixtieth of a second) of the next deaths.</summary>
        public static bool TraceMotion;
        readonly System.Text.StringBuilder mTrace = new System.Text.StringBuilder();
        /// <summary>The outline's closing, the ink along cut edges and the dark outline fade in over these seconds (the
        /// living picture has none of them).</summary>
        public static float CloseFade = 0.12f, InkFade = 0.2f, ShadowFade = 0.15f;
        const float MotionStep = 1 / 480f;
        /// <summary>At most this much motion a frame (24 steps, 0.05 s): a slow frame slows it for a moment rather
        /// than skipping part of it.</summary>
        const int MaxMotionSteps = 24;

        static readonly int CloseTexId = Shader.PropertyToID("_CloseTex"), CloseId = Shader.PropertyToID("_Close");

        Texture2D closeTex;
        SpriteRenderer flatShadowSr;
        float flatShadowA;
        bool crumpleMoving;

        // The folds' springs: angle (0 flat, pi down), speed, frequency, damping, lead; started, first down, held down.
        float[] mAng, mVel, mOmega, mZeta, mLead, mProg, mOnAt, mDownAt;
        bool[] mOn, mDown, mLatch;
        long mWaited;
        // The squeeze.
        float mS, mSV, mSqueezeAt;
        bool mSqueezeOn, mSqueezed;
        // The shape: the chain's pose, the squeeze's offsets (rim pull xy, relief z), the shape about its own frame.
        Vector3[] mChain, mDisp, mLocal, mCheck;
        // The rigid body: centre of mass (canvas px), its speed, turn and spin; the shape's centre and spread (per unit
        // mass); the hull of its inked points about that centre.
        Vector2 mX, mV, mCom;
        float mPhi, mW, mI;
        readonly List<Vector2> mInkedXY = new List<Vector2>(), mHull = new List<Vector2>();
        struct Contact
        {
            public Vector2 R;
            public float Vn0, Pn, Pt;
        }
        readonly List<Contact> mContacts = new List<Contact>();
        float mTime, mStillFor, mTouchAt, mLandAt, mSleepAt, mStretch;
        bool mDone, mCapped, mInContact, mWasTouching, mLandNow, mChainDone, mShapeDone, mHeld, mOnTip, mOverTip, mStopped;
        float mStopSpeed;
        // The support of the last step (along the ground from the centre of mass, its end points, the centre's height
        // over it) and how often the sleep check tipped the wad off a tip.
        float mSupLo, mSupHi, mRise;
        Vector2 mPivotLo, mPivotHi;
        int mKicks;
        /// <summary>The inked points (as `mHull`) that may touch the ground this frame (the lowest, and some above).</summary>
        readonly List<int> mCand = new List<int>();
        Vector3[] mProj, mSProj;
        /// <summary>CPU time per part of a frame (the springs, the shape, the drop, the angles, the drawing): sums and worst, in stopwatch ticks.</summary>
        readonly long[] mTicks = new long[5], mWorstTicks = new long[5];

        void Part(int i, long t0)
        {
            var d = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            mTicks[i] += d;
            if (d > mWorstTicks[i]) mWorstTicks[i] = d;
        }

        string PartsNote(int frames)
        {
            var names = new[] { "springs", "shape", "drop", "angles", "draw" };
            var f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            var note = "";
            for (int i = 0; i < 5; i++) note += $"{(i > 0 ? ", " : "")}{names[i]} {mWorstTicks[i] * f:0.00}/{mTicks[i] * f / Math.Max(1, frames):0.00}";
            return note;
        }
        string mImpacts = "", mTail = "";
        float mTailAt;
        int mRestContacts;
        string mRestNote = "";

        /// <summary>
        /// The crumple: the sheet built and folded flat as a program (the fold check), then played: each fold turns on
        /// its spring, the squeeze overlaps the last turns, and the paper falls as a rigid body the whole time (knocked
        /// up and away by the blow, spun only by what it hits). Ends asleep on the ground. `landed` as in <see cref="Fold"/>.
        /// </summary>
        async Task FoldCrumple(float groundY, Action<float, float> landed)
        {
            folding = true;
            solidOn = true;
            var crumpleStart = Time.time;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // Built as the fight started (a worker thread); a blow that kills at once may wait for it to finish.
            float waitMs = 0;
            if (pre == PreReading || pre == PreBuilding)
            {
                while (pre == PreReading || pre == PreBuilding)
                {
                    await Task.Yield();
                    if (this == null) return;
                }
                waitMs = (float)watch.Elapsed.TotalMilliseconds;
                watch.Restart();
            }
            Color32[] add;
            Scan scan;
            float buildMs;
            var built = pre == PreReady ? "as the fight started" : "at the death";
            if (pre == PreReady)
            {
                // The fight's creases, tears and burns onto the canvas made at the start (the closing, made from the
                // clean picture, adds nothing inside it: a burn stays open).
                BakeDamage();
                add = preAdd;
                scan = preScan;
                buildMs = preMs;
                closeMs = preCloseMs;
            }
            else
            {
                if (pre == PreFailed) Debug.LogWarning($"drakepg: paper crumple, the build as the fight started failed ({preError}); built at the death");
                if (pre == PreNone)
                {
                    seed = NextSeed != 0 ? NextSeed : rng.Next(1, int.MaxValue);
                    NextSeed = 0;
                    MakeCanvas();
                }
                else BakeDamage();
                var srng = new System.Random(seed);
                var px = ReadCanvas();
                var cw = System.Diagnostics.Stopwatch.StartNew();
                add = CloseAdd(px, FW, FH, H);
                closeMs = (float)cw.Elapsed.TotalMilliseconds;
                scan = ScanPixels(px, FW, FH);
                if (CheckChain) CompareScan(px, scan);
                BuildShape(scan, srng);
                buildMs = (float)watch.Elapsed.TotalMilliseconds;
                watch.Restart();
            }
            var marks = "";
            void Mark(string what)
            {
                marks += $"{(marks.Length > 0 ? ", " : "")}{what} {watch.Elapsed.TotalMilliseconds:0.0}";
            }
            StartScene();
            Mark("scene");
            UploadClose(add);
            SetupScene(scan, groundY);
            Mark("setup");
            if (CheckChain) CheckChainNow(sheetSize / CrumpleGrid);
            InitMotion();
            Mark("motion");
            DrawCrumple();
            Mark("draw");
            var startMs = (float)watch.Elapsed.TotalMilliseconds;
            float worstMs = 0, sumMs = 0;
            int frames = 0;
            var firstFrames = "";
            Array.Clear(mTicks, 0, mTicks.Length);
            Array.Clear(mWorstTicks, 0, mWorstTicks.Length);
            long allocated = 0;
            var handle = Anim.Float(0, 30, 30, time =>
            {
                var bytes0 = GC.GetTotalMemory(false);
                var w = System.Diagnostics.Stopwatch.StartNew();
                StepMotion(time, landed);
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                DrawCrumple();
                Part(4, t0);
                var ms = (float)w.Elapsed.TotalMilliseconds;
                worstMs = Mathf.Max(worstMs, ms);
                sumMs += ms;
                if (frames < 6) firstFrames += $" {ms:0.0}";
                frames++;
                allocated += Math.Max(0, GC.GetTotalMemory(false) - bytes0);
            }, Ease.Linear);
            var over = false;
            handle.OnComplete(() => over = true);
            while (!over && !handle.Done)
            {
                if (mDone) handle.Stop();
                await Task.Yield();
                if (this == null) return;
            }
            if (!landedOnce)
            {
                landedOnce = true;
                mLandAt = mTime;
                var (mid, width, _) = Packet();
                landed?.Invoke(mid.x, width);
            }
            if (worstMs > SlowestMs) SlowestMs = worstMs;
            var turns = "";
            for (int k = 0; k < chainK; k++) turns += $"{(k > 0 ? " " : "")}{k}:{mOnAt[k]:0.00}-{mDownAt[k]:0.00}";
            LastSolid = $"took {Time.time - crumpleStart:0.00} s, seed {seed}, {vCount} points, {tCount} triangles, build {buildMs:0} ms {built} (closing {closeMs:0}, shape {crumpleMs:0}), waited {waitMs:0} ms, start {startMs:0.0} ms ({marks}), "
                + $"folds {turns}, squeeze from {mSqueezeAt:0.00}, first touch {mTouchAt:0.00}, landed {mLandAt:0.00}, "
                + $"touches (s:fall){mImpacts}, speed (s:along/spin){mTail}, " + (mCapped ? $"CAPPED at {mTime:0.00} s" : $"asleep at {mSleepAt:0.00} s on {mRestContacts} points{mRestNote}") + $", CPU a frame: worst {worstMs:0.00} ms, mean {sumMs / Mathf.Max(1, frames):0.00} ms over {frames} frames, the first{firstFrames}, {allocated / 1024f:0.0} KB allocated over them; parts worst/mean ms: {PartsNote(frames)}"
                + (CheckChain ? $", chain stretch over the frames played {mStretch / Mathf.Max(1, sheetSize / CrumpleGrid):0.0000} cells" : "")
                + $", crumple: {crumpleNote}";
            Debug.Log($"drakepg: paper crumple, {LastSolid}");
            if (TraceMotion) Debug.Log($"drakepg: paper crumple seed {seed} trace (s:squeeze turn spin place speed width contacts):{mTrace}");
        }

        /// <summary>The canvas's pixels (y up).</summary>
        Color32[] ReadCanvas()
        {
            var tex = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = solidCanvas;
            tex.ReadPixels(new Rect(0, 0, FW, FH), 0, 0, false);
            RenderTexture.active = was;
            var px = tex.GetPixels32();
            Destroy(tex);
            return px;
        }

        /// <summary>
        /// The scan (<see cref="ScanCanvas"/>) from pixels on the CPU: a quarter of the size, each point the mean of the
        /// canvas pixels under it (as the GPU's blit reads the canvas's mips), opaque past 60. No Unity calls.
        /// </summary>
        static Scan ScanPixels(Color32[] px, int fw, int fh)
        {
            int sw = Math.Max(4, fw / 4), sh = Math.Max(4, fh / 4);
            float fx = fw / (float)sw, fy = fh / (float)sh;
            var s = new Scan
            {
                Pts = new List<Vector2>(), Min = new Vector2(float.MaxValue, float.MaxValue), Max = new Vector2(float.MinValue, float.MinValue),
                Solid = new bool[sw * sh], SW = sw, SH = sh, FX = fx, FY = fy,
            };
            var sum = Vector2.zero;
            double cr = 0, cg = 0, cb = 0, cn = 0;
            for (int y = 0; y < sh; y++)
            {
                int y0 = (int)(y * fy), y1 = Math.Max(y0 + 1, (int)((y + 1) * fy));
                for (int x = 0; x < sw; x++)
                {
                    int x0 = (int)(x * fx), x1 = Math.Max(x0 + 1, (int)((x + 1) * fx));
                    int a = 0, r = 0, g = 0, b = 0, n = 0;
                    for (int yy = y0; yy < y1 && yy < fh; yy++)
                    for (int xx = x0; xx < x1 && xx < fw; xx++)
                    {
                        var c = px[yy * fw + xx];
                        a += c.a;
                        r += c.r;
                        g += c.g;
                        b += c.b;
                        n++;
                    }
                    if (n == 0 || a / n <= 60) continue;
                    cr += r / (255.0 * n);
                    cg += g / (255.0 * n);
                    cb += b / (255.0 * n);
                    cn++;
                    s.Solid[y * sw + x] = true;
                    var p = new Vector2((x + 0.5f) * fx, (y + 0.5f) * fy);
                    s.Pts.Add(p);
                    sum += p;
                    s.Min = Vector2.Min(s.Min, p - new Vector2(fx / 2, fy / 2));
                    s.Max = Vector2.Max(s.Max, p + new Vector2(fx / 2, fy / 2));
                }
            }
            s.Mid = s.Pts.Count > 0 ? sum / s.Pts.Count : new Vector2(fw / 2f, fh / 2f);
            s.Mean = cn > 0 ? new Color((float)(cr / cn), (float)(cg / cn), (float)(cb / cn), 1) : Color.gray;
            return s;
        }

        /// <summary>Testing (`CheckChain`): the CPU scan against the GPU's on the same (closed) canvas, logged.</summary>
        void CompareScan(Color32[] px, Scan cpu)
        {
            var tex = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply(false);
            Graphics.Blit(tex, solidCanvas);
            Destroy(tex);
            var gpu = ScanCanvas();
            BakeDamage();
            int both = 0;
            for (int i = 0; i < Math.Min(cpu.Solid.Length, gpu.Solid.Length); i++)
                if (cpu.Solid[i] && gpu.Solid[i]) both++;
            Debug.Log($"drakepg paper scan check: cpu {cpu.Pts.Count} points, gpu {gpu.Pts.Count}, both {both}; cpu box {cpu.Min}-{cpu.Max}, gpu {gpu.Min}-{gpu.Max}; "
                + $"mean colour cpu {cpu.Mean} gpu {gpu.Mean}");
        }

        /// <summary>The crumple's shape from the scan: size, face, mesh, folds (the tries), the fold program, the mesh's
        /// hinges and panels. No Unity calls past the scan (a worker thread can run it).</summary>
        void BuildShape(Scan scan, System.Random srng)
        {
            sheetSize = Mathf.Max(scan.Max.x - scan.Min.x, scan.Max.y - scan.Min.y, 8);
            FindFace(scan);
            {
                var hh = scan.Max.y - scan.Min.y;
                if (scan.Max.x - scan.Min.x <= 1.3f * hh) face.y = Mathf.Min(face.y, scan.Min.y + 0.67f * hh);
                faceRadius = Mathf.Max(faceRadius, 0.28f * hh);
            }
            lines.Clear();
            BuildMesh(scan, srng);
            SetupTopology(srng);
        }

        /// <summary>The closing as its own texture (with mips, as the canvas), shown by the paper's material at `_Close`.</summary>
        void UploadClose(Color32[] add)
        {
            if (closeTex == null) MakeCloseTex(add);
            solidMat.SetTexture(CloseTexId, closeTex);
            solidMat.SetFloat(CloseId, 0);
        }

        /// <summary>The springs, the squeeze and the body at rest, flat, as the picture stood; the blow's knock.</summary>
        void InitMotion()
        {
            PrepareMotion();
            var K = chainK;
            var rnd = new System.Random(seed * 7919 + 13);
            float U(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
            mWaited = 0;
            for (int k = 0; k < K; k++)
            {
                mWaited |= chainWait[k];
                mOmega[k] = 2 * Mathf.PI * U(FoldHz.x, FoldHz.y);
                mZeta[k] = FoldDamp * U(0.85f, 1.15f);
                mLead[k] = U(FoldLead.x, FoldLead.y);
                mOnAt[k] = mDownAt[k] = -1;
                mAng[k] = mVel[k] = mProg[k] = 0;
                mOn[k] = mDown[k] = mLatch[k] = false;
            }
            mS = mSV = 0;
            mSqueezeOn = mSqueezed = false;
            mSqueezeAt = mTouchAt = mLandAt = mSleepAt = -1;
            mTime = mStillFor = mStretch = 0;
            mDone = mCapped = mInContact = mWasTouching = mLandNow = mChainDone = mShapeDone = mHeld = mOnTip = mOverTip = mStopped = false;
            mStopSpeed = 0;
            mKicks = 0;
            mImpacts = "";
            mTrace.Clear();
            mStep = 0;
            mTail = "";
            mTailAt = 0.75f;
            mRestContacts = 0;
            mRestNote = "";
            mContacts.Clear();
            mI = 0;
            mW = 0;
            mPhi = 0;
            ShapeNow();
            mX = mCom;
            var rightL = body.transform.InverseTransformVector(Vector3.right);
            var away = new Vector2(rightL.x, rightL.y).normalized;
            mV = away * (KnockAway * sheetSize) + upC * Mathf.Sqrt(2 * gravityC.magnitude * KnockUp * sheetSize);
            // The living picture has no closing, cut-edge ink or outline: they fade in.
            solidMat.SetFloat(CloseId, 0);
            solidMat.SetFloat(EdgeInkId, 0);
            showMat.SetFloat(OutlineId, 0);
            crumpleMoving = true;
            WorldNow();
            NormalsNow();
        }

        /// <summary>The motion's arrays and the squeeze's offsets (where the build leaves each point, against where the
        /// folds alone leave it): from the build alone, made once.</summary>
        void PrepareMotion()
        {
            if (mDisp != null && mDisp.Length == vCount && mAng != null && mAng.Length == chainK) return;
            var K = chainK;
            var n = vCount;
            mAng = new float[K];
            mVel = new float[K];
            mOmega = new float[K];
            mZeta = new float[K];
            mLead = new float[K];
            mProg = new float[K];
            mOnAt = new float[K];
            mDownAt = new float[K];
            mOn = new bool[K];
            mDown = new bool[K];
            mLatch = new bool[K];
            mChain = new Vector3[n];
            mLocal = new Vector3[n];
            mDisp = new Vector3[n];
            mCheck = CheckChain ? new Vector3[n] : null;
            mProj = new Vector3[n];
            mSProj = new Vector3[n];
            for (int k = 0; k < K; k++)
            {
                mAng[k] = Mathf.PI;
                mProg[k] = 1;
            }
            ChainPose(mAng, mProg, mChain);
            for (int v = 0; v < n; v++)
                mDisp[v] = new Vector3(shape.Final[v].x - mChain[v].x, shape.Final[v].y - mChain[v].y, shape.Relief[v]);
            for (int k = 0; k < K; k++) mAng[k] = mProg[k] = 0;
        }

        /// <summary>
        /// The springs, the squeeze and the body to `time` (Anim seconds) in fixed steps. The shape, the ground's
        /// candidates, the landing and the sleep check run on a fixed tick of `TickSteps` steps (a sixtieth of a second),
        /// counted in steps, not frames, so a death plays the same at any frame rate.
        /// </summary>
        void StepMotion(float time, Action<float, float> landed)
        {
            if (mDone) return;
            var steps = Mathf.Min(MaxMotionSteps, Mathf.FloorToInt((time - mTime) / MotionStep));
            if (steps <= 0) return;
            for (int i = 0; i < steps && !mDone; i++)
            {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                FoldStep(mTime, MotionStep);
                Part(0, t0);
                if (mStep % TickSteps == 0)
                {
                    t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    ShapeNow();
                    Part(1, t0);
                    t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    NearGround();
                    mInContact = false;
                    Part(2, t0);
                }
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                BodyStep(mTime + MotionStep, MotionStep, landed);
                Part(2, t0);
                mTime += MotionStep;
                mStep++;
                if (mStep % TickSteps == 0) TickEnd(landed);
            }
            // Behind (a slow frame): what it couldn't catch up is let go, not saved up.
            if (steps == MaxMotionSteps && !mDone) mTime = time;
            var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            WorldNow();
            NormalsNow();
            Part(3, t1);
            solidMat.SetFloat(CloseId, Mathf.Clamp01(mTime / CloseFade));
            solidMat.SetFloat(EdgeInkId, CrumpleEdgeInk * Mathf.Clamp01(mTime / InkFade));
            if (CrumpleCrinkle.x > 0 || CrumpleCrinkle.z > 0)
            {
                var u = Mathf.Clamp01(mTime / Mathf.Max(1e-3f, CrinkleFade));
                u = u * u * (3 - 2 * u);
                solidMat.SetVector(CrinkleId, new Vector4(CrumpleCrinkle.x * u, Mathf.Max(2, CrumpleCrinkle.y), CrumpleCrinkle.z * u, CrumpleCrinkle.w));
            }
            if (CrumplePrint != Vector2.zero)
            {
                var u = Mathf.Clamp01(mTime / Mathf.Max(1e-3f, CrumplePrintFade));
                u = u * u * (3 - 2 * u);
                solidMat.SetVector(PrintId, new Vector4(CrumplePrint.x * u, CrumplePrint.y * u, 0, 0));
            }
            showMat.SetFloat(OutlineId, CrumpleOutline * Mathf.Clamp01(mTime / InkFade));
            if (CheckChain)
            {
                ChainPose(mAng, null, mCheck);
                var tri3 = shape.Tri;
                for (int t = 0; t < tri3.Count; t += 3)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = tri3[t + j], b = tri3[t + (j + 1) % 3];
                        mStretch = Mathf.Max(mStretch, Mathf.Abs((mCheck[a] - mCheck[b]).magnitude - (shape.Flat[a] - shape.Flat[b]).magnitude));
                    }
            }
        }

        /// <summary>Steps in a tick (the shape, the ground's candidates, the landing and the sleep check): a sixtieth of a second.</summary>
        const int TickSteps = 8;
        int mStep;

        /// <summary>The end of a tick: the landing as it balls up on the ground, the log of its speed, the sleep check.</summary>
        void TickEnd(Action<float, float> landed)
        {
            if (mLandNow && !landedOnce)
            {
                landedOnce = true;
                mLandAt = mTime;
                WorldNow();
                var (mid, width, _) = Packet();
                landed?.Invoke(mid.x, width);
            }
            if (TraceMotion)
            {
                float c = Mathf.Cos(mPhi), sn = Mathf.Sin(mPhi), lo = float.MaxValue, hi = float.MinValue;
                foreach (var h in mHull)
                {
                    var x = c * h.x - sn * h.y;
                    lo = Mathf.Min(lo, x);
                    hi = Mathf.Max(hi, x);
                }
                mTrace.Append($" {mTime:0.000}:s{mS:0.00} phi{mPhi * Mathf.Rad2Deg:0} w{mW:0.0} x{mX.x / sheetSize:0.000},{mX.y / sheetSize:0.000} v{mV.x / sheetSize:0.00},{mV.y / sheetSize:0.00} wid{(hi - lo) / sheetSize:0.00} c{mContacts.Count}{(landedOnce && mLandAt > mTime - TickSteps * MotionStep - 1e-4f ? " LAND" : "")}");
            }
            // Asleep: on the ground and held there (not on a tip), slow, every fold down and the squeeze done, for a moment.
            var allDown = mSqueezed;
            for (int k = 0; k < chainK && allDown; k++) allDown = mLatch[k];
            var speed = mV.magnitude + Mathf.Abs(mW) * Mathf.Sqrt(mI);
            // For the log: how it moves after the folds, each quarter second (sheets a second: along, spin at its radius).
            if (mTime >= mTailAt - 1e-4f && mTail.Length < 300)
            {
                mTail += $" {mTime:0.00}:{mV.magnitude / sheetSize:0.000}/{Mathf.Abs(mW) * Mathf.Sqrt(mI) / sheetSize:0.000}{(mInContact ? "" : "air")}";
                mTailAt += 0.25f;
            }
            // Slow on a tip with its centre over it, paper is never left balanced: it is tipped off at once (at most
            // `MaxKicks` times a death, and none once a settling is full; after that the tip counts as held, so it always
            // sleeps).
            var slow = allDown && mInContact && speed < SleepSpeed * sheetSize;
            if (slow && mOverTip && mKicks < MaxKicks && (SettleRate <= 0 || mTime < SettleBy))
            {
                Kick();
                mStillFor = 0;
            }
            else if (slow && (mHeld || mOverTip || (SettleSleep && SettleRate > 0 && mTime >= SettleBy))) mStillFor += TickSteps * MotionStep;
            else mStillFor = 0;
            // The settling's stop: on the ground at `SettleStop` with the folds down, it sleeps now.
            if (SettleStop > 0 && mTime >= SettleStop - 1e-4f && allDown && mInContact && mStillFor < SleepFor - 1e-4f)
            {
                mStopped = true;
                mStopSpeed = speed / sheetSize;
                mStillFor = SleepFor;
            }
            if (mStillFor >= SleepFor - 1e-4f)
            {
                mDone = true;
                mSleepAt = mTime;
                mRestContacts = mContacts.Count;
                // Where it rests, along the ground from its centre of mass (sheets).
                var tan = new Vector2(upC.y, -upC.x);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var ct in mContacts)
                {
                    var o = Vector2.Dot(ct.R, tan) / sheetSize;
                    lo = Mathf.Min(lo, o);
                    hi = Mathf.Max(hi, o);
                }
                mRestNote = mContacts.Count > 0 ? $" (along the ground {lo:+0.000;-0.000} to {hi:+0.000;-0.000} of the sheet from its centre, radius {Mathf.Sqrt(mI) / sheetSize:0.000}, tipped off a tip {mKicks} times{(mOverTip ? ", resting on one" : mHeld ? "" : ", not held")}{(mStopped ? $", stopped by the settling at {mStopSpeed:0.000} sheets/s" : "")})" : "";
            }
            else if (mTime >= MotionCap)
            {
                mDone = mCapped = true;
                Debug.LogWarning($"drakepg: paper crumple seed {seed} still moving at {mTime:0.00} s (speed {speed / sheetSize:0.000} sheets/s, folds down {allDown}, on the ground {mInContact}, held {mHeld}): capped");
            }
        }

        /// <summary>Tips the wad off the tip it came to rest on: one turn about the tip's end on the side its centre leans
        /// to (else the way it was sliding, else a seeded side), fast enough to carry the centre a `reach` past that end
        /// against the rolling resistance, from there gravity takes it down. The size is seeded (x1.2-1.6).</summary>
        void Kick()
        {
            var tan = new Vector2(upC.y, -upC.x);
            var mid = 0.5f * (mSupLo + mSupHi);
            var vt = Vector2.Dot(mV, tan);
            var krng = new System.Random(seed * 31 + mKicks);
            var side = Mathf.Abs(mid) > 1e-3f * sheetSize ? Mathf.Sign(mid) : Mathf.Abs(vt) > 1e-3f * sheetSize ? -Mathf.Sign(vt) : krng.Next(2) == 0 ? 1f : -1f;
            // The + way it turns about its leftmost point, the - way about its rightmost.
            var pivot = side > 0 ? mPivotLo : mPivotHi;
            var inside = side > 0 ? Mathf.Max(0, -mSupLo) : Mathf.Max(0, mSupHi);
            var reach = RollResist * Mathf.Sqrt(mI);
            var turn = (inside + 2 * reach) / Mathf.Max(mRise, 0.05f * sheetSize);
            // The spin that turns that far before the rolling resistance (a steady slowing) and the lumps (a share a
            // second) stop it: w' = -slowing - RollDamp w turns w/k - slowing/k^2 ln(1 + k w/slowing).
            var slowing = reach * gravityC.magnitude / mI;
            float wLo = 0, wHi = Mathf.Sqrt(2 * slowing * turn) + RollDamp * turn + 1e-3f;
            var damp = RollDamp + SettleNow(mTime);
            wHi += (damp - RollDamp) * turn;
            while (TurnBeforeStop(wHi, slowing, damp) < turn) wHi *= 2;
            for (int it = 0; it < 30; it++)
            {
                var wMid = 0.5f * (wLo + wHi);
                if (TurnBeforeStop(wMid, slowing, damp) < turn) wLo = wMid;
                else wHi = wMid;
            }
            mW = side * (1.2f + 0.4f * (float)krng.NextDouble()) * wHi;
            mV = -mW * new Vector2(-pivot.y, pivot.x);
            mKicks++;
        }

        /// <summary>The settling's share a second at `t`: none before `SettleFrom`, rising smoothly to `SettleRate`
        /// at `SettleBy`.</summary>
        static float SettleNow(float t)
        {
            var u = Mathf.Clamp01((t - SettleFrom) / Mathf.Max(1e-3f, SettleBy - SettleFrom));
            return SettleRate * u * u * (3 - 2 * u);
        }

        /// <summary>The rolling resistance's lever at `t`, as a multiple: 1 before `SettleFrom`, rising smoothly to
        /// 1 + `SettleGrip` at `SettleBy`.</summary>
        static float StopNow(float t)
        {
            if (SettleStop <= 0) return 0;
            var u = Mathf.Clamp01((t - SettleBy) / Mathf.Max(1e-3f, SettleStop - SettleBy));
            return SettleStopRate * u * u * (3 - 2 * u);
        }

        static float GripNow(float t)
        {
            var u = Mathf.Clamp01((t - SettleFrom) / Mathf.Max(1e-3f, SettleBy - SettleFrom));
            return 1 + SettleGrip * u * u * (3 - 2 * u);
        }

        /// <summary>How far a spin of `w` turns while a steady `slowing` and a share `k` a second stop it.</summary>
        static float TurnBeforeStop(float w, float slowing, float k)
        {
            if (k < 1e-4f) return w * w / (2 * slowing);
            return w / k - slowing / (k * k) * Mathf.Log(1 + k * w / slowing);
        }

        /// <summary>One step of the folds' springs and the squeeze's.</summary>
        void FoldStep(float t, float dt)
        {
            var K = chainK;
            var allOn = true;
            var allFar = true;
            for (int k = 0; k < K; k++)
            {
                if (!mOn[k])
                {
                    // Its turn: the fold before it far enough along (or down), and every fold it waits on down.
                    var go = k == 0 || (mOn[k - 1] && (mDown[k - 1] || mAng[k - 1] >= mLead[k] * Mathf.PI));
                    for (int j = 0; j < k && go; j++)
                        if ((chainWait[k] & (1L << j)) != 0 && !mLatch[j]) go = false;
                    if (go)
                    {
                        mOn[k] = true;
                        mOnAt[k] = t;
                    }
                }
                if (!mOn[k])
                {
                    allOn = allFar = false;
                    continue;
                }
                if (!mLatch[k])
                {
                    var w = mOmega[k];
                    mVel[k] += (w * w * (Mathf.PI - mAng[k]) - 2 * mZeta[k] * w * mVel[k]) * dt;
                    mAng[k] += mVel[k] * dt;
                    if (mAng[k] >= Mathf.PI)
                    {
                        // Down onto the paper under it: a fold others wait on lies there; a last one bounces a little.
                        if (!mDown[k])
                        {
                            mDown[k] = true;
                            mDownAt[k] = t + dt;
                        }
                        if ((mWaited & (1L << k)) != 0)
                        {
                            mAng[k] = Mathf.PI;
                            mVel[k] = 0;
                            mLatch[k] = true;
                        }
                        else
                        {
                            mAng[k] = 2 * Mathf.PI - mAng[k];
                            mVel[k] = -FoldBounce * Mathf.Abs(mVel[k]);
                        }
                    }
                    if (mDown[k] && !mLatch[k] && Mathf.PI - mAng[k] < 0.01f && Mathf.Abs(mVel[k]) < 0.5f)
                    {
                        mAng[k] = Mathf.PI;
                        mVel[k] = 0;
                        mLatch[k] = true;
                    }
                }
                if (mAng[k] < SqueezeAt * Mathf.PI) allFar = false;
            }
            if (!mSqueezeOn && allOn && allFar)
            {
                mSqueezeOn = true;
                mSqueezeAt = t;
                // No hard landing yet, and it lies on the ground already: it lands as it balls up.
                if (!landedOnce && mWasTouching) mLandNow = true;
            }
            if (mSqueezeOn && !mSqueezed)
            {
                var w = 2 * Mathf.PI * SqueezeHz;
                mSV += (w * w * (1 - mS) - 2 * SqueezeDamp * w * mSV) * dt;
                mS += mSV * dt;
                // Done once what is left of it is under a canvas pixel.
                if (Mathf.Abs(1 - mS) * sheetSize < 0.5f && Mathf.Abs(mSV) * sheetSize < 2)
                {
                    mS = 1;
                    mSV = 0;
                    mSqueezed = true;
                }
            }
        }

        /// <summary>
        /// The shape now, about its own frame: the fold program at the springs' angles, plus the squeeze `mS` of the way to
        /// the build's lump (the rim pull and relief, then its lean and turn; its roll is left to the drop). Its centre of
        /// mass (the inked points), spread, and hull; the spin kept as the spread changes (a skater pulling in).
        /// </summary>
        void ShapeNow()
        {
            if (mShapeDone) return;
            var K = chainK;
            var allDown = true;
            for (int k = 0; k < K; k++)
            {
                mProg[k] = Mathf.Clamp01(mAng[k] / Mathf.PI);
                allDown &= mLatch[k];
            }
            // The folds all down: the chain no longer changes; the squeeze done too, nothing does.
            if (!(allDown && mChainDone)) ChainPose(mAng, mProg, mChain);
            mChainDone = allDown;
            mShapeDone = allDown && mSqueezed;
            var s = mS;
            var cen3 = new Vector3(shape.Cen.x, shape.Cen.y, 0);
            float rl = s * shape.Roll, tb = s * shape.Lean, yw = s * shape.Turn;
            float cr = Mathf.Cos(-rl), sr = Mathf.Sin(-rl);
            // The pose, then the roll undone in the picture's plane, as one matrix (its columns: the axes turned).
            Vector3 Turned(Vector3 e)
            {
                var p = PoseRot(e, rl, tb, yw);
                return new Vector3(cr * p.x - sr * p.y, sr * p.x + cr * p.y, p.z);
            }
            Vector3 ax = Turned(Vector3.right), ay = Turned(Vector3.up), az = Turned(Vector3.forward);
            var n = vCount;
            for (int v = 0; v < n; v++)
            {
                var q = mChain[v] + s * mDisp[v];
                if (s > 0)
                {
                    var d = q - cen3;
                    q = cen3 + ax * d.x + ay * d.y + az * d.z;
                }
                mLocal[v] = q;
            }
            var c = Vector2.zero;
            int m = 0;
            for (int v = 0; v < n; v++)
                if (inked[v])
                {
                    c += (Vector2)mLocal[v];
                    m++;
                }
            mCom = m > 0 ? c / m : Vector2.zero;
            float spread = 0;
            mInkedXY.Clear();
            for (int v = 0; v < n; v++)
                if (inked[v])
                {
                    var r = (Vector2)mLocal[v] - mCom;
                    spread += r.sqrMagnitude;
                    mInkedXY.Add(r);
                }
            spread = Mathf.Max(spread / Mathf.Max(1, m), 0.0025f * sheetSize * sheetSize);
            if (mI > 0) mW *= mI / spread;
            mI = spread;
            // What touches the ground is the outline as drawn (<see cref="Project"/>: what stands toward the viewer is
            // drawn bigger and lower), so it comes to rest on an edge the player sees, not on a point poking below it.
            var eye = Eye * sheetSize;
            mHull.Clear();
            mDrop.Clear();
            int j = 0;
            for (int v = 0; v < n; v++)
            {
                if (!inked[v]) continue;
                var z = mLocal[v].z;
                var k = eye / Mathf.Max(0.2f * eye, eye - z);
                mHull.Add(mInkedXY[j++] * k);
                mDrop.Add(Tilt * z);
            }
        }

        /// <summary>Per inked point (as `mHull`), how far below its place it is drawn (the look down).</summary>
        readonly List<float> mDrop = new List<float>();

        /// <summary>
        /// One step of the rigid drop (unit mass): gravity, then the ground's contacts (its hull's points under the
        /// ground) as sequential impulses, a bounce of `Bounce` above a small speed and Coulomb friction `Friction`, so
        /// any spin comes from where it hits; then pushed out of the ground. The first hard landing once the squeeze has
        /// started is `landed`.
        /// </summary>
        void BodyStep(float t, float dt, Action<float, float> landed)
        {
            var nrm = upC;
            var tan = new Vector2(nrm.y, -nrm.x);
            mV += (Vector2)gravityC * dt;
            mX += mV * dt;
            mPhi += mW * dt;
            float c = Mathf.Cos(mPhi), s = Mathf.Sin(mPhi);
            mContacts.Clear();
            float deepest = 0, hardest = 0;
            foreach (var i0 in mCand)
            {
                var h = mHull[i0];
                var r = new Vector2(c * h.x - s * h.y, s * h.x + c * h.y);
                var depth = groundC - Vector2.Dot(mX + r, nrm) + mDrop[i0] * nrm.y;
                if (depth <= 0) continue;
                var vn0 = Vector2.Dot(mV + mW * new Vector2(-r.y, r.x), nrm);
                mContacts.Add(new Contact { R = r, Vn0 = vn0 });
                deepest = Mathf.Max(deepest, depth);
                hardest = Mathf.Max(hardest, -vn0);
            }
            var g = gravityC.magnitude;
            var settle = SettleNow(t);
            if (mContacts.Count == 0)
            {
                mWasTouching = false;
                mHeld = mOnTip = mOverTip = false;
                return;
            }
            mInContact = true;
            if (mTouchAt < 0) mTouchAt = t;
            // Each new touch with some speed, for the log: when, and as a fall of what share of the sheet.
            var fall = hardest * hardest / (2 * g * sheetSize);
            if (!mWasTouching && fall > 0.01f && mImpacts.Length < 200) mImpacts += $" {t:0.00}:{fall:0.00}";
            mWasTouching = true;
            var bounceFrom = Mathf.Sqrt(2 * g * 0.01f * sheetSize);
            for (int it = 0; it < 8; it++)
                for (int i = 0; i < mContacts.Count; i++)
                {
                    var ct = mContacts[i];
                    var r = ct.R;
                    var perp = new Vector2(-r.y, r.x);
                    var vn = Vector2.Dot(mV + mW * perp, nrm);
                    var rn = r.x * nrm.y - r.y * nrm.x;
                    var kn = 1 + rn * rn / mI;
                    var bias = ct.Vn0 < -bounceFrom ? -Bounce * (1 - settle / Mathf.Max(1e-3f, SettleRate)) * ct.Vn0 : 0;
                    var pn = Mathf.Max(0, ct.Pn + (bias - vn) / kn);
                    var dPn = pn - ct.Pn;
                    ct.Pn = pn;
                    mV += nrm * dPn;
                    mW += rn * dPn / mI;
                    var vt = Vector2.Dot(mV + mW * perp, tan);
                    var rt = r.x * tan.y - r.y * tan.x;
                    var kt = 1 + rt * rt / mI;
                    var pt = Mathf.Clamp(ct.Pt - vt / kt, -Friction * ct.Pn, Friction * ct.Pn);
                    var dPt = pt - ct.Pt;
                    ct.Pt = pt;
                    mV += tan * dPt;
                    mW += rt * dPt / mI;
                    mContacts[i] = ct;
                }
            // Where the ground holds it, along the ground from its centre of mass, how high that centre stands over it,
            // and the ground's push this step.
            float lo = float.MaxValue, hi = float.MinValue, press = 0, rise = 0;
            for (int i = 0; i < mContacts.Count; i++)
            {
                var o = Vector2.Dot(mContacts[i].R, tan);
                if (o < lo) mPivotLo = mContacts[i].R;
                if (o > hi) mPivotHi = mContacts[i].R;
                lo = Mathf.Min(lo, o);
                hi = Mathf.Max(hi, o);
                rise -= Vector2.Dot(mContacts[i].R, nrm) / mContacts.Count;
                press += mContacts[i].Pn;
            }
            // Rolling resistance: the lumpy wad's spin loses a lever of `reach` times the ground's push (the soft, lumpy
            // patch it rolls on); it never turns the spin back.
            var lever = RollResist * Mathf.Sqrt(mI);
            var reach = lever * GripNow(t);
            var dw = reach * press / mI;
            mW = Mathf.Abs(mW) <= dw ? 0 : mW - Mathf.Sign(mW) * dw;
            // Toppling (`ToppleFree`): its centre of mass past the end of what it stands on (beyond the lever), so
            // gravity tips it over that edge; no lump comes down while it pivots, so neither the lumps' damping nor the
            // settling holds it back (else it creeps over the edge for a second).
            var toppling = ToppleFree && (lo >= reach || hi <= -reach);
            if (!toppling) mW *= Mathf.Exp(-RollDamp * dt);
            // Settling: the slide and spin bled off a share a second (the bounce above softened the same way).
            if (settle > 0 && !toppling)
            {
                var keep = Mathf.Exp(-settle * dt);
                mW *= keep;
                mV -= tan * (Vector2.Dot(mV, tan) * (1 - keep));
            }
            // The settling's stop (`SettleStop`): the whole motion bled, more each moment toward the stop.
            var stop = StopNow(t);
            if (stop > 0)
            {
                var keep = Mathf.Exp(-stop * dt);
                mW *= keep;
                mV *= keep;
            }
            // A tip: a support narrower than the plain lever each way, its centre of mass over it: the sleep check tips
            // it off (`Kick`), never this step's forces, so a rocking wad gets no push. Held: otherwise its centre of
            // mass over its support or within the rolling resistance's lever of it (the settling's grip widens that
            // lever, never the tip test).
            mOnTip = hi - lo < 2 * lever;
            mOverTip = mOnTip && lo < lever && hi > -lever;
            mHeld = !mOverTip && lo < reach && hi > -reach;
            mSupLo = lo;
            mSupHi = hi;
            mRise = rise;
            const float slop = 0.5f;
            if (deepest > slop) mX += nrm * (deepest - slop);
            // The landing: the first touch as hard as a fall of `LandFall` of the sheet (the blow's hop is less).
            if (!landedOnce && hardest > Mathf.Sqrt(2 * g * LandFall * sheetSize))
            {
                landedOnce = true;
                mLandAt = t;
                WorldNow();
                var (mid, width, _) = Packet();
                landed?.Invoke(mid.x, width);
            }
        }

        /// <summary>The shape placed by the body: its centre of mass at `mX`, turned `mPhi` in the picture's plane.</summary>
        void WorldNow()
        {
            float c = Mathf.Cos(mPhi), s = Mathf.Sin(mPhi);
            for (int v = 0; v < vCount; v++)
            {
                var l = mLocal[v];
                float x = l.x - mCom.x, y = l.y - mCom.y;
                pos[v] = prev[v] = new Vector3(mX.x + c * x - s * y, mX.y + s * x + c * y, l.z);
            }
        }

        /// <summary>
        /// The crumple's shadow: the figure's soft one fades out as a flat cut-paper ellipse (a soft blob vanishes on the
        /// stages' dark ground) fades in, both following the paper along the ground, narrowing with it, paling as it rises.
        /// </summary>
        void CrumpleShadow(float width, float rise)
        {
            if (flatShadowSr == null)
            {
                var go = new GameObject("paper shadow");
                go.transform.SetParent(shadow.parent, false);
                flatShadowSr = go.AddComponent<SpriteRenderer>();
                flatShadowSr.sprite = Art.Circle;
                if (quadRenderer != null)
                {
                    flatShadowSr.sortingLayerID = quadRenderer.sortingLayerID;
                    flatShadowSr.sortingOrder = quadRenderer.sortingOrder - 1;
                }
                flatShadowSr.color = new Color(0, 0, 0, 0);
            }
            var f = Mathf.Clamp01(mTime / ShadowFade);
            var narrow = width / startWidthPx;
            shadow.localScale = new Vector3(shadowWidth * Mathf.Clamp(narrow, 0.3f, 1) * (1 - 0.3f * rise), shadowHeight, 1);
            if (shadowSr != null)
            {
                var col = shadowSr.color;
                col.a = shadowAlpha * (1 - f) * (1 - 0.6f * rise);
                shadowSr.color = col;
            }
            var ft = flatShadowSr.transform;
            ft.localPosition = shadow.localPosition;
            ft.localRotation = shadow.localRotation;
            ft.localScale = new Vector3(shadowWidth * Mathf.Clamp(narrow, 0.2f, 1) * 1.57f * (1 - 0.3f * rise), shadowHeight * 0.6f, 1);
            flatShadowA = Mathf.Max(shadowAlpha, 0.7f) * (1 - 0.6f * rise);
            flatShadowSr.color = new Color(0, 0, 0, flatShadowA * f);
        }

        /// <summary>The points that may touch the ground in this frame's steps: those drawn within a reach of the
        /// lowest (it turns and falls a little in a frame; the rest can't get there).</summary>
        void NearGround()
        {
            float c = Mathf.Cos(mPhi), s = Mathf.Sin(mPhi);
            var nrm = upC;
            var low = float.MaxValue;
            if (mHeights == null || mHeights.Length < mHull.Count) mHeights = new float[mHull.Count];
            for (int i = 0; i < mHull.Count; i++)
            {
                var h = mHull[i];
                var y = (c * h.x - s * h.y) * nrm.x + (s * h.x + c * h.y) * nrm.y - mDrop[i] * nrm.y;
                mHeights[i] = y;
                if (y < low) low = y;
            }
            var reach = low + 0.15f * sheetSize + 0.05f * mV.magnitude;
            mCand.Clear();
            for (int i = 0; i < mHull.Count; i++)
                if (mHeights[i] < reach) mCand.Add(i);
        }

        float[] mHeights;

        /// <summary>Every triangle's normal (the crumple needs no hinge angles: its lines come from the normals).</summary>
        void NormalsNow()
        {
            for (int t = 0; t < tCount; t++)
            {
                var a = pos[tri[t * 3]];
                var n = Vector3.Cross(pos[tri[t * 3 + 1]] - a, pos[tri[t * 3 + 2]] - a);
                var m = n.magnitude;
                normal[t] = m > 1e-12f ? n / m : Vector3.forward;
            }
        }

        static readonly float LineCosFrom = Mathf.Cos(14 * Mathf.Deg2Rad), LineCosTo = Mathf.Cos(40 * Mathf.Deg2Rad);

        /// <summary>
        /// <see cref="Draw"/> for the crumple, cheaper: each point projected once (not once per triangle corner), the
        /// fold lines from the two faces' normals (no angles), the packet's box from the same pass.
        /// </summary>
        void DrawCrumple()
        {
            var n = vCount;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            float zSum = 0, low = float.MaxValue;
            for (int v = 0; v < n; v++)
            {
                var p = pos[v];
                if (p.x < mn.x) mn.x = p.x;
                if (p.y < mn.y) mn.y = p.y;
                if (p.x > mx.x) mx.x = p.x;
                if (p.y > mx.y) mx.y = p.y;
                zSum += p.z;
                if (inked[v])
                {
                    var u = p.x * upC.x + p.y * upC.y;
                    if (u < low) low = u;
                }
            }
            var c = (mn + mx) / 2;
            var zPanel = zSum / Mathf.Max(1, n);
            var eye = Eye * sheetSize;
            var lz = Mathf.Max(0.2f, lightC.z);
            Vector2 pmn = new Vector2(float.MaxValue, float.MaxValue), pmx = new Vector2(float.MinValue, float.MinValue);
            for (int v = 0; v < n; v++)
            {
                var p = pos[v];
                var k = eye / Mathf.Max(0.2f * eye, eye - p.z);
                var px = c.x + (p.x - c.x) * k;
                var py = c.y + (p.y - c.y) * k - Tilt * p.z;
                mProj[v] = new Vector3(px, py, p.z);
                if (px < pmn.x) pmn.x = px;
                if (py < pmn.y) pmn.y = py;
                if (px > pmx.x) pmx.x = px;
                if (py > pmx.y) pmx.y = py;
                // Its shadow: moved along the light by its height above the middle, a little behind it.
                var hgt = Mathf.Max(0, p.z - zPanel);
                var qx = p.x - lightC.x * hgt / lz;
                var qy = p.y - lightC.y * hgt / lz;
                mSProj[v] = new Vector3(c.x + (qx - c.x) * k, c.y + (qy - c.y) * k - Tilt * p.z, p.z - 0.004f * sheetSize - 0.02f * hgt);
            }
            for (int t = 0; t < tCount; t++)
            {
                var nt = normal[t];
                var w = Vector3.zero;
                for (int j = 0; j < 3; j++)
                {
                    var i = t * 3 + j;
                    var v = tri[i];
                    rv[i] = mProj[v];
                    sv[i] = mSProj[v];
                    rWorld[i] = pos[v];
                    rNormal[i] = nt;
                    var hi = triHinge[i];
                    if (hi < 0) continue;
                    var d = Vector3.Dot(normal[hinges[hi].T1], normal[hinges[hi].T2]);
                    var x = Mathf.Clamp01((d - LineCosFrom) / (LineCosTo - LineCosFrom));
                    w[j] = x * x * (3 - 2 * x);
                }
                rLines[t * 3] = rLines[t * 3 + 1] = rLines[t * 3 + 2] = w;
            }
            paperMesh.vertices = rv;
            paperMesh.normals = rNormal;
            paperMesh.SetUVs(1, rWorld);
            paperMesh.SetUVs(3, rLines);
            paperMesh.RecalculateBounds();
            shadowMesh.vertices = sv;
            shadowMesh.RecalculateBounds();
            var zc = 4f * Mathf.Max(FW, FH);
            cmd.Clear();
            cmd.SetRenderTarget(solidRt);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(Matrix4x4.Translate(new Vector3(0, 0, -zc)),
                Matrix4x4.Ortho(c.x - FW / 2f, c.x + FW / 2f, c.y - FH / 2f, c.y + FH / 2f, 1, 2 * zc));
            if ((Passes & 1) != 0) cmd.DrawMesh(paperMesh, Matrix4x4.identity, solidMat, 0, 1);
            if ((Passes & 2) != 0) cmd.DrawMesh(shadowMesh, Matrix4x4.identity, solidMat, 0, 2);
            Graphics.ExecuteCommandBuffer(cmd);
            quad.transform.localPosition = new Vector3((c.x - origin.x) / canvasPpu, (c.y - origin.y) / canvasPpu, 0);
            if (quadRenderer == null) quadRenderer = quad.GetComponent<Renderer>();
            if (shadow == null) return;
            var a = CanvasToDesign(pmn);
            var b = CanvasToDesign(pmx);
            var mid = (a + b) / 2;
            var width = Mathf.Abs(b.x - a.x);
            if (startWidthPx <= 0) startWidthPx = Mathf.Max(1, width);
            var rise = Mathf.Clamp01((low - groundC) / (0.6f * sheetSize));
            var sp = shadow.position;
            shadow.position = new Vector3(Design.World(mid.x, 0).x, sp.y, sp.z);
            CrumpleShadow(width, rise);
        }

        // --- The build as the fight starts ---

        const int PreNone = 0, PreReading = 1, PreBuilding = 2, PreReady = 3, PreFailed = 4;
        /// <summary>Where the build made as the fight starts is (set by the worker thread).</summary>
        volatile int pre;
        Color32[] preAdd;
        Scan preScan;
        float preMs, preCloseMs;
        string preError;
        System.Diagnostics.Stopwatch preWatch;

        /// <summary>
        /// The crumple's shape built as the fight starts, so the killing blow costs nothing: the seed picked, the clean
        /// picture onto its canvas, read back without stalling (async), then the closing, the scan, the mesh, the folds
        /// and the fold program on a worker thread (in the browser, where there are none, at once).
        /// </summary>
        void StartPrebuild()
        {
            if (!(Solid && Crumple && CrumpleKind == 2 && CrumpleMotion)) return;
            seed = NextSeed != 0 ? NextSeed : rng.Next(1, int.MaxValue);
            NextSeed = 0;
            MakeCanvas();
            pre = PreReading;
            preWatch = System.Diagnostics.Stopwatch.StartNew();
            if (SystemInfo.supportsAsyncGPUReadback) AsyncGPUReadback.Request(solidCanvas, 0, TextureFormat.RGBA32, OnPreRead);
            else StartBuild(ReadCanvas());
        }

        void OnPreRead(AsyncGPUReadbackRequest req)
        {
            if (this == null || pre != PreReading) return;
            if (req.hasError)
            {
                preError = "the canvas read-back failed";
                pre = PreFailed;
                return;
            }
            var px = req.GetData<Color32>().ToArray();
            if (CheckChain)
            {
                // Testing: the async read's rows against ReadPixels's (the scan assumes y up).
                var sync = ReadCanvas();
                int off = 0;
                for (int i = 0; i < px.Length && i < sync.Length; i++)
                    if (Math.Abs(px[i].a - sync[i].a) > 8) off++;
                Debug.Log($"drakepg paper read-back check: {px.Length} pixels async, {sync.Length} sync, {off} differ in alpha");
            }
            StartBuild(px);
        }

        void StartBuild(Color32[] px)
        {
            pre = PreBuilding;
#if UNITY_WEBGL && !UNITY_EDITOR
            BuildOff(px);
#else
            Task.Run(() => BuildOff(px));
#endif
        }

        /// <summary>The build (no Unity calls: a worker thread runs it). Any failure is kept for the death to log.</summary>
        void BuildOff(Color32[] px)
        {
            try
            {
                var w = System.Diagnostics.Stopwatch.StartNew();
                var srng = new System.Random(seed);
                preAdd = CloseAdd(px, FW, FH, H);
                preCloseMs = (float)w.Elapsed.TotalMilliseconds;
                preScan = ScanPixels(px, FW, FH);
                BuildShape(preScan, srng);
                preMs = (float)w.Elapsed.TotalMilliseconds;
                pre = PreReady;
            }
            catch (Exception e)
            {
                preError = e.ToString();
                pre = PreFailed;
            }
        }

        /// <summary>
        /// Each frame (main thread), once the build is ready: what the death needs that the build alone decides, one
        /// piece a frame as the fight runs: the closing's texture, the scene (hidden), the meshes' buffers, the motion's
        /// arrays, then a rehearsal (the start, a few steps, a draw into the hidden texture) so the code and the shader
        /// are warm. The death then only shows them; a fight that ends first makes the rest at the death.
        /// </summary>
        void PrebuildTick()
        {
            if (pre != PreReady || folding || preStage > 6) return;
            var w = System.Diagnostics.Stopwatch.StartNew();
            switch (preStage)
            {
                case 0:
                    if (preAdd != null && closeTex == null) MakeCloseTex(preAdd);
                    break;
                case 1:
                    PrepareScene();
                    solidMat.SetTexture(CloseTexId, closeTex);
                    break;
                case 2:
                    SetupBuffers();
                    break;
                case 3:
                    PrepareMotion();
                    break;
                case 4:
                    SetupScene(preScan, Design.FromWorld(body.transform.position).y);
                    InitMotion();
                    break;
                case 5:
                    StepMotion(4 * MotionStep, null);
                    break;
                case 6:
                    DrawCrumple();
                    // As before the death (the death's SetupScene and InitMotion set the rest again).
                    crumpleMoving = false;
                    break;
            }
            preStageMs[preStage] = (float)w.Elapsed.TotalMilliseconds;
            preStage++;
            if (preStage > 6)
                Debug.Log($"drakepg: paper crumple built as the fight started: seed {seed}, {vCount} points, {preMs:0} ms on a worker thread, "
                    + $"{preWatch.Elapsed.TotalMilliseconds:0} ms from the start; then a piece a frame (ms): closing {preStageMs[0]:0.0}, scene {preStageMs[1]:0.0}, "
                    + $"buffers {preStageMs[2]:0.0}, motion {preStageMs[3]:0.0}, rehearsal: start {preStageMs[4]:0.0}, steps {preStageMs[5]:0.0}, draw {preStageMs[6]:0.0}");
        }

        int preStage;
        readonly float[] preStageMs = new float[7];

        void MakeCloseTex(Color32[] add)
        {
            closeTex = new Texture2D(FW, FH, TextureFormat.RGBA32, true)
            {
                name = "paper closing", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp,
            };
            closeTex.SetPixels32(add);
            closeTex.Apply(true);
        }

        void DestroyCrumple()
        {
            if (closeTex != null) Destroy(closeTex);
            if (flatShadowSr != null) Destroy(flatShadowSr.gameObject);
        }
    }
}
