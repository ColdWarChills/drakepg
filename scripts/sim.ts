/**
 * Headless balance check: plays thousands of battles with a simple AI and prints win rates.
 * Each cell also runs a random-button bot ("rnd"): if it does nearly as well as the smart AI, choices don't matter there.
 * Run with `npm run sim` (optionally `npm run sim -- 2000` for more battles per matchup).
 */
import { BALANCE } from '../src/config';
import { db, validateData } from '../src/data/db';
import { ELEMENTS } from '../src/data/elements';
import type { ClassId, SkillDef, StatKey } from '../src/data/types';
import { Battle } from '../src/game/Battle';
import { makeElite } from '../src/game/Elites';
import { makeHeroic } from '../src/game/Heroic';
import { makeImpling } from '../src/game/Lucky';
import { LOOT_TIERS, rollLootTier, tierOdds, type LootSource } from '../src/game/Loot';
import { LOOT_TABLES } from '../src/data/loot';
import { rollGear, type GearDef, type SigilTier } from '../src/game/Items';
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
  /** Roll quality of every line, 0-100. Shop gear is 0. */
  quality?: number;
  /** Sigil on every piece (no mods: this measures the base boost). */
  sigil?: SigilTier;
  /** Worn instead of the body armor's shop helm/gloves/boots (`KITS`): set pieces, rings, necklaces. */
  kit?: Record<ClassId, string[]>;
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
/** Best possible rolls: the top of the power range drops can give. */
const SHOP_100: Loadout = { ...SHOP, label: 'shop100', quality: 100 };
const FIRE_100: Loadout = { ...FIRE, label: 'fire100', quality: 100 };
/** Sigil gear at a middling roll, and the very best a drop can be. */
const SHOP_DAWN: Loadout = { ...SHOP, label: 'dawn50', quality: 50, sigil: 'dawn' };
const SHOP_ECLIPSE: Loadout = { ...SHOP, label: 'ecl50', quality: 50, sigil: 'eclipse' };
const SHOP_ECLIPSE_100: Loadout = { ...SHOP, label: 'ecl100', quality: 100, sigil: 'eclipse' };
/** What a player likely carries into Chapter 2: the Thornheart's weapon (Earth, neutral in the fen). */
const HEART: Loadout = {
  label: 'heart',
  weapon: { warrior: 'thornheart_blade', mage: 'heartwood_staff', rogue: 'briar_fang' },
  armor: SHOP.armor,
};
/** Reedwatch shop gear (tier 2), and the fen drops of each element. */
const SHOP2: Loadout = {
  label: 'shop2',
  weapon: { warrior: 'steel_broadsword', mage: 'yew_staff', rogue: 'fenwatch_stiletto' },
  armor: { warrior: 'scale_mail', mage: 'reedweave_robe', rogue: 'scale_mail' },
};
const WATER2: Loadout = { label: 'water2', weapon: { warrior: 'tidebreaker', mage: 'tidecaller_rod', rogue: 'brinefang' }, armor: SHOP2.armor };
const ENERGY2: Loadout = { label: 'energy2', weapon: { warrior: 'stormcleaver', mage: 'sparkglass_wand', rogue: 'sparkfang' }, armor: SHOP2.armor };
const SHOP2_100: Loadout = { ...SHOP2, label: 'shop2_100', quality: 100 };
const GREATER = 'greater_health_potion';
/** What a player likely carries into Chapter 3: the Stormcoil's quest weapon (tier 3 Energy). */
const STORM: Loadout = {
  label: 'storm',
  weapon: { warrior: 'thunderhead_maul', mage: 'stormcoil_scepter', rogue: 'coilfang' },
  armor: SHOP2.armor,
};
/** Hearthold shop gear (tier 3), and the Chapter 3 drops of each element. */
const SHOP3: Loadout = {
  label: 'shop3',
  weapon: { warrior: 'tempered_longsword', mage: 'larchwood_staff', rogue: 'ridgerunner_kris' },
  armor: { warrior: 'ridgeplate', mage: 'fleecewool_robe', rogue: 'ridgeplate' },
};
const ICE3: Loadout = { label: 'ice3', weapon: { warrior: 'frostbite', mage: 'hoarfrost_staff', rogue: 'icicle_kris' }, armor: SHOP3.armor };
const FIRE3: Loadout = { label: 'fire3', weapon: { warrior: 'hearthfire_blade', mage: 'kindling_rod', rogue: 'salamander_kris' }, armor: SHOP3.armor };
const EARTH3: Loadout = { ...HEART, label: 'heart3', armor: SHOP3.armor };
const SUPERIOR = 'superior_health_potion';

