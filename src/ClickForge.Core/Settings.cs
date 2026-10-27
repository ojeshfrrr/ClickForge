using System.Text.Json;

namespace ClickForge.Core;

public enum ActivationMode { Toggle, Hold }
public sealed record Settings
{
    public int Cps { get; init; } = 10;
    public ClickButton Button { get; init; }
    public int CustomKey { get; init; }
    public ClickTimingMode TimingMode { get; init; }
    public int MinimumCps { get; init; } = 5;
    public int MaximumCps { get; init; } = 20;
    public ActivationMode Mode { get; init; }
    public int ActivateKey { get; init; } = 0x75;
    public int PauseKey { get; init; } = 0x76;
    public int StopKey { get; init; } = 0x77;
    public bool SideButtonToggles { get; init; }
    public MouseSetup MouseSetup { get; init; }
    public bool RobloxOnly { get; init; } = true;
    public double Width { get; init; } = 1280;
    public double Height { get; init; } = 920;
    public int LayoutVersion { get; init; }
    public bool CheckForUpdatesOnStartup { get; init; }
    public bool MinimizeToTray { get; init; }
    public bool CloseToTray { get; init; }
    public Guid? ActiveProfileId { get; init; }
    public int? MaximumSessionSeconds { get; init; }
    public long? MaximumSessionPairs { get; init; }
    public void Validate()
    {
        Speed.Validate(Cps);
        new SessionLimits(MaximumSessionSeconds, MaximumSessionPairs).Validate();
        KeyboardOutput.ValidateOutput(Button,CustomKey);
        if (!Enum.IsDefined(TimingMode)) throw new ArgumentException("Invalid click speed mode.");
        _ = new CpsRange(MinimumCps, MaximumCps);
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Invalid activation mode.");
        if (!Enum.IsDefined(MouseSetup)) throw new ArgumentException("Choose a supported mouse setup.");
        int[] keys = [ActivateKey, PauseKey, StopKey];
        if (keys.Any(k => !ShortcutKeys.IsValid(k))) throw new ArgumentException("Choose F1–F11, Mouse 4 (Back), or Mouse 5 (Forward).");
        if (keys.Distinct().Count() != 3) throw new ArgumentException("Each shortcut must use a different key.");
        if (SideButtonToggles && keys.Any(ShortcutKeys.IsMouse)) throw new ArgumentException("Mouse 4 and Mouse 5 are reserved for Left and Right output toggles. Choose keyboard Activate, Pause and Emergency stop shortcuts first.");
        if (Button == ClickButton.CustomKey && keys.Contains(CustomKey)) throw new ArgumentException("The custom output key cannot match Activate, Pause, or Emergency stop. Choose another output key or shortcut.");
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width < 420 || Width > 1400 || Height < 600 || Height > 1600)
            throw new ArgumentException("Invalid window size.");
    }
}
public static class Speed
{
    public static void Validate(int cps) { if (cps is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(cps), "Use a whole number from 1 to 50 CPS."); }
    public static double Interval(int cps) { Validate(cps); return 1000d / cps; }
}
public sealed class SettingsService(string path)
{
    public string? Warning { get; private set; }
    public Settings Load()
    {
        try
        {
            Warning = null;
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 32768) throw new JsonException("Configuration too large.");
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new JsonException("Empty configuration.");
            settings.Validate();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Warning = "Settings could not be loaded. Defaults restored. Check folder access or restore a valid settings file."; return new(); }
    }
    public void Save(Settings settings)
    {
        settings.Validate();
        UserData.Write(path, settings, new JsonSerializerOptions { WriteIndented = true });
    }
}

public interface IHotkeyRegistrar { bool Register(int id, int key); void Unregister(int id); }
public sealed class HotkeyBindings(IHotkeyRegistrar registrar) : IDisposable
{
    private readonly List<int> ids = [];
    public void Apply(Settings settings)
    {
        settings.Validate();
        Dispose();
        int[] keys = [settings.ActivateKey, settings.PauseKey, settings.StopKey];
        try
        {
            // Reserve the emergency key first. Partial registrations are always rolled back.
            foreach (int i in new[] { 2, 0, 1 })
            {
                if (!registrar.Register(i + 1, keys[i])) throw new InvalidOperationException($"{ShortcutKeys.Label(keys[i])} is unavailable. Check shortcut registration, choose another shortcut, and apply again.");
                ids.Add(i + 1);
            }
            if (settings.SideButtonToggles)
                foreach (var (id, key) in SideButtonOutputs.Bindings)
                {
                    if (!registrar.Register(id, key)) throw new InvalidOperationException($"{ShortcutKeys.Label(key)} output toggle is unavailable. Apply shortcuts again. Clicking remains stopped.");
                    ids.Add(id);
                }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (int id in ids) registrar.Unregister(id); ids.Clear(); }
}
