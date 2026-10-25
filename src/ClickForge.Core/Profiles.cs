using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickForge.Core;

public sealed record TimingProfile
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public Guid Id { get; init; } = Guid.NewGuid();
    [JsonRequired] public string Name { get; init; } = "New profile";
    public string Description { get; init; } = "";
    [JsonRequired] public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    [JsonRequired] public DateTimeOffset Modified { get; init; } = DateTimeOffset.UtcNow;
    [JsonRequired] public ClickTimingMode TimingMode { get; init; }
    [JsonRequired] public int FixedCps { get; init; } = 10;
    [JsonRequired] public int MinimumCps { get; init; } = 5;
    [JsonRequired] public int MaximumCps { get; init; } = 20;
    [JsonRequired] public ActivationMode ActivationMode { get; init; }
    public ClickButton Button { get; init; }
    public int CustomKey { get; init; }
    [JsonIgnore] public string Summary => (TimingMode == ClickTimingMode.Fixed ? $"Fixed · {FixedCps} CPS" : $"Variable · {MinimumCps}–{MaximumCps} CPS") + " · " + ClickButtons.OutputLabel(Button,CustomKey);
    public override string ToString() => Name + " · " + Summary;
    public void Validate()
    {
        if (SchemaVersion != 1) throw new ArgumentException("Unsupported profile version. Use a version 1 profile.");
        if (Id == Guid.Empty) throw new ArgumentException("The profile identifier is invalid.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Name.Any(char.IsControl)) throw new ArgumentException("Use a profile name of 1–80 characters without control characters.");
        if (Description is null || Description.Length > 500 || Description.Any(c => char.IsControl(c) && c != '\n' && c != '\r')) throw new ArgumentException("Use a description of up to 500 characters.");
        if (Created == default || Modified < Created) throw new ArgumentException("The profile timestamps are invalid.");
        Speed.Validate(FixedCps); _ = new CpsRange(MinimumCps, MaximumCps);
        KeyboardOutput.ValidateOutput(Button,CustomKey);
        if (!Enum.IsDefined(TimingMode) || !Enum.IsDefined(ActivationMode)) throw new ArgumentException("The profile timing or activation mode is invalid.");
    }
    public static TimingProfile Capture(Settings value, string name) => new() { Name = name.Trim(), FixedCps = value.Cps, MinimumCps = value.MinimumCps, MaximumCps = value.MaximumCps, TimingMode = value.TimingMode, ActivationMode = value.Mode, Button = value.Button, CustomKey = value.CustomKey };
    public Settings Apply(Settings current, ClickEngine engine)
    {
        Validate();
        var next = current with { Cps = FixedCps, MinimumCps = MinimumCps, MaximumCps = MaximumCps, TimingMode = TimingMode, Mode = ActivationMode, Button = Button, CustomKey = CustomKey, ActiveProfileId = Id };
        next.Validate();
        engine.Stop(); // Synchronous barrier waits for an in-flight input pair.
        engine.Cps = FixedCps; engine.ConfigureTiming(TimingMode, new(MinimumCps, MaximumCps)); engine.ConfigureOutput(Button,CustomKey);
        return next; // No target, restriction, shortcut or active state is profile data.
    }
    public bool Matches(Settings value) => FixedCps == value.Cps && MinimumCps == value.MinimumCps && MaximumCps == value.MaximumCps && TimingMode == value.TimingMode && ActivationMode == value.Mode && Button == value.Button && CustomKey == value.CustomKey;
}

public sealed class ProfileStore(string directory)
{
    public const int MaximumProfiles = 100, MaximumImportBytes = 16384;
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true, AllowDuplicateProperties = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public List<string> Warnings { get; } = [];
    private string FileFor(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");
    public TimingProfile[] Load()
    {
        lock (UserData.Gate(directory))
        {
            Warnings.Clear();
            if (!Directory.Exists(directory)) return [];
            var result = new List<TimingProfile>();
            try
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.json").Order().Take(MaximumProfiles + 1))
                {
                    if (result.Count >= MaximumProfiles) { Warnings.Add("Profile limit reached. Extra files were skipped."); break; }
                    try
                    {
                        var item = Read(file);
                        if (!string.Equals(Path.GetFileNameWithoutExtension(file), item.Id.ToString("N"), StringComparison.OrdinalIgnoreCase) || result.Any(p => p.Id == item.Id)) throw new ArgumentException("Profile identifier mismatch.");
                        result.Add(item);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { Warnings.Add("A profile could not be loaded. Other valid profiles remain available."); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Warnings.Add("Profiles could not be read. Check access to the user data folder."); }
            return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
    }
    public void Save(TimingProfile profile)
    {
        profile.Validate();
        lock (UserData.Gate(directory))
        {
            if (!File.Exists(FileFor(profile.Id)) && Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Count() >= MaximumProfiles) throw new ArgumentException("The limit is 100 profiles. Delete a profile before creating another.");
            UserData.Write(FileFor(profile.Id), profile, json);
        }
    }
    public TimingProfile Create(Settings settings, string name) { var p = TimingProfile.Capture(settings, name); Save(p); return p; }
    public TimingProfile Rename(TimingProfile profile, string name, string description)
    { var p = profile with { Name = name.Trim(), Description = description, Modified = Later(profile) }; Save(p); return p; }
    public TimingProfile Duplicate(TimingProfile profile)
    { profile.Validate(); var now = DateTimeOffset.UtcNow; var p = profile with { Id = Guid.NewGuid(), Name = profile.Name[..Math.Min(73, profile.Name.Length)] + " (copy)", Created = now, Modified = now }; Save(p); return p; }
    public void Delete(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Invalid profile identifier."); lock (UserData.Gate(directory)) File.Delete(FileFor(id)); }
    public TimingProfile Import(string path)
    {
        var imported = Read(path); var now = DateTimeOffset.UtcNow;
        var p = imported with { Id = Guid.NewGuid(), Created = now, Modified = now }; Save(p); return p;
    }
    public static void Export(string path, TimingProfile profile) { profile.Validate(); UserData.Write(path, profile, json); }
    public static TimingProfile Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaximumImportBytes) throw new ArgumentException("Profile files must be 16 KiB or smaller.");
        var p = JsonSerializer.Deserialize<TimingProfile>(file, json) ?? throw new ArgumentException("The profile is empty.");
        p.Validate(); return p;
    }
    public TimingProfile[] RestoreBuiltIns()
    {
        // Stable IDs make this repeatable; user-created profiles are retained.
        TimingProfile[] defaults = [new() { Id = Guid.Parse("732e9ed5-cacb-4f21-ae25-cb943210f010"), Name = "Standard" }, new() { Id = Guid.Parse("732e9ed5-cacb-4f21-ae25-cb943210f020"), Name = "Variable", TimingMode = ClickTimingMode.Variable }];
        foreach (var profile in defaults) Save(profile);
        return defaults;
    }
    private static DateTimeOffset Later(TimingProfile p) => DateTimeOffset.UtcNow > p.Created ? DateTimeOffset.UtcNow : p.Created;
}
