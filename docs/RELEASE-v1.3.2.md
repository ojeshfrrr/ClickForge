# ClickForge v1.3.2

This update reduces dashboard work while preserving the existing click worker,
input outputs, target restrictions, profiles, shortcuts, updater and shutdown.

- Idle dashboard refreshes once per second instead of four times per second.
- Active dashboard refreshes twice per second instead of four times per second.
- Hidden/minimized windows skip dashboard rendering while tray state and global
  commands remain available. Hiding to tray still stops input.
- Unchanged session/target text is cached; target observations are refreshed
  independently from their visual representation. Duplicate status writes and
  an extra UI-only target check were removed.
- Commands, window restoration and closing configuration views update controls
  immediately. Restoring never automatically starts input.
- Adds an explicit mock-input performance diagnostic and 12 UI regression checks.

The click scheduler, live target validation, input-release safeguards and
emergency stop remain unchanged. No native input was sent into Roblox during
verification. This release does not claim anti-cheat evasion or target acceptance.

## Downloads

- Installer: `ClickForge-Setup-v1.3.2.exe` for a per-user Windows x64 installation.
- Portable: extract the complete `ClickForge-v1.3.2-win-x64.zip`, keeping the
  updater helper alongside the application.
- Existing users can check Updates and confirm installation/restart. Settings
  and profiles are retained; clicking starts disabled after restart.

## Verification and limits

Local optimization verification passed Release builds with zero warnings/errors,
136 core tests, 36 updater tests, and 172 WPF UI checks. The tagged release workflow
also gates publication on its own Release/test run, exact package verification,
updater helper success/rollback tests and the real installer lifecycle.

Matched short samples showed roughly half the active layout passes and less idle
managed allocation. CPU varied between samples; hidden CPU did not consistently
improve. A substantial RAM or package-size reduction was not established. The
WPF runtime is still bundled. Higher CPU after the screenshot-heavy full UI
diagnostic remains uncharacterized. See [the measurements, exact changed files
and verification details](https://github.com/ojeshfrrr/ClickForge/blob/v1.3.2/docs/LIGHTWEIGHT.md).

Physical input delivery, live-game acceptance, screen-reader sessions and actual
multi-monitor DPI transitions were not verified. Downloads remain unsigned;
checksums verify integrity rather than publisher authentication.
