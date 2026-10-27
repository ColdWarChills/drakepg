import type { ArtRef } from '../data/types';

const artFiles = import.meta.glob<string>('../assets/art/*.{png,jpg,jpeg,webp}', { eager: true, query: '?url', import: 'default' });
const iconFiles = import.meta.glob<string>('../../unity/Assets/Resources/UiIcons/*.png', { eager: true, query: '?url', import: 'default' });

const fileName = (path: string) => path.split('/').pop()!.replace(/\.[^.]+$/, '');

/** Art by key; the sharper file wins when both sizes exist (same rule as src/ui/art.ts). */
const ART = new Map<string, { url: string; res: number }>();
for (const [path, url] of Object.entries(artFiles)) {
  const [, key, res] = /^(.*?)(?:@([\d.]+)x)?$/.exec(fileName(path))!;
  const r = res ? Number(res) : 1;
  if ((ART.get(key)?.res ?? 0) < r) ART.set(key, { url, res: r });
}

/** The Unity UI icons (dark outlines, made for paper): `el_fire`, `status_burn`, `spot_blunt`... */
const ICONS = new Map(Object.entries(iconFiles).map(([path, url]) => [fileName(path), url]));

/** First existing key of an art ref, like the game picks it. */
export function artUrl(ref: ArtRef | undefined): string | undefined {
  const keys = ref === undefined ? [] : Array.isArray(ref) ? ref : [ref];
  for (const key of keys) {
    const art = ART.get(key);
    if (art) return art.url;
  }
  return undefined;
}

export function iconUrl(name: string): string | undefined {
  return ICONS.get(name);
}
