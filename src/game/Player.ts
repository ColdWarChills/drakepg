import { BALANCE } from '../config';
import { db } from '../data/db';
import { START } from '../data/world';
import { gearDef, isGear, resolveGear, rollGear, sellPrice, itemSellPrice, type GearInstance } from './Gear';
import type {
  ArmorDef,
  ClassDef,
  ClassId,
  ConsumableDef,
  DamageType,
  Element,
  EquipSlot,
  Resists,
  SkillDef,
  StatKey,
  Stats,
  WeaponDef,
} from '../data/types';

export const STAT_KEYS: StatKey[] = ['str', 'int', 'dex', 'end'];

/** Melee scales with STR, pierce with DEX, magic with INT. */
export function statFor(stats: Stats, type: DamageType): number {
  return type === 'magic' ? stats.int : type === 'pierce' ? stats.dex : stats.str;
}

export const STAT_LABELS: Record<StatKey, string> = {
  str: 'STR',
  int: 'INT',
  dex: 'DEX',
  end: 'END',
};

export interface Location {
  roomId: string;
  x: number;
  y: number;
}

/** Progress on a quest. No entry = not started. */
export interface QuestState {
  status: 'active' | 'done';
  count: number;
}

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
  /** Uid of the equipped gear (an entry in `gear`). */
  equipment: Record<EquipSlot, number | null>;
  /** Potions and materials (stackable). */
  inventory: InventoryEntry[];
  /** Every weapon and armor piece owned, equipped or not. Added in save version 4. */
  gear: GearInstance[];
  nextUid: number;
  /** Where the player stands. Added in save version 2. */
  location: Location;
  /** Added in save version 3. */
  classId: ClassId;
  /** Unspent points from leveling up. */
  statPoints: number;
  quests: Record<string, QuestState>;
}

export interface Rewards {
  xp: number;
  gold: number;
  /** Stackable item ids (one entry per item). */
  items: string[];
  /** Rolled gear. */
  gear: GearInstance[];
}

/** XP needed to go from `level` to `level + 1`. */
export function xpToNext(level: number): number {
  return Math.floor(50 * Math.pow(level, 1.5));
}

const UNARMED: [number, number] = [2, 4];

export class Player {
  constructor(public data: PlayerData) {}

  static createNew(name = 'Hero', classId: ClassId = 'warrior'): Player {
    const cls = db.class(classId);
    const player = new Player({
      name,
      level: 1,
      xp: 0,
      gold: 30,
      baseStats: { ...cls.stats },
      hp: 1,
      mp: 1,
      equipment: { weapon: null, armor: null },
      inventory: [
        { itemId: 'health_potion', qty: 2 },
        { itemId: 'mana_potion', qty: 1 },
      ],
      gear: [],
      nextUid: 1,
      location: { ...START },
      classId,
      statPoints: 0,
      quests: {},
    });
    for (const id of [cls.weapon, cls.armor]) player.equip(player.addGear(rollGear(id, 'shop')).uid);
    player.restoreFull();
    return player;
  }

