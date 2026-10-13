export const GAME_WIDTH = 1280;
export const GAME_HEIGHT = 720;

export const FONT = 'Georgia, "Times New Roman", serif';

export const SAVE_KEY = 'drakepg.save';
export const SAVE_VERSION = 4;

/** Design knobs. Tune here instead of hunting through code. */
export const BALANCE = {
  /** Fully restore HP/MP after every battle (true) or carry damage between fights (false). */
  restoreAfterBattle: false,
  /** Gold cost to rest at the inn. */
  innCost: 5,
  /** Base hit chance before DEX difference is applied. */
  baseHitChance: 0.9,
  minHitChance: 0.6,
  maxHitChance: 0.98,
  baseCritChance: 0.05,
  critPerDex: 0.005,
  critMultiplier: 1.5,
  /** Share of the main stat (STR or INT) added to each hit. */
  statDamageScale: 0.6,
  baseFleeChance: 0.5,
  maxLevel: 50,
  /** Free stat points per level up (class growth is added on top). */
  statPointsPerLevel: 3,
  /** Damage taken while defending is multiplied by this. */
  defendDamage: 0.4,
  /** MP regained when defending. */
  defendMp: 4,
  /** Highest elemental resistance a player can reach from gear and buffs (100 would be immune). */
  maxPlayerResist: 80,
  /** Monsters can go from -100 (double damage) to 100 (immune). */
  minResist: -100,

  // Gear
  /**
   * Rolled gear: each line of a weapon or armor rolls a quality from 0 to 100%. Shop gear is always 0%.
   * These are the bonuses at 100%: damage/defense/hp are a share of the base, crit is added to crit chance,
   * proc multiplies the weapon special's chance.
   */
  gearRolls: { damage: 0.15, crit: 0.05, proc: 0.5, defense: 0.15, hp: 0.08 },
  /** Dropped line quality = random ^ rollCurve. 1.5: median ~35%, ~7% of lines roll 90%+. */
  rollCurve: 1.5,
  /** Bag slots. Each gear piece or potion stack takes one; equipped gear doesn't count. */
  bagSlots: 30,
  /** Selling gives this share of the shop price... */
  sellRate: 0.3,
  /** ...times (1 + grade * this): a 100% item sells for 3x a 0% one. */
  sellGradeBonus: 2,

  // Exploration
  /** Player walk speed in px/s (horizontal; vertical is 70% of this). */
  walkSpeed: 300,
  monsterWanderSpeed: 50,
  monsterChaseSpeed: 150,
  /** Distance at which a roaming monster notices and chases you. */
  monsterAggroRange: 260,
  /** Seconds of safety after fleeing, so the same monster can't instantly re-engage. */
  fleeGraceSeconds: 2,
};

export const COLORS = {
  bg: 0x0d0b12,
  panel: 0x1d1826,
  panelBorder: 0x8a6d3b,
  button: 0x3a2e4a,
  buttonHover: 0x4d3d63,
  buttonDown: 0x2a2136,
  buttonDisabled: 0x2a2630,
  hp: 0xc0392b,
  mp: 0x2e86de,
  xp: 0xd4ac0d,
  barBg: 0x111111,
  gold: 0xf1c40f,
  text: '#f3e9d2',
  textDim: '#a89f8c',
  textGold: '#f1c40f',
  textGood: '#7ddc7d',
  textBad: '#ff7066',
  textMagic: '#8ec5ff',
};
