using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Drakepg.Rules
{
    /// <summary>Everything in the exported game.json (`npm run export-data`).</summary>
    public class GameData
    {
        public int SaveVersion;
        public Balance Balance;
        public List<string> Elements;
        public Dictionary<string, string> ElementLabels, Opposite, ElementText, DamageTypeLabels;
        public Dictionary<string, long[]> ElementColors;
        public long[] NeutralColors;
        public Location Start, TownStart, InnSpawn;
        public List<ClassDef> Classes;
        public List<ItemDef> Items;
        public List<SkillDef> Skills;
        public List<StatusDef> Statuses;
        public List<MonsterDef> Monsters;
        public List<ZoneDef> Zones;
        public List<RoomDef> Rooms;
        public List<QuestDef> Quests;
        public List<EliteAffixDef> Affixes;
        public List<RecipeDef> Recipes;
        public List<LootTableDef> LootTables;
        public List<FeatureDef> Features;
        public List<SetDef> Sets;
        public List<MapAreaDef> MapAreas;
        public Dictionary<string, PromotionDef> Promotions;
        public List<ChapterStart> ChapterStarts;
        public List<TwistDef> Twists = new List<TwistDef>();
        public List<FloorDef> Floors = new List<FloorDef>();
        public List<FlightDef> Flights = new List<FlightDef>();
        public List<VendorDef> Vendor = new List<VendorDef>();
    }

    /// <summary>
    /// Content lookups, a port of src/data/db.ts. Call `Load` once with the JSON text (Unity: a TextAsset; tests: the
    /// file). Unknown ids throw, like the TypeScript.
    /// </summary>
    public static class Db
    {
        public static GameData Data { get; private set; }
        public static Balance B => Data.Balance;

        static Dictionary<string, ItemDef> items;
        static Dictionary<string, SkillDef> skills;
        static Dictionary<string, MonsterDef> monsters;
        static Dictionary<string, ZoneDef> zones;
        static Dictionary<string, RoomDef> rooms;
        static Dictionary<string, ClassDef> classes;
        static Dictionary<string, QuestDef> quests;
        static Dictionary<string, StatusDef> statuses;
        static Dictionary<string, EliteAffixDef> affixes;
        static Dictionary<string, RecipeDef> recipes;
        static Dictionary<string, LootTableDef> lootTables;
        static Dictionary<string, FeatureDef> features;
        static Dictionary<string, SetDef> sets;
        static Dictionary<string, TwistDef> twists;

        public static bool Loaded => Data != null;

        public static void Load(string json)
        {
            Data = JsonConvert.DeserializeObject<GameData>(json, Json.Settings);
            items = Index("item", Data.Items, x => x.Id);
            skills = Index("skill", Data.Skills, x => x.Id);
            monsters = Index("monster", Data.Monsters, x => x.Id);
            zones = Index("zone", Data.Zones, x => x.Id);
            rooms = Index("room", Data.Rooms, x => x.Id);
            classes = Index("class", Data.Classes, x => x.Id);
            quests = Index("quest", Data.Quests, x => x.Id);
            statuses = Index("status", Data.Statuses, x => x.Id);
            affixes = Index("elite affix", Data.Affixes, x => x.Id);
            recipes = Index("recipe", Data.Recipes, x => x.Id);
            lootTables = Index("loot table", Data.LootTables, x => x.Id);
            features = Index("feature", Data.Features, x => x.Id);
            sets = Index("set", Data.Sets, x => x.Id);
            twists = Index("tower twist", Data.Twists, x => x.Id);
            Guild.ClearCache();
        }

        static Dictionary<string, T> Index<T>(string kind, List<T> list, Func<T, string> id)
        {
            var map = new Dictionary<string, T>();
            foreach (var entry in list)
            {
                if (map.ContainsKey(id(entry))) throw new Exception($"Duplicate {kind} id: {id(entry)}");
                map[id(entry)] = entry;
            }
            return map;
        }

        static T Get<T>(string kind, Dictionary<string, T> map, string id) =>
            id != null && map.TryGetValue(id, out var def) ? def : throw new Exception($"Unknown {kind} id: {id}");

        public static ItemDef Item(string id) => Get("item", items, id);
        public static SkillDef Skill(string id) => Get("skill", skills, id);
        public static TwistDef Twist(string id) => Get("tower twist", twists, id);
        public static MonsterDef Monster(string id) => Get("monster", monsters, id);
        public static ZoneDef Zone(string id) => Get("zone", zones, id);
        public static RoomDef Room(string id) => Get("room", rooms, id);
        public static ClassDef Class(string id) => Get("class", classes, id);
        public static QuestDef Quest(string id) => Get("quest", quests, id);
        public static StatusDef Status(string id) => Get("status", statuses, id);
        public static EliteAffixDef Affix(string id) => Get("elite affix", affixes, id);
        public static RecipeDef Recipe(string id) => Get("recipe", recipes, id);
        public static LootTableDef LootTable(string id) => Get("loot table", lootTables, id);
        public static FeatureDef Feature(string id) => Get("feature", features, id);
        public static SetDef Set(string id) => Get("set", sets, id);
        public static List<FeatureDef> Features() => Data.Features;
        public static List<EliteAffixDef> Affixes() => Data.Affixes;
        public static List<QuestDef> Quests() => Data.Quests;
        public static bool HasClass(string id) => id != null && classes.ContainsKey(id);
        public static bool HasRoom(string id) => id != null && rooms.ContainsKey(id);
        public static bool HasItem(string id) => id != null && items.ContainsKey(id);
        public static bool HasMonster(string id) => id != null && monsters.ContainsKey(id);
        public static bool HasLootTable(string id) => id != null && lootTables.ContainsKey(id);
        public static bool HasSkill(string id) => id != null && skills.ContainsKey(id);
    }

    /// <summary>JSON settings shared by the data and saves: camelCase out, nulls left out (like JSON.stringify).</summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new WholeNumberConverter() },
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver
            {
                // Dictionary keys (item ids, slots, elements) stay as written.
                NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy { ProcessDictionaryKeys = false },
            },
        };
    }

    /// <summary>Writes whole doubles as integers (`5`, not `5.0`), like JSON.stringify, so saves stay web-compatible.</summary>
    public class WholeNumberConverter : JsonConverter
    {
        public override bool CanRead => false;
        public override bool CanConvert(Type objectType) => objectType == typeof(double) || objectType == typeof(double?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
            throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            var v = (double)value;
            if (Math.Abs(v) < 1e15 && v == Math.Floor(v)) writer.WriteValue((long)v);
            else writer.WriteValue(v);
        }
    }

    /// <summary>TS `string | string[]` (and `BareLook | BareLook[]`): always a list in C#.</summary>
    public class OneOrManyConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<T>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.Array) return token.ToObject<List<T>>(serializer);
            return new List<T> { token.ToObject<T>(serializer) };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = (List<T>)value;
            if (list.Count == 1) serializer.Serialize(writer, list[0]);
            else serializer.Serialize(writer, list);
        }
    }
}
