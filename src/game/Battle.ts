import { BALANCE } from '../config';
import { db } from '../data/db';
import type { Apply, DamageType, Element, MonsterAttack, MonsterDef, Resists, StatusDef, Stats } from '../data/types';
import { isGear, rollGear } from './Gear';
import { statFor, type Player, type Rewards } from './Player';
import { chance, clamp, defaultRng, randInt, weightedPick, type Rng } from './rng';

export type Side = 'player' | 'enemy';

/** How an element landed, for the "Weak!" / "Resisted" popups. */
export type Effect = 'weak' | 'resisted' | 'immune';

/**
 * What happened during a turn, in order. The battle scene plays these back as animations,
 * so the rules here never touch Phaser.
 * `moveId` is 'attack', 'proc', a skill id, an item id, or 'enemy' for monster moves.
 */
export type BattleEvent =
  | {
      kind: 'hit';
      source: Side;
      move: string;
      moveId: string;
      damageType: DamageType;
      element?: Element;
      damage: number;
      crit: boolean;
      effect?: Effect;
    }
  | { kind: 'miss'; source: Side; move: string; moveId: string; damageType: DamageType; element?: Element }
  | { kind: 'heal'; source: Side; move: string; moveId: string; hp: number; mp: number }
  /** A move that only applies statuses (buff skills, a monster hardening its bark). */
  | { kind: 'buff'; source: Side; move: string; moveId: string }
  | { kind: 'status'; target: Side; status: string; turns: number }
  /** The target ignores this status (bosses vs stun). */
  | { kind: 'immune'; target: Side; status: string }
  /** Start-of-turn damage or healing from a status. */
  | { kind: 'tick'; target: Side; status: string; damage: number; heal: number; element?: Element; effect?: Effect }
  | { kind: 'expire'; target: Side; status: string }
  | { kind: 'stunned'; side: Side }
  | { kind: 'flee'; success: boolean }
  | { kind: 'defend'; mp: number }
  /** Enemy is winding up a telegraphed attack that lands next turn. */
  | { kind: 'windup'; move: string; text: string }
  | { kind: 'defeated'; side: Side };

export type BattleState = 'player_turn' | 'victory' | 'defeat' | 'fled';

export interface Enemy {
  def: MonsterDef;
  hp: number;
  maxHp: number;
}

export interface ActiveStatus {
  def: StatusDef;
  /** Owner's turns left. */
  turns: number;
  /** Applied during the owner's own turn: that turn doesn't count. */
  fresh: boolean;
  /** Damage-over-time per tick, fixed when applied. */
  tick: number;
  element?: Element;
}

/** Share of damage removed by `armor`. 10 armor = 25%, 30 armor = 50%. */
function mitigate(damage: number, armor: number): number {
  return damage * (1 - armor / (armor + 30));
}

type ModKey = 'damageDealt' | 'damageTaken' | 'hitChance' | 'critChance' | 'evasion';

export class Battle {
  readonly enemy: Enemy;
  state: BattleState = 'player_turn';
  rewards: Rewards | null = null;
  /** Telegraphed attack the enemy will use on its next turn. */
  charging: MonsterAttack | null = null;
  private defending = false;
  private readonly cooldowns = new Map<string, number>();
  private readonly effects: Record<Side, ActiveStatus[]> = { player: [], enemy: [] };

  constructor(
    readonly player: Player,
    monster: MonsterDef,
    private readonly rng: Rng = defaultRng,
  ) {
    this.enemy = { def: monster, hp: monster.hp, maxHp: monster.hp };
  }

  get isOver(): boolean {
    return this.state !== 'player_turn';
  }

  // --- Queries for the UI ---

  statuses(side: Side): readonly ActiveStatus[] {
    return this.effects[side];
  }

  /** Turns until the skill can be used again (0 = ready). */
  cooldownLeft(skillId: string): number {
    return this.cooldowns.get(skillId) ?? 0;
  }

