import type { FeatureDef } from './types';

/**
 * Systems that open up chapter by chapter, so the first hour stays simple. Each unlocks when its quest is turned in
 * and is announced once with a "New!" popup. Chapter 1 has fights, elements, the shop and rolled gear only.
 */
export const FEATURES: FeatureDef[] = [
  // After Chapter 1
  {
    id: 'elites',
    name: 'Elite monsters',
    text: 'Now and then a monster spawns as an Elite: bigger, glowing gold, with one affix. Tougher, but 3x XP and gold and a guaranteed piece of gear.',
    unlockedBy: 'heart_of_thorns',
  },
  {
    id: 'chests',
    name: 'Treasure chests',
    text: 'Some monster rooms hide a chest. Walk into it for gold, materials and often gear.',
    unlockedBy: 'heart_of_thorns',
  },
  {
    id: 'materials',
    name: 'Materials',
    text: 'Monsters now drop parts and elemental essences. They sit in their own pouch (Inventory, Materials tab). Keep them: they will matter soon.',
    unlockedBy: 'heart_of_thorns',
  },
  // After Chapter 2
  {
    id: 'forge',
    name: "Hulda's forge",
    text: 'Hulda the smith has opened her forge in Brindlemoor: craft gear from materials, reinforce it up to +10, hone its quality and salvage what you do not need.',
    unlockedBy: 'eye_of_the_storm',
  },
  {
    id: 'sigils',
    name: 'Sigils and mods',
    text: 'Dropped gear can now be a rare Sigil version (stronger, glowing) or carry extra blue lines (mods). Bosses drop them most.',
    unlockedBy: 'eye_of_the_storm',
  },
  {
    id: 'mimics',
    name: 'Mimics',
    text: 'Some chests bite. Watch for one that breathes, or whose lid twitches when you come near.',
    unlockedBy: 'eye_of_the_storm',
  },
];
