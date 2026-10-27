import { BALANCE } from '../config';
import { gearDef, rollKeys, rollQuality, type Gear, type RollKey } from './Items';
import type { Player } from './Player';
import { defaultRng, type Rng } from './rng';

/**
 * Rework at the shop counter (every town's merchant): pay gold to roll one quality line of a piece again (Damage,
 * Crit, Defense or Max HP). The new value replaces the old one, up or down; high rolls are rare (the same curve as
 * drops). An Anchor Stone (a rare drop) puts the old value back after a worse roll. The price starts at
 * `BALANCE.rework.base` x the gear's tier and grows by `growth` with every rework of that piece.
 */

/** The lines the counter can rework (Special chance and jewelry Power can't be). */
export const REWORK_LINES: RollKey[] = ['damage', 'crit', 'defense', 'hp'];

/** Undoes a worse rework roll. A material: no bag slot. */
export const ANCHOR_STONE = 'anchor_stone';

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

/** Pay and roll the line again; the new value replaces the old. Returns the old and the new quality, or null if you can't. */
export function rework(player: Player, gear: Gear, key: RollKey, rng: Rng = defaultRng): { before: number; after: number } | null {
  if (!canRework(player, gear, key)) return null;
  player.data.gold -= reworkPrice(gear);
  gear.reworks = (gear.reworks ?? 0) + 1;
  const before = gear.rolls[key] ?? 0;
  gear.rolls[key] = rollQuality(rng);
  return { before, after: gear.rolls[key]! };
}

/** An Anchor Stone can undo this roll: it came out worse and the player has one. */
export function canAnchor(player: Player, roll: { before: number; after: number }): boolean {
  return roll.after < roll.before && player.countItem(ANCHOR_STONE) > 0;
}

/** Use an Anchor Stone to put the old value back. False if there is none. */
export function anchor(player: Player, gear: Gear, key: RollKey, before: number): boolean {
  if (player.countItem(ANCHOR_STONE) <= 0) return false;
  player.removeItem(ANCHOR_STONE, 1);
  gear.rolls[key] = before;
  return true;
}
