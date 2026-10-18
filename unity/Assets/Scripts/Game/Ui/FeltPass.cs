using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The menus were written for the old dark panels: light Palette text reads fine on navy felt, but their dark
    /// purple slots and gold rims don't belong on cloth. Inside an element with the `felt` class (Ui.Panel, UiModal,
    /// UiButton, felt cards) dark fills become a darker shade of the felt, the gold panel rim a thread-colored line.
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

        static readonly ConditionalWeakTable<VisualElement, State> states = new ConditionalWeakTable<VisualElement, State>();

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.006f && Mathf.Abs(a.g - b.g) < 0.006f && Mathf.Abs(a.b - b.b) < 0.006f;

        /// <summary>A fill on felt: dark panel colors become a darker shade of the felt (a sunken slot); colors stay, a little softer.</summary>
        public static Color Fill(Color c)
        {
            if (c.a <= 0) return c;
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v < 0.45f && s < 0.6f) return new Color(0.03f, 0.05f, 0.1f, c.a * 0.45f);
            return new Color(c.r, c.g, c.b, c.a * 0.7f);
        }

        /// <summary>A rim on felt: the gold panel rim and dull rims become a soft thread-colored line; strong colors (grade frames) stay.</summary>
        public static Color Border(Color c)
        {
            if (c.a <= 0) return c;
            if (Same(c, Palette.PanelBorder)) return new Color(Felt.Stitch.r, Felt.Stitch.g, Felt.Stitch.b, c.a * 0.35f);
            Color.RGBToHSV(c, out _, out var s, out var v);
            if (v < 0.6f && s < 0.55f) return new Color(Felt.Stitch.r, Felt.Stitch.g, Felt.Stitch.b, c.a * 0.3f);
            return c;
        }

        /// <summary>A square-cornered element on felt (rounded ones are themed by UiSmooth when it bakes them).</summary>
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