  /** Current resistance to an element, including buffs and debuffs. */
  resistOf(side: Side, element: Element): number {
    const base: Resists = side === 'player' ? this.player.resists : (this.enemy.def.resists ?? {});
    let r = (base[element] ?? 0) + (base.all ?? 0);
    for (const s of this.effects[side]) r += (s.def.resists?.[element] ?? 0) + (s.def.resists?.all ?? 0);
    const max = side === 'player' ? BALANCE.maxPlayerResist : 100;
    return clamp(r, BALANCE.minResist, max);
  }

  // --- Player actions. Each returns the player's events plus the enemy's reply. ---

  attack(): BattleEvent[] {
    if (this.isOver) return [];
    const p = this.player;
    const events: BattleEvent[] = [];
    const hit = this.strike('player', 'Attack', 'attack', p.weaponDamageType, p.weaponElement, 1);
    events.push(hit);
    const proc = p.weapon?.proc;
    if (hit.kind === 'hit' && proc && this.enemy.hp > 0 && chance(this.rng, proc.chance)) {
      const element = proc.element ?? p.weaponElement;
      for (let i = 0; i < (proc.hits ?? 1) && proc.power > 0 && this.enemy.hp > 0; i++) {
        events.push(this.strike('player', proc.name, 'proc', p.weaponDamageType, element, proc.power));
      }
      if (proc.power === 0) events.push({ kind: 'buff', source: 'player', move: proc.name, moveId: 'proc' });
      for (const a of proc.applies ?? []) this.pushApply(events, 'player', a, element, p.weaponDamageType);
      if (proc.heal) {
        events.push({ kind: 'heal', source: 'player', move: proc.name, moveId: 'proc', hp: p.heal(Math.round(p.maxHp * proc.heal)), mp: 0 });
      }
    }
    return this.endPlayerTurn(events);
  }

  canUseSkill(skillId: string): boolean {
    return !this.isOver && this.player.mp >= this.player.skillCost(db.skill(skillId)) && !this.cooldowns.has(skillId);
  }

  useSkill(skillId: string): BattleEvent[] {
    if (!this.canUseSkill(skillId)) return [];
    const skill = db.skill(skillId);
    const p = this.player;
    p.spendMp(p.skillCost(skill));
    // +1 because the end of this turn already counts down once; then it's locked for `cooldown` turns.
    if (skill.cooldown > 0) this.cooldowns.set(skill.id, skill.cooldown + 1);

    const events: BattleEvent[] = [];
    const type = !skill.damageType || skill.damageType === 'weapon' ? p.weaponDamageType : skill.damageType;
    const element = !skill.element || skill.element === 'weapon' ? p.weaponElement : skill.element;
    let landed = false;

    if (skill.kind === 'heal') {
      const amount = Math.round(p.maxHp * skill.power + p.stats.int);
      events.push({ kind: 'heal', source: 'player', move: skill.name, moveId: skill.id, hp: p.heal(amount), mp: 0 });
    } else if (skill.kind === 'buff') {
      events.push({ kind: 'buff', source: 'player', move: skill.name, moveId: skill.id });
    } else {
      let dealt = 0;
      for (let i = 0; i < (skill.hits ?? 1) && this.enemy.hp > 0; i++) {
        const e = this.strike('player', skill.name, skill.id, type, element, skill.power, skill.critBonus ?? 0);
        events.push(e);
        if (e.kind === 'hit') {
          landed = true;
          dealt += e.damage;
        }
      }
      if (skill.lifesteal && dealt > 0) {
        events.push({ kind: 'heal', source: 'player', move: skill.name, moveId: skill.id, hp: p.heal(Math.round(dealt * skill.lifesteal)), mp: 0 });
      }
    }

    for (const a of skill.applies ?? []) {
      // Debuffs on the enemy need a living target, and damage skills need a hit to land.
      if (!a.self && (this.enemy.hp <= 0 || (skill.kind === 'damage' && !landed))) continue;
      this.pushApply(events, 'player', a, element, type);
    }
    return this.endPlayerTurn(events);
  }

