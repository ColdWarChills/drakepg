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

  // Exploration
  /** Player walk speed in px/s (horizontal; vertical is 70% of this). */
  walkSpeed: 300,
  monsterWanderSpeed: 50,
  monsterChaseSpeed: 150,
  /** Distance at which a roaming monster notices and chases you. */
  monsterAggroRange: 260,
  /** Seconds of safety after fleeing, so the same monster can't instantly re-engage. */
  fleeGraceSeconds: 2,

  /** Gear lines (src/data/gear.ts) and rolled item quality (src/game/Gear.ts). */
  gear: {
    /** A gear line gets a new version every this many levels, starting at its `from` level. */
    versionStep: 5,
    /** Highest version level generated. */
    maxLevel: 20,
    /** Weapon average damage at 0% quality = (base + perLevel * level) * family power. */
    weaponBase: 4,
    weaponPerLevel: 2,
    /** Min/max damage spread around the average. */
    damageSpread: 0.25,
    /** Armor defense per damage type = (base + perLevel * level) * family power * archetype share. */
    armorBase: 5,
    armorPerLevel: 2,
    /** Price = k * average damage^2 (weapons) or k * total defense^2 (armor), before rarity/quality. */
    weaponPriceK: 1.3,
    armorPriceK: 0.17,
    /** A 100% quality line adds this much on top of the 0% value. */
    qualityDamage: 0.3,
    qualityDefense: 0.3,
    qualityHit: 0.08,
    qualityCrit: 0.06,
    qualityEvasion: 0.05,
    /** Rolled quality = floor + (100 - floor) * random^skew. Higher skew = low rolls more common. */
    qualitySkew: 1.6,
    /** Crafted gear never rolls below this quality. */
    craftFloor: 25,
    /** Quest reward gear quality. */
    questQuality: 50,
    /** Each +1 reinforcement adds this share to damage/defense. */
    plusPower: 0.05,
    maxPlus: 10,
    /** Success chance for going to +1, +2, ... +10. */
    reinforceChance: [1, 1, 1, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3],
    /** Failing a reinforce at or above this plus drops the item one plus. Below it you only lose materials. */
    reinforceRiskFrom: 4,
    /** Honing a line adds this much quality (whole percent). */
    honeGain: [6, 20] as [number, number],
    /** Chance another line drifts down when honing, and by how much. */
    honeDriftChance: 0.3,
    honeDrift: [3, 12] as [number, number],
    /** Selling pays this share of an item's value. */
    sellShare: 0.25,
  },
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
