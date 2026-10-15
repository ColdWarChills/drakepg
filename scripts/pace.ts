/**
 * Headless pacing estimate: a bot plays the prologue road and each chapter's quest chain through the real rooms.
 * It walks the shortest way, fights everything roaming in each room it crosses (rooms refill like in the game,
 * one monster per `BALANCE.respawnSeconds`), farms until a quest is done and turns it in. When a story quest needs a
 * Guild rank, it takes the board's hunts (most rank points, least work) until it has the rank. It prints rooms, fights,
 * level at each boss and rough minutes per chapter. The seconds in `TIME` are guesses: a timed run by hand is still
 * the real check. Run with `npm run pace` (optionally `npm run pace -- 30` for more runs per class).
 */
import { BALANCE } from '../src/config';
import { db, validateData } from '../src/data/db';
import type { ClassId, MonsterDef, QuestDef, RankId, RoomDef, SkillDef, StatKey } from '../src/data/types';
import { ROOMS } from '../src/data/world';
import { Battle } from '../src/game/Battle';
import { makeElite, rollElite } from '../src/game/Elites';
import { featureOn } from '../src/game/Features';
import {
  activeHunts,
  canTakeHunt,
  hasRank,
  huntDef,
  huntDrops,
  huntPoints,
  huntReady,
  huntWantsElite,
  rankId,
  recordHuntKill,
  refreshBoard,
  takeHunt,
  turnInHunt,
  type Hunt,
} from '../src/game/Guild';
import { rollGear, type Gear, type GearDef } from '../src/game/Items';
import { emptyEquipment, Player } from '../src/game/Player';
import { acceptQuest, questStarted, questStatus, questWantsElite, recordKill, turnInQuest } from '../src/game/Quests';
import { visitRoom } from '../src/game/Travel';
import { chance, seeded, weightedPick, type Rng } from '../src/game/rng';
import { isGuard, recordGuard, roomBoss } from '../src/game/Travel';

const RUNS = Number(process.argv[2] ?? 10);

/** Rough seconds per action. */
const TIME = {
  /** Cross a room and fade to the next. */
  room: 7,
  /** One battle turn: pick a move, watch both sides act. */
  turn: 3.5,
  /** Battle intro, result screen, loot, back to the room. */
  fight: 8,
  /** Read a quest offer / turn one in. */
  offer: 12,
  turnIn: 8,
  /** Read the Guild board and take hunts. */
  board: 15,
  /** A quiet room's first-visit scene, and reading one note. */
  scene: 12,
  inspect: 5,
  /** Shopping for the chapter's gear in town. */
  shop: 60,
};

/** Proposed chapter lengths in minutes (docs/PLANS.md). */
const TARGET: Record<string, number> = { Prologue: 0, 'Chapter 1': 35, 'Chapter 2': 55, 'Chapter 3': 70 };

const STAT_PLAN: Record<ClassId, StatKey[]> = {
  warrior: ['str', 'str', 'end'],
  mage: ['int', 'int', 'end'],
  rogue: ['dex', 'dex', 'str'],
};

interface Chapter {
  name: string;
  /** Gear bought at the start (weapon, body, then helm/gloves/boots). Shop gear at 0% rolls. */
  gear: Record<ClassId, string[]>;
  /** A second weapon, swapped in when the enemy resists the first one less (players carry a spare). */
  spare?: Record<ClassId, string>;
  potion: string;
  quests: QuestDef[];
}

