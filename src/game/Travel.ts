import { BALANCE } from '../config';
import { db } from '../data/db';
import type { RoomDef, RoomExit } from '../data/types';
import { ROOMS } from '../data/world';
import type { Player } from './Player';
import { questStarted } from './Quests';

/**
 * Where you've been, the Recall Scroll and the town drivers. Walking into a town makes it the scroll's destination;
 * a scroll is one way (you walk back out) and only works outside battle. A driver (`role: coach`) rides you from
 * their town to any town you've walked into, for a fare by distance.
 */

export const RECALL_SCROLL = 'recall_scroll';

/** Mark a room as visited (the map shows it). Entering a town makes it the Recall Scroll's destination. Returns true on the first visit. */
export function visitRoom(player: Player, roomId: string): boolean {
  const first = !player.data.visited.includes(roomId);
  if (first) player.data.visited.push(roomId);
  if (db.room(roomId).town) player.data.lastTown = roomId;
  return first;
}

/**
 * The boss standing in this room right now (the hero's class picks a `byClass` variant), or null.
 * A quest boss shows once its quest is accepted; a gate guard (no quest) until it's beaten.
 */
export function roomBoss(player: Player, room: RoomDef): string | null {
  const b = room.boss;
  if (!b) return null;
  if (b.questId ? !questStarted(player, b.questId) : player.data.guardsBeaten.includes(room.id)) return null;
  return b.byClass?.[player.data.classId] ?? b.monsterId;
}

/** A gate guard: a room boss with no quest. It is fought once; rematches don't exist. */
export function isGuard(room: RoomDef): boolean {
  return !!room.boss && !room.boss.questId;
}

/** Can you walk through it: its quest is accepted, and no gate guard stands in front of it. */
export function exitOpen(player: Player, room: RoomDef, exit: RoomExit): boolean {
  if (exit.requiresQuest && !questStarted(player, exit.requiresQuest)) return false;
  return !exit.guarded || roomBoss(player, room) === null;
}

/** After a win: a beaten gate guard is gone for good and its `guarded` exits open. */
export function recordGuard(player: Player, roomId: string, monsterId: string): void {
  const room = db.room(roomId);
  if (isGuard(room) && roomBoss(player, room) === monsterId) player.data.guardsBeaten.push(roomId);
}

/** The town of a map area (Brindlemoor for the Thornwood...), if it has one. */
export function areaTown(areaId: string): RoomDef | undefined {
  return ROOMS.find((r) => r.map.area === areaId && r.town);
}

export type RecallCheck = { ok: true; town: RoomDef } | { ok: false; reason: string };

/** Can a scroll be read here, and where would it go? */
export function recallCheck(player: Player, hereRoomId: string): RecallCheck {
  if (player.countItem(RECALL_SCROLL) <= 0) return { ok: false, reason: 'No Recall Scrolls left. Town shops sell them.' };
  const id = player.data.lastTown;
  if (!id) return { ok: false, reason: 'You have not reached a town yet.' };
  const town = db.room(id);
  if (id === hereRoomId) return { ok: false, reason: `You are already in ${town.name}.` };
  return { ok: true, town };
}

/** Read one scroll: it's used up, and you get the landing spot. Null if it can't be used here. */
export function useRecall(player: Player, hereRoomId: string): { roomId: string; x: number; y: number } | null {
  const check = recallCheck(player, hereRoomId);
  if (!check.ok || !player.removeItem(RECALL_SCROLL)) return null;
  return { roomId: check.town.id, ...check.town.town! };
}

/** Rooms on the shortest road between two rooms, through any exit (locks don't count: rides go where you've been). */
export function roomsBetween(fromId: string, toId: string): number {
  if (fromId === toId) return 0;
  const seen = new Set([fromId]);
  let frontier = [fromId];
  for (let steps = 1; frontier.length > 0; steps++) {
    const next: string[] = [];
    for (const id of frontier) {
      for (const exit of db.room(id).exits) {
        if (exit.to === toId) return steps;
        if (!seen.has(exit.to)) {
          seen.add(exit.to);
          next.push(exit.to);
        }
      }
    }
    frontier = next;
  }
  return Infinity;
}

/** A town a driver can take you to, its fare from here, and why you can't go yet (null = you can). */
export interface CoachStop {
  town: RoomDef;
  fare: number;
  reason: string | null;
}

/** Every other town, nearest first, with its fare from the driver's town. */
export function coachStops(player: Player, hereRoomId: string): CoachStop[] {
  return ROOMS.filter((r) => r.town && r.id !== hereRoomId)
    .map((town) => ({ town, fare: roomsBetween(hereRoomId, town.id) * BALANCE.coach.farePerRoom }))
    .sort((a, b) => a.fare - b.fare)
    .map(({ town, fare }) => ({
      town,
      fare,
      reason: !player.data.visited.includes(town.id) ? 'Not reached yet' : player.gold < fare ? 'Not enough gold' : null,
    }));
}

/** Pay the fare: you get the landing spot in that town. Null if the ride isn't possible. */
export function ride(player: Player, hereRoomId: string, townId: string): { roomId: string; x: number; y: number } | null {
  const stop = coachStops(player, hereRoomId).find((s) => s.town.id === townId);
  if (!stop || stop.reason !== null) return null;
  player.data.gold -= stop.fare;
  return { roomId: stop.town.id, ...stop.town.town! };
}
