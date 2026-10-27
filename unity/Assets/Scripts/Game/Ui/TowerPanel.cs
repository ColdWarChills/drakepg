using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The Spire's Warden (docs/TOWER_SPEC.md): a tab per flight with its ten floors (monster, level, twists, first-clear
    /// pay; pick one to read its twists, Enter to fight it), and a Trade tab with the vendor (marks plus gold).
    /// </summary>
    public static class TowerPanel
    {
        const float W = 1180, H = 650;
        const float CardW = 545, CardH = 72, CardStep = 78, ListY = 142;
        static readonly Color ActiveButton = Design.Hex(0x6b5320);

        static string rememberedTab;

        /// <summary>Start a floor: full HP and MP for the fight, the HP you came with afterwards.</summary>
        public static void StartFloor(Player player, int floor)
        {
            var setup = new BattleSetup
            {
                ZoneId = Tower.FlightOf(floor).Id, MonsterId = Tower.FloorDef(floor).MonsterId, RoomId = "spire_foot", X = 640, Y = 650,
                TowerFloor = floor, EntryHp = player.Hp, EntryMp = player.Mp,
            };
            player.RestoreFull();
            GameRoot.I.Go(new BattleScreen(setup), 0.2f);
        }

        /// <summary>A twist's icon, or the star while it has none.</summary>
        public static string IconOf(TwistDef tw) => tw.Icon != null && Felt.HasIcon(tw.Icon) ? tw.Icon : "star";

        /// <summary>"Dawn" for dawn.</summary>
        static string SigilName(string tier) => char.ToUpperInvariant(tier[0]) + tier.Substring(1);

        /// <summary>"10 marks", "30 marks" (5th floors), "30 marks + a Dawn Sigil piece".</summary>
        public static string RewardText(int floor)
        {
            var marks = $"{Tower.MarksFor(floor)} marks";
            if (floor % 10 == 0) return $"{marks} + a {SigilName(Tower.FlightOf(floor).Sigil)} Sigil piece";
            return marks;
        }

        static string LockText(FlightDef flight)
        {
            var quest = Db.Quest(flight.OpensAfter);
            var boss = quest.Objective.MonsterId != null ? Db.Monster(quest.Objective.MonsterId).Name : quest.Name;
            return $"Opens after {boss} is beaten.";
        }

        public static UiModal Open(Player player, NpcDef warden, Action onChange, bool trade = false)
        {
            var modal = new UiModal(W, H, "The Spire", () => { });
            var rng = new SystemRng();
            int? selected = null;
            string note = null;
            Color noteColor = Palette.TextDim;
            int page = 0;
            var flights = Db.Data.Flights;
            var next = Tower.NextFloor(player);
            if (trade) rememberedTab = "trade";
            else if (rememberedTab == null || rememberedTab == "trade")
                rememberedTab = Tower.FlightOf(Math.Min(Tower.LastFloor, player.Data.Tower.Best + 1)).Id;

            void Render()
            {
                var c = modal.Content;
                c.Clear();
                var t = player.Data.Tower;

                // Header: marks, best floor, the Warden's line.
                Felt.Icon(c, 38, 40, 34, "mark", true);
                Ui.Text(c, 78, 44, $"{t.Marks} marks", 24, Palette.TextGold, true);
                Ui.Text(c, 78, 74, t.Best == 0 ? "No floor cleared yet." : $"Best floor: {t.Best} / {Tower.LastFloor}", 15, Palette.TextDim);
                var line = warden.Dialogue != null && warden.Dialogue.Count > 0 ? warden.Dialogue[0] : "";
                Ui.Text(c, 1140, 54, $"{warden.Name}: \"{line}\"", 15, Palette.TextDim, false, 1, 0, 520, align: TextAnchor.UpperRight);

                // Tabs: a flight each, then Trade.
                var tabs = flights.Select(f => (id: f.Id, label: f.Name)).Concat(new[] { (id: "trade", label: "Trade") }).ToList();
                for (int i = 0; i < tabs.Count; i++)
                {
                    var tab = tabs[i];
                    var active = rememberedTab == tab.id;
                    new UiButton(c, 130 + i * 230, 118, tab.label, () =>
                    {
                        rememberedTab = tab.id;
                        selected = null;
                        note = null;
                        page = 0;
                        Render();
                    }, 220, 40, 16, null, active ? ActiveButton : Palette.Button);
                }

                if (rememberedTab == "trade") RenderTrade(c);
                else RenderFlight(c, flights.First(f => f.Id == rememberedTab));
            }

            void RenderFlight(VisualElement c, FlightDef flight)
            {
                var open = Tower.FlightOpen(player, flight);
                var t = player.Data.Tower;
                if (selected == null && next is int n && n >= flight.First && n <= flight.Last) selected = n;
                for (int f = flight.First; f <= flight.Last; f++)
                {
                    var i = f - flight.First;
                    var x = 30 + (i / 5) * (CardW + 30);
                    var y = ListY + 22 + (i % 5) * CardStep;
                    var floor = f;
                    Card(c, x, y, floor, player, open, floor == selected, () =>
                    {
                        selected = floor;
                        note = null;
                        Render();
                    });
                }

                // Bottom: the picked floor's twists and the Enter button.
                var help = "Every floor starts at full HP; afterwards you're back at the HP you came in with. Losing costs nothing: try again.";
                if (!open)
                {
                    Ui.Text(c, 40, 600, LockText(flight), 18, Palette.TextBad, true, ay: 0.5f);
                    return;
                }
                if (selected is int s && s >= flight.First && s <= flight.Last)
                {
                    var def = Tower.FloorMonster(s);
                    var twists = def.Tower.Twists.Select(Db.Twist).Select(tw => $"{tw.Name}: {tw.Text}");
                    var state = s <= t.Best ? "Cleared: fight it again for practice (no reward)." : s == t.Best + 1 ? $"First clear: {RewardText(s)}." : $"Clear floor {s - 1} first.";
                    Ui.Text(c, 40, 566, $"Floor {s}: {def.Name} (Lv {def.Level}{(def.Tough ? ", Tough" : "")}).  {string.Join("  ", twists)}", 16, Palette.Text, wrap: 900);
                    Ui.Text(c, 40, 614, note ?? state, 15, note != null ? noteColor : s <= t.Best ? Palette.TextDim : Palette.TextGood, wrap: 900);
                    var can = Tower.FloorOpen(player, s);
                    new UiButton(c, 1070, 606, s <= t.Best ? "Again" : "Enter", () =>
                    {
                        if (!Tower.FloorOpen(player, s)) return;
                        modal.Close();
                        StartFloor(player, s);
                    }, 150, 56, 22, null, can ? Design.Hex(0x2f5d2f) : Palette.Button).SetEnabled(can);
                }
                else Ui.Text(c, 40, 600, help, 16, Palette.TextDim, wrap: 1080, ay: 0.5f);
            }

            void RenderTrade(VisualElement c)
            {
                var rows = Tower.VendorStock(player);
                const int perPage = 12;
                var pages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)perPage));
                page = Math.Max(0, Math.Min(page, pages - 1));
                var shown = rows.Skip(page * perPage).Take(perPage).ToList();
                for (int i = 0; i < shown.Count; i++)
                {
                    var row = shown[i];
                    var x = 30 + (i / 6) * (CardW + 30);
                    var y = ListY + 22 + (i % 6) * 66;
                    TradeRow(c, x, y, row, player, () =>
                    {
                        var result = Tower.BuyFromVendor(player, row.Key, rng);
                        note = result switch
                        {
                            "ok" => $"Bought: {RowName(row)}. It's in your bag.",
                            "marks" => "Not enough marks: clear more floors.",
                            "gold" => "Not enough gold.",
                            "full" => "Your bag is full.",
                            _ => "That's gone.",
                        };
                        noteColor = result == "ok" ? Palette.TextGood : Palette.TextBad;
                        if (result == "ok")
                        {
                            Sfx.Play("coin");
                            onChange?.Invoke();
                        }
                        Render();
                    });
                }
                if (rows.Count == 0) Ui.Text(c, 40, ListY + 40, "Nothing for you yet. Clear floor 5 and come back.", 18, Palette.TextDim);
                if (pages > 1)
                {
                    var y = ListY + 22 + 6 * 66 + 4;
                    Ui.Text(c, W / 2 - 20, y, $"{page + 1} / {pages}", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
                    new UiButton(c, W / 2 - 84, y, "<", () =>
                    {
                        page -= 1;
                        Render();
                    }, 44, 38).SetEnabled(page > 0);
                    new UiButton(c, W / 2 + 44, y, ">", () =>
                    {
                        page += 1;
                        Render();
                    }, 44, 38).SetEnabled(page < pages - 1);
                }
                // What opens next.
                var best = player.Data.Tower.Best;
                var coming = Db.Data.Vendor.Where(v => v.MinFloor > best).OrderBy(v => v.MinFloor).FirstOrDefault();
                var hint = coming != null ? $"Clear floor {coming.MinFloor} for {VendorName(coming)}." : "Everything is on sale.";
                Ui.Text(c, 40, 610, note ?? $"Set pieces are rolled when you buy them, like a boss drop.  {hint}", 16, note != null ? noteColor : Palette.TextDim,
                    note != null, wrap: 1080, ay: 0.5f);
            }

            Render();
            return modal;
        }

        static string VendorName(VendorDef v) => v.Kind switch
        {
            "set" => $"{Db.Set(v.Set).Name} set pieces",
            "cache" => $"a {AreaName(v.LootTable)} Sigil cache",
            _ => "the Light and Dark books",
        };

        /// <summary>"Thornwood" for the thornwood loot table.</summary>
        static string AreaName(string lootTable) => char.ToUpperInvariant(lootTable[0]) + lootTable.Substring(1);

        static string RowName(VendorRow row) => row.ItemId != null ? Db.Item(row.ItemId).Name : $"{AreaName(row.Entry.LootTable)} Sigil cache";

        /// <summary>A floor: number, the monster's picture, name and level, twist names, first-clear pay, and its state.</summary>
        static void Card(VisualElement c, float x, float y, int floor, Player player, bool open, bool picked, Action pick)
        {
            var t = player.Data.Tower;
            var cleared = floor <= t.Best;
            var isNext = floor == t.Best + 1 && open;
            var def = Tower.FloorMonster(floor);
            var border = picked ? Felt.Gold : isNext ? Design.Hex(0x7ddc7d) : PanelKit.WithAlpha(Palette.PanelBorder, 0.45f);
            var card = Ui.Rect(c, x, y, CardW, CardH, Design.Hex(cleared ? 0x1f2a1d : 0x241e2e), 10, picked || isNext ? 2.5f : 2, border);
            card.pickingMode = PickingMode.Position;
            card.style.opacity = open && floor <= t.Best + 1 ? 1 : 0.5f;
            var boss = floor % 5 == 0;
            PanelKit.Circle(card, 34, CardH / 2, 22, boss ? Design.Hex(0x6b2a2a) : Design.Hex(0x3a3150), 2, boss ? Felt.Gold : PanelKit.WithAlpha(Palette.PanelBorder, 0.7f));
            Ui.Text(card, 34, CardH / 2 + 1, floor.ToString(), 20, Felt.Cream, true, 0.5f, 0.5f);
            var art = def.Look?.Art?.FirstOrDefault(Art.Has);
            if (art != null) Ui.Image(card, 66, 5, 62, 62, art);
            var name = def.IsHeroic ? def.Name : def.Name;
            Ui.Text(card, 140, 7, name, 18, boss ? Palette.TextGold : Palette.Text, true);
            Ui.Text(card, CardW - 14, 12, $"Lv {def.Level}{(def.Tough ? " · Tough" : "")}", 15, Palette.TextDim, ax: 1);
            // Twists: each its icon and name.
            var tx = 140f;
            foreach (var id in def.Tower.Twists)
            {
                var tw = Db.Twist(id);
                var twIcon = Felt.Icon(card, tx, 31, 18, IconOf(tw), true);
                var twName = Ui.Text(card, tx + 22, 30, tw.Name, 15, Felt.Gold);
                Tooltip.Attach(twIcon, tw.Name, tw.Text);
                Tooltip.Attach(twName, tw.Name, tw.Text);
                tx += 22 + tw.Name.Length * 7.6f + 14;
            }
            var state = cleared ? "✔ Cleared" : isNext ? $"Next: {RewardText(floor)}" : open ? RewardText(floor) : "Locked";
            Ui.Text(card, 140, 50, state, 14, cleared ? Palette.TextGood : isNext ? Palette.TextGold : Palette.TextDim);
            card.RegisterCallback<PointerUpEvent>(e =>
            {
                if (GameRoot.I.Screen?.InputEnabled == false) return;
                e.StopPropagation();
                Sfx.Play("click");
                pick();
            });
        }

        /// <summary>A vendor row: the item's picture, name, what it is, the price and Buy.</summary>
        static void TradeRow(VisualElement c, float x, float y, VendorRow row, Player player, Action buy)
        {
            Ui.Rect(c, x, y, CardW, 62, Design.Hex(0x241e2e), 10, 2, PanelKit.WithAlpha(Palette.PanelBorder, 0.45f));
            var def = row.ItemId != null ? Db.Item(row.ItemId) : null;
            if (def != null) ItemUi.Icon(c, x + 36, y + 31, 48, def);
            else Felt.Icon(c, x + 14, y + 9, 44, "cache", true);
            var kind = row.Entry.Kind == "set" ? $"{Db.Set(row.Entry.Set).Name} set, rolled like a boss drop"
                : row.Entry.Kind == "cache" ? $"A random weapon of the area with a {SigilName(row.Entry.Sigil)} Sigil"
                : $"Skill book, read at level {Books.ReadLevelOf(Books.BookSkill(def))}";
            Ui.Text(c, x + 70, y + 8, RowName(row), 17, Palette.Text, true);
            Ui.Text(c, x + 70, y + 34, kind, 13, Palette.TextDim, wrap: 300);
            var afford = player.Data.Tower.Marks >= row.Marks && player.Gold >= row.Gold;
            Ui.Text(c, x + CardW - 128, y + 18, $"{row.Marks} marks", 15, player.Data.Tower.Marks >= row.Marks ? Palette.TextGold : Palette.TextBad, ax: 1, ay: 0.5f);
            Ui.Text(c, x + CardW - 128, y + 42, $"{PanelKit.Num(row.Gold)} g", 15, player.Gold >= row.Gold ? Palette.TextGold : Palette.TextBad, ax: 1, ay: 0.5f);
            new UiButton(c, x + CardW - 58, y + 31, "Buy", buy, 96, 44, 18).SetEnabled(afford);
        }
    }
}
