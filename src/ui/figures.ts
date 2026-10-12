import Phaser from 'phaser';
import type { Look } from '../data/types';

/** Triangle in container-local coordinates. */
function tri(scene: Phaser.Scene, color: number, x1: number, y1: number, x2: number, y2: number, x3: number, y3: number) {
  return scene.add.graphics().fillStyle(color).fillTriangle(x1, y1, x2, y2, x3, y3);
}

/**
 * Placeholder characters built from shapes. Origin is at the feet.
 * Replace with sprites once art exists; scenes only depend on the returned Container.
 */
export function drawFigure(scene: Phaser.Scene, x: number, y: number, look: Look, facing: 1 | -1 = 1) {
  const c = scene.add.container(x, y);
  const { color, accent } = look;

  const shadow = scene.add.ellipse(0, 0, 110, 22, 0x000000, 0.35);
  c.add(shadow);

  switch (look.shape) {
    case 'humanoid': {
      const legs = scene.add.rectangle(0, -30, 38, 60, accent).setOrigin(0.5);
      const body = scene.add.rectangle(0, -95, 60, 80, color).setRounded(12);
      const head = scene.add.circle(0, -160, 28, 0xf0c8a0);
      const hair = scene.add.ellipse(0, -176, 60, 26, accent);
      const eye = scene.add.circle(12 * facing, -162, 4, 0x111111);
      const arm = scene.add.rectangle(30 * facing, -95, 16, 60, color).setRounded(6);
      c.add([legs, body, arm, head, hair, eye]);
      break;
    }
    case 'blob': {
      const body = scene.add.ellipse(0, -45, 120, 90, color);
      const top = scene.add.ellipse(-10, -78, 50, 26, accent);
      const eyeL = scene.add.circle(6 * facing, -55, 8, 0xffffff);
      const eyeR = scene.add.circle(40 * facing, -55, 8, 0xffffff);
      const pupL = scene.add.circle(9 * facing, -55, 4, 0x111111);
      const pupR = scene.add.circle(43 * facing, -55, 4, 0x111111);
      c.add([body, top, eyeL, eyeR, pupL, pupR]);
      break;
    }
    case 'beast': {
      const legs = [-40, -15, 20, 45].map((lx) => scene.add.rectangle(lx, -20, 16, 40, accent));
      const body = scene.add.ellipse(0, -60, 150, 80, color);
      const head = scene.add.ellipse(70 * facing, -70, 60, 50, color);
      const snout = scene.add.ellipse(98 * facing, -62, 26, 22, accent);
      const tusk = tri(scene, 0xf5f0e1, 88 * facing, -58, 96 * facing, -84, 102 * facing, -58);
      const eye = scene.add.circle(78 * facing, -80, 5, 0x111111);
      const spikes = [-40, -15, 10, 35].map((sx) => tri(scene, accent, sx - 12, -92, sx, -122, sx + 12, -92));
      c.add([...legs, body, ...spikes, head, snout, tusk, eye]);
      break;
    }
    case 'imp': {
      const wingL = tri(scene, accent, -20, -100, -90, -150, -30, -60);
      const wingR = tri(scene, accent, 20, -100, 90, -150, 30, -60);
      const body = scene.add.ellipse(0, -70, 64, 90, color);
      const head = scene.add.circle(0, -130, 26, color);
      const hornL = tri(scene, accent, -24, -148, -16, -176, -8, -152);
      const hornR = tri(scene, accent, 24, -148, 16, -176, 8, -152);
      const eye = scene.add.circle(10 * facing, -132, 5, 0xffe066);
      const legs = scene.add.rectangle(0, -14, 30, 28, accent);
      c.add([wingL, wingR, legs, body, head, hornL, hornR, eye]);
      break;
    }
  }

  c.setScale(look.scale);
  return c;
}

/** The player's look. Will come from character customization later. */
export const PLAYER_LOOK: Look = { shape: 'humanoid', color: 0x2e86de, accent: 0x5b3a1e, scale: 1 };