  /** False if the item is missing or would restore nothing (e.g. a health potion at full HP). */
  canUseItem(itemId: string): boolean {
    const item = db.item(itemId);
    if (this.isOver || item.type !== 'consumable' || this.player.countItem(itemId) === 0) return false;
    const p = this.player;
    return Boolean((item.restore.hp && p.hp < p.maxHp) || (item.restore.mp && p.mp < p.maxMp));
  }

  useItem(itemId: string): BattleEvent[] {
    if (this.isOver) return [];
    const item = db.item(itemId);
    const restored = this.player.useConsumable(itemId);
    if (!restored) return [];
    const events: BattleEvent[] = [
      { kind: 'heal', source: 'player', move: item.name, moveId: item.id, hp: restored.hp, mp: restored.mp },
    ];
    return this.endPlayerTurn(events);
  }

  /** Brace: take reduced damage this turn and recover a little MP. */
  defend(): BattleEvent[] {
    if (this.isOver) return [];
    this.defending = true;
    const mp = this.player.restoreMp(BALANCE.defendMp);
    return this.endPlayerTurn([{ kind: 'defend', mp }]);
  }

  get canFlee(): boolean {
    return !this.enemy.def.boss;
  }

  get fleeChance(): number {
    if (!this.canFlee) return 0;
    const diff = this.player.stats.dex - this.enemy.def.stats.dex;
    return clamp(BALANCE.baseFleeChance + diff * 0.03, 0.2, 0.9);
  }

  flee(): BattleEvent[] {
    if (this.isOver || !this.canFlee) return [];
    if (chance(this.rng, this.fleeChance)) {
      this.state = 'fled';
      return [{ kind: 'flee', success: true }];
    }
    return this.endPlayerTurn([{ kind: 'flee', success: false }]);
  }

  // --- Turn flow ---

  private endPlayerTurn(events: BattleEvent[]): BattleEvent[] {
    if (this.checkOver(events)) return events;
    this.endTurn('player', events);
    // If the player starts their turn stunned, the enemy simply goes again.
    for (let round = 0; round < 3; round++) {
      this.enemyTurn(events);
      this.defending = false;
      if (this.checkOver(events)) return events;
      this.startTurn('player', events);
      if (this.checkOver(events)) return events;
      if (!this.consumeStun('player', events)) break;
      this.endTurn('player', events);
    }
    return events;
  }

  private enemyTurn(events: BattleEvent[]): void {
    this.startTurn('enemy', events);
    if (this.enemy.hp <= 0) return;
    if (this.consumeStun('enemy', events)) {
      this.charging = null;
    } else {
      this.enemyAction(events);
    }
    this.endTurn('enemy', events);
  }

  private enemyAction(events: BattleEvent[]): void {
    const m = this.enemy.def;
    let attack: MonsterAttack;
    if (this.charging) {
      attack = this.charging;
      this.charging = null;
    } else {
      attack = weightedPick(this.rng, m.attacks, (a) => a.weight);
      if (attack.windup) {
        this.charging = attack;
        events.push({ kind: 'windup', move: attack.name, text: attack.windup });
        return;
      }
    }
    const element = attack.element ?? m.element;
    if (attack.power === 0) {
      events.push({ kind: 'buff', source: 'enemy', move: attack.name, moveId: 'enemy' });
    } else {
      const hit = this.strike('enemy', attack.name, 'enemy', attack.damageType, element, attack.power);
      events.push(hit);
      if (hit.kind === 'miss') return;
    }
    for (const a of attack.applies ?? []) this.pushApply(events, 'enemy', a, element, attack.damageType);
  }

