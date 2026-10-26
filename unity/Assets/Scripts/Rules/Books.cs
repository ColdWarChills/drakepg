using System.Collections.Generic;
using System.Linq;

namespace Drakepg.Rules
{
    /// <summary>
    /// Skill books (docs/BUILD_VARIETY_SPEC.md), a port of src/game/Books.ts: levels teach each class its core skills,
    /// every other skill is a book. Towns sell element books, story bosses teach class skills, Elites, guild promotions,
    /// Heroic wins and sure chests are extras. A book is used up when read.
    /// </summary>
    public static class Books
    {
        static Balance B => Db.B;

        public static string BookId(string skillId) => "book_" + skillId;

        /// <summary>The skill a book teaches, or null for any other item.</summary>
        public static SkillDef BookSkill(ItemDef def) => def.Type == "book" ? Db.Skill(def.SkillId) : null;

        public static int ReadLevelOf(SkillDef skill) => skill.ReadLevel ?? 1;

        /// <summary>Class books teach only their class; generic ones anyone.</summary>
        public static bool ReadableBy(SkillDef skill, string classId) => skill.ClassId == null || skill.ClassId == classId;

        /// <summary>ok, class (another class's book), level (too low yet) or known.</summary>
        public static string CanRead(Player player, SkillDef skill)
        {
            if (!ReadableBy(skill, player.Data.ClassId)) return "class";
            if (player.KnowsSkill(skill.Id)) return "known";
            if (player.Level < ReadLevelOf(skill)) return "level";
            return "ok";
        }

        /// <summary>Read a book from the bag: learn its skill and use the book up. Returns what CanRead said.</summary>
        public static string ReadBook(Player player, string itemId)
        {
            var skill = BookSkill(Db.Item(itemId));
            if (skill == null || player.CountItem(itemId) == 0) return "class";
            var check = CanRead(player, skill);
            if (check != "ok") return check;
            player.RemoveItem(itemId);
            player.Data.Learned.Add(skill.Id);
            return "ok";
        }

        /// <summary>Known, or its book is in the bag: drops and chests skip it.</summary>
        public static bool KnowsOrHolds(Player player, string skillId) => player.KnowsSkill(skillId) || player.CountItem(BookId(skillId)) > 0;

        /// <summary>The family book and the Ward of an element that this class can read (skill ids).</summary>
        public static List<string> ElementBooks(string classId, string element) =>
            new[] { element + "_" + Db.Class(classId).Family, element + "_ward" }
                .Where(id => Db.HasSkill(id) && Db.HasItem(BookId(id))).ToList();

        /// <summary>What a shop shows this hero: its stock plus its elements' books, minus books the class can't read or knows.</summary>
        public static List<string> ShopStock(Player player, NpcRole role)
        {
            var books = (role.Books ?? new List<string>()).SelectMany(e => ElementBooks(player.Data.ClassId, e)).Select(BookId);
            return role.Stock.Concat(books).Where(id =>
            {
                var skill = BookSkill(Db.Item(id));
                return skill == null || (ReadableBy(skill, player.Data.ClassId) && !player.KnowsSkill(skill.Id));
            }).ToList();
        }

        /// <summary>
        /// An Elite's book: one of its area's element books you neither know nor hold. Balance.Books.Drop.Chance per Elite,
        /// sure on the SureAfter-th Elite without one (PlayerData.BookPity, per loot table). Null when nothing is missing.
        /// </summary>
        public static string EliteBook(Player player, string tableId, IRng rng)
        {
            var elements = Db.LootTable(tableId).Books ?? new List<string>();
            var missing = elements.SelectMany(e => ElementBooks(player.Data.ClassId, e)).Where(id => !KnowsOrHolds(player, id)).ToList();
            if (missing.Count == 0) return null;
            var pity = player.Data.BookPity;
            var d = B.Books.Drop;
            var dry = pity.TryGetValue(tableId, out var n) ? n : 0;
            if (dry + 1 >= d.SureAfter || rng.Chance(Cheats.Boosted(d.Chance)))
            {
                pity.Remove(tableId);
                return BookId(missing[rng.RandInt(0, missing.Count - 1)]);
            }
            pity[tableId] = dry + 1;
            return null;
        }

        /// <summary>A side room's sure chest may hold a book (RoomDef.Chest.Book) until you know or hold it.</summary>
        public static string ChestBook(Player player, RoomDef room)
        {
            var id = room.Chest?.Book;
            return id != null && !KnowsOrHolds(player, id) ? BookId(id) : null;
        }

        /// <summary>The first Heroic win's book (ClassDef.HeroicBook), until you know or hold it.</summary>
        public static string HeroicBook(Player player)
        {
            var id = player.ClassDef.HeroicBook;
            return id != null && !KnowsOrHolds(player, id) ? BookId(id) : null;
        }

