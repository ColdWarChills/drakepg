import type { AccessoryDef, ArmorDef, ArmorSlot, Defenses } from './types';

/**
 * Helms, gloves, boots, rings, necklaces and set pieces. Body armor and weapons are in items.ts.
 *
 * Armor of one tier is split by slot: body ~70% of the tier's defense, helm ~20%, gloves and boots ~15% each,
 * so a full kit is about 20% stronger than the body piece alone used to be. Heavy pieces guard against melee and
 * pierce, light (robe) pieces against magic. Accessories have no defense; everything they give scales with
 * their Power roll.
 */

/** Pieces are written without a tier; `tiered` stamps one on a whole group (it caps modifier lines, `BALANCE.modifierSlots`). */
type Untiered<T> = Omit<T, 'tier'>;
const tiered = (tier: number, items: (Untiered<ArmorDef> | Untiered<AccessoryDef>)[]) => items.map((i) => ({ ...i, tier }) as ArmorDef | AccessoryDef);

const armor = (
  id: string,
  slot: ArmorSlot,
  name: string,
  description: string,
  price: number,
  iconColor: number,
  defense: Defenses,
  extra: Partial<ArmorDef> = {},
): Untiered<ArmorDef> => ({ id, type: 'armor', slot, name, description, price, iconColor, defense, ...extra });

const ring = (id: string, name: string, description: string, price: number, iconColor: number, extra: Partial<AccessoryDef>): Untiered<AccessoryDef> => ({
  id,
  type: 'accessory',
  slot: 'ring',
  name,
  description,
  price,
  iconColor,
  ...extra,
});

const necklace = (id: string, name: string, description: string, price: number, iconColor: number, extra: Partial<AccessoryDef>): Untiered<AccessoryDef> => ({
  ...ring(id, name, description, price, iconColor, extra),
  slot: 'necklace',
});