  /** Damage and healing over time. */
  private startTurn(side: Side, events: BattleEvent[]): void {
    for (const s of [...this.effects[side]]) {
      if (s.tick > 0) {
        const { mult, effect } = this.elementMultiplier(side, s.element);
        const damage = mult > 0 ? Math.max(1, Math.round(s.tick * mult)) : 0;
        this.damage(side, damage);
        events.push({ kind: 'tick', target: side, status: s.def.id, damage, heal: 0, element: s.element, effect });
      }
      if (s.def.regen) {
        const heal = this.healSide(side, Math.round(this.maxHpOf(side) * s.def.regen));
        events.push({ kind: 'tick', target: side, status: s.def.id, damage: 0, heal });
      }
      if (this.hpOf(side) <= 0) return;
    }
  }

  /** Count down statuses (and the player's cooldowns). */
  private endTurn(side: Side, events: BattleEvent[]): void {
    const list = this.effects[side];
    for (const s of [...list]) {
      if (s.fresh) {
        s.fresh = false;
        continue;
      }
      s.turns -= 1;
      if (s.turns <= 0) {
        list.splice(list.indexOf(s), 1);
        events.push({ kind: 'expire', target: side, status: s.def.id });
      }
    }
    if (side === 'player') {
      for (const [id, left] of this.cooldowns) {
        if (left <= 1) this.cooldowns.delete(id);
        else this.cooldowns.set(id, left - 1);
      }
    }
  }

  private consumeStun(side: Side, events: BattleEvent[]): boolean {
    const list = this.effects[side];
    const i = list.findIndex((s) => s.def.stun);
    if (i < 0) return false;
    list.splice(i, 1);
    events.push({ kind: 'stunned', side });
    return true;
  }

  private checkOver(events: BattleEvent[]): boolean {
    if (this.enemy.hp <= 0) {
      events.push({ kind: 'defeated', side: 'enemy' });
      this.state = 'victory';
      this.rewards = this.rollRewards();
      return true;
    }
    if (this.player.isDead) {
      events.push({ kind: 'defeated', side: 'player' });
      this.state = 'defeat';
      return true;
    }
    return false;
  }

  // --- Statuses ---

  private pushApply(events: BattleEvent[], source: Side, a: Apply, element: Element | undefined, type: DamageType): void {
    if (!chance(this.rng, a.chance ?? 1)) return;
    const target: Side = a.self ? source : other(source);
    const def = db.status(a.status);
    if (target === 'enemy' && this.enemy.def.immune?.includes(def.id)) {
      events.push({ kind: 'immune', target, status: def.id });
      return;
    }
    const tick = def.dot ? Math.max(1, Math.round(this.baseHit(source, type) * def.dot.power)) : 0;
    const dotElement = def.dot?.element === 'weapon' ? element : def.dot?.element;
    const fresh = target === source;
    const existing = this.effects[target].find((s) => s.def.id === def.id);
    if (existing) {
      existing.turns = Math.max(existing.turns, a.turns);
      existing.tick = Math.max(existing.tick, tick);
      existing.fresh = existing.fresh || fresh;
    } else {
      this.effects[target].push({ def, turns: a.turns, fresh, tick, element: dotElement });
    }
    events.push({ kind: 'status', target, status: def.id, turns: a.turns });
  }

  private mod(side: Side, key: ModKey): number {
    let total = 0;
    for (const s of this.effects[side]) total += s.def[key] ?? 0;
    return total;
  }

  // --- Damage math ---

  private statsOf(side: Side): Stats {
    return side === 'player' ? this.player.stats : this.enemy.def.stats;
  }

  private rollOf(side: Side): [number, number] {
    return side === 'player' ? this.player.weaponDamage : this.enemy.def.damage;
  }

  private defenseOf(side: Side, type: DamageType): number {
    return side === 'player' ? this.player.defenseFor(type) : (this.enemy.def.defense[type] ?? 0);
  }

  private hpOf(side: Side): number {
    return side === 'player' ? this.player.hp : this.enemy.hp;
  }

  private maxHpOf(side: Side): number {
    return side === 'player' ? this.player.maxHp : this.enemy.maxHp;
  }

