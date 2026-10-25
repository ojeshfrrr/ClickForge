using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClickForge;

internal static class DialogLayout
{
    internal static void Setup(Window window, string title, double width, double height)
    {
        window.Title = "ClickForge · " + title; window.Width = width; window.Height = Math.Min(height, Math.Max(500, SystemParameters.WorkArea.Height - 40));
        window.MinWidth = 460; window.MinHeight = 450; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.SetResourceReference(Window.BackgroundProperty, "Base"); window.SetResourceReference(Window.ForegroundProperty, "Text"); window.SetResourceReference(Window.IconProperty, "ForgeIcon");
        KeyboardNavigation.SetTabNavigation(window, KeyboardNavigationMode.Cycle);
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { window.Close(); e.Handled = true; } };
    }
    internal static TextBlock Text(string text, bool hint = false)
    {
        var result = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 8) };
        if (hint) result.SetResourceReference(FrameworkElement.StyleProperty, "Hint"); return result;
    }
    internal static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new(0, 0, 8, 8), Padding = new(12, 8, 12, 8) };
        System.Windows.Automation.AutomationProperties.SetName(button, text.Replace("_", ""));
        if (primary) button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        button.Click += (_, _) => action(); return button;
    }
}
