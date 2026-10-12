import Phaser from 'phaser';

/**
 * Every image in src/assets/art/ is bundled and loaded at boot, keyed by file name without
 * extension (e.g. `mon_mossling.png` -> `mon_mossling`). Only files that exist are included,
 * so missing art never causes a 404; callers check `hasArt` and fall back to placeholder shapes.
 */
const files = import.meta.glob<string>('../assets/art/*.{png,jpg,jpeg,webp}', {
  eager: true,
  query: '?url',
  import: 'default',
});

const ART: Record<string, string> = {};
for (const [path, url] of Object.entries(files)) {
  const key = path.split('/').pop()!.replace(/\.[^.]+$/, '');
  ART[key] = url;
}

export function preloadArt(scene: Phaser.Scene): void {
  for (const [key, url] of Object.entries(ART)) scene.load.image(key, url);
}

export function hasArt(scene: Phaser.Scene, key: string | undefined): key is string {
  return key !== undefined && scene.textures.exists(key);
}

/** First key in `ref` that has art loaded. */
export function pickArt(scene: Phaser.Scene, ref: string | string[] | undefined): string | undefined {
  const keys = ref === undefined ? [] : Array.isArray(ref) ? ref : [ref];
  return keys.find((k) => hasArt(scene, k));
}

/** Full-screen background, scaled to cover the screen. Returns false if the art is missing. */
export function addBackground(scene: Phaser.Scene, ref: string | string[] | undefined): boolean {
  const key = pickArt(scene, ref);
  if (!key) return false;
  const { width, height } = scene.scale;
  const img = scene.add.image(width / 2, height / 2, key);
  img.setScale(Math.max(width / img.width, height / img.height));
  return true;
}
