import Phaser from 'phaser';
import { ELEMENT_COLORS } from '../data/elements';
import type { Ambient, Element } from '../data/types';

/**
 * Juice: particles, slashes, projectiles, flashes, hit-stop. Textures are drawn in code at boot
 * (createFxTextures), so effects work without any art.
 */

type Emitter = Phaser.GameObjects.Particles.ParticleEmitter;
type EmitterConfig = Phaser.Types.GameObjects.Particles.ParticleEmitterConfig;
type Point = { x: number; y: number };

const TEX = {
  soft: 'fx_soft',
  spark: 'fx_spark',
  star: 'fx_star',
  leaf: 'fx_leaf',
  coin: 'fx_coin',
  ring: 'fx_ring',
  rock: 'fx_rock',
  plus: 'fx_plus',
  chevron: 'fx_chevron',
  streak: 'fx_streak',
} as const;

/** Particles that point where they fly (shards, droplets, streaks). The texture's long side is vertical. */
const ALONG_VELOCITY = { onEmit: () => 0, onUpdate: (p: Phaser.GameObjects.Particles.Particle) => Phaser.Math.RadToDeg(Math.atan2(p.velocityY, p.velocityX)) + 90 };

/** Emit from random points inside a rectangle (relative to the emitter). */
function randomRect(x: number, y: number, w: number, h: number): Phaser.Types.GameObjects.Particles.ParticleEmitterRandomZoneConfig {
  return {
    type: 'random',
    source: {
      getRandomPoint: (p) => {
        p.x = x + Math.random() * w;
        p.y = y + Math.random() * h;
      },
    },
  };
}

export function createFxTextures(scene: Phaser.Scene): void {
  if (scene.textures.exists(TEX.soft)) return;
  const g = scene.add.graphics();

  // Soft glowing dot.
  for (let r = 16; r > 0; r--) {
    g.fillStyle(0xffffff, 0.12 + (1 - r / 16) * 0.25);
    g.fillCircle(16, 16, r);
  }
  g.generateTexture(TEX.soft, 32, 32);
  g.clear();

  // Spark: thin diamond.
  g.fillStyle(0xffffff, 1);
  g.fillTriangle(4, 0, 8, 12, 0, 12);
  g.fillTriangle(0, 12, 8, 12, 4, 24);
  g.generateTexture(TEX.spark, 8, 24);
  g.clear();

  // Four-point star.
  g.fillStyle(0xffffff, 1);
  g.fillTriangle(12, 0, 15, 12, 9, 12);
  g.fillTriangle(12, 24, 15, 12, 9, 12);
  g.fillTriangle(0, 12, 12, 9, 12, 15);
  g.fillTriangle(24, 12, 12, 9, 12, 15);
  g.fillCircle(12, 12, 3);
  g.generateTexture(TEX.star, 24, 24);
  g.clear();

  // Leaf.
  g.fillStyle(0xffffff, 1);
  g.fillEllipse(9, 6, 18, 9);
  g.generateTexture(TEX.leaf, 18, 12);
  g.clear();

  // Coin.
  g.fillStyle(0x9a6b00, 1);
  g.fillCircle(10, 10, 10);
  g.fillStyle(0xf1c40f, 1);
  g.fillCircle(10, 10, 8);
  g.fillStyle(0xfff3a0, 1);
  g.fillCircle(7, 7, 3);
  g.generateTexture(TEX.coin, 20, 20);
  g.clear();

  // Ring for shockwaves.
  g.lineStyle(6, 0xffffff, 1);
  g.strokeCircle(32, 32, 28);
  g.generateTexture(TEX.ring, 64, 64);
  g.clear();

  // Rock chunk (earth hits), lit from the top left.
  g.fillStyle(0xbbbbbb, 1);
  g.fillPoints([new Phaser.Math.Vector2(3, 6), new Phaser.Math.Vector2(9, 1), new Phaser.Math.Vector2(16, 3), new Phaser.Math.Vector2(19, 11), new Phaser.Math.Vector2(13, 18), new Phaser.Math.Vector2(4, 16)], true);
  g.fillStyle(0xffffff, 1);
  g.fillPoints([new Phaser.Math.Vector2(4, 7), new Phaser.Math.Vector2(9, 3), new Phaser.Math.Vector2(14, 5), new Phaser.Math.Vector2(9, 10)], true);
  g.generateTexture(TEX.rock, 20, 20);
  g.clear();

  // Plus sign (healing).
  g.fillStyle(0xffffff, 1);
  g.fillRoundedRect(7, 0, 8, 22, 3);
  g.fillRoundedRect(0, 7, 22, 8, 3);
  g.generateTexture(TEX.plus, 22, 22);
  g.clear();

  // Chevron pointing up (buffs rise, debuffs sink flipped).
  g.fillStyle(0xffffff, 1);
  g.fillPoints([new Phaser.Math.Vector2(0, 14), new Phaser.Math.Vector2(14, 0), new Phaser.Math.Vector2(28, 14), new Phaser.Math.Vector2(28, 22), new Phaser.Math.Vector2(14, 8), new Phaser.Math.Vector2(0, 22)], true);
  g.generateTexture(TEX.chevron, 28, 22);
  g.clear();

  // Long thin streak (speed lines, crit rays).
  for (let i = 0; i < 4; i++) {
    g.fillStyle(0xffffff, 0.25 + i * 0.25);
    g.fillEllipse(48, 3, 96 - i * 20, 6 - i);
  }
  g.generateTexture(TEX.streak, 96, 6);
  g.destroy();
}

/** One-shot particle burst that cleans itself up. */
export function burst(scene: Phaser.Scene, x: number, y: number, texture: string, count: number, config: EmitterConfig): Emitter {
  const emitter = scene.add.particles(x, y, texture, { ...config, emitting: false });
  emitter.setDepth(900);
  emitter.explode(count);
  const life = typeof config.lifespan === 'number' ? config.lifespan : 1500;
  scene.time.delayedCall(life + 200, () => emitter.destroy());
  return emitter;
}

