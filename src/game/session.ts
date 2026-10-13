import { Player } from './Player';

/** A monster roaming a room. Lives only in memory (rooms are remembered for this play session, not saved). */
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

/** A treasure chest standing in a room until it is opened. Mimics bite. */
export interface Chest {
  x: number;
  y: number;
  mimic: boolean;
}

/** A room as you left it: who was still alive, and when you walked out. */
export interface RoomMemory {
  monsters: RoamingMonster[];
  chest: Chest | null;
  /** Date.now() when you left. */
  leftAt: number;
}

let current: Player | null = null;
let snapshot: RoomSnapshot | null = null;
const rooms = new Map<string, RoomMemory>();

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
    rooms.clear();
  },

  /** Remember a room you are leaving, so its dead monsters stay dead for a while. */
  rememberRoom(roomId: string, memory: RoomMemory): void {
    rooms.set(roomId, memory);
  },

  roomMemory(roomId: string): RoomMemory | undefined {
    return rooms.get(roomId);
  },

  /** Every room starts fresh again (the testing menu's "Respawn monsters"). */
  forgetRooms(): void {
    rooms.clear();
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
