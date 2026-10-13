import { BALANCE } from '../config';
import { essenceId } from '../data/crafting';
import { db } from '../data/db';
import { ELEMENTS, ELEMENT_LABELS } from '../data/elements';
import { RARITIES, RARITY, TRAITS } from '../data/rarity';
import type { ArmorDef, Element, GearDef, ItemDef, RarityId, RecipeDef, Resists, Stats, TraitDef, WeaponDef } from '../data/types';
import type { Player } from './Player';
import { chance, clamp, defaultRng, randInt, weightedPick, type Rng } from './rng';

/**
 * Rolled gear. Every weapon/armor the player owns is an instance of a generated base item
 * with its own quality rolls, rarity, reinforcement (+) and traits. Phaser-free.
 *
 * Quality: three lines per item, each 0-100%. 0% = the base item (what shops sell), 100% = flawless.
 *   Weapons: Damage, Hit, Crit. Armor: Physical defense, Magic defense, Evasion.
 */

export interface TraitRoll {
  id: string;
  /** Whole number; percent traits are divided by 100 when applied. */
  value: number;
  /** Resist traits only. */
  element?: Element;
}

export interface GearInstance {
  uid: number;
  baseId: string;
  rarity: RarityId;
  /** Quality per line, 0-100. */
  q: number[];
  /** Reinforcement, 0-BALANCE.gear.maxPlus. */
  plus: number;
  traits: TraitRoll[];
}

/** Where gear came from decides how it rolls. */
export type GearSource = 'shop' | 'drop' | 'craft' | 'quest';

export const QUALITY_LINES: Record<'weapon' | 'armor', string[]> = {
  weapon: ['Damage', 'Hit', 'Crit'],
  armor: ['Physical', 'Magic', 'Evasion'],
};

export function isGear(item: ItemDef): item is GearDef {
  return item.type === 'weapon' || item.type === 'armor';
}

export function gearDef(baseId: string): GearDef {
  const item = db.item(baseId);
  if (!isGear(item)) throw new Error(`Not gear: ${baseId}`);
  return item;
}

const traitDefs = new Map(TRAITS.map((t) => [t.id, t]));
export function traitDef(id: string): TraitDef {
  const t = traitDefs.get(id);
  if (!t) throw new Error(`Unknown trait: ${id}`);
  return t;
}

// --- Rolling ---

function rollQuality(rng: Rng, floor: number): number {
  return Math.round(floor + (100 - floor) * Math.pow(rng.next(), BALANCE.gear.qualitySkew));
}

function traitRange(t: TraitDef, level: number): [number, number] {
  return [Math.round(t.range[0] + t.perLevel * level), Math.round(t.range[1] + t.perLevel * level)];
}

function rollTraits(base: GearDef, count: number, rng: Rng): TraitRoll[] {
  const pool = TRAITS.filter((t) => t.slots.includes(base.type));
  const out: TraitRoll[] = [];
  for (let i = 0; i < count && pool.length > 0; i++) {
    const t = pool.splice(Math.floor(rng.next() * pool.length), 1)[0];
    const [min, max] = traitRange(t, base.level);
    const roll: TraitRoll = { id: t.id, value: randInt(rng, min, max) };
    if (t.kind === 'resist') roll.element = ELEMENTS[Math.floor(rng.next() * ELEMENTS.length)];
    out.push(roll);
  }
  return out;
}

/** A new instance of a base item. `uid` is assigned when the player receives it. */
export function rollGear(baseId: string, source: GearSource, rng: Rng = defaultRng): GearInstance {
  const base = gearDef(baseId);
  const g = BALANCE.gear;
  let rarity: RarityId = 'common';
  if (source === 'drop') rarity = weightedPick(rng, RARITIES, (r) => r.dropWeight).id;
  if (source === 'craft') rarity = weightedPick(rng, RARITIES, (r) => r.craftWeight).id;
  const floor = Math.max(RARITY[rarity].qualityFloor, source === 'craft' ? g.craftFloor : 0);
  const q = [0, 1, 2].map(() => (source === 'shop' ? 0 : source === 'quest' ? g.questQuality : rollQuality(rng, floor)));
  return { uid: 0, baseId, rarity, q, plus: 0, traits: rollTraits(base, RARITY[rarity].traits, rng) };
}

// --- Reading an instance ---

