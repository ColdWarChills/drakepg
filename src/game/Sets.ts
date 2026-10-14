import { db } from '../data/db';
import type { EquipSlot, Resists, SetBonus, SetDef, Stats } from '../data/types';
import { gearDef, type Gear } from './Items';

/**
 * Gear sets: wearing several pieces of one set turns on its bonuses (at 2 pieces, at 4...). The data is in
 * src/data/sets.ts; a piece names its set in `set`. Two copies of the same piece count once.
 */

export interface SetBonusTotals {
  stats: Partial<Stats>;
  hpPct: number;
  crit: number;
  resists: Resists;
  damagePct: number;
  defensePct: number;
}

/** The set an item belongs to, if any. */
export function setOfItem(itemId: string): SetDef | undefined {
  const def = db.item(itemId);
  return (def.type === 'armor' || def.type === 'accessory') && def.set ? db.set(def.set) : undefined;
}

/** Different pieces of each set being worn. */
export function setCounts(equipment: Record<EquipSlot, Gear | null>): Map<string, number> {
  const pieces = new Map<string, Set<string>>();
  for (const g of Object.values(equipment)) {
    if (!g) continue;
    const def = gearDef(g);
    if (def.type === 'weapon' || !def.set) continue;
    if (!pieces.has(def.set)) pieces.set(def.set, new Set());
    pieces.get(def.set)!.add(def.id);
  }
  return new Map([...pieces].map(([id, s]) => [id, s.size]));
}

/** Bonuses switched on by what is worn. */
export function activeSetBonuses(equipment: Record<EquipSlot, Gear | null>): { set: SetDef; bonus: SetBonus }[] {
  const out: { set: SetDef; bonus: SetBonus }[] = [];
  for (const [id, count] of setCounts(equipment)) {
    const set = db.set(id);
    for (const bonus of set.bonuses) if (count >= bonus.pieces) out.push({ set, bonus });
  }
  return out;
}

/** Everything the active set bonuses add up to. */
export function setBonusTotals(equipment: Record<EquipSlot, Gear | null>): SetBonusTotals {
  const t: SetBonusTotals = { stats: {}, hpPct: 0, crit: 0, resists: {}, damagePct: 0, defensePct: 0 };
  for (const { bonus: b } of activeSetBonuses(equipment)) {
    for (const [k, v] of Object.entries(b.stats ?? {}) as [keyof Stats, number][]) t.stats[k] = (t.stats[k] ?? 0) + v;
    for (const [k, v] of Object.entries(b.resists ?? {}) as [keyof Resists, number][]) t.resists[k] = (t.resists[k] ?? 0) + v;
    t.hpPct += b.hpPct ?? 0;
    t.crit += b.crit ?? 0;
    t.damagePct += b.damagePct ?? 0;
    t.defensePct += b.defensePct ?? 0;
  }
  return t;
}