/** Helm, gloves and boots worn with each shop body armor (the same tier, heavy or light). Starter armor has none. */
const KITS: Record<string, string[]> = {
  leather_armor: ['leather_cap', 'leather_gloves', 'leather_boots'],
  apprentice_robe: ['apprentice_hood', 'apprentice_wraps', 'apprentice_slippers'],
  scale_mail: ['scale_coif', 'scale_gauntlets', 'scale_boots'],
  reedweave_robe: ['reedweave_hood', 'reedweave_wraps', 'reedweave_slippers'],
  ridgeplate: ['ridgeplate_helm', 'ridgeplate_gauntlets', 'ridgeplate_boots'],
  fleecewool_robe: ['fleecewool_hood', 'fleecewool_mittens', 'fleecewool_boots'],
};

function makePlayer(classId: ClassId, level: number, gear: Loadout, potions: number, potion: string): Player {
  const p = Player.createNew('Sim', classId);
  while (p.level < level) p.addXp(xpToNext(p.level));
  // Carry the default pick of skills (the class heal and the newest others).
  p.data.loadout = p.defaultLoadout();
  const plan = STAT_PLAN[classId];
  for (let i = 0; p.data.statPoints > 0; i++) p.spendStatPoint(plan[i % plan.length]);
  p.data.inventory = [];
  for (const id of [gear.weapon[classId], gear.armor[classId], ...(gear.kit?.[classId] ?? KITS[gear.armor[classId]] ?? [])]) {
    const piece = p.makeGear(id, { rolls: rollGear(db.item(id) as GearDef, undefined, gear.quality ?? 0, gear.sigil), sigil: gear.sigil });
    p.data.gear.push(piece);
    p.equip(piece.uid);
  }
  p.data.gear = [];
  if (potions > 0) p.addItem(potion, potions);
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
  const skills = p.carriedSkills.filter((s) => b.canUseSkill(s.id));
  const heal = skills.find((s) => s.kind === 'heal');
  const hpPct = p.hp / p.maxHp;
  const enemyHasStatus = (id: string) => b.statuses('enemy').some((s) => s.def.id === id);
  const selfHasStatus = (id: string) => b.statuses('player').some((s) => s.def.id === id);

  // The enemy's next move is shown: brace for a wound-up blow.
  if (b.charging) {
    if (heal && hpPct < 0.6) return void b.useSkill(heal.id);
    return void b.defend();
  }
  if (hpPct < 0.35) {
    if (heal) return void b.useSkill(heal.id);
    const potion = b.player.data.inventory[0]?.itemId;
    if (potion && b.canUseItem(potion)) return void b.useItem(potion);
  }
  const enemyHpPct = b.enemy.hp / b.enemy.maxHp;
  const longFight = b.enemy.maxHp > p.maxHp;
  for (const s of skills) {
    if (s.kind === 'buff' && longFight && enemyHpPct > 0.4 && BUFFS.has(s.id) && !selfHasStatus(s.applies![0].status)) return void b.useSkill(s.id);
  }
  // Keep enough MP for one heal.
  const reserve = heal ? 0 : (p.carriedSkills.find((s) => s.kind === 'heal')?.mpCost ?? 0);
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

/** A player mashing buttons: any ready action at random, but drinks a potion when low. */
function actRandom(b: Battle, rng: Rng): void {
  const p = b.player;
  if (p.hp / p.maxHp < 0.35) {
    const potion = p.data.inventory[0]?.itemId;
    if (potion && b.canUseItem(potion)) return void b.useItem(potion);
  }
  const skills = p.carriedSkills.filter((s) => b.canUseSkill(s.id));
  const options = [() => b.attack(), () => b.defend(), ...skills.map((s) => () => b.useSkill(s.id))];
  options[Math.floor(rng.next() * options.length)]();
}

interface Result {
  wins: number;
  turns: number;
  hpLeft: number;
  potions: number;
}

function fight(row: Row, classId: ClassId, rng: Rng, random = false): Result {
  const { level, monster: monsterId, elite, gear, potions } = row;
  const potion = row.potion ?? 'health_potion';
  const res: Result = { wins: 0, turns: 0, hpLeft: 0, potions: 0 };
  const base = row.zone ? makeImpling(db.zone(row.zone)) : db.monster(monsterId);
  const monster = elite ? makeElite(base, elite) : row.heroic ? makeHeroic(base) : base;
  for (let i = 0; i < RUNS; i++) {
    const p = makePlayer(classId, level, gear, potions, potion);
    if (row.losses) p.data.bossGrit[monster.id] = row.losses;
    const b = new Battle(p, monster, rng);
    let turns = 0;
    while (!b.isOver && turns < 60) {
      if (random) actRandom(b, rng);
      else act(b);
      turns++;
    }
    if (b.state === 'victory') {
      res.wins++;
      res.hpLeft += p.hp / p.maxHp;
    }
    res.turns += turns;
    res.potions += potions - p.countItem(potion);
  }
  return res;
}

function pct(n: number): string {
  return `${Math.round(n * 100)}%`.padStart(5);
}

interface Row {
  monster: string;
  /** Elite affix id: fight the Elite version. */
  elite?: string;
  level: number;
  gear: Loadout;
  potions: number;
  /** Default health_potion. */
  potion?: string;
  /** Earlier losses to this boss (boss grit). */
  losses?: number;
  /** Fight the Heroic rematch. */
  heroic?: boolean;
  /** Lucky Impling of this zone (`monster` is just the label). */
  zone?: string;
}

function table(title: string, rows: Row[]): void {
  const rng = seeded(12345);
  console.log(`\n${title}`);
  console.log(`${'monster'.padEnd(20)} lv gear     | ` + ['warrior', 'mage', 'rogue'].map((c) => c.padEnd(40)).join(' | '));
  for (const row of rows) {
    const cells = (['warrior', 'mage', 'rogue'] as ClassId[]).map((c) => {
      const r = fight(row, c, rng);
      const hp = r.wins ? r.hpLeft / r.wins : 0;
      const rnd = fight(row, c, rng, true);
      const rndHp = rnd.wins ? rnd.hpLeft / rnd.wins : 0;
      return `win${pct(r.wins / RUNS)} hp${pct(hp)} t${(r.turns / RUNS).toFixed(1).padStart(4)} pot${(r.potions / RUNS).toFixed(1)} rnd${pct(rnd.wins / RUNS)}${pct(rndHp)}`;
    });
    const name = row.zone ? `lucky@${row.zone}` : row.elite ? `${row.elite.slice(0, 4)}.${row.monster}` : row.losses ? `${row.monster} grit${row.losses}` : row.heroic ? `H.${row.monster}` : row.monster;
    console.log(`${name.padEnd(20)} ${String(row.level).padStart(2)} ${row.gear.label.padEnd(8)} | ${cells.join(' | ')}`);
  }
}

validateData();
console.log(`${RUNS} battles per cell. win = win rate, hp = HP left after a win, t = turns, pot = potions used, rnd = random-button bot's win rate and HP left.`);
console.log(`Elements: ${ELEMENTS.join(', ')}`);

/** Gear drop odds per kill: exact cascade math, checked against 100k simulated rolls. */
function lootReport(): void {
  console.log('\nGear loot per kill (cascade: rare, then uncommon, then common). sim = 100k rolls; "1 in" = kills per rare');
  console.log(`${'table'.padEnd(10)} ${'source'.padEnd(6)} | ${LOOT_TIERS.map((t) => t.padEnd(9)).join(' ')} none      | sim rare | 1 in`);
  const rng = seeded(777);
  for (const table of LOOT_TABLES) {
    for (const source of ['mob', 'elite', 'boss'] as LootSource[]) {
      const odds = tierOdds(table.id, source);
      let rare = 0;
      for (let i = 0; i < 100000; i++) if (rollLootTier(table.id, source, rng) === 'rare') rare++;
      const cells = [...LOOT_TIERS, 'none' as const].map((t) => `${(odds[t] * 100).toFixed(2).padStart(6)}%  `).join(' ');
      const oneIn = odds.rare > 0 ? Math.round(1 / odds.rare) : '-';
      console.log(`${table.id.padEnd(10)} ${source.padEnd(6)} | ${cells}| ${(rare / 1000).toFixed(2).padStart(6)}%| ${oneIn}`);
    }
  }

  // Bad-luck protection: normal kills until the first Rare, 20k players each.
  const streaks = (pity: boolean) => {
    const kills: number[] = [];
    for (let p = 0; p < 20000; p++) {
      let n = 1;
      while (rollLootTier('thornwood', 'mob', rng, pity ? n - 1 : 0) !== 'rare') n++;
      kills.push(n);
    }
    kills.sort((a, b) => a - b);
    const at = (q: number) => kills[Math.floor(q * (kills.length - 1))];
    return `average ${Math.round(kills.reduce((a, b) => a + b, 0) / kills.length)}, half by ${at(0.5)}, 90% by ${at(0.9)}, unluckiest 1% past ${at(0.99)}`;
  };
  console.log(`Normal kills to the first Rare, no protection:   ${streaks(false)}`);
  console.log(`Normal kills to the first Rare, with protection: ${streaks(true)}  (after ${BALANCE.loot.pityAfter} dry kills +${BALANCE.loot.pityStep * 100}% per kill, max +${BALANCE.loot.pityMax * 100}%)`);
}
lootReport();

table('Prologue road (should take about 2 turns and cost a few HP)', [
  { monster: 'burrow_rat', level: 1, gear: STARTER, potions: 0 },
  { monster: 'dew_slug', level: 1, gear: STARTER, potions: 0 },
  { monster: 'hedge_pixie', level: 1, gear: STARTER, potions: 0 },
]);

table('Chapter 1 trash (full HP each fight, 1 potion)', [
  { monster: 'mossling', level: 1, gear: STARTER, potions: 1 },
  { monster: 'thornback_boar', level: 1, gear: STARTER, potions: 1 },
  { monster: 'thornback_boar', level: 2, gear: STARTER, potions: 1 },
  { monster: 'bramble_bandit', level: 3, gear: STARTER, potions: 1 },
  { monster: 'bramble_bandit', level: 3, gear: SHOP, potions: 1 },
]);

table('Chapter 1 tough monsters (full HP, 1 potion): the counter should matter', [
  { monster: 'ash_imp', level: 1, gear: STARTER, potions: 1 },
  { monster: 'ash_imp', level: 3, gear: STARTER, potions: 1 },
  { monster: 'ash_imp', level: 3, gear: SHOP, potions: 1 },
  { monster: 'barkshell_beetle', level: 4, gear: SHOP, potions: 1 },
  { monster: 'barkshell_beetle', level: 4, gear: FIRE, potions: 1 },
  { monster: 'barkshell_beetle', level: 5, gear: SHOP, potions: 1 },
]);

table('Thornheart (boss, 3 potions)', [
  { monster: 'thornheart', level: 4, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 5, gear: STARTER, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 5, gear: FIRE, potions: 3 },
  { monster: 'thornheart', level: 6, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 4, gear: SHOP_100, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP_100, potions: 3 },
  { monster: 'thornheart', level: 5, gear: FIRE_100, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP_DAWN, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP_ECLIPSE, potions: 3 },
  { monster: 'thornheart', level: 5, gear: SHOP_ECLIPSE_100, potions: 3 },
]);

table('Boss grit: losses before this try (+5% damage each, max +25%), 3 potions', [
  { monster: 'thornheart', level: 4, gear: STARTER, potions: 3 },
  { monster: 'thornheart', level: 4, gear: STARTER, potions: 3, losses: 3 },
  { monster: 'thornheart', level: 4, gear: STARTER, potions: 3, losses: 5 },
  { monster: 'stormcoil', level: 9, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 9, gear: SHOP2, potions: 3, potion: GREATER, losses: 5 },
]);

table('Chapter 1 Elites (2 potions)', [
  { monster: 'mossling', elite: 'venomous', level: 2, gear: STARTER, potions: 2 },
  { monster: 'thornback_boar', elite: 'hulking', level: 3, gear: STARTER, potions: 2 },
  { monster: 'ash_imp', elite: 'frenzied', level: 3, gear: STARTER, potions: 2 },
  { monster: 'bramble_bandit', elite: 'hulking', level: 4, gear: SHOP, potions: 2 },
  { monster: 'bramble_bandit', elite: 'frenzied', level: 4, gear: SHOP, potions: 2 },
  { monster: 'bramble_bandit', elite: 'warded', level: 4, gear: SHOP, potions: 2 },
  { monster: 'bramble_bandit', elite: 'venomous', level: 4, gear: SHOP, potions: 2 },
  { monster: 'bramble_bandit', elite: 'ironhide', level: 4, gear: SHOP, potions: 2 },
]);

table('Chapter 2 trash (full HP each fight, 1 potion)', [
  { monster: 'mire_toad', level: 6, gear: HEART, potions: 1, potion: GREATER },
  { monster: 'mire_toad', level: 6, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'spark_wisp', level: 7, gear: HEART, potions: 1, potion: GREATER },
  { monster: 'spark_wisp', level: 7, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'spark_wisp', level: 7, gear: WATER2, potions: 1, potion: GREATER },
  { monster: 'bog_lurker', level: 7, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'bog_lurker', level: 7, gear: ENERGY2, potions: 1, potion: GREATER },
  { monster: 'sunken_revenant', level: 8, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'stormscale_drakeling', level: 9, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'stormscale_drakeling', level: 9, gear: WATER2, potions: 1, potion: GREATER },
  { monster: 'volt_eel', level: 8, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'volt_eel', level: 8, gear: ENERGY2, potions: 1, potion: GREATER },
  { monster: 'volt_eel', level: 8, gear: HEART, potions: 1, potion: GREATER },
]);

table('Chapter 2 Elites (2 potions)', [
  { monster: 'mire_toad', elite: 'hulking', level: 7, gear: SHOP2, potions: 2, potion: GREATER },
  { monster: 'spark_wisp', elite: 'warded', level: 8, gear: SHOP2, potions: 2, potion: GREATER },
  { monster: 'bog_lurker', elite: 'frenzied', level: 8, gear: SHOP2, potions: 2, potion: GREATER },
  { monster: 'stormscale_drakeling', elite: 'ironhide', level: 10, gear: SHOP2, potions: 2, potion: GREATER },
]);

table('Bogmother (mini-boss, 3 potions)', [
  { monster: 'bogmother', level: 7, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'bogmother', level: 8, gear: HEART, potions: 3, potion: GREATER },
  { monster: 'bogmother', level: 8, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'bogmother', level: 8, gear: ENERGY2, potions: 3, potion: GREATER },
  { monster: 'bogmother', level: 9, gear: SHOP2, potions: 3, potion: GREATER },
]);

table('Stormcoil (boss, changes Energy -> Water at half HP, 3 potions)', [
  { monster: 'stormcoil', level: 9, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: HEART, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: WATER2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: ENERGY2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 11, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: SHOP2_100, potions: 3, potion: GREATER },
]);

table('Chapter 3 trash (full HP each fight, 1 potion)', [
  { monster: 'drift_wolf', level: 10, gear: STORM, potions: 1, potion: GREATER },
  { monster: 'drift_wolf', level: 10, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'crag_ram', level: 10, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'gust_sprite', level: 11, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'gust_sprite', level: 11, gear: EARTH3, potions: 1, potion: SUPERIOR },
  { monster: 'rime_acolyte', level: 12, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'rime_acolyte', level: 12, gear: FIRE3, potions: 1, potion: SUPERIOR },
  { monster: 'ember_salamander', level: 12, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'ember_salamander', level: 12, gear: ICE3, potions: 1, potion: SUPERIOR },
]);

table('Chapter 3 tough monsters (full HP, 1 potion): the counter should matter', [
  { monster: 'glacier_golem', level: 12, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'glacier_golem', level: 12, gear: FIRE3, potions: 1, potion: SUPERIOR },
  { monster: 'glacier_golem', level: 12, gear: STORM, potions: 1, potion: SUPERIOR },
  { monster: 'vent_wyrmling', level: 13, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'vent_wyrmling', level: 13, gear: ICE3, potions: 1, potion: SUPERIOR },
]);

table('Old Snowmaw (mini-boss, Frost Shell: 3 Fire hits, grows back at 50%, 3 potions)', [
  { monster: 'old_snowmaw', level: 12, gear: SHOP3, potions: 3, potion: SUPERIOR },
  { monster: 'old_snowmaw', level: 12, gear: STORM, potions: 3, potion: SUPERIOR },
  { monster: 'old_snowmaw', level: 12, gear: FIRE3, potions: 3, potion: SUPERIOR },
  { monster: 'old_snowmaw', level: 13, gear: SHOP3, potions: 3, potion: SUPERIOR },
]);

table('The Frostbinder (boss, Ice -> Wind at half HP, 3 potions)', [
  { monster: 'frostbinder', level: 13, gear: SHOP3, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: SHOP3, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: FIRE3, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: EARTH3, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: STORM, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 15, gear: SHOP3, potions: 3, potion: SUPERIOR },
]);

table('Heroic rematches (3 potions, a few levels past the chapter)', [
  { monster: 'thornheart', level: 7, gear: FIRE, potions: 3, heroic: true },
  { monster: 'bogmother', level: 10, gear: SHOP2, potions: 3, potion: GREATER, heroic: true },
  { monster: 'stormcoil', level: 12, gear: STORM, potions: 3, potion: GREATER, heroic: true },
  { monster: 'old_snowmaw', level: 14, gear: FIRE3, potions: 3, potion: SUPERIOR, heroic: true },
  { monster: 'frostbinder', level: 16, gear: FIRE3, potions: 3, potion: SUPERIOR, heroic: true },
]);

table('Lucky Impling (sized to its area, runs away on its 3rd turn): win = caught', [
  { monster: 'lucky', zone: 'thornwood', level: 1, gear: STARTER, potions: 0 },
  { monster: 'lucky', zone: 'thornwood', level: 3, gear: STARTER, potions: 0 },
  { monster: 'lucky', zone: 'thornwood_deep', level: 4, gear: SHOP, potions: 0 },
  { monster: 'lucky', zone: 'fen', level: 6, gear: HEART, potions: 0 },
  { monster: 'lucky', zone: 'causeway', level: 8, gear: SHOP2, potions: 0 },
  { monster: 'lucky', zone: 'frostfang', level: 10, gear: SHOP3, potions: 0 },
  { monster: 'lucky', zone: 'windscour', level: 12, gear: SHOP3, potions: 0 },
  { monster: 'lucky', zone: 'steamvent', level: 12, gear: SHOP3, potions: 0 },
]);

/** A full set plus the area's class ring (and necklace where the set has none). */
const setKit = (set: string[], rings: Record<ClassId, string>, necklace?: string): Record<ClassId, string[]> => ({
  warrior: [...set, rings.warrior, ...(necklace ? [necklace] : [])],
  mage: [...set, rings.mage, ...(necklace ? [necklace] : [])],
  rogue: [...set, rings.rogue, ...(necklace ? [necklace] : [])],
});
const THORN_SET: Loadout = {
  ...SHOP,
  label: 'thornset',
  kit: setKit(['thornwarden_helm', 'thornwarden_gloves', 'thornwarden_boots', 'thornwarden_band'], { warrior: 'copper_band', mage: 'moonstone_ring', rogue: 'fox_ring' }, 'oak_pendant'),
};
const STORM_SET: Loadout = {
  ...SHOP2,
  label: 'stormset',
  kit: setKit(['stormwatch_helm', 'stormwatch_gloves', 'stormwatch_boots', 'stormwatch_amulet'], { warrior: 'silver_band', mage: 'tidepearl_ring', rogue: 'eelskin_ring' }),
};
const FROST_SET: Loadout = {
  ...SHOP3,
  label: 'frostset',
  kit: setKit(['frostward_helm', 'frostward_gloves', 'frostward_boots', 'frostward_ring'], { warrior: 'gold_band', mage: 'rimeglass_ring', rogue: 'wolfclaw_ring' }, 'hearthstone_pendant'),
};

table('Sets and jewelry: shop weapon and body, the area set (4 pieces) and jewelry, 0% rolls (3 potions for bosses)', [
  { monster: 'bramble_bandit', level: 3, gear: SHOP, potions: 1 },
  { monster: 'bramble_bandit', level: 3, gear: THORN_SET, potions: 1 },
  { monster: 'thornheart', level: 5, gear: SHOP, potions: 3 },
  { monster: 'thornheart', level: 5, gear: THORN_SET, potions: 3 },
  { monster: 'sunken_revenant', level: 8, gear: SHOP2, potions: 1, potion: GREATER },
  { monster: 'sunken_revenant', level: 8, gear: STORM_SET, potions: 1, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: SHOP2, potions: 3, potion: GREATER },
  { monster: 'stormcoil', level: 10, gear: STORM_SET, potions: 3, potion: GREATER },
  { monster: 'rime_acolyte', level: 12, gear: SHOP3, potions: 1, potion: SUPERIOR },
  { monster: 'rime_acolyte', level: 12, gear: FROST_SET, potions: 1, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: SHOP3, potions: 3, potion: SUPERIOR },
  { monster: 'frostbinder', level: 14, gear: FROST_SET, potions: 3, potion: SUPERIOR },
]);
