import { db } from '../data/db';
import type { StoneDef } from '../data/types';
import { gearDef, rollKeys, rollQuality, type Gear, type RollKey } from './Items';
import type { Player } from './Player';
import { defaultRng, type Rng } from './rng';

/**
 * Roll stones, sold in every shop: each one re-rolls one quality line of a weapon or armor (Damage, Crit, Defense
 * or Max HP) to a new random value. The new value always replaces the old one, and high rolls are rare (the same
 * curve as drops). Higher-tier gear takes more stones per try: one per tier.
 */

/** The stone that re-rolls this line, if there is one (Special chance has none). */
export function stoneFor(key: RollKey): StoneDef | undefined {
  return db.stones().find((s) => s.line === key);
}

/** Stones one re-roll of this piece costs. */
export function stonesNeeded(gear: Gear): number {
  return gearDef(gear).tier;
}

/** Lines of this piece a stone can re-roll. */
export function rerollableLines(gear: Gear): RollKey[] {
  return rollKeys(gearDef(gear), gear.sigil).filter((k) => stoneFor(k));
}

export function canReroll(player: Player, gear: Gear, key: RollKey): boolean {
  const stone = stoneFor(key);
  return !!stone && rerollableLines(gear).includes(key) && player.countItem(stone.id) >= stonesNeeded(gear);
}

/** Use the stones and roll the line again. Returns the old and new quality, or null if you can't. */
export function reroll(player: Player, gear: Gear, key: RollKey, rng: Rng = defaultRng): { before: number; after: number } | null {
  if (!canReroll(player, gear, key)) return null;
  player.removeItem(stoneFor(key)!.id, stonesNeeded(gear));
  const before = gear.rolls[key] ?? 0;
  gear.rolls[key] = rollQuality(rng);
  return { before, after: gear.rolls[key]! };
}
