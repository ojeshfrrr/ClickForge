using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using ClickForge.Core;

namespace ClickForge;

internal sealed record PaletteCommand(string Label, Action Execute)
{ public override string ToString() => Label; }

internal sealed class CommandPaletteWindow : Window
{
    internal TextBox Search { get; } = new();
    internal ListBox Results { get; } = new() { MinHeight = 120 };
    private readonly Func<string, IEnumerable<PaletteCommand>> commands;
    private readonly TextBlock feedback = DialogLayout.Text("Type to search. Enter executes the selected action; Escape closes.", true);
    internal CommandPaletteWindow(Func<string, IEnumerable<PaletteCommand>> commands)
    {
        this.commands = commands; DialogLayout.Setup(this, "Commands", 650, 520);
        var panel = new DockPanel { Margin = new(20) };
        var top = new StackPanel(); top.Children.Add(DialogLayout.Text("Command palette · Ctrl+K")); top.Children.Add(Search); top.Children.Add(feedback);
        DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var run = DialogLayout.Button("Execute selected action", ExecuteSelected); DockPanel.SetDock(run, Dock.Bottom); panel.Children.Add(run); panel.Children.Add(Results);
        AutomationProperties.SetName(Search, "Search ClickForge commands, or type cps and a number from 1 to 50"); AutomationProperties.SetName(Results, "Available commands");
        Content = panel;
        Search.TextChanged += (_, _) => RefreshResults();
        Search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down) { if (Results.Items.Count > 0) { Results.SelectedIndex = Math.Min(Results.Items.Count - 1, Results.SelectedIndex + 1); Results.ScrollIntoView(Results.SelectedItem); } e.Handled = true; }
            if (e.Key == Key.Up) { Results.SelectedIndex = Math.Max(0, Results.SelectedIndex - 1); e.Handled = true; }
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ExecuteSelected(); } };
        Loaded += (_, _) => Search.Focus(); RefreshResults();
    }
    private void RefreshResults()
    {
        Results.ItemsSource = commands(Search.Text).ToArray(); Results.SelectedIndex = Results.Items.Count > 0 ? 0 : -1;
        feedback.Text = Search.Text.TrimStart().StartsWith("cps", StringComparison.OrdinalIgnoreCase) && !PaletteInput.TryCps(Search.Text, out _) ?
            "Use cps followed by a whole number from 1 to 50. No speed has changed." : "Enter executes the selected action; highlighting does nothing. Escape closes.";
    }
    internal void ExecuteSelected()
    {
        if (Results.SelectedItem is not PaletteCommand command) return;
        // Explicit Enter/button only. Closing clears the owner's configuration
        // blocker before the ordinary command runs. Highlighting does nothing.
        Close(); command.Execute();
    }
}
