# ClickForge website and release storage

The root Node project serves the ClickForge website on `PORT` and is suitable for
Portways' Node deployment. Start locally with `npm ci` and `npm start`; run
`npm test` for the HTTP and storage-isolation checks. Node 20 or newer is required.

The public homepage remains available without a bucket and directs visitors to
the official GitHub release page. It does not claim that Portways downloads or the
updater migration are ready. `/health` checks that the process is listening.
`/api/releases/latest` and `/downloads/<version>/<file>` require a linked bucket
with a validated release catalogue. They return 503 if storage is unavailable.

Portways injects `S3_ENDPOINT`, `S3_REGION`, `S3_BUCKET`, `S3_ACCESS_KEY_ID`,
`S3_SECRET_ACCESS_KEY` and `S3_FORCE_PATH_STYLE` after linking a project to its
private S3 bucket. Credentials stay on the server. The service only exposes the
four filenames listed in its validated catalogue and checks object lengths.

Prepare a release in a local folder with the installer, ZIP and both checksum
files, then run:

```
node hosting/create-manifest.js 1.6.1 artifacts/portways-release docs/RELEASE-v1.6.1.md
```

The script verifies each installer/ZIP checksum and creates `latest.json` without
overwriting an existing file. Upload the four files into `releases/1.6.1/` and
upload `latest.json` to `releases/latest.json` only after the files are available.
For later releases, substitute the actual version and release notes. Large S3 API
uploads need multipart transfer; Portways' dashboard documents a 100 MB limit.

This service is the hosting foundation only. ClickForge v1.6.1's updater still
uses GitHub. Migrating it requires an explicitly tested bridge release and must
not be presented as complete merely because the site is online. Preserve package
verification, target-host restrictions and rollback during that work.