  get name() {
    return this.data.name;
  }
  get classDef(): ClassDef {
    return db.class(this.data.classId);
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

  /** Equipped gear instance in a slot. */
  equipped(slot: EquipSlot): GearInstance | null {
    const uid = this.data.equipment[slot];
    return uid === null ? null : (this.gearByUid(uid) ?? null);
  }

  /** The equipped weapon with its rolls applied. */
  get weapon(): WeaponDef | null {
    const g = this.equipped('weapon');
    return g ? (resolveGear(g) as WeaponDef) : null;
  }

  get armor(): ArmorDef | null {
    const g = this.equipped('armor');
    return g ? (resolveGear(g) as ArmorDef) : null;
  }

  /** Added to hit chance by gear. */
  get hitBonus(): number {
    return this.weapon?.hit ?? 0;
  }

  get critBonus(): number {
    return this.weapon?.crit ?? 0;
  }

  /** Subtracted from enemies' hit chance by gear. */
  get evasion(): number {
    return this.armor?.evasion ?? 0;
  }

  /** Room walk speed change from armor (0.2 = 20% faster). */
  get walkBonus(): number {
    return this.armor?.walk ?? 0;
  }

  /** MP a skill costs with the current armor (plate costs more, cloth less). */
  skillCost(skill: SkillDef): number {
    return Math.max(0, Math.round(skill.mpCost * (1 + (this.armor?.mpCost ?? 0))));
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

  defenseFor(type: DamageType): number {
    return this.armor?.defense[type] ?? 0;
  }

  /** Elemental resistances from gear (before buffs and the cap, which Battle applies). */
  get resists(): Resists {
    return { ...this.armor?.resists };
  }

  get weaponDamageType(): DamageType {
    return this.weapon?.damageType ?? 'melee';
  }

  /** Undefined = neutral. */
  get weaponElement(): Element | undefined {
    return this.weapon?.element;
  }

  get weaponDamage(): [number, number] {
    return this.weapon?.damage ?? UNARMED;
  }

  statFor(type: DamageType): number {
    return statFor(this.stats, type);
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

  /** Class skills unlocked at the current level. */
  get knownSkills(): SkillDef[] {
    return this.classDef.skills.filter((s) => s.level <= this.level).map((s) => db.skill(s.skillId));
  }

  /** Skills that unlock after `fromLevel`, up to the current level. */
  skillsLearnedSince(fromLevel: number): SkillDef[] {
    return this.classDef.skills
      .filter((s) => s.level > fromLevel && s.level <= this.level)
      .map((s) => db.skill(s.skillId));
  }

  /** Spend one free point. Returns false if there are none. */
  spendStatPoint(stat: StatKey): boolean {
    if (this.data.statPoints <= 0) return false;
    this.data.statPoints -= 1;
    this.data.baseStats[stat] += 1;
    // Current HP/MP grow along with the max they just raised.
    if (stat === 'end') this.data.hp += 4;
    if (stat === 'int') this.data.mp += 2;
    this.clampVitals();
    return true;
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
      const growth = this.classDef.growth;
      for (const key of STAT_KEYS) this.data.baseStats[key] += growth[key] ?? 0;
      this.data.statPoints += BALANCE.statPointsPerLevel;
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
    for (const g of rewards.gear) this.addGear(g);
    return this.addXp(rewards.xp);
  }

  // --- Inventory ---

  countItem(itemId: string): number {
    return this.data.inventory.find((e) => e.itemId === itemId)?.qty ?? 0;
  }

  /** Stackables go in the bag; gear ids become fresh 0% instances (use addGear for rolled gear). */
  addItem(itemId: string, qty = 1): void {
    if (isGear(db.item(itemId))) {
      for (let i = 0; i < qty; i++) this.addGear(rollGear(itemId, 'shop'));
      return;
    }
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

  // --- Gear ---

  gearByUid(uid: number): GearInstance | undefined {
    return this.data.gear.find((g) => g.uid === uid);
  }

  isEquipped(uid: number): boolean {
    return this.data.equipment.weapon === uid || this.data.equipment.armor === uid;
  }

  /** Gear not currently worn. */
  get bagGear(): GearInstance[] {
    return this.data.gear.filter((g) => !this.isEquipped(g.uid));
  }

  /** Take ownership of a rolled instance. Returns it with its new uid. */
  addGear(inst: GearInstance): GearInstance {
    const owned = { ...inst, q: [...inst.q], traits: inst.traits.map((t) => ({ ...t })), uid: this.data.nextUid++ };
    this.data.gear.push(owned);
    return owned;
  }

  /** Remove unequipped gear (sold or salvaged). */
  removeGear(uid: number): boolean {
    if (this.isEquipped(uid) || !this.gearByUid(uid)) return false;
    this.data.gear = this.data.gear.filter((g) => g.uid !== uid);
    return true;
  }

  canEquip(inst: GearInstance): boolean {
    return this.level >= gearDef(inst.baseId).level;
  }

  /** Wear a piece of owned gear. The previous one stays in the bag. */
  equip(uid: number): boolean {
    const inst = this.gearByUid(uid);
    if (!inst || !this.canEquip(inst)) return false;
    this.data.equipment[gearDef(inst.baseId).type] = uid;
    this.clampVitals();
    return true;
  }

  unequip(slot: EquipSlot): boolean {
    if (this.data.equipment[slot] === null) return false;
    this.data.equipment[slot] = null;
    this.clampVitals();
    return true;
  }

  sellGear(uid: number): number {
    const inst = this.gearByUid(uid);
    if (!inst || !this.removeGear(uid)) return 0;
    const gold = sellPrice(inst);
    this.data.gold += gold;
    return gold;
  }

  /** Sell one of a stackable item. */
  sellItem(itemId: string): number {
    if (!this.removeItem(itemId)) return 0;
    const gold = itemSellPrice(itemId);
    this.data.gold += gold;
    return gold;
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
