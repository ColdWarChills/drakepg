import { BALANCE } from '../config';
import { gearDef, rollKeys, rollQuality, type Gear, type RollKey } from './Items';
import type { Player } from './Player';
import { defaultRng, type Rng } from './rng';

/**
 * Rework at the shop counter (every town's merchant): pay gold to roll one quality line of a piece again (Damage,
 * Crit, Defense or Max HP), then keep the new value or the old one. High rolls are rare (the same curve as drops).
 * The price starts at `BALANCE.rework.base` x the gear's tier and grows by `growth` with every rework of that piece,
 * so chasing a perfect roll costs more and more gold.
 */

/** The lines the counter can rework (Special chance and jewelry Power can't be). */
export const REWORK_LINES: RollKey[] = ['damage', 'crit', 'defense', 'hp'];

/** Lines of this piece that can be reworked. */
export function reworkLines(gear: Gear): RollKey[] {
  return rollKeys(gearDef(gear), gear.sigil).filter((k) => REWORK_LINES.includes(k));
}

/** Gold for this piece's next rework. */
export function reworkPrice(gear: Gear): number {
  const r = BALANCE.rework;
  return Math.round(r.base * gearDef(gear).tier * Math.pow(r.growth, gear.reworks ?? 0));
}

export function canRework(player: Player, gear: Gear, key: RollKey): boolean {
  return reworkLines(gear).includes(key) && player.gold >= reworkPrice(gear);
}

/**
 * Pay and roll the line again. The piece keeps its old value until `keepRework` sets the one the player picks.
 * Returns the old and the new quality, or null if you can't.
 */
export function rework(player: Player, gear: Gear, key: RollKey, rng: Rng = defaultRng): { before: number; after: number } | null {
  if (!canRework(player, gear, key)) return null;
  player.data.gold -= reworkPrice(gear);
  gear.reworks = (gear.reworks ?? 0) + 1;
  return { before: gear.rolls[key] ?? 0, after: rollQuality(rng) };
}

/** Set the value the player kept. */
export function keepRework(gear: Gear, key: RollKey, value: number): void {
  gear.rolls[key] = value;
}
