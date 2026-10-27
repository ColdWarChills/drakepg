import { esc } from './dom';

/**
 * Just enough Markdown for docs/ and the notes pad: headings, paragraphs, nested - and 1. lists (2-space indents),
 * **bold**, *italic*, ~~struck~~, `code` and [links](url). Text that isn't a list item under a list joins the item.
 */
export function renderMarkdown(src: string): string {
  const out: string[] = [];
  const lists: { indent: number; tag: 'ul' | 'ol' }[] = [];
  let para: string[] = [];

  const flushPara = () => {
    if (para.length) out.push(`<p>${inline(para.join(' '))}</p>`);
    para = [];
  };
  const closeTo = (indent: number) => {
    while (lists.length && lists[lists.length - 1].indent > indent) out.push(`</li></${lists.pop()!.tag}>`);
  };

  for (const raw of src.split(/\r?\n/)) {
    const line = raw.replace(/\t/g, '  ');
    if (!line.trim()) {
      flushPara();
      closeTo(-1);
      continue;
    }
    const heading = /^(#{1,4})\s+(.*)$/.exec(line);
    if (heading) {
      flushPara();
      closeTo(-1);
      const level = heading[1].length;
      out.push(`<h${level} id="${slug(heading[2])}">${inline(heading[2])}</h${level}>`);
      continue;
    }
    const item = /^(\s*)([-*]|\d+\.)\s+(.*)$/.exec(line);
    if (item) {
      flushPara();
      const indent = item[1].length;
      const tag = item[2].endsWith('.') ? 'ol' : 'ul';
      closeTo(indent);
      const top = lists[lists.length - 1];
      if (top && top.indent === indent && top.tag === tag) {
        out.push('</li>');
      } else {
        if (top && top.indent === indent) out.push(`</li></${lists.pop()!.tag}>`);
        out.push(`<${tag}>`);
        lists.push({ indent, tag });
      }
      out.push(`<li>${inline(item[3])}`);
      continue;
    }
    if (lists.length) out.push(`<br>${inline(line.trim())}`);
    else para.push(line.trim());
  }
  flushPara();
  closeTo(-1);
  return out.join('\n');
}

function inline(text: string): string {
  const codes: string[] = [];
  return esc(text)
    .replace(/`([^`]+)`/g, (_, code: string) => `\u0000${codes.push(code) - 1}\u0000`)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/~~(.+?)~~/g, '<del>$1</del>')
    .replace(/(^|[\s(])\*([^*\s][^*]*?)\*(?=[\s).,:;!?]|$)/g, '$1<em>$2</em>')
    .replace(/\[([^\]]+)\]\(((?:https?:\/\/|\.{0,2}\/)[^)\s]*)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>')
    .replace(/\u0000(\d+)\u0000/g, (_, i: string) => `<code>${codes[Number(i)]}</code>`);
}

export function slug(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}
