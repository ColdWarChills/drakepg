import { MONSTERS } from '../data/monsters';
import { clock } from './dom';

export type SaveStatus = 'loading' | 'saved' | 'unsaved' | 'saving' | 'conflict' | 'offline';

/**
 * A notes file in docs/, read and written through the dev server (`codexNotes` in vite.config.ts). Edits save
 * half a second after the last key. A save carries the file's time when it was read: if a chat changed the file
 * in between, the server refuses (409) and `merge` decides; without one the page asks.
 */
export class NoteFile {
  text = '';
  status: SaveStatus = 'loading';
  savedAt?: Date;
  /** The file's text on disk after a refused save. */
  disk = '';
  private mtime = 0;
  private diskMtime = 0;
  private dirty = false;
  private saving = false;
  private again = false;
  private timer = 0;
  private readonly listeners = new Set<(textChanged: boolean) => void>();

  constructor(
    private readonly name: string,
    /** Combines a changed file with the edits here; null leaves the conflict to the page. */
    private readonly merge?: (disk: string) => string,
  ) {}

  private get url(): string {
    return `/__codex/${this.name}`;
  }

  subscribe(fn: (textChanged: boolean) => void): void {
    this.listeners.add(fn);
  }

  private emit(textChanged: boolean): void {
    for (const fn of this.listeners) fn(textChanged);
  }

  /** Reads the file; quietly does nothing while edits wait to be saved. */
  async load(): Promise<void> {
    if (this.dirty || this.saving || this.status === 'conflict') return;
    try {
      const res = await fetch(this.url);
      if (!res.ok) throw new Error(String(res.status));
      const { text, mtime } = (await res.json()) as { text: string; mtime: number };
      if (this.dirty || this.saving) return;
      const changed = mtime !== this.mtime || this.status === 'loading' || this.status === 'offline';
      this.text = text;
      this.mtime = mtime;
      this.status = 'saved';
      if (changed) this.emit(true);
    } catch {
      this.status = 'offline';
      this.emit(false);
    }
  }

  edit(text: string): void {
    this.text = text;
    this.dirty = true;
    if (this.status !== 'conflict') this.status = 'unsaved';
    window.clearTimeout(this.timer);
    this.timer = window.setTimeout(() => void this.save(), 500);
    this.emit(false);
  }

  async save(force = false): Promise<void> {
    window.clearTimeout(this.timer);
    if (this.saving) {
      this.again = true;
      return;
    }
    if (!this.dirty || (this.status === 'conflict' && !force)) return;
    const text = this.text;
    this.saving = true;
    this.status = 'saving';
    this.emit(false);
    try {
      const res = await fetch(this.url, { method: 'PUT', body: JSON.stringify({ text, base: force ? undefined : this.mtime }) });
      const body = (await res.json()) as { text: string; mtime: number };
      if (res.status === 409) {
        this.disk = body.text;
        this.diskMtime = body.mtime;
        this.status = 'conflict';
      } else if (!res.ok) {
        throw new Error(String(res.status));
      } else {
        this.mtime = body.mtime;
        this.dirty = this.text !== text;
        this.status = this.dirty ? 'unsaved' : 'saved';
        this.savedAt = new Date();
      }
    } catch {
      this.status = 'offline';
    }
    this.saving = false;
    if (this.status === 'conflict' && this.merge) {
      this.text = this.merge(this.disk);
      this.mtime = this.diskMtime;
      this.status = 'unsaved';
      this.emit(true);
      return this.save();
    }
    this.emit(false);
    if (this.again || this.dirty) {
      this.again = false;
      if (this.status === 'unsaved') return this.save();
    }
  }

  /** Conflict: take the file as it is now, dropping the edits here. */
  takeDisk(): void {
    this.text = this.disk;
    this.mtime = this.diskMtime;
    this.dirty = false;
    this.status = 'saved';
    this.emit(true);
  }

  /** Conflict: write the edits here over the file. */
  keepMine(): void {
    this.status = 'unsaved';
    void this.save(true);
  }

  /** Sends edits still waiting when the page goes away (closing it, or the reload after a game data change). */
  flush(): void {
    if (this.dirty && this.status !== 'conflict') navigator.sendBeacon(this.url, JSON.stringify({ text: this.text, base: this.mtime }));
  }
}

export function statusText(file: NoteFile): string {
  switch (file.status) {
    case 'loading':
      return 'Loading…';
    case 'unsaved':
    case 'saving':
      return 'Saving…';
    case 'saved':
      return file.savedAt ? `Saved ${clock(file.savedAt)}` : '';
    case 'conflict':
      return 'Changed outside the page';
    case 'offline':
      return 'Not saved: is the dev server running?';
  }
}

/** The free notes pad: docs/NOTES.md. */
export const notesPad = new NoteFile('notes');

/**
 * One line per monster in docs/MONSTER_NOTES.md, written next to each monster in the table:
 * "- `mossling` Mossling: hits too hard for level 1".
 */
class MonsterNotes {
  readonly file: NoteFile;
  private notes = new Map<string, string>();
  /** Notes changed here since the last read, kept when a chat changed the file meanwhile. */
  private touched = new Map<string, string>();

  constructor() {
    this.file = new NoteFile('monsters', (disk) => {
      this.notes = parse(disk);
      for (const [id, note] of this.touched) this.put(id, note);
      return this.write();
    });
    this.file.subscribe((textChanged) => {
      if (textChanged) this.notes = parse(this.file.text);
      if (this.file.status === 'saved') this.touched.clear();
    });
  }

  get(id: string): string {
    return this.notes.get(id) ?? '';
  }

  entries(): [string, string][] {
    return [...this.notes];
  }

  set(id: string, note: string): void {
    const text = note.replace(/\s*\n\s*/g, ' ').trim();
    this.touched.set(id, text);
    this.put(id, text);
    this.file.edit(this.write());
  }

  private put(id: string, note: string): void {
    if (note) this.notes.set(id, note);
    else this.notes.delete(id);
  }

  private write(): string {
    const order = MONSTERS.map((m) => m.id);
    const ids = [...this.notes.keys()].sort((a, b) => rank(order, a) - rank(order, b));
    const lines = ids.map((id) => `- \`${id}\` ${MONSTERS.find((m) => m.id === id)?.name ?? '(removed)'}: ${this.notes.get(id)}`);
    return `# Monster notes\n\nWritten next to each monster on the codex page (codex.html), one line per monster.\n\n${lines.join('\n')}\n`;
  }
}

function rank(order: string[], id: string): number {
  const i = order.indexOf(id);
  return i < 0 ? order.length : i;
}

function parse(text: string): Map<string, string> {
  const notes = new Map<string, string>();
  for (const line of text.split(/\r?\n/)) {
    const m = /^- `([^`]+)`[^:]*: (.*)$/.exec(line);
    if (m && m[2].trim()) notes.set(m[1], m[2].trim());
  }
  return notes;
}

export const monsterNotes = new MonsterNotes();
