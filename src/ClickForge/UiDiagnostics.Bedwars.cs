using System.IO;
using System.Windows;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunBedwarsSetupDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        async Task SentAfter(long before) { for (int i = 0; i < 60 && engine.Clicks <= before; i++) await Task.Delay(10); Check(engine.Clicks > before, "Bedwars mock input reaches the existing worker"); }
        async Task CheckDefaultOrDisplayLimited(string setup)
        {
            if (ActualWidth >= 999 && ActualHeight >= 879)
                Check(DashboardScroll.ScrollableHeight == 0, $"Default redesigned {setup} layout fits without forced scrolling");
            else
            {
                results.Add($"UNAVAILABLE Full 1000x880 {setup} layout: work area {SystemParameters.WorkArea}; actual {ActualWidth:0}x{ActualHeight:0}. Verifying display-limited scrolling instead.");
                DashboardScroll.ScrollToEnd(); await Task.Delay(40); UpdateLayout();
                Check(DashboardScroll.ScrollableHeight == 0 || DashboardScroll.VerticalOffset > 0, $"Display-limited {setup} keeps lower controls reachable through intentional scrolling");
                DashboardScroll.ScrollToTop(); UpdateLayout();
            }
        }
        engine.Stop(); FixedTimingInput.IsChecked = true; diagnosticDesktopTarget.CanActivate = false; diagnosticDesktopTarget.CanClick = false;
        bool originalRestriction = settings.RobloxOnly; int originalCps = settings.Cps;
        SetupInput.SelectedIndex = 2;
        Check(settings.SideButtonToggles && settings.MouseSetup == MouseSetup.MinecraftBedwars && settings.Mode == ActivationMode.Toggle && !engine.Enabled, "Bedwars is a distinct selectable setup and configuration never starts input");
        Check(InspectorVerification.Text == "Choose your Minecraft window", "Bedwars target guidance names the Minecraft window rather than an unrelated timing mode");
        Check(BackToggleButton.Content.ToString() == "Back: Right" && FrontToggleButton.Content.ToString() == "Front: Left" && SetupAssignments.Text.Contains("Front (Mouse 5): Left"), "Main setup clearly labels Front Left and Back Right");
        Check(settings.RobloxOnly == originalRestriction && settings.Cps == originalCps && !RestrictInput.IsEnabled && OutputChoicePanel.Visibility == Visibility.Collapsed, "Bedwars keeps regular safety preferences and hides unrelated manual output controls");
        var persisted = new SettingsService(Path.Combine(Path.GetDirectoryName(output)!, "ui-test-settings.json")).Load();
        Check(persisted.MouseSetup == MouseSetup.MinecraftBedwars && persisted.SideButtonToggles, "Actual Bedwars selection persists independently from timing profiles");
        long before = engine.Clicks; FrontToggleClicked(this, new());
        Check(!engine.Enabled && engine.Clicks == before && MessageText.Text.Contains("Minecraft window"), "Bedwars refuses activation without an eligible explicitly selected window");
        diagnosticDesktopTarget.CanActivate = true;
        FrontToggleClicked(this, new()); await Task.Delay(65);
        Check(engine.Enabled && engine.State == EngineState.Paused && engine.Clicks == before, "Bedwars cannot bypass selected-window focus and pointer checks through the ordinary gate");
        diagnosticDesktopTarget.CanClick = true; await SentAfter(before);
        Check(engine.Button == ClickButton.Left, "Front side-button action sends Left in Bedwars");
        FrontToggleClicked(this, new()); Check(!engine.Enabled, "Front toggles Left off in Bedwars");
        before = engine.Clicks; BackToggleClicked(this, new()); await SentAfter(before);
        Check(engine.Button == ClickButton.Right, "Back side-button action sends Right in Bedwars");
        diagnosticDesktopTarget.CanClick = false; await Task.Delay(70); before = engine.Clicks; await Task.Delay(50);
        Check(engine.State == EngineState.Paused && engine.Clicks == before, "Bedwars pauses when selected target eligibility is lost");
        EmergencyClicked(this, new()); Check(!engine.Enabled, "Emergency Stop clears a Bedwars request waiting for its target");
        ShowSideButtonControls(); var small = sideButtonWindow!;
        Check(small.SetupSelector.SelectedIndex == 1 && small.LeftControl.Content.ToString() == "Toggle Right" && small.RightControl.Content.ToString() == "Toggle Left" && !engine.Enabled, "Small view synchronizes the separate Bedwars mapping without activation");
        RenderDiagnostic(small, Path.Combine(Path.GetDirectoryName(output)!, "ui-bedwars-small.png")); small.Close();
        HoldModeInput.IsChecked = true; UpdateStatus();
        Check(!BackToggleButton.IsEnabled && !FrontToggleButton.IsEnabled && SetupDescription.Text.Contains("Hold mode"), "Main preset explains inactive side toggles when a Hold profile is selected");
        ToggleModeInput.IsChecked = true;
        Width = Math.Min(1000, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40));
        Height = Math.Min(880, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 40));
        ShortcutsExpander.IsExpanded = false; SessionDetails.IsExpanded = false; DashboardScroll.ScrollToTop(); UpdateLayout(); UpdateStatus();
        await Task.Delay(70); UpdateLayout();
        await CheckDefaultOrDisplayLimited("Bedwars");
        Check(EmergencyButton.IsVisible && EmergencyButton.TransformToAncestor(this).Transform(new Point()).Y + EmergencyButton.ActualHeight <= ActualHeight, "New whole-app layout keeps Emergency Stop pinned outside scrolling settings");
        RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-bedwars-main.png"));
        double fittedWidth = Width, fittedHeight = Height;
        Width = Math.Min(984, fittedWidth); Height = Math.Min(680, fittedHeight); await Task.Delay(50); UpdateLayout();
        await CheckDefaultOrDisplayLimited("Bedwars small-display simulation");
        Check(EmergencyButton.TransformToAncestor(this).Transform(new Point()).Y + EmergencyButton.ActualHeight <= ActualHeight, "Simulated 1024x720 display keeps Emergency Stop inside the smaller window");
        RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-bedwars-display-limited.png"));
        Width = fittedWidth; Height = fittedHeight; await Task.Delay(40); UpdateLayout();
        diagnosticDesktopTarget.CanClick = true; BackToggleClicked(this, new()); await Task.Delay(25); SetupInput.SelectedIndex = 1;
        Check(settings.MouseSetup == MouseSetup.Standard && !engine.Enabled && BackToggleButton.Content.ToString() == "Back: Left" && FrontToggleButton.Content.ToString() == "Front: Right", "Original mouse-button layout remains distinct when switching away from Bedwars");
        SetupInput.SelectedIndex = 0; diagnosticDesktopTarget.CanActivate = false; diagnosticDesktopTarget.CanClick = false;
        Check(!settings.SideButtonToggles && RestrictInput.IsEnabled && settings.RobloxOnly == originalRestriction && OutputChoicePanel.Visibility == Visibility.Visible && !engine.Enabled, "Regular clicker restores existing output controls and saved restriction without starting");
        UpdateLayout(); await CheckDefaultOrDisplayLimited("Regular clicker");
        RenderDiagnostic(this, Path.Combine(Path.GetDirectoryName(output)!, "ui-simple-main.png"));
    }
}
