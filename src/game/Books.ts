import { BALANCE } from '../config';
import { CLASSES } from '../data/classes';
import { db } from '../data/db';
import { ELEMENTS } from '../data/elements';
import { PROMOTIONS } from '../data/guild';
import { LOOT_TABLES } from '../data/loot';
import { MAP_AREAS } from '../data/map';
import { QUESTS } from '../data/quests';
import type { BookDef, ClassId, Element, ItemDef, NpcRole, RoomDef, SkillDef } from '../data/types';
import { ROOMS } from '../data/world';
import { boosted } from './cheats';
import type { Player } from './Player';
import { chance, randInt, type Rng } from './rng';

/**
 * Skill books (docs/BUILD_VARIETY_SPEC.md): levels teach each class its core skills, every other skill is a book. Towns
 * sell element books, story bosses teach class skills, Elites, guild promotions, Heroic wins and sure chests are extras.
 * A book is used up when read.
 */

export function bookId(skillId: string): string {
  return `book_${skillId}`;
}

/** The skill a book teaches, or null for any other item. */
export function bookSkill(def: ItemDef): SkillDef | null {
  return def.type === 'book' ? db.skill((def as BookDef).skillId) : null;
}

export function readLevelOf(skill: SkillDef): number {
  return skill.readLevel ?? 1;
}

/** Can this class read the skill's book? (Class books teach only their class; generic ones anyone.) */
export function readableBy(skill: SkillDef, classId: ClassId): boolean {
  return skill.classId === undefined || skill.classId === classId;
}

/** 'class': another class's book. 'level': too low to read it yet. 'known': already learned. */
export type ReadCheck = 'ok' | 'class' | 'level' | 'known';

export function canRead(player: Player, skill: SkillDef): ReadCheck {
  if (!readableBy(skill, player.data.classId)) return 'class';
  if (player.knowsSkill(skill.id)) return 'known';
  if (player.level < readLevelOf(skill)) return 'level';
  return 'ok';
}

/** Read a book from the bag: learn its skill (into a free battle slot) and use the book up. */
export function readBook(player: Player, itemId: string): ReadCheck {
  const skill = bookSkill(db.item(itemId));
  if (!skill || player.countItem(itemId) === 0) return 'class';
  const check = canRead(player, skill);
  if (check !== 'ok') return check;
  player.removeItem(itemId);
  player.data.learned.push(skill.id);
  return 'ok';
}

/** Known, or its book is in the bag: drops and chests skip it. */
export function knowsOrHolds(player: Player, skillId: string): boolean {
  return player.knowsSkill(skillId) || player.countItem(bookId(skillId)) > 0;
}

/** The family book and the Ward of an element that this class can read. */
export function elementBooks(classId: ClassId, element: Element): string[] {
  return [`${element}_${db.class(classId).family}`, `${element}_ward`].filter((id) => db.hasSkill(id) && db.hasItem(bookId(id)));
}

/** What a shop shows this hero: its stock, plus its elements' books; books the class can't read or already knows are left out. */
export function shopStock(player: Player, role: Extract<NpcRole, { kind: 'shop' }>): string[] {
  const books = (role.books ?? []).flatMap((e) => elementBooks(player.data.classId, e)).map(bookId);
  return [...role.stock, ...books].filter((id) => {
    const skill = bookSkill(db.item(id));
    return !skill || (readableBy(skill, player.data.classId) && !player.knowsSkill(skill.id));
  });
}

/**
 * An Elite's book: one of its area's element books you neither know nor hold. `BALANCE.books.drop.chance` per Elite,
 * sure on the `sureAfter`th Elite without one (`PlayerData.bookPity`, per loot table). Null when nothing is missing.
 */
export function eliteBook(player: Player, tableId: string, rng: Rng): string | null {
  const elements = db.lootTable(tableId).books ?? [];
  const missing = elements.flatMap((e) => elementBooks(player.data.classId, e)).filter((id) => !knowsOrHolds(player, id));
  if (!missing.length) return null;
  const pity = player.data.bookPity;
  const d = BALANCE.books.drop;
  if ((pity[tableId] ?? 0) + 1 >= d.sureAfter || chance(rng, boosted(d.chance))) {
    delete pity[tableId];
    return bookId(missing[randInt(rng, 0, missing.length - 1)]);
  }
  pity[tableId] = (pity[tableId] ?? 0) + 1;
  return null;
}

