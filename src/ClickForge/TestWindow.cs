using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using ClickForge.Core;

namespace ClickForge;
public enum SafeTestMode { FixedCps, VariableTiming }

public sealed class TestWindow : Window, ITargetGate
{
    private ClickEngine? fixedEngine;
    private readonly VariableTimingSimulation simulation = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly Border pad = new() { Height = 150, CornerRadius = new(7), Child = new TextBlock { Text = "Fixed-CPS mouse input target", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    internal readonly ComboBox ModeSelector = new() { Margin = new(0, 7, 0, 13) };
    internal readonly ComboBox ButtonSelector = new() { Margin = new(0, 4, 0, 10) };
    private readonly TextBlock buttonCaption = new() { Text = "Click button (controlled input test)" };
    private readonly Expander logExpander = new();
    internal readonly TextBox MinimumInput = new() { Text = "5" };
    internal readonly TextBox MaximumInput = new() { Text = "20" };
    internal readonly TextBox SeedInput = new();
    private readonly TextBlock configured = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 0) };
    private readonly TextBlock explanation = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 13) };
    private readonly TextBlock validation = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 9, 0, 0) };
    private readonly TextBox eventLog = new() { IsReadOnly = true, Height = 150, FontFamily = new("Consolas"), FontSize = 12, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button start = new() { Content = "Start test", Margin = new(0, 0, 7, 0) };
    private readonly StackPanel rangePanel = new();
    private readonly TimingScope scope = new() { Height = 150, Margin = new(0, 6, 0, 8) };
    private readonly TextBlock scopeSummary = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 8) };
    private readonly TextBlock scopeLegend = new() { Text = "Timing scope · gold: scheduled · green: measured · last 100 samples", TextWrapping = TextWrapping.Wrap };
    internal TimingScopeSummary ScopeSummary => TimingScopeSummary.Calculate(simulation.Snapshot.Events);
    internal int ScopeSampleCount => scope.SampleCount;
    private nint handle;
    private volatile bool pointerInside;
    private volatile bool keyboardTest, padKeyboardFocused;
    private readonly int customKey;
    private sealed record PadRegion(Rect Bounds);
    private PadRegion? padRegion;
    private DateTime end;
    private int received, released;
    private readonly Queue<string> buttonEvents = new();
    private readonly List<ClickButton> testOutputs = [];
    private bool initialized, started;
    internal SimulationSnapshot VariableSnapshot => simulation.Snapshot;
    internal bool FixedTestEnabled => fixedEngine?.Enabled == true;
    public bool CanClick => pointerInside && (!keyboardTest || padKeyboardFocused) && Native.GetForegroundWindow() == handle && !Native.IsIconic(handle)
        && Native.GetCursorPos(out var point) && Volatile.Read(ref padRegion)?.Bounds.Contains(new Point(point.X, point.Y)) == true
        && Native.GetAncestor(Native.WindowFromPoint(point), 2) == handle;
    private bool Variable => ModeSelector.SelectedIndex == (int)SafeTestMode.VariableTiming;

    internal int ReceivedDowns => received;
    internal Border TestPad => pad;
    internal int ReceivedUps => released;
    internal Point PadCenter => pad.PointToScreen(new Point(pad.ActualWidth / 2, pad.ActualHeight / 2));
    private ClickButton SelectedButton => ButtonSelector.SelectedIndex >= 0 && ButtonSelector.SelectedIndex < testOutputs.Count
        ? testOutputs[ButtonSelector.SelectedIndex] : throw new InvalidOperationException("Choose a supported test output.");
    internal ClickButton TestOutput => SelectedButton;
    internal void SelectOutput(ClickButton button)
    {
        int index = testOutputs.IndexOf(button);
        if (index < 0) throw new ArgumentException("That output is unavailable in this test.");
        ButtonSelector.SelectedIndex = index;
    }
    public TestWindow(SafeTestMode mode = SafeTestMode.FixedCps, ClickButton button = ClickButton.Left, int customKey = 0)
    {
        KeyboardOutput.ValidateOutput(button,customKey);this.customKey=customKey;
        Title = "ClickForge · Safe Click Test"; Width = 610; Height = 820; MinWidth = 520; MinHeight = 560;
        Height = Math.Min(Height, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 50));
        SetResourceReference(BackgroundProperty, "Base"); SetResourceReference(IconProperty, "ForgeIcon");
        pad.SetResourceReference(Border.BackgroundProperty, "Elevated");
        configured.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        explanation.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        validation.SetResourceReference(TextBlock.ForegroundProperty, "Warning");
        var panel = new StackPanel { Margin = new(22) };
        panel.Children.Add(new TextBlock { Text = "Safe Click Test", FontSize = 23, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Test mode", Margin = new(0, 16, 0, 0) });
        ModeSelector.Items.Add("Fixed CPS · controlled input target");
        ModeSelector.Items.Add("Variable Timing — Safe Test Only");
        AutomationProperties.SetName(ModeSelector, "Safe test timing mode");
        panel.Children.Add(ModeSelector); panel.Children.Add(explanation);
        panel.Children.Add(buttonCaption);
        foreach (var choice in ClickButtons.MouseChoices) { testOutputs.Add(choice); ButtonSelector.Items.Add(ClickButtons.Label(choice)); }
        if (KeyboardOutput.IsValid(customKey)) { testOutputs.Add(ClickButton.CustomKey); ButtonSelector.Items.Add(ClickButtons.OutputLabel(ClickButton.CustomKey, customKey)); }
        AutomationProperties.SetName(ButtonSelector, "Safe test output, separate from global shortcuts"); SelectOutput(button);
        panel.Children.Add(ButtonSelector);
        var fields = new Grid();
        for (int i = 0; i < 3; i++) fields.ColumnDefinitions.Add(new());
        AddField(fields, 0, "Minimum CPS (5–20)", MinimumInput);
        AddField(fields, 1, "Maximum CPS (5–20)", MaximumInput);
        AddField(fields, 2, "Seed (optional integer)", SeedInput);
        rangePanel.Children.Add(fields); rangePanel.Children.Add(configured);
        panel.Children.Add(rangePanel); panel.Children.Add(validation); panel.Children.Add(status);
        panel.Children.Add(scopeLegend); panel.Children.Add(scope); panel.Children.Add(scopeSummary);
        panel.Children.Add(pad); logExpander.Content = eventLog; panel.Children.Add(logExpander);
        var actions = new Grid { Margin = new(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new());
        start.SetResourceReference(StyleProperty, "PrimaryButton");
        start.Content=new TextBlock{Text="Start test",Foreground=(Brush)FindResource("Ink"),FontWeight=FontWeights.SemiBold};
        var stop = new Button { Content = "Stop test", Margin = new(7, 0, 0, 0) };
        stop.SetResourceReference(StyleProperty, "EmergencyButton"); Grid.SetColumn(stop, 1);
        actions.Children.Add(start); actions.Children.Add(stop);
        panel.Children.Add(new TextBlock { Text = "Closing or leaving this window stops the test. The main emergency shortcut also stops it.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0), Foreground = (Brush)FindResource("Muted"), FontSize = 12 });
        var root = new DockPanel();
        actions.Margin = new(22, 8, 22, 14); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
        start.Click += (_, _) => StartTest(); stop.Click += (_, _) => StopTest();
        pad.MouseEnter += (_, _) => pointerInside = true;
        pad.MouseLeave += (_, _) => pointerInside = false;
        pad.PreviewMouseDown += (_, e) => { RecordButton(e.ChangedButton, true); e.Handled = true; };
        pad.PreviewMouseUp += (_, e) => { RecordButton(e.ChangedButton, false); e.Handled = true; };
        pad.Focusable=true;
        pad.GotKeyboardFocus+=(_,_)=>padKeyboardFocused=true;pad.LostKeyboardFocus+=(_,_)=>padKeyboardFocused=false;
        pad.PreviewKeyDown+=(_,e)=>{if(SelectedButton==ClickButton.CustomKey){RecordKey(KeyInterop.VirtualKeyFromKey(e.Key==Key.System?e.SystemKey:e.Key),true);e.Handled=true;}};
        pad.PreviewKeyUp+=(_,e)=>{if(SelectedButton==ClickButton.CustomKey){RecordKey(KeyInterop.VirtualKeyFromKey(e.Key==Key.System?e.SystemKey:e.Key),false);e.Handled=true;}};
        ButtonSelector.SelectionChanged += (_, _) => { if (initialized) { StopTest(); received = released = 0; buttonEvents.Clear(); eventLog.Clear(); started = false; UpdateMode(); } };
        foreach (var input in new[] { MinimumInput, MaximumInput, SeedInput }) input.TextChanged += (_, _) => { if (initialized) ValidateInputs(); };
        ModeSelector.SelectionChanged += (_, _) => { if (initialized) { StopTest(); started = false; UpdateMode(); } };
        ModeSelector.SelectedIndex = (int)mode; initialized = true; UpdateMode();
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            int dark = 1; Native.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        };
        Deactivated += (_, _) => StopTest();
        LocationChanged += (_, _) => UpdatePadRegion(); SizeChanged += (_, _) => UpdatePadRegion();
        timer.Tick += (_, _) => RefreshStatus(); timer.Start();
        Closed += (_, _) => { timer.Stop(); simulation.Dispose(); fixedEngine?.Dispose(); };
    }
    private static void AddField(Grid grid, int column, string label, TextBox input)
    {
        var field = new StackPanel { Margin = new(0, 0, column < 2 ? 10 : 0, 0) };
        field.Children.Add(new TextBlock { Text = label, FontSize = 12, Margin = new(0, 0, 0, 7), TextWrapping = TextWrapping.Wrap });
        AutomationProperties.SetName(input, label); field.Children.Add(input); Grid.SetColumn(field, column); grid.Children.Add(field);
    }
    private bool TryConfiguration(out VariableTimingRange? range, out int? seed, out string? error)
    {
        range = null; seed = null; error = null;
        if (!int.TryParse(MinimumInput.Text, out int minimum) || !int.TryParse(MaximumInput.Text, out int maximum))
        { error = "Enter whole-number rates from 5 to 20 CPS."; return false; }
        try { range = new(minimum, maximum); }
        catch (ArgumentException e) { error = e.Message.Split(Environment.NewLine)[0]; return false; }
        if (!string.IsNullOrWhiteSpace(SeedInput.Text))
        {
            if (!int.TryParse(SeedInput.Text, out int parsed)) { error = "Seed must be a 32-bit integer or blank."; return false; }
            seed = parsed;
        }
        return true;
    }
    private void ValidateInputs()
    {
        bool valid = TryConfiguration(out var range, out int? seed, out string? error);
        validation.Text = error ?? ""; start.IsEnabled = !simulation.Snapshot.Running && (!Variable || valid);
        configured.Text = valid ? $"Configured range: {range!.MinimumCps}–{range.MaximumCps} CPS · {range.ShortestIntervalMs:0.##}–{range.LongestIntervalMs:0.##} ms intervals\n{(seed.HasValue ? $"Repeatable interval sequence · seed {seed}" : "Fresh random interval sequence each run")}" : "Configured range is invalid.";
    }
    private void UpdateMode()
    {
        rangePanel.Visibility = Variable ? Visibility.Visible : Visibility.Collapsed;
        scope.Visibility = scopeSummary.Visibility = scopeLegend.Visibility = Variable ? Visibility.Visible : Visibility.Collapsed;
        eventLog.Visibility = Visibility.Visible;
        ButtonSelector.IsEnabled = !Variable;
        buttonCaption.Visibility = ButtonSelector.Visibility = Variable ? Visibility.Collapsed : Visibility.Visible;
        logExpander.Header = Variable ? "Logical event samples" : "Received input events"; logExpander.IsExpanded = !Variable;
        pad.Visibility = Variable ? Visibility.Collapsed : Visibility.Visible;
        explanation.Text = Variable ? "Logical simulation only; no live input. Each run lasts up to 5 seconds. A seed repeats scheduled intervals; Windows scheduling affects measurements. Timing does not establish target input acceptance." : "Controlled test: 2 seconds at 10 CPS using the selected button. Move the pointer inside the pad. Down and up events received here are shown below; no Roblox input is used.";
        if(!Variable&&SelectedButton==ClickButton.CustomKey)explanation.Text="Controlled keyboard test: 2 seconds at 10 CPS. Keep the pointer and keyboard focus inside the pad. It records key down/up events; no Roblox input is used.";
        ((TextBlock)pad.Child).Text=SelectedButton==ClickButton.CustomKey?"Keyboard test pad · "+KeyboardOutput.Label(customKey):"Fixed-CPS mouse input target";
        buttonCaption.Text = SelectedButton == ClickButton.CustomKey ? "Keyboard output (controlled input test)" : "Mouse output (controlled input test)";
        status.Text = Variable ? "Ready · logical events only" : "Ready · 10 CPS · "+ClickButtons.OutputLabel(SelectedButton,customKey);
        ValidateInputs(); if (!Variable) { validation.Text = ""; start.IsEnabled = true; }
    }
    internal void StartTest()
    {
        UpdatePadRegion();
        if (Variable)
        {
            if (!TryConfiguration(out var range, out int? seed, out string? error)) { validation.Text = error; return; }
            if (!simulation.Completion.IsCompleted) { validation.Text = "The previous simulation is stopping. Start again when it finishes."; return; }
            _ = simulation.Start(range!, seed);
            scope.SetSamples([]); scopeSummary.Text = "Waiting for timing samples…";
            eventLog.Text = "#    elapsed ms   requested ms   measured ms";
        }
        else
        {
            fixedEngine ??= new(new MouseInputService(), this);
            keyboardTest=SelectedButton==ClickButton.CustomKey;
            if(keyboardTest)pad.Focus();
            fixedEngine.ConfigureOutput(SelectedButton,customKey);
            received = released = 0; buttonEvents.Clear(); eventLog.Clear(); end = DateTime.UtcNow.AddSeconds(2);
            try { fixedEngine.Start(); }
            catch (InvalidOperationException e) { validation.Text = e.Message; return; }
        }
        started = true; SetFieldsEnabled(false); start.IsEnabled = false; RefreshStatus();
    }
    internal void StopTest()
    {
        simulation.Stop(); fixedEngine?.Stop();
        SetFieldsEnabled(true);
        if (initialized) { ValidateInputs(); if (!Variable) start.IsEnabled = true; RefreshStatus(); }
    }
    private void SetFieldsEnabled(bool enabled)
    {
        MinimumInput.IsEnabled = enabled; MaximumInput.IsEnabled = enabled; SeedInput.IsEnabled = enabled;
        ButtonSelector.IsEnabled = enabled && !Variable;
    }
    private void RefreshStatus()
    {
        if (Variable)
        {
            var snapshot = simulation.Snapshot;
            if (started)
            {
                var measured = snapshot.Measurement;
                scope.SetSamples(snapshot.Events);
                var summary = TimingScopeSummary.Calculate(snapshot.Events);
                scopeSummary.Text = $"Scheduled mean: {summary.ScheduledMean:0.00} ms · measured mean: {summary.MeasuredMean:0.00} ms\nMeasured spread: {summary.Minimum:0.00}–{summary.Maximum:0.00} ms · standard deviation: {summary.StandardDeviation:0.00} ms\nScheduling delay: mean {summary.MeanDelay:0.00} ms · maximum {summary.MaximumDelay:0.00} ms";
                status.Text = $"{(snapshot.Error is not null ? "Error" : snapshot.Running ? "Simulating" : snapshot.Cancelled ? "Stopped" : "Completed")} · {measured.EventCount} logical events\nMeasured average: {measured.AverageCps:0.00} CPS · mean interval: {measured.MeanIntervalMs:0.00} ms\nMeasured elapsed: {measured.ElapsedMs / 1000:0.000} s";
                eventLog.Text = "#    elapsed ms   requested ms   measured ms\n" + string.Join("\n", snapshot.Events.TakeLast(60).Select(e => $"{e.Number,3}  {e.ElapsedMs,10:0.00}  {e.RequestedIntervalMs,12:0.00}  {e.MeasuredIntervalMs,11:0.00}"));
                if (snapshot.Error is not null) validation.Text = snapshot.Error;
            }
            if (!snapshot.Running) { SetFieldsEnabled(true); start.IsEnabled = simulation.Completion.IsCompleted && TryConfiguration(out _, out _, out _); }
        }
        else if (fixedEngine is not null)
        {
            if (DateTime.UtcNow >= end) fixedEngine.Stop();
            status.Text = $"Configured: 10 CPS · {ClickButtons.OutputLabel(SelectedButton,customKey)} · {fixedEngine.Session.InputPairsSent} session input pairs sent\nReceived: {received} downs / {released} ups · {fixedEngine.State}";
            eventLog.Text = string.Join("\n", buttonEvents);
            if (!fixedEngine.Enabled) { SetFieldsEnabled(true); start.IsEnabled = true; }
            if (fixedEngine.Error is not null) validation.Text = fixedEngine.Error;
        }
    }
    private void RecordButton(MouseButton button, bool down)
    {
        ClickButton? mapped = button switch { MouseButton.Left => ClickButton.Left, MouseButton.Right => ClickButton.Right, MouseButton.XButton1 => ClickButton.Mouse4, MouseButton.XButton2 => ClickButton.Mouse5, _ => null };
        if (mapped != SelectedButton) return;
        if (down) received++; else released++;
        if (buttonEvents.Count == 12) buttonEvents.Dequeue();
        buttonEvents.Enqueue(ClickButtons.Label(mapped.Value) + (down ? " · DOWN" : " · UP"));
        eventLog.Text = string.Join("\n", buttonEvents);
    }
    internal void RecordKey(int key,bool down)
    {
        if(SelectedButton!=ClickButton.CustomKey || key!=customKey)return;
        if(down)received++;else released++;
        if(buttonEvents.Count==12)buttonEvents.Dequeue();buttonEvents.Enqueue("Key "+KeyboardOutput.Label(key)+(down?" · DOWN":" · UP"));eventLog.Text=string.Join("\n",buttonEvents);
    }
    internal void SendDiagnosticPair()
    {
        UpdatePadRegion();
        // Explicit native diagnostic only: one bounded pair, guarded by the actual pad.
        if (!CanClick || !pad.IsMouseOver || !Native.GetCursorPos(out var point) || Native.GetAncestor(Native.WindowFromPoint(point), 2) != handle)
            throw new InvalidOperationException("Safe pad is not under the pointer in the foreground. No input was sent.");
        var mouse = new MouseInputService();
        try { mouse.Click(SelectedButton); } finally { mouse.Release(); }
    }
    private void UpdatePadRegion()
    {
        if (!IsLoaded || !pad.IsVisible) { Volatile.Write(ref padRegion, null); return; }
        Volatile.Write(ref padRegion, new PadRegion(new Rect(pad.PointToScreen(new Point()), pad.PointToScreen(new Point(pad.ActualWidth, pad.ActualHeight)))));
    }
}
