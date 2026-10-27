# Simpler ClickForge and Minecraft Bedwars setup

The main app now uses one Home view with plain navigation and three setups.
Detailed statistics and advanced controls are collapsed; Start, Pause and
Emergency Stop stay pinned outside the scrolling area. The shared dark theme
uses lighter charcoal surfaces, restrained gold and readable text. There are no
new animations, dependencies or input workers.

![Simplified Home in Minecraft Bedwars setup](bedwars-layout.png)

## Choose a setup

| Setup | Back / Mouse 4 | Front / Mouse 5 |
| --- | --- | --- |
| Regular clicker | Your ordinary shortcut settings | Your ordinary shortcut settings |
| Mouse buttons | Toggle Left | Toggle Right |
| Minecraft Bedwars | Toggle Right | Toggle Left |

Choose **Minecraft Bedwars** on Home, then **Choose window**, refresh the window
list and select your Minecraft window. Only the selected valid window can receive
input while it is in front and the pointer is inside. The preset changes the
button layout; it does not detect a Bedwars server or claim a target accepted input.

Both side buttons toggle. The same button stops its request; the other stops and
releases the old output before switching the existing single worker. They do not
run simultaneous Left and Right workers. The physical side-button events keep
their existing pass-through behavior. Injected events cannot trigger shortcuts.

Setup selection preserves CPS, fixed/variable timing, session limits and the saved
regular-clicker Roblox restriction. Bedwars uses the existing selected-window gate
for Fixed CPS as well as the selected-target policy already used by Variable CPS.
Returning to Regular clicker restores its saved policy. No configuration, profile,
setup or launch starts input. Side toggles remain inactive in Hold mode with a
clear explanation; selecting either mouse setup explicitly chooses Toggle and
F6/F7/F8 keyboard shortcuts.

**Small view** offers the same setup selector and mappings. **Try safe test** opens
the existing controlled pad/logical simulator, stopping the production worker.
**More settings** contains activation, shortcuts, safe-test options and advanced
controls; **Session stats** shows configured versus measured values. Profiles,
settings, updates, target inspection, tray and shutdown are preserved.

## Executed verification — 27 October 2026

`./build.ps1 -SkipPackage -BuildDirectory artifacts/v1.6.0-review`

This records the local feature review. The unpublished v1.6.0 attempt later
exposed a test assumption on GitHub's 1024×720 desktop: it expected a full
1000×880 window even when Windows capped it. v1.6.1 keeps the full-size no-scroll
assertion when that size is available, sizes to the actual work area otherwise,
and explicitly verifies scrolling and pinned Emergency Stop on the smaller view.
Neither target checks nor production input behavior changed for that correction.

Executed correction review:
`./build.ps1 -SkipPackage -BuildDirectory artifacts/v1.6.1-display-review`.
Release builds again passed with zero warnings/errors; **183 core + 38 updater +
285 WPF checks passed: 506 total**. This includes a local 984×680 window simulation
of the hosted 1024×720 work area, checking actual scrolling and Emergency Stop
placement. Full-size no-scroll checks also passed locally. Logs are
`artifacts/logs/v1.6.1-display-review-build.txt` and the saved release UI logs.

- App/helper Release builds: **zero warnings, zero errors**.
- **183 core + 38 updater + 283 WPF checks passed: 504 total**.
- New tests exercise both mappings, legacy/invalid settings, persistence and
  profile preservation, selected-target routing, focus/pointer eligibility loss,
  explicit activation, single-worker changes, release barriers and emergency stop.
- Actual WPF checks cover selection/persistence, correct labels in both windows,
  no-selection blocking, ordinary-gate exclusion, both toggles, target loss,
  Hold feedback, preset switching, saved restrictions and hidden manual controls.
- Default 1000×880 Regular/Bedwars layouts were checked without scrolling.
  Small-window scrolling, content scaling, keyboard controls, themed dropdowns,
  pinned emergency actions and existing capture/profile/updater regressions ran.
- Runtime default, smaller, preset and target-dialog screenshots were inspected.
- `git diff --check` passed. The click worker, native packet transport and existing
  target-service validation implementations were not edited.

The updater checks additionally cover assembly-derived request versions, Bedwars
settings preservation through upgrade/rollback, and opening/reviewing/closing
Updates with input stopped. Install stays unavailable without a staged package.

Logs: `artifacts/logs/v1.6.0-review-build.txt` and `v1.6.0-review-ui.txt`.
Captures: `ui-bedwars-main.png`, `ui-simple-main.png`, `ui-bedwars-small.png`,
`ui-small.png` and `ui-target-view.png` under `artifacts/logs`.

Production input/target conditions were mocked in tests; no automated input was
sent into Minecraft, Roblox or a live game. Physical side-button delivery, game
input acceptance, screen readers and real multi-monitor DPI changes were not
verified. Existing screenshot-heavy diagnostic CPU variability remains unresolved.
Packaging instructions and SDK source inclusion were inspected. The tagged
v1.6.1 workflow separately checks installation, upgrade, shortcuts, uninstall,
the real helper and uploaded assets before publication. See completed workflow
logs and [release notes](RELEASE-v1.6.1.md) for those results.

## Files changed

- `src/ClickForge.Core/SideButtonOutputs.cs`
- `src/ClickForge.Core/SetupTargetGate.cs` (new)
- `src/ClickForge.Core/Settings.cs`
- `src/ClickForge/MainWindow.xaml`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/MainWindow.SideButtons.cs`
- `src/ClickForge/MainWindow.Shell.cs`
- `src/ClickForge/MainWindow.VariableCps.cs`
- `src/ClickForge/MainWindow.Advanced.cs`
- `src/ClickForge/SideButtonControlWindow.cs`
- `src/ClickForge/Themes/Controls.xaml`
- `src/ClickForge/UiDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.Daily.cs`
- `src/ClickForge/UiDiagnostics.Bedwars.cs` (new)
- `src/ClickForge/MainWindow.Updates.cs`
- `src/ClickForge/UpdateWindow.cs`
- `src/ClickForge.Core/Updates/UpdateClient.cs`
- `tests/ClickForge.Tests/Program.cs`
- `tests/ClickForge.Tests/BedwarsSetupTests.cs` (new)
- `tests/ClickForge.UpdateTests/Program.cs`
- `Version.props`, `README.md`, `scripts/Package-Release.ps1`, this report and its
  screenshot, `docs/UPDATING.md`, and `docs/RELEASE-v1.6.0.md` / `RELEASE-v1.6.1.md`.
