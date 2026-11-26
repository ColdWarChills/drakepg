using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// Water, ice and energy on the paper (docs/PLANS.md step 1d; the mock's soak, frost and bolts): water soaks it dark
    /// from the top down and it dries over a few turns; ice frosts it pale, and the next blow while it is frosted snaps a
    /// piece off; energy rips it where it lands. Fire on wet paper only steams it, fire on frost melts it to wet. Looks
    /// only: no rule reads any of it (a snap does no damage). Drawn by Fx/Paper.cginc on the living picture and baked into
    /// the death, so the crumple keeps it. Eases on Anim time (x2 battle speed, a boss's slow motion).
    /// </summary>
    public partial class Paper
    {
        const int MaxRips = 3, MaxSnaps = 2;
        static readonly int MoreId = Shader.PropertyToID("_PaperMore"), WetId = Shader.PropertyToID("_PaperWet"), FrostId = Shader.PropertyToID("_PaperFrost");
        static readonly int RipsId = Shader.PropertyToID("_PaperRips"), RipInfoId = Shader.PropertyToID("_PaperRipInfo");
        static readonly int SnapsId = Shader.PropertyToID("_PaperSnaps"), SnapInfoId = Shader.PropertyToID("_PaperSnapInfo");

        /// <summary>Soaked: how far down it has soaked (0-1) and where that is going; the ripples drying leaves.</summary>
        float soak, soakTo, cockle, cockleTo;
        /// <summary>Frost now and where it is going; when (`clock`) it was last frosted.</summary>
        float frost, frostTo, frostAt = -99;
        /// <summary>Rips: ends (xy, zw) and info (open, seed, flap side, half width); each one's opening, target 1.</summary>
        readonly Vector4[] rips = new Vector4[MaxRips], ripInfo = new Vector4[MaxRips];
        int ripCount;
        float ripAt = -99;
        /// <summary>Snapped pieces: a point on the break and the normal toward the piece; info (broken off, seed).</summary>
        readonly Vector4[] snaps = new Vector4[MaxSnaps], snapInfo = new Vector4[MaxSnaps];
        int snapCount;
        float wetSeed, frostSeed;
        /// <summary>Seconds of battle time (Anim) since the paper was made.</summary>
        float clock;
        bool elementsOn;
        /// <summary>Seconds (Anim) to the next drip; the picture's lower edges (sheet space, from the scan) it drips from.</summary>
        float dripIn = 0.3f;
        List<Vector2> dripFrom;
        /// <summary>The picture's own color (a hit's white flash swaps it for a moment).</summary>
        Color pictureColor = Color.white;
        /// <summary>Snapped pieces still falling (each goes when it lands).</summary>
        readonly List<Transform> pieces = new List<Transform>();

        /// <summary>The CPU time the last blow's paper work took, ms (logged with each element hit).</summary>
        public static float LastHitMs;
        /// <summary>The last snap's parts, ms: where to break, the falling piece, its flakes.</summary>
        public static string LastSnapLaps = "";

        void InitElements()
        {
            pictureColor = body.color;
            wetSeed = R() * 50;
            frostSeed = R() * 50;
        }

        /// <summary>What the paper does to its look values each frame (Anim time); true when something changed.</summary>
        bool TickElements(float dt)
        {
            clock += dt;
            if (!elementsOn) return false;
            var changed = false;
            // It soaks in fast and dries slowly; frost comes quickly and fades a little slower.
            changed |= Approach(ref soak, soakTo, soakTo > soak ? 2.4f : 0.6f, dt);
            changed |= Approach(ref cockle, cockleTo, cockleTo > cockle ? 0.8f : 0.25f, dt);
            changed |= Approach(ref frost, frostTo, frostTo > frost ? 3.5f : 1.2f, dt);
            for (int i = 0; i < ripCount; i++)
            {
                var o = ripInfo[i].x;
                if (o >= 1) continue;
                // Rips open in about a tenth of a second.
                ripInfo[i].x = Mathf.Min(1, o + dt * 9);
                changed = true;
            }
            // Wet paper hangs limp and frosted paper stands stiff (Bend.Droop / Stiff); soaked paper drips.
            if (bend != null)
            {
                bend.Droop = Mathf.Clamp01((soak - 0.3f) / 0.5f);
                bend.Stiff = Mathf.Clamp01((frost - 0.2f) / 0.5f);
            }
            if (soak > 0.45f && (dripIn -= dt) <= 0)
            {
                Drip();
                dripIn = Mathf.Lerp(0.5f, 0.14f, (soak - 0.45f) / 0.55f) * Random.Range(0.6f, 1.4f);
            }
            return changed;
        }

        static bool Approach(ref float v, float to, float rate, float dt)
        {
            if (Mathf.Approximately(v, to)) return false;
            v = Mathf.MoveTowards(v, to, rate * dt);
            return true;
        }

        /// <summary>The look values onto a property block (the living picture).</summary>
        void PushElements(MaterialPropertyBlock b)
        {
            b.SetVector(MoreId, new Vector4(ripCount, snapCount, 0, size.y));
            b.SetVector(WetId, new Vector4(soak, 1, cockle, wetSeed));
            b.SetVector(FrostId, new Vector4(frost, frostSeed, 0, 0));
            b.SetVectorArray(RipsId, rips);
            b.SetVectorArray(RipInfoId, ripInfo);
            b.SetVectorArray(SnapsId, snaps);
            b.SetVectorArray(SnapInfoId, snapInfo);
        }

        /// <summary>The same onto the death's material, at where each was going (a killing water blow leaves a wet wad,
        /// a rip opened by it is open).</summary>
        void SetElements(Material m)
        {
            m.SetVector(MoreId, new Vector4(ripCount, snapCount, 0, size.y));
            m.SetVector(WetId, new Vector4(Mathf.Max(soak, soakTo), 1, Mathf.Max(cockle, cockleTo), wetSeed));
            m.SetVector(FrostId, new Vector4(Mathf.Max(frost, frostTo), frostSeed, 0, 0));
            var open = new Vector4[MaxRips];
            for (int i = 0; i < MaxRips; i++) open[i] = i < ripCount ? new Vector4(1, ripInfo[i].y, ripInfo[i].z, ripInfo[i].w) : ripInfo[i];
            m.SetVectorArray(RipsId, rips);
            m.SetVectorArray(RipInfoId, open);
            m.SetVectorArray(SnapsId, snaps);
            m.SetVectorArray(SnapInfoId, snapInfo);
        }

        /// <summary>
        /// What a blow's element does to the paper (`at`: where it landed, sheet space). Returns what the screen should
        /// show for it ("steam", "melt", "snap") or null; `burn` is false when fire must not burn a hole (it steams or melts).
        /// </summary>
        string Elements(string element, Vector2 at, float weight, out bool burn)
        {
            burn = element == "fire";
            // Frosted by an earlier move (not this one's own first hits); where the frost is going, so a later round's
            // blow snaps it however long its wind-up took.
            var frozen = frostTo > 0.45f && clock - frostAt > 0.45f;
            switch (element)
            {
                case "water":
                    elementsOn = true;
                    soakTo = 1;
                    cockleTo = 0;
                    // Water puts burns out: they stop eating outward and stop glowing.
                    for (int i = 0; i < holeCount; i++)
                    {
                        holeSize[i] = Mathf.Min(holeSize[i], Mathf.Max(holes[i].z, 0.001f));
                        holes[i].w = 0;
                        holeAge[i] = 2.01f;
                    }
                    return frozen ? Snap(at) : null;
                case "ice":
                    elementsOn = true;
                    if (frost < 0.5f) frostAt = clock;
                    frostTo = 1;
                    if (soakTo > 0.3f) soakTo = 0.3f;
                    return null;
                case "fire":
                    if (frost > 0.3f || frostTo > 0.3f)
                    {
                        // Fire on frost melts it: wet paper, no hole.
                        frostTo = 0;
                        soakTo = Mathf.Max(soakTo, 0.7f);
                        burn = false;
                        return "melt";
                    }
                    if (soak > 0.45f || soakTo > 0.45f)
                    {
                        // Fire on wet paper only steams it dry (wavy where it was wet).
                        soakTo = 0;
                        cockleTo = 1;
                        burn = false;
                        return "steam";
                    }
                    return null;
                case "energy":
                    elementsOn = true;
                    if (frozen) return Snap(at);
                    Rip(at, weight);
                    return null;
            }
            return frozen ? Snap(at) : null;
        }

        /// <summary>
        /// The start of the monster's next turn: wet paper dries a third of the way (gone in three turns, wavy as it
        /// goes), frost fades over three (still frozen enough to snap on the next round).
        /// </summary>
        public void Turn()
        {
            if (folding || !elementsOn) return;
            if (soakTo > 0)
            {
                soakTo = Mathf.Max(0, soakTo - 0.34f);
                cockleTo = Mathf.Max(cockleTo, soakTo > 0 ? 0.6f : 1);
            }
            else if (cockleTo > 0) cockleTo = Mathf.Max(0, cockleTo - 0.5f);
            if (frostTo > 0) frostTo = Mathf.Max(0, frostTo - 0.4f);
        }

        // --- Where on the picture ---

        /// <summary>Whether sheet point `s` is on the picture (the scan the crumple's build read; assumed until it has).</summary>
        bool OnPaper(Vector2 s)
        {
            if (pre != PreReady || preScan.Solid == null || W <= 0 || H <= 0) return s.x >= 0 && s.x <= aspect && s.y >= 0 && s.y <= 1;
            int ix = (int)((pad + s.x / aspect * W) / preScan.FX), iy = (int)((pad + s.y * H) / preScan.FY);
            if (ix < 0 || iy < 0 || ix >= preScan.SW || iy >= preScan.SH) return false;
            return preScan.Solid[iy * preScan.SW + ix];
        }

        /// <summary>A sheet point in the world (the inverse of ToSheet).</summary>
        Vector3 FromSheet(Vector2 s) => body.transform.TransformPoint(new Vector3((s.x * size.y - pivot.x) / ppu, (s.y * size.y - pivot.y) / ppu, 0));

        /// <summary>
        /// A rip where an energy blow landed: through a spot on the picture near it, running within about 35 degrees of
        /// upright (along the paper's height), kept below the face (a jagged gap across it reads as a grin). The hits of
        /// one move (a chain) land within moments of each other: they tear the last rip longer and wider instead.
        /// </summary>
        void Rip(Vector2 at, float weight)
        {
            if (ripCount > 0 && clock - ripAt < 0.6f)
            {
                var j = ripCount - 1;
                var mid = ((Vector2)rips[j] + new Vector2(rips[j].z, rips[j].w)) / 2;
                var half = (new Vector2(rips[j].z, rips[j].w) - mid) * 1.18f;
                var a2 = mid - half;
                var b2 = mid + half;
                if (b2.y > 0.74f) b2 = mid + half * ((0.74f - mid.y) / Mathf.Max(1e-3f, half.y));
                rips[j] = new Vector4(a2.x, a2.y, b2.x, b2.y);
                ripInfo[j].w = Mathf.Min(ripInfo[j].w * 1.2f, 0.05f);
                ripAt = clock;
                return;
            }
            var hit = new Vector2(Mathf.Clamp(at.x, 0, aspect), Mathf.Clamp01(at.y));
            Vector2 bestA = default, bestB = default;
            var best = -1;
            for (int tries = 0; tries < 10; tries++)
            {
                var c = new Vector2(Mathf.Lerp(hit.x, aspect / 2, 0.35f) + (R() - 0.5f) * 0.2f * aspect, Mathf.Clamp(Mathf.Lerp(hit.y, 0.45f, 0.4f) + (R() - 0.5f) * 0.14f, 0.22f, 0.58f));
                var ang = Mathf.PI / 2 + (R() - 0.5f) * 1.2f;
                var len = 0.2f + 0.08f * R() + 0.06f * Mathf.Clamp01(weight);
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (len / 2);
                Vector2 a = c - d, b = c + d;
                if (b.y < a.y) (a, b) = (b, a);
                if (b.y > 0.74f)
                {
                    a.y -= b.y - 0.74f;
                    b.y = 0.74f;
                }
                // Most of its length on the picture: five points along it.
                var score = 0;
                for (int k = 0; k <= 4; k++)
                    if (OnPaper(Vector2.Lerp(a, b, k / 4f))) score++;
                if (score > best)
                {
                    best = score;
                    bestA = a;
                    bestB = b;
                }
                if (score == 5) break;
            }
            if (ripCount == MaxRips)
            {
                for (int i = 1; i < MaxRips; i++)
                {
                    rips[i - 1] = rips[i];
                    ripInfo[i - 1] = ripInfo[i];
                }
                ripCount--;
            }
            rips[ripCount] = new Vector4(bestA.x, bestA.y, bestB.x, bestB.y);
            ripInfo[ripCount] = new Vector4(0.001f, R() * 40, R() < 0.5f ? -1 : 1, 0.026f + 0.016f * Mathf.Clamp01(weight * 2));
            ripCount++;
            ripAt = clock;
        }

        /// <summary>
        /// Frozen paper is brittle: a piece snaps off. The break cuts off about a tenth of the picture from one side
        /// (its back or a lower corner; never the face, at the top of its front, nor both feet), straight across with sharp
        /// kinks. Two at most a fight, so a long fight leaves a picture that still reads; frost thins after it.
        /// </summary>
        string Snap(Vector2 at)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            frostTo = Mathf.Min(frostTo, 0.35f);
            frost = Mathf.Min(frost, 0.6f);
            if (snapCount >= MaxSnaps) return "scraps";
            // Away from the face: the back (-x: the art faces +x), the back's upper and lower corners, the front's lower
            // corner. Not straight across the feet: with both feet gone it floats over its shadow.
            var dirs = new[] { new Vector2(-1, 0), new Vector2(-1, 0.8f), new Vector2(-1, -0.7f), new Vector2(1, -0.8f) };
            var n = dirs[(int)(R() * dirs.Length) % dirs.Length].normalized;
            // The cut where about 10% of the picture lies past it (the scan's cells, else the sheet's corners).
            float cut;
            var centre = Vector2.zero;
            if (pre == PreReady && preScan.Pts != null && preScan.Pts.Count > 20)
            {
                var proj = new float[preScan.Pts.Count];
                for (int i = 0; i < proj.Length; i++)
                {
                    var q = preScan.Pts[i];
                    var s = new Vector2((q.x - pad) / W * aspect, (q.y - pad) / H);
                    proj[i] = Vector2.Dot(s, n);
                }
                // The 87-92th percentile from a histogram (sorting thousands of points cost about 2 ms in the blow's frame).
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < proj.Length; i++)
                {
                    lo = Mathf.Min(lo, proj[i]);
                    hi = Mathf.Max(hi, proj[i]);
                }
                const int Bins = 512;
                var hist = new int[Bins];
                var span = Mathf.Max(hi - lo, 1e-5f);
                for (int i = 0; i < proj.Length; i++) hist[Mathf.Min(Bins - 1, (int)((proj[i] - lo) / span * Bins))]++;
                var want = (int)(proj.Length * (0.87f + 0.05f * R()));
                int b = 0;
                for (int acc = 0; b < Bins - 1; b++)
                    if ((acc += hist[b]) > want) break;
                cut = lo + (b + 0.5f) / Bins * span;
                // The piece's middle: the scan's points past the cut.
                var count = 0;
                for (int i = 0; i < proj.Length; i++)
                {
                    if (proj[i] < cut) continue;
                    var q = preScan.Pts[i];
                    centre += new Vector2((q.x - pad) / W * aspect, (q.y - pad) / H);
                    count++;
                }
                centre = count > 0 ? centre / count : Vector2.zero;
            }
            else cut = Vector2.Dot(new Vector2(n.x > 0 ? aspect : 0, n.y > 0 ? 1 : 0), n) - 0.12f;
            var mid = new Vector2(aspect / 2, 0.5f);
            var onLine = mid + n * (cut - Vector2.Dot(mid, n));
            if (centre == Vector2.zero) centre = onLine + n * 0.06f;
            // Where the break crosses the picture, for the flakes: the piece's middle brought back onto the break.
            var onBreak = centre - n * (Vector2.Dot(centre, n) - cut);
            snaps[snapCount] = new Vector4(onLine.x, onLine.y, n.x, n.y);
            snapInfo[snapCount] = new Vector4(1, R() * 40, 0, 0);
            snapCount++;
            var cutMs = watch.Elapsed.TotalMilliseconds;
            DropPiece(snapCount - 1, centre, n, onBreak);
            LastSnapLaps = $"break {cutMs:0.00}, piece {pieceMs:0.00}, flakes {flakesMs:0.00} ms";
            return "snap";
        }

        /// <summary>A drop of water off a lower edge of the picture where it is wet (below the wet front).</summary>
        void Drip()
        {
            if (dripFrom == null && pre == PreReady && preScan.Solid != null) dripFrom = LowerEdges();
            // The wet front as Paper.cginc draws it (without its wander).
            var front = 1.04f - soak * 1.14f;
            var s = new Vector2(aspect * Random.Range(0.25f, 0.75f), Random.Range(0.15f, 0.35f));
            if (dripFrom != null && dripFrom.Count > 0)
            {
                var found = false;
                for (int k = 0; k < 6 && !found; k++)
                {
                    s = dripFrom[Random.Range(0, dripFrom.Count)];
                    found = s.y > front;
                }
                if (!found) return;
            }
            var d = Design.FromWorld(FromSheet(s));
            var floor = Design.FromWorld(FromSheet(new Vector2(s.x, 0))).y;
            Fx.PaperDrip(null, d.x, d.y, floor, Design.Depth(floor) - 60);
        }

        /// <summary>The bottoms of the picture's parts (a scan cell with none under it), above the feet.</summary>
        List<Vector2> LowerEdges()
        {
            var list = new List<Vector2>();
            var sc = preScan;
            for (int iy = 1; iy < sc.SH; iy++)
                for (int ix = 0; ix < sc.SW; ix++)
                {
                    if (!sc.Solid[iy * sc.SW + ix] || sc.Solid[(iy - 1) * sc.SW + ix]) continue;
                    var s = new Vector2(((ix + 0.5f) * sc.FX - pad) / W * aspect, (iy * sc.FY - pad) / H);
                    if (s.y > 0.1f) list.Add(s);
                }
            return list;
        }

        /// <summary>
        /// Snapped piece `i` (its middle at sheet `centre`, `n` toward it) breaks free and falls: a copy of the picture
        /// showing only that piece, jolted off the break with a burst of white flakes, tumbling outward as it drops and
        /// shattering into flakes where it lands. Its own object on the stage, gone when it lands (or with the screen).
        /// </summary>
        double pieceMs, flakesMs;

        void DropPiece(int i, Vector2 centre, Vector2 n, Vector2 onBreak)
        {
            var lap = System.Diagnostics.Stopwatch.StartNew();
            pieceMs = flakesMs = 0;
            var stage = GameRoot.I != null && GameRoot.I.Screen != null ? GameRoot.I.Screen.World : null;
            if (stage == null || body == null) return;
            var holder = new GameObject("paper piece").transform;
            holder.SetParent(stage, false);
            var c0 = FromSheet(centre);
            holder.position = c0;
            var pic = new GameObject("picture").transform;
            pic.SetParent(body.transform.parent, false);
            pic.localPosition = body.transform.localPosition;
            pic.localRotation = body.transform.localRotation;
            pic.localScale = body.transform.localScale;
            pic.SetParent(holder, true);
            var r = pic.gameObject.AddComponent<SpriteRenderer>();
            r.sprite = body.sprite;
            // The paper material and color, not a hit flash's (the blow that snaps it flashes the figure white).
            r.sharedMaterial = live;
            r.color = pictureColor;
            r.flipX = body.flipX;
            r.flipY = body.flipY;
            r.sortingLayerID = body.sortingLayerID;
            r.sortingOrder = body.sortingOrder + 1;
            var pb = new MaterialPropertyBlock();
            body.GetPropertyBlock(pb);
            pb.SetFloat("_Fill", 0);
            pb.SetVector(MoreId, new Vector4(ripCount, snapCount, i + 1, size.y));
            pb.SetVectorArray(SnapsId, snaps);
            pb.SetVectorArray(SnapInfoId, snapInfo);
            r.SetPropertyBlock(pb);
            pieces.RemoveAll(p => p == null);
            pieces.Add(holder);
            // In world units a sheet height: thrown out off the break and up a little, then falling.
            var unit = (FromSheet(centre + Vector2.up) - c0).magnitude;
            var outward = (Vector3)((Vector2)(FromSheet(centre + n * 0.1f) - c0)).normalized;
            var floorY = FromSheet(new Vector2(centre.x, 0)).y;
            var vel = new Vector3(outward.x * 0.6f, 0.3f + Mathf.Max(0, outward.y) * 0.4f, 0) * unit;
            var spin = (outward.x >= 0 ? -1 : 1) * Random.Range(80f, 160f);
            var pos = c0 + outward * unit * 0.02f;
            var breakAt = Design.FromWorld(FromSheet(onBreak));
            var floorDesign = Design.FromWorld(new Vector3(c0.x, floorY, 0)).y;
            pieceMs = lap.Elapsed.TotalMilliseconds;
            Fx.FrostScraps(null, breakAt.x, breakAt.y, 0.7f, floorDesign, Design.Depth(floorDesign) - 60);
            flakesMs = lap.Elapsed.TotalMilliseconds - pieceMs;
            float last = 0, angle = 0, landedAt = -1;
            const float step = 1 / 120f;
            var tint = r.color;
            Anim.Float(0, 1.6f, 1.6f, time =>
            {
                if (holder == null) return;
                if (landedAt < 0)
                {
                    while (last + step <= time)
                    {
                        last += step;
                        vel.y -= 3.4f * unit * step;
                        pos += vel * step;
                        angle += spin * step;
                        if (pos.y < floorY + 0.05f * unit)
                        {
                            landedAt = last;
                            var at = Design.FromWorld(pos);
                            Fx.FrostScraps(null, at.x, at.y, 0.9f, floorDesign, Design.Depth(floorDesign) - 60);
                            break;
                        }
                    }
                    holder.position = pos;
                    holder.rotation = Quaternion.Euler(0, 0, angle);
                }
                // Brittle: it breaks up as it lands and is gone in a moment.
                else r.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(1 - (time - landedAt) / 0.08f));
            }, Ease.Linear).OnComplete(() =>
            {
                if (holder != null) Destroy(holder.gameObject);
            });
        }

        /// <summary>Snapped pieces still on the stage (CountFolds: none may be left when a fight ends).</summary>
        public int PiecesLeft
        {
            get
            {
                pieces.RemoveAll(p => p == null);
                return pieces.Count;
            }
        }

        /// <summary>Testing: every eased value at where it is going, now (stills of each state).</summary>
        public void Settle()
        {
            soak = soakTo;
            cockle = cockleTo;
            frost = frostTo;
            for (int i = 0; i < ripCount; i++) ripInfo[i].x = 1;
            Push();
        }

        /// <summary>Testing: a blow of `element` at the monster's chest, as a real hit's paper part (no damage, no effects).</summary>
        public string TestHit(string element, float weight = 0.3f)
        {
            var c = body.bounds.center;
            var d = Design.FromWorld(c);
            return Hit(d.x, d.y, weight, element);
        }

        /// <summary>Testing: wipe the paper's creases, water, frost, rips and snaps (back to clean).</summary>
        public void TestClear()
        {
            creases.Clear();
            soak = soakTo = cockle = cockleTo = frost = frostTo = 0;
            ripCount = snapCount = 0;
            for (int i = 0; i < MaxRips; i++) rips[i] = ripInfo[i] = Vector4.zero;
            for (int i = 0; i < MaxSnaps; i++) snaps[i] = snapInfo[i] = Vector4.zero;
            frostAt = ripAt = -99;
            Push();
        }
    }
}
