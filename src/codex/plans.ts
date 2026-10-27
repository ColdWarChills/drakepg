import { esc, recall, remember } from './dom';
import { renderMarkdown } from './markdown';

/** Every doc in docs/ except the notes files (those are the Notes tab). Loaded on demand, reloaded when they change. */
const DOCS = import.meta.glob<string>(['../../docs/*.md', '!../../docs/NOTES.md', '!../../docs/MONSTER_NOTES.md'], { query: '?raw', import: 'default' });
const docName = (path: string) => path.split('/').pop()!;
const DOC_PATHS = Object.keys(DOCS).sort((a, b) => Number(docName(b) === 'PLANS.md') - Number(docName(a) === 'PLANS.md') || a.localeCompare(b));

/** The bold labels docs/PLANS.md starts its points with. The first five can filter the doc. */
const TAGS = [
  { id: 'open', label: 'Open', match: /^Open\b/ },
  { id: 'proposed', label: 'Proposed', match: /^Proposed\b/ },
  { id: 'decided', label: 'Decided', match: /^Decided\b/ },
  { id: 'playtest', label: 'Playtest', match: /^Playtest\b/ },
  { id: 'todo', label: 'To do', match: /^To do\b/ },
  { id: 'built', label: 'Built', match: /^(Built|Fixed|Reworked)\b/ },
  { id: 'asked', label: 'Asked', match: /^(Asked|Bug|Now)\b/ },
];
const FILTERS = TAGS.slice(0, 5);

export function mountPlans(root: HTMLElement): void {
  const state = recall('plans', { doc: 'PLANS.md', tag: null as string | null, q: '' });
  root.innerHTML = `
    <aside class="side">
      <select class="doc-pick">${DOC_PATHS.map((p) => `<option value="${p}">${esc(docName(p))}</option>`).join('')}</select>
      <input class="search" type="search" placeholder="Search this doc" value="${esc(state.q)}">
      <div class="chips stack"></div>
      <span class="count"></span>
      <nav class="toc"></nav>
    </aside>
    <article class="doc md paper"></article>`;

  const pick = root.querySelector<HTMLSelectElement>('.doc-pick')!;
  const search = root.querySelector<HTMLInputElement>('.search')!;
  const chips = root.querySelector<HTMLElement>('.chips')!;
  const count = root.querySelector<HTMLElement>('.count')!;
  const toc = root.querySelector<HTMLElement>('.toc')!;
  const doc = root.querySelector<HTMLElement>('.doc')!;
  /** An item's own text, without its sub-items, for search. */
  const ownText = new Map<HTMLLIElement, string>();

  async function show(): Promise<void> {
    const path = DOC_PATHS.find((p) => docName(p) === state.doc) ?? DOC_PATHS[0];
    state.doc = docName(path);
    pick.value = path;
    doc.innerHTML = renderMarkdown(await DOCS[path]());
    groupSections();
    tagItems();
    toc.innerHTML = [...doc.querySelectorAll('h2')].map((h) => `<a href="#plans" data-to="${h.id}">${esc(h.textContent ?? '')}</a>`).join('');
    const counts = FILTERS.map((t) => ({ ...t, n: doc.querySelectorAll(`li[data-tags~="${t.id}"]`).length })).filter((t) => t.n);
    chips.innerHTML = counts.map((t) => `<button class="chip tag-${t.id}" data-tag="${t.id}">${t.label} <small>${t.n}</small></button>`).join('');
    filter();
  }

  /** Wraps each ## heading and what follows it in a <section>, so a filter can hide a whole empty section. */
  function groupSections(): void {
    let section = document.createElement('section');
    const sections = [section];
    for (const node of [...doc.childNodes]) {
      if (node instanceof HTMLHeadingElement && node.tagName === 'H2') sections.push((section = document.createElement('section')));
      section.appendChild(node);
    }
    doc.replaceChildren(...sections.filter((s) => s.childNodes.length));
  }

  function tagItems(): void {
    ownText.clear();
    for (const li of doc.querySelectorAll('li')) {
      const tags: string[] = [];
      for (const strong of li.querySelectorAll<HTMLElement>(':scope > strong')) {
        const tag = TAGS.find((t) => t.match.test(strong.textContent?.trim() ?? ''));
        if (!tag) continue;
        strong.classList.add('tag', `tag-${tag.id}`);
        tags.push(tag.id);
      }
      if (tags.length) li.dataset.tags = tags.join(' ');
      const clone = li.cloneNode(true) as HTMLLIElement;
      clone.querySelectorAll('ul, ol').forEach((list) => list.remove());
      ownText.set(li, (clone.textContent ?? '').toLowerCase());
    }
  }

  /** Shows the items that match (with their sub-items, and their parents for context), hides the rest. */
  function filter(): void {
    remember('plans', state);
    for (const b of chips.querySelectorAll<HTMLElement>('.chip')) b.classList.toggle('on', b.dataset.tag === state.tag);
    const items = [...doc.querySelectorAll('li')];
    const words = state.q.toLowerCase().split(/\s+/).filter(Boolean);
    const active = state.tag !== null || words.length > 0;
    doc.classList.toggle('filtered', active);
    for (const li of items) li.classList.remove('hide', 'hit');
    for (const s of doc.querySelectorAll('section')) s.classList.remove('hide');
    for (const a of toc.querySelectorAll('a')) a.classList.remove('hide');
    if (!active) {
      count.textContent = '';
      return;
    }
    const matches = (li: HTMLLIElement) =>
      (state.tag === null || (li.dataset.tags ?? '').split(' ').includes(state.tag)) && words.every((w) => ownText.get(li)?.includes(w));
    for (const li of items) li.classList.add('hide');
    let n = 0;
    for (const li of items) {
      if (!matches(li)) continue;
      n++;
      li.classList.remove('hide');
      li.classList.add('hit');
      li.querySelectorAll('li').forEach((sub) => sub.classList.remove('hide'));
      for (let up = li.parentElement?.closest('li'); up; up = up.parentElement?.closest('li')) up.classList.remove('hide');
    }
    for (const s of doc.querySelectorAll('section')) s.classList.toggle('hide', !s.querySelector('li:not(.hide)'));
    for (const a of toc.querySelectorAll<HTMLElement>('a')) a.classList.toggle('hide', !!document.getElementById(a.dataset.to!)?.closest('section.hide'));
    count.textContent = `${n} found`;
  }

  pick.addEventListener('change', () => {
    state.doc = docName(pick.value);
    state.tag = null;
    void show();
  });
  search.addEventListener('input', () => {
    state.q = search.value.trim();
    filter();
  });
  chips.addEventListener('click', (e) => {
    const b = (e.target as HTMLElement).closest<HTMLElement>('.chip');
    if (!b) return;
    state.tag = state.tag === b.dataset.tag ? null : b.dataset.tag!;
    filter();
  });
  toc.addEventListener('click', (e) => {
    const a = (e.target as HTMLElement).closest<HTMLElement>('a[data-to]');
    if (!a) return;
    e.preventDefault();
    document.getElementById(a.dataset.to!)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  void show();
}
