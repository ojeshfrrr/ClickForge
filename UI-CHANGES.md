# Reference-based UI redesign

Implemented the supplied navy/electric-blue reference as real WPF controls. Native Windows window chrome is retained, with a dark caption on supported Windows versions. There is no decorative settings button with no action.

## Exact UI change files

1. `src/ClickForge/App.xaml` — loads the reusable theme dictionary.
2. `src/ClickForge/Themes/Controls.xaml` — palette, pointer icon, panel styles, blue primary action, red stop action, dark ComboBox and popup items, TextBox, Slider, CheckBox, segmented RadioButtons, ScrollBar, tooltips and keyboard focus states.
3. `src/ClickForge/MainWindow.xaml` — reference-based header, horizontal status strip, speed/action panel, balanced mode/restriction panels, visible shortcut editor and understated safe-test panel.
4. `src/ClickForge/MainWindow.xaml.cs` — segmented controls wired to the original modes, explicit emergency action, truthful detection/count labels, shortcut badges, responsive stacking, work-area bounds, opt-in render diagnostics and cached status updates. Active count labels update once a second; control/safety handlers remain immediate.
5. `src/ClickForge/ForegroundTargetService.cs` — exposes verified foreground/checking status for the interface; retains fail-closed validation and asynchronous signature checks.
6. `src/ClickForge/Native.cs` — dark native titlebar support.
7. `src/ClickForge.Core/Settings.cs` — revised default window dimensions, layout-version migration and validated size range. Existing CPS/mode/keys/restriction preferences are preserved.
8. `src/ClickForge/UiDiagnostics.cs` — explicit safe runtime verification with mocked mouse input, temporary configuration and rendered artifacts.

Supporting files added: `README.md`, `build.ps1`, and this change report. ClickEngine scheduling, mouse injection, global-hotkey lifecycle, safe target implementation and exit cleanup remain separate from the theme.

## Verification

- Release build: succeeded, zero warnings and zero errors.
- Core tests: 21 passed, zero failed (`artifacts/logs/tests.txt`).
- UI/runtime checks: 23 passed, zero failed outside the sandbox (`artifacts/logs/ui-tests-native.txt`).
- Native RegisterHotKey/UnregisterHotKey: F9/F10/F11 reservation and cleanup passed. The sandbox initially returned Win32 access denied; the unrestricted check succeeded.
- Verified numeric/slider synchronization, interval calculation, invalid input, routed start/pause/resume/emergency actions, Hold release simulation, restriction changes, conflicting shortcuts, reset, persistence and test-window lifecycle.
- Default 880 x 900 layout: all essential controls fit without scrolling.
- Narrow 640 x 700 layout: options stack; styled scrolling reaches the lower controls.
- Inspected runtime-rendered default, narrow, focus, disabled Pause and open shortcut popup images.
- Self-contained Windows x64 single-file publish succeeded.
- Physical global key delivery was not synthesized in the automated suite. UI shortcut tests route through the real command handler; native tests separately verify registration and cleanup.
- The native click target was opened/closed automatically; actual OS click delivery was not automated. The user's earlier report confirmed that clicking worked in their session.
- No live Roblox/public-game automation was performed.

## Measured resources

Final two-second samples from the visible WPF diagnostics process on a 20-logical-processor machine:

- Idle: 77.62% of one core (3.88% of total logical CPU); 157.1 MiB working set; 129.9 MiB private memory.
- UI plus 10 CPS scheduler with mocked mouse input: 30.49% of one core (1.52% total); 157.8 MiB working set; 130.0 MiB private memory.

Earlier idle sampling recorded 0.00% of one core and 131.5 MiB. Visible-window results varied substantially during desktop/game activity and software rendering; these short diagnostic samples do not establish representative idle CPU or native-input performance. No near-zero visible-idle guarantee is claimed. Status redraws are now cached, shared brushes are reused, and the click counter is throttled independently from safety controls.

The release contains the desktop runtime, so its approximately 133 MiB disk size is separate from these memory measurements.

## Output paths

- Executable: `artifacts\release\ClickForge.exe`
- Source: `src\`
- Tests: `tests\ClickForge.Tests\`
- Build log: `artifacts\logs\build.txt`
- Publish log: `artifacts\logs\publish.txt`
- Core results: `artifacts\logs\tests.txt`
- UI/native results: `artifacts\logs\ui-tests-native.txt`
- Default UI: `artifacts\logs\ui-default.png`
- Focus state: `artifacts\logs\ui-focus.png`
- Dropdown: `artifacts\logs\ui-dropdown.png`
- Narrow top/bottom: `artifacts\logs\ui-small.png`, `ui-small-bottom.png`
