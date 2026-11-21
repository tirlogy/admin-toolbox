// SPDX-License-Identifier: MIT
// Minimal static server for the published app: Brotli/gzip precompressed files, SPA fallback and the
// security headers from wwwroot/_headers (same rules as Cloudflare Pages), so E2E runs under the real CSP.
import { createServer } from 'node:http';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

const root = resolve(process.env.SITE_DIR ?? '../publish/wwwroot');
const port = Number(process.env.PORT ?? 4173);
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png', '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon', '.webmanifest': 'application/manifest+json', '.txt': 'text/plain', '.dat': 'application/octet-stream',
  '.woff2': 'font/woff2', '.pdb': 'application/octet-stream', '.dll': 'application/octet-stream',
};

function parseHeaders() {
  const file = join(root, '_headers');
  const rules = [];
  if (!existsSync(file)) return rules;
  let current = null;
  for (const line of readFileSync(file, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    if (!/^\s/.test(line)) { current = { pattern: line.trim(), headers: {} }; rules.push(current); continue; }
    const i = line.indexOf(':');
    if (current && i > 0) current.headers[line.slice(0, i).trim()] = line.slice(i + 1).trim();
  }
  return rules;
}
const rules = parseHeaders();
const matches = (pattern, path) => pattern.endsWith('*') ? path.startsWith(pattern.slice(0, -1)) : path === pattern;

createServer((req, res) => {
  const url = new URL(req.url ?? '/', 'http://localhost');
  let path = decodeURIComponent(url.pathname);
  let file = normalize(join(root, path));
  if (!file.startsWith(root)) { res.writeHead(403).end(); return; }
  if (path.endsWith('/') || !existsSync(file) || statSync(file).isDirectory()) {
    if (extname(path) && path !== '/') { res.writeHead(404).end('not found'); return; }
    file = join(root, 'index.html');
    path = '/index.html';
  }
  const headers = { 'Content-Type': types[extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-cache' };
  for (const rule of rules) if (matches(rule.pattern, path)) Object.assign(headers, rule.headers);
  const accept = String(req.headers['accept-encoding'] ?? '');
  let body = file;
  if (accept.includes('br') && existsSync(file + '.br')) { body = file + '.br'; headers['Content-Encoding'] = 'br'; }
  else if (accept.includes('gzip') && existsSync(file + '.gz')) { body = file + '.gz'; headers['Content-Encoding'] = 'gzip'; }
  headers['Vary'] = 'Accept-Encoding';
  res.writeHead(200, headers).end(readFileSync(body));
}).listen(port, () => console.log(`listening on http://localhost:${port} (${root})`));
