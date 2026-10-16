import Phaser from 'phaser';
import { COLORS, DESIGN } from '../config';
import { Button } from './Button';
import { play } from './sfx';
import { addText } from './text';

/**
 * Dimmed overlay + centered panel. The overlay swallows clicks so nothing behind it reacts.
 * Add content with `content.add(...)`, positioned relative to the panel's top-left corner.
 */
export class Modal extends Phaser.GameObjects.Container {
  readonly content: Phaser.GameObjects.Container;
  readonly panelWidth: number;
  readonly panelHeight: number;

  constructor(scene: Phaser.Scene, width: number, height: number, title: string, onClose?: () => void) {
    const { width: sw, height: sh } = DESIGN;
    super(scene, 0, 0);
    this.panelWidth = width;
    this.panelHeight = height;
    this.setDepth(5000);

    const shade = scene.add.rectangle(0, 0, sw, sh, 0x000000, 0.6).setOrigin(0).setInteractive();
    const left = (sw - width) / 2;
    const top = (sh - height) / 2;
    const panel = scene.add
      .rectangle(left, top, width, height, COLORS.panel)
      .setOrigin(0)
      .setRounded(14)
      .setStrokeStyle(3, COLORS.panelBorder);
    // Block clicks from falling through the panel to the shade.
    panel.setInteractive();
    const heading = addText(scene, sw / 2, top + 28, title, { size: 28, bold: true, color: COLORS.textGold })
      .setOrigin(0.5);

    this.content = scene.add.container(left, top);
    // Everything but the shade lives in `body` so it can pop in on its own.
    const body = scene.add.container(0, 0, [panel, heading, this.content]);
    this.add([shade, body]);

    if (onClose !== undefined) {
      const close = new Button(scene, left + width - 34, top + 28, 'X', () => this.close(onClose), {
        width: 44,
        height: 40,
        fontSize: 20,
      });
      body.add(close);
    }

    scene.add.existing(this);
    play(scene, 'open');
    shade.setAlpha(0);
    scene.tweens.add({ targets: shade, alpha: 1, duration: 150 });
    body.setAlpha(0).setY(24);
    scene.tweens.add({ targets: body, alpha: 1, y: 0, duration: 200, ease: 'Back.easeOut' });
  }

  close(after?: () => void): void {
    this.destroy();
    after?.();
  }
}
