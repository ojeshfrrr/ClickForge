# Custom input key

> Historical implementation-stage verification record. For the published feature summary, see [v1.3.0 release notes](RELEASE-v1.3.0.md).

The click-output dropdown now includes **Custom input key…** after Left and Mouse 4/5. Selecting it stops input and opens a themed recorder. Press one supported keyboard key and choose Use key; Cancel/X restores the previous output and keeps input stopped. Change: key reopens the recorder. Start input and Sending keys distinguish keyboard output from mouse clicking. Settings and profiles capture the key, and profile application synchronizes the UI without activation.

This repeats one virtual keyboard key as a down/up pair at the existing Fixed or Variable CPS timing. It does not record text or multi-key macros. Letters, digits, F1–F24, Space, Enter, Tab, Backspace, Escape, navigation keys, numpad keys and common OEM punctuation keys are supported. Standalone modifiers, modifier combinations, lock/system keys and unknown codes are rejected with feedback. OEM labels identify the virtual key rather than promising a particular character or layout-independent physical key.

The Windows adapter uses [KEYBDINPUT](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-keybdinput) through SendInput, with KEYUP and appropriate extended-key flags. The existing single worker schedules the pair and counts it only when Windows reports both submitted events. Existing mouse output and defaults remain unchanged. Unsupported/partial input does not fall back to a mouse click. Pending cleanup retains the original key across failures and output changes; new input cannot be submitted until that cleanup succeeds.

The custom key cannot equal the configured Activate, Pause or Emergency stop key. This prevents synthetic keyboard output from invoking ClickForge's own global shortcuts. Validation runs during capture, settings load/save, profile application, shortcut changes and before activation. Resetting shortcuts also validates before mutation: defaults are refused if they would conflict with an active custom F6/F7/F8 output. Global activation is blocked while the recorder is open. No settings/profile/capture action automatically starts input.

The Safe Click Test accepts the configured key and shows bounded down/up events. Keyboard input additionally requires keyboard focus inside the controlled pad, with the existing foreground/pointer/window checks. Logical Variable Timing simulation stays input-free. Native mouse diagnostics still iterate only the three mouse choices; they do not accidentally treat Custom as a mouse packet.

## Verification and review

- Executed `./build.ps1 -SkipPackage -BuildDirectory artifacts/custom-key-final`: Release app/helper builds passed with **zero warnings/errors**, **136 core tests, 36 updater tests and 160 actual WPF checks passed**. Log: `artifacts/logs/custom-key-verified.txt`.
- Added 12 core tests for validation/extended flags, shortcut collisions, settings migration/persistence, profile import/export/apply, safe pre-mutation rejection, existing-worker routing, unsupported input, partial input/release retry, output changes, target waiting and in-flight stop/disposal.
- Actual UI tests verified capture validation, global-start blocking during capture, cancellation, settings persistence, profile synchronization, mocked keyboard start/pause/resume/emergency, and reset/apply conflict handling. Routed WPF key down/up events reached only the isolated pad with both production and native test engines stopped. These are synthetic routed UI events, not native SendInput delivery.
- Capture, main dashboard and safe-test screenshots were generated; capture/dashboard were visually inspected. New primary button text uses explicit dark ink for contrast. Screenshots: `artifacts/logs/ui-custom-key-capture.png`, `ui-custom-key-dashboard.png`, `ui-custom-key-safe-pad.png`.
- Final review checked release identity, separate mouse/keyboard transport routing, old-data defaults, profile/control synchronization, capture cancellation and shortcut-reset collisions. Production SelectionValid, CanActivate and CanClick gates match the committed versions. Timing calculations and target restrictions are unchanged. `git diff --check` passed.
- The verified 13-file build was copied into the local `artifacts/ui-build` directory; every copied file's SHA-256 matched. Old binaries were retained in `artifacts/local-custom-key-backup-e89830f4849b40f981fbfc5045bd9ba6`. Settings/profiles were not replaced. The updated app was opened and remained running. Log: `artifacts/logs/custom-key-local-update.txt`.

Native keyboard delivery, physical key presses, layout-specific OEM behavior and live Roblox acceptance were not exercised. The standard production-engine UI tests use mocked input; no automated Roblox input was sent. Windows can reject input or cleanup, and the service reports this instead of guaranteeing acceptance. Existing software-rendered CPU measurements remain high/variable and were not optimized. Installer/helper lifecycle suites from earlier changes were not repeated for this follow-up; the updater unit suite and Release helper build were rerun. No new release package, version tag, GitHub push or publication was created. SDK compilation embeds the new classes into the existing app payload without new dependencies.

## Files changed for this feature

- `src/ClickForge.Core/KeyboardOutput.cs`: supported keys, labels, output validation and down/up flags.
- `src/ClickForge.Core/ClickButtons.cs`: Custom option, explicit mouse-only choices, keyboard transport extension and retained release identity.
- `src/ClickForge.Core/ClickEngine.cs`: atomic output configuration and key-aware shared input call.
- `src/ClickForge.Core/Settings.cs` and `Profiles.cs`: optional key field, validation, collisions, persistence and matching.
- `src/ClickForge/Native.cs`: keyboard INPUT union member and cached down/up packets.
- `src/ClickForge/NativeButtonDiagnostics.cs`: keep mouse diagnostics limited to mouse outputs.
- `src/ClickForge/CustomKeyWindow.cs` and `DialogLayout.cs`: themed key recorder and optional Escape capture.
- `src/ClickForge/MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.VariableCps.cs`, `MainWindow.Daily.cs`: recorder, change action, labels, activation blocking and reset conflict handling.
- `src/ClickForge/TestWindow.cs`: controlled keyboard focus and key-event reception.
- `src/ClickForge/UiDiagnostics.cs` and `UiDiagnostics.CustomKeys.cs`: key-aware mocks, capture/profile/command/safe-pad regression checks.
- `tests/ClickForge.Tests/CustomKeyTests.cs`, `ButtonTests.cs`, `Program.cs`: new tests and mouse-only enum regression adjustment.
- `README.md` and this report: usage and executed verification.
