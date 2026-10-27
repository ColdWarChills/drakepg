import { BALANCE } from '../config';
import { db } from '../data/db';
import { START } from '../data/world';
import {
  accessoryValue,
  ALL_SLOTS,
  EQUIP_SLOTS,
  fitsSlot,
  gearDef,
  isGearDef,
  lineBonus,
  modTotal,
  modValue,
  rollBonus,
  rolledDamage,
  rolledDefense,
  rolledProcChance,
  sellValue,
  slotsFor,
  type Gear,
  type GearDef,
  type GearTraits,
  type ItemGrant,
  type SigilTier,
} from './Items';
import type { GuildState } from './Guild';
import { setBonusTotals, type SetBonusTotals } from './Sets';
import { emptyTower, type TowerState } from './Tower';
import type {
  ArmorDef,
  ClassDef,
  ClassId,
  ConsumableDef,
  DamageType,
  Element,
  EquipSlot,
  Look,
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

/** A stack of consumables in the bag. */
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
  /** Equipped gear. Changed from item ids to rolled gear in save version 4; 8 slots since version 11, plus the spare weapon since 13. */
  equipment: Record<EquipSlot, Gear | null>;
  /** Consumable stacks. */
  inventory: InventoryEntry[];
  /** Unequipped weapons and armor, each with its own rolls. Added in save version 4. */
  gear: Gear[];
  /** Counter for gear uids. */
  nextUid: number;
  /** Where the player stands. Added in save version 2. */
  location: Location;
  /** Added in save version 3. */
  classId: ClassId;
  /** Unspent points from leveling up. */
  statPoints: number;
  quests: Record<string, QuestState>;
  /** Bad-luck protection: kills since the last Rare, per loot table id. Added in save version 7. */
  lootPity: Record<string, number>;
  /** Boss grit: losses to each boss (monster id) since last beating it. Added in save version 8. */
  bossGrit: Record<string, number>;
  /** Announced unlocks and skills ('feature:<id>', 'skill:<id>'). Added in save version 9. */
  seen: string[];
  /** Skills learned but not used yet: tagged NEW in battle. */
  freshSkills: string[];
  /** Skill ids carried into battle, at most `BALANCE.skillSlots`. Added in save version 10. */
  loadout: string[];
  /** Rooms you've been to (the map). Added in save version 12. */
  visited: string[];
  /** The last town you walked into: where a Recall Scroll takes you. Null before the first town. Added in save version 12. */
  lastTown: string | null;
  /** Rooms whose gate guard (a room boss without a quest) is beaten for good. Added in save version 14. */
  guardsBeaten: string[];
  /** Adventurers' Guild rank, rank points, board and taken hunts (src/game/Guild.ts). Added in save version 15. */
  guild: GuildState;
  /** Skills learned from books, in the order read (src/game/Books.ts). Added in save version 16. */
  learned: string[];
  /** Elite kills without a book drop, per loot table (`BALANCE.books.drop`). Added in save version 16. */
  bookPity: Record<string, number>;
  /** The tower: highest floor cleared and marks (src/game/Tower.ts). Added in save version 17. */
  tower: TowerState;
}

export interface Rewards {
  xp: number;
  gold: number;
  items: ItemGrant[];
}

export type BuyResult = 'ok' | 'gold' | 'full';

/** XP needed to go from `level` to `level + 1`. */
export function xpToNext(level: number): number {
  return Math.floor(50 * Math.pow(level, 1.5));
}

const UNARMED: [number, number] = [2, 4];

