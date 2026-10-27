# ClickForge v1.5.0

Mouse 4 (Back) can now toggle Left-clicking and Mouse 5 (Forward) can toggle
Right-clicking. A simpler window makes the assignments and their actual state
easy to see while retaining the existing dark-and-gold application.

## What's new

- **Right-click output:** choose **Input type → Mouse → Right**. Left stays the
  default; existing saved mouse and keyboard values retain their meanings.
- **Separate output selectors:** Mouse offers Left, Mouse 4, Mouse 5 and Right.
  Keyboard captures one supported key. Output controls are distinct from the
  global activation, pause and emergency shortcuts.
- **Side-button assignments:** open **Hotkeys → Mouse controls → Apply mouse
  setup**. Mouse 4 toggles Left; Mouse 5 toggles Right. Press the same button to
  stop, or the other to switch the existing single worker. Both use Toggle mode.
  Setup selects F6/F7/F8 keyboard shortcuts without changing target restrictions
  or speed. Assignments default off and persist as an application preference.
- **Simple mouse controls:** actual Off/Sending/Paused/Waiting/Error indicators,
  synchronized fixed-speed slider, existing Safe Test and pinned Emergency Stop.
  Opening, closing, setup and speed changes stop input. Variable settings remain
  on the dashboard. Smaller windows scroll intentionally.
- **Safe-test mapping:** typed output mapping keeps Right distinct from legacy
  keyboard values and displays the chosen output correctly in the controlled pad.
- **Repeatable UI tests:** automated runs use fresh disposable defaults and wait
  for popup scrolling/focus to settle. Normal settings loading remains unchanged.

The fixed/variable worker, target checks, profiles, session limits, emergency
release barriers and updater remain in place. No input starts on launch or from
configuration changes. Synthetic input is not claimed to be accepted by a target.

## Downloads and updating

- Installer: `ClickForge-Setup-v1.5.0.exe` for a per-user Windows x64 installation.
- Portable: extract the complete `ClickForge-v1.5.0-win-x64.zip`, including its helper.
- Existing users: **Updates → Check → Download/verify → Install/restart**.
  Existing settings and profiles are preserved; apply the optional mouse setup
  explicitly after updating. Public downloads do not require a GitHub account.

The downloads are unsigned. SHA-256 files check integrity, not publisher identity.

## Verification and limits

Local Release builds passed with zero warnings/errors and **177 core tests,
36 updater tests and 258 actual WPF checks: 471 total**. Mock input/targets were
used for production-worker tests. Default and smaller simple-window screenshots
were inspected; default essential controls fit without scrolling.

The tagged workflow gates publication on the same suites, exact package and helper
verification, actual installer/upgrade/shortcut/uninstall checks, installed-app
WPF regressions and uploaded-asset digest checks. The completed workflow logs
provide release-package results.

No automated input was sent into live Roblox. Native registration and routed pad
events do not verify physical mouse delivery or game acceptance. Physical mouse
driver remapping, screen readers and live multi-monitor DPI transitions were not
exercised. Side-button events retain their existing pass-through behavior and may
also reach the foreground app. Existing high/variable screenshot-heavy diagnostic
CPU remains uncharacterized; this release makes no performance or anti-cheat claim.

See [setup, changed files and test details](https://github.com/ojeshfrrr/ClickForge/blob/v1.5.0/docs/SIDE-BUTTON-CONTROLS.md).
