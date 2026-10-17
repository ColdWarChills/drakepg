using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Drakepg.Rules
{
    /// <summary>
    /// Save files, the JSON half of src/game/SaveSystem.ts (where they are stored is up to the game: a file in Unity).
    /// The format is the web version's: `{ version, savedAt, player }`, so a web save (version 15) loads here.
    /// </summary>
    public static class SaveFile
    {
        class File
        {
            public int Version;
            public double SavedAt;
            public PlayerData Player;
        }

        public static string Write(Player player)
        {
            var file = new File { Version = Db.Data.SaveVersion, SavedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Player = player.Data };
            return JsonConvert.SerializeObject(file, Json.Settings);
        }

        /// <summary>A player from save text, cleaned up; null if it can't be read (or is from a newer version).</summary>
        public static Player Read(string json)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<File>(json, Json.Settings);
                if (file?.Player == null) return null;
                // Older formats were only ever written by the web version, which upgrades them on load first.
                if (file.Version > Db.Data.SaveVersion || file.Version < 15) return null;
                return new Player(Sanitize(file.Player));
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        static Gear SanitizeGear(Gear raw, string slot = null)
        {
            if (raw?.ItemId == null || !Db.HasItem(raw.ItemId) || raw.Uid == null) return null;
            var def = Db.Item(raw.ItemId);
            if (!def.IsGear || (slot != null && !Items.FitsSlot(def, slot))) return null;
            var gear = new Gear { Uid = raw.Uid, ItemId = raw.ItemId };
            Items.SanitizeTraits(def, raw, gear);
            if (raw.Locked == true) gear.Locked = true;
            if (raw.IsNew == true) gear.IsNew = true;
            return gear;
        }

        /// <summary>Fill missing fields and drop references to things that no longer exist (sanitize() in SaveSystem.ts).</summary>
        public static PlayerData Sanitize(PlayerData raw)
        {
            var classId = Db.HasClass(raw.ClassId) ? raw.ClassId : "warrior";
            var fresh = Player.CreateNew("Hero", classId).Data;

            var baseStats = fresh.BaseStats.Clone();
            if (raw.BaseStats != null) foreach (var key in Stat.Keys) if (Finite(raw.BaseStats[key])) baseStats[key] = raw.BaseStats[key];

            var equipment = Player.EmptyEquipment();
            var seen = new HashSet<string>();
            foreach (var slot in Items.AllSlots)
            {
                Gear g = null;
                if (raw.Equipment != null && raw.Equipment.TryGetValue(slot, out var r)) g = SanitizeGear(r, slot);
                if (g != null && !seen.Contains(g.Uid))
                {
                    equipment[slot] = g;
                    seen.Add(g.Uid);
                }
            }
            var gear = new List<Gear>();
            foreach (var g in raw.Gear ?? new List<Gear>())
            {
                var clean = SanitizeGear(g);
                if (clean != null && !seen.Contains(clean.Uid))
                {
                    seen.Add(clean.Uid);
                    gear.Add(clean);
                }
            }
            int highest = 0;
            foreach (var uid in seen) if (uid.Length > 1 && int.TryParse(uid.Substring(1), out var n)) highest = Math.Max(highest, n);

            var quests = new Dictionary<string, QuestState>();
            foreach (var kv in raw.Quests ?? new Dictionary<string, QuestState>())
            {
                if (kv.Value == null || !Db.Quests().Any(d => d.Id == kv.Key)) continue;
                if (kv.Value.Status != "active" && kv.Value.Status != "done") continue;
                quests[kv.Key] = new QuestState { Status = kv.Value.Status, Count = Math.Max(0, kv.Value.Count) };
            }

            var data = new PlayerData
            {
                Name = string.IsNullOrEmpty(raw.Name) ? fresh.Name : raw.Name,
                Level = Math.Max(1, raw.Level),
                Xp = Math.Max(0, Finite(raw.Xp) ? raw.Xp : 0),
                Gold = Math.Max(0, Math.Floor(Finite(raw.Gold) ? raw.Gold : fresh.Gold)),
                BaseStats = baseStats,
                Hp = Finite(raw.Hp) ? raw.Hp : 1,
                Mp = Finite(raw.Mp) ? raw.Mp : 0,
                Equipment = equipment,
                Inventory = (raw.Inventory ?? new List<InventoryEntry>())
                    .Where(e => e != null && Db.HasItem(e.ItemId) && !Db.Item(e.ItemId).IsGear && e.Qty > 0)
                    .Select(e => new InventoryEntry { ItemId = e.ItemId, Qty = e.Qty }).ToList(),
                Gear = gear,
                NextUid = Math.Max(highest + 1, Math.Max(1, raw.NextUid)),
                Location = raw.Location != null && Db.HasRoom(raw.Location.RoomId) ? raw.Location.Clone() : Db.Data.TownStart.Clone(),
                ClassId = classId,
                StatPoints = Math.Max(0, raw.StatPoints),
                Quests = quests,
                LootPity = (raw.LootPity ?? new Dictionary<string, double>()).Where(kv => Db.HasLootTable(kv.Key) && Finite(kv.Value) && kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Floor(kv.Value)),
                BossGrit = (raw.BossGrit ?? new Dictionary<string, double>()).Where(kv => Db.HasMonster(kv.Key) && Db.Monster(kv.Key).Boss && Finite(kv.Value) && kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Floor(kv.Value)),
                Seen = (raw.Seen ?? new List<string>()).Where(s => s != null).ToList(),
                FreshSkills = (raw.FreshSkills ?? new List<string>()).Where(Db.HasSkill).ToList(),
                Loadout = new List<string>(),
                Visited = (raw.Visited ?? new List<string>()).Where(Db.HasRoom).Distinct().ToList(),
                LastTown = raw.LastTown != null && Db.HasRoom(raw.LastTown) && Db.Room(raw.LastTown).Town != null ? raw.LastTown : null,
                GuardsBeaten = (raw.GuardsBeaten ?? new List<string>()).Where(Db.HasRoom).Distinct().ToList(),
                Guild = Guild.Sanitize(raw.Guild),
            };

            var player = new Player(data);
            var known = new HashSet<string>(player.KnownSkills.Select(s => s.Id));
            data.Loadout = raw.Loadout != null
                ? raw.Loadout.Where(known.Contains).Distinct().Take(Db.B.SkillSlots).ToList()
                : player.DefaultLoadout();
            foreach (var s in player.KnownSkills) if (!data.Seen.Contains("skill:" + s.Id)) player.CarryNew(s.Id);
            player.ClampVitals();
            if (player.Hp <= 0) player.RestoreFull();
            return player.Data;
        }
    }
}
