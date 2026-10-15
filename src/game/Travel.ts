import { db } from '../data/db';
import type { RoomDef, RoomExit } from '../data/types';
import { ROOMS } from '../data/world';
import type { Player } from './Player';
import { questStarted } from './Quests';

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
