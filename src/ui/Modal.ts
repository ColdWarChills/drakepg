import Phaser from 'phaser';
import { COLORS } from '../config';
import { Button } from './Button';
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
    const { width: sw, height: sh } = scene.scale;
    super(scene, 0, 0);
    this.panelWidth = width;
    this.panelHeight = height;
    this.setDepth(500);

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
    this.add([shade, panel, heading, this.content]);

    if (onClose !== undefined) {
      const close = new Button(scene, left + width - 34, top + 28, 'X', () => this.close(onClose), {
        width: 44,
        height: 40,
        fontSize: 20,
      });
      this.add(close);
    }

    scene.add.existing(this);
  }

  close(after?: () => void): void {
    this.destroy();
    after?.();
  }
}
