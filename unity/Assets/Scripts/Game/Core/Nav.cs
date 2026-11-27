using System.Collections.Generic;
using System.Linq;
using Drakepg.Rules;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// How the player arrives in a room (RoomSceneData in src/scenes/keys.ts):
    /// spawn at an exact spot (new game, loaded save, waking at the inn), through an exit (enters from the opposite
    /// side at height `Y`), or back from a battle (restores the room as it was).
    /// </summary>
    public class RoomArrival
    {
        public string RoomId;
        /// <summary>spawn, exit or battle.</summary>
        public string Via;
        public float X, Y;
        /// <summary>Exit: the side you appear on (left, right, back, down). Back/down exits use `X`.</summary>
        public string Side;
        public bool HasX = true;
        /// <summary>Spawn: a new hero's first arrival (the elder greets you).</summary>
        public bool Intro;
        /// <summary>Spawn: arriving by Recall Scroll (a shimmer).</summary>
        public bool Recalled;
        /// <summary>Battle: victory, fled, escaped, or retry (Try again after losing to a monster on the map).</summary>
        public string Outcome;

        public static RoomArrival Spawn(string roomId, float x, float y, bool intro = false, bool recalled = false) =>
            new RoomArrival { RoomId = roomId, Via = "spawn", X = x, Y = y, Intro = intro, Recalled = recalled };

        public static RoomArrival Exit(string roomId, string side, float y, float? x = null) =>
            new RoomArrival { RoomId = roomId, Via = "exit", Side = side, Y = y, X = x ?? 0, HasX = x.HasValue };

        public static RoomArrival Battle(string roomId, string outcome, float x, float y) =>
            new RoomArrival { RoomId = roomId, Via = "battle", Outcome = outcome, X = x, Y = y };

        /// <summary>
        /// The door of a room's boss: arriving through its exit from the room before (the quiet one with a campfire, else
        /// the first exit that isn't guarded), at height `y`. Null if the room has no boss or no way in.
        /// </summary>
        public static RoomArrival BossDoor(string roomId, float y)
        {
            var room = Db.Room(roomId);
            if (room.Boss == null) return null;
            var exit = room.Exits.FirstOrDefault(e => !e.Guarded && Db.Room(e.To).Campfire != null) ?? room.Exits.FirstOrDefault(e => !e.Guarded);
            return exit == null ? null : Exit(roomId, exit.Side, y, exit.X is double x ? (float)x : (float?)null);
        }
    }

    /// <summary>What a battle is against and where it goes back to (BattleSceneData).</summary>
    public class BattleSetup
    {
        public string ZoneId, MonsterId;
        /// <summary>Elite affix id: fight the Elite version.</summary>
        public string Elite;
        /// <summary>A mimic sized to the zone.</summary>
        public bool Mimic;
        /// <summary>Heroic rematch of a boss.</summary>
        public bool Heroic;
        /// <summary>A monster walking on the room's map (not a mimic, a Lucky Impling or a boss): a loss offers Try again where it began.</summary>
        public bool OnMap;
        public string RoomId;
        public float X, Y;
        /// <summary>A tower floor (0 = none): its monster, twists and pay come from Tower.cs.</summary>
        public int TowerFloor;
        /// <summary>Tower fights start at full HP and MP; these come back afterwards.</summary>
        public double EntryHp, EntryMp;
    }

    /// <summary>A quest or hunt counter a kill advanced.</summary>
    public class QuestTick
    {
        public string Name, Goal, TurnIn;
        public int Count, Target;
        /// <summary>The story quest this ticked (null for a Guild hunt).</summary>
        public QuestDef Quest;
    }

    /// <summary>The result screen's input (ResultSceneData).</summary>
    public class BattleResult
    {
        /// <summary>victory or defeat.</summary>
        public string Outcome;
        public string MonsterName;
        public Rewards Rewards;
        /// <summary>Loot that didn't fit in the bag.</summary>
        public List<ItemGrant> Lost = new List<ItemGrant>();
        public int LevelsGained;
        public List<string> NewSkills = new List<string>();
        /// <summary>Victory: the hero as the fight began paying out (the XP bar fills from here; a level-up shows before -> after).</summary>
        public HeroBefore Before;
        public List<QuestTick> Quests = new List<QuestTick>();
        public RoomArrival Back;
        /// <summary>Defeat: where you wake up.</summary>
        public Location Wake;
        /// <summary>Defeat against a boss: grit you now have (0-1).</summary>
        public double? Grit;
        /// <summary>Defeat: how the fight went (the biggest hit taken, the hits it resisted).</summary>
        public FightRecap Recap;
        /// <summary>Defeat: the monster as the fight ended (its weak spots and resists, after any change of form).</summary>
        public MonsterDef Monster;
        /// <summary>
        /// Defeat: where Try again puts you, fully rested (null: no Try again). A room's boss: its door (Via "exit"). A
        /// monster on the map: where the fight began, the monster still there (Via "battle", Outcome "retry").
        /// </summary>
        public RoomArrival Door;
        /// <summary>A tower floor's outcome (null for other fights).</summary>
        public TowerOutcome Tower;
        /// <summary>Victory over a boss for the first time: its own card (null for every other win).</summary>
        public FirstKill FirstKill;
    }

    /// <summary>A story boss or gate guard beaten for the first time, and what that opened.</summary>
    public class FirstKill
    {
        public MonsterDef Monster;
        public int Turns;
        /// <summary>A story boss: its quest, who to see and where (null for a gate guard).</summary>
        public QuestDef Quest;
        public string Giver, GiverRoom;
        /// <summary>What waits at the giver: the next story quest, the features turning it in unlocks.</summary>
        public List<string> Opens = new List<string>();
        /// <summary>A gate guard: the room its gate opens onto.</summary>
        public string Road;

        /// <summary>The card for this win, or null: a quest boss whose quest this kill finished, or a guard beaten just now.</summary>
        public static FirstKill Of(MonsterDef monster, int turns, List<Quests.Update> kills, bool guardBeaten, string roomId)
        {
            if (!monster.Boss || monster.IsHeroic || monster.Elite != null) return null;
            var quest = kills.FirstOrDefault(k => k.Count >= k.Target && k.Quest.Objective.MonsterId == monster.Id)?.Quest;
            if (quest != null)
                return new FirstKill
                {
                    Monster = monster, Turns = turns, Quest = quest, Giver = Quests.GiverName(quest), GiverRoom = Quests.GiverRoom(quest)?.Name,
                    Opens = Quests.Opens(quest),
                };
            if (!guardBeaten) return null;
            var gate = Db.Room(roomId).Exits.FirstOrDefault(e => e.Guarded);
            return new FirstKill { Monster = monster, Turns = turns, Road = gate != null ? Db.Room(gate.To).Name : null };
        }
    }

    /// <summary>The hero's numbers before a fight's rewards landed.</summary>
    public class HeroBefore
    {
        public int Level;
        public double Xp, XpNeeded, MaxHp, MaxMp;
        public Dictionary<string, double> Stats = new Dictionary<string, double>();

        public static HeroBefore Of(Player p)
        {
            var b = new HeroBefore { Level = p.Level, Xp = p.Xp, XpNeeded = p.XpNeeded, MaxHp = p.MaxHp, MaxMp = p.MaxMp };
            var s = p.Stats;
            foreach (var k in Stat.Keys) b.Stats[k] = s[k];
            return b;
        }
    }

    public class TowerOutcome
    {
        public int Floor;
        /// <summary>A first clear's pay; null for a loss or a practice run.</summary>
        public FloorClear Clear;
        /// <summary>The floor to climb next, if open.</summary>
        public int? Next;
        public List<string> Twists = new List<string>();
    }

    /// <summary>The game's random numbers (defaultRng in the web version): a fresh seed per play session.</summary>
    public static class Dice
    {
        public static readonly IRng Rng = new SystemRng();
    }

    /// <summary>One-shot key presses (keydown-X) and held keys, ignored while typing in a text box.</summary>
    public static class Keys
    {
        public static bool Typing
        {
            get
            {
                if (NameBox.Focused != null) return true;
                var focused = GameRoot.I?.DesignRoot?.panel?.focusController?.focusedElement as VisualElement;
                return focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            }
        }

        public static bool Pressed(Key key)
        {
            if (Quiet) return false;
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame && (key == Key.Enter || key == Key.Escape || !Typing);
        }

        public static bool Held(Key key)
        {
            if (Quiet) return false;
            var kb = Keyboard.current;
            return kb != null && kb[key].isPressed && !Typing;
        }

        // --- Catching a key for Options (Controls) ---

        static System.Action<Key> catching;
        static int caughtFrame = -1;

        /// <summary>While a key is being caught, and on the frame it was caught, no screen sees any key.</summary>
        static bool Quiet => catching != null || caughtFrame == UnityEngine.Time.frameCount;

        public static bool Catching => catching != null;

        /// <summary>The next key pressed goes to `got` (Esc too: the caller treats it as cancel) and nowhere else.</summary>
        public static void Catch(System.Action<Key> got) => catching = got;

        public static void StopCatching() => catching = null;

        /// <summary>Run by GameRoot every frame before the screen's Tick, so the caught key can't also do its old job.</summary>
        public static void CatchStep()
        {
            if (catching == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            foreach (var k in kb.allKeys)
            {
                if (k == null || !k.wasPressedThisFrame) continue;
                var got = catching;
                catching = null;
                caughtFrame = UnityEngine.Time.frameCount;
                got(k.keyCode);
                return;
            }
        }
    }
}
