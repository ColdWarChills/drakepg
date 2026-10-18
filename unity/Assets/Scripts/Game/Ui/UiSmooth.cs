using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// Smooth rounded corners. UI Toolkit draws border-radius without anti-aliasing at runtime (stair-stepped corners,
    /// MSAA doesn't reach the UI), so every element with a radius gets its fill and border baked into a small
    /// anti-aliased 9-slice image instead, drawn at the screen's real resolution. Code keeps using
    /// style.backgroundColor / Ui.Border / Ui.Round as usual: GameRoot runs `Pass` every frame and re-bakes an element
    /// when its colors, border width or radius change (baked images are cached by look). The same walk themes menus for
    /// felt and sets the UI fonts (FeltPass).
    /// </summary>
    public static class UiSmooth
    {
        /// <summary>What an element should look like, and what was last baked for it.</summary>
        class Look
        {
            public Color Fill, Border, Thread;
            public float Width, Radius;
            public string Key;
        }

        static readonly ConditionalWeakTable<VisualElement, Look> looks = new ConditionalWeakTable<VisualElement, Look>();
        static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();
        /// <summary>Texels per design px (the UI's screen scale x 2, so corners stay smooth on big screens).</summary>
        static int texels = 2;

        /// <summary>Walk the UI and bake any rounded element whose look changed. `scale` = screen px per design px.</summary>
        public static void Pass(VisualElement root, float scale)
        {
            var k = Mathf.Clamp(Mathf.CeilToInt(scale * 2), 2, 6);
            if (k != texels)
            {
                texels = k;
                foreach (var t in baked.Values) Object.Destroy(t);
                baked.Clear();
            }
            Visit(root, false);
        }

        static void Visit(VisualElement e, bool inked)
        {
            if (e.resolvedStyle.display == DisplayStyle.None) return;
            if (e.ClassListContains(FeltPass.Keep)) inked = false;
            else if (e.ClassListContains(FeltPass.Class)) inked = true;
            if (e is Label label) FeltPass.Font(label);
            var radius = e.resolvedStyle.borderTopLeftRadius;
            if (radius >= 1) Apply(e, radius, inked);
            else if (inked) FeltPass.Box(e);
            var n = e.hierarchy.childCount;
            for (int i = 0; i < n; i++) Visit(e.hierarchy[i], inked);
        }

        static void Apply(VisualElement e, float radius, bool inked)
        {
            var rs = e.resolvedStyle;
            var mine = looks.TryGetValue(e, out var look);
            // Someone else's picture (an icon or art): leave it alone.
            if (!mine && (rs.backgroundImage.texture != null || rs.backgroundImage.sprite != null || rs.backgroundImage.vectorImage != null)) return;
            if (!mine)
            {
                look = new Look();
                looks.Add(e, look);
            }
            // After baking, the element's own background and border colors are cleared; anything set since is new.
            if (rs.backgroundColor.a > 0 || !mine) look.Fill = inked ? FeltPass.Fill(rs.backgroundColor) : rs.backgroundColor;
            if (inked && rs.borderTopWidth > 0)
            {
                // On felt a rim is sewn on (FeltPass.Seam): only the fill is baked, inside where the rim was.
                if (rs.borderTopColor.a > 0 || !mine) look.Thread = FeltPass.Border(rs.borderTopColor);
                look.Border = Color.clear;
                if (look.Thread.a > 0) FeltPass.Seam(e, look.Thread, rs.borderTopWidth, radius);
            }
            else if (rs.borderTopColor.a > 0 || !mine) look.Border = rs.borderTopColor;
            look.Width = rs.borderTopWidth;
            look.Radius = radius;
            var key = Key(look);
            if (key == look.Key)
            {
                if (rs.backgroundColor.a > 0 || rs.borderTopColor.a > 0) Clear(e);
                return;
            }
            look.Key = key;
            if (look.Fill.a <= 0 && (look.Width <= 0 || look.Border.a <= 0))
            {
                // Nothing to draw (an invisible hit area with a radius).
                e.style.backgroundImage = StyleKeyword.Null;
                return;
            }
            if (!baked.TryGetValue(key, out var tex))
            {
                tex = Bake(look, texels);
                baked[key] = tex;
            }
            var slice = Corner(look, texels);
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundImageTintColor = Color.white;
            e.style.unitySliceLeft = e.style.unitySliceRight = e.style.unitySliceTop = e.style.unitySliceBottom = slice;
            e.style.unitySliceScale = 1f / texels;
            Clear(e);
        }

        static void Clear(VisualElement e)
        {
            e.style.backgroundColor = Color.clear;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = Color.clear;
        }

        static string Key(Look l) =>
            $"{ColorUtility.ToHtmlStringRGBA(l.Fill)}|{ColorUtility.ToHtmlStringRGBA(l.Border)}|{l.Width:0.##}|{l.Radius:0.##}|{texels}";

        /// <summary>The 9-slice corner in texels: the radius or the border, whichever is bigger.</summary>
        static int Corner(Look l, int k) => Mathf.CeilToInt(Mathf.Max(l.Radius, l.Width) * k) + 1;

        /// <summary>A rounded rectangle, fill and border ring, with soft (anti-aliased) edges, sized for 9-slicing.</summary>
        static Texture2D Bake(Look l, int k)
        {
            var corner = Corner(l, k);
            var size = corner * 2 + 2;
            var r = l.Radius * k;
            var w = Mathf.Max(0, l.Width) * k;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var outer = Coverage(x + 0.5f, y + 0.5f, 0, 0, size, size, r);
                    var inner = w > 0 ? Coverage(x + 0.5f, y + 0.5f, w, w, size - w, size - w, Mathf.Max(0, r - w)) : outer;
                    var ring = Mathf.Max(0, outer - inner);
                    var aFill = l.Fill.a * inner;
                    var aBorder = l.Border.a * ring;
                    var a = aFill + aBorder;
                    px[y * size + x] = a <= 0
                        ? new Color(l.Fill.r, l.Fill.g, l.Fill.b, 0)
                        : new Color((l.Fill.r * aFill + l.Border.r * aBorder) / a, (l.Fill.g * aFill + l.Border.g * aBorder) / a,
                            (l.Fill.b * aFill + l.Border.b * aBorder) / a, Mathf.Clamp01(a));
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ui round" };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>How much of a pixel at (px, py) lies inside a rounded rect (x0, y0)-(x1, y1) with radius r (0-1).</summary>
        static float Coverage(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            // Signed distance to the rounded rectangle, then a 1-texel soft edge.
            var cx = (x0 + x1) / 2;
            var cy = (y0 + y1) / 2;
            var hx = (x1 - x0) / 2 - r;
            var hy = (y1 - y0) / 2 - r;
            var dx = Mathf.Abs(px - cx) - hx;
            var dy = Mathf.Abs(py - cy) - hy;
            var outside = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude;
            var inside = Mathf.Min(Mathf.Max(dx, dy), 0);
            var d = outside + inside - r;
            return Mathf.Clamp01(0.5f - d);
        }
    }
}
