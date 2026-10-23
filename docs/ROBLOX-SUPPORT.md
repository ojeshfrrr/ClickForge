# Roblox Variable CPS support — 1.1.2

The earlier 1.1.1 target picker intentionally excluded Roblox from Variable CPS. At the user's request, 1.1.2 allows explicitly selected, verified Roblox clients.

Open Roblox, select **Variable CPS**, click **Refresh windows**, and choose the Roblox window. Set the rate limits, then use **Start clicking** and focus Roblox, or press the activation shortcut while Roblox is in front. Keep the pointer inside its window. Pause/resume and the configured emergency stop remain available. Nothing starts automatically after launch or an update.

Both timing modes share the original Roblox executable-name, company, Authenticode trust and certificate-publisher check. The variable picker revalidates the selected process and retains its original process handle. Unknown or unverifiable identities are excluded. Variable CPS always requires the exact selected foreground window and the pointer inside it; the fixed-mode restriction preference remains unchanged. Target, mode or range changes stop input and require explicit activation.

No input injection into the process, memory access, driver or anti-cheat bypass was added. The feature uses the existing Windows input service. Listing a verified Roblox window and successfully sending Windows input do not establish whether a particular Roblox experience accepts synthetic clicks.

Changed files: `src/ClickForge.Core/ClickTiming.cs`, `src/ClickForge.Core/ClickEngine.cs`, `src/ClickForge/RobloxIdentity.cs` (new), `src/ClickForge/ForegroundTargetService.cs`, `src/ClickForge/DesktopTargetService.cs`, `src/ClickForge/MainWindow.VariableCps.cs`, `src/ClickForge/MainWindow.xaml`, `src/ClickForge/UiDiagnostics.cs`, `tests/ClickForge.Tests/Program.cs`, `README.md`, `scripts/Package-Release.ps1`, `Version.props`, and this document.

The core regression suite covers verified/unverified eligibility and selected-target focus loss with mocked input. Native UI diagnostics check that the shared Roblox verifier rejects ClickForge itself. The complete release build, core/updater/UI suites and release workflow must pass before publication. No live Roblox session was available during local verification, and no live game input was generated. Live client discovery and click acceptance remain manual checks.

The older implementation report documents the original 1.1.1 restrictions and verification; this document supersedes its Roblox-exclusion behavior for 1.1.2.
