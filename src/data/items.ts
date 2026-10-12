import type { ItemDef } from './types';

export const ITEMS: ItemDef[] = [
  // Consumables
  {
    id: 'health_potion',
    type: 'consumable',
    name: 'Health Potion',
    description: 'Restores 40 HP.',
    price: 15,
    restore: { hp: 40 },
  },
  {
    id: 'mana_potion',
    type: 'consumable',
    name: 'Mana Potion',
    description: 'Restores 20 MP.',
    price: 15,
    restore: { mp: 20 },
  },

  // Weapons
  {
    id: 'training_sword',
    type: 'weapon',
    name: 'Training Sword',
    description: 'Dull, but it beats fists.',
    price: 10,
    damage: [4, 7],
    damageType: 'physical',
  },
  {
    id: 'iron_sword',
    type: 'weapon',
    name: 'Iron Sword',
    description: 'A sturdy blade from the town forge.',
    price: 120,
    damage: [7, 11],
    damageType: 'physical',
  },
  {
    id: 'oak_staff',
    type: 'weapon',
    name: 'Oak Staff',
    description: 'Hits with magic. Scales with INT.',
    price: 100,
    damage: [5, 9],
    damageType: 'magic',
    stats: { int: 3 },
  },
  {
    id: 'tusk_dagger',
    type: 'weapon',
    name: 'Tusk Dagger',
    description: 'Carved from a boar tusk. Quick and sharp.',
    price: 90,
    damage: [6, 9],
    damageType: 'physical',
    stats: { dex: 2 },
  },

  // Armor
  {
    id: 'travel_garb',
    type: 'armor',
    name: "Traveler's Garb",
    description: 'Simple cloth. Better than nothing.',
    price: 10,
    defense: 3,
    resist: 3,
  },
  {
    id: 'leather_armor',
    type: 'armor',
    name: 'Leather Armor',
    description: 'Good against claws and blades.',
    price: 80,
    defense: 10,
    resist: 4,
    stats: { end: 1 },
  },
  {
    id: 'apprentice_robe',
    type: 'armor',
    name: 'Apprentice Robe',
    description: 'Woven with warding runes.',
    price: 90,
    defense: 4,
    resist: 12,
    stats: { int: 2 },
  },
];
