using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using ClickForge.Core.Updates;

namespace ClickForge;

public partial class MainWindow
{
    private readonly UpdateClient updateClient = new();
    private readonly CancellationTokenSource updateLifetime = new();
    private UpdateWindow? updateWindow;
    internal static SemanticVersion InstalledVersion => SemanticVersion.Installed(typeof(MainWindow).Assembly);
    private void InitializeUpdates()
    {
        VersionLabel.Text = "v" + InstalledVersion.Value.Split('+')[0];
        updateClient.StatusChanged += UpdateChanged;
        ContentRendered += async (_, _) =>
        {
            if (previewPath is not null || selfTestPath is not null) return;
            await ReadInstallationOutcomeAsync();
            _ = Task.Run(() => UpdateCache.CleanupCompleted(UpdateChannel.UpdatesRoot));
            if (settings.CheckForUpdatesOnStartup && !closing)
            {
                try { await updateClient.CheckAsync(InstalledVersion, updateLifetime.Token); }
                catch (OperationCanceledException) { }
            }
        };
    }
    private void UpdateChanged(UpdateStatus status)
    {
        if (Dispatcher.HasShutdownStarted) return;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (closing) return;
            if (status.State == UpdateState.Available) MessageText.Text = status.Message + " Open Updates to review it.";
        }));
    }
    private void UpdatesClicked(object sender, RoutedEventArgs e)
    {
        if (updateWindow is not null) { updateWindow.Activate(); return; }
        updateWindow = new UpdateWindow(updateClient, settings.CheckForUpdatesOnStartup,
            enabled => { settings = settings with { CheckForUpdatesOnStartup = enabled }; Persist(); }, InstallUpdateAsync) { Owner = this };
        updateWindow.Closed += (_, _) => updateWindow = null; updateWindow.Show();
    }
    private async Task InstallUpdateAsync()
    {
        var staged = updateClient.Staged ?? throw new InvalidOperationException("Download and verify the package first.");
        // Stop input before presenting a confirmation dialog, including unrestricted input.
        engine.Stop(); activeTest?.StopTest();
        if (MessageBox.Show(updateWindow ?? (Window)this, "Install the verified update and restart ClickForge? Clicking will remain disabled after restart.", "Install ClickForge update", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        string install = Path.GetDirectoryName(Environment.ProcessPath)!;
        UpdateFiles.CheckWritable(install);
        string helper = Path.Combine(install, "ClickForge.Updater.exe");
        if (!File.Exists(helper)) throw new IOException("The separate update helper is missing. Extract the complete current release ZIP into a folder you own before updating.");
        if (new UpdatePackage().VersionOf(helper).CompareTo(InstalledVersion) != 0) throw new IOException("The installed helper version does not match ClickForge. Extract the complete release package and retry.");
        string workspace = UpdateChannel.Workspace(UpdateChannel.UpdatesRoot, staged.Workspace);
        string runner = Path.Combine(workspace, "runner.exe"); File.Copy(helper, runner, true);
        using var current = Process.GetCurrentProcess();
        var request = new InstallRequest(workspace, install, current.Id, current.StartTime.ToUniversalTime().Ticks,
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), staged.Release.Version.Value, staged.ArchiveHash);
        string requestPath = Path.Combine(workspace, "request.json"); UpdateFiles.WriteJson(requestPath, request);
        var info = new ProcessStartInfo(runner) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workspace };
        info.ArgumentList.Add("--request"); info.ArgumentList.Add(requestPath);
        updateClient.Report(UpdateState.Installing, "Preparing the independent updater…");
        using var process = Process.Start(info) ?? throw new IOException("Could not start the update helper.");
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            string ready = Path.Combine(workspace, "ready.json");
            if (File.Exists(ready))
            {
                using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ready));
                if (json.RootElement.GetProperty("Nonce").GetString() == request.Nonce && json.RootElement.GetProperty("Ready").GetBoolean())
                {
                    updateClient.Report(UpdateState.Restarting, "Updater ready. Closing ClickForge cleanly; installation will continue after exit.");
                    Application.Current.Shutdown(); return;
                }
            }
            if (process.HasExited) throw new IOException(File.Exists(Path.Combine(workspace, "result.json")) ? UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(workspace, "result.json")).Message : "The update helper could not initialize. No installed files changed.");
            await Task.Delay(100, updateLifetime.Token);
        }
        throw new IOException("The helper did not become ready in time. Keep this window open; no installed files can be changed until ClickForge exits.");
    }
    private async Task ReadInstallationOutcomeAsync()
    {
        string? resultPath = GetArgument("--update-result"), nonce = GetArgument("--update-nonce");
        if (resultPath is null || nonce is null) return;
        try
        {
            string workspace = UpdateChannel.Workspace(UpdateChannel.UpdatesRoot, Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
            if (Path.GetFileName(resultPath) != "result.json") throw new InvalidDataException("Invalid update result path.");
            var receipt = UpdateStartup.Acknowledge(resultPath, nonce, InstalledVersion, Environment.ProcessPath!);
            if (receipt.Status == "Restarting")
            {
                // Window is fully rendered, engine was constructed disabled, and
                // no active state is loaded from preferences.
                updateClient.Report(UpdateState.Restarting, "New version started. Waiting for the helper to finish…");
                for (int i = 0; i < 160 && receipt.Status == "Restarting"; i++)
                {
                    await Task.Delay(250, updateLifetime.Token); receipt = UpdateFiles.ReadJson<InstallReceipt>(resultPath);
                }
            }
            string message = receipt.Status == "Succeeded" ? receipt.Message : receipt.Status == "Restarting" ? "Startup confirmed; helper outcome is still pending. Check Updates or the recovery receipt." : receipt.Message;
            if (receipt.Status is "RecoveryRequired" or "Failed") message += "\nRecovery folder: " + workspace;
            updateClient.Report(receipt.Status == "Succeeded" ? UpdateState.UpToDate : UpdateState.Failed, message); MessageText.Text = message;
        }
        catch (Exception e) { if (!closing) MessageText.Text = "Update outcome could not be confirmed: " + e.Message; }
    }
}
