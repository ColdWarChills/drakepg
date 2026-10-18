using System.Collections.Generic;
using Newtonsoft.Json;

// Content types, a port of src/data/types.ts. The JSON comes from `npm run export-data`; field names match the
// TypeScript ones (Newtonsoft matches them case-insensitively). TS unions are one class with every field optional.
// Ids, elements, damage types and slots stay strings, like in the TypeScript.
namespace Drakepg.Rules
{
    public static class Stat
    {
        public const string Str = "str", Int = "int", Dex = "dex", End = "end";
        public static readonly string[] Keys = { Str, Int, Dex, End };
    }

    /// <summary>STR, INT, DEX, END.</summary>
    public class Stats
    {
        public double Str, Int, Dex, End;

        public double this[string key]
        {
            get => key switch { Stat.Str => Str, Stat.Int => Int, Stat.Dex => Dex, _ => End };
            set
            {
                switch (key)
                {
                    case Stat.Str: Str = value; break;
                    case Stat.Int: Int = value; break;
                    case Stat.Dex: Dex = value; break;
                    default: End = value; break;
                }
            }
        }

        public Stats Clone() => (Stats)MemberwiseClone();
    }

    public class Apply
    {
        public string Status;
        public int Turns;
        public double? Chance;
        public bool Self;
    }

    public class DotDef
    {
        public double Power;
        /// <summary>An element, or "weapon" for the applier's weapon element.</summary>
        public string Element;
    }

    public class StatusDef
    {
        public string Id, Name, Description;
        public bool Harmful;
        public DotDef Dot;
        public double? Regen;
        public bool Stun;
        public double? DamageDealt, DamageTaken, HitChance, CritChance, Evasion;
        public Dictionary<string, double> Resists;
        public long? Tint;

        /// <summary>The battle modifier named like the TS key ('damageDealt', ...); 0 when missing.</summary>
        public double Mod(string key) => key switch
        {
            "damageDealt" => DamageDealt ?? 0,
            "damageTaken" => DamageTaken ?? 0,
            "hitChance" => HitChance ?? 0,
            "critChance" => CritChance ?? 0,
            _ => Evasion ?? 0,
        };
    }

    public class Hand
    {
        public double X, Y, Angle;
        /// <summary>The fingers that wrap the handle, drawn over it; null: a circle of the fist covers the grip.</summary>
        public FingersArt Fingers;
    }

    /// <summary>A sprite of the fingers around a grip, its top-left at (X, Y) on the bare art in 1x px.</summary>
    public class FingersArt
    {
        public string Art;
        public double X, Y;
    }

    public class BareLook
    {
        public string Art;
        public List<Hand> Hands = new List<Hand>();
        /// <summary>Key poses for the big skills: pose name -> the weapon hands on `{Art}_{pose}` (drawn at Art's scale, feet on its baseline).</summary>
        public Dictionary<string, List<Hand>> Poses;
    }

    public class Look
    {
        public string Shape;
        public long Color, Accent;
        public double Scale = 1;
        [JsonConverter(typeof(OneOrManyConverter<string>))] public List<string> Art;
        [JsonConverter(typeof(OneOrManyConverter<BareLook>))] public List<BareLook> Bare;
        /// <summary>What spills when it's hit (null: red liquid blood).</summary>
        public Blood Blood;

        public Look Clone()
        {
            var c = (Look)MemberwiseClone();
            if (Art != null) c.Art = new List<string>(Art);
            if (Bare != null) c.Bare = new List<BareLook>(Bare);
            return c;
        }
    }

    /// <summary>A monster's blood: "liquid" (drops, splats, pools), "chips" (stone, bark, shell, wood) or "wisps" (a spirit's essence).</summary>
    public class Blood
    {
        public string Kind = "liquid";
        public long Color;
    }

    public class SkillDef
    {
        public string Id, Name, Description;
        public double MpCost;
        public int Cooldown;
        /// <summary>damage, heal or buff.</summary>
        public string Kind;
        public string DamageType, Element;
        public double Power;
        public int? Hits;
        public double? CritBonus, Lifesteal;
        public bool Blunt;
        public List<Apply> Applies;
        public string Fx;
        /// <summary>A top skill: the hero takes drawn key poses for its charge-up and blow (BareLook.Poses).</summary>
        public bool Epic;
    }

    public class ClassSkill
    {
        public string SkillId;
        public int Level;
    }

