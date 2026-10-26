# Advanced control and safety features

Implemented in the existing WPF application on 26 October 2026. This document
records the implementation and its original local verification. The subsequent
v1.4.0 release preparation is described in [release notes](RELEASE-v1.4.0.md);
the tagged GitHub workflow supplies the final publication evidence.

## Using the features

Expand **Global shortcuts & advanced controls → Advanced session controls** for
session limits, application-profile associations and the private safety timeline.
The navigation footer has compact-panel and command-palette buttons; **Ctrl+K**
opens the palette from the dashboard. Existing theme resources are reused.

### Session auto-stop

- Optional duration: 1–86400 whole seconds. Optional submitted input pairs:
  1–1,000,000,000. Either limit ends the request.
- Check the desired options, enter values and choose **Apply limits · stops
  input**. Unapplied changes are marked. Invalid values stop input and retain
  the last applied limits. Disabled fields are ignored.
- Duration is monotonic wall-clock session time, including manual pauses and
  target waits. Submitted pairs are successful input-service pairs, not target
  acceptance. Failed/partial submissions are not counted as complete pairs.
- The dashboard shows applied limits and remaining time/pairs for the last or
  current explicitly started session. Changing settings freezes progress; it
  does not reset it. Newly applied limits are captured on the next explicit start.
- Count limits are checked immediately after a successful pair. Duration is
  checked on worker wakes and again before dispatch, including after target
  validation. Paused/waiting requests also expire. There is no UI-timer dependency.
- Stop uses the existing release barrier. A synchronous input call already in
  progress cannot be preempted; Windows scheduling and that call can delay a
  duration stop. This is not a hard real-time guarantee.
- Limits persist in settings, default to disabled in older settings, and remain
  application preferences when timing profiles are applied.

### Per-application profiles

Select an eligible window through the existing Target selector. Apply a saved
profile, then choose **Associate current saved profile**. On a later selection,
the application suggests a matching saved profile; **Apply suggested profile**
is a separate explicit action that stops input. Selection and matching never
restore a target or start a request. Applying requires a currently valid selected
process/window; actual activation/input still uses the original target gates.

Only the SHA-256 fingerprint of the executable's normalized full path and a
profile ID are stored, in `%LOCALAPPDATA%/ClickForge/application-profiles.json`.
The fingerprint is for matching, not trust verification or an anonymity promise.
The executable path text, process ID, HWND, window title, app content and profile
name are not stored in that file. Moving an executable requires a new association.
The original process/signature/window checks remain authoritative.

**Remove association** removes the selected application's mapping. **Clear all
application associations…** also handles applications that are no longer open;
it stops synthetic input before confirmation. Saved profiles are retained.
Associations are separate from portable version-1 timing-profile files, whose
strict import/export schema is unchanged. Stale/missing profiles are not applied.
Files are limited to 32 KiB/100 associations; malformed files yield no suggestion
and must be explicitly cleared before replacement.

### Safe-test timing scope

Choose **Variable Timing — Safe Test Only** in Safe Click Test. The existing
5–20 CPS limits and optional integer seed configure the isolated logical simulator.
Gold plots scheduled intervals; green plots measured intervals, with the last
100 samples displayed. Summary statistics cover all samples in the bounded run:
scheduled/measured means, measured min/max, population standard deviation, and
mean/max positive scheduling delay. Expand **Logical event samples** for events.

Configured rates and measured average CPS are labeled separately. A seed repeats
the requested interval sequence, not Windows scheduling delays or the number of
events that fit into a time-bounded run. The existing simulator samples uniformly
in interval space; its average throughput need not be the midpoint of CPS bounds.
The scope has no input service or production-engine callback. Stop/close and the
main emergency shortcut cancel the simulator. Stop remains visible outside the
scrolling content. The existing controlled Fixed CPS input pad remains available.
No target acceptance, human mimicry or anti-cheat evasion is claimed.

### Compact controls

The small movable window shows actual engine state, the active profile, elapsed
time, submitted session pairs, measured average CPS, limits and input errors.
Sub-second measurements are marked as short samples. **Keep on top** is opt-in.
Start/Stop, Pause/Resume and Emergency Stop use the dashboard's validated commands
and the same engine. Hold mode still uses its shortcut; configuration/test views
block new activation. Genuine target waiting and input errors are displayed.

Opening, showing again, or closing the panel stops the existing request so a
focus change cannot indirectly resume it. Activate explicitly afterward. Main
window tray preferences remain in force; hiding the dashboard stops input, and
restoring never activates it. The compact panel can remain available while the
dashboard is hidden. Explicit application exit closes both new windows and
performs the existing cleanup. Emergency Stop is outside the scrolling details.

### Command palette

Search commands to open dashboard, profiles, target, settings, shortcuts, updates,
Safe Click Test, limits/history or compact controls. Saved profile commands are
listed by name and stop input when explicitly executed. Profiles are revalidated
and reloaded before application. Search results are cached for the open palette,
so typing does not repeatedly read all profile files.

Type **cps 25**, for example, to offer a validated 1–50 Fixed CPS change. Execute
it to stop input and ask for confirmation; declining keeps the configuration and
leaves input stopped. Changing to Fixed retains its existing restriction
preference. Invalid syntax/rates offer no executable speed command.

Opening, searching and highlighting execute no result. Enter or **Execute
selected action** is required; Escape closes. While open, the palette blocks new
activation through the shared command paths. Existing active requests still
follow the original engine/target checks; emergency stop is always routed.

### Private safety timeline

