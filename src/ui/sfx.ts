import Phaser from 'phaser';
import type { Element } from '../data/types';

/**
 * Sound effects synthesized with WebAudio, so there are no audio files to source.
 * Phaser unlocks the AudioContext on the first click/tap/key; before that, sounds are skipped.
 * Mute is remembered per browser (M key or the speaker button).
 */

export type Sfx =
  | 'click'
  | 'open'
  | 'slash'
  | 'hit'
  | 'crit'
  | 'miss'
  | 'charge'
  | 'fireball'
  | 'explode'
  | 'heal'
  | 'coin'
  | 'levelup'
  | 'step'
  | 'alert'
  | 'encounter'
  | 'victory'
  | 'defeat'
  | 'quest'
  | 'warning'
  | 'denied'
  | 'equip'
  | 'rare'
  | 'sigil'
  | 'eclipse'
  | 'recall'
  | 'omen'
  | 'page'
  | 'whoosh'
  | 'block'
  | 'debuff'
  | 'stun'
  | 'burn'
  | 'freeze'
  | 'splash'
  | 'gust'
  | 'quake'
  | 'zap'
  | 'holy'
  | 'void';

/** The extra layer an element's hit adds on top of the normal hit sound. */
export const ELEMENT_SFX: Record<Element, Sfx> = {
  fire: 'burn',
  ice: 'freeze',
  water: 'splash',
  wind: 'gust',
  earth: 'quake',
  energy: 'zap',
  light: 'holy',
  dark: 'void',
};

const MUTE_KEY = 'drakepg.muted';
const VOLUME = 0.45;

let muted = (() => {
  try {
    return localStorage.getItem(MUTE_KEY) === '1';
  } catch {
    return false;
  }
})();

let noise: AudioBuffer | null = null;
let master: GainNode | null = null;

export function isMuted(): boolean {
  return muted;
}

export function toggleMute(): boolean {
  muted = !muted;
  try {
    localStorage.setItem(MUTE_KEY, muted ? '1' : '0');
  } catch {
    // Storage blocked: mute still works for this session.
  }
  return muted;
}

function ctxOf(scene: Phaser.Scene): AudioContext | null {
  const sound = scene.sound as Phaser.Sound.WebAudioSoundManager;
  const ctx = sound.context;
  if (!ctx || ctx.state !== 'running') return null;
  if (!master || master.context !== ctx) {
    master = ctx.createGain();
    master.gain.value = VOLUME;
    master.connect(ctx.destination);
    noise = null;
  }
  if (!noise) {
    noise = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
    const data = noise.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
  }
  return ctx;
}

/** A pitched tone with an attack/decay envelope. Frequency can glide to `to`. */
function tone(ctx: AudioContext, t: number, type: OscillatorType, from: number, to: number, dur: number, vol: number): void {
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(from, t);
  osc.frequency.exponentialRampToValueAtTime(Math.max(20, to), t + dur);
  gain.gain.setValueAtTime(0.0001, t);
  gain.gain.exponentialRampToValueAtTime(vol, t + 0.008);
  gain.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  osc.connect(gain).connect(master!);
  osc.start(t);
  osc.stop(t + dur + 0.02);
}

/** Filtered noise burst; the filter can sweep (whooshes, impacts, rumbles). */
function hiss(
  ctx: AudioContext,
  t: number,
  dur: number,
  vol: number,
  filter: BiquadFilterType,
  from: number,
  to: number,
  q = 1,
): void {
  const src = ctx.createBufferSource();
  src.buffer = noise;
  const f = ctx.createBiquadFilter();
  f.type = filter;
  f.Q.value = q;
  f.frequency.setValueAtTime(from, t);
  f.frequency.exponentialRampToValueAtTime(Math.max(20, to), t + dur);
  const gain = ctx.createGain();
  gain.gain.setValueAtTime(0.0001, t);
  gain.gain.exponentialRampToValueAtTime(vol, t + 0.006);
  gain.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  src.connect(f).connect(gain).connect(master!);
  src.start(t, Math.random() * 0.5);
  src.stop(t + dur + 0.02);
}

function arpeggio(ctx: AudioContext, t: number, notes: number[], step: number, type: OscillatorType, vol: number, len = 0.25): void {
  notes.forEach((n, i) => tone(ctx, t + i * step, type, n, n, len, vol));
}

