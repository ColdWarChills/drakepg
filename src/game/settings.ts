/**
 * Per-browser preferences (not part of the save). Storage can be blocked (private mode, iframes),
 * so every read and write is guarded and falls back to the defaults.
 */
const KEY = 'drakepg.settings';

export interface Settings {
  /** Show the full story dialogue for quests. Off: one short line saying what to do. */
  storyText: boolean;
}

const DEFAULTS: Settings = { storyText: false };

function load(): Settings {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<Settings>;
    return { storyText: typeof raw.storyText === 'boolean' ? raw.storyText : DEFAULTS.storyText };
  } catch {
    return { ...DEFAULTS };
  }
}

export const settings: Settings = load();

export function saveSettings(): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(settings));
  } catch {
    // Not remembered next time; the game still works.
  }
}
