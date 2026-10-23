# Variable Timing — Safe Test Only

## Implemented

- Added a safe-test profile selector in the premium main interface and test window. Fixed CPS remains the default.
- Added validated 5–20 CPS minimum/maximum fields and optional 32-bit seed.
- Implemented a separate logical-event simulator with uniform bounded interval sampling, monotonic measured timing, bounded duration, prompt interruptible cancellation, and no catch-up bursts.
- Added configured-range labels, per-event requested/measured intervals, elapsed time, logical-event count, measured average CPS and measured mean interval.
- Variable timing never sends mouse input and never receives or calls the production click engine. The existing fixed-CPS scheduler and mouse-input implementation were not modified.
- Opening the test stops the live engine. Toggle, Hold and the Start action cannot activate it while the test is open. Emergency stop also cancels the isolated simulation and existing fixed test.
- Test parameters remain local to the window; no active state or test range is persisted into live preferences.

## Exact source changes

1. Added `src/ClickForge.Core/VariableTimingSimulation.cs`: validated range, seeded interval sequence, measurement calculation, logical-event snapshots and independent simulator lifecycle.
2. Updated `src/ClickForge/TestWindow.cs`: themed profile selector, range/seed validation, logical event/timing presentation and test cancellation. Existing fixed 10 CPS mouse target is created only on an explicit fixed-test start.
3. Updated `src/ClickForge/MainWindow.xaml`: safe-test profile selector reusing the existing dark ComboBox style; small spacing adjustment to preserve the default no-scroll layout.
4. Updated `src/ClickForge/MainWindow.xaml.cs`: passes selected test profile, blocks live activation while a test is visible, and routes emergency stop/suspend/closing to test cancellation.
5. Updated `src/ClickForge/UiDiagnostics.cs`: real-control validation, selected-profile opening, logical simulation, isolation and emergency-stop regressions with runtime-rendered screenshots.
6. Updated `tests/ClickForge.Tests/Program.cs`: 16 additional automated core tests.
7. Updated `README.md`: feature operation, sampling/measurement semantics and new artifact location.
8. Added this report, `VARIABLE-TIMING.md`.

## Verification

- Release compilation: succeeded; zero warnings, zero errors.
- Core suite: **37 passed, 0 failed** (21 existing and 16 new).
- UI/runtime suite outside the sandbox: **35 passed, 0 failed**, including native global-hotkey reservation and cleanup.
- New tests cover boundary/equal/reversed/out-of-range rates, interval bounds, repeated seeds, interval statistics, measurement calculation, automatic completion, explicit/external/pre-start cancellation, duplicate starts, restart, snapshot isolation, shutdown, duration validation and absence of live input dependencies.
- UI regressions cover profile selection and actual Open test action, invalid range/seed handling, logical-event presentation, unchanged production settings, blocked Toggle/Hold activation during tests, emergency cancellation and no automatic activation on profile changes.
- Default main layout fits without scrolling. Narrow layout retains reachable controls. Inspected actual WPF-rendered variable test and main-window images.
- Automated tests generated no real mouse or keyboard input. Native hotkey registration was checked separately; command routing was exercised without synthesizing physical key presses. No Roblox session was automated.

## Release and logs

The prior standalone executable was running and Windows denied replacement. It was left intact; the new standalone release was successfully published to a different directory.

- Updated executable: `artifacts\release-variable-timing\ClickForge.exe`
- Release build log: `artifacts\logs\build-variable-timing.txt`
- Core results: `artifacts\logs\tests-variable-timing.txt`
- UI/native results: `artifacts\logs\ui-tests-variable-native.txt`
- Publish log: `artifacts\logs\publish-variable-timing.txt`
- Test-window preview: `artifacts\logs\ui-variable-test.png`
- Updated main-window preview: `artifacts\logs\ui-default.png`

Close the old instance before launching the new artifact so it can register the configured global shortcuts.