const CHAPTERS: Chapter[] = [
  {
    name: 'Prologue',
    gear: { warrior: ['training_sword'], mage: ['worn_staff'], rogue: ['rusty_dagger'] },
    potion: 'health_potion',
    quests: [],
  },
  {
    name: 'Chapter 1',
    gear: {
      warrior: ['iron_sword', 'leather_armor', 'leather_cap', 'leather_gloves', 'leather_boots'],
      mage: ['oak_staff', 'apprentice_robe', 'apprentice_hood', 'apprentice_wraps', 'apprentice_slippers'],
      rogue: ['hunting_knife', 'leather_armor', 'leather_cap', 'leather_gloves', 'leather_boots'],
    },
    potion: 'health_potion',
    quests: [],
  },
  {
    name: 'Chapter 2',
    gear: {
      warrior: ['steel_broadsword', 'scale_mail', 'scale_coif', 'scale_gauntlets', 'scale_boots'],
      mage: ['yew_staff', 'reedweave_robe', 'reedweave_hood', 'reedweave_wraps', 'reedweave_slippers'],
      rogue: ['fenwatch_stiletto', 'scale_mail', 'scale_coif', 'scale_gauntlets', 'scale_boots'],
    },
    potion: 'greater_health_potion',
    quests: [],
  },
  {
    name: 'Chapter 3',
    gear: {
      warrior: ['hearthfire_blade', 'ridgeplate', 'ridgeplate_helm', 'ridgeplate_gauntlets', 'ridgeplate_boots'],
      mage: ['kindling_rod', 'fleecewool_robe', 'fleecewool_hood', 'fleecewool_mittens', 'fleecewool_boots'],
      rogue: ['salamander_kris', 'ridgeplate', 'ridgeplate_helm', 'ridgeplate_gauntlets', 'ridgeplate_boots'],
    },
    spare: { warrior: 'tempered_longsword', mage: 'larchwood_staff', rogue: 'ridgerunner_kris' },
    potion: 'superior_health_potion',
    quests: [],
  },
];
// Quests in order, split where a chapter ends.
{
  let i = 1;
  for (const q of db.quests()) {
    CHAPTERS[i].quests.push(q);
    if (q.endsChapter) i++;
  }
}

// --- The world as the bot sees it ---

interface Roamer {
  monsterId: string;
  elite?: string;
}
interface RoomState {
  monsters: Roamer[];
  leftAt: number;
}

class Run {
  clock = 0;
  here: string;
  rooms = new Map<string, RoomState>();
  seen = new Set<string>();
  fights = 0;
  losses = 0;
  bossLevels: string[] = [];
  potion = 'health_potion';
  weapons: Gear[] = [];

  constructor(
    readonly p: Player,
    readonly rng: Rng,
    start: string,
  ) {
    this.here = start;
  }

  /** Shortest walk to `to` through open exits. */
  path(to: string): string[] {
    const steps = this.route(to);
    if (!steps) throw new Error(`No way from ${this.here} to ${to}`);
    return steps;
  }

  route(to: string): string[] | null {
    const prev = new Map<string, string>([[this.here, '']]);
    const queue = [this.here];
    while (queue.length) {
      const id = queue.shift()!;
      if (id === to) break;
      for (const e of db.room(id).exits) {
        if (prev.has(e.to) || (e.requiresQuest && !questStarted(this.p, e.requiresQuest))) continue;
        prev.set(e.to, id);
        queue.push(e.to);
      }
    }
    if (!prev.has(to)) return null;
    const steps: string[] = [];
    for (let id = to; id !== this.here; id = prev.get(id)!) steps.unshift(id);
    return steps;
  }

  walkTo(to: string): void {
    for (const id of this.path(to)) this.enter(id);
  }

  /**
   * Step into a room: first-visit scene and notes, then fight everything roaming there.
   * `farming`: wait next door until at least one monster has wandered back (leaving resets a room's timer).
   */
  enter(id: string, farming = false): void {
    const prev = this.rooms.get(this.here);
    if (prev) prev.leftAt = this.clock;
    const next = this.rooms.get(id);
    if (farming && next) this.clock = Math.max(this.clock, next.leftAt + BALANCE.respawnSeconds - TIME.room);
    this.here = id;
    this.clock += TIME.room;
    visitRoom(this.p, id);
    const room = db.room(id);
    if (!this.seen.has(id)) {
      this.seen.add(id);
      if (room.firstVisit) this.clock += TIME.scene;
      this.clock += (room.inspect?.length ?? 0) * TIME.inspect;
    }
    const guard = isGuard(room) ? roomBoss(this.p, room) : null;
    if (guard) this.guard(room, guard);
    if (!room.zoneId || !room.monsterCount) return;
    let state = this.rooms.get(id);
    if (!state) {
      state = { monsters: [], leftAt: this.clock };
      for (let i = 0; i < room.monsterCount; i++) state.monsters.push(this.spawn(room));
      this.rooms.set(id, state);
    } else {
      const back = Math.floor((this.clock - state.leftAt) / BALANCE.respawnSeconds);
      for (let i = Math.min(back, room.monsterCount - state.monsters.length); i > 0; i--) state.monsters.push(this.spawn(room));
    }
    while (state.monsters.length) this.fight(room, state.monsters.shift()!);
  }

