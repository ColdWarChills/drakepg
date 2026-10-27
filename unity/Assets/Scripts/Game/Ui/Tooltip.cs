using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// A small paper note that opens right by the thing you hover (or tap): a bold title and a line or two. One at a time,
    /// under the thing when there's room, else above it; it never covers the pointer's target and never takes clicks.
    /// <c>Tooltip.Attach(icon, "Weak to Water", "Water hits deal 50% more damage.")</c>.
    /// </summary>
    public static class Tooltip
    {
        const float Width = 330, Gap = 8, Margin = 8;
        static readonly Color Fill = new Color(Felt.Paper.r, Felt.Paper.g, Felt.Paper.b, 0.98f);

        static VisualElement box;
        static VisualElement owner;

        public static void Attach(VisualElement e, string title, string body) => Attach(e, () => (title, body));

        /// <summary>`text` is read when the tooltip opens, so it can show numbers that change during a fight.</summary>
        public static void Attach(VisualElement e, Func<(string title, string body)> text)
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<PointerEnterEvent>(_ => Show(e, text()));
            e.RegisterCallback<PointerDownEvent>(_ => Show(e, text()));
            e.RegisterCallback<PointerLeaveEvent>(_ => Hide(e));
            e.RegisterCallback<DetachFromPanelEvent>(_ => Hide(e));
        }

        static void Show(VisualElement anchor, (string title, string body) text)
        {
            Hide(null);
            var root = GameRoot.I?.DesignRoot;
            if (root == null || anchor.panel == null) return;
            owner = anchor;
            box = new VisualElement { pickingMode = PickingMode.Ignore, name = "tooltip" };
            box.style.position = Position.Absolute;
            box.style.width = Width;
            box.style.paddingLeft = box.style.paddingRight = 12;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 9;
            box.style.backgroundColor = Fill;
            Ui.Round(box, 8);
            Ui.Border(box, 1.5f, new Color(Felt.Trim.r, Felt.Trim.g, Felt.Trim.b, 0.7f));
            box.AddToClassList(FeltPass.Keep);
            if (!string.IsNullOrEmpty(text.title))
            {
                var t = Line(text.title, 17, Felt.Ink);
                Fonts.Heavy(t);
                box.Add(t);
            }
            if (!string.IsNullOrEmpty(text.body)) box.Add(Line(text.body, 16, Felt.Text));
            box.style.opacity = 0;
            root.Add(box);
            // Placed once its height is known: under the anchor, or above it near the bottom edge.
            var shown = box;
            box.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (shown != box || anchor.panel == null) return;
                var r = root.WorldToLocal(anchor.worldBound);
                var h = box.resolvedStyle.height;
                var x = Mathf.Clamp(r.center.x - Width / 2, Margin, Design.Width - Width - Margin);
                var y = r.yMax + Gap;
                if (y + h > Design.Height - Margin) y = r.yMin - Gap - h;
                box.style.left = x;
                box.style.top = Mathf.Max(Margin, y);
                box.style.opacity = 1;
            });
        }

        static Label Line(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = l.style.marginRight = 0;
            l.style.paddingLeft = l.style.paddingRight = 0;
            Fonts.Body(l);
            return l;
        }

        /// <summary>Close it if `from` opened it (or whatever is open, for null).</summary>
        public static void Hide(VisualElement from)
        {
            if (box == null || (from != null && from != owner)) return;
            box.RemoveFromHierarchy();
            box = null;
            owner = null;
        }
    }
}
