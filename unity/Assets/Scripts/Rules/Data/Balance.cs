using System.Collections.Generic;

namespace Drakepg.Rules
{
    /// <summary>
    /// The design knobs, `BALANCE` in src/config.ts, loaded from the exported JSON (so they are tuned in one place).
    /// Odds tables keyed by tier or source stay dictionaries, like the TypeScript records.
    /// </summary>
    public class Balance
    {
        public bool RestoreAfterBattle;
        public double InnCost, StartingRecallScrolls;
        public double BaseHitChance, MinHitChance, MaxHitChance;
        public double BaseCritChance, CritPerDex, CritMultiplier;
        public double StatDamageScale, BaseFleeChance;
        public int MaxLevel, SkillSlots, StatPointsPerLevel;
        public BookTuning Books;
        public Dictionary<string, FamilyTuning> Families;
        public WardTuning Ward;
        public FreezeTuning Freeze;
        public TowerTuning Tower;
        public ReworkTuning Rework;
        public double DefendDamage, DefendMp, MaxPlayerResist, MinResist;

        public Dictionary<string, double> GearRolls;
        public Dictionary<string, double> ArmorHpShare;
        public Dictionary<string, double> SetDrop;
        public double RollCurve;
        public int BagSlots;
        public double SellRate, SellGradeBonus;
        public Dictionary<string, SigilTuning> Sigils;
        public LootTuning Loot;
        public Dictionary<string, Dictionary<string, double>> SigilChance;
        public int[] ModifierSlots;
        public Dictionary<string, double[]> ModRanges;
        public ForgeTuning Forge;
        public double MobDamage, BossDamage;
        public EnrageTuning Enrage;
        public GritTuning Grit;
        public double BossRepeatRewards;
        public HeroicTuning Heroic;
        public EliteTuning Elite;
        public LuckyTuning Lucky;
        public Dictionary<string, double> DropReveal;
        public GuildTuning Guild;
        public double RespawnSeconds;
        public ChestTuning Chest;
        public double WalkSpeed, MonsterWanderSpeed, MonsterChaseSpeed, MonsterAggroRange, FleeGraceSeconds;
    }

    /// <summary>Skill books (docs/BUILD_VARIETY_SPEC.md).</summary>
    public class BookTuning
    {
        /// <summary>Level an element's books need.</summary>
        public Dictionary<string, int> Levels;
        public double Price, Match;
        public BookDropTuning Drop;
    }

    public class BookDropTuning
    {
        public double Chance;
        public int SureAfter;
    }

    public class FamilyTuning
    {
        public double Power, MpCost, Chance;
        public int Hits, Cooldown;
    }

    public class WardTuning
    {
        public double Resist, MpCost;
        public int Turns, Cooldown;
    }

    public class FreezeTuning
    {
        public double PerStat, Max;
    }

    /// <summary>The tower (BALANCE.tower): the level curve, twist numbers and first-clear pay.</summary>
    public class TowerTuning
    {
        public TowerCurve Curve;
        public double DefensePerLevel;
        public int ShortFuse;
        public VeilTuning Veil;
        public ShellTuning Shell;
        public MilestoneTuning Milestone;
    }

    /// <summary>Rework at the shop counter (BALANCE.rework): base gold x tier, x growth per earlier rework; the Anchor Stone's drop chance by kind of monster.</summary>
    public class ReworkTuning
    {
        public double Base, Growth;
        public Dictionary<string, double> AnchorDrop;
    }

    public class TowerCurve
    {
        public double[] Hp, Damage;
    }

    public class VeilTuning
    {
        public double Up, Down;
    }

    public class ShellTuning
    {
        public int Hits;
        public double Reduction, ReformAt;
    }

    public class MilestoneTuning
    {
        public int Fifth, Tenth;
    }

    public class SigilTuning
    {
        public double Boost, Sell;
        public int Lines;
    }

    public class LootTuning
    {
        public Dictionary<string, double> Mob, Elite, Boss, Chest;
        public Dictionary<string, Dictionary<string, double>> TableMob;
        public double PityAfter, PityStep, PityMax;

        /// <summary>`BALANCE.loot[source]`.</summary>
        public Dictionary<string, double> For(string source) => source switch
        {
            "elite" => Elite,
            "boss" => Boss,
            "chest" => Chest,
            _ => Mob,
        };
    }

    public class ForgeTuning
    {
        public double CraftFloor, PlusPower;
        public int MaxPlus, ReinforceRiskFrom;
        public double[] ReinforceChance;
        public int[] HoneGain;
        public double HoneDriftChance;
        public int[] HoneDrift;
    }

    public class EnrageTuning
    {
        public int Turns, BossTurns, Warn;
        public double Step;
    }

    public class GritTuning
    {
        public double Step, Max;
    }

    public class HeroicTuning
    {
        public int Levels, LootRolls;
        public double Damage, Rewards;
    }

    public class EliteTuning
    {
        public double Chance, QuestChance, Hp, Damage, Rewards, Size;
        public int MaxPerRoom, Levels;
    }

    public class LuckyTuning
    {
        public double Chance, RunSpeed, Hp, Dex, Damage, Xp, Gold, SigilChance;
        public int EscapeAfter;
        public Dictionary<string, double> SigilTiers;
    }

    public class RankTuning
    {
        public string Id;
        public int MinLevel, ToNext;
    }

    public class GuildTuning
    {
        public List<RankTuning> Ranks;
        public int Reach, BoardSize, MaxActive, Kills, ToughKills, Gather, EliteKills;
        public double GatherChance, EliteChance, Xp, Gold;
    }

    public class ChestTuning
    {
        public double Chance, MimicChance;
    }
}
