import { db } from '../data/db';
import { MAP_AREAS } from '../data/map';
import type { MapAreaDef, RoomDef, RoomExit } from '../data/types';
import { ROOMS } from '../data/world';
import { anyHuntReady, huntTargets } from './Guild';
import type { Player } from './Player';
import { questStarted, questStatus } from './Quests';

/**
 * What the map shows, kept free of Phaser. Visited rooms show in full; rooms next to them show as "?";
 * the rest stay hidden. Exits to another map page become arrows off the node.
 */

export type MapIcon = 'town' | 'boss' | 'campfire';

/**
 * '!' a quest to pick up here, '?' a quest (or a Guild hunt, at a clerk) to turn in here, 'target' what an active quest
 * or hunt asks you to fight is here.
 */
export type MapQuestMark = '!' | '?' | 'target' | null;

export interface MapNode {
  room: RoomDef;
  /** Grid spot from the room's data. */
  x: number;
  y: number;
  visited: boolean;
  here: boolean;
  icons: MapIcon[];
  quest: MapQuestMark;
}

export interface MapLink {
  a: MapNode;
  b: MapNode;
  /** False while a quest still blocks the way (thorns, a barricade...). */
  open: boolean;
}

/** An exit from this page to another one. */
export interface MapStub {
  from: MapNode;
  side: RoomExit['side'];
  /** The room it leads to, or "?" if you haven't been there. */
  label: string;
  area: MapAreaDef;
  open: boolean;
}

export interface AreaMap {
  area: MapAreaDef;
  nodes: MapNode[];
  links: MapLink[];
  stubs: MapStub[];
  /** Grid bounds of every room on the page, found or not, so the layout never shifts as you explore. */
  bounds: { minX: number; maxX: number; minY: number; maxY: number };
}

/** A sealed back or down exit isn't drawn in the room until its quest can be picked up, so the map hides it too. */
function exitVisible(player: Player, exit: RoomExit): boolean {
  if (exit.side !== 'back' && exit.side !== 'down') return true;
  return !exit.requiresQuest || questStatus(player, db.quest(exit.requiresQuest)) !== 'locked';
}

function exitOpen(player: Player, exit: RoomExit): boolean {
  return !exit.requiresQuest || questStarted(player, exit.requiresQuest);
}

/** Rooms on the map: everywhere you've been, plus the rooms one step from there. */
function shownRooms(player: Player, here: string): Set<string> {
  const visited = new Set([...player.data.visited, here]);
  const shown = new Set(visited);
  for (const id of visited) {
    if (!db.hasRoom(id)) continue;
    for (const exit of db.room(id).exits) if (exitVisible(player, exit)) shown.add(exit.to);
  }
  return shown;
}

function questMark(player: Player, room: RoomDef): MapQuestMark {
  const npcs = new Set(room.npcs.map((n) => n.id));
  if (room.npcs.some((n) => n.role.kind === 'guild') && anyHuntReady(player)) return '?';
  let mark: MapQuestMark = null;
  for (const quest of db.quests()) {
    const status = questStatus(player, quest);
    if (npcs.has(quest.giver) && status === 'ready') return '?';
    if (npcs.has(quest.giver) && status === 'available') mark = '!';
    if (mark || status !== 'active' || quest.objective.kind !== 'kill' || !quest.objective.monsterId) continue;
    const id = quest.objective.monsterId;
    const here = room.boss?.monsterId === id || (!!room.monsterCount && !!room.zoneId && db.zone(room.zoneId).encounters.some((e) => e.monsterId === id));
    if (here) mark = 'target';
  }
  if (mark || !room.monsterCount || !room.zoneId) return mark;
  // Guild hunts: the monsters they want roam here, or it's a gather hunt's area.
  const hunts = huntTargets(player);
  const zone = db.zone(room.zoneId);
  if (hunts.zones.has(zone.id) || zone.encounters.some((e) => hunts.monsters.has(e.monsterId))) return 'target';
  return mark;
}

/** Map pages with at least one room you've been to. */
export function knownAreas(player: Player, here: string): MapAreaDef[] {
  const visited = new Set([...player.data.visited, here]);
  return MAP_AREAS.filter((a) => ROOMS.some((r) => r.map.area === a.id && visited.has(r.id)));
}

export function areaOf(roomId: string): MapAreaDef {
  const id = db.room(roomId).map.area;
  return MAP_AREAS.find((a) => a.id === id)!;
}

/** One map page as the player knows it. `here` is the room they stand in. */
export function areaMap(player: Player, areaId: string, here: string): AreaMap {
  const area = MAP_AREAS.find((a) => a.id === areaId)!;
  const rooms = ROOMS.filter((r) => r.map.area === areaId);
  const shown = shownRooms(player, here);
  const visited = new Set([...player.data.visited, here]);

  const nodes: MapNode[] = rooms
    .filter((r) => shown.has(r.id))
    .map((room) => {
      const known = visited.has(room.id);
      const icons: MapIcon[] = [];
      if (known && room.town) icons.push('town');
      if (known && room.boss) icons.push('boss');
      if (known && room.campfire) icons.push('campfire');
      return { room, x: room.map.x, y: room.map.y, visited: known, here: room.id === here, icons, quest: questMark(player, room) };
    });
  const byId = new Map(nodes.map((n) => [n.room.id, n]));

  const links: MapLink[] = [];
  const stubs: MapStub[] = [];
  const linked = new Set<string>();
  for (const node of nodes) {
    if (!node.visited) continue;
    for (const exit of node.room.exits) {
      if (!exitVisible(player, exit)) continue;
      const open = exitOpen(player, exit);
      const target = db.room(exit.to);
      if (target.map.area !== areaId) {
        stubs.push({ from: node, side: exit.side, label: visited.has(target.id) ? target.name : '?', area: areaOf(target.id), open });
        continue;
      }
      const other = byId.get(exit.to);
      const key = [node.room.id, exit.to].sort().join('|');
      if (!other || linked.has(key)) continue;
      linked.add(key);
      links.push({ a: node, b: other, open });
    }
  }

  const xs = rooms.map((r) => r.map.x);
  const ys = rooms.map((r) => r.map.y);
  return { area, nodes, links, stubs, bounds: { minX: Math.min(...xs), maxX: Math.max(...xs), minY: Math.min(...ys), maxY: Math.max(...ys) } };
}
