/**
 * Golden results for the Unity port's parity tests (docs/UNITY_PORT.md, step 3): seeded fights, loot and Guild
 * boards played by the TypeScript rules, written to JSON. The C# port must reproduce every line exactly
 * (unity/Assets/Tests/EditMode/ParityTests.cs). Run `npm run golden` after changing rules or data, then the tests.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { db } from '../src/data/db';
import type { ClassId, MonsterDef } from '../src/data/types';
import { Battle, type BattleEvent } from '../src/game/Battle';
import { CHAPTER_STARTS, applyChapterStart } from '../src/game/cheats';
import { makeElite } from '../src/game/Elites';
import { refreshBoard, takeHunt } from '../src/game/Guild';
import { makeHeroic } from '../src/game/Heroic';
import { grantFor } from '../src/game/Items';
import { makeImpling } from '../src/game/Lucky';
import { rollChestLoot } from '../src/game/Chests';
import { hone, reinforce } from '../src/game/Forge';
import { Player } from '../src/game/Player';
import { seeded, type Rng } from '../src/game/rng';

const OUT = process.argv[2] ?? 'unity/Assets/Tests/EditMode/golden.json';
const SEEDS = 12;

function n(v: number): string {
  return String(v);
}

/** The same text as BattleEvent.ToString() in C#. */
export function eventText(e: BattleEvent): string {
  switch (e.kind) {
    case 'hit':
      return `hit ${e.source} ${e.moveId} ${n(e.damage)}${e.crit ? ' crit' : ''}${e.effect ? ` ${e.effect}` : ''}${e.absorbed ? ' absorbed' : ''}${e.shellLeft !== undefined ? ` shell${e.shellLeft}` : ''}`;
    case 'miss':
      return `miss ${e.source} ${e.moveId}`;
    case 'heal':
      return `heal ${e.source} ${e.moveId} ${n(e.hp)} ${n(e.mp)}`;
    case 'buff':
      return `buff ${e.source} ${e.moveId}`;
    case 'status':
      return `status ${e.target} ${e.status} ${e.turns}`;
    case 'immune':
      return `immune ${e.target} ${e.status}`;
    case 'tick':
      return `tick ${e.target} ${e.status} ${n(e.damage)} ${n(e.heal)}${e.effect ? ` ${e.effect}` : ''}`;
    case 'expire':
      return `expire ${e.target} ${e.status}`;
    case 'stunned':
      return `stunned ${e.side}`;
    case 'flee':
      return `flee ${e.success ? 'ok' : 'fail'}`;
    case 'defend':
      return `defend ${n(e.mp)}`;
    case 'swap':
      return `swap ${e.weapon}`;
    case 'windup':
      return `windup ${e.move}`;
    case 'rage':
      return `rage ${n(e.mp)}`;
    case 'phase':
      return `phase ${e.element}`;
    case 'shell':
      return `shell ${e.left}`;
    case 'enrage':
      return `enrage ${n(e.bonus)}`;
    case 'escape':
      return 'escape';
    case 'defeated':
      return `defeated ${e.side}`;
  }
}

/** A plain policy both sides can follow: potion when low, Defend against a wind-up, best ready damage skill, else Attack. */
function act(b: Battle, turn: number): { action: string; events: BattleEvent[] } {
  const p = b.player;
  const potion = ['superior_health_potion', 'greater_health_potion', 'health_potion'].find((id) => b.canUseItem(id));
  if (p.hp < p.maxHp * 0.35 && potion) return { action: `item ${potion}`, events: b.useItem(potion) };
  if (b.charging) return { action: 'defend', events: b.defend() };
  if (turn % 7 === 6 && b.canFlee && !b.enemy.def.boss && b.enemy.def.lucky) return { action: 'flee', events: b.flee() };
  const skills = p.carriedSkills.filter((s) => b.canUseSkill(s.id));
  const heal = skills.find((s) => s.kind === 'heal');
  if (heal && p.hp < p.maxHp * 0.5) return { action: `skill ${heal.id}`, events: b.useSkill(heal.id) };
  const dmg = skills.filter((s) => s.kind !== 'heal');
  if (dmg.length && turn % 3 !== 2) {
    const s = dmg[turn % dmg.length];
    return { action: `skill ${s.id}`, events: b.useSkill(s.id) };
  }
  return { action: 'attack', events: b.attack() };
}

function heroFor(cls: ClassId, chapter: number): Player {
  const p = Player.createNew('Golden', cls);
  const start = CHAPTER_STARTS.find((s) => s.chapter === chapter)!;
  if (chapter > 1) applyChapterStart(p, start);
  // Every system open, so Sigils, materials and Elites drop the way they do late in the game.
  for (const f of ['materials', 'sigils', 'elites', 'chests']) {
    const unlock = db.feature(f).unlockedBy;
    if (!p.data.quests[unlock]) p.data.quests[unlock] = { status: 'done', count: 0 };
  }
  return p;
}

