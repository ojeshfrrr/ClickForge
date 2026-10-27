# Mouse and keyboard output selection — 27 October 2026

ClickForge v1.5.0 exposes the existing mouse/keyboard engine support
through a separate **Input type** selector beside the mouse-button control.
This is independent of Activate, Pause and Emergency Stop shortcuts.

Right-click output was added afterward. See [Right-click verification](RIGHT-CLICK.md)
for the final combined suite, compatibility mapping and local delivery.

## Usage and behavior

- **Mouse:** choose Left, Mouse 4 (Back), Mouse 5 (Forward), or Right. Fresh settings
  continue to use Mouse → Left.
- **Keyboard:** capture one supported key and confirm **Use key**. If a previously
  captured key is still valid and does not conflict with current shortcuts,
  selecting Keyboard reuses it. **Key: … · Change** captures another key.
- Only the relevant mouse picker or keyboard capture control is shown. The
  keyboard control's accessible name includes the chosen key. Hidden mouse-picker
  events cannot switch a keyboard output. Keyboard navigation uses existing WPF
  controls and theme/focus styles; the layout wraps at small widths.
- Changing type, mouse button or captured key stops the existing request and any
  active safe test. Cancellation keeps the prior output and leaves input stopped.
  The previous mouse button is remembered within this run; returning to Mouse
  after a fresh keyboard-mode launch defaults to Left.
- Unsupported/system/modifier keys and Ctrl/Alt/Shift/Windows combinations are
  rejected by capture. Activation, pause and emergency keys cannot be used as
  keyboard output. Retained keys are rechecked against current shortcut settings
  whenever Keyboard is selected; conflicts require a new capture.

Select **Safe click test → Fixed CPS → Open test** to test the chosen output in
the existing controlled pad. Keyboard tests require pointer and keyboard focus
inside that foreground pad. The safe-test output caption identifies mouse or
keyboard; received down/up events are shown there. Opening the test does not
start native input. The isolated variable simulator is unchanged and sends no input.

## Implementation and persistence

The application already supported mouse and custom keyboard output in its single
click worker. This change reuses `ClickEngine.ConfigureOutput`, `ButtonMouseInput`
and the existing Windows `SendInput` transport; it adds no new input loop, native
method, dependency or timer. Fixed/Variable CPS, target checks, Roblox verification,
hold/toggle, auto-stop limits, shortcuts and shared shutdown remain in place.

Mouse packets use Left/Right DOWN/UP or XBUTTON1/2 DOWN/UP. Keyboard packets use the
existing supported virtual-key mapping and KEYUP/extended-key flags. Unsupported
methods fail before starting; partial/blocked submission reports a clear error.
Pending release retains the original mouse button/key. Stop, pause, emergency stop,
errors, cancellation and disposal continue through the existing release barrier.
No other output is silently substituted.

The type is derived from the existing validated output discriminator:
`Button = CustomKey` means Keyboard; the four mouse values mean Mouse.
`CustomKey` stores the captured key. A second persisted type flag is unnecessary
and could disagree with the output. Existing settings and version-1 profiles keep
their schema and values; imported profiles synchronize the new controls. Invalid
settings restore safe defaults with a warning; invalid profiles remain rejected
by the existing strict profile loader. Global shortcut values are unchanged.

## Checks actually executed

Command:

`./build.ps1 -SkipPackage -BuildDirectory artifacts/output-types-delivery`

- Release application and updater builds passed with **0 warnings, 0 errors**.
- Complete core suite: **166 passed, 0 failed**, including seven new tests for
  category classification, validated transitions, preserved safety settings,
  settings/profile round trips and invalid data, both output categories in both
  timing modes, unsupported-method rejection without fallback, and partial-key
  release while Pause/Emergency Stop waits for an in-flight submission.
- Complete updater suite: **36 passed, 0 failed**.
- Actual WPF suite: **225 passed**, including fifteen new output-selection checks.
  These exercise capture rejection/cancellation, mode-specific visibility,
  persistence, hidden-picker protection, profile synchronization, key reuse,
  shortcut-conflict recapture, shared hotkeys, actual Open Test parameter binding,
  and selected-key-only DOWN/UP reception in the real safe-pad control.
- Existing button/key packet mapping, release-failure recovery, stop/disposal,
  foreground/target restrictions, startup-disabled state, profiles, shortcuts,
  compact controls, limits, simulator, updater and shutdown regressions also ran.
- The real built app was launched by the WPF suite. Mouse, keyboard and safe-test
  views were rendered and inspected. The clipped Keyboard selector label was
  corrected; existing default-size, narrow-layout and content-DPI tests passed.
- Final diff reviewed: no click-engine, transport, target-validation or shutdown
  implementation changes. `git diff --check` passed.

Logs: `artifacts/logs/output-types-delivery-build.txt` and
`artifacts/logs/output-types-delivery-ui.txt`. Captures:
`ui-mouse-output.png`, `ui-keyboard-output.png`,
`ui-keyboard-output-safe-test.png` in the same folder.

Production input is mocked in these tests. Safe-pad keyboard events are routed
WPF events, not native injection. Physical key capture, actual synthetic input
delivery into a live application/Roblox, screen readers and actual monitor-DPI
transitions were not verified. Native transport mappings were inspected and
mock-tested; Windows or a target can still reject synthetic input. No game input
acceptance, physical-input disguise or anti-cheat evasion is claimed.

The pre-existing high/variable CPU after screenshot-heavy full UI diagnostics
remains uncharacterized and was not fixed by this work. These checks do not
establish a RAM or general performance improvement.

Packaging/updater source inclusion and the existing explicit payload list were
inspected. The SDK includes the new source in the current app/core assemblies;
no payload/schema change is required. This report records the original local
selector stage; installer/public-feed checks were not run at that stage.
The combined v1.5.0 release includes these selectors, Right output and side-button
controls. See [current release verification](RELEASE-v1.5.0.md).

The 13 verified app/helper files were copied from `artifacts/output-types-delivery`
into `artifacts/ui-build`, with SHA-256 equality checked for each file. Previous
files are preserved under `artifacts/backups/before-output-types-20261027-124807`.
The normal local `artifacts/ui-build/ClickForge.exe` was reopened successfully;
settings and profiles were not overwritten by the file replacement. Other
portable/installed copies and the public release were not replaced.

## Exact files changed

- `src/ClickForge.Core/OutputSelection.cs` (new)
- `src/ClickForge/MainWindow.xaml`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/MainWindow.VariableCps.cs`
- `src/ClickForge/TestWindow.cs`
- `src/ClickForge/UiDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.CustomKeys.cs`
- `src/ClickForge/UiDiagnostics.OutputSelection.cs` (new)
- `tests/ClickForge.Tests/Program.cs`
- `tests/ClickForge.Tests/OutputSelectionTests.cs` (new)
- `README.md`
- `docs/OUTPUT-TYPES.md` (new)
