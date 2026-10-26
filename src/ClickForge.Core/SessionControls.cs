namespace ClickForge.Core;

public readonly record struct SessionLimits(int? Seconds = null, long? InputPairs = null)
{
    public void Validate()
    {
        if (Seconds is < 1 or > 86400) throw new ArgumentException("Duration must be 1–86400 whole seconds, or disabled.");
        if (InputPairs is < 1 or > 1_000_000_000) throw new ArgumentException("Pair limit must be 1–1,000,000,000, or disabled.");
    }
    public SessionEventKind? Reached(SessionSnapshot session) =>
        Seconds is { } seconds && session.Elapsed.TotalSeconds >= seconds ? SessionEventKind.DurationLimit :
        InputPairs is { } count && session.InputPairsSent >= count ? SessionEventKind.PairLimit : null;
    public string Summary => Seconds is null && InputPairs is null ? "No auto-stop limits" :
        string.Join(" · ", new[] { Seconds is { } s ? $"{s:N0}s wall-clock" : null,
            InputPairs is { } p ? $"{p:N0} input pairs sent" : null }.Where(s => s is not null));
}

public enum SessionEventKind { Started, Paused, Resumed, Stopped, EmergencyStop, TargetLost, TargetRestored, DurationLimit, PairLimit, ActivationEnded, InputError, ConfigurationStop, Shutdown }
public readonly record struct SessionEvent(DateTimeOffset Time, SessionEventKind Kind)
{
    public string Description => Kind switch
    {
        SessionEventKind.Started => "Session explicitly started", SessionEventKind.Paused => "Paused by user",
        SessionEventKind.Resumed => "Resumed by user", SessionEventKind.Stopped => "Session stopped",
        SessionEventKind.EmergencyStop => "Emergency stop requested", SessionEventKind.TargetLost => "Target ineligible; input waiting",
        SessionEventKind.TargetRestored => "Target eligibility restored", SessionEventKind.DurationLimit => "Wall-clock duration limit reached",
        SessionEventKind.PairLimit => "Input-pair limit reached", SessionEventKind.ActivationEnded => "Hold activation ended",
        SessionEventKind.InputError => "Input or validation error; stopped safely",
        SessionEventKind.ConfigurationStop => "Configuration changed; session stopped", _ => "Application shutdown"
    };
    public override string ToString() => $"{Time.LocalDateTime:HH:mm:ss}  {Description}";
}

// No callbacks or per-pair logging. Records contain only time and event type.
public sealed class SafetyTimeline
{
    private readonly Queue<SessionEvent> entries = new();
    private readonly TimeProvider clock;
    private readonly int capacity;
    private long version;
    public SafetyTimeline(TimeProvider? clock = null, int capacity = 100)
    {
        if (capacity is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.clock = clock ?? TimeProvider.System; this.capacity = capacity;
    }
    public long Version { get { lock (entries) return version; } }
    public SessionEvent[] Entries { get { lock (entries) return entries.ToArray(); } }
    public void Record(SessionEventKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Invalid safety event.");
        lock (entries) { entries.Enqueue(new(clock.GetUtcNow(), kind)); while (entries.Count > capacity) entries.Dequeue(); version++; }
    }
    public void Clear() { lock (entries) { entries.Clear(); version++; } }
}

public readonly record struct ControlAvailability(bool StartEnabled, bool PauseEnabled, string StartLabel, string PauseLabel)
{
    public static ControlAvailability For(bool enabled, bool paused, ActivationMode mode, bool shortcutsReady, bool blocked) =>
        new(shortcutsReady && (enabled || !blocked && mode == ActivationMode.Toggle), enabled,
            enabled ? "Stop input" : mode == ActivationMode.Hold ? "Use Hold shortcut" : "Start input", paused ? "Resume" : "Pause");
}

public static class PaletteInput
{
    public static bool TryCps(string query, out int cps)
    {
        cps = 0;
        var parts = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0].Equals("cps", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(parts[1], out cps) && cps is >= 1 and <= 50;
    }
}
