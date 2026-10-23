namespace ClickForge.Core.Updates;

public static class UpdateCache
{
    // Only completed, old workspaces are eligible. Failed/incomplete journals
    // and their recovery backups are never removed automatically.
    public static void CleanupCompleted(string root, string? exclude = null)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            UpdateChannel.RejectReparse(root);
            foreach (string directory in Directory.EnumerateDirectories(root).Take(40))
            {
                try
                {
                    string full = UpdateChannel.Workspace(root, directory);
                    if (full.Equals(exclude, StringComparison.OrdinalIgnoreCase) || Directory.GetLastWriteTimeUtc(full) > DateTime.UtcNow.AddDays(-7)) continue;
                    var receipt = UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(full, "result.json"));
                    if (receipt.Status != "Succeeded") continue;
                    var pending = new Queue<string>(); pending.Enqueue(full); int count = 0;
                    while (pending.TryDequeue(out string? current))
                        foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                        {
                            if (++count > 100) throw new InvalidDataException("Unexpected cache contents.");
                            UpdateChannel.RejectReparse(entry);
                            if (Directory.Exists(entry)) pending.Enqueue(entry);
                        }
                    Directory.Delete(UpdateChannel.Workspace(root, full), true);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }
}
