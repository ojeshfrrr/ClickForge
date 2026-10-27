# Updating ClickForge

## v1.6.1 update flow

Open **Updates**, choose **Check for updates**, then **Download update**. After the
package is verified, **Install and restart** becomes available and still asks for
confirmation. Opening Updates stops input and blocks activation while it is open;
closing it does not resume a request. Automatic startup checks only check for a
release and never install or start input.

The updater and helper share `Version.props`. Requests now identify the actual
assembly release version rather than a fixed historic updater version. The
official public feed, allowed download hosts, strict payload/manifest checks,
checksums, independent helper, startup acknowledgement and rollback are unchanged.
The installer and ZIP are still built and verified by the tagged release workflow.
Existing updater-enabled versions use the same public channel without an account.

Settings, profiles and the Bedwars `MouseSetup` preference are retained by both
upgrade and rollback. The running request is never persisted. An install button
cannot be enabled merely by a status label without an actual verified staged
package. See [v1.6.1 release verification](RELEASE-v1.6.1.md).

## Versions and the first updater-enabled build

`Version.props` is the single source of the application/helper version. The first updater-enabled version is **1.1.1**. Use `MAJOR.MINOR.PATCH`: patches for fixes, minor versions for compatible features, and major versions for breaking changes. Release tags must exactly match it, prefixed with `v`.

The runtime reads `AssemblyInformationalVersion`; build metadata such as the source commit does not affect semantic precedence. Tags with a `v` prefix work. Prereleases/drafts are not update candidates, and equal or older versions are never installed.

**Existing v1.0.0 installations cannot discover an updater that was not in their executable.** Download/extract the first updater-enabled ZIP once. Keep both executables together. Subsequent compatible releases can be installed from Updates.

## Publish an update

1. Change `ClickForgeVersion` in `Version.props` to a new, unused stable version.
2. Make the code/documentation changes and run `./build.ps1 -SkipPackage` on Windows with the SDK in `global.json`.
3. Commit and push the changes to the intended branch.
4. Create and push the matching version tag. For the initial updater release:

```powershell
git push origin main
git tag -a v1.1.1 -m "ClickForge v1.1.1"
git push origin v1.1.1
```

For later releases, update the version and tag together, for example `1.1.2` / `v1.1.2`. Never move an existing published tag or replace its assets. If a workflow left an unpublished draft, inspect its failure and remove that incomplete draft explicitly before retrying; the workflow refuses to overwrite it.

A normal branch push runs build/tests. **It does not publish an application update.** Only a trusted `push` to a matching `v*` tag in `ojeshfrrr/ClickForge` can run the release job. Pull requests receive read-only permissions and cannot publish. Repository Actions settings and the GitHub token must permit the release job's `contents: write` permission.

## What GitHub Actions does

The existing `.github/workflows/build.yml` now has build and release jobs:

- Restore/build the WPF application and separate helper; run the core, update, and safe UI suites.
- Reject existing releases, mismatched tags/versions, or a mismatched source commit.
- Package the exact checked-out, clean tagged commit using `scripts/Package-Release.ps1 -Strict`.
- Publish self-contained single-file Windows x64 app and helper, with no separate runtime installation needed.
- Validate both embedded executable versions/source commits and the archive's strict payload contract.
- Exercise the actual helper with disposable fixture processes, including failure/rollback. No real installation is replaced and no mouse input is generated.
- Generate `ClickForge-v<version>-win-x64.zip` and its `.zip.sha256` checksum.
- Generate GitHub release notes and upload both assets to a **draft** release.
- Verify uploaded filenames, state, ZIP size and GitHub's SHA-256 digest before making the release stable/latest.

If build/tests/verification fail, publication stops. An upload failure may leave an unpublished draft for inspection; the workflow does not overwrite existing releases. It uses the ephemeral Actions token only in the trusted release job. No token is embedded in the application.

For a local review package, run:

```powershell
./scripts/Package-Release.ps1 -OutputDirectory artifacts/review/payload
```

This generates the ZIP/checksum beside `payload`. It refuses to overwrite a ZIP. Use a new output directory for another package. Local packages can contain uncommitted development work; only strict tagged workflow builds are publication artifacts.

## Package contract

The ZIP contains exactly:

- `ClickForge.exe`
- `ClickForge.Updater.exe`
- `update-manifest.json`
- `README.txt`
- `licenses/DOTNET-LICENSE.txt`
- `licenses/DOTNET-THIRD-PARTY-NOTICES.txt`
- `licenses/WINDOWSDESKTOP-LICENSE.txt`

The manifest has format version 1, the release version, a source commit and hashes of the six payload files. There are no settings defaults, user databases, scripts, debug symbols or private configuration in the package. Older ZIPs without this contract cannot be installed by the updater.

The v1.2.0 candidate embeds its tray, theme and native icon resources in the WPF executable, so the payload contract is unchanged. Profiles live under `%LOCALAPPDATA%/ClickForge/Profiles`, outside the installation. The updater replaces only the manifest-managed payload; success and rollback tests also verify unrelated profile files remain byte-identical. Close-to-tray is explicitly bypassed for updater shutdown, and startup never restores an active click session. This task prepares a local review package only; no v1.2.0 tag or release is published.

The added Inno Setup installer wraps the same verified ZIP payload and installs it per-user at `%LOCALAPPDATA%/Programs/ClickForge`. Its uninstaller and registry registration are not update payload entries. Desktop/Start menu shortcuts continue targeting the same installed executable through updates. The application refreshes its registered display version only for an owned registration at that exact path. The in-app updater still chooses the ZIP/checksum pair when a release also contains Setup; it never downloads or runs Setup. See [installation details](INSTALLING.md).

