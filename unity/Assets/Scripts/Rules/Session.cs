using System.Collections.Generic;

namespace Drakepg.Rules
{
    /// <summary>A monster roaming a room. Only in memory: rooms are remembered for the play session, not saved.</summary>
    public class RoamingMonster
    {
        public int Uid;
        public string MonsterId;
        /// <summary>Elite affix id, if it's an Elite.</summary>
        public string Elite;
        public double X, Y;
    }

    public class Chest
    {
        public double X, Y;
        public bool Mimic;
        /// <summary>A side room's sure chest: also holds this skill's book (RoomDef.Chest.Book) until you know or hold it.</summary>
        public string Book;
    }

    /// <summary>The room you left to fight, so you come back to the same spot with the same monsters.</summary>
    public class RoomSnapshot
    {
        public string RoomId;
        public List<RoamingMonster> Monsters = new List<RoamingMonster>();
        public int FightingUid;
    }

    /// <summary>A room as you left it: who was still alive, and when you walked out.</summary>
    public class RoomMemory
    {
        public List<RoamingMonster> Monsters = new List<RoamingMonster>();
        public Chest Chest;
        /// <summary>Seconds (game clock) when you left.</summary>
        public double LeftAt;
    }

    /// <summary>The character being played plus transient world state. A port of src/game/session.ts.</summary>
    public static class Session
    {
        static Player current;
        static RoomSnapshot snapshot;
        static readonly Dictionary<string, RoomMemory> rooms = new Dictionary<string, RoomMemory>();

        public static Player Player
        {
            get
            {
                current ??= Player.CreateNew();
                return current;
            }
        }

        public static bool HasPlayer => current != null;

        /// <summary>The next freshly filled monster room gets a Lucky Impling.</summary>
        public static bool LuckyNext;
        /// <summary>The room whose campfire you rested at on this visit.</summary>
        public static string RestedAt;
        /// <summary>Notes and signs read this play session.</summary>
        public static readonly HashSet<string> Inspected = new HashSet<string>();

        public static void Start(Player player, bool fresh = false)
        {
            current = player;
            snapshot = null;
            rooms.Clear();
            LuckyNext = false;
            RestedAt = null;
            Inspected.Clear();
        }

        public static void RememberRoom(string roomId, RoomMemory memory) => rooms[roomId] = memory;

        public static RoomMemory RoomMemory(string roomId) => rooms.TryGetValue(roomId, out var m) ? m : null;

        public static void ForgetRooms() => rooms.Clear();

        public static void SaveRoom(RoomSnapshot s) => snapshot = s;

        public static RoomSnapshot TakeRoom(string roomId)
        {
            var s = snapshot != null && snapshot.RoomId == roomId ? snapshot : null;
            snapshot = null;
            return s;
        }
    }
}
