import { CLASSES } from './classes';
import { RECIPES } from './crafting';
import { ELITE_AFFIXES } from './elites';
import { ITEMS } from './items';
import { LOOT_TABLES } from './loot';
import { MONSTERS } from './monsters';
import { QUESTS } from './quests';
import { SKILLS } from './skills';
import { STATUSES } from './statuses';
import { ROOMS, ZONES } from './world';
import type { Apply, ClassDef, ClassId, EliteAffixDef, ItemDef, LootTableDef, MonsterDef, QuestDef, RecipeDef, RoomDef, SkillDef, StatusDef, ZoneDef } from './types';

/** Build an id -> def lookup and fail loudly on duplicate ids. */
function index<T extends { id: string }>(kind: string, list: T[]): Map<string, T> {
  const map = new Map<string, T>();
  for (const entry of list) {
    if (map.has(entry.id)) throw new Error(`Duplicate ${kind} id: ${entry.id}`);
    map.set(entry.id, entry);
  }
  return map;
}

const items = index('item', ITEMS);
const skills = index('skill', SKILLS);
const monsters = index('monster', MONSTERS);
const zones = index('zone', ZONES);
const rooms = index('room', ROOMS);
const classes = index('class', CLASSES);
const quests = index('quest', QUESTS);
const statuses = index('status', STATUSES);
const affixes = index('elite affix', ELITE_AFFIXES);
const recipes = index('recipe', RECIPES);
const lootTables = index('loot table', LOOT_TABLES);

function get<T>(kind: string, map: Map<string, T>, id: string): T {
  const def = map.get(id);
  if (!def) throw new Error(`Unknown ${kind} id: ${id}`);
  return def;
}

export const db = {
  item: (id: string): ItemDef => get('item', items, id),
  skill: (id: string): SkillDef => get('skill', skills, id),
  monster: (id: string): MonsterDef => get('monster', monsters, id),
  zone: (id: string): ZoneDef => get('zone', zones, id),
  room: (id: string): RoomDef => get('room', rooms, id),
  class: (id: ClassId): ClassDef => get('class', classes, id),
  quest: (id: string): QuestDef => get('quest', quests, id),
  status: (id: string): StatusDef => get('status', statuses, id),
  affix: (id: string): EliteAffixDef => get('elite affix', affixes, id),
  recipe: (id: string): RecipeDef => get('recipe', recipes, id),
  lootTable: (id: string): LootTableDef => get('loot table', lootTables, id),
  affixes: (): readonly EliteAffixDef[] => ELITE_AFFIXES,
  hasClass: (id: string) => classes.has(id),
  quests: (): readonly QuestDef[] => QUESTS,
  hasRoom: (id: string) => rooms.has(id),
  hasItem: (id: string) => items.has(id),
  hasLootTable: (id: string) => lootTables.has(id),
  hasSkill: (id: string) => skills.has(id),
};

/** Catch broken cross-references (e.g. a drop pointing at a deleted item) at startup. */
export function validateData(): void {
  const checkApplies = (list: Apply[] | undefined) => list?.forEach((a) => db.status(a.status));
  for (const m of MONSTERS) {
    for (const d of m.drops) {
      const t = db.item(d.itemId).type;
      if (t === 'weapon' || t === 'armor') throw new Error(`Monster ${m.id}: gear ${d.itemId} belongs in a loot table, not drops`);
    }
    if (m.loot) db.lootTable(m.loot);
    if (m.attacks.length === 0) throw new Error(`Monster ${m.id} has no attacks`);
    m.attacks.forEach((a) => checkApplies(a.applies));
    m.phase?.attacks?.forEach((a) => checkApplies(a.applies));
    m.immune?.forEach((id) => db.status(id));
  }
  for (const s of SKILLS) checkApplies(s.applies);
  for (const table of LOOT_TABLES) {
    for (const entries of Object.values(table.tiers)) {
      for (const e of entries) {
        const t = db.item(e.itemId).type;
        if (t !== 'weapon' && t !== 'armor') throw new Error(`Loot table ${table.id}: ${e.itemId} is not gear`);
      }
    }
  }
  for (const rec of RECIPES) {
    const result = db.item(rec.result);
    if (result.type !== 'weapon' && result.type !== 'armor') throw new Error(`Recipe ${rec.id}: result must be gear`);
    rec.materials.forEach((m) => db.item(m.itemId));
  }
  for (const a of ELITE_AFFIXES) checkApplies(a.applies);
  for (const item of ITEMS) if (item.type === 'weapon') checkApplies(item.proc?.applies);
  for (const z of ZONES) for (const e of z.encounters) db.monster(e.monsterId);
  for (const c of CLASSES) {
    db.item(c.weapon);
    db.item(c.armor);
    c.skills.forEach((s) => db.skill(s.skillId));
  }
  const npcIds = new Set(ROOMS.flatMap((r) => r.npcs.map((n) => n.id)));
  for (const q of QUESTS) {
    if (!npcIds.has(q.giver)) throw new Error(`Quest ${q.id}: unknown giver ${q.giver}`);
    if (q.requires) db.quest(q.requires);
    if (q.objective.monsterId) db.monster(q.objective.monsterId);
    else if (!q.objective.elite) throw new Error(`Quest ${q.id}: objective needs a monsterId or elite`);
    q.rewards.items?.forEach((id) => db.item(id));
    Object.values(q.rewards.classItems ?? {}).forEach((id) => db.item(id));
  }
  for (const r of ROOMS) {
    if (r.zoneId) db.zone(r.zoneId);
    if (r.boss) {
      db.monster(r.boss.monsterId);
      db.quest(r.boss.questId);
    }
    for (const exit of r.exits) {
      db.room(exit.to);
      if (exit.requiresQuest) db.quest(exit.requiresQuest);
    }
    for (const npc of r.npcs) {
      if (npc.role.kind === 'shop') npc.role.stock.forEach((id) => db.item(id));
      if (npc.role.kind === 'forge') npc.role.recipes.forEach((id) => db.recipe(id));
    }
  }
}
