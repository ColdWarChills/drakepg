using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    public class QuestState
    {
        /// <summary>active or done.</summary>
        public string Status;
        public int Count;
    }

    public class InventoryEntry
    {
        public string ItemId;
        public int Qty;
    }

    /// <summary>Everything that gets saved: the same JSON shape as the TypeScript `PlayerData` (save version 15).</summary>
    public class PlayerData
    {
        public string Name;
        public int Level;
        public double Xp, Gold;
        public Stats BaseStats;
        public double Hp, Mp;
        public Dictionary<string, Gear> Equipment;
        public List<InventoryEntry> Inventory = new List<InventoryEntry>();
        public List<Gear> Gear = new List<Gear>();
        public int NextUid;
        public Location Location;
        public string ClassId;
        public int StatPoints;
        public Dictionary<string, QuestState> Quests = new Dictionary<string, QuestState>();
        public Dictionary<string, double> LootPity = new Dictionary<string, double>();
        public Dictionary<string, double> BossGrit = new Dictionary<string, double>();
        public List<string> Seen = new List<string>();
        public List<string> FreshSkills = new List<string>();
        public List<string> Loadout = new List<string>();
        public List<string> Visited = new List<string>();
        public string LastTown;
        public List<string> GuardsBeaten = new List<string>();
        public GuildState Guild = new GuildState();
        /// <summary>Skills learned from books, in the order read (Books.cs). Save v16.</summary>
        public List<string> Learned = new List<string>();
        /// <summary>Elite kills without a book drop, per loot table. Save v16.</summary>
        public Dictionary<string, double> BookPity = new Dictionary<string, double>();
    }

    public class Rewards
    {
        public double Xp, Gold;
        public List<ItemGrant> Items = new List<ItemGrant>();
    }

    /// <summary>The hero, a port of src/game/Player.ts.</summary>
    public class Player
    {
        public PlayerData Data;
        public Player(PlayerData data) => Data = data;

        static Balance B => Db.B;
        static readonly double[] Unarmed = { 2, 4 };

        public static double StatFor(Stats stats, string type) => type == "magic" ? stats.Int : type == "pierce" ? stats.Dex : stats.Str;

        public static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>
        {
            ["str"] = "STR", ["int"] = "INT", ["dex"] = "DEX", ["end"] = "END",
        };

        /// <summary>XP needed to go from `level` to `level + 1`.</summary>
        public static double XpToNext(int level) => Math.Floor(50 * Math.Pow(level, 1.5));

        public static Dictionary<string, Gear> EmptyEquipment()
        {
            var e = new Dictionary<string, Gear>();
            foreach (var s in Items.AllSlots) e[s] = null;
            return e;
        }

        public static Player CreateNew(string name = "Hero", string classId = "warrior")
        {
            var cls = Db.Class(classId);
            var player = new Player(new PlayerData
            {
                Name = name,
                Level = 1,
                Xp = 0,
                Gold = 30,
                BaseStats = cls.Stats.Clone(),
                Hp = 1,
                Mp = 1,
                Equipment = EmptyEquipment(),
                Inventory = new List<InventoryEntry>
                {
                    new InventoryEntry { ItemId = "health_potion", Qty = 2 },
                    new InventoryEntry { ItemId = "mana_potion", Qty = 1 },
                    new InventoryEntry { ItemId = "recall_scroll", Qty = (int)B.StartingRecallScrolls },
                },
                NextUid = 1,
                Location = Db.Data.Start.Clone(),
                ClassId = classId,
                Guild = new GuildState(),
            });
            player.Data.Loadout = player.DefaultLoadout();
            player.Data.Equipment["weapon"] = player.MakeGear(cls.Weapon);
            player.RestoreFull();
            return player;
        }

        public string Name => Data.Name;
        public ClassDef ClassDef => Db.Class(Data.ClassId);
        public int Level => Data.Level;
        public double Hp => Data.Hp;
        public double Mp => Data.Mp;
        public double Gold => Data.Gold;

        public ItemDef Weapon => Data.Equipment["weapon"] is Gear g ? Items.GearDef(g) : null;
        public ItemDef SpareWeapon => Data.Equipment["spare"] is Gear g ? Items.GearDef(g) : null;

        public double[] SpareAttackRange => Data.Equipment["spare"] != null ? WithSpareInHand(() => AttackRange) : null;

        T WithSpareInHand<T>(Func<T> fn)
        {
            var e = Data.Equipment;
            (e["weapon"], e["spare"]) = (e["spare"], e["weapon"]);
            try { return fn(); }
            finally { (e["weapon"], e["spare"]) = (e["spare"], e["weapon"]); }
        }

        public bool SwapWeapons()
        {
            var e = Data.Equipment;
            if (e["spare"] == null && e["weapon"] == null) return false;
            (e["weapon"], e["spare"]) = (e["spare"], e["weapon"]);
            ClampVitals();
            return true;
        }

        public string WeaponSigil => Data.Equipment["weapon"]?.Sigil;

        public ItemDef BodyArmor => Data.Equipment["body"] is Gear g ? Items.GearDef(g) : null;

        /// <summary>How the hero is drawn: the class look, in the body armor's outfit when that art exists.</summary>
        public Look Look
        {
            get
            {
                var cls = ClassDef.Look;
                var armor = BodyArmor;
                if (armor == null) return cls;
                var classBare = cls.Bare ?? new List<BareLook>();
                var baseArt = classBare.Count > 0 && classBare[0].Art.EndsWith("_bare") ? classBare[0].Art.Substring(0, classBare[0].Art.Length - 5)
                    : classBare.Count > 0 ? classBare[0].Art : "char_" + Data.ClassId;
                var art = baseArt + "_" + armor.Id;
                List<Hand> hands = null;
                if (armor.Hands != null) armor.Hands.TryGetValue(Data.ClassId, out hands);
                hands ??= classBare.Count > 0 ? classBare[0].Hands : new List<Hand>();
                var look = cls.Clone();
                look.Art = new List<string> { art };
                if (cls.Art != null) look.Art.AddRange(cls.Art);
                look.Bare = new List<BareLook> { new BareLook { Art = art, Hands = hands, Poses = classBare.Count > 0 ? classBare[0].Poses : null } };
                look.Bare.AddRange(classBare);
                return look;
            }
        }

        GearTraits TraitsOf(string slot) => (GearTraits)Data.Equipment[slot] ?? new GearTraits();

        IEnumerable<Gear> Worn => Items.EquipSlots.Select(s => Data.Equipment[s]);

        IEnumerable<(ItemDef def, Gear gear)> WornPieces => Worn.Where(g => g != null).Select(g => (Items.GearDef(g), g));

        public SetBonusTotals SetBonus => Sets.SetBonusTotals(Data.Equipment);

        /// <summary>Base stats plus equipment bonuses (item stats, accessories, stat mods and set bonuses).</summary>
        public Stats Stats
        {
            get
            {
                var total = Data.BaseStats.Clone();
                var set = SetBonus;
                foreach (var (def, gear) in WornPieces)
                {
                    if (def.Stats == null) continue;
                    foreach (var key in Stat.Keys)
                    {
                        var v = def.StatOf(key);
                        total[key] += def.IsAccessory ? Items.AccessoryValue(v, gear) : v;
                    }
                }
                foreach (var key in Stat.Keys) total[key] += Items.ModTotal(Worn, key) + (set.Stats.TryGetValue(key, out var s) ? s : 0);
                return total;
            }
        }

        double AccessoryTotal(string key)
        {
            double total = 0;
            foreach (var (def, gear) in WornPieces)
            {
                var v = key == "hp" ? def.Hp : def.Mp;
                if (def.IsAccessory && v.HasValue && v.Value != 0) total += Items.AccessoryValue(v.Value, gear);
            }
            return total;
        }

        public double MaxHp
        {
            get
            {
                var pct = SetBonus.HpPct / 100;
                foreach (var (def, gear) in WornPieces) if (def.IsArmor) pct += Items.LineBonus(def, "hp", gear.Rolls);
                var b = (40 + Level * 10 + Stats.End * 4) * (1 + pct);
                return JsMath.Round(b) + Items.ModTotal(Worn, "hp") + AccessoryTotal("hp");
            }
        }

        public double MaxMp => 12 + Level * 3 + Stats.Int * 2 + Items.ModTotal(Worn, "mp") + AccessoryTotal("mp");

        public double DefenseFor(string type)
        {
            double total = 0;
            foreach (var (def, gear) in WornPieces) if (def.IsArmor) total += Items.RolledDefense(def, gear, type);
            return JsMath.Round(total * (1 + SetBonus.DefensePct / 100));
        }

        /// <summary>Elemental resistances from gear (before buffs and the cap).</summary>
        public Dictionary<string, double> Resists
        {
            get
            {
                var output = new Dictionary<string, double>();
                void Add(Dictionary<string, double> r, Func<double, double> scale = null)
                {
                    if (r == null) return;
                    foreach (var kv in r) output[kv.Key] = (output.TryGetValue(kv.Key, out var o) ? o : 0) + (scale != null ? scale(kv.Value) : kv.Value);
                }
                foreach (var (def, gear) in WornPieces)
                {
                    if (def.IsArmor) Add(def.Resists);
                    if (def.IsAccessory) Add(def.Resists, v => Items.AccessoryValue(v, gear));
                    foreach (var m in gear.Mods ?? new List<Mod>())
                        if (m.Key == "resist" && m.Element != null) output[m.Element] = (output.TryGetValue(m.Element, out var o) ? o : 0) + Items.ModValue(m);
                }
                Add(SetBonus.Resists);
                return output;
            }
        }

        public double DamageBonus => SetBonus.DamagePct / 100;

        public string WeaponDamageType => Weapon?.DamageType ?? "melee";

        /// <summary>Null = neutral.</summary>
        public string WeaponElement => Weapon?.Element;

        public double[] WeaponDamage => Weapon is ItemDef w ? Items.RolledDamage(w, TraitsOf("weapon")) : Unarmed;

        public double ProcChance => Weapon is ItemDef w ? Items.RolledProcChance(w, TraitsOf("weapon")) : 0;

        public double CritBonus
        {
            get
            {
                double accessories = 0;
                foreach (var (def, gear) in WornPieces) if (def.IsAccessory && def.Crit.HasValue && def.Crit.Value != 0) accessories += Items.AccessoryValue(def.Crit.Value, gear, 1);
                return Items.RollBonus("crit", TraitsOf("weapon").Rolls) + (Items.ModTotal(Worn, "crit") + accessories + SetBonus.Crit) / 100;
            }
        }

        public double CritChance => B.BaseCritChance + Stats.Dex * B.CritPerDex + CritBonus;

        public double StatForType(string type) => StatFor(Stats, type);

        public double[] AttackRange
        {
            get
            {
                var bonus = Math.Floor(StatForType(WeaponDamageType) * B.StatDamageScale);
                var d = WeaponDamage;
                return new[] { d[0] + bonus, d[1] + bonus };
            }
        }

        public double Xp => Data.Xp;
        public double XpNeeded => XpToNext(Level);

        /// <summary>Level skills unlocked at the current level, then skills learned from books (in the order read).</summary>
        public List<SkillDef> KnownSkills
        {
            get
            {
                var fromLevels = ClassDef.Skills.Where(s => s.Level <= Level).Select(s => s.SkillId).ToList();
                return fromLevels.Concat(Data.Learned.Where(id => !fromLevels.Contains(id))).Select(Db.Skill).ToList();
            }
        }

        public bool KnowsSkill(string skillId) =>
            Data.Learned.Contains(skillId) || ClassDef.Skills.Any(s => s.SkillId == skillId && s.Level <= Level);

        public List<SkillDef> CarriedSkills
        {
            get
            {
                var known = new HashSet<string>(KnownSkills.Select(s => s.Id));
                return Data.Loadout.Where(known.Contains).Select(Db.Skill).ToList();
            }
        }

        public bool IsCarried(string skillId) => Data.Loadout.Contains(skillId);

        public List<string> DefaultLoadout()
        {
            var known = KnownSkills;
            var heal = known.FirstOrDefault(s => s.Kind == "heal");
            var rest = known.Where(s => s != heal).Reverse();
            var picked = new HashSet<string>((heal != null ? new[] { heal } : new SkillDef[0]).Concat(rest).Take(B.SkillSlots).Select(s => s.Id));
            return known.Where(s => picked.Contains(s.Id)).Select(s => s.Id).ToList();
        }

        /// <summary>added, removed, full or unknown.</summary>
        public string ToggleCarry(string skillId)
        {
            if (!KnowsSkill(skillId)) return "unknown";
            var list = Data.Loadout;
            if (list.Contains(skillId))
            {
                list.Remove(skillId);
                return "removed";
            }
            if (list.Count >= B.SkillSlots) return "full";
            list.Add(skillId);
            return "added";
        }

        public bool CarryNew(string skillId)
        {
            if (IsCarried(skillId) || Data.Loadout.Count >= B.SkillSlots) return IsCarried(skillId);
            Data.Loadout.Add(skillId);
            return true;
        }

        public List<SkillDef> SkillsLearnedSince(int fromLevel) =>
            ClassDef.Skills.Where(s => s.Level > fromLevel && s.Level <= Level).Select(s => Db.Skill(s.SkillId)).ToList();

        public bool SpendStatPoint(string stat)
        {
            if (Data.StatPoints <= 0) return false;
            Data.StatPoints -= 1;
            Data.BaseStats[stat] += 1;
            if (stat == "end") Data.Hp += 4;
            if (stat == "int") Data.Mp += 2;
            ClampVitals();
            return true;
        }

        public bool IsDead => Data.Hp <= 0;

        public void RestoreFull()
        {
            Data.Hp = MaxHp;
            Data.Mp = MaxMp;
        }

        public void ClampVitals()
        {
            Data.Hp = Math.Min(Math.Max(0, Data.Hp), MaxHp);
            Data.Mp = Math.Min(Math.Max(0, Data.Mp), MaxMp);
        }

        public void TakeDamage(double amount) => Data.Hp = Math.Max(0, Data.Hp - amount);

        public double Heal(double amount)
        {
            var before = Data.Hp;
            Data.Hp = Math.Min(MaxHp, Data.Hp + amount);
            return Data.Hp - before;
        }

        public double RestoreMp(double amount)
        {
            var before = Data.Mp;
            Data.Mp = Math.Min(MaxMp, Data.Mp + amount);
            return Data.Mp - before;
        }

        public bool SpendMp(double amount)
        {
            if (Data.Mp < amount) return false;
            Data.Mp -= amount;
            return true;
        }

        /// <summary>Returns the number of levels gained.</summary>
        public int AddXp(double amount)
        {
            int gained = 0;
            Data.Xp += amount;
            while (Level < B.MaxLevel && Data.Xp >= XpNeeded)
            {
                Data.Xp -= XpNeeded;
                Data.Level += 1;
                var growth = ClassDef.Growth;
                foreach (var key in Stat.Keys) Data.BaseStats[key] += growth.TryGetValue(key, out var g) ? g : 0;
                Data.StatPoints += B.StatPointsPerLevel;
                gained += 1;
            }
            if (Level >= B.MaxLevel) Data.Xp = 0;
            if (gained > 0) RestoreFull();
            return gained;
        }

        public (int levels, List<ItemGrant> lost) GrantRewards(Rewards rewards, bool force = false)
        {
            Data.Gold += rewards.Gold;
            var lost = rewards.Items.Where(grant => !Receive(grant, force)).ToList();
            return (AddXp(rewards.Xp), lost);
        }

        // --- Inventory ---

        /// <summary>Bag slots in use: one per gear piece and per consumable stack (materials take none).</summary>
        public int UsedSlots => Data.Gear.Count + Data.Inventory.Count(e => Db.Item(e.ItemId).Type != "material");

        public int FreeSlots => Math.Max(0, B.BagSlots - UsedSlots);

        public Gear MakeGear(string itemId, GearTraits traits = null, bool isNew = false)
        {
            var gear = new Gear { Uid = "g" + Data.NextUid++, ItemId = itemId, Rolls = new Dictionary<string, double>(traits?.Rolls ?? new Dictionary<string, double>()) };
            if (traits?.Sigil != null) gear.Sigil = traits.Sigil;
            if (traits?.Mods != null && traits.Mods.Count > 0) gear.Mods = traits.Mods.Select(m => m.Clone()).ToList();
            if (isNew) gear.IsNew = true;
            if (isNew && gear.Sigil != null) gear.Locked = true;
            return gear;
        }

        public bool Receive(ItemGrant grant, bool force = false)
        {
            var def = Db.Item(grant.ItemId);
            var stacks = def.Type == "material" || (!def.IsGear && Data.Inventory.Any(e => e.ItemId == grant.ItemId));
            if (!stacks && !force && FreeSlots == 0) return false;
            if (def.IsGear) Data.Gear.Add(MakeGear(grant.ItemId, grant.Traits(), true));
            else AddItem(grant.ItemId);
            return true;
        }

        public int CountItem(string itemId)
        {
            if (!Db.Item(itemId).IsGear) return Data.Inventory.FirstOrDefault(e => e.ItemId == itemId)?.Qty ?? 0;
            var equipped = Data.Equipment.Values.Count(g => g?.ItemId == itemId);
            return equipped + Data.Gear.Count(g => g.ItemId == itemId);
        }

        public void AddItem(string itemId, int qty = 1)
        {
            if (Db.Item(itemId).IsGear)
            {
                for (int i = 0; i < qty; i++) Data.Gear.Add(MakeGear(itemId));
                return;
            }
            var entry = Data.Inventory.FirstOrDefault(e => e.ItemId == itemId);
            if (entry != null) entry.Qty += qty;
            else Data.Inventory.Add(new InventoryEntry { ItemId = itemId, Qty = qty });
        }

        public bool RemoveItem(string itemId, int qty = 1)
        {
            var entry = Data.Inventory.FirstOrDefault(e => e.ItemId == itemId);
            if (entry == null || entry.Qty < qty) return false;
            entry.Qty -= qty;
            if (entry.Qty == 0) Data.Inventory.Remove(entry);
            return true;
        }

        /// <summary>ok, gold or full. Shop gear is always Standard (0%).</summary>
        public string Buy(string itemId)
        {
            var item = Db.Item(itemId);
            if (Data.Gold < item.Price) return "gold";
            if (!Receive(new ItemGrant { ItemId = itemId, Rolls = new Dictionary<string, double>() })) return "full";
            Data.Gold -= item.Price;
            return "ok";
        }

        public Gear FindGear(string uid) => Data.Gear.FirstOrDefault(g => g.Uid == uid) ?? Data.Equipment.Values.FirstOrDefault(g => g?.Uid == uid);

        public string EquippedSlot(string uid)
        {
            foreach (var slot in Items.AllSlots) if (Data.Equipment[slot]?.Uid == uid) return slot;
            return null;
        }

        public string SlotFor(ItemDef def)
        {
            var slots = Items.SlotsFor(def);
            return slots.FirstOrDefault(s => Data.Equipment[s] == null) ?? slots[0];
        }

        public bool Equip(string uid, string slot = null)
        {
            int index = Data.Gear.FindIndex(g => g.Uid == uid);
            if (index < 0) return false;
            var gear = Data.Gear[index];
            var def = Items.GearDef(gear);
            if (slot == null || !Items.FitsSlot(def, slot)) slot = SlotFor(def);
            var previous = Data.Equipment[slot];
            gear.IsNew = null;
            Data.Equipment[slot] = gear;
            if (previous != null) Data.Gear[index] = previous;
            else Data.Gear.RemoveAt(index);
            ClampVitals();
            return true;
        }

        public bool Unequip(string slot)
        {
            var current = Data.Equipment[slot];
            if (current == null || FreeSlots == 0) return false;
            Data.Gear.Add(current);
            Data.Equipment[slot] = null;
            ClampVitals();
            return true;
        }

        public void ToggleLock(string uid)
        {
            var gear = FindGear(uid);
            if (gear == null) return;
            gear.Locked = gear.Locked == true ? (bool?)null : true;
        }

        public double SellGear(string uid)
        {
            int index = Data.Gear.FindIndex(g => g.Uid == uid);
            if (index < 0) return 0;
            var gear = Data.Gear[index];
            if (gear.Locked == true) return 0;
            var gold = Items.SellValue(Items.GearDef(gear), gear);
            Data.Gear.RemoveAt(index);
            Data.Gold += gold;
            return gold;
        }

        public double SellItem(string itemId)
        {
            if (!RemoveItem(itemId)) return 0;
            var gold = Items.SellValue(Db.Item(itemId));
            Data.Gold += gold;
            return gold;
        }

        /// <summary>What a consumable restored, or null if it would do nothing.</summary>
        public (double hp, double mp)? UseConsumable(string itemId)
        {
            var item = Db.Item(itemId);
            if (item.Type != "consumable") return null;
            var restore = item.Restore;
            var useful = (restore.Hp.HasValue && restore.Hp.Value != 0 && Hp < MaxHp) || (restore.Mp.HasValue && restore.Mp.Value != 0 && Mp < MaxMp);
            if (!useful || !RemoveItem(itemId)) return null;
            var hp = restore.Hp.HasValue && restore.Hp.Value != 0 ? Heal(restore.Hp.Value) : 0;
            var mp = restore.Mp.HasValue && restore.Mp.Value != 0 ? RestoreMp(restore.Mp.Value) : 0;
            return (hp, mp);
        }
    }
}
