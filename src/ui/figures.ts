import Phaser from 'phaser';
import type { Look, WeaponDef } from '../data/types';
import type { SigilTier } from '../game/Items';
import { hasArt, pickArt } from './art';
import { addSigilGlow } from './itemUi';

/** On-screen size of a scale-1 character drawn from art. */
const ART_HEIGHT = 240;
const ART_MAX_WIDTH = 300;

/** Triangle in container-local coordinates. */
function tri(scene: Phaser.Scene, color: number, x1: number, y1: number, x2: number, y2: number, x3: number, y3: number) {
  return scene.add.graphics().fillStyle(color).fillTriangle(x1, y1, x2, y2, x3, y3);
}

/**
 * A character, origin at the feet. Uses `look.art` when that image exists in src/assets/art/,
 * otherwise draws placeholder shapes. Scenes only depend on the returned Container.
 * With a `weapon` that has art (and a look with bare art), the weapon is drawn in the figure's hands;
 * the weapon images are stored as `c.getData('weapons')` so battle animations can swing them.
 * A Sigil weapon glows in its tier color.
 */
export function drawFigure(scene: Phaser.Scene, x: number, y: number, look: Look, facing: 1 | -1 = 1, weapon?: WeaponDef | null, sigil?: SigilTier) {
  const c = scene.add.container(x, y);
  const { color, accent } = look;

  const bares = look.bare === undefined ? [] : Array.isArray(look.bare) ? look.bare : [look.bare];
  const bare = bares.find((b) => hasArt(scene, b.art));
  if (bare && weapon?.art && hasArt(scene, weapon.art)) {
    const img = scene.add.image(0, 0, bare.art).setOrigin(0.5, 1);
    const s = Math.min(ART_HEIGHT / img.height, ART_MAX_WIDTH / img.width);
    img.setScale(s);
    const weapons = bare.hands.map((hand) => {
      const w = scene.add.image((hand.x - img.width / 2) * s, (hand.y - img.height) * s, weapon.art!);
      w.setOrigin(0.5, weapon.grip ?? 0.85).setAngle(hand.angle);
      w.setScale(((weapon.size ?? 0.6) * img.displayHeight) / w.height);
      w.setData('angle', hand.angle);
      if (sigil) addSigilGlow(scene, w, sigil);
      return w;
    });
    // Weapons go behind the body so the fists cover the grips.
    c.add([scene.add.ellipse(0, 0, img.displayWidth * 0.7, 22, 0x000000, 0.35), ...weapons]);
    if (sigil === 'eclipse') c.add(eclipseMotes(scene, weapons[0], weapon.grip ?? 0.85));
    c.add(img);
    c.setData('weapons', weapons);
    c.setScale(look.scale * facing, look.scale);
    return c;
  }

  const art = pickArt(scene, look.art);
  if (art) {
    // Art is drawn facing right; flip for left-facing figures.
    const img = scene.add.image(0, 0, art).setOrigin(0.5, 1).setFlipX(facing === -1);
    img.setScale(Math.min(ART_HEIGHT / img.height, ART_MAX_WIDTH / img.width));
    c.add([scene.add.ellipse(0, 0, img.displayWidth * 0.7, 22, 0x000000, 0.35), img]);
    c.setScale(look.scale);
    return c;
  }

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


/** Slow embers drifting off the blade of an Eclipse weapon. */
function eclipseMotes(scene: Phaser.Scene, w: Phaser.GameObjects.Image, grip: number): Phaser.GameObjects.Particles.ParticleEmitter {
  // From the grip toward the tip along the weapon's angle (0 = up).
  const reach = w.displayHeight * grip;
  const a = Phaser.Math.DegToRad(w.angle);
  const sin = Math.sin(a);
  const cos = Math.cos(a);
  return scene.add.particles(w.x, w.y, 'fx_soft', {
    emitZone: {
      type: 'random',
      source: {
        getRandomPoint: (p) => {
          const t = 0.2 + Math.random() * 0.8;
          p.x = sin * reach * t;
          p.y = -cos * reach * t;
        },
      },
    },
    speedY: { min: -40, max: -15 },
    speedX: { min: -10, max: 10 },
    scale: { start: 0.6, end: 0 },
    alpha: { start: 1, end: 0 },
    lifespan: 1000,
    frequency: 60,
    tint: [0xff3358, 0xff8a9c, 0xffd0d8],
    blendMode: 'ADD',
  });
}