  private damage(side: Side, amount: number): void {
    if (side === 'player') this.player.takeDamage(amount);
    else this.enemy.hp = Math.max(0, this.enemy.hp - amount);
  }

  private healSide(side: Side, amount: number): number {
    if (side === 'player') return this.player.heal(amount);
    const before = this.enemy.hp;
    this.enemy.hp = Math.min(this.enemy.maxHp, this.enemy.hp + amount);
    return this.enemy.hp - before;
  }

  /** An average non-crit hit before defenses. Damage over time is a share of this. */
  private baseHit(side: Side, type: DamageType): number {
    const [min, max] = this.rollOf(side);
    return (min + max) / 2 + Math.floor(statFor(this.statsOf(side), type) * BALANCE.statDamageScale);
  }

  private elementMultiplier(target: Side, element: Element | undefined): { mult: number; effect?: Effect } {
    if (!element) return { mult: 1 };
    const r = this.resistOf(target, element);
    const effect: Effect | undefined = r >= 100 ? 'immune' : r >= 25 ? 'resisted' : r <= -25 ? 'weak' : undefined;
    return { mult: 1 - r / 100, effect };
  }

  /** One attack from `source` at the other side. Applies the damage. */
  private strike(
    source: Side,
    move: string,
    moveId: string,
    damageType: DamageType,
    element: Element | undefined,
    power: number,
    critBonus = 0,
  ): Extract<BattleEvent, { kind: 'hit' | 'miss' }> {
    const target = other(source);
    const atk = this.statsOf(source);
    const def = this.statsOf(target);
    const base = { source, move, moveId, damageType, element };

    const rolled = clamp(BALANCE.baseHitChance + (atk.dex - def.dex) * 0.01, BALANCE.minHitChance, BALANCE.maxHitChance);
    // Gear: the player's weapon adds hit and crit, their armor adds evasion.
    const gearHit = source === 'player' ? this.player.hitBonus : -this.player.evasion;
    const hitChance = clamp(rolled + gearHit + this.mod(source, 'hitChance') - this.mod(target, 'evasion'), 0.05, BALANCE.maxHitChance);
    if (!chance(this.rng, hitChance)) return { kind: 'miss', ...base };

    const [min, max] = this.rollOf(source);
    let damage = randInt(this.rng, min, max) + Math.floor(statFor(atk, damageType) * BALANCE.statDamageScale);
    damage *= power;

    const gearCrit = source === 'player' ? this.player.critBonus : 0;
    const critChance = BALANCE.baseCritChance + atk.dex * BALANCE.critPerDex + this.mod(source, 'critChance') + critBonus + gearCrit;
    const crit = chance(this.rng, critChance);
    if (crit) damage *= BALANCE.critMultiplier;

    damage = mitigate(damage, this.defenseOf(target, damageType));
    const { mult, effect } = this.elementMultiplier(target, element);
    damage *= mult * Math.max(0, 1 + this.mod(source, 'damageDealt')) * Math.max(0, 1 + this.mod(target, 'damageTaken'));
    if (target === 'player' && this.defending) damage *= BALANCE.defendDamage;

    const final = mult > 0 ? Math.max(1, Math.round(damage)) : 0;
    this.damage(target, final);
    return { kind: 'hit', ...base, damage: final, crit, effect };
  }

  private rollRewards(): Rewards {
    const m = this.enemy.def;
    const rewards: Rewards = { xp: m.xp, gold: randInt(this.rng, m.gold[0], m.gold[1]), items: [], gear: [] };
    for (const d of m.drops) {
      if (!chance(this.rng, d.chance)) continue;
      for (let i = 0; i < (d.qty ?? 1); i++) {
        if (isGear(db.item(d.itemId))) rewards.gear.push(rollGear(d.itemId, 'drop', this.rng));
        else rewards.items.push(d.itemId);
      }
    }
    return rewards;
  }
}

function other(side: Side): Side {
  return side === 'player' ? 'enemy' : 'player';
}
