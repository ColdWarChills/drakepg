import { BALANCE } from '../config';
import { db } from '../data/db';
import { PROMOTIONS } from '../data/guild';
import type { MonsterDef, RankId, ZoneDef } from '../data/types';
import { ROOMS, ZONES } from '../data/world';
import { boosted, cheats } from './cheats';
import { featureOn } from './Features';
import { grantFor, type ItemGrant } from './Items';
import type { Player, Rewards } from './Player';
import { chance, defaultRng, randInt, type Rng } from './rng';

/**
 * The Adventurers' Guild: ranked hunts from the Guild Clerk's board (one board, a clerk in every town).
 *
 * - Ranks F to S (`BALANCE.guild.ranks`). Story quests can ask for one (`QuestDef.requiresRank`).
 * - Hunts aren't written by hand: the board builds them from what you can reach (rooms walkable from a town you know,
 *   up to one room past where you've been). Kill N of a monster, gather N of an area's item (any monster there drops
 *   it while the hunt is on), or, once Elites are unlocked, an Elite bounty.
 * - A hunt's rank comes from its monster's level; you may take hunts up to `reach` ranks above yours.
 * - Turning one in pays XP, gold and rank points; enough points promote you (`PROMOTIONS` rewards).
 *
 * Saved as `PlayerData.guild`; hunts are stored by key ('kill:mire_toad', 'gather:fen', 'elite:mire_toad') and
 * rebuilt from data, so retuning applies to hunts already taken.
 */

const G = BALANCE.guild;

export type HuntKind = 'kill' | 'gather' | 'elite';

export interface Hunt {
  key: string;
  kind: HuntKind;
  /** Kill hunts and Elite bounties. */
  monsterId?: string;
  /** Gather hunts: the area, and the item its monsters drop. */
  zoneId?: string;
  itemId?: string;
  count: number;
  /** Index into `BALANCE.guild.ranks` (0 = F). */
  rank: number;
  /** Paid on turn-in, before testing rates. */
  xp: number;
  gold: number;
}

/** A hunt you've taken. Gather hunts count the items in your bag instead of `count`. */
export interface HuntState {
  key: string;
  count: number;
}

export interface GuildState {
  /** Index into `BALANCE.guild.ranks` (0 = F). */
  rank: number;
  /** Rank points toward the next rank. */
  points: number;
  /** Hunt keys pinned on the board. */
  board: string[];
  hunts: HuntState[];
  /** Hunts turned in, all time. */
  done: number;
}

export function newGuild(): GuildState {
  return { rank: 0, points: 0, board: [], hunts: [], done: 0 };
}

export const MAX_RANK = G.ranks.length - 1;

export function rankId(rank: number): RankId {
  return G.ranks[Math.max(0, Math.min(MAX_RANK, rank))].id;
}

export function rankIndex(id: RankId): number {
  return G.ranks.findIndex((r) => r.id === id);
}

/** Points to go from this rank to the next (0 at the top). */
export function pointsToNext(rank: number): number {
  return rank >= MAX_RANK ? 0 : G.ranks[rank].toNext;
}

export function hasRank(player: Player, id: RankId): boolean {
  return player.data.guild.rank >= rankIndex(id);
}

/** The rank a monster of this level belongs to. */
export function rankForLevel(level: number): number {
  let rank = 0;
  G.ranks.forEach((r, i) => level >= r.minLevel && (rank = i));
  return rank;
}

const avg = (pair: [number, number]) => (pair[0] + pair[1]) / 2;

/** Weighted average of an area's monsters: level, XP and gold per kill. */
function zoneAverages(zone: ZoneDef): { level: number; xp: number; gold: number } {
  const total = zone.encounters.reduce((s, e) => s + e.weight, 0) || 1;
  const sum = (f: (m: MonsterDef) => number) => zone.encounters.reduce((s, e) => s + f(db.monster(e.monsterId)) * e.weight, 0) / total;
  return { level: sum((m) => m.level), xp: sum((m) => m.xp), gold: sum((m) => avg(m.gold)) };
}

const cache = new Map<string, Hunt | null>();

/** The hunt behind a key, or null if it no longer exists (a monster or area removed from the data). */
export function huntDef(key: string): Hunt | null {
  if (cache.has(key)) return cache.get(key)!;
  const hunt = buildHunt(key);
  cache.set(key, hunt);
  return hunt;
}