/** Expanding ring. */
export function shockwave(scene: Phaser.Scene, x: number, y: number, color: number, size = 1.6, squash = 0.45): void {
  const ring = scene.add.image(x, y, TEX.ring).setTint(color).setDepth(899).setScale(0.2, 0.2 * squash);
  scene.tweens.add({
    targets: ring,
    scaleX: size,
    scaleY: size * squash,
    alpha: { from: 1, to: 0 },
    duration: 320,
    ease: 'Cubic.easeOut',
    onComplete: () => ring.destroy(),
  });
}

/** Sparks where a hit lands. */
export function hitSparks(scene: Phaser.Scene, x: number, y: number, magic: boolean, crit: boolean, palette?: number[]): void {
  const colors = palette ?? (magic ? [0xffffff, 0x9fd3ff, 0x6c63ff] : [0xffffff, 0xfff1a8, 0xffb347]);
  burst(scene, x, y, TEX.spark, crit ? 26 : 14, {
    speed: { min: 180, max: crit ? 560 : 420 },
    angle: { min: 0, max: 360 },
    rotate: { min: 0, max: 360 },
    scale: { start: crit ? 1.2 : 0.9, end: 0 },
    lifespan: 420,
    color: colors,
    blendMode: 'ADD',
  });
  burst(scene, x, y, TEX.soft, crit ? 10 : 5, {
    speed: { min: 20, max: 120 },
    scale: { start: crit ? 2.2 : 1.5, end: 0 },
    alpha: { start: 0.9, end: 0 },
    lifespan: 300,
    tint: palette ? colors[1] : magic ? 0x9fd3ff : 0xffe7a0,
    blendMode: 'ADD',
  });
  shockwave(scene, x, y, palette ? colors[1] : magic ? 0x9fd3ff : 0xffffff, crit ? 2.2 : 1.4, 1);
}

/** A crescent sweep across a target. `dir` 1 = swinging from the left. */
export function slash(scene: Phaser.Scene, x: number, y: number, dir: 1 | -1, color = 0xffffff, tilt = 0, big = false): void {
  const g = scene.add.graphics().setPosition(x, y).setDepth(901);
  const r = big ? 120 : 90;
  g.lineStyle(big ? 22 : 14, color, 0.9);
  g.beginPath();
  g.arc(0, 0, r, -1.1, 1.1);
  g.strokePath();
  g.lineStyle(big ? 8 : 5, 0xffffff, 1);
  g.beginPath();
  g.arc(0, 0, r - 4, -0.9, 0.9);
  g.strokePath();
  // Starts near full size: a hit-stop freezes tweens right after contact, and the frozen frame should show the cut.
  g.setScale(0.8 * dir, 0.8).setRotation(-0.5 * dir + tilt).setBlendMode(Phaser.BlendModes.ADD);
  scene.tweens.add({
    targets: g,
    scaleX: 1.15 * dir,
    scaleY: 1.15,
    rotation: 0.5 * dir + tilt,
    alpha: { from: 1, to: 0 },
    duration: 240,
    ease: 'Cubic.easeOut',
    onComplete: () => g.destroy(),
  });
}

/**
 * A spell flying from `from` to `to`; resolves when it lands. Its shape follows the element: a glowing orb (fire,
 * water, light, dark, neutral), an ice shard, a tumbling rock, a spinning gust, or an instant lightning bolt.
 */
export function projectile(
  scene: Phaser.Scene,
  from: Point,
  to: Point,
  colors: number[],
  duration = 420,
  element?: Element,
): Promise<void> {
  if (element === 'energy') {
    // Lightning doesn't fly: it cracks across in a blink.
    return new Promise((resolve) => {
      lightning(scene, from, to, colors[1], 1);
      scene.time.delayedCall(70, () => lightning(scene, from, to, colors[2] ?? colors[1], 0.6));
      scene.time.delayedCall(Math.min(150, duration / 2), () => {
        explosion(scene, to.x, to.y, colors);
        resolve();
      });
    });
  }
  return new Promise((resolve) => {
    const kind = element === 'ice' ? 'shard' : element === 'earth' ? 'rock' : element === 'wind' ? 'gust' : 'orb';
    const tex = kind === 'shard' ? TEX.spark : kind === 'rock' ? TEX.rock : kind === 'gust' ? TEX.ring : TEX.soft;
    const core = scene.add.image(from.x, from.y, tex).setDepth(902);
    if (kind === 'rock') core.setTint(colors[1]).setScale(2.4);
    else core.setTint(colors[1] ?? colors[0]).setBlendMode('ADD').setScale(kind === 'shard' ? 1.8 : kind === 'gust' ? 1.2 : 2.2);
    const trail = scene.add.particles(0, 0, TEX.soft, {
      follow: core,
      lifespan: kind === 'rock' ? 260 : 320,
      speed: { min: 5, max: 40 },
      scale: { start: kind === 'shard' ? 0.9 : 1.6, end: 0 },
      alpha: { start: kind === 'rock' ? 0.5 : 0.9, end: 0 },
      color: kind === 'rock' ? [0xd9c8a9, 0x8a6a2c] : colors,
      blendMode: kind === 'rock' ? 'NORMAL' : 'ADD',
      frequency: 12,
    });
    trail.setDepth(901);
    // Rocks are lobbed high, shards and gusts fly nearly straight.
    const lift = kind === 'rock' ? 160 : kind === 'orb' ? 70 : 20;
    const arc = { t: 0 };
    scene.tweens.add({
      targets: arc,
      t: 1,
      duration,
      ease: kind === 'rock' ? 'Linear' : 'Sine.easeIn',
      onUpdate: () => {
        const px = core.x;
        const py = core.y;
        core.x = Phaser.Math.Linear(from.x, to.x, arc.t);
        core.y = Phaser.Math.Linear(from.y, to.y, arc.t) - Math.sin(arc.t * Math.PI) * lift;
        if (kind === 'shard') core.setRotation(Math.atan2(core.y - py, core.x - px) + Math.PI / 2);
        else if (kind === 'rock') core.setAngle(arc.t * 540);
        else if (kind === 'gust') core.setAngle(arc.t * 1080).setScale(1.2 + arc.t * 0.6, (1.2 + arc.t * 0.6) * 0.45);
        else core.setScale(2.2 + Math.sin(arc.t * 40) * 0.25);
      },
      onComplete: () => {
        core.destroy();
        trail.stop();
        scene.time.delayedCall(400, () => trail.destroy());
        explosion(scene, to.x, to.y, colors);
        resolve();
      },
    });
  });
}

