# Mouse 4 and Mouse 5 as global shortcuts

> Historical implementation-stage verification record. For the published feature summary, see [v1.3.0 release notes](RELEASE-v1.3.0.md).

This extends the previous selectable click-output buttons. All three Global shortcuts dropdowns now offer F1–F11, Mouse 4 (Back) and Mouse 5 (Forward). Scroll below F11 to find the mouse choices, then choose Apply shortcuts. Defaults remain F6 activation, F7 pause/resume and F8 emergency stop. Reset restores those shortcuts without resetting timing, click-output or target preferences. Global shortcuts remain application settings, not profile data.

Mouse keys persist as Windows virtual-key values 5 and 6. Labels, action keycaps (M4/M5 with full tooltips) and activation instructions reflect the selection. Duplicate shortcuts are rejected before replacing registrations. Failed keyboard or mouse registration rolls back reserved shortcuts, keeps input stopped and displays an error. Applying settings never activates input.

The mouse listener runs WH_MOUSE_LL on one dedicated message-pump thread only when mouse shortcuts are selected. Microsoft recommends a [dedicated thread and quick callback](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) for this API; the hook runs in ClickForge, not in the target process. It observes only side-button down/up state, retains no pointer coordinates or application content, and always passes events onward. Normal Back/Forward actions are not suppressed. Injected and lower-integrity injected events cannot activate, pause, emergency-stop, or change the physical hold tracker, preventing generated click output from triggering shortcuts.

Physical down events post the same command IDs used by keyboard shortcuts. A separate message and listener-generation token reject queued commands from disposed/replaced mouse bindings. No click engine or UI work occurs in the hook callback. Dispose unregisters the listener and stops its dedicated message pump; replacing shortcuts clears held state. Existing click scheduling, target gates, input-release safeguards, shutdown and safe-test command blocking are preserved.

Hold requires both observed physical down state and the Windows asynchronous button state, plus the existing foreground condition. This conservatively stops if the physical release event is missed. Hold cannot use the same button for activation and generated output: synthesized up events would affect Windows' button state. This combination is refused at activation with a clear explanation, while Toggle can use matching buttons safely because injected events are ignored.

The corrupted expanded-arrow glyph visible in the screenshot was also replaced with a Unicode numeric entity. Existing dark reusable styles are unchanged.

## Files changed for this follow-up

- `src/ClickForge.Core/ShortcutKeys.cs`: choices, labels, Hold conflict detection and thread-safe physical-event classifier.
- `src/ClickForge.Core/Settings.cs`: validate mouse shortcuts and produce accurate registration errors.
- `src/ClickForge/Native.cs`: hook declarations and mixed keyboard/mouse registrar with stale-command checks.
- `src/ClickForge/MouseShortcutHook.cs`: dedicated native hook thread, injection filtering, queued commands and cleanup.
- `src/ClickForge/MainWindow.xaml.cs`: dropdown population, selection persistence, labels, held-button checking and command routing.
- `src/ClickForge/MainWindow.VariableCps.cs`: refuse unsafe matching activation/output in Hold mode.
- `src/ClickForge/Themes/Controls.xaml`: correct expanded-arrow encoding.
- `src/ClickForge/UiDiagnostics.cs`: dropdown, persistence, activation/pause, Hold restriction, native listener lifecycle and stale-event checks.
- `tests/ClickForge.Tests/MouseShortcutTests.cs` and `Program.cs`: 11 classifier/settings/engine regression tests and registration.
- `build.ps1`: optional BuildDirectory to verify without overwriting a running application; the default remains artifacts/ui-build.
- `README.md` and this report: usage and verification.

## Verification

The first build attempt failed because a running copy locked the old artifacts/ui-build binaries. No application was terminated to resolve it. Verification was moved to a fresh output folder using the new BuildDirectory option.

- Executed `./build.ps1 -SkipPackage -BuildDirectory artifacts/mouse-hotkeys-final`: Release application/helper builds passed with zero warnings/errors; **124 core tests, 36 updater tests and 149 actual WPF checks passed**. Log: `artifacts/logs/mouse-hotkeys-verified.txt`.
- Native Windows listener installation, mixed keyboard/mouse registration, teardown/replacement, accepted command IDs and obsolete-generation rejection passed. Physical mouse event classification, Hold release, injected-event rejection, duplicate bindings, registration rollback and persisted settings were tested using mocks.
- The actual dropdown was opened and captured with Mouse 4/5 visible; screenshot: `artifacts/logs/ui-mouse-hotkeys-dropdown.png`. Default/narrow layout, keycap labels, instructions, settings persistence, keyboard regressions, routed emergency stop and shutdown checks passed. Native shortcut installation does not prove physical key/button delivery.
- `git diff --check` passed. Existing installer/helper lifecycle checks from the previous click-output feature were not rerun for this hotkey-only follow-up, and no new release package was built/published. The SDK compiles the listener into the existing app payload; there is no new package or runtime dependency.
- After the old application was closed, the verified 13-file build was copied into the existing local `artifacts/ui-build` directory. Every copied file's SHA-256 matched the verified build. Previous binaries were backed up to `artifacts/local-hotkeys-backup-406bfb39ab764290b6154fffbd39d81b`. No settings/profile file was replaced. Log: `artifacts/logs/mouse-hotkeys-local-update.txt`. The updated local app was opened and remained running after startup. This refresh applies to the local build, not another installer/portable copy elsewhere on disk.

Physical gaming-mouse presses and real hardware/driver remapping were not exercised. Mocked physical events establish classifier and command behavior, not physical global delivery. The native hook can be installed and removed without foreground input, so that check is distinct from the earlier native output-pad test that Windows blocked. Generated clicks remain marked as injected; no masking or anti-cheat evasion was added. Target validation and input submission remain separate OS calls, and no application acceptance guarantee is made.
