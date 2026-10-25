# Windows installer implementation report

## Outcome

Implemented a real **Inno Setup 6.7.3** installer alongside the existing portable ZIP. Both are built from the same verified seven-entry application payload. The v1.2.0 candidate is prepared for source review; no version tag or GitHub Release has been published. The ongoing UI/tray/profile work and existing user changes were preserved. Version.props remains 1.2.0.

The ordinary installer defaults to `%LOCALAPPDATA%\Programs\ClickForge`. It creates a standard per-user Installed Apps registration and uninstaller. Desktop and Start menu shortcuts are independently selectable and on by default. Both target the installed EXE, use its directory as working directory and its embedded gold pointer icon. Reinstallation remembers opt-outs. The optional final Launch checkbox is unchecked, and silent installation does not launch the application.

User settings, profiles and update recovery data are preserved on uninstall. No service, driver, startup task, administrator requirement, second updater or click-engine implementation was added.

## Outputs

- Setup: `artifacts/windows-installer/ClickForge-Setup-v1.2.0.exe`
- Setup checksum: `artifacts/windows-installer/ClickForge-Setup-v1.2.0.exe.sha256`
- Portable ZIP: `artifacts/windows-installer/ClickForge-v1.2.0-win-x64.zip`
- ZIP checksum: `artifacts/windows-installer/ClickForge-v1.2.0-win-x64.zip.sha256`

Setup SHA-256: `22a0bd2ab4cf78d06e6d6fa784602a975e0a9dad15441846d8e1e0c0951279a3`.

ZIP SHA-256: `c75daf40b7b1a373ef6442c706f14a46c6aa6072fa2b146a3197327ae481c47b`.

The application/helper/ZIP/Setup version is 1.2.0. These review packages were built before the candidate commits, so their manifest/assembly provenance identifies base HEAD `6201f2bfbde18118bfef02076f28fd4985579332` and includes the working-tree changes at build time. They are not proof of an exact committed release. The future strict workflow requires a clean matching source commit/tag/version and rebuilds both distributions.

The Setup executable is unsigned (verified with Get-AuthenticodeSignature). Signing was not configured and no signing credentials were used. Checksums are not publisher authentication; users must not disable Windows security protections.

## Architecture and safeguards

`Package-Release.ps1` retains the portable archive contract, then invokes `Build-Installer.ps1`. That script calls the **existing production archive verifier**, stages into a fresh directory, generates Inno's [Files] entries from the accepted manifest plus the manifest itself, and validates Setup's numeric/file/product versions. There is no second manually maintained runtime payload list or wildcard source folder copy.

The pinned compiler bootstrap verifies the official distribution SHA-256 before per-user installation into ignored workspace tooling. The native multi-resolution icon is rendered from the actual ForgeIcon WPF vector and embedded into both the app and Setup.

The installer uses a stable application identity and verifies its existing registry ownership/path before upgrading. It rejects unrelated nonempty folders, relocation without uninstall, newer installed binaries and conflicting same-name shortcuts. The application exposes a lifetime mutex checked by Setup/Uninstall. Restart Manager automatic close/restart is off, and no process is force-killed. Users are told to stop clicking and use tray Exit before retrying.

Shortcut ownership is recorded after Inno commits its uninstall registration. Creation/removal uses the Windows Shell link API; uninstall deletes only recorded links that still target this exact installation. Unrelated user files and links survive. Standard Inno registration/uninstaller cleanup removes the application entry and known managed payload.

The existing updater still derives installation directory from the running EXE, uses its separate helper, verifies the ZIP/checksum/stage, waits for clean exit, requires startup acknowledgment, and rolls back failures. It ignores additional Setup assets. The app updates Installed Apps' display version only for an already-owned registration at its own exact directory; portable launches create no entries. The uninstaller/shortcut metadata remain outside the update payload. No package verification, target check, Roblox validation, input-release, emergency-stop or profile restriction was weakened.

## Executed checks

| Check | Result |
|---|---|
| `./build.ps1 -SkipPackage` | Release app/helper builds: zero warnings, zero errors |
| Core executable test suite | 74 passed, 0 failed |
| Updater executable test suite | 36 passed, 0 failed; includes coexistence with Setup assets |
| Native WPF diagnostics | 122 passed; adds registration ownership/path/version and lifetime-guard checks |
| Portable ZIP production verifier | Passed exact payload, PE version and manifest hashes |
| Inno compiler and output validation | Passed version/resource/hash checks |
| Actual native installer lifecycle | **26 passed** |
| Final production-identity Setup smoke checks | **4 passed**, disposable install and uninstall |
| Actual installed application's WPF suite | **122 passed**, mocked mouse, separate test data, explicit disabled-on-start check |
| Real independent helper scenarios in a per-user Programs directory | Both success/acknowledgment and failed-startup rollback passed; settings/profiles preserved |
| Uploaded GitHub assets / Actions execution | Not executed; publishing prohibited for this task |

