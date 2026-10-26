using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// The spellbook (K, or the Character panel): every skill your class can get (docs/BUILD_VARIETY_SPEC.md). Known ones
    /// in color, click one to carry it into battle or put it away; the rest dark, with where to find them. Tabs per
    /// element, HoMM3-like. Rules and sources: Rules/Books.cs.
    /// </summary>
    public static class SpellbookPanel
    {
        const float W = 1180, H = 640;
        const float ListX = 40, ListY = 150, RowH = 36, ListW = 520;
        const int PerPage = 11;
        const float CardX = 600, CardY = 150, CardW = 540;

        static readonly Color ActiveButton = Design.Hex(0x6b5320);

        /// <summary>Remembered while the game runs, so the book reopens on the page you left.</summary>
        static string rememberedTab = "all";

        public static UiModal Open(Player player, Action onChange)
        {
            var modal = new UiModal(W, H, "Spellbook", () => { });
            string selected = null;
            string note = null;
            int page = 0;

            void Render()
            {
                modal.Content.Clear();
                var c = modal.Content;
                var all = Books.Spellbook(player);
                selected ??= all.FirstOrDefault(e => e.Known)?.Skill.Id;

                // Tabs: All, Weapon (no fixed element), then the eight elements.
                var tabs = new List<(string id, string label)> { ("all", "All"), ("weapon", "Weapon") };
                tabs.AddRange(Db.Data.Elements.Select(e => (e, Db.Data.ElementLabels[e])));
                for (int i = 0; i < tabs.Count; i++)
                {
                    var t = tabs[i];
                    var active = rememberedTab == t.id;
                    new UiButton(c, 40 + 52 + i * 110, 96, t.label, () =>
                    {
                        rememberedTab = t.id;
                        page = 0;
                        note = null;
                        Render();
                    }, 104, 40, 15, null, active ? ActiveButton : Palette.Button);
                }

                var shown = all.Where(e => rememberedTab == "all" || (SkillUi.ElementOf(e.Skill) ?? "weapon") == rememberedTab).ToList();
                var pages = Math.Max(1, (int)Math.Ceiling(shown.Count / (double)PerPage));
                page = Math.Max(0, Math.Min(page, pages - 1));

                // --- Left page: the list ---
                Ui.Rect(c, ListX - 8, ListY - 8, ListW + 16, PerPage * RowH + 16, new Color(0, 0, 0, 0.18f), 10);
                var rows = shown.Skip(page * PerPage).Take(PerPage).ToList();
                for (int i = 0; i < rows.Count; i++)
                {
                    var entry = rows[i];
                    Row(c, ListX, ListY + i * RowH, entry, player, entry.Skill.Id == selected, () =>
                    {
                        selected = entry.Skill.Id;
                        note = null;
                        Render();
                    });
                }
                if (shown.Count == 0) Ui.Text(c, ListX + 12, ListY + 12, "Nothing here yet.", 17, Palette.TextDim);
                if (pages > 1)
                {
                    var mid = ListX + ListW / 2;
                    var y = ListY + PerPage * RowH + 34;
                    Ui.Text(c, mid, y, $"{page + 1} / {pages}", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
                    new UiButton(c, mid - 64, y, "<", () =>
                    {
                        page -= 1;
                        Render();
                    }, 44, 38).SetEnabled(page > 0);
                    new UiButton(c, mid + 64, y, ">", () =>
                    {
                        page += 1;
                        Render();
                    }, 44, 38).SetEnabled(page < pages - 1);
                }

                // --- Right page: the picked skill ---
                var pick = all.FirstOrDefault(e => e.Skill.Id == selected);
                if (pick != null) Card(c, pick, player, note, () =>
                {
                    var result = player.ToggleCarry(pick.Skill.Id);
                    onChange?.Invoke();
                    note = result == "full" ? $"All {Db.B.SkillSlots} skill slots are full: put a carried skill away first." : null;
                    Render();
                });

                // Bottom: what's carried into battle.
                var carried = player.CarriedSkills.Select(s => s.Name).ToList();
                Ui.Text(c, CardX, ListY + PerPage * RowH + 22, $"Carried {carried.Count} / {Db.B.SkillSlots}:  {(carried.Count > 0 ? string.Join(", ", carried) : "none")}", 17,
                    Palette.TextMagic, wrap: CardW);
            }

            Render();
            return modal;
        }

        /// <summary>One line of the list: a diamond (filled = carried, hollow = known), the drawing, the name and a short state.</summary>
        static void Row(VisualElement c, float x, float y, Books.Entry entry, Player player, bool picked, Action pick)
        {
            var carried = entry.Known && player.IsCarried(entry.Skill.Id);
            var color = carried ? Palette.TextMagic : entry.Known ? Palette.Text : Palette.TextDim;
            var row = new VisualElement { pickingMode = PickingMode.Position };
            Ui.Place(row, x, y);
            Ui.Size(row, ListW, RowH - 4);
            Ui.Round(row, 6);
            if (picked) row.style.backgroundColor = new Color(1, 0.9f, 0.6f, 0.12f);
            c.Add(row);

            if (entry.Known)
            {
                var gem = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Place(gem, 10, RowH / 2 - 7);
                Ui.Size(gem, 9, 9);
                gem.style.rotate = new Rotate(Angle.Degrees(45));
                if (carried) gem.style.backgroundColor = color;
                else Ui.Border(gem, 1.5f, color);
                row.Add(gem);
            }
            var icon = Felt.Icon(row, 28, 3, 24, SkillUi.Icon(entry.Skill), true);
            if (SkillUi.IconTint(entry.Skill) is Color tint) icon.style.unityBackgroundImageTintColor = tint;
            icon.style.opacity = entry.Known ? 1 : 0.35f;
            Ui.Text(row, 62, RowH / 2 - 2, entry.Skill.Name, 17, color, carried, ay: 0.5f);
            // Known: its cost. Not yet: the level it needs, or where it's found.
            var state = entry.Known ? $"{PanelKit.Num(entry.Skill.MpCost)} MP"
                : entry.Group == "level" ? $"Lv {entry.Level}" : entry.Sources[0];
            Ui.Text(row, ListW - 10, RowH / 2 - 2, state, 14, entry.Known ? Palette.TextDim : Design.Hex(0x9a8f78), ax: 1, ay: 0.5f);
            row.RegisterCallback<PointerUpEvent>(e =>
            {
                if (GameRoot.I.Screen?.InputEnabled == false) return;
                e.StopPropagation();
                Sfx.Play("click");
                pick();
            });
        }

        /// <summary>The picked skill: what it does, where it comes from, and Carry / Put away when it's known.</summary>
        static void Card(VisualElement c, Books.Entry entry, Player player, string note, Action toggle)
        {
            var s = entry.Skill;
            Ui.Rect(c, CardX - 8, CardY - 8, CardW + 16, PerPage * RowH + 16, new Color(0, 0, 0, 0.18f), 10);
            var icon = Felt.Icon(c, CardX + 8, CardY + 6, 64, SkillUi.Icon(s), true);
            if (SkillUi.IconTint(s) is Color tint) icon.style.unityBackgroundImageTintColor = tint;
            icon.style.opacity = entry.Known ? 1 : 0.45f;
            Ui.Text(c, CardX + 88, CardY + 4, s.Name, 26, entry.Known ? Palette.Text : Palette.TextDim, true);
            var who = entry.Group switch
            {
                "level" => $"{player.ClassDef.Name} skill · level {entry.Level}",
                "class" => $"{player.ClassDef.Name} book · read at level {entry.Level}",
                "family" => $"{SkillUi.KindWord(s)} family · read at level {entry.Level}",
                _ => $"Any class · read at level {entry.Level}",
            };
            Ui.Text(c, CardX + 88, CardY + 40, who, 16, Palette.TextDim);
            var cd = s.Cooldown > 0 ? $"  ·  cooldown {s.Cooldown}" : "";
            var kind = SkillUi.KindWord(s);
            Ui.Text(c, CardX + 8, CardY + 92, $"{PanelKit.Num(s.MpCost)} MP{cd}{(kind != null ? $"  ·  {kind}" : "")}", 18, Palette.TextGold);
            Ui.Text(c, CardX + 8, CardY + 124, s.Description, 18, Palette.Text, wrap: CardW - 16);

            var lines = new List<(string text, Color color)>();
            if (entry.Known)
            {
                lines.Add((player.IsCarried(s.Id) ? "Known · carried into battle" : "Known · not carried", Palette.TextMagic));
            }
            else
            {
                lines.Add((entry.Group == "level" ? $"Learned at level {entry.Level}" : "Not found yet", Palette.TextDim));
                if (entry.Group != "level") foreach (var src in entry.Sources) lines.Add(("· " + src, Palette.Text));
                if (entry.Group != "level" && player.Level < entry.Level) lines.Add(($"Needs level {entry.Level} to read", Design.Hex(0xd9604a)));
            }
            if (entry.Known && entry.Group != "level" && entry.Sources.Count > 0) lines.Add(("Came from: " + entry.Sources[0], Palette.TextDim));
            var y = CardY + 240;
            foreach (var (text, color) in lines)
            {
                Ui.Text(c, CardX + 8, y, text, 17, color, wrap: CardW - 16);
                y += 28;
            }
            if (note != null) Ui.Text(c, CardX + 8, y + 6, note, 16, Design.Hex(0xd9604a), wrap: CardW - 16);
            if (entry.Known)
                new UiButton(c, CardX + CardW / 2, CardY + PerPage * RowH - 34, player.IsCarried(s.Id) ? "Put away" : "Carry into battle", toggle, 240, 50, 19);
        }
    }
}
