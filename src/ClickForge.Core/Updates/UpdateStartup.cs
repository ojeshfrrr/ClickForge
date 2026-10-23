namespace ClickForge.Core.Updates;

public static class UpdateStartup
{
    public static InstallReceipt Acknowledge(string resultPath, string nonce, SemanticVersion installed, string executable, string? updatesRoot = null)
    {
        string workspace = UpdateChannel.Workspace(updatesRoot ?? UpdateChannel.UpdatesRoot, Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        if (Path.GetFileName(resultPath) != "result.json" || nonce.Length != 64 || !nonce.All(char.IsAsciiHexDigit)) throw new InvalidDataException("Invalid update startup arguments.");
        UpdateChannel.RejectReparse(resultPath);
        var receipt = UpdateFiles.ReadJson<InstallReceipt>(resultPath);
        if (receipt.Nonce != nonce || !string.Equals(receipt.InstalledExecutable, executable, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update startup identity mismatch.");
        if (receipt.Status is "Restarting" or "Succeeded")
        {
            if (SemanticVersion.Parse(receipt.Version).CompareTo(installed) != 0) throw new InvalidDataException("The restarted version does not match the update.");
            if (receipt.Status == "Restarting") UpdateFiles.WriteJson(Path.Combine(workspace, "ack.json"), new StartupAcknowledgement(nonce, installed.Value, executable));
        }
        return receipt;
    }
}