  /** A gate guard: fought until beaten; a loss wakes you one room back, so walk in again. */
  guard(room: RoomDef, monsterId: string): void {
    const def = db.monster(monsterId);
    this.bossLevels.push(`${def.name} L${def.level}: hero L${this.p.level}`);
    for (let tries = 0; tries < 10; tries++) {
      if (this.battle(def, 2).state === 'victory') {
        this.gain(def.xp);
        recordGuard(this.p, room.id, def.id);
        return;
      }
      this.losses++;
      this.clock += TIME.room;
    }
  }

  spawn(room: RoomDef): Roamer {
    const zone = db.zone(room.zoneId!);
    const monsterId = weightedPick(this.rng, zone.encounters, (e) => e.weight).monsterId;
    const elite = featureOn(this.p, 'elites') ? rollElite(this.rng, questWantsElite(this.p) || huntWantsElite(this.p)) : undefined;
    return { monsterId, elite };
  }

  battle(def: MonsterDef, potions: number): Battle {
    const p = this.p;
    p.data.inventory = p.data.inventory.filter((s) => db.item(s.itemId).type === 'material');
    if (potions) p.addItem(this.potion, potions);
    // Carry whichever weapon the enemy resists least.
    const resist = (g: Gear) => {
      const e = (db.item(g.itemId) as GearDef).element;
      return e ? (def.resists[e] ?? 0) : 0;
    };
    if (this.weapons.length > 1) p.data.equipment.weapon = [...this.weapons].sort((a, b) => resist(a) - resist(b))[0];
    p.restoreFull();
    const b = new Battle(p, def, this.rng);
    let turns = 0;
    while (!b.isOver && turns < 80) {
      act(b);
      turns++;
    }
    this.fights++;
    this.clock += TIME.fight + turns * TIME.turn;
    return b;
  }

  fight(room: RoomDef, r: Roamer): void {
    const base = db.monster(r.monsterId);
    const def = r.elite ? makeElite(base, r.elite) : base;
    const b = this.battle(def, 1);
    if (b.state !== 'victory') {
      this.losses++;
      return;
    }
    this.gain(def.xp * (r.elite ? BALANCE.elite.rewards : 1));
    recordKill(this.p, def);
    recordHuntKill(this.p, def);
    for (const g of huntDrops(this.p, room.zoneId!, this.rng)) this.p.addItem(g.itemId);
    // Quest materials drop while a collect quest wants them.
    for (const q of db.quests()) {
      const o = q.objective;
      if (o.kind !== 'collect' || this.p.data.quests[q.id]?.status !== 'active') continue;
      for (const d of def.drops) if (d.itemId === o.itemId && chance(this.rng, d.chance)) this.p.addItem(d.itemId, d.qty ?? 1);
    }
  }

  /** A boss: retried (after the walk back from town) until beaten. */
  boss(roomId: string, monsterId: string): void {
    this.walkTo(roomId);
    const def = db.monster(monsterId);
    this.bossLevels.push(`${def.name} L${def.level}: hero L${this.p.level}`);
    for (let tries = 0; tries < 10; tries++) {
      if (this.battle(def, 3).state === 'victory') {
        this.gain(def.xp);
        recordKill(this.p, def);
        return;
      }
      this.losses++;
      const wake = db.room(roomId).wakeAt?.roomId ?? 'brindlemoor';
      this.here = wake;
      this.walkTo(roomId);
    }
  }

  gain(xp: number): void {
    const before = this.p.level;
    this.p.addXp(xp);
    if (this.p.level > before) this.levelUp();
  }

  levelUp(): void {
    const p = this.p;
    const plan = STAT_PLAN[p.data.classId];
    for (let i = 0; p.data.statPoints > 0; i++) p.spendStatPoint(plan[i % plan.length]);
    p.data.loadout = p.defaultLoadout();
  }

