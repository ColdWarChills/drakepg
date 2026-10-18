import type { ClassDef, Hand } from './types';

/**
 * One base hero for every class (docs/PLANS.md, Art): classes differ by gear, not by the picture. Each class holds its
 * weapon in its own fists (`bare.hands`); body armor swaps in `char_hero_<armor id>` (see `Player.look`).
 */
const HERO_ART = 'char_hero_bare';
/**
 * The base hero's raised fist (front hand), in layers like a real grip: the hero's art is the palm behind the handle,
 * then the handle, then the fingers that wrap it (`part_hero_fingers`). Grip line and fingers come from an edit of the
 * art where the fist grips a rod (scripts/grip_hands.py layers 1912959231): the handle goes under the index finger at
 * the top, where the guard rests, and comes out under the curled fingertips at the bottom.
 */
const RAISED_FIST: Hand = { x: 235.1, y: 133.5, angle: -10, fingers: { art: 'part_hero_fingers', x: 190, y: 95 } };
/**
 * Key poses for the top skills (`SkillDef.epic`; scripts/hero_poses.py, Qwen edits of the base): the weapon fist on each
 * (`char_hero_bare_<pose>`, 1x px). A circle of the fist covers the grip. charge: the power stance, the sword raised
 * beside his head; leap: both fists together overhead, the weapon swung back behind his head; land: the deep lunge after the blow, both fists low in front.
 */
const HERO_POSES: Record<string, Hand[]> = {
  charge: [{ x: 157, y: 48, angle: -30 }],
  leap: [{ x: 110, y: 28, angle: -120 }],
  land: [{ x: 388, y: 305, angle: 128 }],
};

export const CLASSES: ClassDef[] = [
  {
    id: 'warrior',
    name: 'Warrior',
    tagline: 'Tough and steady. Big HP, heavy hits. Rage: gains MP when hit.',
    stats: { str: 8, int: 2, dex: 5, end: 7 },
    growth: { str: 2, end: 1 },
    weapon: 'training_sword',
    armor: 'travel_garb',
    skills: [
      { skillId: 'heavy_strike', level: 1 },
      { skillId: 'second_wind', level: 2 },
      { skillId: 'war_cry', level: 3 },
      { skillId: 'skull_splitter', level: 4 },
      { skillId: 'shield_bash', level: 6 },
      { skillId: 'sunder', level: 8 },
      { skillId: 'iron_skin', level: 10 },
      { skillId: 'cleave', level: 12 },
      { skillId: 'bloodlust', level: 14 },
      { skillId: 'earthshaker', level: 16 },
      { skillId: 'rallying_roar', level: 18 },
      { skillId: 'titans_wrath', level: 20 },
    ],
    mpOnHit: 3,
    look: {
      shape: 'humanoid',
      color: 0x2e86de,
      accent: 0x5b3a1e,
      scale: 1,
      art: HERO_ART,
      bare: { art: HERO_ART, hands: [RAISED_FIST], poses: HERO_POSES },
    },
  },
  {
    id: 'mage',
    name: 'Mage',
    tagline: 'Fragile, but burns and freezes everything.',
    stats: { str: 2, int: 9, dex: 5, end: 6 },
    growth: { int: 1, end: 1 },
    weapon: 'worn_staff',
    armor: 'novice_robe',
    skills: [
      { skillId: 'light_bolt', level: 1 },
      { skillId: 'mend', level: 2 },
      { skillId: 'ember_bolt', level: 2 },
      { skillId: 'frost_lance', level: 3 },
      { skillId: 'meteor', level: 5 },
      { skillId: 'spark_chain', level: 6 },
      { skillId: 'stone_skin', level: 8 },
      { skillId: 'gale', level: 10 },
      { skillId: 'tidal_wave', level: 12 },
      { skillId: 'sunburst', level: 14 },
      { skillId: 'shadow_hex', level: 16 },
      { skillId: 'thunderstorm', level: 18 },
      { skillId: 'cataclysm', level: 20 },
    ],
    look: {
      shape: 'humanoid',
      color: 0x7d3c98,
      accent: 0x2c1a3a,
      scale: 1,
      art: HERO_ART,
      bare: { art: HERO_ART, hands: [RAISED_FIST], poses: HERO_POSES },
    },
  },
  {
    id: 'rogue',
    name: 'Rogue',
    tagline: 'Quick and deadly. Strikes twice, crits often.',
    stats: { str: 6, int: 2, dex: 9, end: 5 },
    growth: { dex: 1, str: 1 },
    weapon: 'rusty_dagger',
    armor: 'travel_garb',
    skills: [
      { skillId: 'gas_shot', level: 1 },
      { skillId: 'twin_cut', level: 2 },
      { skillId: 'bandage', level: 2 },
      { skillId: 'flurry', level: 3 },
      { skillId: 'envenom', level: 4 },
      { skillId: 'assassinate', level: 5 },
      { skillId: 'smoke_bomb', level: 7 },
      { skillId: 'expose_weakness', level: 9 },
      { skillId: 'shadowstep', level: 11 },
      { skillId: 'fan_of_knives', level: 13 },
      { skillId: 'hemorrhage', level: 15 },
      { skillId: 'death_mark', level: 18 },
      { skillId: 'thousand_cuts', level: 20 },
    ],
    look: {
      shape: 'humanoid',
      color: 0x27ae60,
      accent: 0x1b2e1f,
      scale: 1,
      art: HERO_ART,
      bare: {
        art: HERO_ART,
        poses: HERO_POSES,
        hands: [
          RAISED_FIST,
          // Reverse grip: the blade out of the back of the lowered fist, down and behind.
          { x: 50, y: 268, angle: -130 },
        ],
      },
    },
  },
];
