# ClickForge v1.4.0

Six advanced control and safety features are now part of the existing Windows
application, retaining the obsidian-and-champagne interface and shared engine.

## New controls

- **Session auto-stop:** optional 1–86400 wall-clock seconds, 1–1,000,000,000
  submitted input pairs, or both. Either limit stops input using the existing
  worker/release barrier. Pauses and target waits count toward duration; only a
  new explicit start resets progress. Applied limits and remaining values are shown.
- **Application-profile suggestions:** associate a saved profile with a valid
  selected application. Only a path fingerprint and profile ID are stored locally.
  Applying a suggestion is explicit, stops input and keeps the selected target
  and validation intact. Remove one association or clear all.
- **Safe-test timing scope:** scheduled/measured interval graphs, interval spread,
  standard deviation and scheduling-delay summaries in the isolated logical
  simulator. Seeds reproduce scheduled intervals, not Windows timing delays.
- **Compact controls:** movable window with actual state, profile, session values,
  Start/Stop, Pause/Resume and a pinned Emergency Stop. Opening or closing it
  stops the current request; a fresh activation is explicit. Keep-on-top is opt-in.
- **Command palette:** Ctrl+K searches sections, profiles, safe test and stop
  commands. `cps 25`, for example, offers a validated Fixed CPS change requiring
  explicit execution and confirmation. Opening/highlighting executes no result.
- **Private safety timeline:** the last 100 actual engine/safety events, recording
  only type and time in memory. Clear it at any time; no per-pair disk logging,
  pointer coordinates, target titles or application content are collected.

Expand **Global shortcuts & advanced controls → Advanced session controls**.
The navigation footer opens compact controls and commands. Existing Fixed and
Variable CPS behavior, output choices, global shortcuts, profiles, target checks,
updater and coordinated shutdown are preserved. Configuration never starts input.

## Downloads and updating

- Installer: `ClickForge-Setup-v1.4.0.exe`, per-user Windows x64 installation.
- Portable: extract the complete `ClickForge-v1.4.0-win-x64.zip` and keep its helper.
- Existing users: open Updates, check, download/verify, then confirm install/restart.
  Settings and profiles remain in the existing user-data directory. Old settings
  default to disabled auto-stop limits. Associations remain separate local data
  and do not change portable timing-profile import/export schema.

Downloads remain unsigned; published SHA-256 files provide integrity checking,
not publisher authentication.

## Verification and limits

Local verification passed Release builds with zero warnings/errors, 159 core
tests, 36 updater tests and 210 real WPF checks with mock production input/targets.
The tagged workflow additionally gates publication on the exact release package,
helper success/rollback, actual installation/upgrade/shortcuts/uninstall and
uploaded-asset integrity. Workflow logs provide final release results.

No automated input was sent into Roblox. Native registration, routed safe-pad
events and simulation tests do not establish physical input delivery or target
acceptance. Positive profile suggestion used a synthetic application fingerprint;
selection/application against a separate live app, physical confirmation dialogs,
screen readers and actual multi-monitor DPI transitions were not exercised.

Duration stops depend on scheduling and an input call already in flight can finish
before the stop barrier returns. Measured rates from very short sessions can be
unstable and are labeled in compact controls. Existing high/variable CPU after
screenshot-heavy UI diagnostics remains uncharacterized; no major RAM reduction,
human-mimicry, anti-cheat evasion or game input acceptance is claimed.

See [usage, exact files, tests and privacy details](https://github.com/ojeshfrrr/ClickForge/blob/v1.4.0/docs/ADVANCED-CONTROLS.md).
