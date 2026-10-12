import { ITEMS } from './items';
import { MONSTERS } from './monsters';
import { SKILLS } from './skills';
import { TOWNS, ZONES } from './world';
import type { ItemDef, MonsterDef, SkillDef, TownDef, ZoneDef } from './types';

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
const towns = index('town', TOWNS);

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
  town: (id: string): TownDef => get('town', towns, id),
  hasItem: (id: string) => items.has(id),
  hasSkill: (id: string) => skills.has(id),
};

/** Catch broken cross-references (e.g. a drop pointing at a deleted item) at startup. */
export function validateData(): void {
  for (const m of MONSTERS) {
    for (const d of m.drops) db.item(d.itemId);
    if (m.attacks.length === 0) throw new Error(`Monster ${m.id} has no attacks`);
  }
  for (const z of ZONES) for (const e of z.encounters) db.monster(e.monsterId);
  for (const t of TOWNS) {
    for (const npc of t.npcs) {
      if (npc.role.kind === 'shop') npc.role.stock.forEach((id) => db.item(id));
      if (npc.role.kind === 'gate') npc.role.zoneIds.forEach((id) => db.zone(id));
    }
  }
}
