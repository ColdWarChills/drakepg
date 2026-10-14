import { BALANCE } from '../config';
import { db } from '../data/db';
import type { LootTier } from '../data/types';
import { grantFor, type DropSource, type ItemGrant } from './Items';
import { chance, defaultRng, weightedPick, type Rng } from './rng';

/** Rarest first: the order tiers are rolled in. */
export const LOOT_TIERS: readonly LootTier[] = ['rare', 'uncommon', 'common'];

export type LootSource = Exclude<DropSource, 'quest'>;

/** Extra Rare chance after `misses` kills on a table without a Rare (bad-luck protection). */
export function pityBonus(misses: number): number {
  const l = BALANCE.loot;
  return Math.min(l.pityMax, Math.max(0, misses - l.pityAfter) * l.pityStep);
}

/** Tiers this table can drop, with their odds. Empty tiers are skipped, so they never eat a roll. */
function tiersFor(tableId: string, source: LootSource, misses: number): { tier: LootTier; odds: number }[] {
  const table = db.lootTable(tableId);
  const odds = (source === 'mob' && BALANCE.loot.tableMob[tableId]) || BALANCE.loot[source];
  return LOOT_TIERS.filter((t) => table.tiers[t].length > 0).map((tier) => {
    const base = odds[tier];
    return { tier, odds: tier === 'rare' ? Math.min(1, base + pityBonus(misses)) : base };
  });
}

/** Cascade: roll the rarest tier; only if it misses, roll the next. Undefined = no gear this time. */
export function rollLootTier(tableId: string, source: LootSource, rng: Rng = defaultRng, misses = 0): LootTier | undefined {
  for (const { tier, odds } of tiersFor(tableId, source, misses)) if (chance(rng, odds)) return tier;
  return undefined;
}

/** One kill's gear: the tier that hit (if any) and the rolled item (quality, Sigil, mods). */
export function rollLoot(tableId: string, source: LootSource, rng: Rng = defaultRng, misses = 0, extras = true): { tier: LootTier; grant: ItemGrant } | null {
  const tier = rollLootTier(tableId, source, rng, misses);
  if (!tier) return null;
  const entry = weightedPick(rng, db.lootTable(tableId).tiers[tier], (e) => e.weight ?? 1);
  return { tier, grant: grantFor(entry.itemId, rng, source, extras) };
}

/** Exact chance per kill of each tier (and of nothing). Shown in battle and printed by the sim. */
export function tierOdds(tableId: string, source: LootSource, misses = 0): Record<LootTier | 'none', number> {
  const out = { rare: 0, uncommon: 0, common: 0, none: 1 };
  for (const { tier, odds } of tiersFor(tableId, source, misses)) {
    out[tier] = out.none * odds;
    out.none -= out[tier];
  }
  return out;
}
