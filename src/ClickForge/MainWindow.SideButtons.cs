using System.Windows;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private SideButtonControlWindow? sideButtonWindow;
    private void OpenMouseControlsClicked(object sender, RoutedEventArgs e) => ShowSideButtonControls();
    private void ShowSideButtonControls()
    {
        engine.Stop();
        if (sideButtonWindow is not null) { sideButtonWindow.Show(); sideButtonWindow.Activate(); UpdateStatus(); return; }
        sideButtonWindow = new(ApplySideButtonPreset,
            () => { ToggleSideButtonOutput(SideButtonOutputs.LeftId); UpdateStatus(); },
            () => { ToggleSideButtonOutput(SideButtonOutputs.RightId); UpdateStatus(); },
            () => EmergencyClicked(this, new()), () => TestClicked(this, new()),
            cps => { engine.Stop(); CpsInput.Text = cps.ToString(); UpdateStatus(); }) { Owner = this };
        sideButtonWindow.Closing += (_, _) => engine.Stop();
        sideButtonWindow.Closed += (_, _) => { sideButtonWindow = null; UpdateStatus(); };
        sideButtonWindow.Show(); UpdateStatus();
    }
    private void ApplySideButtonPreset()
    {
        engine.Stop(); activeTest?.StopTest();
        settings = settings with { SideButtonToggles = true, Mode = ActivationMode.Toggle, ActivateKey = 0x75, PauseKey = 0x76, StopKey = 0x77, Button = ClickButton.Left };
        settings.Validate(); LoadControls();
        if (bindings is not null) RegisterShortcuts();
        Persist(); lastDisplay = null; UpdateStatus();
    }
    private void RefreshSideButtonControls()
    {
        if (sideButtonWindow?.IsVisible != true) return;
        string reason = settings.TimingMode == ClickTimingMode.Variable ? desktopTarget.Status.Explanation : target.Status.Explanation;
        sideButtonWindow.Refresh(settings, shortcutsReady, ConfigurationBlocked, engine.State, engine.Enabled, engine.IsPaused, engine.Button, reason, engine.Error);
    }
    private void SideButtonTogglesChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Stop(); activeTest?.StopTest();
        bool enabled = SideButtonTogglesInput.IsChecked == true;
        var candidate = settings with { SideButtonToggles = enabled, Mode = enabled ? ActivationMode.Toggle : settings.Mode };
        try { candidate.Validate(); }
        catch (ArgumentException error)
        {
            ready = false; SideButtonTogglesInput.IsChecked = settings.SideButtonToggles; ready = true;
            MessageText.Text = error.Message; return;
        }
        settings = candidate;
        ready = false; ToggleModeInput.IsChecked = settings.Mode == ActivationMode.Toggle; HoldModeInput.IsChecked = settings.Mode == ActivationMode.Hold; ready = true;
        if (bindings is not null) RegisterShortcuts();
        Persist(); lastDisplay = null; UpdateStatus();
        MessageText.Text = !shortcutsReady ? MessageText.Text : enabled
            ? "Assigned: Mouse 4 (Back) → Left; Mouse 5 (Forward) → Right. Press the same button again to stop; the other switches output. Input remains stopped."
            : "Side-button output toggles disabled. Input remains stopped.";
    }

    private void ToggleSideButtonOutput(int id)
    {
        if (!shortcutsReady || !settings.SideButtonToggles) { MessageText.Text = "Apply mouse setup and register shortcuts before using the side-button toggles."; return; }
        if (settings.Mode != ActivationMode.Toggle) { MessageText.Text = "Choose Toggle activation mode to use the Left / Right side-button assignments."; return; }
        var output = SideButtonOutputs.OutputFor(id);
        if (engine.Enabled && engine.Button == output) { engine.Stop(); history.Add(ActivityKind.Stopped); return; }
        // Keep all modal/target/configuration guards in the existing activation path.
        // Changing buttons is a synchronous release barrier on the single worker.
        if (ConfigurationBlocked) { MessageText.Text = "Close the open configuration or test window before using side-button output toggles."; return; }
        engine.Stop();
        if (engine.State == EngineState.Error) { MessageText.Text = engine.Error ?? "Input could not be released. Clicking remains stopped."; return; }
        var candidate = settings with { Button = output };
        candidate.Validate();
        engine.ConfigureOutput(output, candidate.CustomKey); settings = candidate;
        SynchronizeOutputControls(); Persist(); lastDisplay = null;
        StartValidated();
    }
}
