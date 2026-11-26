// Ask Gemini to do what a brief file says, through Google's Antigravity CLI (agy). Started by scripts/gemini.cmd.
// The brief goes inside the message itself (an argument array, so line breaks and quotes survive), and Gemini is told
// to use no tools: an unattended run can't answer an approval request, and a run that stalled waiting on one
// (Gemini 4 Carbon in plan mode, 2026-11-26) timed out with no answer.
// Writes next to the brief: <name>.gemini.txt (the answer), <name>.gemini.json (agy's JSON), <name>.gemini_err.txt,
// and <name>.agy_log.txt (names the model that answered; it holds the account email, so never commit it).
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const [briefArg, model = 'gemini-4-neon-high'] = process.argv.slice(2);
if (!briefArg) {
  console.log('Usage: scripts\\gemini.cmd <brief.md> [model]');
  process.exit(1);
}
const brief = path.resolve(briefArg);
const dir = path.dirname(brief);
const name = path.parse(brief).name;
const out = (ext) => path.join(dir, `${name}.${ext}`);
const agy = path.join(process.env.LOCALAPPDATA ?? '', 'agy', 'bin', 'agy.exe');
if (!existsSync(agy)) {
  console.log(`agy not found at ${agy}`);
  process.exit(1);
}

const prompt = `${readFileSync(brief, 'utf8')}\n\n---\nAnswer from this message alone. Use no tools, and read or write no files. Reply with only what the brief asks for.`;
const run = spawnSync(
  agy,
  [`--print=${prompt}`, '--model', model, '--sandbox', '--output-format', 'json', '--print-timeout', '300s', '--log-file', out('agy_log.txt')],
  { cwd: dir, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 },
);

writeFileSync(out('gemini.json'), run.stdout ?? '');
writeFileSync(out('gemini_err.txt'), `${run.stderr ?? ''}${run.error ? `\n${run.error}` : ''}`);
let answer = '';
try {
  answer = JSON.parse(run.stdout).response ?? '';
} catch {
  // not JSON: the error file says why
}
writeFileSync(out('gemini.txt'), answer);

let label = '?';
try {
  const hit = readFileSync(out('agy_log.txt'), 'utf8').match(/label="([^"]+)"/);
  if (hit) label = hit[1];
} catch {
  // no log
}
console.log(`done, exit ${run.status}, model ${label}, ${answer ? 'answered' : 'NO ANSWER (see the .gemini_err.txt)'}`);