/** The base item with quality, rarity, + and traits applied. Battle and UI read this. */
export function resolveGear(inst: GearInstance): GearDef {
  const base = gearDef(inst.baseId);
  const g = BALANCE.gear;
  const boost = (1 + g.plusPower * inst.plus) * (1 + RARITY[inst.rarity].power);
  const q = (i: number) => (inst.q[i] ?? 0) / 100;

  const stats: Partial<Stats> = { ...base.stats };
  const resists: Resists = base.type === 'armor' ? { ...base.resists } : {};
  let hit = 0;
  let crit = 0;
  let evasion = 0;
  for (const roll of inst.traits) {
    const t = traitDef(roll.id);
    const v = t.percent ? roll.value / 100 : roll.value;
    if (t.kind === 'stat' && t.stat) stats[t.stat] = (stats[t.stat] ?? 0) + v;
    else if (t.kind === 'resist' && roll.element) resists[roll.element] = (resists[roll.element] ?? 0) + v;
    else if (t.kind === 'hit') hit += v;
    else if (t.kind === 'crit') crit += v;
    else if (t.kind === 'evasion') evasion += v;
  }

  if (base.type === 'weapon') {
    const mult = boost * (1 + g.qualityDamage * q(0));
    const weapon: WeaponDef = {
      ...base,
      damage: [Math.round(base.damage[0] * mult), Math.round(base.damage[1] * mult)],
      stats,
      hit: hit + g.qualityHit * q(1),
      crit: crit + g.qualityCrit * q(2),
    };
    return weapon;
  }
  const phys = boost * (1 + g.qualityDefense * q(0));
  const magic = boost * (1 + g.qualityDefense * q(1));
  const armor: ArmorDef = {
    ...base,
    defense: {
      melee: Math.round((base.defense.melee ?? 0) * phys),
      pierce: Math.round((base.defense.pierce ?? 0) * phys),
      magic: Math.round((base.defense.magic ?? 0) * magic),
    },
    resists: Object.keys(resists).length ? resists : undefined,
    stats,
    evasion: evasion + g.qualityEvasion * q(2),
  };
  return armor;
}

export function gearName(inst: GearInstance): string {
  return `${inst.plus > 0 ? `+${inst.plus} ` : ''}${gearDef(inst.baseId).name}`;
}

export function rarityColor(inst: GearInstance): string {
  return RARITY[inst.rarity].color;
}

/** "Damage 41% · Hit 12% · Crit 88%" */
export function qualityText(inst: GearInstance): string {
  const lines = QUALITY_LINES[gearDef(inst.baseId).type];
  return lines.map((name, i) => `${name} ${inst.q[i] ?? 0}%${inst.q[i] >= 100 ? '!' : ''}`).join(' · ');
}

export function traitText(roll: TraitRoll): string {
  const t = traitDef(roll.id);
  return t.label.replace('{v}', String(roll.value)).replace('{e}', roll.element ? ELEMENT_LABELS[roll.element] : '');
}

/** Average quality, 0-100. */
export function avgQuality(inst: GearInstance): number {
  return Math.round(inst.q.reduce((a, b) => a + b, 0) / Math.max(1, inst.q.length));
}

export function gearValue(inst: GearInstance): number {
  const base = gearDef(inst.baseId);
  return Math.round(base.price * RARITY[inst.rarity].value * (1 + avgQuality(inst) / 100) * (1 + 0.15 * inst.plus));
}

export function sellPrice(inst: GearInstance): number {
  return Math.max(1, Math.round(gearValue(inst) * BALANCE.gear.sellShare));
}

export function itemSellPrice(itemId: string): number {
  return Math.max(1, Math.round(db.item(itemId).price * BALANCE.gear.sellShare));
}

// --- The smith ---

export interface Cost {
  items: { itemId: string; qty: number }[];
  gold: number;
}

export function canAfford(player: Player, cost: Cost): boolean {
  return player.gold >= cost.gold && cost.items.every((c) => player.countItem(c.itemId) >= c.qty);
}

function pay(player: Player, cost: Cost): void {
  player.data.gold -= cost.gold;
  for (const c of cost.items) player.removeItem(c.itemId, c.qty);
}

