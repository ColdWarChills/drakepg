import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig, type Plugin } from 'vite';

/**
 * The codex page (codex.html, dev server only) keeps the designer's notes in docs/: GET `/__codex/<file>` reads one
 * ({ text, mtime }), PUT or POST ({ text, base }) writes it. A write whose `base` isn't the file's current time is
 * refused with 409 and the file as it is now, so a page never overwrites what a chat wrote meanwhile unknowingly.
 */
function codexNotes(): Plugin {
  const FILES: Record<string, string> = { notes: 'docs/NOTES.md', monsters: 'docs/MONSTER_NOTES.md' };
  return {
    name: 'codex-notes',
    configureServer(server) {
      server.middlewares.use('/__codex/', (req, res) => {
        const rel = FILES[(req.url ?? '').replace(/^\//, '').split('?')[0]];
        if (!rel) {
          res.statusCode = 404;
          res.end();
          return;
        }
        const path = resolve(server.config.root, rel);
        const read = () => ({ text: existsSync(path) ? readFileSync(path, 'utf8') : '', mtime: existsSync(path) ? statSync(path).mtimeMs : 0 });
        const reply = (status: number, body: unknown) => {
          res.statusCode = status;
          res.setHeader('content-type', 'application/json');
          res.end(JSON.stringify(body));
        };
        if (req.method === 'GET') return reply(200, read());
        if (req.method !== 'PUT' && req.method !== 'POST') return reply(405, {});
        let body = '';
        req.on('data', (chunk) => (body += chunk));
        req.on('end', () => {
          try {
            const { text, base } = JSON.parse(body) as { text: string; base?: number };
            const now = read();
            if (base !== undefined && base !== now.mtime) return reply(409, now);
            writeFileSync(path, text);
            reply(200, read());
          } catch {
            reply(400, {});
          }
        });
      });
    },
  };
}

export default defineConfig({
  // Relative paths so the build works from any subfolder (itch.io serves games from a CDN path).
  base: './',
  plugins: [codexNotes()],
  build: {
    outDir: 'dist',
    chunkSizeWarningLimit: 2000,
  },
});
