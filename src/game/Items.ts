import { BALANCE } from '../config';
import { db } from '../data/db';
import { ELEMENTS } from '../data/elements';
import type { ArmorDef, Element, ItemDef, StatKey, WeaponDef } from '../data/types';
import { defaultRng, type Rng } from './rng';

/**
 * Rolled gear. Every weapon and armor piece is a unique instance with a quality per line (0-100%).
 * Shop gear is always 0% ("Standard"); drops roll each line on a curve, so high rolls are rare.
 */
export type RollKey = 'damage' | 'crit' | 'proc' | 'defense' | 'hp';

/** Whole percent, 0-100. Missing = 0. */
export type Rolls = Partial<Record<RollKey, number>>;

export type GearDef = WeaponDef | ArmorDef;

/** Rare variants, weakest first. A Sigil raises the item's base and brings extra mods. */
export type SigilTier = 'dawn' | 'dusk' | 'eclipse';
export const SIGIL_TIERS: SigilTier[] = ['dawn', 'dusk', 'eclipse'];

export const SIGIL_NAMES: Record<SigilTier, string> = {
  dawn: 'Sigil of Dawn',
  dusk: 'Sigil of Dusk',
  eclipse: 'Sigil of Eclipse',
};

/** Extra ("blue") lines. Stats and resist are flat points, crit is percent, hp/mp are flat. */
export type ModKey = StatKey | 'crit' | 'hp' | 'mp' | 'resist';

export interface Mod {
  key: ModKey;
  /** Quality 0-100, like a white line. */
  roll: number;
  /** Which element a `resist` mod covers. */
  element?: Element;
}

export const MOD_POOLS: Record<GearDef['type'], ModKey[]> = {
  weapon: ['str', 'int', 'dex', 'end', 'crit', 'mp'],
  armor: ['str', 'int', 'dex', 'end', 'hp', 'mp', 'resist'],
};

/** What makes one piece unique. Owned gear and loot grants both carry it. */
export interface GearTraits {
  rolls: Rolls;
  sigil?: SigilTier;
  mods?: Mod[];
  /** Smith reinforcement, 0 to `BALANCE.forge.maxPlus`. Missing = 0. */
  plus?: number;
}

/** A weapon or armor piece the player owns. Plain JSON: it is saved as is. */
export interface Gear extends GearTraits {
  uid: string;
  itemId: string;
  /** Locked gear can't be sold. */
  locked?: boolean;
  /** Not looked at in the inventory yet. */
  isNew?: boolean;
}

/** Something handed to the player: a consumable, or gear with its rolls already decided. */
export interface ItemGrant extends Partial<GearTraits> {
  itemId: string;
}

/** Where loot comes from. Quest rewards roll white lines only. */
export type DropSource = 'mob' | 'elite' | 'boss' | 'chest' | 'quest';

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

/** The quality lines this item rolls, in display order: as many as its modifier slots allow. */
export function rollKeys(def: GearDef, sigil?: SigilTier): RollKey[] {
  const all: RollKey[] = def.type === 'armor' ? ['defense', 'hp'] : def.proc ? ['damage', 'crit', 'proc'] : ['damage', 'crit'];
  return all.slice(0, modifierSlots(def, sigil));
}

/** Modifier lines (quality lines + mods) this item can have: its tier's cap, plus the extra lines a Sigil brings. */
export function modifierSlots(def: GearDef, sigil?: SigilTier): number {
  const slots = BALANCE.modifierSlots;
  return slots[Math.min(def.tier, slots.length - 1)] + (sigil ? BALANCE.sigils[sigil].lines : 0);
}

/** How many mods fit after the quality lines (only Sigil gear has room for now). */
export function modSlots(def: GearDef, sigil?: SigilTier): number {
  return Math.max(0, modifierSlots(def, sigil) - rollKeys(def, sigil).length);
}

/** One drop line: random ^ curve, so most lines land low and 90%+ is rare. */
export function rollQuality(rng: Rng = defaultRng): number {
  return Math.round(Math.pow(rng.next(), BALANCE.rollCurve) * 100);
}

/** `quality` fixes every line (0 = shop, 100 = perfect); otherwise each line is rolled. */
export function rollGear(def: GearDef, rng: Rng = defaultRng, quality?: number, sigil?: SigilTier): Rolls {
  const rolls: Rolls = {};
  for (const key of rollKeys(def, sigil)) rolls[key] = quality ?? rollQuality(rng);
  return rolls;
}

/** Item grant for a dropped or rewarded item: gear gets rolled (and maybe a Sigil and mods), consumables don't. */
export function grantFor(itemId: string, rng: Rng = defaultRng, source: DropSource = 'quest', extras = true): ItemGrant {
  const def = db.item(itemId);
  if (!isGearDef(def)) return { itemId };
  // `extras` off: no Sigils or mods yet (they unlock in a later chapter).
  const sigil = source === 'quest' || !extras ? undefined : rollSigil(rng, source);
  const grant: ItemGrant = { itemId, rolls: rollGear(def, rng, undefined, sigil) };
  if (!sigil) return grant;
  // A Sigil's extra lines: first any quality line the tier cap left out, then mods.
  grant.sigil = sigil;
  const mods = modSlots(def, sigil);
  if (mods) grant.mods = rollMods(def, mods, rng);
  return grant;
}

