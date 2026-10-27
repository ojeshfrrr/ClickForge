using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunOutputSelectionDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); results.Add("PASS " + description); }
        string root = Path.GetDirectoryName(output)!;
        int[] hotkeys = [settings.ActivateKey, settings.PauseKey, settings.StopKey];
        engine.Stop(); InputTypeInput.SelectedIndex = 0; ClickButtonInput.SelectedIndex = 0;
        settings = settings with { CustomKey = 0 }; engine.ConfigureOutput(settings.Button, 0); SynchronizeOutputControls();
        Check(InputTypeInput.SelectedIndex == 0 && ClickButtonInput.IsVisible && ChangeCustomKeyButton.Visibility == Visibility.Collapsed &&
            AutomationProperties.GetName(InputTypeInput).Contains("separate"), "Output category uses accessible controls distinct from global hotkeys");

        bool invalid = false, comboRejected = false, conflictRejected = false;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            invalid = !customKeyWindow!.Capture(17); comboRejected = !customKeyWindow.Capture(65, true);
            conflictRejected = !customKeyWindow.Capture(settings.StopKey); customKeyWindow.DialogResult = false;
        }));
        InputTypeInput.SelectedIndex = 1;
        Check(invalid && comboRejected && conflictRejected && settings.Button == ClickButton.Left && InputTypeInput.SelectedIndex == 0 && !engine.Enabled,
            "Cancelled first keyboard capture rejects modifiers and shortcut conflicts and restores Mouse Left");
        _ = Dispatcher.BeginInvoke(new Action(() => { Check(customKeyWindow!.Capture(65), "Capture accepts supported keyboard output A"); customKeyWindow.DialogResult = true; }));
        InputTypeInput.SelectedIndex = 1;
        Check(settings.Button == ClickButton.CustomKey && engine.CustomKey == 65 && !engine.Enabled &&
            ClickButtonInput.Visibility == Visibility.Collapsed && ChangeCustomKeyButton.IsVisible && ChangeCustomKeyButton.Content.ToString()!.Contains("A"),
            "Keyboard selector displays only captured key controls and configures shared worker stopped");
        ClickButtonInput.SelectedIndex = 1;
        Check(settings.Button == ClickButton.CustomKey && engine.Button == ClickButton.CustomKey, "Hidden mouse selection cannot silently change keyboard output");
        Check(new SettingsService(Path.Combine(root, "ui-test-settings.json")).Load().Button == ClickButton.CustomKey,
            "Input type is persisted using the existing validated output discriminator");
        _ = Dispatcher.BeginInvoke(new Action(() => customKeyWindow!.DialogResult = false)); ChangeCustomKeyClicked(this, new());
        Check(settings.CustomKey == 65 && InputTypeInput.SelectedIndex == 1 && !engine.Enabled, "Cancelling key change preserves the chosen keyboard output without restarting");
        ToggleStart(); await Task.Delay(50); InputTypeInput.SelectedIndex = 0;
        Check(!engine.Enabled && engine.Button == ClickButton.Left && ClickButtonInput.IsVisible && ChangeCustomKeyButton.Visibility == Visibility.Collapsed,
            "Switching active keyboard output to Mouse stops input and restores mouse-only controls");
        ClickButtonInput.SelectedIndex = 2; InputTypeInput.SelectedIndex = 1;
        Check(engine.Button == ClickButton.CustomKey && settings.CustomKey == 65 && customKeyWindow is null && !engine.Enabled,
            "Keyboard selection reuses a supported conflict-free captured key without activation");
        InputTypeInput.SelectedIndex = 0;
        Check(settings.Button == ClickButton.Mouse5, "Mouse category remembers its last selected button within the current run");
        var profile = profiles.Create(settings with { Button = ClickButton.CustomKey, CustomKey = 65 }, "Output category diagnostic");
        ApplyProfile(profile);
        Check(InputTypeInput.SelectedIndex == 1 && ClickButtonInput.Visibility == Visibility.Collapsed && engine.Button == ClickButton.CustomKey && !engine.Enabled,
            "Existing keyboard profiles synchronize the new type selector without schema changes or activation");
        RenderDiagnostic(this, Path.Combine(root, "ui-keyboard-output.png"));
        bool passedToTest = false;
        SafeTestModeInput.SelectedIndex = 0;
        _ = Dispatcher.BeginInvoke(new Action(() => { passedToTest = activeTest?.TestOutput == ClickButton.CustomKey && activeTest.ButtonSelector.SelectedItem!.ToString()!.Contains("A"); activeTest?.Close(); }));
        TestClicked(this, new());
        Check(passedToTest && !engine.Enabled, "Open Safe Click Test receives the actual selected keyboard output without starting input");
        // Routed events exercise the existing controlled pad, never native injection.
        var keyboardTest = new TestWindow(SafeTestMode.FixedCps, ClickButton.CustomKey, 65) { Owner = this, ShowActivated = false };
        try
        {
            keyboardTest.Show();
            foreach (var key in new[] { Key.B, Key.A })
                foreach (bool down in new[] { true, false })
                    keyboardTest.TestPad.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(keyboardTest), Environment.TickCount, key)
                    { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent });
            Check(keyboardTest.ReceivedDowns == 1 && keyboardTest.ReceivedUps == 1 && !keyboardTest.FixedTestEnabled && !engine.Enabled,
                "Safe keyboard pad records only selected A down/up events while both input engines remain stopped");
            RenderDiagnostic(keyboardTest, Path.Combine(root, "ui-keyboard-output-safe-test.png"));
        }
        finally { keyboardTest.Close(); }
        profiles.Delete(profile.Id); settings = settings with { ActiveProfileId = null }; InputTypeInput.SelectedIndex = 0; ClickButtonInput.SelectedIndex = 0;
        // A previously captured key can become a hotkey after switching to Mouse.
        settings = settings with { CustomKey = settings.StopKey }; engine.ConfigureOutput(settings.Button, settings.CustomKey); SynchronizeOutputControls();
        _ = Dispatcher.BeginInvoke(new Action(() => customKeyWindow!.DialogResult = false)); InputTypeInput.SelectedIndex = 1;
        Check(InputTypeInput.SelectedIndex == 0 && settings.Button == ClickButton.Left && !engine.Enabled,
            "A retained key now conflicting with emergency stop requires recapture and cancellation keeps Mouse");
        Check(hotkeys.SequenceEqual(new[] { settings.ActivateKey, settings.PauseKey, settings.StopKey }), "Output selection never modifies activation pause or emergency hotkeys");
        ClickButtonInput.SelectedItem = "Right";
        Check(settings.Button == ClickButton.Right && engine.Button == ClickButton.Right && !engine.Enabled,
            "Mouse picker selects actual Right output without confusing it with keyboard output");
        Check(new SettingsService(Path.Combine(root, "ui-test-settings.json")).Load().Button == ClickButton.Right, "Right picker choice persists through actual settings controls");
        var rightProfile = profiles.Create(settings, "Right UI diagnostic"); ClickButtonInput.SelectedItem = "Left"; ApplyProfile(rightProfile);
        Check(ClickButtonInput.SelectedItem as string == "Right" && InputTypeInput.SelectedIndex == 0 && !engine.Enabled, "Right profile restores the mouse picker and stays stopped");
        bool rightPassedToTest = false;
        _ = Dispatcher.BeginInvoke(new Action(() => { rightPassedToTest = activeTest?.TestOutput == ClickButton.Right; activeTest?.Close(); }));
        TestClicked(this, new()); Check(rightPassedToTest && !engine.Enabled, "Open Safe Click Test receives Right using an explicit output mapping");
        var rightTest = new TestWindow(SafeTestMode.FixedCps, ClickButton.Right, 69) { Owner = this, ShowActivated = false };
        try
        {
            rightTest.Show();
            foreach (var button in new[] { MouseButton.Left, MouseButton.Right })
                foreach (bool down in new[] { true, false })
                    rightTest.TestPad.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                    { RoutedEvent = down ? Mouse.PreviewMouseDownEvent : Mouse.PreviewMouseUpEvent });
            Check(rightTest.ReceivedDowns == 1 && rightTest.ReceivedUps == 1 && !rightTest.FixedTestEnabled && !engine.Enabled, "Safe pad records selected Right down/up only without native or production input");
            rightTest.SelectOutput(ClickButton.CustomKey);
            Check(rightTest.TestOutput == ClickButton.CustomKey && rightTest.ButtonSelector.SelectedItem!.ToString()!.Contains("E"), "Safe-test Keyboard remains correctly mapped after adding Right");
            rightTest.SelectOutput(ClickButton.Right); RenderDiagnostic(rightTest, Path.Combine(root, "ui-right-safe-test.png"));
        }
        finally { rightTest.Close(); }
        ClickButtonInput.IsDropDownOpen = true; await Task.Delay(40);
        if (ClickButtonInput.Template.FindName("PART_Popup", ClickButtonInput) is System.Windows.Controls.Primitives.Popup popup && popup.Child is FrameworkElement menu)
            RenderDiagnostic(menu, Path.Combine(root, "ui-right-output-popup.png"));
        ClickButtonInput.IsDropDownOpen = false;
        profiles.Delete(rightProfile.Id); settings = settings with { ActiveProfileId = null }; ClickButtonInput.SelectedItem = "Left";
        UpdateStatus(); RenderDiagnostic(this, Path.Combine(root, "ui-mouse-output.png"));
    }
}
