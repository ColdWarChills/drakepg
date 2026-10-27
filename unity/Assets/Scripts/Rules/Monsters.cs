using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>Elites, Heroic bosses, Lucky Implings, chests and mimics: ports of Elites.ts, Heroic.ts, Lucky.ts, Chests.ts.</summary>
    public static class Monsters
    {
        static Balance B => Db.B;

        static double[] Range(double[] r, double m) => new[] { JsMath.Round(r[0] * m), JsMath.Round(r[1] * m) };

        // --- Elites ---

        /// <summary>An affix id if this roaming spawn should be an Elite, else null.</summary>
        public static string RollElite(IRng rng, bool questWantsOne = false)
        {
            var odds = Cheats.Elites ? 1 : questWantsOne ? B.Elite.QuestChance : B.Elite.Chance;
            if (!rng.Chance(odds)) return null;
            var list = Db.Affixes();
            return list[rng.RandInt(0, list.Count - 1)].Id;
        }

        public static MonsterDef MakeElite(MonsterDef def, string affixId)
        {
            var a = Db.Affix(affixId);
            var e = B.Elite;
            var defense = new Dictionary<string, double>();
            foreach (var kv in def.Defense) defense[kv.Key] = JsMath.Round(kv.Value * (1 + (a.Defense ?? 0)));
            var resists = def.Resists != null ? new Dictionary<string, double>(def.Resists) : new Dictionary<string, double>();
            if (a.Resists != null) foreach (var kv in a.Resists) resists[kv.Key] = (resists.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            var copy = def.Clone();
            copy.Name = $"{a.Name} {def.Name}";
            copy.Level = def.Level + e.Levels;
            copy.Look = def.Look.Clone();
            copy.Look.Scale = def.Look.Scale * e.Size;
            copy.Hp = JsMath.Round(def.Hp * e.Hp * (1 + (a.Hp ?? 0)));
            copy.Damage = Range(def.Damage, e.Damage * (1 + (a.Damage ?? 0)));
            copy.Defense = defense;
            copy.Resists = resists;
            copy.Attacks = def.Attacks.Select(atk =>
            {
                var extra = (a.Applies ?? new List<Apply>()).Where(x => !(atk.Applies?.Any(y => y.Status == x.Status) ?? false)).ToList();
                if (!(atk.Power > 0 && extra.Count > 0)) return atk;
                var c = atk.Clone();
                c.Applies = new List<Apply>(atk.Applies ?? new List<Apply>());
                c.Applies.AddRange(extra);
                return c;
            }).ToList();
            copy.Xp = JsMath.Round(def.Xp * e.Rewards);
            copy.Gold = Range(def.Gold, e.Rewards);
            copy.Elite = new EliteInfo { Affix = a.Id };
            return copy;
        }

        /// <summary>Only an affix's own effects (a tower twist): no Elite size-up, rewards or name change.</summary>
        public static MonsterDef ApplyAffix(MonsterDef def, string affixId)
        {
            var a = Db.Affix(affixId);
            var defense = new Dictionary<string, double>();
            foreach (var kv in def.Defense) defense[kv.Key] = JsMath.Round(kv.Value * (1 + (a.Defense ?? 0)));
            var resists = def.Resists != null ? new Dictionary<string, double>(def.Resists) : new Dictionary<string, double>();
            if (a.Resists != null) foreach (var kv in a.Resists) resists[kv.Key] = (resists.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            var copy = def.Clone();
            copy.Hp = JsMath.Round(def.Hp * (1 + (a.Hp ?? 0)));
            copy.Damage = Range(def.Damage, 1 + (a.Damage ?? 0));
            copy.Defense = defense;
            copy.Resists = resists;
            copy.Attacks = def.Attacks.Select(atk =>
            {
                var extra = (a.Applies ?? new List<Apply>()).Where(x => !(atk.Applies?.Any(y => y.Status == x.Status) ?? false)).ToList();
                if (!(atk.Power > 0 && extra.Count > 0)) return atk;
                var c = atk.Clone();
                c.Applies = new List<Apply>(atk.Applies ?? new List<Apply>());
                c.Applies.AddRange(extra);
                return c;
            }).ToList();
            return copy;
        }

        // --- Heroic ---

        public static MonsterDef MakeHeroic(MonsterDef def)
        {
            var h = B.Heroic;
            var extra = def.Heroic?.Attacks ?? new List<MonsterAttack>();
            var copy = def.Clone();
            copy.Name = "Heroic " + (def.Name.StartsWith("The ") ? def.Name.Substring(4) : def.Name);
            copy.Level = def.Level + h.Levels;
            copy.Damage = Range(def.Damage, h.Damage);
            copy.Attacks = def.Attacks.Concat(extra).ToList();
            if (def.Phase != null)
            {
                copy.Phase = new MonsterPhase
                {
                    At = def.Phase.At, Text = def.Phase.Text, Element = def.Phase.Element, Resists = def.Phase.Resists,
                    Attacks = (def.Phase.Attacks ?? def.Attacks).Concat(extra).ToList(),
                };
            }
            copy.IsHeroic = true;
            return copy;
        }

        // --- Lucky Implings ---

        public const string LuckyId = "lucky_impling";

        public static bool IsLucky(string monsterId) => monsterId == LuckyId;

        public static bool RollLucky(IRng rng) => Cheats.Lucky || rng.Chance(B.Lucky.Chance);

        public static MonsterDef MakeImpling(ZoneDef zone)
        {
            var b = Db.Monster(LuckyId);
            var l = B.Lucky;
            var mobs = zone.Encounters.Select(e => Db.Monster(e.MonsterId)).ToList();
            double Avg(Func<MonsterDef, double> f) => mobs.Sum(f) / mobs.Count;
            var loot = ZoneLootTable(zone);
            double Dmg(int i) => Math.Max(1, JsMath.Round(Avg(m => m.Damage[i]) * l.Damage));
            var gold = Avg(m => (m.Gold[0] + m.Gold[1]) / 2) * l.Gold;
            var copy = b.Clone();
            copy.Level = ZoneLevel(zone);
            copy.Hp = JsMath.Round(Avg(m => m.Hp) * l.Hp);
            copy.Stats = new Stats
            {
                Str = JsMath.Round(Avg(m => m.Stats.Str) * 0.5),
                Int = JsMath.Round(Avg(m => m.Stats.Int) * 0.5),
                Dex = mobs.Max(m => m.Stats.Dex) + l.Dex,
                End = JsMath.Round(Avg(m => m.Stats.End) * 0.5),
            };
            copy.Damage = new[] { Dmg(0), Dmg(1) };
            copy.Defense = new Dictionary<string, double>
            {
                ["melee"] = JsMath.Round(Avg(m => m.DefenseOf("melee"))),
                ["pierce"] = JsMath.Round(Avg(m => m.DefenseOf("pierce"))),
                ["magic"] = JsMath.Round(Avg(m => m.DefenseOf("magic"))),
            };
            copy.Xp = JsMath.Round(Avg(m => m.Xp) * l.Xp);
            copy.Gold = new[] { JsMath.Round(gold * 0.8), JsMath.Round(gold * 1.2) };
            copy.EscapeAfter = l.EscapeAfter;
            if (loot != null) copy.Loot = loot;
            return copy;
        }

        public static ItemGrant LuckySigil(string tableId, IRng rng) => Lucky.LuckySigil(tableId, rng);

        // --- Chests and mimics ---

        public const int MimicUid = -2;

        public static string ZoneLootTable(ZoneDef zone)
        {
            foreach (var e in zone.Encounters)
            {
                var loot = Db.Monster(e.MonsterId).Loot;
                if (loot != null) return loot;
            }
            return null;
        }

        public static int ZoneLevel(ZoneDef zone) => zone.RecommendedLevel[1];

        /// <summary>Null: no chest. Otherwise whether it is a mimic.</summary>
        public static bool? RollChest(IRng rng)
        {
            if (!rng.Chance(B.Chest.Chance)) return null;
            return rng.Chance(B.Chest.MimicChance);
        }

        public static Rewards RollChestLoot(ZoneDef zone, IRng rng, bool extras = true, bool withMaterials = true)
        {
            var level = ZoneLevel(zone);
            var items = new List<ItemGrant>();
            var drops = zone.Encounters.SelectMany(e => Db.Monster(e.MonsterId).Drops).ToList();
            var materials = withMaterials ? drops.Where(d => Db.Item(d.ItemId).Type == "material").ToList() : new List<Drop>();
            var potions = drops.Where(d => Db.Item(d.ItemId).Type == "consumable").ToList();
            for (int i = rng.RandInt(1, 2); i > 0 && materials.Count > 0; i--)
            {
                var m = materials[rng.RandInt(0, materials.Count - 1)];
                for (int n = rng.RandInt(1, 2); n > 0; n--) items.Add(new ItemGrant { ItemId = m.ItemId });
            }
            if (potions.Count > 0 && rng.Chance(Cheats.Boosted(0.35))) items.Add(new ItemGrant { ItemId = potions[rng.RandInt(0, potions.Count - 1)].ItemId });
            var table = ZoneLootTable(zone);
            var gear = table != null ? Loot.RollLoot(table, "chest", rng, 0, extras) : null;
            if (gear != null) items.Add(gear.Grant);
            return new Rewards { Xp = 0, Gold = rng.RandInt(8, 14) * level * Cheats.Rates, Items = items };
        }

        public static MonsterDef MakeMimic(ZoneDef zone, IRng rng)
        {
            var b = Db.Monster("mimic");
            var level = ZoneLevel(zone);
            double Grow(double v) => JsMath.Round(v + (level - 1) * 1.2);
            var loot = ZoneLootTable(zone);
            var copy = b.Clone();
            copy.Level = level;
            copy.Hp = JsMath.Round(30 + 28 * level);
            copy.Stats = new Stats { Str = Grow(b.Stats.Str), Int = Grow(b.Stats.Int), Dex = Grow(b.Stats.Dex), End = Grow(b.Stats.End) };
            copy.Damage = new[] { JsMath.Round(3 + 1.5 * level), JsMath.Round(6 + 1.9 * level) };
            copy.Defense = new Dictionary<string, double>
            {
                ["melee"] = JsMath.Round(2 + 0.9 * level), ["pierce"] = JsMath.Round(2 + 0.9 * level), ["magic"] = JsMath.Round(1 + 0.6 * level),
            };
            copy.Xp = JsMath.Round(10 * level + 1.2 * level * level);
            copy.Gold = new double[] { 4 * level, 8 * level };
            if (loot != null) copy.Loot = loot;
            copy.Drops = RollChestLoot(zone, rng).Items.Where(g => g.Rolls == null).Select(g => new Drop { ItemId = g.ItemId, Chance = 1 }).ToList();
            return copy;
        }
    }

    public static class Lucky
    {
        /// <summary>The Impling's jackpot: a piece of the area's gear as a Sigil with its mods.</summary>
        public static ItemGrant LuckySigil(string tableId, IRng rng)
        {
            var table = Db.LootTable(tableId);
            var itemId = Loot.RollLoot(tableId, "elite", rng, 0, false)?.Grant.ItemId;
            if (itemId == null)
            {
                var all = new List<LootEntry>();
                foreach (var t in new[] { "common", "uncommon", "rare" }) if (table.Tiers.TryGetValue(t, out var l)) all.AddRange(l);
                itemId = rng.WeightedPick(all, e => e.Weight ?? 1).ItemId;
            }
            var tier = rng.WeightedPick(Items.SigilTiers, t => Db.B.Lucky.SigilTiers[t]);
            var def = Db.Item(itemId);
            var grant = new ItemGrant { ItemId = itemId, Rolls = Items.RollGear(def, rng, null, tier), Sigil = tier };
            var mods = Items.ModSlots(def, tier);
            if (mods > 0) grant.Mods = Items.RollMods(def, mods, rng);
            return grant;
        }
    }

    /// <summary>Rework at the shop counter, a port of src/game/Rework.ts: pay gold to roll a line again, keep new or old.</summary>
    public static class Rework
    {
        /// <summary>The lines the counter can rework (Special chance and jewelry Power can't be).</summary>
        public static readonly string[] Lines = { "damage", "crit", "defense", "hp" };

        public static List<string> ReworkLines(Gear gear) => Items.RollKeys(Items.GearDef(gear), gear.Sigil).Where(k => Lines.Contains(k)).ToList();

        /// <summary>Gold for this piece's next rework: base x tier, x growth for every earlier rework.</summary>
        public static double Price(Gear gear) =>
            JsMath.Round(Db.B.Rework.Base * Items.GearDef(gear).Tier * Math.Pow(Db.B.Rework.Growth, gear.Reworks ?? 0));

        public static bool CanRework(Player player, Gear gear, string key) => ReworkLines(gear).Contains(key) && player.Gold >= Price(gear);

        /// <summary>Pay and roll the line again; the piece keeps its old value until `Keep`. Null if you can't.</summary>
        public static (double before, double after)? Run(Player player, Gear gear, string key, IRng rng)
        {
            if (!CanRework(player, gear, key)) return null;
            player.Data.Gold -= Price(gear);
            gear.Reworks = (gear.Reworks ?? 0) + 1;
            return (gear.Roll(key), Items.RollQuality(rng));
        }

        /// <summary>Set the value the player kept.</summary>
        public static void Keep(Gear gear, string key, double value) => gear.Rolls[key] = value;
    }
}
