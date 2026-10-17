using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Drakepg
{
    /// <summary>
    /// The game's art by key (src/ui/art.ts): sprites in Resources/Art, copied there by scripts/unity_art.py and
    /// imported so they draw at their 1x design size. Missing art returns null; callers fall back to shapes.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static HashSet<string> keys;

        static void LoadIndex()
        {
            if (keys != null) return;
            keys = new HashSet<string>();
            var index = Resources.Load<TextAsset>("Art/index");
            if (index == null) return;
            foreach (var kv in JObject.Parse(index.text)) keys.Add(kv.Key);
        }

        public static bool Has(string key)
        {
            LoadIndex();
            return key != null && keys.Contains(key);
        }

        public static Sprite Get(string key)
        {
            if (!Has(key)) return null;
            if (cache.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>("Art/" + key);
            cache[key] = s;
            return s;
        }

        /// <summary>The first key in the list that has art.</summary>
        public static string Pick(IEnumerable<string> refs)
        {
            if (refs == null) return null;
            foreach (var k in refs) if (Has(k)) return k;
            return null;
        }

        /// <summary>A sprite's size in design pixels.</summary>
        public static Vector2 Size(Sprite s) => s == null ? Vector2.zero : (Vector2)s.bounds.size * Design.PixelsPerUnit;

        // --- Generated shapes (no art needed) ---

        static Sprite white, circle, soft;

        /// <summary>A white square 1 design pixel across, center pivot: scale it to any rectangle (w, h).</summary>
        public static Sprite White => white ??= Make(4, 4, (x, y) => 1, 1, new Vector2(0.5f, 0.5f));

        /// <summary>A white disc, 64 design pixels across.</summary>
        public static Sprite Circle => circle ??= Make(64, 64, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
            return Mathf.Clamp01(32 - d);
        }, 64, new Vector2(0.5f, 0.5f));

        /// <summary>A soft round glow, 64 design pixels across.</summary>
        public static Sprite Soft => soft ??= Make(64, 64, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32;
            return Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d);
        }, 64, new Vector2(0.5f, 0.5f));

        /// <summary>A sprite from an alpha function (x, y in texels); `designSize` is its width in design pixels.</summary>
        public static Sprite Make(int w, int h, System.Func<int, int, float> alpha, float designSize, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, alpha(x, y));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, w / designSize * Design.PixelsPerUnit);
        }
    }
}
