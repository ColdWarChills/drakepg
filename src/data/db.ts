import { CLASSES } from './classes';
import { RECIPES } from './crafting';
import { ELITE_AFFIXES } from './elites';
import { FEATURES } from './features';
import { ITEMS } from './items';
import { LOOT_TABLES } from './loot';
import { MAP_AREAS } from './map';
import { MONSTERS } from './monsters';
import { QUESTS } from './quests';
import { SETS } from './sets';
import { SKILLS } from './skills';
import { STATUSES } from './statuses';
import { ROOMS, ZONES } from './world';
import type { Apply, ClassDef, ClassId, EliteAffixDef, FeatureDef, ItemDef, StoneDef, LootTableDef, MonsterDef, QuestDef, RecipeDef, RoomDef, SetDef, SkillDef, StatusDef, ZoneDef } from './types';

const GEAR_TYPES: ItemDef['type'][] = ['weapon', 'armor', 'accessory'];
const isGearType = (t: ItemDef['type']) => GEAR_TYPES.includes(t);

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
const features = index('feature', FEATURES);
const sets = index('set', SETS);

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
  feature: (id: string): FeatureDef => get('feature', features, id),
  stones: (): StoneDef[] => ITEMS.filter((i): i is StoneDef => i.type === 'stone'),
  set: (id: string): SetDef => get('set', sets, id),
  features: (): readonly FeatureDef[] => FEATURES,
  affixes: (): readonly EliteAffixDef[] => ELITE_AFFIXES,
  hasClass: (id: string) => classes.has(id),
  quests: (): readonly QuestDef[] => QUESTS,
  hasRoom: (id: string) => rooms.has(id),
  hasItem: (id: string) => items.has(id),
  hasMonster: (id: string) => monsters.has(id),
  hasLootTable: (id: string) => lootTables.has(id),
  hasSkill: (id: string) => skills.has(id),
};

/** Catch broken cross-references (e.g. a drop pointing at a deleted item) at startup. */
export function validateData(): void {
  const checkApplies = (list: Apply[] | undefined) => list?.forEach((a) => db.status(a.status));
  for (const m of MONSTERS) {
    for (const d of m.drops) {
      const t = db.item(d.itemId).type;
      if (isGearType(t)) throw new Error(`Monster ${m.id}: gear ${d.itemId} belongs in a loot table, not drops`);
    }
    if (m.loot) db.lootTable(m.loot);
    if (m.attacks.length === 0) throw new Error(`Monster ${m.id} has no attacks`);
    m.attacks.forEach((a) => checkApplies(a.applies));
    m.phase?.attacks?.forEach((a) => checkApplies(a.applies));
    m.immune?.forEach((id) => db.status(id));
  }
  for (const s of SKILLS) checkApplies(s.applies);
  for (const f of FEATURES) db.quest(f.unlockedBy);
  for (const table of LOOT_TABLES) {
    for (const entries of Object.values(table.tiers)) {
      for (const e of entries) {
        const t = db.item(e.itemId).type;
        if (!isGearType(t)) throw new Error(`Loot table ${table.id}: ${e.itemId} is not gear`);
      }
    }
    if (table.set) db.set(table.set);
  }
  for (const set of SETS) {
    for (const id of set.pieces) {
      const def = db.item(id);
      if ((def.type !== 'armor' && def.type !== 'accessory') || def.set !== set.id) throw new Error(`Set ${set.id}: ${id} must be armor or an accessory with set '${set.id}'`);
    }
  }
  for (const item of ITEMS) {
    if ((item.type === 'armor' || item.type === 'accessory') && item.set && !db.set(item.set).pieces.includes(item.id)) {
      throw new Error(`Item ${item.id}: not listed in its set ${item.set}`);
    }
  }
  for (const rec of RECIPES) {
    const result = db.item(rec.result);
    if (!isGearType(result.type)) throw new Error(`Recipe ${rec.id}: result must be gear`);
    rec.materials.forEach((m) => db.item(m.itemId));
  }
  for (const a of ELITE_AFFIXES) checkApplies(a.applies);
  for (const item of ITEMS) if (item.type === 'weapon') checkApplies(item.proc?.applies);
  for (const z of ZONES) for (const e of z.encounters) db.monster(e.monsterId);
  for (const c of CLASSES) {
    db.item(c.weapon);
    const body = db.item(c.armor);
    if (body.type !== 'armor' || (body.slot ?? 'body') !== 'body') throw new Error(`Class ${c.id}: starting armor must be body armor`);
    c.skills.forEach((s) => db.skill(s.skillId));
  }
  const npcIds = new Set(ROOMS.flatMap((r) => r.npcs.map((n) => n.id)));
  for (const q of QUESTS) {
    if (!npcIds.has(q.giver)) throw new Error(`Quest ${q.id}: unknown giver ${q.giver}`);
    if (q.requires) db.quest(q.requires);
    const o = q.objective;
    if (o.kind === 'collect') {
      const t = db.item(o.itemId).type;
      if (isGearType(t)) throw new Error(`Quest ${q.id}: collect quests take potions or materials, not gear`);
    } else if (o.monsterId) db.monster(o.monsterId);
    else if (!o.elite) throw new Error(`Quest ${q.id}: objective needs a monsterId or elite`);
    q.rewards.items?.forEach((id) => db.item(id));
    Object.values(q.rewards.classItems ?? {}).forEach((id) => db.item(id));
  }
  const mapSpots = new Set<string>();
  for (const r of ROOMS) {
    if (r.zoneId) db.zone(r.zoneId);
    if (!MAP_AREAS.some((a) => a.id === r.map.area)) throw new Error(`Room ${r.id}: unknown map area ${r.map.area}`);
    const spot = `${r.map.area}:${r.map.x},${r.map.y}`;
    if (mapSpots.has(spot)) throw new Error(`Room ${r.id}: another room already sits at map spot ${spot}`);
    mapSpots.add(spot);
    if (r.boss) {
      db.monster(r.boss.monsterId);
      Object.values(r.boss.byClass ?? {}).forEach((id) => db.monster(id));
      if (r.boss.questId) db.quest(r.boss.questId);
    }
    for (const exit of r.exits) {
      db.room(exit.to);
      if (exit.guarded && (!r.boss || r.boss.questId)) throw new Error(`Room ${r.id}: guarded exit to ${exit.to} needs a gate guard (a boss with no quest)`);
      if ((exit.side === 'back' || exit.side === 'down') && exit.x === undefined) throw new Error(`Room ${r.id}: ${exit.side} exit to ${exit.to} needs an x`);
      if (exit.requiresQuest) db.quest(exit.requiresQuest);
    }
    for (const npc of r.npcs) {
      if (npc.role.kind === 'shop') npc.role.stock.forEach((id) => db.item(id));
      if (npc.role.kind === 'forge') npc.role.recipes.forEach((id) => db.recipe(id));
      if (npc.feature) db.feature(npc.feature);
    }
  }
}
