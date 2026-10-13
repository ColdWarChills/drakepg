import { BALANCE } from '../config';
import { db } from '../data/db';
import type { LootTier } from '../data/types';
import { grantFor, type DropSource, type ItemGrant } from './Items';
import { chance, defaultRng, weightedPick, type Rng } from './rng';

/** Rarest first: the order tiers are rolled in. */
export const LOOT_TIERS: readonly LootTier[] = ['rare', 'uncommon', 'common'];

export type LootSource = Exclude<DropSource, 'quest'>;

/** Tiers this table can drop, with their odds for `source`. Empty tiers are skipped, so they never eat a roll. */
function tiersFor(tableId: string, source: LootSource): { tier: LootTier; odds: number }[] {
  const table = db.lootTable(tableId);
  return LOOT_TIERS.filter((t) => table.tiers[t].length > 0).map((tier) => ({ tier, odds: BALANCE.loot[source][tier] }));
}

/** Cascade: roll the rarest tier; only if it misses, roll the next. Undefined = no gear this time. */
export function rollLootTier(tableId: string, source: LootSource, rng: Rng = defaultRng): LootTier | undefined {
  for (const { tier, odds } of tiersFor(tableId, source)) if (chance(rng, odds)) return tier;
  return undefined;
}

/** The gear drop for one kill, rolled (quality, Sigil, mods) and ready to hand out; null if nothing dropped. */
export function rollLoot(tableId: string, source: LootSource, rng: Rng = defaultRng): ItemGrant | null {
  const tier = rollLootTier(tableId, source, rng);
  if (!tier) return null;
  const entry = weightedPick(rng, db.lootTable(tableId).tiers[tier], (e) => e.weight ?? 1);
  return grantFor(entry.itemId, rng, source);
}

/** Exact chance per kill of each tier (and of nothing), for the sim and the UI. */
export function tierOdds(tableId: string, source: LootSource): Record<LootTier | 'none', number> {
  const out = { rare: 0, uncommon: 0, common: 0, none: 1 };
  for (const { tier, odds } of tiersFor(tableId, source)) {
    out[tier] = out.none * odds;
    out.none -= out[tier];
  }
  return out;
}
