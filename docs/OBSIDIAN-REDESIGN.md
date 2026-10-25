# Obsidian and champagne redesign

> Historical implementation-stage verification record. For the published feature summary, see [v1.3.1 release notes](RELEASE-v1.3.1.md).

This v1.2.0 candidate builds on the tray/profile/usability work and public v1.1.2 baseline. The results below describe verification of the working tree before the candidate commits. No version tag or GitHub Release has been published.

## Actual application changes

- Replaced the blue theme with obsidian `#08090B`, graphite `#101216`, raised graphite `#171A20`, champagne `#E7C477`, soft gold `#B99A57`, warm white `#F4F1E9` and silver `#A6A8AE`. Emergency red and verification green retain their distinct meanings. Reusable templates cover the actual inputs, segmented controls, menus, lists, sliders, checkboxes, buttons, scrollbars, tooltips and keyboard focus.
- Rebuilt the main WPF window around a navigation rail, central dashboard and optional right inspector. The faceted pointer mark and navigation icons are embedded vector resources. Typography uses Segoe UI Variable with Segoe UI fallback. There are no new dependencies, browser surfaces, ambient animations or large rendering effects.
- Initial size is 1280×920 DIP with a one-time layout-version-3 migration. Existing later saved sizes remain respected. Below 1240 DIP, the right inspector is hidden; Target opens the same inspector controls in an owned, scrollable window. Below 1120 DIP, rail labels collapse. At smaller central widths, the status and activation/restriction sections stack appropriately. Small windows scroll intentionally.
- Status shows actual engine state, configured CPS/range, calculated interval and submitted input-pair count. It never claims a game accepted input. The neutral circular indicator is decorative state framing, not fabricated progress.
- Fixed numeric CPS is prominent, editable and synchronized with its original slider/engine path. Variable controls retain range validation and require the original explicitly selected target. Gold identifies active timing, sliders and the primary action; Emergency Stop retains red styling. Shortcuts remain visible on the action buttons and configurable in the expandable section.
- The target inspector uses a read-only snapshot from the existing retained-process target service. It reports selected executable filename, current title, validity, exact foreground state and pointer-under-target result. Roblox verification is explicitly described as having occurred at selection, using the existing executable/company/signature/certificate checks. There are no target thumbnails or fake verification badges. Missing selection displays an honest empty state.
- The right profile selector applies real stored profiles through the existing stop barrier. Save updates the current named profile, or opens the manager for a new custom configuration. Manage opens the complete existing profile manager. Profile selection never changes the selected target or starts input.
- Safe Click Test descriptions distinguish the existing controlled fixed-input pad from the logical variable simulator. The logical badge appears only for the logical mode. Its isolated implementation remains unchanged.
- Profile manager, Settings/diagnostics, Updates, Safe Click Test, tray context menu and popup controls share the new theme. The tray, global shortcuts, updater/helper and shutdown path remain integrated. Activation is blocked while configuration views are open, with an explanation.

## Verification

`./build.ps1 -SkipPackage` built the actual app and independent helper in Release with **zero warnings and zero errors**, then passed **74 core tests, 35 updater tests and 117 native WPF checks**. An intermediate layout assertion caught a 2.28-DIP overflow after the font and test description changed; panel spacing was corrected and the unchanged no-scroll assertion passed on the next run.

The WPF checks use mocked production mouse input and separate test settings/profiles. They exercise Fixed/Variable controls, routed activation/pause/emergency shortcuts, conflict handling, native spare-key registration/cleanup, Hold behavior, Safe Click Test isolation, profile management/import/export, actual Shell icon registration/rectangle, hide/restore/close/disposal, updater UI states, the inspector/profile actions, navigation relocation and shutdown with an active mocked request. They require an explicit successful-completion marker.

The actual machine is **150% Windows DPI**. The default 1280×920-DIP layout fit with no dashboard overflow. Compact windows, profile dialogs, open dropdowns, keyboard focus and disabled Pause were rendered and inspected. Additional content-layout scaling checks use 125%, 150% and 200%; they do not substitute for independent Windows DPI settings. Native 96-DPI, other display configurations, screen-reader speech and physical keyboard/notification-area interaction remain manual checks. No automated clicks were sent to Roblox. The earlier interactive Computer Use check was stopped by the user; this redesign was verified through the application's own diagnostic harness and rendered screenshots, without further Computer Use interaction.

Contrast calculations against actual specified surfaces: primary text **16.61:1**, secondary text **7.89:1**, primary-button dark text at the darker gold endpoint **8.46:1**, selected timing text **11.08:1**, and error text **5.99:1**. Disabled states are distinct and have real blockers. These figures are not an exhaustive accessibility certification.

