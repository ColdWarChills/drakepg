import { BALANCE } from '../config';
import { db } from '../data/db';
import type { Apply, DamageType, Element, MonsterAttack, MonsterDef, Resists, StatusDef, Stats } from '../data/types';
import { cheats } from './cheats';
import { grantFor } from './Items';
import { featureOn } from './Features';
import { rollLoot } from './Loot';
import { statFor, type Player, type Rewards } from './Player';
import { chance, clamp, defaultRng, randInt, weightedPick, type Rng } from './rng';

export type Side = 'player' | 'enemy';

/** How an element landed, for the "Weak!" / "Resisted" popups. 'absorb': over 100 resistance heals instead. */
export type Effect = 'weak' | 'resisted' | 'immune' | 'absorb';

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
      /** This hit cracked the enemy's shell: pips left (0 = broken). */
      shellLeft?: number;
      /** The target absorbed the element: `damage` is the HP it healed instead. */
      absorbed?: boolean;
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
  /** MP from Rage (a Warrior taking a hit). */
  | { kind: 'rage'; mp: number }
  /** A boss changes form (new element and resistances). */
  | { kind: 'phase'; text: string; element: Element }
  /** The enemy's shell grew back. */
  | { kind: 'shell'; left: number; text: string }
  /** The fight ran long: the enemy now deals `bonus` more damage (0.2 = +20%). */
  | { kind: 'enrage'; bonus: number }
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
  /** XP and gold multiplier (repeat boss fights give less). Set before the battle ends. */
  rewardScale = 1;
  /** Telegraphed attack the enemy is winding up; it lands on its next turn. */
  charging: MonsterAttack | null = null;
  /** The move the enemy will make on its next turn, shown on the battle panel. */
  intent: MonsterAttack;
  /** Player turns taken so far. */
  turn = 0;
  /** Moves the enemy has picked so far (for `opener` and `every`). */
  private picks = 0;
  /** `hpBelow` moves already used (once per fight). */
  private readonly usedOnce = new Set<string>();
  private enrageShown = 0;
  /** Pips left on the enemy's shell (0 = none or broken). */
  shell = 0;
  private shellReformed = false;
  private defending = false;
  private readonly cooldowns = new Map<string, number>();
  private readonly effects: Record<Side, ActiveStatus[]> = { player: [], enemy: [] };

  constructor(
    readonly player: Player,
    monster: MonsterDef,
    private readonly rng: Rng = defaultRng,
  ) {
    this.enemy = { def: monster, hp: monster.hp, maxHp: monster.hp };
    this.shell = monster.shell?.hits ?? 0;
    this.intent = this.pickIntent();
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
    // Monsters can go over 100: they absorb that element and heal from it.
    const max = side === 'player' ? BALANCE.maxPlayerResist : 200;
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
    if (hit.kind === 'hit' && proc && this.enemy.hp > 0 && chance(this.rng, p.procChance)) {
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
    return !this.isOver && this.player.mp >= db.skill(skillId).mpCost && !this.cooldowns.has(skillId);
  }

  useSkill(skillId: string): BattleEvent[] {
    if (!this.canUseSkill(skillId)) return [];
    const skill = db.skill(skillId);
    const p = this.player;
    p.spendMp(skill.mpCost);
    p.data.freshSkills = p.data.freshSkills.filter((id) => id !== skill.id);
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
    this.turn += 1;
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
      // A stun breaks a wind-up; the enemy picks something new.
      if (this.charging) {
        this.charging = null;
        this.intent = this.pickIntent();
      }
    } else {
      this.enemyAction(events);
    }
    this.endTurn('enemy', events);
  }

  private enemyAction(events: BattleEvent[]): void {
    const m = this.enemy.def;
    const bonus = this.enrage;
    if (bonus > this.enrageShown) {
      this.enrageShown = bonus;
      events.push({ kind: 'enrage', bonus });
    }
    let attack: MonsterAttack;
    if (this.charging) {
      attack = this.charging;
      this.charging = null;
    } else {
      attack = this.intent;
      if (attack.windup) {
        this.charging = attack;
        events.push({ kind: 'windup', move: attack.name, text: attack.windup });
        return;
      }
    }
    this.intent = this.pickIntent();
    const element = attack.element ?? m.element;
    if (attack.heal) {
      events.push({ kind: 'heal', source: 'enemy', move: attack.name, moveId: 'enemy', hp: this.healSide('enemy', Math.round(this.enemy.maxHp * attack.heal)), mp: 0 });
    } else if (attack.power === 0) {
      events.push({ kind: 'buff', source: 'enemy', move: attack.name, moveId: 'enemy' });
    } else {
      const hit = this.strike('enemy', attack.name, 'enemy', attack.damageType, element, attack.power);
      events.push(hit);
      if (hit.kind === 'miss') return;
      this.rage(hit.damage, events);
    }
    for (const a of attack.applies ?? []) this.pushApply(events, 'enemy', a, element, attack.damageType);
  }

  /**
   * The enemy's next move: its `opener` first, an `every` move on its turn, an `hpBelow` move once when hurt,
   * else a weighted pick from its other moves. A wind-up in progress stays the intent.
   */
  private pickIntent(count = true): MonsterAttack {
    if (this.charging) return this.charging;
    const attacks = this.enemy.def.attacks;
    const n = this.picks + 1;
    if (count) this.picks = n;
    const opener = n === 1 ? attacks.find((a) => a.opener) : undefined;
    if (opener) return opener;
    const timed = attacks.find((a) => a.every && n % a.every === 0);
    if (timed) return timed;
    const hurt = attacks.find((a) => a.hpBelow && !this.usedOnce.has(a.name) && this.enemy.hp <= this.enemy.maxHp * a.hpBelow);
    if (hurt) {
      this.usedOnce.add(hurt.name);
      return hurt;
    }
    const pool = attacks.filter((a) => !a.opener && !a.every && !a.hpBelow);
    return weightedPick(this.rng, pool.length ? pool : attacks, (a) => a.weight);
  }

  /** Damage multiplier the enemy has from a long fight (0 until the enrage turn). */
  get enrage(): number {
    return this.enrageAt(this.turn);
  }

  private enrageAt(turn: number): number {
    const e = BALANCE.enrage;
    const at = this.enemy.def.boss ? e.bossTurns : e.turns;
    return Math.max(0, turn - at + 1) * e.step;
  }

  /** Player turns left before the enemy enrages (0 = already enraged). */
  get turnsToEnrage(): number {
    const e = BALANCE.enrage;
    return Math.max(0, (this.enemy.def.boss ? e.bossTurns : e.turns) - this.turn);
  }

  /** Rough damage of the enemy's next move against the player (average roll, no crit, before Defend). Null if it doesn't hit. */
  intentDamage(): number | null {
    const a = this.intent;
    if (a.power <= 0 || a.heal) return null;
    const element = a.element ?? this.enemy.def.element;
    const raw = this.baseHit('enemy', a.damageType) * a.power;
    const { mult } = this.elementMultiplier('player', element);
    // It strikes after your next action, which may push the enrage up a step.
    const enraged = (1 + this.enrageAt(this.turn + 1)) / (1 + this.enrage);
    return Math.max(1, Math.round(this.scaleHit('enemy', 'player', mitigate(raw, this.defenseOf('player', a.damageType)), mult) * enraged));
  }

  /** Damage and healing over time. */
  private startTurn(side: Side, events: BattleEvent[]): void {
    for (const s of [...this.effects[side]]) {
      if (s.tick > 0) {
        const { mult, effect } = this.elementMultiplier(side, s.element);
        if (mult < 0) {
          const heal = this.healSide(side, Math.max(1, Math.round(s.tick * -mult)));
          events.push({ kind: 'tick', target: side, status: s.def.id, damage: 0, heal, element: s.element, effect });
        } else {
          const damage = mult > 0 && !(cheats.god && side === 'player') ? Math.max(1, Math.round(s.tick * mult)) : 0;
          this.damage(side, damage);
          events.push({ kind: 'tick', target: side, status: s.def.id, damage, heal: 0, element: s.element, effect });
        }
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
    this.checkPhase(events);
    const grit = this.player.data.bossGrit;
    const id = this.enemy.def.id;
    if (this.enemy.hp <= 0) {
      events.push({ kind: 'defeated', side: 'enemy' });
      this.state = 'victory';
      this.rewards = this.rollRewards();
      if (this.enemy.def.boss) delete grit[id];
      return true;
    }
    if (this.player.isDead) {
      events.push({ kind: 'defeated', side: 'player' });
      this.state = 'defeat';
      // Losing to a boss makes the next attempt a little easier.
      if (this.enemy.def.boss) grit[id] = (grit[id] ?? 0) + 1;
      return true;
    }
    return false;
  }

  /** Boss grit: extra damage against this boss from earlier losses (0 for other monsters). */
  get grit(): number {
    if (!this.enemy.def.boss) return 0;
    const g = BALANCE.grit;
    return Math.min(g.max, (this.player.data.bossGrit[this.enemy.def.id] ?? 0) * g.step);
  }

  /** Rage: a class with `mpOnHit` gains MP from every enemy hit that hurts. */
  private rage(damage: number, events: BattleEvent[]): void {
    const gain = this.player.classDef.mpOnHit ?? 0;
    if (gain <= 0 || damage <= 0 || this.player.isDead) return;
    const mp = this.player.restoreMp(gain);
    if (mp > 0) events.push({ kind: 'rage', mp });
  }

  /** A boss's second form kicks in once its HP falls to the threshold (only once, and not on the killing blow). */
  private checkPhase(events: BattleEvent[]): void {
    const { def, hp, maxHp } = this.enemy;
    const shell = def.shell;
    if (shell?.reformAt && !this.shellReformed && hp > 0 && hp <= maxHp * shell.reformAt) {
      this.shellReformed = true;
      this.shell = shell.hits;
      events.push({ kind: 'shell', left: shell.hits, text: `The ${shell.name} grows back over ${def.name.replace(/^The /, 'the ')}!` });
    }
    const phase = def.phase;
    if (!phase || hp <= 0 || hp > maxHp * phase.at) return;
    // A wind-up started in the old form still lands in the old element.
    if (this.charging) this.charging = { ...this.charging, element: this.charging.element ?? def.element };
    this.enemy.def = { ...def, element: phase.element, resists: phase.resists, attacks: phase.attacks ?? def.attacks, phase: undefined };
    this.intent = this.pickIntent(false);
    events.push({ kind: 'phase', text: phase.text, element: phase.element });
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
    if (side === 'player') return this.player.weaponDamage;
    const [min, max] = this.enemy.def.damage;
    const m = this.enemy.def.boss ? BALANCE.bossDamage : BALANCE.mobDamage;
    return [Math.round(min * m), Math.round(max * m)];
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
    const effect: Effect | undefined = r > 100 ? 'absorb' : r === 100 ? 'immune' : r >= 25 ? 'resisted' : r <= -25 ? 'weak' : undefined;
    return { mult: 1 - r / 100, effect };
  }

  /** Element, status modifiers, grit and enrage on a mitigated hit. */
  private scaleHit(source: Side, target: Side, damage: number, mult: number): number {
    damage *= mult * Math.max(0, 1 + this.mod(source, 'damageDealt')) * Math.max(0, 1 + this.mod(target, 'damageTaken'));
    if (source === 'player') damage *= 1 + this.grit;
    else damage *= 1 + this.enrage;
    return damage;
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
    const hitChance = clamp(rolled + this.mod(source, 'hitChance') - this.mod(target, 'evasion'), 0.05, BALANCE.maxHitChance);
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
    // Absorbed: the hit heals the target by the share over 100 instead.
    if (mult < 0) {
      const healed = this.healSide(target, Math.max(1, Math.round(this.scaleHit(source, target, damage, -mult))));
      return { kind: 'hit', ...base, damage: healed, crit, effect, absorbed: true };
    }
    damage = this.scaleHit(source, target, damage, mult);
    if (target === 'player' && this.defending) damage *= BALANCE.defendDamage;
    const shell = this.enemy.def.shell;
    const shielded = target === 'enemy' && shell !== undefined && this.shell > 0;
    if (shielded) damage *= 1 - shell.reduction;

    let final = mult > 0 ? Math.max(1, Math.round(damage)) : 0;
    if (cheats.god && target === 'player') final = 0;
    if (cheats.oneHit && target === 'enemy') final = Math.max(final, this.enemy.hp);
    this.damage(target, final);
    // A landed hit of the shell's element knocks off a pip.
    if (shielded && element === shell.element) {
      this.shell--;
      return { kind: 'hit', ...base, damage: final, crit, effect, shellLeft: this.shell };
    }
    return { kind: 'hit', ...base, damage: final, crit, effect };
  }

  private rollRewards(): Rewards {
    const m = this.enemy.def;
    const source = m.boss ? 'boss' : m.elite ? 'elite' : m.chestLoot ? 'chest' : 'mob';
    // Materials, Sigils and mods only drop once their chapter has unlocked them.
    const materials = featureOn(this.player, 'materials');
    const extras = featureOn(this.player, 'sigils');
    const items = m.drops
      .filter((d) => materials || db.item(d.itemId).type !== 'material')
      .filter((d) => chance(this.rng, d.chance))
      .flatMap((d) => Array.from({ length: d.qty ?? 1 }, () => grantFor(d.itemId, this.rng, source, extras)));
    // Gear: one cascading tier roll (Elites and bosses always drop something). Missing a Rare builds up luck.
    // Heroic bosses roll gear more than once.
    for (let i = 0; m.loot && i < (m.isHeroic ? BALANCE.heroic.lootRolls : 1); i++) {
      const pity = this.player.data.lootPity;
      const gear = rollLoot(m.loot, source, this.rng, pity[m.loot] ?? 0, extras);
      if (gear?.tier === 'rare') delete pity[m.loot];
      else pity[m.loot] = (pity[m.loot] ?? 0) + 1;
      if (gear) items.push(gear.grant);
    }
    return {
      xp: Math.round(m.xp * this.rewardScale),
      gold: Math.round(randInt(this.rng, m.gold[0], m.gold[1]) * this.rewardScale),
      items,
    };
  }
}

function other(side: Side): Side {
  return side === 'player' ? 'enemy' : 'player';
}
