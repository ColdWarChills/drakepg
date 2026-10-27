import { BALANCE } from '../config';
import { CLASSES } from '../data/classes';
import { db } from '../data/db';
import { DAMAGE_TYPE_LABELS, ELEMENT_LABELS, ELEMENTS } from '../data/elements';
import { MAP_AREAS } from '../data/map';
import { MONSTERS } from '../data/monsters';
import { FLOORS } from '../data/tower';
import { ROOMS, ZONES } from '../data/world';
import type { Element, LootTier, MonsterAttack, MonsterDef, Resists } from '../data/types';
import { LOOT_TIERS, tierOdds } from '../game/Loot';
import { statFor } from '../game/Player';
import { artUrl, iconUrl } from './assets';
import { esc, recall, remember } from './dom';
import { monsterNotes, statusText } from './notes';

type Kind = 'normal' | 'tough' | 'boss' | 'special';
type SortKey = 'name' | 'level' | 'where' | 'hp' | 'hit' | 'def' | 'element' | 'xp' | 'gold';

interface Place {
  area: string;
  name: string;
  detail: string;
}

interface Row {
  m: MonsterDef;
  order: number;
  kind: Kind;
  places: Place[];
  /** Index of its first area in AREAS (story order). */
  areaRank: number;
  /** Average hit of its usual move, and its biggest move. */
  hit: number;
  maxHit: { value: number; name: string };
  search: string;
}

const SPIRE = 'spire';
const ANYWHERE = 'anywhere';
const AREAS = [...MAP_AREAS.map((a) => ({ id: a.id, name: a.name })), { id: SPIRE, name: 'The Spire' }, { id: ANYWHERE, name: 'Anywhere' }];
const AREA_COLORS = ['#a08a64', '#5f8a3a', '#3e8088', '#5a82b8', '#8a5aa6', '#c0902c'];
const areaColor = (id: string) => AREA_COLORS[Math.max(0, AREAS.findIndex((a) => a.id === id)) % AREA_COLORS.length];
const areaName = (id: string) => AREAS.find((a) => a.id === id)?.name ?? id;

const KINDS: { id: Kind; label: string }[] = [
  { id: 'normal', label: 'Normal' },
  { id: 'tough', label: 'Tough' },
  { id: 'boss', label: 'Boss' },
  { id: 'special', label: 'Special' },
];

const COLUMNS: { key: SortKey | null; label: string; title?: string; num?: boolean }[] = [
  { key: null, label: '' },
  { key: 'name', label: 'Monster' },
  { key: 'level', label: 'Lv', num: true },
  { key: 'where', label: 'Where' },
  { key: 'hp', label: 'HP', num: true },
  { key: 'hit', label: 'Hit', num: true, title: 'Average hit of its usual move, before your defense and resists. Small: its biggest move.' },
  { key: 'def', label: 'Def', num: true, title: 'Defense vs Melee / Pierce / Magic (sorts by the total)' },
  { key: 'element', label: 'Element' },
  { key: null, label: 'Weak to', title: 'Extra damage taken, in %' },
  { key: null, label: 'Resists', title: 'Damage resisted, in % (over 100 heals it)' },
  { key: 'xp', label: 'XP', num: true },
  { key: 'gold', label: 'Gold', num: true },
];

const damageMult = (m: MonsterDef) => (m.boss ? BALANCE.bossDamage : BALANCE.mobDamage);

/** Average hit before the hero's defense, resists and crits (Battle.strike); a landed hit does at least 1. */
function hitOf(m: MonsterDef, a: MonsterAttack): number {
  if (!a.power) return 0;
  const mult = damageMult(m);
  const roll = (Math.round(m.damage[0] * mult) + Math.round(m.damage[1] * mult)) / 2;
  return Math.max(1, (roll + Math.floor(statFor(m.stats, a.damageType) * BALANCE.statDamageScale)) * a.power);
}

const isSignature = (a: MonsterAttack) => !!(a.opener || a.every || a.hpBelow);