/** A jagged lightning bolt between two points that flickers out. `width` 1 = a full bolt. */
export function lightning(scene: Phaser.Scene, from: Point, to: Point, color: number, width = 1): void {
  const g = scene.add.graphics().setDepth(903).setBlendMode(Phaser.BlendModes.ADD);
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const len = Math.hypot(dx, dy);
  const nx = -dy / len;
  const ny = dx / len;
  const steps = Math.max(5, Math.round(len / 40));
  const points: Phaser.Math.Vector2[] = [];
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const off = i === 0 || i === steps ? 0 : Phaser.Math.Between(-26, 26);
    points.push(new Phaser.Math.Vector2(from.x + dx * t + nx * off, from.y + dy * t + ny * off));
  }
  g.lineStyle(14 * width, color, 0.35);
  g.strokePoints(points);
  g.lineStyle(6 * width, color, 0.9);
  g.strokePoints(points);
  g.lineStyle(2.5 * width, 0xffffff, 1);
  g.strokePoints(points);
  scene.tweens.add({ targets: g, alpha: { from: 1, to: 0 }, duration: 200, ease: 'Stepped', easeParams: [3], onComplete: () => g.destroy() });
}

export function explosion(scene: Phaser.Scene, x: number, y: number, colors: number[]): void {
  burst(scene, x, y, TEX.soft, 24, {
    speed: { min: 60, max: 320 },
    scale: { start: 2.2, end: 0 },
    alpha: { start: 1, end: 0 },
    lifespan: 520,
    color: colors,
    blendMode: 'ADD',
  });
  burst(scene, x, y, TEX.spark, 12, {
    speed: { min: 200, max: 500 },
    rotate: { min: 0, max: 360 },
    scale: { start: 0.9, end: 0 },
    lifespan: 380,
    tint: colors[0],
    blendMode: 'ADD',
  });
  shockwave(scene, x, y, colors[0], 2.4, 1);
}

/** Cast-up glow at a caster's hands. */
export function chargeUp(scene: Phaser.Scene, x: number, y: number, color: number, duration = 300): Promise<void> {
  return new Promise((resolve) => {
    const glow = scene.add.image(x, y, TEX.soft).setTint(color).setScale(0.3).setDepth(902).setBlendMode('ADD');
    burst(scene, x, y, TEX.soft, 10, {
      emitZone: { type: 'edge' as const, source: new Phaser.Geom.Circle(0, 0, 70), quantity: 10 },
      moveToX: 0,
      moveToY: 0,
      lifespan: duration,
      scale: { start: 0.9, end: 0.2 },
      tint: color,
      blendMode: 'ADD',
    });
    scene.tweens.add({
      targets: glow,
      scale: 3,
      alpha: { from: 0.4, to: 1 },
      duration,
      ease: 'Cubic.easeIn',
      onComplete: () => {
        glow.destroy();
        resolve();
      },
    });
  });
}

/** Sparkles rising from a character's feet. */
export function healSparkles(scene: Phaser.Scene, x: number, y: number, color = 0x7dff9a): void {
  burst(scene, x, y, TEX.soft, 26, {
    emitZone: randomRect(-60, -20, 120, 30),
    speedY: { min: -220, max: -80 },
    speedX: { min: -20, max: 20 },
    scale: { start: 1.1, end: 0 },
    alpha: { start: 1, end: 0 },
    lifespan: 900,
    tint: color,
    blendMode: 'ADD',
  });
  burst(scene, x, y - 80, TEX.star, 8, {
    emitZone: randomRect(-60, -80, 120, 140),
    speed: { min: 10, max: 40 },
    scale: { start: 0.9, end: 0 },
    rotate: { min: 0, max: 180 },
    lifespan: 800,
    tint: 0xffffff,
    blendMode: 'ADD',
  });
  shockwave(scene, x, y, color, 2, 0.35);
}

/** Puff of smoke + chunks in the monster's color. */
export function deathPoof(scene: Phaser.Scene, x: number, y: number, color: number): void {
  burst(scene, x, y, TEX.soft, 22, {
    emitZone: randomRect(-70, -60, 140, 120),
    speed: { min: 20, max: 90 },
    speedY: { min: -120, max: -30 },
    scale: { start: 2.6, end: 0.4 },
    alpha: { start: 0.7, end: 0 },
    lifespan: 900,
    tint: [0xdddddd, 0xbbbbbb, 0x999999],
  });
  burst(scene, x, y, TEX.soft, 16, {
    speed: { min: 150, max: 380 },
    angle: { min: 200, max: 340 },
    gravityY: 900,
    scale: { start: 0.8, end: 0.3 },
    lifespan: 800,
    tint: color,
  });
}

/** Coins pop out of `from` then fly to `to`. Resolves when the last one lands. */
export function coinShower(
  scene: Phaser.Scene,
  from: { x: number; y: number },
  to: { x: number; y: number },
  count: number,
  onLand?: () => void,
): Promise<void> {
  const n = Phaser.Math.Clamp(count, 1, 14);
  const flights: Promise<void>[] = [];
  for (let i = 0; i < n; i++) {
    flights.push(
      new Promise((resolve) => {
        const coin = scene.add.image(from.x, from.y, TEX.coin).setDepth(950).setScale(0);
        const popX = from.x + Phaser.Math.Between(-90, 90);
        const popY = from.y + Phaser.Math.Between(-140, -40);
        scene.tweens.chain({
          targets: coin,
          tweens: [
            { x: popX, y: popY, scale: 1.3, duration: 260, ease: 'Back.easeOut' },
            { x: to.x, y: to.y, scale: 0.7, duration: 480, delay: 120 + i * 45, ease: 'Cubic.easeIn' },
          ],
          onComplete: () => {
            coin.destroy();
            burst(scene, to.x, to.y, TEX.star, 3, {
              speed: { min: 40, max: 120 },
              scale: { start: 0.6, end: 0 },
              lifespan: 300,
              tint: 0xfff3a0,
              blendMode: 'ADD',
            });
            onLand?.();
            resolve();
          },
        });
      }),
    );
  }
  return Promise.all(flights).then(() => undefined);
}

