# Selectable mouse buttons

> Historical implementation-stage verification record. For the published feature summary, see [v1.3.0 release notes](RELEASE-v1.3.0.md).

ClickForge supports **Left**, **Mouse 4 (Back)** and **Mouse 5 (Forward)** in both Fixed and Variable CPS. The new themed selector is beside Click speed, with accessible labeling and a wrapping layout at narrow widths. Left remains the default. A changed choice synchronously stops the current request, releases any pending button, persists the choice and requires explicit activation. Profiles capture, apply, match, import and export the choice; application still stops input and never changes target restrictions.

The production engine remains a single worker. The selected button is passed into that worker's existing input call under its existing lock; no alternate scheduler or click loop was added. Unsupported input is rejected before activation when known, and rejected/partial submissions stop the engine without a fallback button. Legacy left-only input adapters explicitly refuse side-button calls even if they incorrectly advertise support.

The Windows adapter uses the documented [MOUSEINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput) contract: Left uses LEFTDOWN/LEFTUP (0x2/0x4), Mouse 4 uses XDOWN/XUP (0x80/0x100) with mouseData XBUTTON1=1, and Mouse 5 uses those flags with XBUTTON2=2. Both events are submitted as one pair through SendInput; only a return value of two counts as a submitted input pair. No application acceptance is inferred. Physical gaming-mouse hardware is not needed to construct these synthetic input events.

The input service retains the original button for any potentially incomplete pair. Stop, pause, target loss, timing/button changes, input failures and disposal continue through the shared release path. A failed release retains its button identity for retry, blocks a different button down until cleanup succeeds and shows the specific physical button to release if Windows continues rejecting cleanup. Windows/input hooks can reject synthetic events; acceptance and successful OS cleanup cannot be guaranteed when Windows denies them. There is no injection or anti-cheat bypass.

## Settings and profiles

`Button` is stored as 0=Left, 1=Mouse 4, 2=Mouse 5. Missing fields in existing settings and version-1 profiles default to Left. Unknown numeric values, nulls, strings and other malformed values are rejected. Invalid settings restore conservative defaults with a warning; invalid profiles are skipped/rejected before they can mutate the engine. Version-1 profiles gain an optional Button field. Older ClickForge versions that strictly reject unknown profile fields cannot import newly exported button profiles; no backward-reader compatibility is claimed.

## Safe test

The existing controlled Fixed CPS test now offers the same three choices. It displays separate down/up counts and a bounded on-screen event list for the selected button. The pad handles side-button events without browser Back/Forward navigation. The engine checks foreground, actual pointer position, the current pad rectangle and the window under the pointer before submitting input. The main emergency shortcut, leaving the window, Stop test and closing still stop the controlled engine. A new test session resets the sent-pair display using the engine session counter. The logical Variable Timing simulation remains input-free and disables the button selector.

An explicit `--native-button-self-test <output-path>` diagnostic opens only this isolated test window, uses no personal settings/profile files and never creates the main production engine. It attempts one pair per button only when the actual pad is foreground and under the pointer, verifies one matching down/up event and restores pointer/focus when appropriate. If Windows denies foreground, it records UNAVAILABLE and sends no input. Standard `--ui-self-test` continues to use mock production input.

## Files changed for this feature

- `src/ClickForge.Core/ClickButtons.cs`: validated button identifiers/labels, Win32 packet mapping, injectable transport and pending-button cleanup.
- `src/ClickForge.Core/ClickEngine.cs`: backward-compatible left-only adapter defaults, capability check, button selection stop barrier and selected-button worker routing.
- `src/ClickForge.Core/Settings.cs`: persisted, validated button choice.
- `src/ClickForge.Core/Profiles.cs`: optional profile field, capture/apply/match and summary.
- `src/ClickForge/Native.cs`: cached Windows input packets and native diagnostic focus/pointer declarations.
- `src/ClickForge/MainWindow.xaml`: themed, responsive button selector.
- `src/ClickForge/MainWindow.xaml.cs`: selector population and safe-test choice propagation.
- `src/ClickForge/MainWindow.VariableCps.cs`: stopped configuration/load handling.
- `src/ClickForge/TestWindow.cs`: side-button down/up reception, selection and live pointer checks.
- `src/ClickForge/NativeButtonDiagnostics.cs`: bounded isolated native test entry point.
- `src/ClickForge/App.xaml.cs`: explicit isolated diagnostic launch branch.
- `src/ClickForge/UiDiagnostics.cs`: actual-control checks and button-aware mock input.
- `tests/ClickForge.Tests/ButtonTests.cs`: mapping, persistence, legacy migration, invalid data, unsupported input, partial failures and cancellation cleanup tests.
- `tests/ClickForge.Tests/Program.cs`: register button tests.
- `README.md` and this report: usage, behavior and verification.

