import { ELEMENTS, ELEMENT_LABELS } from './elements';
import type { Element, MaterialDef, RecipeDef } from './types';

export const essenceId = (e: Element) => `essence_${e}`;

/** Monster parts, elemental essences, and what salvaging gear gives back. */
export const MATERIALS: MaterialDef[] = [
  { id: 'scrap', type: 'material', name: 'Iron Scrap', description: 'Salvaged metal. The smith uses it to reinforce gear.', price: 4 },
  { id: 'dust', type: 'material', name: 'Honing Dust', description: 'Ground from salvaged gear. Used to hone quality.', price: 8 },
  { id: 'moss_clump', type: 'material', name: 'Moss Clump', description: 'Springy, damp and oddly tough.', price: 3 },
  { id: 'boar_tusk', type: 'material', name: 'Boar Tusk', description: 'Hard as bone and twice as sharp.', price: 6 },
  ...ELEMENTS.map(
    (e): MaterialDef => ({
      id: essenceId(e),
      type: 'material',
      name: `${ELEMENT_LABELS[e]} Essence`,
      description: `Condensed ${ELEMENT_LABELS[e].toLowerCase()}. Dropped by ${ELEMENT_LABELS[e].toLowerCase()} creatures or salvaged from ${ELEMENT_LABELS[e].toLowerCase()} gear.`,
      price: 10,
    }),
  ),
];

/** What the smith can make. Crafted gear rolls at least BALANCE.gear.craftFloor quality and better rarity odds. */
export const RECIPES: RecipeDef[] = [
  { id: 'tusk_dagger', result: 'tusk_dagger', materials: [{ itemId: 'boar_tusk', qty: 3 }, { itemId: 'scrap', qty: 1 }], gold: 25 },
  { id: 'cinder_fang', result: 'cinder_fang', materials: [{ itemId: 'essence_fire', qty: 4 }, { itemId: 'scrap', qty: 2 }], gold: 50 },
  { id: 'emberbrand', result: 'emberbrand', materials: [{ itemId: 'essence_fire', qty: 5 }, { itemId: 'scrap', qty: 3 }], gold: 60 },
  { id: 'ember_wand', result: 'ember_wand', materials: [{ itemId: 'essence_fire', qty: 4 }, { itemId: 'scrap', qty: 1 }], gold: 50 },
  {
    id: 'thornhide_vest',
    result: 'thornhide_vest',
    materials: [{ itemId: 'boar_tusk', qty: 2 }, { itemId: 'moss_clump', qty: 4 }, { itemId: 'essence_earth', qty: 2 }],
    gold: 40,
  },
  { id: 'mossweave_robe', result: 'mossweave_robe', materials: [{ itemId: 'moss_clump', qty: 5 }, { itemId: 'essence_earth', qty: 2 }], gold: 40 },
];
