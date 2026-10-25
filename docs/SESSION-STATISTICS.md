# Session statistics and target explanations

> Historical implementation-stage verification record. For the published feature summary, see [v1.3.0 release notes](RELEASE-v1.3.0.md).

The compact dashboard statistics show wall-clock elapsed time, session input pairs sent and measured average CPS. Average CPS is successful input-pair submissions divided by monotonic elapsed seconds; it is separate from configured CPS and says nothing about target acceptance. Elapsed time includes manual pauses and target waits, explicitly labeled in the UI. At zero duration the average is zero.

A successful new activation resets the session. Duplicate Start requests and refused activations do not reset it. Stop, emergency stop, Hold release, changed timing configuration, input failure and disposal freeze the session. Pause/resume keeps the same session and includes the paused time. The run input-pair counter remains separate. No session information is persisted and no per-click logging, UI dispatch or content collection was added. The existing 250 ms visible / 1 s hidden UI refresh reads a snapshot under the engine's existing lock.

The selected-target inspector explains missing selection, rejected selection, closed process/window, changed identity, hidden/minimized window, unverified Roblox identity, foreground mismatch, unavailable pointer, pointer outside and unavailable inspection. Process, window, foreground and pointer rows show actual observations; unavailable values do not appear green. The dashboard also explains the effective Fixed CPS foreground policy, including restriction off, verification in progress and verified foreground. Selected-window details apply to Variable CPS; the policy paragraph identifies the current mode.

Existing target gates and Roblox signature/publisher verification remain unchanged. Verified Roblox clients retain the repository's existing support. No anti-cheat evasion, injection, memory access or alternate click worker was introduced. Settings, targets and profiles never automatically activate input.

## Files changed

- `src/ClickForge.Core/SessionStatistics.cs`: monotonic in-memory tracker and immutable snapshot.
- `src/ClickForge.Core/ClickEngine.cs`: start/stop lifecycle integration, existing lock and count reuse.
- `src/ClickForge.Core/TargetStatus.cs`: content-free validation facts and explanation mapping.
- `src/ClickForge/DesktopTargetService.cs`: native inspection facts and rejected-selection status.
- `src/ClickForge/ForegroundTargetService.cs`: effective Fixed CPS policy status.
- `src/ClickForge/Native.cs`: read-only IsWindow declaration.
- `src/ClickForge/MainWindow.Shell.cs`: statistics and actual target status rendering.
- `src/ClickForge/MainWindow.xaml`: compact statistics, process state and explanation fields using existing styles.
- `src/ClickForge/UiDiagnostics.cs`: actual-control checks with mock mouse input.
- `tests/ClickForge.Tests/SessionTargetTests.cs`: 24 statistics/target tests with mock clocks, input and target gates, including stop-barrier concurrency coverage.
- `tests/ClickForge.Tests/Program.cs`: register added tests.
- `README.md` and this report: behavior and verification documentation.

## Packaging and verification

The existing SDK projects compile the new classes into the application; no extra runtime files or dependencies are required. The existing portable ZIP and installer use the same verified seven-entry update contract. The tagged-release workflow builds the current source, runs the suites, verifies helper recovery and installer lifecycle, and publishes only matching new version tags. No version bump or release publication is part of this change.

Executed verification on Windows, 2026-10-25:

- `./build.ps1 -SkipPackage`: Release application and helper builds passed with zero warnings/errors; 95 core tests, 36 updater tests and 128 actual WPF diagnostic checks passed. The 21 new core tests use deterministic clocks and mock input/target services.
- Actual WPF window: 1280×920, Windows DPI 150%, zero dashboard overflow; narrow layout, open/closed popup, focus traversal, disabled state, profile/settings persistence, routed start/pause/resume/emergency and shutdown checks passed. Screenshots are under `artifacts/logs/ui-*.png`; the default screenshot was visually inspected.
- `./scripts/Package-Release.ps1 -OutputDirectory artifacts/session-final/payload`: fresh self-contained Release ZIP and installer built; archive/manifest hashes and the existing updater contract verified. This is a local working-tree validation build, not a tagged or published release; its source-commit field identifies the base commit.
- `./scripts/Test-UpdateProcess.ps1 -PayloadDirectory .../session-final/payload -InstallRoot .../session-updater-tests`: real helper success and failed-start rollback scenarios passed with disposable fixture processes; settings and profiles preserved.
- `./scripts/Test-Installer.ps1 -ZipPath .../session-final/ClickForge-v1.2.0-win-x64.zip`: all 26 real installer lifecycle checks passed, including historical v1.1.2 upgrade, verified payload hashes, Desktop/Start integration, opt-outs, running-application guards, coordinated shutdown, uninstall and byte-identical personal settings/profiles. The actual installed executable also passed all 128 WPF diagnostic checks. Report: `artifacts/installer-tests/cb26859fd2c04a7aa118ef994f93a770/results.txt`.

