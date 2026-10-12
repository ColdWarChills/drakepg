import Phaser from 'phaser';
import type { ArtRef, Backdrop } from '../data/types';
import { addBackground } from './art';
import { addText } from './text';

/**
 * Draw a full-screen scene: painted art if it exists, otherwise simple procedural scenery.
 * `groundY` is where the ground starts in the procedural version.
 */
export function drawScenery(scene: Phaser.Scene, art: ArtRef | undefined, backdrop: Backdrop, groundY: number): void {
  if (!addBackground(scene, art)) {
    if (backdrop.kind === 'town') drawTown(scene, groundY);
    else drawForest(scene, groundY);
  }
  if (backdrop.shade) {
    const { width, height } = scene.scale;
    scene.add.rectangle(0, 0, width, height, 0x050a12, backdrop.shade).setOrigin(0);
  }
}

function drawTown(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = scene.scale;
  scene.add.rectangle(0, 0, width, height, 0x87a9c9).setOrigin(0);
  scene.add.rectangle(0, 260, width, 200, 0xa9c4d9).setOrigin(0);
  scene.add.ellipse(250, groundY - 130, 700, 200, 0x6f8f6a);
  scene.add.ellipse(950, groundY - 120, 900, 220, 0x5f7f5c);

  building(scene, groundY, 200, 260, 230, 0x8e7b65, 0x5d4037, 'Elder');
  building(scene, groundY, 470, 280, 250, 0x9c8462, 0x7b3f00, 'Shop');
  building(scene, groundY, 760, 300, 260, 0xa1887f, 0x4e342e, 'Inn');

  // Town wall with an open gate on the right edge.
  scene.add.rectangle(960, groundY, 320, 260, 0x7f8c8d).setOrigin(0, 1);
  scene.add.rectangle(1150, groundY, 130, 180, 0x2b1b17).setOrigin(0.5, 1).setRounded(24);
  scene.add.rectangle(1150, groundY - 90, 90, 150, 0x6f8f6a).setOrigin(0.5, 0.5);

  scene.add.rectangle(0, groundY, width, height - groundY, 0x8d7b68).setOrigin(0);
  scene.add.rectangle(0, groundY, width, 8, 0x6d5d4b).setOrigin(0);
  // Cobbles.
  const g = scene.add.graphics();
  g.fillStyle(0x7a6957, 1);
  for (let i = 0; i < 70; i++) {
    const x = (i * 97) % width;
    const y = groundY + 18 + ((i * 53) % (height - groundY - 24));
    g.fillEllipse(x, y, 26, 10);
  }
}

function building(scene: Phaser.Scene, groundY: number, x: number, w: number, h: number, wall: number, roof: number, sign: string): void {
  scene.add.rectangle(x, groundY, w, h, wall).setOrigin(0.5, 1);
  const top = groundY - h;
  scene.add.graphics().fillStyle(roof).fillTriangle(x - w / 2 - 20, top, x, top - 90, x + w / 2 + 20, top);
  scene.add.rectangle(x - w / 4, top + 60, 50, 44, 0xf9e79f).setStrokeStyle(4, roof);
  scene.add.rectangle(x + w / 4, top + 60, 50, 44, 0xf9e79f).setStrokeStyle(4, roof);
  scene.add.rectangle(x, groundY, 54, 90, 0x4e342e).setOrigin(0.5, 1).setRounded(10);
  scene.add.rectangle(x, top - 30, 90, 28, 0x3e2723).setStrokeStyle(2, 0xc9a66b);
  addText(scene, x, top - 30, sign, { size: 16, bold: true, color: '#f3e9d2' }).setOrigin(0.5);
}

function drawForest(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = scene.scale;
  scene.add.rectangle(0, 0, width, height, 0x2c3e2f).setOrigin(0);
  // Far trees.
  for (let i = 0; i < 18; i++) {
    const x = i * 80 + ((i * 29) % 40);
    scene.add.circle(x, groundY - 220 - ((i * 17) % 60), 80, 0x22382a, 1);
    scene.add.rectangle(x, groundY, 18, 260, 0x2a2219).setOrigin(0.5, 1);
  }
  // Near trees.
  for (let i = 0; i < 9; i++) {
    const x = i * 160 + (i % 2) * 50;
    const h = 260 + ((i * 37) % 90);
    scene.add.rectangle(x, groundY + 10, 34, h, 0x3b2a1e).setOrigin(0.5, 1);
    scene.add.circle(x, groundY - h, 95 + ((i * 13) % 30), 0x1e3a24, 1);
    scene.add.circle(x - 50, groundY - h + 40, 60, 0x24452b, 1);
  }
  // Light shafts.
  const g = scene.add.graphics().setBlendMode(Phaser.BlendModes.ADD);
  for (let i = 0; i < 4; i++) {
    const x = 180 + i * 300;
    g.fillStyle(0xfff5c0, 0.06);
    g.fillTriangle(x, 0, x + 90, 0, x + 260, groundY);
    g.fillTriangle(x, 0, x + 260, groundY, x + 140, groundY);
  }
  scene.add.rectangle(0, groundY, width, height - groundY, 0x23301f).setOrigin(0);
  scene.add.rectangle(0, groundY, width, 10, 0x1b2617).setOrigin(0);
  const grass = scene.add.graphics();
  grass.fillStyle(0x2f4a29, 1);
  for (let i = 0; i < 60; i++) {
    const x = (i * 131) % width;
    const y = groundY + 20 + ((i * 47) % (height - groundY - 30));
    grass.fillTriangle(x - 6, y, x, y - 14, x + 6, y);
  }
}
