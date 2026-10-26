using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using ClickForge.Core;

namespace ClickForge;

internal sealed class CompactControlWindow : Window
{
    private readonly TextBlock state = DialogLayout.Text("");
    private readonly TextBlock statistics = DialogLayout.Text("");
    private readonly TextBlock profile = DialogLayout.Text("", true);
    internal Button StartControl { get; }
    internal Button PauseControl { get; }
    internal Button EmergencyControl { get; }
    internal string DisplayedState => state.Text;
    internal CompactControlWindow(Action start, Action pause, Action emergency, Action restore, Action palette)
    {
        DialogLayout.Setup(this, "Compact controls", 430, 320);
        MinWidth = 340; MinHeight = 270; Height = 320; ShowInTaskbar = false;
        var panel = new StackPanel();
        state.FontSize = 21; AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
        panel.Children.Add(state); panel.Children.Add(profile); panel.Children.Add(statistics);
        var actions = new WrapPanel();
        StartControl = DialogLayout.Button("Start input", start); PauseControl = DialogLayout.Button("Pause", pause);
        EmergencyControl = DialogLayout.Button("Emergency stop", emergency); EmergencyControl.SetResourceReference(StyleProperty, "EmergencyButton");
        var bottom = new StackPanel();
        actions.Children.Add(StartControl); actions.Children.Add(PauseControl); actions.Children.Add(EmergencyControl); bottom.Children.Add(actions);
        var secondary = new WrapPanel(); secondary.Children.Add(DialogLayout.Button("Open dashboard", restore)); secondary.Children.Add(DialogLayout.Button("Commands…", palette)); bottom.Children.Add(secondary);
        var top = new CheckBox { Content = "Keep on top", Margin = new(0, 2, 0, 4) };
        top.Checked += (_, _) => Topmost = true; top.Unchecked += (_, _) => Topmost = false; bottom.Children.Add(top);
        var root = new DockPanel { Margin = new(16) }; DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
    }
    internal void Refresh(string actualState, SessionSnapshot session, string activeProfile, ControlAvailability controls, string limits, string? error)
    {
        state.Text = actualState; profile.Text = "Profile: " + activeProfile;
        string sample = session.HasStarted && session.Elapsed.TotalSeconds < 1 ? " (sample under 1s)" : "";
        statistics.Text = $"{(int)session.Elapsed.TotalHours:00}:{session.Elapsed.Minutes:00}:{session.Elapsed.Seconds:00} wall-clock · {session.InputPairsSent:N0} input pairs sent\nMeasured average {session.MeasuredAverageCps:0.00} CPS{sample}\n{limits}" + (error is null ? "" : "\n" + error);
        StartControl.Content = controls.StartLabel; StartControl.IsEnabled = controls.StartEnabled;
        PauseControl.Content = controls.PauseLabel; PauseControl.IsEnabled = controls.PauseEnabled;
        StartControl.ToolTip = controls.StartEnabled ? "Shared validated activation; never bypasses target checks." : "Close configuration/test views and register shortcuts. Hold mode uses its activation shortcut.";
    }
}