function placesOf(m: MonsterDef): Place[] {
  const places: Place[] = [];
  for (const zone of ZONES) {
    const enc = zone.encounters.find((e) => e.monsterId === m.id);
    if (!enc) continue;
    const total = zone.encounters.reduce((sum, e) => sum + e.weight, 0);
    const share = `${pct(enc.weight / total)} of spawns`;
    for (const room of ROOMS) if (room.zoneId === zone.id && room.monsterCount) places.push({ area: room.map.area, name: room.name, detail: share });
  }
  for (const room of ROOMS) {
    const boss = room.boss;
    if (!boss) continue;
    const classes = CLASSES.filter((c) => boss.byClass?.[c.id] === m.id).map((c) => c.name);
    const isDefault = boss.monsterId === m.id && !Object.values(boss.byClass ?? {}).includes(m.id);
    if (!classes.length && !isDefault) continue;
    const role = boss.questId ? `Boss of "${db.quest(boss.questId).name}"` : 'Gate guard';
    places.push({ area: room.map.area, name: room.name, detail: classes.length ? `${role} (${classes.join(', ')})` : role });
  }
  for (const floor of FLOORS) {
    if (floor.monsterId !== m.id) continue;
    const twists = floor.twists.map((t) => db.twist(t).name).join(', ');
    places.push({ area: SPIRE, name: `Spire floor ${floor.floor}`, detail: `L${floor.level}${floor.heroic ? ', Heroic' : ''}${twists ? ` · ${twists}` : ''}` });
  }
  if (m.chestLoot) places.push({ area: ANYWHERE, name: 'Treasure chests', detail: `${pct(BALANCE.chest.mimicChance)} of chests, sized to the area` });
  if (m.lucky) places.push({ area: ANYWHERE, name: 'Any monster room', detail: 'Rare spawn, sized to the area; runs away' });
  return places;
}

function buildRows(): Row[] {
  return MONSTERS.map((m, order) => {
    const places = placesOf(m);
    const firstArea = places.find((p) => p.area !== SPIRE)?.area ?? places[0]?.area ?? ANYWHERE;
    const usual = m.attacks.filter((a) => !isSignature(a)).sort((a, b) => b.weight - a.weight)[0] ?? m.attacks[0];
    const all = [...m.attacks, ...(m.phase?.attacks ?? [])];
    const biggest = all.reduce((best, a) => (hitOf(m, a) > hitOf(m, best) ? a : best), all[0]);
    const words = [m.name, m.id, m.trait ?? '', ...all.map((a) => a.name), ...places.map((p) => `${p.name} ${areaName(p.area)}`)];
    return {
      m,
      order,
      kind: m.boss ? 'boss' : m.lucky || m.chestLoot ? 'special' : m.tough ? 'tough' : 'normal',
      places,
      areaRank: AREAS.findIndex((a) => a.id === firstArea),
      hit: usual ? hitOf(m, usual) : 0,
      maxHit: { value: biggest ? hitOf(m, biggest) : 0, name: biggest?.name ?? '' },
      search: words.join(' ').toLowerCase(),
    };
  });
}

function sortValue(r: Row, key: SortKey): number | string {
  const m = r.m;
  switch (key) {
    case 'name':
      return m.name;
    case 'level':
      return m.level;
    case 'where':
      return r.areaRank * 1000 + m.level;
    case 'hp':
      return m.hp;
    case 'hit':
      return r.hit;
    case 'def':
      return (m.defense.melee ?? 0) + (m.defense.pierce ?? 0) + (m.defense.magic ?? 0);
    case 'element':
      return m.element ? ELEMENTS.indexOf(m.element) : ELEMENTS.length;
    case 'xp':
      return m.xp;
    case 'gold':
      return m.gold[0] + m.gold[1];
  }
}

// --- Small pieces ---

function pct(share: number): string {
  const p = share * 100;
  return `${p >= 10 ? Math.round(p) : Number(p.toFixed(1))}%`;
}

function icon(name: string, title: string): string {
  const url = iconUrl(name);
  return url ? `<img class="ico" src="${url}" alt="${esc(title)}" title="${esc(title)}">` : `<span class="ico-text">${esc(title)}</span>`;
}

function elementChip(element: Element | undefined): string {
  if (!element) return '<span class="dim" title="Neutral">—</span>';
  return `<span class="el" title="${ELEMENT_LABELS[element]}">${icon(`el_${element}`, ELEMENT_LABELS[element])}<span>${ELEMENT_LABELS[element]}</span></span>`;
}

function weakChips(resists: Resists | undefined, weakTo?: MonsterDef['weakTo']): string {
  const chips = Object.entries(resists ?? {})
    .filter(([, v]) => v < 0)
    .sort((a, b) => a[1] - b[1])
    .map(([el, v]) => affChip(el, -v, 'weak'));
  if (weakTo?.blunt) chips.push(`<span class="aff weak" title="Blunt +${weakTo.blunt}%">${icon('spot_blunt', 'Blunt')}${weakTo.blunt}</span>`);
  if (weakTo?.poison) chips.push(`<span class="aff weak" title="Poison ticks +${weakTo.poison}%">${icon('spot_poison', 'Poison')}${weakTo.poison}</span>`);
  return chips.join('') || '<span class="dim">—</span>';
}

