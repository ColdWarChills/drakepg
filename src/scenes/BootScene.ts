import Phaser from 'phaser';
import { COLORS, DESIGN } from '../config';
import { validateData } from '../data/db';
import { preloadArt } from '../ui/art';
import { createFxTextures } from '../ui/fx';
import { addText } from '../ui/text';
import { fitCamera } from '../ui/view';
import { SceneKeys } from './keys';

/** Loads art and checks game data before anything else runs. */
export class BootScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Boot);
  }

  preload(): void {
    fitCamera(this.cameras.main);
    const { width, height } = DESIGN;
    const label = addText(this, width / 2, height / 2, 'Loading...', { size: 28, color: COLORS.textDim }).setOrigin(0.5);
    this.load.on('progress', (p: number) => label.setText(`Loading... ${Math.round(p * 100)}%`));
    preloadArt(this);
  }

  create(): void {
    validateData();
    createFxTextures(this);
    this.scene.start(SceneKeys.Title);
  }
}
