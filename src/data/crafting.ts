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
];
