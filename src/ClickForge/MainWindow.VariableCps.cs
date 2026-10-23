using System.Windows;
using System.Windows.Controls;
using ClickForge.Core;

namespace ClickForge;

public partial class MainWindow
{
    private readonly DesktopTargetService desktopTarget = new();
    private bool synchronizingVariable;
    private void LoadTimingControls()
    {
        FixedTimingInput.IsChecked = settings.TimingMode == ClickTimingMode.Fixed;
        VariableTimingInput.IsChecked = settings.TimingMode == ClickTimingMode.Variable;
        SynchronizeRange();
        engine.ConfigureTiming(settings.TimingMode, new(settings.MinimumCps, settings.MaximumCps));
        RenderTimingMode();
    }
    private void RenderTimingMode()
    {
        bool variable = settings.TimingMode == ClickTimingMode.Variable;
        FixedSpeedPanel.Visibility = variable ? Visibility.Collapsed : Visibility.Visible;
        VariableSpeedPanel.Visibility = variable ? Visibility.Visible : Visibility.Collapsed;
        RestrictInput.IsEnabled = !variable;
        RestrictionHelp.Text = variable ? "Variable CPS always requires the selected window, including a verified Roblox client. Your fixed-mode preference is preserved." : "Clicking pauses automatically when Roblox loses foreground focus.";
        lastDisplay = null;
    }
    private void SynchronizeRange()
    {
        synchronizingVariable = true;
        MinimumCpsInput.Text = settings.MinimumCps.ToString(); MaximumCpsInput.Text = settings.MaximumCps.ToString();
        MinimumCpsSlider.Value = settings.MinimumCps; MaximumCpsSlider.Value = settings.MaximumCps;
        VariableRangeText.Text = $"Configured: {settings.MinimumCps}–{settings.MaximumCps} CPS · {1000d / settings.MaximumCps:0.#}–{1000d / settings.MinimumCps:0.#} ms intervals";
        VariableValidationText.Text = "";
        synchronizingVariable = false;
    }
    private bool TryRange(out CpsRange range)
    {
        range = default;
        try
        {
            if (!int.TryParse(MinimumCpsInput.Text, out int min) || !int.TryParse(MaximumCpsInput.Text, out int max))
                throw new ArgumentException("Enter whole numbers from 1 to 50 for both CPS limits.");
            range = new(min, max); VariableValidationText.Text = ""; return true;
        }
        catch (ArgumentException e) { VariableValidationText.Text = e.Message; return false; }
    }
    private void TimingModeChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        engine.Stop(); settings = settings with { TimingMode = VariableTimingInput.IsChecked == true ? ClickTimingMode.Variable : ClickTimingMode.Fixed };
        engine.ConfigureTiming(settings.TimingMode, new(settings.MinimumCps, settings.MaximumCps));
        SynchronizeRange(); RenderTimingMode(); Persist(); UpdateStatus();
        MessageText.Text = settings.TimingMode == ClickTimingMode.Variable ? "Variable CPS requires an explicitly selected window, including a verified Roblox client. Refresh the window list to select one." : "Fixed CPS selected. Starts disabled.";
    }
    private void VariableTyped(object sender, TextChangedEventArgs e)
    {
        if (!ready || synchronizingVariable) return;
        engine.Stop();
        if (TryRange(out var range))
        {
            settings = settings with { MinimumCps = range.Minimum, MaximumCps = range.Maximum };
            engine.ConfigureTiming(settings.TimingMode, range); SynchronizeRange(); Persist();
        }
        else VariableRangeText.Text = "Invalid range · clicking stopped; correct both limits before starting.";
        lastDisplay = null; UpdateStatus();
    }
    private void VariableSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || synchronizingVariable) return;
        synchronizingVariable = true;
        int min = (int)MinimumCpsSlider.Value, max = (int)MaximumCpsSlider.Value;
        if (min > max) { if (ReferenceEquals(sender, MinimumCpsSlider)) max = min; else min = max; }
        MinimumCpsInput.Text = min.ToString(); MaximumCpsInput.Text = max.ToString();
        synchronizingVariable = false; VariableTyped(sender, null!);
    }
    private async void RefreshTargetsClicked(object sender, RoutedEventArgs e)
    {
        engine.Stop();
        var button = (Button)sender; button.IsEnabled = false;
        try
        {
            var windows = await Task.Run(DesktopTargetService.Enumerate);
            if (closing) return;
            DesktopTargetInput.ItemsSource = windows;
            VariableValidationText.Text = windows.Length == 0 ? "No eligible windows found. Open a desktop application and refresh." : "Select a window. Verified Roblox clients are supported; unknown identities remain excluded.";
        }
        catch (Exception ex) { VariableValidationText.Text = "Could not list windows: " + ex.Message; }
        finally { button.IsEnabled = true; }
    }
    private void DesktopTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        engine.Stop();
        try { desktopTarget.Select(DesktopTargetInput.SelectedItem as DesktopWindow); VariableValidationText.Text = ""; }
        catch (Exception ex) { VariableValidationText.Text = ex.Message; }
        lastDisplay = null; UpdateStatus();
    }
    private void StartValidated()
    {
        if (settings.TimingMode == ClickTimingMode.Variable && !TryRange(out _)) { engine.Stop(); MessageText.Text = VariableValidationText.Text; return; }
        try { engine.Start(); }
        catch (InvalidOperationException ex) { MessageText.Text = ex.Message; VariableValidationText.Text = ex.Message; }
    }
    private void RenderVariableStatus()
    {
        if (settings.TimingMode != ClickTimingMode.Variable) { ConfiguredCpsText.FontSize = 23; return; }
        bool allowed = desktopTarget.CanClick;
        RobloxStatusText.Text = allowed ? "Desktop target in focus" : "Desktop target required";
        RobloxStatusText.Foreground = (System.Windows.Media.Brush)FindResource(allowed ? "Success" : "Text");
        string title = desktopTarget.SelectedTitle;
        TargetText.Text = title.Length > 70 ? title[..67] + "…" : title;
        TargetText.ToolTip = title;
        ConfiguredCpsText.FontSize = 18; ConfiguredCpsText.Text = $"{settings.MinimumCps}–{settings.MaximumCps}";
        StartButton.ToolTip = "Variable CPS requires an eligible selected desktop window. Verified Roblox clients are supported. Input pauses on focus loss or when the pointer leaves the target.";
    }
}
