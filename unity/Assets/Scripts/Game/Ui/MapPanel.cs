using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The map (M or the Map button), a port of src/ui/mapPanel.ts: one area at a time as dots and lines. Visited
    /// rooms show their name and icons; the rooms next to them show as "?". Arrows lead to other pages.
    /// </summary>
    public static class MapPanel
    {
        const float W = 1180;
        const float H = 640;
        /// <summary>Where the node graph is drawn inside the panel.</summary>
        static readonly Rect Area = new Rect(50, 130, W - 100, H - 260);
        const float NodeR = 30;
        static readonly Color GoldLine = Design.Hex(0xf1c40f);
        static readonly Color Dim = Design.Hex(0x6f6578);
        static readonly Color Road = Design.Hex(0x8a6d3b);
        static readonly Color Blocked = Design.Hex(0x7a3b33);
        static readonly Color HereGreen = Design.Hex(0x7dff9a);
        static readonly Color HereText = Design.Css("#b8ffcb");
        static readonly Color VisitedFill = Design.Hex(0x3b3350);
        static readonly Color UnknownFill = Design.Hex(0x15111c);

        /// <summary>Remembered while the game runs: the page you looked at last, if it's not the one you stand on.</summary>
        static string lastPage;

        /// <summary>Same as `OpenMap`.</summary>
        public static UiModal Open(Player player, string here, Action onRecall) => OpenMap(player, here, onRecall);

        /// <summary>
        /// Opens the map on the page of the room `here` (or the page looked at last). `onRecall` reads a Recall Scroll;
        /// the map closes first.
        /// </summary>
        public static UiModal OpenMap(Player player, string here, Action onRecall)
        {
            var areas = WorldMap.KnownAreas(player, here);
            var page = areas.Any(a => a.Id == lastPage) ? lastPage : WorldMap.AreaOf(here).Id;
            var modal = new UiModal(W, H, "Map", () => { });
            // Looping tweens (markers, pulses) end with the page they belong to.
            var anims = new List<Anim.Handle>();
            PanelKit.StopOnClose(modal, anims);

            void Turn(string areaId)
            {
                page = areaId;
                lastPage = page;
                Sfx.Play("page");
                Render();
            }

            void Render()
            {
                var c = modal.Content;
                PanelKit.StopAll(anims);
                c.Clear();
                var map = WorldMap.Build(player, page, here);
                Ui.Text(c, W / 2, 70, map.Area.Name, 22, Palette.Text, true, 0.5f, 0.5f);

                // Other pages you know: one button each.
                if (areas.Count > 1)
                {
                    const float bw = 210;
                    for (int i = 0; i < areas.Count; i++)
                    {
                        var a = areas[i];
                        var x = W / 2 + (i - (areas.Count - 1) / 2f) * (bw + 10);
                        var b = new UiButton(c, x, 104, a.Name, () => Turn(a.Id), bw, 32, 13);
                        if (a.Id == page) b.SetEnabled(false);
                    }
                }

                // Grid to screen, sized so the whole area (found or not) fits and stays put as you explore.
                var cols = map.MaxX - map.MinX + 1;
                var rows = map.MaxY - map.MinY + 1;
                var cell = Mathf.Min(Area.width / (cols + 0.6f), Area.height / (rows + 0.4f), 210);
                var ox = Area.x + (Area.width - (cols - 1) * cell) / 2;
                var oy = Area.y + 20 + (Area.height - (rows - 1) * cell) / 2;
                Vector2 At(MapNode n) => new Vector2(ox + (n.X - map.MinX) * cell, oy + (n.Y - map.MinY) * cell);

                var g = new PanelPaint(c, 0, 0, W, H);
                foreach (var link in map.Links)
                {
                    var a = At(link.A);
                    var b = At(link.B);
                    if (link.Open) g.Line(a.x, a.y, b.x, b.y, 7, Road);
                    else g.Dashed(a.x, a.y, b.x, b.y, Blocked);
                }

                // Exits to other pages: an arrow off the node with where it goes.
                foreach (var stub in map.Stubs)
                {
                    var p = At(stub.From);
                    float dx = stub.Side == "left" ? -1 : stub.Side == "right" ? 1 : 0;
                    float dy = stub.Side == "down" ? 1 : stub.Side == "left" || stub.Side == "right" ? 0 : -1;
                    var len = Mathf.Min(cell * 0.55f, 90);
                    var ex = p.x + dx * (NodeR + len);
                    var ey = p.y + dy * (NodeR + len);
                    if (stub.Open) g.Line(p.x + dx * NodeR, p.y + dy * NodeR, ex, ey, 5, Road);
                    else g.Dashed(p.x + dx * NodeR, p.y + dy * NodeR, ex, ey, Blocked);
                    g.Triangle(ex + dx * 14, ey + dy * 14, ex - dy * 10, ey + dx * 10, ex + dy * 10, ey - dx * 10, stub.Open ? GoldLine : Blocked);
                    var known = areas.Any(a => a.Id == stub.Area.Id);
                    // Side arrows: the label sits under the arrow's tip, kept inside the panel on wide pages.
                    var label = Ui.Text(c, ex, dx != 0 ? ey + 14 : ey + dy * 28, stub.Label, 15, known ? Palette.TextGold : Palette.TextDim, true,
                        0.5f, dy < 0 ? 1 : 0, stroke: true);
                    var tipX = ex;
                    label.RegisterCallback<GeometryChangedEvent>(_ =>
                    {
                        var lw = label.resolvedStyle.width;
                        if (float.IsNaN(lw) || lw <= 0) return;
                        var cx = Mathf.Clamp(tipX, lw / 2 + 12, W - lw / 2 - 12);
                        if (Mathf.Abs(label.resolvedStyle.left - cx) > 0.5f) label.style.left = cx;
                    });
                    // A page you know: tap the arrow to turn to it.
                    if (known)
                    {
                        var to = stub.Area.Id;
                        PanelKit.OnClick(label, () => Turn(to));
                    }
                }

                foreach (var node in map.Nodes) DrawNode(c, node, At(node), anims, Mathf.Min(170, cell - 12));

                Legend(c);
                RecallRow(c, player, here, () => modal.Close(onRecall));
            }

            Render();
            return modal;
        }

        static void DrawNode(VisualElement c, MapNode node, Vector2 p, List<Anim.Handle> anims, float nameWrap)
        {
            if (node.Quest == "target")
            {
                var ring = PanelKit.Circle(c, p.x, p.y, NodeR + 10, Color.clear, 4, PanelKit.WithAlpha(GoldLine, 0.9f));
                anims.Add(Anim.Float(0, 1, 0.7f, v =>
                {
                    ring.style.scale = new Scale(Vector3.one * (1 + 0.15f * v));
                    ring.style.opacity = 1 - 0.6f * v;
                }, Ease.SineInOut, repeat: -1, yoyo: true));
            }
            if (node.Here)
            {
                var pulse = PanelKit.Circle(c, p.x, p.y, NodeR + 4, PanelKit.WithAlpha(HereGreen, 0.25f));
                anims.Add(Anim.Float(0, 1, 1.1f, v =>
                {
                    pulse.style.scale = new Scale(Vector3.one * (1 + 0.5f * v));
                    pulse.style.opacity = 1 - v;
                }, Ease.Linear, repeat: -1));
            }
            var fill = node.Visited ? VisitedFill : UnknownFill;
            PanelKit.Circle(c, p.x, p.y, NodeR, fill, node.Here ? 5 : 3, node.Here ? HereGreen : node.Visited ? Road : Dim);

            var g = new PanelPaint(c, p.x - 60, p.y - 60, 120, 120);
            // The painter's own coordinates: the node's center is (60, 60).
            const float o = 60;
            if (!node.Visited) Ui.Text(c, p.x, p.y, "?", 30, Palette.TextDim, true, 0.5f, 0.5f);
            else if (node.Icons.Contains("town")) g.House(o, o);
            else if (node.Icons.Contains("boss")) g.Skull(o, o + 1, Palette.TextBad, fill, 1.1f);
            else g.Circle(o, o, 7, Road);
            if (node.Visited && node.Icons.Contains("campfire")) g.Flame(o + NodeR * 0.75f, o + NodeR * 0.7f, 0.8f);

            if (node.Visited)
                Ui.Text(c, p.x, p.y + NodeR + 8, node.Room.Name, 15, node.Here ? HereText : Palette.Text, node.Here, 0.5f, 0, nameWrap, true,
                    TextAnchor.UpperCenter);
            if (node.Here) Ui.Text(c, p.x, p.y - NodeR - 6, "You", 15, HereText, true, 0.5f, 1, stroke: true);
            if (node.Quest == "!" || node.Quest == "?")
            {
                var y = p.y - NodeR * 0.9f;
                var mark = Ui.Text(c, p.x + NodeR * 0.8f, y, node.Quest, 28, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
                anims.Add(Anim.Float(y, y - 4, 0.5f, v => mark.style.top = v, Ease.SineInOut, repeat: -1, yoyo: true));
            }
        }

        static void Legend(VisualElement c)
        {
            const float y = H - 95;
            // A row that lays itself out: icon (28 px), 6 px, text, 26 px, next icon (the TS measured each text).
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(row, 50, y, 0, 0.5f);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            c.Add(row);

            void Item(Action<VisualElement, PanelPaint> draw, string text)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Size(icon, 28, 28);
                icon.style.flexShrink = 0;
                row.Add(icon);
                draw(icon, new PanelPaint(icon, 0, 0, 28, 28));
                var t = new Label(text) { pickingMode = PickingMode.Ignore };
                t.style.fontSize = 14;
                t.style.color = Palette.TextDim;
                t.style.whiteSpace = WhiteSpace.NoWrap;
                t.style.marginLeft = 6;
                t.style.marginRight = 26;
                t.style.marginTop = t.style.marginBottom = 0;
                t.style.paddingLeft = t.style.paddingRight = t.style.paddingTop = t.style.paddingBottom = 0;
                row.Add(t);
            }

            // Icons are drawn around the box's center (14, 14), like the TS draws them at (x + 14, y).
            const float m = 14;
            Item((_, g) => g.Circle(m, m, 9, HereGreen), "You");
            Item((box, _) => Ui.Text(box, m, m, "?", 20, Palette.TextDim, true, 0.5f, 0.5f), "Not explored");
            Item((_, g) => g.House(m, m + 2, 0.75f), "Town");
            Item((_, g) => g.Skull(m, m, Palette.TextBad, Palette.Panel, 0.75f), "Boss");
            Item((_, g) => g.Flame(m, m, 0.7f), "Campfire");
            Item((_, g) => g.Ring(m, m, 9, 3, GoldLine), "Quest or hunt");
            Item((box, _) => Ui.Text(box, m, m, "!", 20, Palette.TextGold, true, 0.5f, 0.5f), "New quest");
            Item((_, g) => g.Dashed(m - 12, m, m + 12, m, Blocked), "Blocked");
        }

        /// <summary>"Recall to Brindlemoor (5 scrolls)" or why you can't.</summary>
        static void RecallRow(VisualElement c, Player player, string here, Action read)
        {
            const float y = H - 45;
            var left = player.CountItem(Travel.RecallScroll);
            var (town, reason) = Travel.RecallCheck(player, here);
            var ok = reason == null;
            var label = ok ? $"Recall to {town.Name}" : "Recall Scroll";
            var btn = new UiButton(c, W - 190, y, label, read, 300, 48, 17, $"{left} scroll{(left == 1 ? "" : "s")} left");
            btn.SetEnabled(ok);
            var note = ok ? "One way: you walk back out. Shops sell more scrolls." : reason;
            Ui.Text(c, 50, y, note, 15, Palette.TextDim, ay: 0.5f, wrap: 760);
        }
    }
}