function resistChips(resists: Resists | undefined): string {
  const chips = Object.entries(resists ?? {})
    .filter(([, v]) => v > 0)
    .sort((a, b) => b[1] - a[1])
    .map(([el, v]) => affChip(el, v, v > 100 ? 'absorb' : 'resist'));
  return chips.join('') || '<span class="dim">—</span>';
}

function affChip(el: string, value: number, cls: string): string {
  const label = el === 'all' ? 'All' : ELEMENT_LABELS[el as Element];
  const what = cls === 'weak' ? `+${value}% damage` : value > 100 ? `absorbs (${value})` : value === 100 ? 'immune' : `${value}% resisted`;
  const face = el === 'all' ? '<span class="ico-text">All</span>' : icon(`el_${el}`, label);
  return `<span class="aff ${cls}" title="${label}: ${what}">${face}${value}</span>`;
}

function badges(r: Row): string {
  const m = r.m;
  const out: string[] = [];
  if (m.boss) out.push('<span class="badge boss">Boss</span>');
  if (m.tough) out.push('<span class="badge tough">Tough</span>');
  if (m.chestLoot) out.push('<span class="badge special">Mimic</span>');
  if (m.lucky) out.push('<span class="badge special">Lucky</span>');
  if (m.phase) out.push('<span class="badge plain" title="Changes at low HP">2 forms</span>');
  if (m.shell) out.push(`<span class="badge plain" title="${esc(m.shell.name)}">Shell</span>`);
  return out.length ? `<span class="badges">${out.join('')}</span>` : '';
}

function whereCell(r: Row): string {
  const story = r.places.filter((p) => p.area !== SPIRE);
  const floors = r.places.filter((p) => p.area === SPIRE);
  const first = story[0];
  const title = r.places.map((p) => `${p.name} (${areaName(p.area)})`).join('\n');
  const main = first
    ? `<span class="dot" style="background:${areaColor(first.area)}"></span>${esc(first.name)}${story.length > 1 ? `<small> +${story.length - 1}</small>` : ''}`
    : '';
  const spire = floors.length ? `<span class="spire-chip">${icon('spire', 'Spire')}${floors.map((f) => f.name.replace(/\D+/, '')).join(', ')}</span>` : '';
  return `<span title="${esc(title)}">${main || (spire ? '' : '<span class="dim">—</span>')}${spire}</span>`;
}

function noteLine(id: string): string {
  const note = monsterNotes.get(id);
  return note ? `<span class="note-line" title="${esc(note)}">✎ ${esc(note)}</span>` : '';
}

function rowHtml(r: Row): string {
  const m = r.m;
  const art = artUrl(m.look.art);
  const d = m.defense;
  return `<tr class="mon ${r.kind}" data-id="${m.id}">
    <td class="art">${art ? `<img src="${art}" loading="lazy" alt="">` : ''}</td>
    <td class="name"><b>${esc(m.name)}</b>${badges(r)}${noteLine(m.id)}</td>
    <td class="num">${m.level}</td>
    <td class="where">${whereCell(r)}</td>
    <td class="num">${m.hp}</td>
    <td class="num" title="Biggest: ${esc(r.maxHit.name)}">${Math.round(r.hit)}${r.maxHit.value > r.hit + 0.5 ? `<small>max ${Math.round(r.maxHit.value)}</small>` : ''}</td>
    <td class="num def">${d.melee ?? 0}<i>/</i>${d.pierce ?? 0}<i>/</i>${d.magic ?? 0}</td>
    <td>${elementChip(m.element)}</td>
    <td class="affs">${weakChips(m.resists, m.weakTo)}</td>
    <td class="affs">${resistChips(m.resists)}</td>
    <td class="num">${m.xp}</td>
    <td class="num">${m.gold[0]}–${m.gold[1]}</td>
  </tr>`;
}

// --- Details ---

