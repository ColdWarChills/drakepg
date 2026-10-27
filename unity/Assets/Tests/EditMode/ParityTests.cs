using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drakepg.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Drakepg.Tests
{
    /// <summary>
    /// The C# rules against the TypeScript ones: golden.json holds seeded fights, loot, smith and Guild results written
    /// by scripts/golden.ts (`npm run golden`). Every log line, end state and reward must match exactly.
    /// </summary>
    public class ParityTests
    {
        static JObject golden;

        [OneTimeSetUp]
        public void Load()
        {
            Db.Load(File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Data/game.json")));
            golden = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/golden.json")));
            Cheats.Reset();
        }

        static Player HeroFor(string cls, int chapter)
        {
            var p = Player.CreateNew("Golden", cls);
            if (chapter > 1) DevTools.ApplyChapterStart(p, Db.Data.ChapterStarts.First(s => s.Chapter == chapter));
            foreach (var f in new[] { "materials", "sigils", "elites", "chests" })
            {
                var unlock = Db.Feature(f).UnlockedBy;
                if (!p.Data.Quests.ContainsKey(unlock)) p.Data.Quests[unlock] = new QuestState { Status = "done", Count = 0 };
            }
            return p;
        }

        static MonsterDef MonsterFor(string name)
        {
            switch (name)
            {
                case "elite mire_toad": return Monsters.MakeElite(Db.Monster("mire_toad"), Db.Affixes()[1].Id);
                case "elite drift_wolf": return Monsters.MakeElite(Db.Monster("drift_wolf"), Db.Affixes()[3].Id);
                case "heroic thornheart": return Monsters.MakeHeroic(Db.Monster("thornheart"));
                case "heroic stormcoil": return Monsters.MakeHeroic(Db.Monster("stormcoil"));
                case "impling fen": return Monsters.MakeImpling(Db.Zone("fen"));
                default:
                    if (name.StartsWith("tower ")) return Tower.FloorMonster(int.Parse(name.Substring(6)));
                    return Db.Monster(name.StartsWith("books ") ? name.Substring(6) : name);
            }
        }

        /// <summary>The policy scripts/golden.ts plays with.</summary>
        static (string action, List<BattleEvent> events) Act(Battle b, int turn)
        {
            var p = b.Player;
            var potion = new[] { "superior_health_potion", "greater_health_potion", "health_potion" }.FirstOrDefault(b.CanUseItem);
            if (p.Hp < p.MaxHp * 0.35 && potion != null) return ("item " + potion, b.UseItem(potion));
            if (b.Charging != null) return ("defend", b.Defend());
            if (turn % 7 == 6 && b.CanFlee && !b.Enemy.Def.Boss && b.Enemy.Def.Lucky) return ("flee", b.Flee());
            var skills = p.CarriedSkills.Where(s => b.CanUseSkill(s.Id)).ToList();
            var heal = skills.FirstOrDefault(s => s.Kind == "heal");
            if (heal != null && p.Hp < p.MaxHp * 0.5) return ("skill " + heal.Id, b.UseSkill(heal.Id));
            var dmg = skills.Where(s => s.Kind != "heal").ToList();
            if (dmg.Count > 0 && turn % 3 != 2)
            {
                var s = dmg[turn % dmg.Count];
                return ("skill " + s.Id, b.UseSkill(s.Id));
            }
            return ("attack", b.Attack());
        }

        static string N(double v) => JsMath.Str(v);

        static JToken ToJson(object o) => JToken.Parse(JsonConvert.SerializeObject(o, Json.Settings));

        [Test]
        public void FightsMatchTypeScript()
        {
            int checkedFights = 0, lines = 0;
            foreach (JObject f in golden["fights"])
            {
                var name = (string)f["name"];
                var cls = (string)f["cls"];
                var seed = (int)f["seed"];
                var player = HeroFor(cls, (int)f["chapter"]);
                if (f["books"] != null && f["books"].Type != JTokenType.Null)
                {
                    var books = f["books"].Select(t => (string)t).ToList();
                    player.Data.Learned = new List<string>(books);
                    var heal = player.ClassDef.Skills.Select(s => Db.Skill(s.SkillId)).First(s => s.Kind == "heal");
                    player.Data.Loadout = new[] { heal.Id }.Concat(books).ToList();
                }
                var rng = new Mulberry32((uint)(seed * 7919 + name.Length));
                var battle = new Battle(player, MonsterFor(name), rng);
                if (f["zoneId"].Type != JTokenType.Null) battle.ZoneId = (string)f["zoneId"];
                var log = new List<string> { "intent " + battle.Intent.Name };
                for (int turn = 0; !battle.IsOver && turn < 80; turn++)
                {
                    var (action, events) = Act(battle, turn);
                    log.Add("> " + action);
                    log.AddRange(events.Select(e => e.ToString()));
                    log.Add($"intent {battle.Intent.Name} hp {N(player.Hp)}/{N(battle.Enemy.Hp)}");
                }
                var expected = f["log"].Select(t => (string)t).ToList();
                var where = $"{name} ({cls}, seed {seed})";
                for (int i = 0; i < System.Math.Min(expected.Count, log.Count); i++)
                    Assert.AreEqual(expected[i], log[i], $"{where}, line {i}:\n{string.Join("\n", expected.Skip(System.Math.Max(0, i - 6)).Take(7))}");
                Assert.AreEqual(expected.Count, log.Count, $"{where}: log length");
                Assert.AreEqual((string)f["state"], battle.State, $"{where}: state");
                if (f["rewards"].Type != JTokenType.Null)
                    Assert.IsTrue(JToken.DeepEquals(f["rewards"], ToJson(battle.Rewards)), $"{where}: rewards\nTS {f["rewards"]}\nC# {ToJson(battle.Rewards)}");
                Assert.IsTrue(JToken.DeepEquals(f["pity"], ToJson(player.Data.LootPity)), $"{where}: loot pity");
                if (f["bookPity"] != null) Assert.IsTrue(JToken.DeepEquals(f["bookPity"], ToJson(player.Data.BookPity)), $"{where}: book pity");
                checkedFights++;
                lines += log.Count;
            }
            Debug.Log($"Parity: {checkedFights} fights, {lines} log lines match the TypeScript");
        }

        [Test]
        public void LootSmithAndGuildMatchTypeScript()
        {
            var x = golden["extras"];
            var rng = new Mulberry32(424242);
            var grants = new List<ItemGrant>();
            foreach (var id in new[] { "thornheart_blade", "scale_mail", "tidepearl_ring", "stormwatch_amulet", "health_potion" })
                foreach (var src in new[] { "mob", "elite", "boss", "chest" }) grants.Add(Items.GrantFor(id, rng, src));
            Assert.IsTrue(JToken.DeepEquals(x["grants"], ToJson(grants)), $"grants\nTS {x["grants"]}\nC# {ToJson(grants)}");

            var chests = new[] { "thornwood", "fen", "frostfang", "windscour" }.Select(z => Monsters.RollChestLoot(Db.Zone(z), rng)).ToList();
            Assert.IsTrue(JToken.DeepEquals(x["chests"], ToJson(chests)), $"chests\nTS {x["chests"]}\nC# {ToJson(chests)}");

            var smith = HeroFor("warrior", 3);
            smith.Data.Gold = 100000;
            smith.AddItem("scrap", 200);
            smith.AddItem("dust", 200);
            var gear = smith.Data.Equipment["weapon"];
            var forge = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                if (i % 2 == 0) forge.Add(Forge.Reinforce(smith, gear, rng));
                else
                {
                    var r = Forge.Hone(smith, gear, i % 4 == 1 ? "damage" : "crit", rng);
                    forge.Add(r == null ? "null" : r.DriftKey == null ? $"{{\"gain\":{N(r.Gain)}}}" : $"{{\"gain\":{N(r.Gain)},\"drift\":{{\"key\":\"{r.DriftKey}\",\"loss\":{N(r.DriftLoss)}}}}}");
                }
            }
            Assert.AreEqual(string.Join("|", x["forge"].Select(t => (string)t)), string.Join("|", forge), "smith results");
            Assert.IsTrue(JToken.DeepEquals(x["forgeGear"], ToJson(gear)), $"smith gear\nTS {x["forgeGear"]}\nC# {ToJson(gear)}");

            var guild = HeroFor("rogue", 3);
            guild.Data.Visited = new List<string> { "brindlemoor", "thornwood_path", "fallen_oak", "reedwatch", "fen_road", "hearthold", "frostfang_pass" };
            Guild.RefreshBoard(guild, rng);
            var board1 = new List<string>(guild.Data.Guild.Board);
            Guild.TakeHunt(guild, board1[0]);
            Guild.TakeHunt(guild, board1[2]);
            Guild.RefreshBoard(guild, rng);
            Assert.AreEqual(string.Join(",", x["guild"]["board1"].Select(t => (string)t)), string.Join(",", board1), "first board");
            Assert.AreEqual(string.Join(",", x["guild"]["board2"].Select(t => (string)t)), string.Join(",", guild.Data.Guild.Board), "second board");

            var climber = HeroFor("mage", 3);
            climber.Data.Gold = 100000;
            var clears = new List<FloorClear>();
            for (int f = 1; f <= Tower.LastFloor; f++) clears.Add(Tower.ClearFloor(climber, f, rng));
            var buys = new[] { "set:thornwarden_helm", "cache:thornwood", "cache:frostfang", "book:dark_bolt", "set:frostward_ring", "book:dark_bolt" }
                .Select(k => Tower.BuyFromVendor(climber, k, rng)).ToList();
            var t = x["tower"];
            Assert.IsTrue(JToken.DeepEquals(t["clears"], ToJson(clears)), $"tower clears\nTS {t["clears"]}\nC# {ToJson(clears)}");
            Assert.AreEqual(string.Join(",", t["buys"].Select(v => (string)v)), string.Join(",", buys), "vendor results");
            Assert.IsTrue(JToken.DeepEquals(t["tower"], ToJson(climber.Data.Tower)), $"tower state\nTS {t["tower"]}\nC# {ToJson(climber.Data.Tower)}");
            Assert.AreEqual((double)t["gold"], climber.Data.Gold, "gold after the vendor");
            var towerGear = climber.Data.Gear.Skip(Math.Max(0, climber.Data.Gear.Count - 7)).ToList();
            Assert.IsTrue(JToken.DeepEquals(t["gear"], ToJson(towerGear)), $"tower gear\nTS {t["gear"]}\nC# {ToJson(towerGear)}");

            var reworker = HeroFor("warrior", 3);
            reworker.Data.Gold = 5000;
            var piece = reworker.Data.Equipment["weapon"];
            var steps = new List<object>();
            for (int i = 0; i < 8; i++)
            {
                var key = i % 2 == 0 ? "damage" : "crit";
                var price = Rework.Price(piece);
                var r = Rework.Run(reworker, piece, key, rng);
                if (r != null && i % 3 != 2) Rework.Keep(piece, key, r.Value.after);
                steps.Add(r != null ? (object)new { price, before = r.Value.before, after = r.Value.after } : new { price });
            }
            var rw = x["rework"];
            Assert.IsTrue(JToken.DeepEquals(rw["steps"], ToJson(steps)), $"rework steps\nTS {rw["steps"]}\nC# {ToJson(steps)}");
            Assert.IsTrue(JToken.DeepEquals(rw["gear"], ToJson(piece)), $"reworked gear\nTS {rw["gear"]}\nC# {ToJson(piece)}");
            Assert.AreEqual((double)rw["gold"], reworker.Data.Gold, "gold after reworking");
        }

        [Test]
        public void SavesRoundTrip()
        {
            var p = HeroFor("mage", 3);
            // Loading carries skills that were never announced (like the web), so announce them first.
            Features.MarkSkillsSeen(p, false);
            p.Data.Gear.Add(p.MakeGear("tidepearl_ring", Items.GrantFor("tidepearl_ring", new Mulberry32(5), "boss").Traits(), true));
            var text = SaveFile.Write(p);
            var back = SaveFile.Read(text);
            Assert.NotNull(back);
            var a = JObject.Parse(SaveFile.Write(p))["player"];
            var b = JObject.Parse(SaveFile.Write(back))["player"];
            Assert.IsTrue(JToken.DeepEquals(a, b), $"save survives a round trip\n{a}\n---\n{b}");
            Assert.AreEqual(p.MaxHp, back.MaxHp);
            Assert.AreEqual(p.Data.Equipment["weapon"].ItemId, back.Data.Equipment["weapon"].ItemId);
        }
    }
}
