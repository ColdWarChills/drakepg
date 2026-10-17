using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    public class MapNode
    {
        public RoomDef Room;
        public int X, Y;
        public bool Visited, Here;
        /// <summary>town, boss, campfire.</summary>
        public List<string> Icons = new List<string>();
        /// <summary>"!", "?", "target" or null.</summary>
        public string Quest;
    }

    public class MapLink
    {
        public MapNode A, B;
        public bool Open;
    }

    public class MapStub
    {
        public MapNode From;
        public string Side, Label;
        public MapAreaDef Area;
        public bool Open;
    }

    public class AreaMap
    {
        public MapAreaDef Area;
        public List<MapNode> Nodes = new List<MapNode>();
        public List<MapLink> Links = new List<MapLink>();
        public List<MapStub> Stubs = new List<MapStub>();
        public int MinX, MaxX, MinY, MaxY;
    }

    /// <summary>What the map shows, a port of src/game/WorldMap.ts.</summary>
    public static class WorldMap
    {
        static bool ExitVisible(Player player, RoomExit exit)
        {
            if (exit.Side != "back" && exit.Side != "down") return true;
            return exit.RequiresQuest == null || Quests.Status(player, Db.Quest(exit.RequiresQuest)) != "locked";
        }

        static HashSet<string> ShownRooms(Player player, string here)
        {
            var visited = new HashSet<string>(player.Data.Visited) { here };
            var shown = new HashSet<string>(visited);
            foreach (var id in visited)
            {
                if (!Db.HasRoom(id)) continue;
                foreach (var exit in Db.Room(id).Exits) if (ExitVisible(player, exit)) shown.Add(exit.To);
            }
            return shown;
        }

        static string QuestMark(Player player, RoomDef room)
        {
            var npcs = new HashSet<string>(room.Npcs.Select(n => n.Id));
            if (room.Npcs.Any(n => n.Role.Kind == "guild") && Guild.AnyHuntReady(player)) return "?";
            string mark = null;
            foreach (var quest in Db.Quests())
            {
                var status = Quests.Status(player, quest);
                if (npcs.Contains(quest.Giver) && status == "ready") return "?";
                if (npcs.Contains(quest.Giver) && status == "available") mark = "!";
                if (mark != null || status != "active" || quest.Objective.Kind != "kill" || quest.Objective.MonsterId == null) continue;
                var id = quest.Objective.MonsterId;
                var here = room.Boss?.MonsterId == id || ((room.MonsterCount ?? 0) != 0 && room.ZoneId != null && Db.Zone(room.ZoneId).Encounters.Any(e => e.MonsterId == id));
                if (here) mark = "target";
            }
            if (mark != null || (room.MonsterCount ?? 0) == 0 || room.ZoneId == null) return mark;
            var (monsters, zones) = Guild.HuntTargets(player);
            var zone = Db.Zone(room.ZoneId);
            if (zones.Contains(zone.Id) || zone.Encounters.Any(e => monsters.Contains(e.MonsterId))) return "target";
            return mark;
        }

        public static List<MapAreaDef> KnownAreas(Player player, string here)
        {
            var visited = new HashSet<string>(player.Data.Visited) { here };
            return Db.Data.MapAreas.Where(a => Db.Data.Rooms.Any(r => r.Map.Area == a.Id && visited.Contains(r.Id))).ToList();
        }

        public static MapAreaDef AreaOf(string roomId)
        {
            var id = Db.Room(roomId).Map.Area;
            return Db.Data.MapAreas.First(a => a.Id == id);
        }

        public static AreaMap Build(Player player, string areaId, string here)
        {
            var area = Db.Data.MapAreas.First(a => a.Id == areaId);
            var rooms = Db.Data.Rooms.Where(r => r.Map.Area == areaId).ToList();
            var shown = ShownRooms(player, here);
            var visited = new HashSet<string>(player.Data.Visited) { here };
            var map = new AreaMap { Area = area };
            foreach (var room in rooms.Where(r => shown.Contains(r.Id)))
            {
                var known = visited.Contains(room.Id);
                var node = new MapNode { Room = room, X = room.Map.X, Y = room.Map.Y, Visited = known, Here = room.Id == here, Quest = QuestMark(player, room) };
                if (known && room.Town != null) node.Icons.Add("town");
                if (known && room.Boss != null) node.Icons.Add("boss");
                if (known && room.Campfire != null) node.Icons.Add("campfire");
                map.Nodes.Add(node);
            }
            var byId = map.Nodes.ToDictionary(n => n.Room.Id);
            var linked = new HashSet<string>();
            foreach (var node in map.Nodes)
            {
                if (!node.Visited) continue;
                foreach (var exit in node.Room.Exits)
                {
                    if (!ExitVisible(player, exit)) continue;
                    var open = Travel.ExitOpen(player, node.Room, exit);
                    var target = Db.Room(exit.To);
                    if (target.Map.Area != areaId)
                    {
                        map.Stubs.Add(new MapStub { From = node, Side = exit.Side, Label = visited.Contains(target.Id) ? target.Name : "?", Area = AreaOf(target.Id), Open = open });
                        continue;
                    }
                    if (!byId.TryGetValue(exit.To, out var other)) continue;
                    var key = string.CompareOrdinal(node.Room.Id, exit.To) < 0 ? node.Room.Id + "|" + exit.To : exit.To + "|" + node.Room.Id;
                    if (linked.Contains(key)) continue;
                    linked.Add(key);
                    map.Links.Add(new MapLink { A = node, B = other, Open = open });
                }
            }
            map.MinX = rooms.Min(r => r.Map.X);
            map.MaxX = rooms.Max(r => r.Map.X);
            map.MinY = rooms.Min(r => r.Map.Y);
            map.MaxY = rooms.Max(r => r.Map.Y);
            return map;
        }
    }
}
