# Installing ClickForge

The v1.2.0 candidate adds a real **Inno Setup 6.7.3** per-user Windows installer alongside the portable ZIP. No matching version tag or GitHub Release has been published. Public v1.1.2 still provides only the ZIP.

## Install and launch

Run `ClickForge-Setup-vX.Y.Z.exe`. Setup defaults to `{localappdata}\Programs\ClickForge`, normally `%LOCALAPPDATA%\Programs\ClickForge`. It uses `PrivilegesRequired=lowest`, targets x64 Windows (not ARM), and has a conservative Windows 10 22H2 / Windows 11 minimum. No administrator prompt, service, driver, scheduled task or startup registration is added.

Desktop and Start menu shortcuts are independently selectable and enabled by default. They target the installed `ClickForge.exe`, use its folder as the working directory, and use its embedded gold pointer icon. Setup remembers opt-outs on reinstallation. At the end, launching the application is an unchecked optional action; silent installs never launch it. Clicking remains disabled on application launch.

The stable installation is independent of where Setup was downloaded. Keep `ClickForge.Updater.exe` beside the main EXE. Both files, the README, runtime notices and update manifest come from the same verified ZIP. Installation also adds Inno's uninstaller/log and a standard **Windows Installed Apps** entry. No personal configuration is copied into the application folder.

## Upgrade, repair and collisions

Rerun the same or a newer Setup to repair or upgrade an installation created by this installer. Its stable identity is `ClickForge.Desktop`. Setup checks the installed executable's version and refuses downgrades, including when an in-app update is newer than the original installer registration. It refuses to relocate an existing registered installation silently; uninstall first if you intend to change its folder.

Setup refuses an unrelated nonempty destination, including an existing portable copy. It does not import, move, trust or overwrite that copy. Choose an empty destination or keep using the portable application separately. A same-name Desktop/Start shortcut pointing elsewhere also blocks creation; rename that shortcut or opt out.

ClickForge holds `Local\ClickForge.SetupGuard` for its lifetime. Setup and Uninstall use Inno's `AppMutex` check and ask you to close ClickForge. **Press Emergency Stop, then choose Exit ClickForge in its tray menu**, especially when close-to-tray is enabled. Retry after it exits. Setup's Restart Manager automatic closure/restart is disabled; it never force-kills processes. Locked-file failures do not bypass validation or request administrative access.

## Existing in-app updates

The application still discovers the official stable GitHub release and downloads the verified portable ZIP. Setup EXEs are additional release assets; the updater selects the exact ZIP/checksum names and never runs Setup as an in-app update.

The installation directory remains the directory of the running executable. Staging/backups remain under `%LOCALAPPDATA%\ClickForge\Updates`. The existing helper waits for clean shutdown, verifies package and hashes again, replaces only manifest-managed files, and requires startup acknowledgment before success. On failure it rolls back. The uninstaller and its registration are outside that managed payload, so updates preserve them and shortcut targets stay stable.

On startup, the app refreshes Installed Apps' display version **only** when the existing per-user registry marker belongs to ClickForge and its installation path exactly matches the current executable directory. Portable instances create no registry entries, and a copy elsewhere cannot overwrite the registration. Registry display metadata failures do not block startup. Rollback to a previous installer-enabled application refreshes the display version again on restart.

## Uninstall and personal data

Use **Windows Settings → Apps → Installed apps → ClickForge → Uninstall**, or run `unins000.exe` in the installation folder. Exit ClickForge first.

The uninstaller removes the known installed payload, uninstaller metadata and Installed Apps registration. It removes only recorded shortcuts that still point to this exact installed executable. A user-replaced shortcut pointing to another application is preserved. Empty installer-created Start menu directories are removed; directories containing other files are preserved. Unrelated files in the installation folder are not recursively removed.

Personal data is **always preserved**:

- `%LOCALAPPDATA%\ClickForge\settings.json`
- `%LOCALAPPDATA%\ClickForge\Profiles\`
- `%LOCALAPPDATA%\ClickForge\Updates\`, including failed-update recovery journals/backups

There is no default or hidden data-deletion option. The existing updater cleans only old successful caches according to its established policy, retaining failed recovery data.

## Build the two distributions

On Windows, use the SDK in `global.json`:

```powershell
./build.ps1 -SkipPackage
./scripts/Package-Release.ps1 -OutputDirectory artifacts/installer-release/payload
./scripts/Test-Installer.ps1 -ZipPath artifacts/installer-release/ClickForge-v1.2.0-win-x64.zip
```

`Package-Release.ps1` builds the self-contained application/helper, writes the existing seven-entry ZIP and SHA-256, then calls `Build-Installer.ps1`. The installer builder reuses the **existing production archive verifier** before staging. It generates Inno's file entries directly from the accepted manifest plus that manifest itself; no second independent payload list or wildcard directory copy is maintained.

The resulting files beside `payload` are:

- `ClickForge-vX.Y.Z-win-x64.zip`
- `ClickForge-vX.Y.Z-win-x64.zip.sha256`
- `ClickForge-Setup-vX.Y.Z.exe`
- `ClickForge-Setup-vX.Y.Z.exe.sha256`

The compiler bootstrap script downloads the immutable official `is-6_7_3/innosetup-6.7.3.exe` release and requires SHA-256 `9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732`. The compiler is installed per-user into ignored `artifacts/tools/inno-6.7.3`, without shell integration/tasks, and cached with an integrity receipt. Inno's compiler executables use `0.0.0.0` file-version resources; provenance comes from the pinned distribution hash, not those resources. This is build tooling and is not bundled with ClickForge.

`scripts/Export-AppIcon.ps1` deterministically renders the actual `ForgeIcon` WPF vector resource into the checked-in multi-resolution ICO. Regenerate it when changing the brand icon. The application and Setup embed it; shortcuts reference the installed application rather than a temporary ICO.

## Future releases

The existing trusted matching-tag GitHub Actions release job produces **both** distributions from the same clean source/version. It runs the app tests, package validation, actual installer lifecycle tests and independent-helper scenarios before upload. All four assets are uploaded to a draft and their names, sizes and SHA-256 digests are checked before publication. Ordinary branch/PR builds do not publish. No existing tag or release is replaced.

Local review packages include uncommitted candidate changes and identify the base HEAD in the existing manifest. Only the strict matching-tag workflow is an official release build. The binaries and Setup output are **unsigned**; checksums are integrity checks, not publisher signatures. No signing credentials are present. Do not disable Windows security protections.

## Verification boundaries

The actual lifecycle test builds isolated test-identity installers from the published v1.1.2 ZIP and the current verified ZIP. It uses unique Installed Apps entries and shortcut names, a disposable per-user Programs folder, mocked mouse input, and separate test settings. It tests clean install, upgrade, downgrade refusal, repair, opt-outs, shortcut collisions, running-app refusal, installed WPF startup, helper success/rollback in a per-user Programs directory, uninstall and preservation of personal data/unrelated files.

These are native installer/uninstaller executions, not just compilation checks. They do not publish a release, replace the user's portable installation, send clicks to Roblox, or claim a real future public GitHub end-to-end upgrade was performed. See the generated lifecycle report and task completion report for the actual results of this run.
