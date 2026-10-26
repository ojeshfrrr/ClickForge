# Lightweight dashboard update — local verification, 26 October 2026

This records the optimization's local verification before release preparation.
For v1.3.2 publication and download details, see [release notes](RELEASE-v1.3.2.md)
and the tagged GitHub workflow results.

The optimization reduces dashboard work without changing the click worker,
input mapping, target gates, cancellation, hotkey routing, or emergency stop.

## Changes

- Visible idle dashboard: refresh every 1 second instead of 250 ms.
- Visible active dashboard: refresh every 500 ms instead of 250 ms.
- Commands, restoration, and configuration-window closure refresh immediately.
- Hidden/minimized windows skip dashboard rendering and keep only the existing
  tray/error-state bookkeeping on the 1-second timer. Global commands and the
  input worker continue independently. Hiding to tray still stops input.
- Target and session displays cache their rendered values. Target facts are
  still read on each visible refresh; changed facts, restrictions, timing mode,
  range, or validation feedback invalidate the inspector cache.
- Removed redundant, conflicting target/state text assignments and an extra
  UI-only `CanClick` call. Production target validation is unchanged.
- Added an explicit `--performance-test <absolute-output-path>` diagnostic with
  mock input and disposable settings. It never activates native input, registers
  global shortcuts, takes screenshots, or subscribes to the rendering event.

## Executed verification

Final command: `./build.ps1 -SkipPackage -BuildDirectory artifacts/lightweight-final`

- Application and updater: Release builds passed, 0 warnings and 0 errors.
- Core tests: 136 passed, 0 failed.
- Updater tests: 36 passed, 0 failed.
- Actual WPF UI diagnostics: 172 checks passed, including 12 new optimization
  checks for idle text stability, cache invalidation, hidden/minimized cadence,
  immediate restoration, configuration blockers, and emergency stop while hidden.
- Tests use mock output. Native shortcut registration, mouse-hook lifecycle,
  registry test entries, and tray lifecycle were exercised successfully with
  normal Windows access. Physical hotkey delivery and target input acceptance
  were not tested. No automated input was sent into Roblox.
- An initial restricted UI run failed at its existing test-only registry write;
  the subsequent normal-access run and final complete run passed.
- Final source diff reviewed for target-validation changes, stale status text,
  timing interference, shutdown, and offscreen UI work; `git diff --check` passed.

Logs: `artifacts/logs/lightweight-build.txt` and `artifacts/logs/ui-tests.txt`.

## Performance observations

Separate fresh processes were used for the baseline and optimized application,
at 1280×920 DIP, Windows DPI 150%, rendering tier 2, on this PC. Each phase lasts
approximately 4 seconds and uses mock input. The first optimized probe preceded
the final removal of duplicate state text; a second probe used the final build.
There are two baseline samples, one initial optimized sample, and one final
optimized sample. These short observations are not a general benchmark.

| Observation per ~4-second phase | Baseline, two samples | Optimized, initial / final |
| --- | --- | --- |
| Idle managed allocation | 38.5 / 39.7 KiB | 16.0 / 15.9 KiB |
| Active 10 CPS layout passes | 17 / 17 | 8 / 8 |
| Active 50 CPS layout passes | 16 / 16 | 8 / 8 |
| Hidden layout passes | 1 / 1 | 0 / 0 |
| Hidden text mutations | 14 / 14 | 0 / 0 |
| Idle CPU, percent of one core | 2.34 / 3.11% | 0.78 / 0.39% |
| Hidden CPU, percent of one core | 1.56 / 4.28% | 0.00 / 4.68% |

The direct UI-work counts and idle allocation show the intended reduction.
CPU and memory vary with runtime warmup and background work: hidden CPU did not
consistently improve, so no zero-CPU claim is made. Across the standalone phases,
working set remained approximately 118–144 MiB. This does not establish a
substantial RAM reduction. First active samples also include runtime warmup.
The final 10-CPS phase submitted 40 mock pairs in 4.014 seconds; the 50-CPS phase
submitted 201 in 4.007 seconds. These are scheduling observations, not target
acceptance measurements or exact rate guarantees.

Performance logs: `artifacts/logs/lightweight-baseline.txt`,
`lightweight-baseline-repeat.txt`, `lightweight-after.txt`, and
`lightweight-final-performance.txt` in the same folder.

## Limits and delivery

The screenshot/resize-heavy full UI diagnostic still reported higher CPU after
its workload (47–67% of one core in the final 2-second samples). Its root cause
was not established or fixed by this change. It is not equivalent to a fresh
idle process and is not used as the performance baseline.

The self-contained WPF runtime remains the main distribution-size cost. No new
packages, rendering framework, forced garbage collection, or working-set trimming
were added. Packaging and updater configuration were inspected: SDK default
source inclusion builds these files into the existing application and the
existing single-file package. Installer creation/lifecycle and a new public
GitHub release had not been run at this local-verification stage (version 1.3.1).
The subsequent v1.3.2 release preparation changes `Version.props`, `README.md`
and `docs/RELEASE-v1.3.2.md`; the tagged workflow performs its publication checks.

The verified build replaced the 13 application/helper files in
`artifacts/ui-build`, with SHA-256 equality checked against
`artifacts/lightweight-final`. The previous files were preserved in
`artifacts/backups/before-lightweight-20261026-130313`. The local application was
reopened successfully from `artifacts/ui-build/ClickForge.exe`; this does not
update unrelated portable copies or the existing public release.

## Exact source files changed

- `src/ClickForge/App.xaml.cs`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/MainWindow.Daily.cs`
- `src/ClickForge/MainWindow.Shell.cs`
- `src/ClickForge/MainWindow.VariableCps.cs`
- `src/ClickForge/UiDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.Lightweight.cs` (new)
- `src/ClickForge/PerformanceDiagnostics.cs` (new)
- `docs/LIGHTWEIGHT.md` (new)
