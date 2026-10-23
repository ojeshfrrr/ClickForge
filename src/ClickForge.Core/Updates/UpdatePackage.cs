using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClickForge.Core.Updates;

public sealed record PackageManifest(int FormatVersion, string Version, string SourceCommit, Dictionary<string, string> Files);
public sealed class UpdatePackage(Func<string, string>? versionReader = null)
{
    public const long MaximumArchiveBytes = 400L * 1024 * 1024;
    public static readonly string[] Payload = ["ClickForge.exe", "ClickForge.Updater.exe", "README.txt", "licenses/DOTNET-LICENSE.txt", "licenses/DOTNET-THIRD-PARTY-NOTICES.txt", "licenses/WINDOWSDESKTOP-LICENSE.txt"];
    private readonly Func<string, string> readVersion = versionReader ?? ReadExecutableVersion;
    private static string ReadExecutableVersion(string path)
    {
        using var file = File.OpenRead(path); using var reader = new BinaryReader(file);
        if (reader.ReadUInt16() != 0x5A4D || file.Length < 256) throw new InvalidDataException("The executable is not a Windows application.");
        file.Position = 0x3C; int offset = reader.ReadInt32();
        if (offset < 64 || offset > file.Length - 26) throw new InvalidDataException("Invalid executable header.");
        file.Position = offset;
        if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("The executable is not Windows x64.");
        return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "";
    }
    public SemanticVersion VersionOf(string path) => SemanticVersion.Parse(readVersion(path));
    public static string Hash(string file, CancellationToken token = default)
    {
        using var stream = File.OpenRead(file); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[81920]; int read;
        while ((read = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static void VerifyHash(string file, string expected, CancellationToken token = default)
    {
        if (expected is null || expected.Length != 64 || !expected.All(char.IsAsciiHexDigit) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(file, token)), Convert.FromHexString(expected))) throw new InvalidDataException("SHA-256 checksum mismatch. The package was not installed.");
    }
    public static string ParseChecksum(string text, string assetName)
    {
        string[] parts = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0].Length != 64 || !parts[0].All(char.IsAsciiHexDigit) || parts[1].TrimStart('*') != assetName) throw new InvalidDataException("The published checksum file is malformed or names a different package.");
        return parts[0].ToLowerInvariant();
    }
    public static string SafeEntry(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || name.Split('/').Any(p => p is "" or "." or "..")) throw new InvalidDataException("Unsafe ZIP entry path.");
        if (!Payload.Contains(name, StringComparer.Ordinal) && name != "update-manifest.json") throw new InvalidDataException($"Unexpected package entry: {name}");
        return name;
    }
    public PackageManifest ExtractAndVerify(string archivePath, string staging, SemanticVersion expected, CancellationToken token)
    {
        if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
        Directory.CreateDirectory(staging);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > Payload.Length + 2) throw new InvalidDataException("Too many archive entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        bool directorySeen = false;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0) throw new InvalidDataException("Linked ZIP entry.");
            if (entry.FullName == "licenses/" && entry.Length == 0) { if (directorySeen) throw new InvalidDataException("Duplicate directory entry."); directorySeen = true; continue; }
            string name = SafeEntry(entry.FullName);
            if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0) throw new InvalidDataException("Duplicate or linked archive entry.");
            total += entry.Length;
            if (entry.Length > 250L * 1024 * 1024 || total > 600L * 1024 * 1024 || (name == "update-manifest.json" && entry.Length > 16384)) throw new InvalidDataException("Package exceeds allowed size.");
            string path = Path.GetFullPath(Path.Combine(staging, name));
            if (!path.StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP path escaped staging.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = entry.Open(); using var destination = File.Create(path);
            var buffer = new byte[81920]; long written = 0; int read;
            while ((read = source.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); written += read; if (written > entry.Length) throw new InvalidDataException("Invalid archive size."); destination.Write(buffer, 0, read); }
            if (written != entry.Length) throw new InvalidDataException("Truncated ZIP entry.");
        }
        if (names.Count != Payload.Length + 1) throw new InvalidDataException("The package is missing required application or updater files.");
        return VerifyStaging(staging, expected);
    }
    public PackageManifest VerifyStaging(string staging, SemanticVersion expected)
    {
        UpdateChannel.RejectReparse(staging);
        foreach (string file in Directory.EnumerateFileSystemEntries(staging, "*", SearchOption.TopDirectoryOnly)) UpdateChannel.RejectReparse(file);
        string manifestPath = Path.Combine(staging, "update-manifest.json");
        if (new FileInfo(manifestPath).Length > 16384) throw new InvalidDataException("Manifest is too large.");
        var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath)) ?? throw new InvalidDataException("Missing package manifest.");
        if (manifest.FormatVersion != 1 || SemanticVersion.Parse(manifest.Version).CompareTo(expected) != 0 || expected.Prerelease is not null || manifest.SourceCommit is null || manifest.SourceCommit.Length != 40 || !manifest.SourceCommit.All(char.IsAsciiHexDigit) || manifest.Files is null || !manifest.Files.Keys.Order().SequenceEqual(Payload.Order())) throw new InvalidDataException("Unsupported or inconsistent package manifest.");
        foreach (var file in Payload)
        {
            string path = Path.Combine(staging, file); UpdateChannel.RejectReparse(path); VerifyHash(path, manifest.Files[file]);
        }
        foreach (string executable in new[] { "ClickForge.exe", "ClickForge.Updater.exe" })
            if (SemanticVersion.Parse(readVersion(Path.Combine(staging, executable))).CompareTo(expected) != 0) throw new InvalidDataException("Executable version does not match the release.");
        return manifest;
    }
    public PackageManifest VerifyStagingFromArchive(string archivePath, string staging, SemanticVersion expected)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Where(e => e.FullName == "update-manifest.json").ToArray();
        if (entries.Length != 1 || entries[0].Length > 16384) throw new InvalidDataException("Invalid archived manifest.");
        using var source = entries[0].Open();
        byte[] content = new byte[checked((int)entries[0].Length)]; source.ReadExactly(content);
        if (source.ReadByte() != -1) throw new InvalidDataException("Archived manifest exceeds its declared size.");
        byte[] original = SHA256.HashData(content);
        if (!CryptographicOperations.FixedTimeEquals(original, Convert.FromHexString(Hash(Path.Combine(staging, "update-manifest.json"))))) throw new InvalidDataException("Staged manifest differs from the verified archive.");
        return VerifyStaging(staging, expected);
    }
}
