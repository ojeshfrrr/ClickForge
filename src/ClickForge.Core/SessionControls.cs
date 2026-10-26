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