interface FightCase {
  name: string;
  cls: ClassId;
  chapter: number;
  monster: () => MonsterDef;
  zoneId?: string;
}

const CH1 = ['burrow_rat', 'mossling', 'thornback_boar', 'ash_imp', 'bramble_bandit', 'barkshell_beetle', 'thornheart', 'millstone_crab', 'dusk_wraith', 'hedge_troll'];
const CH2 = ['mire_toad', 'spark_wisp', 'bog_lurker', 'sunken_revenant', 'stormscale_drakeling', 'bogmother', 'stormcoil'];
const CH3 = ['drift_wolf', 'crag_ram', 'gust_sprite', 'rime_acolyte', 'glacier_golem', 'ember_salamander', 'vent_wyrmling', 'old_snowmaw', 'frostbinder'];

const cases: FightCase[] = [];
for (const cls of ['warrior', 'mage', 'rogue'] as ClassId[]) {
  for (const id of CH1) cases.push({ name: id, cls, chapter: 1, monster: () => db.monster(id), zoneId: 'thornwood' });
  for (const id of CH2) cases.push({ name: id, cls, chapter: 2, monster: () => db.monster(id), zoneId: 'fen' });
  for (const id of CH3) cases.push({ name: id, cls, chapter: 3, monster: () => db.monster(id), zoneId: 'frostfang' });
  cases.push({ name: 'elite mire_toad', cls, chapter: 2, monster: () => makeElite(db.monster('mire_toad'), db.affixes()[1].id) });
  cases.push({ name: 'elite drift_wolf', cls, chapter: 3, monster: () => makeElite(db.monster('drift_wolf'), db.affixes()[3].id) });
  cases.push({ name: 'heroic thornheart', cls, chapter: 2, monster: () => makeHeroic(db.monster('thornheart')) });
  cases.push({ name: 'heroic stormcoil', cls, chapter: 3, monster: () => makeHeroic(db.monster('stormcoil')) });
  cases.push({ name: 'impling fen', cls, chapter: 2, monster: () => makeImpling(db.zone('fen')) });
}

const fights = [];
for (const c of cases) {
  for (let seed = 1; seed <= SEEDS; seed++) {
    const player = heroFor(c.cls, c.chapter);
    // A gather hunt in the fight's area, so hunt drops are covered too.
    const rng: Rng = seeded(seed * 7919 + c.name.length);
    const battle = new Battle(player, c.monster(), rng);
    if (c.zoneId) battle.zoneId = c.zoneId;
    const log: string[] = [`intent ${battle.intent.name}`];
    for (let turn = 0; !battle.isOver && turn < 80; turn++) {
      const { action, events } = act(battle, turn);
      log.push(`> ${action}`, ...events.map(eventText), `intent ${battle.intent.name} hp ${player.hp}/${battle.enemy.hp}`);
    }
    fights.push({
      name: c.name,
      cls: c.cls,
      chapter: c.chapter,
      seed,
      zoneId: c.zoneId ?? null,
      log,
      state: battle.state,
      rewards: battle.rewards,
      pity: player.data.lootPity,
    });
  }
}

// Loot without a fight: drops, chests, the smith, the Guild board.
const extras: Record<string, unknown> = {};
{
  const rng = seeded(424242);
  extras.grants = ['thornheart_blade', 'scale_mail', 'tidepearl_ring', 'stormwatch_amulet', 'health_potion'].flatMap((id) =>
    ['mob', 'elite', 'boss', 'chest'].map((src) => grantFor(id, rng, src as 'mob')),
  );
  extras.chests = ['thornwood', 'fen', 'frostfang', 'windscour'].map((z) => rollChestLoot(db.zone(z), rng));
  const smith = heroFor('warrior', 3);
  smith.data.gold = 100000;
  smith.addItem('scrap', 200);
  smith.addItem('dust', 200);
  const gear = smith.data.equipment.weapon!;
  extras.forge = Array.from({ length: 12 }, (_, i) =>
    i % 2 === 0 ? reinforce(smith, gear, rng) : JSON.stringify(hone(smith, gear, i % 4 === 1 ? 'damage' : 'crit', rng)),
  );
  extras.forgeGear = gear;
  const guild = heroFor('rogue', 3);
  guild.data.visited = ['brindlemoor', 'thornwood_path', 'fallen_oak', 'reedwatch', 'fen_road', 'hearthold', 'frostfang_pass'];
  refreshBoard(guild, rng);
  const board1 = [...guild.data.guild.board];
  takeHunt(guild, board1[0]);
  takeHunt(guild, board1[2]);
  refreshBoard(guild, rng);
  extras.guild = { board1, board2: guild.data.guild.board, hunts: guild.data.guild.hunts };
}

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, JSON.stringify({ fights, extras }));
console.log(`${OUT}: ${fights.length} fights, ${fights.reduce((s, f) => s + f.log.length, 0)} log lines`);
