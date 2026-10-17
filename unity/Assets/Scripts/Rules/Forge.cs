using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    public class Cost
    {
        public List<ItemQty> Items = new List<ItemQty>();
        public double Gold;
    }

    /// <summary>The smith: craft, reinforce, hone, salvage. A port of src/game/Forge.ts.</summary>
    public static class Forge
    {
        static ForgeTuning F => Db.B.Forge;

        public static bool CanAfford(Player player, Cost cost) => player.Gold >= cost.Gold && cost.Items.All(c => player.CountItem(c.ItemId) >= c.Qty);

        static void Pay(Player player, Cost cost)
        {
            player.Data.Gold -= cost.Gold;
            foreach (var c in cost.Items) player.RemoveItem(c.ItemId, c.Qty);
        }

        public static Cost RecipeCost(RecipeDef recipe) => new Cost { Items = recipe.Materials, Gold = recipe.Gold };

        public static Gear Craft(Player player, RecipeDef recipe, IRng rng)
        {
            var cost = RecipeCost(recipe);
            if (!CanAfford(player, cost) || player.FreeSlots == 0) return null;
            Pay(player, cost);
            var def = Db.Item(recipe.Result);
            var rolls = new Dictionary<string, double>();
            foreach (var key in Items.RollKeys(def)) rolls[key] = Math.Max(F.CraftFloor, Items.RollQuality(rng));
            var gear = player.MakeGear(recipe.Result, new GearTraits { Rolls = rolls }, true);
            player.Data.Gear.Add(gear);
            return gear;
        }

        public static int PlusOf(Gear gear) => gear.Plus ?? 0;

        public static Cost ReinforceCost(Gear gear)
        {
            var next = PlusOf(gear) + 1;
            return new Cost { Items = new List<ItemQty> { new ItemQty { ItemId = "scrap", Qty = next } }, Gold = Math.Max(5, JsMath.Round(Items.GearDef(gear).Price * 0.05 * next)) };
        }

        public static double ReinforceChance(Gear gear) => PlusOf(gear) < F.ReinforceChance.Length ? F.ReinforceChance[PlusOf(gear)] : 0;

        public static bool ReinforceRisky(Gear gear) => PlusOf(gear) >= F.ReinforceRiskFrom;

        /// <summary>success, fail, dropped or invalid.</summary>
        public static string Reinforce(Player player, Gear gear, IRng rng)
        {
            var cost = ReinforceCost(gear);
            if (PlusOf(gear) >= F.MaxPlus || !CanAfford(player, cost)) return "invalid";
            Pay(player, cost);
            if (rng.Chance(ReinforceChance(gear)))
            {
                gear.Plus = PlusOf(gear) + 1;
                return "success";
            }
            if (ReinforceRisky(gear))
            {
                gear.Plus = PlusOf(gear) - 1;
                return "dropped";
            }
            return "fail";
        }

        public static Cost HoneCost(Gear gear)
        {
            var price = Items.GearDef(gear).Price;
            return new Cost { Items = new List<ItemQty> { new ItemQty { ItemId = "dust", Qty = 1 + (int)Math.Floor(price / 200) } }, Gold = 10 + JsMath.Round(price * 0.05) };
        }

        public class HoneResult
        {
            public double Gain;
            public string DriftKey;
            public double DriftLoss;
        }

        public static HoneResult Hone(Player player, Gear gear, string key, IRng rng)
        {
            var cost = HoneCost(gear);
            var before = gear.Roll(key);
            if (before >= 100 || !CanAfford(player, cost)) return null;
            Pay(player, cost);
            gear.Rolls[key] = Math.Min(100, before + rng.RandInt(F.HoneGain[0], F.HoneGain[1]));
            var result = new HoneResult { Gain = gear.Rolls[key] - before };
            var others = Items.RollKeys(Items.GearDef(gear)).Where(k => k != key && gear.Roll(k) > 0).ToList();
            if (others.Count > 0 && rng.Chance(F.HoneDriftChance))
            {
                var k = others[(int)Math.Floor(rng.Next() * others.Count)];
                var was = gear.Roll(k);
                gear.Rolls[k] = Math.Max(0, was - rng.RandInt(F.HoneDrift[0], F.HoneDrift[1]));
                result.DriftKey = k;
                result.DriftLoss = was - gear.Rolls[k];
            }
            return result;
        }

        static readonly Dictionary<string, int> SalvageDust = new Dictionary<string, int> { ["standard"] = 0, ["fair"] = 1, ["fine"] = 2, ["superior"] = 3, ["masterwork"] = 5 };
        static readonly Dictionary<string, int> SigilDust = new Dictionary<string, int> { ["dawn"] = 3, ["dusk"] = 6, ["eclipse"] = 12 };

        static string GearElement(Gear gear)
        {
            var def = Items.GearDef(gear);
            if (def.IsWeapon) return def.Element;
            if (def.Resists == null) return null;
            var best = def.Resists.Where(kv => kv.Key != "all" && kv.Value > 0).OrderByDescending(kv => kv.Value).FirstOrDefault();
            return best.Key;
        }

        public static List<ItemQty> SalvageYield(Gear gear)
        {
            var def = Items.GearDef(gear);
            var output = new List<ItemQty> { new ItemQty { ItemId = "scrap", Qty = 1 + (int)Math.Floor(def.Price / 100) + PlusOf(gear) / 2 } };
            var dust = SalvageDust[Items.GradeTier(Items.Grade(def, gear.Rolls)).tier] + (gear.Sigil != null ? SigilDust[gear.Sigil] : 0);
            if (dust > 0) output.Add(new ItemQty { ItemId = "dust", Qty = dust });
            var element = GearElement(gear);
            if (element != null) output.Add(new ItemQty { ItemId = "essence_" + element, Qty = 1 + (int)Math.Floor(def.Price / 150) });
            return output;
        }

        public static bool CanSalvage(Player player, Gear gear) => gear.Locked != true && player.Data.Gear.Contains(gear);

        public static bool Salvage(Player player, Gear gear)
        {
            if (!CanSalvage(player, gear)) return false;
            var yields = SalvageYield(gear);
            player.Data.Gear.Remove(gear);
            foreach (var y in yields) player.AddItem(y.ItemId, y.Qty);
            return true;
        }

        public static string CostText(Player player, Cost cost)
        {
            var parts = cost.Items.Select(c => $"{Db.Item(c.ItemId).Name} {player.CountItem(c.ItemId)}/{c.Qty}").ToList();
            if (cost.Gold != 0) parts.Add($"{cost.Gold} g");
            return string.Join(" · ", parts);
        }
    }
}
