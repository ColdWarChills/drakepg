import Phaser from 'phaser';
import type { Ambient } from '../data/types';

/**
 * Juice: particles, slashes, projectiles, flashes, hit-stop. Textures are drawn in code at boot
 * (createFxTextures), so effects work without any art.
 */

type Emitter = Phaser.GameObjects.Particles.ParticleEmitter;
type EmitterConfig = Phaser.Types.GameObjects.Particles.ParticleEmitterConfig;

const TEX = {
  soft: 'fx_soft',
  spark: 'fx_spark',
  star: 'fx_star',
  leaf: 'fx_leaf',
  coin: 'fx_coin',
  ring: 'fx_ring',
} as const;

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
export function hitSparks(scene: Phaser.Scene, x: number, y: number, magic: boolean, crit: boolean): void {
  const colors = magic ? [0xffffff, 0x9fd3ff, 0x6c63ff] : [0xffffff, 0xfff1a8, 0xffb347];
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
    tint: magic ? 0x9fd3ff : 0xffe7a0,
    blendMode: 'ADD',
  });
  shockwave(scene, x, y, magic ? 0x9fd3ff : 0xffffff, crit ? 2.2 : 1.4, 1);
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
  g.setScale(0.5 * dir, 0.5).setRotation(-0.8 * dir + tilt).setBlendMode(Phaser.BlendModes.ADD);
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

/** Glowing ball that flies in an arc and resolves when it lands. */
export function projectile(
  scene: Phaser.Scene,
  from: { x: number; y: number },
  to: { x: number; y: number },
  colors: number[],
  duration = 420,
): Promise<void> {
  return new Promise((resolve) => {
    const core = scene.add.image(from.x, from.y, TEX.soft).setTint(colors[1] ?? colors[0]).setScale(2.2).setDepth(902).setBlendMode('ADD');
    const trail = scene.add.particles(0, 0, TEX.soft, {
      follow: core,
      lifespan: 320,
      speed: { min: 5, max: 40 },
      scale: { start: 1.6, end: 0 },
      alpha: { start: 0.9, end: 0 },
      color: colors,
      blendMode: 'ADD',
      frequency: 12,
    });
    trail.setDepth(901);
    const arc = { t: 0 };
    scene.tweens.add({
      targets: arc,
      t: 1,
      duration,
      ease: 'Sine.easeIn',
      onUpdate: () => {
        core.x = Phaser.Math.Linear(from.x, to.x, arc.t);
        core.y = Phaser.Math.Linear(from.y, to.y, arc.t) - Math.sin(arc.t * Math.PI) * 70;
        core.setScale(2.2 + Math.sin(arc.t * 40) * 0.25);
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
  window.setTimeout(() => {
    if (!scene.sys.isActive()) return;
    scene.tweens.timeScale = 1;
    scene.time.timeScale = 1;
  }, ms);
}

/** Flash a figure (Container from drawFigure) pure white for a moment. */
export function whiteFlash(scene: Phaser.Scene, fig: Phaser.GameObjects.Container, ms = 90): void {
  const restore: (() => void)[] = [];
  for (const child of fig.list) {
    if (child instanceof Phaser.GameObjects.Image) {
      child.setTint(0xffffff).setTintMode(Phaser.TintModes.FILL);
      restore.push(() => child.clearTint().setTintMode(Phaser.TintModes.MULTIPLY));
    } else if (child instanceof Phaser.GameObjects.Shape && child.isFilled && child.fillAlpha === 1) {
      const color = child.fillColor;
      child.setFillStyle(0xffffff);
      restore.push(() => child.setFillStyle(color));
    }
  }
  scene.time.delayedCall(ms, () => restore.forEach((r) => r()));
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