    public class ClassDef
    {
        public string Id, Name, Tagline;
        public Stats Stats;
        public Dictionary<string, double> Growth = new Dictionary<string, double>();
        public string Weapon, Armor;
        public List<ClassSkill> Skills = new List<ClassSkill>();
        public double? MpOnHit;
        public Look Look;
    }

    public class ProcDef
    {
        public string Name;
        public double Chance, Power;
        public int? Hits;
        public string Element;
        public List<Apply> Applies;
        public double? Heal;
    }

    public class Restore
    {
        public double? Hp, Mp;
    }

    /// <summary>
    /// Every item kind in one class (weapon, armor, accessory, consumable, material, stone, scroll); `Type` says which.
    /// </summary>
    public class ItemDef
    {
        public string Id, Name, Description, Type;
        public double Price;
        public long? IconColor;
        public int Tier;
        // weapon
        public double[] Damage;
        public string DamageType, Element;
        public Dictionary<string, double> Stats;
        public ProcDef Proc;
        public string Art;
        public double? Grip, Size;
        /// <summary>sword, axe, hammer, dagger, staff or wand: how it's swung in battle.</summary>
        public string Kind;
        // armor (slot: body/helm/gloves/boots) and accessory (slot: ring/necklace)
        public string Slot;
        public Dictionary<string, double> Defense;
        public Dictionary<string, double> Resists;
        public string Set;
        public Dictionary<string, List<Hand>> Hands;
        // accessory
        public double? Hp, Mp, Crit;
        // consumable
        public Restore Restore;
        // stone
        public string Line;
        // scroll
        public string Effect;

        public bool IsWeapon => Type == "weapon";
        public bool IsArmor => Type == "armor";
        public bool IsAccessory => Type == "accessory";
        public bool IsGear => IsWeapon || IsArmor || IsAccessory;
        public double StatOf(string key) => Stats != null && Stats.TryGetValue(key, out var v) ? v : 0;
    }

    public class SetBonus
    {
        public int Pieces;
        public Dictionary<string, double> Stats;
        public double? HpPct, Crit, DamagePct, DefensePct;
        public Dictionary<string, double> Resists;
    }

    public class SetDef
    {
        public string Id, Name;
        public List<string> Pieces = new List<string>();
        public List<SetBonus> Bonuses = new List<SetBonus>();
    }

    public class ItemQty
    {
        public string ItemId;
        public int Qty;
    }

    public class RecipeDef
    {
        public string Id, Result;
        public List<ItemQty> Materials = new List<ItemQty>();
        public double Gold;
    }

    public class MonsterAttack
    {
        public string Name, DamageType, Element;
        public double Power, Weight;
        public string Windup;
        public List<Apply> Applies;
        public double? Heal;
        public bool Opener;
        public int? Every;
        public double? HpBelow;

        public MonsterAttack Clone() => (MonsterAttack)MemberwiseClone();
    }

    public class Drop
    {
        public string ItemId;
        public double Chance;
        public int? Qty;
    }

    public class WeakTo
    {
        public double? Blunt, Poison;
    }

    public class EliteInfo
    {
        public string Affix;
    }

    public class MonsterPhase
    {
        public double At;
        public string Text, Element;
        public Dictionary<string, double> Resists;
        public List<MonsterAttack> Attacks;
    }

    public class MonsterShell
    {
        public string Name, Element;
        public int Hits;
        public double Reduction;
        public double? ReformAt;
    }

    public class HeroicDef
    {
        public string Text;
        public List<MonsterAttack> Attacks = new List<MonsterAttack>();
    }

    public class MonsterDef
    {
        public string Id, Name;
        public int Level;
        public Look Look;
        public double Hp;
        public Stats Stats;
        public double[] Damage;
        public string Element;
        public Dictionary<string, double> Defense = new Dictionary<string, double>();
        public Dictionary<string, double> Resists;
        public WeakTo WeakTo;
        public List<string> Immune;
        public List<MonsterAttack> Attacks = new List<MonsterAttack>();
        public double Xp;
        public double[] Gold;
        public List<Drop> Drops = new List<Drop>();
        public string Loot;
        public bool Boss, Tough, ChestLoot, Lucky;
        public int? EscapeAfter;
        public EliteInfo Elite;
        public MonsterPhase Phase;
        public MonsterShell Shell;
        public string Trait;
        public HeroicDef Heroic;
        public bool IsHeroic;

