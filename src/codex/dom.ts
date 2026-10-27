const ESCAPES: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

export function esc(text: string): string {
  return text.replace(/[&<>"']/g, (c) => ESCAPES[c]);
}

/**
 * View state (tab, filters, open rows) kept for the browser tab, so the reload that follows a game data change
 * lands where you were. Storage can be missing (private windows); the page then just starts fresh.
 */
export function remember(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(`codex.${key}`, JSON.stringify(value));
  } catch {
    // Not kept: fine.
  }
}

export function recall<T>(key: string, fallback: T): T {
  try {
    const raw = sessionStorage.getItem(`codex.${key}`);
    return raw === null ? fallback : (JSON.parse(raw) as T);
  } catch {
    return fallback;
  }
}

/** Short time for "Saved 14:02". */
export function clock(date: Date): string {
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}