function attacksTable(m: MonsterDef, attacks: MonsterAttack[], element: Element | undefined): string {
  const pool = attacks.filter((a) => !isSignature(a)).reduce((sum, a) => sum + a.weight, 0);
  const rows = attacks.map((a) => {
    const el = a.element ?? element;
    const when = a.opener
      ? 'Opener'
      : a.every
        ? `Every ${a.every}${a.every === 2 ? 'nd' : a.every === 3 ? 'rd' : 'th'} move`
        : a.hpBelow
          ? `Once under ${pct(a.hpBelow)} HP`
          : pct(a.weight / pool);
    const effects: string[] = [];
    if (a.windup) effects.push(`<span class="pill" title="${esc(a.windup)}">Wind-up</span>`);
    if (a.heal) effects.push(`<span class="pill good">Heals ${pct(a.heal)}</span>`);
    for (const ap of a.applies ?? []) {
      const s = db.status(ap.status);
      const odds = ap.chance !== undefined && ap.chance < 1 ? ` ${pct(ap.chance)}` : '';
      effects.push(`<span class="pill${ap.self ? ' good' : ''}" title="${esc(s.description)}">${icon(`status_${s.id}`, s.name)}${esc(s.name)} ${ap.turns}t${odds}${ap.self ? ' (self)' : ''}</span>`);
    }
    return `<tr>
      <td><b>${esc(a.name)}</b></td>
      <td>${DAMAGE_TYPE_LABELS[a.damageType]}${el ? ` ${icon(`el_${el}`, ELEMENT_LABELS[el])}` : ''}</td>
      <td class="num">×${a.power}</td>
      <td class="num">${a.power ? Math.round(hitOf(m, a)) : '—'}</td>
      <td>${when}</td>
      <td>${effects.join(' ')}</td>
    </tr>`;
  });
  return `<table class="moves"><thead><tr><th>Move</th><th>Type</th><th class="num">Power</th><th class="num">Hit</th><th>When</th><th>Effects</th></tr></thead><tbody>${rows.join('')}</tbody></table>`;
}

function lootHtml(m: MonsterDef): string {
  if (m.chestLoot) return '<p class="dim">Gear from the area\'s chest table, at chest odds.</p>';
  if (!m.loot) return '<p class="dim">No gear.</p>';
  const table = db.lootTable(m.loot);
  if (m.lucky) return `<p>A ${pct(BALANCE.lucky.sigilChance)} shot at a Sigil piece of <code>${m.loot}</code>, no normal gear.</p>`;
  const odds = tierOdds(m.loot, m.boss ? 'boss' : 'mob');
  const tiers = LOOT_TIERS.filter((t: LootTier) => table.tiers[t].length).map((t: LootTier) => {
    const items = table.tiers[t].map((e) => `${esc(db.item(e.itemId).name)}${e.weight && e.weight !== 1 ? ` <small>×${e.weight}</small>` : ''}`).join(', ');
    return `<li><span class="tier ${t}">${t[0].toUpperCase() + t.slice(1)} ${pct(odds[t])}</span> ${items}</li>`;
  });
  return `<p class="dim">Table <code>${m.loot}</code>, per kill:</p><ul class="loot">${tiers.join('')}</ul>`;
}

