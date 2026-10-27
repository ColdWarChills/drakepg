using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>Character sheet, a port of openCharacterPanel in src/ui/panels.ts: stats, stat points and skills.</summary>
    public static class CharacterPanel
    {
        static readonly Dictionary<string, string> StatHelp = new Dictionary<string, string>
        {
            ["str"] = "Melee damage",
            ["int"] = "Magic damage, max MP",
            ["dex"] = "Pierce damage, hit, crit",
            ["end"] = "Max HP",
        };

        /// <summary>
        /// Stats, stat points and the skill loadout. Gear lives in the inventory: the Inventory button closes this and
        /// calls `openInventory`; the skills still to find are in the spellbook (`openSpellbook`). `onChange` refreshes the
        /// room (HUD, save).
        /// </summary>
        public static UiModal OpenCharacterPanel(Player player, Action onChange, Action openInventory, Action openSpellbook = null)
        {
            var modal = new UiModal(1100, 600, "Character", () => { });
            var anims = new List<Anim.Handle>();
            PanelKit.StopOnClose(modal, anims);
            // What a skill click said; it stays on the info line (through the re-render) until the pointer leaves.
            string pinned = null;

            void Render()
            {
                PanelKit.StopAll(anims);
                modal.Content.Clear();
                var c = modal.Content;
                var s = player.Stats;

                // --- Stats column ---
                var range = player.AttackRange;
                var info = Ui.Text(c, 40, 545, pinned ?? "", 18, Palette.TextDim, wrap: 1020);
                Ui.Text(c, 40, 64, player.Name, 26, bold: true);
                Ui.Text(c, 40, 98, $"Level {player.Level} {player.ClassDef.Name}   ·   {PanelKit.Num(player.Xp)} / {PanelKit.Num(player.XpNeeded)} XP", 18, Palette.TextDim);
                Ui.Text(c, 40, 130, $"HP  {PanelKit.Num(player.Hp)} / {PanelKit.Num(player.MaxHp)}      MP  {PanelKit.Num(player.Mp)} / {PanelKit.Num(player.MaxMp)}", 20);
                if (player.ClassDef.MpOnHit is double rage && rage != 0)
                    Ui.Text(c, 40, 152, $"Rage: +{PanelKit.Num(rage)} MP whenever an enemy hit lands on you", 15, Palette.TextMagic);

                var points = player.Data.StatPoints;
                if (points > 0)
                {
                    var label = Ui.Text(c, 40, 176, $"{points} stat point{(points > 1 ? "s" : "")} to spend", 19, Palette.TextGold, true);
                    anims.Add(Anim.Float(1, 0.55f, 0.6f, v => label.style.opacity = v, Ease.Linear, repeat: -1, yoyo: true));
                }
                for (int i = 0; i < Stat.Keys.Length; i++)
                {
                    var k = Stat.Keys[i];
                    var y = 214 + i * 40;
                    Ui.Text(c, 40, y, Player.StatLabels[k], 21, bold: true, ay: 0.5f);
                    Ui.Text(c, 110, y, PanelKit.Num(s[k]), 21, ay: 0.5f);
                    Ui.Text(c, points > 0 ? 205 : 165, y, StatHelp[k], 15, Palette.TextDim, ay: 0.5f);
                    if (points > 0)
                    {
                        new UiButton(c, 175, y, "+", () =>
                        {
                            player.SpendStatPoint(k);
                            onChange?.Invoke();
                            Render();
                        }, 40, 34, 22);
                    }
                }
                var element = player.WeaponElement;
                var damageType = Db.Data.DamageTypeLabels.TryGetValue(player.WeaponDamageType, out var dt) ? dt : player.WeaponDamageType;
                var damageBonus = player.DamageBonus != 0 ? $"  ·  Damage +{PanelKit.Num(JsMath.Round(player.DamageBonus * 100))}%" : "";
                var resists = PanelKit.ResistText(player.Resists, "Resists  ");
                var lines = new List<string>
                {
                    $"Attack  {PanelKit.Num(range[0])}-{PanelKit.Num(range[1])} {damageType} · {(element != null ? PanelKit.ElementLabel(element) : "Neutral")}",
                    $"Crit  {PanelKit.Fixed1(player.CritChance * 100)}%{damageBonus}",
                    PanelKit.DefenseText(player.DefenseFor("melee"), player.DefenseFor("pierce"), player.DefenseFor("magic")),
                    resists != "" ? resists : "Resists  none",
                    $"Gold  {PanelKit.Num(player.Gold)}",
                };
                foreach (var (set, bonus) in Sets.ActiveSetBonuses(player.Data.Equipment)) lines.Add($"{set.Name} ({bonus.Pieces}): {PanelKit.SetBonusText(bonus)}");
                PanelKit.Lines(c, 40, 380, lines, 17, Palette.Text, 6);

                // --- Equipped (details and changes in the inventory) ---
                Ui.Text(c, 380, 70, "Equipped", 22, Palette.TextGold, true);
                // The weapon by name; the other 7 slots as a count, plus the sets being worn.
                var weapon = player.Data.Equipment.TryGetValue("weapon", out var wg) ? wg : null;
                Ui.Text(c, 380, 108, "Weapon", 17, Palette.TextDim);
                if (weapon != null)
                {
                    var def = Items.GearDef(weapon);
                    Ui.Text(c, 470, 108, PanelKit.LootName(def, weapon), 17, PanelKit.GradeInfo(def, weapon).NameColor, true);
                }
                else Ui.Text(c, 470, 108, "(none)", 17, Palette.TextDim);
                var worn = Items.EquipSlots.Count(slot => slot != "weapon" && player.Data.Equipment.TryGetValue(slot, out var piece) && piece != null);
                var sets = Sets.SetCounts(player.Data.Equipment).Select(sc => $"{Db.Set(sc.id).Name} {sc.count}/{Db.Set(sc.id).Pieces.Count}");
                Ui.Text(c, 380, 138, string.Join("  ·  ", new[] { $"Gear  {worn}/{Items.EquipSlots.Length - 1} slots" }.Concat(sets)), 17, Palette.TextDim);
                new UiButton(c, 960, 122, "Inventory (I)", () => modal.Close(openInventory), 190, 48, 19);

                // --- Skills ---
                // Known skills (level skills, then books read); carried ones (filled diamond) go into battle. Click one to
                // carry it or put it away. The ones still to find, and where, are in the spellbook.
                var slots = Db.B.SkillSlots;
                Ui.Text(c, 380, 190, "Skills", 22, Palette.TextGold, true);
                Ui.Text(c, 460, 194, $"carried {player.Data.Loadout.Count} / {slots}  ·  click to swap", 15, Palette.TextDim);
                if (openSpellbook != null) new UiButton(c, 960, 196, "Spellbook (K)", () => modal.Close(openSpellbook), 190, 44, 18);
                var skills = player.KnownSkills;
                var perColumn = Math.Max(8, (skills.Count + 2) / 3);
                for (int i = 0; i < skills.Count && i < perColumn * 3; i++)
                {
                    var skill = skills[i];
                    var known = true;
                    var carried = player.IsCarried(skill.Id);
                    var x = 380 + (i / perColumn) * 230;
                    var y = 236 + (i % perColumn) * 32;
                    var row = SkillRow(c, x, y, known, carried, skill.Name, SkillUi.Icon(skill), SkillUi.IconTint(skill));
                    var cd = skill.Cooldown > 0 ? $", cooldown {skill.Cooldown}" : "";
                    var describe = $"{skill.Name} ({PanelKit.Num(skill.MpCost)} MP{cd}): {skill.Description}";
                    row.pickingMode = PickingMode.Position;
                    row.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        if (pinned == null) info.text = describe;
                    });
                    row.RegisterCallback<PointerLeaveEvent>(_ =>
                    {
                        // A row taken away by a re-render doesn't count as the pointer leaving.
                        if (row.panel == null) return;
                        pinned = null;
                        info.text = "";
                    });
                    if (!known) continue;
                    row.RegisterCallback<PointerUpEvent>(e =>
                    {
                        if (GameRoot.I.Screen?.InputEnabled == false) return;
                        e.StopPropagation();
                        var result = player.ToggleCarry(skill.Id);
                        onChange?.Invoke();
                        // The new info line (Render makes one) starts with this.
                        pinned = result == "full" ? $"All {slots} skill slots are full: click a carried skill (filled diamond) to put it away first." : describe;
                        Render();
                    });
                }
            }

            Render();
            return modal;
        }

        /// <summary>
        /// One skill line: a diamond (filled = carried, hollow = learned; none when not learned yet) and the name.
        /// The TS uses the ◆/◇ glyphs, which the default font may not have, so the diamond is drawn.
        /// </summary>
        static VisualElement SkillRow(VisualElement c, float x, float y, bool known, bool carried, string text, string icon = null, Color? tint = null)
        {
            var color = carried ? Palette.TextMagic : known ? Palette.Text : Palette.TextDim;
            var row = new VisualElement();
            Ui.Place(row, x, y);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            if (known)
            {
                var gem = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Size(gem, 9, 9);
                gem.style.flexShrink = 0;
                gem.style.marginLeft = 3;
                gem.style.marginRight = 9;
                gem.style.rotate = new Rotate(Angle.Degrees(45));
                if (carried) gem.style.backgroundColor = color;
                else Ui.Border(gem, 1.5f, color);
                row.Add(gem);
            }
            // The skill's drawing (faded until it's learned).
            if (Felt.HasIcon(icon))
            {
                var ic = Felt.Icon(null, 0, 0, 22, icon, tint != null);
                if (tint is Color t) ic.style.unityBackgroundImageTintColor = Felt.Readable(t);
                ic.style.position = Position.Relative;
                ic.style.flexShrink = 0;
                ic.style.marginLeft = known ? 0 : 21;
                ic.style.marginRight = 7;
                ic.style.opacity = known ? 1 : 0.4f;
                row.Add(ic);
            }
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 17;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = carried ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            row.Add(label);
            c.Add(row);
            return row;
        }
    }
}
