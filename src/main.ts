import Phaser from 'phaser';
import { COLORS, GAME_HEIGHT, GAME_WIDTH } from './config';
import { session } from './game/session';
import { BattleScene } from './scenes/BattleScene';
import { BootScene } from './scenes/BootScene';
import { CreateScene } from './scenes/CreateScene';
import { ResultScene } from './scenes/ResultScene';
import { TitleScene } from './scenes/TitleScene';
import { RoomScene } from './scenes/RoomScene';

const game = new Phaser.Game({
  type: Phaser.AUTO,
  parent: 'game',
  backgroundColor: COLORS.bg,
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
  },
  // Needed for the name box in character creation.
  dom: { createContainer: true },
  scene: [BootScene, TitleScene, CreateScene, RoomScene, BattleScene, ResultScene],
});

// Handy for poking at state from the browser console during development.
if (import.meta.env.DEV) Object.assign(window, { game, session });
