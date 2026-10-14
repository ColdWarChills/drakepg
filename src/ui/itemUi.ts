import Phaser from 'phaser';
import { DAMAGE_TYPE_LABELS, ELEMENTS, ELEMENT_LABELS } from '../data/elements';
import { db } from '../data/db';
import type { DamageType, Defenses, EquipSlot, ItemDef, Resists, SetBonus, SetDef, Stats } from '../data/types';
import {
  accessoryValue,
  armorSlot,
  grade,
  gradeTier,
  isGearDef,
  lineBonus,
  modTotal,
  modValue,
  rollBonus,
  rolledDamage,
  rolledDefense,
  rolledProcChance,
  rollKeys,
  ROLL_LABELS,
  SIGIL_NAMES,
  type Gear,
  type GearDef,
  type GearTraits,
  type GradeTier,
  type Mod,
  type RollKey,
  type Rolls,
  type SigilTier,
} from '../game/Items';
import { STAT_KEYS, STAT_LABELS } from '../game/Player';
import { activeSetBonuses, setCounts, setOfItem } from '../game/Sets';
import { hasArt } from './art';

/** Border and text color per grade tier. */
export const GRADE_COLORS: Record<GradeTier, { color: number; css: string }> = {
  standard: { color: 0x8c8577, css: '#b3ab9a' },
  fair: { color: 0xe8e2d4, css: '#f3e9d2' },
  fine: { color: 0x5cc85c, css: '#7ddc7d' },
  superior: { color: 0x4a9cf0, css: '#7fb8ff' },
  masterwork: { color: 0xf1c40f, css: '#ffd34d' },
};

/** Sigil glow and name colors. Kept apart from the grade colors (grey, white, green, blue, gold). */
export const SIGIL_COLORS: Record<SigilTier, { color: number; css: string }> = {
  dawn: { color: 0xff7a2e, css: '#ff9550' },
  dusk: { color: 0xb070ff, css: '#c79bff' },
  eclipse: { color: 0xff3358, css: '#ff6680' },
};

/** Particle colors and pace per Sigil tier: rarer tiers sparkle faster. Used on icons and on the weapon in hand. */
export const SIGIL_FX: Record<SigilTier, { tint: number[]; moteEvery: number; twinkleEvery: number }> = {
  dawn: { tint: [0xff7a2e, 0xffb070, 0xffe0b0], moteEvery: 160, twinkleEvery: 520 },
  dusk: { tint: [0xb070ff, 0xd4b0ff, 0xffffff], moteEvery: 95, twinkleEvery: 340 },
  eclipse: { tint: [0xff3358, 0xff8a9c, 0xffd0d8], moteEvery: 55, twinkleEvery: 220 },
};

/** Blue lines, like the mods themselves. */
export const MOD_CSS = '#7fb8ff';

export interface GradeInfo {
  grade: number;
  tier: GradeTier;
  name: string;
  color: number;
  css: string;
  sigil?: SigilTier;
  /** Name color: the Sigil's if it has one, else the grade's. */
  nameCss: string;
  /** Border color: the Sigil's if it has one, else the grade's. */
  frame: number;
}

/** Grade, tier and colors of an item. Consumables count as Standard. */
export function gradeInfo(def: ItemDef, g?: Partial<GearTraits>): GradeInfo {
  const gr = isGearDef(def) && g ? grade(def, g.rolls ?? {}, g.sigil) : 0;
  const t = gradeTier(gr);
  const colors = GRADE_COLORS[t.tier];
  const sigil = isGearDef(def) ? g?.sigil : undefined;
  return {
    grade: gr,
    ...t,
    ...colors,
    sigil,
    nameCss: sigil ? SIGIL_COLORS[sigil].css : colors.css,
    frame: sigil ? SIGIL_COLORS[sigil].color : colors.color,
  };
}

/** "Fine 52%" or "Standard". */
export function gradeLabel(info: GradeInfo): string {
  return info.tier === 'standard' ? 'Standard' : `${info.name} ${info.grade}%`;
}

