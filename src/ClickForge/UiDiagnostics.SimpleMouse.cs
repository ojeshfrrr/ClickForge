using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunSimpleMouseControlsDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        engine.Stop(); FixedTimingInput.IsChecked = true; diagnosticTarget.Allowed = true;
        ShowSideButtonControls(); var panel = sideButtonWindow!;
        Check(panel.IsVisible && !engine.Enabled && panel.DisplayedState == "Stopped" && !panel.LeftControl.IsEnabled, "Simple mouse controls open stopped with unassigned controls genuinely disabled");
        bool restricted = settings.RobloxOnly; int cps = settings.Cps;
        Press(panel.SetupControl);
        Check(settings.SideButtonToggles && settings.Mode == ActivationMode.Toggle && settings.ActivateKey == 0x75 && settings.PauseKey == 0x76 && settings.StopKey == 0x77 && settings.RobloxOnly == restricted && settings.Cps == cps && !engine.Enabled, "One-click mouse setup enables assignments and default keyboard safeguards while preserving speed and restriction");
        Check(panel.LeftControl.IsEnabled && panel.RightControl.IsEnabled && panel.LeftStatus == "Off" && panel.RightStatus == "Off", "Simple panel shows both outputs Off until explicit activation");
        Press(panel.RightControl); await Task.Delay(60); UpdateStatus();
        Check(engine.Enabled && engine.Button == ClickButton.Right && panel.RightStatus == "Sending" && panel.LeftStatus == "Off", "Simple Right control activates shared mock worker and reports real sending status");
        Press(panel.LeftControl); await Task.Delay(60); UpdateStatus();
        Check(engine.Enabled && engine.Button == ClickButton.Left && panel.LeftStatus == "Sending" && panel.RightStatus == "Off", "Simple Left control switches output and clears Right state");
        PauseClicked(this, new());
        Check(panel.LeftStatus == "Paused", "Simple panel distinguishes manual Pause from active sending");
        Press(panel.EmergencyControl);
        Check(!engine.Enabled && panel.LeftStatus == "Off" && panel.RightStatus == "Off", "Simple emergency control stops the shared worker and both output indicators");
        diagnosticTarget.Allowed = false; Press(panel.RightControl); await Task.Delay(65); UpdateStatus();
        Check(panel.RightStatus == "Waiting for target" && engine.State == EngineState.Paused, "Simple panel never reports Sending for a blocked target");
        Press(panel.EmergencyControl); diagnosticTarget.Allowed = true;
        panel.SpeedControl.Value = 19;
        Check(settings.Cps == 19 && engine.Cps == 19 && CpsInput.Text == "19" && !engine.Enabled, "Simple speed control synchronizes dashboard and existing engine without starting");
        VariableTimingInput.IsChecked = true; UpdateStatus();
        Check(panel.SpeedControl.Visibility == Visibility.Collapsed && !engine.Enabled, "Variable mode hides irrelevant fixed slider without changing the timing policy");
        FixedTimingInput.IsChecked = true;
        panel.UpdateLayout();
        Check(panel.DetailsScroll.ScrollableHeight == 0, "Default simple window shows both assignments and speed without scrolling");
        RenderDiagnostic(panel, Path.Combine(Path.GetDirectoryName(output)!, "ui-simple-mouse-controls.png"));
        panel.Width = 440; panel.Height = 500; panel.UpdateLayout();
        Check(panel.EmergencyControl.IsVisible && panel.EmergencyControl.TransformToAncestor(panel).Transform(new Point()).Y + panel.EmergencyControl.ActualHeight <= panel.ActualHeight, "Small simple panel keeps emergency action visible outside scrollable content");
        RenderDiagnostic(panel, Path.Combine(Path.GetDirectoryName(output)!, "ui-simple-mouse-small.png"));
        Press(panel.LeftControl); await Task.Delay(30); panel.Close();
        Check(sideButtonWindow is null && !engine.Enabled, "Closing simple mouse controls releases and stops active input");
        SideButtonTogglesInput.IsChecked = false; CpsInput.Text = "10";
    }
}
