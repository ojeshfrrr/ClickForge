namespace ClickForge.Core;

public readonly record struct SessionSnapshot(bool HasStarted, bool TimerRunning, TimeSpan Elapsed, long InputPairsSent)
{
    // Wall-clock duration deliberately includes manual pauses and target waits.
    // This is submitted input throughput, never target acceptance or configured CPS.
    public double MeasuredAverageCps => Elapsed.TotalSeconds > 0 ? InputPairsSent / Elapsed.TotalSeconds : 0;
}

// The engine owns this tracker under its existing gate; no per-click callbacks,
// UI dispatch, content collection or persistence are required.
public sealed class SessionStatistics(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private bool started, running;
    private long startTime, stopTime, startCount, stopCount;
    public void Start(long lifetimePairs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lifetimePairs);
        started = running = true; startCount = lifetimePairs; startTime = clock.GetTimestamp();
    }
    public void Stop(long lifetimePairs)
    {
        if (!running) return;
        stopTime = clock.GetTimestamp(); stopCount = lifetimePairs; running = false;
    }
    public SessionSnapshot Snapshot(long lifetimePairs)
    {
        if (!started) return default;
        var elapsed = clock.GetElapsedTime(startTime, running ? clock.GetTimestamp() : stopTime);
        return new(true, running, elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed, Math.Max(0, (running ? lifetimePairs : stopCount) - startCount));
    }
}
