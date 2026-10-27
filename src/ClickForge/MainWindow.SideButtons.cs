using System.Windows;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private SideButtonControlWindow? sideButtonWindow;
    private bool synchronizingSetup;
    private bool MinecraftSetupActive => settings.SideButtonToggles && settings.MouseSetup == MouseSetup.MinecraftBedwars;
    private bool SelectedSetupCanActivate => selfTestPath is not null ? diagnosticDesktopTarget.CanActivate : desktopTarget.CanActivate;
    private void SetupChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!ready || synchronizingSetup || SetupInput.SelectedIndex is < 0 or > 2) return;
        engine.Stop(); activeTest?.StopTest();
        if (SetupInput.SelectedIndex == 0)
        {
            settings = settings with { SideButtonToggles = false };
            if (bindings is not null) RegisterShortcuts();
            LoadControls(); Persist(); lastDisplay = null; UpdateStatus();
        }
        else { settings = settings with { MouseSetup = (MouseSetup)(SetupInput.SelectedIndex - 1) }; ApplySideButtonPreset(); }
    }
    private void SynchronizeSetupControls()
    {
        if (SetupInput is null) return;
        synchronizingSetup = true;
        try
        {
            SetupInput.SelectedIndex = settings.SideButtonToggles ? (int)settings.MouseSetup + 1 : 0;
            SetupDescription.Text = !settings.SideButtonToggles ? "Choose your output and speed, then start when ready." : settings.Mode == ActivationMode.Hold ? "Side-button toggles are off in Hold mode. Choose Toggle in More settings." : MinecraftSetupActive ? "Your Bedwars layout. Choose the Minecraft window first. Both buttons toggle." : "Your original mouse layout. Both buttons toggle.";
            SetupAssignments.Text = SideButtonOutputs.Summary(settings.MouseSetup);
            SetupAssignments.Visibility = Visibility.Collapsed;
            OutputChoicePanel.Visibility = settings.SideButtonToggles ? Visibility.Collapsed : Visibility.Visible;
            AssignmentActions.Visibility = settings.SideButtonToggles || settings.TimingMode == ClickTimingMode.Variable ? Visibility.Visible : Visibility.Collapsed;
            BackToggleButton.Visibility = FrontToggleButton.Visibility = settings.SideButtonToggles ? Visibility.Visible : Visibility.Collapsed;
            BackToggleButton.Content = "Back: " + ClickButtons.Label(SideButtonOutputs.OutputFor(SideButtonOutputs.LeftId, settings.MouseSetup));
            FrontToggleButton.Content = "Front: " + ClickButtons.Label(SideButtonOutputs.OutputFor(SideButtonOutputs.RightId, settings.MouseSetup));
            BackToggleButton.ToolTip = "Mouse 4 (Back): press to toggle this output.";
            FrontToggleButton.ToolTip = "Mouse 5 (Forward): press to toggle this output.";
            SetupTargetButton.Visibility = MinecraftSetupActive || settings.TimingMode == ClickTimingMode.Variable ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { synchronizingSetup = false; }
    }
    private void BackToggleClicked(object sender, RoutedEventArgs e) { ToggleSideButtonOutput(SideButtonOutputs.LeftId); UpdateStatus(); }
    private void FrontToggleClicked(object sender, RoutedEventArgs e) { ToggleSideButtonOutput(SideButtonOutputs.RightId); UpdateStatus(); }
    private void OpenMouseControlsClicked(object sender, RoutedEventArgs e) => ShowSideButtonControls();
    private void ShowSideButtonControls()
    {
        engine.Stop();
        if (sideButtonWindow is not null) { sideButtonWindow.Show(); sideButtonWindow.Activate(); UpdateStatus(); return; }
        sideButtonWindow = new(ApplySideButtonPreset, setup => { engine.Stop(); settings = settings with { MouseSetup = setup }; ApplySideButtonPreset(); },
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
        bool enabled = settings.SideButtonToggles && settings.Mode == ActivationMode.Toggle && shortcutsReady;
        BackToggleButton.IsEnabled = enabled && (!ConfigurationBlocked || engine.Enabled && engine.Button == SideButtonOutputs.OutputFor(SideButtonOutputs.LeftId, settings.MouseSetup));
        FrontToggleButton.IsEnabled = enabled && (!ConfigurationBlocked || engine.Enabled && engine.Button == SideButtonOutputs.OutputFor(SideButtonOutputs.RightId, settings.MouseSetup));
        if (sideButtonWindow?.IsVisible != true) return;
        string reason = settings.TimingMode == ClickTimingMode.Variable || MinecraftSetupActive ? desktopTarget.Status.Explanation : target.Status.Explanation;
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
        SynchronizeSetupControls(); RenderTimingMode(); Persist(); lastDisplay = null; UpdateStatus();
        MessageText.Text = !shortcutsReady ? MessageText.Text : enabled
            ? SideButtonOutputs.Summary(settings.MouseSetup) + ". Press again to stop."
            : "Side-button output toggles disabled. Input remains stopped.";
    }

    private void ToggleSideButtonOutput(int id)
    {
        if (!shortcutsReady || !settings.SideButtonToggles) { MessageText.Text = "Apply mouse setup and register shortcuts before using the side-button toggles."; return; }
        if (settings.Mode != ActivationMode.Toggle) { MessageText.Text = "Choose Toggle activation mode to use the Left / Right side-button assignments."; return; }
        var output = SideButtonOutputs.OutputFor(id, settings.MouseSetup);
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