export function play(scene: Phaser.Scene, name: Sfx): void {
  if (muted) return;
  const ctx = ctxOf(scene);
  if (!ctx) return;
  const t = ctx.currentTime + 0.005;
  // Small random pitch drift keeps repeated sounds from feeling mechanical.
  const v = 1 + (Math.random() - 0.5) * 0.08;

  switch (name) {
    case 'click':
      tone(ctx, t, 'square', 900 * v, 700 * v, 0.05, 0.12);
      break;
    case 'open':
      tone(ctx, t, 'triangle', 500, 900, 0.09, 0.15);
      break;
    case 'slash':
      hiss(ctx, t, 0.16, 0.5, 'bandpass', 5000 * v, 900, 2);
      break;
    case 'hit':
      tone(ctx, t, 'sine', 160 * v, 50, 0.16, 0.7);
      hiss(ctx, t, 0.09, 0.45, 'lowpass', 3000, 400);
      break;
    case 'crit':
      tone(ctx, t, 'sine', 200 * v, 40, 0.28, 0.9);
      tone(ctx, t, 'square', 1300, 600, 0.12, 0.12);
      hiss(ctx, t, 0.18, 0.6, 'lowpass', 6000, 300);
      break;
    case 'miss':
      hiss(ctx, t, 0.2, 0.25, 'bandpass', 2500 * v, 600, 3);
      break;
    case 'charge':
      tone(ctx, t, 'sawtooth', 120, 600, 0.3, 0.09);
      hiss(ctx, t, 0.3, 0.12, 'bandpass', 400, 3000, 4);
      break;
    case 'fireball':
      hiss(ctx, t, 0.4, 0.35, 'lowpass', 800, 2500, 1);
      break;
    case 'explode':
      hiss(ctx, t, 0.5, 0.8, 'lowpass', 2500, 80);
      tone(ctx, t, 'sine', 120, 35, 0.4, 0.8);
      break;
    case 'heal':
      arpeggio(ctx, t, [523, 659, 784, 1047], 0.07, 'sine', 0.22, 0.3);
      break;
    case 'coin':
      tone(ctx, t, 'square', 1250 * v, 1250 * v, 0.06, 0.08);
      tone(ctx, t + 0.055, 'square', 1680 * v, 1680 * v, 0.12, 0.08);
      break;
    case 'levelup':
      arpeggio(ctx, t, [392, 523, 659, 784, 1047, 1319], 0.08, 'triangle', 0.3, 0.35);
      break;
    case 'step':
      hiss(ctx, t, 0.05, 0.08, 'lowpass', 700 * v, 200);
      break;
    case 'alert':
      tone(ctx, t, 'square', 700, 700, 0.07, 0.12);
      tone(ctx, t + 0.08, 'square', 1050, 1050, 0.1, 0.12);
      break;
    case 'encounter':
      tone(ctx, t, 'sawtooth', 880, 110, 0.45, 0.18);
      hiss(ctx, t, 0.45, 0.4, 'bandpass', 4000, 200, 2);
      break;
    case 'victory':
      arpeggio(ctx, t, [523, 659, 784], 0.11, 'triangle', 0.3, 0.2);
      tone(ctx, t + 0.36, 'triangle', 1047, 1047, 0.6, 0.32);
      break;
    case 'defeat':
      arpeggio(ctx, t, [392, 349, 311, 262], 0.18, 'triangle', 0.25, 0.4);
      break;
    case 'quest':
      arpeggio(ctx, t, [659, 880, 1175], 0.09, 'sine', 0.25, 0.4);
      break;
    case 'warning':
      tone(ctx, t, 'sawtooth', 220, 180, 0.35, 0.16);
      tone(ctx, t + 0.18, 'sawtooth', 200, 150, 0.4, 0.16);
      break;
    case 'denied':
      tone(ctx, t, 'square', 220, 180, 0.12, 0.1);
      break;
    case 'equip':
      // Buckle click plus a short metal ring.
      tone(ctx, t, 'square', 900, 500, 0.04, 0.08);
      tone(ctx, t + 0.04, 'triangle', 1400, 1350, 0.25, 0.12);
      break;
    case 'rare':
      // Shimmer up for a high-grade drop.
      arpeggio(ctx, t, [784, 988, 1175, 1568, 1976], 0.06, 'sine', 0.22, 0.5);
      tone(ctx, t + 0.3, 'triangle', 2637, 2637, 0.6, 0.06);
      break;
    case 'sigil':
      // Bell chord, then a rising shimmer: a Sigil of Dawn or Dusk dropped.
      [523, 659, 784, 1047].forEach((n) => tone(ctx, t, 'sine', n, n, 1.2, 0.09));
      arpeggio(ctx, t + 0.25, [1047, 1319, 1568, 2093, 2637], 0.07, 'triangle', 0.1, 0.5);
      hiss(ctx, t + 0.2, 0.9, 0.05, 'highpass', 4000, 9000);
      break;
    case 'eclipse':
      // Deep boom, dark chord swelling up, then the shimmer.
      tone(ctx, t, 'sine', 110, 40, 1.1, 0.4);
      hiss(ctx, t, 0.6, 0.18, 'lowpass', 900, 80);
      [220, 262, 330, 415].forEach((n, i) => tone(ctx, t + 0.15 + i * 0.05, 'sawtooth', n, n * 2, 1.4, 0.05));
      arpeggio(ctx, t + 0.7, [880, 1047, 1319, 1661, 2093, 2637], 0.08, 'sine', 0.14, 0.7);
      hiss(ctx, t + 0.7, 1.4, 0.06, 'highpass', 5000, 11000);
      break;
    case 'recall':
      // A Recall Scroll: paper whoosh, then a soft rising bell.
      hiss(ctx, t, 0.35, 0.25, 'bandpass', 800, 3000, 1.5);
      arpeggio(ctx, t + 0.15, [392, 523, 659, 784, 1047], 0.09, 'sine', 0.18, 0.6);
      hiss(ctx, t + 0.4, 0.8, 0.04, 'highpass', 4000, 9000);
      break;
    case 'omen':
      // A quiet room's first visit: a low swell and a far-off creak.
      tone(ctx, t, 'sine', 98, 92, 1.8, 0.22);
      tone(ctx, t + 0.05, 'triangle', 147, 139, 1.6, 0.06);
      tone(ctx, t + 0.9, 'sawtooth', 70, 55, 0.5, 0.05);
      break;
    case 'page':
      hiss(ctx, t, 0.18, 0.12, 'bandpass', 2500, 1200, 1.2);
      break;
    case 'whoosh':
      // A quick lunge through the air.
      hiss(ctx, t, 0.14, 0.22, 'bandpass', 900 * v, 2600, 1.5);
      break;
    case 'block':
      // Blow glancing off a guard: a dull knock and a bright ring.
      tone(ctx, t, 'sine', 140 * v, 70, 0.1, 0.45);
      tone(ctx, t, 'triangle', 1900 * v, 1750, 0.3, 0.12);
      tone(ctx, t, 'triangle', 2850 * v, 2700, 0.2, 0.06);
      break;
    case 'debuff':
      tone(ctx, t, 'triangle', 520 * v, 260, 0.25, 0.12);
      tone(ctx, t + 0.06, 'sine', 390 * v, 190, 0.25, 0.08);
      break;
    case 'stun':
      // Dizzy bird chirps.
      [1568, 1319, 1568].forEach((n, i) => tone(ctx, t + i * 0.07, 'sine', n * v, n * v * 1.06, 0.07, 0.08));
      break;
    case 'burn':
      hiss(ctx, t, 0.35, 0.3, 'bandpass', 700 * v, 2400, 0.8);
      hiss(ctx, t + 0.05, 0.25, 0.12, 'highpass', 3000, 6000);
      break;
    case 'freeze':
      // Ice cracking: glassy tinkles.
      [2637, 3136, 2349].forEach((n, i) => tone(ctx, t + i * 0.035, 'triangle', n * v, n * v * 0.97, 0.18, 0.06));
      hiss(ctx, t, 0.08, 0.2, 'highpass', 5000, 8000);
      break;
    case 'splash':
      hiss(ctx, t, 0.3, 0.3, 'lowpass', 2400 * v, 500, 1.2);
      tone(ctx, t, 'sine', 600 * v, 1400, 0.08, 0.1);
      break;
    case 'gust':
      hiss(ctx, t, 0.4, 0.22, 'bandpass', 500 * v, 1800, 2.5);
      break;
    case 'quake':
      tone(ctx, t, 'sine', 70 * v, 35, 0.4, 0.6);
      hiss(ctx, t, 0.35, 0.3, 'lowpass', 600, 80);
      break;
    case 'zap':
      tone(ctx, t, 'sawtooth', 1800 * v, 200, 0.12, 0.1);
      hiss(ctx, t, 0.15, 0.25, 'highpass', 3000, 1500);
      tone(ctx, t + 0.06, 'square', 900 * v, 120, 0.08, 0.06);
      break;
    case 'holy':
      [1047, 1319, 1568].forEach((n) => tone(ctx, t, 'sine', n * v, n * v, 0.45, 0.06));
      hiss(ctx, t, 0.3, 0.06, 'highpass', 5000, 9000);
      break;
    case 'void':
      tone(ctx, t, 'sine', 220 * v, 55, 0.35, 0.35);
      tone(ctx, t, 'sawtooth', 110 * v, 60, 0.3, 0.05);
      break;
  }
}
