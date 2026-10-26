using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunLightweightDiagnosticsAsync(List<string> results)
    {
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            results.Add("PASS " + name);
        }
        engine.Stop(); FixedTimingInput.IsChecked = true; ToggleModeInput.IsChecked = true;
        UpdateStatus();
        Check(refresh.Interval == TimeSpan.FromSeconds(1), "Idle dashboard uses reduced refresh cadence");

        // Observe actual dependency-property changes, including transient writes
        // replaced later in the same refresh (a source of unnecessary WPF work).
        int mutations = 0;
        EventHandler changed = (_, _) => mutations++;
        var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        TextBlock[] observed = [StatusText, StateHintText, RobloxStatusText, TargetText, SessionElapsedText, SessionPairsText, SessionAverageText];
        foreach (var text in observed) descriptor.AddValueChanged(text, changed);
        try
        {
            for (int n = 0; n < 20; n++) UpdateStatus();
            Check(mutations == 0, "Unchanged idle refreshes do not rewrite target or session text");
        }
        finally { foreach (var text in observed) descriptor.RemoveValueChanged(text, changed); }

        RestrictInput.IsChecked = false; UpdateStatus();
        Check(RobloxStatusText.Text == new TargetStatus(TargetBlockReason.RestrictionOff).Label,
            "Inspector cache invalidates on restriction changes without a false verified claim");
        VariableTimingInput.IsChecked = true; UpdateStatus();
        Check(RobloxStatusText.Text == new TargetStatus(TargetBlockReason.NoSelection).Label,
            "Inspector cache invalidates on timing-mode change and shows missing target");
        VariableValidationText.Text = "Diagnostic validation feedback"; UpdateStatus();
        Check(InspectorFeedback.Text == VariableValidationText.Text && InspectorFeedback.Visibility == Visibility.Visible,
            "Changed validation feedback reaches cached inspector");
        VariableValidationText.Text = ""; FixedTimingInput.IsChecked = true; RestrictInput.IsChecked = true;

        engine.Start(); UpdateStatus();
        Check(refresh.Interval == TimeSpan.FromMilliseconds(500), "Visible active statistics refresh twice per second independently of worker timing");
        string pairs = SessionPairsText.Text, elapsed = SessionElapsedText.Text;
        Hide(); await Task.Delay(150); UpdateStatus();
        Check(refresh.Interval == TimeSpan.FromSeconds(1) && SessionPairsText.Text == pairs && SessionElapsedText.Text == elapsed,
            "Hidden dashboard skips offscreen statistics while mock worker continues");
        bool handled = false; WindowMessage(0, 0x0312, 3, 0, ref handled);
        Check(handled && !engine.Enabled, "Emergency stop remains immediate while dashboard rendering is suspended");
        Show(); UpdateStatus();
        Check(SessionPairsText.Text == engine.Session.InputPairsSent.ToString("N0") && StatusText.Text == "Stopped" && !engine.Enabled,
            "Restoring dashboard immediately shows final session and stopped state without activation");
        WindowState = WindowState.Minimized; UpdateStatus();
        Check(refresh.Interval == TimeSpan.FromSeconds(1) && !engine.Enabled, "Ordinary minimization reduces refresh work without activation");
        WindowState = WindowState.Normal; UpdateStatus();
        SettingsClicked(this, new()); UpdateStatus();
        Check(!StartButton.IsEnabled, "Configuration view keeps activation disabled with reduced refresh cadence");
        preferencesWindow!.Close();
        Check(StartButton.IsEnabled && !(StartButton.ToolTip as string)!.Contains("Close configuration"),
            "Closing configuration immediately restores accurate activation controls and tooltip");
    }
}
