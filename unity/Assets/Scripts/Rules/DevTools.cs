using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>The testing menu's actions and chapter starts, the rest of src/game/cheats.ts.</summary>
    public static class DevTools
    {
        static Balance B => Db.B;

        public static void AddLevels(Player player, int count)
        {
            for (int i = 0; i < count && player.Level < B.MaxLevel; i++) player.AddXp(Player.XpToNext(player.Level) - player.Xp);
        }

        public static void ResetStats(Player player)
        {
            var cls = player.ClassDef;
            var levels = player.Level - 1;
            foreach (var key in Stat.Keys) player.Data.BaseStats[key] = cls.Stats[key] + (cls.Growth.TryGetValue(key, out var g) ? g : 0) * levels;
            player.Data.StatPoints = B.StatPointsPerLevel * levels;
            player.ClampVitals();
        }

        /// <summary>A gear piece made to order. `quality` null = random like a drop.</summary>
        public static ItemGrant CheatGrant(string itemId, double? quality, string sigil, int mods, IRng rng)
        {
            var def = Db.Item(itemId);
            if (!def.IsGear) return new ItemGrant { ItemId = itemId };
            var grant = new ItemGrant { ItemId = itemId, Rolls = Items.RollGear(def, rng, quality, sigil) };
            if (sigil != null) grant.Sigil = sigil;
            if (mods > 0)
            {
                grant.Mods = Items.RollMods(def, Math.Min(mods, Items.ModSlots(def, sigil)), rng);
                if (quality.HasValue) foreach (var m in grant.Mods) m.Roll = quality.Value;
            }
            return grant;
        }

        public static void CompleteQuestChain(Player player, QuestDef quest)
        {
            var guild = player.Data.Guild;
            for (var q = quest; q != null; q = q.Requires != null ? Db.Quest(q.Requires) : null)
            {
                player.Data.Quests[q.Id] = new QuestState { Status = "done", Count = q.Objective.Count };
                if (q.RequiresRank != null) guild.Rank = Math.Max(guild.Rank, Guild.RankIndex(q.RequiresRank));
            }
        }

        public static int FinishActiveQuests(Player player)
        {
            int n = 0;
            foreach (var a in Guild.ActiveHunts(player))
            {
                if (a.Ready) continue;
                if (a.Hunt.Kind == "gather") player.AddItem(a.Hunt.ItemId, a.Hunt.Count - player.CountItem(a.Hunt.ItemId));
                else a.State.Count = a.Hunt.Count;
                n++;
            }
            foreach (var q in Db.Quests())
            {
                if (!player.Data.Quests.TryGetValue(q.Id, out var state) || state.Status != "active") continue;
                var o = q.Objective;
                if (o.Kind == "collect")
                {
                    var missing = o.Count - player.CountItem(o.ItemId);
                    if (missing > 0)
                    {
                        player.AddItem(o.ItemId, missing);
                        n++;
                    }
                }
                else if (state.Count < o.Count)
                {
                    state.Count = o.Count;
                    n++;
                }
            }
            return n;
        }

        static readonly Dictionary<string, string[]> StatPlan = new Dictionary<string, string[]>
        {
            ["warrior"] = new[] { "str", "str", "end" },
            ["mage"] = new[] { "int", "int", "end" },
            ["rogue"] = new[] { "dex", "dex", "str" },
        };

        public static void ApplyChapterStart(Player player, ChapterStart start)
        {
            var cls = player.Data.ClassId;
            AddLevels(player, start.Level - player.Level);
            var plan = StatPlan[cls];
            for (int i = 0; player.Data.StatPoints > 0; i++) player.SpendStatPoint(plan[i % plan.Length]);
            if (start.After != null) CompleteQuestChain(player, Db.Quest(start.After));
            var ids = new List<string>();
            if (start.Weapon != null && start.Weapon.TryGetValue(cls, out var w)) ids.Add(w);
            if (start.Armor != null && start.Armor.TryGetValue(cls, out var a)) ids.Add(a);
            if (start.Kit != null && start.Kit.TryGetValue(cls, out var kit)) ids.AddRange(kit);
            foreach (var id in ids)
            {
                var def = Db.Item(id);
                if (!def.IsGear) continue;
                var gear = player.MakeGear(id, new GearTraits { Rolls = Items.RollGear(def, null, 40) });
                player.Data.Gear.Add(gear);
                player.Equip(gear.Uid);
            }
            player.Data.Gear = new List<Gear>();
            foreach (var p in start.Potions ?? new List<object[]>()) player.AddItem(Convert.ToString(p[0]), Convert.ToInt32(p[1]));
            if (start.Gold.HasValue && start.Gold.Value != 0) player.Data.Gold = start.Gold.Value;
            player.RestoreFull();
        }
    }
}