  outfit(ids: string[], spare?: string): void {
    const p = this.p;
    p.data.equipment = emptyEquipment();
    for (const id of ids) {
      const piece = p.makeGear(id, { rolls: rollGear(db.item(id) as GearDef, undefined, 0) });
      p.data.gear.push(piece);
      p.equip(piece.uid);
    }
    p.data.gear = [];
    this.weapons = [p.data.equipment.weapon!, ...(spare ? [p.makeGear(spare, { rolls: rollGear(db.item(spare) as GearDef, undefined, 0) })] : [])];
  }

  /** Talk to the giver, do the job, come back. A rank gate first sends it to the Guild. */
  quest(q: QuestDef): void {
    if (questStatus(this.p, q) === 'rank') this.guild(q.requiresRank!);
    const giver = ROOMS.find((r) => r.npcs.some((n) => n.id === q.giver))!;
    this.walkTo(giver.id);
    acceptQuest(this.p, q);
    this.clock += TIME.offer;
    const o = q.objective;
    const bossRoom = o.kind === 'kill' && o.monsterId ? ROOMS.find((r) => r.boss?.monsterId === o.monsterId) : undefined;
    if (bossRoom) this.boss(bossRoom.id, bossRoom.boss!.monsterId);
    else this.farm(q);
    this.walkTo(giver.id);
    if (!turnInQuest(this.p, q)) throw new Error(`Could not turn in ${q.id}`);
    this.clock += TIME.turnIn;
    this.levelUp();
  }

  /** Go to the nearest room where the quest's monster (or item) can be found; step out and back in until it's done. */
  farm(q: QuestDef): void {
    const o = q.objective;
    const wants = (room: RoomDef) => {
      if (!room.zoneId || !room.monsterCount) return false;
      const ids = db.zone(room.zoneId).encounters.map((e) => e.monsterId);
      if (o.kind === 'collect') return ids.some((id) => db.monster(id).drops.some((d) => d.itemId === o.itemId));
      return !o.monsterId || ids.includes(o.monsterId);
    };
    const done = () => (o.kind === 'collect' ? this.p.countItem(o.itemId) >= o.count : (this.p.data.quests[q.id]?.count ?? 0) >= o.count);
    this.farmFor(q.id, wants, done);
  }

  /** Take hunts from the nearest Guild Clerk and do them until the Guild ranks you `rank`. */
  guild(rank: RankId): void {
    const clerks = ROOMS.filter((r) => r.npcs.some((n) => n.role.kind === 'guild'));
    for (let round = 0; !hasRank(this.p, rank); round++) {
      if (round > 40) throw new Error(`Stuck at Guild rank ${rankId(this.p.data.guild.rank)}, wanting ${rank}`);
      const clerk = clerks.filter((r) => this.route(r.id)).sort((a, b) => this.path(a.id).length - this.path(b.id).length)[0];
      this.walkTo(clerk.id);
      this.clock += TIME.board;
      for (const a of activeHunts(this.p)) {
        if (!a.ready) continue;
        turnInHunt(this.p, a.state.key, this.rng);
        this.clock += TIME.turnIn;
        this.levelUp();
      }
      if (hasRank(this.p, rank)) break;
      refreshBoard(this.p, this.rng);
      // Most rank points for the least fighting; Elite bounties only when nothing else is up.
      const work = (h: Hunt) => (h.kind === 'gather' ? h.count / BALANCE.guild.gatherChance : h.kind === 'elite' ? 40 : h.count);
      const board = this.p.data.guild.board.map((k) => huntDef(k)!).sort((a, b) => huntPoints(this.p, b) * 20 - work(b) - (huntPoints(this.p, a) * 20 - work(a)));
      for (const h of board) if (canTakeHunt(this.p)) takeHunt(this.p, h.key);
      for (const a of activeHunts(this.p)) {
        const h = a.hunt;
        const wants = (room: RoomDef) =>
          !!room.zoneId && !!room.monsterCount && (h.kind === 'gather' ? room.zoneId === h.zoneId : db.zone(room.zoneId).encounters.some((e) => e.monsterId === h.monsterId));
        this.farmFor(h.key, wants, () => huntReady(this.p, a.state));
      }
    }
  }

