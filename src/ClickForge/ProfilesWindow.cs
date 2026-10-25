using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Microsoft.Win32;
using ClickForge.Core;

namespace ClickForge;

internal sealed class ProfilesWindow : Window
{
    private readonly ProfileStore store;
    private readonly Func<Settings> current;
    private readonly Action<TimingProfile> apply;
    private readonly Action<Guid> deleted;
    private readonly Action imported;
    internal readonly ListBox ProfileList = new() { MinHeight = 120, Margin = new(0, 5, 0, 12) };
    internal readonly TextBox NameInput = new() { MaxLength = 80 };
    internal readonly TextBox DescriptionInput = new() { MaxLength = 500, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 64, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock feedback = DialogLayout.Text("");
    private readonly List<Button> selectedActions = [];
    internal string Feedback => feedback.Text;
    private TimingProfile? Selected => ProfileList.SelectedItem as TimingProfile;
    internal ProfilesWindow(ProfileStore store, Func<Settings> current, Action<TimingProfile> apply, Action<Guid> deleted, Action imported)
    {
        this.store = store; this.current = current; this.apply = apply; this.deleted = deleted; this.imported = imported;
        DialogLayout.Setup(this, "Profiles", 670, 740);
        var root = new StackPanel { Margin = new(22) }; ProfileList.Height = 190;
        var heading = new StackPanel(); heading.Children.Add(new TextBlock { Text = "Configuration profiles", FontSize = 23, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(DialogLayout.Text("Save timing and activation settings. Applying a profile stops clicking and keeps your selected target.", true)); root.Children.Add(heading);
        var template = new DataTemplate(typeof(TimingProfile));
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name")); name.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); stack.AppendChild(name);
        var summary = new FrameworkElementFactory(typeof(TextBlock)); summary.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Summary")); summary.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0)); summary.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); stack.AppendChild(summary); template.VisualTree = stack; ProfileList.ItemTemplate = template;
        AutomationProperties.SetName(ProfileList, "Saved configuration profiles"); Grid.SetRow(ProfileList, 1); root.Children.Add(ProfileList);
        var panel = new StackPanel(); Grid.SetRow(panel, 2); root.Children.Add(panel);
        var fields = new Grid(); fields.ColumnDefinitions.Add(new()); fields.ColumnDefinitions.Add(new());
        var left = new StackPanel { Margin = new(0, 0, 8, 0) }; left.Children.Add(new Label { Content = "Profile _name", Target = NameInput }); left.Children.Add(NameInput); fields.Children.Add(left);
        var right = new StackPanel(); right.Children.Add(new Label { Content = "_Description (optional)", Target = DescriptionInput }); right.Children.Add(DescriptionInput); Grid.SetColumn(right, 1); fields.Children.Add(right); panel.Children.Add(fields);
        AutomationProperties.SetName(NameInput, "Profile name"); AutomationProperties.SetName(DescriptionInput, "Profile description");
        var actions = new WrapPanel { Margin = new(0, 12, 0, 0) };
        actions.Children.Add(DialogLayout.Button("Create from current", () => Run(() => Select(store.Create(current(), NameInput.Text)), "Profile created. Select Apply to use it.")));
        Add("Apply profile", () => { apply(Selected!); Refresh(Selected!.Id); feedback.Text = "Profile applied; clicking stopped."; }, true);
        Add("Save name / description", () => Select(store.Rename(Selected!, NameInput.Text, DescriptionInput.Text)));
        Add("Save current timing", () => { var p = TimingProfile.Capture(current(), NameInput.Text) with { Id = Selected!.Id, Created = Selected.Created, Modified = DateTimeOffset.UtcNow, Description = DescriptionInput.Text }; store.Save(p); Select(p); });
        Add("Duplicate", () => Select(store.Duplicate(Selected!)));
        Add("Delete…", DeleteSelected);
        actions.Children.Add(DialogLayout.Button("Import JSON…", Import)); Add("Export JSON…", Export);
        actions.Children.Add(DialogLayout.Button("Restore built-in defaults", () => Run(() => { store.RestoreBuiltIns(); Refresh(); }, "Built-in profiles restored. User profiles retained; clicking is stopped.")));
        actions.Children.Add(DialogLayout.Button("Close", Close)); panel.Children.Add(actions);
        feedback.TextWrapping = TextWrapping.Wrap; feedback.SetResourceReference(TextBlock.ForegroundProperty, "Warning"); AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite); panel.Children.Add(feedback);
        // One intentional scroll surface handles short displays and long messages.
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        ProfileList.SelectionChanged += (_, _) =>
        {
            var item = Selected; NameInput.Text = item?.Name ?? ""; DescriptionInput.Text = item?.Description ?? "";
            foreach (var button in selectedActions) button.IsEnabled = item is not null;
            if (item is not null) feedback.Text = current().ActiveProfileId == item.Id ? (item.Matches(current()) ? "Active profile." : "Active profile · timing modified in the main window.") : "Selected. Apply to load this configuration and stop clicking.";
        };
        Refresh(current().ActiveProfileId); Loaded += (_, _) => ProfileList.Focus();
        void Add(string text, Action action, bool primary = false) { var button = DialogLayout.Button(text, () => Run(action), primary); selectedActions.Add(button); actions.Children.Add(button); }
    }
    internal void Refresh(Guid? id = null)
    {
        var items = store.Load(); ProfileList.ItemsSource = items; ProfileList.SelectedItem = items.FirstOrDefault(p => p.Id == id);
        foreach (var button in selectedActions) button.IsEnabled = Selected is not null;
        if (store.Warnings.Count > 0) feedback.Text = string.Join(" ", store.Warnings.Distinct());
        else if (items.Length == 0) feedback.Text = "No saved profiles. Enter a name and create one from the current configuration, or import a JSON profile.";
    }
    private void Select(TimingProfile p) => Refresh(p.Id);
    private void Run(Action action, string? success = null)
    {
        try { action(); if (success is not null) feedback.Text = success; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
        { feedback.Text = "Profile operation failed: " + (e is ArgumentException ? e.Message : "Check that the file is a supported profile and its folder is writable."); }
    }
    private void DeleteSelected()
    {
        if (Selected is not { } item) return;
        if (MessageBox.Show(this, "Delete this saved profile? Your current timing settings will remain available.", "Delete profile", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        DeleteConfirmed();
    }
    internal void DeleteConfirmed()
    {
        if (Selected is not { } item) return;
        store.Delete(item.Id); deleted(item.Id); Refresh(); feedback.Text = "Profile deleted. Clicking remains stopped.";
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "ClickForge profile (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) ImportPath(dialog.FileName);
    }
    internal void ImportPath(string path) => Run(() => { Select(store.Import(path)); imported(); }, "Profile imported. Review it and select Apply to use it.");
    private void Export()
    {
        if (Selected is null) return;
        var dialog = new SaveFileDialog { Filter = "ClickForge profile (*.json)|*.json", FileName = "ClickForge-profile.json", DefaultExt = ".json" };
        if (dialog.ShowDialog(this) == true) ExportPath(dialog.FileName);
    }
    internal void ExportPath(string path) => Run(() => ProfileStore.Export(path, Selected!), "Profile exported. Only portable timing data was included.");
}
