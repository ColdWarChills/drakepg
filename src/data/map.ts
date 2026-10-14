import type { MapAreaDef } from './types';

/**
 * Map pages, one per stretch of the world. Each room names its page and grid spot in `RoomDef.map`;
 * exits to rooms on another page show as an arrow off the edge with that room's name.
 */
export const MAP_AREAS: MapAreaDef[] = [
  { id: 'road', name: 'The Old Road' },
  { id: 'thornwood', name: 'Brindlemoor and the Thornwood' },
  { id: 'fen', name: 'Mirewater Fen' },
  { id: 'frostfang', name: 'Frostfang Mountains' },
];
