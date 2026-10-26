using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunAdvancedDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        async Task Until(Func<bool> value) { for (int n = 0; n < 100 && !value(); n++) await Task.Delay(20); Check(value(), "Advanced worker condition completed within timeout"); }
        void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        string root = Path.GetDirectoryName(output)!;
        engine.Stop(); ToggleModeInput.IsChecked = true; FixedTimingInput.IsChecked = true; ClickButtonInput.SelectedIndex = 0;
        ShortcutsExpander.IsExpanded = true; advancedControls.IsExpanded = true; UpdateStatus();
        Check(AutomationProperties.GetName(durationValue).Length > 0 && AutomationProperties.GetName(safetyEvents).Length > 0, "Advanced limits and private history expose accessible labels");
        durationLimitInput.IsChecked = true; durationValue.Text = "0"; ApplyLimits();
        Check(!engine.Enabled && engine.Limits == default && limitValidation.Text.Contains("last valid"), "Invalid limit UI retains applied configuration and stops input");
        durationLimitInput.IsChecked = false; pairLimitInput.IsChecked = true; pairValue.Text = "3"; ApplyLimits(); CpsInput.Text = "50";
        Check(settings.MaximumSessionPairs == 3 && engine.Limits.InputPairs == 3 && !engine.Enabled, "Applied pair limit persists and never activates input");
        Check(new SettingsService(Path.Combine(root, "ui-test-settings.json")).Load().MaximumSessionPairs == 3, "Actual session limit controls round-trip through settings persistence");
        ToggleStart(); await Until(() => !engine.Enabled); UpdateStatus();
        Check(engine.Session.InputPairsSent == 3 && limitSummary.Text.Contains("Automatically stopped") && !engine.IsPaused, "Mock input auto-stops exactly at pair limit and dashboard explains remaining progress");
        var frozen = engine.Session; ApplyLimits(); Check(engine.Session == frozen, "Reapplying limits preserves prior session progress without activation");
        pairLimitInput.IsChecked = false; ApplyLimits(); CpsInput.Text = "10";
        ShowCompact(); var compact = compactWindow!;
        Check(compact.IsVisible && !engine.Enabled && compact.DisplayedState == "Stopped" && !compact.PauseControl.IsEnabled, "Compact panel opens with genuine stopped state and disabled pause");
        double oldLeft = compact.Left; compact.Left += 12; Check(compact.Left == oldLeft + 12, "Compact control window is movable");
        Press(compact.StartControl); await Until(() => engine.Clicks > frozen.InputPairsSent + 1 && engine.State == EngineState.Running); UpdateStatus();
        Check(engine.Enabled && compact.DisplayedState == "Clicking", "Compact Start uses shared mock worker and real engine state");
        Press(compact.PauseControl); UpdateStatus(); Check(engine.IsPaused && compact.PauseControl.Content as string == "Resume", "Compact Pause reflects genuine resumable state");
        Press(compact.PauseControl); await Task.Delay(50); Press(compact.EmergencyControl);
        Check(!engine.Enabled && engine.Timeline.Entries.Last().Kind == SessionEventKind.EmergencyStop, "Compact Emergency Stop shares release barrier and safety history");
        diagnosticTarget.Allowed = false; Press(compact.StartControl); await Until(() => engine.State == EngineState.Paused); UpdateStatus();
        Check(compact.DisplayedState == "Waiting for target" && engine.Enabled, "Compact waiting status comes from actual mock target gating instead of readiness assumptions");
        Press(compact.EmergencyControl); diagnosticTarget.Allowed = true; diagnosticMouse.Fail = true;
        Press(compact.StartControl); await Until(() => engine.State == EngineState.Error); UpdateStatus();
        Check(compact.DisplayedState == "Error" && !compact.PauseControl.IsEnabled && !engine.Enabled, "Compact input failure displays genuine error with disabled pause");
        diagnosticMouse.Fail = false; Press(compact.StartControl); await Until(() => engine.State == EngineState.Running); Press(compact.EmergencyControl);
        RenderDiagnostic(compact, Path.Combine(root, "ui-compact-controls.png")); compact.Close();
        Check(compactWindow is null && !engine.Enabled, "Closing compact controls does not start or resume input");
        ToggleStart(); await Task.Delay(30); ShowCompact();
        Check(!engine.Enabled, "Showing compact controls stops an existing request to avoid focus-induced resume");
        Press(compactWindow!.StartControl); await Task.Delay(30); compactWindow.Close();
        Check(!engine.Enabled, "Closing compact controls stops active input instead of indirectly resuming a target");

        ShowPalette(); var palette = paletteWindow!; long count = engine.Clicks;
        palette.Search.Text = "cps 0"; Check(palette.Results.Items.Count == 0 && settings.Cps == 10, "Palette rejects invalid speed without applying configuration");
        palette.Search.Text = "cps 25"; palette.Results.SelectedIndex = 0; await Task.Delay(40);
        Check(palette.Results.Items.Count == 1 && !engine.Enabled && engine.Clicks == count && settings.Cps == 10, "Opening searching and highlighting palette results execute no input or configuration actions");
        RenderDiagnostic(palette, Path.Combine(root, "ui-command-palette.png")); palette.Close();
        Check(!ApplyPaletteCps(25, () => false) && settings.Cps == 10, "Declined palette speed confirmation makes no configuration change");
        ToggleStart(); await Task.Delay(30);
        Check(ApplyPaletteCps(25, () => true) && engine.Cps == 25 && !engine.Enabled, "Confirmed palette speed command validates persists and stops shared worker");
        Check(new SettingsService(Path.Combine(root, "ui-test-settings.json")).Load().Cps == 25, "Confirmed palette CPS persists its actual applied engine configuration");
        var profile = profiles.Create(settings with { Cps = 17 }, "Palette UI profile");
        string mockKey = ApplicationProfiles.Identity(Path.GetFullPath(Path.Combine(root, "mock-application.exe")));
        applicationProfiles.Associate(mockKey, profile.Id); RenderAssociationSuggestion(mockKey);
        Check(suggestedProfile == profile.Id && !engine.Enabled && desktopTarget.Snapshot is null && settings.ActiveProfileId != profile.Id,
            "Mock application identity suggests a saved profile without applying it starting input or restoring a target");
        bool invalidTarget = false; try { ApplySuggestedProfile(); } catch (InvalidOperationException) { invalidTarget = true; }
        Check(invalidTarget && !engine.Enabled && desktopTarget.Snapshot is null, "Suggested profile cannot bypass the real selected-target validation guard");
        applicationProfiles.Remove(mockKey); RefreshAssociationSuggestion();
        ShowPalette(); paletteWindow!.Search.Text = "Apply profile: Palette UI profile"; paletteWindow.ExecuteSelected();
        Check(settings.ActiveProfileId == profile.Id && settings.Cps == 17 && !engine.Enabled && desktopTarget.Snapshot is null && settings.RobloxOnly, "Explicit palette profile execution preserves target restrictions and never restores a target or activation");
        profiles.Delete(profile.Id); settings = settings with { ActiveProfileId = null }; UpdateProfileLabel();
        ShowPalette(); string selectedTitle = desktopTarget.SelectedTitle; ShowCompact(); UpdateStatus();
        Check(!compactWindow!.StartControl.IsEnabled && !StartButton.IsEnabled && !engine.Enabled, "Palette configuration blocker is shared by dashboard compact panel and validated activation");
        paletteWindow!.Close(); ToggleStart(); await Task.Delay(30); ShowPalette(); paletteWindow!.Search.Text = "Emergency stop all input"; paletteWindow.ExecuteSelected();
        Check(!engine.Enabled && engine.LastStopReason == SessionEventKind.EmergencyStop && desktopTarget.SelectedTitle == selectedTitle, "Explicit palette emergency stop cannot mutate selected target and stops existing request");
        compactWindow!.Close();
        bool rejected = false; try { AssociateCurrentProfile(); } catch (ArgumentException) { rejected = true; }
        Check(rejected && suggestedProfile is null && !engine.Enabled, "Application association UI refuses missing saved profile and missing selected target");
        UpdateStatus(); Check(safetyEvents.Items.Count > 0, "Private safety view reads actual engine events");
        var content = (StackPanel)advancedControls.Content;
        Press(content.Children.OfType<Button>().Single(b => b.Content as string == "Clear safety timeline"));
        Check(engine.Timeline.Entries.Length == 0 && safetyEvents.Items.Count == 0, "Clear timeline button clears bounded in-memory history and visible entries");
        using (var test = new DiagnosticTestWindowScope(this))
        {
            var window = test.Window; window.ModeSelector.SelectedIndex = 1; window.MinimumInput.Text = "20"; window.MaximumInput.Text = "20"; window.SeedInput.Text = "42";
            long before = engine.Clicks; window.StartTest(); await Until(() => window.ScopeSampleCount >= 3);
            Check(window.ScopeSampleCount > 0 && window.ScopeSummary.ScheduledMean == 50 && window.ScopeSummary.MeanDelay >= 0 && !engine.Enabled && engine.Clicks == before,
                "Safe-test scope draws scheduled and measured logical samples without activating live input");
            RenderDiagnostic(window, Path.Combine(root, "ui-timing-scope.png")); window.StopTest(); int samples = window.VariableSnapshot.Events.Length; await Task.Delay(80);
            Check(!window.VariableSnapshot.Running && window.VariableSnapshot.Events.Length == samples, "Scope simulation cancellation stops further logical events");
        }
        CpsInput.Text = "10"; durationLimitInput.IsChecked = pairLimitInput.IsChecked = false; ApplyLimits();
        RenderDiagnostic(this, Path.Combine(root, "ui-advanced-controls.png"));
        RenderDiagnostic((FrameworkElement)advancedControls.Content, Path.Combine(root, "ui-advanced-features.png"));
        advancedControls.IsExpanded = false; ShortcutsExpander.IsExpanded = false; DashboardScroll.ScrollToTop(); UpdateStatus();
    }
}