Future trusted matching-tag release jobs produce and validate both `ClickForge-v<version>-win-x64.zip` and `ClickForge-Setup-v<version>.exe`, plus a separate SHA-256 file for each. The workflow executes native installer lifecycle and helper checks before uploading all four assets to a draft, then verifies all uploaded asset sizes and digests before publication. No new branch-publish path is added.

## Discovery, download and verification

Open **Updates** in the navigation rail. The current version is assembly-derived. **Check for updates** asynchronously requests the configured public endpoint `https://api.github.com/repos/ojeshfrrr/ClickForge/releases/latest`, confirmed from `origin`.

The manual check has a 15-second timeout. Optional startup checks default off and never install anything automatically. API failures, missing assets, malformed responses and rate limits leave normal clicking functionality available. One request/operation at a time prevents duplicate checks/downloads.

Release notes are displayed as plain text, never interpreted as commands, scripts, HTML or XAML. The client validates the official repository's exact Windows asset names/URLs, then downloads using the discovered **asset IDs**, so deleting/re-uploading a same-named asset cannot silently substitute a different ID after discovery. Only HTTPS is used. Asset redirects are limited to GitHub and the approved GitHub asset CDN hosts.

**Update now** downloads to a fresh per-user `%LOCALAPPDATA%/ClickForge/Updates/<random-id>` directory. Progress includes bytes and percentage. Downloads have a bounded timeout and cancellation. The client checks advertised size, the published SHA-256 file, archive entry names, traversal/duplicate/link entries, size limits, payload hashes, Windows x64 executable headers and matching versions. It stages files outside the running installation. The helper repeats checksum/manifest verification; the staged manifest must be byte-identical to the one inside the verified ZIP.

Checksums detect incomplete/corrupt or mismatched content. A checksum hosted by the same compromised release account is **not proof of publisher identity**. No Authenticode identity verification or code-signing guarantee is claimed for these unsigned app/helper binaries.

## Install and restart

After verification, **Install and restart** asks for confirmation. Input is stopped before that confirmation. Cancelling never resumes clicking automatically.

1. The application checks that its own folder is writable and its installed helper matches its version.
2. It copies that current helper into the private update workspace and launches it with `ProcessStartInfo.ArgumentList`; no shell command or release-note text is executed.
3. The helper verifies the request, archive, stage, parent executable path/start time, and acquires an exclusive per-installation lock.
4. Only after the helper reports ready does ClickForge request normal shutdown. The helper waits up to 60 seconds for that exact process to exit. It never force-kills it or terminates unrelated processes.
5. The helper backs up every existing managed file, flushes backups, records their hashes and writes a persistent journal before replacement.
6. It replaces only the known package files. Settings in LocalApplicationData and unrelated files are preserved.
7. It validates the installed files, starts the new executable, and waits up to 30 seconds for a nonce/version/path acknowledgement.
8. After its main window renders, the new app acknowledges its actual version. The helper then commits success. The app waits for that receipt before reporting the update successful.
9. The new ClickEngine starts disabled regardless of preferences or the prior session.

If the application folder is not writable, move the **entire extracted package** to a folder you own and run it there before retrying. There is no automatic elevation.

## Failure and recovery

Before replacement, failures leave installed files unchanged. During replacement/startup, the helper attempts rollback and relaunches the prior version. It validates backup hashes before restoration. A child that fails startup is asked to close; if it refuses, the helper does not kill it, and records that manual recovery is required. Backups are retained on every failure.

If an update is interrupted by a crash/restart, the workspace retains `request.json`, `journal.json`, `result.json`, `runner.exe` and `backup/`. The failure message identifies the recovery folder. Close ClickForge, inspect the receipt, and run the **trusted helper retained in that folder** with the exact request path, for example:

```powershell
& "$env:LOCALAPPDATA/ClickForge/Updates/<id>/runner.exe" --recover "$env:LOCALAPPDATA/ClickForge/Updates/<id>/request.json"
```

Replace `<id>` with the folder from the failure receipt. Recovery only restores managed files from that journal; it does not erase user settings. If backups are damaged or files remain locked, recovery reports failure and retains them. Keep the workspace for inspection rather than deleting backups or overwriting an unknown installation. A previously successful transaction is not rolled back by a repeated invocation.

Success removes the staged/archive/backup bulk. The currently running helper and receipts remain temporarily; completed cache folders older than seven days are cleaned on subsequent normal startup. Failed/incomplete recovery folders are never automatically deleted. Incomplete cancelled downloads are removed immediately; a process crash during download can leave an abandoned workspace for manual inspection.

## Tests and verification boundaries

`tests/ClickForge.UpdateTests` uses mocked HTTP responses and tiny local archives for discovery, versions, malformed metadata, missing assets, checksum errors, size/traversal/link rejection, cancellation/duplicates, settings preservation, shutdown coordination, rollback, startup receipts and cache safety.

`./scripts/Test-UpdateProcess.ps1 -PayloadDirectory artifacts/package` uses the real published helper with disposable console fixture executables. It checks parent-exit coordination, replacement, restart acknowledgement, and failed-startup rollback. It does not install over a user's real ClickForge copy.

The WPF diagnostic suite checks actual updater controls/state presentation and ensures preferences never activate clicking. Its input service is mocked; no game is automated. `--check-live` on the update test harness checks public discovery without installing. Local and process tests do not prove a remote end-to-end upgrade between two published app versions; validate that separately on a disposable installation after publishing the first updater-enabled release.

## Private repositories and trust

The configured channel is public and needs no client credential. A private repository would require an appropriate authenticated distribution design; do not embed a PAT in the app, manifest or client configuration. Making the source private also restricts access to release assets. Protect the GitHub account and tag/release permissions. This updater does not bypass Windows security or antivirus protections.
