export type StatKey = 'str' | 'int' | 'dex' | 'end';
export type Stats = Record<StatKey, number>;

export type DamageType = 'physical' | 'magic';

/** How a character is drawn: art if it exists, otherwise placeholder shapes. */
export interface Look {
  shape: 'humanoid' | 'blob' | 'beast' | 'imp';
  color: number;
  accent: number;
  /** 1 = player-sized. */
  scale: number;
  /** Image key(s) in src/assets/art/ (file name without extension); first existing wins. Falls back to shapes. */
  art?: ArtRef;
}

export interface SkillDef {
  id: string;
  name: string;
  description: string;
  mpCost: number;
  kind: 'damage' | 'heal';
  /** Damage skills only. */
  damageType?: DamageType;
  /** Damage: multiplier on a normal hit. Heal: share of max HP restored. */
  power: number;
  /** Damage: number of separate hits (default 1). */
  hits?: number;
  /** Battle visual. Defaults: physical -> 'slash', magic -> 'fire', heal -> 'heal'. */
  fx?: SkillFx;
}

export type SkillFx = 'slash' | 'heavy' | 'fire' | 'frost' | 'heal';

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
  look: Look;
}

interface ItemBase {
  id: string;
  name: string;
  description: string;
  /** Shop price in gold. */
  price: number;
}

export interface WeaponDef extends ItemBase {
  type: 'weapon';
  damage: [number, number];
  /** Physical weapons scale with STR, magic weapons with INT. */
  damageType: DamageType;
  stats?: Partial<Stats>;
}

export interface ArmorDef extends ItemBase {
  type: 'armor';
  /** Reduces physical damage. */
  defense: number;
  /** Reduces magic damage. */
  resist: number;
  stats?: Partial<Stats>;
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
  /** Multiplier on the monster's damage roll. */
  power: number;
  /** Relative chance to pick this attack. */
  weight: number;
  /** Telegraphed attack: the monster spends a turn winding up (this text is shown), then hits next turn. */
  windup?: string;
}

export interface MonsterDef {
  id: string;
  name: string;
  level: number;
  look: Look;
  hp: number;
  stats: Stats;
  damage: [number, number];
  defense: number;
  resist: number;
  attacks: MonsterAttack[];
  xp: number;
  gold: [number, number];
  drops: { itemId: string; chance: number }[];
  /** Bosses can't be fled from and get a bigger presence on screen. */
  boss?: boolean;
}

/** Painted background image key(s); the first one that exists is used. */
export type ArtRef = string | string[];

/** Procedural scenery used when there's no background art. */
export interface Backdrop {
  kind: 'town' | 'forest';
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
}

export type Ambient = 'dust' | 'leaves' | 'fireflies';

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
  /** A boss standing in the room. Gone once its quest is complete. */
  boss?: { monsterId: string; x: number; y: number; questId: string };
}

export interface QuestDef {
  id: string;
  name: string;
  /** NPC id who offers and accepts the quest. */
  giver: string;
  /** Quest that must be completed first. */
  requires?: string;
  objective: { kind: 'kill'; monsterId: string; count: number };
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
