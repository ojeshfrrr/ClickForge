# ClickForge v1.6.1

A simpler Home view and separate mouse setups make the existing Windows utility
easier to use, with a clearer updater and the same single input worker.

## New layouts and controls

- **Minecraft Bedwars setup:** Front / Mouse 5 toggles Left; Back / Mouse 4 toggles
  Right. Select your Minecraft window first. Input waits when that window is not
  valid, not in front or outside the pointer. The preset does not identify a
  Bedwars server or establish target acceptance.
- **Original Mouse buttons setup:** Back toggles Left and Front toggles Right.
  Both setups remain distinct and persist as application preferences.
- **Regular clicker:** keeps existing mouse/keyboard output, Fixed/Variable CPS,
  ordinary shortcut settings and the saved Roblox restriction.
- **Simpler whole-app UI:** readable top navigation, setup and speed together,
  collapsed statistics/advanced controls, calmer dark surfaces and pinned
  Start/Pause/Emergency Stop. The default Regular/Bedwars layouts fit without
  scrolling at the tested display scale; small windows scroll intentionally.
- **Small view:** the same setup selector, accurate front/back labels, speed,
  real output state, Safe Test and Emergency Stop in a separate compact window.

Both side buttons use Toggle. Press the same button to stop; the other stops and
releases the previous output before switching. They share the existing worker,
not simultaneous Left/Right loops. Setup changes stop input and never start it.
Bedwars keeps the regular-clicker Roblox preference saved and uses the existing
selected-window validation for its own Fixed CPS policy. Existing Variable CPS,
profiles, session limits, hold mode, tray, safety timeline and shutdown remain.

## Updater

**Updates → Check for updates → Download update → Install and restart**.
Opening Updates stops input and blocks activation while reviewing the update;
closing it never resumes input. Install is available only with a verified staged
package. Requests identify the actual release version. Public-channel restrictions,
checksums, package validation, startup confirmation and rollback are retained.
Upgrade and rollback preserve the new setup preference, settings and profiles.

- Installer: `ClickForge-Setup-v1.6.1.exe`, per-user Windows x64 installation.
- Portable: extract the entire `ClickForge-v1.6.1-win-x64.zip`, including its helper.
- Existing users can update through the same public GitHub channel. Choose the
  optional Bedwars setup explicitly; launch/update never starts input.

Downloads are unsigned. SHA-256 checks integrity, not publisher identity.

## Verification and limits

Local Release app/helper builds passed with zero warnings/errors and **183 core,
38 updater and 285 actual WPF checks: 506 total**. Preset mappings, legacy/invalid
settings, persistence, selected-target routing/loss, toggles, release and emergency
paths, updater state guards and upgrade/rollback preservation were exercised.
Default, small and target-view screenshots were inspected. The final source diff
was reviewed for target bypasses, timing changes and misleading ready states.

The tagged workflow separately gates publication on the full suites, exact tagged
package, real helper success/rollback, actual install/upgrade/shortcut/uninstall,
installed-app WPF regressions and uploaded-asset digest checks. Completed workflow
logs provide final release-package results.

Production input and target conditions were mocked in tests. No automated input
was sent into Minecraft, Roblox or another live game. Physical side-button
delivery, game acceptance, screen readers and real monitor-DPI transitions were
not tested. Existing side events remain pass-through and may reach the target.
Existing high/variable CPU after screenshot-heavy diagnostics remains unresolved;
no performance, human-mimicry or anti-cheat evasion claim is made.

See [setup, exact files and test details](https://github.com/ojeshfrrr/ClickForge/blob/v1.6.1/docs/BEDWARS-SETUP.md).

The v1.6.0 candidate was not published. This version corrects the hosted UI check to respect the available display size while retaining the full-size no-scroll check locally and scrolling/emergency accessibility checks on smaller displays.
