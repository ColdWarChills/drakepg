import { Player } from './Player';

/** A monster roaming a room. Lives only in memory; rooms respawn their monsters when re-entered. */
export interface RoamingMonster {
  uid: number;
  monsterId: string;
  /** Elite affix id, if it's an Elite. */
  elite?: string;
  x: number;
  y: number;
}

/** Snapshot of the room you left to fight, so you return to the same spot with the same monsters. */
export interface RoomSnapshot {
  roomId: string;
  monsters: RoamingMonster[];
  /** The monster being fought. */
  fightingUid: number;
}

let current: Player | null = null;
let snapshot: RoomSnapshot | null = null;

/** The character currently being played, plus transient world state. Shared by all scenes. */
export const session = {
  get player(): Player {
    // Scenes can be started directly during development; fall back to a fresh character.
    if (!current) current = Player.createNew();
    return current;
  },

  start(player: Player): void {
    current = player;
    snapshot = null;
  },

  /** Remember the room before a battle. */
  saveRoom(s: RoomSnapshot): void {
    snapshot = s;
  },

  /** Take the remembered room (once). */
  takeRoom(roomId: string): RoomSnapshot | null {
    const s = snapshot && snapshot.roomId === roomId ? snapshot : null;
    snapshot = null;
    return s;
  },
};
