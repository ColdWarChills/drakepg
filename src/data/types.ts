export type StatKey = 'str' | 'int' | 'dex' | 'end';
export type Stats = Record<StatKey, number>;

export type DamageType = 'physical' | 'magic';

/** Placeholder look, drawn from shapes until real art exists. */
export interface Look {
  shape: 'humanoid' | 'blob' | 'beast' | 'imp';
  color: number;
  accent: number;
  /** 1 = player-sized. */
  scale: number;
  /** Image key in src/assets/art/ (file name without extension). Falls back to shapes if missing. */
  art?: string;
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
}

export interface ZoneDef {
  id: string;
  name: string;
  description: string;
  recommendedLevel: [number, number];
  /** Background tint for the battle scene. */
  skyColor: number;
  groundColor: number;
  /** Battle background image key. */
  background?: string;
  encounters: { monsterId: string; weight: number }[];
}

export type NpcRole =
  | { kind: 'talk' }
  | { kind: 'shop'; stock: string[] }
  | { kind: 'inn' }
  | { kind: 'gate'; zoneIds: string[] };

export interface NpcDef {
  id: string;
  name: string;
  title: string;
  /** X position on the town street (0-1280). */
  x: number;
  look: Look;
  role: NpcRole;
  /** Shown when the dialog opens. Several lines are paged with "Next". */
  dialogue: string[];
}

export interface TownDef {
  id: string;
  name: string;
  /** Town background image key. */
  background?: string;
  npcs: NpcDef[];
}
