namespace ClickForge.Core;

public readonly record struct TimingScopeSummary(int Count, double ScheduledMean, double MeasuredMean, double Minimum, double Maximum, double StandardDeviation, double MeanDelay, double MaximumDelay)
{
    public static TimingScopeSummary Calculate(IReadOnlyList<LogicalClickEvent> events)
    {
        if (events.Count == 0) return default;
        if (events.Any(e => !double.IsFinite(e.RequestedIntervalMs) || !double.IsFinite(e.MeasuredIntervalMs) || e.RequestedIntervalMs < 0 || e.MeasuredIntervalMs < 0))
            throw new ArgumentException("Scope samples must be finite nonnegative intervals.");
        double mean = events.Average(e => e.MeasuredIntervalMs);
        return new(events.Count, events.Average(e => e.RequestedIntervalMs), mean, events.Min(e => e.MeasuredIntervalMs), events.Max(e => e.MeasuredIntervalMs),
            Math.Sqrt(events.Average(e => Math.Pow(e.MeasuredIntervalMs - mean, 2))),
            events.Average(e => Math.Max(0, e.MeasuredIntervalMs - e.RequestedIntervalMs)), events.Max(e => Math.Max(0, e.MeasuredIntervalMs - e.RequestedIntervalMs)));
    }
}
