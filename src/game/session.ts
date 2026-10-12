import { Player } from './Player';

let current: Player | null = null;

/** The character currently being played. Shared by all scenes. */
export const session = {
  get player(): Player {
    // Scenes can be started directly during development; fall back to a fresh character.
    if (!current) current = Player.createNew();
    return current;
  },

  start(player: Player): void {
    current = player;
  },
};
