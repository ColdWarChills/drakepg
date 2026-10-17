using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// Vector drawing for the menus (what Phaser's Graphics did): lines, dashed lines, filled polygons and circles,
    /// in the element's own design pixels. Shapes are queued and drawn by `generateVisualContent`.
    /// </summary>
    internal class PanelPaint : VisualElement
    {
        readonly List<Action<Painter2D>> ops = new List<Action<Painter2D>>();

        public PanelPaint(VisualElement parent, float x, float y, float w, float h)
        {
            pickingMode = PickingMode.Ignore;
            Ui.Place(this, x, y);
            Ui.Size(this, w, h);
            generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                foreach (var op in ops) op(p);
            };
            parent?.Add(this);
        }

        PanelPaint Op(Action<Painter2D> op)
        {
            ops.Add(op);
            MarkDirtyRepaint();
            return this;
        }

        public PanelPaint Line(float x1, float y1, float x2, float y2, float width, Color color) => Op(p =>
        {
            p.lineWidth = width;
            p.strokeColor = color;
            p.lineCap = LineCap.Butt;
            p.BeginPath();
            p.MoveTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x2, y2));
            p.Stroke();
        });

        /// <summary>A blocked way: a dashed line (dashes of about 14 px).</summary>
        public PanelPaint Dashed(float x1, float y1, float x2, float y2, Color color, float width = 5)
        {
            var len = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            var steps = Mathf.Max(1, Mathf.FloorToInt(len / 14));
            for (int i = 0; i < steps; i += 2)
            {
                var a = (float)i / steps;
                var b = Mathf.Min(1, (float)(i + 1) / steps);
                Line(x1 + (x2 - x1) * a, y1 + (y2 - y1) * a, x1 + (x2 - x1) * b, y1 + (y2 - y1) * b, width, color);
            }
            return this;
        }

        public PanelPaint Poly(Color color, params Vector2[] points) => Op(p =>
        {
            if (points.Length < 3) return;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill(FillRule.NonZero);
        });

        public PanelPaint Triangle(float x1, float y1, float x2, float y2, float x3, float y3, Color color) =>
            Poly(color, new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3));

        public PanelPaint FillRect(float x, float y, float w, float h, Color color) =>
            Poly(color, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));

        public PanelPaint Circle(float x, float y, float r, Color color) => Op(p =>
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(new Vector2(x, y), r, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
            p.ClosePath();
            p.Fill(FillRule.NonZero);
        });

        public PanelPaint Ring(float x, float y, float r, float width, Color color) => Op(p =>
        {
            p.lineWidth = width;
            p.strokeColor = color;
            p.BeginPath();
            p.Arc(new Vector2(x, y), r, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
            p.ClosePath();
            p.Stroke();
        });

        // --- Map icons (mapPanel.ts), shared by the map and its legend ---

        /// <summary>A little house (drawHouse).</summary>
        public PanelPaint House(float x, float y, float s = 1)
        {
            FillRect(x - 11 * s, y - 2 * s, 22 * s, 15 * s, Design.Hex(0xd9b46a));
            Triangle(x - 15 * s, y - 1 * s, x + 15 * s, y - 1 * s, x, y - 15 * s, Design.Hex(0xa8432f));
            return FillRect(x - 3 * s, y + 5 * s, 6 * s, 8 * s, Design.Hex(0x3e2723));
        }

        /// <summary>A campfire (drawFlame).</summary>
        public PanelPaint Flame(float x, float y, float s = 1)
        {
            FillRect(x - 10 * s, y + 6 * s, 20 * s, 5 * s, Design.Hex(0x6d4c33));
            Triangle(x - 8 * s, y + 7 * s, x + 8 * s, y + 7 * s, x, y - 14 * s, Design.Hex(0xff7a1a));
            return Triangle(x - 4 * s, y + 7 * s, x + 4 * s, y + 7 * s, x, y - 5 * s, Design.Hex(0xffd75e));
        }

        /// <summary>
        /// A skull (the TS draws the "☠" glyph, which the default font may not have). `hole` is the color behind it,
        /// used for the eyes, nose and teeth gaps. About 24 px tall at s = 1.
        /// </summary>
        public PanelPaint Skull(float x, float y, Color color, Color hole, float s = 1)
        {
            Circle(x, y - 3 * s, 10 * s, color);
            FillRect(x - 6 * s, y + 3 * s, 12 * s, 8 * s, color);
            Circle(x - 4 * s, y - 3 * s, 3 * s, hole);
            Circle(x + 4 * s, y - 3 * s, 3 * s, hole);
            Triangle(x - 1.6f * s, y + 3 * s, x + 1.6f * s, y + 3 * s, x, y, hole);
            Line(x - 2 * s, y + 7 * s, x - 2 * s, y + 11 * s, 1.4f * s, hole);
            return Line(x + 2 * s, y + 7 * s, x + 2 * s, y + 11 * s, 1.4f * s, hole);
        }
    }

    /// <summary>The color and name a piece of gear shows (gradeInfo in src/ui/itemUi.ts).</summary>
    internal struct GradeView
    {
        public double Grade;
        public string Tier, Name;
        public Color Color, Css;
        public string Sigil;
        /// <summary>Name color: the Sigil's if it has one, else the grade's.</summary>
        public Color NameColor;
        /// <summary>Border color: the Sigil's if it has one, else the grade's.</summary>
        public Color Frame;
    }

    /// <summary>
    /// Small text and item helpers the menus share (from src/ui/itemUi.ts and text.ts). Kept local to these panels;
    /// ItemUi.cs has the full set.
    /// </summary>
    internal static class PanelKit
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A number printed like JavaScript does (`${n}`).</summary>
        public static string Num(double v) => JsMath.Str(v);

        /// <summary>`n.toFixed(1)`.</summary>
        public static string Fixed1(double v) => v.ToString("0.0", Inv);

        /// <summary>Several lines under each other, `spacing` extra px between them (Phaser's setLineSpacing).</summary>
        public static VisualElement Lines(VisualElement parent, float x, float y, IEnumerable<string> lines, float size, Color? color = null,
            float spacing = 0, float wrap = 0, bool bold = false)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(box, x, y);
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.FlexStart;
            if (wrap > 0) box.style.width = wrap;
            foreach (var line in lines)
            {
                var l = new Label(line) { pickingMode = PickingMode.Ignore };
                l.style.fontSize = size;
                l.style.color = color ?? Palette.Text;
                l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
                l.style.unityTextAlign = TextAnchor.UpperLeft;
                l.style.marginLeft = l.style.marginRight = l.style.marginTop = 0;
                l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
                l.style.marginBottom = spacing;
                l.style.whiteSpace = wrap > 0 ? WhiteSpace.Normal : WhiteSpace.NoWrap;
                if (wrap > 0) l.style.maxWidth = wrap;
                box.Add(l);
            }
            parent?.Add(box);
            return box;
        }

        /// <summary>A circle with a stroke centered on its edge, like Phaser's circle + setStrokeStyle. (x, y) = center.</summary>
        public static VisualElement Circle(VisualElement parent, float x, float y, float r, Color fill, float stroke = 0, Color? strokeColor = null)
        {
            var d = 2 * r + stroke;
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(e, x, y, 0.5f, 0.5f);
            Ui.Size(e, d, d);
            Ui.Round(e, d / 2);
            e.style.backgroundColor = fill;
            if (stroke > 0) Ui.Border(e, stroke, strokeColor ?? Color.white);
            parent?.Add(e);
            return e;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // --- Items (itemUi.ts) ---

        static readonly Dictionary<string, (long color, string css)> GradeColors = new Dictionary<string, (long, string)>
        {
            ["standard"] = (0x8c8577, "#b3ab9a"),
            ["fair"] = (0xe8e2d4, "#f3e9d2"),
            ["fine"] = (0x5cc85c, "#7ddc7d"),
            ["superior"] = (0x4a9cf0, "#7fb8ff"),
            ["masterwork"] = (0xf1c40f, "#ffd34d"),
        };

        static readonly Dictionary<string, (long color, string css)> SigilColors = new Dictionary<string, (long, string)>
        {
            ["dawn"] = (0xff7a2e, "#ff9550"),
            ["dusk"] = (0xb070ff, "#c79bff"),
            ["eclipse"] = (0xff3358, "#ff6680"),
        };

        /// <summary>GRADE_COLORS[tier].color.</summary>
        public static Color GradeColor(string tier) => Design.Hex(GradeColors.TryGetValue(tier, out var c) ? c.color : GradeColors["standard"].color);

        /// <summary>Grade, tier and colors of an item. Consumables count as Standard.</summary>
        public static GradeView GradeInfo(ItemDef def, GearTraits g)
        {
            var gr = def.IsGear && g != null ? Items.Grade(def, g.Rolls ?? new Dictionary<string, double>(), g.Sigil) : 0;
            var (tier, name) = Items.GradeTier(gr);
            var colors = GradeColors[tier];
            var sigil = def.IsGear ? g?.Sigil : null;
            var hasSigil = sigil != null && SigilColors.ContainsKey(sigil);
            return new GradeView
            {
                Grade = gr,
                Tier = tier,
                Name = name,
                Color = Design.Hex(colors.color),
                Css = Design.Css(colors.css),
                Sigil = sigil,
                NameColor = Design.Css(hasSigil ? SigilColors[sigil].css : colors.css),
                Frame = Design.Hex(hasSigil ? SigilColors[sigil].color : colors.color),
            };
        }

        /// <summary>"Fine 52%" or "Standard".</summary>
        public static string GradeLabel(GradeView info) => info.Tier == "standard" ? "Standard" : $"{info.Name} {Num(info.Grade)}%";

        /// <summary>"+3 Iron Sword" for reinforced gear, else just the name.</summary>
        public static string ItemName(ItemDef def, GearTraits g) => g?.Plus is int plus && plus != 0 ? $"+{plus} {def.Name}" : def.Name;

        /// <summary>Item name plus its Sigil and grade, e.g. "Cinder Fang (Sigil of Dusk, Fine 52%)"; consumables are just the name.</summary>
        public static string LootName(ItemDef def, GearTraits g)
        {
            if (!def.IsGear || g == null) return def.Name;
            var info = GradeInfo(def, g);
            var sigil = info.Sigil != null && Items.SigilNames.TryGetValue(info.Sigil, out var s) ? s + ", " : "";
            return $"{ItemName(def, g)} ({sigil}{GradeLabel(info)})";
        }

        static string DamageTypeLabel(string type) => type != null && Db.Data.DamageTypeLabels.TryGetValue(type, out var l) ? l : type;

        public static string ElementLabel(string element) => element != null && Db.Data.ElementLabels.TryGetValue(element, out var l) ? l : element;

        /// <summary>"+2 STR  +1 END".</summary>
        public static string BonusText(Dictionary<string, double> stats)
        {
            if (stats == null) return "";
            var parts = new List<string>();
            foreach (var k in Stat.Keys)
                if (stats.TryGetValue(k, out var v) && v != 0) parts.Add($"+{Num(v)} {Player.StatLabels[k]}");
            return string.Join("  ", parts);
        }

        /// <summary>"DEF Melee 9 · Pierce 10 · Magic 4"</summary>
        public static string DefenseText(double melee, double pierce, double magic) =>
            $"DEF {DamageTypeLabel("melee")} {Num(melee)} · {DamageTypeLabel("pierce")} {Num(pierce)} · {DamageTypeLabel("magic")} {Num(magic)}";

        /// <summary>"Fire +15, Ice -10" after `prefix`, or "" if none.</summary>
        public static string ResistText(Dictionary<string, double> r, string prefix = "")
        {
            if (r == null) return "";
            r.TryGetValue("all", out var all);
            var parts = new List<string>();
            foreach (var e in Db.Data.Elements)
            {
                var v = (r.TryGetValue(e, out var x) ? x : 0) + all;
                if (v == 0) continue;
                parts.Add($"{ElementLabel(e)} {(v > 0 ? "+" : "")}{Num(v)}");
            }
            return parts.Count > 0 ? prefix + string.Join(", ", parts) : "";
        }

        /// <summary>"+8% Max HP, Earth +15"</summary>
        public static string SetBonusText(SetBonus b)
        {
            var parts = new List<string>();
            var stats = BonusText(b.Stats);
            if (stats != "") parts.Add(stats);
            if (b.HpPct is double hp && hp != 0) parts.Add($"+{Num(hp)}% Max HP");
            if (b.DefensePct is double def && def != 0) parts.Add($"+{Num(def)}% defense");
            if (b.DamagePct is double dmg && dmg != 0) parts.Add($"+{Num(dmg)}% damage");
            if (b.Crit is double crit && crit != 0) parts.Add($"+{Num(crit)}% Crit chance");
            var res = ResistText(b.Resists, "Resists ");
            if (res != "") parts.Add(res);
            return string.Join(", ", parts);
        }

        /// <summary>The item's numbers with its rolls applied, one fact per line (itemLines; mods are not listed).</summary>
        public static List<string> ItemLines(ItemDef def, GearTraits g = null)
        {
            g ??= new GearTraits();
            var lines = new List<string>();
            switch (def.Type)
            {
                case "weapon":
                {
                    var d = Items.RolledDamage(def, g);
                    lines.Add($"{Num(d[0])}-{Num(d[1])} {DamageTypeLabel(def.DamageType)} damage");
                    lines.Add(def.Element != null ? $"Element: {ElementLabel(def.Element)}" : "Element: Neutral");
                    if (def.Stats != null) lines.Add(BonusText(def.Stats));
                    if (def.Proc != null) lines.Add($"Special: {def.Proc.Name}, {Num(JsMath.Round(Items.RolledProcChance(def, g) * 100))}% on Attack");
                    break;
                }
                case "armor":
                {
                    lines.Add(DefenseText(Items.RolledDefense(def, g, "melee"), Items.RolledDefense(def, g, "pierce"), Items.RolledDefense(def, g, "magic")));
                    var res = ResistText(def.Resists, "Resists ");
                    if (res != "") lines.Add(res);
                    if (def.Stats != null) lines.Add(BonusText(def.Stats));
                    break;
                }
                case "accessory":
                {
                    // Everything scales with the Power roll (and a Sigil or +).
                    var stats = new Dictionary<string, double>();
                    foreach (var k in Stat.Keys)
                        if (def.Stats != null && def.Stats.TryGetValue(k, out var v) && v != 0) stats[k] = Items.AccessoryValue(v, g);
                    if (stats.Count > 0) lines.Add(BonusText(stats));
                    if (def.Hp is double hp && hp != 0) lines.Add($"+{Num(Items.AccessoryValue(hp, g))} Max HP");
                    if (def.Mp is double mp && mp != 0) lines.Add($"+{Num(Items.AccessoryValue(mp, g))} Max MP");
                    if (def.Crit is double crit && crit != 0) lines.Add($"+{Fixed1(Items.AccessoryValue(crit, g, 1))}% Crit chance");
                    if (def.Resists != null)
                    {
                        var res = new Dictionary<string, double>();
                        foreach (var kv in def.Resists) res[kv.Key] = Items.AccessoryValue(kv.Value, g);
                        var rt = ResistText(res, "Resists ");
                        if (rt != "") lines.Add(rt);
                    }
                    break;
                }
                case "scroll":
                    // The card's description says it all.
                    break;
                default:
                    lines.Add(def.Description ?? "");
                    break;
            }
            return lines;
        }

        /// <summary>Stop looping tweens when a modal goes away (they belong to the screen otherwise).</summary>
        public static void StopOnClose(UiModal modal, List<Anim.Handle> anims)
        {
            modal.Root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                foreach (var h in anims) h.Stop();
                anims.Clear();
            });
        }

        public static void StopAll(List<Anim.Handle> anims)
        {
            foreach (var h in anims) h.Stop();
            anims.Clear();
        }

        /// <summary>Makes an element clickable (pointer up inside it), like setInteractive().on('pointerup').</summary>
        public static void OnClick(VisualElement e, Action run)
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<PointerUpEvent>(ev =>
            {
                if (GameRoot.I.Screen?.InputEnabled == false) return;
                ev.StopPropagation();
                run();
            });
        }

        /// <summary>All the ids of one item type, in data order.</summary>
        public static List<ItemDef> ItemsOfType(string type) => Db.Data.Items.Where(i => i.Type == type).ToList();
    }
}
