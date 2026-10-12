import Phaser from 'phaser';
import { COLORS, FONT } from '../config';

export interface TextOptions {
  size?: number;
  color?: string;
  bold?: boolean;
  align?: 'left' | 'center' | 'right';
  wrap?: number;
  stroke?: boolean;
}

export function textStyle(opts: TextOptions = {}): Phaser.Types.GameObjects.Text.TextStyle {
  return {
    fontFamily: FONT,
    fontSize: `${opts.size ?? 20}px`,
    fontStyle: opts.bold ? 'bold' : 'normal',
    color: opts.color ?? COLORS.text,
    align: opts.align ?? 'left',
    stroke: opts.stroke ? '#000000' : undefined,
    strokeThickness: opts.stroke ? 4 : 0,
    wordWrap: opts.wrap ? { width: opts.wrap, useAdvancedWrap: true } : undefined,
  };
}

export function addText(
  scene: Phaser.Scene,
  x: number,
  y: number,
  text: string,
  opts: TextOptions = {},
): Phaser.GameObjects.Text {
  return scene.add.text(x, y, text, textStyle(opts));
}

/** Damage numbers, "+40 HP", etc. Rises and fades out. */
export function floatText(scene: Phaser.Scene, x: number, y: number, text: string, color: string, size = 32): void {
  const label = addText(scene, x, y, text, { size, color, bold: true, stroke: true }).setOrigin(0.5).setDepth(1000);
  scene.tweens.add({
    targets: label,
    y: y - 70,
    alpha: { from: 1, to: 0 },
    duration: 1000,
    ease: 'Cubic.easeOut',
    onComplete: () => label.destroy(),
  });
}

/** Short message near the top of the screen. */
export function toast(scene: Phaser.Scene, text: string, color: string = COLORS.text): void {
  const { width } = scene.scale;
  const label = addText(scene, width / 2, 110, text, { size: 24, color, bold: true, stroke: true })
    .setOrigin(0.5)
    .setDepth(2000)
    .setAlpha(0);
  scene.tweens.chain({
    targets: label,
    tweens: [
      { alpha: 1, y: 100, duration: 200 },
      { alpha: 1, duration: 1200 },
      { alpha: 0, y: 90, duration: 300 },
    ],
    onComplete: () => label.destroy(),
  });
}
