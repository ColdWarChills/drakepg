import type { SetDef } from './types';

/**
 * Gear sets, one per chapter area. Wear 2 or 4 different pieces of a set to turn its bonuses on (rules in
 * src/game/Sets.ts). Pieces drop from the area's Elites and bosses (`BALANCE.setDrop`, the loot table's `set`) and
 * Hulda crafts them. Items are in src/data/equipment.ts.
 */
export const SETS: SetDef[] = [
  {
    id: 'thornwarden',
    name: 'Thornwarden',
    pieces: ['thornwarden_helm', 'thornwarden_gloves', 'thornwarden_boots', 'thornwarden_band'],
    bonuses: [
      { pieces: 2, hpPct: 8 },
      { pieces: 4, damagePct: 8, resists: { earth: 15 } },
    ],
  },
  {
    id: 'stormwatch',
    name: 'Stormwatch',
    pieces: ['stormwatch_helm', 'stormwatch_gloves', 'stormwatch_boots', 'stormwatch_amulet'],
    bonuses: [
      { pieces: 2, resists: { water: 15, energy: 15 } },
      { pieces: 4, crit: 4, damagePct: 8 },
    ],
  },
  {
    id: 'frostward',
    name: 'Frostward',
    pieces: ['frostward_helm', 'frostward_gloves', 'frostward_boots', 'frostward_ring'],
    bonuses: [
      { pieces: 2, defensePct: 10, resists: { ice: 15 } },
      { pieces: 4, hpPct: 10, damagePct: 10 },
    ],
  },
];
