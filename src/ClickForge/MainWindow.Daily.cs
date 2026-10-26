using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Interop;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private ProfileStore profiles = null!;
    private readonly ActivityHistory history = new();
    private TrayService? tray;
    private ProfilesWindow? profilesWindow;
    private PreferencesWindow? preferencesWindow;
    private bool exitRequested, hiddenToTray;
    private WindowState restoredState = WindowState.Normal;
    private string? lastActivityState;
    private string? lastAnnouncement;
    private bool persistenceFailed;
    private string UserRoot => selfTestPath is null && previewPath is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge") : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(selfTestPath ?? previewPath!))!, "ui-user-data");

    private void InitializeDaily()
    {
        profiles = new(Path.Combine(UserRoot, "Profiles"));
        history.Add(ActivityKind.Startup);
        if (store.Warning is not null) history.Add(ActivityKind.SettingsRecovery);
        try
        {
            if (!Directory.Exists(Path.Combine(UserRoot, "Profiles"))) profiles.RestoreBuiltIns();
            var saved = profiles.Load();
            if (profiles.Warnings.Count > 0) { MessageText.Text = string.Join(" ", profiles.Warnings.Distinct()); history.Add(ActivityKind.SettingsRecovery); }
            if (settings.ActiveProfileId is { } id && saved.All(p => p.Id != id)) { settings = settings with { ActiveProfileId = null }; MessageText.Text = "The selected profile is unavailable. Your last valid timing settings remain loaded; clicking is stopped."; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageText.Text = "Profiles could not be initialized. Check access to your ClickForge user data folder."; history.Add(ActivityKind.PersistenceError); }
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && settings.MinimizeToTray && CanHideToTray) HideToTray();
            else if (WindowState != WindowState.Minimized) restoredState = WindowState;
            UpdateStatus();
        };
        UpdateProfileLabel();
        AutomationProperties.SetName(StartButton, "Start or stop clicking");
        AutomationProperties.SetName(PauseButton, "Pause or resume clicking");
        AutomationProperties.SetName(EmergencyButton, "Emergency stop");
        AutomationProperties.SetHelpText(CpsSlider, "Use arrow keys to change the fixed rate from 1 to 50 CPS.");
    }
    private void InitializeTray(bool simulateFailure = false)
    {
        if (tray is not null) return;
        try
        {
            tray = new(HwndSource.FromHwnd(new WindowInteropHelper(this).Handle), (ImageSource)FindResource("ForgeIcon"), ExecuteTrayCommand, simulateFailure);
            tray.Unavailable += () => { history.Add(ActivityKind.TrayUnavailable); RestoreWindow(); MessageText.Text = "The system tray is unavailable. ClickForge remains open."; };
            UpdateDailyStatus();
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        { tray?.Dispose(); tray = null; MessageText.Text = "System tray unavailable. Minimize and Close will use normal window behavior."; history.Add(ActivityKind.TrayUnavailable); }
    }
    private bool CanHideToTray => tray?.Available == true && activeTest is null && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible);
    private bool HideToTray()
    {
        if (!CanHideToTray) return false;
        engine.Stop(); hiddenToTray = true; Hide(); refresh.Interval = TimeSpan.FromSeconds(1); UpdateStatus(); return true;
    }
    internal void RestoreWindow()
    {
        if (closing) return;
        hiddenToTray = false; Show(); WindowState = restoredState == WindowState.Minimized ? WindowState.Normal : restoredState; Activate(); UpdateStatus();
        // Restoring a window never changes engine activation.
    }
    internal void PrepareExit() { exitRequested = true; engine.Stop(); activeTest?.StopTest(); }
    private void ExitApplication() { if (closing) return; PrepareExit(); Close(); }
    private void ExecuteTrayCommand(TrayCommand command)
    {
        if (closing) return;
        switch (command)
        {
            case TrayCommand.Open: RestoreWindow(); break;
            case TrayCommand.StartStop: ToggleStart(); break;
            case TrayCommand.PauseResume: PauseClicked(this, new()); break;
            case TrayCommand.EmergencyStop: EmergencyClicked(this, new()); break;
            case TrayCommand.Profiles: RestoreWindow(); ProfilesClicked(this, new()); break;
            case TrayCommand.Updates: RestoreWindow(); UpdatesClicked(this, new()); break;
            case TrayCommand.Settings: RestoreWindow(); SettingsClicked(this, new()); break;
            case TrayCommand.Exit: ExitApplication(); break;
        }
    }
    private string EffectiveState => engine.State == EngineState.Paused && !engine.IsPaused && engine.Enabled ? "Waiting for target" : engine.State switch { EngineState.Running => settings.Button==ClickButton.CustomKey?"Sending keys":"Clicking", EngineState.Error => "Error", EngineState.Paused => "Paused", _ => "Stopped" };
    private void UpdateActivityAndTray()
    {
        string state = EffectiveState;
        if (state != lastActivityState)
        {
            // Polling doesn't log focus changes or every input pair. Only requested
            // state changes and the first actionable error enter bounded history.
            if (state == "Error") history.Add(ActivityKind.InputError);
            lastActivityState = state;
        }
        tray?.Update(state, engine.Enabled, engine.IsPaused, shortcutsReady && settings.Mode == ActivationMode.Toggle && activeTest is null && profilesWindow is null && preferencesWindow is null && targetWindow is null && customKeyWindow is null);
    }
    private void UpdateDailyStatus()
    {
        if (closing) return;
        RenderInspector();
        UpdateActivityAndTray();
        string state = EffectiveState;
        if (!engine.Enabled && (profilesWindow?.IsVisible == true || preferencesWindow?.IsVisible == true || targetWindow?.IsVisible == true || customKeyWindow?.IsVisible == true))
        { StartButton.IsEnabled = false; StartButton.ToolTip = "Close configuration views before activating clicking."; }
        else StartButton.IsEnabled = shortcutsReady && (settings.Mode == ActivationMode.Toggle || engine.Enabled);
        StatusText.Text = state;
        if (lastAnnouncement != state)
        {
            lastAnnouncement = state;
            StatusText.Text = state;
            if (UIElementAutomationPeer.FromElement(StatusText) is { } peer) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        preferencesWindow?.Refresh();
    }
    private void UpdateProfileLabel()
    {
        if (profiles is null) return;
        var profile = settings.ActiveProfileId is { } id ? profiles.Load().FirstOrDefault(p => p.Id == id) : null;
        ActiveProfileText.Text = profile is null ? "Custom configuration" : profile.Name + (profile.Matches(settings) ? "" : " · modified");
        ActiveProfileText.ToolTip = profile is null ? "Create a profile to save this timing configuration." : profile.Summary;
        RefreshQuickProfiles();
    }
    private void ApplyProfile(TimingProfile profile)
    {
        settings = profile.Apply(settings, engine); activeTest?.StopTest();
        // Load only timing and activation controls, keeping the session-local target
        // and all application safety preferences exactly as they are.
        ready = false; CpsInput.Text = settings.Cps.ToString(); CpsSlider.Value = settings.Cps;
        ToggleModeInput.IsChecked = settings.Mode == ActivationMode.Toggle; HoldModeInput.IsChecked = settings.Mode == ActivationMode.Hold;
        LoadTimingControls(); ready = true;
        bool saved = Persist(); UpdateProfileLabel(); UpdateStatus(); history.Add(ActivityKind.ProfileApplied);
        MessageText.Text = saved ? "Profile applied. Clicking is stopped; activate explicitly when ready." : "Profile applied for this session, but could not be saved. Check user-folder access.";
    }
    private void ProfilesClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop(); activeTest?.StopTest();
        if (profilesWindow is not null) { profilesWindow.Activate(); return; }
        profilesWindow = new(profiles, () => settings, ApplyProfile, id =>
        {
            if (settings.ActiveProfileId == id) { engine.Stop(); settings = settings with { ActiveProfileId = null }; Persist(); }
            UpdateProfileLabel();
        }, () => { history.Add(ActivityKind.ProfileImported); tray?.Notify("Profile imported. Select and apply it to use it."); }) { Owner = this };
        profilesWindow.Closed += (_, _) => { profilesWindow = null; UpdateProfileLabel(); }; profilesWindow.Show();
    }
    private void SettingsClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop(); activeTest?.StopTest();
        if (preferencesWindow is not null) { preferencesWindow.Activate(); return; }
        preferencesWindow = new(() => settings, value => { settings = value; return Persist(); }, history, DiagnosticReport, () => tray?.Available == true, () => { engine.Stop(); settings = new Settings { ActiveProfileId = settings.ActiveProfileId, Width = settings.Width, Height = settings.Height }; LoadControls(); if (bindings is not null) RegisterShortcuts(); Persist(); UpdateProfileLabel(); UpdateStatus(); }) { Owner = this };
        preferencesWindow.Closed += (_, _) => preferencesWindow = null; preferencesWindow.Show();
    }
    private string DiagnosticReport() => $"ClickForge {InstalledVersion.Value.Split('+')[0]}\nWindows {Environment.OSVersion.Version}\nEngine: {EffectiveState}\nTiming: {settings.TimingMode}\nProfile: {(settings.ActiveProfileId is null ? "Custom configuration" : "Selected (name redacted)")}\nShortcuts: {(shortcutsReady ? "Registered or diagnostic routing enabled" : "Unavailable; review shortcut configuration")}\nTray: {(tray?.Available == true ? "Available" : "Unavailable")}\nUpdates: {updateClient.Status.State}\nPreferences: {(persistenceFailed ? "Save failed" : "No save failure reported")}\nPhysical global-key delivery and target input acceptance are not measured here.\n\nRecent activity (profile names, paths and target identities omitted):\n{history.RedactedReport()}";
}
