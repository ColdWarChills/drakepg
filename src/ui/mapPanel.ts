import Phaser from 'phaser';
import { COLORS } from '../config';
import type { Player } from '../game/Player';
import { RECALL_SCROLL, recallCheck } from '../game/Travel';
import { areaMap, areaOf, knownAreas, type MapNode } from '../game/WorldMap';
import { Button } from './Button';
import { Modal } from './Modal';
import * as sfx from './sfx';
import { addText } from './text';

const W = 1180;
const H = 640;
/** Where the node graph is drawn inside the panel. */
const AREA = { x: 50, y: 130, w: W - 100, h: H - 260 };
const NODE_R = 30;
const GOLD = 0xf1c40f;
const DIM = 0x6f6578;

/** Remembered while the game runs: the page you looked at last, if it's not the one you stand on. */
let lastPage: string | null = null;

/**
 * The map (M or the Map button): one area at a time as dots and lines. Visited rooms show their name and icons;
 * the rooms next to them show as "?". Arrows lead to other pages. `onRecall` reads a Recall Scroll.
 */
export function openMap(scene: Phaser.Scene, player: Player, here: string, onRecall: () => void): Modal {
  const areas = knownAreas(player, here);
  let page = areas.some((a) => a.id === lastPage) ? lastPage! : areaOf(here).id;
  const modal = new Modal(scene, W, H, 'Map', () => {});
  // Looping tweens (markers, pulses) end with the page they belong to.
  const anim: Phaser.GameObjects.GameObject[] = [];
  const stopAnim = () => {
    scene.tweens.killTweensOf(anim);
    anim.length = 0;
  };
  modal.once(Phaser.GameObjects.Events.DESTROY, stopAnim);

  const render = () => {
    const c = modal.content;
    stopAnim();
    c.removeAll(true);
    const map = areaMap(player, page, here);
    c.add(addText(scene, W / 2, 70, map.area.name, { size: 22, bold: true, color: COLORS.text }).setOrigin(0.5));

    // Other pages you know: one button each.
    if (areas.length > 1) {
      const bw = 210;
      areas.forEach((a, i) => {
        const x = W / 2 + (i - (areas.length - 1) / 2) * (bw + 10);
        const b = new Button(scene, x, 104, a.name, () => ((page = a.id), (lastPage = page), sfx.play(scene, 'page'), render()), {
          width: bw,
          height: 32,
          fontSize: 13,
        });
        if (a.id === page) b.setEnabled(false);
        c.add(b);
      });
    }

    // Grid to screen, sized so the whole area (found or not) fits and stays put as you explore.
    const { minX, maxX, minY, maxY } = map.bounds;
    const cols = maxX - minX + 1;
    const rows = maxY - minY + 1;
    const cell = Math.min(AREA.w / (cols + 0.6), AREA.h / (rows + 0.4), 210);
    const ox = AREA.x + (AREA.w - (cols - 1) * cell) / 2;
    const oy = AREA.y + 20 + (AREA.h - (rows - 1) * cell) / 2;
    const at = (n: MapNode) => ({ x: ox + (n.x - minX) * cell, y: oy + (n.y - minY) * cell });

    const g = scene.add.graphics();
    c.add(g);
    for (const link of map.links) {
      const a = at(link.a);
      const b = at(link.b);
      if (link.open) g.lineStyle(7, 0x8a6d3b, 1).lineBetween(a.x, a.y, b.x, b.y);
      else dashed(g, a.x, a.y, b.x, b.y, 0x7a3b33);
    }

    // Exits to other pages: an arrow off the node with where it goes.
    for (const stub of map.stubs) {
      const p = at(stub.from);
      const [dx, dy] = stub.side === 'left' ? [-1, 0] : stub.side === 'right' ? [1, 0] : stub.side === 'down' ? [0, 1] : [0, -1];
      const len = Math.min(cell * 0.55, 90);
      const ex = p.x + dx * (NODE_R + len);
      const ey = p.y + dy * (NODE_R + len);
      if (stub.open) g.lineStyle(5, 0x8a6d3b, 1).lineBetween(p.x + dx * NODE_R, p.y + dy * NODE_R, ex, ey);
      else dashed(g, p.x + dx * NODE_R, p.y + dy * NODE_R, ex, ey, 0x7a3b33);
      const head = [
        new Phaser.Math.Vector2(ex + dx * 14, ey + dy * 14),
        new Phaser.Math.Vector2(ex - dy * 10, ey + dx * 10),
        new Phaser.Math.Vector2(ex + dy * 10, ey - dx * 10),
      ];
      g.fillStyle(stub.open ? GOLD : 0x7a3b33, 1).fillPoints(head, true);
      const known = areas.some((a) => a.id === stub.area.id);
      // Side arrows: the label sits under the arrow's tip, kept inside the panel on wide pages.
      const label = addText(scene, ex, dx ? ey + 14 : ey + dy * 28, stub.label, {
        size: 15,
        bold: true,
        color: known ? COLORS.textGold : COLORS.textDim,
        stroke: true,
      }).setOrigin(0.5, dy < 0 ? 1 : 0);
      label.x = Phaser.Math.Clamp(label.x, label.width / 2 + 12, W - label.width / 2 - 12);
      c.add(label);
      // A page you know: tap the arrow to turn to it.
      if (known) {
        label.setInteractive({ useHandCursor: true }).on('pointerup', () => {
          page = stub.area.id;
          lastPage = page;
          sfx.play(scene, 'page');
          render();
        });
      }
    }

    for (const node of map.nodes) drawNode(scene, c, node, at(node), anim, Math.min(170, cell - 12));

    legend(scene, c);
    recallRow(scene, c, player, here, () => (modal.close(), onRecall()));
  };
  render();
  return modal;
}

