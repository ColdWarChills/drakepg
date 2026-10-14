import type { StatusDef } from './types';

/** Effects that skills, monster attacks and weapon specials can apply. Durations are set by whoever applies them. */
export const STATUSES: StatusDef[] = [
  // Damage over time
  { id: 'burn', name: 'Burn', harmful: true, description: 'Takes Fire damage each turn.', dot: { power: 0.3, element: 'fire' } },
  { id: 'poison', name: 'Poison', harmful: true, description: 'Takes damage each turn. Ignores resistances.', dot: { power: 0.25 }, tint: 0x8fe36b },
  { id: 'bleed', name: 'Bleed', harmful: true, description: 'Takes damage each turn from the weapon that cut it.', dot: { power: 0.35, element: 'weapon' }, tint: 0xe03a3a },
  { id: 'spores', name: 'Spores', harmful: true, description: 'Choking spores. Earth damage each turn.', dot: { power: 0.3, element: 'earth' } },
  { id: 'windchill', name: 'Windchill', harmful: true, description: 'Freezing wind. Wind damage each turn.', dot: { power: 0.3, element: 'wind' } },
  { id: 'shadow_rot', name: 'Shadow Rot', harmful: true, description: 'Dark damage each turn and -15 to all resistances.', dot: { power: 0.3, element: 'dark' }, resists: { all: -15 } },

  // Control
  { id: 'stun', name: 'Stun', harmful: true, description: 'Loses the next turn.', stun: true, tint: 0xffe066 },
  { id: 'chill', name: 'Chill', harmful: true, description: '-15% hit chance.', hitChance: -0.15, tint: 0xbff4ff },
  { id: 'blind', name: 'Blind', harmful: true, description: '-30% hit chance.', hitChance: -0.3, tint: 0x8a7aa8 },
  { id: 'shock', name: 'Shock', harmful: true, description: 'Takes 15% more damage.', damageTaken: 0.15, tint: 0xffe14d },

  // Debuffs
  { id: 'weaken', name: 'Weaken', harmful: true, description: 'Deals 25% less damage.', damageDealt: -0.25, tint: 0xb08cff },
  { id: 'expose', name: 'Expose', harmful: true, description: '-25 to all resistances.', resists: { all: -25 }, tint: 0xff9a4d },
  { id: 'marked', name: 'Marked', harmful: true, description: 'Takes 30% more damage.', damageTaken: 0.3, tint: 0xff4d4d },

  // Buffs
  { id: 'war_cry', name: 'War Cry', harmful: false, description: 'Deals 30% more damage.', damageDealt: 0.3, tint: 0xffb347 },
  { id: 'iron_skin', name: 'Iron Skin', harmful: false, description: 'Takes 40% less damage.', damageTaken: -0.4, tint: 0xc8d6e5 },
  { id: 'stone_skin', name: 'Stone Skin', harmful: false, description: 'Takes 30% less damage, +25 Earth resistance.', damageTaken: -0.3, resists: { earth: 25 }, tint: 0xc9a25a },
  { id: 'regen', name: 'Regen', harmful: false, description: 'Heals 8% of max HP each turn.', regen: 0.08, tint: 0x7dff9a },
  { id: 'focus', name: 'Focus', harmful: false, description: '+25% crit chance.', critChance: 0.25, tint: 0xffe08a },
  { id: 'evasive', name: 'Evasive', harmful: false, description: 'Attacks against it have -40% hit chance.', evasion: 0.4, tint: 0xb8f5c8 },
  { id: 'bark_hide', name: 'Bark Hide', harmful: false, description: 'Takes 25% less damage.', damageTaken: -0.25, tint: 0xa0784a },
];
