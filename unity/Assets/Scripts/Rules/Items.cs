using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>An extra ("blue") line: stats/resist are points, crit percent, hp/mp flat.</summary>
    public class Mod
    {
        public string Key;
        /// <summary>Quality 0-100.</summary>
        public double Roll;
        /// <summary>Which element a resist mod covers.</summary>
        public string Element;

        public Mod Clone() => (Mod)MemberwiseClone();
    }

    /// <summary>What makes one piece unique. Owned gear and loot grants both carry it.</summary>
    public class GearTraits
    {
        /// <summary>Whole percents 0-100 per line (damage, crit, proc, defense, hp, power). Missing = 0.</summary>
        public Dictionary<string, double> Rolls = new Dictionary<string, double>();
        public string Sigil;
        public List<Mod> Mods;
        /// <summary>Smith reinforcement. Null = 0.</summary>
        public int? Plus;

        public double Roll(string key) => Rolls != null && Rolls.TryGetValue(key, out var v) ? v : 0;
    }

    /// <summary>A weapon, armor piece or accessory the player owns. Saved as is.</summary>
    public class Gear : GearTraits
    {
        public string Uid, ItemId;
        public bool? Locked;
        public bool? IsNew;
        /// <summary>Times reworked at a shop counter (each one costs more: Rework). Null = 0.</summary>
        public int? Reworks;
    }

    /// <summary>Something handed to the player: a consumable (no rolls), or gear with its rolls decided.</summary>
    public class ItemGrant
    {
        public string ItemId;
        public Dictionary<string, double> Rolls;
        public string Sigil;
        public List<Mod> Mods;
        public int? Plus;

        public GearTraits Traits() => new GearTraits { Rolls = Rolls ?? new Dictionary<string, double>(), Sigil = Sigil, Mods = Mods, Plus = Plus };
    }

    /// <summary>Rolled gear rules, a port of src/game/Items.ts.</summary>
    public static class Items
    {
        public static readonly string[] SigilTiers = { "dawn", "dusk", "eclipse" };

        public static readonly Dictionary<string, string> SigilNames = new Dictionary<string, string>
        {
            ["dawn"] = "Sigil of Dawn", ["dusk"] = "Sigil of Dusk", ["eclipse"] = "Sigil of Eclipse",
        };

        public static readonly Dictionary<string, string[]> ModPools = new Dictionary<string, string[]>
        {
            ["weapon"] = new[] { "str", "int", "dex", "end", "crit", "mp" },
            ["armor"] = new[] { "str", "int", "dex", "end", "hp", "mp", "resist" },
            ["accessory"] = new[] { "str", "int", "dex", "end", "crit", "hp", "mp", "resist" },
        };

        /// <summary>Worn slots in display order. Their stats count; the spare weapon's don't.</summary>
        public static readonly string[] EquipSlots = { "weapon", "helm", "body", "gloves", "boots", "necklace", "ring1", "ring2" };

        /// <summary>The worn slots plus the spare weapon.</summary>
        public static readonly string[] AllSlots = { "weapon", "helm", "body", "gloves", "boots", "necklace", "ring1", "ring2", "spare" };

        public static readonly Dictionary<string, string> SlotLabels = new Dictionary<string, string>
        {
            ["weapon"] = "Weapon", ["helm"] = "Helm", ["body"] = "Body", ["gloves"] = "Gloves", ["boots"] = "Boots",
            ["necklace"] = "Necklace", ["ring1"] = "Ring", ["ring2"] = "Ring", ["spare"] = "Spare\nweapon",
        };

        public static readonly Dictionary<string, string> RollLabels = new Dictionary<string, string>
        {
            ["damage"] = "Damage", ["crit"] = "Crit chance", ["proc"] = "Special chance", ["defense"] = "Defense", ["hp"] = "Max HP", ["power"] = "Power",
        };

        static Balance B => Db.B;

        public static string ArmorSlot(ItemDef def) => def.Slot ?? "body";

        public static bool FitsSlot(ItemDef def, string slot) => slot == "spare" ? def.IsWeapon : SlotsFor(def).Contains(slot);

        public static string[] SlotsFor(ItemDef def)
        {
            if (def.IsWeapon) return new[] { "weapon" };
            if (def.IsArmor) return new[] { ArmorSlot(def) };
            return def.Slot == "ring" ? new[] { "ring1", "ring2" } : new[] { "necklace" };
        }

        public static string KindLabel(ItemDef def)
        {
            if (def.IsWeapon) return "Weapon";
            if (def.IsAccessory) return def.Slot == "ring" ? "Ring" : "Necklace";
            var slot = ArmorSlot(def);
            return slot == "body" ? "Body armor" : SlotLabels[slot];
        }

        public static ItemDef GearDef(Gear gear) => Db.Item(gear.ItemId);

        /// <summary>The quality lines this item rolls, in display order.</summary>
        public static List<string> RollKeys(ItemDef def, string sigil = null)
        {
            string[] all = def.IsArmor ? new[] { "defense", "hp" }
                : def.IsAccessory ? new[] { "power" }
                : def.Proc != null ? new[] { "damage", "crit", "proc" } : new[] { "damage", "crit" };
            return all.Take(Math.Max(0, ModifierSlots(def, sigil))).ToList();
        }

        public static int ModifierSlots(ItemDef def, string sigil = null)
        {
            var slots = B.ModifierSlots;
            return slots[Math.Min(def.Tier, slots.Length - 1)] + (sigil != null ? B.Sigils[sigil].Lines : 0);
        }

        public static int ModSlots(ItemDef def, string sigil = null) => Math.Max(0, ModifierSlots(def, sigil) - RollKeys(def, sigil).Count);

        /// <summary>One drop line: random ^ curve, so most lines land low.</summary>
        public static double RollQuality(IRng rng) => JsMath.Round(Math.Pow(rng.Next(), B.RollCurve) * 100);

        public static Dictionary<string, double> RollGear(ItemDef def, IRng rng, double? quality = null, string sigil = null)
        {
            var rolls = new Dictionary<string, double>();
            foreach (var key in RollKeys(def, sigil)) rolls[key] = quality ?? RollQuality(rng);
            return rolls;
        }

        /// <summary>Gear gets rolled (maybe a Sigil and mods), consumables don't.</summary>
        public static ItemGrant GrantFor(string itemId, IRng rng, string source = "quest", bool extras = true)
        {
            var def = Db.Item(itemId);
            if (!def.IsGear) return new ItemGrant { ItemId = itemId };
            var sigil = source == "quest" || !extras ? null : RollSigil(rng, source);
            var grant = new ItemGrant { ItemId = itemId, Rolls = RollGear(def, rng, null, sigil) };
            if (sigil == null) return grant;
            grant.Sigil = sigil;
            var mods = ModSlots(def, sigil);
            if (mods > 0) grant.Mods = RollMods(def, mods, rng);
            return grant;
        }

        /// <summary>Rarest tier checked first, so each chance is its own.</summary>
        public static string RollSigil(IRng rng, string source)
        {
            var table = B.SigilChance[source];
            var r = rng.Next();
            double edge = 0;
            for (int i = SigilTiers.Length - 1; i >= 0; i--)
            {
                edge += Cheats.Boosted(table[SigilTiers[i]]);
                if (r < edge) return SigilTiers[i];
            }
            return null;
        }

        public static List<Mod> RollMods(ItemDef def, int count, IRng rng)
        {
            var pool = new List<string>(ModPools[def.Type]);
            var mods = new List<Mod>();
            while (mods.Count < count && pool.Count > 0)
            {
                int i = (int)Math.Floor(rng.Next() * pool.Count);
                var key = pool[i];
                pool.RemoveAt(i);
                var mod = new Mod { Key = key, Roll = RollQuality(rng) };
                if (key == "resist") mod.Element = Db.Data.Elements[(int)Math.Floor(rng.Next() * Db.Data.Elements.Count)];
                mods.Add(mod);
            }
            return mods;
        }

        public static double ModValue(Mod mod)
        {
            var range = B.ModRanges[mod.Key];
            var v = range[0] + (range[1] - range[0]) * mod.Roll / 100;
            return mod.Key == "crit" ? JsMath.Round(v * 10) / 10 : JsMath.Round(v);
        }

        /// <summary>Sum of one mod key over some gear. Resist mods count only for `element`.</summary>
        public static double ModTotal(IEnumerable<GearTraits> pieces, string key, string element = null)
        {
            double total = 0;
            foreach (var g in pieces)
            {
                if (g?.Mods == null) continue;
                foreach (var m in g.Mods) if (m.Key == key && (key != "resist" || m.Element == element)) total += ModValue(m);
            }
            return total;
        }

        public static double SigilBoost(GearTraits g) =>
            (1 + (g.Sigil != null ? B.Sigils[g.Sigil].Boost : 0)) * (1 + (g.Plus ?? 0) * B.Forge.PlusPower);

        /// <summary>Average line quality, 0-100.</summary>
        public static double Grade(ItemDef def, Dictionary<string, double> rolls, string sigil = null)
        {
            var keys = RollKeys(def, sigil);
            double sum = 0;
            foreach (var k in keys) sum += rolls != null && rolls.TryGetValue(k, out var v) ? v : 0;
            return JsMath.Round(sum / keys.Count);
        }

        public static readonly (string tier, string name, double min)[] GradeTiers =
        {
            ("masterwork", "Masterwork", 90), ("superior", "Superior", 70), ("fine", "Fine", 40), ("fair", "Fair", 1), ("standard", "Standard", 0),
        };

        public static (string tier, string name) GradeTier(double g)
        {
            foreach (var t in GradeTiers) if (g >= t.min) return (t.tier, t.name);
            return ("standard", "Standard");
        }

        public static double RollBonus(string key, Dictionary<string, double> rolls) =>
            (rolls != null && rolls.TryGetValue(key, out var v) ? v : 0) / 100 * B.GearRolls[key];

        public static double LineBonus(ItemDef def, string key, Dictionary<string, double> rolls)
        {
            var share = def.IsArmor && key == "hp" ? B.ArmorHpShare[ArmorSlot(def)] : 1;
            return RollBonus(key, rolls) * share;
        }

        /// <summary>An accessory's number at this piece's Power roll, Sigil and +.</summary>
        public static double AccessoryValue(double value, GearTraits g, int decimals = 0)
        {
            var v = value * SigilBoost(g) * (1 + RollBonus("power", g.Rolls));
            var f = Math.Pow(10, decimals);
            return JsMath.Round(v * f) / f;
        }

        public static double[] RolledDamage(ItemDef def, GearTraits g)
        {
            var m = SigilBoost(g) * (1 + RollBonus("damage", g.Rolls));
            return new[] { JsMath.Round(def.Damage[0] * m), JsMath.Round(def.Damage[1] * m) };
        }

        public static double RolledProcChance(ItemDef def, GearTraits g) =>
            def.Proc != null ? Math.Min(1, def.Proc.Chance * (1 + RollBonus("proc", g.Rolls))) : 0;

        public static double RolledDefense(ItemDef def, GearTraits g, string type)
        {
            var baseDef = def.Defense != null && def.Defense.TryGetValue(type, out var v) ? v : 0;
            return JsMath.Round(baseDef * SigilBoost(g) * (1 + RollBonus("defense", g.Rolls)));
        }

        public static double SellValue(ItemDef def, GearTraits g = null)
        {
            var gr = def.IsGear && g != null ? Grade(def, g.Rolls, g.Sigil) : 0;
            var sigil = g?.Sigil != null ? B.Sigils[g.Sigil].Sell : 1;
            var plus = 1 + 0.15 * (g?.Plus ?? 0);
            return Math.Max(1, Math.Floor(def.Price * B.SellRate * (1 + gr / 100 * B.SellGradeBonus) * sigil * plus));
        }

        /// <summary>Keep only known lines as whole percents in 0-100. For loading saves.</summary>
        public static Dictionary<string, double> SanitizeRolls(ItemDef def, Dictionary<string, double> raw, string sigil = null)
        {
            var output = new Dictionary<string, double>();
            if (raw == null) return output;
            foreach (var key in RollKeys(def, sigil))
                if (raw.TryGetValue(key, out var v) && !double.IsNaN(v) && !double.IsInfinity(v)) output[key] = Math.Min(100, Math.Max(0, JsMath.Round(v)));
            return output;
        }

        public static bool IsSigil(string v) => v != null && SigilTiers.Contains(v);

        /// <summary>Sigil, mods and rolls of a saved piece, keeping only what this item can have.</summary>
        public static void SanitizeTraits(ItemDef def, GearTraits raw, GearTraits output)
        {
            var sigil = IsSigil(raw.Sigil) ? raw.Sigil : null;
            output.Rolls = SanitizeRolls(def, raw.Rolls, sigil);
            output.Sigil = sigil;
            output.Plus = raw.Plus.HasValue && raw.Plus.Value >= 1 ? Math.Min(B.Forge.MaxPlus, raw.Plus.Value) : (int?)null;
            var mods = new List<Mod>();
            foreach (var m in raw.Mods ?? new List<Mod>())
            {
                if (mods.Count >= ModSlots(def, sigil)) break;
                if (m == null || !ModPools[def.Type].Contains(m.Key) || mods.Any(x => x.Key == m.Key)) continue;
                var mod = new Mod { Key = m.Key, Roll = Math.Min(100, Math.Max(0, JsMath.Round(m.Roll))) };
                if (mod.Key == "resist")
                {
                    if (!Db.Data.Elements.Contains(m.Element)) continue;
                    mod.Element = m.Element;
                }
                mods.Add(mod);
            }
            output.Mods = mods.Count > 0 ? mods : null;
        }
    }
}
