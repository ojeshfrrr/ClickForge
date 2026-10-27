using System.IO;
using System.Windows;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunSideButtonOutputDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); results.Add("PASS " + label); }
        void Hotkey(int id) { bool handled = false; WindowMessage(0, 0x0312, id, 0, ref handled); }
        async Task Sent() { for (int i = 0; i < 40 && engine.Clicks == 0; i++) await Task.Delay(10); }
        engine.Stop(); ResetClicked(this, new()); ToggleModeInput.IsChecked = true; FixedTimingInput.IsChecked = true;
        SideButtonTogglesInput.IsChecked = true;
        Check(settings.SideButtonToggles && settings.Mode == ActivationMode.Toggle && !engine.Enabled && ModeHelp.Text.Contains("Mouse 4"), "Side assignments opt in with Toggle mode and never start on configuration");
        Check(new SettingsService(Path.Combine(Path.GetDirectoryName(output)!, "ui-test-settings.json")).Load().SideButtonToggles, "Side toggle preference persists through actual control");
        Hotkey(SideButtonOutputs.RightId); await Sent();
        Check(engine.Enabled && engine.Button == ClickButton.Right && settings.Button == ClickButton.Right && InputTypeInput.SelectedIndex == 0 && ClickButtonInput.SelectedItem?.ToString() == "Right", "Front side button toggles Right on and synchronizes visible output");
        Hotkey(SideButtonOutputs.RightId); long count = engine.Clicks; await Task.Delay(35);
        Check(!engine.Enabled && engine.Clicks == count, "Front side button toggles Right off without later input");
        Hotkey(SideButtonOutputs.LeftId); await Task.Delay(35);
        Check(engine.Enabled && engine.Button == ClickButton.Left && settings.Button == ClickButton.Left, "Back side button toggles Left on");
        Hotkey(SideButtonOutputs.RightId); await Task.Delay(35);
        Check(engine.Enabled && engine.Button == ClickButton.Right && settings.Button == ClickButton.Right, "Other side button switches the existing worker from Left to Right");
        Hotkey(2); Check(engine.IsPaused, "Existing Pause shortcut pauses side-activated output");
        Hotkey(SideButtonOutputs.RightId); Check(!engine.Enabled, "Same side button stops a paused request");
        Hotkey(SideButtonOutputs.LeftId); Hotkey(3); Check(!engine.Enabled && engine.LastStopReason == SessionEventKind.EmergencyStop, "Existing emergency shortcut stops side-activated output");
        diagnosticTarget.Allowed = false; Hotkey(SideButtonOutputs.RightId); count = engine.Clicks; await Task.Delay(65);
        Check(engine.Enabled && engine.State == EngineState.Paused && engine.Clicks == count, "Side output toggles cannot bypass fixed target validation");
        engine.Stop(); diagnosticTarget.Allowed = true;
        HoldModeInput.IsChecked = true; Hotkey(SideButtonOutputs.LeftId);
        Check(!engine.Enabled && MessageText.Text.Contains("Toggle activation"), "Hold mode cannot silently activate a side Toggle request");
        ToggleModeInput.IsChecked = true;
        ActivateInput.SelectedIndex = 11; ApplyClicked(this, new());
        Check(settings.ActivateKey == 0x75 && MessageText.Text.Contains("reserved") && !engine.Enabled, "Shortcut editor rejects side buttons reserved by output assignments");
        ActivateInput.SelectedItem = ShortcutKeys.Label(settings.ActivateKey);
        activeTest = new TestWindow(SafeTestMode.FixedCps, ClickButton.Right) { Owner = this, ShowActivated = false }; activeTest.Show();
        try { Hotkey(SideButtonOutputs.LeftId); Check(!engine.Enabled, "Safe test blocks side-button activation of the live worker"); }
        finally { activeTest.Close(); activeTest = null; }
        ShortcutsExpander.IsExpanded = true; UpdateLayout();
        RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-side-button-toggles.png"));
        SideButtonTogglesInput.IsChecked = false; Hotkey(SideButtonOutputs.RightId);
        Check(!settings.SideButtonToggles && !engine.Enabled, "Disabled side assignment ignores queued shortcut IDs");
        ClickButtonInput.SelectedItem = "Left";
        ShortcutsExpander.IsExpanded = false; UpdateStatus();
    }
}
