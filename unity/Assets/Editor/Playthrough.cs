using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Drakepg.Rules;
using UnityEngine;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// A bot that plays the whole game on the game's own rules, like a player with no help (scripts/pace.ts gives its bot
    /// the gear and books; this one pays): it starts with a new hero's 30 g, buys gear, books and potions in town, keeps
    /// its HP and MP between fights (campfires and inns heal, potions cost gold), wakes at the inn when it loses, equips
    /// better drops, sells the rest and reads the books it finds. Quests, bosses and Guild ranks as in pace.ts. It plays
    /// carefully: heals to 80% before Tough monsters, Elites and bosses, holds the bag weapon a monster resists least,
    /// walks around a monster that beat it twice, and puts spare gold into roll stones.
    /// From a chat: <c>Drakepg.EditorTools.Playthrough.Run(20)</c> through the MCP's execute_code (edit mode is fine,
    /// ~15 s); the report goes to the editor log and the returned string. Test a data change by editing Db in the same
    /// call and restoring it in a finally (a call over ~30 s times out the MCP but keeps running and blocks the editor).
    /// </summary>
    public static class Playthrough
    {
        /// <summary>Rough seconds per action (pace.ts's guesses, plus shopping and resting).</summary>
        static class T
        {
            public const double Room = 7, Turn = 3.5, Fight = 8, Offer = 12, TurnIn = 8, Board = 15, Scene = 12, Inspect = 5, Shop = 30, Rest = 6, Wake = 10;
        }

        static readonly Dictionary<string, string[]> StatPlan = new Dictionary<string, string[]>
        {
            ["warrior"] = new[] { "str", "str", "end" },
            ["mage"] = new[] { "int", "int", "end" },
            ["rogue"] = new[] { "dex", "dex", "str" },
        };

        public class Stats
        {
            public double Minutes, Fights, Deaths, Flees, Rests, Potions, LevelIn, LevelOut, GoldOut, Gear, Books, PotionGold, Inn, Read, Lost, Stones, Avoided;
            /// <summary>The tower: best floor at the chapter's end, fights and losses there, marks earned, gold the vendor took, farming rounds when stuck.</summary>
            public double TowerBest, TowerFights, TowerLosses, Marks, VendorGold, TowerFarms;
            /// <summary>Floors that beat the bot ("F9 Ash Imp" -> losses).</summary>
            public Dictionary<string, int> TowerWalls = new Dictionary<string, int>();
            /// <summary>Who beat the hero: "Monster Lx (elite) vs hero Ly" -> times.</summary>
            public Dictionary<string, int> Killers = new Dictionary<string, int>();
            public List<string> Bosses = new List<string>();
            /// <summary>Gear and books bought, and the gear worn at the chapter's end (for `verbose`).</summary>
            public List<string> Bought = new List<string>();
            public string Kit;
            public double Sold;
        }

        class RoomState
        {
            public List<(string monster, string elite)> Monsters = new List<(string, string)>();
            public double LeftAt;
        }

        class Bot
        {
            public readonly Player P;
            readonly IRng rng;
            public string Here;
            public double Clock;
            readonly Dictionary<string, RoomState> rooms = new Dictionary<string, RoomState>();
            readonly HashSet<string> seen = new HashSet<string>();
            (string room, string monster, string elite) lostTo;
            public Stats S = new Stats();

            public Bot(Player p, IRng rng, string start)
            {
                P = p;
                this.rng = rng;
                Here = start;
            }

            // --- Walking ---

            List<string> Route(string to)
            {
                var prev = new Dictionary<string, string> { [Here] = "" };
                var queue = new Queue<string>();
                queue.Enqueue(Here);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (id == to) break;
                    var room = Db.Room(id);
                    foreach (var e in room.Exits)
                    {
                        // A guarded exit counts: walking in fights the guard first.
                        if (prev.ContainsKey(e.To) || (e.RequiresQuest != null && !Quests.Started(P, e.RequiresQuest))) continue;
                        prev[e.To] = id;
                        queue.Enqueue(e.To);
                    }
                }
                if (!prev.ContainsKey(to)) return null;
                var steps = new List<string>();
                for (var id = to; id != Here; id = prev[id]) steps.Insert(0, id);
                return steps;
            }

            int Distance(string to) => Route(to)?.Count ?? 999;

            /// <summary>Walk to `to`, one room at a time (a loss wakes you elsewhere, so the way is worked out each step).</summary>
            public void WalkTo(string to)
            {
                for (int i = 0; Here != to; i++)
                {
                    if (i > 200) throw new Exception($"Lost walking from {Here} to {to}");
                    var steps = Route(to) ?? throw new Exception($"No way from {Here} to {to}");
                    Enter(steps[0]);
                }
            }

            /// <summary>Walk without fighting (going back to rest: a player walks around the monsters).</summary>
            void SneakTo(string to)
            {
                var steps = Route(to) ?? throw new Exception($"No way from {Here} to {to}");
                foreach (var id in steps)
                {
                    Leave();
                    Here = id;
                    Clock += T.Room;
                    Travel.VisitRoom(P, id);
                }
                Arrive();
            }

            void Leave()
            {
                if (rooms.TryGetValue(Here, out var prev)) prev.LeftAt = Clock;
            }

            /// <summary>Things done in any room on arrival: rest at a campfire, shop in town.</summary>
            void Arrive()
            {
                var room = Db.Room(Here);
                if (room.Campfire != null && (P.Hp < P.MaxHp || P.Mp < P.MaxMp))
                {
                    P.RestoreFull();
                    Clock += T.Rest;
                    S.Rests++;
                }
                if (room.Npcs.Any(n => n.Role?.Kind == "shop" || n.Role?.Kind == "inn")) Town(room);
            }

            /// <summary>Step into a room: first-visit scene, rest or shop, then fight what roams there.</summary>
            public void Enter(string id, bool farming = false)
            {
                Leave();
                if (farming && rooms.TryGetValue(id, out var next)) Clock = Math.Max(Clock, next.LeftAt + Db.B.RespawnSeconds - T.Room);
                Here = id;
                Clock += T.Room;
                Travel.VisitRoom(P, id);
                var room = Db.Room(id);
                if (seen.Add(id))
                {
                    if (room.FirstVisit != null) Clock += T.Scene;
                    Clock += (room.Inspect?.Count ?? 0) * T.Inspect;
                }
                Arrive();
                var guard = Travel.IsGuard(room) ? Travel.RoomBoss(P, room) : null;
                if (guard != null && !Guard(room, guard)) return;
                if (room.ZoneId == null || (room.MonsterCount ?? 0) == 0) return;
                var zone = Db.Zone(room.ZoneId);
                if (!rooms.TryGetValue(id, out var state))
                {
                    state = new RoomState { LeftAt = Clock };
                    for (int i = 0; i < room.MonsterCount; i++) state.Monsters.Add(Spawn(zone, state));
                    rooms[id] = state;
                    if (!Chest(room, true)) return;
                }
                else
                {
                    var missing = room.MonsterCount.Value - state.Monsters.Count;
                    var back = (int)Math.Floor((Clock - state.LeftAt) / Db.B.RespawnSeconds);
                    for (int i = Math.Min(back, missing); i > 0; i--) state.Monsters.Add(Spawn(zone, state));
                    if (missing > 0 && back >= missing && !Chest(room, false)) return;
                }
                while (state.Monsters.Count > 0)
                {
                    var (monster, elite) = state.Monsters[0];
                    var def = Db.Monster(monster);
                    if (elite != null) def = Monsters.MakeElite(def, elite);
                    // Tough monsters and Elites get a full-ish bar first; trash is fought from half HP.
                    var careful = def.Tough || elite != null;
                    if (!ReadyToFight(careful ? 0.8 : 0.5)) Retreat(careful ? 0.8 : 0.5);
                    var b = Battle(def, room.ZoneId);
                    if (b.State == "victory")
                    {
                        state.Monsters.RemoveAt(0);
                        continue;
                    }
                    // Ran from it, or it won twice in a row: a player walks around that one from now on.
                    if (b.State == "fled" || (lostTo == (id, monster, elite) && b.State == "defeat"))
                    {
                        state.Monsters.RemoveAt(0);
                        S.Avoided++;
                        lostTo = default;
                    }
                    else lostTo = (id, monster, elite);
                    if (b.State == "fled") Retreat();
                    else return;
                }
            }

            (string, string) Spawn(ZoneDef zone, RoomState state)
            {
                var monsterId = rng.WeightedPick(zone.Encounters, e => e.Weight).MonsterId;
                var elites = state.Monsters.Count(m => m.elite != null);
                var elite = Features.On(P, "elites") && elites < Db.B.Elite.MaxPerRoom
                    ? Monsters.RollElite(rng, Quests.WantsElite(P) || Guild.HuntWantsElite(P))
                    : null;
                return (monsterId, elite);
            }

            /// <summary>A chest on a fresh fill (a side room's own chest the first time). False if a mimic won.</summary>
            bool Chest(RoomDef room, bool first)
            {
                if (!Features.On(P, "chests")) return true;
                var zone = Db.Zone(room.ZoneId);
                if (first && room.Chest != null)
                {
                    var rewards = Monsters.RollChestLoot(zone, rng, Features.On(P, "sigils"), Features.On(P, "materials"));
                    var book = room.Chest.Book != null ? Books.ChestBook(P, room) : null;
                    if (book != null) rewards.Items.Add(new ItemGrant { ItemId = book });
                    Gain(rewards, book != null);
                    return true;
                }
                var roll = Monsters.RollChest(rng);
                if (roll == null) return true;
                if (roll.Value && Features.On(P, "mimics"))
                {
                    if (!ReadyToFight()) Retreat();
                    return Battle(Monsters.MakeMimic(zone, rng), room.ZoneId).State == "victory";
                }
                Gain(Monsters.RollChestLoot(zone, rng, Features.On(P, "sigils"), Features.On(P, "materials")), false);
                return true;
            }

            // --- Fighting ---

            /// <summary>Fight one monster with what's in the bag and the current HP. Rewards, kills and losses are applied.</summary>
            public Battle Battle(MonsterDef def, string zoneId)
            {
                WieldAgainst(def);
                P.Data.Loadout = PickLoadout(P, def);
                var b = new Battle(P, def, rng) { ZoneId = zoneId };
                int turns = 0;
                for (; !b.IsOver && turns < 80; turns++) Act(b);
                S.Fights++;
                Clock += T.Fight + turns * T.Turn;
                switch (b.State)
                {
                    case "victory":
                        Gain(b.Rewards ?? new Rewards(), false);
                        Travel.RecordGuard(P, Here, def.Id);
                        Quests.RecordKill(P, def);
                        Guild.RecordHuntKill(P, def);
                        break;
                    case "defeat":
                        S.Deaths++;
                        var hunted = Guild.ActiveHunts(P).Any(h => !h.Ready && h.Hunt.MonsterId == def.Id);
                        var killer = $"{def.Name} L{def.Level} vs hero L{P.Level} ({P.Weapon?.Id}{(P.WeaponElement != null ? " " + P.WeaponElement : "")}){(hunted ? " hunting it" : "")}";
                        S.Killers[killer] = (S.Killers.TryGetValue(killer, out var k) ? k : 0) + 1;
                        P.RestoreFull();
                        var wake = Db.Room(Here).WakeAt ?? Db.Data.InnSpawn;
                        Leave();
                        Here = wake.RoomId;
                        Clock += T.Wake;
                        Travel.VisitRoom(P, Here);
                        Arrive();
                        break;
                    case "fled":
                        S.Flees++;
                        break;
                }
                return b;
            }

            /// <summary>pace.ts's AI: brace for wind-ups, heal or drink when low, flee a lost cause, else the best damage skill.</summary>
            void Act(Battle b)
            {
                var p = b.Player;
                var skills = p.CarriedSkills.Where(s => b.CanUseSkill(s.Id)).ToList();
                var heal = skills.FirstOrDefault(s => s.Kind == "heal");
                var hpPct = p.Hp / p.MaxHp;
                if (b.Charging != null)
                {
                    if (heal != null && hpPct < 0.6) { b.UseSkill(heal.Id); return; }
                    b.Defend();
                    return;
                }
                if (hpPct < 0.35)
                {
                    if (heal != null) { b.UseSkill(heal.Id); return; }
                    var potion = BestPotion("hp");
                    if (potion != null && b.CanUseItem(potion))
                    {
                        b.UseItem(potion);
                        S.Potions++;
                        return;
                    }
                    if (hpPct < 0.2 && b.CanFlee && b.Enemy.Hp / b.Enemy.MaxHp > 0.3) { b.Flee(); return; }
                }
                var reserve = heal != null ? 0 : p.CarriedSkills.FirstOrDefault(s => s.Kind == "heal")?.MpCost ?? 0;
                SkillDef best = null;
                var bestValue = 1.05;
                foreach (var s in skills)
                {
                    if (s.Kind != "damage" || p.Mp - s.MpCost < reserve) continue;
                    var element = s.Element == null || s.Element == "weapon" ? p.WeaponElement : s.Element;
                    var weak = b.Enemy.Def.WeakTo;
                    var value = s.Power * (s.Hits ?? 1) * (element != null ? 1 - b.ResistOf("enemy", element) / 100 : 1) * (s.Blunt ? 1 + (weak?.Blunt ?? 0) / 100 : 1);
                    var poisons = s.Applies?.Any(a => a.Status == "poison" && !a.Self) == true;
                    if (poisons && (weak?.Poison ?? 0) > 0 && !b.Statuses("enemy").Any(x => x.Def.Id == "poison")) value += 0.8 * (1 + weak.Poison.Value / 100);
                    if (value > bestValue)
                    {
                        best = s;
                        bestValue = value;
                    }
                }
                if (best != null) b.UseSkill(best.Id);
                else b.Attack();
            }

            static double Resist(MonsterDef m, string element) =>
                element == null || m.Resists == null ? 0 : (m.Resists.TryGetValue(element, out var v) ? v : 0) + (m.Resists.TryGetValue("all", out var a) ? a : 0);

            /// <summary>Before a fight (out of battle, so no swap needed): hold the bag weapon the monster resists least, if it's much better.</summary>
            void WieldAgainst(MonsterDef m)
            {
                var now = Resist(m, P.WeaponElement);
                if (now < 40) return;
                var other = P.Data.Gear.Where(g => Items.GearDef(g).IsWeapon).OrderBy(g => Resist(m, Items.GearDef(g).Element)).FirstOrDefault();
                if (other != null && Resist(m, Items.GearDef(other).Element) <= now - 40) P.Equip(other.Uid, "weapon");
            }

            /// <summary>A weapon kept in the bag for monsters that resist the one in hand: the best one of another element.</summary>
            Gear Backup()
            {
                var hand = P.WeaponElement;
                return P.Data.Gear.Where(g => Items.GearDef(g).IsWeapon && Items.GearDef(g).Element != hand)
                    .OrderByDescending(g => Items.RolledDamage(Items.GearDef(g), g).Sum()).FirstOrDefault();
            }

            /// <summary>The strongest HP (or MP) potion in the bag.</summary>
            string BestPotion(string kind) =>
                P.Data.Inventory.Select(e => Db.Item(e.ItemId))
                    .Where(d => d.Type == "consumable" && (kind == "hp" ? d.Restore?.Hp : d.Restore?.Mp) > 0)
                    .OrderByDescending(d => kind == "hp" ? d.Restore.Hp : d.Restore.Mp).FirstOrDefault()?.Id;

            /// <summary>Between fights: drink up to `want` of max HP if there's a potion; false if it still should go and rest.</summary>
            bool ReadyToFight(double want = 0.5)
            {
                while (P.Hp < P.MaxHp * want && BestPotion("hp") is string hp && P.Hp + Db.Item(hp).Restore.Hp.Value <= P.MaxHp * 1.1)
                {
                    if (P.UseConsumable(hp) == null) break;
                    S.Potions++;
                }
                if (P.Mp < P.MaxMp * 0.25 && BestPotion("mp") is string mp)
                {
                    if (P.UseConsumable(mp) != null) S.Potions++;
                }
                return P.Hp >= P.MaxHp * want;
            }

            /// <summary>Low on HP with nothing to drink: walk to the nearest campfire or inn, rest, and walk back.</summary>
            void Retreat(double want = 0.5)
            {
                if (P.Hp >= P.MaxHp * want) return;
                var back = Here;
                var spots = Db.Data.Rooms.Where(r => r.Campfire != null || (r.Npcs.Any(n => n.Role?.Kind == "inn") && P.Gold >= Db.B.InnCost))
                    .Where(r => r.Id == Here || Route(r.Id) != null).OrderBy(r => r.Id == Here ? 0 : Distance(r.Id)).ToList();
                if (spots.Count == 0) return;
                SneakTo(spots[0].Id);
                if (P.Hp < P.MaxHp * 0.95) Inn();
                SneakTo(back);
            }

            void Inn()
            {
                if (!Db.Room(Here).Npcs.Any(n => n.Role?.Kind == "inn") || P.Gold < Db.B.InnCost) return;
                P.Data.Gold -= Db.B.InnCost;
                P.RestoreFull();
                S.Inn += Db.B.InnCost;
                S.Rests++;
                Clock += T.Rest;
            }

            /// <summary>A gate guard, fought until beaten (a loss wakes you back in town). False if it gave up.</summary>
            bool Guard(RoomDef room, string monsterId)
            {
                var def = Db.Monster(monsterId);
                for (int tries = 1; tries <= 10; tries++)
                {
                    if (!ReadyToFight(0.8)) Retreat(0.8);
                    if (Battle(def, room.ZoneId).State == "victory")
                    {
                        S.Bosses.Add($"{def.Name} L{def.Level}: {tries} {(tries == 1 ? "try" : "tries")} at L{P.Level}");
                        return true;
                    }
                    if (Here != room.Id) WalkTo(room.Id);
                }
                S.Bosses.Add($"{def.Name}: gave up");
                return false;
            }

            /// <summary>A story boss: rest and stock up, then fight until beaten.</summary>
            void Boss(string roomId, string monsterId)
            {
                var def = Db.Monster(monsterId);
                for (int tries = 1; tries <= 15; tries++)
                {
                    WalkTo(roomId);
                    Retreat(0.8);
                    if (Battle(def, Db.Room(roomId).ZoneId).State == "victory")
                    {
                        S.Bosses.Add($"{def.Name} L{def.Level}: {tries} {(tries == 1 ? "try" : "tries")} at L{P.Level}");
                        return;
                    }
                }
                throw new Exception($"Stuck on {def.Name} at L{P.Level}");
            }

            // --- Loot, levels, books ---

            void Gain(Rewards rewards, bool force)
            {
                // Make room first, like a player throwing out the cheapest junk.
                while (P.FreeSlots < 2 && P.Data.Gear.Count > 0)
                {
                    var keep = Backup();
                    var junk = P.Data.Gear.Where(g => g != keep).OrderBy(g => Items.SellValue(Items.GearDef(g), g)).FirstOrDefault();
                    if (junk == null) break;
                    P.Data.Gear.Remove(junk);
                }
                var before = P.Level;
                var (_, lost) = P.GrantRewards(rewards, force);
                S.Lost += lost?.Count ?? 0;
                if (P.Level > before) LevelUp();
                ReadAll();
                EquipUpgrades();
            }

            void LevelUp()
            {
                var plan = StatPlan[P.Data.ClassId];
                for (int i = 0; P.Data.StatPoints > 0; i++) P.SpendStatPoint(plan[i % plan.Length]);
                ReadAll();
            }

            void ReadAll()
            {
                foreach (var e in P.Data.Inventory.ToList())
                    if (Db.Item(e.ItemId).Type == "book" && Books.ReadBook(P, e.ItemId) == "ok") S.Read++;
            }

            /// <summary>How strong the hero is: hit size x crit, times HP stretched by defense.</summary>
            double Score()
            {
                var r = P.AttackRange;
                var def = new[] { "melee", "pierce", "magic" }.Average(t => P.DefenseFor(t));
                return (r[0] + r[1]) / 2 * (1 + P.CritChance) * P.MaxHp * (def + 30) / 30;
            }

            /// <summary>The score with a bag piece worn in its best slot (nothing changes).</summary>
            (double score, string slot) TryOn(Gear g)
            {
                double best = 0;
                string bestSlot = null;
                foreach (var slot in Items.SlotsFor(Items.GearDef(g)))
                {
                    var equipment = new Dictionary<string, Gear>(P.Data.Equipment);
                    var bag = new List<Gear>(P.Data.Gear);
                    P.Equip(g.Uid, slot);
                    var s = Score();
                    P.Data.Equipment = equipment;
                    P.Data.Gear = bag;
                    if (s > best)
                    {
                        best = s;
                        bestSlot = slot;
                    }
                }
                return (best, bestSlot);
            }

            void EquipUpgrades()
            {
                for (bool changed = true; changed;)
                {
                    changed = false;
                    var now = Score();
                    foreach (var g in P.Data.Gear.ToList())
                    {
                        var (s, slot) = TryOn(g);
                        if (s <= now * 1.001) continue;
                        P.Equip(g.Uid, slot);
                        changed = true;
                        break;
                    }
                }
                P.ClampVitals();
            }

            // --- Town ---

            void Town(RoomDef room)
            {
                var shops = room.Npcs.Where(n => n.Role?.Kind == "shop").Select(n => n.Role).ToList();
                if (shops.Count > 0)
                {
                    Clock += T.Shop;
                    var backup = Backup();
                    foreach (var g in P.Data.Gear.ToList())
                    {
                        if (g == backup) continue;
                        g.Locked = null;
                        S.Sold += P.SellGear(g.Uid);
                    }
                    var stock = shops.SelectMany(r => Books.ShopStock(P, r)).Distinct().ToList();
                    var potion = stock.Select(Db.Item).Where(d => d.Type == "consumable" && d.Restore?.Hp > 0).OrderByDescending(d => d.Restore.Hp).FirstOrDefault();
                    var reserve = (potion?.Price ?? 0) * 3 + Db.B.InnCost * 2;
                    BuyGear(stock, reserve);
                    BuyBooks(stock, reserve);
                    // Top up potions: 4 of the best HP potion, 2 of the best MP potion for casters.
                    if (potion != null) Stock(potion, 4);
                    var mana = stock.Select(Db.Item).Where(d => d.Type == "consumable" && d.Restore?.Mp > 0).OrderByDescending(d => d.Restore.Mp).FirstOrDefault();
                    if (mana != null && P.Data.ClassId == "mage") Stock(mana, 2);
                    Reroll(stock, reserve + 300);
                }
                if (P.Hp < P.MaxHp || P.Mp < P.MaxMp) Inn();
            }

            void Stock(ItemDef potion, int want)
            {
                for (int have = P.CountItem(potion.Id); have < want && P.Gold >= potion.Price; have++)
                {
                    if (P.Buy(potion.Id) != "ok") break;
                    S.PotionGold += potion.Price;
                }
            }

            /// <summary>Buy the upgrade with the most strength per gold, again and again, keeping `reserve` for potions and inns.</summary>
            void BuyGear(List<string> stock, double reserve)
            {
                for (int i = 0; i < 20; i++)
                {
                    var now = Score();
                    ItemDef best = null;
                    double bestValue = 0;
                    foreach (var def in stock.Select(Db.Item).Where(d => d.IsGear && d.Price <= P.Gold - reserve))
                    {
                        var trial = P.MakeGear(def.Id);
                        P.Data.Gear.Add(trial);
                        var (s, _) = TryOn(trial);
                        P.Data.Gear.Remove(trial);
                        var value = (s / now - 1) / Math.Max(1, def.Price);
                        if (s > now * 1.02 && value > bestValue)
                        {
                            best = def;
                            bestValue = value;
                        }
                    }
                    if (best == null) return;
                    if (P.Buy(best.Id) != "ok") return;
                    S.Gear += best.Price;
                    S.Bought.Add(best.Id);
                    EquipUpgrades();
                    // Sell what it replaced.
                    var keep = Backup();
                    foreach (var g in P.Data.Gear.ToList()) if (g != keep) S.Sold += P.SellGear(g.Uid);
                }
            }

            /// <summary>
            /// Spare gold goes into roll stones: reroll the worn line with the lowest roll until every line is 75% or better
            /// (a reroll is a fresh roll, so it can also come out worse), keeping `reserve`.
            /// </summary>
            void Reroll(List<string> stock, double reserve)
            {
                for (int i = 0; i < 300; i++)
                {
                    var worst = Items.EquipSlots.Select(slot => P.Data.Equipment[slot]).Where(g => g != null)
                        .SelectMany(g => Stones.RerollableLines(g).Where(k => stock.Contains(Stones.StoneFor(k).Id)).Select(k => (gear: g, key: k)))
                        .OrderBy(x => x.gear.Roll(x.key)).FirstOrDefault();
                    if (worst.gear == null || worst.gear.Roll(worst.key) >= 75) return;
                    var stone = Stones.StoneFor(worst.key);
                    var need = Stones.StonesNeeded(worst.gear) - P.CountItem(stone.Id);
                    if (P.Gold - reserve < need * stone.Price) return;
                    for (int n = 0; n < need; n++)
                    {
                        if (P.Buy(stone.Id) != "ok") return;
                        S.Stones += stone.Price;
                    }
                    Stones.Reroll(P, worst.gear, worst.key, rng);
                }
            }

            /// <summary>Books the hero can read now and would carry against some monster: damage skills and heals.</summary>
            void BuyBooks(List<string> stock, double reserve)
            {
                foreach (var def in stock.Select(Db.Item).Where(d => d.Type == "book").OrderBy(d => d.Price))
                {
                    var skill = Books.BookSkill(def);
                    if (skill.Kind != "damage" && skill.Kind != "heal") continue;
                    if (Books.CanRead(P, skill) != "ok" || def.Price > P.Gold - reserve) continue;
                    if (P.Buy(def.Id) != "ok") continue;
                    S.Books += def.Price;
                    S.Bought.Add(def.Id);
                    ReadAll();
                }
            }

            // --- The tower (docs/TOWER_SPEC.md) ---

            /// <summary>
            /// Climb every open floor: three tries each (fresh HP, potions used up for real), the vendor before each floor.
            /// Stuck: farm the area's monsters (drops, gold) and try again, at most twice; then leave it for later.
            /// </summary>
            public void Climb()
            {
                if (!Features.On(P, "tower") || Tower.NextFloor(P) == null) return;
                WalkTo("spire_foot");
                int farms = 0;
                for (int guard = 0; guard < 120; guard++)
                {
                    var n = Tower.NextFloor(P);
                    if (n == null) break;
                    Vendor();
                    var won = false;
                    for (int tries = 0; tries < 3 && !won; tries++) won = TowerFight(n.Value);
                    if (won) continue;
                    if (++farms > 2) break;
                    S.TowerFarms++;
                    FarmFlight(Tower.FlightOf(n.Value));
                    WalkTo("spire_foot");
                }
                S.TowerBest = P.Data.Tower.Best;
            }

            /// <summary>One try at a floor: full HP and MP in, the HP you came with out; a first clear pays.</summary>
            bool TowerFight(int n)
            {
                var def = Tower.FloorMonster(n);
                var hp = P.Hp;
                var mp = P.Mp;
                // Top up potions for the fight like a player would (the town is next door).
                P.RestoreFull();
                WieldAgainst(def);
                P.Data.Loadout = PickLoadout(P, def);
                var b = new Battle(P, def, rng);
                int turns = 0;
                for (; !b.IsOver && turns < 80; turns++) Act(b);
                S.TowerFights++;
                Clock += T.Fight + turns * T.Turn;
                P.Data.Hp = hp;
                P.Data.Mp = mp;
                P.ClampVitals();
                if (b.State != "victory")
                {
                    S.TowerLosses++;
                    var wall = $"F{n} {def.Name}";
                    S.TowerWalls[wall] = (S.TowerWalls.TryGetValue(wall, out var w) ? w : 0) + 1;
                    return false;
                }
                var clear = Tower.ClearFloor(P, n, rng);
                if (clear != null) S.Marks += clear.Marks;
                ReadAll();
                EquipUpgrades();
                return true;
            }

            /// <summary>Farm the flight's area for gear and gold: about a dozen fights in its rooms.</summary>
            void FarmFlight(FlightDef flight)
            {
                var start = S.Fights;
                bool Wants(RoomDef room) => room.ZoneId != null && (room.MonsterCount ?? 0) > 0 && Monsters.ZoneLootTable(Db.Zone(room.ZoneId)) == flight.LootTable;
                FarmFor("tower farm", Wants, () => S.Fights - start >= 12);
                // Spend what came in: the shops on the way back.
                WalkTo("brindlemoor");
            }

            /// <summary>The Warden's vendor: set pieces that are upgrades, Sigil caches and books when marks and gold allow.</summary>
            void Vendor()
            {
                for (int i = 0; i < 12; i++)
                {
                    var now = Score();
                    VendorRow pick = null;
                    foreach (var row in Tower.VendorStock(P))
                    {
                        if (P.Data.Tower.Marks < row.Marks || P.Gold < row.Gold + 200) continue;
                        if (row.Entry.Kind == "books")
                        {
                            var skill = Books.BookSkill(Db.Item(row.ItemId));
                            if ((skill.Kind == "damage" || skill.Kind == "heal") && Books.CanRead(P, skill) == "ok") pick = row;
                        }
                        else if (row.Entry.Kind == "cache") pick ??= row;
                        else
                        {
                            // A set piece at 0% rolls as a stand-in: buy it if even that is an upgrade.
                            var trial = P.MakeGear(row.ItemId);
                            P.Data.Gear.Add(trial);
                            var (s, _) = TryOn(trial);
                            P.Data.Gear.Remove(trial);
                            if (s > now * 1.02) pick ??= row;
                        }
                    }
                    if (pick == null) return;
                    var gold = P.Gold;
                    if (Tower.BuyFromVendor(P, pick.Key, rng) != "ok") return;
                    S.VendorGold += gold - P.Gold;
                    ReadAll();
                    EquipUpgrades();
                }
            }

            // --- Quests and the Guild (pace.ts) ---

            public void Quest(QuestDef q)
            {
                if (Quests.Status(P, q) == "rank") GuildUntil(q.RequiresRank);
                var giver = Db.Data.Rooms.First(r => r.Npcs.Any(n => n.Id == q.Giver));
                WalkTo(giver.Id);
                Quests.Accept(P, q);
                Clock += T.Offer;
                var o = q.Objective;
                var bossRoom = o.Kind == "kill" && o.MonsterId != null ? Db.Data.Rooms.FirstOrDefault(r => r.Boss?.MonsterId == o.MonsterId) : null;
                if (bossRoom != null) Boss(bossRoom.Id, bossRoom.Boss.MonsterId);
                else Farm(q);
                WalkTo(giver.Id);
                var before = P.Level;
                if (Quests.TurnIn(P, q, rng) == null) throw new Exception($"Could not turn in {q.Id}");
                Clock += T.TurnIn;
                if (P.Level > before) LevelUp();
                ReadAll();
                EquipUpgrades();
            }

            void Farm(QuestDef q)
            {
                var o = q.Objective;
                bool Wants(RoomDef room)
                {
                    if (room.ZoneId == null || (room.MonsterCount ?? 0) == 0) return false;
                    var ids = Db.Zone(room.ZoneId).Encounters.Select(e => e.MonsterId).ToList();
                    if (o.Kind == "collect") return ids.Any(id => Db.Monster(id).Drops.Any(d => d.ItemId == o.ItemId));
                    return o.MonsterId == null || ids.Contains(o.MonsterId);
                }
                bool Done() => o.Kind == "collect" ? P.CountItem(o.ItemId) >= o.Count : (P.Data.Quests.TryGetValue(q.Id, out var s) ? s.Count : 0) >= o.Count;
                FarmFor(q.Id, Wants, Done);
            }

            void GuildUntil(string rank)
            {
                var clerks = Db.Data.Rooms.Where(r => r.Npcs.Any(n => n.Role?.Kind == "guild")).ToList();
                for (int round = 0; !Guild.HasRank(P, rank); round++)
                {
                    if (round > 40) throw new Exception($"Stuck at Guild rank {Guild.RankId(P.Data.Guild.Rank)}, wanting {rank}");
                    var clerk = clerks.Where(r => Route(r.Id) != null || r.Id == Here).OrderBy(r => r.Id == Here ? 0 : Distance(r.Id)).First();
                    WalkTo(clerk.Id);
                    Clock += T.Board;
                    foreach (var a in Guild.ActiveHunts(P))
                    {
                        if (!a.Ready) continue;
                        var before = P.Level;
                        Guild.TurnIn(P, a.State.Key, rng);
                        Clock += T.TurnIn;
                        if (P.Level > before) LevelUp();
                        ReadAll();
                        EquipUpgrades();
                    }
                    if (Guild.HasRank(P, rank)) break;
                    Guild.RefreshBoard(P, rng);
                    double Work(Hunt h) => h.Kind == "gather" ? h.Count / Db.B.Guild.GatherChance : h.Kind == "elite" ? 40 : h.Count;
                    var board = P.Data.Guild.Board.Select(Guild.HuntDef).Where(h => h != null)
                        .OrderByDescending(h => Guild.HuntPoints(P, h) * 20 - Work(h)).ToList();
                    foreach (var h in board) if (Guild.CanTakeHunt(P)) Guild.TakeHunt(P, h.Key);
                    foreach (var a in Guild.ActiveHunts(P))
                    {
                        var h = a.Hunt;
                        bool Wants(RoomDef room) => room.ZoneId != null && (room.MonsterCount ?? 0) > 0 &&
                            (h.Kind == "gather" ? room.ZoneId == h.ZoneId : Db.Zone(room.ZoneId).Encounters.Any(e => e.MonsterId == h.MonsterId));
                        FarmFor(h.Key, Wants, () => Guild.HuntReady(P, a.State));
                    }
                }
            }

            void FarmFor(string label, Func<RoomDef, bool> wants, Func<bool> done)
            {
                if (done()) return;
                var targets = Db.Data.Rooms.Where(r => wants(r) && (r.Id == Here || Route(r.Id) != null)).OrderBy(r => r.Id == Here ? 0 : Distance(r.Id)).ToList();
                if (targets.Count == 0) throw new Exception($"Nowhere to do {label}");
                var spot = targets[0].Id;
                for (int i = 0; !done(); i++)
                {
                    if (i > 600) throw new Exception($"Stuck farming {label} ({S.Fights} fights, {S.Deaths} lost)");
                    if (Here != spot)
                    {
                        WalkTo(spot);
                        continue;
                    }
                    // Step into a neighbour and back in, so monsters wander back.
                    var room = Db.Room(spot);
                    var other = targets.Count > 1 && Distance(targets[1].Id) <= 1 ? targets[1].Id : room.Exits.First(e => Travel.ExitOpen(P, room, e)).To;
                    Enter(other, true);
                    if (Here == other) Enter(spot, true);
                }
            }
        }

        // --- Skill choice (scripts/bookplay.ts) ---

        static double SkillValueVs(Player p, SkillDef s, MonsterDef m)
        {
            var element = s.Element == null || s.Element == "weapon" ? p.WeaponElement : s.Element;
            double R(string k) => m.Resists != null && m.Resists.TryGetValue(k, out var v) ? v : 0;
            var resist = element != null ? R(element) + R("all") : 0;
            var match = s.Family != null && element == p.WeaponElement ? 1 + Db.B.Books.Match : 1;
            var crush = s.Blunt ? 1 + (m.WeakTo?.Blunt ?? 0) / 100 : 1;
            var debuff = s.Applies?.Any(a => !a.Self) == true ? 0.3 : 0;
            return s.Power * (s.Hits ?? 1) * (1 - resist / 100) * match * crush + debuff;
        }

        static List<string> PickLoadout(Player p, MonsterDef m)
        {
            var known = p.KnownSkills;
            var heal = known.Where(s => s.Kind == "heal").OrderByDescending(s => s.Power).FirstOrDefault();
            var damage = known.Where(s => s.Kind == "damage").OrderByDescending(s => SkillValueVs(p, s, m)).Select(s => s.Id);
            return (heal != null ? new[] { heal.Id } : new string[0]).Concat(damage).Take(Db.B.SkillSlots).ToList();
        }

        // --- Running it ---

        static readonly string[] ChapterNames = { "Prologue", "Chapter 1", "Chapter 2", "Chapter 3" };

        /// <summary>Play `runs` heroes of each class from the road to the end of the story; returns the report.</summary>
        public static string Run(int runs = 5, int seed = 1234, bool verbose = false, bool climb = true)
        {
            if (!Db.Loaded) Db.Load(Resources.Load<TextAsset>("Data/game").text);
            var chapters = new List<List<QuestDef>> { new List<QuestDef>(), new List<QuestDef>() };
            foreach (var q in Db.Quests())
            {
                chapters[chapters.Count - 1].Add(q);
                if (q.EndsChapter != null) chapters.Add(new List<QuestDef>());
            }
            if (chapters[chapters.Count - 1].Count == 0) chapters.RemoveAt(chapters.Count - 1);
            var classes = new[] { "warrior", "mage", "rogue" };
            var results = new Dictionary<string, List<Stats>>();
            var errors = new List<string>();
            var rng = new Mulberry32((uint)seed);
            foreach (var classId in classes)
            {
                for (int run = 0; run < runs; run++)
                {
                    var p = Player.CreateNew("Bot", classId);
                    var bot = new Bot(p, rng, Db.Data.Start.RoomId);
                    for (int c = 0; c < chapters.Count; c++)
                    {
                        var name = c < ChapterNames.Length ? ChapterNames[c] : $"Chapter {c}";
                        var start = (clock: bot.Clock, gold: p.Gold, level: p.Level);
                        bot.S = new Stats { LevelIn = p.Level };
                        try
                        {
                            if (c == 0)
                            {
                                bot.Enter(bot.Here);
                                bot.WalkTo("brindlemoor");
                            }
                            foreach (var q in chapters[c]) bot.Quest(q);
                            if (climb) bot.Climb();
                        }
                        catch (Exception e)
                        {
                            errors.Add($"{classId} run {run + 1}, {name}: {e.Message}");
                            break;
                        }
                        finally
                        {
                            bot.S.Minutes = (bot.Clock - start.clock) / 60;
                            bot.S.LevelOut = p.Level;
                            bot.S.GoldOut = p.Gold;
                            bot.S.Kit = string.Join(" ", Items.EquipSlots.Select(slot => p.Data.Equipment[slot]).Where(g => g != null)
                                .Select(g => $"{g.ItemId}({Items.Grade(Items.GearDef(g), g.Rolls, g.Sigil):0})"));
                            var key = $"{name}|{classId}";
                            if (!results.ContainsKey(key)) results[key] = new List<Stats>();
                            results[key].Add(bot.S);
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Playthrough: {runs} heroes per class, paying their own way (Editor/Playthrough.cs). Minutes are rough.");
            for (int c = 0; c < chapters.Count; c++)
            {
                var name = c < ChapterNames.Length ? ChapterNames[c] : $"Chapter {c}";
                sb.AppendLine(name);
                foreach (var classId in classes)
                {
                    if (!results.TryGetValue($"{name}|{classId}", out var list)) continue;
                    double A(Func<Stats, double> f) => list.Average(f);
                    sb.AppendLine($"  {classId,-8} {A(s => s.Minutes),3:0} min, {A(s => s.Fights),3:0} fights, {A(s => s.Deaths):0.0} deaths, {A(s => s.Flees):0.0} fled, {A(s => s.Avoided):0.0} avoided, " +
                        $"{A(s => s.Rests):0} rests, {A(s => s.Potions):0} potions, level {A(s => s.LevelIn):0.0} -> {A(s => s.LevelOut):0.0}; " +
                        $"spent {A(s => s.Gear):0} g gear, {A(s => s.Books):0} g books, {A(s => s.PotionGold):0} g potions, {A(s => s.Stones):0} g stones, {A(s => s.Inn):0} g inn; " +
                        $"{A(s => s.Read):0.0} books read, {A(s => s.GoldOut):0} g left, {A(s => s.Lost):0.0} drops lost");
                    var bosses = list.SelectMany(s => s.Bosses).GroupBy(b => b.Split(':')[0]).Select(g => $"{g.Key}: {string.Join(", ", g.Select(b => b.Split(':')[1].Trim()))}");
                    foreach (var b in bosses) sb.AppendLine($"           {b}");
                    var killers = list.SelectMany(s => s.Killers).GroupBy(kv => kv.Key).Select(g => (who: g.Key, n: g.Sum(kv => kv.Value))).OrderByDescending(x => x.n).Take(6);
                    if (killers.Any()) sb.AppendLine($"           deaths: {string.Join(", ", killers.Select(x => $"{x.who} x{x.n}"))}");
                    if (list.Any(s => s.TowerFights > 0))
                    {
                        var bests = list.Select(s => (int)s.TowerBest).OrderBy(b => b).ToList();
                        sb.AppendLine($"           tower: best floor {bests.First()}-{bests.Last()} (median {bests[bests.Count / 2]}), {A(s => s.TowerFights):0} fights, {A(s => s.TowerLosses):0.0} lost, " +
                            $"{A(s => s.TowerFarms):0.0} farm trips, {A(s => s.Marks):0} marks, {A(s => s.VendorGold):0} g at the vendor");
                        var walls = list.SelectMany(s => s.TowerWalls).GroupBy(kv => kv.Key).Select(g => (who: g.Key, n: g.Sum(kv => kv.Value))).OrderByDescending(x => x.n).Take(6);
                        if (walls.Any()) sb.AppendLine($"           tower losses: {string.Join(", ", walls.Select(x => $"{x.who} x{x.n}"))}");
                    }
                    if (verbose)
                        foreach (var s in list)
                            sb.AppendLine($"           sold {s.Sold:0} g; bought {string.Join(" ", s.Bought)}; wearing {s.Kit}");
                }
            }
            foreach (var e in errors) sb.AppendLine("ERROR " + e);
            var report = sb.ToString();
            Debug.Log("drakepg: " + report);
            return report;
        }
    }
}
