import { BALANCE } from '../config';
import type { ArmorDef, ArmorType, DamageType, Defenses, Element, ProcDef, Resists, Stats, WeaponDef } from './types';

/**
 * Gear lines. Each family is written once and the game generates a version every
 * `BALANCE.gear.versionStep` levels from its `from` level (Iron Sword, Iron Sword II, ...).
 * Numbers come from the level budget in BALANCE.gear, so new lines need no hand balancing:
 * pay for stats, elements and specials by lowering `power`.
 * The first version keeps the family id (e.g. `iron_sword`); later ones are `<id>_<level>`.
 */

interface FamilyBase {
  id: string;
  name: string;
  description: string;
  /** Level of the first version. */
  from: number;
  /** Last version level (default BALANCE.gear.maxLevel). */
  to?: number;
  /** Share of the level budget. ~1 for plain gear, lower when it also gives stats or a special. */
  power: number;
  /** Stat bonus of the first version; each later version adds it again. */
  stats?: Partial<Stats>;
}

export interface WeaponFamily extends FamilyBase {
  damageType: DamageType;
  element?: Element;
  proc?: ProcDef;
  art?: string;
  grip?: number;
  size?: number;
}

export interface ArmorFamily extends FamilyBase {
  archetype: ArmorType;
  /** First version's resistances; each later version adds +5 to each. */
  resists?: Resists;
}

/** How each armor type splits its defense budget, and what it does besides. */
export const ARMOR_TYPES: Record<ArmorType, { name: string; split: Required<Defenses>; mpCost: number; walk: number }> = {
  plate: { name: 'Plate', split: { melee: 1.2, pierce: 1.1, magic: 0.35 }, mpCost: 0, walk: -0.1 },
  leather: { name: 'Leather', split: { melee: 0.8, pierce: 0.85, magic: 0.4 }, mpCost: -0.1, walk: 0.1 },
  cloth: { name: 'Cloth', split: { melee: 0.4, pierce: 0.4, magic: 1.1 }, mpCost: -0.2, walk: 0.2 },
};

export const WEAPON_FAMILIES: WeaponFamily[] = [
  // Starters
  { id: 'training_sword', name: 'Training Sword', description: 'Dull, but it beats fists.', from: 1, power: 0.9, damageType: 'melee', art: 'wpn_training_sword', grip: 0.84, size: 0.6 },
  { id: 'worn_staff', name: 'Worn Staff', description: 'An apprentice staff. Hits with magic.', from: 1, power: 0.9, damageType: 'magic', art: 'wpn_worn_staff', grip: 0.45, size: 1.0 },
  { id: 'rusty_dagger', name: 'Rusty Dagger', description: 'Short, quick, a bit rusty.', from: 1, power: 0.85, damageType: 'pierce', stats: { dex: 1 }, art: 'wpn_rusty_dagger', grip: 0.76, size: 0.3 },

  // Plain lines (shops and drops)
  { id: 'tusk_dagger', name: 'Tusk Dagger', description: 'Carved from a boar tusk. Quick and sharp.', from: 2, power: 0.85, damageType: 'pierce', stats: { dex: 2 }, art: 'wpn_tusk_dagger', grip: 0.72, size: 0.32 },
  { id: 'iron_sword', name: 'Iron Sword', description: 'A sturdy blade from the town forge.', from: 3, power: 0.9, damageType: 'melee', art: 'wpn_iron_sword', grip: 0.84, size: 0.62 },
  { id: 'oak_staff', name: 'Oak Staff', description: 'Hits with magic. Scales with INT.', from: 3, power: 0.7, damageType: 'magic', stats: { int: 3 }, art: 'wpn_oak_staff', grip: 0.45, size: 1.0 },
  { id: 'hunting_knife', name: 'Hunting Knife', description: "A trapper's knife. Quick in the hand.", from: 3, power: 0.85, damageType: 'pierce', stats: { dex: 1 }, art: 'wpn_hunting_knife', grip: 0.76, size: 0.32 },

  // Fire (Ash Imps, the smith)
  {
    id: 'cinder_fang', name: 'Cinder Fang', description: 'A dagger of black glass, warm to the touch.', from: 3, power: 0.8,
    damageType: 'pierce', element: 'fire', stats: { dex: 1 }, art: 'wpn_cinder_fang', grip: 0.77, size: 0.32,
    proc: { name: 'Ignite', chance: 0.15, power: 0, applies: [{ status: 'burn', turns: 2 }] },
  },
  {
    id: 'emberbrand', name: 'Emberbrand', description: 'Its edge never quite stops glowing.', from: 3, power: 1.0,
    damageType: 'melee', element: 'fire', art: 'wpn_emberbrand', grip: 0.84, size: 0.64,
    proc: { name: 'Flare', chance: 0.12, power: 0.6 },
  },
  { id: 'ember_wand', name: 'Ember Wand', description: 'A charred wand with a coal for a heart.', from: 3, power: 0.8, damageType: 'magic', element: 'fire', stats: { int: 2 }, art: 'wpn_ember_wand', grip: 0.75, size: 0.45 },

  // Chapter 1 boss rewards (Earth)
  {
    id: 'thornheart_blade', name: 'Thornheart Blade', description: 'Carved from the Thornheart. Still smells of spring rain.', from: 5, power: 1.05,
    damageType: 'melee', element: 'earth', stats: { str: 2, end: 1 }, art: 'wpn_thornheart_blade', grip: 0.86, size: 0.66,
    proc: { name: 'Thorn Burst', chance: 0.15, power: 0.8, applies: [{ status: 'bleed', turns: 2 }] },
  },
  {
    id: 'heartwood_staff', name: 'Heartwood Staff', description: 'A living staff that hums with old magic.', from: 5, power: 0.9,
    damageType: 'magic', element: 'earth', stats: { int: 4 }, art: 'wpn_heartwood_staff', grip: 0.45, size: 1.05,
    proc: { name: 'Verdant Pulse', chance: 0.15, power: 0, heal: 0.08 },
  },
  {
    id: 'briar_fang', name: 'Briar Fang', description: 'A dagger grown from a single great thorn.', from: 5, power: 0.85,
    damageType: 'pierce', element: 'earth', stats: { dex: 3 }, art: 'wpn_briar_fang', grip: 0.78, size: 0.34,
    proc: { name: 'Briar Sting', chance: 0.2, power: 0, applies: [{ status: 'poison', turns: 3 }] },
  },
];

