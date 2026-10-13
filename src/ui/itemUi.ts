import Phaser from 'phaser';
import { DAMAGE_TYPE_LABELS, ELEMENTS, ELEMENT_LABELS } from '../data/elements';
import type { ArmorDef, DamageType, Defenses, ItemDef, Resists, Stats, WeaponDef } from '../data/types';
import {
  grade,
  gradeTier,
  isGearDef,
  modTotal,
  modValue,
  rollBonus,
  rolledDamage,
  rolledDefense,
  rolledProcChance,
  rollKeys,
  ROLL_LABELS,
  SIGIL_NAMES,
  type GearDef,
  type GearTraits,
  type GradeTier,
  type Mod,
  type RollKey,
  type Rolls,
  type SigilTier,
} from '../game/Items';
import { STAT_KEYS, STAT_LABELS } from '../game/Player';
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
  const gr = isGearDef(def) && g ? grade(def, g.rolls ?? {}) : 0;
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
  } else if (def.type === 'armor') {
    const pts = [
      [-12, -24], [-4, -18], [4, -18], [12, -24], [26, -16], [22, -4], [16, -6], [16, 24], [-16, 24], [-16, -6], [-22, -4], [-26, -16],
    ].map(([x, y]) => new Phaser.Math.Vector2(x * s, y * s));
    g.fillStyle(color).fillPoints(pts, true);
    g.lineStyle(2, shade(color, 0.5)).strokePoints(pts, true);
    g.fillStyle(shade(color, 0.6)).fillRect(-16 * s, 6 * s, 32 * s, 5 * s);
    g.fillStyle(0xffffff, 0.18).fillRect(-12 * s, -14 * s, 6 * s, 18 * s);
  } else if (def.type === 'material') {
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
    case 'consumable':
    case 'material':
      return [def.description];
  }
}

/** What a roll line adds at its quality, e.g. "+11%". */
export function rollEffectText(key: RollKey, rolls: Rolls): string {
  const b = rollBonus(key, rolls) * 100;
  return `+${key === 'crit' ? b.toFixed(1) : Math.round(b)}%`;
}

export interface RollLine {
  key: RollKey;
  label: string;
  effect: string;
  /** 0-100 */
  quality: number;
}

export function rollLines(def: GearDef, rolls: Rolls): RollLine[] {
  return rollKeys(def).map((key) => ({ key, label: ROLL_LABELS[key], effect: rollEffectText(key, rolls), quality: rolls[key] ?? 0 }));
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
  } else {
    for (const t of ['melee', 'pierce', 'magic'] as const) out.defense[t] = rolledDefense(def, g, t);
    out.hp = rollBonus('hp', g.rolls) * 100;
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
  equipped: { def: WeaponDef | ArmorDef; gear: GearTraits } | null,
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
