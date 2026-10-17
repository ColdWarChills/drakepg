using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>Cascading gear drops, a port of src/game/Loot.ts.</summary>
    public static class Loot
    {
        /// <summary>Rarest first: the order tiers are rolled in.</summary>
        public static readonly string[] Tiers = { "rare", "uncommon", "common" };

        public static double PityBonus(double misses)
        {
            var l = Db.B.Loot;
            return Math.Min(l.PityMax, Math.Max(0, misses - l.PityAfter) * l.PityStep);
        }

        static List<(string tier, double odds)> TiersFor(string tableId, string source, double misses)
        {
            var table = Db.LootTable(tableId);
            Dictionary<string, double> odds = null;
            if (source == "mob" && Db.B.Loot.TableMob != null) Db.B.Loot.TableMob.TryGetValue(tableId, out odds);
            odds ??= Db.B.Loot.For(source);
            var output = new List<(string, double)>();
            foreach (var tier in Tiers)
            {
                if (!table.Tiers.TryGetValue(tier, out var entries) || entries.Count == 0) continue;
                var b = Cheats.Boosted(odds[tier]);
                output.Add((tier, tier == "rare" ? Math.Min(1, b + PityBonus(misses)) : b));
            }
            return output;
        }

        /// <summary>Cascade: roll the rarest tier; only if it misses, roll the next. Null = no gear.</summary>
        public static string RollLootTier(string tableId, string source, IRng rng, double misses = 0)
        {
            foreach (var (tier, odds) in TiersFor(tableId, source, misses)) if (rng.Chance(odds)) return tier;
            return null;
        }

        public class Drop
        {
            public string Tier;
            public ItemGrant Grant;
        }

        public static Drop RollLoot(string tableId, string source, IRng rng, double misses = 0, bool extras = true)
        {
            var tier = RollLootTier(tableId, source, rng, misses);
            if (tier == null) return null;
            var entry = rng.WeightedPick(Db.LootTable(tableId).Tiers[tier], e => e.Weight ?? 1);
            return new Drop { Tier = tier, Grant = Items.GrantFor(entry.ItemId, rng, source, extras) };
        }

        /// <summary>Exact chance per kill of each tier (and "none").</summary>
        public static Dictionary<string, double> TierOdds(string tableId, string source, double misses = 0)
        {
            var output = new Dictionary<string, double> { ["rare"] = 0, ["uncommon"] = 0, ["common"] = 0, ["none"] = 1 };
            foreach (var (tier, odds) in TiersFor(tableId, source, misses))
            {
                output[tier] = output["none"] * odds;
                output["none"] -= output[tier];
            }
            return output;
        }
    }

    public class SetBonusTotals
    {
        public Dictionary<string, double> Stats = new Dictionary<string, double>();
        public double HpPct, Crit, DamagePct, DefensePct;
        public Dictionary<string, double> Resists = new Dictionary<string, double>();
    }

    /// <summary>Gear sets, a port of src/game/Sets.ts.</summary>
    public static class Sets
    {
        public static SetDef SetOfItem(string itemId)
        {
            var def = Db.Item(itemId);
            return (def.IsArmor || def.IsAccessory) && def.Set != null ? Db.Set(def.Set) : null;
        }

        /// <summary>Different pieces of each set being worn (in the order sets are first met).</summary>
        public static List<(string id, int count)> SetCounts(Dictionary<string, Gear> equipment)
        {
            var order = new List<string>();
            var pieces = new Dictionary<string, HashSet<string>>();
            foreach (var g in equipment.Values)
            {
                if (g == null) continue;
                var def = Items.GearDef(g);
                if (def.IsWeapon || def.Set == null) continue;
                if (!pieces.ContainsKey(def.Set))
                {
                    pieces[def.Set] = new HashSet<string>();
                    order.Add(def.Set);
                }
                pieces[def.Set].Add(def.Id);
            }
            return order.Select(id => (id, pieces[id].Count)).ToList();
        }

        public static List<(SetDef set, SetBonus bonus)> ActiveSetBonuses(Dictionary<string, Gear> equipment)
        {
            var output = new List<(SetDef, SetBonus)>();
            foreach (var (id, count) in SetCounts(equipment))
            {
                var set = Db.Set(id);
                foreach (var bonus in set.Bonuses) if (count >= bonus.Pieces) output.Add((set, bonus));
            }
            return output;
        }

        public static SetBonusTotals SetBonusTotals(Dictionary<string, Gear> equipment)
        {
            var t = new SetBonusTotals();
            foreach (var (_, b) in ActiveSetBonuses(equipment))
            {
                if (b.Stats != null) foreach (var kv in b.Stats) t.Stats[kv.Key] = (t.Stats.TryGetValue(kv.Key, out var s) ? s : 0) + kv.Value;
                if (b.Resists != null) foreach (var kv in b.Resists) t.Resists[kv.Key] = (t.Resists.TryGetValue(kv.Key, out var r) ? r : 0) + kv.Value;
                t.HpPct += b.HpPct ?? 0;
                t.Crit += b.Crit ?? 0;
                t.DamagePct += b.DamagePct ?? 0;
                t.DefensePct += b.DefensePct ?? 0;
            }
            return t;
        }
    }

    /// <summary>Testing cheats (session only, never saved), a port of src/game/cheats.ts.</summary>
    public static class Cheats
    {
        public static bool God, OneHit, Elites, Lucky, AllFeatures;
        /// <summary>Testing rates: XP, gold and every drop chance are multiplied by this.</summary>
        public static double Rates = 1;

        public static double Boosted(double p) => Math.Min(1, p * Rates);

        public static void Reset()
        {
            God = OneHit = Elites = Lucky = AllFeatures = false;
            Rates = 1;
        }
    }

    /// <summary>Chapter-by-chapter unlocks and "New!" news, a port of src/game/Features.ts.</summary>
    public static class Features
    {
        public static bool On(Player player, string id)
        {
            var f = Db.Feature(id);
            if (f.Disabled) return false;
            if (Cheats.AllFeatures) return true;
            return player.Data.Quests.TryGetValue(f.UnlockedBy, out var q) && q.Status == "done";
        }

        public class News
        {
            public List<FeatureDef> Features = new List<FeatureDef>();
            public List<SkillDef> Skills = new List<SkillDef>();
            public bool Any => Features.Count > 0 || Skills.Count > 0;
        }

        public static News Unseen(Player player)
        {
            var seen = new HashSet<string>(player.Data.Seen);
            return new News
            {
                Features = Db.Features().Where(f => On(player, f.Id) && !seen.Contains("feature:" + f.Id)).ToList(),
                Skills = player.KnownSkills.Where(s => !seen.Contains("skill:" + s.Id)).ToList(),
            };
        }

        public static void MarkSeen(Player player, News news)
        {
            foreach (var f in news.Features) player.Data.Seen.Add("feature:" + f.Id);
            foreach (var s in news.Skills)
            {
                player.Data.Seen.Add("skill:" + s.Id);
                player.CarryNew(s.Id);
                if (!player.Data.FreshSkills.Contains(s.Id)) player.Data.FreshSkills.Add(s.Id);
            }
        }

        public static void MarkSkillsSeen(Player player, bool tag)
        {
            MarkSeen(player, new News { Skills = Unseen(player).Skills });
            player.Data.Loadout = player.DefaultLoadout();
            if (!tag) player.Data.FreshSkills = new List<string>();
        }
    }
}
