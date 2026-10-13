export type StatKey = 'str' | 'int' | 'dex' | 'end';
export type Stats = Record<StatKey, number>;

/** Melee scales with STR, pierce with DEX, magic with INT. Each has its own defense. */
export type DamageType = 'melee' | 'pierce' | 'magic';

export type Element = 'fire' | 'water' | 'ice' | 'wind' | 'earth' | 'energy' | 'light' | 'dark';

/**
 * Elemental resistance in percent: 50 = takes half damage, -50 = takes 50% more, 100 = immune.
 * `all` adds to every element.
 */
export type Resists = Partial<Record<Element | 'all', number>>;

/** Defense against each damage type (see `mitigate` in Battle.ts). */
export type Defenses = Partial<Record<DamageType, number>>;

/** A status a move puts on its target (or on the user with `self`). */
export interface Apply {
  status: string;
  /** How many of the target's turns it lasts. */
  turns: number;
  /** 0-1, default 1. */
  chance?: number;
  self?: boolean;
}

/** Buffs, debuffs, damage over time, stuns. Lives in statuses.ts. */
export interface StatusDef {
  id: string;
  name: string;
  /** Shown red on the target when true, green when false. */
  harmful: boolean;
  description: string;
  /** Damage each turn: share of the applier's normal hit, in this element ('weapon' = applier's weapon element). */
  dot?: { power: number; element?: Element | 'weapon' };
  /** Heal each turn: share of max HP. */
  regen?: number;
  /** Skip the next turn. */
  stun?: boolean;
  /** +0.3 = deal 30% more damage. */
  damageDealt?: number;
  /** -0.4 = take 40% less damage. */
  damageTaken?: number;
  /** Added to the holder's resistances. */
  resists?: Resists;
  /** Added to the holder's hit chance, e.g. -0.3. */
  hitChance?: number;
  /** Added to the holder's crit chance. */
  critChance?: number;
  /** Subtracted from the hit chance of anyone attacking the holder. */
  evasion?: number;
}

/** How a character is drawn: art if it exists, otherwise placeholder shapes. */
export interface Look {
  shape: 'humanoid' | 'blob' | 'beast' | 'imp';
  color: number;
  accent: number;
  /** 1 = player-sized. */
  scale: number;
  /** Image key(s) in src/assets/art/ (file name without extension); first existing wins. Falls back to shapes. */
  art?: ArtRef;
  /**
   * Empty-handed art used when the equipped weapon has art of its own; the weapon is drawn in `hands`.
   * Several: the first whose art exists wins (the hero in their armor's outfit, then the default outfit).
   */
  bare?: BareLook | BareLook[];
}

export interface BareLook {
  art: string;
  hands: Hand[];
}

/** A fist on the bare art that holds the weapon. Weapons are drawn behind the body, so the fist covers the grip. */
export interface Hand {
  /** Fist center in the bare art's own pixels (as written by prepare_art.py). */
  x: number;
  y: number;
  /** Weapon angle in degrees: 0 = pointing up, 90 = pointing forward. */
  angle: number;
}

export interface SkillDef {
  id: string;
  name: string;
  description: string;
  mpCost: number;
  /** After use, the skill is locked for this many of your turns. 0 = usable every turn. */
  cooldown: number;
  /** 'buff' only applies statuses. */
  kind: 'damage' | 'heal' | 'buff';
  /** Damage skills: default 'weapon' (the equipped weapon's type). */
  damageType?: DamageType | 'weapon';
  /** Damage skills: default 'weapon' (the equipped weapon's element, if any). */
  element?: Element | 'weapon';
  /** Damage: multiplier on a normal hit. Heal: share of max HP restored. Buff: unused (0). */
  power: number;
  /** Damage: number of separate hits (default 1). */
  hits?: number;
  /** Added to crit chance for this skill's hits. */
  critBonus?: number;
  /** Heal this share of the damage dealt. */
  lifesteal?: number;
  applies?: Apply[];
  /** Battle visual. Defaults: melee/pierce -> 'slash', magic -> 'bolt', heal -> 'heal', buff -> 'buff'. */
  fx?: SkillFx;
}

