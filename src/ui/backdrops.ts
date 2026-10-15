import Phaser from 'phaser';
import { DESIGN } from '../config';
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
    else if (backdrop.kind === 'marsh') drawMarsh(scene, groundY);
    else if (backdrop.kind === 'outpost') drawOutpost(scene, groundY);
    else if (backdrop.kind === 'snow') drawSnow(scene, groundY);
    else if (backdrop.kind === 'village') drawVillage(scene, groundY);
    else if (backdrop.kind === 'cave') drawCave(scene, groundY);
    else drawForest(scene, groundY);
  }
  if (backdrop.shade) {
    const { width, height } = DESIGN;
    scene.add.rectangle(0, 0, width, height, 0x050a12, backdrop.shade).setOrigin(0);
  }
}

function drawTown(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = DESIGN;
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
  const { width, height } = DESIGN;
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

function drawMarsh(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = DESIGN;
  // Overcast sky fading into mist.
  scene.add.rectangle(0, 0, width, height, 0x52666b).setOrigin(0);
  scene.add.rectangle(0, groundY - 260, width, 260, 0x6c8180).setOrigin(0);
  scene.add.rectangle(0, groundY - 120, width, 120, 0x7d918d).setOrigin(0);
  // Far dead trees.
  const far = scene.add.graphics();
  for (let i = 0; i < 12; i++) {
    const x = i * 115 + ((i * 41) % 60);
    const h = 180 + ((i * 53) % 120);
    far.fillStyle(0x3d4d4c, 1).fillRect(x, groundY - h, 12, h);
    far.lineStyle(6, 0x3d4d4c, 1);
    far.beginPath();
    far.moveTo(x + 6, groundY - h * 0.7);
    far.lineTo(x + 40, groundY - h * 0.9);
    far.moveTo(x + 6, groundY - h * 0.5);
    far.lineTo(x - 30, groundY - h * 0.75);
    far.strokePath();
  }
  // Muddy ground with still pools.
  scene.add.rectangle(0, groundY, width, height - groundY, 0x3b4630).setOrigin(0);
  scene.add.rectangle(0, groundY, width, 8, 0x2c3524).setOrigin(0);
  const pools = scene.add.graphics();
  for (let i = 0; i < 6; i++) {
    const x = 90 + i * 230 + ((i * 37) % 80);
    const y = groundY + 40 + ((i * 61) % (height - groundY - 70));
    pools.fillStyle(0x2f5260, 0.9).fillEllipse(x, y, 200 + ((i * 29) % 90), 34);
    pools.fillStyle(0x8fb3bd, 0.25).fillEllipse(x - 20, y - 4, 90, 6);
  }
  // Reeds.
  const reeds = scene.add.graphics();
  for (let i = 0; i < 46; i++) {
    const x = (i * 89) % width;
    const base = groundY + 6 + ((i * 31) % 26);
    const h = 40 + ((i * 47) % 70);
    reeds.lineStyle(3, i % 2 ? 0x6b7f3a : 0x55682d, 1);
    reeds.beginPath();
    reeds.moveTo(x, base);
    reeds.lineTo(x + ((i * 7) % 12) - 6, base - h);
    reeds.strokePath();
    if (i % 4 === 0) reeds.fillStyle(0x5d3a1e, 1).fillEllipse(x + ((i * 7) % 12) - 6, base - h + 8, 7, 18);
  }
}

function drawOutpost(scene: Phaser.Scene, groundY: number): void {
  drawMarsh(scene, groundY);
  // A boardwalk and huts on stilts.
  scene.add.rectangle(0, groundY - 6, DESIGN.width, 14, 0x6d4c33).setOrigin(0);
  stiltHut(scene, groundY, 200, 230, 'Watch');
  stiltHut(scene, groundY, 560, 250, 'Supplies');
  stiltHut(scene, groundY, 900, 230, 'Healer');
  // Watchtower at the far right.
  const g = scene.add.graphics();
  g.fillStyle(0x4e3524, 1).fillRect(1150, groundY - 330, 14, 330).fillRect(1220, groundY - 330, 14, 330);
  g.fillStyle(0x6d4c33, 1).fillRect(1135, groundY - 360, 115, 40);
  g.fillStyle(0x3e2723, 1).fillTriangle(1125, groundY - 360, 1192, groundY - 420, 1260, groundY - 360);
  g.fillStyle(0xf9e79f, 1).fillCircle(1192, groundY - 340, 8);
}

function stiltHut(scene: Phaser.Scene, groundY: number, x: number, w: number, sign: string): void {
  const g = scene.add.graphics();
  const floor = groundY - 60;
  g.fillStyle(0x4e3524, 1);
  for (const sx of [-w / 2 + 12, -w / 6, w / 6, w / 2 - 24]) g.fillRect(x + sx, floor, 12, 66);
  g.fillStyle(0x7b5a3c, 1).fillRect(x - w / 2, floor - 150, w, 150);
  g.lineStyle(2, 0x4e3524, 1);
  for (let i = 1; i < 6; i++) g.lineBetween(x - w / 2, floor - i * 25, x + w / 2, floor - i * 25);
  g.fillStyle(0x8a7a4a, 1).fillTriangle(x - w / 2 - 24, floor - 150, x, floor - 235, x + w / 2 + 24, floor - 150);
  g.fillStyle(0xf9e79f, 1).fillRect(x - w / 4 - 22, floor - 110, 44, 36).fillRect(x + w / 4 - 22, floor - 110, 44, 36);
  g.fillStyle(0x3e2723, 1).fillRect(x - 24, floor - 80, 48, 80);
  scene.add.rectangle(x, floor - 175, 110, 28, 0x3e2723).setStrokeStyle(2, 0xc9a66b);
  addText(scene, x, floor - 175, sign, { size: 16, bold: true, color: '#f3e9d2' }).setOrigin(0.5);
}

function drawSnow(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = DESIGN;
  // Cold sky, far peaks with snowcaps, then a near ridge.
  scene.add.rectangle(0, 0, width, height, 0x9db4cc).setOrigin(0);
  scene.add.rectangle(0, groundY - 300, width, 300, 0xb9cadb).setOrigin(0);
  const peaks = scene.add.graphics();
  for (let i = 0; i < 7; i++) {
    const x = i * 210 - 40 + ((i * 53) % 70);
    const h = 300 + ((i * 97) % 160);
    const w = 300 + ((i * 41) % 120);
    peaks.fillStyle(0x6f8199, 1).fillTriangle(x - w / 2, groundY, x, groundY - h, x + w / 2, groundY);
    peaks.fillStyle(0xf2f6fa, 1).fillTriangle(x - w * 0.16, groundY - h * 0.68, x, groundY - h, x + w * 0.16, groundY - h * 0.68);
  }
  // Snowy pines.
  const pines = scene.add.graphics();
  for (let i = 0; i < 16; i++) {
    const x = (i * 83 + ((i * 29) % 40)) % width;
    const h = 110 + ((i * 37) % 90);
    const base = groundY + 4;
    pines.fillStyle(0x4a3527, 1).fillRect(x - 5, base - 20, 10, 20);
    for (let t = 0; t < 3; t++) {
      const y = base - 20 - t * (h / 3.2);
      const w = (h / 2.2) * (1 - t * 0.25);
      pines.fillStyle(0x2f5a46, 1).fillTriangle(x - w / 2, y, x, y - h / 2.4, x + w / 2, y);
      pines.fillStyle(0xf4f8fc, 1).fillTriangle(x - w / 4, y - h / 4.8, x, y - h / 2.4, x + w / 4, y - h / 4.8);
    }
  }
  // Snow on the ground.
  scene.add.rectangle(0, groundY, width, height - groundY, 0xe6eef6).setOrigin(0);
  scene.add.rectangle(0, groundY, width, 8, 0xb9cadb).setOrigin(0);
  const drifts = scene.add.graphics();
  for (let i = 0; i < 7; i++) {
    const x = 80 + i * 200 + ((i * 31) % 70);
    const y = groundY + 50 + ((i * 59) % (height - groundY - 80));
    drifts.fillStyle(0xd3e0ec, 1).fillEllipse(x, y, 220 + ((i * 23) % 80), 26);
  }
}

function drawVillage(scene: Phaser.Scene, groundY: number): void {
  drawSnow(scene, groundY);
  // Log cabins with snowy roofs.
  cabin(scene, groundY, 230, 'Ranger');
  cabin(scene, groundY, 620, 'Trader');
  cabin(scene, groundY, 1000, 'Hearth');
}

function cabin(scene: Phaser.Scene, groundY: number, x: number, sign: string): void {
  const g = scene.add.graphics();
  const w = 240;
  const h = 150;
  const top = groundY - h;
  g.fillStyle(0x6b4a30, 1).fillRect(x - w / 2, top, w, h);
  g.lineStyle(3, 0x4a3220, 1);
  for (let i = 1; i < 6; i++) g.lineBetween(x - w / 2, top + i * 25, x + w / 2, top + i * 25);
  g.fillStyle(0x3e2a1c, 1).fillTriangle(x - w / 2 - 24, top, x, top - 90, x + w / 2 + 24, top);
  g.fillStyle(0xf4f8fc, 1).fillTriangle(x - w / 2 - 20, top - 4, x, top - 84, x + w / 2 + 20, top - 4);
  g.fillStyle(0xffd27a, 1).fillRect(x - w / 4 - 22, top + 40, 44, 36).fillRect(x + w / 4 - 22, top + 40, 44, 36);
  g.fillStyle(0x3e2723, 1).fillRect(x - 26, groundY - 86, 52, 86);
  g.fillStyle(0x7a7a7a, 1).fillRect(x + w / 4, top - 70, 22, 50);
  scene.add.rectangle(x, top - 110, 110, 28, 0x3e2723).setStrokeStyle(2, 0xc9a66b);
  addText(scene, x, top - 110, sign, { size: 16, bold: true, color: '#f3e9d2' }).setOrigin(0.5);
}

function drawCave(scene: Phaser.Scene, groundY: number): void {
  const { width, height } = DESIGN;
  // Dark rock, warm glow from below.
  scene.add.rectangle(0, 0, width, height, 0x1e1a1c).setOrigin(0);
  scene.add.rectangle(0, groundY - 220, width, 220, 0x2c2224).setOrigin(0);
  const rock = scene.add.graphics();
  for (let i = 0; i < 18; i++) {
    const x = i * 75 + ((i * 37) % 40);
    const h = 60 + ((i * 53) % 140);
    rock.fillStyle(0x352a2c, 1).fillTriangle(x - 30, 0, x, h, x + 30, 0);
  }
  for (let i = 0; i < 9; i++) {
    const x = i * 150 + ((i * 47) % 60);
    const h = 70 + ((i * 61) % 110);
    rock.fillStyle(0x3d3032, 1).fillTriangle(x - 40, groundY, x, groundY - h, x + 40, groundY);
  }
  scene.add.rectangle(0, groundY, width, height - groundY, 0x3a2e2b).setOrigin(0);
  scene.add.rectangle(0, groundY, width, 8, 0x241c1a).setOrigin(0);
  // Hot springs glowing orange, with steam above.
  const pools = scene.add.graphics();
  for (let i = 0; i < 5; i++) {
    const x = 140 + i * 260 + ((i * 41) % 80);
    const y = groundY + 50 + ((i * 59) % (height - groundY - 90));
    pools.fillStyle(0xff7a2e, 0.85).fillEllipse(x, y, 190 + ((i * 29) % 70), 30);
    pools.fillStyle(0xffd27a, 0.6).fillEllipse(x - 20, y - 3, 90, 8);
    pools.fillStyle(0xffffff, 0.08).fillEllipse(x, y - 60, 160, 90);
  }
}
