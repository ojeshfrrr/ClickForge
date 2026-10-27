using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ClickForge.Core;

namespace ClickForge;

internal sealed class SideButtonControlWindow : Window
{
    private readonly TextBlock state = DialogLayout.Text("Stopped");
    private readonly TextBlock detail = DialogLayout.Text("", true);
    private readonly TextBlock leftState = DialogLayout.Text("Off", true), rightState = DialogLayout.Text("Off", true);
    private readonly TextBlock speed = DialogLayout.Text("");
    private readonly TextBlock shortcuts = DialogLayout.Text("", true);
    private readonly TextBlock setupTitle = new();
    internal ScrollViewer DetailsScroll { get; }
    internal Button SetupControl { get; }
    internal Button LeftControl { get; }
    internal Button RightControl { get; }
    internal Button EmergencyControl { get; }
    internal Button SafeTestControl { get; }
    internal Slider SpeedControl { get; } = new() { Minimum = 1, Maximum = 50, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new(0, 4, 0, 4) };
    internal string LeftStatus => leftState.Text;
    internal string RightStatus => rightState.Text;
    internal string DisplayedState => state.Text;
    private bool refreshing;
    private (Settings, bool, bool, EngineState, bool, bool, ClickButton, string, string?)? last;

    internal SideButtonControlWindow(Action setup, Action left, Action right, Action emergency, Action safeTest, Action<int> changeSpeed)
    {
        DialogLayout.Setup(this, "Mouse controls", 540, 620);
        MinWidth = 440; MinHeight = 500; Height = Math.Min(620, SystemParameters.WorkArea.Height - 40);
        state.FontSize = 24; state.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
        var content = new StackPanel();
        content.Children.Add(state); content.Children.Add(detail);
        SetupControl = DialogLayout.Button("Apply mouse setup", setup, true);
        setupTitle.SetResourceReference(TextBlock.ForegroundProperty, "Ink"); SetupControl.Content = setupTitle;
        SetupControl.ToolTip = "Enable both assignments in Toggle mode. Set Activate to F6, Pause to F7 and Emergency stop to F8. Keeps speed and target restrictions. Input remains stopped.";
        content.Children.Add(SetupControl); content.Children.Add(shortcuts);
        LeftControl = DialogLayout.Button("Toggle Left", left);
        RightControl = DialogLayout.Button("Toggle Right", right);
        content.Children.Add(OutputRow("Back · Mouse 4", "Left click", leftState, LeftControl));
        content.Children.Add(OutputRow("Front · Mouse 5", "Right click", rightState, RightControl));
        content.Children.Add(DialogLayout.Text("Press again to stop. The other side button switches output.", true));
        content.Children.Add(speed);
        AutomationProperties.SetName(SpeedControl, "Fixed click speed, 1 to 50 CPS");
        SpeedControl.ValueChanged += (_, _) => { if (!refreshing) changeSpeed((int)SpeedControl.Value); };
        content.Children.Add(SpeedControl);
        var footer = new StackPanel();
        SafeTestControl = DialogLayout.Button("Open safe test", safeTest); footer.Children.Add(SafeTestControl);
        EmergencyControl = DialogLayout.Button("Emergency stop", emergency);
        EmergencyControl.SetResourceReference(StyleProperty, "EmergencyButton"); footer.Children.Add(EmergencyControl);
        var root = new DockPanel { Margin = new(20) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        DetailsScroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(DetailsScroll);
        Content = root;
    }

    private static Border OutputRow(string source, string output, TextBlock status, Button toggle)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var text = new StackPanel(); var caption = DialogLayout.Text(source, true); caption.FontSize = 11; caption.Margin = new(0); text.Children.Add(caption);
        var title = DialogLayout.Text(output); title.FontSize = 19; title.FontWeight = FontWeights.SemiBold; title.Margin = new(0, 2, 0, 0);
        status.Margin = new(0, 3, 0, 0); status.FontSize = 11; text.Children.Add(title); text.Children.Add(status);
        grid.Children.Add(text); Grid.SetColumn(toggle, 1); toggle.VerticalAlignment = VerticalAlignment.Center; toggle.Margin = new(12, 0, 0, 0); grid.Children.Add(toggle);
        var border = new Border { Child = grid, Padding = new(14, 10, 14, 10), Margin = new(0, 5, 0, 3) };
        border.SetResourceReference(StyleProperty, "PanelStyle"); return border;
    }

    internal void Refresh(Settings settings, bool ready, bool blocked, EngineState actual, bool enabled, bool manualPause, ClickButton output, string targetReason, string? error)
    {
        var display = (settings, ready, blocked, actual, enabled, manualPause, output, targetReason, error);
        if (last == display) return; last = display;
        bool assigned = settings.SideButtonToggles && settings.Mode == ActivationMode.Toggle;
        string active = actual == EngineState.Error ? "Error" : enabled ? manualPause ? "Paused" : actual == EngineState.Running ? "Sending" : "Waiting for target" : "Off";
        leftState.Text = output == ClickButton.Left ? active : "Off";
        rightState.Text = output == ClickButton.Right ? active : "Off";
        state.Text = enabled ? ClickButtons.OutputLabel(output, settings.CustomKey) + " · " + active : actual == EngineState.Error ? "Input error" : "Stopped";
        detail.Text = error ?? (!ready ? "Shortcuts unavailable. Review the dashboard message and apply again." : !assigned ? "Apply mouse setup to enable the two toggle assignments." : blocked ? "Close the open test or configuration window before activating." : targetReason);
        setupTitle.Text = assigned && ready ? "Reapply mouse setup" : "Apply mouse setup";
        shortcuts.Text = $"Both toggle · Pause {ShortcutKeys.Label(settings.PauseKey)} · Stop {ShortcutKeys.Label(settings.StopKey)}";
        LeftControl.IsEnabled = assigned && ready && (!blocked || enabled && output == ClickButton.Left);
        RightControl.IsEnabled = assigned && ready && (!blocked || enabled && output == ClickButton.Right);
        LeftControl.Content = enabled && output == ClickButton.Left ? "Stop Left" : "Toggle Left";
        RightControl.Content = enabled && output == ClickButton.Right ? "Stop Right" : "Toggle Right";
        EmergencyControl.Content = "Emergency stop · " + ShortcutKeys.Label(settings.StopKey);
        bool fixedMode = settings.TimingMode == ClickTimingMode.Fixed;
        speed.Text = fixedMode ? $"Speed · {settings.Cps} CPS" : $"Variable speed · {settings.MinimumCps}–{settings.MaximumCps} CPS (change on dashboard)";
        SpeedControl.Visibility = fixedMode ? Visibility.Visible : Visibility.Collapsed;
        refreshing = true; try { SpeedControl.Value = settings.Cps; } finally { refreshing = false; }
    }
}