export type SkillFx = 'slash' | 'heavy' | 'bolt' | 'heal' | 'buff';

export type ClassId = 'warrior' | 'mage' | 'rogue';

export interface ClassDef {
  id: ClassId;
  name: string;
  tagline: string;
  /** Starting base stats. */
  stats: Stats;
  /** Added automatically on every level up, on top of the free stat points. */
  growth: Partial<Stats>;
  weapon: string;
  armor: string;
  /** Skills and the level each unlocks at. */
  skills: { skillId: string; level: number }[];
  /** Rage: MP gained each time an enemy hit lands on you. Shown on the class card and Character panel. */
  mpOnHit?: number;
  look: Look;
}

interface ItemBase {
  id: string;
  name: string;
  description: string;
  /** Shop price in gold. */
  price: number;
  /** Color of the drawn inventory icon when the item has no art. */
  iconColor?: number;
}

/** Weapon special: may trigger after a normal Attack lands. */
export interface ProcDef {
  name: string;
  /** 0-1 per landed Attack. */
  chance: number;
  /** Extra hit at this multiplier (0 = no extra hit). */
  power: number;
  hits?: number;
  /** Default: the weapon's element. */
  element?: Element;
  applies?: Apply[];
  /** Heal this share of max HP. */
  heal?: number;
}

export interface WeaponDef extends ItemBase {
  type: 'weapon';
  damage: [number, number];
  /** Melee scales with STR, pierce with DEX, magic with INT. */
  damageType: DamageType;
  /** No element = neutral (ignores resistances). */
  element?: Element;
  stats?: Partial<Stats>;
  proc?: ProcDef;
  /** Upright weapon image (handle at the bottom) drawn in the hero's hand. */
  art?: string;
  /** Where the hand holds it: 0 = top of the image, 1 = bottom. Default 0.85. */
  grip?: number;
  /** Length on screen relative to the hero's height. Default 0.6. */
  size?: number;
}

export interface ArmorDef extends ItemBase {
  type: 'armor';
  defense: Defenses;
  resists?: Resists;
  stats?: Partial<Stats>;
  /**
   * Wearing it, a hero is drawn from `char_<class>_<armor id>` (empty-handed, like `char_<class>_bare`) when that art
   * exists. Its fist anchors default to the class's bare art; set them here per class once the art is measured.
   */
  hands?: Partial<Record<ClassId, Hand[]>>;
}

export interface ConsumableDef extends ItemBase {
  type: 'consumable';
  restore: { hp?: number; mp?: number };
}

export type ItemDef = WeaponDef | ArmorDef | ConsumableDef;
export type EquipSlot = 'weapon' | 'armor';

export interface MonsterAttack {
  name: string;
  damageType: DamageType;
  /** Default: the monster's element. */
  element?: Element;
  /** Multiplier on the monster's damage roll. */
  power: number;
  /** Relative chance to pick this attack. */
  weight: number;
  /** Telegraphed attack: the monster spends a turn winding up (this text is shown), then hits next turn. */
  windup?: string;
  applies?: Apply[];
}

export interface MonsterDef {
  id: string;
  name: string;
  level: number;
  look: Look;
  hp: number;
  stats: Stats;
  damage: [number, number];
  /** Element of its attacks unless an attack says otherwise. */
  element?: Element;
  defense: Defenses;
  resists?: Resists;
  /** Status ids that never stick (bosses ignore stuns). */
  immune?: string[];
  attacks: MonsterAttack[];
  xp: number;
  gold: [number, number];
  drops: { itemId: string; chance: number }[];
  /** Bosses can't be fled from and get a bigger presence on screen. */
  boss?: boolean;
  /** Only on Elite copies made by `makeElite` (src/game/Elites.ts), never in monsters.ts. */
  elite?: EliteInfo;
  /** Changes once, the first time its HP drops to `at` (share of max HP) or below. */
  phase?: MonsterPhase;
}

