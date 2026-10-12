import { BALANCE } from '../config';
import { db } from '../data/db';
import type { DamageType, MonsterAttack, MonsterDef, Stats } from '../data/types';
import type { Player, Rewards } from './Player';
import { chance, clamp, defaultRng, randInt, weightedPick, type Rng } from './rng';

export type Side = 'player' | 'enemy';

/**
 * What happened during a turn, in order. The battle scene plays these back as animations,
 * so the rules here never touch Phaser.
 */
/** `moveId` is 'attack', a skill id, an item id, or 'enemy' for monster attacks. */
export type BattleEvent =
  | { kind: 'hit'; source: Side; move: string; moveId: string; damageType: DamageType; damage: number; crit: boolean }
  | { kind: 'miss'; source: Side; move: string; moveId: string; damageType: DamageType }
  | { kind: 'heal'; source: Side; move: string; moveId: string; hp: number; mp: number }
  | { kind: 'flee'; success: boolean }
  | { kind: 'defeated'; side: Side };

export type BattleState = 'player_turn' | 'victory' | 'defeat' | 'fled';

export interface Enemy {
  def: MonsterDef;
  hp: number;
  maxHp: number;
}

interface AttackerInfo {
  stats: Stats;
  roll: [number, number];
}

/** Share of damage removed by `armor`. 10 armor = 25%, 30 armor = 50%. */
function mitigate(damage: number, armor: number): number {
  return damage * (1 - armor / (armor + 30));
}

export class Battle {
  readonly enemy: Enemy;
  state: BattleState = 'player_turn';
  rewards: Rewards | null = null;

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

  // --- Player actions. Each returns the player's events plus the enemy's reply. ---

  attack(): BattleEvent[] {
    if (this.isOver) return [];
    const p = this.player;
    const events = [this.strikeEnemy('Attack', 'attack', p.weaponDamageType, 1)];
    return this.endPlayerTurn(events);
  }

  canUseSkill(skillId: string): boolean {
    return !this.isOver && this.player.mp >= db.skill(skillId).mpCost;
  }

  useSkill(skillId: string): BattleEvent[] {
    if (!this.canUseSkill(skillId)) return [];
    const skill = db.skill(skillId);
    this.player.spendMp(skill.mpCost);
    const events: BattleEvent[] = [];
    if (skill.kind === 'heal') {
      const amount = Math.round(this.player.maxHp * skill.power + this.player.stats.int);
      events.push({ kind: 'heal', source: 'player', move: skill.name, moveId: skill.id, hp: this.player.heal(amount), mp: 0 });
    } else {
      const hits = skill.hits ?? 1;
      for (let i = 0; i < hits && this.enemy.hp > 0; i++) {
        events.push(this.strikeEnemy(skill.name, skill.id, skill.damageType ?? 'physical', skill.power));
      }
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

  get fleeChance(): number {
    const diff = this.player.stats.dex - this.enemy.def.stats.dex;
    return clamp(BALANCE.baseFleeChance + diff * 0.03, 0.2, 0.9);
  }

  flee(): BattleEvent[] {
    if (this.isOver) return [];
    if (chance(this.rng, this.fleeChance)) {
      this.state = 'fled';
      return [{ kind: 'flee', success: true }];
    }
    return this.endPlayerTurn([{ kind: 'flee', success: false }]);
  }

  // --- Internals ---

  private endPlayerTurn(events: BattleEvent[]): BattleEvent[] {
    if (this.enemy.hp <= 0) {
      events.push({ kind: 'defeated', side: 'enemy' });
      this.state = 'victory';
      this.rewards = this.rollRewards();
      return events;
    }
    events.push(this.enemyTurn());
    if (this.player.isDead) {
      events.push({ kind: 'defeated', side: 'player' });
      this.state = 'defeat';
    }
    return events;
  }

  private enemyTurn(): BattleEvent {
    const attack: MonsterAttack = weightedPick(this.rng, this.enemy.def.attacks, (a) => a.weight);
    const m = this.enemy.def;
    const attacker: AttackerInfo = { stats: m.stats, roll: m.damage };
    const armor = attack.damageType === 'magic' ? this.player.resist : this.player.defense;
    const result = this.resolveHit(attacker, this.player.stats.dex, armor, attack.damageType, attack.power);
    const base = { source: 'enemy' as const, move: attack.name, moveId: 'enemy', damageType: attack.damageType };
    if (!result) return { kind: 'miss', ...base };
    this.player.takeDamage(result.damage);
    return { kind: 'hit', ...base, ...result };
  }

  private strikeEnemy(move: string, moveId: string, damageType: DamageType, power: number): BattleEvent {
    const p = this.player;
    const attacker: AttackerInfo = { stats: p.stats, roll: p.weaponDamage };
    const m = this.enemy.def;
    const armor = damageType === 'magic' ? m.resist : m.defense;
    const result = this.resolveHit(attacker, m.stats.dex, armor, damageType, power);
    if (!result) return { kind: 'miss', source: 'player', move, moveId, damageType };
    this.enemy.hp = Math.max(0, this.enemy.hp - result.damage);
    return { kind: 'hit', source: 'player', move, moveId, damageType, ...result };
  }

  /** Shared hit/crit/damage math for both sides. Returns null on a miss. */
  private resolveHit(
    attacker: AttackerInfo,
    defenderDex: number,
    armor: number,
    damageType: DamageType,
    power: number,
  ): { damage: number; crit: boolean } | null {
    const hitChance = clamp(
      BALANCE.baseHitChance + (attacker.stats.dex - defenderDex) * 0.01,
      BALANCE.minHitChance,
      BALANCE.maxHitChance,
    );
    if (!chance(this.rng, hitChance)) return null;

    const stat = damageType === 'magic' ? attacker.stats.int : attacker.stats.str;
    let damage = randInt(this.rng, attacker.roll[0], attacker.roll[1]) + Math.floor(stat * BALANCE.statDamageScale);
    damage *= power;

    const crit = chance(this.rng, BALANCE.baseCritChance + attacker.stats.dex * BALANCE.critPerDex);
    if (crit) damage *= BALANCE.critMultiplier;

    return { damage: Math.max(1, Math.round(mitigate(damage, armor))), crit };
  }

  private rollRewards(): Rewards {
    const m = this.enemy.def;
    return {
      xp: m.xp,
      gold: randInt(this.rng, m.gold[0], m.gold[1]),
      items: m.drops.filter((d) => chance(this.rng, d.chance)).map((d) => d.itemId),
    };
  }
}
