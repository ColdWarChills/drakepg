import { ELEMENTS, ELEMENT_LABELS } from './elements';
import type { Element, MaterialDef, RecipeDef } from './types';

/** One essence per element. Elemental monsters drop them; salvaging elemental gear gives them back. */
export function essenceId(element: Element): string {
  return `essence_${element}`;
}

const ESSENCE_ICON: Record<Element, number> = {
  fire: 0xff7a2e,
  water: 0x2f8fe0,
  ice: 0x9fe6ff,
  wind: 0x6fd6a0,
  earth: 0xb08a3c,
  energy: 0xffd21f,
  light: 0xfff2b0,
  dark: 0x8a5cd6,
};

export const MATERIALS: MaterialDef[] = [
  { id: 'scrap', type: 'material', name: 'Iron Scrap', description: 'Salvaged metal. The smith uses it to craft and reinforce.', price: 6, iconColor: 0x8a8f96 },
  { id: 'dust', type: 'material', name: 'Honing Dust', description: 'Ground from salvaged gear. Used to hone quality.', price: 12, iconColor: 0xc9b8ff },
  { id: 'moss_clump', type: 'material', name: 'Moss Clump', description: 'Springy, damp and oddly tough.', price: 3, iconColor: 0x5e9c4a },
  { id: 'boar_tusk', type: 'material', name: 'Boar Tusk', description: 'Hard as bone and twice as sharp.', price: 6, iconColor: 0xe8dcc0 },
  { id: 'fen_hide', type: 'material', name: 'Fen Hide', description: 'Slick, waterproof hide from the Mirewater Fen.', price: 10, iconColor: 0x4a6b4a },
  { id: 'storm_scale', type: 'material', name: 'Storm Scale', description: 'A drakeling scale that still crackles.', price: 14, iconColor: 0x7ab8ff },
  { id: 'frost_pelt', type: 'material', name: 'Frost Pelt', description: 'Thick white wolf fur, stiff with ice.', price: 16, iconColor: 0xeceff1 },
  { id: 'ram_horn', type: 'material', name: 'Ram Horn', description: 'A curled horn, scarred from a hundred head-butts.', price: 16, iconColor: 0xbcaaa4 },
  { id: 'rime_shard', type: 'material', name: 'Rime Shard', description: 'A splinter of glacier ice that never melts.', price: 20, iconColor: 0xb3e5fc },
  { id: 'salamander_scale', type: 'material', name: 'Salamander Scale', description: 'Still warm, and faintly glowing.', price: 20, iconColor: 0xff8a50 },
  ...ELEMENTS.map(
    (e): MaterialDef => ({
      id: essenceId(e),
      type: 'material',
      name: `${ELEMENT_LABELS[e]} Essence`,
      description: `Distilled ${ELEMENT_LABELS[e].toLowerCase()}. The smith binds it into elemental gear.`,
      price: 10,
      iconColor: ESSENCE_ICON[e],
    }),
  ),
];

const r = (result: string, gold: number, ...materials: [string, number][]): RecipeDef => ({
  id: result,
  result,
  gold,
  materials: materials.map(([itemId, qty]) => ({ itemId, qty })),
});

/** Hulda's recipes. Boss weapons stay drop-only. */
export const RECIPES: RecipeDef[] = [
  // Chapter 1
  r('tusk_dagger', 25, ['boar_tusk', 3], ['scrap', 1]),
  r('apprentice_robe', 30, ['moss_clump', 4], ['scrap', 1]),
  r('cinder_fang', 50, ['essence_fire', 4], ['scrap', 2]),
  r('emberbrand', 60, ['essence_fire', 5], ['scrap', 3]),
  r('ember_wand', 50, ['essence_fire', 4], ['scrap', 1]),
  // Chapter 2
  r('marshhide_vest', 110, ['fen_hide', 4], ['scrap', 2]),
  r('tidebreaker', 140, ['essence_water', 5], ['fen_hide', 1], ['scrap', 4]),
  r('tidecaller_rod', 130, ['essence_water', 4], ['fen_hide', 1], ['scrap', 2]),
  r('brinefang', 120, ['essence_water', 4], ['fen_hide', 2], ['scrap', 3]),
  r('stormweave_robe', 110, ['essence_energy', 3], ['storm_scale', 1], ['scrap', 1]),
  r('stormcleaver', 150, ['essence_energy', 5], ['storm_scale', 2], ['scrap', 4]),
  r('sparkglass_wand', 130, ['essence_energy', 5], ['scrap', 2]),
  r('sparkfang', 130, ['essence_energy', 4], ['storm_scale', 2], ['scrap', 3]),
  // Chapter 3: Ice from the mountain, Fire from the Steamvent Caves.
  r('frostbite', 240, ['essence_ice', 5], ['rime_shard', 2], ['scrap', 4]),
  r('hoarfrost_staff', 230, ['essence_ice', 6], ['rime_shard', 1], ['scrap', 2]),
  r('icicle_kris', 230, ['essence_ice', 5], ['frost_pelt', 2], ['scrap', 3]),
  r('hearthfire_blade', 240, ['essence_fire', 5], ['salamander_scale', 2], ['scrap', 4]),
  r('kindling_rod', 230, ['essence_fire', 6], ['salamander_scale', 1], ['scrap', 2]),
  r('salamander_kris', 230, ['essence_fire', 5], ['salamander_scale', 2], ['scrap', 3]),
  r('frostpelt_vest', 200, ['frost_pelt', 4], ['ram_horn', 1], ['scrap', 2]),
  r('galeweave_robe', 200, ['essence_wind', 4], ['frost_pelt', 2], ['scrap', 1]),
];
