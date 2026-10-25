using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private bool updatingQuickProfiles;
    private Window? targetWindow;
    private Guid? displayedProfile;
    private TimingProfile[] quickProfiles = [];
    private void DashboardSizeChanged(object sender, SizeChangedEventArgs e) => LayoutChanged(sender, e);
    private void SafeModeDescriptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SafeTestBadge is null || SafeTestDescription is null) return;
        bool logical = SafeTestModeInput.SelectedIndex == 1;
        SafeTestBadge.Text = logical ? "Logical simulation only" : "Controlled input test";
        SafeTestDescription.Text = logical ? "Simulated timing events only. No live clicks are sent." : "Fixed CPS sends input only to the controlled test pad.";
    }
    private void DashboardClicked(object sender, RoutedEventArgs e) { DashboardScroll.ScrollToTop(); CpsInput.Focus(); }
    private void HotkeysClicked(object sender, RoutedEventArgs e) { ShortcutsExpander.IsExpanded = true; ShortcutsExpander.BringIntoView(); ActivateInput.Focus(); }
    private void TargetClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop(); activeTest?.StopTest();
        if (InspectorHost.Visibility == Visibility.Visible) { DesktopTargetInput.BringIntoView(); DesktopTargetInput.Focus(); return; }
        if (targetWindow is not null) { targetWindow.Activate(); return; }
        InspectorHost.Content = null;
        targetWindow = new Window(); DialogLayout.Setup(targetWindow, "Target and profile", 480, 760); targetWindow.Owner = this;
        targetWindow.Content = new ScrollViewer { Content = InspectorContent, Margin = new(20), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        targetWindow.Closed += (_, _) =>
        {
            ((ScrollViewer)targetWindow.Content).Content = null; InspectorHost.Content = InspectorContent; targetWindow = null;
        };
        targetWindow.Show(); DesktopTargetInput.Focus();
    }
    private void RefreshQuickProfiles()
    {
        if (profiles is null || QuickProfileInput is null) return;
        var items = profiles.Load();
        if (displayedProfile == settings.ActiveProfileId && quickProfiles.SequenceEqual(items))
        { UpdateQuickSummary(); return; }
        updatingQuickProfiles = true;
        try
        {
            quickProfiles = items; displayedProfile = settings.ActiveProfileId;
            QuickProfileInput.ItemsSource = items; QuickProfileInput.DisplayMemberPath = nameof(TimingProfile.Name);
            QuickProfileInput.SelectedItem = items.FirstOrDefault(p => p.Id == settings.ActiveProfileId);
        }
        finally { updatingQuickProfiles = false; }
        UpdateQuickSummary();
    }
    private void UpdateQuickSummary()
    {
        var selected = quickProfiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId);
        ProfilePlaceholder.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        QuickProfileSummary.Text = selected is null ? "Custom configuration · Save to create a profile" : selected.Summary + (selected.Matches(settings) ? "" : " · modified");
    }
    private void QuickProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || updatingQuickProfiles || QuickProfileInput.SelectedItem is not TimingProfile profile) return;
        try { ApplyProfile(profile); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { MessageText.Text = "Profile could not be applied. Review it in Profiles."; }
    }
    private void QuickProfileSaveClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop();
        var existing = quickProfiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId);
        if (existing is null) { ProfilesClicked(sender, e); return; }
        try
        {
            var replacement = TimingProfile.Capture(settings, existing.Name) with { Id = existing.Id, Created = existing.Created, Modified = DateTimeOffset.UtcNow >= existing.Created ? DateTimeOffset.UtcNow : existing.Created, Description = existing.Description };
            profiles.Save(replacement); UpdateProfileLabel(); MessageText.Text = "Profile saved. Clicking remains stopped.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { MessageText.Text = "Profile could not be saved. Check the profile and user-folder access."; history.Add(ActivityKind.PersistenceError); }
    }
    private void RenderInspector()
    {
        if (InspectorIdentity is null) return;
        var snapshot = desktopTarget.Snapshot;
        TargetPlaceholder.Visibility = DesktopTargetInput.SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;
        InspectorFeedback.Text = VariableValidationText.Text;
        InspectorFeedback.Visibility = string.IsNullOrEmpty(InspectorFeedback.Text) ? Visibility.Collapsed : Visibility.Visible;
        InspectorIdentity.Text = snapshot is null ? "No window selected" : snapshot.VerifiedRoblox ? "Roblox client" : "Desktop application";
        InspectorVerification.Text = snapshot is null ? "Select a window for Variable CPS" : snapshot.VerifiedRoblox ? "Roblox identity verified at selection" : "Desktop metadata checked at selection";
        InspectorVerification.Foreground = (Brush)FindResource(snapshot is { Valid: true, VerifiedRoblox: true } ? "Success" : "Muted");
        InspectorProcess.Text = snapshot?.Identity ?? "—"; InspectorTitle.Text = snapshot?.Title ?? "—";
        InspectorFocus.Text = snapshot is null ? "No target" : snapshot.Foreground ? "●  In focus" : "○  Not in focus";
        InspectorValidity.Text = snapshot is null ? "Not selected" : snapshot.Valid ? "●  Valid" : "!  Unavailable";
        InspectorPointer.Text = snapshot is null ? "Not measured" : snapshot.PointerInside ? "●  Inside window" : "○  Outside window";
        InspectorFocus.Foreground = (Brush)FindResource(snapshot?.Foreground == true ? "Success" : "Muted");
        InspectorValidity.Foreground = (Brush)FindResource(snapshot?.Valid == true ? "Success" : "Muted");
        InspectorPointer.Foreground = (Brush)FindResource(snapshot?.PointerInside == true ? "Success" : "Muted");
        ConfiguredIntervalText.Text = settings.TimingMode == ClickTimingMode.Fixed ? $"≈ {Speed.Interval(settings.Cps):0.#} ms" : $"{1000d / settings.MaximumCps:0.#}–{1000d / settings.MinimumCps:0.#} ms";
        StateGlyph.Text = engine.State switch { EngineState.Running => "▶", EngineState.Paused => "Ⅱ", EngineState.Error => "!", _ => "■" };
        RailState.Text = RailColumn.Width.Value == 58 ? "●" : engine.Enabled ? "●  " + EffectiveState : "●  Ready";
        RailState.ToolTip = EffectiveState;
    }
    private void LayoutShell()
    {
        if (InspectorHost is null) return;
        bool compact = ActualWidth < 1120;
        RailColumn.Width = new(compact ? 58 : 170);
        RailFooter.Margin = compact ? new Thickness(2, 0, 0, 0) : new Thickness(14, 0, 0, 0);
        foreach (var button in new[] { NavDashboard, NavProfiles, NavTarget, NavSettings, NavHotkeys, NavUpdates }) button.Padding = compact ? new(9, 15, 9, 15) : new(13, 15, 13, 15);
        foreach (var element in new FrameworkElement[] { BrandWordmark, BrandDescriptor, NavDashboardLabel, NavProfilesLabel, NavTargetLabel, NavSettingsLabel, NavHotkeysLabel, NavUpdatesLabel }) element.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        bool inspector = ActualWidth >= 1240;
        InspectorColumn.Width = new(inspector ? 258 : 0); InspectorHost.Visibility = inspector ? Visibility.Visible : Visibility.Collapsed;
        StateRing.Visibility = DashboardScroll.ActualWidth < 680 ? Visibility.Collapsed : Visibility.Visible;
    }
}