The existing worker records actual starts, pauses, resumes, stops, hold loss,
target eligibility transitions, automatic limit stops, input/validation errors,
configuration stops and shutdown. Each entry contains only event type and time.
The last 100 events are retained in memory for this run; no per-pair event or
location/application content is collected. **Clear safety timeline** clears it.
It is not included in the existing optional activity export or persisted to disk.
The timeline view updates only when its version changes while expanded.

## Verification actually executed

Final full command:

`./build.ps1 -SkipPackage -BuildDirectory artifacts/advanced-release-final`

- Release application and updater builds: zero warnings/errors.
- Complete core suite: 159 passed, 0 failed, including 23 new advanced-control
  cases. Covers bounds, exact pair caps in both modes, deadlines through pauses
  and target waits, a slow validation crossing the deadline, session reset,
  duplicate starts, in-flight release, auto-stop release failure, shutdown,
  history privacy/bounds/clearing, settings migration/persistence, fingerprint
  matching/removal/corruption/capacity, scope calculations/seeds/isolation, compact
  availability and palette parsing.
- Complete updater suite: 36 passed, 0 failed.
- Actual WPF suite: 210 checks passed with mock production input/targets,
  including 38 additional checks. Covers actual limit controls/persistence,
  automatic stop, compact shared commands and genuine waiting/error states,
  lifecycle stop behavior, palette nonexecution/confirmation/application,
  profile-suggestion privacy/target blocking, history clearing, isolated scope
  rendering/cancellation and shared shutdown. Existing native shortcut/hook,
  tray, disposable registry, layout and controlled-pad regressions ran as well.
- Images from the real diagnostic application were inspected for compact
  controls, command palette, timing scope, dashboard and expanded advanced forms.
  Existing default-size/narrow/DPI-content-scale layout checks passed.
- Regressions caught and fixed: default-window overflow from an extra row;
  palette CPS settings not reaching the engine; a test's obsolete layout cast
  after fixing the test window's stop-button placement. Final assertions still
  exercise the same actual pad and down/up behavior.
- Final diff reviewed for unsafe target changes, inaccurate status, scheduler
  changes, privacy, release/shutdown, and UI update work. `git diff --check` passed.

Logs are under `artifacts/logs/advanced-release-final-build.txt` and
`advanced-release-final-ui.txt`. UI captures include `ui-compact-controls.png`,
`ui-command-palette.png`, `ui-timing-scope.png`, and `ui-advanced-features.png`.

## Packaging, release and remaining limits

SDK default source inclusion builds these components into the existing app and
core assembly. The self-contained app/helper packaging and tagged release
workflow were inspected; no new dependencies, payload files or click loops were
added. The updater only manages its explicit payload list, leaving settings,
profiles and the separate local association file outside replacement.

At the original local-verification stage, no installer package, new tag or public
release had been created for these features. Installer lifecycle and public-feed
checks had not been rerun. The subsequent v1.4.0 tagged workflow independently
builds/verifies its exact package, checks helper success/rollback, runs the real
installer lifecycle, and verifies uploaded assets before publication.

Local delivery: the 13 application/helper files from the verified
`artifacts/advanced-release-final` build were copied into `artifacts/ui-build`,
with SHA-256 equality checked for every file. The previous files are preserved
under `artifacts/backups/before-advanced-controls-20261026-155420`. The actual
local `artifacts/ui-build/ClickForge.exe` was reopened successfully with normal
Windows access. This file replacement did not overwrite settings or profiles;
it does not update other portable copies or the public release.

Production input was mocked throughout this task. Native hooks/registration and
routed controlled-pad events do not prove physical key/mouse delivery or target
acceptance. Positive association matching used a synthetic app fingerprint and
saved profiles; actual selection of a separate live application and its positive
suggested-profile application were not exercised. Confirmation outcomes were
supplied by UI-test delegates; physical message-box interaction, screen readers,
real multi-monitor DPI changes and live Roblox input were not tested.

The pre-existing high/variable CPU after screenshot-heavy UI diagnostics remains
uncharacterized; this work does not claim it is fixed or establish a major RAM
reduction. Short measured-rate samples can be unstable, as explicitly labeled in
the compact panel. Scheduling delays and target input acceptance remain separate.

## Exact files changed

- `src/ClickForge.Core/ClickEngine.cs`
- `src/ClickForge.Core/Settings.cs`
- `src/ClickForge.Core/SessionControls.cs` (new)
- `src/ClickForge.Core/ApplicationProfiles.cs` (new)
- `src/ClickForge.Core/TimingScopeSummary.cs` (new)
- `src/ClickForge/DesktopTargetService.cs`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/MainWindow.Daily.cs`
- `src/ClickForge/MainWindow.VariableCps.cs`
- `src/ClickForge/MainWindow.Advanced.cs` (new)
- `src/ClickForge/CompactControlWindow.cs` (new)
- `src/ClickForge/CommandPaletteWindow.cs` (new)
- `src/ClickForge/TestWindow.cs`
- `src/ClickForge/TimingScope.cs` (new)
- `src/ClickForge/UiDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.CustomKeys.cs`
- `src/ClickForge/UiDiagnostics.Advanced.cs` (new)
- `tests/ClickForge.Tests/Program.cs`
- `tests/ClickForge.Tests/AdvancedControlTests.cs` (new)
- `README.md`
- `docs/ADVANCED-CONTROLS.md` (new)

Release preparation additionally changes `Version.props`,
`scripts/Package-Release.ps1` and adds `docs/RELEASE-v1.4.0.md`.
