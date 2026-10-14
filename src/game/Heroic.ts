import { BALANCE } from '../config';
import type { MonsterDef } from '../data/types';

/**
 * Heroic bosses: a harder rematch, open once a boss's quest is done. Not a bigger HP pool:
 * a few levels, more damage and a new move (`MonsterDef.heroic`), for full XP and gold and extra gear rolls.
 * It keeps the boss's id, so grit carries over.
 */
export function makeHeroic(def: MonsterDef): MonsterDef {
  const h = BALANCE.heroic;
  const extra = def.heroic?.attacks ?? [];
  const range = (r: [number, number]): [number, number] => [Math.round(r[0] * h.damage), Math.round(r[1] * h.damage)];
  return {
    ...def,
    name: `Heroic ${def.name.replace(/^The /, '')}`,
    level: def.level + h.levels,
    damage: range(def.damage),
    attacks: [...def.attacks, ...extra],
    phase: def.phase && { ...def.phase, attacks: [...(def.phase.attacks ?? def.attacks), ...extra] },
    isHeroic: true,
  };
}
