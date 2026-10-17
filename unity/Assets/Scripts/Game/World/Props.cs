using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>Room props drawn from shapes until they get art: the campfire and things to inspect (src/ui/props.ts).</summary>
    public static class Props
    {
        static Vector2 P(float x, float y) => new Vector2(x, y);

        /// <summary>A soft round glow sprite (center x, y in design px), scaled like the web's 32 px fx_soft dot.</summary>
        public static SpriteRenderer Glow(Transform parent, float x, float y, Color color, float sx, float sy, int order)
        {
            var go = new GameObject("glow");
            go.transform.SetParent(parent, false);
            go.transform.position = Design.World(x, y, 1);
            go.transform.localScale = new Vector3(sx / 2, sy / 2, 1);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Art.Soft;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>Stones, crossed logs and a flickering fire with rising embers. Feet (base) at x, y.</summary>
        public static Transform Campfire(Transform parent, float x, float y)
        {
            var root = new GameObject("campfire").transform;
            root.SetParent(parent, false);
            var order = Design.Depth(y);
            var glow = Glow(root, x, y - 30, Design.Hex(0xff8a3d, 0.35f), 7, 5, order - 1);
            var g = Shapes.Create(root, x, y, order);
            g.Ellipse(new Color(0, 0, 0, 0.35f), 0, 4, 130, 26);
            for (int i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.PI * 2;
                g.Ellipse(Design.Hex(i % 2 == 1 ? 0x6e6a66 : 0x85807a), Mathf.Cos(a) * 52, Mathf.Sin(a) * 12 - 4, 26, 16);
            }
            g.Poly(Design.Hex(0x5d3a1f), P(-44, -2), P(36, -18), P(40, -8), P(-40, 8));
            g.Poly(Design.Hex(0x5d3a1f), P(44, -2), P(-36, -18), P(-40, -8), P(40, 8));
            var flame = Shapes.Create(root, x, y - 6, order + 1, "flame");
            flame.Tri(Design.Hex(0xff5a1a), -24, 0, 24, 0, 0, -72);
            flame.Tri(Design.Hex(0xff9f2e), -16, 0, 16, 0, 2, -52);
            flame.Tri(Design.Hex(0xffe27a), -8, 0, 8, 0, 0, -28);
            Anim.Float(0, 1, 0.18f, t => flame.transform.localScale = new Vector3(1 - 0.1f * t, 1 + 0.18f * t, 1), Ease.SineInOut, repeat: -1, yoyo: true);
            Anim.Float(0.35f, 0.5f, 0.42f, a => glow.color = Design.Hex(0xff8a3d, a), Ease.SineInOut, repeat: -1, yoyo: true);
            // Rising embers: a few soft dots on a loop.
            for (int i = 0; i < 8; i++)
            {
                var ember = Glow(root, x, y - 40, Design.Hex(i % 3 == 0 ? 0xffe27a : i % 3 == 1 ? 0xffb347 : 0xff7a1a), 0.35f, 0.35f, order + 2);
                var drift = Random.Range(-18f, 18f);
                var rise = Random.Range(60f, 120f);
                Anim.Float(0, 1, 1.4f, t =>
                {
                    if (ember == null) return;
                    ember.transform.position = Design.World(x + drift * t, y - 40 - rise * t, 1);
                    var c = ember.color;
                    c.a = 1 - t;
                    ember.color = c;
                    ember.transform.localScale = Vector3.one * 0.175f * (1 - t);
                }, Ease.Linear, i * 0.175f, -1);
            }
            return root;
        }

        /// <summary>A note, sign, claw marks or bones on the ground, with a slow glint until it's been read.</summary>
        public static Transform Prop(Transform parent, InspectDef def, bool glint, out SpriteRenderer star)
        {
            var root = new GameObject("prop " + def.Id).transform;
            root.SetParent(parent, false);
            float x = (float)def.X, y = (float)def.Y;
            var order = Design.Depth(y);
            var g = Shapes.Create(root, x, y, order);
            g.Ellipse(new Color(0, 0, 0, 0.3f), 0, 2, 70, 14);
            switch (def.Look)
            {
                case "note":
                    g.Poly(Design.Hex(0xe8dcb5), P(-24, -4), P(18, -12), P(26, 2), P(-16, 8));
                    g.Line(Design.Hex(0x8a7a55), 2, -12, -3, 12, -8).Line(Design.Hex(0x8a7a55), 2, -10, 2, 14, -3);
                    break;
                case "sign":
                    g.Rect(Design.Hex(0x5d3a1f), -4, -70, 8, 72);
                    g.Rect(Design.Hex(0x8d5a2b), -36, -84, 72, 34);
                    g.StrokeRect(Design.Hex(0x3e2723), 3, -36, -84, 72, 34);
                    break;
                case "marks":
                    g.RoundRect(Design.Hex(0x5d3a1f), -60, -26, 120, 26, 10);
                    for (int i = 0; i < 3; i++) g.Line(Design.Hex(0x2b1a0e), 4, -30 + i * 18, -22, -10 + i * 18, -4);
                    break;
                case "bones":
                    var bone = Design.Hex(0xe6e0d0);
                    g.Rect(bone, -26, -6, 52, 7).Circle(bone, -26, -6, 5).Circle(bone, -26, 1, 5).Circle(bone, 26, -6, 5).Circle(bone, 26, 1, 5);
                    g.Circle(bone, 14, -16, 10);
                    g.Circle(Design.Hex(0x2b2620), 10, -17, 2.5f).Circle(Design.Hex(0x2b2620), 18, -17, 2.5f);
                    break;
            }
            star = null;
            if (glint)
            {
                var s = Glow(root, x, y + (def.Look == "sign" ? -96 : -26), Design.Hex(0xfff6c4, 0), 0.9f, 0.9f, order + 1);
                star = s;
                Anim.Float(0, 1, 0.7f, t =>
                {
                    if (s == null) return;
                    s.color = Design.Hex(0xfff6c4, t);
                    s.transform.localRotation = Quaternion.Euler(0, 0, -90 * t);
                    s.transform.localScale = new Vector3(0.2f + 0.25f * t, 0.45f, 1);
                }, Ease.SineInOut, 0, -1, true);
            }
            return root;
        }
    }
}