  /** Farm the nearest room `wants` (stepping out and back in) until `done`. */
  farmFor(label: string, wants: (room: RoomDef) => boolean, done: () => boolean): void {
    if (done()) return;
    const targets = ROOMS.filter((r) => wants(r) && this.route(r.id)).sort((a, b) => this.path(a.id).length - this.path(b.id).length);
    if (!targets.length) throw new Error(`Nowhere to do ${label}`);
    const spot = targets[0].id;
    this.walkTo(spot);
    // Step into the next room toward another target (or any neighbour) and back.
    const other = targets[1] && this.path(targets[1].id).length <= 2 ? this.path(targets[1].id)[0] : db.room(spot).exits.find((e) => !e.requiresQuest || questStarted(this.p, e.requiresQuest))!.to;
    for (let i = 0; !done(); i++) {
      if (i > 400) throw new Error(`Stuck farming ${label} (${this.fights} fights, ${this.losses} lost)`);
      this.enter(i % 2 ? spot : other, true);
    }
  }
}

/** The sim's simple AI (scripts/sim.ts): brace for wind-ups, heal when low, else the best damage skill. */
function act(b: Battle): void {
  const p = b.player;
  const skills = p.carriedSkills.filter((s) => b.canUseSkill(s.id));
  const heal = skills.find((s) => s.kind === 'heal');
  const hpPct = p.hp / p.maxHp;
  if (b.charging) {
    if (heal && hpPct < 0.6) return void b.useSkill(heal.id);
    return void b.defend();
  }
  if (hpPct < 0.35) {
    if (heal) return void b.useSkill(heal.id);
    const potion = p.data.inventory.find((s) => db.item(s.itemId).type === 'consumable')?.itemId;
    if (potion && b.canUseItem(potion)) return void b.useItem(potion);
  }
  const reserve = heal ? 0 : (p.carriedSkills.find((s) => s.kind === 'heal')?.mpCost ?? 0);
  let best: SkillDef | null = null;
  let bestValue = 1.05;
  for (const s of skills) {
    if (s.kind !== 'damage' || p.mp - s.mpCost < reserve) continue;
    const element = !s.element || s.element === 'weapon' ? p.weaponElement : s.element;
    const weak = b.enemy.def.weakTo ?? {};
    let value = s.power * (s.hits ?? 1) * (element ? 1 - b.resistOf('enemy', element) / 100 : 1) * (s.blunt ? 1 + (weak.blunt ?? 0) / 100 : 1);
    // Poison on a monster weak to it, if it isn't poisoned already.
    const poisons = s.applies?.some((a) => a.status === 'poison' && !a.self);
    if (poisons && weak.poison && !b.statuses('enemy').some((x) => x.def.id === 'poison')) value += 0.8 * (1 + weak.poison / 100);
    if (value > bestValue) {
      best = s;
      bestValue = value;
    }
  }
  if (best) return void b.useSkill(best.id);
  b.attack();
}

// --- Report ---

/** Rooms of each chapter's map pages (the prologue is the road). */
const PAGES: Record<string, string[]> = { Prologue: ['road'], 'Chapter 1': ['thornwood'], 'Chapter 2': ['fen'], 'Chapter 3': ['frostfang'] };

function roomSummary(chapter: string): string {
  const rooms = ROOMS.filter((r) => PAGES[chapter].includes(r.map.area) && !r.town);
  // The main path: the shortest walk from Brindlemoor (or the road's start) to the page's last boss. Side rooms are off it.
  const from = chapter === 'Prologue' ? 'road_cart' : 'brindlemoor';
  const end = [...rooms].reverse().find((r) => r.boss) ?? rooms[rooms.length - 1];
  const prev = new Map<string, string>([[from, '']]);
  const queue = [from];
  while (queue.length) {
    const id = queue.shift()!;
    for (const e of db.room(id).exits) if (!prev.has(e.to)) (prev.set(e.to, id), queue.push(e.to));
  }
  const main = new Set<string>();
  for (let id = end.id; id; id = prev.get(id)!) main.add(id);
  const combat = rooms.filter((r) => r.monsterCount).length;
  const bosses = rooms.filter((r) => r.boss).length;
  const quiet = rooms.length - combat - bosses;
  const side = rooms.filter((r) => !main.has(r.id) && !r.boss).map((r) => r.name);
  const fires = rooms.filter((r) => r.campfire).length;
  const mobs = rooms.reduce((n, r) => n + (r.monsterCount ?? 0), 0);
  return `${rooms.length} rooms (${combat} combat, ${quiet} quiet, ${bosses} boss; side: ${side.join(', ') || 'none'}), ${fires} campfires, ${mobs} roaming monsters`;
}

