using System.Diagnostics;

namespace ClickForge.Core;

public sealed record VariableTimingRange
{
    public int MinimumCps { get; }
    public int MaximumCps { get; }
    public double ShortestIntervalMs => 1000d / MaximumCps;
    public double LongestIntervalMs => 1000d / MinimumCps;
    public VariableTimingRange(int minimumCps, int maximumCps)
    {
        if (minimumCps is < 5 or > 20 || maximumCps is < 5 or > 20)
            throw new ArgumentOutOfRangeException(nameof(minimumCps), "Both rates must be whole numbers from 5 to 20 CPS.");
        if (minimumCps > maximumCps) throw new ArgumentException("Minimum CPS must be less than or equal to maximum CPS.");
        MinimumCps = minimumCps; MaximumCps = maximumCps;
    }
}

// Uniform sampling in interval space: 5–20 CPS means intervals of 50–200 ms.
// Its expected long-run throughput is 8 CPS, not the midpoint of the rate range.
public sealed class VariableIntervalSequence(VariableTimingRange range, int? seed = null)
{
    private readonly Random random = seed.HasValue ? new Random(seed.Value) : new Random();
    public double NextMilliseconds() => range.ShortestIntervalMs + random.NextDouble() * (range.LongestIntervalMs - range.ShortestIntervalMs);
}

public sealed record LogicalClickEvent(int Number, double RequestedIntervalMs, double MeasuredIntervalMs, double ElapsedMs);
public sealed record TimingMeasurement(int EventCount, double ElapsedMs, double AverageCps, double MeanIntervalMs)
{
    public static TimingMeasurement Calculate(IReadOnlyList<LogicalClickEvent> events, double elapsedMs)
    {
        if (!double.IsFinite(elapsedMs) || elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        return new(events.Count, elapsedMs, elapsedMs > 0 ? events.Count * 1000d / elapsedMs : 0,
            events.Count > 0 ? events.Average(e => e.MeasuredIntervalMs) : 0);
    }
}
public sealed record SimulationSnapshot(bool Running, bool Cancelled, string? Error, LogicalClickEvent[] Events, TimingMeasurement Measurement);

/// <summary>Bounded logical-event simulation. Has no mouse service, live engine,
/// native input API, callback or target-process dependency.</summary>
public sealed class VariableTimingSimulation : IDisposable
{
    private readonly object gate = new();
    private readonly LoopWaiter wake = new();
    private readonly List<LogicalClickEvent> events = [];
    private readonly Stopwatch clock = new();
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private bool running, cancelled, disposed;
    private string? error;
    public Task Completion { get { lock (gate) return worker ?? Task.CompletedTask; } }
    public SimulationSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                var copy = events.ToArray();
                return new(running, cancelled, error, copy, TimingMeasurement.Calculate(copy, clock.Elapsed.TotalMilliseconds));
            }
        }
    }
    public Task Start(VariableTimingRange range, int? seed = null, TimeSpan? duration = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(range);
        TimeSpan length = duration ?? TimeSpan.FromSeconds(5);
        if (length <= TimeSpan.Zero || length > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(duration), "Use a duration above zero and at most 30 seconds.");
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (worker is { IsCompleted: false }) throw new InvalidOperationException("The test is already running or stopping.");
            cancellation?.Dispose(); cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            events.Clear(); clock.Reset(); error = null; cancelled = false; running = true;
            var runToken = cancellation.Token;
            worker = Task.Factory.StartNew(() => Run(range, seed, length.TotalMilliseconds, runToken), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return worker;
        }
    }
    private void Run(VariableTimingRange range, int? seed, double durationMs, CancellationToken token)
    {
        using var registration = token.Register(wake.Set);
        var sequence = new VariableIntervalSequence(range, seed);
        try
        {
            lock (gate) clock.Start();
            double previous = 0;
            while (true)
            {
                double interval = sequence.NextMilliseconds();
                double due = previous + interval;
                while (true)
                {
                    int wait;
                    lock (gate)
                    {
                        if (!running || token.IsCancellationRequested) { cancelled = true; return; }
                        double now = clock.Elapsed.TotalMilliseconds;
                        if (now >= durationMs) return;
                        if (now >= due)
                        {
                            events.Add(new(events.Count + 1, interval, now - previous, now));
                            previous = now; // Never burst to catch up after a delayed wake.
                            break;
                        }
                        wait = (int)Math.Clamp(Math.Ceiling(Math.Min(due, durationMs) - now), 1, 50);
                    }
                    wake.WaitOne(wait);
                }
            }
        }
        catch (Exception e) { lock (gate) error = e.Message; }
        finally { lock (gate) { clock.Stop(); running = false; cancelled |= token.IsCancellationRequested; } }
    }
    public void Stop()
    {
        lock (gate)
        {
            if (disposed || !running) return;
            running = false; cancelled = true; clock.Stop();
            cancellation!.Cancel(); wake.Set();
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            Stop(); disposed = true;
        }
        worker?.GetAwaiter().GetResult();
        cancellation?.Dispose(); wake.Dispose();
    }
}
