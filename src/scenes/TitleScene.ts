import Phaser from 'phaser';
import { COLORS } from '../config';
import { SaveSystem } from '../game/SaveSystem';
import { session } from '../game/session';
import { hasArt } from '../ui/art';
import { Button } from '../ui/Button';
import * as fx from '../ui/fx';
import { Modal } from '../ui/Modal';
import { addText } from '../ui/text';
import { enterScene, goTo, SceneKeys } from './keys';

export class TitleScene extends Phaser.Scene {
  constructor() {
    super(SceneKeys.Title);
  }

  create(): void {
    enterScene(this);
    const { width, height } = this.scale;

    // Menu column center. Moves left when portrait title art takes the right side.
    const cx = this.drawBackdrop();
    fx.starField(this, 70, height * 0.55);
    // Embers drifting up.
    this.add.particles(0, 0, 'fx_soft', {
      x: { min: 0, max: width },
      y: height + 10,
      speedY: { min: -70, max: -30 },
      speedX: { min: -15, max: 15 },
      scale: { start: 0.35, end: 0 },
      alpha: { start: 0.9, end: 0 },
      lifespan: 7000,
      frequency: 140,
      tint: [0xffb347, 0xff7b39, 0xffe066],
      blendMode: 'ADD',
    }).fastForward(7000);

    const logo = addText(this, cx, 170, 'DRAKEPG', { size: 96, bold: true, color: COLORS.textGold, stroke: true })
      .setOrigin(0.5)
      .setScale(0.6)
      .setAlpha(0);
    this.tweens.chain({
      targets: logo,
      tweens: [
        { scale: 1, alpha: 1, duration: 600, ease: 'Back.easeOut' },
        { y: 178, duration: 2200, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' },
      ],
    });
    addText(this, cx, 250, 'a turn-based adventure', { size: 26, color: COLORS.textDim }).setOrigin(0.5);

    const hasSave = SaveSystem.hasSave();

    new Button(this, cx, 380, 'New Game', () => {
      if (hasSave) this.confirmOverwrite();
      else this.startNew();
    }, { width: 280, height: 64, fontSize: 26 });

    new Button(this, cx, 465, 'Continue', () => this.continueGame(), {
      width: 280,
      height: 64,
      fontSize: 26,
    }).setEnabled(hasSave);

    addText(this, cx, height - 30, 'v0.8 · Chapter 3', { size: 16, color: COLORS.textDim }).setOrigin(0.5);
  }

  /** Draws the background and returns the x to center the menu on. */
  private drawBackdrop(): number {
    const { width, height } = this.scale;
    if (hasArt(this, 'bg_title')) {
      const img = this.add.image(0, 0, 'bg_title');
      if (img.width >= img.height) {
        img.setPosition(width / 2, height / 2).setScale(Math.max(width / img.width, height / img.height));
        // Darken a little so the logo and buttons stay readable.
        this.add.rectangle(0, 0, width, height, 0x000000, 0.25).setOrigin(0);
        return width / 2;
      }
      // Portrait art: show it whole on the right, menu on the left, blended with a night-sky gradient.
      img.setScale(height / img.height).setOrigin(1, 0).setPosition(width, 0);
      const artLeft = width - img.displayWidth;
      const sky = this.add.graphics().setDepth(-1);
      sky.fillGradientStyle(0x2a2856, 0x2a2856, 0x0a0e23, 0x0a0e23, 1);
      sky.fillRect(0, 0, artLeft + 4, height);
      const fade = this.add.graphics();
      fade.fillGradientStyle(0x232250, 0x232250, 0x0c1027, 0x0c1027, 1, 0, 1, 0);
      fade.fillRect(artLeft, 0, 180, height);
      return artLeft / 2;
    }
    // Simple layered backdrop: night sky, moon, distant hills.
    this.add.rectangle(0, 0, width, height, 0x1a1428).setOrigin(0);
    this.add.circle(1020, 150, 70, 0xf5e6c4, 0.9);
    this.add.ellipse(300, height, 900, 320, 0x2b2140);
    this.add.ellipse(1000, height, 1000, 260, 0x221a33);
    return width / 2;
  }

  /** The save is only replaced once the new character is confirmed. */
  private startNew(): void {
    goTo(this, SceneKeys.Create);
  }

  private continueGame(): void {
    const player = SaveSystem.load();
    if (!player) {
      this.startNew();
      return;
    }
    session.start(player);
    goTo(this, SceneKeys.Room, { ...player.data.location, via: 'spawn' });
  }

  private confirmOverwrite(): void {
    const modal = new Modal(this, 640, 260, 'Start over?');
    modal.content.add(
      addText(this, 320, 100, 'Your current save is replaced once you begin.', { size: 22, align: 'center' }).setOrigin(0.5),
    );
    modal.content.add(new Button(this, 200, 190, 'New character', () => this.startNew(), { width: 220 }));
    modal.content.add(new Button(this, 440, 190, 'Cancel', () => modal.close(), { width: 220 }));
  }
}
