# ClickForge v1.3.0

This release brings the local v1.2.0 candidate and subsequent input/statistics improvements to the public update channel. Existing v1.1.x releases and local v1.2.0 builds can discover v1.3.0 through Updates.

## Changes

- Obsidian-and-champagne desktop UI with responsive navigation and actual target/profile inspection.
- Per-user Windows installer, optional Desktop/Start menu shortcuts, and the portable ZIP alternative.
- Configuration profiles, opt-in tray workflow, single-instance activation and redacted diagnostics.
- Session elapsed time, input pairs sent and measured average CPS. Pauses/target waits are explicitly included; Stop freezes the session and a new activation resets it.
- Plain-language target blocking reasons based on process/window identity, visibility, foreground and pointer observations.
- Left, Mouse 4 (Back) and Mouse 5 (Forward) click outputs, preserving Left defaults and original-button cleanup on failures.
- Mouse 4/5 in Activate, Pause and Emergency stop dropdowns. Injected events cannot trigger mouse shortcuts; stale queued events from replaced listeners are ignored.
- Custom input key recorder: repeat one supported keyboard key at the configured Fixed/Variable CPS, persist it in settings/profiles, and reject conflicts with global shortcuts.
- Controlled safe test with mouse/key down/up reception and the existing isolated logical timing simulator.

All output uses the existing worker, timing and stop barriers. Target restrictions remain enabled; no application input acceptance, physical-input disguise or anti-cheat bypass is claimed. Changing settings/profiles/output and launching the app never automatically activate input. Hold activation requires a different output button when using a mouse shortcut.

## Downloads and updating

- Recommended: `ClickForge-Setup-v1.3.0.exe`, a per-user Windows x64 installation.
- Portable: extract the complete `ClickForge-v1.3.0-win-x64.zip` and keep the helper alongside the app.
- Existing updater users: open Updates, check for a new release, then confirm download/install and restart. v1.0.0 requires a manual bootstrap download.

The executable remains unsigned. SHA-256 files verify integrity, not publisher authentication. Settings and profiles stay in the existing per-user data directory; installation/update does not enable input.

## Verification limits

Local implementation checks passed 136 core tests, 36 updater tests and 160 WPF checks with mocked production input. Release gating additionally builds the exact tagged package, verifies the helper's success/rollback behavior and runs the real installer lifecycle. Published workflow logs provide the final release evidence.

Physical gaming-mouse presses, native keyboard delivery into targets, live Roblox acceptance, screen-reader sessions and real multi-DPI transitions were not verified. Routed safe-pad events and native hook installation do not establish physical input delivery. Windows/input hooks may reject synthetic events; unsupported input stops without a fallback. Software-rendered diagnostic CPU measurements remain variable and are not hardware benchmarks.

Detailed implementation records: [statistics](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.0/docs/SESSION-STATISTICS.md), [mouse outputs](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.0/docs/MOUSE-BUTTONS.md), [mouse shortcuts](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.0/docs/MOUSE-HOTKEYS.md), [custom keys](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.0/docs/CUSTOM-INPUT-KEY.md), [installation](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.0/docs/INSTALLING.md).
