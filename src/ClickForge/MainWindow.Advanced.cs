using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private ApplicationProfiles applicationProfiles = null!;
    private CompactControlWindow? compactWindow;
    private CommandPaletteWindow? paletteWindow;
    private Expander advancedControls = null!;
    private readonly CheckBox durationLimitInput = new() { Content = "Duration limit (wall-clock seconds)" };
    private readonly CheckBox pairLimitInput = new() { Content = "Input-pair limit (sent, not accepted)" };
    private readonly TextBox durationValue = new() { Text = "60", MaxLength = 5 };
    private readonly TextBox pairValue = new() { Text = "1000", MaxLength = 10 };
    private readonly TextBlock limitValidation = DialogLayout.Text("", true);
    private readonly TextBlock limitSummary = DialogLayout.Text("", true);
    private readonly TextBlock associationStatus = DialogLayout.Text("Select an eligible target window to associate a profile.", true);
    private readonly ListBox safetyEvents = new() { MaxHeight = 145, MinHeight = 65 };
    private Guid? suggestedProfile;
    private TimingProfile[] paletteProfiles = [];
    private long timelineVersion = -1;
    private bool loadingLimits;
    private bool ConfigurationBlocked => activeTest?.IsVisible == true || profilesWindow?.IsVisible == true || preferencesWindow?.IsVisible == true ||
        targetWindow?.IsVisible == true || customKeyWindow?.IsVisible == true || paletteWindow?.IsVisible == true;

    private void InitializeAdvanced()
    {
        applicationProfiles = new(Path.Combine(UserRoot, "application-profiles.json"));
        limitValidation.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
        var header = new TextBlock { Text = "Advanced session controls" };
        var compact = DialogLayout.Button("Compact panel", ShowCompact); compact.Padding = new(8, 3, 8, 3); compact.Margin = new(0, 0, 8, 0);
        var commands = DialogLayout.Button("Commands · Ctrl+K", ShowPalette); commands.Padding = new(8, 3, 8, 3); commands.Margin = new(0);
        compact.Content = "Small window";
        commands.Content = "Find a command";
        compact.ToolTip = "Open compact control panel"; commands.ToolTip = "Command palette · Ctrl+K";
        var footerActions = new WrapPanel(); footerActions.Children.Add(compact); footerActions.Children.Add(commands);
        var content = new StackPanel { Margin = new(0, 12, 0, 0) };
        content.Children.Add(footerActions);
        content.Children.Add(DialogLayout.Text("Session auto-stop", false));
        content.Children.Add(DialogLayout.Text("Duration includes manual pauses and target waits. Either applied limit stops input. Only a new explicit activation resets progress.", true));
        var fields = new Grid(); fields.ColumnDefinitions.Add(new()); fields.ColumnDefinitions.Add(new());
        var duration = new StackPanel { Margin = new(0, 0, 10, 0) }; duration.Children.Add(durationLimitInput); duration.Children.Add(durationValue);
        var pairs = new StackPanel(); pairs.Children.Add(pairLimitInput); pairs.Children.Add(pairValue); Grid.SetColumn(pairs, 1); fields.Children.Add(duration); fields.Children.Add(pairs);
        content.Children.Add(fields); content.Children.Add(DialogLayout.Button("Apply limits · stops input", ApplyLimits)); content.Children.Add(limitValidation);
        content.Children.Add(DialogLayout.Text("Application profile suggestion"));
        content.Children.Add(DialogLayout.Text("Stores only a SHA-256 fingerprint of the selected executable's full path and a saved profile ID, in local preferences. No path text, title, PID, window handle or app content is saved. Moving the executable requires a new association. This is matching, not trust verification; target checks still apply.", true));
        content.Children.Add(associationStatus);
        var appActions = new WrapPanel(); appActions.Children.Add(DialogLayout.Button("Associate current saved profile", () => AdvancedAction(AssociateCurrentProfile)));
        appActions.Children.Add(DialogLayout.Button("Apply suggested profile", () => AdvancedAction(ApplySuggestedProfile)));
        appActions.Children.Add(DialogLayout.Button("Remove association", () => AdvancedAction(RemoveAssociation))); content.Children.Add(appActions);
        content.Children.Add(DialogLayout.Button("Clear all application associations…", () => AdvancedAction(() =>
        {
            engine.Stop(); activeTest?.StopTest(); UpdateStatus();
            if (MessageBox.Show(this, "Remove every local application-profile association? Saved profiles are retained.", "Clear associations", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            { applicationProfiles.Clear(); RefreshAssociationSuggestion(); }
        })));
        content.Children.Add(DialogLayout.Text("Private safety timeline · current run only"));
        content.Children.Add(DialogLayout.Text("Event type and time only; at most 100 entries in memory. Input pairs sent do not establish target acceptance.", true));
        AutomationProperties.SetName(safetyEvents, "Private session safety timeline"); content.Children.Add(safetyEvents);
        content.Children.Add(DialogLayout.Button("Clear safety timeline", () => { engine.Timeline.Clear(); UpdateAdvancedStatus(); }));
        advancedControls = new Expander { Header = header, Content = content, Margin = new(0, 0, 0, 10) };
        advancedControls.Expanded += (_, _) => UpdateAdvancedStatus();
        var container = (Panel)MessageText.Parent;
        limitSummary.Visibility = Visibility.Collapsed;
        container.Children.Insert(container.Children.IndexOf(MessageText), limitSummary);
        ((StackPanel)ShortcutsExpander.Content).Children.Add(advancedControls);
        ShortcutsExpander.Header = "More settings";
        AutomationProperties.SetName(durationValue, "Maximum session seconds, 1 to 86400"); AutomationProperties.SetName(pairValue, "Maximum session input pairs sent, 1 to 1000000000");
        foreach (var input in new[] { durationValue, pairValue }) input.TextChanged += (_, _) => DraftLimitsChanged();
        foreach (var input in new[] { durationLimitInput, pairLimitInput }) { input.Checked += (_, _) => DraftLimitsChanged(); input.Unchecked += (_, _) => DraftLimitsChanged(); }
        LoadLimitControls(); RefreshAssociationSuggestion();
        Loaded += (_, _) => UpdateStatus();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; ShowPalette(); } };
    }
    private void DraftLimitsChanged()
    {
        durationValue.IsEnabled = durationLimitInput.IsChecked == true; pairValue.IsEnabled = pairLimitInput.IsChecked == true;
        if (!loadingLimits) limitValidation.Text = "Unapplied limit changes. Apply limits to stop input and use these values next session.";
    }
    private void LoadLimitControls()
    {
        loadingLimits = true;
        durationLimitInput.IsChecked = settings.MaximumSessionSeconds.HasValue; pairLimitInput.IsChecked = settings.MaximumSessionPairs.HasValue;
        durationValue.Text = (settings.MaximumSessionSeconds ?? 60).ToString(); pairValue.Text = (settings.MaximumSessionPairs ?? 1000).ToString();
        DraftLimitsChanged(); limitValidation.Text = "Applied: " + new SessionLimits(settings.MaximumSessionSeconds, settings.MaximumSessionPairs).Summary;
        loadingLimits = false;
    }
    private void ApplyLimits()
    {
        engine.Stop(); activeTest?.StopTest();
        try
        {
            int? seconds = null; long? pairs = null;
            if (durationLimitInput.IsChecked == true) { if (!int.TryParse(durationValue.Text, out int value)) throw new ArgumentException("Enter a whole duration from 1 to 86400 seconds."); seconds = value; }
            if (pairLimitInput.IsChecked == true) { if (!long.TryParse(pairValue.Text, out long value)) throw new ArgumentException("Enter a whole pair limit from 1 to 1,000,000,000."); pairs = value; }
            var limits = new SessionLimits(seconds, pairs); limits.Validate(); engine.ConfigureLimits(limits);
            settings = settings with { MaximumSessionSeconds = seconds, MaximumSessionPairs = pairs };
            bool saved = Persist(); limitValidation.Text = "Applied: " + limits.Summary + (saved ? ". Input is stopped." : ". Applies this run; saving failed.");
        }
        catch (ArgumentException error) { limitValidation.Text = error.Message + " Input stopped; last valid limits retained."; }
        UpdateStatus();
    }
    private void AdvancedAction(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { MessageText.Text = e.Message; }
    }
    private string EligibleApplicationKey() => desktopTarget.Snapshot?.Valid == true && desktopTarget.ApplicationKey is { } key ? key : throw new InvalidOperationException("Select a currently valid eligible target window first.");
    private void RefreshAssociationSuggestion()
        => RenderAssociationSuggestion(desktopTarget.ApplicationKey);
    private void RenderAssociationSuggestion(string? key)
    {
        if (applicationProfiles is null) return;
        suggestedProfile = null;
        if (key is null) { associationStatus.Text = "Select an eligible target window to associate a profile."; return; }
        var saved = profiles.Load(); suggestedProfile = applicationProfiles.Find(key, saved);
        associationStatus.Text = applicationProfiles.Warning ?? (suggestedProfile is { } id ? "Suggested: " + saved.Single(p => p.Id == id).Name + ". Apply explicitly; input stays stopped." : "No saved association for this application. Deleted or unavailable profiles are never applied.");
    }
    private void AssociateCurrentProfile()
    {
        engine.Stop();
        if (settings.ActiveProfileId is not { } id || !profiles.Load().Any(p => p.Id == id)) throw new ArgumentException("Apply a saved profile before associating it.");
        applicationProfiles.Associate(EligibleApplicationKey(), id); RefreshAssociationSuggestion();
    }
    private void ApplySuggestedProfile()
    {
        _ = EligibleApplicationKey(); RefreshAssociationSuggestion();
        if (suggestedProfile is not { } id || profiles.Load().FirstOrDefault(p => p.Id == id) is not { } profile) throw new ArgumentException("No available profile is associated with this application.");
        ApplyProfile(profile);
    }
    private void RemoveAssociation() { applicationProfiles.Remove(EligibleApplicationKey()); RefreshAssociationSuggestion(); }
    private void ShowCompact()
    {
        // Focus changes caused by opening/closing the panel must not resume a
        // previously waiting request. Activation is always explicit afterward.
        engine.Stop();
        if (compactWindow is not null) { compactWindow.Show(); compactWindow.Activate(); UpdateStatus(); return; }
        compactWindow = new(ToggleStart, () => PauseClicked(this, new()), () => EmergencyClicked(this, new()), RestoreWindow, ShowPalette);
        compactWindow.Closing += (_, _) => engine.Stop();
        compactWindow.Closed += (_, _) => { compactWindow = null; UpdateStatus(); };
        compactWindow.Show(); UpdateStatus();
    }
    private void ShowPalette()
    {
        if (paletteWindow is not null) { paletteWindow.Activate(); return; }
        paletteProfiles = profiles.Load();
        paletteWindow = new(PaletteCommands) { Owner = IsVisible ? this : compactWindow };
        paletteWindow.Closed += (_, _) => { paletteWindow = null; lastDisplay = null; UpdateStatus(); };
        paletteWindow.Show(); UpdateStatus();
    }
    private IEnumerable<PaletteCommand> PaletteCommands(string query)
    {
        var commands = new List<PaletteCommand>
        {
            new("Emergency stop all input", () => EmergencyClicked(this, new())),
            new("Stop input", () => { engine.Stop(); activeTest?.StopTest(); UpdateStatus(); }),
            new("Open dashboard", () => { RestoreWindow(); DashboardClicked(this, new()); }),
            new("Open profiles", () => { RestoreWindow(); ProfilesClicked(this, new()); }),
            new("Open target", () => { RestoreWindow(); TargetClicked(this, new()); }),
            new("Open settings", () => { RestoreWindow(); SettingsClicked(this, new()); }),
            new("Open shortcuts", () => { RestoreWindow(); HotkeysClicked(this, new()); }),
            new("Open updates", () => { RestoreWindow(); UpdatesClicked(this, new()); }),
            new("Open Safe Click Test", () => { RestoreWindow(); TestClicked(this, new()); }),
            new("Show compact controls", ShowCompact),
            new("Open session limits and private safety timeline", () => { RestoreWindow(); ShortcutsExpander.IsExpanded = true; advancedControls.IsExpanded = true; advancedControls.BringIntoView(); })
        };
        commands.AddRange(paletteProfiles.Select(p => new PaletteCommand("Apply profile: " + p.Name + " (stops input)", () => AdvancedAction(() => ApplyProfile(profiles.Load().FirstOrDefault(saved => saved.Id == p.Id) ?? throw new ArgumentException("That profile is no longer available."))))));
        if (PaletteInput.TryCps(query, out int cps)) return [new($"Set Fixed CPS to {cps} (stops input; confirm)", () => ApplyPaletteCps(cps, () => MessageBox.Show(this, $"Stop input and set Fixed CPS to {cps}?", "Change CPS", MessageBoxButton.YesNo) == MessageBoxResult.Yes))];
        return commands.Where(c => c.Label.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
    }
    private bool ApplyPaletteCps(int cps, Func<bool> confirm)
    {
        Speed.Validate(cps);
        engine.Stop(); activeTest?.StopTest();
        UpdateStatus();
        if (!confirm()) return false;
        settings = settings with { TimingMode = ClickTimingMode.Fixed, Cps = cps };
        engine.Cps = cps;
        ready = false; CpsInput.Text = cps.ToString(); CpsSlider.Value = cps; LoadTimingControls(); ready = true;
        Persist(); lastDisplay = null; UpdateStatus(); return true;
    }
    private void UpdateAdvancedStatus()
    {
        RefreshSideButtonControls();
        var session = engine.Session;
        var active = engine.ActiveSessionLimits;
        string progress = active.Seconds is { } seconds ? $"{Math.Max(0, seconds - session.Elapsed.TotalSeconds):0.0}s remaining" : "";
        if (active.InputPairs is { } pairs) progress += (progress.Length == 0 ? "" : " · ") + $"{Math.Max(0, pairs - session.InputPairsSent):N0} pairs remaining";
        if (!engine.Enabled && engine.LastStopReason is SessionEventKind.DurationLimit or SessionEventKind.PairLimit)
            progress += " · Automatically stopped at session limit";
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            limitSummary.Text = "Applied: " + engine.Limits.Summary + (session.HasStarted && progress.Length > 0 ? "\nLast/current session: " + progress : "");
            limitSummary.Visibility = engine.Limits == default && progress.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (advancedControls?.IsExpanded == true && timelineVersion != engine.Timeline.Version)
            { timelineVersion = engine.Timeline.Version; safetyEvents.ItemsSource = engine.Timeline.Entries.Reverse().ToArray(); }
        }
        if (compactWindow?.IsVisible == true)
            compactWindow.Refresh(EffectiveState, session, ActiveProfileText.Text,
                ControlAvailability.For(engine.Enabled, engine.IsPaused, settings.Mode, shortcutsReady, ConfigurationBlocked),
                engine.Limits.Summary + (progress.Length > 0 ? " · " + progress : ""), engine.Error);
    }
}
