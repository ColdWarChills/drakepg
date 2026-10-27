/**
 * The codex (codex.html): the designer's page for the game's data, the docs and their own notes. Dev server only
 * (`npm run dev`, then /codex.html); it reads src/data directly, so it reloads by itself when the data changes.
 */
import './codex.css';
import { mountMonsters } from './monsters';
import { monsterNotes, notesPad } from './notes';
import { mountNotes } from './pad';
import { mountPlans } from './plans';

interface View {
  focus?(arg: string): void;
}

const TABS: { id: string; label: string; mount: (root: HTMLElement) => View | void }[] = [
  { id: 'monsters', label: 'Monsters', mount: mountMonsters },
  { id: 'plans', label: 'Plans', mount: mountPlans },
  { id: 'notes', label: 'Notes', mount: mountNotes },
];

const app = document.getElementById('app')!;
app.innerHTML = `
  <header class="top">
    <h1>drakepg <span>codex</span></h1>
    <nav class="tabs">${TABS.map((t) => `<a href="#${t.id}" data-tab="${t.id}">${t.label}</a>`).join('')}</nav>
    <span class="live" title="Reads src/data and docs/ directly: it reloads when they change">● live</span>
  </header>
  ${TABS.map((t) => `<main class="view view-${t.id}" data-view="${t.id}" hidden></main>`).join('')}`;

const views = new Map<string, View>();

/** `#monsters`, `#plans`, `#notes`; `#monsters/<id>` opens that monster. */
function route(): void {
  const [tabId, arg] = location.hash.slice(1).split('/');
  const tab = TABS.find((t) => t.id === tabId) ?? TABS[0];
  for (const t of TABS) {
    app.querySelector<HTMLElement>(`[data-view="${t.id}"]`)!.hidden = t !== tab;
    app.querySelector(`[data-tab="${t.id}"]`)!.classList.toggle('on', t === tab);
  }
  if (!views.has(tab.id)) views.set(tab.id, tab.mount(app.querySelector<HTMLElement>(`[data-view="${tab.id}"]`)!) ?? {});
  if (arg) views.get(tab.id)!.focus?.(decodeURIComponent(arg));
}

window.addEventListener('hashchange', route);
route();

const files = [notesPad, monsterNotes.file];
for (const file of files) void file.load();
// A chat may have written the notes meanwhile.
window.addEventListener('focus', () => files.forEach((f) => void f.load()));
const flush = () => files.forEach((f) => f.flush());
window.addEventListener('pagehide', flush);
import.meta.hot?.on('vite:beforeFullReload', flush);
