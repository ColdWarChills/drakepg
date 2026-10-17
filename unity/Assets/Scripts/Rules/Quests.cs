using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>Story quests, a port of src/game/Quests.ts. Status: locked, rank, available, active, ready or done.</summary>
    public static class Quests
    {
        public static int Progress(Player player, QuestDef quest)
        {
            var o = quest.Objective;
            if (o.Kind == "collect") return System.Math.Min(o.Count, player.CountItem(o.ItemId));
            return player.Data.Quests.TryGetValue(quest.Id, out var s) ? s.Count : 0;
        }

        public static string Status(Player player, QuestDef quest)
        {
            player.Data.Quests.TryGetValue(quest.Id, out var state);
            if (state?.Status == "done") return "done";
            if (state?.Status == "active") return Progress(player, quest) >= quest.Objective.Count ? "ready" : "active";
            if (quest.Requires != null && !(player.Data.Quests.TryGetValue(quest.Requires, out var req) && req.Status == "done")) return "locked";
            if (quest.RequiresRank != null && !Guild.HasRank(player, quest.RequiresRank)) return "rank";
            return "available";
        }

        public static bool Started(Player player, string questId) => player.Data.Quests.ContainsKey(questId);

        public static bool NeedsKill(Player player, string monsterId) =>
            Db.Quests().Any(q => q.Objective.Kind == "kill" && q.Objective.MonsterId == monsterId && Status(player, q) == "active");

        public static bool QuestWantsItem(Player player, string itemId) =>
            Db.Quests().Any(q => q.Objective.Kind == "collect" && q.Objective.ItemId == itemId && Status(player, q) == "active");

        public static bool WantsElite(Player player) =>
            Db.Quests().Any(q => q.Objective.Kind == "kill" && q.Objective.Elite && q.Objective.MonsterId == null && Status(player, q) == "active");

        static bool ObjectiveMatches(QuestDef quest, MonsterDef monster)
        {
            var o = quest.Objective;
            if (o.Kind != "kill") return false;
            return (o.MonsterId == null || o.MonsterId == monster.Id) && (!o.Elite || monster.Elite != null);
        }

        public static bool Done(Player player, string questId) => player.Data.Quests.TryGetValue(questId, out var s) && s.Status == "done";

        /// <summary>The quest this NPC has something to say about: ready > available > active > rank.</summary>
        public static (QuestDef quest, string status)? ForNpc(Player player, string npcId)
        {
            var mine = Db.Quests().Where(q => q.Giver == npcId).Select(q => (q, Status(player, q))).ToList();
            foreach (var wanted in new[] { "ready", "available", "active", "rank" })
                foreach (var hit in mine) if (hit.Item2 == wanted) return hit;
            return null;
        }

        public static QuestDef WaitingOnRank(Player player) => Db.Quests().FirstOrDefault(q => Status(player, q) == "rank");

        public static List<(QuestDef quest, string status, int count)> Active(Player player) =>
            Db.Quests().Select(q => (q, Status(player, q), Progress(player, q))).Where(x => x.Item2 == "active" || x.Item2 == "ready").ToList();

        public static void Accept(Player player, QuestDef quest)
        {
            if (Status(player, quest) != "available") return;
            player.Data.Quests[quest.Id] = new QuestState { Status = "active", Count = 0 };
        }

        public class Update
        {
            public QuestDef Quest;
            public int Count, Target;
        }

        public static List<Update> RecordKill(Player player, MonsterDef monster)
        {
            var updates = new List<Update>();
            foreach (var quest in Db.Quests())
            {
                if (!player.Data.Quests.TryGetValue(quest.Id, out var state) || state.Status != "active" || !ObjectiveMatches(quest, monster)) continue;
                if (state.Count >= quest.Objective.Count) continue;
                state.Count += 1;
                updates.Add(new Update { Quest = quest, Count = state.Count, Target = quest.Objective.Count });
            }
            return updates;
        }

        public class TurnInResult
        {
            public Rewards Rewards;
            public int LevelsGained;
            public List<SkillDef> NewSkills;
        }

        public static TurnInResult TurnIn(Player player, QuestDef quest, IRng rng)
        {
            if (Status(player, quest) != "ready") return null;
            var o = quest.Objective;
            if (o.Kind == "collect" && !player.RemoveItem(o.ItemId, o.Count)) return null;
            player.Data.Quests[quest.Id] = new QuestState { Status = "done", Count = o.Count };
            string classItem = null;
            quest.Rewards.ClassItems?.TryGetValue(player.Data.ClassId, out classItem);
            var ids = new List<string>(quest.Rewards.Items ?? new List<string>());
            if (classItem != null) ids.Add(classItem);
            var rewards = new Rewards
            {
                Xp = quest.Rewards.Xp * Cheats.Rates,
                Gold = quest.Rewards.Gold * Cheats.Rates,
                Items = ids.Select(id => Items.GrantFor(id, rng)).ToList(),
            };
            var levelBefore = player.Level;
            var levels = player.GrantRewards(rewards, true).levels;
            return new TurnInResult { Rewards = rewards, LevelsGained = levels, NewSkills = player.SkillsLearnedSince(levelBefore) };
        }
    }

    /// <summary>Visited rooms, gate guards and the Recall Scroll, a port of src/game/Travel.ts.</summary>
    public static class Travel
    {
        public const string RecallScroll = "recall_scroll";

        public static bool VisitRoom(Player player, string roomId)
        {
            var first = !player.Data.Visited.Contains(roomId);
            if (first) player.Data.Visited.Add(roomId);
            if (Db.Room(roomId).Town != null) player.Data.LastTown = roomId;
            return first;
        }

        /// <summary>The boss standing in this room right now (the class's variant), or null.</summary>
        public static string RoomBoss(Player player, RoomDef room)
        {
            var b = room.Boss;
            if (b == null) return null;
            if (b.QuestId != null ? !Quests.Started(player, b.QuestId) : player.Data.GuardsBeaten.Contains(room.Id)) return null;
            return b.ByClass != null && b.ByClass.TryGetValue(player.Data.ClassId, out var id) ? id : b.MonsterId;
        }

        public static bool IsGuard(RoomDef room) => room.Boss != null && room.Boss.QuestId == null;

        public static bool ExitOpen(Player player, RoomDef room, RoomExit exit)
        {
            if (exit.RequiresQuest != null && !Quests.Started(player, exit.RequiresQuest)) return false;
            return !exit.Guarded || RoomBoss(player, room) == null;
        }

        public static void RecordGuard(Player player, string roomId, string monsterId)
        {
            var room = Db.Room(roomId);
            if (IsGuard(room) && RoomBoss(player, room) == monsterId) player.Data.GuardsBeaten.Add(roomId);
        }

        public static RoomDef AreaTown(string areaId) => Db.Data.Rooms.FirstOrDefault(r => r.Map.Area == areaId && r.Town != null);

        /// <summary>Null reason = ok, and `town` is where it goes.</summary>
        public static (RoomDef town, string reason) RecallCheck(Player player, string hereRoomId)
        {
            if (player.CountItem(RecallScroll) <= 0) return (null, "No Recall Scrolls left. Town shops sell them.");
            var id = player.Data.LastTown;
            if (id == null) return (null, "You have not reached a town yet.");
            var town = Db.Room(id);
            if (id == hereRoomId) return (null, $"You are already in {town.Name}.");
            return (town, null);
        }

        public static Location UseRecall(Player player, string hereRoomId)
        {
            var (town, reason) = RecallCheck(player, hereRoomId);
            if (reason != null || !player.RemoveItem(RecallScroll)) return null;
            return new Location { RoomId = town.Id, X = town.Town.X, Y = town.Town.Y };
        }
    }
}