function buildHunt(key: string): Hunt | null {
  const [kind, id] = key.split(':') as [HuntKind, string];
  if (kind === 'gather') {
    const zone = ZONES.find((z) => z.id === id);
    if (!zone?.gather || !db.hasItem(zone.gather) || zone.encounters.length === 0) return null;
    const a = zoneAverages(zone);
    const kills = G.gather / G.gatherChance;
    return {
      key,
      kind,
      zoneId: zone.id,
      itemId: zone.gather,
      count: G.gather,
      rank: rankForLevel(Math.round(a.level)),
      xp: Math.round(kills * a.xp * G.xp),
      gold: Math.round(kills * a.gold * G.gold),
    };
  }
  if ((kind !== 'kill' && kind !== 'elite') || !db.hasMonster(id)) return null;
  const m = db.monster(id);
  if (m.boss) return null;
  const base = rankForLevel(m.level) + (m.tough ? 1 : 0);
  const kills = kind === 'elite' ? G.eliteKills : m.tough ? G.toughKills : G.kills;
  return {
    key,
    kind,
    monsterId: id,
    count: kind === 'elite' ? 1 : m.tough ? G.toughKills : G.kills,
    rank: Math.min(MAX_RANK, base + (kind === 'elite' ? 1 : 0)),
    xp: Math.round(kills * m.xp * G.xp),
    gold: Math.round(kills * avg(m.gold) * G.gold),
  };
}

/** "Mire Toad" -> "Mire Toads", "Drift Wolf" -> "Drift Wolves", "Bog Lily" -> "Bog Lilies". */
export function plural(name: string, n: number): string {
  if (n === 1) return name;
  if (/f$/.test(name)) return name.replace(/f$/, 'ves');
  if (/[^aeiou]y$/.test(name)) return name.replace(/y$/, 'ies');
  if (/(s|x|ch|sh)$/.test(name)) return `${name}es`;
  return `${name}s`;
}

/** "Hunt 5 Mire Toads", "Gather 4 Bog Lilies", "Elite bounty: Mire Toad". */
export function huntTitle(h: Hunt): string {
  if (h.kind === 'gather') return `Gather ${h.count} ${plural(db.item(h.itemId!).name, h.count)}`;
  const name = db.monster(h.monsterId!).name;
  return h.kind === 'elite' ? `Elite bounty: ${name}` : `Hunt ${h.count} ${plural(name, h.count)}`;
}

/** Short tracker line: "Mire Toads", "Bog Lilies", "Elite Mire Toad". */
export function huntGoal(h: Hunt): string {
  if (h.kind === 'gather') return plural(db.item(h.itemId!).name, h.count);
  const name = db.monster(h.monsterId!).name;
  return h.kind === 'elite' ? `Elite ${name}` : plural(name, h.count);
}

/** Where to go: "Thornwood, Deep Thornwood" or "Any monster in Mirewater Fen: 50% a kill". */
export function huntWhere(player: Player, h: Hunt): string {
  if (h.kind === 'gather') return `Any monster in ${db.zone(h.zoneId!).name}: ${Math.round(boosted(G.gatherChance) * 100)}% a kill`;
  const zones = reachableZones(player).filter((z) => db.zone(z).encounters.some((e) => e.monsterId === h.monsterId));
  const where = zones.map((z) => db.zone(z).name).join(', ');
  return h.kind === 'elite' ? `A gold-glowing one, in ${where}` : where;
}

/** Areas a hunt may send you to: their monsters roam rooms you can walk to from a town you know. */
export function reachableZones(player: Player): string[] {
  const visited = new Set(player.data.visited);
  const rooms = new Set<string>();
  const queue = ROOMS.filter((r) => r.town && visited.has(r.id)).map((r) => r.id);
  while (queue.length > 0) {
    const id = queue.shift()!;
    if (rooms.has(id)) continue;
    rooms.add(id);
    // One room past where you've been, like the map's "?" rooms.
    if (!visited.has(id)) continue;
    for (const e of db.room(id).exits) {
      if (!rooms.has(e.to) && (!e.requiresQuest || player.data.quests[e.requiresQuest] !== undefined)) queue.push(e.to);
    }
  }
  const zones = new Set<string>();
  for (const id of rooms) {
    const r = db.room(id);
    if (r.zoneId && r.monsterCount) zones.add(r.zoneId);
  }
  return [...zones];
}

/** Every hunt the board could post right now, ignoring rank. */
export function huntPool(player: Player): Hunt[] {
  const zones = reachableZones(player);
  const monsters = [...new Set(zones.flatMap((z) => db.zone(z).encounters.map((e) => e.monsterId)))];
  const keys = [
    ...monsters.map((m) => `kill:${m}`),
    ...(featureOn(player, 'elites') ? monsters.map((m) => `elite:${m}`) : []),
    ...zones.map((z) => `gather:${z}`),
  ];
  return keys.map(huntDef).filter((h): h is Hunt => h !== null);
}

/** Highest hunt rank you may take. */
export function maxHuntRank(player: Player): number {
  return Math.min(MAX_RANK, player.data.guild.rank + G.reach);
}

