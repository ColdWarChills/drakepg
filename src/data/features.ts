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
    id: 'weapon_swap',
    name: 'Spare weapon',
    text: 'Carry a second weapon in the Spare slot (Inventory) and swap to it mid-fight with Swap. It costs your turn, so bring one of the element your foe is weak to.',
    unlockedBy: 'heart_of_thorns',
  },
  {
    id: 'materials',
    name: 'Materials',
    text: 'Monsters now drop parts and elemental essences. They sit in their own pouch (Inventory, Materials tab). Keep them: they will matter soon.',
    unlockedBy: 'heart_of_thorns',
    // Off while crafting is: only quests that ask for a material make it drop.
    disabled: true,
  },
  // After Chapter 2
  {
    id: 'forge',
    name: "Hulda's forge",
    text: 'Hulda the smith has opened her forge in Brindlemoor: craft gear from materials, reinforce it up to +10, hone its quality and salvage what you do not need.',
    unlockedBy: 'eye_of_the_storm',
    // Crafting is off for now: gear gets stronger with stones from the shops instead.
    disabled: true,
  },
  {
    id: 'sigils',
    name: 'Sigils',
    text: 'Dropped gear can now be a rare Sigil version: stronger, glowing, and with extra lines (Dawn +1, Dusk +2, Eclipse +3). Bosses drop them most.',
    unlockedBy: 'eye_of_the_storm',
  },
  {
    id: 'mimics',
    name: 'Mimics',
    text: 'Some chests bite. Watch for one that breathes, or whose lid twitches when you come near.',
    unlockedBy: 'eye_of_the_storm',
  },
];