/** Golden pillar + star burst. */
export function levelUpBurst(scene: Phaser.Scene, x: number, y: number): void {
  const pillar = scene.add.image(x, y - 180, TEX.soft).setTint(0xffd75e).setBlendMode('ADD').setDepth(898);
  pillar.setScale(0.5, 0.5).setAlpha(0);
  scene.tweens.chain({
    targets: pillar,
    tweens: [
      { scaleX: 4, scaleY: 18, alpha: 0.9, duration: 260, ease: 'Cubic.easeOut' },
      { scaleX: 2.5, alpha: 0, duration: 900, ease: 'Sine.easeIn' },
    ],
    onComplete: () => pillar.destroy(),
  });
  burst(scene, x, y - 120, TEX.star, 30, {
    speed: { min: 120, max: 420 },
    angle: { min: 200, max: 340 },
    gravityY: 300,
    rotate: { min: 0, max: 360 },
    scale: { start: 1.2, end: 0 },
    lifespan: 1200,
    color: [0xffffff, 0xffe066, 0xffb300],
    blendMode: 'ADD',
  });
  shockwave(scene, x, y, 0xffd75e, 3, 0.35);
}

/** Confetti rain over the whole screen. */
export function confetti(scene: Phaser.Scene): void {
  const { width } = scene.scale;
  burst(scene, width / 2, -20, TEX.leaf, 60, {
    emitZone: randomRect(-width / 2, 0, width, 10),
    speedY: { min: 120, max: 320 },
    speedX: { min: -60, max: 60 },
    rotate: { start: 0, end: 720 },
    scale: { min: 0.6, max: 1.1 },
    lifespan: 2600,
    tint: [0xf1c40f, 0xe74c3c, 0x3498db, 0x2ecc71, 0x9b59b6, 0xffffff],
  });
}

/** Little dust puff at the feet (footsteps, landings). */
export function dust(scene: Phaser.Scene, x: number, y: number, amount = 3, depth = 0): void {
  const e = burst(scene, x, y, TEX.soft, amount, {
    speedX: { min: -50, max: 50 },
    speedY: { min: -40, max: -10 },
    scale: { start: 0.9, end: 0.2 },
    alpha: { start: 0.45, end: 0 },
    lifespan: 450,
    tint: 0xd9c8a9,
  });
  e.setDepth(depth);
}

/**
 * Hit-stop: freeze tweens and timers for a moment so impacts land with weight.
 * Uses real time, since scene time is what gets frozen.
 */
export function hitStop(scene: Phaser.Scene, ms: number): void {
  scene.tweens.timeScale = 0.02;
  scene.time.timeScale = 0.02;
  // Restored even if the scene has stopped meanwhile: a restarted scene reuses the same tween and time managers.
  window.setTimeout(() => {
    if (scene.tweens) scene.tweens.timeScale = 1;
    if (scene.time) scene.time.timeScale = 1;
  }, ms);
}

/**
 * Flash a figure (Container from drawFigure) one solid color for a moment: white by default, or `then` right after
 * the white (an element's color, a heal's green).
 */
export function whiteFlash(scene: Phaser.Scene, fig: Phaser.GameObjects.Container, ms = 90, color = 0xffffff, then?: number): void {
  // Flashes can overlap (combo hits): the look from before the first one is kept, and only the last one restores it.
  let restore = fig.getData('flashRestore') as (() => void)[] | undefined;
  if (!restore) {
    restore = [];
    for (const child of fig.list) {
      if (child instanceof Phaser.GameObjects.Image) {
        // A Phase form's tint (or none) comes back afterwards.
        const tint = child.isTinted ? child.tintTopLeft : null;
        restore.push(() => (tint === null ? child.clearTint() : child.setTint(tint)).setTintMode(Phaser.TintModes.MULTIPLY));
      } else if (child instanceof Phaser.GameObjects.Shape && child.isFilled && child.fillAlpha === 1) {
        const fill = child.fillColor;
        restore.push(() => child.setFillStyle(fill));
      }
    }
    fig.setData('flashRestore', restore);
  }
  const paint = (c: number) => {
    for (const child of fig.list) {
      if (child instanceof Phaser.GameObjects.Image) child.setTint(c).setTintMode(Phaser.TintModes.FILL);
      else if (child instanceof Phaser.GameObjects.Shape && child.isFilled && child.fillAlpha === 1) child.setFillStyle(c);
    }
  };
  const token = ((fig.getData('flashToken') as number | undefined) ?? 0) + 1;
  fig.setData('flashToken', token);
  const latest = () => fig.active && fig.getData('flashToken') === token;
  paint(color);
  if (then !== undefined) scene.time.delayedCall(ms / 2, () => latest() && paint(then));
  scene.time.delayedCall(ms, () => {
    if (!latest()) return;
    restore.forEach((r) => r());
    fig.setData('flashRestore', undefined);
  });
}

/** A see-through silhouette of a figure that fades out (lunges, dodges). The caller draws the copy; this fades it. */
export function fadeGhost(scene: Phaser.Scene, ghost: Phaser.GameObjects.Container, color: number, alpha = 0.45, ms = 260): void {
  for (const child of ghost.list) {
    if (child instanceof Phaser.GameObjects.Image) child.setTint(color).setTintMode(Phaser.TintModes.FILL);
    else if (child instanceof Phaser.GameObjects.Shape) {
      if (child.isFilled && child.fillAlpha === 1) child.setFillStyle(color);
      else child.setVisible(false);
    } else if (child instanceof Phaser.GameObjects.Graphics) child.setVisible(false);
  }
  ghost.setAlpha(alpha);
  scene.tweens.add({ targets: ghost, alpha: 0, duration: ms, ease: 'Quad.easeIn', onComplete: () => ghost.destroy() });
}

/** Wind lines streaking behind a fast-moving figure. `dir` 1 = moving right. */
export function speedLines(scene: Phaser.Scene, x: number, y: number, dir: 1 | -1, color = 0xffffff): void {
  burst(scene, x, y, TEX.streak, 7, {
    emitZone: randomRect(-30, -150, 60, 140),
    speedX: { min: -900 * dir, max: -500 * dir },
    scaleX: { start: 1.1, end: 0.3 },
    scaleY: 0.8,
    alpha: { start: 0.55, end: 0 },
    lifespan: 200,
    tint: color,
    blendMode: 'ADD',
  });
}