/** Every exit should have a way back, except the one-way road into town. */
function checkExits(): string[] {
  const out: string[] = [];
  for (const r of ROOMS) {
    for (const e of r.exits) {
      if (r.id === 'road_gate' && e.to === 'brindlemoor') continue;
      if (!db.room(e.to).exits.some((back) => back.to === r.id)) out.push(`${r.id} -> ${e.to} has no way back`);
    }
  }
  return out;
}

validateData();
for (const line of checkExits()) console.log(`WARNING: ${line}`);

const classes: ClassId[] = ['warrior', 'mage', 'rogue'];
type Totals = { minutes: number; fights: number; losses: number; levelIn: number; levelOut: number; hunts: number; rank: number; bosses: string[][] };
const totals = new Map<string, Totals[]>();
const rng = seeded(1234);
for (const classId of classes) {
  for (let run = 0; run < RUNS; run++) {
    const p = Player.createNew('Pace', classId);
    p.data.inventory = [];
    const bot = new Run(p, rng, 'road_cart');
    for (const ch of CHAPTERS) {
      const start = { clock: bot.clock, fights: bot.fights, losses: bot.losses, level: p.level, hunts: p.data.guild.done };
      bot.bossLevels = [];
      bot.outfit(ch.gear[classId], ch.spare?.[classId]);
      bot.potion = ch.potion;
      // The hero starts in the first room of the road: its pests count too.
      if (ch.name === 'Prologue') {
        bot.enter('road_cart');
        bot.walkTo('brindlemoor');
      }
      else bot.clock += TIME.shop;
      for (const q of ch.quests) bot.quest(q);
      const t: Totals = {
        minutes: (bot.clock - start.clock) / 60,
        fights: bot.fights - start.fights,
        losses: bot.losses - start.losses,
        levelIn: start.level,
        levelOut: p.level,
        hunts: p.data.guild.done - start.hunts,
        rank: p.data.guild.rank,
        bosses: [bot.bossLevels],
      };
      totals.set(`${ch.name}|${classId}`, [...(totals.get(`${ch.name}|${classId}`) ?? []), t]);
    }
  }
}

const avg = (xs: number[]) => xs.reduce((a, b) => a + b, 0) / xs.length;
console.log(`\nPacing estimate: ${RUNS} runs per class. Times are rough guesses (see TIME in scripts/pace.ts).\n`);
for (const ch of CHAPTERS) {
  console.log(`${ch.name}: ${roomSummary(ch.name)}${TARGET[ch.name] ? `. Target ~${TARGET[ch.name]} min` : ''}`);
  for (const classId of classes) {
    const ts = totals.get(`${ch.name}|${classId}`)!;
    const bosses = ts[0].bosses[0].map((_, i) => {
      const levels = ts.map((t) => Number(t.bosses[0][i].split('hero L')[1]));
      return `${ts[0].bosses[0][i].split(': ')[0]} at hero L${Math.min(...levels)}-${Math.max(...levels)}`;
    });
    console.log(
      `  ${classId.padEnd(8)} ${avg(ts.map((t) => t.minutes)).toFixed(0).padStart(3)} min, ${avg(ts.map((t) => t.fights)).toFixed(0).padStart(3)} fights, ` +
        `${avg(ts.map((t) => t.losses)).toFixed(1)} losses, level ${avg(ts.map((t) => t.levelIn)).toFixed(1)} -> ${avg(ts.map((t) => t.levelOut)).toFixed(1)}, ` +
        `${avg(ts.map((t) => t.hunts)).toFixed(1)} hunts, Guild rank ${rankId(Math.min(...ts.map((t) => t.rank)))}-${rankId(Math.max(...ts.map((t) => t.rank)))}` +
        (bosses.length ? `; ${bosses.join(', ')}` : ''),
    );
  }
}
