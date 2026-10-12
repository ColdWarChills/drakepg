import { BALANCE } from '../config';
import { db } from '../data/db';
import { STARTING_SKILLS } from '../data/skills';
import type {
  ArmorDef,
  ConsumableDef,
  DamageType,
  EquipSlot,
  SkillDef,
  StatKey,
  Stats,
  WeaponDef,
} from '../data/types';

export const STAT_KEYS: StatKey[] = ['str', 'int', 'dex', 'end'];

export const STAT_LABELS: Record<StatKey, string> = {
  str: 'STR',
  int: 'INT',
  dex: 'DEX',
  end: 'END',
};

export interface InventoryEntry {
  itemId: string;
  qty: number;
}

/** Everything that gets saved. Keep it plain JSON. */
export interface PlayerData {
  name: string;
  level: number;
  xp: number;
  gold: number;
  baseStats: Stats;
  hp: number;
  mp: number;
  equipment: Record<EquipSlot, string | null>;
  inventory: InventoryEntry[];
  skills: string[];
}

export interface Rewards {
  xp: number;
  gold: number;
  items: string[];
}

/** XP needed to go from `level` to `level + 1`. */
export function xpToNext(level: number): number {
  return Math.floor(50 * Math.pow(level, 1.5));
}

const UNARMED: [number, number] = [2, 4];

export class Player {
  constructor(public data: PlayerData) {}

  static createNew(name = 'Hero'): Player {
    const player = new Player({
      name,
      level: 1,
      xp: 0,
      gold: 30,
      baseStats: { str: 5, int: 5, dex: 5, end: 5 },
      hp: 1,
      mp: 1,
      equipment: { weapon: 'training_sword', armor: 'travel_garb' },
      inventory: [
        { itemId: 'health_potion', qty: 2 },
        { itemId: 'mana_potion', qty: 1 },
      ],
      skills: [...STARTING_SKILLS],
    });
    player.restoreFull();
    return player;
  }

  get name() {
    return this.data.name;
  }
  get level() {
    return this.data.level;
  }
  get hp() {
    return this.data.hp;
  }
  get mp() {
    return this.data.mp;
  }
  get gold() {
    return this.data.gold;
  }

  get weapon(): WeaponDef | null {
    const id = this.data.equipment.weapon;
    return id ? (db.item(id) as WeaponDef) : null;
  }

  get armor(): ArmorDef | null {
    const id = this.data.equipment.armor;
    return id ? (db.item(id) as ArmorDef) : null;
  }

  /** Base stats plus equipment bonuses. */
  get stats(): Stats {
    const total = { ...this.data.baseStats };
    for (const gear of [this.weapon, this.armor]) {
      if (!gear?.stats) continue;
      for (const key of STAT_KEYS) total[key] += gear.stats[key] ?? 0;
    }
    return total;
  }

  get maxHp(): number {
    return 40 + this.level * 10 + this.stats.end * 4;
  }

  get maxMp(): number {
    return 12 + this.level * 3 + this.stats.int * 2;
  }

  get defense(): number {
    return this.armor?.defense ?? 0;
  }

  get resist(): number {
    return this.armor?.resist ?? 0;
  }

  get weaponDamageType(): DamageType {
    return this.weapon?.damageType ?? 'physical';
  }

  get weaponDamage(): [number, number] {
    return this.weapon?.damage ?? UNARMED;
  }

  statFor(type: DamageType): number {
    return type === 'magic' ? this.stats.int : this.stats.str;
  }

  /** Damage range of a basic attack, before enemy defense. For UI. */
  get attackRange(): [number, number] {
    const bonus = Math.floor(this.statFor(this.weaponDamageType) * BALANCE.statDamageScale);
    const [min, max] = this.weaponDamage;
    return [min + bonus, max + bonus];
  }

  get xp() {
    return this.data.xp;
  }

  get xpNeeded(): number {
    return xpToNext(this.level);
  }

  get knownSkills(): SkillDef[] {
    return this.data.skills.map((id) => db.skill(id));
  }

  get isDead(): boolean {
    return this.data.hp <= 0;
  }