/** Three parallel claw rakes (monster melee). `dir` 1 = raking from the left. */
export function clawMarks(scene: Phaser.Scene, x: number, y: number, dir: 1 | -1, color: number): void {
  for (let i = 0; i < 3; i++) {
    const g = scene.add.graphics().setPosition(x + (i - 1) * 26 * dir, y + (i - 1) * 8).setDepth(901).setBlendMode(Phaser.BlendModes.ADD);
    g.lineStyle(12, color, 0.75);
    g.lineBetween(-50 * dir, -70, 50 * dir, 70);
    g.lineStyle(4, 0xffffff, 1);
    g.lineBetween(-44 * dir, -60, 44 * dir, 60);
    g.setScale(0.75);
    scene.tweens.add({
      targets: g,
      scale: 1.05,
      alpha: { from: 1, to: 0 },
      delay: i * 30,
      duration: 230,
      ease: 'Cubic.easeOut',
      onComplete: () => g.destroy(),
    });
  }
}

/** Critical hit: a bright star flash with rays bursting out around it. */
export function critFlare(scene: Phaser.Scene, x: number, y: number, color: number): void {
  const flash = scene.add.image(x, y, TEX.star).setTint(0xffffff).setBlendMode('ADD').setDepth(903).setScale(3).setAngle(Phaser.Math.Between(0, 45));
  scene.tweens.add({ targets: flash, scale: 7, alpha: { from: 1, to: 0 }, angle: flash.angle + 30, duration: 260, ease: 'Cubic.easeOut', onComplete: () => flash.destroy() });
  burst(scene, x, y, TEX.streak, 14, {
    speed: { min: 700, max: 1100 },
    angle: { min: 0, max: 360 },
    rotate: { onEmit: () => 0, onUpdate: (p) => Phaser.Math.RadToDeg(Math.atan2(p.velocityY, p.velocityX)) },
    scaleX: { start: 1.4, end: 0.4 },
    scaleY: 1.4,
    alpha: { start: 1, end: 0 },
    lifespan: 220,
    color: [0xffffff, color],
    blendMode: 'ADD',
  });
}

/**
 * What an element looks like when it lands, on top of the normal sparks. `power` 0-1 scales the size
 * (a small Burn tick is about 0.3, a crit or weak hit 1).
 */
