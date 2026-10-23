using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickForge.Core.Updates;

namespace ClickForge;

public sealed class UpdateWindow : Window
{
    private readonly UpdateClient client;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 15, 0, 7) };
    private readonly TextBlock latest = new() { Margin = new(0, 12, 0, 6) };
    private readonly TextBox notes = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 145, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ProgressBar progress = new() { Height = 5, Margin = new(0, 8, 0, 5), Minimum = 0, Maximum = 100 };
    private readonly TextBlock bytes = new() { FontSize = 12 };
    private readonly Button check = new() { Content = "Check for updates", Margin = new(0, 12, 0, 0) };
    private readonly Button download = new() { Content = "Update now", Margin = new(0, 0, 7, 0) };
    private readonly Button install = new() { Content = "Install and restart", Margin = new(0, 10, 0, 0) };
    private readonly Button cancel = new() { Content = "Cancel download", Margin = new(0, 0, 7, 0) };
    private readonly Button later = new() { Content = "Later", Margin = new(7, 0, 0, 0) };
    internal string DisplayedStatus => status.Text;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    public UpdateWindow(UpdateClient client, bool automatic, Action<bool> saveAutomatic, Func<Task> installAction)
    {
        this.client = client; Title = "ClickForge · Updates"; Width = 560; Height = 650; MinWidth = 460; MinHeight = 560;
        SetResourceReference(BackgroundProperty, "Base"); SetResourceReference(IconProperty, "ForgeIcon");
        progress.SetResourceReference(Control.ForegroundProperty, "Accent"); progress.SetResourceReference(Control.BackgroundProperty, "Elevated");
        install.SetResourceReference(StyleProperty, "PrimaryButton");
        var panel = new StackPanel { Margin = new(24) };
        panel.Children.Add(new TextBlock { Text = "Updates", FontSize = 23, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Installed version: " + MainWindow.InstalledVersion.Value.Split('+')[0], Margin = new(0, 10, 0, 0), Foreground = (Brush)FindResource("Muted") });
        var auto = new CheckBox { Content = "Check automatically on startup", IsChecked = automatic, Margin = new(0, 17, 0, 0) };
        auto.Checked += (_, _) => saveAutomatic(true); auto.Unchecked += (_, _) => saveAutomatic(false); panel.Children.Add(auto);
        panel.Children.Add(check); panel.Children.Add(status); panel.Children.Add(latest); panel.Children.Add(notes); panel.Children.Add(progress); panel.Children.Add(bytes);
        var actions = new Grid { Margin = new(0, 16, 0, 0) }; actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new());
        download.SetResourceReference(StyleProperty, "PrimaryButton"); actions.Children.Add(download);
        Grid.SetColumn(later, 1); actions.Children.Add(later);
        panel.Children.Add(actions); panel.Children.Add(install); panel.Children.Add(cancel);
        panel.Children.Add(new TextBlock { Text = "Downloads come only from the official public GitHub release channel. SHA-256 detects mismatches; it is not a publisher signature. Updates never start clicking.", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), FontSize = 12, Margin = new(0, 16, 0, 0) });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        check.Click += async (_, _) => await Run(() => client.CheckAsync(MainWindow.InstalledVersion, operation!.Token));
        download.Click += async (_, _) => await Run(() => client.DownloadAsync(MainWindow.InstalledVersion, operation!.Token));
        install.Click += async (_, _) => { try { await installAction(); } catch (Exception e) { client.Report(UpdateState.Failed, e.Message); } };
        cancel.Click += (_, _) => operation?.Cancel(); later.Click += (_, _) => Close();
        client.StatusChanged += Changed;
        Closed += (_, _) => { client.StatusChanged -= Changed; lifetime.Cancel(); operation?.Cancel(); };
        Refresh(client.Status);
    }
    private async Task Run(Func<Task> action)
    {
        if (operation is { IsCancellationRequested: false }) return;
        operation?.Dispose(); operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try { await action(); } catch (OperationCanceledException) { } finally { operation.Dispose(); operation = null; }
    }
    private void Changed(UpdateStatus value) { if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) Refresh(value); })); }
    private void Refresh(UpdateStatus value)
    {
        status.Text = $"{value.State switch { UpdateState.Available => "Update available", UpdateState.UpToDate => "Up to date", UpdateState.ReadyToInstall => "Ready to install", UpdateState.Unavailable => "Offline or unavailable", _ => value.State.ToString() }}\n{value.Message}";
        status.SetResourceReference(TextBlock.ForegroundProperty, value.State == UpdateState.Failed ? "Failure" : value.State == UpdateState.Unavailable ? "Warning" : "Text");
        latest.Text = client.Latest is null ? "Latest version: not checked" : "Latest stable version: " + client.Latest.Version;
        notes.Text = client.Latest?.Notes ?? "Published release notes will appear here.";
        progress.IsIndeterminate = value.State is UpdateState.Checking or UpdateState.Verifying or UpdateState.Installing or UpdateState.Restarting;
        double percent = value.Total > 0 ? 100d * value.Downloaded / value.Total.Value : 0; progress.Value = percent;
        bytes.Text = value.Total is long total ? $"{percent:0.0}% · {value.Downloaded:N0} / {total:N0} bytes" : "";
        bool busy = value.State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Verifying or UpdateState.Installing or UpdateState.Restarting;
        check.IsEnabled = !busy; download.IsEnabled = !busy && client.Latest is not null && client.Latest.Version.CompareTo(MainWindow.InstalledVersion) > 0 && value.State != UpdateState.ReadyToInstall;
        later.IsEnabled = value.State is not (UpdateState.Installing or UpdateState.Restarting);
        install.IsEnabled = value.State == UpdateState.ReadyToInstall; install.Visibility = client.Staged is null ? Visibility.Collapsed : Visibility.Visible;
        cancel.Visibility = value.State is UpdateState.Downloading or UpdateState.Verifying or UpdateState.Checking ? Visibility.Visible : Visibility.Collapsed;
    }
}
