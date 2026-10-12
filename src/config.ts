export const GAME_WIDTH = 1280;
export const GAME_HEIGHT = 720;

export const FONT = 'Georgia, "Times New Roman", serif';

export const SAVE_KEY = 'drakepg.save';
export const SAVE_VERSION = 2;

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
