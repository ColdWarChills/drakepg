import { MONSTERS } from '../data/monsters';
import { esc, recall, remember } from './dom';
import { renderMarkdown } from './markdown';
import { monsterNotes, notesPad, statusText } from './notes';

/** The Notes tab: a free pad (docs/NOTES.md) and every monster note in one list. */
export function mountNotes(root: HTMLElement): void {
  let reading = recall('pad.reading', false);
  root.innerHTML = `
    <section class="pad paper">
      <div class="pad-head">
        <h2>Notes</h2>
        <span class="status"></span>
        <button class="btn mode"></button>
      </div>
      <div class="conflict" hidden>
        A chat changed docs/NOTES.md while you were writing.
        <button class="btn" data-act="disk">Use the file</button>
        <button class="btn" data-act="mine">Keep mine</button>
      </div>
      <textarea class="pad-text" spellcheck="true" placeholder="Anything: ideas, bugs, what felt off in the last play, what to build next…"></textarea>
      <div class="pad-read md" hidden></div>
      <p class="hint">Saved to <code>docs/NOTES.md</code> as you type. Claude chats read it when they start. Markdown works: <code># heading</code>, <code>- list</code>, <code>**bold**</code>.</p>
    </section>
    <aside class="mnotes paper">
      <h3>Monster notes</h3>
      <ul></ul>
      <p class="hint">Written in each monster's row. Saved to <code>docs/MONSTER_NOTES.md</code>.</p>
    </aside>`;

  const text = root.querySelector<HTMLTextAreaElement>('.pad-text')!;
  const read = root.querySelector<HTMLElement>('.pad-read')!;
  const status = root.querySelector<HTMLElement>('.status')!;
  const mode = root.querySelector<HTMLButtonElement>('.mode')!;
  const conflict = root.querySelector<HTMLElement>('.conflict')!;
  const list = root.querySelector<HTMLElement>('.mnotes ul')!;

  function showMode(): void {
    remember('pad.reading', reading);
    text.hidden = reading;
    read.hidden = !reading;
    mode.textContent = reading ? 'Write' : 'Read';
    if (reading) read.innerHTML = renderMarkdown(notesPad.text) || '<p class="dim">Nothing written yet.</p>';
  }

  function showStatus(textChanged: boolean): void {
    if (textChanged && document.activeElement !== text) text.value = notesPad.text;
    if (textChanged && reading) showMode();
    status.textContent = statusText(notesPad);
    status.dataset.status = notesPad.status;
    conflict.hidden = notesPad.status !== 'conflict';
  }

  function showMonsterNotes(): void {
    const names = new Map(MONSTERS.map((m) => [m.id, m.name]));
    const entries = monsterNotes.entries();
    list.innerHTML = entries.length
      ? entries.map(([id, note]) => `<li><a href="#monsters/${id}">${esc(names.get(id) ?? id)}</a> ${esc(note)}</li>`).join('')
      : '<li class="dim">None yet.</li>';
  }

  text.value = notesPad.text;
  text.addEventListener('input', () => notesPad.edit(text.value));
  mode.addEventListener('click', () => {
    reading = !reading;
    showMode();
  });
  conflict.addEventListener('click', (e) => {
    const act = (e.target as HTMLElement).closest<HTMLElement>('[data-act]')?.dataset.act;
    if (act === 'disk') notesPad.takeDisk();
    if (act === 'mine') notesPad.keepMine();
  });
  notesPad.subscribe(showStatus);
  monsterNotes.file.subscribe(showMonsterNotes);

  showMode();
  showStatus(true);
  showMonsterNotes();
}
