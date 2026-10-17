// Shared bits for the hand-made UI mocks (art/ui/battle_*.html): seeded random, outline makers, SVG helpers, the
// ink icons (same drawings as scripts/ui_icons.py) and the battle screen's content at the game's own positions.
// Nothing here is generated: every piece is drawn in code. Rendered with headless Edge (scripts/ui_mocks.py).

const NS = 'http://www.w3.org/2000/svg';
function rng(seed) { let s = seed * 9301 + 49297; return () => ((s = (s * 16807) % 2147483647) / 2147483647); }
function el(name, attrs, parent) {
  const e = document.createElementNS(NS, name);
  for (const k in attrs) e.setAttribute(k, attrs[k]);
  parent?.appendChild(e);
  return e;
}
function div(cls, style, parent, html) {
  const d = document.createElement('div');
  if (cls) d.className = cls;
  if (style) d.style.cssText = style;
  if (html !== undefined) d.innerHTML = html;
  parent?.appendChild(d);
  return d;
}
const path = (pts, close = true) => 'M' + pts.map((p) => p.map((v) => v.toFixed(1)).join(' ')).join(' L') + (close ? ' Z' : '');

// A scissor-cut outline (the round-15 mock): straight cuts of uneven length, ends a little off the line.
function cutPath(x, y, w, h, seed, { j = 2.2, cut = 46, corner = 5 } = {}) {
  const R = rng(seed), pts = [];
  const edge = (x0, y0, x1, y1) => {
    const len = Math.hypot(x1 - x0, y1 - y0), nx = -(y1 - y0) / len, ny = (x1 - x0) / len;
    for (let t = 0; t < 1;) {
      const o = (R() - 0.5) * 2 * j;
      pts.push([x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o]);
      t += (cut * (0.55 + R() * 0.9)) / len;
    }
  };
  const c = corner;
  edge(x + c, y, x + w - c, y); edge(x + w, y + c, x + w, y + h - c); edge(x + w - c, y + h, x + c, y + h); edge(x, y + h - c, x, y + c);
  return path(pts);
}

// A rounded rectangle whose edges wave gently (paper cut with curved scissors): `amp` px, `wave` px per bump.
function wavyRect(x, y, w, h, seed, { r = 14, amp = 2.2, wave = 70, step = 6 } = {}) {
  const R = rng(seed), pts = [];
  const phase = [R() * 6.28, R() * 6.28, R() * 6.28, R() * 6.28];
  const side = (x0, y0, x1, y1, ph) => {
    const len = Math.hypot(x1 - x0, y1 - y0), nx = -(y1 - y0) / len, ny = (x1 - x0) / len;
    for (let d = 0; d < len; d += step) {
      const t = d / len, o = Math.sin(ph + (d / wave) * 6.28) * amp * Math.sin(t * Math.PI);
      pts.push([x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o]);
    }
  };
  const arc = (cx, cy, a0) => { for (let i = 0; i <= 6; i++) { const a = a0 + (i / 6) * Math.PI / 2; pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]); } };
  side(x + r, y, x + w - r, y, phase[0]); arc(x + w - r, y + r, -Math.PI / 2);
  side(x + w, y + r, x + w, y + h - r, phase[1]); arc(x + w - r, y + h - r, 0);
  side(x + w - r, y + h, x + r, y + h, phase[2]); arc(x + r, y + h - r, Math.PI / 2);
  side(x, y + h - r, x, y + r, phase[3]); arc(x + r, y + r, Math.PI);
  return path(pts);
}

// A deckled (hand-torn) rectangle: many tiny ragged steps.
function deckled(x, y, w, h, seed, { amp = 1.6, step = 3 } = {}) {
  const R = rng(seed), pts = [];
  const side = (x0, y0, x1, y1) => {
    const len = Math.hypot(x1 - x0, y1 - y0), nx = -(y1 - y0) / len, ny = (x1 - x0) / len;
    for (let d = 0; d < len; d += step + R() * step) {
      const t = d / len, o = (R() - 0.5) * 2 * amp;
      pts.push([x0 + (x1 - x0) * t + nx * o, y0 + (y1 - y0) * t + ny * o]);
    }
  };
  side(x, y, x + w, y); side(x + w, y, x + w, y + h); side(x + w, y + h, x, y + h); side(x, y + h, x, y);
  return path(pts);
}

