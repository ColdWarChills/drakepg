import { CLASSES } from './classes';
import { ITEMS } from './items';
import { MONSTERS } from './monsters';
import { QUESTS } from './quests';
import { SKILLS } from './skills';
import { ROOMS, ZONES } from './world';
import type { ClassDef, ClassId, ItemDef, MonsterDef, QuestDef, RoomDef, SkillDef, ZoneDef } from './types';

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
  hasClass: (id: string) => classes.has(id),
  quests: (): readonly QuestDef[] => QUESTS,
  hasRoom: (id: string) => rooms.has(id),
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
  for (const c of CLASSES) {
    db.item(c.weapon);
    db.item(c.armor);
    c.skills.forEach((s) => db.skill(s.skillId));
  }
  const npcIds = new Set(ROOMS.flatMap((r) => r.npcs.map((n) => n.id)));
  for (const q of QUESTS) {
    if (!npcIds.has(q.giver)) throw new Error(`Quest ${q.id}: unknown giver ${q.giver}`);
    if (q.requires) db.quest(q.requires);
    db.monster(q.objective.monsterId);
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
    }
  }
}
