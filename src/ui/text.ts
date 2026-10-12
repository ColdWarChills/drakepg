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

/** Damage numbers, "+40 HP", etc. Pops in, drifts up and fades. */
export function floatText(
  scene: Phaser.Scene,
  x: number,
  y: number,
  text: string,
  color: string,
  size = 32,
  tag?: string,
): void {
  const jitter = Phaser.Math.Between(-24, 24);
  const label = addText(scene, x + jitter, y, text, { size, color, bold: true, stroke: true })
    .setOrigin(0.5)
    .setDepth(4000)
    .setScale(0.3);
  scene.tweens.chain({
    targets: label,
    tweens: [
      { scale: 1.3, duration: 110, ease: 'Back.easeOut' },
      { scale: 1, duration: 120 },
      { y: y - 80, x: x + jitter * 1.6, alpha: 0, duration: 800, delay: 250, ease: 'Cubic.easeIn' },
    ],
    onComplete: () => label.destroy(),
  });
  if (tag) {
    const t = addText(scene, x + jitter, y - size * 0.9, tag, { size: Math.round(size * 0.5), color: COLORS.textGold, bold: true, stroke: true })
      .setOrigin(0.5)
      .setDepth(4000)
      .setAngle(-8);
    scene.tweens.add({ targets: t, y: t.y - 60, alpha: 0, delay: 500, duration: 700, onComplete: () => t.destroy() });
  }
}

/** Short message near the top of the screen. */
export function toast(scene: Phaser.Scene, text: string, color: string = COLORS.text): void {
  const { width } = scene.scale;
  const label = addText(scene, width / 2, 110, text, { size: 24, color, bold: true, stroke: true })
    .setOrigin(0.5)
    .setDepth(6000)
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
