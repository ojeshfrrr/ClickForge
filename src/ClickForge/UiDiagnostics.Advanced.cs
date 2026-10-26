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
        CpsInput.Text = "10"; durationLimitInput.IsChecked = pairLimitInput.IsChecked = false; ApplyLimits();
        RenderDiagnostic(this, Path.Combine(root, "ui-advanced-controls.png"));
        RenderDiagnostic((FrameworkElement)advancedControls.Content, Path.Combine(root, "ui-advanced-features.png"));
        advancedControls.IsExpanded = false; ShortcutsExpander.IsExpanded = false; DashboardScroll.ScrollToTop(); UpdateStatus();
    }
}
