using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>What the cheat menu needs from the room (CheatHooks in src/ui/cheatMenu.ts).</summary>
    public class CheatHooks
    {
        /// <summary>HUD, quest tracker, hero figure and save.</summary>
        public Action OnChange;
        /// <summary>Go to a room (its middle when no spot is given).</summary>
        public Action<string, float?, float?> Travel;
        /// <summary>Throw the character away and start over (same name and class).</summary>
        public Action ResetCharacter;
    }

    /// <summary>
    /// Testing menu, a port of src/ui/cheatMenu.ts: levels, gold, any item made to order, a wardrobe to try every
    /// weapon and armor on, teleports, quest skips and battle toggles. Changes are saved like normal play.
    /// </summary>
    public static class CheatMenu
    {
        const float W = 1200;
        const float H = 660;
        static readonly Color On = Design.Hex(0x5a6b2e);
        static readonly Color Picked = Design.Hex(0x6b5a2e);
        static readonly Color Danger = Design.Hex(0x6e1f1f);

        // Remembered while the game runs.
        static string tab = "hero";
        static string category = "weapon";
        static int page;
        /// <summary>Item options. Quality null = random like a drop.</summary>
        static double? quality;
        static string sigil;
        static int mods;

        public static UiModal OpenCheatMenu(Player player, CheatHooks hooks)
        {
            var modal = new UiModal(W, H, "Cheats", () => { });
            var c = modal.Content;
            var rng = new SystemRng();

            void Changed(string message = null)
            {
                hooks.OnChange?.Invoke();
                if (message != null) Ui.Toast(message, Palette.TextGood);
                Render();
            }

            UiButton Btn(float x, float y, string label, Action run, float w = 170, Color? color = null, bool enabled = true)
            {
                var b = new UiButton(c, x + w / 2, y, label, run, w, 40, label.Length > 18 ? 15 : 17, color: color);
                if (!enabled) b.SetEnabled(false);
                return b;
            }

            void Caption(float x, float y, string text, Color? color = null, float size = 18) =>
                Ui.Text(c, x, y, text, size, color ?? Palette.TextDim, true, 0, 0.5f);

            // One row of mutually exclusive choices.
            void Choices<T>(float x, float y, string title, IList<(string label, T value)> options, T current, Action<T> pick, float w = 104)
            {
                Caption(x, y, title);
                for (int i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    Btn(x + 90 + i * (w + 8), y, o.label, () =>
                    {
                        pick(o.value);
                        Render();
                    }, w, EqualityComparer<T>.Default.Equals(o.value, current) ? Picked : (Color?)null);
                }
            }

            void ItemOptions(float y)
            {
                Choices(40, y, "Quality", new List<(string, double?)>
                {
                    ("Random", null), ("0%", 0), ("50%", 50), ("90%", 90), ("100%", 100),
                }, quality, v => quality = v);
                var sigils = new List<(string, string)> { ("None", null) };
                foreach (var t in Items.SigilTiers) sigils.Add((Items.SigilNames[t].Replace("Sigil of ", ""), t));
                Choices(40, y + 50, "Sigil", sigils, sigil, v => sigil = v);
                Choices(620, y + 50, "Mods", new List<(string, int)> { ("0", 0), ("1", 1), ("2", 2), ("3", 3) }, mods, v => mods = v, 60);
            }

            ItemGrant Grant(string itemId) => DevTools.CheatGrant(itemId, quality, sigil, mods, rng);

            void Render()
            {
                c.Clear();
                var tabs = new[] { ("hero", "Hero"), ("items", "Give item"), ("wardrobe", "Wardrobe"), ("world", "World") };
                for (int i = 0; i < tabs.Length; i++)
                {
                    var (id, text) = tabs[i];
                    Btn(40 + i * 180, 78, text, () =>
                    {
                        tab = id;
                        page = 0;
                        Render();
                    }, 170, id == tab ? Picked : (Color?)null);
                }
                if (tab == "hero") RenderHero();
                else if (tab == "items") RenderItems();
                else if (tab == "wardrobe") RenderWardrobe();
                else RenderWorld();
            }

            void RenderHero()
            {
                Caption(40, 140, $"Level {player.Level}   ·   {PanelKit.Num(player.Gold)} gold   ·   HP {PanelKit.Num(player.Hp)}/{PanelKit.Num(player.MaxHp)}   ·   " +
                               $"{player.Data.StatPoints} stat points", Palette.Text, 20);
                var max = Db.B.MaxLevel;
                Caption(40, 200, "Level");
                Btn(150, 200, "+1", () =>
                {
                    DevTools.AddLevels(player, 1);
                    Changed($"Level {player.Level}");
                }, 90, null, player.Level < max);
                Btn(250, 200, "+5", () =>
                {
                    DevTools.AddLevels(player, 5);
                    Changed($"Level {player.Level}");
                }, 90, null, player.Level < max);
                var targets = new[] { 5, 10, 15, 20 };
                for (int i = 0; i < targets.Length; i++)
                {
                    var lv = targets[i];
                    Btn(350 + i * 100, 200, $"To {lv}", () =>
                    {
                        DevTools.AddLevels(player, lv - player.Level);
                        Changed($"Level {player.Level}");
                    }, 90, null, player.Level < lv);
                }
                Caption(40, 260, "Gold");
                var golds = new[] { 100, 1000, 10000 };
                for (int i = 0; i < golds.Length; i++)
                {
                    var gold = golds[i];
                    Btn(150 + i * 130, 260, "+" + gold.ToString("N0", CultureInfo.InvariantCulture), () =>
                    {
                        player.Data.Gold += gold;
                        Changed($"+{gold} gold");
                    }, 120);
                }
                Btn(40, 330, "Full heal", () =>
                {
                    player.RestoreFull();
                    Changed("Fully healed");
                });
                Btn(220, 330, "Reset stat points", () =>
                {
                    DevTools.ResetStats(player);
                    Changed("Stat points refunded");
                }, 200);
                Btn(430, 330, "Clear bag (keeps locked)", () =>
                {
                    var before = player.Data.Gear.Count;
                    player.Data.Gear = player.Data.Gear.Where(g => g.Locked == true).ToList();
                    Changed($"Removed {before - player.Data.Gear.Count} pieces");
                }, 240);
                // Testing rates: more XP, gold and drops to see the drop screen and level up fast. Until reload, like the toggles.
                Choices(700, 330, "Rates", new List<(string, double)> { ("x1", 1), ("x2", 2), ("x5", 5), ("x10", 10) }, Cheats.Rates, v =>
                {
                    Cheats.Rates = v;
                    Ui.Toast($"Rates x{PanelKit.Num(v)}: XP, gold and drop chances", Palette.TextGood);
                }, 80);
                Ui.Text(c, 700, 362, "XP, gold and every drop chance (gear, Sigils, potions, chests; capped at 100%). Until reload.", 14, Palette.TextDim, wrap: 440);

                Caption(40, 410, "Toggles (until reload, not saved)");
                void Toggle(int i, string name, Func<bool> get, Action<bool> set, string note)
                {
                    var x = 40 + i * 212;
                    var on = get();
                    Btn(x, 460, $"{name}: {(on ? "ON" : "off")}", () =>
                    {
                        set(!get());
                        Render();
                    }, 202, on ? On : (Color?)null);
                    Ui.Text(c, x, 492, note, 14, Palette.TextDim, wrap: 202);
                }
                Toggle(0, "God mode", () => Cheats.God, v => Cheats.God = v, "You take no damage.");
                Toggle(1, "One-hit kills", () => Cheats.OneHit, v => Cheats.OneHit = v, "Any hit that lands finishes the enemy.");
                Toggle(2, "Elites everywhere", () => Cheats.Elites, v => Cheats.Elites = v,
                    "Every roaming monster spawns as an Elite (re-enter a room or Respawn).");
                Toggle(3, "Lucky everywhere", () => Cheats.Lucky, v => Cheats.Lucky = v,
                    "A Lucky Impling in every monster room (re-enter a room or Respawn).");
                Toggle(4, "All unlocks", () => Cheats.AllFeatures, v => Cheats.AllFeatures = v,
                    "Forge, Sigils, chests and the rest without finishing chapters.");

                Btn(40, 600, "Reset character...", ConfirmReset, 220, Danger);
            }

            void ConfirmReset()
            {
                var m = new UiModal(640, 260, "Start over?");
                Ui.Text(m.Content, 320, 100, $"{player.Name} goes back to level 1 with starting gear.\nThis replaces your save.", 20, Palette.Text, false,
                    0.5f, 0.5f, align: TextAnchor.MiddleCenter);
                new UiButton(m.Content, 200, 200, "Reset", () =>
                {
                    m.Close();
                    modal.Close();
                    hooks.ResetCharacter?.Invoke();
                }, 220, color: Danger);
                new UiButton(m.Content, 440, 200, "Cancel", () => m.Close(), 220);
            }

            void RenderItems()
            {
                ItemOptions(140);
                Choices(40, 240, "Type", new List<(string, string)>
                {
                    ("Weapons", "weapon"), ("Armor", "armor"), ("Rings etc", "accessory"), ("Potions", "consumable"),
                }, category, v =>
                {
                    category = v;
                    page = 0;
                }, 130);
                var items = PanelKit.ItemsOfType(category);
                const int cols = 4;
                const int rows = 6;
                const int per = cols * rows;
                var pages = Math.Max(1, (items.Count + per - 1) / per);
                page = Math.Min(page, pages - 1);
                var shown = items.Skip(page * per).Take(per).ToList();
                for (int i = 0; i < shown.Count; i++)
                {
                    var item = shown[i];
                    Btn(40 + (i % cols) * 282, 300 + (i / cols) * 48, item.Name, () => Give(item), 272);
                }
                if (pages > 1)
                {
                    Btn(40, 600, "Prev", () =>
                    {
                        page = (page + pages - 1) % pages;
                        Render();
                    }, 120);
                    Caption(180, 600, $"{page + 1} / {pages}");
                    Btn(260, 600, "Next", () =>
                    {
                        page = (page + 1) % pages;
                        Render();
                    }, 120);
                }
                Caption(440, 600, "Gear goes to your bag (even past 30 slots). Potions come in fives.", Palette.TextDim, 15);
            }

            void Give(ItemDef item)
            {
                if (!item.IsGear)
                {
                    player.AddItem(item.Id, 5);
                    Changed($"+5 {item.Name}");
                    return;
                }
                var grant = Grant(item.Id);
                player.Receive(grant, true);
                Changed($"Added {PanelKit.LootName(item, grant.Traits())}");
            }

            // Equip a piece: one from the bag if you have it, otherwise a new one made with the item options.
            void Wear(ItemDef item)
            {
                var owned = player.Data.Gear.FirstOrDefault(g => g.ItemId == item.Id);
                var gear = owned ?? player.MakeGear(item.Id, Grant(item.Id).Traits());
                if (owned == null) player.Data.Gear.Add(gear);
                player.Equip(gear.Uid);
                Changed($"Wearing {item.Name}");
            }

            void RenderWardrobe()
            {
                // Big hero preview on the left.
                Ui.Rect(c, 30, 120, 300, 510, Design.Hex(0x15111c, 0.8f), 0, 2, Palette.PanelBorder);
                HeroPreview(c, 180, 560, player.Look, player.Weapon, 1.25f);
                var weapon = player.Data.Equipment.TryGetValue("weapon", out var wg) ? wg : null;
                var armor = player.Data.Equipment.TryGetValue("body", out var bg) ? bg : null;
                var worn = new[]
                {
                    weapon != null ? PanelKit.LootName(Db.Item(weapon.ItemId), weapon) : "No weapon",
                    armor != null ? PanelKit.LootName(Db.Item(armor.ItemId), armor) : "No body armor",
                };
                Ui.Text(c, 180, 600, string.Join("\n", worn), 15, Palette.Text, false, 0.5f, 0.5f, 280, align: TextAnchor.MiddleCenter);

                var classId = player.Data.ClassId;
                // A class's starting armor is its default outfit.
                bool HasLook(ItemDef item) =>
                    item.Type == "weapon" ? Art.Has(item.Art) : item.Id == player.ClassDef.Armor || Art.Has($"char_{classId}_{item.Id}");
                const int cols = 4;
                void Grid(List<ItemDef> list, float top)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        var x = 350 + (i % cols) * 206;
                        var y = top + (i / cols) * 44;
                        var wearing = weapon?.ItemId == item.Id || armor?.ItemId == item.Id;
                        var b = Btn(x, y, item.Name, () => Wear(item), 198, wearing ? Picked : (Color?)null);
                        if (!HasLook(item)) b.style.opacity = 0.6f;
                    }
                }
                var weapons = PanelKit.ItemsOfType("weapon");
                Caption(350, 128, "Weapons");
                Grid(weapons, 160);
                var armorTop = 160 + (weapons.Count + cols - 1) / cols * 44 + 10;
                Caption(350, armorTop - 4, "Body armor (the outfit)");
                Grid(PanelKit.ItemsOfType("armor").Where(i => (i.Slot ?? "body") == "body").ToList(), armorTop + 28);
                Caption(350, 640, "Faded = no art yet (falls back to the default look). New pieces use the Give item options.", Palette.TextDim, 14);
            }

            void RenderWorld()
            {
                Caption(40, 140, "Teleport");
                var rooms = Db.Data.Rooms;
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    Btn(40 + (i % 4) * 190, 180 + (i / 4) * 46, r.Name, () =>
                    {
                        modal.Close();
                        hooks.Travel?.Invoke(r.Id, null, null);
                    }, 180);
                }
                var loc = player.Data.Location;
                Btn(800, 180, "Respawn monsters", () =>
                {
                    modal.Close();
                    Session.ForgetRooms();
                    hooks.Travel?.Invoke(loc.RoomId, (float)loc.X, (float)loc.Y);
                }, 220);

                var top = 180 + (rooms.Count + 3) / 4 * 46 + 30;
                Caption(40, top, "Quests (click one to mark it and everything before it done)");
                var quests = Db.Quests();
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    var status = Quests.Status(player, q);
                    Btn(40 + (i % 3) * 300, top + 40 + (i / 3) * 46, $"{q.Name} ({status})", () =>
                    {
                        DevTools.CompleteQuestChain(player, q);
                        Changed($"{q.Name}: done");
                    }, 290, status == "done" ? On : (Color?)null);
                }
                var qTop = top + 40 + (quests.Count + 2) / 3 * 46 + 10;
                Btn(40, qTop, "Finish quests + hunts", () => Changed($"{DevTools.FinishActiveQuests(player)} quest(s)/hunt(s) ready to turn in"), 220);
                Btn(280, qTop, "Reset all quests", () =>
                {
                    player.Data.Quests = new Dictionary<string, QuestState>();
                    Changed("Quests reset");
                }, 200, Danger);
                var guild = player.Data.Guild;
                Btn(500, qTop, $"Guild rank {Guild.RankId(guild.Rank)} +1", () =>
                {
                    guild.Rank = Math.Min(Guild.MaxRank, guild.Rank + 1);
                    guild.Points = 0;
                    Changed($"Guild rank {Guild.RankId(guild.Rank)}");
                }, 200);
                Btn(720, qTop, "Reset guild", () =>
                {
                    player.Data.Guild = new GuildState();
                    Changed("Guild reset to Rank F");
                }, 160, Danger);
            }

            Render();
            return modal;
        }

        /// <summary>
        /// The hero drawn into the menu (drawFigure in src/ui/figures.ts, as UI images): feet at (x, feetY), facing right,
        /// the weapon in its fists when both have art. `extra` scales it on top of the look's own scale.
        /// </summary>
        static void HeroPreview(VisualElement parent, float x, float feetY, Look look, ItemDef weapon, float extra)
        {
            var scale = (float)look.Scale * extra;
            var bare = look.Bare?.Find(b => Art.Has(b.Art));
            var weaponArt = weapon?.Art != null && Art.Has(weapon.Art) ? weapon.Art : null;
            if (bare != null && weaponArt != null)
            {
                var size = Art.Size(Art.Get(bare.Art));
                if (size.x <= 0 || size.y <= 0) return;
                var s = Mathf.Min(Figure.ArtHeight / size.y, Figure.ArtMaxWidth / size.x) * scale;
                var w = size.x * s;
                var h = size.y * s;
                var left = x - w / 2;
                var top = feetY - h;
                Shadow(parent, x, feetY, w * 0.7f);
                // Weapons first: the body (and its fists) draw over the grip, like the figure's sorting.
                var wSize = Art.Size(Art.Get(weaponArt));
                if (wSize.y > 0)
                {
                    var wh = (float)(weapon.Size ?? 0.6) * h;
                    var ww = wSize.x * wh / wSize.y;
                    var grip = (float)(weapon.Grip ?? 0.85);
                    foreach (var hand in bare.Hands ?? new List<Hand>())
                    {
                        // A zero-size pivot at the fist, turned by the anchor's angle; the weapon hangs from its grip point.
                        var pivot = new VisualElement { pickingMode = PickingMode.Ignore };
                        Ui.Place(pivot, left + (float)hand.X * s, top + (float)hand.Y * s);
                        Ui.Size(pivot, 0, 0);
                        pivot.style.rotate = new Rotate(Angle.Degrees((float)hand.Angle));
                        Ui.Image(pivot, -ww / 2, -grip * wh, ww, wh, weaponArt);
                        parent.Add(pivot);
                    }
                }
                Ui.Image(parent, left, top, w, h, bare.Art);
                return;
            }
            var key = Art.Pick(look.Art);
            if (key != null)
            {
                var size = Art.Size(Art.Get(key));
                if (size.x <= 0 || size.y <= 0) return;
                var s = Mathf.Min(Figure.ArtHeight / size.y, Figure.ArtMaxWidth / size.x) * scale;
                Shadow(parent, x, feetY, size.x * s * 0.7f);
                Ui.Image(parent, x - size.x * s / 2, feetY - size.y * s, size.x * s, size.y * s, key);
                return;
            }
            // No art: a body and head in the look's colors (Figure's placeholder shape).
            var humanoid = look.Shape == "humanoid";
            float bw = (humanoid ? 60 : 130) * scale, bh = (humanoid ? 150 : 90) * scale;
            Shadow(parent, x, feetY, 110 * scale);
            var body = Ui.Rect(parent, x - bw / 2, feetY - bh, bw, bh, Design.Hex(look.Color));
            Ui.Round(body, bw / 2);
            var head = (humanoid ? 56 : 50) * scale;
            var hx = humanoid ? x : x + 60 * scale;
            var hy = humanoid ? feetY - bh - 20 * scale : feetY - bh * 0.8f;
            var headEl = Ui.Rect(parent, hx - head / 2, hy - head / 2, head, head, humanoid ? Design.Hex(0xf0c8a0) : Design.Hex(look.Accent));
            Ui.Round(headEl, head / 2);
        }

        static void Shadow(VisualElement parent, float x, float y, float width)
        {
            var e = Ui.Rect(parent, x - width / 2, y - 13, width, 26, new Color(0, 0, 0, 0.35f));
            Ui.Round(e, 13);
        }
    }
}
