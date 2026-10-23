# ClickForge implementation and verification — 23 October 2026

## Variable CPS

Implemented directly in the existing WPF application and single click worker. Fixed CPS retains its scheduling path and immediate first event. Variable CPS samples a uniform rate, then schedules `1000 / sampledCps` milliseconds. Limits are 1–50 CPS, initially 5–20; equal endpoints produce constant intervals. Configuration changes stop the current request and require explicit activation. The settings file stores the timing mode and valid limits, never activation or a window/process identifier.

The dark interface has reusable segmented selectors, numeric fields and synchronized sliders, configured range/interval labels, inline validation, and an explicit desktop-window picker. The fixed Roblox-only preference is preserved. Variable mode always requires its separate desktop gate: readable non-Roblox metadata, the retained original process, the exact selected foreground window, and the pointer inside that window. Unknown identities and Roblox metadata/titles are excluded. Focus loss pauses input; changing selection stops it. The original logical Safe Click Test remains isolated from live input.

Start, pause/resume, duplicate activation, Hold, emergency stop, input failures and shutdown retain the existing lock, worker and release path. No process injection, game-memory access, driver or human-behavior model was added.

## Updater and release packaging

The pending updater implementation is included: explicit public GitHub channel, semantic versions, cancellable checks/downloads, SHA-256 and strict archive verification, independent Windows helper, clean shutdown coordination, persistent backups/journal, startup acknowledgement and rollback. The native dark Updates window presents real operation states. Startup checks are optional and never install automatically.

The first published updater version is prepared as 1.1.1 in the central Version.props file. The existing GitHub workflow builds/tests branch pushes and publishes only matching new version tags from the official repository. The release ZIP includes the application, independent helper, manifest, README and bundled-runtime notices. Both executables include the Core project containing the new timing code; there is no additional timing asset or dependency to omit. See [UPDATING.md](UPDATING.md) for the exact release process and recovery instructions.

## Tests actually executed

| Command/check | Result |
| --- | --- |
| `./build.ps1 -SkipPackage` | Release application and helper builds: zero warnings, zero errors. Core: **55 passed, 0 failed**. Updater: **34 passed, 0 failed**. WPF diagnostics completed successfully. |
| Core suite coverage | Fixed-rate regressions; variable validation, equal limits, deterministic seeded sampling, reciprocal interval bounds and statistical means; cancellation, in-flight cycles, mode/range changes, duplicate starts, target invalidation, Roblox metadata exclusion, input/release failures, settings and shutdown. All engine input is mocked. |
| WPF diagnostics, rerun after final UI/target-service changes | **59 checks passed**. Includes actual control actions, persistence, routed shortcuts, invalid ranges, mode switching, fail-closed target selection, emergency stop, safe simulator, update states, default layout, resizing, dark popup and focus rendering. |
| `./scripts/Test-UpdateProcess.ps1 -PayloadDirectory artifacts/updater/final/payload` | **2 real helper-process scenarios passed**: wait/replace/restart/acknowledge and failed-startup rollback/restart. Both preserved settings in disposable installations. |
| `./scripts/Package-Release.ps1 -OutputDirectory artifacts/delivery/payload` | Self-contained Windows x64 publish succeeded. Strict payload contents, manifest hashes and both executable versions validated by the package verifier. |
| Final self-contained `ClickForge.exe --ui-self-test ...` | **59 checks passed** again from the packaged executable. Its helper was confirmed byte-identical to the helper used in both real process tests. |

Local package: `artifacts/delivery/ClickForge-v1.1.0-win-x64.zip` and adjacent `.sha256`.

SHA-256: `97966b9abcd4e7adec18119e4903519ad5a55ee2bc079960473e9e2f665312d6`.

Build/unit results: `artifacts/logs/final-build-tests.txt`. WPF results/screenshots: `artifacts/logs/`. Real-helper results: `artifacts/updater/logs/process-tests.txt`. Published-application diagnostics: `artifacts/delivery/diagnostics/`.

## Limits of verification