function drawNode(
  scene: Phaser.Scene,
  c: Phaser.GameObjects.Container,
  node: MapNode,
  p: { x: number; y: number },
  anim: Phaser.GameObjects.GameObject[],
  nameWrap: number,
): void {
  if (node.quest === 'target') {
    const ring = scene.add.circle(p.x, p.y, NODE_R + 10).setStrokeStyle(4, GOLD, 0.9);
    c.add(ring);
    anim.push(ring);
    scene.tweens.add({ targets: ring, scale: 1.15, alpha: 0.4, duration: 700, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  }
  if (node.here) {
    const pulse = scene.add.circle(p.x, p.y, NODE_R + 4, 0x7dff9a, 0.25);
    c.add(pulse);
    anim.push(pulse);
    scene.tweens.add({ targets: pulse, scale: 1.5, alpha: 0, duration: 1100, repeat: -1 });
  }
  const dot = scene.add
    .circle(p.x, p.y, NODE_R, node.visited ? 0x3b3350 : 0x15111c)
    .setStrokeStyle(node.here ? 5 : 3, node.here ? 0x7dff9a : node.visited ? 0x8a6d3b : DIM);
  c.add(dot);

  const g = scene.add.graphics();
  c.add(g);
  if (!node.visited) {
    c.add(addText(scene, p.x, p.y, '?', { size: 30, bold: true, color: COLORS.textDim }).setOrigin(0.5));
  } else if (node.icons.includes('town')) {
    drawHouse(g, p.x, p.y);
  } else if (node.icons.includes('boss')) {
    c.add(addText(scene, p.x, p.y + 1, '☠', { size: 30, color: COLORS.textBad }).setOrigin(0.5));
  } else {
    g.fillStyle(0x8a6d3b, 1).fillCircle(p.x, p.y, 7);
  }
  if (node.visited && node.icons.includes('campfire')) drawFlame(g, p.x + NODE_R * 0.75, p.y + NODE_R * 0.7, 0.8);

  if (node.visited) {
    const name = addText(scene, p.x, p.y + NODE_R + 8, node.room.name, { size: 15, bold: node.here, color: node.here ? '#b8ffcb' : COLORS.text, stroke: true, align: 'center', wrap: nameWrap }).setOrigin(0.5, 0);
    c.add(name);
  }
  if (node.here) c.add(addText(scene, p.x, p.y - NODE_R - 6, 'You', { size: 15, bold: true, color: '#b8ffcb', stroke: true }).setOrigin(0.5, 1));
  if (node.quest === '!' || node.quest === '?') {
    const mark = addText(scene, p.x + NODE_R * 0.8, p.y - NODE_R * 0.9, node.quest, { size: 28, bold: true, color: COLORS.textGold, stroke: true }).setOrigin(0.5);
    c.add(mark);
    anim.push(mark);
    scene.tweens.add({ targets: mark, y: mark.y - 4, duration: 500, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  }
}

function drawHouse(g: Phaser.GameObjects.Graphics, x: number, y: number, s = 1): void {
  g.fillStyle(0xd9b46a, 1).fillRect(x - 11 * s, y - 2 * s, 22 * s, 15 * s);
  g.fillStyle(0xa8432f, 1).fillTriangle(x - 15 * s, y - 1 * s, x + 15 * s, y - 1 * s, x, y - 15 * s);
  g.fillStyle(0x3e2723, 1).fillRect(x - 3 * s, y + 5 * s, 6 * s, 8 * s);
}

function drawFlame(g: Phaser.GameObjects.Graphics, x: number, y: number, s = 1): void {
  g.fillStyle(0x6d4c33, 1).fillRect(x - 10 * s, y + 6 * s, 20 * s, 5 * s);
  g.fillStyle(0xff7a1a, 1).fillTriangle(x - 8 * s, y + 7 * s, x + 8 * s, y + 7 * s, x, y - 14 * s);
  g.fillStyle(0xffd75e, 1).fillTriangle(x - 4 * s, y + 7 * s, x + 4 * s, y + 7 * s, x, y - 5 * s);
}

/** A blocked way: a dashed red line. */
function dashed(g: Phaser.GameObjects.Graphics, x1: number, y1: number, x2: number, y2: number, color: number): void {
  const len = Math.hypot(x2 - x1, y2 - y1);
  const steps = Math.max(1, Math.floor(len / 14));
  g.lineStyle(5, color, 1);
  for (let i = 0; i < steps; i += 2) {
    const a = i / steps;
    const b = Math.min(1, (i + 1) / steps);
    g.lineBetween(x1 + (x2 - x1) * a, y1 + (y2 - y1) * a, x1 + (x2 - x1) * b, y1 + (y2 - y1) * b);
  }
}

function legend(scene: Phaser.Scene, c: Phaser.GameObjects.Container): void {
  const y = H - 95;
  const g = scene.add.graphics();
  c.add(g);
  let x = 50;
  const item = (draw: (x: number) => void, text: string, color: string = COLORS.textDim) => {
    draw(x + 14);
    const t = addText(scene, x + 34, y, text, { size: 14, color }).setOrigin(0, 0.5);
    c.add(t);
    x += 34 + t.width + 26;
  };
  item((cx) => g.fillStyle(0x7dff9a, 1).fillCircle(cx, y, 9), 'You');
  item((cx) => c.add(addText(scene, cx, y, '?', { size: 20, bold: true, color: COLORS.textDim }).setOrigin(0.5)), 'Not explored');
  item((cx) => drawHouse(g, cx, y + 2, 0.75), 'Town');
  item((cx) => c.add(addText(scene, cx, y, '☠', { size: 20, color: COLORS.textBad }).setOrigin(0.5)), 'Boss');
  item((cx) => drawFlame(g, cx, y, 0.7), 'Campfire');
  item((cx) => g.lineStyle(3, GOLD, 1).strokeCircle(cx, y, 9), 'Quest or hunt');
  item((cx) => c.add(addText(scene, cx, y, '!', { size: 20, bold: true, color: COLORS.textGold }).setOrigin(0.5)), 'New quest');
  item((cx) => dashed(g, cx - 12, y, cx + 12, y, 0x7a3b33), 'Blocked');
}

/** "Recall to Brindlemoor (5 scrolls)" or why you can't. */
function recallRow(scene: Phaser.Scene, c: Phaser.GameObjects.Container, player: Player, here: string, read: () => void): void {
  const y = H - 45;
  const left = player.countItem(RECALL_SCROLL);
  const check = recallCheck(player, here);
  const label = check.ok ? `Recall to ${check.town.name}` : 'Recall Scroll';
  const btn = new Button(scene, W - 190, y, label, read, { width: 300, height: 48, fontSize: 17, sublabel: `${left} scroll${left === 1 ? '' : 's'} left` });
  btn.setEnabled(check.ok);
  c.add(btn);
  const note = check.ok ? 'One way: you walk back out. Shops sell more scrolls.' : check.reason;
  c.add(addText(scene, 50, y, note, { size: 15, color: COLORS.textDim, wrap: 760 }).setOrigin(0, 0.5));
}
