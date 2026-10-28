import http from 'node:http';
import { pipeline } from 'node:stream/promises';
import { pathToFileURL } from 'node:url';
import { createReadStream } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { renderHome } from './site.js';

const versionPattern = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const allowed = version => [
  `ClickForge-Setup-v${version}.exe`, `ClickForge-Setup-v${version}.exe.sha256`,
  `ClickForge-v${version}-win-x64.zip`, `ClickForge-v${version}-win-x64.zip.sha256`
];

function parseRelease(text) {
  const value = JSON.parse(text);
  if (!value || !versionPattern.test(value.version) || !Array.isArray(value.files) || value.files.length !== 4 ||
      typeof value.notes !== 'string' || value.notes.length > 12000) throw new Error('Invalid release metadata');
  const names = allowed(value.version);
  for (const name of names) {
    const matches = value.files.filter(file => file?.name === name);
    if (matches.length !== 1 || !Number.isSafeInteger(matches[0].size) || matches[0].size < 1 ||
        matches[0].size > 400 * 1024 * 1024 || !/^[a-f0-9]{64}$/.test(matches[0].sha256)) throw new Error('Invalid release file');
  }
  return { version: value.version, notes: value.notes, files: value.files };
}

export function createServer(storage) {
  return http.createServer(async (req, res) => {
    try {
      if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405, { Allow: 'GET, HEAD' }).end(); return; }
      const url = new URL(req.url, 'https://clickforge.portways.app');
      if (url.pathname === '/health') { res.writeHead(200, { 'content-type': 'text/plain' }).end('ok'); return; }
      if (url.pathname === '/preview.png') {
        res.writeHead(200, { 'content-type': 'image/png', 'cache-control': 'public, max-age=3600', 'x-content-type-options': 'nosniff' });
        if (req.method === 'HEAD') { res.end(); return; }
        await pipeline(createReadStream(fileURLToPath(new URL('../docs/bedwars-layout.png', import.meta.url))), res); return;
      }
      if (url.pathname === '/') {
        let homeRelease = null;
        try { homeRelease = parseRelease(await storage.metadata()); } catch { /* Show the existing official release while storage is unavailable. */ }
        const page = renderHome(homeRelease);
        res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-cache', 'x-content-type-options': 'nosniff' }).end(req.method === 'HEAD' ? undefined : page); return;
      }
      const release = parseRelease(await storage.metadata());
      const files = release.files.map(file => ({ ...file, url: `/downloads/${release.version}/${encodeURIComponent(file.name)}` }));
      if (url.pathname === '/api/releases/latest') {
        const data = JSON.stringify({ ...release, files });
        res.writeHead(200, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-cache', 'x-content-type-options': 'nosniff' }).end(req.method === 'HEAD' ? undefined : data); return;
      }
      const match = /^\/downloads\/([^/]+)\/([^/]+)$/.exec(url.pathname);
      if (!match || match[1] !== release.version) { res.writeHead(404).end(); return; }
      const file = files.find(item => encodeURIComponent(item.name) === match[2]);
      if (!file) { res.writeHead(404).end(); return; }
      const object = await storage.download(`releases/${release.version}/${file.name}`);
      if (object.length !== file.size) throw new Error('Stored file size differs from metadata');
      res.writeHead(200, { 'content-type': 'application/octet-stream', 'content-length': file.size, 'content-disposition': `attachment; filename="${file.name}"`, 'cache-control': 'public, max-age=3600', 'x-content-type-options': 'nosniff' });
      if (req.method === 'HEAD') { object.body.destroy(); res.end(); return; }
      await pipeline(object.body, res);
    } catch {
      if (!res.headersSent) res.writeHead(503, { 'content-type': 'text/plain', 'cache-control': 'no-store' }).end('Download service temporarily unavailable.');
      else res.destroy();
    }
  });
}

async function main() {
  const { S3Client, GetObjectCommand } = await import('@aws-sdk/client-s3');
  const required = ['S3_ENDPOINT', 'S3_BUCKET', 'S3_ACCESS_KEY_ID', 'S3_SECRET_ACCESS_KEY'];
  if (required.some(key => !process.env[key])) {
    const unavailable = async () => { throw new Error('Storage is not connected'); };
    createServer({ metadata: unavailable, download: unavailable }).listen(Number(process.env.PORT || 3000), '0.0.0.0');
    return;
  }
  const client = new S3Client({ endpoint: process.env.S3_ENDPOINT, region: process.env.S3_REGION || 'auto', forcePathStyle: true, credentials: { accessKeyId: process.env.S3_ACCESS_KEY_ID, secretAccessKey: process.env.S3_SECRET_ACCESS_KEY } });
  const get = key => client.send(new GetObjectCommand({ Bucket: process.env.S3_BUCKET, Key: key }));
  const storage = {
    async metadata() {
      const object = await get('releases/latest.json');
      if (object.ContentLength > 65536) throw new Error('Metadata too large');
      const chunks = []; let size = 0;
      for await (const chunk of object.Body) { size += chunk.length; if (size > 65536) { object.Body.destroy(); throw new Error('Metadata too large'); } chunks.push(chunk); }
      return Buffer.concat(chunks).toString('utf8');
    },
    async download(key) { const object = await get(key); return { body: object.Body, length: object.ContentLength }; }
  };
  createServer(storage).listen(Number(process.env.PORT || 3000), '0.0.0.0');
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main().catch(() => { console.error('ClickForge download service failed to start'); process.exitCode = 1; });
