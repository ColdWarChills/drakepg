/**
 * Headless balance check: plays thousands of battles with a simple AI and prints win rates.
 * Run with `npm run sim` (optionally `npm run sim -- 2000` for more battles per matchup).
 */
import { db, validateData } from '../src/data/db';
import { ELEMENTS } from '../src/data/elements';
import type { ClassId, SkillDef, StatKey } from '../src/data/types';
import { Battle } from '../src/game/Battle';
import { Player, xpToNext } from '../src/game/Player';
import { seeded, type Rng } from '../src/game/rng';

const RUNS = Number(process.argv[2] ?? 400);

/** Where free stat points go, repeating. */
const STAT_PLAN: Record<ClassId, StatKey[]> = {
  warrior: ['str', 'str', 'end'],
  mage: ['int', 'int', 'end'],
  rogue: ['dex', 'dex', 'str'],
};

interface Loadout {
  label: string;
  weapon: Record<ClassId, string>;
  armor: Record<ClassId, string>;
}

const STARTER: Loadout = {
  label: 'starter',
  weapon: { warrior: 'training_sword', mage: 'worn_staff', rogue: 'rusty_dagger' },
  armor: { warrior: 'travel_garb', mage: 'novice_robe', rogue: 'travel_garb' },
};
const SHOP: Loadout = {
  label: 'shop',
  weapon: { warrior: 'iron_sword', mage: 'oak_staff', rogue: 'hunting_knife' },
  armor: { warrior: 'leather_armor', mage: 'apprentice_robe', rogue: 'leather_armor' },
};
const FIRE: Loadout = {
  label: 'fire',
  weapon: { warrior: 'emberbrand', mage: 'ember_wand', rogue: 'cinder_fang' },
  armor: SHOP.armor,
};

function makePlayer(classId: ClassId, level: number, gear: Loadout, potions: number): Player {
  const p = Player.createNew('Sim', classId);
  while (p.level < level) p.addXp(xpToNext(p.level));
  const plan = STAT_PLAN[classId];
  for (let i = 0; p.data.statPoints > 0; i++) p.spendStatPoint(plan[i % plan.length]);
  p.data.inventory = [];
  for (const id of [gear.weapon[classId], gear.armor[classId]]) {
    p.addItem(id);
    p.equip(id);
  }
  if (potions > 0) p.addItem('health_potion', potions);
  p.restoreFull();
  return p;
}

/** Rough value of a damage skill against this enemy right now. */
function skillValue(b: Battle, s: SkillDef): number {
  const p = b.player;
  const element = !s.element || s.element === 'weapon' ? p.weaponElement : s.element;
  const mult = element ? 1 - b.resistOf('enemy', element) / 100 : 1;
  return s.power * (s.hits ?? 1) * mult;
}

const BUFFS = new Set(['war_cry', 'iron_skin', 'stone_skin', 'smoke_bomb']);

/** Pick and perform one action. */
function act(b: Battle): void {
  const p = b.player;
  const skills = p.knownSkills.filter((s) => b.canUseSkill(s.id));
  const heal = skills.find((s) => s.kind === 'heal');
  const hpPct = p.hp / p.maxHp;
  const enemyHasStatus = (id: string) => b.statuses('enemy').some((s) => s.def.id === id);
  const selfHasStatus = (id: string) => b.statuses('player').some((s) => s.def.id === id);

  if (b.charging) {
    if (heal && hpPct < 0.6) return void b.useSkill(heal.id);
    return void b.defend();
  }
  if (hpPct < 0.35) {
    if (heal) return void b.useSkill(heal.id);
    if (b.canUseItem('health_potion')) return void b.useItem('health_potion');
  }
  const enemyHpPct = b.enemy.hp / b.enemy.maxHp;
  const longFight = b.enemy.maxHp > p.maxHp;
  for (const s of skills) {
    if (s.kind === 'buff' && longFight && enemyHpPct > 0.4 && BUFFS.has(s.id) && !selfHasStatus(s.applies![0].status)) return void b.useSkill(s.id);
  }
  // Keep enough MP for one heal.
  const reserve = heal ? 0 : (p.knownSkills.find((s) => s.kind === 'heal')?.mpCost ?? 0);
  let best: SkillDef | null = null;
  let bestValue = 1.05;
  for (const s of skills) {
    if (s.kind !== 'damage' || p.mp - s.mpCost < reserve) continue;
    let value = skillValue(b, s);
    const debuff = s.applies?.find((a) => !a.self);
    if (debuff && !enemyHasStatus(debuff.status) && enemyHpPct > 0.4) value += 0.8;
    if (value > bestValue) {
      best = s;
      bestValue = value;
    }
  }
  if (best) return void b.useSkill(best.id);
  b.attack();
}

interface Result {
  wins: number;
  turns: number;
  hpLeft: number;
  potions: number;
}

function fight(classId: ClassId, level: number, monsterId: string, gear: Loadout, potions: number, rng: Rng): Result {
  const res: Result = { wins: 0, turns: 0, hpLeft: 0, potions: 0 };
  for (let i = 0; i < RUNS; i++) {
    const p = makePlayer(classId, level, gear, potions);
    const b = new Battle(p, db.monster(monsterId), rng);
    let turns = 0;
    while (!b.isOver && turns < 60) {
      act(b);
      turns++;
    }
    if (b.state === 'victory') {
      res.wins++;
      res.hpLeft += p.hp / p.maxHp;
    }
    res.turns += turns;
    res.potions += potions - p.countItem('health_potion');
  }
  return res;
}

function pct(n: number): string {
  return `${Math.round(n * 100)}%`.padStart(5);
}

function table(title: string, rows: { monster: string; level: number; gear: Loadout; potions: number }[]): void {
  const rng = seeded(12345);
  console.log(`\n${title}`);
  console.log('monster            lv gear     | ' + ['warrior', 'mage', 'rogue'].map((c) => c.padEnd(26)).join(' | '));
  for (const row of rows) {
    const cells = (['warrior', 'mage', 'rogue'] as ClassId[]).map((c) => {
      const r = fight(c, row.level, row.monster, row.gear, row.potions, rng);
      const hp = r.wins ? r.hpLeft / r.wins : 0;
      return `win${pct(r.wins / RUNS)} hp${pct(hp)} t${(r.turns / RUNS).toFixed(1).padStart(4)} pot${(r.potions / RUNS).toFixed(1)}`;
    });
    console.log(`${row.monster.padEnd(18)} ${String(row.level).padStart(2)} ${row.gear.label.padEnd(8)} | ${cells.join(' | ')}`);
  }
}

validateData();
console.log(`${RUNS} battles per cell. win = win rate, hp = HP left after a win, t = turns, pot = potions used.`);
console.log(`Elements: ${ELEMENTS.join(', ')}`);

table('Chapter 1 trash (full HP each fight, 1 potion)', [
  { monster: 'mossling', level: 1, gear: STARTER, potions: 1 },
  { monster: 'thornback_boar', level: 1, gear: STARTER, potions: 1 },
  { monster: 'thornback_boar', level: 2, gear: STARTER, potions: 1 },
  { monster: 'ash_imp', level: 2, gear: STARTER, potions: 1 },
  { monster: 'bramble_bandit', level: 3, gear: STARTER, potions: 1 },
  { monster: 'bramble_bandit', level: 3, gear: SHOP, potions: 1 },
]);

table('Thornheart (boss, 3 potions)', [
  { monster: 'thornheart', level: 4, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 5, gear: STARTER, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 5, gear: FIRE, potions: 3 },
  { monster: 'thornheart', level: 6, gear: SHOP, potions: 3 },
]);
