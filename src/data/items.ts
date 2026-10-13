import { MATERIALS } from './crafting';
import { GEAR_ITEMS } from './gear';
import type { ConsumableDef, ItemDef } from './types';

const CONSUMABLES: ConsumableDef[] = [
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
];

/** Weapons and armor are generated from gear lines in gear.ts; materials live in crafting.ts. */
export const ITEMS: ItemDef[] = [...CONSUMABLES, ...MATERIALS, ...GEAR_ITEMS];
