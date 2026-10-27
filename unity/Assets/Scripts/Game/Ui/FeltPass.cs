using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The menus were written for the old dark panels: light Palette text, dark purple slots, gold rims. Inside an
    /// element with the `felt` class (Ui.Panel, UiModal, paper cards) they are themed for paper: text and the colors in
    /// its rich text turn to ink (Felt.Readable), dark fills are pressed into the paper (a kraft shade), rims become thin
    /// brown lines (strong colors, like an item's grade frame, keep their hue).
    /// Code keeps writing Palette colors; UiSmooth's walk calls this every frame and only touches what changed. An
    /// element with the `no-felt` class keeps its colors (and so does all inside it). The walk also gives every bold
    /// label Alegreya Sans Bold.
    /// </summary>
    public static class FeltPass
    {
        public const string Class = "felt", Keep = "no-felt";

        class State
        {
            public Color FillOut, BorderOut;
            public bool HasFill, HasBorder;
        }

        class InkState
        {
            public Color ColorOut;
            public bool HasColor;
            public string TextOut;
        }

        static readonly ConditionalWeakTable<VisualElement, State> states = new ConditionalWeakTable<VisualElement, State>();
        static readonly ConditionalWeakTable<Label, InkState> inks = new ConditionalWeakTable<Label, InkState>();
        static readonly Regex ColorTag = new Regex("<color=(#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)>", RegexOptions.Compiled);

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.006f && Mathf.Abs(a.g - b.g) < 0.006f && Mathf.Abs(a.b - b.b) < 0.006f;

        /// <summary>A fill on paper: dark panel colors are pressed into the paper (a kraft shade); colors stay.</summary>
        public static Color Fill(Color c)
        {
            if (c.a <= 0) return c;
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v < 0.45f && s < 0.6f) return new Color(0.5f, 0.33f, 0.12f, 0.12f + 0.08f * c.a);
            return c;
        }

        /// <summary>A rim on paper: the gold panel rim and dull rims become thin brown lines; strong colors (grade frames) keep their hue, a little darker.</summary>
        public static Color Border(Color c)
        {
            if (c.a <= 0) return c;
            if (Same(c, Palette.PanelBorder)) return new Color(Felt.Trim.r, Felt.Trim.g, Felt.Trim.b, c.a * 0.7f);
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (s < 0.2f || (v < 0.6f && s < 0.55f)) return new Color(Felt.Trim.r, Felt.Trim.g, Felt.Trim.b, c.a * 0.55f);
            var o = Color.HSVToRGB(h, s, Mathf.Min(v, 0.75f));
            o.a = c.a;
            return o;
        }

        /// <summary>A square-cornered element on paper (rounded ones are themed by UiSmooth when it bakes them).</summary>
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

        /// <summary>A label on paper: its color, and the colors in its rich text, made readable (once per change).</summary>
        public static void Ink(Label l)
        {
            var st = inks.GetValue(l, _ => new InkState());
            // The inline color is there at once; an inherited one only once styles resolve.
            var sc = l.style.color;
            var c = sc.keyword == StyleKeyword.Undefined ? sc.value : l.resolvedStyle.color;
            if (!st.HasColor || !Same(c, st.ColorOut))
            {
                st.ColorOut = Felt.Readable(c);
                st.HasColor = true;
                l.style.color = st.ColorOut;
            }
            var text = l.text;
            if (text == st.TextOut || text == null || !text.Contains("<color=")) return;
            st.TextOut = ColorTag.Replace(text, m =>
                ColorUtility.TryParseHtmlString(m.Groups[1].Value, out var tc) ? $"<color=#{ColorUtility.ToHtmlStringRGBA(Felt.Readable(tc))}>" : m.Value);
            l.text = st.TextOut;
        }

        /// <summary>Every label gets the UI fonts: Alegreya Sans (the root's), bold ones Alegreya Sans Bold.</summary>
        public static void Font(Label l)
        {
            if (l.style.unityFontDefinition.keyword != StyleKeyword.Null) return;
            if (l.style.unityFontStyleAndWeight.value == FontStyle.Bold) Fonts.Bold(l);
        }
    }
}
