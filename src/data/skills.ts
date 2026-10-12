import type { SkillDef } from './types';

export const SKILLS: SkillDef[] = [
  {
    id: 'heavy_strike',
    name: 'Heavy Strike',
    description: 'A slow, crushing blow. 1.6x physical damage.',
    mpCost: 6,
    kind: 'damage',
    damageType: 'physical',
    power: 1.6,
    fx: 'heavy',
  },
  {
    id: 'ember_bolt',
    name: 'Ember Bolt',
    description: 'Hurl a bolt of fire. 1.8x magic damage.',
    mpCost: 8,
    kind: 'damage',
    damageType: 'magic',
    power: 1.8,
  },
  {
    id: 'twin_cut',
    name: 'Twin Cut',
    description: 'Two quick slashes at 0.75x each.',
    mpCost: 5,
    kind: 'damage',
    damageType: 'physical',
    power: 0.75,
    hits: 2,
  },
  {
    id: 'mend',
    name: 'Mend',
    description: 'Heal 30% of max HP.',
    mpCost: 10,
    kind: 'heal',
    power: 0.3,
  },
];

/** Skills every new character starts with. */
export const STARTING_SKILLS = ['heavy_strike', 'ember_bolt', 'twin_cut', 'mend'];
