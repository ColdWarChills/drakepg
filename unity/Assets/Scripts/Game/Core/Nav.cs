using System.Collections.Generic;
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
        /// <summary>Battle: victory, fled or escaped.</summary>
        public string Outcome;

        public static RoomArrival Spawn(string roomId, float x, float y, bool intro = false, bool recalled = false) =>
            new RoomArrival { RoomId = roomId, Via = "spawn", X = x, Y = y, Intro = intro, Recalled = recalled };

        public static RoomArrival Exit(string roomId, string side, float y, float? x = null) =>
            new RoomArrival { RoomId = roomId, Via = "exit", Side = side, Y = y, X = x ?? 0, HasX = x.HasValue };

        public static RoomArrival Battle(string roomId, string outcome, float x, float y) =>
            new RoomArrival { RoomId = roomId, Via = "battle", Outcome = outcome, X = x, Y = y };
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
        public List<QuestTick> Quests = new List<QuestTick>();
        public RoomArrival Back;
        /// <summary>Defeat: where you wake up.</summary>
        public Location Wake;
        /// <summary>Defeat against a boss: grit you now have (0-1).</summary>
        public double? Grit;
        /// <summary>A tower floor's outcome (null for other fights).</summary>
        public TowerOutcome Tower;
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
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame && (key == Key.Enter || key == Key.Escape || !Typing);
        }

        public static bool Held(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].isPressed && !Typing;
        }
    }
}
