namespace ClickForge.Core;

public enum ActivityKind { Startup, Started, Stopped, Paused, Waiting, EmergencyStop, InputError, ProfileApplied, ProfileImported, PersistenceError, SettingsRecovery, UpdateAvailable, UpdateFailure, TrayUnavailable }
public sealed record ActivityEntry(DateTimeOffset Time, ActivityKind Kind)
{
    public string Message => Kind switch
    {
        ActivityKind.Startup => "Application opened; clicking disabled.", ActivityKind.Started => "Click request activated.",
        ActivityKind.Stopped => "Click request stopped.", ActivityKind.Paused => "Click request paused.",
        ActivityKind.Waiting => "Waiting for an eligible foreground target and pointer position.",
        ActivityKind.EmergencyStop => "Emergency stop requested.", ActivityKind.InputError => "Input stopped after an error. Review the main status.",
        ActivityKind.ProfileApplied => "Profile applied; clicking remains stopped.", ActivityKind.ProfileImported => "Profile imported; select and apply it to use it.",
        ActivityKind.PersistenceError => "User configuration could not be saved.", ActivityKind.SettingsRecovery => "User configuration recovery was required.",
        ActivityKind.UpdateAvailable => "A stable update is available.", ActivityKind.UpdateFailure => "Update operation failed. Review Updates for details.",
        _ => "System tray unavailable. The window will remain accessible."
    };
    public override string ToString() => $"{Time.LocalDateTime:HH:mm:ss}  {Message}";
}
public sealed class ActivityHistory(int capacity = 100)
{
    private readonly Queue<ActivityEntry> entries = new();
    public ActivityEntry[] Entries { get { lock (entries) return entries.ToArray(); } }
    public void Add(ActivityKind kind)
    {
        lock (entries)
        {
            if (capacity is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(capacity));
            entries.Enqueue(new(DateTimeOffset.Now, kind)); while (entries.Count > capacity) entries.Dequeue();
        }
    }
    public void Clear() { lock (entries) entries.Clear(); }
    public string RedactedReport() => string.Join(Environment.NewLine, Entries.Select(e => $"{e.Time:O} {e.Message}"));
}