The existing updater's complete suite passed, including installation/rollback preservation of profile files. Actual independent-helper process scenarios also passed with disposable parent/child fixtures, preserving settings and profiles. The portable review package uses the existing exact seven-entry manifest contract. Theme/icon resources are embedded; no additional runtime payload is required. A local package from uncommitted work records base HEAD as provenance and is not an official reproducible release.

Screenshots and execution logs remain under ignored `artifacts/logs/`; the README application image is updated from a real stopped, zero-input preview of this build. Compilation is not being presented as runtime verification.

The actual self-contained ZIP was also launched and passed all **117 UI checks**. A separate idle run of that packaged app measured **3.43% of one CPU core**, **142.4 MiB working set**, and **105.9 MiB private memory** over **5.022 seconds**, after five seconds of warmup, with input mocked and the engine disabled. Heavy diagnostic sequences produce substantially higher short post-test samples; these measurements are environment-specific, not a performance guarantee. The diagnostic process closed cleanly.

Local review artifact: `artifacts/obsidian-review/ClickForge-v1.2.0-win-x64.zip`. SHA-256: `dbc214e4cffdf142c1b960681aa7b1a6a9ddbf623510c83ef72ae352e47f0829`. Its strict archive/version/hash validation passed. Helper checks used `./scripts/Test-UpdateProcess.ps1 -PayloadDirectory artifacts/obsidian-review/payload`; both real process scenarios passed. Source and packaged UI reports contain explicit successful-completion markers. `git diff --check` reported no whitespace errors.

## Files for this visual pass

- `src/ClickForge/Themes/Controls.xaml`: palette, typography, vector identity/icons, templates and shared tokens.
- `src/ClickForge/MainWindow.xaml`: actual rail/dashboard/inspector layout and connected controls.
- `src/ClickForge/MainWindow.Shell.cs`: responsive shell, Target view relocation, read-only inspector rendering, quick profile actions and accurate test-mode descriptions.
- `src/ClickForge/MainWindow.xaml.cs`: layout migration, adaptive central layout and coordinated Target-view shutdown.
- `src/ClickForge/MainWindow.Daily.cs`: inspector/profile updates and configuration-view activation state.
- `src/ClickForge/MainWindow.VariableCps.cs`: configuration-view activation guard.
- `src/ClickForge/DesktopTargetService.cs`: read-only inspection snapshot; validation predicates and input gate are preserved.
- `src/ClickForge.Core/Settings.cs`: sensible initial dimensions; existing settings system retained.
- `src/ClickForge/UiDiagnostics.cs` and `src/ClickForge/UiDiagnostics.Daily.cs`: root-layout adaptation, accurate supported-display handling and added shell/inspector/profile checks.
- `README.md`, `docs/screenshot.png`, `docs/V1.2.0.md`, `docs/UPDATING.md`, this report: current navigation, appearance, screenshots and verification limits.

The full v1.2.0 candidate also includes the earlier tray/profile/accessibility files listed in `docs/V1.2.0.md`. The click scheduling engine, Roblox verifier and updater implementation have no functional changes from this visual pass. No existing user changes were discarded.

Release preparation left: review the source, complete remaining manual accessibility/display checks, then separately authorize publication through the existing strict matching-tag workflow. No published tag or asset was changed.

## Complete local candidate change list

33 files, including the preserved earlier v1.2.0 work:

- `README.md`
- `Version.props`
- `build.ps1`
- `docs/OBSIDIAN-REDESIGN.md`
- `docs/UPDATING.md`
- `docs/V1.2.0.md`
- `docs/screenshot.png`
- `scripts/Package-Release.ps1`
- `scripts/Test-UpdateProcess.ps1`
- `src/ClickForge.Core/ActivityHistory.cs`
- `src/ClickForge.Core/Profiles.cs`
- `src/ClickForge.Core/Settings.cs`
- `src/ClickForge.Core/UserData.cs`
- `src/ClickForge/App.xaml`
- `src/ClickForge/App.xaml.cs`
- `src/ClickForge/DesktopTargetService.cs`
- `src/ClickForge/DialogLayout.cs`
- `src/ClickForge/MainWindow.Daily.cs`
- `src/ClickForge/MainWindow.Shell.cs`
- `src/ClickForge/MainWindow.Updates.cs`
- `src/ClickForge/MainWindow.VariableCps.cs`
- `src/ClickForge/MainWindow.xaml`
- `src/ClickForge/MainWindow.xaml.cs`
- `src/ClickForge/PreferencesWindow.cs`
- `src/ClickForge/ProfilesWindow.cs`
- `src/ClickForge/SingleInstance.cs`
- `src/ClickForge/Themes/Controls.xaml`
- `src/ClickForge/TrayService.cs`
- `src/ClickForge/UiDiagnostics.Daily.cs`
- `src/ClickForge/UiDiagnostics.cs`
- `tests/ClickForge.Tests/ProfileTests.cs`
- `tests/ClickForge.Tests/Program.cs`
- `tests/ClickForge.UpdateTests/Program.cs`
