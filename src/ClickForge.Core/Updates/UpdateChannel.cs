namespace ClickForge.Core.Updates;

// Confirmed from origin: https://github.com/ojeshfrrr/ClickForge.git.
public static class UpdateChannel
{
    public const string Repository = "ojeshfrrr/ClickForge";
    public static readonly Uri Latest = new($"https://api.github.com/repos/{Repository}/releases/latest");
    public static Uri Asset(long id) => id > 0 ? new($"https://api.github.com/repos/{Repository}/releases/assets/{id}") : throw new InvalidDataException("Invalid asset identity.");
    public static string UpdatesRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge", "Updates");
    public static bool IsReleaseAsset(Uri uri, string tag, string name) => uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath == $"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}";
    public static bool IsAllowedRedirect(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        new[] { "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    public static string Workspace(string root, string path)
    {
        string full = Path.GetFullPath(path), parent = Path.GetFullPath(root);
        if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)) throw new InvalidDataException("Invalid update workspace.");
        RejectReparse(parent); RejectReparse(full); return full;
    }
    public static void RejectReparse(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Update paths must not be symbolic links or junctions.");
    }
}