/** Rank points a hunt gives you now: 1 at your rank, +1 per rank above, none below (or at the top rank). */
export function huntPoints(player: Player, h: Hunt): number {
  const rank = player.data.guild.rank;
  return rank >= MAX_RANK ? 0 : Math.max(0, h.rank - rank + 1);
}

/**
 * Keep the board full: drop notices you can't take any more (or that a better one should replace), then pin new
 * ones. Hunts at your rank or above first (an Elite bounty now and then); lower ones only fill empty space.
 */
export function refreshBoard(player: Player, rng: Rng = defaultRng): void {
  const g = player.data.guild;
  const taken = new Set(g.hunts.map((h) => h.key));
  const pool = huntPool(player).filter((h) => !taken.has(h.key) && h.rank <= maxHuntRank(player));
  const fresh = (h: Hunt) => h.rank >= g.rank;
  const byKey = new Map(pool.map((h) => [h.key, h]));
  const spare = () => pool.filter((h) => !g.board.includes(h.key));
  // A low notice gives way once there is work at your rank to replace it.
  g.board = g.board.filter((k) => byKey.has(k));
  for (const k of [...g.board]) {
    if (fresh(byKey.get(k)!) || !spare().some((h) => fresh(h) && h.kind !== 'elite')) continue;
    g.board = g.board.filter((b) => b !== k);
  }
  const pick = (list: Hunt[]) => list[randInt(rng, 0, list.length - 1)];
  while (g.board.length < G.boardSize) {
    const left = spare();
    const elites = left.filter((h) => fresh(h) && h.kind === 'elite');
    const normal = left.filter((h) => fresh(h) && h.kind !== 'elite');
    const low = left.filter((h) => !fresh(h) && h.kind !== 'elite');
    let next: Hunt | undefined;
    if (elites.length > 0 && (normal.length === 0 || chance(rng, G.eliteChance))) next = pick(elites);
    else if (normal.length > 0) next = pick(normal);
    else if (low.length > 0) {
      const best = Math.max(...low.map((h) => h.rank));
      next = pick(low.filter((h) => h.rank === best));
    }
    if (!next) break;
    g.board.push(next.key);
  }
}

/** New notices after a turn-in. */
export function rerollBoard(player: Player, rng: Rng = defaultRng): void {
  player.data.guild.board = [];
  refreshBoard(player, rng);
}

export function canTakeHunt(player: Player): boolean {
  return player.data.guild.hunts.length < G.maxActive;
}

export function takeHunt(player: Player, key: string): boolean {
  const g = player.data.guild;
  const h = huntDef(key);
  if (!h || !canTakeHunt(player) || g.hunts.some((s) => s.key === key) || h.rank > maxHuntRank(player)) return false;
  g.board = g.board.filter((k) => k !== key);
  g.hunts.push({ key, count: 0 });
  return true;
}

/** Give a hunt up. Its kills are lost; gather items stay in the bag. */
export function dropHunt(player: Player, key: string): void {
  const g = player.data.guild;
  g.hunts = g.hunts.filter((s) => s.key !== key);
}

export function huntProgress(player: Player, state: HuntState): number {
  const h = huntDef(state.key);
  if (!h) return 0;
  if (h.kind === 'gather') return Math.min(h.count, player.countItem(h.itemId!));
  return Math.min(h.count, state.count);
}

export function huntReady(player: Player, state: HuntState): boolean {
  const h = huntDef(state.key);
  return !!h && huntProgress(player, state) >= h.count;
}

/** Taken hunts with their definitions (skipping any whose data is gone). */
export function activeHunts(player: Player): { state: HuntState; hunt: Hunt; count: number; ready: boolean }[] {
  return player.data.guild.hunts.flatMap((state) => {
    const hunt = huntDef(state.key);
    return hunt ? [{ state, hunt, count: huntProgress(player, state), ready: huntReady(player, state) }] : [];
  });
}

export function anyHuntReady(player: Player): boolean {
  return activeHunts(player).some((a) => a.ready);
}

/** True while an Elite bounty is on (Elites spawn more often then). */
export function huntWantsElite(player: Player): boolean {
  return activeHunts(player).some((a) => a.hunt.kind === 'elite' && !a.ready);
}

/** What unfinished hunts send you after: monster ids, and areas for gather hunts. For map marks. */
export function huntTargets(player: Player): { monsters: Set<string>; zones: Set<string> } {
  const monsters = new Set<string>();
  const zones = new Set<string>();
  for (const a of activeHunts(player)) {
    if (a.ready) continue;
    if (a.hunt.monsterId) monsters.add(a.hunt.monsterId);
    if (a.hunt.zoneId) zones.add(a.hunt.zoneId);
  }
  return { monsters, zones };
}