The separate session-statistics/target-status files already modified earlier in this workspace remain intact. No version/tag, release publication or GitHub push is part of this feature task.

## Verification

Executed on Windows, 2026-10-25:

- `./build.ps1 -SkipPackage`: Release app/helper builds passed with zero warnings/errors, **113 core tests, 36 updater tests and 138 actual WPF checks**. The 15 new button tests include real worker cancellation with a blocking mock transport. Production input is mocked in standard UI diagnostics.
- The default 1280×920 layout had zero dashboard overflow at actual Windows DPI 150%. Narrow layout, selector/profile synchronization, persisted settings, open/closed existing themed popup, focus traversal, pause/resume, routed emergency and shutdown regressions passed. The default screenshot (`artifacts/logs/ui-default.png`) was visually inspected.
- Actual native safe-pad delivery was attempted twice, first hidden and then as the application window. Windows did not grant foreground; the guard sent no input. **Native Mouse 4/5 delivery and down/up reception have not passed a runtime test in this environment.** The documented API contract and exact packet mapping tests do not substitute for this unperformed delivery check. Logs: `artifacts/logs/buttons-native.txt`, `buttons-native-visible.txt`. The first attempt logged a failure; the diagnostic now distinguishes foreground unavailability with exit code 2.
- Final suite log: `artifacts/logs/buttons-build-final.txt`. No suite assertion failed during feature verification. Unsupported/partial input failures in unit tests are intentional mocked cases.

Physical hotkey delivery, gaming-mouse driver remapping, live Roblox acceptance, screen-reader behavior and real multi-DPI transitions remain untested. Existing software-rendered UI CPU measurements remain variable and are not a hardware performance benchmark. Native target validation and input submission remain separate Windows calls; this change does not claim to eliminate their existing race window.

Packaging and installer verification:

- `./scripts/Package-Release.ps1 -OutputDirectory artifacts/buttons-release/payload`: fresh self-contained Release ZIP and installer built successfully; archive, manifest and existing seven-entry update contract validated. New classes are compiled into the existing payload; no dependency or package-contract change was needed. This is a working-tree validation build whose source-commit field identifies the base commit, not a tagged release. Log: `artifacts/logs/buttons-package.txt`.
- `./scripts/Test-UpdateProcess.ps1 -PayloadDirectory .../buttons-release/payload -InstallRoot .../buttons-updater-tests`: actual helper success/restart/confirmation and failed-start rollback passed using disposable fixture processes; settings and profiles preserved. Log: `artifacts/logs/buttons-updater-process.txt`.
- `./scripts/Test-Installer.ps1 -ZipPath .../buttons-release/ClickForge-v1.2.0-win-x64.zip`: **26 installer lifecycle checks passed**, covering historical v1.1.2 upgrade, verified payload hashes, shortcuts/opt-outs, running-app guards, coordinated shutdown, uninstall and byte-identical existing personal settings/profiles. The actual installed executable also passed **138 WPF checks**, with input mocked. Log: `artifacts/logs/buttons-installer.txt`; lifecycle report: `artifacts/installer-tests/5aad0d8c93414a53935adf3b69f0ad43/results.txt`.

## Final regression review

The review checked input flags/data, stable release identity after errors and button changes, synchronous stop barriers, settings/profile validation before mutation, profile/control synchronization and safe-test isolation. It found a potential adapter fallback in the first implementation: a legacy adapter that advertised side-button capability but only implemented the old parameterless Click could otherwise send Left. The default side-button method now always refuses that unsupported path; a dedicated regression test verifies zero input pairs and zero legacy Left calls.

Production `DesktopTargetService.SelectionValid`, `CanActivate` and `CanClick` were compared with the committed versions and match exactly. Roblox signature/publisher verification is unchanged. Click interval calculations and the existing worker/cancellation model are unchanged. The Safe Click Test gate is stricter: it verifies the actual cursor/pad rectangle and root window, rather than relying only on cached mouse-enter state.

The standard UI suite still uses mock input. It passed selector labels/defaults, actual settings persistence, stopped configuration changes, profile application and Safe Test side-button selection. It does not establish native side-button delivery; the separate guarded native attempt remains unavailable as recorded above. No unsupported input was silently converted to a different button in executed tests.