function detailsHtml(r: Row): string {
  const m = r.m;
  const art = artUrl(m.look.art);
  const mult = damageMult(m);
  const s = m.stats;
  const drops = m.drops.map((d) => `<li>${esc(db.item(d.itemId).name)}${d.qty && d.qty > 1 ? ` ×${d.qty}` : ''} <small>${pct(d.chance)}</small></li>`).join('');
  const where = r.places.map((p) => `<li><span class="dot" style="background:${areaColor(p.area)}"></span><b>${esc(p.name)}</b> <small>${esc(areaName(p.area))} · ${esc(p.detail)}</small></li>`).join('');
  const phase = m.phase
    ? `<h4>At ${pct(m.phase.at)} HP</h4>
       <p>${esc(m.phase.text)}</p>
       <p class="aff-row">Becomes ${elementChip(m.phase.element)} · Weak ${weakChips(m.phase.resists)} · Resists ${resistChips(m.phase.resists)}</p>
       ${m.phase.attacks ? attacksTable(m, m.phase.attacks, m.phase.element) : ''}`
    : '';
  const shell = m.shell
    ? `<h4>${esc(m.shell.name)}</h4><p>Takes ${pct(m.shell.reduction)} less damage until ${m.shell.hits} ${elementChip(m.shell.element)} hits break it${m.shell.reformAt ? `; grows back once at ${pct(m.shell.reformAt)} HP` : ''}.</p>`
    : '';
  const heroic = m.heroic ? `<h4>Heroic rematch</h4><p>${esc(m.heroic.text)}</p>${attacksTable(m, m.heroic.attacks, m.element)}` : '';
  const immune = m.immune?.length ? `<dt>Immune</dt><dd>${m.immune.map((id) => esc(db.status(id).name)).join(', ')}</dd>` : '';
  return `<div class="det">
    <div class="det-art">${art ? `<img src="${art}" alt="">` : '<div class="no-art">No art</div>'}</div>
    <div class="det-main">
      ${m.trait ? `<p class="trait">${esc(m.trait)}</p>` : ''}
      <h4>Moves</h4>
      ${attacksTable(m, m.attacks, m.element)}
      ${phase}${shell}${heroic}
    </div>
    <div class="det-side">
      <h4>Stats</h4>
      <dl>
        <dt>STR · INT · DEX · END</dt><dd>${s.str} · ${s.int} · ${s.dex} · ${s.end}</dd>
        <dt>Damage roll</dt><dd>${m.damage[0]}–${m.damage[1]} ×${mult} = ${Math.round(m.damage[0] * mult)}–${Math.round(m.damage[1] * mult)}</dd>
        ${immune}
      </dl>
      <h4>Where</h4><ul class="where-list">${where || '<li class="dim">Nowhere yet</li>'}</ul>
      <h4>Drops</h4>${drops ? `<ul class="drops">${drops}</ul>` : '<p class="dim">None.</p>'}
      <h4>Gear</h4>${lootHtml(m)}
      <h4>Note <span class="note-status"></span></h4>
      <textarea class="note" data-id="${m.id}" rows="2" placeholder="Your note on ${esc(m.name)}…">${esc(monsterNotes.get(m.id))}</textarea>
      <p class="ids"><code>${m.id}</code>${typeof m.look.art === 'string' ? ` · <code>${m.look.art}</code>` : ''}</p>
    </div>
  </div>`;
}

// --- The view ---

interface State {
  q: string;
  area: string | null;
  kind: Kind | null;
  element: string | null;
  sort: { key: SortKey; dir: 1 | -1 } | null;
  open: string[];
}

