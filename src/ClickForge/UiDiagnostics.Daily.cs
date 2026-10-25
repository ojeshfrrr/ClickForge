using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Interop;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private async Task RunDailyDiagnosticsAsync(List<string> results, string output)
    {
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); results.Add("PASS " + name); }
        string directory = Path.GetDirectoryName(Path.GetFullPath(output))!;
        static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            { var child = VisualTreeHelper.GetChild(root, i); if (child is T result) yield return result; foreach (var value in Descendants<T>(child)) yield return value; }
        }
        void Button(ProfilesWindow window, string text) => Descendants<Button>(window).Single(b => b.Content as string == text).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

        FixedTimingInput.IsChecked = true; ToggleModeInput.IsChecked = true; CpsInput.Text = "10";
        var p = profiles.Create(settings, "UI profile " + Guid.NewGuid().ToString("N")[..8]);
        engine.Start(); await Task.Delay(60); ApplyProfile(p);
        Check(!engine.Enabled && settings.ActiveProfileId == p.Id && ActiveProfileText.Text == p.Name, "Applying an actual profile stops active input and updates active indicator");
        Check(settings.RobloxOnly && desktopTarget.SelectedTitle == "No desktop window selected", "Profile application preserves safety and never restores target identity");
        ProfilesClicked(this, new()); var manager = profilesWindow!; await Task.Delay(80);
        Check(manager.IsVisible && !engine.Enabled, "Profile manager opens with input stopped");
        ToggleStart(); Check(!engine.Enabled && MessageText.Text.Contains("Close Profiles"), "Activation is blocked while editing profiles");
        manager.NameInput.Text = new string('W', 80); Button(manager, "Create from current");
        var created = (TimingProfile)manager.ProfileList.SelectedItem;
        Check(created.Name.Length == 80 && created.FixedCps == 10, "Create profile routes actual UI fields to validated storage");
        await Task.Delay(30); RenderDiagnostic(manager, Path.Combine(directory, "ui-profiles-long-name.png"));
        manager.NameInput.Text = "Renamed profile"; manager.DescriptionInput.Text = "A disposable UI profile"; Button(manager, "Save name / description");
        Check(((TimingProfile)manager.ProfileList.SelectedItem).Name == "Renamed profile", "Rename and description save through real controls");
        Button(manager, "Duplicate"); var duplicate = (TimingProfile)manager.ProfileList.SelectedItem;
        Check(duplicate.Id != created.Id && duplicate.Name.EndsWith("(copy)"), "Duplicate creates an independent profile");
        string exported = Path.Combine(directory, "ui-disposable-profile.json"); manager.ExportPath(exported);
        Check(ProfileStore.Read(exported).Id == duplicate.Id, "Profile export writes validated portable JSON");
        Guid? activeBeforeImport = settings.ActiveProfileId; manager.ImportPath(exported);
        Check(((TimingProfile)manager.ProfileList.SelectedItem).Id != duplicate.Id && settings.ActiveProfileId == activeBeforeImport && !engine.Enabled, "Import uses a fresh ID and requires explicit application");
        Button(manager, "Apply profile"); var active = (TimingProfile)manager.ProfileList.SelectedItem;
        Check(settings.ActiveProfileId == active.Id && !engine.Enabled && manager.Feedback.Contains("stopped"), "Apply profile button uses normal stop barrier");
        manager.DeleteConfirmed(); // Confirmation acceptance simulated; actual deletion and active-ID handling run.
        Check(settings.ActiveProfileId is null && !profiles.Load().Any(v => v.Id == active.Id) && !engine.Enabled, "Confirmed active-profile deletion clears selection without activation");
        results.Add("NOTE Delete confirmation acceptance simulated; no physical dialog input generated.");
        manager.NameInput.Text = ""; Button(manager, "Create from current");
        Check(manager.Feedback.Contains("failed"), "Invalid profile name produces visible validation feedback");
        manager.Refresh(created.Id); manager.ProfileList.Focus(); await Task.Delay(20);
        Check(AutomationProperties.GetName(manager.ProfileList).Length > 0 && AutomationProperties.GetName(manager.NameInput).Length > 0, "Profile selector and edit fields expose accessible names");
        Check(KeyboardNavigation.GetTabNavigation(manager) == KeyboardNavigationMode.Cycle, "Profile dialog contains keyboard tab navigation");
        manager.Width = 490; manager.Height = 500; await Task.Delay(40); manager.UpdateLayout();
        Check(((ScrollViewer)manager.Content).ScrollableHeight > 0, "Compact profile manager scrolls to all actions");
        RenderDiagnostic(manager, Path.Combine(directory, "ui-profiles-compact.png"));
        manager.Width = 670; manager.Height = 740; await Task.Delay(40);
        RenderDiagnostic(manager, Path.Combine(directory, "ui-profiles.png")); manager.Close();

        SettingsClicked(this, new()); var preferences = preferencesWindow!;
        Check(preferences.IsVisible && !engine.Enabled, "Settings and live diagnostics open with input stopped");
        int profileCount = profiles.Load().Length; CpsInput.Text = "29"; RestrictInput.IsChecked = false;
        Descendants<Button>(preferences).Single(b => b.Content as string == "Restore application defaults").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(settings.Cps == 10 && settings.RobloxOnly && profiles.Load().Length == profileCount && !engine.Enabled, "Restore application defaults resets timing and restrictions without deleting profiles or activating");
        preferences.MinimizeInput.IsChecked = true; preferences.MinimizeInput.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        preferences.CloseInput.IsChecked = true; preferences.CloseInput.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(settings.MinimizeToTray && settings.CloseToTray && new SettingsService(Path.Combine(directory, "ui-test-settings.json")).Load().CloseToTray, "Explicit tray preferences persist through real controls");
        history.Add(ActivityKind.EmergencyStop); preferences.Refresh();
        string report = DiagnosticReport();
        Check(report.Contains("Emergency stop") && !report.Contains(UserRoot) && !report.Contains(p.Name), "Diagnostic report redacts local paths, profile names and target identity");
        RenderDiagnostic(preferences, Path.Combine(directory, "ui-settings.png")); preferences.Close();

        InitializeTray(simulateFailure: true);
        Check(tray is null && IsVisible && !CanHideToTray && !engine.Enabled, "Tray initialization failure preserves accessible window and stopped engine");
        InitializeTray();
        if (tray is { Available: true })
        {
            var original = tray; InitializeTray(); Check(ReferenceEquals(tray, original), "Repeated tray initialization cannot create duplicate icons");
            results.Add(tray.HasShellRectangle() ? "PASS Native Shell notification-area registration and icon rectangle confirmed" : "UNAVAILABLE Shell icon rectangle; native add returned success but displayed icon could not be located");
            tray.Update("Stopped", false, false, true);
            Check(tray.Tooltip.Contains("Stopped"), "Tray tooltip reflects true stopped state");
            tray.Route(TrayCommand.PauseResume); Check(!engine.Enabled, "Disabled tray Pause does not activate engine");
            tray.Route(TrayCommand.StartStop); await Task.Delay(70); Check(engine.Enabled, "Tray start routes through shared validated activation command (mock input)");
            tray.Route(TrayCommand.PauseResume); Check(engine.IsPaused, "Tray pause shares engine state");
            tray.Route(TrayCommand.EmergencyStop); Check(!engine.Enabled, "Tray emergency stop uses existing release path");
            tray.Route(TrayCommand.Open); tray.Route(TrayCommand.Open); Check(IsVisible && !engine.Enabled, "Repeated tray Open restores the same window without activation");
            WindowState = WindowState.Minimized; await Task.Delay(40);
            Check(hiddenToTray && !IsVisible && !engine.Enabled, "Opt-in minimize hides to the real tray and stops input");
            bool handled = false; WindowMessage(0, 0x0312, 3, 0, ref handled);
            Check(!engine.Enabled && handled, "Emergency shortcut remains routed while hidden");
            tray.Route(TrayCommand.Open); await Task.Delay(40);
            Check(IsVisible && !engine.Enabled, "Tray restore never automatically resumes clicking");
            Close(); Check(!closing && hiddenToTray && !IsVisible, "Opt-in close hides instead of terminating the instance");
            RestoreWindow(); settings = settings with { CloseToTray = false, MinimizeToTray = false };
            tray.Route(TrayCommand.Profiles); Check(profilesWindow?.IsVisible == true && !engine.Enabled, "Tray Profiles uses the same profile manager"); profilesWindow!.Close();
            tray.Route(TrayCommand.Settings); Check(preferencesWindow?.IsVisible == true && !engine.Enabled, "Tray Settings uses the same preferences dialog"); preferencesWindow!.Close();
            tray.Route(TrayCommand.Updates); Check(updateWindow?.IsVisible == true, "Tray Updates uses existing updater window"); updateWindow!.Close();
            tray.Menu.PlacementTarget = this; tray.Menu.IsOpen = true; await Task.Delay(40);
            RenderDiagnostic(tray.Menu, Path.Combine(directory, "ui-tray-menu.png")); tray.Menu.IsOpen = false;
            tray.Dispose(); tray.Dispose(); Check(!tray.Available && !tray.HasShellRectangle(), "Tray disposal is idempotent and removes native registration"); tray = null;
            results.Add("NOTE Tray commands invoked programmatically; no physical notification-area pointer or keyboard interaction was generated.");
        }
        else results.Add("UNAVAILABLE Real Windows tray creation in this environment; failure behavior passed.");

        // Separate-thread and process tests are necessary: Windows mutex ownership is
        // recursive on its owning thread. A same-thread second object proves nothing.
        using (var signal = new ManualResetEventSlim())
        using (var primary = new SingleInstance("-diagnostics-" + Environment.ProcessId, signal.Set))
        {
            Check(primary.IsPrimary, "Single-instance coordinator obtains session-local ownership");
            bool secondary = await Task.Run(() => { using var second = new SingleInstance("-diagnostics-" + Environment.ProcessId, () => { }); return !second.IsPrimary; });
            Check(secondary && signal.Wait(2000), "Secondary launch signals existing instance rather than creating another engine");
        }
        CpsInput.Focus(); await Task.Delay(20); int visited = 0;
        for (int i = 0; i < 12; i++) if (MoveFocus(new TraversalRequest(FocusNavigationDirection.Next))) visited++;
        Check(visited > 0 && !engine.Enabled, "WPF keyboard traversal moves focus without activation");
        Check(AutomationProperties.GetName(CpsInput).Length > 0 && AutomationProperties.GetName(MinimumCpsSlider).Length > 0 && AutomationProperties.GetName(EmergencyButton).Length > 0, "Speed and emergency controls expose accessible labels");
        Check(FindResource("FocusRing") is Style && AutomationProperties.GetLiveSetting(StatusText) == AutomationLiveSetting.Polite, "Visible focus style and polite engine-state announcements are configured");
        string registrationTest = @"Software\ClickForge\Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            InstallationRegistration.RefreshVersion(AppContext.BaseDirectory, "9.0.0", registrationTest);
            Check(Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registrationTest) is null, "Portable launch never creates Installed Apps registration");
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(registrationTest);
            key.SetValue("ClickForgeInstallerId", InstallationRegistration.InstallerId); key.SetValue("InstallLocation", directory); key.SetValue("DisplayVersion", "1.0.0");
            InstallationRegistration.RefreshVersion(Path.Combine(directory, "another-app"), "9.0.0", registrationTest);
            Check(key.GetValue("DisplayVersion") as string == "1.0.0", "Registry version refresh rejects another install location");
            InstallationRegistration.RefreshVersion(directory, "1.2.0", registrationTest);
            Check(key.GetValue("DisplayVersion") as string == "1.2.0", "Installed registration version refresh follows a successful app restart");
            key.SetValue("ClickForgeInstallerId", "unrelated"); InstallationRegistration.RefreshVersion(directory, "9.0.0", registrationTest);
            Check(key.GetValue("DisplayVersion") as string == "1.2.0", "Registration refresh cannot overwrite unrelated ownership");
            using var guard = Mutex.OpenExisting(InstallationRegistration.GuardName);
            Check(guard is not null, "Application lifetime exposes the native installer/uninstaller guard");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(registrationTest, false); }
        var content = DashboardScroll.Content as FrameworkElement;
        foreach (double scale in new[] { 1.25, 1.5, 2.0 })
        {
            content!.LayoutTransform = new ScaleTransform(scale, scale); UpdateLayout();
            Check(DashboardScroll.ScrollableHeight > 0, $"Scaled-content layout at {scale * 100:0}% provides scrolling");
        }
        content!.LayoutTransform = Transform.Identity; DashboardScroll.ScrollToTop();
        Width = 1280; Height = 920; await Task.Delay(50); UpdateLayout();
        if (ActualWidth >= 1240)
            Check(InspectorHost.Visibility == Visibility.Visible && RailColumn.Width.Value == 170, "Wide obsidian shell exposes rail and genuine target/profile inspector");
        else results.Add("UNAVAILABLE Wide inspector layout: Windows capped the requested 1280-DIP window on this display.");
        Check(desktopTarget.Snapshot is null && InspectorIdentity.Text == "No window selected" && InspectorValidity.Text == "Not selected", "Target inspector never fabricates a selected or verified window");
        Check(((SolidColorBrush)FindResource("Base")).Color == Color.FromRgb(8, 9, 11) && ((SolidColorBrush)FindResource("Accent")).Color == Color.FromRgb(231, 196, 119), "Obsidian and champagne design tokens replace the blue palette");
        Check(StartLabel.Foreground == FindResource("Ink") && FixedTimingInput.Style == FindResource("TimingSegment"), "Gold primary action and timing selector use high-contrast reusable styles");
        var quick = profiles.Create(settings, "Quick profile check"); RefreshQuickProfiles(); QuickProfileInput.SelectedItem = quick;
        Check(settings.ActiveProfileId == quick.Id && !engine.Enabled, "Inspector profile selector applies through the existing stop barrier");
        CpsInput.Text = "13"; QuickProfileSaveClicked(this, new());
        Check(profiles.Load().Single(p => p.Id == quick.Id).FixedCps == 13 && !engine.Enabled, "Inspector Save persists the current timing in the active profile");
        profiles.Delete(quick.Id); settings = settings with { ActiveProfileId = null }; CpsInput.Text = "10"; UpdateProfileLabel();
        Width = 800; Height = 700; await Task.Delay(50); UpdateLayout();
        Check(InspectorHost.Visibility == Visibility.Collapsed && RailColumn.Width.Value == 58, "Narrow shell collapses secondary inspector and navigation labels");
        TargetClicked(this, new()); await Task.Delay(30);
        Check(targetWindow?.IsVisible == true && InspectorHost.Content is null && !engine.Enabled, "Target navigation relocates the same live inspector into a compact view");
        RenderDiagnostic(targetWindow!, Path.Combine(directory, "ui-target-view.png")); targetWindow!.Close();
        Check(ReferenceEquals(InspectorHost.Content, InspectorContent), "Closing target view restores the same inspector without duplicating controls or services");
        Width = 1280; Height = 920; await Task.Delay(50); UpdateLayout(); DashboardScroll.ScrollToTop();
        RenderDiagnostic(this, Path.Combine(directory, "ui-obsidian-final.png"));
        results.Add($"NOTE Actual Windows DPI scale: {VisualTreeHelper.GetDpi(this).DpiScaleX:0.##}. Content-scale checks do not substitute for changing Windows display DPI or a screen-reader session.");
        settings = settings with { CloseToTray = false, MinimizeToTray = false }; Persist();
        results.Add("PASS No animated transitions or timed effects; reduced-motion preferences require no animation suppression");
        profiles.Delete(p.Id); profiles.Delete(created.Id); profiles.Delete(duplicate.Id);
    }
}