/** Rarest tier is checked first, so each chance in `BALANCE.sigilChance` is its own. */
export function rollSigil(rng: Rng, source: Exclude<DropSource, 'quest'>): SigilTier | undefined {
  const table = BALANCE.sigilChance[source];
  const r = rng.next();
  let edge = 0;
  for (const tier of [...SIGIL_TIERS].reverse()) {
    edge += table[tier];
    if (r < edge) return tier;
  }
  return undefined;
}

/** `count` different mods from the item's pool. */
export function rollMods(def: GearDef, count: number, rng: Rng = defaultRng): Mod[] {
  const pool = [...MOD_POOLS[def.type]];
  const mods: Mod[] = [];
  while (mods.length < count && pool.length) {
    const key = pool.splice(Math.floor(rng.next() * pool.length), 1)[0];
    const mod: Mod = { key, roll: rollQuality(rng) };
    if (key === 'resist') mod.element = ELEMENTS[Math.floor(rng.next() * ELEMENTS.length)];
    mods.push(mod);
  }
  return mods;
}

/** A mod's value at its quality: stat points, crit percent, HP, MP or resist points. */
export function modValue(mod: Mod): number {
  const [min, max] = BALANCE.modRanges[mod.key];
  const v = min + ((max - min) * mod.roll) / 100;
  return mod.key === 'crit' ? Math.round(v * 10) / 10 : Math.round(v);
}

/** Sum of one mod key over some gear. Resist mods count only for `element`. */
export function modTotal(pieces: (GearTraits | null | undefined)[], key: ModKey, element?: Element): number {
  let total = 0;
  for (const g of pieces) for (const m of g?.mods ?? []) if (m.key === key && (key !== 'resist' || m.element === element)) total += modValue(m);
  return total;
}

/** Base multiplier from a Sigil and reinforcement (+): 1 without either. */
export function sigilBoost(g: GearTraits): number {
  return (1 + (g.sigil ? BALANCE.sigils[g.sigil].boost : 0)) * (1 + (g.plus ?? 0) * BALANCE.forge.plusPower);
}

/** Average line quality, 0-100. */
export function grade(def: GearDef, rolls: Rolls, sigil?: SigilTier): number {
  const keys = rollKeys(def, sigil);
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

/** The Sigil raises the base first, then the Damage line applies on top. */
export function rolledDamage(def: WeaponDef, g: GearTraits): [number, number] {
  const m = sigilBoost(g) * (1 + rollBonus('damage', g.rolls));
  return [Math.round(def.damage[0] * m), Math.round(def.damage[1] * m)];
}

export function rolledProcChance(def: WeaponDef, g: GearTraits): number {
  return def.proc ? Math.min(1, def.proc.chance * (1 + rollBonus('proc', g.rolls))) : 0;
}

export function rolledDefense(def: ArmorDef, g: GearTraits, type: keyof ArmorDef['defense']): number {
  return Math.round((def.defense[type] ?? 0) * sigilBoost(g) * (1 + rollBonus('defense', g.rolls)));
}

/** Gold the shop pays. Better grades sell for more, Sigils a lot more. */
export function sellValue(def: ItemDef, g?: GearTraits): number {
  const gr = isGearDef(def) && g ? grade(def, g.rolls, g.sigil) : 0;
  const sigil = g?.sigil ? BALANCE.sigils[g.sigil].sell : 1;
  const plus = 1 + 0.15 * (g?.plus ?? 0);
  return Math.max(1, Math.floor(def.price * BALANCE.sellRate * (1 + (gr / 100) * BALANCE.sellGradeBonus) * sigil * plus));
}

/** Keep only known lines, as whole percents in 0-100. For loading saves. */
export function sanitizeRolls(def: GearDef, raw: unknown, sigil?: SigilTier): Rolls {
  const out: Rolls = {};
  if (!raw || typeof raw !== 'object') return out;
  for (const key of rollKeys(def, sigil)) {
    const v = (raw as Record<string, unknown>)[key];
    if (typeof v === 'number' && Number.isFinite(v)) out[key] = Math.min(100, Math.max(0, Math.round(v)));
  }
  return out;
}

const isSigil = (v: unknown): v is SigilTier => typeof v === 'string' && (SIGIL_TIERS as string[]).includes(v);

/** Sigil, mods and rolls of a saved piece, keeping only what this item can have. For loading saves. */
export function sanitizeTraits(def: GearDef, raw: Partial<Record<keyof GearTraits, unknown>>): GearTraits {
  const sigil = isSigil(raw.sigil) ? raw.sigil : undefined;
  const out: GearTraits = { rolls: sanitizeRolls(def, raw.rolls, sigil) };
  if (sigil) out.sigil = sigil;
  if (typeof raw.plus === 'number' && raw.plus >= 1) out.plus = Math.min(BALANCE.forge.maxPlus, Math.floor(raw.plus));
  const mods: Mod[] = [];
  for (const m of Array.isArray(raw.mods) ? (raw.mods as Partial<Mod>[]) : []) {
    if (mods.length >= modSlots(def, sigil)) break;
    if (!m || !MOD_POOLS[def.type].includes(m.key as ModKey) || mods.some((x) => x.key === m.key)) continue;
    if (typeof m.roll !== 'number' || !Number.isFinite(m.roll)) continue;
    const mod: Mod = { key: m.key as ModKey, roll: Math.min(100, Math.max(0, Math.round(m.roll))) };
    if (mod.key === 'resist') {
      if (!(ELEMENTS as unknown[]).includes(m.element)) continue;
      mod.element = m.element;
    }
    mods.push(mod);
  }
  if (mods.length) out.mods = mods;
  return out;
}
