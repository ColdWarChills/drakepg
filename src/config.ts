export const GAME_WIDTH = 1280;
export const GAME_HEIGHT = 720;

export const FONT = 'Georgia, "Times New Roman", serif';

export const SAVE_KEY = 'drakepg.save';
export const SAVE_VERSION = 14;

/** Design knobs. Tune here instead of hunting through code. */
export const BALANCE = {
  /** Fully restore HP/MP after every battle (true) or carry damage between fights (false). */
  restoreAfterBattle: false,
  /** Gold cost to rest at the inn. */
  innCost: 5,
  /** Recall Scrolls a new hero starts with (src/game/Travel.ts); after that, town shops sell them. */
  startingRecallScrolls: 5,
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
  /** Skills a hero carries into battle (picked in the Character panel; new skills fill free slots). */
  skillSlots: 5,
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
   * proc multiplies the weapon special's chance, power scales everything an accessory gives.
   */
  gearRolls: { damage: 0.5, crit: 0.05, proc: 0.5, defense: 0.5, hp: 0.15, power: 0.5 },
  /** How much of an armor piece's Max HP line counts, by slot: a full set of perfect rolls gives +21% Max HP. */
  armorHpShare: { body: 0.7, helm: 0.3, gloves: 0.2, boots: 0.2 },
  /**
   * Gear sets (src/data/sets.ts): an Elite or boss killed in an area with a set (its loot table's `set`) may also
   * drop one of the set's pieces. Shown on the battle panel's loot line.
   */
  setDrop: { elite: 0.25, boss: 0.4 },
  /** Dropped line quality = random ^ rollCurve. 1.5: median ~35%, ~7% of lines roll 90%+. */
  rollCurve: 1.5,
  /** Bag slots. Each gear piece or potion stack takes one; equipped gear and materials don't count. */
  bagSlots: 30,
  /** Selling gives this share of the shop price... */
  sellRate: 0.3,
  /** ...times (1 + grade * this): a 100% item sells for 3x a 0% one. */
  sellGradeBonus: 2,
  /**
   * Sigils: rare variants of any dropped weapon or armor. `boost` raises the base damage/defense
   * (before the rolls), `lines` is how many extra modifier lines it adds above the tier's cap, `sell` multiplies the sell price.
   */
  sigils: {
    dawn: { boost: 0.2, lines: 1, sell: 3 },
    dusk: { boost: 0.35, lines: 2, sell: 6 },
    eclipse: { boost: 0.6, lines: 3, sell: 12 },
  },
  /**
   * Gear drops (src/game/Loot.ts): one cascading roll per kill, rarest first. Roll `rare`; only if it misses roll
   * `uncommon`, then `common`. The first hit picks an item from that tier of the monster's loot table.
   * A `common` of 1 means something always drops (Elites, bosses).
   */
  loot: {
    mob: { rare: 0.01, uncommon: 0.05, common: 0.06 },
    elite: { rare: 0.05, uncommon: 0.4, common: 1 },
    boss: { rare: 0.35, uncommon: 0.5, common: 1 },
    /** Treasure chests and mimics: gear about two times in three. */
    chest: { rare: 0.03, uncommon: 0.3, common: 0.5 },
    /** Normal-monster odds for one loot table instead of `mob`. The prologue road drops gear about one kill in three. */
    tableMob: { road: { rare: 0, uncommon: 0, common: 0.35 } } as Record<string, { rare: number; uncommon: number; common: number }>,
    /**
     * Bad-luck protection: after `pityAfter` kills on a table without a Rare, each further kill adds `pityStep`
     * to that table's Rare chance, up to `pityMax` extra. A Rare resets it. Counted per table (each area has its
     * own streak). It only kicks in on long dry streaks, so average luck barely moves.
     */
    pityAfter: 80,
    pityStep: 0.001,
    pityMax: 0.2,
  },
  /** Chance that a gear drop is a Sigil, per tier. */
  sigilChance: {
    mob: { dawn: 0.02, dusk: 0.005, eclipse: 0.0005 },
    boss: { dawn: 0.2, dusk: 0.07, eclipse: 0.015 },
    elite: { dawn: 0.08, dusk: 0.02, eclipse: 0.003 },
    chest: { dawn: 0.05, dusk: 0.01, eclipse: 0.001 },
  },
  /**
   * Modifier lines a piece of gear can carry, by tier (index = tier; higher tiers use the last entry).
   * Rolled lines fill first (Damage, then Crit, then Special / Defense, then Max HP); slots left over can hold mods.
   */
  // Kept simple for now: at most 2 everywhere. The plan for later: T3-T5 4, T6+ 6.
  modifierSlots: [0, 1, 2, 2, 2, 2, 2],
  /** Mod value at 0% and at 100% quality. Stats/resist/HP/MP are points, crit is percent. */
  modRanges: {
    str: [1, 4],
    int: [1, 4],
    dex: [1, 4],
    end: [1, 4],
    crit: [1, 4],
    hp: [5, 25],
    mp: [5, 20],
    resist: [5, 20],
  } as Record<'str' | 'int' | 'dex' | 'end' | 'crit' | 'hp' | 'mp' | 'resist', [number, number]>,
  /**
   * The smith (src/game/Forge.ts). Crafted gear rolls every line at least `craftFloor`%.
   * Reinforcing: each + adds `plusPower` to base damage/defense, up to `maxPlus`; `reinforceChance[plus]` is the
   * odds of the next one; failing from `reinforceRiskFrom` up drops a +. Honing raises one line by `honeGain`;
   * with `honeDriftChance` another line slips by `honeDrift`.
   */
  forge: {
    craftFloor: 25,
    plusPower: 0.05,
    maxPlus: 10,
    reinforceChance: [1, 1, 1, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3],
    reinforceRiskFrom: 4,
    honeGain: [8, 15] as [number, number],
    honeDriftChance: 0.35,
    honeDrift: [3, 8] as [number, number],
  },
  /**
   * Damage multiplier for every non-boss monster (Elites and Mimics too). Monsters show their next move,
   * so normal fights can hit hard enough that Defend and healing matter.
   */
  mobDamage: 2,
  /** The same for bosses: high enough that reading their wind-ups and Defending is what wins. */
  bossDamage: 1.3,
  /**
   * Long fights: from player turn `turns` (`bossTurns` vs bosses) on, the enemy enrages and deals `step` more
   * damage every turn. The battle panel counts down the last `warn` turns.
   */
  enrage: { turns: 15, bossTurns: 30, step: 0.2, warn: 3 },
  /** Boss grit: each loss to a boss adds `step` damage against it, up to `max`. Beating it resets the count. */
  grit: { step: 0.05, max: 0.25 },
  /** XP and gold from beating a boss again after its quest no longer needs it. */
  bossRepeatRewards: 0.5,
  /**
   * Heroic rematch (src/game/Heroic.ts), open after a boss's quest: `levels` added, damage times `damage`, plus
   * its heroic moves. XP and gold times `rewards` (instead of the repeat share), and `lootRolls` gear rolls.
   */
  heroic: { levels: 3, damage: 1.25, rewards: 1, lootRolls: 2 },
  /**
   * Elites: tougher roaming monsters with one affix (src/data/elites.ts). `chance` per roaming spawn
   * (`questChance` while a quest wants an Elite), at most `maxPerRoom`. `levels` is added to the shown level;
   * hp/damage/rewards multiply the base monster; `size` scales the figure. Every Elite drops one gear
   * piece from its loot table (`loot.elite` odds). Their Sigil odds are `sigilChance.elite`.
   */
  elite: {
    chance: 0.08,
    questChance: 0.25,
    maxPerRoom: 1,
    levels: 1,
    hp: 1.5,
    damage: 1.1,
    rewards: 3,
    size: 1.2,
  },
  /**
   * Lucky Implings (src/game/Lucky.ts): a rare roaming spawn in any monster room from the start, at most one per room
   * (`chance` per spawn). It runs from you (`runSpeed`, px/s) and escapes on its `escapeAfter`-th turn in battle.
   * Sized to the area: `hp` x the area monsters' average HP, their best DEX + `dex` (hard to hit), `xp`/`gold` x their
   * average. A win has a `sigilChance` shot at a Sigil piece of the area's gear, tier picked by `sigilTiers`.
   */
  lucky: {
    chance: 0.015,
    runSpeed: 230,
    escapeAfter: 3,
    hp: 0.6,
    dex: 12,
    damage: 0.5,
    xp: 1.5,
    gold: 5,
    sigilChance: 0.1,
    sigilTiers: { dawn: 0.8, dusk: 0.17, eclipse: 0.03 },
  },

  /**
   * Result screen drop reveal (src/scenes/ResultScene.ts): ms of wind-up before each item pops in, by how good it is.
   * Plain drops just pop; better grades and Sigils build up first. Best items are revealed last.
   */
  dropReveal: { start: 900, gap: 160, plain: 0, fine: 0, superior: 200, masterwork: 450, dawn: 700, dusk: 1000, eclipse: 1500 },

  // Exploration
  /** A cleared room refills one monster per this many seconds you spend away from it. */
  respawnSeconds: 45,
  /** Chance a freshly filled monster room has a treasure chest, and that a chest is a mimic. */
  chest: { chance: 0.3, mimicChance: 0.25 },
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
