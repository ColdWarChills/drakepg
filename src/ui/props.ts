import Phaser from 'phaser';
import type { InspectDef } from '../data/types';

/** Room props drawn from shapes until they get art: the campfire and things to inspect. */

/** Stones, crossed logs and a flickering fire with rising embers. Feet (base) at x, y. */
export function drawCampfire(scene: Phaser.Scene, x: number, y: number): Phaser.GameObjects.Container {
  const c = scene.add.container(x, y).setDepth(y);
  const glow = scene.add.image(0, -30, 'fx_soft').setTint(0xff8a3d).setBlendMode('ADD').setScale(7, 5).setAlpha(0.35);
  const g = scene.add.graphics();
  g.fillStyle(0x000000, 0.35).fillEllipse(0, 4, 130, 26);
  for (let i = 0; i < 9; i++) {
    const a = (i / 9) * Math.PI * 2;
    g.fillStyle(i % 2 ? 0x6e6a66 : 0x85807a, 1).fillEllipse(Math.cos(a) * 52, Math.sin(a) * 12 - 4, 26, 16);
  }
  g.fillStyle(0x5d3a1f, 1);
  g.fillPoints([new Phaser.Math.Vector2(-44, -2), new Phaser.Math.Vector2(36, -18), new Phaser.Math.Vector2(40, -8), new Phaser.Math.Vector2(-40, 8)], true);
  g.fillPoints([new Phaser.Math.Vector2(44, -2), new Phaser.Math.Vector2(-36, -18), new Phaser.Math.Vector2(-40, -8), new Phaser.Math.Vector2(40, 8)], true);
  const flame = scene.add.graphics();
  flame.fillStyle(0xff5a1a, 1).fillTriangle(-24, -6, 24, -6, 0, -78);
  flame.fillStyle(0xff9f2e, 1).fillTriangle(-16, -6, 16, -6, 2, -58);
  flame.fillStyle(0xffe27a, 1).fillTriangle(-8, -6, 8, -6, 0, -34);
  const embers = scene.add.particles(0, -40, 'fx_soft', {
    speedY: { min: -90, max: -40 },
    speedX: { min: -18, max: 18 },
    scale: { start: 0.35, end: 0 },
    alpha: { start: 1, end: 0 },
    lifespan: 1400,
    frequency: 140,
    tint: [0xffb347, 0xff7a1a, 0xffe27a],
    blendMode: 'ADD',
  });
  c.add([glow, g, flame, embers]);
  scene.tweens.add({ targets: flame, scaleY: 1.18, scaleX: 0.9, duration: 180, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  scene.tweens.add({ targets: glow, alpha: 0.5, duration: 420, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  return c;
}

/** A note, sign, claw marks or bones on the ground, with a slow glint until it's been read. */
export function drawProp(scene: Phaser.Scene, def: InspectDef, glint: boolean): Phaser.GameObjects.Container {
  const c = scene.add.container(def.x, def.y).setDepth(def.y);
  const g = scene.add.graphics();
  g.fillStyle(0x000000, 0.3).fillEllipse(0, 2, 70, 14);
  switch (def.look) {
    case 'note':
      g.fillStyle(0xe8dcb5, 1).fillPoints([new Phaser.Math.Vector2(-24, -4), new Phaser.Math.Vector2(18, -12), new Phaser.Math.Vector2(26, 2), new Phaser.Math.Vector2(-16, 8)], true);
      g.lineStyle(2, 0x8a7a55, 1).lineBetween(-12, -3, 12, -8).lineBetween(-10, 2, 14, -3);
      break;
    case 'sign':
      g.fillStyle(0x5d3a1f, 1).fillRect(-4, -70, 8, 72);
      g.fillStyle(0x8d5a2b, 1).fillRect(-36, -84, 72, 34);
      g.lineStyle(3, 0x3e2723, 1).strokeRect(-36, -84, 72, 34);
      break;
    case 'marks':
      g.fillStyle(0x5d3a1f, 1).fillRoundedRect(-60, -26, 120, 26, 10);
      g.lineStyle(4, 0x2b1a0e, 1);
      for (let i = 0; i < 3; i++) g.lineBetween(-30 + i * 18, -22, -10 + i * 18, -4);
      break;
    case 'bones':
      g.fillStyle(0xe6e0d0, 1).fillRect(-26, -6, 52, 7).fillCircle(-26, -6, 5).fillCircle(-26, 1, 5).fillCircle(26, -6, 5).fillCircle(26, 1, 5);
      g.fillCircle(14, -16, 10);
      g.fillStyle(0x2b2620, 1).fillCircle(10, -17, 2.5).fillCircle(18, -17, 2.5);
      break;
  }
  c.add(g);
  if (glint) {
    const star = scene.add.image(0, def.look === 'sign' ? -96 : -26, 'fx_star').setBlendMode('ADD').setScale(0.9).setAlpha(0);
    c.add(star);
    scene.tweens.add({ targets: star, alpha: 1, angle: 90, duration: 700, yoyo: true, repeat: -1, repeatDelay: 900, ease: 'Sine.easeInOut' });
  }
  return c;
}
