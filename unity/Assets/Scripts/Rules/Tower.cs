using System;
using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>The tower on a hero (save v17): highest floor cleared and marks.</summary>
    public class TowerState
    {
        public int Best;
        public int Marks;
    }

    /// <summary>A first clear's pay.</summary>
    public class FloorClear
    {
        public int Floor, Marks;
        public List<ItemGrant> Items = new List<ItemGrant>();
        public List<ItemGrant> Lost = new List<ItemGrant>();
    }

    /// <summary>One thing the vendor sells: a set piece, a Sigil cache or a book.</summary>
    public class VendorRow
    {
        public string Key;
        public VendorDef Entry;
        /// <summary>The item you get (a cache has none: its weapon is random).</summary>
        public string ItemId;
        public int Marks;
        public double Gold;
    }

    /// <summary>
    /// The tower (docs/TOWER_SPEC.md, data in src/data/tower.ts), a port of src/game/Tower.ts: floors cleared in order,
    /// each one fight against a known monster moved up to the floor's level, with twists. First clears pay marks (more
    /// on 5th and 10th floors, and a Sigil piece on 10th); marks and gold buy from the Warden's vendor.
    /// </summary>
    public static class Tower
    {
        static TowerTuning T => Db.B.Tower;

        public static int LastFloor => Db.Data.Floors.Count;

        public static FloorDef FloorDef(int n) =>
            n >= 1 && n <= Db.Data.Floors.Count ? Db.Data.Floors[n - 1] : throw new Exception($"Unknown tower floor: {n}");

        public static FlightDef FlightOf(int n) =>
            Db.Data.Flights.FirstOrDefault(x => n >= x.First && n <= x.Last) ?? throw new Exception($"Tower floor {n} is in no flight");

        /// <summary>A flight opens with the tower feature and its quest done.</summary>
        public static bool FlightOpen(Player player, FlightDef flight) => Features.On(player, "tower") && Quests.Done(player, flight.OpensAfter);

        /// <summary>Cleared floors (to fight again) and the next one, if its flight is open.</summary>
        public static bool FloorOpen(Player player, int n) =>
            n >= 1 && n <= LastFloor && n <= player.Data.Tower.Best + 1 && FlightOpen(player, FlightOf(n));

        /// <summary>The floor to climb next, or null when it's locked or the top is cleared.</summary>
        public static int? NextFloor(Player player)
        {
            var n = player.Data.Tower.Best + 1;
            return n <= LastFloor && FloorOpen(player, n) ? n : (int?)null;
        }

        /// <summary>First-clear marks: the flight's rate, x2 on a 5th floor, x3 on a 10th.</summary>
        public static int MarksFor(int n)
        {
            var m = T.Milestone;
            return FlightOf(n).Marks * (n % 10 == 0 ? m.Tenth : n % 5 == 0 ? m.Fifth : 1);
        }

        static double CurveAt(double[] curve, int level) => curve[Math.Max(0, Math.Min(curve.Length - 1, level - 1))];

        static double[] Range(double[] r, double m) => new[] { JsMath.Round(r[0] * m), JsMath.Round(r[1] * m) };

        /// <summary>The floor's monster: moved up to the floor's level (Heroic for bosses), twists applied, no rewards of its own.</summary>
        public static MonsterDef FloorMonster(int n)
        {
            var f = FloorDef(n);
            var def = f.Heroic ? Monsters.MakeHeroic(Db.Monster(f.MonsterId)) : Db.Monster(f.MonsterId);
            var up = Math.Max(0, f.Level - def.Level);
            var defense = new Dictionary<string, double>();
            foreach (var kv in def.Defense) defense[kv.Key] = JsMath.Round(kv.Value + T.DefensePerLevel * up);
            var hp = CurveAt(T.Curve.Hp, f.Level) / CurveAt(T.Curve.Hp, def.Level);
            var damage = CurveAt(T.Curve.Damage, f.Level) / CurveAt(T.Curve.Damage, def.Level);
            def = def.Clone();
            def.Level = f.Level;
            def.Hp = JsMath.Round(def.Hp * hp);
            def.Damage = Range(def.Damage, damage);
            def.Defense = defense;
            var tower = new TowerFight { Floor = n, Twists = new List<string>(f.Twists) };
            foreach (var id in f.Twists)
            {
                var tw = Db.Twist(id);
                if (tw.Affix != null) def = Monsters.ApplyAffix(def, tw.Affix);
                if (tw.Unshakable && !(def.Immune ?? new List<string>()).Contains("stun"))
                {
                    def = def.Clone();
                    def.Immune = new List<string>(def.Immune ?? new List<string>()) { "stun" };
                }
                if (tw.Veil != null)
                {
                    var resists = def.Resists != null ? new Dictionary<string, double>(def.Resists) : new Dictionary<string, double>();
                    foreach (var e in Db.Data.Elements)
                    {
                        var r = resists.TryGetValue(e, out var v) ? v : 0;
                        resists[e] = e == tw.Veil ? r - T.Veil.Down : Math.Min(100, r + T.Veil.Up);
                    }
                    def = def.Clone();
                    def.Resists = resists;
                }
                if (tw.Shell != null)
                {
                    def = def.Clone();
                    def.Shell = new MonsterShell { Name = tw.Name, Element = tw.Shell.Element, Hits = tw.Shell.Hits, Reduction = T.Shell.Reduction, ReformAt = T.Shell.ReformAt };
                }
                if (tw.Starts != null) tower.Starts = (tower.Starts ?? new List<Apply>()).Concat(tw.Starts).ToList();
                if (tw.ShortFuse) tower.EnrageAt = T.ShortFuse;
                if (tw.NoItems) tower.NoItems = true;
            }
            def = def.Clone();
            def.Xp = 0;
            def.Gold = new double[] { 0, 0 };
            def.Drops = new List<Drop>();
            def.Loot = null;
            def.Tower = tower;
            return def;
        }

        /// <summary>Record a won floor. Only the next floor counts (a cleared one is practice: null).</summary>
        public static FloorClear ClearFloor(Player player, int n, IRng rng)
        {
            var tower = player.Data.Tower;
            if (n != tower.Best + 1) return null;
            tower.Best = n;
            var marks = MarksFor(n);
            tower.Marks += marks;
            var items = new List<ItemGrant>();
            if (n % 10 == 0)
            {
                var fl = FlightOf(n);
                items.Add(SigilPiece(fl.LootTable, fl.Sigil, rng, false));
            }
            var (_, lost) = player.GrantRewards(new Rewards { Xp = 0, Gold = 0, Items = items }, true);
            return new FloorClear { Floor = n, Marks = marks, Items = items, Lost = lost };
        }

        static readonly string[] Tiers = { "common", "uncommon", "rare" };

        /// <summary>A random piece of a loot table (any tier, or weapons only) with a set Sigil, rolled like a drop.</summary>
        public static ItemGrant SigilPiece(string tableId, string sigil, IRng rng, bool weaponsOnly)
        {
            var table = Db.LootTable(tableId);
            var ids = new List<string>();
            foreach (var tier in Tiers)
            {
                if (!table.Tiers.TryGetValue(tier, out var entries)) continue;
                foreach (var e in entries)
                {
                    if (ids.Contains(e.ItemId)) continue;
                    if (weaponsOnly && Db.Item(e.ItemId).Type != "weapon") continue;
                    ids.Add(e.ItemId);
                }
            }
            var itemId = ids[rng.RandInt(0, ids.Count - 1)];
            var def = Db.Item(itemId);
            if (!def.IsGear) return new ItemGrant { ItemId = itemId };
            var grant = new ItemGrant { ItemId = itemId, Rolls = Items.RollGear(def, rng, null, sigil), Sigil = sigil };
            var mods = Items.ModSlots(def, sigil);
            if (mods > 0) grant.Mods = Items.RollMods(def, mods, rng);
            return grant;
        }

        /// <summary>What the vendor shows this hero: rows whose floor is cleared; books your class can read and doesn't know or hold.</summary>
        public static List<VendorRow> VendorStock(Player player)
        {
            var rows = new List<VendorRow>();
            var best = player.Data.Tower.Best;
            foreach (var v in Db.Data.Vendor)
            {
                if (best < v.MinFloor) continue;
                if (v.Kind == "set" && v.Set != null)
                {
                    foreach (var piece in Db.Set(v.Set).Pieces) rows.Add(new VendorRow { Key = "set:" + piece, Entry = v, ItemId = piece, Marks = v.Marks, Gold = v.Gold });
                }
                else if (v.Kind == "cache" && v.LootTable != null)
                {
                    rows.Add(new VendorRow { Key = "cache:" + v.LootTable, Entry = v, Marks = v.Marks, Gold = v.Gold });
                }
                else if (v.Kind == "books")
                {
                    foreach (var e in v.Elements ?? new List<string>())
                    {
                        foreach (var skillId in Books.ElementBooks(player.Data.ClassId, e))
                        {
                            if (Books.KnowsOrHolds(player, skillId)) continue;
                            var itemId = Books.BookId(skillId);
                            rows.Add(new VendorRow { Key = "book:" + skillId, Entry = v, ItemId = itemId, Marks = v.Marks, Gold = v.Gold + Db.Item(itemId).Price });
                        }
                    }
                }
            }
            return rows;
        }

        /// <summary>Buy a vendor row: ok, marks, gold, full or gone. Set pieces are rolled like a boss drop.</summary>
        public static string BuyFromVendor(Player player, string key, IRng rng)
        {
            var row = VendorStock(player).FirstOrDefault(r => r.Key == key);
            if (row == null) return "gone";
            var tower = player.Data.Tower;
            if (tower.Marks < row.Marks) return "marks";
            if (player.Gold < row.Gold) return "gold";
            if (player.FreeSlots == 0) return "full";
            var v = row.Entry;
            var grant = v.Kind == "cache" && v.LootTable != null && v.Sigil != null ? SigilPiece(v.LootTable, v.Sigil, rng, true)
                : v.Kind == "set" ? Items.GrantFor(row.ItemId, rng, "boss")
                : new ItemGrant { ItemId = row.ItemId };
            if (!player.Receive(grant)) return "full";
            tower.Marks -= row.Marks;
            player.Data.Gold -= row.Gold;
            return "ok";
        }
    }
}
