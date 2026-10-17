using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drakepg.Rules
{
    public class Hunt
    {
        public string Key;
        /// <summary>kill, gather or elite.</summary>
        public string Kind;
        public string MonsterId, ZoneId, ItemId;
        public int Count, Rank;
        public double Xp, Gold;
    }

    public class HuntState
    {
        public string Key;
        public int Count;
    }

    public class GuildState
    {
        public int Rank, Points, Done;
        public List<string> Board = new List<string>();
        public List<HuntState> Hunts = new List<HuntState>();
    }

    /// <summary>The Adventurers' Guild, a port of src/game/Guild.ts.</summary>
    public static class Guild
    {
        static GuildTuning G => Db.B.Guild;
        public static int MaxRank => G.Ranks.Count - 1;

        public static string RankId(int rank) => G.Ranks[Math.Max(0, Math.Min(MaxRank, rank))].Id;
        public static int RankIndex(string id) => G.Ranks.FindIndex(r => r.Id == id);
        public static int PointsToNext(int rank) => rank >= MaxRank ? 0 : G.Ranks[rank].ToNext;
        public static bool HasRank(Player player, string id) => player.Data.Guild.Rank >= RankIndex(id);

        public static int RankForLevel(double level)
        {
            int rank = 0;
            for (int i = 0; i < G.Ranks.Count; i++) if (level >= G.Ranks[i].MinLevel) rank = i;
            return rank;
        }

        static double Avg(double[] pair) => (pair[0] + pair[1]) / 2;

        static (double level, double xp, double gold) ZoneAverages(ZoneDef zone)
        {
            var total = zone.Encounters.Sum(e => e.Weight);
            if (total == 0) total = 1;
            double Sum(Func<MonsterDef, double> f) => zone.Encounters.Sum(e => f(Db.Monster(e.MonsterId)) * e.Weight) / total;
            return (Sum(m => m.Level), Sum(m => m.Xp), Sum(m => Avg(m.Gold)));
        }

        static readonly Dictionary<string, Hunt> cache = new Dictionary<string, Hunt>();
        public static void ClearCache() => cache.Clear();

        public static Hunt HuntDef(string key)
        {
            if (cache.TryGetValue(key, out var h)) return h;
            h = BuildHunt(key);
            cache[key] = h;
            return h;
        }

        static Hunt BuildHunt(string key)
        {
            var parts = key.Split(':');
            if (parts.Length < 2) return null;
            var kind = parts[0];
            var id = parts[1];
            if (kind == "gather")
            {
                var zone = Db.Data.Zones.FirstOrDefault(z => z.Id == id);
                if (zone?.Gather == null || !Db.HasItem(zone.Gather) || zone.Encounters.Count == 0) return null;
                var a = ZoneAverages(zone);
                var gk = (double)G.Gather / G.GatherChance;
                return new Hunt
                {
                    Key = key, Kind = kind, ZoneId = zone.Id, ItemId = zone.Gather, Count = G.Gather,
                    Rank = RankForLevel(JsMath.Round(a.level)), Xp = JsMath.Round(gk * a.xp * G.Xp), Gold = JsMath.Round(gk * a.gold * G.Gold),
                };
            }
            if ((kind != "kill" && kind != "elite") || !Db.HasMonster(id)) return null;
            var m = Db.Monster(id);
            if (m.Boss) return null;
            var b = RankForLevel(m.Level) + (m.Tough ? 1 : 0);
            double kills = kind == "elite" ? G.EliteKills : m.Tough ? G.ToughKills : G.Kills;
            return new Hunt
            {
                Key = key, Kind = kind, MonsterId = id,
                Count = kind == "elite" ? 1 : m.Tough ? G.ToughKills : G.Kills,
                Rank = Math.Min(MaxRank, b + (kind == "elite" ? 1 : 0)),
                Xp = JsMath.Round(kills * m.Xp * G.Xp), Gold = JsMath.Round(kills * Avg(m.Gold) * G.Gold),
            };
        }

        public static string Plural(string name, int n)
        {
            if (n == 1) return name;
            if (Regex.IsMatch(name, "f$")) return Regex.Replace(name, "f$", "ves");
            if (Regex.IsMatch(name, "[^aeiou]y$")) return Regex.Replace(name, "y$", "ies");
            if (Regex.IsMatch(name, "(s|x|ch|sh)$")) return name + "es";
            return name + "s";
        }

        public static string Title(Hunt h)
        {
            if (h.Kind == "gather") return $"Gather {h.Count} {Plural(Db.Item(h.ItemId).Name, h.Count)}";
            var name = Db.Monster(h.MonsterId).Name;
            return h.Kind == "elite" ? $"Elite bounty: {name}" : $"Hunt {h.Count} {Plural(name, h.Count)}";
        }

        public static string Goal(Hunt h)
        {
            if (h.Kind == "gather") return Plural(Db.Item(h.ItemId).Name, h.Count);
            var name = Db.Monster(h.MonsterId).Name;
            return h.Kind == "elite" ? "Elite " + name : Plural(name, h.Count);
        }

        public static string Where(Player player, Hunt h)
        {
            if (h.Kind == "gather") return $"Any monster in {Db.Zone(h.ZoneId).Name}: {JsMath.Round(Cheats.Boosted(G.GatherChance) * 100)}% a kill";
            var zones = ReachableZones(player).Where(z => Db.Zone(z).Encounters.Any(e => e.MonsterId == h.MonsterId));
            var where = string.Join(", ", zones.Select(z => Db.Zone(z).Name));
            return h.Kind == "elite" ? "A gold-glowing one, in " + where : where;
        }

        public static List<string> ReachableZones(Player player)
        {
            var visited = new HashSet<string>(player.Data.Visited);
            var rooms = new List<string>();
            var roomSet = new HashSet<string>();
            var queue = new Queue<string>(Db.Data.Rooms.Where(r => r.Town != null && visited.Contains(r.Id)).Select(r => r.Id));
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (roomSet.Contains(id)) continue;
                roomSet.Add(id);
                rooms.Add(id);
                if (!visited.Contains(id)) continue;
                foreach (var e in Db.Room(id).Exits)
                    if (!roomSet.Contains(e.To) && (e.RequiresQuest == null || player.Data.Quests.ContainsKey(e.RequiresQuest))) queue.Enqueue(e.To);
            }
            var zones = new List<string>();
            foreach (var id in rooms)
            {
                var r = Db.Room(id);
                if (r.ZoneId != null && (r.MonsterCount ?? 0) != 0 && !zones.Contains(r.ZoneId)) zones.Add(r.ZoneId);
            }
            return zones;
        }

        public static List<Hunt> HuntPool(Player player)
        {
            var zones = ReachableZones(player);
            var monsters = new List<string>();
            foreach (var z in zones) foreach (var e in Db.Zone(z).Encounters) if (!monsters.Contains(e.MonsterId)) monsters.Add(e.MonsterId);
            var keys = new List<string>();
            keys.AddRange(monsters.Select(m => "kill:" + m));
            if (Features.On(player, "elites")) keys.AddRange(monsters.Select(m => "elite:" + m));
            keys.AddRange(zones.Select(z => "gather:" + z));
            return keys.Select(HuntDef).Where(h => h != null).ToList();
        }

        public static int MaxHuntRank(Player player) => Math.Min(MaxRank, player.Data.Guild.Rank + G.Reach);

        public static int HuntPoints(Player player, Hunt h)
        {
            var rank = player.Data.Guild.Rank;
            return rank >= MaxRank ? 0 : Math.Max(0, h.Rank - rank + 1);
        }

        public static void RefreshBoard(Player player, IRng rng)
        {
            var g = player.Data.Guild;
            var taken = new HashSet<string>(g.Hunts.Select(h => h.Key));
            var pool = HuntPool(player).Where(h => !taken.Contains(h.Key) && h.Rank <= MaxHuntRank(player)).ToList();
            bool Fresh(Hunt h) => h.Rank >= g.Rank;
            var byKey = pool.GroupBy(h => h.Key).ToDictionary(x => x.Key, x => x.Last());
            List<Hunt> Spare() => pool.Where(h => !g.Board.Contains(h.Key)).ToList();
            g.Board = g.Board.Where(byKey.ContainsKey).ToList();
            foreach (var k in g.Board.ToList())
            {
                if (Fresh(byKey[k]) || !Spare().Any(h => Fresh(h) && h.Kind != "elite")) continue;
                g.Board = g.Board.Where(b => b != k).ToList();
            }
            Hunt Pick(List<Hunt> list) => list[rng.RandInt(0, list.Count - 1)];
            while (g.Board.Count < G.BoardSize)
            {
                var left = Spare();
                var elites = left.Where(h => Fresh(h) && h.Kind == "elite").ToList();
                var normal = left.Where(h => Fresh(h) && h.Kind != "elite").ToList();
                var low = left.Where(h => !Fresh(h) && h.Kind != "elite").ToList();
                Hunt next = null;
                if (elites.Count > 0 && (normal.Count == 0 || rng.Chance(G.EliteChance))) next = Pick(elites);
                else if (normal.Count > 0) next = Pick(normal);
                else if (low.Count > 0)
                {
                    var best = low.Max(h => h.Rank);
                    next = Pick(low.Where(h => h.Rank == best).ToList());
                }
                if (next == null) break;
                g.Board.Add(next.Key);
            }
        }

        public static void RerollBoard(Player player, IRng rng)
        {
            player.Data.Guild.Board = new List<string>();
            RefreshBoard(player, rng);
        }

        public static bool CanTakeHunt(Player player) => player.Data.Guild.Hunts.Count < G.MaxActive;

        public static bool TakeHunt(Player player, string key)
        {
            var g = player.Data.Guild;
            var h = HuntDef(key);
            if (h == null || !CanTakeHunt(player) || g.Hunts.Any(s => s.Key == key) || h.Rank > MaxHuntRank(player)) return false;
            g.Board = g.Board.Where(k => k != key).ToList();
            g.Hunts.Add(new HuntState { Key = key, Count = 0 });
            return true;
        }

        public static void DropHunt(Player player, string key) => player.Data.Guild.Hunts = player.Data.Guild.Hunts.Where(s => s.Key != key).ToList();

        public static int HuntProgress(Player player, HuntState state)
        {
            var h = HuntDef(state.Key);
            if (h == null) return 0;
            if (h.Kind == "gather") return Math.Min(h.Count, player.CountItem(h.ItemId));
            return Math.Min(h.Count, state.Count);
        }

        public static bool HuntReady(Player player, HuntState state)
        {
            var h = HuntDef(state.Key);
            return h != null && HuntProgress(player, state) >= h.Count;
        }

        public class ActiveHunt
        {
            public HuntState State;
            public Hunt Hunt;
            public int Count;
            public bool Ready;
        }

        public static List<ActiveHunt> ActiveHunts(Player player)
        {
            var output = new List<ActiveHunt>();
            foreach (var state in player.Data.Guild.Hunts)
            {
                var hunt = HuntDef(state.Key);
                if (hunt != null) output.Add(new ActiveHunt { State = state, Hunt = hunt, Count = HuntProgress(player, state), Ready = HuntReady(player, state) });
            }
            return output;
        }

        public static bool AnyHuntReady(Player player) => ActiveHunts(player).Any(a => a.Ready);

        public static bool HuntWantsElite(Player player) => ActiveHunts(player).Any(a => a.Hunt.Kind == "elite" && !a.Ready);

        public static (HashSet<string> monsters, HashSet<string> zones) HuntTargets(Player player)
        {
            var monsters = new HashSet<string>();
            var zones = new HashSet<string>();
            foreach (var a in ActiveHunts(player))
            {
                if (a.Ready) continue;
                if (a.Hunt.MonsterId != null) monsters.Add(a.Hunt.MonsterId);
                if (a.Hunt.ZoneId != null) zones.Add(a.Hunt.ZoneId);
            }
            return (monsters, zones);
        }

        public class HuntUpdate
        {
            public Hunt Hunt;
            public int Count, Target;
        }

        public static List<HuntUpdate> RecordHuntKill(Player player, MonsterDef monster)
        {
            var updates = new List<HuntUpdate>();
            foreach (var state in player.Data.Guild.Hunts)
            {
                var h = HuntDef(state.Key);
                if (h == null || h.Kind == "gather" || h.MonsterId != monster.Id || state.Count >= h.Count) continue;
                if (h.Kind == "elite" && monster.Elite == null) continue;
                state.Count += 1;
                updates.Add(new HuntUpdate { Hunt = h, Count = state.Count, Target = h.Count });
            }
            return updates;
        }

        public static List<(Hunt hunt, double chance)> GatherHere(Player player, string zoneId) =>
            ActiveHunts(player).Where(a => a.Hunt.Kind == "gather" && a.Hunt.ZoneId == zoneId && !a.Ready)
                .Select(a => (a.Hunt, Cheats.Boosted(G.GatherChance))).ToList();

        /// <summary>Every chance is rolled first, then the items granted (like the TS filter/map chain).</summary>
        public static List<ItemGrant> HuntDrops(Player player, string zoneId, IRng rng)
        {
            var hits = GatherHere(player, zoneId).Where(g => rng.Chance(g.chance)).ToList();
            return hits.Select(g => Items.GrantFor(g.hunt.ItemId, rng)).ToList();
        }

        public class TurnInHunt
        {
            public Hunt Hunt;
            public Rewards Rewards;
            public int Points;
            public List<string> Promotions;
            public int LevelsGained;
        }

        public static TurnInHunt TurnIn(Player player, string key, IRng rng)
        {
            var g = player.Data.Guild;
            var state = g.Hunts.FirstOrDefault(s => s.Key == key);
            var hunt = HuntDef(key);
            if (state == null || hunt == null || !HuntReady(player, state)) return null;
            if (hunt.Kind == "gather" && !player.RemoveItem(hunt.ItemId, hunt.Count)) return null;
            var points = HuntPoints(player, hunt);
            g.Hunts = g.Hunts.Where(s => s.Key != key).ToList();
            g.Done += 1;
            g.Points += points;
            var rewards = new Rewards { Xp = hunt.Xp * Cheats.Rates, Gold = hunt.Gold * Cheats.Rates };
            var promotions = new List<string>();
            while (g.Rank < MaxRank && g.Points >= PointsToNext(g.Rank))
            {
                g.Points -= PointsToNext(g.Rank);
                g.Rank += 1;
                var id = RankId(g.Rank);
                promotions.Add(id);
                if (Db.Data.Promotions == null || !Db.Data.Promotions.TryGetValue(id, out var p) || p == null) continue;
                rewards.Gold += p.Gold;
                string classItem = null;
                p.ClassItems?.TryGetValue(player.Data.ClassId, out classItem);
                var ids = new List<string>(p.Items ?? new List<string>());
                if (classItem != null) ids.Add(classItem);
                foreach (var itemId in ids) rewards.Items.Add(Items.GrantFor(itemId, rng));
            }
            if (g.Rank >= MaxRank) g.Points = 0;
            var levels = player.GrantRewards(rewards, true).levels;
            RerollBoard(player, rng);
            return new TurnInHunt { Hunt = hunt, Rewards = rewards, Points = points, Promotions = promotions, LevelsGained = levels };
        }

        /// <summary>Save cleanup: a valid rank, and only hunts that still exist.</summary>
        public static GuildState Sanitize(GuildState raw)
        {
            var g = new GuildState();
            if (raw == null) return g;
            int Int(double v, int max) => double.IsNaN(v) ? 0 : (int)Math.Max(0, Math.Min(max, Math.Floor(v)));
            g.Rank = Int(raw.Rank, MaxRank);
            g.Points = Int(raw.Points, Math.Max(0, PointsToNext(g.Rank) - 1));
            g.Done = Int(raw.Done, 1000000000);
            foreach (var s in raw.Hunts ?? new List<HuntState>())
            {
                if (s?.Key == null || HuntDef(s.Key) == null || g.Hunts.Any(h => h.Key == s.Key)) continue;
                g.Hunts.Add(new HuntState { Key = s.Key, Count = Int(s.Count, HuntDef(s.Key).Count) });
            }
            g.Hunts = g.Hunts.Take(G.MaxActive).ToList();
            g.Board = (raw.Board ?? new List<string>()).Where(k => k != null && HuntDef(k) != null && !g.Hunts.Any(h => h.Key == k)).Distinct().Take(G.BoardSize).ToList();
            return g;
        }
    }
}
