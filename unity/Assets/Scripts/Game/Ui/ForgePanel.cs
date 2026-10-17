using System;
using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>The smith, a port of src/ui/forge.ts: craft from recipes, reinforce (+), hone a quality line, salvage.</summary>
    public static class ForgePanel
    {
        static readonly (string id, string label)[] Tabs =
        {
            ("craft", "Craft"),
            ("reinforce", "Reinforce"),
            ("hone", "Hone"),
            ("salvage", "Salvage"),
        };

        const int ListRows = 7;

        static string Help(string tab)
        {
            var F = Db.B.Forge;
            switch (tab)
            {
                case "craft": return $"Crafted gear rolls every line at {PanelKit.Num(F.CraftFloor)}% or better.";
                case "reinforce":
                    return $"Each + adds {PanelKit.Num(JsMath.Round(F.PlusPower * 100))}% base damage or defense, up to +{F.MaxPlus}. " +
                           $"From +{F.ReinforceRiskFrom}, a failure drops the item one +.";
                case "hone":
                    return $"Raises one quality line by {F.HoneGain[0]}-{F.HoneGain[1]}%. {PanelKit.Num(JsMath.Round(F.HoneDriftChance * 100))}% chance " +
                           $"another line slips {F.HoneDrift[0]}-{F.HoneDrift[1]}%.";
                default: return "Breaks bag gear into scrap, dust and essences. Worn and locked gear can't be salvaged.";
            }
        }

        static void Pager(VisualElement c, float x, float y, int page, int pages, Action<int> go)
        {
            if (pages <= 1) return;
            Ui.Text(c, x, y, $"{page + 1} / {pages}", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
            new UiButton(c, x - 80, y, "<", () => go(Math.Max(0, page - 1)), 50, 36).SetEnabled(page > 0);
            new UiButton(c, x + 80, y, ">", () => go(Math.Min(pages - 1, page + 1)), 50, 36).SetEnabled(page < pages - 1);
        }

        /// <summary>
        /// The smith's forge. `title` is e.g. "Brom's Forge", `recipes` the smith's recipe ids (NpcRole.Recipes);
        /// `onChange` refreshes the room (HUD, save).
        /// </summary>
        public static UiModal OpenForge(Player player, string title, IReadOnlyList<string> recipes, Action onChange)
        {
            var F = Db.B.Forge;
            var rng = new SystemRng();
            recipes ??= new List<string>();
            var modal = new UiModal(1120, 640, title, () => { });
            var tab = "craft";
            var page = 0;
            string selected = null;
            var confirmSalvage = false;
            // Result of the last action, shown on the bottom line (toasts would cover the tabs).
            string messageText = null;
            var messageColor = Palette.TextDim;

            void Say(string text, Color color)
            {
                messageText = text;
                messageColor = color;
            }

            void Changed()
            {
                onChange?.Invoke();
                Render();
            }

            void Render()
            {
                var c = modal.Content;
                c.Clear();
                Ui.Text(c, 40, 590, messageText ?? Help(tab), 17, messageText != null ? messageColor : Palette.TextDim, messageText != null, wrap: 1040);
                Ui.Text(c, 40, 62, $"Gold: {PanelKit.Num(player.Gold)}", 20, Palette.TextGold, true);
                for (int i = 0; i < Tabs.Length; i++)
                {
                    var t = Tabs[i];
                    new UiButton(c, 420 + i * 150, 72, t.label, () =>
                    {
                        tab = t.id;
                        page = 0;
                        confirmSalvage = false;
                        messageText = null;
                        Render();
                    }, 140, 42, 18, color: tab == t.id ? Design.Hex(0x6b5320) : Palette.Button);
                }

                if (tab == "craft")
                {
                    var pagesC = Math.Max(1, (recipes.Count + ListRows - 1) / ListRows);
                    page = Math.Min(page, pagesC - 1);
                    var shown = recipes.Skip(page * ListRows).Take(ListRows).ToList();
                    for (int i = 0; i < shown.Count; i++)
                    {
                        var recipe = Db.Recipe(shown[i]);
                        var def = Db.Item(recipe.Result);
                        var cost = Forge.RecipeCost(recipe);
                        var ok = Forge.CanAfford(player, cost);
                        var y = 150 + i * 60;
                        Ui.Text(c, 40, y - 26, def.Name, 19, bold: true);
                        Ui.Text(c, 40, y - 2, string.Join("  ·  ", PanelKit.ItemLines(def)), 14, Palette.TextDim);
                        Ui.Text(c, 40, y + 16, Forge.CostText(player, cost), 14, ok ? Palette.TextGood : Palette.TextBad);
                        new UiButton(c, 1000, y, "Craft", () =>
                        {
                            var made = Forge.Craft(player, recipe, rng);
                            if (made != null)
                            {
                                Sfx.Play("quest");
                                Say($"Made {PanelKit.LootName(def, made)}.", PanelKit.GradeInfo(def, made).NameColor);
                            }
                            else Say("Your bag is full.", Palette.TextBad);
                            Changed();
                        }, 140, 48, 18).SetEnabled(ok);
                    }
                    Pager(c, 560, 555, page, pagesC, p =>
                    {
                        page = p;
                        Render();
                    });
                    return;
                }

                // Gear list on the left, the selected piece on the right. Worn gear first.
                var worn = Items.AllSlots.Select(s => player.Data.Equipment.TryGetValue(s, out var w) ? w : null).Where(w => w != null).ToList();
                var list = tab == "salvage"
                    ? player.Data.Gear.Where(gr => Forge.CanSalvage(player, gr)).ToList()
                    : worn.Concat(player.Data.Gear).ToList();
                if (selected != null && !list.Any(gr => gr.Uid == selected)) selected = null;
                if (selected == null && list.Count > 0) selected = list[0].Uid;
                var pages = Math.Max(1, (list.Count + ListRows - 1) / ListRows);
                page = Math.Min(page, pages - 1);
                if (list.Count == 0) Ui.Text(c, 40, 130, "No gear to work on.", 18, Palette.TextDim);
                var rows = list.Skip(page * ListRows).Take(ListRows).ToList();
                for (int i = 0; i < rows.Count; i++)
                {
                    var g = rows[i];
                    var def = Items.GearDef(g);
                    var gi = PanelKit.GradeInfo(def, g);
                    var y = 140 + i * 58;
                    var isSel = g.Uid == selected;
                    var row = Ui.Rect(c, 30, y - 26, 500, 54, Design.Hex(isSel ? 0x3a2e4a : 0x241e2e), 8, 2,
                        isSel ? Palette.PanelBorder : PanelKit.WithAlpha(gi.Frame, 0.5f), true);
                    var uid = g.Uid;
                    row.RegisterCallback<PointerDownEvent>(e =>
                    {
                        if (GameRoot.I.Screen?.InputEnabled == false) return;
                        e.StopPropagation();
                        selected = uid;
                        confirmSalvage = false;
                        messageText = null;
                        Render();
                    });
                    var wornTag = worn.Contains(g) ? "  (worn)" : g.Locked == true ? "  (locked)" : "";
                    Ui.Text(c, 44, y - 22, PanelKit.ItemName(def, g) + wornTag, 18, gi.NameColor, true);
                    Ui.Text(c, 44, y + 2, PanelKit.GradeLabel(gi), 13, Palette.TextDim);
                }
                Pager(c, 280, 555, page, pages, p =>
                {
                    page = p;
                    Render();
                });

                var gear = list.FirstOrDefault(gr => gr.Uid == selected);
                if (gear == null) return;
                var gearDef = Items.GearDef(gear);
                var info = PanelKit.GradeInfo(gearDef, gear);
                const float x = 570;
                Ui.Text(c, x, 120, PanelKit.ItemName(gearDef, gear), 24, info.NameColor, true);
                Ui.Text(c, x, 152, PanelKit.GradeLabel(info), 16, Palette.TextDim);
                PanelKit.Lines(c, x, 176, PanelKit.ItemLines(gearDef, gear), 15, Palette.Text, 4, 520);

                if (tab == "reinforce") RenderReinforce(gear, x);
                else if (tab == "hone") RenderHone(gear, x);
                else RenderSalvage(gear, x);
            }

            void RenderReinforce(Gear gear, float x)
            {
                var c = modal.Content;
                var plus = Forge.PlusOf(gear);
                if (plus >= F.MaxPlus)
                {
                    Ui.Text(c, x, 290, $"Fully reinforced (+{F.MaxPlus}).", 20, Palette.TextGood);
                    return;
                }
                var cost = Forge.ReinforceCost(gear);
                var ok = Forge.CanAfford(player, cost);
                var lines = new[]
                {
                    $"Next: +{plus + 1}  (+{PanelKit.Num(JsMath.Round(F.PlusPower * 100 * (plus + 1)))}% base in total)",
                    $"Success chance: {PanelKit.Num(JsMath.Round(Forge.ReinforceChance(gear) * 100))}%",
                    Forge.ReinforceRisky(gear) ? $"On failure: drops to +{plus - 1}" : "On failure: only the materials are lost",
                    $"Cost: {Forge.CostText(player, cost)}",
                };
                PanelKit.Lines(c, x, 290, lines, 17, ok ? Palette.Text : Palette.TextBad, 10, 520);
                new UiButton(c, x + 120, 480, "Reinforce", () =>
                {
                    var before = PanelKit.ItemName(Items.GearDef(gear), gear);
                    var result = Forge.Reinforce(player, gear, rng);
                    if (result == "success")
                    {
                        Sfx.Play("levelup");
                        Say($"Success! {before} is now +{Forge.PlusOf(gear)}.", Palette.TextGood);
                    }
                    else if (result == "dropped")
                    {
                        Sfx.Play("denied");
                        Say($"It cracked. Down to +{Forge.PlusOf(gear)}.", Palette.TextBad);
                    }
                    else if (result == "fail")
                    {
                        Sfx.Play("denied");
                        Say("It failed. The materials are gone.", Palette.TextBad);
                    }
                    Changed();
                }, 220, 56, 20).SetEnabled(ok);
            }

            void RenderHone(Gear gear, float x)
            {
                var c = modal.Content;
                var cost = Forge.HoneCost(gear);
                var ok = Forge.CanAfford(player, cost);
                Ui.Text(c, x, 290, $"Cost per hone: {Forge.CostText(player, cost)}", 16, ok ? Palette.Text : Palette.TextBad);
                var keys = Items.RollKeys(Items.GearDef(gear));
                for (int i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var y = 350 + i * 64;
                    var q = gear.Roll(key);
                    Ui.Text(c, x, y, Items.RollLabels[key], 18, bold: true, ay: 0.5f);
                    Ui.Rect(c, x + 150, y - 8, 200, 16, Design.Hex(0x111111), 4);
                    if (q > 0) Ui.Rect(c, x + 150, y - 8, (float)(2 * q), 16, PanelKit.GradeColor(Items.GradeTier(q).tier), 4);
                    Ui.Text(c, x + 362, y, $"{PanelKit.Num(q)}%", 17, ay: 0.5f);
                    new UiButton(c, x + 470, y, q >= 100 ? "Max" : "Hone", () =>
                    {
                        var r = Forge.Hone(player, gear, key, rng);
                        if (r != null)
                        {
                            Sfx.Play("heal");
                            var drift = r.DriftKey != null ? $"  {Items.RollLabels[r.DriftKey]} slipped -{PanelKit.Num(r.DriftLoss)}%." : "";
                            Say($"{Items.RollLabels[key]} +{PanelKit.Num(r.Gain)}%.{drift}", r.DriftKey != null ? Palette.TextGold : Palette.TextGood);
                        }
                        Changed();
                    }, 100, 44, 17).SetEnabled(ok && q < 100);
                }
            }

            void RenderSalvage(Gear gear, float x)
            {
                var c = modal.Content;
                var yields = Forge.SalvageYield(gear).Select(y => $"{Db.Item(y.ItemId).Name} x{y.Qty}").ToList();
                PanelKit.Lines(c, x, 290, new[] { "Salvaging gives:" }.Concat(yields), 18, Palette.Text, 8);
                var gi = PanelKit.GradeInfo(Items.GearDef(gear), gear);
                var precious = gear.Sigil != null || Forge.PlusOf(gear) > 0 || gi.Grade >= 40;
                new UiButton(c, x + 120, 480, confirmSalvage ? "Really break it?" : "Salvage", () =>
                {
                    if (precious && !confirmSalvage)
                    {
                        confirmSalvage = true;
                        Render();
                        return;
                    }
                    var name = PanelKit.ItemName(Items.GearDef(gear), gear);
                    if (Forge.Salvage(player, gear))
                    {
                        Sfx.Play("hit");
                        Say($"Salvaged {name}: {string.Join(", ", yields)}.", Palette.TextGood);
                    }
                    confirmSalvage = false;
                    selected = null;
                    Changed();
                }, 240, 56, 20, color: confirmSalvage ? Design.Hex(0x7a2e2e) : (Color?)null);
            }

            Render();
            return modal;
        }
    }
}
