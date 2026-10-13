import { BALANCE } from '../config';
import { db } from '../data/db';
import type { ArmorDef, ItemDef, WeaponDef } from '../data/types';
import { defaultRng, type Rng } from './rng';

/**
 * Rolled gear. Every weapon and armor piece is a unique instance with a quality per line (0-100%).
 * Shop gear is always 0% ("Standard"); drops roll each line on a curve, so high rolls are rare.
 */
export type RollKey = 'damage' | 'crit' | 'proc' | 'defense' | 'hp';

/** Whole percent, 0-100. Missing = 0. */
export type Rolls = Partial<Record<RollKey, number>>;

export type GearDef = WeaponDef | ArmorDef;

/** A weapon or armor piece the player owns. Plain JSON: it is saved as is. */
export interface Gear {
  uid: string;
  itemId: string;
  rolls: Rolls;
  /** Locked gear can't be sold. */
  locked?: boolean;
  /** Not looked at in the inventory yet. */
  isNew?: boolean;
}

/** Something handed to the player: a consumable, or gear with its rolls already decided. */
export interface ItemGrant {
  itemId: string;
  rolls?: Rolls;
}

export const ROLL_LABELS: Record<RollKey, string> = {
  damage: 'Damage',
  crit: 'Crit chance',
  proc: 'Special chance',
  defense: 'Defense',
  hp: 'Max HP',
};

export function isGearDef(item: ItemDef): item is GearDef {
  return item.type === 'weapon' || item.type === 'armor';
}

export function gearDef(gear: Gear): GearDef {
  return db.item(gear.itemId) as GearDef;
}

/** The lines this item rolls, in display order. */
export function rollKeys(def: GearDef): RollKey[] {
  if (def.type === 'armor') return ['defense', 'hp'];
  return def.proc ? ['damage', 'crit', 'proc'] : ['damage', 'crit'];
}

/** One drop line: random ^ curve, so most lines land low and 90%+ is rare. */
export function rollQuality(rng: Rng = defaultRng): number {
  return Math.round(Math.pow(rng.next(), BALANCE.rollCurve) * 100);
}

/** `quality` fixes every line (0 = shop, 100 = perfect); otherwise each line is rolled. */
export function rollGear(def: GearDef, rng: Rng = defaultRng, quality?: number): Rolls {
  const rolls: Rolls = {};
  for (const key of rollKeys(def)) rolls[key] = quality ?? rollQuality(rng);
  return rolls;
}

/** Item grant for a dropped or rewarded item: gear gets rolled, consumables don't. */
export function grantFor(itemId: string, rng: Rng = defaultRng): ItemGrant {
  const def = db.item(itemId);
  return isGearDef(def) ? { itemId, rolls: rollGear(def, rng) } : { itemId };
}

/** Average line quality, 0-100. */
export function grade(def: GearDef, rolls: Rolls): number {
  const keys = rollKeys(def);
  return Math.round(keys.reduce((sum, k) => sum + (rolls[k] ?? 0), 0) / keys.length);
}

export type GradeTier = 'standard' | 'fair' | 'fine' | 'superior' | 'masterwork';

/** Lowest grade of each tier, best first. */
export const GRADE_TIERS: { tier: GradeTier; name: string; min: number }[] = [
  { tier: 'masterwork', name: 'Masterwork', min: 90 },
  { tier: 'superior', name: 'Superior', min: 70 },
  { tier: 'fine', name: 'Fine', min: 40 },
  { tier: 'fair', name: 'Fair', min: 1 },
  { tier: 'standard', name: 'Standard', min: 0 },
];

export function gradeTier(g: number): { tier: GradeTier; name: string } {
  return GRADE_TIERS.find((t) => g >= t.min)!;
}

/** Bonus a line gives at its quality, as a fraction (damage 0.15 = +15%). */
export function rollBonus(key: RollKey, rolls: Rolls): number {
  return ((rolls[key] ?? 0) / 100) * BALANCE.gearRolls[key];
}

export function rolledDamage(def: WeaponDef, rolls: Rolls): [number, number] {
  const m = 1 + rollBonus('damage', rolls);
  return [Math.round(def.damage[0] * m), Math.round(def.damage[1] * m)];
}

export function rolledProcChance(def: WeaponDef, rolls: Rolls): number {
  return def.proc ? Math.min(1, def.proc.chance * (1 + rollBonus('proc', rolls))) : 0;
}

export function rolledDefense(def: ArmorDef, rolls: Rolls, type: keyof ArmorDef['defense']): number {
  return Math.round((def.defense[type] ?? 0) * (1 + rollBonus('defense', rolls)));
}

/** Gold the shop pays. Better grades sell for more. */
export function sellValue(def: ItemDef, rolls?: Rolls): number {
  const g = isGearDef(def) && rolls ? grade(def, rolls) : 0;
  return Math.max(1, Math.floor(def.price * BALANCE.sellRate * (1 + (g / 100) * BALANCE.sellGradeBonus)));
}

/** Keep only known lines, as whole percents in 0-100. For loading saves. */
export function sanitizeRolls(def: GearDef, raw: unknown): Rolls {
  const out: Rolls = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const key of rollKeys(def)) {
    const v = (raw as Record<string, unknown>)[key];
    if (typeof v === 'number' && Number.isFinite(v)) out[key] = Math.min(100, Math.max(0, Math.round(v)));
  }
  return out;
}
