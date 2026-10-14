import type { LootTableDef } from './types';

/**
 * Gear loot by area. Monsters point at a table with `loot`; how often each tier drops depends on who dies
 * (`BALANCE.loot.mob / elite / boss`), not on how many items a tier holds.
 * - common: the area's plain gear (shop models, but dropped gear rolls quality)
 * - uncommon: the area's elemental gear and its rings and necklaces
 * - rare: the area's boss weapons, a long-shot chase from normal monsters
 * `set`: the area's gear set, which Elites and bosses may drop on top (`BALANCE.setDrop`).
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
        { itemId: 'leather_cap' },
        { itemId: 'leather_gloves' },
        { itemId: 'leather_boots' },
        { itemId: 'apprentice_hood' },
        { itemId: 'apprentice_wraps' },
        { itemId: 'apprentice_slippers' },
      ],
      uncommon: [
        { itemId: 'tusk_dagger' },
        { itemId: 'cinder_fang' },
        { itemId: 'emberbrand' },
        { itemId: 'ember_wand' },
        { itemId: 'copper_band' },
        { itemId: 'moonstone_ring' },
        { itemId: 'fox_ring' },
        { itemId: 'oak_pendant' },
      ],
      rare: [{ itemId: 'thornheart_blade' }, { itemId: 'heartwood_staff' }, { itemId: 'briar_fang' }],
    },
    set: 'thornwarden',
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
        { itemId: 'scale_coif' },
        { itemId: 'scale_gauntlets' },
        { itemId: 'scale_boots' },
        { itemId: 'reedweave_hood' },
        { itemId: 'reedweave_wraps' },
        { itemId: 'reedweave_slippers' },
      ],
      uncommon: [
        { itemId: 'silver_band' },
        { itemId: 'tidepearl_ring' },
        { itemId: 'eelskin_ring' },
        { itemId: 'stormglass_amulet' },
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
    set: 'stormwatch',
  },
  {
    // The Bogmother's own: her rare tier is the three Water weapons.
    id: 'bogmother',
    tiers: {
      common: [{ itemId: 'scale_mail' }, { itemId: 'reedweave_robe' }, { itemId: 'marshhide_vest' }],
      uncommon: [{ itemId: 'stormcleaver' }, { itemId: 'sparkglass_wand' }, { itemId: 'sparkfang' }, { itemId: 'stormweave_robe' }],
      rare: [{ itemId: 'tidebreaker' }, { itemId: 'tidecaller_rod' }, { itemId: 'brinefang' }],
    },
    set: 'stormwatch',
  },
  {
    id: 'frostfang',
    tiers: {
      common: [
        { itemId: 'tempered_longsword' },
        { itemId: 'larchwood_staff' },
        { itemId: 'ridgerunner_kris' },
        { itemId: 'ridgeplate' },
        { itemId: 'fleecewool_robe' },
        { itemId: 'ridgeplate_helm' },
        { itemId: 'ridgeplate_gauntlets' },
        { itemId: 'ridgeplate_boots' },
        { itemId: 'fleecewool_hood' },
        { itemId: 'fleecewool_mittens' },
        { itemId: 'fleecewool_boots' },
      ],
      uncommon: [
        { itemId: 'gold_band' },
        { itemId: 'rimeglass_ring' },
        { itemId: 'wolfclaw_ring' },
        { itemId: 'hearthstone_pendant' },
        { itemId: 'frostbite' },
        { itemId: 'hoarfrost_staff' },
        { itemId: 'icicle_kris' },
        { itemId: 'hearthfire_blade' },
        { itemId: 'kindling_rod' },
        { itemId: 'salamander_kris' },
        { itemId: 'frostpelt_vest' },
        { itemId: 'galeweave_robe' },
      ],
      rare: [{ itemId: 'whiteout' }, { itemId: 'squall_stave' }, { itemId: 'galecutter' }],
    },
    set: 'frostward',
  },
  {
    // Old Snowmaw's own: its rare tier is the three weapons made from it.
    id: 'snowmaw',
    tiers: {
      common: [{ itemId: 'ridgeplate' }, { itemId: 'fleecewool_robe' }, { itemId: 'frostpelt_vest' }],
      uncommon: [{ itemId: 'hearthfire_blade' }, { itemId: 'kindling_rod' }, { itemId: 'salamander_kris' }, { itemId: 'galeweave_robe' }],
      rare: [{ itemId: 'snowmaw_cleaver' }, { itemId: 'mawtooth_staff' }, { itemId: 'snowmaw_claw' }],
    },
    set: 'frostward',
  },
];
