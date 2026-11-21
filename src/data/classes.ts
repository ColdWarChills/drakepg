import type { ClassDef, Hand, WeaponDef, WeaponKind } from './types';

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
 * The ready stance the standing hero is drawn in for each kind of weapon (Unity; docs/PLANS.md "Weapon holds: ready
 * stances"): a key pose in `HERO_POSES` whose arms are drawn for that grip. Without the pose's art (an outfit that has
 * none yet) the hero stands in the base picture with the weapon upright in `RAISED_FIST`.
 */
export const READY_POSE: Record<WeaponKind, string> = {
  sword: 'sword_ready',
  axe: 'sword_ready',
  hammer: 'hammer_ready',
  dagger: 'dagger_ready',
  staff: 'staff_ready',
  wand: 'wand_ready',
};

/**
 * A rod this long (of the hero's height) or longer is held and swung as a staff, a shorter one as a wand, whatever its
 * data calls it: a long, top-heavy rod aimed from its end looks wrong.
 */
export const LONG_ROD = 0.7;

/**
 * How a weapon is held and swung: its data, else from its damage type. A wand or staff goes by its length (`LONG_ROD`).
 */
export function weaponKind(w: WeaponDef): WeaponKind {
  const kind = w.kind ?? (w.damageType === 'pierce' ? 'dagger' : w.damageType === 'magic' ? 'staff' : 'sword');
  if (kind === 'wand' || kind === 'staff') return (w.size ?? 0.6) >= LONG_ROD ? 'staff' : 'wand';
  return kind;
}

/**
 * How far below the fist a planted staff must reach (of the hero's height) to stand on the ground in `staff_ready`:
 * `(1 - grip) * size`. Checked for every staff in `validateData`.
 */
export const PLANTED_REACH: [number, number] = [0.53, 0.57];

/**
 * Key poses for the top skills (`SkillDef.epic`; scripts/hero_poses.py, Qwen edits of the base): the weapon fist on each
 * (canvases include hero_poses.MARGIN_X on both sides, so outfit robes fit; x is measured with it)
 * (`char_hero_bare_<pose>`, 1x px). A circle of the fist covers the grip. charge: the power stance, the sword raised
 * beside his head; leap: both fists together overhead, the weapon swung back behind his head; land: the deep lunge after the blow, both fists low in front.
 */
const HERO_POSES: Record<string, Hand[]> = {
  charge: [{ x: 186.7, y: 48, angle: -30 }],
  leap: [{ x: 139.7, y: 28, angle: -120 }],
  land: [{ x: 417.7, y: 305, angle: 128 }],
  // A blow per weapon kind (Unity BattleScreen.WeaponBlow): <kind>_wind, _step, _strike. The first hand holds the weapon;
  // dagger poses also give the rogue's second dagger hand. The hammer steps in with the epic leap.
  // The wind-up's fist is drawn holding the handle upright, thumb on top (like RAISED_FIST): the sword stands up behind
  // the head (it was -75, across the fist), and its fingers wrap the handle (scripts/grip_hands.py --pose sword_wind,
  // rod try 2124597813).
  sword_wind: [{ x: 137.5, y: 74.3, angle: 0, fingers: { art: 'part_hero_fingers_sword_wind', x: 94, y: 28 } }],
  sword_step: [{ x: 128.7, y: 21, angle: -45 }],
  sword_strike: [{ x: 331.7, y: 388, angle: 115 }],
  axe_wind: [{ x: 145.7, y: 23, angle: -110 }],
  axe_step: [{ x: 158.7, y: 21, angle: -90 }],
  axe_strike: [{ x: 350.7, y: 292, angle: 115 }],
  hammer_wind: [{ x: 56.7, y: 267, angle: -150 }],
  hammer_strike: [{ x: 296.7, y: 383, angle: 112 }],
  dagger_wind: [{ x: 329.7, y: 240, angle: 70 }, { x: 89.7, y: 233, angle: -130 }],
  dagger_step: [{ x: 387.7, y: 238, angle: 60 }, { x: 135.7, y: 238, angle: -120 }],
  dagger_strike: [{ x: 473.7, y: 125, angle: 90 }, { x: 173.7, y: 238, angle: -140 }],
  staff_wind: [{ x: 104.7, y: 21, angle: 0 }],
  staff_step: [{ x: 215.7, y: 35, angle: 30 }],
  staff_strike: [{ x: 408.7, y: 123, angle: 80 }],
  wand_wind: [{ x: 125.7, y: 79, angle: -12 }],
  wand_step: [{ x: 246.7, y: 133, angle: 40 }],
  wand_strike: [{ x: 386.7, y: 100, angle: 75 }],
  // The ready stances (READY_POSE): how the standing hero holds each kind of weapon. A closed fist holds a handle square
  // to the forearm, so each angle follows the drawn forearm; only the wand continues the arm.
  sword_ready: [{ x: 244, y: 262, angle: 55 }],
  // A knife fighter's guard, both fists clear of the body: the lead dagger forward and up from a low fist, the near
  // one in a reverse grip beside the hip (the arm nearest the viewer, so its whole blade is drawn in front of the leg).
  dagger_ready: [{ x: 274, y: 214, angle: 30 }, { x: 127, y: 241, angle: -140, front: 150 }],
  wand_ready: [{ x: 213, y: 138, angle: 48 }],
  // Two hands on the maul: the rear fist at the belt holds it (the whole shaft in front of the body), the lead fist
  // only closes over the shaft.
  hammer_ready: [{ x: 148, y: 222, angle: 59, front: 270 }, { x: 272, y: 147, angle: 59, cover: true }],
  // The staff planted by the front foot, leaning a little back to him.
  staff_ready: [{ x: 278, y: 213, angle: -4 }],
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
      { skillId: 'sunder', level: 6 },
      { skillId: 'cleave', level: 9 },
    ],
    books: ['shield_bash', 'iron_skin', 'bloodlust', 'earthshaker', 'rallying_roar', 'titans_wrath'],
    family: 'strike',
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
      { skillId: 'ember_bolt', level: 2 },
      { skillId: 'mend', level: 2 },
      { skillId: 'frost_lance', level: 3 },
      { skillId: 'spark_chain', level: 5 },
      { skillId: 'stone_skin', level: 7 },
    ],
    books: ['meteor', 'sunburst', 'tidal_wave', 'gale', 'thunderstorm', 'shadow_hex', 'cataclysm'],
    family: 'bolt',
    heroicBook: 'shadow_hex',
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
      { skillId: 'assassinate', level: 5 },
      { skillId: 'smoke_bomb', level: 7 },
    ],
    books: ['expose_weakness', 'envenom', 'shadowstep', 'hemorrhage', 'fan_of_knives', 'death_mark', 'thousand_cuts'],
    family: 'knives',
    heroicBook: 'death_mark',
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
