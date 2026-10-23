using System.Diagnostics;
using ClickForge.Core.Updates;

if (args.Length != 2 || args[0] is not ("--request" or "--recover")) return 2;
InstallRequest? request = null;
try
{
    string requestPath = Path.GetFullPath(args[1]);
    string workspace = UpdateChannel.Workspace(UpdateChannel.UpdatesRoot, Path.GetDirectoryName(requestPath)!);
    if (Path.GetFileName(requestPath) != "request.json") throw new InvalidDataException("Invalid request filename.");
    request = UpdateFiles.ReadJson<InstallRequest>(requestPath);
    if (!string.Equals(Path.GetFullPath(request.Workspace), workspace, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Request workspace mismatch.");
    return await new UpdateInstaller(new WindowsInstallPlatform()).InstallAsync(request, args[0] == "--recover") ? 0 : 1;
}
catch (Exception e)
{
    if (request is not null)
    {
        try { UpdateFiles.WriteJson(Path.Combine(UpdateChannel.Workspace(UpdateChannel.UpdatesRoot, request.Workspace), "result.json"), new InstallReceipt(request.Nonce, request.ExpectedVersion, Path.Combine(request.InstallDirectory, "ClickForge.exe"), "Failed", e.Message)); } catch (Exception) { }
    }
    return 1;
}

sealed class WindowsInstallPlatform : IInstallPlatform
{
    public async Task WaitForParentExitAsync(InstallRequest request, Action ready, CancellationToken token)
    {
        using var parent = Process.GetProcessById(request.ParentPid); _ = parent.SafeHandle;
        if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartUtcTicks || !string.Equals(parent.MainModule?.FileName, Path.Combine(request.InstallDirectory, "ClickForge.exe"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The process identity does not match the application requesting this update.");
        ready();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await parent.WaitForExitAsync(timeout.Token);
    }
    private static Process Start(string executable, string result, string nonce)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        info.ArgumentList.Add("--update-result"); info.ArgumentList.Add(result); info.ArgumentList.Add("--update-nonce"); info.ArgumentList.Add(nonce);
        return Process.Start(info) ?? throw new IOException("Could not launch ClickForge.");
    }
    public async Task<bool> StartAndConfirmAsync(string executable, string resultPath, string nonce, CancellationToken token)
    {
        using var child = Start(executable, resultPath, nonce);
        string acknowledgement = Path.Combine(Path.GetDirectoryName(resultPath)!, "ack.json");
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (File.Exists(acknowledgement))
            {
                try
                {
                    var ack = UpdateFiles.ReadJson<StartupAcknowledgement>(acknowledgement);
                    var expected = UpdateFiles.ReadJson<InstallReceipt>(resultPath);
                    if (ack.Nonce == nonce && ack.Executable.Equals(executable, StringComparison.OrdinalIgnoreCase) && SemanticVersion.Parse(ack.Version).CompareTo(SemanticVersion.Parse(expected.Version)) == 0) return true;
                }
                catch (IOException) { }
            }
            if (child.HasExited) return false;
            await Task.Delay(200, token);
        }
        // Ask only this child to close. Never force-kill or touch other processes.
        if (!child.HasExited)
        {
            child.CloseMainWindow();
            using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await child.WaitForExitAsync(closeTimeout.Token); }
            catch (OperationCanceledException) { throw new IOException("The new application is still running. Close it before using recovery; the backup has been retained."); }
        }
        return false;
    }
    public void StartPrevious(string executable, string resultPath, string nonce) { using var child = Start(executable, resultPath, nonce); }
}
