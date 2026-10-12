import type { RoomDef, ZoneDef } from './types';

export const ZONES: ZoneDef[] = [
  {
    id: 'thornwood',
    name: 'Thornwood',
    recommendedLevel: [1, 3],
    background: 'bg_thornwood',
    backdrop: { kind: 'forest' },
    encounters: [
      { monsterId: 'mossling', weight: 4 },
      { monsterId: 'thornback_boar', weight: 3 },
      { monsterId: 'ash_imp', weight: 3 },
    ],
  },
  {
    id: 'thornwood_deep',
    name: 'Deep Thornwood',
    recommendedLevel: [2, 5],
    background: ['bg_thornwood_deep', 'bg_thornwood'],
    backdrop: { kind: 'forest', shade: 0.35 },
    encounters: [
      { monsterId: 'thornback_boar', weight: 3 },
      { monsterId: 'ash_imp', weight: 3 },
      { monsterId: 'bramble_bandit', weight: 4 },
    ],
  },
  {
    id: 'thornheart_hollow',
    name: 'Thornheart Hollow',
    recommendedLevel: [4, 6],
    background: ['bg_thornheart_hollow', 'bg_thornwood_deep', 'bg_thornwood'],
    backdrop: { kind: 'forest', shade: 0.5 },
    encounters: [],
  },
];

export const ROOMS: RoomDef[] = [
  {
    id: 'brindlemoor',
    name: 'Brindlemoor',
    background: 'bg_brindlemoor',
    backdrop: { kind: 'town' },
    walk: [590, 700],
    ambient: ['dust'],
    exits: [{ side: 'right', to: 'thornwood_path', label: 'Thornwood' }],
    npcs: [
      {
        id: 'elder_maren',
        name: 'Elder Maren',
        title: 'Town Elder',
        x: 150,
        y: 615,
        look: { shape: 'humanoid', color: 0x7f8c8d, accent: 0xecf0f1, scale: 1, art: 'npc_elder_maren' },
        role: { kind: 'talk' },
        dialogue: [
          'Ah, a new face. Welcome to Brindlemoor.',
          'Since the old dragon went quiet, the Thornwood has grown restless. Imps, boars, bandits...',
          'Grow strong out there. Visit Tobin for gear and Ilsa when you need rest. The forest is east, through the gate.',
        ],
      },
      {
        id: 'tobin',
        name: 'Tobin',
        title: 'Shopkeeper',
        x: 500,
        y: 605,
        look: { shape: 'humanoid', color: 0xb9770e, accent: 0x5d3a0a, scale: 1, art: 'npc_tobin' },
        role: {
          kind: 'shop',
          stock: ['health_potion', 'mana_potion', 'iron_sword', 'oak_staff', 'leather_armor', 'apprentice_robe'],
        },
        dialogue: ['Best prices this side of the river. Have a look!'],
      },
      {
        id: 'ilsa',
        name: 'Ilsa',
        title: 'Innkeeper',
        x: 850,
        y: 610,
        look: { shape: 'humanoid', color: 0xa93226, accent: 0xf5cba7, scale: 1, art: 'npc_ilsa' },
        role: { kind: 'inn' },
        dialogue: ['A warm bed and a hot meal. You look like you need both.'],
      },
      {
        id: 'gate_guard',
        name: 'Garrick',
        title: 'Gate Guard',
        x: 1060,
        y: 600,
        look: { shape: 'humanoid', color: 0x34495e, accent: 0xbdc3c7, scale: 1.05, art: 'npc_gate_guard' },
        role: { kind: 'talk' },
        dialogue: [
          'The Thornwood starts right past this gate.',
          "Monsters out there can see you coming. If one charges, fight or run. Can't outrun a boar for long, mind.",
        ],
      },
    ],
  },
  {
    id: 'thornwood_path',
    name: 'Thornwood Path',
    background: 'bg_thornwood',
    backdrop: { kind: 'forest' },
    walk: [530, 700],
    ambient: ['leaves'],
    exits: [
      { side: 'left', to: 'brindlemoor', label: 'Brindlemoor' },
      { side: 'right', to: 'thornwood_deep', label: 'Deep Thornwood' },
    ],
    npcs: [],
    zoneId: 'thornwood',
    monsterCount: 3,
  },
  {
    id: 'thornwood_deep',
    name: 'Deep Thornwood',
    background: ['bg_thornwood_deep', 'bg_thornwood'],
    backdrop: { kind: 'forest', shade: 0.35 },
    walk: [530, 700],
    ambient: ['leaves', 'fireflies'],
    exits: [
      { side: 'left', to: 'thornwood_path', label: 'Thornwood Path' },
      { side: 'right', to: 'thornheart_hollow', label: 'Thornheart Hollow', requiresQuest: 'heart_of_thorns' },
    ],
    npcs: [],
    zoneId: 'thornwood_deep',
    monsterCount: 4,
  },
  {
    id: 'thornheart_hollow',
    name: 'Thornheart Hollow',
    background: ['bg_thornheart_hollow', 'bg_thornwood_deep', 'bg_thornwood'],
    backdrop: { kind: 'forest', shade: 0.5 },
    walk: [530, 700],
    ambient: ['fireflies', 'leaves'],
    exits: [{ side: 'left', to: 'thornwood_deep', label: 'Deep Thornwood' }],
    npcs: [],
    zoneId: 'thornheart_hollow',
    boss: { monsterId: 'thornheart', x: 930, y: 630, questId: 'heart_of_thorns' },
  },
];

/** Where new characters start, and where you wake up after losing a battle. */
export const START = { roomId: 'brindlemoor', x: 620, y: 650 };
export const INN_SPAWN = { roomId: 'brindlemoor', x: 760, y: 650 };
