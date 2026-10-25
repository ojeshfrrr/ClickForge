using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Win32;
using ClickForge.Core;

namespace ClickForge;

internal sealed class PreferencesWindow : Window
{
    private readonly Func<Settings> get;
    private readonly Func<Settings, bool> save;
    private readonly ActivityHistory history;
    private readonly Func<string> report;
    private readonly TextBlock diagnostics = DialogLayout.Text("");
    private readonly TextBlock feedback = DialogLayout.Text("");
    private readonly ListBox activity = new() { Height = 150 };
    private int count = -1;
    internal readonly CheckBox MinimizeInput = new() { Content = "Minimize to the system tray", Margin = new(0, 8, 0, 8) };
    internal readonly CheckBox CloseInput = new() { Content = "Close to the system tray (keep running)", Margin = new(0, 0, 0, 8) };
    internal PreferencesWindow(Func<Settings> get, Func<Settings, bool> save, ActivityHistory history, Func<string> report, Func<bool> trayAvailable, Action restoreDefaults)
    {
        this.get = get; this.save = save; this.history = history; this.report = report;
        DialogLayout.Setup(this, "Settings and diagnostics", 650, 780);
        var panel = new StackPanel { Margin = new(22) }; panel.Children.Add(new TextBlock { Text = "Settings and diagnostics", FontSize = 23, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(DialogLayout.Text("Window behavior", true));
        MinimizeInput.IsChecked = get().MinimizeToTray; CloseInput.IsChecked = get().CloseToTray;
        foreach (var input in new[] { MinimizeInput, CloseInput }) { panel.Children.Add(input); input.Click += PreferenceChanged; }
        panel.Children.Add(DialogLayout.Text("Both tray options are off by default. Hiding stops clicking. Use Exit ClickForge in the tray menu to quit. These options work only when the tray icon is available.", true));
        panel.Children.Add(DialogLayout.Text(trayAvailable() ? "System tray is available." : "System tray is currently unavailable; the window stays accessible.", true));
        panel.Children.Add(DialogLayout.Button("Restore application defaults", restoreDefaults));
        panel.Children.Add(DialogLayout.Text("Restores timing, shortcuts and preferences. Saved profiles are retained. Clicking remains stopped.", true));
        panel.Children.Add(new TextBlock { Text = "Diagnostics", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new(0, 12, 0, 4) }); panel.Children.Add(diagnostics);
        var actions = new WrapPanel(); actions.Children.Add(DialogLayout.Button("Copy redacted report", () => { try { Clipboard.SetText(report()); feedback.Text = "Redacted diagnostic report copied."; } catch (System.Runtime.InteropServices.ExternalException) { feedback.Text = "Clipboard is busy. Try again."; } }));
        actions.Children.Add(DialogLayout.Button("Export activity…", Export)); actions.Children.Add(DialogLayout.Button("Clear activity", () => { history.Clear(); count = -1; Refresh(); feedback.Text = "Activity cleared."; })); panel.Children.Add(actions);
        panel.Children.Add(activity); AutomationProperties.SetName(activity, "Recent application activity");
        feedback.SetResourceReference(TextBlock.ForegroundProperty, "Warning"); AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite); panel.Children.Add(feedback);
        panel.Children.Add(DialogLayout.Button("Close", Close));
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Refresh(); Loaded += (_, _) => MinimizeInput.Focus();
    }
    private void PreferenceChanged(object sender, RoutedEventArgs e)
    {
        var value = get() with { MinimizeToTray = MinimizeInput.IsChecked == true, CloseToTray = CloseInput.IsChecked == true };
        feedback.Text = save(value) ? "Preferences saved." : "Preferences apply for this session but could not be saved.";
    }
    internal void Refresh()
    {
        MinimizeInput.IsChecked = get().MinimizeToTray; CloseInput.IsChecked = get().CloseToTray;
        string text = report().Split("\n\nRecent activity", StringSplitOptions.None)[0]; if (diagnostics.Text != text) diagnostics.Text = text;
        var entries = history.Entries; if (entries.Length == count && (entries.Length == 0 || Equals(activity.Items.Cast<ActivityEntry>().LastOrDefault(), entries[^1]))) return;
        count = entries.Length; activity.ItemsSource = entries;
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { FileName = "ClickForge-activity.txt", Filter = "Text report (*.txt)|*.txt", DefaultExt = ".txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, history.RedactedReport()); feedback.Text = "Redacted activity exported."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { feedback.Text = "Activity could not be exported. Choose a writable folder."; }
    }
}
