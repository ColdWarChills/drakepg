import { BALANCE } from '../config';
import { essenceId } from '../data/crafting';
import { db } from '../data/db';
import type { Element, RecipeDef } from '../data/types';
import { gearDef, grade, gradeTier, rollKeys, rollQuality, type Gear, type GearDef, type GradeTier, type RollKey } from './Items';
import type { Player } from './Player';
import { chance, defaultRng, randInt, type Rng } from './rng';

/**
 * The smith: craft gear from recipes, reinforce it (+1 to +10), hone one quality line, salvage it into materials.
 * Costs scale with the item's shop price, since items have no level.
 */

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

// --- Craft ---

export function recipeCost(recipe: RecipeDef): Cost {
  return { items: recipe.materials, gold: recipe.gold };
}

/** Pay and make the recipe's gear: every line rolls at least `craftFloor`%. Null if unaffordable or the bag is full. */
export function craft(player: Player, recipe: RecipeDef, rng: Rng = defaultRng): Gear | null {
  const cost = recipeCost(recipe);
  if (!canAfford(player, cost) || player.freeSlots === 0) return null;
  pay(player, cost);
  const def = db.item(recipe.result) as GearDef;
  const rolls: Gear['rolls'] = {};
  for (const key of rollKeys(def)) rolls[key] = Math.max(BALANCE.forge.craftFloor, rollQuality(rng));
  const gear = player.makeGear(recipe.result, { rolls }, true);
  player.data.gear.push(gear);
  return gear;
}

// --- Reinforce ---

/** A weapon or armor piece's +. */
export function plusOf(gear: Gear): number {
  return gear.plus ?? 0;
}

export function reinforceCost(gear: Gear): Cost {
  const next = plusOf(gear) + 1;
  return { items: [{ itemId: 'scrap', qty: next }], gold: Math.max(5, Math.round(gearDef(gear).price * 0.05 * next)) };
}

export function reinforceChance(gear: Gear): number {
  return BALANCE.forge.reinforceChance[plusOf(gear)] ?? 0;
}

/** Failing at this + loses a + (not just the materials). */
export function reinforceRisky(gear: Gear): boolean {
  return plusOf(gear) >= BALANCE.forge.reinforceRiskFrom;
}

export type ReinforceResult = 'success' | 'fail' | 'dropped' | 'invalid';

export function reinforce(player: Player, gear: Gear, rng: Rng = defaultRng): ReinforceResult {
  const cost = reinforceCost(gear);
  if (plusOf(gear) >= BALANCE.forge.maxPlus || !canAfford(player, cost)) return 'invalid';
  pay(player, cost);
  if (chance(rng, reinforceChance(gear))) {
    gear.plus = plusOf(gear) + 1;
    return 'success';
  }
  if (reinforceRisky(gear)) {
    gear.plus = plusOf(gear) - 1;
    return 'dropped';
  }
  return 'fail';
}

// --- Hone ---

export function honeCost(gear: Gear): Cost {
  const price = gearDef(gear).price;
  return { items: [{ itemId: 'dust', qty: 1 + Math.floor(price / 200) }], gold: 10 + Math.round(price * 0.05) };
}

export interface HoneResult {
  gain: number;
  /** Another line that slipped, if any. */
  drift?: { key: RollKey; loss: number };
}

/** Raise one quality line. May make another line drift down. Null if maxed or unaffordable. */
export function hone(player: Player, gear: Gear, key: RollKey, rng: Rng = defaultRng): HoneResult | null {
  const f = BALANCE.forge;
  const cost = honeCost(gear);
  const before = gear.rolls[key] ?? 0;
  if (before >= 100 || !canAfford(player, cost)) return null;
  pay(player, cost);
  gear.rolls[key] = Math.min(100, before + randInt(rng, f.honeGain[0], f.honeGain[1]));
  const result: HoneResult = { gain: gear.rolls[key]! - before };
  const others = rollKeys(gearDef(gear)).filter((k) => k !== key && (gear.rolls[k] ?? 0) > 0);
  if (others.length > 0 && chance(rng, f.honeDriftChance)) {
    const k = others[Math.floor(rng.next() * others.length)];
    const was = gear.rolls[k] ?? 0;
    gear.rolls[k] = Math.max(0, was - randInt(rng, f.honeDrift[0], f.honeDrift[1]));
    result.drift = { key: k, loss: was - gear.rolls[k]! };
  }
  return result;
}

// --- Salvage ---

const SALVAGE_DUST: Record<GradeTier, number> = { standard: 0, fair: 1, fine: 2, superior: 3, masterwork: 5 };
const SIGIL_DUST = { dawn: 3, dusk: 6, eclipse: 12 };

/** The element salvage gives an essence of: a weapon's element, or armor's best resist. */
function gearElement(gear: Gear): Element | undefined {
  const def = gearDef(gear);
  if (def.type === 'weapon') return def.element;
  const best = Object.entries(def.resists ?? {})
    .filter(([k, v]) => k !== 'all' && (v ?? 0) > 0)
    .sort((a, b) => (b[1] ?? 0) - (a[1] ?? 0))[0];
  return best?.[0] as Element | undefined;
}

/** What salvaging gives back. */
export function salvageYield(gear: Gear): { itemId: string; qty: number }[] {
  const def = gearDef(gear);
  const out = [{ itemId: 'scrap', qty: 1 + Math.floor(def.price / 100) + Math.floor(plusOf(gear) / 2) }];
  const dust = SALVAGE_DUST[gradeTier(grade(def, gear.rolls)).tier] + (gear.sigil ? SIGIL_DUST[gear.sigil] : 0);
  if (dust) out.push({ itemId: 'dust', qty: dust });
  const element = gearElement(gear);
  if (element) out.push({ itemId: essenceId(element), qty: 1 + Math.floor(def.price / 150) });
  return out;
}

/** Break bag gear into materials. Worn and locked gear can't be salvaged. */
export function canSalvage(player: Player, gear: Gear): boolean {
  return !gear.locked && player.data.gear.includes(gear);
}

export function salvage(player: Player, gear: Gear): boolean {
  if (!canSalvage(player, gear)) return false;
  const yields = salvageYield(gear);
  player.data.gear = player.data.gear.filter((g) => g !== gear);
  for (const y of yields) player.addItem(y.itemId, y.qty);
  return true;
}

/** "Iron Scrap 2/3 · 25 g": owned/needed per material. */
export function costText(player: Player, cost: Cost): string {
  const parts = cost.items.map((c) => `${db.item(c.itemId).name} ${player.countItem(c.itemId)}/${c.qty}`);
  if (cost.gold) parts.push(`${cost.gold} g`);
  return parts.join(' · ');
}