export function reinforceCost(inst: GearInstance): Cost {
  const base = gearDef(inst.baseId);
  return {
    items: [{ itemId: 'scrap', qty: inst.plus + 1 }],
    gold: Math.max(5, Math.round(base.price * 0.08 * (inst.plus + 1))),
  };
}

export function reinforceChance(inst: GearInstance): number {
  return BALANCE.gear.reinforceChance[inst.plus] ?? 0;
}

/** Failing at this plus loses a plus (not just the materials). */
export function reinforceRisky(inst: GearInstance): boolean {
  return inst.plus >= BALANCE.gear.reinforceRiskFrom;
}

export type ReinforceResult = 'success' | 'fail' | 'dropped' | 'invalid';

export function reinforce(player: Player, inst: GearInstance, rng: Rng = defaultRng): ReinforceResult {
  const cost = reinforceCost(inst);
  if (inst.plus >= BALANCE.gear.maxPlus || !canAfford(player, cost)) return 'invalid';
  pay(player, cost);
  if (chance(rng, reinforceChance(inst))) {
    inst.plus += 1;
    return 'success';
  }
  if (reinforceRisky(inst)) {
    inst.plus -= 1;
    return 'dropped';
  }
  return 'fail';
}

export function honeCost(inst: GearInstance): Cost {
  const level = gearDef(inst.baseId).level;
  return { items: [{ itemId: 'dust', qty: 1 + Math.floor(level / 5) }], gold: 10 + 3 * level };
}

export interface HoneResult {
  gain: number;
  /** Another line that slipped, if any. */
  drift?: { line: number; loss: number };
}

/** Raise one quality line. May make another line drift down, but never below the rarity's floor. */
export function hone(player: Player, inst: GearInstance, line: number, rng: Rng = defaultRng): HoneResult | null {
  const g = BALANCE.gear;
  const cost = honeCost(inst);
  if (inst.q[line] >= 100 || !canAfford(player, cost)) return null;
  pay(player, cost);
  const floor = RARITY[inst.rarity].qualityFloor;
  const before = inst.q[line];
  inst.q[line] = Math.min(100, before + randInt(rng, g.honeGain[0], g.honeGain[1]));
  const result: HoneResult = { gain: inst.q[line] - before };
  const others = inst.q.map((_, i) => i).filter((i) => i !== line && inst.q[i] > floor);
  if (others.length > 0 && chance(rng, g.honeDriftChance)) {
    const o = others[Math.floor(rng.next() * others.length)];
    const was = inst.q[o];
    inst.q[o] = clamp(was - randInt(rng, g.honeDrift[0], g.honeDrift[1]), floor, 100);
    result.drift = { line: o, loss: was - inst.q[o] };
  }
  return result;
}

const SALVAGE_DUST: Record<RarityId, number> = { common: 1, fine: 2, rare: 4, mythic: 8 };

/** What salvaging gives back. */
export function salvageYield(inst: GearInstance): { itemId: string; qty: number }[] {
  const base = gearDef(inst.baseId);
  const out = [
    { itemId: 'scrap', qty: 1 + Math.floor(base.level / 5) + Math.floor(inst.plus / 2) },
    { itemId: 'dust', qty: SALVAGE_DUST[inst.rarity] },
  ];
  const element = base.type === 'weapon' ? base.element : (Object.keys(base.resists ?? {}).find((k) => k !== 'all') as Element | undefined);
  if (element) out.push({ itemId: essenceId(element), qty: 1 + RARITIES.findIndex((r) => r.id === inst.rarity) });
  return out;
}

export function salvage(player: Player, inst: GearInstance): boolean {
  if (player.isEquipped(inst.uid)) return false;
  const yields = salvageYield(inst);
  if (!player.removeGear(inst.uid)) return false;
  for (const y of yields) player.addItem(y.itemId, y.qty);
  return true;
}

export function recipeCost(recipe: RecipeDef): Cost {
  return { items: recipe.materials, gold: recipe.gold };
}

/** Make a recipe. Returns the new gear (or null for non-gear results and failures). */
export function craft(player: Player, recipe: RecipeDef, rng: Rng = defaultRng): GearInstance | null {
  const cost = recipeCost(recipe);
  if (!canAfford(player, cost)) return null;
  pay(player, cost);
  if (!isGear(db.item(recipe.result))) {
    player.addItem(recipe.result);
    return null;
  }
  return player.addGear(rollGear(recipe.result, 'craft', rng));
}