- Windows denied native shortcut reservation during diagnostics (Win32 error 5). Routed shortcut behavior was exercised; physical global-key delivery is not claimed as passed.
- No automated clicks were sent to Roblox or other live applications. Input acceptance by a real desktop program remains a manual check. Native target classification uses metadata/title checks, not attestation or browser-content inspection. Foreground checks and OS input delivery can race an instantaneous focus change.
- Layout was rendered at the default 880×900 size and a smaller 640×700 size at 96 DPI. Higher DPI scales were not independently exercised. The GitHub runner has a 1024�720 work area and Windows caps the requested window to 788 DIP; diagnostics record this limitation and verify intentional scrolling there while retaining the full-size no-scroll assertion on sufficiently large desktops. The expanded variable controls can require vertical scrolling, especially with validation messages; the default fixed layout fits without scrolling.
- Real helper tests used disposable console fixtures, not an update over a user's installation. A remote upgrade between two published updater-enabled versions was not executed here. GitHub Actions has exercised builds and tests; the initial v1.1.0 tag was blocked by an overly strict display-size assertion and was not published. That tag is retained unchanged; release preparation continues as v1.1.1.
- The development ZIP/hash above predates the release-preparation commits and is retained only as local verification evidence. Publication uses the clean, matching `v1.1.1` tagged commit through GitHub Actions; its official assets have their own manifest and checksum. Consult the repository release page and workflow result for publication status. Existing v1.0.0 installations need one manual updater bootstrap download.
- The executables are unsigned. Checksums validate integrity relative to the release account; they do not establish publisher identity.

## Exact files changed

Timing/target/UI integration and regression tests:

- `src/ClickForge.Core/ClickTiming.cs` (new)
- `src/ClickForge.Core/ClickEngine.cs`
- `src/ClickForge.Core/Settings.cs`
- `src/ClickForge/DesktopTargetService.cs` (new)
- `src/ClickForge/MainWindow.VariableCps.cs` (new)
- `src/ClickForge/MainWindow.xaml`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/Native.cs`
- `src/ClickForge/Themes/Controls.xaml`
- `src/ClickForge/UiDiagnostics.cs`
- `tests/ClickForge.Tests/Program.cs`

Updater, versioning, packaging, workflow and documentation:

- `Version.props` (new)
- `Directory.Build.props`
- `src/ClickForge/ClickForge.csproj`
- `src/ClickForge.Core/Updates/SemanticVersion.cs` (new)
- `src/ClickForge.Core/Updates/UpdateCache.cs` (new)
- `src/ClickForge.Core/Updates/UpdateChannel.cs` (new)
- `src/ClickForge.Core/Updates/UpdateClient.cs` (new)
- `src/ClickForge.Core/Updates/UpdateInstaller.cs` (new)
- `src/ClickForge.Core/Updates/UpdatePackage.cs` (new)
- `src/ClickForge.Core/Updates/UpdateStartup.cs` (new)
- `src/ClickForge.Updater/ClickForge.Updater.csproj` (new)
- `src/ClickForge.Updater/Program.cs` (new)
- `src/ClickForge/MainWindow.Updates.cs` (new)
- `src/ClickForge/UpdateWindow.cs` (new)
- `tests/ClickForge.UpdateTests/ClickForge.UpdateTests.csproj` (new)
- `tests/ClickForge.UpdateTests/Program.cs` (new)
- `tests/ClickForge.Updater.Fixture/ClickForge.Updater.Fixture.csproj` (new)
- `tests/ClickForge.Updater.Fixture/Program.cs` (new)
- `scripts/Package-Release.ps1` (new)
- `scripts/Test-UpdateProcess.ps1` (new)
- `.github/workflows/build.yml`
- `build.ps1`
- `README.md`
- `docs/UPDATING.md` (new)
- `docs/IMPLEMENTATION-REPORT.md` (new)

Generated packages, diagnostics, fixtures, runtime caches and build products are under ignored `artifacts/`, `bin/` and `obj/` directories.
