import type { EliteAffixDef } from './types';

/**
 * Elite affixes. An Elite is a roaming monster with more HP and damage (BALANCE.elite) plus one of these.
 * The affix name goes in front of the monster's name and its description shows in battle.
 */
export const ELITE_AFFIXES: EliteAffixDef[] = [
  { id: 'hulking', name: 'Hulking', description: '+50% HP.', hp: 0.5 },
  { id: 'frenzied', name: 'Frenzied', description: 'Deals 25% more damage.', damage: 0.25 },
  { id: 'warded', name: 'Warded', description: '+25 to all resistances.', resists: { all: 25 } },
  {
    id: 'venomous',
    name: 'Venomous',
    description: 'Its attacks may poison (30%).',
    applies: [{ status: 'poison', turns: 3, chance: 0.3 }],
  },
  { id: 'ironhide', name: 'Ironhide', description: '+50% defense.', defense: 0.5 },
];
