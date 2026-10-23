using System.Diagnostics;
using System.Text.Json;

namespace ClickForge.Core.Updates;

public sealed record InstallRequest(string Workspace, string InstallDirectory, int ParentPid, long ParentStartUtcTicks, string Nonce, string ExpectedVersion, string ArchiveHash);
public sealed record InstallReceipt(string Nonce, string Version, string InstalledExecutable, string Status, string Message);
public sealed record StartupAcknowledgement(string Nonce, string Version, string Executable);
public sealed record InstallJournal(string InstallDirectory, string[] OriginallyPresent, string Phase, Dictionary<string, string> BackupHashes);
public interface IInstallPlatform
{
    Task WaitForParentExitAsync(InstallRequest request, Action ready, CancellationToken token);
    Task<bool> StartAndConfirmAsync(string executable, string resultPath, string nonce, CancellationToken token);
    void StartPrevious(string executable, string resultPath, string nonce);
}
public static class UpdateFiles
{
    public static void WriteJson<T>(string path, T value)
    {
        string temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { JsonSerializer.Serialize(file, value); file.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
    }
    public static T ReadJson<T>(string path)
    {
        if (new FileInfo(path).Length > 32768) throw new InvalidDataException("Update control file is too large.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid update control file.");
    }
    public static void CheckWritable(string directory)
    {
        UpdateChannel.RejectReparse(directory);
        string probe = Path.Combine(directory, ".clickforge-write-" + Guid.NewGuid().ToString("N"));
        try { using (File.Create(probe)) { } File.Delete(probe); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new IOException("The application folder is not writable. Move the extracted application to a folder you own, restart it there, and try again. No administrator access is requested.", e); }
    }
}

public sealed class UpdateInstaller(IInstallPlatform platform, string? updatesRoot = null, UpdatePackage? verifier = null, Action<string>? afterReplace = null)
{
    private readonly string root = updatesRoot ?? UpdateChannel.UpdatesRoot;
    private readonly UpdatePackage package = verifier ?? new();
    private static string[] Managed => [.. UpdatePackage.Payload, "update-manifest.json"];
    public async Task<bool> InstallAsync(InstallRequest request, bool recover = false, CancellationToken token = default)
    {
        string workspace = UpdateChannel.Workspace(root, request.Workspace);
        string install = Path.GetFullPath(request.InstallDirectory);
        if (install.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Do not install an application inside its update cache.");
        if (!Path.IsPathFullyQualified(request.InstallDirectory) || install == Path.GetPathRoot(install) || request.ParentPid <= 0 || request.ParentStartUtcTicks <= 0 || request.Nonce.Length != 64 || !request.Nonce.All(char.IsAsciiHexDigit)) throw new InvalidDataException("Invalid installer arguments.");
        var version = SemanticVersion.Parse(request.ExpectedVersion); if (version.Prerelease is not null) throw new InvalidDataException("Only stable updates can be installed.");
        UpdateFiles.CheckWritable(install); UpdateChannel.RejectReparse(Path.Combine(install, "licenses"));
        string result = Path.Combine(workspace, "result.json"), journalPath = Path.Combine(workspace, "journal.json"), backup = Path.Combine(workspace, "backup");
        using var installationLock = new FileStream(Path.Combine(install, ".clickforge-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        void Receipt(string state, string message) => UpdateFiles.WriteJson(result, new InstallReceipt(request.Nonce, request.ExpectedVersion, Path.Combine(install, "ClickForge.exe"), state, message));
        InstallJournal? journal = File.Exists(journalPath) ? UpdateFiles.ReadJson<InstallJournal>(journalPath) : null;
        if (journal is not null && (journal.OriginallyPresent is null || journal.BackupHashes is null || !string.Equals(Path.GetFullPath(journal.InstallDirectory), install, StringComparison.OrdinalIgnoreCase) || journal.OriginallyPresent.Any(f => !Managed.Contains(f)) || !journal.BackupHashes.Keys.Order().SequenceEqual(journal.OriginallyPresent.Order()))) throw new InvalidDataException("Invalid recovery journal. Backup retained.");
        if (journal?.Phase == "Succeeded") return true;
        bool replacing = journal?.Phase is "Replacing" or "Installed" or "Launching";
        try
        {
            if (recover || replacing)
            {
                if (journal is null || journal.Phase == "RolledBack") throw new InvalidOperationException("No interrupted installation needs recovery.");
                Restore(install, backup, journal);
                UpdateFiles.WriteJson(journalPath, journal with { Phase = "RolledBack" });
                Receipt("Failed", "Interrupted update recovered. Previous files restored; clicking remains disabled.");
                platform.StartPrevious(Path.Combine(install, "ClickForge.exe"), result, request.Nonce); return false;
            }
            string staged = Path.Combine(workspace, "staging");
            UpdatePackage.VerifyHash(Path.Combine(workspace, "package.zip"), request.ArchiveHash);
            package.VerifyStagingFromArchive(Path.Combine(workspace, "package.zip"), staged, version);
            var current = package.VersionOf(Path.Combine(install, "ClickForge.exe"));
            if (version.CompareTo(current) <= 0) throw new InvalidDataException("The staged version is not newer than the installed version.");
            await platform.WaitForParentExitAsync(request, () => UpdateFiles.WriteJson(Path.Combine(workspace, "ready.json"), new { request.Nonce, Ready = true }), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // No installed file is touched before the entire backup is durable.
            Directory.CreateDirectory(backup);
            var originals = Managed.Where(f => File.Exists(Path.Combine(install, f))).ToArray();
            foreach (string name in originals)
            {
                string source = Path.Combine(install, name); UpdateChannel.RejectReparse(source);
                string destination = Path.Combine(backup, name); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true);
                using var durable = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None); durable.Flush(true);
            }
            journal = new(install, originals, "Replacing", originals.ToDictionary(name => name, name => UpdatePackage.Hash(Path.Combine(backup, name)))); UpdateFiles.WriteJson(journalPath, journal); replacing = true;
            Receipt("Installing", "Replacing application files; a recovery backup is available.");
            foreach (string name in Managed)
            {
                string destination = Path.Combine(install, name); UpdateChannel.RejectReparse(destination); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(staged, name), destination, true); afterReplace?.Invoke(name);
            }
            package.VerifyStaging(install, version);
            journal = journal with { Phase = "Launching" }; UpdateFiles.WriteJson(journalPath, journal);
            Receipt("Restarting", "New files are installed. Waiting for verified application startup…");
            if (!await platform.StartAndConfirmAsync(Path.Combine(install, "ClickForge.exe"), result, request.Nonce, token).ConfigureAwait(false)) throw new IOException("The updated application did not confirm startup.");
            UpdateFiles.WriteJson(journalPath, journal with { Phase = "Succeeded" });
            Receipt("Succeeded", $"Updated to {request.ExpectedVersion}. Clicking starts disabled.");
            // Known, validated children of this workspace only. Keep the small
            // receipt/journal and running helper; backups remain on any failure.
            try { Directory.Delete(Path.Combine(workspace, "staging"), true); Directory.Delete(backup, true); File.Delete(Path.Combine(workspace, "package.zip")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return true;
        }
        catch (Exception e)
        {
            if (replacing && journal is not null)
            {
                try
                {
                    Restore(install, backup, journal); UpdateFiles.WriteJson(journalPath, journal with { Phase = "RolledBack" });
                    Receipt("Failed", "Update failed; previous files restored. " + e.Message);
                    platform.StartPrevious(Path.Combine(install, "ClickForge.exe"), result, request.Nonce);
                }
                catch (Exception restoreError) { Receipt("RecoveryRequired", "Update interrupted. Close ClickForge and run recovery using this workspace's request.json. Backup retained. " + restoreError.Message); }
            }
            else Receipt("Failed", "No installed files were changed. " + e.Message);
            return false;
        }
    }
    private static void Restore(string install, string backup, InstallJournal journal)
    {
        UpdateChannel.RejectReparse(backup); UpdateChannel.RejectReparse(Path.Combine(backup, "licenses"));
        foreach (string name in journal.OriginallyPresent) UpdatePackage.VerifyHash(Path.Combine(backup, name), journal.BackupHashes[name]);
        foreach (string name in Managed)
        {
            string destination = Path.Combine(install, name); UpdateChannel.RejectReparse(destination);
            if (journal.OriginallyPresent.Contains(name))
            {
                string source = Path.Combine(backup, name); UpdateChannel.RejectReparse(source);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true);
            }
            else if (File.Exists(destination)) File.Delete(destination);
        }
    }
}