// The ink icons: [viewBox 32 body]. `ink`/`paper` recolor the outline and the cream fills (for dark themes).
function icon(name, size = 24, { ink = '#2a1d16', paper = '#efe2c2', wobble = true } = {}) {
  const G = `stroke="${ink}" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"`;
  const B = {
    sword: `<g ${G}><path d="M24 3 L29 3 L29 8 L13 24 L8 19 Z" fill="${paper}"/><path d="M6 17 L15 26" fill="none"/><path d="M9 23 L4 28" fill="none" stroke-width="3.2"/></g>`,
    book: `<g ${G}><path d="M16 8 C12 5 7 5 3 6 L3 26 C7 25 12 25 16 28 Z" fill="#d9c59c"/><path d="M16 8 C20 5 25 5 29 6 L29 26 C25 25 20 25 16 28 Z" fill="${paper}"/></g>`,
    bag: `<g ${G}><path d="M8 12 C6 18 6 26 10 28 L22 28 C26 26 26 18 24 12 Z" fill="#c09060"/><path d="M8 12 L24 12 L21 7 L11 7 Z" fill="#a8764a"/><path d="M12 12 L16 16 L20 12" fill="none"/></g>`,
    shield: `<path d="M16 3 L27 7 V15 C27 22 22 27 16 29 C10 27 5 22 5 15 V7 Z" fill="#2f7f78" ${G}/>`,
    boot: `<g ${G}><path d="M10 4 L18 4 L18 18 L27 22 L27 28 L8 28 Z" fill="#8a6440"/><path d="M3 12 L8 12 M2 17 L7 17 M4 22 L8 22" fill="none"/></g>`,
    claw: `<path d="M5 26 C6 15 13 7 26 5 C21 12 20 19 22 26 Z" fill="${paper}" ${G}/>`,
    flame: `<path d="M16 3 C21 10 26 13 26 20 C26 26 21 29 16 29 C11 29 6 26 6 20 C6 16 9 13 11 10 C12 14 14 15 16 15 C15 11 15 7 16 3 Z" fill="#e0a93a" ${G}/>`,
    star: `<path d="M16 3 L20 12 L29 13 L22 19 L24 28 L16 23 L8 28 L10 19 L3 13 L12 12 Z" fill="${paper}" ${G}/>`,
    swap: `<g ${G} fill="none"><path d="M5 11 H23 M19 6 L24 11 L19 16"/><path d="M27 21 H9 M13 16 L8 21 L13 26"/></g>`,
  }[name];
  return `<svg width="${size}" height="${size}" viewBox="-2 -2 36 36" style="flex: none"><g ${wobble ? 'filter="url(#wobble)"' : ''}>${B}</g></svg>`;
}

// Filters every mock can use.
function defs(extra = '') {
  document.body.insertAdjacentHTML('afterbegin', `<svg width="0" height="0" style="position:absolute"><defs>
    <filter id="wobble"><feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="5"/><feDisplacementMap in="SourceGraphic" scale="2.4"/></filter>
    <filter id="soft"><feGaussianBlur stdDeviation="3"/></filter>
    <filter id="soft6"><feGaussianBlur stdDeviation="6"/></filter>
    ${extra}</defs></svg>`);
}

// An SVG layer behind a box (overflow visible), returned for drawing into.
function layer(box, w, h) {
  const svg = el('svg', { width: w, height: h, style: 'position:absolute; left:0; top:0; overflow:visible; z-index:0' });
  box.prepend(svg);
  return svg;
}

// The battle screen's content, as in the game: the hero, a Thornback Boar, the main menu.
const DATA = {
  hero: { name: 'Wanderer', lv: 'Lv 3', tag: 'Neutral', hp: [48, 60], mp: [20, 30] },
  foe: {
    name: 'Thornback Boar', lv: 'Lv 2', hp: [46, 46],
    weak: [['Weak:', 'dim'], [' Water', '#3d6fa8'], [' Wind', '#2f8f7a'], [' Energy', '#a08a18']],
    trait: 'Opens with a Thorn Charge.',
    next: ['Next: Thorn Charge', '~18 dmg'],
    gear: [['Gear', 'dim'], ['  Rare 1.0%', 'gold'], ['  Uncommon 5.0%', 'good'], ['  Common 5.6%', 'ink']],
  },
  log: 'A wild Thornback Boar (Lv 2) appears in the Thornwood!',
  buttons: [
    { label: 'Attack', icon: 'sword', main: true },
    { label: 'Skills', icon: 'book', sub: '1 NEW' },
    { label: 'Items', icon: 'bag' },
    { label: 'Defend', icon: 'shield', sub: '-60% dmg, +MP' },
    { label: 'Flee', icon: 'boot', sub: '90%' },
    { label: 'Swap', icon: 'swap', sub: 'No spare', off: true },
  ],
};

// The game's layout (design px): panels, the board, the log and the 3 x 2 buttons.
const LAYOUT = {
  hero: { x: 14, y: 16, w: 420, h: 122 },
  foe: { x: 836, y: 16, w: 430, h: 220 },
  board: { y: 540 },
  log: { x: 22, y: 562, w: 590, h: 140 },
  button: (i) => ({ x: 640 + (i % 3) * 210, y: 566 + Math.floor(i / 3) * 74, w: 196, h: 64 }),
};

function stage() {
  return div('stage', 'position:relative; width:1280px; height:720px; overflow:hidden; background:url(parts/battle_scene.webp) 0 0 / 1280px 720px no-repeat;', document.body);
}
