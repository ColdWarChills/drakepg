using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// Grid inventory (I) and the shop, a port of src/ui/inventory.ts. Both share the grid, the tabs and the item card:
    /// - bag:  hero with 8 equipment slots around it on the left; equip by double-click, the card's button, or drag.
    /// - shop: Buy (the merchant's stock, always Standard) and Sell (your bag) on the left.
    /// </summary>
    public static class InventoryPanel
    {
        /// <summary>
        /// The inventory (openInventory). `onChange` runs after every change and when it closes; `onRecall`: reading a
        /// Recall Scroll (the room handles the trip; without it the scroll can't be read here).
        /// </summary>
        public static UiModal Open(Player player, Action onChange, Action onRecall = null) =>
            new View(player, "bag", "Inventory", new List<string>(), onChange, onRecall).Modal;

        /// <summary>Same as `Open`, named like the TS.</summary>
        public static UiModal OpenInventory(Player player, Action onChange, Action onRecall = null) => Open(player, onChange, onRecall);

        /// <summary>A merchant's shop (openShop): Buy from `stock` (item ids), Sell from the bag.</summary>
        public static UiModal OpenShop(Player player, string title, List<string> stock, Action onChange) =>
            new View(player, "buy", title, stock ?? new List<string>(), onChange, null).Modal;

        /// <summary>
        /// A still hero for menus, feet at (x, y), like drawFigure: the outfit art 240 px tall (at most 300 wide) times the
        /// look's scale and `scale`, the weapon in its fists when both have art (Figure.cs's rules), a soft shadow.
        /// Missing art falls back to a simple shape. Not clickable.
        /// </summary>
        public static VisualElement HeroFigure(VisualElement parent, float x, float y, Look look, ItemDef weapon = null, float scale = 1)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = "hero" };
            Ui.Place(root, 0, 0);
            parent?.Add(root);
            if (look == null) return root;
            var k = (float)look.Scale * scale;
            var bare = look.Bare?.Find(b => b != null && Art.Has(b.Art));
            var weaponArt = weapon?.Art != null && Art.Has(weapon.Art) ? weapon.Art : null;
            var key = bare != null && weaponArt != null ? bare.Art : Art.Pick(look.Art);
            var size = key != null ? Art.Size(Art.Get(key)) : Vector2.zero;
            if (size.x <= 0 || size.y <= 0)
            {
                // Placeholder: a body and head in the look's colors (Figure.BuildShape).
                var humanoid = look.Shape == "humanoid";
                float bw = (humanoid ? 60 : 130) * k, bh = (humanoid ? 150 : 90) * k, head = (humanoid ? 56 : 50) * k;
                Shadow(root, x, y, 110 * k, 26 * k);
                Ui.Rect(root, x - bw / 2, y - bh, bw, bh, Design.Hex(look.Color), bw / 2);
                var hx = humanoid ? x : x + 60 * k;
                var hy = humanoid ? y - bh - 20 * k : y - bh * 0.8f;
                Ui.Rect(root, hx - head / 2, hy - head / 2, head, head, humanoid ? Design.Hex(0xf0c8a0) : Design.Hex(look.Accent), head / 2);
                return root;
            }
            var s = Mathf.Min(Figure.ArtHeight / size.y, Figure.ArtMaxWidth / size.x);
            float w = size.x * s * k, h = size.y * s * k, top = y - h;
            Shadow(root, x, y, w * 0.7f, 26 * k);
            if (bare != null && weaponArt != null)
            {
                // Like Figure: each weapon whole behind the body (bare.hands are 1x art pixels from the top-left), the
                // body, the weapon's grip end again in front (clipped from the guard down), then each fist over the grip.
                var wSize = Art.Size(Art.Get(weaponArt));
                var wh = (float)(weapon.Size ?? 0.6) * size.y * s * k;
                var ww = wSize.y > 0 ? wSize.x * wh / wSize.y : 0;
                var grip = (float)(weapon.Grip ?? 0.85);
                // Figure.GuardRoom as a fraction of the weapon: the weapon is Size x 240 design px tall there.
                var cut = Mathf.Max(0, grip - Figure.GuardRoom / ((float)(weapon.Size ?? 0.6) * Figure.ArtHeight)) * wh;
                var hands = bare.Hands ?? new List<Hand>();
                foreach (var hand in hands)
                {
                    var hx = x + ((float)hand.X - size.x / 2) * s * k;
                    var hy = top + (float)hand.Y * s * k;
                    var img = Ui.Image(root, hx - ww / 2, hy - grip * wh, ww, wh, weaponArt);
                    img.style.transformOrigin = new TransformOrigin(new Length(ww / 2, LengthUnit.Pixel), new Length(grip * wh, LengthUnit.Pixel));
                    img.style.rotate = new Rotate(Angle.Degrees((float)hand.Angle));
                }
                Ui.Image(root, x - w / 2, top, w, h, key);
                foreach (var hand in hands)
                {
                    var hx = x + ((float)hand.X - size.x / 2) * s * k;
                    var hy = top + (float)hand.Y * s * k;
                    var clip = new VisualElement { pickingMode = PickingMode.Ignore, name = "grip" };
                    Ui.Place(clip, hx - ww / 2, hy - grip * wh + cut);
                    Ui.Size(clip, ww, wh - cut);
                    clip.style.overflow = Overflow.Hidden;
                    clip.style.transformOrigin = new TransformOrigin(new Length(ww / 2, LengthUnit.Pixel), new Length(grip * wh - cut, LengthUnit.Pixel));
                    clip.style.rotate = new Rotate(Angle.Degrees((float)hand.Angle));
                    Ui.Image(clip, 0, -cut, ww, wh, weaponArt);
                    root.Add(clip);
                }
                var body = Art.Get(key);
                foreach (var hand in hands)
                {
                    // The fingers that wrap the handle on top, like Figure; a hand without them gets a circle of the fist.
                    if (hand.Fingers != null && Art.Has(hand.Fingers.Art))
                    {
                        var fs = Art.Size(Art.Get(hand.Fingers.Art));
                        Ui.Image(root, x + ((float)hand.Fingers.X - size.x / 2) * s * k, top + (float)hand.Fingers.Y * s * k, fs.x * s * k, fs.y * s * k,
                            hand.Fingers.Art);
                        continue;
                    }
                    var circle = new List<Vector2>();
                    for (int i = 0; i < 24; i++)
                    {
                        var ang = i / 24f * Mathf.PI * 2;
                        circle.Add(new Vector2((float)hand.X + Mathf.Cos(ang) * Figure.FistRadius, (float)hand.Y + Mathf.Sin(ang) * Figure.FistRadius));
                    }
                    if (body != null) root.Add(new HandOverGrip(body, size, circle, x - size.x / 2 * s * k, top, s * k));
                }
                return root;
            }
            Ui.Image(root, x - w / 2, top, w, h, key);
            return root;
        }

        /// <summary>
        /// The menus' version of Figure's circle of the fist over the grip (hands without a fingers sprite): the body's own
        /// pixels inside `poly` (1x art px from the art's top-left) as a textured mesh. The art's top-left is drawn at
        /// (left, top) with `scale` UI px per 1x px.
        /// </summary>
        sealed class HandOverGrip : VisualElement
        {
            public HandOverGrip(Sprite body, Vector2 size, List<Vector2> poly, float left, float top, float scale)
            {
                pickingMode = PickingMode.Ignore;
                name = "fist";
                Ui.Place(this, left, top);
                Ui.Size(this, size.x * scale, size.y * scale);
                var tex = body.texture;
                var k = body.rect.width / size.x;
                // UI meshes are wound clockwise (y down).
                float area = 0;
                for (int i = 0; i < poly.Count; i++) area += poly[i].x * poly[(i + 1) % poly.Count].y - poly[(i + 1) % poly.Count].x * poly[i].y;
                if (area < 0) poly = Enumerable.Reverse(poly).ToList();
                generateVisualContent += ctx =>
                {
                    if (poly.Count < 3) return;
                    var mesh = ctx.Allocate(poly.Count, (poly.Count - 2) * 3, tex);
                    foreach (var p in poly)
                        mesh.SetNextVertex(new Vertex
                        {
                            position = new Vector3(p.x * scale, p.y * scale, Vertex.nearZ),
                            tint = Color.white,
                            uv = mesh.uvRegion.position + Vector2.Scale(mesh.uvRegion.size,
                                new Vector2((body.rect.x + p.x * k) / tex.width, (body.rect.y + (size.y - p.y) * k) / tex.height)),
                        });
                    for (int i = 1; i + 1 < poly.Count; i++)
                    {
                        mesh.SetNextIndex(0);
                        mesh.SetNextIndex((ushort)i);
                        mesh.SetNextIndex((ushort)(i + 1));
                    }
                };
            }
        }

        static void Shadow(VisualElement parent, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(e, x - w / 2, y - h / 2);
            Ui.Size(e, w, h);
            e.style.backgroundImage = new StyleBackground(Art.Soft);
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.45f);
            parent.Add(e);
        }

        /// <summary>The padlock drawn on locked gear, centered on (x, y) (lockBadge).</summary>
        public static VisualElement LockBadge(VisualElement parent, float x, float y) =>
            ItemUi.Paint(parent, x - 10, y - 10, 20, 20, g =>
            {
                var c = Design.Hex(0xe8d9a8);
                g.StrokeCircle(c, 2.5f, 0, -3, 5);
                g.FillRect(c, -7, -2, 14, 10);
                g.FillRect(Design.Hex(0x2a2236), -1, 1, 2, 4);
            });

        // --- Flow layout helpers: stacked text whose height is only known after layout ---

        static VisualElement Flow(VisualElement parent)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.flexShrink = 0;
            parent?.Add(e);
            return e;
        }

        /// <summary>A fixed-height row; its children are placed absolutely inside it.</summary>
        static VisualElement Row(VisualElement parent, float height)
        {
            var r = Flow(parent);
            r.style.height = height;
            return r;
        }

        /// <summary>A label in a flow column (below the previous one).</summary>
        static Label FlowText(VisualElement parent, string text, float size, Color? color = null, bool bold = false, float wrap = 0)
        {
            var l = Ui.Text(null, 0, 0, text, size, color, bold, wrap: wrap);
            l.style.position = Position.Relative;
            l.style.flexShrink = 0;
            parent?.Add(l);
            return l;
        }

        static string N(double v) => JsMath.Str(v);

        enum EntryKind { Gear, Stack, Stock }

        /// <summary>A grid cell's item: owned gear (worn when `Slot` is set), a stack of consumables, or shop stock.</summary>
        sealed class Entry
        {
            public EntryKind Kind;
            public string Key;
            public Gear Gear;
            public ItemDef Def;
            public string Slot;
            public int Qty;
            public bool IsGear => Kind == EntryKind.Gear;
        }

        sealed class CardButton
        {
            public string Label;
            public Action Run;
            public bool Enabled = true;
        }

        sealed class View
        {
            const float W = 1240, H = 680;
            const float Cell = 80, Gap = 8;
            const int Cols = 6, Rows = 5, PerPage = Cols * Rows;
            const float GridX = 316, GridY = 132, GridW = Cols * Cell + (Cols - 1) * Gap;
            const float CardX = 858, CardY = 70, CardW = 358, CardH = 590;
            /// <summary>The hero's equipment slot size.</summary>
            const float EqSize = 58;
            const float GhostSize = 70;
            /// <summary>A small move turns a press into a drag; less than that is a click (design px).</summary>
            const float DragThreshold = 10;
            /// <summary>Grades below this are offered by "Sell junk" (Standard and Fair).</summary>
            const double JunkBelow = 40;

            static readonly Color SlotBg = ItemUi.SlotBg;
            static readonly Color SlotHover = Design.Hex(0x3a3050);
            static readonly Color EmptyBorder = Design.Hex(0x4a3f5a);
            static readonly Color ActiveButton = Design.Hex(0x6b5320);
            static readonly Color CardBg = Design.Hex(0x241d30);
            static readonly Color BarBg = Design.Hex(0x111111);
            static readonly Color NewDot = Design.Hex(0xffd34d);

            /// <summary>The hero's equipment slots: left column armor, right column weapon and jewelry; the spare weapon beside the weapon.</summary>
            static readonly Dictionary<string, Vector2> EqPos = new Dictionary<string, Vector2>
            {
                ["helm"] = new Vector2(42, 112),
                ["body"] = new Vector2(42, 178),
                ["gloves"] = new Vector2(42, 244),
                ["boots"] = new Vector2(42, 310),
                ["weapon"] = new Vector2(258, 112),
                ["necklace"] = new Vector2(258, 178),
                ["ring1"] = new Vector2(258, 244),
                ["ring2"] = new Vector2(258, 310),
                ["spare"] = new Vector2(192, 112),
            };

            static readonly (string tab, string label)[] Tabs =
            {
                ("all", "All"), ("weapon", "Weapons"), ("armor", "Armor"), ("accessory", "Jewelry"), ("consumable", "Items"), ("material", "Materials"),
            };

            static readonly (string sort, string label)[] Sorts = { ("newest", "Newest"), ("grade", "Grade"), ("type", "Type"), ("value", "Value") };

            static readonly Dictionary<string, int> TypeOrder = new Dictionary<string, int>
            {
                ["weapon"] = 0, ["armor"] = 1, ["accessory"] = 2, ["consumable"] = 3, ["book"] = 4, ["scroll"] = 5, ["stone"] = 6, ["material"] = 7,
            };

            static readonly Dictionary<string, string> NonGearKind = new Dictionary<string, string>
            {
                ["material"] = "Material", ["stone"] = "Roll stone", ["consumable"] = "Potion", ["scroll"] = "Scroll", ["book"] = "Skill book",
            };

            /// <summary>Remembered while the game runs, so the inventory reopens the way you left it.</summary>
            static string rememberedTab = "all", rememberedSort = "newest";

            static readonly IRng rng = new SystemRng();

            public readonly UiModal Modal;
            readonly Player player;
            readonly List<string> stock;
            readonly Action onChange, onRecall;
            string mode;
            int page;
            string selected;
            VisualElement card;
            List<Entry> entries = new List<Entry>();
            readonly List<(string slot, VisualElement bg)> equipSlots = new List<(string slot, VisualElement bg)>();
            VisualElement gridZone;
            bool dragging, finished;
            VisualElement dragBg, ghost;
            Vector2 pressAt;
            float lastDragEnd = -10;
            string lastClickKey = "";
            float lastClickTime = -10;
            /// <summary>Where each item sits in this render (grid cells by key, the hero's slots by slot), for things flying between them.</summary>
            readonly Dictionary<string, (Vector2 at, VisualElement view)> cells = new Dictionary<string, (Vector2, VisualElement)>();
            readonly Dictionary<string, VisualElement> slotViews = new Dictionary<string, VisualElement>();
            VisualElement heroView;
            /// <summary>The hero's stat lines before an equip, so the render after it can light up the ones that changed.</summary>
            List<string> statsBefore;

            static float Now => Time.realtimeSinceStartup;
            static bool InputBlocked => GameRoot.I != null && GameRoot.I.Screen?.InputEnabled == false;

            public View(Player player, string mode, string title, List<string> stock, Action onChange, Action onRecall)
            {
                this.player = player;
                this.mode = mode;
                this.stock = stock;
                this.onChange = onChange;
                this.onRecall = onRecall;
                Modal = new UiModal(W, H, title, () => { });
                Modal.Root.RegisterCallback<DetachFromPanelEvent>(e =>
                {
                    if ((object)e.target != Modal.Root || finished) return;
                    finished = true;
                    CancelDragVisuals();
                    // Everything seen while open is no longer new.
                    foreach (var g in player.Data.Gear) g.IsNew = null;
                    // Skip when the whole screen is going away (the modal wasn't closed, its screen was).
                    if (!Modal.Closed) return;
                    onChange?.Invoke();
                });
                Render();
            }

            // --- Data ---

            Gear Worn(string slot) => player.Data.Equipment != null && player.Data.Equipment.TryGetValue(slot, out var g) ? g : null;

            List<Entry> BagEntries()
            {
                var list = player.Data.Gear.Select(g => new Entry { Kind = EntryKind.Gear, Key = g.Uid, Gear = g, Def = Items.GearDef(g) }).ToList();
                // Newest first; uids count up.
                list.Reverse();
                list.AddRange(player.Data.Inventory.Select(e => new Entry { Kind = EntryKind.Stack, Key = "stack:" + e.ItemId, Def = Db.Item(e.ItemId), Qty = e.Qty }));
                return list;
            }

            Entry EquippedEntry(string slot)
            {
                var g = Worn(slot);
                return g != null ? new Entry { Kind = EntryKind.Gear, Key = g.Uid, Gear = g, Def = Items.GearDef(g), Slot = slot } : null;
            }

            List<Entry> Shown()
            {
                var all = mode == "buy"
                    ? stock.Select(id => new Entry { Kind = EntryKind.Stock, Key = "stock:" + id, Def = Db.Item(id) }).ToList()
                    : BagEntries();
                // Materials live in their own pouch (no bag slots), so only their tab shows them.
                // Stones and scrolls sit with the potions under Items.
                string TabOf(string t) => t == "stone" || t == "scroll" || t == "book" ? "consumable" : t;
                var filtered = rememberedTab == "all"
                    ? all.Where(e => e.Def.Type != "material").ToList()
                    : all.Where(e => TabOf(e.Def.Type) == rememberedTab).ToList();
                // Sigils rank above every grade: Eclipse 300+, Dusk 200+, Dawn 100+.
                double SigilRank(Entry e) => e.IsGear && e.Gear.Sigil != null ? (Array.IndexOf(Items.SigilTiers, e.Gear.Sigil) + 1) * 100 : 0;
                double GradeOf(Entry e) => e.IsGear ? SigilRank(e) + Items.Grade(e.Def, e.Gear.Rolls, e.Gear.Sigil) : -1;
                double ValueOf(Entry e) => e.IsGear ? Items.SellValue(e.Def, e.Gear) : e.Kind == EntryKind.Stack ? Items.SellValue(e.Def) : e.Def.Price;
                int TypeOf(Entry e) => TypeOrder.TryGetValue(e.Def.Type, out var o) ? o : 99;
                switch (rememberedSort)
                {
                    case "grade":
                        return filtered.OrderByDescending(e => GradeOf(e)).ThenBy(e => TypeOf(e)).ToList();
                    case "type":
                        return filtered.OrderBy(e => TypeOf(e)).ThenBy(e => e.Def.Name, StringComparer.InvariantCulture).ThenByDescending(e => GradeOf(e)).ToList();
                    case "value":
                        return filtered.OrderByDescending(e => ValueOf(e)).ToList();
                    default:
                        return filtered;
                }
            }

            /// <summary>The hero's slots: the worn ones, plus the spare weapon once weapon swaps are open (or one is already carried).</summary>
            string[] HeroSlots => Features.On(player, "weapon_swap") || Worn("spare") != null ? Items.AllSlots : Items.EquipSlots;

            Entry Find(string key)
            {
                if (key == null) return null;
                var e = entries.FirstOrDefault(x => x.Key == key);
                if (e != null) return e;
                foreach (var slot in HeroSlots)
                {
                    var q = EquippedEntry(slot);
                    if (q != null && q.Key == key) return q;
                }
                return null;
            }

            // --- Layout ---

            void Render()
            {
                if (dragging)
                {
                    dragging = false;
                    CancelDragVisuals();
                }
                var c = Modal.Content;
                c.Clear();
                equipSlots.Clear();
                cells.Clear();
                slotViews.Clear();
                heroView = null;
                entries = Shown();
                if (selected != null && Find(selected) == null) selected = null;

                if (mode == "bag") RenderHero();
                else RenderShopSide();

                // Tabs. Materials tab once materials drop, or as soon as you carry one (a quest item).
                var hasMaterials = player.Data.Inventory.Any(e => Db.Item(e.ItemId).Type == "material");
                var tabs = Tabs.Where(t => t.tab != "material" || hasMaterials || Features.On(player, "materials")).ToList();
                if (!tabs.Any(t => t.tab == rememberedTab)) rememberedTab = "all";
                for (int i = 0; i < tabs.Count; i++)
                {
                    var t = tabs[i];
                    var active = rememberedTab == t.tab;
                    new UiButton(c, GridX + 42 + i * 88, 96, t.label, () =>
                    {
                        rememberedTab = t.tab;
                        page = 0;
                        Render();
                    }, 84, 40, 14, null, active ? ActiveButton : Palette.Button);
                }

                // Grid area (also the drop target for unequipping).
                gridZone = Ui.Rect(c, GridX - 6, GridY - 6, GridW + 12, Rows * Cell + (Rows - 1) * Gap + 12, new Color(0, 0, 0, 0.18f), 10);
                var pages = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)PerPage));
                page = Math.Max(0, Math.Min(page, pages - 1));
                var onPage = entries.Skip(page * PerPage).Take(PerPage).ToList();
                for (int i = 0; i < PerPage; i++)
                {
                    var x = GridX + (i % Cols) * (Cell + Gap) + Cell / 2;
                    var y = GridY + (i / Cols) * (Cell + Gap) + Cell / 2;
                    Slot(c, x, y, i < onPage.Count ? onPage[i] : null);
                }

                // Bottom row: sort, pages, capacity.
                var sortIndex = Math.Max(0, Array.FindIndex(Sorts, s => s.sort == rememberedSort));
                new UiButton(c, GridX + 85, 606, $"Sort: {Sorts[sortIndex].label}", () =>
                {
                    rememberedSort = Sorts[(sortIndex + 1) % Sorts.Length].sort;
                    Render();
                }, 170, 42, 17);
                if (pages > 1)
                {
                    var mid = GridX + GridW / 2 + 40;
                    Ui.Text(c, mid, 606, $"{page + 1} / {pages}", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
                    new UiButton(c, mid - 60, 606, "<", () =>
                    {
                        page -= 1;
                        Render();
                    }, 44, 38).SetEnabled(page > 0);
                    new UiButton(c, mid + 60, 606, ">", () =>
                    {
                        page += 1;
                        Render();
                    }, 44, 38).SetEnabled(page < pages - 1);
                }
                if (mode != "buy")
                {
                    var used = player.UsedSlots;
                    var full = used >= Db.B.BagSlots;
                    Ui.Text(c, GridX + GridW, 606, $"Bag {used} / {Db.B.BagSlots}", 18, full ? Palette.TextBad : Palette.TextDim, full, 1, 0.5f);
                }

                card = new VisualElement { pickingMode = PickingMode.Ignore, name = "item-card" };
                Ui.Place(card, CardX, CardY);
                Ui.Size(card, CardW, CardH);
                c.Add(card);
                ShowCard(Find(selected));
            }

            void RenderHero()
            {
                var c = Modal.Content;
                heroView = HeroFigure(c, 150, 352, player.Look, player.Weapon, 0.8f);

                foreach (var slot in HeroSlots) Slot(c, EqPos[slot].x, EqPos[slot].y, EquippedEntry(slot), slot, EqSize);
                // A filled spare slot looks like any weapon: name it.
                if (Worn("spare") != null) Ui.Text(c, EqPos["spare"].x, EqPos["spare"].y - EqSize / 2 - 3, "Spare", 12, Palette.TextDim, ax: 0.5f, ay: 1);

                var lines = StatLines();
                var col = Flow(c);
                Ui.Place(col, 20, 378);
                col.style.width = 284;
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = FlowText(col, lines[i], 15, null, false, 284);
                    if (i > 0) l.style.marginTop = 5;
                    // A line an equip just changed glows gold and fades back.
                    if (statsBefore == null || (i < statsBefore.Count && statsBefore[i] == lines[i])) continue;
                    Anim.Float(0, 1, 1.4f, t => l.style.color = Color.Lerp(Palette.TextGold, Palette.Text, t), Ease.QuadIn, 0.25f);
                }
                statsBefore = null;
                // Sets being worn, and what they switch on.
                var active = Sets.ActiveSetBonuses(player.Data.Equipment);
                var first = true;
                foreach (var (id, count) in Sets.SetCounts(player.Data.Equipment))
                {
                    var set = Db.Set(id);
                    var t = FlowText(col, $"{set.Name} set {count}/{set.Pieces.Count}", 15, Palette.TextGold, true);
                    t.style.marginTop = first ? 8 : 0;
                    t.style.marginBottom = 2;
                    first = false;
                    foreach (var b in set.Bonuses)
                    {
                        var on = active.Any(a => a.set.Id == id && ReferenceEquals(a.bonus, b));
                        var bt = FlowText(col, $"({b.Pieces}) {ItemUi.SetBonusText(b)}", 13, on ? Palette.TextGood : Palette.TextDim, false, 270);
                        bt.style.marginLeft = 8;
                        bt.style.marginBottom = 2;
                    }
                }
            }

            /// <summary>The hero's stat lines under the figure: attack, spare, crit, defense, HP/MP, gold.</summary>
            List<string> StatLines()
            {
                var range = player.AttackRange;
                var element = player.WeaponElement;
                var spare = player.SpareWeapon;
                var spareRange = player.SpareAttackRange;
                var lines = new List<string>
                {
                    $"Attack {N(range[0])}-{N(range[1])} {ItemUi.DamageTypeLabel(player.WeaponDamageType)} · {(element != null ? ItemUi.ElementLabel(element) : "Neutral")}",
                };
                if (spare != null && spareRange != null)
                    lines.Add($"Spare {N(spareRange[0])}-{N(spareRange[1])} {ItemUi.DamageTypeLabel(spare.DamageType)} · {(spare.Element != null ? ItemUi.ElementLabel(spare.Element) : "Neutral")}");
                var damage = player.DamageBonus != 0 ? $" · Damage +{N(JsMath.Round(player.DamageBonus * 100))}%" : "";
                lines.Add($"Crit {(player.CritChance * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%{damage}");
                lines.Add(ItemUi.DefenseText(player.DefenseFor("melee"), player.DefenseFor("pierce"), player.DefenseFor("magic")));
                lines.Add($"Max HP {N(player.MaxHp)} · Max MP {N(player.MaxMp)}");
                lines.Add($"Gold {N(player.Gold)}");
                return lines;
            }

            void RenderShopSide()
            {
                var c = Modal.Content;
                Ui.Text(c, 30, 70, $"Gold: {N(player.Gold)}", 26, Palette.TextGold, true);
                var modes = new[] { "buy", "sell" };
                for (int i = 0; i < modes.Length; i++)
                {
                    var m = modes[i];
                    new UiButton(c, 90 + i * 130, 150, m == "buy" ? "Buy" : "Sell", () =>
                    {
                        mode = m;
                        page = 0;
                        selected = null;
                        Render();
                    }, 120, 48, 22, null, mode == m ? ActiveButton : Palette.Button);
                }
                var hint = mode == "buy"
                    ? "Shop gear is always Standard (0%). Gear from monsters can roll better."
                    : "Better grades sell for more, Sigils far more. Locked items are never sold.";
                Ui.Text(c, 30, 196, hint, 16, Palette.TextDim, wrap: 260);

                if (mode == "sell")
                {
                    var junk = player.Data.Gear
                        .Where(g => g.Locked != true && g.Sigil == null && (g.Plus ?? 0) == 0 && Items.Grade(Items.GearDef(g), g.Rolls, g.Sigil) < JunkBelow)
                        .ToList();
                    var value = junk.Sum(g => Items.SellValue(Items.GearDef(g), g));
                    new UiButton(c, 150, 310, "Sell junk", () =>
                    {
                        double gold = 0;
                        foreach (var g in junk) gold += player.SellGear(g.Uid);
                        Sfx.Play("coin");
                        Ui.Toast($"Sold {junk.Count} item{(junk.Count > 1 ? "s" : "")} for {N(gold)} g", Palette.TextGold);
                        Changed();
                    }, 250, 60, 22, junk.Count > 0 ? $"{junk.Count} Standard/Fair · {N(value)} g" : "Nothing to sell").SetEnabled(junk.Count > 0);
                }
            }

            /// <summary>One grid cell, centered on (x, y). `equipSlot` marks the hero's equipment slots (drawn at `size`).</summary>
            VisualElement Slot(VisualElement parent, float x, float y, Entry entry, string equipSlot = null, float size = Cell)
            {
                var s = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(s, x - size / 2, y - size / 2);
                Ui.Size(s, size, size);
                parent.Add(s);
                var info = entry != null ? ItemUi.GradeInfo(entry.Def, entry.IsGear ? entry.Gear : null) : null;
                var bg = Ui.Rect(s, 0, 0, size, size, SlotBg, 8, entry != null ? 3 : 2, info != null ? info.Frame : EmptyBorder, true);
                s.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                if (equipSlot != null)
                {
                    equipSlots.Add((equipSlot, bg));
                    slotViews[equipSlot] = s;
                }
                else if (entry != null) cells[entry.Key] = (new Vector2(x, y), s);
                if (entry == null)
                {
                    if (equipSlot != null && Items.SlotLabels.TryGetValue(equipSlot, out var label))
                        Ui.Text(s, size / 2, size / 2, label, 12, Palette.TextDim, ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter);
                    return s;
                }

                if (entry.Key == selected) Ui.Rect(s, -4, -4, size + 8, size + 8, Color.clear, 11, 3, Color.white);
                var iconSize = Mathf.Round(size * 0.75f);
                var icon = ItemUi.DrawItemIcon(s, size / 2, size / 2, iconSize, entry.Def);
                if (info.Sigil != null)
                {
                    ItemUi.AnimateSigilIcon(icon, info.Sigil, iconSize);
                    Ui.Text(s, 5, size - 2, "✦", 16, ItemUi.SigilColors[info.Sigil].TextColor, true, 0, 1, stroke: true);
                }
                if (entry.Kind == EntryKind.Stack && entry.Qty > 1)
                    Ui.Text(s, size / 2 + Cell / 2 - 6, size / 2 + Cell / 2 - 4, entry.Qty.ToString(), 17, null, true, 1, 1, stroke: true);
                if (entry.Kind == EntryKind.Stock)
                    Ui.Text(s, size / 2, size / 2 + Cell / 2 - 3, $"{N(entry.Def.Price)} g", 14, Palette.TextGold, true, 0.5f, 1, stroke: true);
                VisualElement newDot = null;
                if (entry.IsGear)
                {
                    if (entry.Gear.Locked == true) LockBadge(s, 13, 13);
                    if (entry.Gear.IsNew == true) newDot = Ui.Rect(s, size - 16, 4, 12, 12, NewDot, 6, 2, Color.black);
                    if (equipSlot == null && info.Tier != "standard")
                        Ui.Text(s, size / 2 + Cell / 2 - 5, size / 2 + Cell / 2 - 3, $"{N(info.Grade)}%", 12, info.TextColor, true, 1, 1, stroke: true);
                }

                Interactive(bg, entry, mode == "bag" && entry.IsGear, newDot);
                return s;
            }

            /// <summary>Hover shows the card, a click selects (double-click equips/uses), and bag gear drags.</summary>
            void Interactive(VisualElement bg, Entry entry, bool draggable, VisualElement newDot)
            {
                bg.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (dragging) return;
                    bg.style.backgroundColor = SlotHover;
                    if (entry.IsGear && entry.Gear.IsNew == true)
                    {
                        entry.Gear.IsNew = null;
                        newDot?.RemoveFromHierarchy();
                    }
                    ShowCard(entry);
                });
                bg.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    bg.style.backgroundColor = SlotBg;
                    if (!dragging) ShowCard(Find(selected));
                });
                bg.RegisterCallback<PointerDownEvent>(e =>
                {
                    // Right-click in the bag does what a double-click does: read a book, drink a potion, equip.
                    if (e.button == 1 && mode == "bag" && entry != null && !InputBlocked)
                    {
                        e.StopPropagation();
                        selected = entry.Key;
                        Primary(entry);
                        return;
                    }
                    if (e.button != 0 || InputBlocked) return;
                    pressAt = Local(e.position);
                    bg.CapturePointer(e.pointerId);
                    e.StopPropagation();
                });
                bg.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (!draggable || !bg.HasPointerCapture(e.pointerId)) return;
                    var at = Local(e.position);
                    if (!dragging)
                    {
                        if ((at - pressAt).sqrMagnitude < DragThreshold * DragThreshold) return;
                        StartDrag(bg, entry, at);
                    }
                    if (ghost != null)
                    {
                        ghost.style.left = at.x - GhostSize / 2;
                        ghost.style.top = at.y - GhostSize / 2;
                    }
                });
                bg.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!bg.HasPointerCapture(e.pointerId)) return;
                    var wasDragging = dragging && dragBg == bg;
                    var pos = (Vector2)e.position;
                    if (wasDragging) dragging = false;
                    bg.ReleasePointer(e.pointerId);
                    e.StopPropagation();
                    if (wasDragging)
                    {
                        EndDrag(entry, pos);
                        return;
                    }
                    if (InputBlocked || Now - lastDragEnd < 0.08f || !bg.worldBound.Contains(pos)) return;
                    Click(entry);
                });
                bg.RegisterCallback<PointerCaptureOutEvent>(_ =>
                {
                    // Capture lost mid-drag (window focus, the element went away): put everything back.
                    if (!dragging || dragBg != bg) return;
                    dragging = false;
                    lastDragEnd = Now;
                    CancelDragVisuals();
                    Render();
                });
            }

            /// <summary>A panel-space pointer position in the modal's content coordinates (design px from the panel's top-left).</summary>
            Vector2 Local(Vector2 panelPos) => Modal.Content.WorldToLocal(panelPos);

            /// <summary>Drag bag gear onto the hero side to equip it; drag equipped gear onto the grid to take it off.</summary>
            void StartDrag(VisualElement bg, Entry entry, Vector2 at)
            {
                dragging = true;
                dragBg = bg;
                ghost = ItemUi.DrawItemIcon(Modal.Content, at.x, at.y, GhostSize, entry.Def);
                ghost.style.opacity = 0.85f;
                bg.style.opacity = 0.4f;
                foreach (var t in equipSlots)
                    if (entry.Slot == null && Items.FitsSlot(entry.Def, t.slot)) Ui.Border(t.bg, 4, Color.white);
            }

            void CancelDragVisuals()
            {
                ghost?.RemoveFromHierarchy();
                ghost = null;
                if (dragBg != null) dragBg.style.opacity = 1;
                dragBg = null;
            }

            void EndDrag(Entry entry, Vector2 panelPos)
            {
                lastDragEnd = Now;
                CancelDragVisuals();
                var heroSide = Local(panelPos).x < 300;
                // Dropped on a slot it fits (a ring on the second ring slot, a weapon on the spare slot): there. Anywhere else on
                // the hero side: its usual slot. The weapon in hand dropped on the spare slot (or back) swaps the two.
                string target = null;
                foreach (var t in equipSlots)
                {
                    if (!t.bg.worldBound.Contains(panelPos)) continue;
                    target = t.slot;
                    break;
                }
                var swap = (entry.Slot == "weapon" && target == "spare") || (entry.Slot == "spare" && target == "weapon");
                if (entry.Slot == null && heroSide) Equip(entry, target, Local(panelPos));
                else if (swap) SwapWeapons();
                else if (entry.Slot != null && gridZone != null && gridZone.worldBound.Contains(panelPos)) Unequip(entry.Slot);
                else Render();
            }

            void Click(Entry entry)
            {
                var now = Now;
                var twice = lastClickKey == entry.Key && now - lastClickTime < 0.35f;
                lastClickKey = entry.Key;
                lastClickTime = now;
                selected = entry.Key;
                if (twice && mode == "bag")
                {
                    Primary(entry);
                    return;
                }
                Render();
            }

            /// <summary>Double-click action in the bag: equip, unequip or use.</summary>
            void Primary(Entry entry)
            {
                if (entry.IsGear)
                {
                    if (entry.Slot != null) Unequip(entry.Slot);
                    else Equip(entry);
                }
                else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "consumable") Use(entry.Def);
                else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "scroll") onRecall?.Invoke();
                else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "book") Read(entry.Def);
                else Render();
            }

            // --- Actions ---

            void Changed()
            {
                onChange?.Invoke();
                Render();
            }

            /// <summary>`from`: where the item was let go (a drag), else its grid cell.</summary>
            void Equip(Entry entry, string slot = null, Vector2? from = null)
            {
                var start = from ?? (cells.TryGetValue(entry.Key, out var cell) ? cell.at : (Vector2?)null);
                var before = Numbers();
                statsBefore = StatLines();
                if (!player.Equip(entry.Gear.Uid, slot))
                {
                    statsBefore = null;
                    Render();
                    return;
                }
                Sfx.Play("equip");
                selected = entry.Gear.Uid;
                Changed();
                var landed = HeroSlots.FirstOrDefault(sl => Worn(sl)?.Uid == entry.Gear.Uid);
                if (landed != null && slotViews.TryGetValue(landed, out var view))
                {
                    Fly(entry.Def, start, EqPos[landed], view, EqSize);
                    HeroHop();
                }
                StatFloats(before);
            }

            /// <summary>The spare weapon into the hand and the old one to the spare slot (either may be empty).</summary>
            void SwapWeapons()
            {
                var before = Numbers();
                statsBefore = StatLines();
                if (!player.SwapWeapons())
                {
                    statsBefore = null;
                    Render();
                    return;
                }
                Sfx.Play("equip");
                Changed();
                foreach (var sl in new[] { "weapon", "spare" })
                    if (slotViews.TryGetValue(sl, out var view)) Land(view, EqPos[sl], EqSize);
                HeroHop();
                StatFloats(before);
            }

            void Unequip(string slot)
            {
                var gear = Worn(slot);
                var before = Numbers();
                statsBefore = StatLines();
                if (!player.Unequip(slot))
                {
                    statsBefore = null;
                    Sfx.Play("denied");
                    Ui.Toast("Your bag is full", Palette.TextBad);
                    return;
                }
                Sfx.Play("equip");
                Changed();
                // Back into the bag: it flies from the slot to its cell (when that cell is on this page).
                if (gear != null && cells.TryGetValue(gear.Uid, out var cell)) Fly(Items.GearDef(gear), EqPos[slot], cell.at, cell.view, Cell);
                StatFloats(before);
            }

            // --- Equip feedback ---

            /// <summary>An item's icon flies from `from` to `to` in an arc; the place it lands in stays empty until then.</summary>
            void Fly(ItemDef def, Vector2? from, Vector2 to, VisualElement landing, float landSize)
            {
                if (from == null || (from.Value - to).sqrMagnitude < 4)
                {
                    Land(landing, to, landSize);
                    return;
                }
                var a = from.Value;
                landing.style.opacity = 0;
                const float size = 64;
                var flyer = ItemUi.DrawItemIcon(Modal.Content, a.x, a.y, size, def);
                flyer.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                var lift = Mathf.Min(90, 30 + Vector2.Distance(a, to) * 0.18f);
                var end = landSize * 0.75f / size;
                Sfx.Play("whoosh");
                Anim.Float(0, 1, 0.3f, t =>
                {
                    var p = Vector2.LerpUnclamped(a, to, t) + Vector2.down * Mathf.Sin(t * Mathf.PI) * lift;
                    flyer.style.left = p.x - size / 2;
                    flyer.style.top = p.y - size / 2;
                    var s = Mathf.Lerp(1.1f, end, t);
                    flyer.style.scale = new Scale(new Vector3(s, s, 1));
                    flyer.style.rotate = new Rotate(Mathf.Sin(t * Mathf.PI) * -14);
                }, Ease.CubicInOut).OnComplete(() =>
                {
                    flyer.RemoveFromHierarchy();
                    Land(landing, to, landSize);
                });
            }

            /// <summary>An item arriving in its place: the slot pops and a gold ring bursts from it.</summary>
            void Land(VisualElement view, Vector2 at, float size)
            {
                view.style.opacity = 1;
                Anim.Float(1.3f, 1, 0.32f, s => view.style.scale = new Scale(new Vector3(s, s, 1)), Ease.BackOut);
                UiMotion.Ring(Modal.Content, at.x, at.y, size * 0.75f, Palette.TextGold, 3, 0.4f);
            }

            /// <summary>The hero in the panel hops a little in the new gear.</summary>
            void HeroHop()
            {
                var hero = heroView;
                if (hero == null) return;
                Anim.Float(0, 1, 0.34f, t => hero.style.translate = new Translate(0, -16 * Mathf.Sin(t * Mathf.PI)), Ease.Linear, 0.18f);
                UiMotion.Ring(Modal.Content, 150, 340, 70, Palette.TextGold, 3, 0.45f, 0.3f);
            }

            /// <summary>What an equip changes, in the numbers a player compares.</summary>
            Dictionary<string, double> Numbers()
            {
                var r = player.AttackRange;
                return new Dictionary<string, double>
                {
                    ["Attack"] = r != null && r.Length > 1 ? Math.Round((r[0] + r[1]) / 2) : 0,
                    ["Defense"] = Math.Round(player.DefenseFor("melee") + player.DefenseFor("pierce") + player.DefenseFor("magic")),
                    ["Max HP"] = player.MaxHp,
                    ["Max MP"] = player.MaxMp,
                    ["Crit %"] = Math.Round(player.CritChance * 1000) / 10,
                };
            }

            /// <summary>"+4 Attack", "-2 Defense": the changes float up from the hero, green up, red down.</summary>
            void StatFloats(Dictionary<string, double> before)
            {
                var after = Numbers();
                var changes = after.Where(kv => Math.Abs(kv.Value - before[kv.Key]) > 0.001).Take(4).ToList();
                for (int i = 0; i < changes.Count; i++)
                {
                    var d = changes[i].Value - before[changes[i].Key];
                    var text = $"{(d > 0 ? "+" : "")}{N(Math.Round(d * 10) / 10)} {changes[i].Key}";
                    // From the hero's chest, under the spare-weapon slot.
                    var y = 200 + i * 26f;
                    var l = Ui.Text(Modal.Content, 150, y, text, 20, d > 0 ? Palette.TextGood : Palette.TextBad, true, 0.5f, 0.5f, stroke: true);
                    l.style.opacity = 0;
                    Anim.Float(0, 1, 1.3f, t =>
                    {
                        l.style.top = y - 40 * t;
                        l.style.opacity = t < 0.15f ? t / 0.15f : t > 0.7f ? (1 - t) / 0.3f : 1;
                    }, Ease.QuadOut, 0.25f + i * 0.12f).OnComplete(l.RemoveFromHierarchy);
                }
            }

            void Use(ItemDef def)
            {
                var r = player.UseConsumable(def.Id);
                if (r != null)
                {
                    Sfx.Play("heal");
                    Ui.Toast(ItemUi.RestoreText(r.Value.hp, r.Value.mp), Palette.TextGood);
                }
                else Ui.Toast("Already full", Palette.TextDim);
                Changed();
            }

            /// <summary>Learn a skill book's skill (the book is used up; the skill goes into a free battle slot).</summary>
            void Read(ItemDef def)
            {
                var skill = Books.BookSkill(def);
                var result = Books.ReadBook(player, def.Id);
                if (result == "ok")
                {
                    Sfx.Play("levelup");
                    Ui.Toast($"Learned {skill.Name}!", Palette.TextMagic);
                }
                else
                {
                    Sfx.Play("denied");
                    Ui.Toast(result switch
                    {
                        "class" => "Another class's book",
                        "level" => $"Needs level {Books.ReadLevelOf(skill)}",
                        _ => "You know this already",
                    }, Palette.TextBad);
                }
                Changed();
            }

            void Buy(ItemDef def)
            {
                var result = player.Buy(def.Id);
                if (result == "ok")
                {
                    Sfx.Play("coin");
                    Ui.Toast($"Bought {def.Name}", Palette.TextGood);
                }
                else
                {
                    Sfx.Play("denied");
                    Ui.Toast(result == "gold" ? "Not enough gold" : "Your bag is full", Palette.TextBad);
                }
                Changed();
            }

            void Sell(Entry entry)
            {
                var gold = entry.IsGear ? player.SellGear(entry.Gear.Uid) : entry.Kind == EntryKind.Stack ? player.SellItem(entry.Def.Id) : 0;
                if (gold <= 0) return;
                Sfx.Play("coin");
                Ui.Toast($"Sold {entry.Def.Name} for {N(gold)} g", Palette.TextGold);
                Changed();
            }

            // --- Item card ---

            void ShowCard(Entry entry)
            {
                if (card == null || card.parent == null) return;
                card.Clear();
                var gear = entry != null && entry.IsGear ? entry.Gear : null;
                var info = entry != null ? ItemUi.GradeInfo(entry.Def, gear) : null;
                var border = info != null && (info.Sigil != null || info.Tier != "standard") ? info.Frame : Palette.PanelBorder;
                Ui.Rect(card, 0, 0, CardW, CardH, CardBg, 12, info?.Sigil != null ? 3 : 2, border);
                if (entry == null || info == null)
                {
                    var hint = mode == "bag" ? "Hover or tap an item.\n\nDouble-click or drag gear\nonto the hero to equip it.\nRight-click to use or read." : "Hover or tap an item.";
                    Ui.Text(card, CardW / 2, CardH / 2, hint, 18, Palette.TextDim, ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter);
                    return;
                }

                const float left = 20;
                ItemUi.Icon(card, left + 38, 56, 76, entry.Def, gear);

                // Everything below the icon flows down; heights are only known after layout.
                var body = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(body, 0, 0);
                body.style.width = CardW;
                body.style.paddingTop = 18;
                body.style.paddingLeft = left;
                body.style.paddingRight = left;
                card.Add(body);

                // Header: name, Sigil, kind and grade beside the icon; the lines start below whichever is lower.
                var header = Flow(body);
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.FlexStart;
                var iconSpace = Flow(header);
                Ui.Size(iconSpace, 92, 76);
                iconSpace.style.marginBottom = 18;
                var head = Flow(header);
                head.style.width = CardW - 120;
                head.style.marginBottom = 10;
                FlowText(head, ItemUi.ItemName(entry.Def, gear), 22, info.NameColor, true, CardW - 120).style.marginBottom = 2;
                if (info.Sigil != null) FlowText(head, ItemUi.SigilLabel(info.Sigil), 17, ItemUi.SigilColors[info.Sigil].TextColor, true).style.marginBottom = 2;
                var kind = entry.Def.IsGear ? Items.KindLabel(entry.Def) : NonGearKind.TryGetValue(entry.Def.Type, out var k) ? k : entry.Def.Type;
                // Gear shows its tier, which also sets how many modifier lines it can carry.
                var tiered = entry.Def.IsGear ? $"Tier {entry.Def.Tier} {kind}" : kind;
                var wornText = entry.IsGear && entry.Slot != null ? (entry.Slot == "spare" ? " · Spare" : " · Equipped") : "";
                var sub = entry.IsGear ? $"{tiered} · {ItemUi.GradeLabel(info)}{wornText}" : entry.Kind == EntryKind.Stack ? $"{kind} · x{entry.Qty}" : tiered;
                FlowText(head, sub, 15, Palette.TextDim);

                Label Line(string text, Color color, float size = 16)
                {
                    var t = FlowText(body, text, size, color, false, CardW - 40);
                    t.style.marginBottom = 4;
                    return t;
                }

                var traits = entry.IsGear ? (GearTraits)entry.Gear : new GearTraits();
                foreach (var l in ItemUi.ItemLines(entry.Def, traits)) Line(l, Palette.Text);
                // A skill book says whether you can read it now.
                if (Books.BookSkill(entry.Def) is SkillDef bookSkill)
                {
                    var check = Books.CanRead(player, bookSkill);
                    if (check == "ok") Line("You can read it now.", Palette.TextGood);
                    else Line(check switch
                    {
                        "level" => $"Needs level {Books.ReadLevelOf(bookSkill)} (you are {player.Level}).",
                        "known" => "You know this skill already: sell it.",
                        _ => "Another class's book: sell it.",
                    }, Palette.TextBad);
                }

                if (entry.Def.IsGear)
                {
                    Row(body, 6);
                    foreach (var r in ItemUi.RollLines(entry.Def, traits.Rolls, traits.Sigil))
                    {
                        var row = Row(body, 24);
                        Ui.Text(row, 0, 0, r.Label, 15, Palette.TextDim);
                        Ui.Text(row, 118, 0, r.Effect, 15, null, true);
                        Ui.Rect(row, 178, 5, 100, 8, BarBg);
                        if (r.Quality > 0) Ui.Rect(row, 178, 5, (float)Math.Min(100, r.Quality), 8, ItemUi.QualityColor(r.Quality));
                        Ui.Text(row, CardW - 40, 0, $"{N(r.Quality)}%", 15, Palette.TextDim, ax: 1);
                    }
                    // Roll stones: re-roll a line right here (in the bag only).
                    if (mode == "bag" && entry.IsGear)
                    {
                        var piece = entry.Gear;
                        var need = Stones.StonesNeeded(piece);
                        foreach (var key in Stones.RerollableLines(piece))
                        {
                            var stone = Stones.StoneFor(key);
                            var have = player.CountItem(stone.Id);
                            var row = Row(body, 36);
                            var label = ItemUi.RollLabel(key);
                            new UiButton(row, 159, 15, $"Re-roll {label}: {need} {stone.Name}{(need > 1 ? "s" : "")} (have {have})", () =>
                            {
                                var res = Stones.Reroll(player, piece, key, rng);
                                if (res == null) return;
                                var (before, after) = res.Value;
                                Sfx.Play(after > before ? "levelup" : "click");
                                Ui.Toast($"{label}: {N(before)}% -> {N(after)}%", after >= before ? Palette.TextGood : Palette.TextBad);
                                Changed();
                            }, 318, 30, 13).SetEnabled(have >= need);
                        }
                    }
                    // Mods: the blue lines.
                    foreach (var m in ItemUi.ModLines(traits))
                    {
                        var row = Row(body, 24);
                        Ui.Text(row, 0, 0, m.Text, 15, ItemUi.ModColor, true);
                        Ui.Rect(row, 178, 5, 100, 8, BarBg);
                        if (m.Quality > 0) Ui.Rect(row, 178, 5, (float)Math.Min(100, m.Quality), 8, ItemUi.ModBarColor);
                        Ui.Text(row, CardW - 40, 0, $"{N(m.Quality)}%", 15, Palette.TextDim, ax: 1);
                    }
                }

                Row(body, 6);
                Line(entry.Def.Description ?? "", Palette.TextDim, 15);
                if (entry.IsGear && entry.Slot == "spare")
                    Line("Spare weapon: its lines count once it is in your hand. Swap in battle (costs your turn) or here.", Palette.TextMagic, 15);

                // The item's set: pieces (worn ones ticked) and bonuses (active ones lit).
                var set = entry.Def.IsGear ? ItemUi.SetCard(entry.Def.Id, player.Data.Equipment) : null;
                if (set != null)
                {
                    Row(body, 4);
                    Line(set.Title, Palette.TextGold, 15);
                    foreach (var l in set.Lines) Line(l.Text, l.On ? Palette.TextGood : Palette.TextDim, 14);
                }

                // Compare with what is worn in the slot it would go to, for gear that isn't worn.
                if (entry.Kind != EntryKind.Stack && entry.Def.IsGear && !(entry.IsGear && entry.Slot != null))
                {
                    var slot = player.SlotFor(entry.Def);
                    var worn = Worn(slot);
                    var wornDef = worn != null ? Items.GearDef(worn) : null;
                    double StatBonus(string t) => Math.Floor(player.StatForType(t) * Db.B.StatDamageScale);
                    var piece = entry.IsGear ? entry.Gear : new Gear { Uid = "", ItemId = entry.Def.Id, Rolls = new Dictionary<string, double>() };
                    var diff = ItemUi.CompareGear(entry.Def, traits, wornDef, worn, StatBonus);
                    diff.AddRange(ItemUi.SetCompareLines(player.Data.Equipment, slot, piece));
                    Row(body, 6);
                    Line(worn != null ? $"Compared to your {wornDef.Name}:" : "Nothing equipped in that slot.", Palette.TextGold, 15);
                    var shown = diff.Take(6).ToList();
                    if (worn != null && shown.Count == 0) Line("Same as equipped.", Palette.TextDim, 15);
                    var labels = shown.Select(d => Line(d.Text, ItemUi.ToneColor(d.Tone), 15)).ToList();
                    // As many lines as fit above the price (about 20 px each), at least one.
                    if (labels.Count > 1)
                    {
                        labels[0].RegisterCallback<GeometryChangedEvent>(_ =>
                        {
                            var y0 = labels[0].layout.y;
                            if (float.IsNaN(y0)) return;
                            var room = Math.Max(1, (int)Math.Floor((CardH - 100 - y0) / 20));
                            for (int i = room; i < labels.Count; i++)
                                if (labels[i].resolvedStyle.display != DisplayStyle.None) labels[i].style.display = DisplayStyle.None;
                        });
                    }
                }

                // Value and buttons.
                var valueText = entry.Kind == EntryKind.Stock
                    ? $"Price {N(entry.Def.Price)} g"
                    : $"Sells for {N(entry.IsGear ? Items.SellValue(entry.Def, entry.Gear) : Items.SellValue(entry.Def))} g";
                Ui.Text(card, left, CardH - 92, valueText, 16, Palette.TextGold);

                var buttons = new List<CardButton>();
                void Add(string label, Action run, bool enabled = true) => buttons.Add(new CardButton { Label = label, Run = run, Enabled = enabled });
                if (mode == "bag")
                {
                    if (entry.IsGear)
                    {
                        if (entry.Slot != null) Add("Unequip", () => Unequip(entry.Slot));
                        else Add("Equip", () => Equip(entry));
                        // Weapons: carry one as the spare, or swap the hand and the spare.
                        var spareOpen = HeroSlots.Contains("spare");
                        if (spareOpen && entry.Def.IsWeapon && entry.Slot == null) Add("As spare", () => Equip(entry, "spare"));
                        if (spareOpen && (entry.Slot == "spare" || (entry.Slot == "weapon" && Worn("spare") != null))) Add("Swap", SwapWeapons);
                    }
                    else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "consumable")
                    {
                        var def = entry.Def;
                        Add("Use", () => Use(def));
                    }
                    else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "scroll" && onRecall != null)
                    {
                        Add("Read", () => onRecall());
                    }
                    else if (entry.Kind == EntryKind.Stack && entry.Def.Type == "book")
                    {
                        var def = entry.Def;
                        Add("Read", () => Read(def));
                    }
                }
                else if (mode == "buy" && entry.Kind == EntryKind.Stock)
                {
                    Add($"Buy {N(entry.Def.Price)} g", () => Buy(entry.Def), player.Gold >= entry.Def.Price);
                }
                else if (mode == "sell" && entry.Kind != EntryKind.Stock)
                {
                    var locked = entry.IsGear && entry.Gear.Locked == true;
                    Add(locked ? "Locked" : "Sell", () => Sell(entry), !locked);
                }
                if (entry.IsGear)
                {
                    Add(entry.Gear.Locked == true ? "Unlock" : "Lock", () =>
                    {
                        player.ToggleLock(entry.Gear.Uid);
                        Sfx.Play("click");
                        Changed();
                    });
                }
                var bw = buttons.Count > 2 ? 104f : 150f;
                var gap = buttons.Count > 2 ? 8f : 12f;
                for (int i = 0; i < buttons.Count; i++)
                {
                    var b = buttons[i];
                    var x = CardW / 2 + (i - (buttons.Count - 1) / 2f) * (bw + gap);
                    new UiButton(card, x, CardH - 40, b.Label, b.Run, bw, 48, 18).SetEnabled(b.Enabled);
                }
            }
        }
    }
}
