using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClickForge.Core;

namespace ClickForge;
public partial class MainWindow : Window
{
    private readonly SettingsService store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge", "settings.json"));
    private readonly ForegroundTargetService target = new();
    private readonly ClickEngine engine;
    private Settings settings;
    private HotkeyBindings? bindings;
    private GlobalHotkeyService? globalShortcuts;
    private HwndSource? source;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool ready, shortcutsReady, closing;
    private nint holdWindow;
    private TestWindow? activeTest;
    private long displayedClicks, nextCounterUpdate;
    private (EngineState, bool, bool, long, int, ActivationMode, bool, bool, int, int, int)? lastDisplay;
    private readonly string? previewPath = GetPreviewPath();
    private readonly string? selfTestPath = GetArgument("--ui-self-test") ?? GetArgument("--ui-interactive-test") ?? GetArgument("--performance-test");
    private readonly bool performanceTest = GetArgument("--performance-test") is not null;
    private readonly bool interactiveTest = GetArgument("--ui-interactive-test") is not null;
    private static string? GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
    private static string? GetPreviewPath()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--render-preview");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    public MainWindow()
    {
        if (selfTestPath is not null) store = new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(selfTestPath))!, "ui-test-settings.json"));
        else if (previewPath is not null) store = new SettingsService(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(previewPath))!, "preview-settings.json"));
        settings = store.Load();
        if (settings.LayoutVersion < 3) settings = settings with { Width = 1280, Height = 920, LayoutVersion = 3 };
        engine = selfTestPath is null ? new(new MouseInputService(), target, desktopTarget) : new(new DiagnosticMouse(), new DiagnosticTarget());
        InitializeComponent();
        foreach (var button in Enum.GetValues<ClickButton>()) ClickButtonInput.Items.Add(ClickButtons.Label(button));
        InitializeUpdates();
        foreach (var combo in new[] { ActivateInput, PauseInput, StopInput }) foreach (int key in ShortcutKeys.Choices) combo.Items.Add(ShortcutKeys.Label(key));
        LoadControls();
        MessageText.Text = store.Warning ?? "Starts disabled. Clicks are sent at the current pointer.";
        InitializeDaily();
        ready = true;
        SourceInitialized += (_, _) =>
        {
            int dark = 1;
            Native.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
            if (previewPath is not null || (selfTestPath is not null && !interactiveTest)) { shortcutsReady = true; return; }
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WindowMessage);
            globalShortcuts = new(source.Handle); bindings = new(globalShortcuts);
            RegisterShortcuts(); InitializeTray();
            if (shortcutsReady && store.Warning is not null) MessageText.Text = store.Warning;
            else if (shortcutsReady && profiles.Warnings.Count > 0) MessageText.Text = string.Join(" ", profiles.Warnings.Distinct());
        };
        engine.ActivationStillValid = () => settings.Mode != ActivationMode.Hold ||
            ((ShortcutKeys.IsMouse(settings.ActivateKey) ? globalShortcuts?.IsHeld(settings.ActivateKey) == true : (Native.GetAsyncKeyState(settings.ActivateKey) & 0x8000) != 0) && Native.GetForegroundWindow() == holdWindow);
        refresh.Tick += (_, _) => UpdateStatus(); refresh.Start();
        Closing += OnClosing;
        ContentRendered += (_, _) =>
        {
            if (performanceTest) { _ = RunPerformanceTestAsync(selfTestPath!); return; }
            if (interactiveTest) { Title = "ClickForge · UI verification (mock input)"; MessageText.Text = "UI verification: input is mocked and configuration is disposable."; return; }
            if (selfTestPath is not null) File.AppendAllText(selfTestPath, "WPF content rendered" + Environment.NewLine);
            if (selfTestPath is not null) { _ = RunUiSelfTestAsync(selfTestPath); return; }
            if (previewPath is null) return;
            UpdateStatus();
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(previewPath)) encoder.Save(stream);
            Close();
        };
    }
    private void LoadControls()
    {
        ready = false;
        Width = Math.Clamp(settings.Width, MinWidth, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40));
        Height = Math.Clamp(settings.Height, MinHeight, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 40));
        string[] previewArgs = Environment.GetCommandLineArgs();
        int resizeIndex = Array.IndexOf(previewArgs, "--preview-size");
        if (previewPath is not null && resizeIndex >= 0 && resizeIndex + 2 < previewArgs.Length) { Width = double.Parse(previewArgs[resizeIndex + 1], System.Globalization.CultureInfo.InvariantCulture); Height = double.Parse(previewArgs[resizeIndex + 2], System.Globalization.CultureInfo.InvariantCulture); }
        CpsInput.Text = settings.Cps.ToString(); CpsSlider.Value = settings.Cps;
        ToggleModeInput.IsChecked = settings.Mode == ActivationMode.Toggle;
        HoldModeInput.IsChecked = settings.Mode == ActivationMode.Hold;
        RestrictInput.IsChecked = settings.RobloxOnly;
        ActivateInput.SelectedItem = ShortcutKeys.Label(settings.ActivateKey);
        PauseInput.SelectedItem = ShortcutKeys.Label(settings.PauseKey);
        StopInput.SelectedItem = ShortcutKeys.Label(settings.StopKey);
        engine.Cps = settings.Cps; target.Restricted = settings.RobloxOnly;
        UpdateSpeedLabel(); LoadTimingControls(); ready = true;
    }
    private bool Persist()
    {
        try { store.Save(settings); persistenceFailed = false; UpdateProfileLabel(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { persistenceFailed = true; history.Add(ActivityKind.PersistenceError); MessageText.Text = "Could not save preferences. Check access to your ClickForge user data folder."; return false; }
    }
    private void RegisterShortcuts()
    {
        engine.Stop(); shortcutsReady = false;
        try { bindings!.Apply(settings); shortcutsReady = true; MessageText.Text = "Shortcuts ready. F1–F11 and Mouse 4/5 are supported."; }
        catch (Exception e) { MessageText.Text = e.Message + " Clicking is disabled until shortcuts are available."; }
        StartKeyText.Text = ShortcutBadge(settings.ActivateKey); StartKeyText.ToolTip = ShortcutKeys.Label(settings.ActivateKey);
        PauseKeyText.Text = ShortcutBadge(settings.PauseKey); PauseKeyText.ToolTip = ShortcutKeys.Label(settings.PauseKey);
        StopKeyText.Text = ShortcutBadge(settings.StopKey); StopKeyText.ToolTip = ShortcutKeys.Label(settings.StopKey);
    }
    private static string ShortcutBadge(int key) => key switch { 5 => "M4", 6 => "M5", _ => ShortcutKeys.Label(key) };
    private nint WindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.MouseMessage)
        {
            if (globalShortcuts?.AcceptMouseMessage((int)wParam,lParam) != true) { handled = true; return 0; }
            msg = 0x0312;
        }
        if (msg == 0x0312)
        {
            if ((int)wParam == 3) { EmergencyClicked(this, new()); }
            else if (activeTest?.IsVisible == true) { handled = true; return 0; }
            else if (shortcutsReady && (int)wParam == 1)
            {
                if (settings.Mode == ActivationMode.Toggle) ToggleStart();
                else { holdWindow = Native.GetForegroundWindow(); StartValidated(); }
            }
            else if (shortcutsReady && (int)wParam == 2) PauseClicked(this, new());
            handled = true; UpdateStatus();
        }
        // Session locking/suspend cannot preserve an active request.
        if (msg == 0x0218 || msg == 0x0011) { engine.Stop(); activeTest?.StopTest(); }
        return 0;
    }
    private void ToggleStart() { if (activeTest?.IsVisible == true) return; if (engine.Enabled) { engine.Stop(); history.Add(ActivityKind.Stopped); } else if (shortcutsReady && settings.Mode == ActivationMode.Toggle) StartValidated(); UpdateStatus(); }
    private void StartClicked(object sender, RoutedEventArgs e) => ToggleStart();
    private void PauseClicked(object sender, RoutedEventArgs e) { engine.TogglePause(); if (engine.Enabled) history.Add(engine.IsPaused ? ActivityKind.Paused : ActivityKind.Started); UpdateStatus(); }
    private void EmergencyClicked(object sender, RoutedEventArgs e) { engine.Stop(); activeTest?.StopTest(); history.Add(ActivityKind.EmergencyStop); UpdateStatus(); }
    private void SpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        int value = (int)CpsSlider.Value;
        settings = settings with { Cps = value }; engine.Cps = value;
        CpsInput.Text = value.ToString(); UpdateSpeedLabel(); Persist();
    }
    private void CpsTyped(object sender, TextChangedEventArgs e)
    {
        if (!ready) return;
        if (int.TryParse(CpsInput.Text, out int cps) && cps is >= 1 and <= 50)
        {
            settings = settings with { Cps = cps }; engine.Cps = cps;
            CpsSlider.Value = cps; UpdateSpeedLabel(); Persist();
            MessageText.Text = "Click speed updated.";
        }
        else MessageText.Text = "Enter a whole number from 1 to 50. The last valid speed remains active.";
    }
    private void CpsLostFocus(object sender, RoutedEventArgs e) { if (ready) CpsInput.Text = settings.Cps.ToString(); }
    private void CpsKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { CpsLostFocus(sender, e); Keyboard.ClearFocus(); e.Handled = true; } }
    private void UpdateSpeedLabel() { IntervalText.Text = $"{Speed.Interval(settings.Cps):0.#} ms interval"; ConfiguredCpsText.Text = settings.Cps.ToString(); }
    private void ModeSegmentChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Stop(); settings = settings with { Mode = HoldModeInput.IsChecked == true ? ActivationMode.Hold : ActivationMode.Toggle };
        Persist(); UpdateStatus();
    }
    private void RestrictionChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Stop(); settings = settings with { RobloxOnly = RestrictInput.IsChecked == true };
        target.Restricted = settings.RobloxOnly; Persist(); UpdateStatus();
        MessageText.Text = settings.RobloxOnly ? "Roblox foreground restriction enabled." : "Unrestricted: clicks go to any foreground application. Starts disabled.";
    }
    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop();
        int SelectedKey(ComboBox combo) => combo.SelectedIndex >= 0 && combo.SelectedIndex < ShortcutKeys.Choices.Count ? ShortcutKeys.Choices[combo.SelectedIndex] : 0;
        var candidate = settings with { ActivateKey = SelectedKey(ActivateInput), PauseKey = SelectedKey(PauseInput), StopKey = SelectedKey(StopInput) };
        try { candidate.Validate(); }
        catch (ArgumentException ex) { MessageText.Text = ex.Message; return; }
        settings = candidate; RegisterShortcuts(); if (shortcutsReady) Persist(); UpdateStatus();
    }
    private void ResetClicked(object sender, RoutedEventArgs e) { engine.Stop(); var candidate=settings with { ActivateKey = 0x75, PauseKey = 0x76, StopKey = 0x77 }; try{candidate.Validate();}catch(ArgumentException ex){MessageText.Text=ex.Message;return;} settings=candidate;LoadControls(); RegisterShortcuts(); Persist(); UpdateStatus(); }
    private void TestClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop();
        activeTest = new TestWindow((SafeTestMode)SafeTestModeInput.SelectedIndex, settings.Button,settings.CustomKey) { Owner = this };
        try { activeTest.ShowDialog(); }
        finally { activeTest = null; engine.Stop(); }
    }
    private void UpdateStatus()
    {
        if (closing) return;
        UpdateRefreshCadence();
        bool active = engine.Enabled;
        if (!active || Environment.TickCount64 >= nextCounterUpdate) { displayedClicks = engine.Clicks; nextCounterUpdate = Environment.TickCount64 + 1000; }
        var display = (engine.State, active, engine.IsPaused, displayedClicks, settings.Cps, settings.Mode, settings.RobloxOnly, shortcutsReady, settings.ActivateKey, settings.PauseKey, settings.StopKey);
        if (lastDisplay == display) { UpdateDailyStatus(); return; }
        lastDisplay = display;
        StatusDot.Fill = (Brush)FindResource(engine.State switch { EngineState.Running => "Success", EngineState.Error => "Failure", EngineState.Paused => "Warning", _ => "Muted" });
        CycleText.Text = displayedClicks.ToString("N0");
        if (settings.TimingMode == ClickTimingMode.Fixed) ConfiguredCpsText.Text = settings.Cps.ToString();
        StartLabel.Text = settings.Button==ClickButton.CustomKey ? active?"Stop input":settings.Mode==ActivationMode.Hold?"Hold to repeat":"Start input" : active ? "Stop clicking" : settings.Mode == ActivationMode.Hold ? "Hold to click" : "Start clicking";
        StartIcon.Text = active ? "■" : "▶";
        StartButton.IsEnabled = shortcutsReady && (settings.Mode == ActivationMode.Toggle || active);
        PauseLabel.Text = engine.IsPaused ? "Resume" : "Pause"; PauseButton.IsEnabled = active;
        PauseButton.ToolTip = active ? "Pause or resume the active request" : "Start clicking before using pause";
        StartButton.ToolTip = !shortcutsReady ? "Apply available, nonconflicting shortcuts to enable clicking" : settings.Mode == ActivationMode.Hold && !active ? $"Hold {ShortcutKeys.Label(settings.ActivateKey)} to activate. Release it or change focus to stop." : "Start or stop the request. Roblox-only mode waits for a verified foreground client.";
        ModeHelp.Text = ShortcutKeys.ConflictsWithHoldOutput(settings) ? "Hold activation requires a different click-output button." : settings.Mode == ActivationMode.Hold ? $"Hold {ShortcutKeys.Label(settings.ActivateKey)} to click. Release or change focus to stop." : $"Press {ShortcutKeys.Label(settings.ActivateKey)} once to start, again to stop.";
        RenderVariableStatus(); UpdateDailyStatus();
        if (engine.Error is { } error) MessageText.Text = error;
    }
    private void UpdateRefreshCadence()
    {
        // State-changing commands still refresh immediately. Idle and background
        // windows wake once a second; visible session metrics update twice a second.
        var interval = TimeSpan.FromMilliseconds(engine.Enabled && IsVisible && WindowState != WindowState.Minimized ? 500 : 1000);
        if (refresh.Interval != interval) refresh.Interval = interval;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (selfTestPath is not null) File.AppendAllText(selfTestPath, "WPF closing" + Environment.NewLine);
        if (closing) return;
        if (!exitRequested && previewPath is null && settings.CloseToTray && CanHideToTray) { e.Cancel = true; HideToTray(); return; }
        closing = true; customKeyWindow?.Close(); targetWindow?.Close(); tray?.Dispose(); tray = null; profilesWindow?.Close(); preferencesWindow?.Close(); updateLifetime.Cancel(); updateClient.StatusChanged -= UpdateChanged; updateWindow?.Close(); refresh.Stop(); activeTest?.StopTest(); engine.Dispose(); desktopTarget.Dispose(); target.Dispose(); bindings?.Dispose(); source?.RemoveHook(WindowMessage);
        if (previewPath is null && selfTestPath is null) { settings = settings with { Width = Math.Clamp(Width, 620, 1400), Height = Math.Clamp(Height, 600, 1600), LayoutVersion = 3 }; Persist(); }
    }
    private void LayoutChanged(object sender, SizeChangedEventArgs e)
    {
        if (OptionsGrid is null) return;
        LayoutShell();
        bool narrow = DashboardScroll.ActualWidth < 680;
        OptionsGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(RestrictionPanel, narrow ? 0 : 1); Grid.SetRow(RestrictionPanel, narrow ? 1 : 0);
        ModePanel.Margin = narrow ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 6, 0);
        RestrictionPanel.Margin = narrow ? new Thickness(0) : new Thickness(6, 0, 0, 0);
        if (StatusGrid.RowDefinitions.Count == 0) { StatusGrid.RowDefinitions.Add(new() { Height = GridLength.Auto }); StatusGrid.RowDefinitions.Add(new() { Height = GridLength.Auto }); }
        for (int i = 0; i < StatusGrid.Children.Count; i++)
        {
            var child = (FrameworkElement)StatusGrid.Children[i];
            Grid.SetRow(child, narrow && i >= 2 ? 1 : 0);
            Grid.SetColumn(child, narrow ? i % 2 * 2 : i);
            Grid.SetColumnSpan(child, narrow ? 2 : 1);
            if (i >= 2) child.Margin = narrow ? new Thickness(0, 14, 0, 0) : new Thickness(0);
        }
    }
}