/** Every slot empty. */
export function emptyEquipment(): Record<EquipSlot, Gear | null> {
  return Object.fromEntries(ALL_SLOTS.map((s) => [s, null])) as Record<EquipSlot, Gear | null>;
}

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
      equipment: emptyEquipment(),
      inventory: [
        { itemId: 'health_potion', qty: 2 },
        { itemId: 'mana_potion', qty: 1 },
        { itemId: 'recall_scroll', qty: BALANCE.startingRecallScrolls },
      ],
      gear: [],
      nextUid: 1,
      location: { ...START },
      classId,
      statPoints: 0,
      quests: {},
      lootPity: {},
      bossGrit: {},
      seen: [],
      freshSkills: [],
      loadout: [],
      visited: [],
      lastTown: null,
      guardsBeaten: [],
      guild: { rank: 0, points: 0, board: [], hunts: [], done: 0 },
      learned: [],
      bookPity: {},
      tower: emptyTower(),
    });
    player.data.loadout = player.defaultLoadout();
    player.data.equipment.weapon = player.makeGear(cls.weapon);
    // No armor yet: it drops on the prologue road (or Tobin sells it in town).
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

  get weapon(): WeaponDef | null {
    const g = this.data.equipment.weapon;
    return g ? (gearDef(g) as WeaponDef) : null;
  }

  /** The second weapon, carried for a swap (its stats don't count until it's in hand). */
  get spareWeapon(): WeaponDef | null {
    const g = this.data.equipment.spare;
    return g ? (gearDef(g) as WeaponDef) : null;
  }

  /** Attack range with the spare weapon in hand (its stats and lines counted, like after a swap). For UI and the sim. */
  get spareAttackRange(): [number, number] | null {
    return this.data.equipment.spare ? this.withSpareInHand(() => this.attackRange) : null;
  }

  /** Run `fn` as if the weapons were swapped, then swap back. Nothing else changes. */
  private withSpareInHand<T>(fn: () => T): T {
    const e = this.data.equipment;
    [e.weapon, e.spare] = [e.spare, e.weapon];
    try {
      return fn();
    } finally {
      [e.weapon, e.spare] = [e.spare, e.weapon];
    }
  }

  /** Put the spare weapon in hand and carry the old one as the spare (either may be empty). False with neither. */
  swapWeapons(): boolean {
    const e = this.data.equipment;
    if (!e.spare && !e.weapon) return false;
    [e.weapon, e.spare] = [e.spare, e.weapon];
    this.clampVitals();
    return true;
  }

  /** Sigil of the equipped weapon, for the glow in the hero's hands. */
  get weaponSigil(): SigilTier | undefined {
    return this.data.equipment.weapon?.sigil;
  }

  /** Body armor: it decides the hero's outfit. */
  get bodyArmor(): ArmorDef | null {
    const g = this.data.equipment.body;
    return g ? (gearDef(g) as ArmorDef) : null;
  }

  /**
   * How the hero is drawn: the class look, dressed in the armor's outfit when that art exists. The outfit is named after
   * the bare art (`char_hero_bare` -> `char_hero_<armor id>`). Missing art falls back to the class's own pictures, so
   * armors without art just keep the default look.
   */
  get look(): Look {
    const cls = this.classDef.look;
    const armor = this.bodyArmor;
    if (!armor) return cls;
    const classBare = cls.bare === undefined ? [] : Array.isArray(cls.bare) ? cls.bare : [cls.bare];
    const base = classBare[0]?.art.replace(/_bare$/, '') ?? `char_${this.data.classId}`;
    const art = `${base}_${armor.id}`;
    const hands = armor.hands?.[this.data.classId] ?? classBare[0]?.hands ?? [];
    const classArt = cls.art === undefined ? [] : Array.isArray(cls.art) ? cls.art : [cls.art];
    const poses = classBare[0]?.poses;
    return { ...cls, art: [art, ...classArt], bare: [{ art, hands, ...(poses ? { poses } : {}) }, ...classBare] };
  }

  private traitsOf(slot: EquipSlot): GearTraits {
    return this.data.equipment[slot] ?? { rolls: {} };
  }

  private get worn(): (Gear | null)[] {
    return EQUIP_SLOTS.map((s) => this.data.equipment[s]);
  }

  /** Every worn piece with its item. */
  private get wornPieces(): { def: GearDef; gear: Gear }[] {
    return this.worn.filter((g): g is Gear => !!g).map((gear) => ({ def: gearDef(gear), gear }));
  }

  /** What the active set bonuses add up to (src/game/Sets.ts). */
  get setBonus(): SetBonusTotals {
    return setBonusTotals(this.data.equipment);
  }

  /** Base stats plus equipment bonuses (item stats, accessories, stat mods and set bonuses). */
  get stats(): Stats {
    const total = { ...this.data.baseStats };
    const set = this.setBonus;
    for (const { def, gear } of this.wornPieces) {
      if (!def.stats) continue;
      for (const key of STAT_KEYS) {
        const v = def.stats[key] ?? 0;
        total[key] += def.type === 'accessory' ? accessoryValue(v, gear) : v;
      }
    }
    for (const key of STAT_KEYS) total[key] += modTotal(this.worn, key) + (set.stats[key] ?? 0);
    return total;
  }

  /** Flat HP or MP from accessories. */
  private accessoryTotal(key: 'hp' | 'mp'): number {
    let total = 0;
    for (const { def, gear } of this.wornPieces) if (def.type === 'accessory' && def[key]) total += accessoryValue(def[key]!, gear);
    return total;
  }

  get maxHp(): number {
    let pct = this.setBonus.hpPct / 100;
    for (const { def, gear } of this.wornPieces) if (def.type === 'armor') pct += lineBonus(def, 'hp', gear.rolls);
    const base = (40 + this.level * 10 + this.stats.end * 4) * (1 + pct);
    return Math.round(base) + modTotal(this.worn, 'hp') + this.accessoryTotal('hp');
  }

  get maxMp(): number {
    return 12 + this.level * 3 + this.stats.int * 2 + modTotal(this.worn, 'mp') + this.accessoryTotal('mp');
  }

  /** Every armor piece's defense, raised by set bonuses. */
  defenseFor(type: DamageType): number {
    let total = 0;
    for (const { def, gear } of this.wornPieces) if (def.type === 'armor') total += rolledDefense(def, gear, type);
    return Math.round(total * (1 + this.setBonus.defensePct / 100));
  }

  /** Elemental resistances from armor, accessories, resist mods and set bonuses (before buffs and the cap, which Battle applies). */
  get resists(): Resists {
    const out: Resists = {};
    const add = (r: Resists | undefined, scale = (v: number) => v) => {
      for (const [k, v] of Object.entries(r ?? {}) as [keyof Resists, number][]) out[k] = (out[k] ?? 0) + scale(v);
    };
    for (const { def, gear } of this.wornPieces) {
      if (def.type === 'armor') add(def.resists);
      if (def.type === 'accessory') add(def.resists, (v) => accessoryValue(v, gear));
      for (const m of gear.mods ?? []) if (m.key === 'resist' && m.element) out[m.element] = (out[m.element] ?? 0) + modValue(m);
    }
    add(this.setBonus.resists);
    return out;
  }

  /** Extra damage dealt from set bonuses, as a fraction. Battle multiplies the hero's hits by 1 + this. */
  get damageBonus(): number {
    return this.setBonus.damagePct / 100;
  }

  get weaponDamageType(): DamageType {
    return this.weapon?.damageType ?? 'melee';
  }

  /** Undefined = neutral. */
  get weaponElement(): Element | undefined {
    return this.weapon?.element;
  }

  get weaponDamage(): [number, number] {
    const weapon = this.weapon;
    return weapon ? rolledDamage(weapon, this.traitsOf('weapon')) : UNARMED;
  }

  /** Chance the weapon special triggers on a landed Attack (0 without one). */
  get procChance(): number {
    const weapon = this.weapon;
    return weapon ? rolledProcChance(weapon, this.traitsOf('weapon')) : 0;
  }

  /** Crit chance added by the weapon's Crit line, crit mods, accessories and set bonuses. */
  get critBonus(): number {
    let accessories = 0;
    for (const { def, gear } of this.wornPieces) if (def.type === 'accessory' && def.crit) accessories += accessoryValue(def.crit, gear, 1);
    return rollBonus('crit', this.traitsOf('weapon').rolls) + (modTotal(this.worn, 'crit') + accessories + this.setBonus.crit) / 100;
  }

  /** Crit chance before battle buffs. For UI; Battle works it out the same way. */
  get critChance(): number {
    return BALANCE.baseCritChance + this.stats.dex * BALANCE.critPerDex + this.critBonus;
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

  /** Level skills unlocked at the current level, then skills learned from books (in the order read). */
  get knownSkills(): SkillDef[] {
    const fromLevels = this.classDef.skills.filter((s) => s.level <= this.level).map((s) => s.skillId);
    return [...fromLevels, ...this.data.learned.filter((id) => !fromLevels.includes(id))].map((id) => db.skill(id));
  }

  knowsSkill(skillId: string): boolean {
    return this.data.learned.includes(skillId) || this.classDef.skills.some((s) => s.skillId === skillId && s.level <= this.level);
  }

  /** Known skills carried into battle, in loadout order. */
  get carriedSkills(): SkillDef[] {
    const known = new Set(this.knownSkills.map((s) => s.id));
    return this.data.loadout.filter((id) => known.has(id)).map((id) => db.skill(id));
  }

  isCarried(skillId: string): boolean {
    return this.data.loadout.includes(skillId);
  }

  /** A sensible pick: the class's first heal plus its newest other skills, up to the slot count. */
  defaultLoadout(): string[] {
    const known = this.knownSkills;
    const heal = known.find((s) => s.kind === 'heal');
    const rest = known.filter((s) => s !== heal).reverse();
    const picked = new Set([...(heal ? [heal] : []), ...rest].slice(0, BALANCE.skillSlots).map((s) => s.id));
    // Keep the class's own order (the order they were learned) for the battle menu.
    return known.filter((s) => picked.has(s.id)).map((s) => s.id);
  }

  /** Carry or put away a known skill. 'full' if every slot is taken. */
  toggleCarry(skillId: string): 'added' | 'removed' | 'full' | 'unknown' {
    if (!this.knowsSkill(skillId)) return 'unknown';
    const list = this.data.loadout;
    if (list.includes(skillId)) {
      list.splice(list.indexOf(skillId), 1);
      return 'removed';
    }
    if (list.length >= BALANCE.skillSlots) return 'full';
    list.push(skillId);
    return 'added';
  }

  /** A newly learned skill goes into a free slot, if there is one. */
  carryNew(skillId: string): boolean {
    if (this.isCarried(skillId) || this.data.loadout.length >= BALANCE.skillSlots) return this.isCarried(skillId);
    this.data.loadout.push(skillId);
    return true;
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

  /**
   * Gold, XP and items. Items that don't fit in the bag are returned in `lost`;
   * `force` lets them overflow the bag instead (quest rewards are never lost).
   */
  grantRewards(rewards: Rewards, force = false): { levels: number; lost: ItemGrant[] } {
    this.data.gold += rewards.gold;
    const lost = rewards.items.filter((grant) => !this.receive(grant, force));
    return { levels: this.addXp(rewards.xp), lost };
  }

  // --- Inventory ---

  /** Bag slots in use: one per gear piece and one per consumable stack. */
  /** Materials ride in a pouch and take no bag slots. */
  get usedSlots(): number {
    return this.data.gear.length + this.data.inventory.filter((e) => db.item(e.itemId).type !== 'material').length;
  }

  get freeSlots(): number {
    return Math.max(0, BALANCE.bagSlots - this.usedSlots);
  }

  /** A new gear instance (not yet placed anywhere). */
  makeGear(itemId: string, traits: Partial<GearTraits> = {}, isNew = false): Gear {
    const gear: Gear = { uid: `g${this.data.nextUid++}`, itemId, rolls: { ...traits.rolls } };
    if (traits.sigil) gear.sigil = traits.sigil;
    if (traits.mods?.length) gear.mods = traits.mods.map((m) => ({ ...m }));
    if (isNew) gear.isNew = true;
    // Sigils are rare enough that selling one by accident would hurt.
    if (isNew && gear.sigil) gear.locked = true;
    return gear;
  }

  /** Put an item in the bag. False if it doesn't fit (unless forced). */
  receive(grant: ItemGrant, force = false): boolean {
    const def = db.item(grant.itemId);
    const stacks = def.type === 'material' || (!isGearDef(def) && this.data.inventory.some((e) => e.itemId === grant.itemId));
    if (!stacks && !force && this.freeSlots === 0) return false;
    if (isGearDef(def)) this.data.gear.push(this.makeGear(grant.itemId, grant, true));
    else this.addItem(grant.itemId);
    return true;
  }

  /** Consumables in the bag plus gear owned (bag and equipped). */
  countItem(itemId: string): number {
    if (!isGearDef(db.item(itemId))) return this.data.inventory.find((e) => e.itemId === itemId)?.qty ?? 0;
    const equipped = Object.values(this.data.equipment).filter((g) => g?.itemId === itemId).length;
    return equipped + this.data.gear.filter((g) => g.itemId === itemId).length;
  }

  /** Add consumables, or 0% gear. Ignores the bag limit; use `receive` for loot. */
  addItem(itemId: string, qty = 1): void {
    if (isGearDef(db.item(itemId))) {
      for (let i = 0; i < qty; i++) this.data.gear.push(this.makeGear(itemId));
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

  /** Shop gear is always Standard (0%). */
  buy(itemId: string): BuyResult {
    const item = db.item(itemId);
    if (this.data.gold < item.price) return 'gold';
    if (!this.receive({ itemId, rolls: {} })) return 'full';
    this.data.gold -= item.price;
    return 'ok';
  }

  /** Gear in the bag or equipped. */
  findGear(uid: string): Gear | null {
    return this.data.gear.find((g) => g.uid === uid) ?? Object.values(this.data.equipment).find((g) => g?.uid === uid) ?? null;
  }

  equippedSlot(uid: string): EquipSlot | null {
    for (const slot of ALL_SLOTS) if (this.data.equipment[slot]?.uid === uid) return slot;
    return null;
  }

  /** Where a piece would go: its only slot, or for rings the first empty ring slot (else the first). */
  slotFor(def: GearDef): EquipSlot {
    const slots = slotsFor(def);
    return slots.find((s) => !this.data.equipment[s]) ?? slots[0];
  }

  /**
   * Equip gear from the bag, into `slot` if it fits there (rings: either ring slot, weapons: the spare slot), else its
   * usual slot. The previously equipped piece takes its place in the bag.
   */
  equip(uid: string, slot?: EquipSlot): boolean {
    const index = this.data.gear.findIndex((g) => g.uid === uid);
    if (index < 0) return false;
    const gear = this.data.gear[index];
    const def = gearDef(gear);
    if (!slot || !fitsSlot(def, slot)) slot = this.slotFor(def);
    const previous = this.data.equipment[slot];
    delete gear.isNew;
    this.data.equipment[slot] = gear;
    if (previous) this.data.gear.splice(index, 1, previous);
    else this.data.gear.splice(index, 1);
    this.clampVitals();
    return true;
  }

  /** Needs a free bag slot. */
  unequip(slot: EquipSlot): boolean {
    const current = this.data.equipment[slot];
    if (!current || this.freeSlots === 0) return false;
    this.data.gear.push(current);
    this.data.equipment[slot] = null;
    this.clampVitals();
    return true;
  }

  toggleLock(uid: string): void {
    const gear = this.findGear(uid);
    if (!gear) return;
    if (gear.locked) delete gear.locked;
    else gear.locked = true;
  }

  /** Sell unequipped, unlocked gear. Returns the gold earned (0 = not sold). */
  sellGear(uid: string): number {
    const index = this.data.gear.findIndex((g) => g.uid === uid);
    const gear = this.data.gear[index];
    if (!gear || gear.locked) return 0;
    const gold = sellValue(gearDef(gear), gear);
    this.data.gear.splice(index, 1);
    this.data.gold += gold;
    return gold;
  }

  /** Sell one consumable. Returns the gold earned (0 = none left). */
  sellItem(itemId: string): number {
    if (!this.removeItem(itemId)) return 0;
    const gold = sellValue(db.item(itemId));
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
