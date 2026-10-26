using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickForge.Core;

// Separate local preferences. Never added to portable/imported timing profiles.
public sealed class ApplicationProfiles(string path)
{
    private sealed record Data(int Version, Dictionary<string, Guid> Associations);
    private static readonly JsonSerializerOptions json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false, MaxDepth = 4, WriteIndented = true };
    public string? Warning { get; private set; }
    public static string Identity(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath)) throw new ArgumentException("An absolute executable path is required.");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(executablePath).ToUpperInvariant())));
    }
    private static void ValidateKey(string key)
    { if (key is null || key.Length != 64 || !key.All(char.IsAsciiHexDigit)) throw new ArgumentException("Invalid application association key."); }
    private Data Load()
    {
        Warning = null;
        try
        {
            if (!File.Exists(path)) return new(1, new());
            if (new FileInfo(path).Length > 32768) throw new JsonException("Associations are too large.");
            var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), json) ?? throw new JsonException("Empty associations.");
            if (data.Version != 1 || data.Associations is null || data.Associations.Count > 100) throw new JsonException("Invalid association format.");
            foreach (var item in data.Associations) { ValidateKey(item.Key); if (item.Value == Guid.Empty) throw new ArgumentException("Invalid profile ID."); }
            return data;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Warning = "Application associations could not be read. No profile will be suggested."; return new(1, new()); }
    }
    public Guid? Find(string key, IEnumerable<TimingProfile> profiles)
    {
        ValidateKey(key);
        lock (UserData.Gate(path)) { var data = Load(); return data.Associations.TryGetValue(key, out var id) && profiles.Any(p => p.Id == id) ? id : null; }
    }
    public void Associate(string key, Guid profile)
    {
        ValidateKey(key); if (profile == Guid.Empty) throw new ArgumentException("Select a saved profile.");
        lock (UserData.Gate(path))
        {
            var data = Load(); if (Warning is not null) throw new ArgumentException(Warning + " Remove the invalid local association file before saving new associations.");
            if (!data.Associations.ContainsKey(key) && data.Associations.Count >= 100) throw new ArgumentException("Remove an association before adding more than 100.");
            data.Associations[key] = profile; UserData.Write(path, data, json);
        }
    }
    public void Remove(string key)
    {
        ValidateKey(key);
        lock (UserData.Gate(path)) { var data = Load(); if (Warning is not null) throw new ArgumentException(Warning); data.Associations.Remove(key); UserData.Write(path, data, json); }
    }
    public void Clear() { lock (UserData.Gate(path)) { UserData.Write(path, new Data(1, new()), json); Warning = null; } }
}