export function elementBurst(scene: Phaser.Scene, x: number, y: number, element: Element, power = 0.6): void {
  const c = ELEMENT_COLORS[element];
  const n = (k: number) => Math.max(2, Math.round(k * (0.5 + power)));
  switch (element) {
    case 'fire':
      // Flames licking upward, then a little smoke.
      burst(scene, x, y + 20, TEX.soft, n(16), {
        emitZone: randomRect(-50, -20, 100, 40),
        speedY: { min: -320, max: -120 },
        speedX: { min: -50, max: 50 },
        scale: { start: 1.6, end: 0 },
        alpha: { start: 0.9, end: 0 },
        lifespan: 520,
        color: [c[1], c[2], c[3]],
      });
      burst(scene, x, y + 10, TEX.soft, n(6), {
        speedY: { min: -200, max: -80 },
        speedX: { min: -30, max: 30 },
        scale: { start: 1, end: 0 },
        lifespan: 360,
        color: [c[0], c[1]],
        blendMode: 'ADD',
      });
      burst(scene, x, y - 40, TEX.soft, n(5), {
        speedY: { min: -90, max: -40 },
        speedX: { min: -30, max: 30 },
        scale: { start: 1, end: 2.4 },
        alpha: { start: 0.35, end: 0 },
        lifespan: 800,
        tint: 0x3a2a24,
      });
      break;
    case 'ice':
      // Shards burst out and a frost star flashes.
      burst(scene, x, y, TEX.spark, n(14), {
        speed: { min: 220, max: 520 },
        angle: { min: 0, max: 360 },
        rotate: ALONG_VELOCITY,
        scaleX: { start: 0.9, end: 0.2 },
        scaleY: { start: 1.6, end: 0.4 },
        gravityY: 600,
        lifespan: 520,
        color: [0xffffff, c[1], c[2]],
      });
      {
        const star = scene.add.image(x, y, TEX.star).setTint(c[1]).setBlendMode('ADD').setDepth(902).setScale(1);
        scene.tweens.add({ targets: star, scale: 4 + power * 3, angle: 45, alpha: { from: 1, to: 0 }, duration: 380, ease: 'Cubic.easeOut', onComplete: () => star.destroy() });
      }
      burst(scene, x, y, TEX.soft, n(8), {
        emitZone: randomRect(-70, -70, 140, 140),
        speedY: { min: 10, max: 60 },
        scale: { start: 0.5, end: 0 },
        lifespan: 900,
        tint: 0xffffff,
        blendMode: 'ADD',
      });
      break;
    case 'water':
      // A splash of droplets that fall back down, and a ripple on the ground.
      burst(scene, x, y, TEX.spark, n(18), {
        speed: { min: 200, max: 460 },
        angle: { min: 200, max: 340 },
        rotate: ALONG_VELOCITY,
        scaleX: 0.7,
        scaleY: { start: 1, end: 0.5 },
        gravityY: 1300,
        lifespan: 650,
        color: [c[1], c[2], c[2]],
        alpha: { start: 1, end: 0.3 },
      });
      burst(scene, x, y, TEX.ring, n(5), {
        speed: { min: 40, max: 160 },
        scale: { start: 0.15, end: 0.45 },
        alpha: { start: 0.9, end: 0 },
        lifespan: 500,
        tint: c[0],
      });
      shockwave(scene, x, y + 120, c[1], 2.2 + power, 0.3);
      break;
    case 'wind':
      // Leaves and air whipping around the target.
      burst(scene, x, y, TEX.leaf, n(10), {
        emitZone: { type: 'edge', source: new Phaser.Geom.Circle(0, 0, 60), quantity: n(10) },
        speedX: { min: -260, max: 260 },
        speedY: { min: -260, max: 60 },
        rotate: { start: 0, end: 720 },
        scale: { start: 1.1, end: 0.3 },
        lifespan: 600,
        color: [c[1], c[2], 0xffffff],
      });
      for (let i = 0; i < 2; i++) {
        const ring = scene.add.image(x, y - 10 + i * 30, TEX.ring).setTint(c[1]).setBlendMode('ADD').setDepth(901).setScale(0.6, 0.25);
        scene.tweens.add({
          targets: ring,
          scaleX: 3 + power * 1.5,
          scaleY: 1 + power * 0.5,
          angle: i ? -40 : 40,
          alpha: { from: 0.9, to: 0 },
          delay: i * 60,
          duration: 360,
          ease: 'Cubic.easeOut',
          onComplete: () => ring.destroy(),
        });
      }
      break;
    case 'earth':
      // Rock chunks thrown up, a dust cloud and a ground thump.
      burst(scene, x, y + 40, TEX.rock, n(10), {
        speed: { min: 220, max: 480 },
        angle: { min: 215, max: 325 },
        rotate: { start: 0, end: 540 },
        scale: { min: 0.8, max: 1.6 },
        gravityY: 1500,
        lifespan: 700,
        color: [c[0], c[1], c[2]],
      });
      burst(scene, x, y + 110, TEX.soft, n(10), {
        emitZone: randomRect(-80, -10, 160, 20),
        speedX: { min: -120, max: 120 },
        speedY: { min: -60, max: -10 },
        scale: { start: 1.6, end: 3 },
        alpha: { start: 0.45, end: 0 },
        lifespan: 700,
        tint: c[1],
      });
      shockwave(scene, x, y + 120, c[1], 2.6 + power, 0.3);
      break;
    case 'energy':
      // Little arcs crackling over the target.
      for (let i = 0; i < Math.round(2 + power * 2); i++) {
        scene.time.delayedCall(i * 45, () => {
          const a = Phaser.Math.FloatBetween(0, Math.PI * 2);
          const r = Phaser.Math.Between(50, 90);
          lightning(scene, { x: x + Math.cos(a) * r, y: y + Math.sin(a) * r }, { x: x + Phaser.Math.Between(-15, 15), y: y + Phaser.Math.Between(-15, 15) }, c[2], 0.45);
        });
      }
      burst(scene, x, y, TEX.spark, n(12), {
        speed: { min: 300, max: 700 },
        angle: { min: 0, max: 360 },
        rotate: ALONG_VELOCITY,
        scale: { start: 0.8, end: 0 },
        lifespan: 240,
        color: [0xffffff, c[1], c[3]],
        blendMode: 'ADD',
      });
      break;
    case 'light': {
      // A pillar of light falls on the target and stars ring out.
      const pillar = scene.add.image(x, y - 40, TEX.soft).setTint(c[2]).setBlendMode('ADD').setDepth(898).setScale(1.5, 14).setAlpha(0);
      scene.tweens.chain({
        targets: pillar,
        tweens: [
          { alpha: 0.9, scaleX: 2.5 + power * 1.5, duration: 90, ease: 'Cubic.easeOut' },
          { alpha: 0, scaleX: 0.5, duration: 380, ease: 'Sine.easeIn' },
        ],
        onComplete: () => pillar.destroy(),
      });
      burst(scene, x, y, TEX.star, n(10), {
        speed: { min: 120, max: 320 },
        angle: { min: 0, max: 360 },
        rotate: { min: 0, max: 180 },
        scale: { start: 1, end: 0 },
        lifespan: 520,
        color: [0xffffff, c[1], c[2]],
        blendMode: 'ADD',
      });
      break;
    }
    case 'dark': {
      // Shadow wisps pulled into the target, then a dark pop.
      burst(scene, x, y, TEX.soft, n(16), {
        emitZone: { type: 'edge', source: new Phaser.Geom.Circle(0, 0, 110), quantity: n(16) },
        moveToX: 0,
        moveToY: 0,
        scale: { start: 1.4, end: 0.2 },
        alpha: { start: 0.2, end: 1 },
        lifespan: 260,
        color: [c[1], c[2], c[3]],
      });
      const ring = scene.add.image(x, y, TEX.ring).setTint(c[1]).setDepth(901).setScale(3.4);
      scene.tweens.add({ targets: ring, scale: 0.3, alpha: { from: 0.2, to: 1 }, duration: 240, ease: 'Cubic.easeIn', onComplete: () => ring.destroy() });
      scene.time.delayedCall(240, () =>
        burst(scene, x, y, TEX.soft, n(10), {
          speed: { min: 80, max: 260 },
          scale: { start: 1.6, end: 0 },
          alpha: { start: 0.9, end: 0 },
          lifespan: 420,
          color: [c[0], c[1], c[2]],
          blendMode: 'ADD',
        }),
      );
      break;
    }
  }
}

/**
 * Element pulled into a target that absorbs it (the reverse of a burst), with a heal glow.
 */
export function absorbInto(scene: Phaser.Scene, x: number, y: number, colors: number[]): void {
  burst(scene, x, y, TEX.soft, 18, {
    emitZone: { type: 'edge', source: new Phaser.Geom.Circle(0, 0, 130), quantity: 18 },
    moveToX: 0,
    moveToY: 0,
    scale: { start: 1.2, end: 0.3 },
    lifespan: 380,
    color: colors,
    blendMode: 'ADD',
  });
}

/**
 * Magic circle on the ground under a caster, spinning while the spell charges. Returned so the caller can
 * put it behind the caster.
 */
