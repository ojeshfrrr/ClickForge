using System.Diagnostics;

namespace ClickForge.Core;

public enum EngineState { Stopped, Running, Paused, Error }
public interface IMouseInput
{
    void Click(); void Release();
    bool Supports(ClickButton button) => button == ClickButton.Left;
    void Click(ClickButton button) { if (button != ClickButton.Left || !Supports(button)) throw new InvalidOperationException($"{ClickButtons.Label(button)} is not supported by the input service. No other button was sent."); Click(); }
    bool Supports(ClickButton button,int key) => button != ClickButton.CustomKey && Supports(button);
    void Click(ClickButton button,int key) { if (button == ClickButton.CustomKey) throw new InvalidOperationException("Custom keyboard input is unsupported. No fallback input was sent."); Click(button); }
}
public interface ITargetGate { bool CanClick { get; } }
public sealed class ClickSchedule
{
    public double Next { get; private set; }
    public void Reset(double now) => Next = now;
    public void Advance(double now, int cps) { Next += Speed.Interval(cps); if (Next <= now) Next = now + Speed.Interval(cps); }
    public void AdvanceInterval(double now, double interval) => Next = now + interval;
    public int WaitMilliseconds(double now) => (int)Math.Clamp(Math.Ceiling(Next - now), 0, 50);
}

public sealed class ClickEngine(IMouseInput input, ITargetGate target, IVariableTargetGate? variableTarget = null, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly object gate = new();
    private readonly LoopWaiter wake = new();
    private Task? worker;
    private bool enabled, paused, disposed;
    private int cps = 10;
    private ClickTimingMode timingMode;
    private CpsRange range = new(5, 20);
    private RateIntervalSequence sequence = new(new(5, 20));
    private int revision;
    private EngineState state;
    private string? error;
    private long clicks;
    private ClickButton button;
    private int customKey;
    public int CustomKey { get { lock(gate) return customKey; } }
    public ClickButton Button
    {
        get { lock (gate) return button; }
        set { lock(gate) ConfigureOutput(value,customKey); }
    }
    public void ConfigureOutput(ClickButton value,int key)
    {
        KeyboardOutput.ValidateOutput(value,key);
        lock(gate) { ObjectDisposedException.ThrowIf(disposed,this); if(button==value&&customKey==key)return; Stop(); button=value;customKey=key; }
    }
    private readonly SessionStatistics session = new(timeProvider);
    public SessionSnapshot Session { get { lock (gate) return session.Snapshot(clicks); } }
    public Func<bool> ActivationStillValid { get; set; } = () => true;
    public EngineState State { get { lock (gate) return state; } }
    public string? Error { get { lock (gate) return error; } }
    public bool Enabled { get { lock (gate) return enabled; } }
    public bool IsPaused { get { lock (gate) return paused; } }
    public long Clicks => Interlocked.Read(ref clicks);
    public ClickTimingMode TimingMode { get { lock (gate) return timingMode; } }
    public void ConfigureTiming(ClickTimingMode mode, CpsRange variableRange)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Invalid click speed mode.");
        variableRange.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (timingMode == mode && range == variableRange) return;
            // Configuration changes form a stop barrier. A fresh activation is explicit.
            enabled = false; paused = false; session.Stop(clicks); ReleaseSafely();
            if (state != EngineState.Error) state = EngineState.Stopped;
            timingMode = mode; range = variableRange; sequence = new(variableRange); revision++;
            wake.Set();
        }
    }
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
            if (!input.Supports(button,customKey)) throw new InvalidOperationException($"{ClickButtons.OutputLabel(button,customKey)} is not supported by the input service. Select a supported input. No fallback input was sent.");
            if (timingMode == ClickTimingMode.Variable && variableTarget?.CanActivate != true)
                throw new InvalidOperationException("Select an eligible target window, then activate from ClickForge or that window. Roblox must be a verified client.");
            session.Start(clicks);
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
            session.Stop(clicks);
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
    private void Fail(Exception e) { enabled = false; paused = false; session.Stop(clicks); state = EngineState.Error; error = e.Message; }
    private void ReleaseSafely() { try { input.Release(); } catch (Exception e) { Fail(e); } }
    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        var schedule = new ClickSchedule();
        int previousCps = cps;
        int previousRevision = -1;
        bool wasActive = false;
        while (true)
        {
            int wait;
            lock (gate)
            {
                if (disposed) return;
                try
                {
                    if (enabled && !ActivationStillValid()) { enabled = false; paused = false; session.Stop(clicks); state = EngineState.Stopped; ReleaseSafely(); }
                    bool active = enabled && !paused && (timingMode == ClickTimingMode.Variable ? variableTarget?.CanClick == true : target.CanClick);
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
                        if (!wasActive || previousCps != cps || previousRevision != revision)
                        {
                            schedule.Reset(now);
                            if (timingMode == ClickTimingMode.Variable) schedule.AdvanceInterval(now, sequence.NextMilliseconds());
                        }
                        previousRevision = revision;
                        wasActive = true; previousCps = cps; state = EngineState.Running;
                        if (schedule.WaitMilliseconds(now) == 0)
                        {
                            // Stop and pause share this lock: no new pair can begin after either returns.
                            input.Click(button,customKey); Interlocked.Increment(ref clicks);
                            if (timingMode == ClickTimingMode.Variable) schedule.AdvanceInterval(clock.Elapsed.TotalMilliseconds, sequence.NextMilliseconds());
                            else schedule.Advance(clock.Elapsed.TotalMilliseconds, cps);
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
            session.Stop(clicks);
            ReleaseSafely(); if (state != EngineState.Error) state = EngineState.Stopped;
            wake.Set();
        }
        worker?.GetAwaiter().GetResult();
        wake.Dispose();
    }
}