        /// <summary>A shallow copy, like `{ ...def }`.</summary>
        public MonsterDef Clone() => (MonsterDef)MemberwiseClone();

        public double DefenseOf(string type) => Defense != null && Defense.TryGetValue(type, out var v) ? v : 0;
    }

    public class LootEntry
    {
        public string ItemId;
        public double? Weight;
    }

    public class LootTableDef
    {
        public string Id;
        public Dictionary<string, List<LootEntry>> Tiers = new Dictionary<string, List<LootEntry>>();
        public string Set;
    }

    public class EliteAffixDef
    {
        public string Id, Name, Description;
        public double? Hp, Damage, Defense;
        public Dictionary<string, double> Resists;
        public List<Apply> Applies;
    }

    public class Backdrop
    {
        public string Kind;
        public double? Shade;
    }

    public class Encounter
    {
        public string MonsterId;
        public double Weight;
    }

    public class ZoneDef
    {
        public string Id, Name;
        public int[] RecommendedLevel;
        [JsonConverter(typeof(OneOrManyConverter<string>))] public List<string> Background;
        public Backdrop Backdrop;
        public List<Encounter> Encounters = new List<Encounter>();
        public string Gather;
    }

    public class NpcRole
    {
        /// <summary>talk, shop, inn, forge or guild.</summary>
        public string Kind;
        public List<string> Stock;
        public List<string> Recipes;
    }

    public class NpcDef
    {
        public string Id, Name, Title;
        public double X, Y;
        public Look Look;
        public NpcRole Role;
        public string Feature;
        public List<string> Dialogue = new List<string>();
    }

    public class RoomExit
    {
        /// <summary>left, right, back or down.</summary>
        public string Side;
        public double? X;
        public string Frame;
        public string To, Label;
        public string RequiresQuest;
        public bool Guarded;
        public string Blocker;
    }

    public class Spot
    {
        public double X, Y;
    }

    public class RoomBoss
    {
        public string MonsterId;
        public Dictionary<string, string> ByClass;
        public double X, Y;
        public string QuestId, Intro;
    }

    public class Location
    {
        public string RoomId;
        public double X, Y;

        public Location Clone() => (Location)MemberwiseClone();
    }

    public class MapSpot
    {
        public string Area;
        public int X, Y;
    }

    public class InspectDef
    {
        public string Id, Label, Look;
        public double X, Y;
        public List<string> Lines = new List<string>();
    }

    public class FirstVisit
    {
        public List<string> Lines = new List<string>();
        public double? PanTo;
    }

    public class RoomDef
    {
        public string Id, Name;
        [JsonConverter(typeof(OneOrManyConverter<string>))] public List<string> Background;
        public Backdrop Backdrop;
        public double[] Walk;
        public List<RoomExit> Exits = new List<RoomExit>();
        public List<NpcDef> Npcs = new List<NpcDef>();
        public List<string> Ambient;
        public string ZoneId;
        public int? MonsterCount;
        public Spot Chest;
        public RoomBoss Boss;
        public Location WakeAt;
        public Spot Town;
        public MapSpot Map;
        public Spot Campfire;
        public List<InspectDef> Inspect;
        public FirstVisit FirstVisit;
    }

    public class MapAreaDef
    {
        public string Id, Name;
    }

    public class FeatureDef
    {
        public string Id, Name, Text, UnlockedBy;
        public bool Disabled;
    }

    public class QuestObjective
    {
        /// <summary>kill or collect.</summary>
        public string Kind;
        public string MonsterId;
        public bool Elite;
        public string ItemId;
        public int Count;
    }

    public class QuestRewards
    {
        public double Xp, Gold;
        public List<string> Items;
        public Dictionary<string, string> ClassItems;
    }

    public class QuestDef
    {
        public string Id, Name, Giver, Requires, RequiresRank;
        public QuestObjective Objective;
        public string Goal;
        public List<string> Offer = new List<string>();
        public string Reminder;
        public List<string> Complete = new List<string>();
        public QuestRewards Rewards;
        public string EndsChapter;
    }

    public class PromotionDef
    {
        public double Gold;
        public List<string> Items;
        public Dictionary<string, string> ClassItems;
    }

    public class ChapterStart
    {
        public int Chapter, Level;
        public string After;
        public Dictionary<string, string> Weapon, Armor;
        public Dictionary<string, List<string>> Kit;
        /// <summary>[itemId, qty] pairs.</summary>
        public List<object[]> Potions;
        public double? Gold;
    }
}