The lifecycle run used actual Setup/uninstall executables, not packaging-only assertions. It verified:

- Clean installation in an actual per-user Programs directory without administrative privileges (native installer log records User privileges: None / Administrative mode: No).
- Desktop and Start menu links, correct EXE/working directory/native icon location, and no automatic launch.
- Upgrade from the actual published v1.1.2 ZIP to this candidate, exact installed hashes and unchanged shortcut paths.
- Refused downgrade and refused silent relocation/unrelated-folder replacement.
- Independent Start menu and Desktop opt-outs, remembered choices and collision protection.
- Running application protection for Setup and Uninstall, with no force closure, followed by coordinated normal exit.
- The installed app's actual WPF diagnostics with mock input and separate settings.
- Real helper success/rollback in a stable per-user Programs directory and unchanged installed shortcut targets.
- Actual uninstall, managed executable/registration/owned-shortcut removal, preservation of an unrelated shortcut/file, and byte-identical pre-existing personal settings/profile files.

Actual disposable installation path for the completed lifecycle run:

`%LOCALAPPDATA%\Programs\ClickForge Test c2b40933da0247e6b087f7fe22388739`

That application was uninstalled. Its intentionally unrelated test sentinel was preserved, proving the uninstaller did not recursively remove the directory. The user's existing portable installation was not replaced by the installer tests.

The final non-test Setup output was additionally installed under a disposable workspace directory using its real `ClickForge.Desktop` identity, with both shortcut options off. Four smoke checks passed: actual production registration, opt-out preservation of the user's real shortcut paths, launch of the installed executable and refresh of its own deliberately stale display-version value back to 1.2.0, and actual production uninstall/registration removal. Evidence is `artifacts/installer-production-smoke/aeedff111c6c4a6fb3bef5364fa359c4/results.txt`. No production or test ClickForge uninstall registration remained afterward.

Evidence:

- `artifacts/logs/installer-source-build.txt`
- `artifacts/logs/installer-lifecycle-complete.txt`
- `artifacts/installer-tests/c2b40933da0247e6b087f7fe22388739/results.txt`
- `artifacts/installer-tests/c2b40933da0247e6b087f7fe22388739/installed-ui-tests.txt`
- Native Setup/uninstall logs in the same lifecycle directory
- `artifacts/updater/logs/process-tests.txt`
- `artifacts/logs/installer-final-output.txt`

An intermediate native upgrade test exposed Inno's uninstall-key recreation ordering; the ownership marker now writes after registration commits. A second run exposed an absolute-path assumption in the helper test harness; the harness now supports the per-user Programs fixture directory. Both issues were fixed and the complete 26-check lifecycle subsequently passed. Tests were not weakened to bypass those failures.

## Exact source files changed in this installer task

- `.github/workflows/build.yml`
- `README.md`
- `docs/UPDATING.md`
- `docs/INSTALLING.md` (new)
- `docs/INSTALLER-REPORT.md` (new)
- `installer/ClickForge.iss` (new)
- `scripts/Build-Installer.ps1` (new)
- `scripts/Export-AppIcon.ps1` (new)
- `scripts/Get-InnoSetup.ps1` (new)
- `scripts/Test-Installer.ps1` (new)
- `scripts/Package-Release.ps1`
- `scripts/Test-UpdateProcess.ps1`
- `src/ClickForge/Assets/ClickForge.ico` (new)
- `src/ClickForge/ClickForge.csproj`
- `src/ClickForge/InstallationRegistration.cs` (new)
- `src/ClickForge/App.xaml.cs`
- `src/ClickForge/UiDiagnostics.Daily.cs`
- `tests/ClickForge.UpdateTests/Program.cs`

Earlier pending UI/profile/tray changes remain in the checkout and are described in their existing reports. No reset, history rewrite, forced push or user-data deletion occurred.

## Limits and release work remaining

Native tests ran on this Windows 11 x64 environment. Other Windows versions, architectures, security products and enterprise restrictions on Windows Script Host were not runtime-tested. Shortcut creation failures are surfaced rather than reported as success. Installer UI was exercised silently; physical wizard interaction was not claimed.

The helper used disposable protocol fixtures in the per-user Programs tree; no real future public GitHub release was installed over the user's current application. Live input/Roblox acceptance was not tested. Actual settings/profile contents were not inspected or exported, only hashed for preservation checks.

The workflow has been extended with pinned tooling and installer/helper gates. Its GitHub execution status must be checked separately from the local results above. YAML was reviewed locally; a separate YAML parser was unavailable. Signing remains outstanding. Publication requires source review, remaining manual checks and explicit authorization for a new matching-tag release. Existing releases and tags were untouched.
