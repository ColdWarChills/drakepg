import { BALANCE } from '../config';
import { db } from '../data/db';
import type { Defenses, MonsterDef, Resists } from '../data/types';
import { cheats } from './cheats';
import { chance, defaultRng, randInt, type Rng } from './rng';

/**
 * Elites: rare, tougher versions of roaming monsters. An Elite is a plain MonsterDef copy with
 * boosted numbers and `elite` set, so battles, quests and the UI treat it like any monster.
 * It keeps the base monster's id, so it also counts for "defeat N of X" quests.
 */

/** An affix id if this roaming spawn should be an Elite, else undefined. */
export function rollElite(rng: Rng = defaultRng, questWantsOne = false): string | undefined {
  const odds = cheats.elites ? 1 : questWantsOne ? BALANCE.elite.questChance : BALANCE.elite.chance;
  if (!chance(rng, odds)) return undefined;
  const list = db.affixes();
  return list[randInt(rng, 0, list.length - 1)].id;
}

/** The Elite version of `def` with the given affix. Its guaranteed gear drop comes from `def.loot`. */
export function makeElite(def: MonsterDef, affixId: string): MonsterDef {
  const a = db.affix(affixId);
  const e = BALANCE.elite;
  const range = (r: [number, number], m: number): [number, number] => [Math.round(r[0] * m), Math.round(r[1] * m)];
  const defense: Defenses = {};
  for (const [type, v] of Object.entries(def.defense) as [keyof Defenses, number][]) defense[type] = Math.round(v * (1 + (a.defense ?? 0)));
  const resists: Resists = { ...def.resists };
  for (const [key, v] of Object.entries(a.resists ?? {}) as [keyof Resists, number][]) resists[key] = (resists[key] ?? 0) + v;
  return {
    ...def,
    name: `${a.name} ${def.name}`,
    level: def.level + e.levels,
    look: { ...def.look, scale: def.look.scale * e.size },
    hp: Math.round(def.hp * e.hp * (1 + (a.hp ?? 0))),
    damage: range(def.damage, e.damage * (1 + (a.damage ?? 0))),
    defense,
    resists,
    attacks: def.attacks.map((atk) => {
      // Add the affix's statuses to damaging attacks that don't already apply them.
      const extra = (a.applies ?? []).filter((x) => !atk.applies?.some((y) => y.status === x.status));
      return atk.power > 0 && extra.length ? { ...atk, applies: [...(atk.applies ?? []), ...extra] } : atk;
    }),
    xp: Math.round(def.xp * e.rewards),
    gold: range(def.gold, e.rewards),
    elite: { affix: a.id },
  };
}
