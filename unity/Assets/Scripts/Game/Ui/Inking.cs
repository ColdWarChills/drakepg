using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The menus were written for the old dark panels (light text, dark slots, gold rims). On parchment (anything inside
    /// an element with the `parchment` class: Ui.Panel, UiModal, UiButton) their colors are inked instead: light text
    /// becomes dark ink of the same hue, dark fills become faint ink tints, rims become ink lines, and rich-text colors
    /// are inked the same way. Code keeps writing Palette colors; UiSmooth's walk calls this every frame, and only
    /// touches what changed. An element with the `no-ink` class keeps its colors (and so does all inside it).
    /// </summary>
    public static class Inking
    {
        public const string Class = "parchment", Keep = "no-ink";

        class State
        {
            public Color TextOut, FillOut, BorderOut;
            public string RichOut;
            public bool HasText, HasFill, HasBorder, Halo;
        }

        static readonly ConditionalWeakTable<VisualElement, State> states = new ConditionalWeakTable<VisualElement, State>();
        static readonly Regex ColorTag = new Regex("<color=(#[0-9a-fA-F]{6,8})>", RegexOptions.Compiled);

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.006f && Mathf.Abs(a.g - b.g) < 0.006f && Mathf.Abs(a.b - b.b) < 0.006f;

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A text color for parchment: the palette's light colors map to their inks, other light colors darken in their hue.</summary>
        public static Color Text(Color c)
        {
            if (Same(c, Palette.Text)) return WithAlpha(Paper.Ink, c.a);
            if (Same(c, Palette.TextDim)) return WithAlpha(Paper.InkDim, c.a);
            if (Same(c, Palette.TextGold) || Same(c, Palette.Gold)) return WithAlpha(Paper.InkGold, c.a);
            if (Same(c, Palette.TextGood)) return WithAlpha(Paper.InkGood, c.a);
            if (Same(c, Palette.TextBad)) return WithAlpha(Paper.InkBad, c.a);
            if (Same(c, Palette.TextMagic)) return WithAlpha(Paper.InkMagic, c.a);
            Color.RGBToHSV(c, out _, out var s, out var v);
            // Already dark enough to read on parchment.
            if (v < 0.5f) return c;
            if (s < 0.15f) return WithAlpha(v > 0.8f ? Paper.Ink : Paper.InkDim, c.a);
            return WithAlpha(Paper.OnPaper(c), c.a);
        }

        /// <summary>A fill on parchment: dark panel colors become a faint ink tint (a recessed area), colors a light wash of themselves.</summary>
        public static Color Fill(Color c)
        {
            if (c.a <= 0) return c;
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v < 0.45f && s < 0.5f) return WithAlpha(Paper.Ink, c.a * (0.07f + 0.1f * (1 - v / 0.45f)));
            return WithAlpha(c, c.a * 0.35f);
        }

        /// <summary>A rim on parchment: the gold panel rim and dark rims become ink lines, colored rims ink of their hue.</summary>
        public static Color Border(Color c)
        {
            if (c.a <= 0) return c;
            if (Same(c, Palette.PanelBorder)) return WithAlpha(Paper.Ink, c.a * 0.55f);
            Color.RGBToHSV(c, out _, out var s, out var v);
            // Dull rims (slot outlines): a soft ink line. Strong colors (grade frames): ink of their hue.
            if (v < 0.6f && s < 0.55f) return WithAlpha(Paper.Ink, c.a * 0.45f);
            if (s < 0.15f) return WithAlpha(Paper.InkDim, c.a * 0.8f);
            return WithAlpha(Paper.OnPaper(c), c.a);
        }

        /// <summary>Ink every &lt;color=#...&gt; tag in a rich text.</summary>
        public static string Rich(string text) =>
            ColorTag.Replace(text, m => ColorUtility.TryParseHtmlString(m.Groups[1].Value, out var c) ? $"<color={Ui.Css(Text(c))}>" : m.Value);

        /// <summary>A label inside parchment: its color, its rich-text colors, and a light halo instead of a dark outline.</summary>
        public static void Label(Label l)
        {
            var st = states.GetValue(l, _ => new State());
            var col = l.style.color;
            if (col.keyword == StyleKeyword.Null || col.keyword == StyleKeyword.Undefined)
            {
                var c = col.keyword == StyleKeyword.Null ? Palette.Text : col.value;
                if (!st.HasText || c != st.TextOut)
                {
                    st.TextOut = Text(c);
                    st.HasText = true;
                    l.style.color = st.TextOut;
                }
            }
            var text = l.text;
            if (text != null && text != st.RichOut && text.IndexOf("<color=#", StringComparison.Ordinal) >= 0)
            {
                st.RichOut = Rich(text);
                l.text = st.RichOut;
            }
            if (!st.Halo && l.style.unityTextOutlineWidth.value > 0)
            {
                st.Halo = true;
                l.style.unityTextOutlineColor = Paper.Parch;
            }
        }

        /// <summary>A square-cornered element inside parchment (rounded ones are inked by UiSmooth when it bakes them).</summary>
        public static void Box(VisualElement e)
        {
            var bg = e.style.backgroundColor;
            var bc = e.style.borderTopColor;
            var hasBg = bg.keyword == StyleKeyword.Undefined && bg.value.a > 0;
            var hasBorder = bc.keyword == StyleKeyword.Undefined && bc.value.a > 0;
            if (!hasBg && !hasBorder) return;
            var st = states.GetValue(e, _ => new State());
            if (hasBg && (!st.HasFill || bg.value != st.FillOut))
            {
                st.FillOut = Fill(bg.value);
                st.HasFill = true;
                e.style.backgroundColor = st.FillOut;
            }
            if (hasBorder && (!st.HasBorder || bc.value != st.BorderOut))
            {
                st.BorderOut = Border(bc.value);
                st.HasBorder = true;
                e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = st.BorderOut;
            }
        }

        /// <summary>Every label gets the UI fonts: Alegreya Sans (the root's), bold ones Alegreya Sans Bold.</summary>
        public static void Font(Label l)
        {
            if (l.style.unityFontDefinition.keyword != StyleKeyword.Null) return;
            if (l.style.unityFontStyleAndWeight.value == FontStyle.Bold) Fonts.Bold(l);
        }
    }
}
