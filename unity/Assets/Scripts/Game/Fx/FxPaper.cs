using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drakepg
{
    /// <summary>
    /// What a paper monster sheds instead of blood (World/Paper.cs, the paper damage proof): scraps of red paper flung
    /// out by the blow ("might look fun red paper scraps", the designer). Each one tumbles, showing its pale back as it
    /// turns over, slows in the air and drifts down swaying, then lies flat on the ground until the fight ends. And
    /// embers off a burn.
    /// </summary>
    public static partial class Fx
    {
        static readonly long[] ScrapReds = { 0xc8372d, 0xe0483a, 0xa82a24, 0xd9402f };
        const long ScrapBack = 0xf1b3aa;
        /// <summary>Frozen paper's flakes (a snapped piece breaking up): icy white, a pale blue back.</summary>
        static readonly long[] FrostWhites = { 0xf6f9fc, 0xe4eef7, 0xffffff, 0xd6e5f1 };
        const long FrostBack = 0xbcd3e6;
        /// <summary>Scraps kept at once (the oldest go first).</summary>
        const int MaxScraps = 80;
        static readonly List<Transform> scrapList = new List<Transform>();
        static Sprite[] scrapSprites;

        /// <summary>
        /// A blow's scraps at (x, y). `dir` 1 = the blow travels right (most fly that way). `amount` like Bleed's (0.15 a
        /// scratch to 1 a heavy blow, 2 the last one). They lie down around `floorY`, at sorting order `groundOrder`.
        /// </summary>
        public static void PaperScraps(Transform parent, float x, float y, int dir, float amount, float floorY, int groundOrder)
        {
            var root = Root(parent);
            if (root == null || amount <= 0) return;
            dir = dir < 0 ? -1 : 1;
            var count = Mathf.Clamp(Mathf.RoundToInt(4 + amount * 8), 3, 20);
            for (int i = 0; i < count; i++)
            {
                // Most fly on with the blow; a few spatter back toward the attacker.
                var back = Random.value < 0.2f;
                var vx = dir * Random.Range(80f, 360f + 220 * Mathf.Min(1.5f, amount)) * (back ? -0.5f : 1);
                var vy = -Random.Range(150f, 480f + 120 * Mathf.Min(1.5f, amount));
                Scrap(root, x + Random.Range(-14f, 14f), y + Random.Range(-26f, 26f), vx, vy, Random.Range(0.95f, 1.5f), floorY, groundOrder);
            }
        }

        /// <summary>
        /// White flakes of frozen paper at (x, y): a frosted piece snapping off (or breaking up where it lands). They
        /// burst out all around, smaller than the red scraps, and drift down like them. `amount` 0.3-1.
        /// </summary>
        public static void FrostScraps(Transform parent, float x, float y, float amount, float floorY, int groundOrder)
        {
            var root = Root(parent);
            if (root == null || amount <= 0) return;
            var count = Mathf.Clamp(Mathf.RoundToInt(5 + amount * 9), 4, 16);
            for (int i = 0; i < count; i++)
            {
                var vx = Random.Range(-300f, 300f) * (0.6f + 0.6f * amount);
                var vy = -Random.Range(120f, 380f) * (0.6f + 0.6f * amount);
                Scrap(root, x + Random.Range(-18f, 18f), y + Random.Range(-14f, 14f), vx, vy, Random.Range(0.55f, 1.05f), floorY, groundOrder,
                    FrostWhites, FrostBack);
            }
        }

        /// <summary>A drop of water falling off soaked paper at (x, y), landing around `floorY`.</summary>
        public static void PaperDrip(Transform parent, float x, float y, float floorY, int groundOrder)
        {
            var root = Root(parent);
            if (root == null) return;
            Drop(parent, root, x, y, Random.Range(-8f, 8f), Random.Range(20f, 60f), Random.Range(0.48f, 0.68f), 0x8cc2ea, floorY, groundOrder, 0, 0);
        }

        /// <summary>Steam off wet paper that fire dried (or frost it melted): pale wisps rising from the w x h body around (x, y).</summary>
        public static void PaperSteam(Transform parent, float x, float y, float w, float h)
        {
            Burst(parent, x, y, Tex.Soft, 16, new Emit
            {
                Zone = new Rect(-w / 2, -h / 2, w, h),
                SpeedX = Val.Rand(-25, 25),
                SpeedY = Val.Rand(-130, -55),
                Scale = Val.Life(0.9f, 2.6f),
                Alpha = Val.Life(0.6f, 0),
                Lifespan = Val.Rand(800, 1400),
                Delay = Val.Rand(0, 250),
                Tints = new[] { 0xf4f7f9L, 0xdfe6ec },
            });
        }

        static void Scrap(Transform root, float x, float y, float vx, float vy, float size, float floorY, int groundOrder,
            long[] fronts = null, long backColor = ScrapBack)
        {
            fronts ??= ScrapReds;
            var holder = Holder(root, x, y, "paper scrap");
            var r = Img(holder, ScrapSprite(), 0, 0, fronts[Random.Range(0, fronts.Length)], 1, OrderFx - 1, false);
            var front = r.color;
            var back = Design.Hex(backColor);
            var spin = Random.Range(-600f, 600f);
            var turnRate = Random.Range(7f, 13f) * (Random.value < 0.5f ? -1 : 1);
            var phase = Random.Range(0f, Mathf.PI * 2);
            var sway = Random.Range(40f, 90f) * (Random.value < 0.5f ? -1 : 1);
            var swayRate = Random.Range(4f, 7f);
            // The ground has depth: some land nearer the viewer than others.
            var rest = floorY + Random.Range(-8f, 30f);
            float px = x, py = y, angle = Random.Range(0, 360f), last = 0, landedAt = -1;
            const float step = 1 / 120f, life = 30;
            AngleTo(r, angle);
            Track(holder);
            Anim.Float(0, life, life, time =>
            {
                if (holder == null) return;
                if (landedAt < 0)
                {
                    while (last + step <= time)
                    {
                        last += step;
                        // Paper is light: the throw dies quickly in the air and it drifts down, swaying side to side.
                        vx *= 1 - 3.4f * step;
                        vy *= 1 - 1.6f * step;
                        vy = Mathf.Min(vy + 1100 * step, 190);
                        px += (vx + sway * Mathf.Sin(last * swayRate + phase)) * step;
                        py += vy * step;
                        angle += spin * step;
                        spin *= 1 - 0.8f * step;
                        if (py >= rest)
                        {
                            py = rest;
                            landedAt = last;
                            break;
                        }
                    }
                    holder.localPosition = P(px, py);
                    AngleTo(r, angle);
                    if (landedAt < 0)
                    {
                        // Tumbling: squashed as it turns edge-on, its pale back showing half the time.
                        var c = Mathf.Cos(last * turnRate + phase);
                        ScaleTo(r, size, size * Mathf.Max(0.12f, Mathf.Abs(c)));
                        r.color = c >= 0 ? front : back;
                    }
                    else
                    {
                        // Lying on the ground, seen at a slant; under the fighters.
                        holder.localScale = new Vector3(1, 0.45f, 1);
                        ScaleTo(r, size, size);
                        r.color = Color.Lerp(r.color, Color.black, 0.12f);
                        r.sortingOrder = groundOrder + 2;
                    }
                }
            }, Ease.Linear);
        }

        /// <summary>Keeps the newest scraps; the oldest go.</summary>
        static void Track(Transform scrap)
        {
            scrapList.RemoveAll(s => s == null);
            scrapList.Add(scrap);
            while (scrapList.Count > MaxScraps)
            {
                if (scrapList[0] != null) Object.Destroy(scrapList[0].gameObject);
                scrapList.RemoveAt(0);
            }
        }

        /// <summary>One of a few torn-paper shapes (white, tinted when drawn), drawn the first time.</summary>
        static Sprite ScrapSprite()
        {
            if (scrapSprites == null)
            {
                var shapes = new[]
                {
                    new float[] { 1, 3, 9, 1, 15, 4, 13, 10, 5, 11, 2, 8 },
                    new float[] { 2, 1, 14, 3, 11, 9, 1, 7 },
                    new float[] { 1, 5, 6, 1, 12, 2, 15, 7, 9, 11, 3, 10 },
                    new float[] { 3, 1, 13, 2, 15, 6, 8, 10, 1, 9 },
                };
                scrapSprites = new Sprite[shapes.Length];
                for (int i = 0; i < shapes.Length; i++)
                {
                    var c = new Canvas(0, 0, 16, 12);
                    c.Fill(Inside(c.D, Poly(shapes[i])), 0xffffff, 1);
                    scrapSprites[i] = c.ToSprite("fx_paper_scrap" + i);
                }
            }
            return scrapSprites[Random.Range(0, scrapSprites.Length)];
        }

        /// <summary>Sparks of a burn: orange specks drifting up off a hole fire burnt in a paper monster, and a wisp of smoke.</summary>
        public static void PaperEmbers(Transform parent, float x, float y)
        {
            Burst(parent, x, y, Tex.Spark, 14, new Emit
            {
                SpeedX = Val.Rand(-60, 60),
                SpeedY = Val.Rand(-170, -50),
                X = Val.Rand(-26, 26),
                Y = Val.Rand(-26, 26),
                Scale = Val.Life(0.55f, 0.1f),
                Alpha = Val.Life(1, 0),
                Lifespan = Val.Rand(700, 1300),
                Tints = new[] { 0xffb347L, 0xff7a1aL, 0xffd27aL },
                Add = true,
            });
            Burst(parent, x, y, Tex.Soft, 4, new Emit
            {
                SpeedX = Val.Rand(-20, 20),
                SpeedY = Val.Rand(-80, -40),
                Scale = Val.Life(1.2f, 2.6f),
                Alpha = Val.Life(0.35f, 0),
                Lifespan = 1100,
                Tint = 0x3a3330,
            });
        }
    }
}