export function castCircle(scene: Phaser.Scene, x: number, y: number, color: number, ms = 400): Phaser.GameObjects.Container {
  const g = scene.add.graphics().setBlendMode(Phaser.BlendModes.ADD);
  g.lineStyle(4, color, 0.9);
  g.strokeCircle(0, 0, 80);
  g.lineStyle(2, color, 0.7);
  g.strokeCircle(0, 0, 64);
  // Six rune ticks between the rings, and a hexagram inside.
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * Math.PI * 2;
    g.fillStyle(color, 1).fillCircle(Math.cos(a) * 72, Math.sin(a) * 72, 4);
  }
  const tri = (rot: number) =>
    [0, 1, 2].map((k) => new Phaser.Math.Vector2(Math.cos(rot + (k / 3) * Math.PI * 2) * 60, Math.sin(rot + (k / 3) * Math.PI * 2) * 60));
  g.lineStyle(2, color, 0.6);
  g.strokePoints(tri(-Math.PI / 2), true);
  g.strokePoints(tri(Math.PI / 2), true);
  // The holder squashes it flat onto the ground; the circle spins inside.
  const holder = scene.add.container(x, y, [g]).setScale(0.2, 0.08).setAlpha(0);
  scene.tweens.add({ targets: holder, scaleX: 1, scaleY: 0.32, alpha: 1, duration: Math.min(180, ms / 2), ease: 'Back.easeOut' });
  scene.tweens.add({ targets: g, angle: 120, duration: ms + 250 });
  scene.tweens.add({ targets: holder, alpha: 0, delay: ms, duration: 250, onComplete: () => holder.destroy() });
  return holder;
}

/** Rising rings and a column of glow around a figure (buffs, war cries). */
export function aura(scene: Phaser.Scene, x: number, feetY: number, color: number): void {
  for (let i = 0; i < 3; i++) {
    const ring = scene.add.image(x, feetY - 10, TEX.ring).setTint(color).setBlendMode('ADD').setDepth(901).setScale(2.6, 0.8).setAlpha(0);
    scene.tweens.add({
      targets: ring,
      y: feetY - 200,
      scaleX: 1.6,
      scaleY: 0.5,
      alpha: { from: 0.9, to: 0 },
      delay: i * 90,
      duration: 520,
      ease: 'Sine.easeOut',
      onComplete: () => ring.destroy(),
    });
  }
  const column = scene.add.image(x, feetY - 110, TEX.soft).setTint(color).setBlendMode('ADD').setDepth(898).setScale(4, 9).setAlpha(0);
  scene.tweens.add({ targets: column, alpha: { from: 0.5, to: 0 }, scaleX: 2, duration: 650, ease: 'Sine.easeIn', onComplete: () => column.destroy() });
}

/** Chevrons over a head: rising for a buff, sinking for a debuff. */
export function statusMark(scene: Phaser.Scene, x: number, y: number, color: number, up: boolean): void {
  for (let i = 0; i < 3; i++) {
    const c = scene.add
      .image(x + (i - 1) * 34, y + (up ? 30 : -30), TEX.chevron)
      .setTint(color)
      .setDepth(902)
      .setFlipY(!up)
      .setAlpha(0)
      .setScale(1.1);
    scene.tweens.chain({
      targets: c,
      tweens: [
        { alpha: 1, duration: 90, delay: i * 70 },
        { y: c.y + (up ? -60 : 60), alpha: 0, duration: 520, ease: 'Sine.easeIn' },
      ],
      onComplete: () => c.destroy(),
    });
  }
}

/** Stars circling over a stunned head. */
export function stunStars(scene: Phaser.Scene, x: number, y: number, ms = 900): void {
  for (let i = 0; i < 3; i++) {
    const star = scene.add.image(x, y, TEX.star).setTint(0xffe066).setBlendMode('ADD').setDepth(902).setScale(1.1);
    const orbit = { a: (i / 3) * Math.PI * 2 };
    scene.tweens.add({
      targets: orbit,
      a: orbit.a + Math.PI * 4,
      duration: ms,
      onUpdate: () => star.setPosition(x + Math.cos(orbit.a) * 50, y + Math.sin(orbit.a) * 14).setScale(Math.sin(orbit.a) > 0 ? 1.2 : 0.8),
      onComplete: () => star.destroy(),
    });
    scene.tweens.add({ targets: star, alpha: 0, delay: ms - 200, duration: 200 });
  }
}

/** Plus signs floating up (healing), in `color`. */
export function healPluses(scene: Phaser.Scene, x: number, feetY: number, color: number): void {
  burst(scene, x, feetY - 60, TEX.plus, 8, {
    emitZone: randomRect(-70, -120, 140, 130),
    speedY: { min: -140, max: -60 },
    scale: { start: 0.9, end: 0.2 },
    alpha: { start: 1, end: 0 },
    lifespan: 800,
    tint: [color, 0xffffff],
  });
}

/** Motes streaming from one point to another (lifesteal: life pulled out of the target). */
export function drain(scene: Phaser.Scene, from: Point, to: Point, color: number): void {
  burst(scene, from.x, from.y, TEX.soft, 16, {
    emitZone: randomRect(-40, -50, 80, 100),
    moveToX: to.x - from.x,
    moveToY: to.y - from.y,
    delay: { min: 0, max: 160 },
    scale: { start: 1.1, end: 0.4 },
    alpha: { start: 1, end: 0.6 },
    lifespan: 320,
    color: [color, 0xffffff],
    blendMode: 'ADD',
  });
}

/** A status taking hold (or ticking): a cloud and bubbles in its color around the body. */
export function statusPuff(scene: Phaser.Scene, x: number, y: number, color: number): void {
  burst(scene, x, y, TEX.soft, 12, {
    emitZone: randomRect(-60, -60, 120, 120),
    speed: { min: 10, max: 60 },
    scale: { start: 1.6, end: 0.2 },
    alpha: { start: 0.6, end: 0 },
    lifespan: 600,
    tint: color,
  });
  burst(scene, x, y, TEX.ring, 7, {
    emitZone: randomRect(-50, -40, 100, 80),
    speedY: { min: -120, max: -50 },
    speedX: { min: -20, max: 20 },
    scale: { start: 0.12, end: 0.3 },
    alpha: { start: 1, end: 0 },
    lifespan: 600,
    tint: color,
    blendMode: 'ADD',
  });
}

/** Sparks glancing off a raised guard (a Defend that soaks a hit). `dir` 1 = the blow came from the left. */
export function deflect(scene: Phaser.Scene, x: number, y: number, dir: 1 | -1, color: number): void {
  burst(scene, x, y, TEX.spark, 16, {
    speed: { min: 260, max: 560 },
    angle: dir === 1 ? { min: 150, max: 260 } : { min: -80, max: 30 },
    rotate: ALONG_VELOCITY,
    scale: { start: 0.9, end: 0 },
    gravityY: 500,
    lifespan: 380,
    color: [0xffffff, color],
    blendMode: 'ADD',
  });
}