/** A boss's second form: new element and resistances (and optionally attacks), announced in battle. */
export interface MonsterPhase {
  /** 0.5 = at half HP. */
  at: number;
  /** Battle log line and banner when it happens. */
  text: string;
  element: Element;
  resists: Resists;
  attacks?: MonsterAttack[];
}

/** What makes a monster an Elite: its affix, and the gear pool its guaranteed drop comes from. */
export interface EliteInfo {
  affix: string;
  loot: string[];
}

/** One Elite affix. Lives in elites.ts; every effect shows in the battle UI through `description`. */
export interface EliteAffixDef {
  id: string;
  /** Prefixed to the monster's name: "Frenzied Ash Imp". */
  name: string;
  description: string;
  /** +0.5 = 50% more HP (on top of the Elite HP multiplier). */
  hp?: number;
  /** +0.25 = 25% more damage. */
  damage?: number;
  /** +0.5 = 50% more defense of every type. */
  defense?: number;
  resists?: Resists;
  /** Added to every damaging attack. */
  applies?: Apply[];
}

/** Painted background image key(s); the first one that exists is used. */
export type ArtRef = string | string[];

/** Procedural scenery used when there's no background art. */
export interface Backdrop {
  kind: 'town' | 'forest' | 'marsh' | 'outpost';
  /** Darken the whole scene, 0-1. */
  shade?: number;
}

/** Encounter table + battle scenery shared by the rooms of an area. */
export interface ZoneDef {
  id: string;
  name: string;
  recommendedLevel: [number, number];
  background?: ArtRef;
  backdrop: Backdrop;
  encounters: { monsterId: string; weight: number }[];
}

export type NpcRole = { kind: 'talk' } | { kind: 'shop'; stock: string[] } | { kind: 'inn' };

export interface NpcDef {
  id: string;
  name: string;
  title: string;
  /** Feet position in the room (1280x720 screen). */
  x: number;
  y: number;
  look: Look;
  role: NpcRole;
  /** Shown when the dialog opens. Several lines are paged with "Next". */
  dialogue: string[];
}

export interface RoomExit {
  side: 'left' | 'right';
  to: string;
  /** Shown on the arrow at the screen edge. */
  label: string;
  /** Exit stays blocked until this quest is accepted (or done). */
  requiresQuest?: string;
  /** What blocks it until then. Default thorns. */
  blocker?: Blocker;
}

export type Blocker = 'thorns' | 'barricade' | 'reeds';

export type Ambient = 'dust' | 'leaves' | 'fireflies' | 'rain' | 'mist';

/** One walkable screen. Walk off a side with an exit to enter the next room. */
export interface RoomDef {
  id: string;
  name: string;
  background?: ArtRef;
  backdrop: Backdrop;
  /** Feet can move between these y values (the ground strip). */
  walk: [number, number];
  exits: RoomExit[];
  npcs: NpcDef[];
  ambient?: Ambient[];
  /** Monsters roaming here come from this zone; battles use its scenery. */
  zoneId?: string;
  monsterCount?: number;
  /** A boss standing in the room once its quest is accepted. It stays after the quest for rematches (less XP and gold). */
  boss?: { monsterId: string; x: number; y: number; questId: string; intro?: string };
  /** Where you wake up after losing a battle here. Default: the Brindlemoor inn. */
  wakeAt?: { roomId: string; x: number; y: number };
}

export interface QuestDef {
  id: string;
  name: string;
  /** NPC id who offers and accepts the quest. */
  giver: string;
  /** Quest that must be completed first. */
  requires?: string;
  /** Kill `count` of `monsterId`, or of any Elite with `elite` (both: Elites of that monster). */
  objective: { kind: 'kill'; monsterId?: string; elite?: boolean; count: number };
  /** Short tracker text, e.g. "Mosslings defeated". */
  goal: string;
  offer: string[];
  /** Said when you talk to the giver before finishing. */
  reminder: string;
  complete: string[];
  rewards: { xp: number; gold: number; items?: string[]; classItems?: Partial<Record<ClassId, string>> };
  /** Last quest of a chapter: shows the chapter-complete banner. */
  endsChapter?: string;
}