  restoreFull(): void {
    this.data.hp = this.maxHp;
    this.data.mp = this.maxMp;
  }

  /** Keep HP/MP inside bounds after gear or level changes. */
  clampVitals(): void {
    this.data.hp = Math.min(Math.max(0, this.data.hp), this.maxHp);
    this.data.mp = Math.min(Math.max(0, this.data.mp), this.maxMp);
  }

  takeDamage(amount: number): void {
    this.data.hp = Math.max(0, this.data.hp - amount);
  }

  heal(amount: number): number {
    const before = this.data.hp;
    this.data.hp = Math.min(this.maxHp, this.data.hp + amount);
    return this.data.hp - before;
  }

  restoreMp(amount: number): number {
    const before = this.data.mp;
    this.data.mp = Math.min(this.maxMp, this.data.mp + amount);
    return this.data.mp - before;
  }

  spendMp(amount: number): boolean {
    if (this.data.mp < amount) return false;
    this.data.mp -= amount;
    return true;
  }

  // --- Progression ---

  /** Returns the number of levels gained. */
  addXp(amount: number): number {
    let gained = 0;
    this.data.xp += amount;
    while (this.level < BALANCE.maxLevel && this.data.xp >= this.xpNeeded) {
      this.data.xp -= this.xpNeeded;
      this.data.level += 1;
      for (const key of STAT_KEYS) this.data.baseStats[key] += 1;
      gained += 1;
    }
    if (this.level >= BALANCE.maxLevel) this.data.xp = 0;
    if (gained > 0) this.restoreFull();
    return gained;
  }

  /** Returns the number of levels gained. */
  grantRewards(rewards: Rewards): number {
    this.data.gold += rewards.gold;
    for (const id of rewards.items) this.addItem(id);
    return this.addXp(rewards.xp);
  }

  // --- Inventory ---

  countItem(itemId: string): number {
    return this.data.inventory.find((e) => e.itemId === itemId)?.qty ?? 0;
  }

  addItem(itemId: string, qty = 1): void {
    const entry = this.data.inventory.find((e) => e.itemId === itemId);
    if (entry) entry.qty += qty;
    else this.data.inventory.push({ itemId, qty });
  }

  removeItem(itemId: string, qty = 1): boolean {
    const entry = this.data.inventory.find((e) => e.itemId === itemId);
    if (!entry || entry.qty < qty) return false;
    entry.qty -= qty;
    if (entry.qty === 0) this.data.inventory = this.data.inventory.filter((e) => e !== entry);
    return true;
  }

  buy(itemId: string): boolean {
    const item = db.item(itemId);
    if (this.data.gold < item.price) return false;
    this.data.gold -= item.price;
    this.addItem(itemId);
    return true;
  }

  /** Equip an item from the bag. The previously equipped item goes back to the bag. */
  equip(itemId: string): boolean {
    const item = db.item(itemId);
    if (item.type === 'consumable') return false;
    if (!this.removeItem(itemId)) return false;
    const slot: EquipSlot = item.type;
    const previous = this.data.equipment[slot];
    if (previous) this.addItem(previous);
    this.data.equipment[slot] = itemId;
    this.clampVitals();
    return true;
  }

  unequip(slot: EquipSlot): boolean {
    const current = this.data.equipment[slot];
    if (!current) return false;
    this.addItem(current);
    this.data.equipment[slot] = null;
    this.clampVitals();
    return true;
  }

  /** Use a consumable from the bag. Returns what was restored, or null if it would do nothing. */
  useConsumable(itemId: string): { hp: number; mp: number } | null {
    const item = db.item(itemId);
    if (item.type !== 'consumable') return null;
    const restore = (item as ConsumableDef).restore;
    const useful = (restore.hp && this.hp < this.maxHp) || (restore.mp && this.mp < this.maxMp);
    if (!useful || !this.removeItem(itemId)) return null;
    return {
      hp: restore.hp ? this.heal(restore.hp) : 0,
      mp: restore.mp ? this.restoreMp(restore.mp) : 0,
    };
  }
}