/** A body breaking apart into motes that drift up (monster deaths). */
export function dissolve(scene: Phaser.Scene, x: number, feetY: number, w: number, h: number, colors: number[]): void {
  burst(scene, x, feetY, TEX.soft, 34, {
    emitZone: randomRect(-w / 2, -h, w, h),
    speedY: { min: -180, max: -40 },
    speedX: { min: -40, max: 40 },
    scale: { start: 0.9, end: 0 },
    alpha: { start: 1, end: 0 },
    lifespan: 900,
    color: colors,
    blendMode: 'ADD',
  });
  burst(scene, x, feetY, TEX.star, 8, {
    emitZone: randomRect(-w / 2, -h, w, h),
    speedY: { min: -120, max: -30 },
    scale: { start: 0.7, end: 0 },
    rotate: { min: 0, max: 180 },
    lifespan: 700,
    tint: 0xffffff,
    blendMode: 'ADD',
  });
}

/** Looping background particles for a room or screen. Returns the emitters. */
export function ambient(scene: Phaser.Scene, kinds: Ambient[], depth = 50): Emitter[] {
  const { width, height } = scene.scale;
  const emitters: Emitter[] = [];
  for (const kind of kinds) {
    let e: Emitter;
    switch (kind) {
      case 'dust':
        e = scene.add.particles(0, 0, TEX.soft, {
          emitZone: randomRect(0, 0, width, height),
          speedX: { min: -12, max: 12 },
          speedY: { min: -18, max: -4 },
          scale: { min: 0.15, max: 0.4 },
          alpha: { onEmit: () => 0, onUpdate: (_p, _k, t) => Math.sin(t * Math.PI) * 0.6 },
          lifespan: 6000,
          frequency: 220,
          tint: 0xfff6dd,
          blendMode: 'ADD',
        });
        break;
      case 'leaves':
        e = scene.add.particles(0, 0, TEX.leaf, {
          emitZone: randomRect(-100, -30, width + 200, 10),
          speedY: { min: 35, max: 80 },
          speedX: { min: -40, max: 20 },
          accelerationX: { min: -12, max: 12 },
          rotate: { start: 0, end: 540 },
          scale: { min: 0.6, max: 1.2 },
          alpha: { start: 0.95, end: 0.6 },
          lifespan: 11000,
          frequency: 420,
          tint: [0x6b8e23, 0x8fbc3f, 0xc0a040, 0xb5651d],
        });
        break;
      case 'fireflies':
        e = scene.add.particles(0, 0, TEX.soft, {
          emitZone: randomRect(0, 120, width, height - 200),
          speedX: { min: -25, max: 25 },
          speedY: { min: -25, max: 25 },
          scale: { min: 0.3, max: 0.6 },
          alpha: {
            onEmit: () => 0,
            onUpdate: (p, _k, t) => Math.sin(t * Math.PI) * (0.55 + 0.45 * Math.sin(t * 30 + p.x)),
          },
          lifespan: 5000,
          frequency: 260,
          tint: 0xd8ff7a,
          blendMode: 'ADD',
        });
        break;
      case 'rain':
        e = scene.add.particles(0, 0, TEX.spark, {
          emitZone: randomRect(-200, -40, width + 300, 10),
          speedY: { min: 620, max: 760 },
          speedX: { min: -150, max: -120 },
          rotate: 12,
          scaleX: 0.25,
          scaleY: { min: 0.8, max: 1.3 },
          alpha: { min: 0.15, max: 0.35 },
          lifespan: 1400,
          frequency: 12,
          tint: 0xbfd8ff,
        });
        break;
      case 'snow':
        e = scene.add.particles(0, 0, TEX.soft, {
          emitZone: randomRect(-100, -30, width + 200, 10),
          speedY: { min: 40, max: 90 },
          speedX: { min: -30, max: 10 },
          scale: { min: 0.12, max: 0.3 },
          alpha: { min: 0.5, max: 0.9 },
          lifespan: 12000,
          frequency: 70,
          tint: 0xffffff,
        });
        break;
      case 'embers':
        e = scene.add.particles(0, 0, TEX.soft, {
          emitZone: randomRect(0, height - 160, width, 120),
          speedY: { min: -70, max: -30 },
          speedX: { min: -15, max: 15 },
          scale: { min: 0.15, max: 0.35 },
          alpha: { onEmit: () => 0, onUpdate: (_p, _k, t) => Math.sin(t * Math.PI) * 0.8 },
          lifespan: 5000,
          frequency: 220,
          tint: 0xffa040,
          blendMode: 'ADD',
        });
        break;
      case 'mist':
        e = scene.add.particles(0, 0, TEX.soft, {
          emitZone: randomRect(-200, height - 330, width + 200, 200),
          speedX: { min: 8, max: 22 },
          speedY: { min: -3, max: 3 },
          scale: { min: 6, max: 11 },
          alpha: { onEmit: () => 0, onUpdate: (_p, _k, t) => Math.sin(t * Math.PI) * 0.12 },
          lifespan: 14000,
          frequency: 700,
          tint: 0xcfe3dc,
        });
        break;
    }
    e.setDepth(depth);
    // Pre-warm so the screen isn't empty on entry.
    e.fastForward(6000);
    emitters.push(e);
  }
  return emitters;
}

/** Twinkling star field (title screen). */
export function starField(scene: Phaser.Scene, count: number, maxY: number): void {
  const { width } = scene.scale;
  for (let i = 0; i < count; i++) {
    const star = scene.add
      .image(Phaser.Math.Between(0, width), Phaser.Math.Between(0, maxY), i % 6 === 0 ? TEX.star : TEX.soft)
      .setScale(Phaser.Math.FloatBetween(0.1, i % 6 === 0 ? 0.5 : 0.25))
      .setBlendMode('ADD')
      .setAlpha(Phaser.Math.FloatBetween(0.2, 0.9));
    scene.tweens.add({
      targets: star,
      alpha: { from: star.alpha, to: 0.1 },
      duration: Phaser.Math.Between(800, 2600),
      yoyo: true,
      repeat: -1,
      delay: Phaser.Math.Between(0, 2000),
    });
  }
}