export const ARMOR_FAMILIES: ArmorFamily[] = [
  { id: 'travel_garb', name: "Traveler's Garb", description: 'Simple leathers. Better than nothing.', from: 1, power: 0.45, archetype: 'leather' },
  { id: 'novice_robe', name: 'Novice Robe', description: 'Thin cloth with a few protective stitches.', from: 1, power: 0.75, archetype: 'cloth' },
  { id: 'leather_armor', name: 'Leather Armor', description: 'Good against claws and blades.', from: 3, power: 1.0, archetype: 'leather', stats: { end: 1 } },
  { id: 'chain_mail', name: 'Chain Mail', description: 'Heavy rings of iron. Stops steel, not spells.', from: 3, power: 1.0, archetype: 'plate' },
  { id: 'apprentice_robe', name: 'Apprentice Robe', description: 'Woven with warding runes. Shrugs off a little fire.', from: 3, power: 1.0, archetype: 'cloth', resists: { fire: 15 }, stats: { int: 2 } },
  // Smith recipes (Earth resist, for the Thornheart)
  { id: 'thornhide_vest', name: 'Thornhide Vest', description: 'Boar hide sewn with moss. The forest slides off it.', from: 3, power: 0.9, archetype: 'leather', resists: { earth: 20 } },
  { id: 'mossweave_robe', name: 'Mossweave Robe', description: 'Living moss, woven tight. Smells of rain.', from: 3, power: 0.9, archetype: 'cloth', resists: { earth: 20 }, stats: { int: 1 } },
];

const NUMERALS = ['', ' II', ' III', ' IV', ' V', ' VI', ' VII', ' VIII'];

function versionLevels(f: FamilyBase): number[] {
  const g = BALANCE.gear;
  const levels: number[] = [];
  for (let l = f.from; l <= (f.to ?? g.maxLevel); l += g.versionStep) levels.push(l);
  return levels;
}

function scaleStats(stats: Partial<Stats> | undefined, tier: number): Partial<Stats> | undefined {
  if (!stats) return undefined;
  const out: Partial<Stats> = {};
  for (const [k, v] of Object.entries(stats) as [keyof Stats, number][]) out[k] = v * (tier + 1);
  return out;
}

function versionId(familyId: string, tier: number, level: number): string {
  return tier === 0 ? familyId : `${familyId}_${level}`;
}

function makeWeapons(f: WeaponFamily): WeaponDef[] {
  const g = BALANCE.gear;
  return versionLevels(f).map((level, tier) => {
    const avg = (g.weaponBase + g.weaponPerLevel * level) * f.power;
    const value = g.weaponPriceK * avg * avg * (f.element ? 1.2 : 1) * (f.proc ? 1.15 : 1);
    return {
      id: versionId(f.id, tier, level),
      type: 'weapon',
      family: f.id,
      level,
      name: f.name + NUMERALS[tier],
      description: f.description,
      price: Math.max(5, Math.round(value)),
      damage: [Math.max(1, Math.round(avg * (1 - g.damageSpread))), Math.round(avg * (1 + g.damageSpread))],
      damageType: f.damageType,
      element: f.element,
      stats: scaleStats(f.stats, tier),
      proc: f.proc,
      art: f.art,
      grip: f.grip,
      size: f.size,
    };
  });
}

function makeArmors(f: ArmorFamily): ArmorDef[] {
  const g = BALANCE.gear;
  const type = ARMOR_TYPES[f.archetype];
  return versionLevels(f).map((level, tier) => {
    const budget = (g.armorBase + g.armorPerLevel * level) * f.power;
    const defense = {
      melee: Math.round(budget * type.split.melee),
      pierce: Math.round(budget * type.split.pierce),
      magic: Math.round(budget * type.split.magic),
    };
    const total = defense.melee + defense.pierce + defense.magic;
    let resists: Resists | undefined;
    if (f.resists) {
      resists = {};
      for (const [k, v] of Object.entries(f.resists) as [keyof Resists, number][]) resists[k] = v + 5 * tier;
    }
    return {
      id: versionId(f.id, tier, level),
      type: 'armor',
      family: f.id,
      level,
      archetype: f.archetype,
      name: f.name + NUMERALS[tier],
      description: f.description,
      price: Math.max(5, Math.round(g.armorPriceK * total * total * (resists ? 1.2 : 1))),
      defense,
      resists,
      stats: scaleStats(f.stats, tier),
      mpCost: type.mpCost,
      walk: type.walk,
    };
  });
}

export const GEAR_ITEMS: (WeaponDef | ArmorDef)[] = [...WEAPON_FAMILIES.flatMap(makeWeapons), ...ARMOR_FAMILIES.flatMap(makeArmors)];
