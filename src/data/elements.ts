import type { DamageType, Element } from './types';

export const ELEMENTS: readonly Element[] = ['fire', 'water', 'ice', 'wind', 'earth', 'energy', 'light', 'dark'];

export const ELEMENT_LABELS: Record<Element, string> = {
  fire: 'Fire',
  water: 'Water',
  ice: 'Ice',
  wind: 'Wind',
  earth: 'Earth',
  energy: 'Energy',
  light: 'Light',
  dark: 'Dark',
};

/**
 * Design rule for monsters: a creature of an element resists it and is weak to its opposite.
 * Individual monsters can add more (forest things also burn).
 */
export const OPPOSITE: Record<Element, Element> = {
  fire: 'ice',
  ice: 'fire',
  water: 'energy',
  energy: 'water',
  earth: 'wind',
  wind: 'earth',
  light: 'dark',
  dark: 'light',
};

/** Particle palettes per element, bright to dark. Neutral uses `NEUTRAL_COLORS`. */
export const ELEMENT_COLORS: Record<Element, number[]> = {
  fire: [0xfff2a8, 0xffa733, 0xff5a1f, 0x8a1c00],
  water: [0xe0f7ff, 0x6ccfff, 0x1f8fe0, 0x0b3f7a],
  ice: [0xffffff, 0xbff4ff, 0x5ec8ff, 0x1f6fb2],
  wind: [0xf4fff4, 0xb8f5c8, 0x6fd6a0, 0x2f8f6a],
  earth: [0xf3e2b8, 0xc9a25a, 0x8a6a2c, 0x4a3414],
  energy: [0xffffff, 0xfff27a, 0xffd21f, 0xa98bff],
  light: [0xffffff, 0xfff8d6, 0xffe58a, 0xd9b84a],
  dark: [0xe6d6ff, 0xa27cff, 0x5a2d9c, 0x1e0b38],
};
export const NEUTRAL_COLORS = [0xe6f0ff, 0x9fb7ff, 0x7a5cff];

/** Text colors for element names and damage numbers. */
export const ELEMENT_TEXT: Record<Element, string> = {
  fire: '#ff9a4d',
  water: '#5cc3ff',
  ice: '#aeeeff',
  wind: '#8fe8b4',
  earth: '#d8b46a',
  energy: '#ffe14d',
  light: '#fff2b0',
  dark: '#c39bff',
};

export const DAMAGE_TYPE_LABELS: Record<DamageType, string> = {
  melee: 'Melee',
  pierce: 'Pierce',
  magic: 'Magic',
};
