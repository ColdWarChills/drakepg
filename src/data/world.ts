import type { TownDef, ZoneDef } from './types';

export const ZONES: ZoneDef[] = [
  {
    id: 'thornwood',
    name: 'Thornwood',
    description: 'A tangled forest just past the town gate.',
    recommendedLevel: [1, 4],
    skyColor: 0x2c3e2f,
    groundColor: 0x23301f,
    encounters: [
      { monsterId: 'mossling', weight: 4 },
      { monsterId: 'thornback_boar', weight: 3 },
      { monsterId: 'ash_imp', weight: 3 },
      { monsterId: 'bramble_bandit', weight: 2 },
    ],
  },
];

export const TOWNS: TownDef[] = [
  {
    id: 'brindlemoor',
    name: 'Brindlemoor',
    npcs: [
      {
        id: 'elder_maren',
        name: 'Elder Maren',
        title: 'Town Elder',
        x: 200,
        look: { shape: 'humanoid', color: 0x7f8c8d, accent: 0xecf0f1, scale: 1 },
        role: { kind: 'talk' },
        dialogue: [
          'Ah, a new face. Welcome to Brindlemoor.',
          'Since the old dragon went quiet, the Thornwood has grown restless. Imps, boars, bandits...',
          'Grow strong out there. Visit Tobin for gear and Ilsa when you need rest.',
        ],
      },
      {
        id: 'tobin',
        name: 'Tobin',
        title: 'Shopkeeper',
        x: 470,
        look: { shape: 'humanoid', color: 0xb9770e, accent: 0x5d3a0a, scale: 1 },
        role: {
          kind: 'shop',
          stock: [
            'health_potion',
            'mana_potion',
            'iron_sword',
            'oak_staff',
            'leather_armor',
            'apprentice_robe',
          ],
        },
        dialogue: ['Best prices this side of the river. Have a look!'],
      },
      {
        id: 'ilsa',
        name: 'Ilsa',
        title: 'Innkeeper',
        x: 740,
        look: { shape: 'humanoid', color: 0xa93226, accent: 0xf5cba7, scale: 1 },
        role: { kind: 'inn' },
        dialogue: ['A warm bed and a hot meal. You look like you need both.'],
      },
      {
        id: 'gate_guard',
        name: 'Garrick',
        title: 'Gate Guard',
        x: 1080,
        look: { shape: 'humanoid', color: 0x34495e, accent: 0xbdc3c7, scale: 1.05 },
        role: { kind: 'gate', zoneIds: ['thornwood'] },
        dialogue: ['Heading out? Mind the thorns, and run if it gets ugly.'],
      },
    ],
  },
];

export const START_TOWN_ID = 'brindlemoor';