export function mountMonsters(root: HTMLElement): { focus(id: string): void } {
  const rows = buildRows();
  const state: State = recall('monsters', { q: '', area: null, kind: null, element: null, sort: null, open: [] });

  const chip = (group: string, id: string, label: string, extra = '') => `<button class="chip" data-group="${group}" data-id="${id}" ${extra}>${label}</button>`;
  root.innerHTML = `
    <div class="toolbar">
      <input class="search" type="search" placeholder="Search monsters, moves, rooms" value="${esc(state.q)}">
      <div class="chips">${chip('area', '', 'All areas')}${AREAS.filter((a) => rows.some((r) => r.places.some((p) => p.area === a.id)))
        .map((a) => chip('area', a.id, `<span class="dot" style="background:${areaColor(a.id)}"></span>${esc(a.name)}`))
        .join('')}</div>
      <div class="chips">${chip('kind', '', 'All kinds')}${KINDS.map((k) => chip('kind', k.id, k.label)).join('')}</div>
      <div class="chips">${chip('element', '', 'All elements')}${ELEMENTS.map((e) => chip('element', e, icon(`el_${e}`, ELEMENT_LABELS[e]), `title="${ELEMENT_LABELS[e]}"`)).join('')}${chip('element', 'neutral', 'Neutral')}</div>
      <span class="count"></span>
    </div>
    <div class="table-wrap paper">
      <table class="mons">
        <thead><tr>${COLUMNS.map((c) => `<th class="${c.num ? 'num' : ''}${c.key ? ' sortable' : ''}" ${c.key ? `data-sort="${c.key}"` : ''} ${c.title ? `title="${esc(c.title)}"` : ''}>${c.label}<span class="arrow"></span></th>`).join('')}</tr></thead>
        <tbody></tbody>
      </table>
    </div>`;

  const tbody = root.querySelector('tbody')!;
  const count = root.querySelector<HTMLElement>('.count')!;
  const search = root.querySelector<HTMLInputElement>('.search')!;

  // Rows are made once and only moved around, so their pictures don't load again on every click.
  const make = (html: string) => {
    const t = document.createElement('template');
    t.innerHTML = html.trim();
    return t.content.firstElementChild as HTMLTableRowElement;
  };
  const trs = new Map(rows.map((r) => [r.m.id, make(rowHtml(r))]));
  const detailRows = new Map<string, HTMLTableRowElement>();
  const detailsFor = (r: Row) => {
    let tr = detailRows.get(r.m.id);
    if (!tr) detailRows.set(r.m.id, (tr = make(`<tr class="details"><td colspan="${COLUMNS.length}">${detailsHtml(r)}</td></tr>`)));
    return tr;
  };

  function showNote(id: string): void {
    const name = trs.get(id)!.querySelector('td.name')!;
    name.querySelector('.note-line')?.remove();
    name.insertAdjacentHTML('beforeend', noteLine(id));
  }

  const visible = (r: Row) =>
    (!state.q || state.q.toLowerCase().split(/\s+/).every((w) => r.search.includes(w))) &&
    (!state.area || r.places.some((p) => p.area === state.area)) &&
    (!state.kind || r.kind === state.kind) &&
    (!state.element || (r.m.element ?? 'neutral') === state.element);

  function render(): void {
    remember('monsters', state);
    let list = rows.filter(visible);
    if (state.sort) {
      const { key, dir } = state.sort;
      list = [...list].sort((a, b) => {
        const va = sortValue(a, key);
        const vb = sortValue(b, key);
        const c = typeof va === 'string' ? va.localeCompare(String(vb)) : va - (vb as number);
        return c * dir || a.order - b.order;
      });
    }
    tbody.replaceChildren(
      ...list.flatMap((r) => {
        const open = state.open.includes(r.m.id);
        const tr = trs.get(r.m.id)!;
        tr.classList.toggle('open', open);
        return open ? [tr, detailsFor(r)] : [tr];
      }),
    );
    count.textContent = list.length === rows.length ? `${rows.length} monsters` : `${list.length} of ${rows.length}`;
    for (const b of root.querySelectorAll<HTMLElement>('.chip')) {
      const value = state[b.dataset.group as 'area' | 'kind' | 'element'] ?? '';
      b.classList.toggle('on', value === b.dataset.id);
    }
    for (const th of root.querySelectorAll<HTMLElement>('th[data-sort]')) {
      const on = state.sort?.key === th.dataset.sort;
      th.classList.toggle('sorted', on);
      th.querySelector('.arrow')!.textContent = on ? (state.sort!.dir === 1 ? ' ▲' : ' ▼') : '';
    }
  }

  search.addEventListener('input', () => {
    state.q = search.value.trim();
    render();
  });

  root.addEventListener('click', (e) => {
    const target = e.target as HTMLElement;
    const chipEl = target.closest<HTMLElement>('.chip');
    if (chipEl) {
      const group = chipEl.dataset.group as 'area' | 'kind' | 'element';
      (state as unknown as Record<string, string | null>)[group] = chipEl.dataset.id || null;
      render();
      return;
    }
    const th = target.closest<HTMLElement>('th[data-sort]');
    if (th) {
      const key = th.dataset.sort as SortKey;
      // Words sort A-Z first, numbers biggest first; the third click goes back to story order.
      const first: 1 | -1 = key === 'name' || key === 'where' || key === 'element' ? 1 : -1;
      if (state.sort?.key !== key) state.sort = { key, dir: first };
      else state.sort = state.sort.dir === first ? { key, dir: first === 1 ? -1 : 1 } : null;
      render();
      return;
    }
    const tr = target.closest<HTMLElement>('tr.mon');
    if (tr) {
      const id = tr.dataset.id!;
      state.open = state.open.includes(id) ? state.open.filter((o) => o !== id) : [...state.open, id];
      render();
    }
  });

  tbody.addEventListener('input', (e) => {
    const area = e.target as HTMLElement;
    if (!(area instanceof HTMLTextAreaElement) || !area.classList.contains('note')) return;
    monsterNotes.set(area.dataset.id!, area.value);
    showNote(area.dataset.id!);
  });

  const file = monsterNotes.file;
  file.subscribe((textChanged) => {
    if (textChanged) {
      for (const id of trs.keys()) showNote(id);
      for (const [id, tr] of detailRows) {
        const area = tr.querySelector<HTMLTextAreaElement>('textarea.note')!;
        if (area !== document.activeElement) area.value = monsterNotes.get(id);
      }
    }
    for (const el of root.querySelectorAll('.note-status')) el.textContent = statusText(file);
  });

  render();
  return {
    focus(id: string) {
      if (!rows.some((r) => r.m.id === id)) return;
      Object.assign(state, { q: '', area: null, kind: null, element: null });
      search.value = '';
      if (!state.open.includes(id)) state.open.push(id);
      render();
      trs.get(id)!.scrollIntoView({ block: 'start' });
    },
  };
}
