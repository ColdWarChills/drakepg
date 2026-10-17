import Phaser from 'phaser';
import { DESIGN } from '../config';
import { setTextureRes } from './view';

/**
 * Every image in src/assets/art/ is bundled and loaded at boot, keyed by file name without
 * extension (e.g. `mon_mossling.png` -> `mon_mossling`). Only files that exist are included,
 * so missing art never causes a 404; callers check `hasArt` and fall back to placeholder shapes.
 *
 * A `@2x` file (`mon_mossling@2x.webp`, key `mon_mossling`) has twice the pixels of the 1x size (backgrounds
 * 1280x720, sprites 480px tall) and is drawn at the 1x size, so old and new art mix freely. Code always works in
 * 1x sizes: a 2x image reports its 1x width and height, and hand anchors (`look.bare.hands`) are 1x pixels.
 */
const files = import.meta.glob<string>('../assets/art/*.{png,jpg,jpeg,webp}', {
  eager: true,
  query: '?url',
  import: 'default',
});

const ART: Record<string, { url: string; res: number }> = {};
for (const [path, url] of Object.entries(files)) {
  const name = path.split('/').pop()!.replace(/\.[^.]+$/, '');
  const [, key, res] = /^(.*?)(?:@([\d.]+)x)?$/.exec(name)!;
  const art = { url, res: res ? Number(res) : 1 };
  // Both sizes of one key: the sharper file wins.
  if (!ART[key] || art.res > ART[key].res) ART[key] = art;
}

export function preloadArt(scene: Phaser.Scene): void {
  for (const [key, { url }] of Object.entries(ART)) scene.load.image(key, url);
  scene.load.on(Phaser.Loader.Events.FILE_COMPLETE, (key: string) => {
    const res = ART[key]?.res ?? 1;
    if (res !== 1 && scene.textures.exists(key)) setTextureRes(scene.textures.get(key), res);
  });
}

export function hasArt(scene: Phaser.Scene, key: string | undefined): key is string {
  return key !== undefined && scene.textures.exists(key);
}

/** First key in `ref` that has art loaded. */
export function pickArt(scene: Phaser.Scene, ref: string | string[] | undefined): string | undefined {
  const keys = ref === undefined ? [] : Array.isArray(ref) ? ref : [ref];
  return keys.find((k) => hasArt(scene, k));
}

/**
 * Full-screen background, scaled to cover the screen. Returns false if the art is missing.
 * `raise` lifts it by that many pixels (battle: the ground the art keeps in its bottom 30% shows above the bottom
 * panel); the strip it leaves at the bottom is filled dark.
 */
export function addBackground(scene: Phaser.Scene, ref: string | string[] | undefined, raise = 0): boolean {
  const key = pickArt(scene, ref);
  if (!key) return false;
  const { width, height } = DESIGN;
  if (raise > 0) scene.add.rectangle(0, height - raise, width, raise, 0x14101c).setOrigin(0);
  const img = scene.add.image(width / 2, height / 2 - raise, key);
  img.setScale(Math.max(width / img.width, height / img.height));
  return true;
}
