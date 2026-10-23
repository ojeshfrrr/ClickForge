using System.Diagnostics;

namespace ClickForge.Core;

public enum EngineState { Stopped, Running, Paused, Error }
public interface IMouseInput { void Click(); void Release(); }
public interface ITargetGate { bool CanClick { get; } }
public sealed class ClickSchedule
{
    public double Next { get; private set; }
    public void Reset(double now) => Next = now;
    public void Advance(double now, int cps) { Next += Speed.Interval(cps); if (Next <= now) Next = now + Speed.Interval(cps); }
    public int WaitMilliseconds(double now) => (int)Math.Clamp(Math.Ceiling(Next - now), 0, 50);
}

public sealed class ClickEngine(IMouseInput input, ITargetGate target) : IDisposable
{
    private readonly object gate = new();
    private readonly LoopWaiter wake = new();
    private Task? worker;
    private bool enabled, paused, disposed;
    private int cps = 10;
    private EngineState state;
    private string? error;
    private long clicks;
    public Func<bool> ActivationStillValid { get; set; } = () => true;
    public EngineState State { get { lock (gate) return state; } }
    public string? Error { get { lock (gate) return error; } }
    public bool Enabled { get { lock (gate) return enabled; } }
    public bool IsPaused { get { lock (gate) return paused; } }
    public long Clicks => Interlocked.Read(ref clicks);
    public int Cps
    {
        get { lock (gate) return cps; }
        set { Speed.Validate(value); lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); cps = value; wake.Set(); } }
    }
    public void Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (enabled) return;
            enabled = true; paused = false; error = null;
            state = EngineState.Paused;
            worker ??= Task.Factory.StartNew(Loop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            wake.Set();
        }
    }
    public void Stop()
    {
        lock (gate)
        {
            if (disposed) return;
            enabled = false; paused = false;
            if (state != EngineState.Error) state = EngineState.Stopped;
            ReleaseSafely(); wake.Set();
        }
    }
    public void TogglePause()
    {
        lock (gate)
        {
            if (!enabled || disposed) return;
            paused = !paused;
            if (paused) { state = EngineState.Paused; ReleaseSafely(); }
            wake.Set();
        }
    }
    private void Fail(Exception e) { enabled = false; paused = false; state = EngineState.Error; error = e.Message; }
    private void ReleaseSafely() { try { input.Release(); } catch (Exception e) { Fail(e); } }
    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        var schedule = new ClickSchedule();
        int previousCps = cps;
        bool wasActive = false;
        while (true)
        {
            int wait;
            lock (gate)
            {
                if (disposed) return;
                try
                {
                    if (enabled && !ActivationStillValid()) { enabled = false; paused = false; state = EngineState.Stopped; ReleaseSafely(); }
                    bool active = enabled && !paused && target.CanClick;
                    if (!active)
                    {
                        if (wasActive) ReleaseSafely();
                        if (enabled) state = EngineState.Paused;
                        wasActive = false;
                        wait = enabled ? 50 : Timeout.Infinite;
                    }
                    else
                    {
                        double now = clock.Elapsed.TotalMilliseconds;
                        if (!wasActive || previousCps != cps) schedule.Reset(now);
                        wasActive = true; previousCps = cps; state = EngineState.Running;
                        if (schedule.WaitMilliseconds(now) == 0)
                        {
                            // Stop and pause share this lock: no new pair can begin after either returns.
                            input.Click(); Interlocked.Increment(ref clicks);
                            schedule.Advance(clock.Elapsed.TotalMilliseconds, cps);
                        }
                        wait = schedule.WaitMilliseconds(clock.Elapsed.TotalMilliseconds);
                    }
                }
                catch (Exception e) { Fail(e); ReleaseSafely(); wasActive = false; wait = Timeout.Infinite; }
            }
            try { wake.WaitOne(wait); }
            catch (Exception e) { lock (gate) { Fail(e); ReleaseSafely(); } }
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            enabled = false; paused = false; disposed = true;
            ReleaseSafely(); if (state != EngineState.Error) state = EngineState.Stopped;
            wake.Set();
        }
        worker?.GetAwaiter().GetResult();
        wake.Dispose();
    }
}