/** "+3 Iron Sword" for reinforced gear, else just the name. */
export function itemName(def: ItemDef, g?: Partial<GearTraits>): string {
  return g?.plus ? `+${g.plus} ${def.name}` : def.name;
}

/** Item name plus its Sigil and grade, e.g. "Cinder Fang (Sigil of Dusk, Fine 52%)"; consumables are just the name. */
export function lootName(def: ItemDef, g?: Partial<GearTraits>): string {
  if (!isGearDef(def) || !g) return def.name;
  const info = gradeInfo(def, g);
  return `${itemName(def, g)} (${info.sigil ? `${SIGIL_NAMES[info.sigil]}, ` : ''}${gradeLabel(info)})`;
}

/** "✦ Sigil of Dusk" */
export function sigilLabel(tier: SigilTier): string {
  return `✦ ${SIGIL_NAMES[tier]}`;
}

/**
 * Pulsing glow in the Sigil's color. Filters are WebGL only; without them nothing happens.
 */
export function addSigilGlow(scene: Phaser.Scene, obj: Phaser.GameObjects.Image | Phaser.GameObjects.Container, tier: SigilTier, size = 1): void {
  obj.enableFilters();
  // Distance is in the object's own pixels, so scaled-down art needs more of it.
  const scale = Math.max(0.1, Math.abs(obj.scaleX));
  const glow = obj.filters?.internal.addGlow(SIGIL_COLORS[tier].color, 2, 0, 1, false, 10, Math.round((8 * size) / scale));
  if (!glow) return;
  const peak = tier === 'eclipse' ? 6 : tier === 'dusk' ? 4.5 : 3.5;
  const tween = scene.tweens.add({ targets: glow, outerStrength: peak, duration: tier === 'eclipse' ? 700 : 1100, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  obj.once(Phaser.GameObjects.Events.DESTROY, () => tween.remove());
}

/**
 * A Sigil item's icon comes alive: the pulsing glow, stars twinkling on it and a slow swell.
 * `size` is the icon's size in px (the stars stay inside it).
 */
export function animateSigilIcon(scene: Phaser.Scene, icon: Phaser.GameObjects.Container, tier: SigilTier, size: number): void {
  addSigilGlow(scene, icon, tier, size / 72);
  const fx = SIGIL_FX[tier];
  const half = size * 0.42;
  const stars = scene.add.particles(0, 0, 'fx_star', {
    x: { min: -half, max: half },
    y: { min: -half, max: half },
    // Each star swells in and out over its life: a twinkle.
    scale: { onEmit: () => 0, onUpdate: (_p, _k, t) => Math.sin(t * Math.PI) * 0.55 },
    alpha: { start: 1, end: 0.6 },
    rotate: { min: 0, max: 90 },
    lifespan: 650,
    frequency: fx.twinkleEvery,
    tint: [0xffffff, ...fx.tint],
    blendMode: 'ADD',
  });
  icon.add(stars);
  if (tier !== 'dawn') {
    icon.add(
      scene.add.particles(0, half * 0.6, 'fx_soft', {
        x: { min: -half * 0.8, max: half * 0.8 },
        speedY: { min: -26, max: -12 },
        scale: { start: 0.35, end: 0 },
        alpha: { start: 0.9, end: 0 },
        lifespan: 1100,
        frequency: fx.moteEvery * 2,
        tint: fx.tint,
        blendMode: 'ADD',
      }),
    );
  }
  const tween = scene.tweens.add({ targets: icon, scale: 1.07, duration: tier === 'eclipse' ? 700 : 1100, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  icon.once(Phaser.GameObjects.Events.DESTROY, () => tween.remove());
}

// --- Icons ---

/** Darken a 0xRRGGBB color. */
function shade(color: number, f: number): number {
  const c = Phaser.Display.Color.IntegerToColor(color);
  return Phaser.Display.Color.GetColor(Math.round(c.red * f), Math.round(c.green * f), Math.round(c.blue * f));
}

/** Square item icon centered on (0, 0). Uses `icon_<id>` art if it exists, else the weapon's art, else a drawn shape. */
export function drawItemIcon(scene: Phaser.Scene, def: ItemDef, size: number): Phaser.GameObjects.Container {
  const c = scene.add.container(0, 0);
  const color = def.iconColor ?? 0x9a8a6a;
  const iconKey = `icon_${def.id}`;
  if (hasArt(scene, iconKey)) {
    const img = scene.add.image(0, 0, iconKey);
    img.setScale((size * 0.95) / Math.max(img.width, img.height));
    c.add(img);
    return c;
  }
  if (def.type === 'weapon' && def.art && hasArt(scene, def.art)) {
    // Upright art laid diagonally, like most inventory icons.
    const img = scene.add.image(0, 0, def.art).setAngle(45);
    img.setScale((size * 1.3) / (img.width + img.height));
    c.add(img);
    return c;
  }
  const g = scene.add.graphics();
  const s = size / 64;
  if (def.type === 'weapon') {
    g.fillStyle(0xd8dde3).fillRect(-4 * s, -28 * s, 8 * s, 40 * s);
    g.fillStyle(0x8a6d3b).fillRect(-14 * s, 12 * s, 28 * s, 5 * s).fillRect(-3 * s, 17 * s, 6 * s, 12 * s);
    g.setAngle(45);
  } else if (def.type === 'armor' && armorSlot(def) !== 'body') {
    drawArmorPiece(g, armorSlot(def) as 'helm' | 'gloves' | 'boots', color, s);
  } else if (def.type === 'accessory') {
    if (def.slot === 'ring') {
      g.lineStyle(7 * s, color).strokeCircle(0, 6 * s, 15 * s);
      g.lineStyle(2 * s, shade(color, 0.5)).strokeCircle(0, 6 * s, 19 * s).strokeCircle(0, 6 * s, 11 * s);
      g.fillStyle(0xbfe9ff).fillCircle(0, -13 * s, 7 * s);
      g.fillStyle(0xffffff, 0.6).fillCircle(-2 * s, -15 * s, 2.5 * s);
    } else {
      g.lineStyle(3 * s, 0xc9a84a).beginPath().arc(0, -14 * s, 20 * s, Phaser.Math.DegToRad(200), Phaser.Math.DegToRad(340), false).strokePath();
      g.fillStyle(color).fillCircle(0, 12 * s, 12 * s);
      g.lineStyle(2 * s, shade(color, 0.5)).strokeCircle(0, 12 * s, 12 * s);
      g.fillStyle(0xffffff, 0.45).fillCircle(-4 * s, 8 * s, 3.5 * s);
    }
  } else if (def.type === 'armor') {
    const pts = [
      [-12, -24], [-4, -18], [4, -18], [12, -24], [26, -16], [22, -4], [16, -6], [16, 24], [-16, 24], [-16, -6], [-22, -4], [-26, -16],
    ].map(([x, y]) => new Phaser.Math.Vector2(x * s, y * s));
    g.fillStyle(color).fillPoints(pts, true);
    g.lineStyle(2, shade(color, 0.5)).strokePoints(pts, true);
    g.fillStyle(shade(color, 0.6)).fillRect(-16 * s, 6 * s, 32 * s, 5 * s);
    g.fillStyle(0xffffff, 0.18).fillRect(-12 * s, -14 * s, 6 * s, 18 * s);
  } else if (def.type === 'scroll') {
    // A rolled parchment with a wax seal.
    g.fillStyle(shade(color, 0.75)).fillEllipse(-20 * s, 0, 10 * s, 40 * s).fillEllipse(20 * s, 0, 10 * s, 40 * s);
    g.fillStyle(color).fillRect(-20 * s, -18 * s, 40 * s, 36 * s);
    g.lineStyle(2, shade(color, 0.5)).strokeRect(-20 * s, -18 * s, 40 * s, 36 * s);
    g.lineStyle(2 * s, shade(color, 0.55)).lineBetween(-12 * s, -8 * s, 12 * s, -8 * s).lineBetween(-12 * s, 0, 8 * s, 0);
    g.fillStyle(0xb03a2e).fillCircle(8 * s, 10 * s, 7 * s);
    g.fillStyle(0xffffff, 0.35).fillCircle(6 * s, 8 * s, 2 * s);
  } else if (def.type === 'material' || def.type === 'stone') {
    // A faceted chunk.
    const pts = [[0, -24], [20, -8], [14, 20], [-14, 20], [-20, -8]].map(([x, y]) => new Phaser.Math.Vector2(x * s, y * s));
    g.fillStyle(color).fillPoints(pts, true);
    g.lineStyle(2, shade(color, 0.5)).strokePoints(pts, true);
    g.fillStyle(0xffffff, 0.35).fillTriangle(0, -24 * s, -20 * s, -8 * s, -4 * s, -4 * s);
    g.fillStyle(shade(color, 0.7)).fillTriangle(14 * s, 20 * s, 20 * s, -8 * s, 2 * s, 4 * s);
  } else {
    g.fillStyle(0xdfe8f0, 0.55).fillRect(-6 * s, -24 * s, 12 * s, 14 * s);
    g.fillStyle(0x8a6d3b).fillRect(-7 * s, -28 * s, 14 * s, 6 * s);
    g.fillStyle(0xdfe8f0, 0.55).fillCircle(0, 6 * s, 20 * s);
    g.fillStyle(color).fillCircle(0, 9 * s, 16 * s);
    g.fillStyle(0xffffff, 0.45).fillCircle(-7 * s, 2 * s, 5 * s);
  }
  c.add(g);
  return c;
}

/** Drawn helm, gloves or boots (until `icon_<id>` art exists). */
function drawArmorPiece(g: Phaser.GameObjects.Graphics, slot: 'helm' | 'gloves' | 'boots', color: number, s: number): void {
  const dark = shade(color, 0.5);
  const poly = (pts: number[][]) => {
    const v = pts.map(([x, y]) => new Phaser.Math.Vector2(x * s, y * s));
    g.fillStyle(color).fillPoints(v, true);
    g.lineStyle(2, dark).strokePoints(v, true);
  };
  if (slot === 'helm') {
    poly([[-20, 18], [-22, -2], [-14, -18], [0, -24], [14, -18], [22, -2], [20, 18], [10, 18], [10, 4], [-10, 4], [-10, 18]]);
    g.fillStyle(dark).fillRect(-12 * s, -2 * s, 24 * s, 5 * s);
    g.fillStyle(0xffffff, 0.2).fillRect(-12 * s, -16 * s, 5 * s, 12 * s);
  } else if (slot === 'gloves') {
    for (const dx of [-12, 12]) {
      poly([[dx - 9, 22], [dx - 9, -2], [dx - 10, -14], [dx - 5, -16], [dx - 3, -6], [dx - 1, -20], [dx + 3, -20], [dx + 4, -6], [dx + 6, -16], [dx + 10, -12], [dx + 9, 22]]);
      g.fillStyle(dark).fillRect((dx - 9) * s, 14 * s, 18 * s, 4 * s);
    }
  } else {
    for (const dx of [-11, 11]) {
      poly([[dx - 7, -22], [dx + 5, -22], [dx + 5, 10], [dx + 14, 14], [dx + 14, 22], [dx - 7, 22]]);
      g.fillStyle(dark).fillRect((dx - 7) * s, 18 * s, 21 * s, 4 * s);
    }
  }
}

// --- Text ---

function bonusText(stats?: Partial<Stats>): string {
  if (!stats) return '';
  return STAT_KEYS.filter((k) => stats[k])
    .map((k) => `+${stats[k]} ${STAT_LABELS[k]}`)
    .join('  ');
}

/** "DEF Melee 9 · Pierce 10 · Magic 4" */
export function defenseText(d: Defenses): string {
  return `DEF ${(['melee', 'pierce', 'magic'] as const).map((t) => `${DAMAGE_TYPE_LABELS[t]} ${d[t] ?? 0}`).join(' · ')}`;
}

/** "  Fire +15, Ice -10" or '' if none. */
export function resistText(r: Resists | undefined, prefix = ''): string {
  if (!r) return '';
  const parts = ELEMENTS.filter((e) => (r[e] ?? 0) + (r.all ?? 0) !== 0).map((e) => {
    const v = (r[e] ?? 0) + (r.all ?? 0);
    return `${ELEMENT_LABELS[e]} ${v > 0 ? '+' : ''}${v}`;
  });
  return parts.length ? `${prefix}${parts.join(', ')}` : '';
}

/** The item's numbers with its rolls applied, one fact per line. Mods are listed separately (`modLines`). */
export function itemLines(def: ItemDef, g: GearTraits = { rolls: {} }): string[] {
  switch (def.type) {
    case 'weapon': {
      const [min, max] = rolledDamage(def, g);
      const lines = [
        `${min}-${max} ${DAMAGE_TYPE_LABELS[def.damageType]} damage`,
        def.element ? `Element: ${ELEMENT_LABELS[def.element]}` : 'Element: Neutral',
      ];
      if (def.stats) lines.push(bonusText(def.stats));
      if (def.proc) lines.push(`Special: ${def.proc.name}, ${Math.round(rolledProcChance(def, g) * 100)}% on Attack`);
      return lines;
    }
    case 'armor': {
      const d = { melee: rolledDefense(def, g, 'melee'), pierce: rolledDefense(def, g, 'pierce'), magic: rolledDefense(def, g, 'magic') };
      const lines = [defenseText(d)];
      const res = resistText(def.resists, 'Resists ');
      if (res) lines.push(res);
      if (def.stats) lines.push(bonusText(def.stats));
      return lines;
    }
    case 'accessory': {
      // Everything scales with the Power roll (and a Sigil or +).
      const lines: string[] = [];
      const stats: Partial<Stats> = {};
      for (const k of STAT_KEYS) if (def.stats?.[k]) stats[k] = accessoryValue(def.stats[k]!, g);
      if (Object.keys(stats).length) lines.push(bonusText(stats));
      if (def.hp) lines.push(`+${accessoryValue(def.hp, g)} Max HP`);
      if (def.mp) lines.push(`+${accessoryValue(def.mp, g)} Max MP`);
      if (def.crit) lines.push(`+${accessoryValue(def.crit, g, 1).toFixed(1)}% Crit chance`);
      const res: Resists = {};
      for (const [k, v] of Object.entries(def.resists ?? {}) as [keyof Resists, number][]) res[k] = accessoryValue(v, g);
      const rt = resistText(res, 'Resists ');
      if (rt) lines.push(rt);
      return lines;
    }
    case 'consumable':
    case 'material':
    case 'stone':
      return [def.description];
    case 'scroll':
      // The card's description says it all.
      return [];
  }
}

/** What a roll line adds at its quality, e.g. "+11%". */
export function rollEffectText(def: GearDef, key: RollKey, rolls: Rolls): string {
  const b = lineBonus(def, key, rolls) * 100;
  return `+${key === 'crit' ? b.toFixed(1) : Math.round(b)}%`;
}

export interface RollLine {
  key: RollKey;
  label: string;
  effect: string;
  /** 0-100 */
  quality: number;
}

export function rollLines(def: GearDef, rolls: Rolls, sigil?: SigilTier): RollLine[] {
  return rollKeys(def, sigil).map((key) => ({ key, label: ROLL_LABELS[key], effect: rollEffectText(def, key, rolls), quality: rolls[key] ?? 0 }));
}

/** "+3 STR", "+2.5% Crit chance", "+14 Fire resist" */
export function modText(mod: Mod): string {
  const v = modValue(mod);
  switch (mod.key) {
    case 'crit':
      return `+${v.toFixed(1)}% Crit chance`;
    case 'hp':
      return `+${v} Max HP`;
    case 'mp':
      return `+${v} Max MP`;
    case 'resist':
      return `+${v} ${mod.element ? ELEMENT_LABELS[mod.element] : ''} resist`;
    default:
      return `+${v} ${STAT_LABELS[mod.key]}`;
  }
}

export interface ModLine {
  text: string;
  /** 0-100 */
  quality: number;
}

export function modLines(g: GearTraits): ModLine[] {
  return (g.mods ?? []).map((m) => ({ text: modText(m), quality: m.roll }));
}

/** "+8% Max HP, Earth +15" */
export function setBonusText(b: SetBonus): string {
  const parts: string[] = [];
  const stats = bonusText(b.stats);
  if (stats) parts.push(stats);
  if (b.hpPct) parts.push(`+${b.hpPct}% Max HP`);
  if (b.defensePct) parts.push(`+${b.defensePct}% defense`);
  if (b.damagePct) parts.push(`+${b.damagePct}% damage`);
  if (b.crit) parts.push(`+${b.crit}% Crit chance`);
  const res = resistText(b.resists, 'Resists ');
  if (res) parts.push(res);
  return parts.join(', ');
}

export interface SetCardLine {
  text: string;
  /** Worn piece or active bonus: drawn bright; the rest dim. */
  on: boolean;
}

/** The item's set for its card: name with pieces worn, the pieces, and each bonus. Null if it has no set. */
export function setCard(itemId: string, equipment: Record<EquipSlot, Gear | null>): { set: SetDef; title: string; lines: SetCardLine[] } | null {
  const set = setOfItem(itemId);
  if (!set) return null;
  const count = setCounts(equipment).get(set.id) ?? 0;
  const worn = new Set(Object.values(equipment).filter((g): g is Gear => !!g).map((g) => g.itemId));
  return {
    set,
    title: `${set.name} set (${count}/${set.pieces.length})`,
    lines: [
      // "Helm ✓ · Gloves · Boots ✓ · Band": worn pieces ticked.
      { text: set.pieces.map((id) => `${db.item(id).name.replace(`${set.name} `, '')}${worn.has(id) ? ' ✓' : ''}`).join(' · '), on: false },
      ...set.bonuses.map((b) => ({ text: `(${b.pieces}) ${setBonusText(b)}`, on: count >= b.pieces })),
    ],
  };
}

/** Set bonuses that wearing a piece in `slot` would switch on (good) or off (bad). */
export function setCompareLines(equipment: Record<EquipSlot, Gear | null>, slot: EquipSlot, piece: Gear): CompareLine[] {
  const key = (x: { set: SetDef; bonus: SetBonus }) => `${x.set.id}:${x.bonus.pieces}`;
  const before = activeSetBonuses(equipment);
  const after = activeSetBonuses({ ...equipment, [slot]: piece });
  const was = new Set(before.map(key));
  const now = new Set(after.map(key));
  return [
    ...after.filter((x) => !was.has(key(x))).map((x) => ({ text: `${x.set.name} (${x.bonus.pieces}): ${setBonusText(x.bonus)}`, tone: 'good' as const })),
    ...before.filter((x) => !now.has(key(x))).map((x) => ({ text: `Loses ${x.set.name} (${x.bonus.pieces})`, tone: 'bad' as const })),
  ];
}

/** "+40 HP, +20 MP" */
export function restoreText(r: { hp: number; mp: number }): string {
  const parts = [r.hp ? `+${r.hp} HP` : '', r.mp ? `+${r.mp} MP` : ''].filter(Boolean);
  return parts.length ? parts.join(', ') : 'No effect';
}

// --- Compare ---

interface GearNumbers {
  avgDamage: number;
  crit: number;
  special: number;
  defense: Required<Defenses>;
  hp: number;
  hpFlat: number;
  mp: number;
  stats: Partial<Stats>;
  resists: Resists;
}

/** `statBonus` is the hero's stat damage for a damage type, so weapons compare by real attack. */
function numbers(def: GearDef | null, g: GearTraits, statBonus: (type: DamageType) => number): GearNumbers {
  const out: GearNumbers = { avgDamage: 0, crit: 0, special: 0, defense: { melee: 0, pierce: 0, magic: 0 }, hp: 0, hpFlat: 0, mp: 0, stats: {}, resists: {} };
  if (!def) return out;
  out.stats = { ...def.stats };
  for (const k of STAT_KEYS) out.stats[k] = (out.stats[k] ?? 0) + modTotal([g], k);
  out.crit = modTotal([g], 'crit');
  out.hpFlat = modTotal([g], 'hp');
  out.mp = modTotal([g], 'mp');
  out.resists = def.type === 'armor' ? { ...def.resists } : {};
  for (const m of g.mods ?? []) if (m.key === 'resist' && m.element) out.resists[m.element] = (out.resists[m.element] ?? 0) + modValue(m);
  if (def.type === 'weapon') {
    const [min, max] = rolledDamage(def, g);
    out.avgDamage = (min + max) / 2 + statBonus(def.damageType);
    out.crit += rollBonus('crit', g.rolls) * 100;
    out.special = rolledProcChance(def, g) * 100;
  } else if (def.type === 'armor') {
    for (const t of ['melee', 'pierce', 'magic'] as const) out.defense[t] = rolledDefense(def, g, t);
    out.hp = lineBonus(def, 'hp', g.rolls) * 100;
  } else {
    // Accessories: every number scales with Power.
    for (const k of STAT_KEYS) out.stats[k] = (out.stats[k] ?? 0) - (def.stats?.[k] ?? 0) + accessoryValue(def.stats?.[k] ?? 0, g);
    out.hpFlat += accessoryValue(def.hp ?? 0, g);
    out.mp += accessoryValue(def.mp ?? 0, g);
    out.crit += accessoryValue(def.crit ?? 0, g, 1);
    for (const [k, v] of Object.entries(def.resists ?? {}) as [keyof Resists, number][]) out.resists[k] = (out.resists[k] ?? 0) + accessoryValue(v, g);
  }
  return out;
}

export interface CompareLine {
  text: string;
  /** Up, down, or just different (damage type, element). */
  tone: 'good' | 'bad' | 'neutral';
}

/**
 * Differences if `def` replaced what is equipped in its slot. Empty = no change.
 * Attack includes the hero's stat damage, so a sword compares honestly against a staff for a mage.
 */
export function compareGear(
  def: GearDef,
  g: GearTraits,
  equipped: { def: GearDef; gear: GearTraits } | null,
  statBonus: (type: DamageType) => number,
): CompareLine[] {
  const a = numbers(def, g, statBonus);
  const b = numbers(equipped?.def ?? null, equipped?.gear ?? { rolls: {} }, statBonus);
  const lines: CompareLine[] = [];
  const add = (label: string, delta: number, unit = '', digits = 0) => {
    const rounded = Number(delta.toFixed(digits));
    if (rounded === 0) return;
    lines.push({ text: `${label} ${rounded > 0 ? '+' : ''}${rounded.toFixed(digits)}${unit}`, tone: rounded > 0 ? 'good' : 'bad' });
  };
  add('Attack (avg)', a.avgDamage - b.avgDamage, '', 1);
  add('Crit', a.crit - b.crit, '%', 1);
  add('Special chance', a.special - b.special, '%');
  for (const t of ['melee', 'pierce', 'magic'] as const) add(`DEF ${DAMAGE_TYPE_LABELS[t]}`, a.defense[t] - b.defense[t]);
  add('Max HP', a.hp - b.hp, '%');
  add('Max HP', a.hpFlat - b.hpFlat);
  add('Max MP', a.mp - b.mp);
  for (const k of STAT_KEYS) add(STAT_LABELS[k], (a.stats[k] ?? 0) - (b.stats[k] ?? 0));
  for (const e of ELEMENTS) add(`${ELEMENT_LABELS[e]} resist`, (a.resists[e] ?? 0) - (b.resists[e] ?? 0));
  if (def.type === 'weapon' && equipped?.def.type === 'weapon') {
    const was = equipped.def;
    if (was.damageType !== def.damageType) lines.push({ text: `Damage type: ${DAMAGE_TYPE_LABELS[def.damageType]} (was ${DAMAGE_TYPE_LABELS[was.damageType]})`, tone: 'neutral' });
    if (was.element !== def.element) {
      lines.push({ text: `Element: ${def.element ? ELEMENT_LABELS[def.element] : 'Neutral'} (was ${was.element ? ELEMENT_LABELS[was.element] : 'Neutral'})`, tone: 'neutral' });
    }
  }
  return lines;
}