/** A side room's sure chest may hold a book (`RoomDef.chest.book`) until you know or hold it. */
export function chestBook(player: Player, room: RoomDef): string | null {
  const id = room.chest?.book;
  return id && !knowsOrHolds(player, id) ? bookId(id) : null;
}

/** The first Heroic win's book (`ClassDef.heroicBook`), until you know or hold it. */
export function heroicBook(player: Player): string | null {
  const id = player.classDef.heroicBook;
  return id && !knowsOrHolds(player, id) ? bookId(id) : null;
}

// --- Spellbook ---

export interface SpellbookEntry {
  skill: SkillDef;
  known: boolean;
  /** 'level': a level skill; 'class': a class book; 'family': an element family book; 'generic': any class. */
  group: 'level' | 'class' | 'family' | 'generic';
  /** Level skills: the level that teaches it. Books: the level to read it. */
  level: number;
  /** Where to get it, first the likeliest ("Level 9", "Sold in Brindlemoor", "The Thornheart's first defeat"...). */
  sources: string[];
}

/** Every skill this class can get, in spellbook order: level skills, class books, the element family, generic books. */
export function spellbook(player: Player): SpellbookEntry[] {
  const cls = player.classDef;
  const entry = (skill: SkillDef, group: SpellbookEntry['group'], level: number): SpellbookEntry => ({
    skill,
    known: player.knowsSkill(skill.id),
    group,
    level,
    sources: group === 'level' ? [`Level ${level}`] : bookSources(skill.id),
  });
  const levelIds = new Set(cls.skills.map((s) => s.skillId));
  return [
    ...cls.skills.map((s) => entry(db.skill(s.skillId), 'level', s.level)),
    ...cls.books.map((id) => entry(db.skill(id), 'class', readLevelOf(db.skill(id)))),
    ...ELEMENTS.map((e) => db.skill(`${e}_${cls.family}`))
      .filter((s) => !levelIds.has(s.id))
      .map((s) => entry(s, 'family', readLevelOf(s))),
    ...db
      .skills()
      .filter((s) => s.classId === undefined)
      .map((s) => entry(s, 'generic', readLevelOf(s))),
  ];
}

let sourceIndex: Map<string, string[]> | null = null;

/** Where a skill's book comes from, read from the data (shops, quests, promotions, Heroic, chests, Elites). */
export function bookSources(skillId: string): string[] {
  if (!sourceIndex) sourceIndex = buildSources();
  const found = sourceIndex.get(skillId);
  if (found?.length) return found;
  return readLevelOf(db.skill(skillId)) >= 20 ? ['Class quest at level 20 (later)'] : ['Later in the story'];
}

function buildSources(): Map<string, string[]> {
  const out = new Map<string, string[]>();
  const add = (skillId: string, text: string) => {
    const list = out.get(skillId) ?? [];
    if (!list.includes(text)) list.push(text);
    out.set(skillId, list);
  };
  const familyAndWard = (e: Element) => [...CLASSES.map((c) => `${e}_${c.family}`), `${e}_ward`];
  for (const room of ROOMS) {
    for (const npc of room.npcs) {
      if (npc.role.kind !== 'shop') continue;
      for (const id of npc.role.stock) {
        const def = db.item(id);
        if (def.type === 'book') add(def.skillId, `Sold in ${room.name}`);
      }
      for (const e of npc.role.books ?? []) familyAndWard(e).forEach((id) => add(id, `Sold in ${room.name}`));
    }
  }
  for (const q of QUESTS) {
    if (q.objective.kind !== 'kill' || !q.objective.monsterId) continue;
    const name = db.monster(q.objective.monsterId).name;
    for (const id of Object.values(q.rewards.classBooks ?? {})) add(id, `${name}'s first defeat`);
  }
  for (const [rank, p] of Object.entries(PROMOTIONS)) {
    for (const id of Object.values(p?.classBooks ?? {})) add(id, `Guild promotion to Rank ${rank}`);
  }
  for (const c of CLASSES) if (c.heroicBook) add(c.heroicBook, 'First Heroic boss win');
  for (const room of ROOMS) if (room.chest?.book) add(room.chest.book, `The chest in ${room.name}`);
  for (const t of LOOT_TABLES) {
    const area = MAP_AREAS.find((a) => a.id === t.id)?.name ?? t.id;
    for (const e of t.books ?? []) familyAndWard(e).forEach((id) => add(id, `Elites in ${area}`));
  }
  return out;
}
