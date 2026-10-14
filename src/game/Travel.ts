import { db } from '../data/db';
import type { RoomDef } from '../data/types';
import { ROOMS } from '../data/world';
import type { Player } from './Player';

/**
 * Where you've been and the Recall Scroll. Walking into a town makes it the scroll's destination;
 * a scroll is one way (you walk back out) and only works outside battle.
 */

export const RECALL_SCROLL = 'recall_scroll';

/** Mark a room as visited (the map shows it). Entering a town makes it the Recall Scroll's destination. Returns true on the first visit. */
export function visitRoom(player: Player, roomId: string): boolean {
  const first = !player.data.visited.includes(roomId);
  if (first) player.data.visited.push(roomId);
  if (db.room(roomId).town) player.data.lastTown = roomId;
  return first;
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