        // --- Spellbook ---

        public class Entry
        {
            public SkillDef Skill;
            public bool Known;
            /// <summary>level (a level skill), class (a class book), family (an element family book) or generic.</summary>
            public string Group;
            /// <summary>Level skills: the level that teaches it. Books: the level to read it.</summary>
            public int Level;
            /// <summary>Where to get it, the likeliest first.</summary>
            public List<string> Sources;
        }

        /// <summary>Every skill this class can get, in spellbook order: level skills, class books, the element family, generic books.</summary>
        public static List<Entry> Spellbook(Player player)
        {
            var cls = player.ClassDef;
            Entry Make(SkillDef skill, string group, int level) => new Entry
            {
                Skill = skill,
                Known = player.KnowsSkill(skill.Id),
                Group = group,
                Level = level,
                Sources = group == "level" ? new List<string> { $"Level {level}" } : BookSources(skill.Id),
            };
            var levelIds = new HashSet<string>(cls.Skills.Select(s => s.SkillId));
            var list = new List<Entry>();
            list.AddRange(cls.Skills.Select(s => Make(Db.Skill(s.SkillId), "level", s.Level)));
            list.AddRange(cls.Books.Select(id => Make(Db.Skill(id), "class", ReadLevelOf(Db.Skill(id)))));
            list.AddRange(Db.Data.Elements.Select(e => Db.Skill(e + "_" + cls.Family)).Where(s => !levelIds.Contains(s.Id))
                .Select(s => Make(s, "family", ReadLevelOf(s))));
            list.AddRange(Db.Data.Skills.Where(s => s.ClassId == null).Select(s => Make(s, "generic", ReadLevelOf(s))));
            return list;
        }

        static Dictionary<string, List<string>> sourceIndex;
        static GameData indexedData;

        /// <summary>Where a skill's book comes from, read from the data (shops, quests, promotions, Heroic, chests, Elites).</summary>
        public static List<string> BookSources(string skillId)
        {
            if (sourceIndex == null || indexedData != Db.Data) BuildSources();
            if (sourceIndex.TryGetValue(skillId, out var found) && found.Count > 0) return found;
            return new List<string> { ReadLevelOf(Db.Skill(skillId)) >= 20 ? "Class quest at level 20 (later)" : "Later in the story" };
        }

        static void BuildSources()
        {
            var output = new Dictionary<string, List<string>>();
            void Add(string skillId, string text)
            {
                if (!output.TryGetValue(skillId, out var list)) output[skillId] = list = new List<string>();
                if (!list.Contains(text)) list.Add(text);
            }
            IEnumerable<string> FamilyAndWard(string e) => Db.Data.Classes.Select(c => e + "_" + c.Family).Append(e + "_ward");
            foreach (var room in Db.Data.Rooms)
            {
                foreach (var npc in room.Npcs)
                {
                    if (npc.Role?.Kind != "shop") continue;
                    foreach (var id in npc.Role.Stock)
                    {
                        var def = Db.Item(id);
                        if (def.Type == "book") Add(def.SkillId, $"Sold in {room.Name}");
                    }
                    foreach (var e in npc.Role.Books ?? new List<string>())
                        foreach (var id in FamilyAndWard(e)) Add(id, $"Sold in {room.Name}");
                }
            }
            foreach (var q in Db.Data.Quests)
            {
                if (q.Objective.Kind != "kill" || q.Objective.MonsterId == null) continue;
                var name = Db.Monster(q.Objective.MonsterId).Name;
                foreach (var id in (q.Rewards.ClassBooks ?? new Dictionary<string, string>()).Values) Add(id, $"{name}'s first defeat");
            }
            foreach (var kv in Db.Data.Promotions)
                foreach (var id in (kv.Value?.ClassBooks ?? new Dictionary<string, string>()).Values) Add(id, $"Guild promotion to Rank {kv.Key}");
            foreach (var c in Db.Data.Classes)
                if (c.HeroicBook != null) Add(c.HeroicBook, "First Heroic boss win");
            foreach (var room in Db.Data.Rooms)
                if (room.Chest?.Book != null) Add(room.Chest.Book, $"The chest in {room.Name}");
            foreach (var t in Db.Data.LootTables)
            {
                var area = Db.Data.MapAreas.FirstOrDefault(a => a.Id == t.Id)?.Name ?? t.Id;
                foreach (var e in t.Books ?? new List<string>())
                    foreach (var id in FamilyAndWard(e)) Add(id, $"Elites in {area}");
            }
            sourceIndex = output;
            indexedData = Db.Data;
        }
    }
}
