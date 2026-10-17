using System.Collections.Generic;
using UnityEngine;

namespace Drakepg
{
    /// <summary>Full-screen painted backgrounds (addBackground / drawScenery). Missing art: a plain dusk gradient.</summary>
    public static class Backdrop
    {
        /// <summary>
        /// The first background in `refs` that has art, covering the screen. `raise` lifts it by that many design px
        /// (battle: the ground shows above the bottom panel); the strip left at the bottom is filled dark.
        /// </summary>
        public static SpriteRenderer Draw(Transform parent, IEnumerable<string> refs, Rules.Backdrop backdrop = null, float raise = 0)
        {
            var key = Art.Pick(refs);
            if (raise > 0) Rect(parent, Design.Width / 2, Design.Height - raise / 2, Design.Width, raise, Design.Hex(0x14101c), -32000);
            if (key == null)
            {
                Rect(parent, Design.Width / 2, Design.Height / 2, Design.Width, Design.Height, Design.Hex(0x2b2140), -32001);
                Rect(parent, Design.Width / 2, Design.Height * 0.8f, Design.Width, Design.Height * 0.4f, Design.Hex(0x3b4a2a), -32000);
                return null;
            }
            var sprite = Art.Get(key);
            var go = new GameObject("background " + key);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = -32000;
            var size = Art.Size(sprite);
            var s = Mathf.Max(Design.Width / size.x, Design.Height / size.y);
            go.transform.localScale = new Vector3(s, s, 1);
            go.transform.position = Design.World(Design.Width / 2, Design.Height / 2 - raise, 5);
            if (backdrop?.Shade is double shade && shade > 0)
                Rect(parent, Design.Width / 2, Design.Height / 2, Design.Width, Design.Height, new Color(0.02f, 0.04f, 0.07f, (float)shade), -31999);
            return r;
        }

        /// <summary>A flat rectangle in the world (center x, y in design px).</summary>
        public static SpriteRenderer Rect(Transform parent, float cx, float cy, float w, float h, Color color, int order)
        {
            var go = new GameObject("rect");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Art.White;
            r.color = color;
            r.sortingOrder = order;
            go.transform.localScale = new Vector3(w, h, 1);
            go.transform.position = Design.World(cx, cy, 4);
            return r;
        }
    }
}
