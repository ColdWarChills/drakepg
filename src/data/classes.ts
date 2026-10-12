import type { ClassDef } from './types';

export const CLASSES: ClassDef[] = [
  {
    id: 'warrior',
    name: 'Warrior',
    tagline: 'Tough and steady. Big HP, heavy hits.',
    stats: { str: 8, int: 2, dex: 5, end: 7 },
    growth: { str: 1, end: 1 },
    weapon: 'training_sword',
    armor: 'travel_garb',
    skills: [
      { skillId: 'heavy_strike', level: 1 },
      { skillId: 'second_wind', level: 2 },
      { skillId: 'skull_splitter', level: 4 },
    ],
    look: { shape: 'humanoid', color: 0x2e86de, accent: 0x5b3a1e, scale: 1, art: ['char_warrior', 'char_player'] },
  },
  {
    id: 'mage',
    name: 'Mage',
    tagline: 'Fragile, but burns and freezes everything.',
    stats: { str: 2, int: 9, dex: 5, end: 6 },
    growth: { int: 1, end: 1 },
    weapon: 'worn_staff',
    armor: 'novice_robe',
    skills: [
      { skillId: 'ember_bolt', level: 1 },
      { skillId: 'mend', level: 2 },
      { skillId: 'frost_lance', level: 3 },
      { skillId: 'meteor', level: 5 },
    ],
    look: { shape: 'humanoid', color: 0x7d3c98, accent: 0x2c1a3a, scale: 1, art: ['char_mage', 'char_player'] },
  },
  {
    id: 'rogue',
    name: 'Rogue',
    tagline: 'Quick and deadly. Strikes twice, crits often.',
    stats: { str: 6, int: 2, dex: 9, end: 5 },
    growth: { dex: 1, str: 1 },
    weapon: 'rusty_dagger',
    armor: 'travel_garb',
    skills: [
      { skillId: 'twin_cut', level: 1 },
      { skillId: 'bandage', level: 2 },
      { skillId: 'flurry', level: 3 },
      { skillId: 'assassinate', level: 5 },
    ],
    look: { shape: 'humanoid', color: 0x27ae60, accent: 0x1b2e1f, scale: 1, art: ['char_rogue', 'char_player'] },
  },
];
