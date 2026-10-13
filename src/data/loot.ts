import type { LootTableDef } from './types';

/**
 * Gear loot by area. Monsters point at a table with `loot`; how often each tier drops depends on who dies
 * (`BALANCE.loot.mob / elite / boss`), not on how many items a tier holds.
 * - common: the area's plain gear (shop models, but dropped gear rolls quality)
 * - uncommon: the area's elemental gear
 * - rare: the area's boss weapons, a long-shot chase from normal monsters
 */
export const LOOT_TABLES: LootTableDef[] = [
  {
    id: 'thornwood',
    tiers: {
      common: [
        { itemId: 'iron_sword' },
        { itemId: 'oak_staff' },
        { itemId: 'hunting_knife' },
        { itemId: 'leather_armor' },
        { itemId: 'apprentice_robe' },
      ],
      uncommon: [{ itemId: 'tusk_dagger' }, { itemId: 'cinder_fang' }, { itemId: 'emberbrand' }, { itemId: 'ember_wand' }],
      rare: [{ itemId: 'thornheart_blade' }, { itemId: 'heartwood_staff' }, { itemId: 'briar_fang' }],
    },
  },
  {
    id: 'fen',
    tiers: {
      common: [
        { itemId: 'steel_broadsword' },
        { itemId: 'yew_staff' },
        { itemId: 'fenwatch_stiletto' },
        { itemId: 'scale_mail' },
        { itemId: 'reedweave_robe' },
      ],
      uncommon: [
        { itemId: 'tidebreaker' },
        { itemId: 'tidecaller_rod' },
        { itemId: 'brinefang' },
        { itemId: 'stormcleaver' },
        { itemId: 'sparkglass_wand' },
        { itemId: 'sparkfang' },
        { itemId: 'marshhide_vest' },
        { itemId: 'stormweave_robe' },
      ],
      rare: [{ itemId: 'thunderhead_maul' }, { itemId: 'stormcoil_scepter' }, { itemId: 'coilfang' }],
    },
  },
  {
    // The Bogmother's own: her rare tier is the three Water weapons.
    id: 'bogmother',
    tiers: {
      common: [{ itemId: 'scale_mail' }, { itemId: 'reedweave_robe' }, { itemId: 'marshhide_vest' }],
      uncommon: [{ itemId: 'stormcleaver' }, { itemId: 'sparkglass_wand' }, { itemId: 'sparkfang' }, { itemId: 'stormweave_robe' }],
      rare: [{ itemId: 'tidebreaker' }, { itemId: 'tidecaller_rod' }, { itemId: 'brinefang' }],
    },
  },
];
