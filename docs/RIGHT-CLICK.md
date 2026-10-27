# Right-click output — 27 October 2026

Choose **Input type → Mouse → Right**. Right is available in the production
output picker, existing settings/profiles and the controlled Safe Click Test.
Left stays the default. Output changes stop input and require explicit activation.

Right uses the same worker and Windows input transport: RIGHTDOWN `0x0008` and
RIGHTUP `0x0010`, with mouseData zero. Existing stop, pause, emergency, failure and
shutdown release barriers remain in place. Unsupported input never substitutes
another button. Target restrictions and global shortcut choices are unchanged.

Persisted enum values remain Left=0, Mouse4=1, Mouse5=2 and CustomKey=3; Right is
appended as 4. This preserves existing keyboard and mouse configurations. The
safe test now maps display indices to explicit output values, so the fourth
mouse option cannot be mistaken for the existing keyboard value. The optional
native diagnostic also uses this mapping.

## Verification

Executed `./build.ps1 -SkipPackage -BuildDirectory artifacts/right-click-final`:

- Release application/helper builds: zero warnings/errors.
- **171 core tests**, **36 updater tests**, **231 WPF checks** passed: **438 total**.
- Five new core tests cover exact Right flags and numeric compatibility,
  Fixed/Variable routing, pause/resume/emergency, unsupported input, partial-pair
  release through pause/stop/emergency/disposal, and settings/profile persistence.
- Six new WPF checks cover actual Right selection/persistence/profile application,
  Open Test binding, selected-Right-only down/up reception and preservation of the
  keyboard test mapping. Existing packet, keyboard, target, capture and shutdown
  regressions also ran.
- The open dropdown and safe-test view were rendered and visually inspected.
  Logs: `artifacts/logs/right-click-final-build.txt`, `right-click-final-ui.txt`.
  Images: `ui-right-output-popup.png`, `ui-right-safe-test.png` in that folder.
- An existing idle-rendering test initially raced asynchronous foreground
  verification. Its idle-only assertion now observes a stable test policy and
  restores the original restriction in `finally`; separate target/cache tests
  still run. Production target checks were not changed.
- Final mapping/diff review and `git diff --check` passed. No new click loop,
  dependency or transport API was introduced.

Production input was mocked. Pad events were routed WPF events, not native Right
injection; actual delivery into Roblox or another live target was not tested.
The optional native diagnostic was updated but not executed. No target acceptance
or anti-cheat bypass is claimed. Existing screenshot-heavy diagnostic CPU
variability remains uncharacterized.

The local app was normally closed, 13 verified application/helper files were
hash-checked into `artifacts/ui-build`, and ClickForge reopened successfully.
Previous files: `artifacts/backups/before-right-click-20261027-130051`. Personal
settings/profiles were not overwritten by the replacement. No new public release
or installer was published at that local stage. Right output is now part of the
combined [v1.5.0 release](RELEASE-v1.5.0.md), with final verification in the
[side-button controls report](SIDE-BUTTON-CONTROLS.md).

## Files changed for Right-click support

- `src/ClickForge.Core/ClickButtons.cs`
- `src/ClickForge.Core/OutputSelection.cs`
- `src/ClickForge/TestWindow.cs`
- `src/ClickForge/NativeButtonDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.cs`
- `src/ClickForge/UiDiagnostics.OutputSelection.cs`
- `src/ClickForge/UiDiagnostics.Lightweight.cs`
- `tests/ClickForge.Tests/Program.cs`
- `tests/ClickForge.Tests/RightClickTests.cs` (new)
- `README.md`
- `docs/OUTPUT-TYPES.md`
- `docs/RIGHT-CLICK.md` (new)

These extend the prior, still-unreleased separate Mouse/Keyboard selector changes.
