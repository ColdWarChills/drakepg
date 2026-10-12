/** Random source returning a float in [0, 1). Swap in `seeded()` for reproducible tests. */
export interface Rng {
  next(): number;
}

export const defaultRng: Rng = { next: () => Math.random() };

/** Mulberry32: small, fast, deterministic. */
export function seeded(seed: number): Rng {
  let a = seed >>> 0;
  return {
    next() {
      a = (a + 0x6d2b79f5) >>> 0;
      let t = a;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    },
  };
}

/** Inclusive on both ends. */
export function randInt(rng: Rng, min: number, max: number): number {
  return min + Math.floor(rng.next() * (max - min + 1));
}

export function chance(rng: Rng, p: number): boolean {
  return rng.next() < p;
}

export function weightedPick<T>(rng: Rng, entries: readonly T[], weight: (entry: T) => number): T {
  if (entries.length === 0) throw new Error('weightedPick: empty list');
  const total = entries.reduce((sum, e) => sum + Math.max(0, weight(e)), 0);
  let roll = rng.next() * total;
  for (const entry of entries) {
    roll -= Math.max(0, weight(entry));
    if (roll < 0) return entry;
  }
  return entries[entries.length - 1];
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
