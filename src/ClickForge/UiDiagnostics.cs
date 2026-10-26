using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClickForge.Core;

namespace ClickForge;

// Explicit diagnostic mode exercises the actual controls with a fake mouse.
// It never sends mouse or keyboard input and uses its own settings file.
public partial class MainWindow
{
    private readonly DiagnosticMouse diagnosticMouse = new();
    private readonly DiagnosticTarget diagnosticTarget = new();
    private sealed class DiagnosticMouse : IMouseInput
    {
        public volatile bool Fail;
        public void Click() { if (Fail) throw new InvalidOperationException("Mock input failure"); }
        public void Click(ClickButton button) { ClickButtons.Validate(button); Click(); }
        public bool Supports(ClickButton button) => button != ClickButton.CustomKey && Enum.IsDefined(button);
        public bool Supports(ClickButton button, int key) => button == ClickButton.CustomKey ? KeyboardOutput.IsValid(key) : Supports(button);
        public void Click(ClickButton button, int key) { KeyboardOutput.ValidateOutput(button, key); Click(); }
        public void Release() { }
    }
    private sealed class DiagnosticTarget : ITargetGate { public volatile bool Allowed = true; public bool CanClick => Allowed; }
    private async Task RunUiSelfTestAsync(string output)
    {
        var results = new List<string>();
        int exit = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        void Hotkey(int id) { bool handled = false; WindowMessage(0, 0x0312, id, 0, ref handled); }
        try
        {
            Check(!engine.Enabled, "Startup never enables clicking");
            var inspectionFacts = new SelectedTargetFacts { HasSelection = true, ProcessAlive = true, WindowExists = true, IdentityMatches = true, Foreground = true };
            foreach (var invalid in new[] { inspectionFacts with { ProcessAlive = false }, inspectionFacts with { WindowExists = false }, inspectionFacts with { IdentityMatches = false }, inspectionFacts with { InspectionSucceeded = false } })
                Check(DesktopTargetService.RetainedTitle(invalid, "Original selection", () => throw new InvalidOperationException("Unrelated title read")) == "Original selection" && SelectedFocusText(invalid) == "Unavailable", "Invalid selected identity cannot read unrelated title or claim observed focus: " + TargetStatus.Selected(invalid).Reason);
            Check(DesktopTargetService.RetainedTitle(inspectionFacts, "Old title", () => "Current selected title") == "Current selected title" && SelectedFocusText(inspectionFacts) == "●  In focus", "Owned window metadata and focus remain observable despite other eligibility failures");
            UpdateStatus();
            Check(!engine.Session.HasStarted && SessionElapsedText.Text == "00:00:00" && SessionPairsText.Text == "0" && SessionAverageText.Text == "0.00", "Session dashboard starts at zero without activation");
            Check(ClickButtonInput.Items.Cast<string>().SequenceEqual(new[] { "Left", "Mouse 4 (Back)", "Mouse 5 (Forward)", "Custom input key…" }) && engine.Button == ClickButton.Left, "Click-button selector exposes Custom input key with Left unchanged by default");
            Hotkey(1); await Task.Delay(40); ClickButtonInput.SelectedIndex = 1;
            Check(!engine.Enabled && engine.Button == ClickButton.Mouse4 && settings.Button == ClickButton.Mouse4, "Changing click button stops the shared engine without automatic restart");
            ClickButtonInput.SelectedIndex = 2;
            Check(new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "ui-test-settings.json")).Load().Button == ClickButton.Mouse5, "Actual button selector persists Mouse 5 settings");
            ApplyProfile(TimingProfile.Capture(settings with { Button = ClickButton.Mouse4 }, "Button diagnostic"));
            Check(ClickButtonInput.SelectedIndex == 1 && engine.Button == ClickButton.Mouse4 && !engine.Enabled, "Applying a button profile synchronizes selector and engine while stopped");
            settings = settings with { ActiveProfileId = null }; ClickButtonInput.SelectedIndex = 0;
            CpsInput.Text = "19";
            Check(engine.Cps == 19 && CpsSlider.Value == 19 && IntervalText.Text.Contains("52.6"), "Numeric CPS updates slider, interval and actual engine");
            CpsInput.Text = "0";
            Check(engine.Cps == 19, "Invalid numeric CPS leaves last valid rate active");
            CpsSlider.Value = 50;
            Check(engine.Cps == 50 && CpsInput.Text == "50", "Slider updates engine and numeric field");
            ToggleModeInput.IsChecked = true;
            Hotkey(1); await Task.Delay(160);
            Check(engine.Enabled && engine.Clicks > 0, "Routed activation shortcut starts fake-input worker");
            Hotkey(2); long count = engine.Clicks; await Task.Delay(90);
            Check(engine.IsPaused && engine.Clicks == count, "Routed pause shortcut suspends cycles");
            var averageBeforeRender = engine.Session.MeasuredAverageCps;
            UpdateStatus();
            Check(engine.Session.TimerRunning && engine.Session.Elapsed.TotalSeconds >= .20 && SessionPairsText.Text == engine.Session.InputPairsSent.ToString("N0"), "Session dashboard shows sent pairs and includes pause time");
            double renderedAverage = double.Parse(SessionAverageText.Text);
            Check(renderedAverage <= averageBeforeRender + .005 && renderedAverage >= engine.Session.MeasuredAverageCps - .005, "Measured average dashboard binds to session calculation while wall-clock advances");
            Hotkey(2); await Task.Delay(70);
            Check(!engine.IsPaused && engine.Clicks > count, "Routed pause shortcut resumes cycles");
            EmergencyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!engine.Enabled, "Emergency button stops immediately");
            var stoppedSession = engine.Session; await Task.Delay(50); UpdateStatus();
            Check(engine.Session == stoppedSession && !stoppedSession.TimerRunning, "Emergency button freezes dashboard session statistics");
            Hotkey(1); await Task.Delay(50); Hotkey(3);
            Check(!engine.Enabled, "Routed emergency shortcut stops immediately");
            Check(engine.Session.Elapsed < stoppedSession.Elapsed && engine.Session.InputPairsSent < engine.Clicks, "Explicit activation resets session independently of run input count");
            HoldModeInput.IsChecked = true;
            Check(settings.Mode == ActivationMode.Hold && !engine.Enabled, "Hold segment changes mode without starting");
            var physicalGuard = engine.ActivationStillValid;
            engine.ActivationStillValid = () => false;
            Hotkey(1); await Task.Delay(120);
            Check(!engine.Enabled, "Simulated hold release ends activation");
            engine.ActivationStillValid = physicalGuard;
            ToggleModeInput.IsChecked = true;
            RestrictInput.IsChecked = false;
            Check(!settings.RobloxOnly && !engine.Enabled, "Restriction change stops and updates settings");
            RestrictInput.IsChecked = true;
            Check(settings.RobloxOnly && target.Restricted, "Roblox-only restriction restored");
            int previous = PauseInput.SelectedIndex; PauseInput.SelectedIndex = ActivateInput.SelectedIndex;
            ApplyClicked(this, new());
            Check(MessageText.Text.Contains("different key") && !engine.Enabled, "Apply shortcuts rejects conflicts");
            PauseInput.SelectedIndex = previous;
            // Use spare function keys; no key presses are generated.
            using (var nativeBindings = new HotkeyBindings(new GlobalHotkeyService(new WindowInteropHelper(this).Handle)))
            {
                try
                {
                    nativeBindings.Apply(new Settings { ActivateKey = 0x78, PauseKey = 0x79, StopKey = 0x7A });
                    results.Add("PASS Native RegisterHotKey and cleanup for F9/F10/F11");
                }
                catch (InvalidOperationException e) { results.Add("UNAVAILABLE Native shortcut reservation: " + e.Message + " Win32 error " + System.Runtime.InteropServices.Marshal.GetLastWin32Error()); }
            }
            // Reset and apply use a fake registrar so the user's running instance
            // can keep owning its shortcuts during UI diagnostics.
            bindings = new(new DiagnosticRegistrar());
            ResetClicked(this, new());
            Check(settings.Cps == 50 && settings.Mode == ActivationMode.Toggle && settings.RobloxOnly && settings.ActivateKey == 0x75 && !engine.Enabled, "Reset shortcuts preserves timing and target preferences while stopping input");
            ActivateInput.SelectedIndex = 11; PauseInput.SelectedIndex = 12; ApplyClicked(this, new());
            Check(settings.ActivateKey == 5 && settings.PauseKey == 6 && settings.StopKey == 0x77 && !engine.Enabled && StartKeyText.Text == "M4" && PauseKeyText.Text == "M5", "Mouse activation and pause shortcuts apply with correct labels and no automatic activation");
            Check(new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "ui-test-settings.json")).Load().ActivateKey == 5, "Mouse hotkey selection persists through real UI actions");
            Hotkey(1); await Task.Delay(50); Hotkey(2);
            Check(engine.Enabled && engine.IsPaused, "Mouse shortcut command IDs reuse activation and pause behavior with mock input");
            Hotkey(3); HoldModeInput.IsChecked = true; ClickButtonInput.SelectedIndex = 1; Hotkey(1);
            Check(!engine.Enabled && MessageText.Text.Contains("different click-output") && ModeHelp.Text.Contains("different click-output"), "Hold with matching mouse output is blocked with a clear explanation");
            ClickButtonInput.SelectedIndex = 0;
            Check(ModeHelp.Text.Contains("Mouse 4 (Back)"), "Hold instructions display the selected mouse shortcut correctly");
            ToggleModeInput.IsChecked = true; ResetClicked(this, new());
            var mouseService = new GlobalHotkeyService(new WindowInteropHelper(this).Handle);
            using (var mouseNative = new HotkeyBindings(mouseService))
            {
                try
                {
                    mouseNative.Apply(new Settings { ActivateKey = 5, PauseKey = 6, StopKey = 0x7A });
                    results.Add("PASS Native dedicated-thread mouse hook installation and mixed keyboard/mouse bindings");
                    nint generation = mouseService.MouseGeneration;
                    Check(generation != 0 && mouseService.AcceptMouseMessage(1,generation) && !mouseService.AcceptMouseMessage(99,generation), "Mouse listener accepts only current registered command IDs");
                    mouseNative.Dispose();
                    Check(!mouseService.AcceptMouseMessage(1,generation), "Disposed mouse shortcuts reject queued activation events");
                    mouseNative.Apply(new Settings { ActivateKey = 6, PauseKey = 5, StopKey = 0x7A });
                    Check(mouseService.MouseGeneration != generation && !mouseService.AcceptMouseMessage(1,generation), "Replacement listener rejects obsolete mouse shortcut generations");
                    results.Add("PASS Native mouse hook teardown and replacement with reversed mouse shortcuts");
                }
                catch (InvalidOperationException e) { results.Add("UNAVAILABLE Native mouse shortcut registration: " + e.Message); }
            }
            await RunCustomKeyDiagnosticsAsync(results,output);
            CpsInput.Text = "23"; ApplyClicked(this, new());
            Check(new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "ui-test-settings.json")).Load().Cps == 23, "Preferences persist through real UI actions");
            CpsInput.Text = "10"; UpdateStatus();
            VariableTimingInput.IsChecked = true;
            Check(settings.TimingMode == ClickTimingMode.Variable && engine.TimingMode == ClickTimingMode.Variable && !engine.Enabled, "Variable CPS selection updates existing engine without activation");
            UpdateStatus();
            Check(InspectorEligibilityText.Text.Contains("No target selected") && RobloxStatusText.Text == desktopTarget.Status.Label && !desktopTarget.Status.ChecksPassed, "Unselected variable target explains blocking without claiming readiness");
            Check(VariableSpeedPanel.Visibility == Visibility.Visible && FixedSpeedPanel.Visibility == Visibility.Collapsed && !RestrictInput.IsEnabled && settings.RobloxOnly, "Variable controls preserve fixed Roblox restriction preference");
            Check(MinimumCpsInput.Text == "5" && MaximumCpsInput.Text == "20", "Production variable defaults are 5 to 20 CPS");
            MinimumCpsInput.Text = "30";
            Hotkey(1);
            Check(!engine.Enabled && VariableValidationText.Text.Contains("cannot exceed"), "Invalid variable range blocks routed activation");
            MinimumCpsInput.Text = "0";
            Check(VariableValidationText.Text.Contains("1 to 50") && !engine.Enabled, "Variable numeric bounds have accessible feedback");
            MinimumCpsInput.Text = "5"; MaximumCpsInput.Text = "25";
            Check(settings.MaximumCps == 25 && MaximumCpsSlider.Value == 25 && ConfiguredCpsText.Text == "5–25", "Valid variable text synchronizes slider, persisted range and dashboard");
            MaximumCpsSlider.Value = 3;
            Check(settings.MinimumCps == 3 && settings.MaximumCps == 3 && MinimumCpsInput.Text == "3", "Crossing variable sliders keeps an ordered equal-endpoint range");
            MinimumCpsSlider.Value = 5; MaximumCpsSlider.Value = 20;
            Hotkey(1);
            Check(!engine.Enabled && MessageText.Text.Contains("target window"), "Variable activation cannot fall back to fixed diagnostic target");
            Check(new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "ui-test-settings.json")).Load().TimingMode == ClickTimingMode.Variable, "Variable speed mode persists through actual UI controls");
            Width = 1280; Height = 920; await Task.Delay(100); UpdateLayout();
            RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-variable-cps.png"));
            Check(((System.Windows.Media.SolidColorBrush)MinimumCpsInput.Foreground).Color == ((System.Windows.Media.SolidColorBrush)FindResource("Text")).Color, "Variable numeric input uses dark theme text contrast");
            Check(!RobloxIdentity.IsVerified(Environment.ProcessPath!, System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!)), "Roblox verification rejects the non-Roblox ClickForge executable");
            using (var noSelection = new DesktopTargetService())
                Check(!noSelection.CanActivate && !noSelection.CanClick, "Native desktop gate fails closed without explicit selection");
            FixedTimingInput.IsChecked = true;
            Check(RestrictInput.IsEnabled && settings.RobloxOnly && engine.TimingMode == ClickTimingMode.Fixed && !engine.Enabled, "Returning to fixed mode restores original restrictions without activation");
            Width = 1280; Height = 920; await Task.Delay(100); UpdateLayout();
            RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-default.png"));
            var scroll = DashboardScroll;
            results.Add($"DISPLAY requested {Width}x{Height}; actual {ActualWidth}x{ActualHeight}; viewport {scroll.ViewportWidth}x{scroll.ViewportHeight}; overflow {scroll.ScrollableHeight}; work area {SystemParameters.WorkArea}; DPI {VisualTreeHelper.GetDpi(this).DpiScaleX}");
            if (ActualHeight >= 919)
                Check(scroll.ScrollableHeight == 0, "Default layout fits all controls without scrolling");
            else
            {
                results.Add("UNAVAILABLE Full 900-DIP window: Windows capped its height on this display; full-size layout must also be verified on a larger desktop.");
                Check(scroll.ScrollableHeight > 0, "Display-capped layout provides intentional scrolling");
            }
            CpsInput.Focus(); await Task.Delay(30);
            RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-focus.png"));
            ShortcutsExpander.IsExpanded = true; UpdateLayout();
            ActivateInput.IsDropDownOpen = true; await Task.Delay(70); ActivateInput.ApplyTemplate();
            var popup = (System.Windows.Controls.Primitives.Popup)ActivateInput.Template.FindName("PART_Popup", ActivateInput);
            Check(popup.IsOpen && popup.Child is FrameworkElement, "Dark shortcut popup opens");
            RenderDiagnostic((FrameworkElement)popup.Child!, Path.Combine(Path.GetDirectoryName(output)!, "ui-dropdown.png"));
            ActivateInput.IsDropDownOpen = false; int keyboardSelection = ActivateInput.SelectedIndex;
            ActivateInput.SelectedIndex = 11; ActivateInput.IsDropDownOpen = true; await Task.Delay(50);
            Check(ActivateInput.Items.Cast<string>().TakeLast(2).SequenceEqual(new[] { "Mouse 4 (Back)", "Mouse 5 (Forward)" }) && popup.IsOpen, "Actual dark hotkey popup exposes both mouse buttons");
            RenderDiagnostic((FrameworkElement)popup.Child!, Path.Combine(Path.GetDirectoryName(output)!, "ui-mouse-hotkeys-dropdown.png"));
            ActivateInput.IsDropDownOpen = false; ActivateInput.SelectedIndex = keyboardSelection;
            ActivateInput.IsDropDownOpen = false; ShortcutsExpander.IsExpanded = false;
            Width = 640; Height = 700; await Task.Delay(100); UpdateLayout();
            RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-small.png"));
            Check(Grid.GetRow(RestrictionPanel) == 1 && scroll.ScrollableHeight > 0, "Narrow layout stacks options and scrolls intentionally");
            scroll.ScrollToEnd(); await Task.Delay(70);
            Check(scroll.VerticalOffset > 0, "Styled scrollbar reaches lower controls");
            RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-small-bottom.png"));
            using (var test = new DiagnosticTestWindowScope(this))
            {
                Check(test.Window.IsVisible, "Controlled test window opens and closes");
                test.Window.ButtonSelector.SelectedIndex = 1;
                Check(!test.Window.FixedTestEnabled && test.Window.ButtonSelector.Items[1].ToString() == "Mouse 4 (Back)", "Safe test offers side buttons without starting input on selection");
            }
            Check(SafeTestModeInput.SelectedIndex == 0, "Safe test defaults to existing fixed-CPS profile");
            SafeTestModeInput.SelectedIndex = 1;
            Check(!engine.Enabled && engine.Cps == 10, "Variable profile selection leaves production settings and activation unchanged");
            bool openedSelectedProfile = false;
            _ = Dispatcher.BeginInvoke(new Action(() => { openedSelectedProfile = activeTest?.ModeSelector.SelectedIndex == 1; activeTest?.Close(); }));
            TestClicked(this, new());
            Check(openedSelectedProfile && activeTest is null && !engine.Enabled, "Open test action passes selected profile and closes with live engine stopped");
            activeTest = new TestWindow(SafeTestMode.VariableTiming) { Owner = this, ShowActivated = false };
            try
            {
                activeTest.Show();
                Check(activeTest.ModeSelector.SelectedIndex == 1, "Variable safe test opens with correct profile");
                activeTest.MinimumInput.Text = "21"; activeTest.StartTest();
                Check(!activeTest.VariableSnapshot.Running, "Safe test rejects out-of-range CPS");
                activeTest.MinimumInput.Text = "20"; activeTest.MaximumInput.Text = "5"; activeTest.StartTest();
                Check(!activeTest.VariableSnapshot.Running, "Safe test rejects reversed range");
                activeTest.MinimumInput.Text = "5"; activeTest.MaximumInput.Text = "20";
                activeTest.SeedInput.Text = "invalid"; activeTest.StartTest();
                Check(!activeTest.VariableSnapshot.Running, "Safe test rejects invalid seed");
                activeTest.SeedInput.Text = "42"; activeTest.StartTest(); await Task.Delay(350);
                Check(activeTest.VariableSnapshot.Events.Length > 0 && !activeTest.FixedTestEnabled, "Variable UI produces logical events without activating fixed input");
                RenderDiagnostic(activeTest, Path.Combine(Path.GetDirectoryName(output)!, "ui-variable-test.png"));
                long liveCount = engine.Clicks;
                Hotkey(1); Hotkey(2); ToggleStart();
                Check(!engine.Enabled && engine.Clicks == liveCount, "Live hotkeys and start action cannot enable production engine during safe test");
                HoldModeInput.IsChecked = true; Hotkey(1);
                Check(!engine.Enabled, "Hold activation is also blocked during safe test");
                Hotkey(3); int simulatedCount = activeTest.VariableSnapshot.Events.Length; await Task.Delay(220);
                Check(!activeTest.VariableSnapshot.Running && activeTest.VariableSnapshot.Events.Length == simulatedCount, "Emergency shortcut cancels safe simulation with no further events");
                activeTest.ModeSelector.SelectedIndex = 0;
                Check(!activeTest.FixedTestEnabled && !activeTest.VariableSnapshot.Running, "Switching safe-test profiles never automatically starts input");
            }
            finally { activeTest.Close(); activeTest = null; ToggleModeInput.IsChecked = true; SafeTestModeInput.SelectedIndex = 0; }
            Check(!engine.Enabled, "UI checks finish stopped");
            UpdatesClicked(this, new());
            Check(updateWindow is { IsVisible: true } && VersionLabel.Text == "v" + InstalledVersion.Value.Split('+')[0], "Updates window opens with assembly-derived version");
            var updatePanel = (StackPanel)((ScrollViewer)updateWindow!.Content).Content;
            var autoCheck = updatePanel.Children.OfType<CheckBox>().Single();
            autoCheck.IsChecked = true; Check(settings.CheckForUpdatesOnStartup && !engine.Enabled, "Startup update preference persists without activation");
            autoCheck.IsChecked = false;
            foreach (var state in Enum.GetValues<ClickForge.Core.Updates.UpdateState>())
            {
                updateClient.Report(state, "Diagnostic update status"); await Task.Delay(15);
                Check(updateWindow.DisplayedStatus.Contains("Diagnostic update status"), "Updater displays " + state);
            }
            RenderDiagnostic(updateWindow, Path.Combine(Path.GetDirectoryName(output)!, "ui-updates.png"));
            updateWindow.Close(); updateClient.Report(ClickForge.Core.Updates.UpdateState.Idle, "Check for a newer stable release.");
            Width = 1280; Height = 920;
            await RunDailyDiagnosticsAsync(results, output);
            await RunLightweightDiagnosticsAsync(results);
            await RunAdvancedDiagnosticsAsync(results, output);
            Width = 1280; Height = 920;
            await Task.Delay(1500); // Exclude resize, popup and software-render warmup.
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            async Task Measure(string label)
            {
                process.Refresh(); var cpu = process.TotalProcessorTime; var clock = System.Diagnostics.Stopwatch.StartNew();
                await Task.Delay(2000); process.Refresh();
                results.Add($"MEASURE {label}: {(process.TotalProcessorTime - cpu).TotalMilliseconds / clock.Elapsed.TotalMilliseconds * 100:F2}% of one CPU core; working set {process.WorkingSet64 / 1048576d:F1} MiB; private {process.PrivateMemorySize64 / 1048576d:F1} MiB; {clock.Elapsed.TotalSeconds:F3}s sample");
            }
            await Measure("WPF idle");
            ToggleModeInput.IsChecked = true; Hotkey(1);
            await Measure("WPF + 10 CPS scheduler (mocked mouse input)");
            Hotkey(3);
        }
        catch (Exception e) { results.Add("FAIL " + e); exit = 1; }
        finally
        {
            engine.Stop();
            results.Add("NOTE Mouse input mocked. Routed shortcut tests do not prove physical global-key delivery.");
            if (exit == 0) { ShowCompact(); ShowPalette(); engine.Start(); results.Add("PASS Exit requested while the mock click worker is active"); }
            PrepareExit();
            ExecuteTrayCommand(TrayCommand.Exit); ExecuteTrayCommand(TrayCommand.Exit);
            if (closing && !engine.Enabled && tray is null) results.Add("PASS Explicit and repeated Exit complete shared engine, hotkey and tray cleanup");
            else { results.Add("FAIL Exit cleanup incomplete"); exit = 1; }
            if (compactWindow is null && paletteWindow is null) results.Add("PASS Shutdown closes compact controls and command palette through shared cleanup");
            else { results.Add("FAIL Advanced windows survived shutdown"); exit = 1; }
            results.Add(exit == 0 ? "RESULT UI checks completed successfully" : "RESULT UI checks failed");
            File.WriteAllLines(output, results);
            Application.Current.Shutdown(exit);
        }
    }
    private static void RenderDiagnostic(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }
    private sealed class DiagnosticRegistrar : IHotkeyRegistrar { public bool Register(int id, int key) => true; public void Unregister(int id) { } }
    private sealed class DiagnosticTestWindowScope : IDisposable
    {
        public TestWindow Window { get; }
        public DiagnosticTestWindowScope(Window owner) { Window = new() { Owner = owner }; Window.Show(); }
        public void Dispose() => Window.Close();
    }
}