export interface HuntUpdate {
  hunt: Hunt;
  count: number;
  target: number;
}

/** Count a defeated monster toward every kill hunt and Elite bounty that wants it. Elites keep their base id. */
export function recordHuntKill(player: Player, monster: MonsterDef): HuntUpdate[] {
  const updates: HuntUpdate[] = [];
  for (const state of player.data.guild.hunts) {
    const h = huntDef(state.key);
    if (!h || h.kind === 'gather' || h.monsterId !== monster.id || state.count >= h.count) continue;
    if (h.kind === 'elite' && monster.elite === undefined) continue;
    state.count += 1;
    updates.push({ hunt: h, count: state.count, target: h.count });
  }
  return updates;
}

/** Gather hunts in this area that still want items, with their drop chance per kill. For the battle panel. */
export function gatherHere(player: Player, zoneId: string): { hunt: Hunt; chance: number }[] {
  return activeHunts(player)
    .filter((a) => a.hunt.kind === 'gather' && a.hunt.zoneId === zoneId && !a.ready)
    .map((a) => ({ hunt: a.hunt, chance: boosted(G.gatherChance) }));
}

/** A kill in this area: maybe a gather hunt item for each gather hunt here that still wants some. */
export function huntDrops(player: Player, zoneId: string, rng: Rng = defaultRng): ItemGrant[] {
  return gatherHere(player, zoneId)
    .filter((g) => chance(rng, g.chance))
    .map((g) => grantFor(g.hunt.itemId!, rng));
}

export interface TurnInHunt {
  hunt: Hunt;
  rewards: Rewards;
  points: number;
  /** Ranks reached by this turn-in, lowest first (usually none or one). */
  promotions: RankId[];
  levelsGained: number;
}

/** Hand a finished hunt in at a Guild Clerk: XP, gold and rank points, then any promotions, then fresh notices. */
export function turnInHunt(player: Player, key: string, rng: Rng = defaultRng): TurnInHunt | null {
  const g = player.data.guild;
  const state = g.hunts.find((s) => s.key === key);
  const hunt = huntDef(key);
  if (!state || !hunt || !huntReady(player, state)) return null;
  if (hunt.kind === 'gather' && !player.removeItem(hunt.itemId!, hunt.count)) return null;
  const points = huntPoints(player, hunt);
  g.hunts = g.hunts.filter((s) => s.key !== key);
  g.done += 1;
  g.points += points;
  // Testing rates (`cheats.rates`) multiply XP and gold.
  const rewards: Rewards = { xp: hunt.xp * cheats.rates, gold: hunt.gold * cheats.rates, items: [] };
  const promotions: RankId[] = [];
  while (g.rank < MAX_RANK && g.points >= pointsToNext(g.rank)) {
    g.points -= pointsToNext(g.rank);
    g.rank += 1;
    const id = rankId(g.rank);
    promotions.push(id);
    const p = PROMOTIONS[id];
    if (!p) continue;
    rewards.gold += p.gold;
    const classItem = p.classItems?.[player.data.classId];
    for (const itemId of [...(p.items ?? []), ...(classItem ? [classItem] : [])]) rewards.items.push(grantFor(itemId, rng));
  }
  if (g.rank >= MAX_RANK) g.points = 0;
  // Rewards may overflow a full bag rather than be lost, like quest rewards.
  const levelsGained = player.grantRewards(rewards, true).levels;
  rerollBoard(player, rng);
  return { hunt, rewards, points, promotions, levelsGained };
}

/** Save cleanup: a valid rank, and only hunts that still exist. */
export function sanitizeGuild(raw: unknown): GuildState {
  const g = newGuild();
  if (!raw || typeof raw !== 'object') return g;
  const r = raw as Partial<GuildState>;
  const int = (v: unknown, max: number) => (typeof v === 'number' && Number.isFinite(v) ? Math.max(0, Math.min(max, Math.floor(v))) : 0);
  g.rank = int(r.rank, MAX_RANK);
  g.points = int(r.points, Math.max(0, pointsToNext(g.rank) - 1));
  g.done = int(r.done, 1e9);
  const hunts = Array.isArray(r.hunts) ? r.hunts : [];
  for (const s of hunts) {
    if (!s || typeof s.key !== 'string' || !huntDef(s.key) || g.hunts.some((h) => h.key === s.key)) continue;
    g.hunts.push({ key: s.key, count: int(s.count, huntDef(s.key)!.count) });
  }
  g.hunts = g.hunts.slice(0, G.maxActive);
  const board = Array.isArray(r.board) ? r.board : [];
  g.board = [...new Set(board.filter((k): k is string => typeof k === 'string' && !!huntDef(k) && !g.hunts.some((h) => h.key === k)))].slice(0, G.boardSize);
  return g;
}
