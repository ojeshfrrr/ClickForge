import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { readFile, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';

const [version, directory, notesPath] = process.argv.slice(2);
if (!version || !/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(version) || !directory || !notesPath) {
  throw new Error('Usage: node hosting/create-manifest.js <version> <release-directory> <release-notes>');
}
const root = path.resolve(directory);
const names = [`ClickForge-Setup-v${version}.exe`, `ClickForge-Setup-v${version}.exe.sha256`, `ClickForge-v${version}-win-x64.zip`, `ClickForge-v${version}-win-x64.zip.sha256`];
const files = [];
for (const name of names) {
  const file = path.join(root, name); const info = await stat(file);
  if (!info.isFile() || info.size < 1 || info.size > 400 * 1024 * 1024) throw new Error(`Invalid release file: ${name}`);
  const hash = createHash('sha256'); for await (const chunk of createReadStream(file)) hash.update(chunk);
  files.push({ name, size: info.size, sha256: hash.digest('hex') });
}
for (const file of files.filter(item => !item.name.endsWith('.sha256'))) {
  const checksum = (await readFile(path.join(root, file.name + '.sha256'), 'utf8')).trim().split(/\s+/);
  if (checksum.length !== 2 || checksum[0].toLowerCase() !== file.sha256 || checksum[1].replace(/^\*/, '') !== file.name) throw new Error(`Checksum mismatch: ${file.name}`);
}
const notes = await readFile(path.resolve(notesPath), 'utf8');
if (notes.length > 12000) throw new Error('Release notes exceed 12000 characters');
await writeFile(path.join(root, 'latest.json'), JSON.stringify({ version, notes, files }, null, 2) + '\n', { flag: 'wx' });
console.log(`Validated ${files.length} release files and wrote latest.json for ${version}.`);
