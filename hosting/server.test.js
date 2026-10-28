import test from 'node:test';
import assert from 'node:assert/strict';
import { Readable } from 'node:stream';
import { createServer } from './server.js';

test('public feed, allowed download and private-object isolation', async () => {
  const version = '1.6.1';
  const names = [`ClickForge-Setup-v${version}.exe`, `ClickForge-Setup-v${version}.exe.sha256`, `ClickForge-v${version}-win-x64.zip`, `ClickForge-v${version}-win-x64.zip.sha256`];
  const metadata = JSON.stringify({ version, notes: 'Release', files: names.map(name => ({ name, size: 3, sha256: 'a'.repeat(64) })) });
  const requested = [];
  const server = createServer({ metadata: async () => metadata, download: async key => { requested.push(key); return { body: Readable.from(['abc']), length: 3 }; } });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const root = `http://127.0.0.1:${server.address().port}`;
  try {
    const feed = await (await fetch(root + '/api/releases/latest')).json();
    assert.equal(feed.version, version); assert.equal(feed.files.length, 4);
    assert.equal(await (await fetch(root + feed.files[0].url)).text(), 'abc');
    assert.deepEqual(requested, [`releases/${version}/${names[0]}`]);
    assert.equal((await fetch(root + '/downloads/1.6.1/secret.txt')).status, 404);
    assert.equal((await fetch(root + '/downloads/1.6.1/..%2fsecret')).status, 404);
    assert.equal((await fetch(root + '/api/releases/latest', { method: 'POST' })).status, 405);
  } finally { await new Promise(resolve => server.close(resolve)); }
});

test('invalid or unavailable metadata fails closed without exposing errors', async () => {
  const server = createServer({ metadata: async () => '{"version":"../../secret"}', download: async () => { throw new Error('private'); } });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try { const result = await fetch(`http://127.0.0.1:${server.address().port}/api/releases/latest`); assert.equal(result.status, 503); assert.doesNotMatch(await result.text(), /secret/); }
  finally { await new Promise(resolve => server.close(resolve)); }
});

test('home stays available with a clearly identified official release fallback', async () => {
  const server = createServer({ metadata: async () => { throw new Error('offline'); }, download: async () => { throw new Error('offline'); } });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    const root = `http://127.0.0.1:${server.address().port}`;
    const page = await fetch(root); assert.equal(page.status, 200);
    const html = await page.text(); assert.match(html, /Download from the official GitHub release/); assert.match(html, /github\.com\/ojeshfrrr\/ClickForge\/releases\/latest/);
    assert.equal((await fetch(root + '/api/releases/latest')).status, 503);
    assert.equal((await fetch(root + '/preview.png')).status, 200);
  } finally { await new Promise(resolve => server.close(resolve)); }
});