export const EQUIPMENT: (ArmorDef | AccessoryDef)[] = [
  // Tier 1: Brindlemoor shop and the Thornwood
  ...tiered(1, [
    armor('leather_cap', 'helm', 'Leather Cap', 'Boiled leather. Keeps branches out of your eyes.', 30, 0x8a5a32, { melee: 2, pierce: 2, magic: 1 }),
    armor('leather_gloves', 'gloves', 'Leather Gloves', 'Stiff at first, then like a second skin.', 25, 0x8a5a32, { melee: 1, pierce: 2, magic: 1 }),
    armor('leather_boots', 'boots', 'Leather Boots', 'Made for long roads and wet paths.', 25, 0x7a4e2b, { melee: 1, pierce: 2, magic: 1 }),
    armor('apprentice_hood', 'helm', 'Apprentice Hood', 'Warding thread stitched round the rim.', 30, 0xa83232, { melee: 1, pierce: 1, magic: 2 }),
    armor('apprentice_wraps', 'gloves', 'Apprentice Wraps', 'Cloth wraps that keep the fingers nimble for spells.', 25, 0xa83232, { melee: 1, pierce: 1, magic: 2 }),
    armor('apprentice_slippers', 'boots', 'Apprentice Slippers', 'Soft soles, quiet steps.', 25, 0x8a2a2a, { melee: 1, pierce: 1, magic: 2 }),
    ring('copper_band', 'Copper Band', 'A plain band that makes the arm feel heavier, in a good way.', 60, 0xc87533, { stats: { str: 2 } }),
    ring('moonstone_ring', 'Moonstone Ring', 'Cool to the touch, and quietly humming.', 60, 0xc9d8f0, { stats: { int: 2 }, mp: 5 }),
    ring('fox_ring', 'Fox Ring', 'A tiny fox curls around the finger.', 60, 0xe07a2e, { stats: { dex: 2 } }),
    necklace('oak_pendant', 'Oak Pendant', 'A carved acorn on a leather cord.', 70, 0x8a6d3b, { hp: 12 }),
  ]),

  // Tier 2: Reedwatch shop and the Mirewater Fen
  ...tiered(2, [
    armor('scale_coif', 'helm', 'Scale Coif', 'A hood of small steel scales.', 100, 0x9aa4ad, { melee: 3, pierce: 3, magic: 1 }),
    armor('scale_gauntlets', 'gloves', 'Scale Gauntlets', 'Scaled cuffs over padded leather.', 80, 0x9aa4ad, { melee: 2, pierce: 2, magic: 1 }),
    armor('scale_boots', 'boots', 'Scale Boots', 'Heavy, but they keep the bog out.', 80, 0x7d868e, { melee: 2, pierce: 2, magic: 1 }),
    armor('reedweave_hood', 'helm', 'Reedweave Hood', 'Woven reeds with a faint blue glow in the weave.', 100, 0xa8a24a, { melee: 1, pierce: 1, magic: 4 }),
    armor('reedweave_wraps', 'gloves', 'Reedweave Wraps', 'Light, dry and covered in tiny runes.', 80, 0xa8a24a, { melee: 1, pierce: 1, magic: 3 }),
    armor('reedweave_slippers', 'boots', 'Reedweave Slippers', 'They float. Nobody knows why.', 80, 0x8f8a3a, { melee: 1, pierce: 1, magic: 3 }),
    ring('silver_band', 'Silver Band', 'Polished smooth by years of sword grips.', 160, 0xc0c8d0, { stats: { str: 3, end: 1 } }),
    ring('tidepearl_ring', 'Tidepearl Ring', 'A pearl that is always a little damp.', 160, 0x9fd8e8, { stats: { int: 3 }, resists: { water: 10 } }),
    ring('eelskin_ring', 'Eelskin Ring', 'Slippery. Makes your hands quicker, somehow.', 160, 0x4a6b6b, { stats: { dex: 3 }, crit: 1 }),
    necklace('stormglass_amulet', 'Stormglass Amulet', 'A bead of fused glass with a spark trapped inside.', 180, 0xffd21f, { hp: 20, resists: { energy: 10 } }),
  ]),

  // Tier 3: Hearthold shop and the Frostfang mountains
  ...tiered(3, [
    armor('ridgeplate_helm', 'helm', 'Ridgeplate Helm', 'Thick steel over a fur lining.', 220, 0xb0bec5, { melee: 4, pierce: 4, magic: 2 }),
    armor('ridgeplate_gauntlets', 'gloves', 'Ridgeplate Gauntlets', 'Fur-lined, so fingers still bend in the cold.', 180, 0xb0bec5, { melee: 3, pierce: 3, magic: 1 }),
    armor('ridgeplate_boots', 'boots', 'Ridgeplate Boots', 'Spiked soles for ice.', 180, 0x90a4ae, { melee: 3, pierce: 3, magic: 1 }),
    armor('fleecewool_hood', 'helm', 'Fleecewool Hood', 'Warm wool with blue warding runes.', 220, 0xf0e6d2, { melee: 2, pierce: 2, magic: 5 }),
    armor('fleecewool_mittens', 'gloves', 'Fleecewool Mittens', 'Spellwork in mittens. Hearthold insists.', 180, 0xf0e6d2, { melee: 1, pierce: 1, magic: 4 }),
    armor('fleecewool_boots', 'boots', 'Fleecewool Boots', 'Fur inside, runes outside.', 180, 0xd8ccb4, { melee: 1, pierce: 1, magic: 4 }),
    ring('gold_band', 'Gold Band', 'Heavy gold. Heavy hands.', 320, 0xf1c40f, { stats: { str: 4, end: 2 } }),
    ring('rimeglass_ring', 'Rimeglass Ring', 'Ice that never melts, set in silver.', 320, 0xb3e5fc, { stats: { int: 4 }, resists: { ice: 10 } }),
    ring('wolfclaw_ring', 'Wolfclaw Ring', 'A claw curled into a ring. Still sharp.', 320, 0xeceff1, { stats: { dex: 4 }, crit: 2 }),
    necklace('hearthstone_pendant', 'Hearthstone Pendant', 'A warm red stone from the Steamvent Caves.', 360, 0xd84315, { hp: 30, resists: { fire: 10 } }),
  ]),

  // Sets (src/data/sets.ts). Hybrid pieces anyone can wear; Elites and bosses of the area drop them, Hulda crafts them.
  ...tiered(1, [
    armor('thornwarden_helm', 'helm', 'Thornwarden Helm', 'Bark and bramble, grown into a helm.', 110, 0x5e8c3a, { melee: 2, pierce: 2, magic: 2 }, { set: 'thornwarden', stats: { end: 1 } }),
    armor('thornwarden_gloves', 'gloves', 'Thornwarden Gloves', 'Thorny knuckles. Mind the handshake.', 90, 0x5e8c3a, { melee: 2, pierce: 1, magic: 1 }, { set: 'thornwarden' }),
    armor('thornwarden_boots', 'boots', 'Thornwarden Boots', 'Roots for laces.', 90, 0x4f7a30, { melee: 1, pierce: 2, magic: 1 }, { set: 'thornwarden' }),
    ring('thornwarden_band', 'Thornwarden Band', 'A living thorn ring. It grows a little each spring.', 110, 0x7aa84a, { set: 'thornwarden', stats: { end: 2 }, resists: { earth: 5 } }),
  ]),
  ...tiered(2, [
    armor('stormwatch_helm', 'helm', 'Stormwatch Helm', 'Copper-crested. Lightning likes it, and it likes lightning.', 260, 0x3a6ea5, { melee: 3, pierce: 3, magic: 3 }, { set: 'stormwatch', stats: { end: 1 } }),
    armor('stormwatch_gloves', 'gloves', 'Stormwatch Gloves', 'Oiled hide with copper studs.', 220, 0x3a6ea5, { melee: 2, pierce: 2, magic: 2 }, { set: 'stormwatch' }),
    armor('stormwatch_boots', 'boots', 'Stormwatch Boots', 'Waterproof, mostly.', 220, 0x2f5a88, { melee: 2, pierce: 2, magic: 2 }, { set: 'stormwatch' }),
    necklace('stormwatch_amulet', 'Stormwatch Amulet', 'A storm scale on a copper chain.', 260, 0x7ab8ff, { set: 'stormwatch', hp: 15, mp: 10 }),
  ]),
  ...tiered(3, [
    armor('frostward_helm', 'helm', 'Frostward Helm', 'Rime-steel that never fogs.', 520, 0xb3e5fc, { melee: 4, pierce: 4, magic: 4 }, { set: 'frostward', stats: { end: 2 } }),
    armor('frostward_gloves', 'gloves', 'Frostward Gloves', 'White fur cuffs over rime-steel.', 440, 0xb3e5fc, { melee: 3, pierce: 3, magic: 2 }, { set: 'frostward' }),
    armor('frostward_boots', 'boots', 'Frostward Boots', 'Each step leaves a little frost.', 440, 0x90caf9, { melee: 3, pierce: 3, magic: 2 }, { set: 'frostward' }),
    ring('frostward_ring', 'Frostward Ring', 'A sliver of glacier set in white gold.', 520, 0xe1f5fe, { set: 'frostward', stats: { end: 3 }, resists: { ice: 10 } }),
  ]),
];