The first UI run exposed an assertion that compared different instants on an advancing clock; it now checks the rendered value between bracketing readings with display-rounding tolerance. A subsequent run exposed 1.55 DIP of default-layout overflow; reduced status-panel padding fixed it, and the unchanged zero-overflow assertion passed on the final build.

Input was mocked in diagnostics and all new engine tests. No live Roblox clicks, physical global-key delivery, screen-reader session, or real Windows DPI changes were tested. Native F9/F10/F11 registration/cleanup passed; routed shortcut checks do not establish physical key delivery. Content-scale tests at 125/150/200% do not substitute for actual multi-DPI testing. The software-rendered diagnostic environment measured roughly 85% of one CPU core idle and 80% with mock 10 CPS during two-second samples while packaging was running; these are not representative hardware performance benchmarks. No performance improvement is claimed.

Logs: `artifacts/logs/session-build.txt`, `session-package-final.txt`, `session-updater-process.txt`, `session-installer.txt`.

## Final bug review

The review found and fixed two inspection issues introduced by the new display code:

- Reading a current title before checking process/window identity could inspect an unrelated process after HWND reuse. Current title reads now require an observed live retained process, existing window and matching PID; failed checks retain the original selection title. Regression tests supply a title reader that throws if called for closed, mismatched or unavailable identities, and verify it is never invoked in those cases.
- The focus row used overall eligibility as a substitute for focus, so another validation failure could inaccurately display "Not in focus". It now reports the observed foreground fact when window identity is established, and "Unavailable" when identity cannot be established. Overall eligibility still controls ready/verified styling.

Ready explanations now describe input eligibility without instructing an already-running session to activate again. Added tests confirm that unchanged Fixed CPS configuration preserves session statistics and that Stop waits for an in-flight mock pair, counts that completed pair and freezes the correct elapsed time/count.

Final executed verification: `./build.ps1 -SkipPackage` passed, with zero Release build warnings/errors, **98 core tests, 36 updater tests and 133 actual WPF checks**. Default layout again had zero overflow at 1280×920 / 150% Windows DPI. `git diff --check` passed. Final log: `artifacts/logs/bug-review-build-final.txt`. No failing assertions were observed in the review runs.

The final diff retains the original `SelectionValid`, `CanActivate`, `CanClick`, signature verification, interval calculation and input-submission/release paths. Session snapshots use the existing engine lock and monotonic clock, freeze at the existing stop barrier, reset only on a successful new request and include paused/waiting time. UI snapshots remain on the existing timer; no per-click dispatch or logging was added.

Existing or unresolved limits, separate from these fixes:

- Native foreground/identity checks, title reads and input submission are separate Windows calls, not one atomic transaction. Focus or window lifetime can change after an observation; snapshots are not a guarantee about the next instant. Existing click gates continue to recheck on the worker, and no claim is made that this race is eliminated.
- Software-rendered diagnostic CPU measurements remain high and variable (final two-second samples: about 69% of one core idle / 37% with mock 10 CPS). Hardware performance has not been established and no performance fix is claimed.
- Physical global-key delivery, live Roblox input acceptance, screen-reader behavior and actual multi-DPI transitions remain untested.
- The ZIP/installer/helper lifecycle results above were executed before these final inspection-only fixes. They have not been repackaged or rerun after the review; `artifacts/session-final` is therefore the earlier validation build, not the final reviewed source. Release app/helper builds and all three main suites were rerun after the fixes. No GitHub update was published.
