/**
 * How the sim and pace bots use skill books (docs/BUILD_VARIETY_SPEC.md): learn what a player could have by a level,
 * read books from rewards, and carry the class heal plus the best damage skills against the next monster.
 */
import { BALANCE } from '../src/config';
import { db } from '../src/data/db';
import type { MonsterDef, SkillDef } from '../src/data/types';
import { bookSources, readBook, spellbook } from '../src/game/Books';
import type { Player } from '../src/game/Player';

/** Sources a bot can count on (not "Later in the story" or the level-20 class quest). */
function realSources(skillId: string): string[] {
  return bookSources(skillId).filter((s) => !s.startsWith('Later') && !s.startsWith('Class quest'));
}

/**
 * Learn every book this class could have by `cap` (read level at most `cap`, with a real source). `from` narrows the
 * sources (e.g. only shops and chests: boss and promotion books come through the rewards instead).
 */
export function learnBooks(p: Player, cap: number, from?: (source: string) => boolean): void {
  for (const e of spellbook(p)) {
    if (e.group === 'level' || e.known || e.level > cap) continue;
    const sources = realSources(e.skill.id);
    if (!sources.length || (from && !sources.some(from))) continue;
    p.data.learned.push(e.skill.id);
  }
}

/** Read every book in the bag that can be read now. */
export function readAll(p: Player): void {
  for (const e of [...p.data.inventory]) if (db.item(e.itemId).type === 'book') readBook(p, e.itemId);
}

/** Rough value of a damage skill against a monster: power, hits, element, the family match bonus, Blunt. */
export function skillValueVs(p: Player, s: SkillDef, m: MonsterDef): number {
  const element = !s.element || s.element === 'weapon' ? p.weaponElement : s.element;
  const resist = element ? (m.resists?.[element] ?? 0) + (m.resists?.all ?? 0) : 0;
  const match = s.family && element === p.weaponElement ? 1 + BALANCE.books.match : 1;
  const crush = s.blunt ? 1 + (m.weakTo?.blunt ?? 0) / 100 : 1;
  const debuff = s.applies?.some((a) => !a.self) ? 0.3 : 0;
  return s.power * (s.hits ?? 1) * (1 - resist / 100) * match * crush + debuff;
}

/** The class heal (the strongest known heal) plus the best damage skills against this monster. */
export function pickLoadout(p: Player, m: MonsterDef): string[] {
  const known = p.knownSkills;
  const heal = known.filter((s) => s.kind === 'heal').sort((a, b) => b.power - a.power)[0];
  const damage = known
    .filter((s) => s.kind === 'damage')
    .sort((a, b) => skillValueVs(p, b, m) - skillValueVs(p, a, m))
    .map((s) => s.id);
  return [...(heal ? [heal.id] : []), ...damage].slice(0, BALANCE.skillSlots);
}
