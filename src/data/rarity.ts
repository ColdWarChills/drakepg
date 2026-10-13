import type { RarityDef, RarityId, TraitDef } from './types';

/**
 * Any dropped or crafted item can roll a rarity. Rarer items have a higher quality floor
 * (their lines never roll, hone or drift below it), bonus traits and a little extra power.
 * Shop gear is always Common at 0% quality.
 */
export const RARITIES: RarityDef[] = [
  { id: 'common', name: 'Common', color: '#e8e0cc', dropWeight: 85, craftWeight: 60, qualityFloor: 0, traits: 0, power: 0, value: 1 },
  { id: 'fine', name: 'Fine', color: '#7ddc7d', dropWeight: 11, craftWeight: 28, qualityFloor: 15, traits: 1, power: 0.05, value: 1.6 },
  { id: 'rare', name: 'Rare', color: '#5cc3ff', dropWeight: 3.5, craftWeight: 10, qualityFloor: 30, traits: 2, power: 0.1, value: 3 },
  { id: 'mythic', name: 'Mythic', color: '#ffb347', dropWeight: 0.5, craftWeight: 2, qualityFloor: 50, traits: 3, power: 0.2, value: 6 },
];

export const RARITY: Record<RarityId, RarityDef> = Object.fromEntries(RARITIES.map((r) => [r.id, r])) as Record<RarityId, RarityDef>;

/** Bonus lines rolled on Fine and better gear. Values grow with the item's level. */
export const TRAITS: TraitDef[] = [
  { id: 'might', label: '+{v} STR', slots: ['weapon', 'armor'], kind: 'stat', stat: 'str', range: [1, 2], perLevel: 0.25 },
  { id: 'wit', label: '+{v} INT', slots: ['weapon', 'armor'], kind: 'stat', stat: 'int', range: [1, 2], perLevel: 0.25 },
  { id: 'grace', label: '+{v} DEX', slots: ['weapon', 'armor'], kind: 'stat', stat: 'dex', range: [1, 2], perLevel: 0.25 },
  { id: 'vigor', label: '+{v} END', slots: ['weapon', 'armor'], kind: 'stat', stat: 'end', range: [1, 2], perLevel: 0.25 },
  { id: 'warding', label: '{e} resist +{v}', slots: ['armor'], kind: 'resist', range: [8, 14], perLevel: 0.5 },
  { id: 'keen', label: 'Crit +{v}%', slots: ['weapon'], kind: 'crit', range: [2, 4], perLevel: 0.1, percent: true },
  { id: 'true', label: 'Hit +{v}%', slots: ['weapon'], kind: 'hit', range: [2, 5], perLevel: 0.1, percent: true },
  { id: 'nimble', label: 'Evasion +{v}%', slots: ['armor'], kind: 'evasion', range: [2, 4], perLevel: 0.1, percent: true },
];
