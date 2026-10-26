using System.Text.Json;
using ClickForge.Core;

internal static class AdvancedControlTests
{
    public static void Run(Action<string, Action> test)
    {
        void Check(bool condition) { if (!condition) throw new Exception("Advanced control assertion failed"); }
        void Until(Func<bool> condition) { if (!SpinWait.SpinUntil(condition, 2500)) throw new Exception("Advanced control test timed out"); }
        void Invalid(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected validation rejection"); }
        test("Limits validate optional independent bounds and reject invalid settings", () =>
        {
            new SessionLimits().Validate(); new SessionLimits(1, 1).Validate(); new SessionLimits(86400, 1_000_000_000).Validate();
            foreach (int seconds in new[] { -1, 0, 86401 }) Invalid(() => new SessionLimits(seconds).Validate());
            foreach (long pairs in new[] { -1L, 0, 1_000_000_001 }) Invalid(() => new SessionLimits(InputPairs: pairs).Validate());
            Invalid(() => new Settings { MaximumSessionPairs = 0 }.Validate());
        });
        test("Pair cap is exact with duplicate starts and one worker", () =>
        {
            var input = new Input(); using var engine = new ClickEngine(input, new Gate()) { Cps = 50 };
            engine.ConfigureLimits(new(InputPairs: 3)); engine.Start(); engine.Start(); Until(() => !engine.Enabled);
            Check(input.Count == 3 && engine.Session.InputPairsSent == 3 && engine.LastStopReason == SessionEventKind.PairLimit && input.Releases > 0);
            Thread.Sleep(70); Check(input.Count == 3);
        });
        test("Pair caps also stop the existing variable worker without overshoot", () =>
        {
            var input = new Input(); using var engine = new ClickEngine(input, new Gate(), new Gate());
            engine.ConfigureTiming(ClickTimingMode.Variable, new(50, 50)); engine.ConfigureLimits(new(InputPairs: 2)); engine.Start();
            Until(() => !engine.Enabled); Check(input.Count == 2 && engine.LastStopReason == SessionEventKind.PairLimit);
        });
        test("Duration limit includes manual pause and freezes session", () =>
        {
            var clock = new Clock(); using var engine = new ClickEngine(new Input(), new Gate(), timeProvider: clock);
            engine.ConfigureLimits(new(2)); engine.Start(); Until(() => engine.Clicks > 0); engine.TogglePause();
            clock.Advance(2); Until(() => !engine.Enabled); var stopped = engine.Session;
            Check(engine.LastStopReason == SessionEventKind.DurationLimit && stopped.Elapsed.TotalSeconds == 2);
            clock.Advance(10); engine.TogglePause(); Check(engine.Session == stopped && !engine.Enabled);
        });
        test("Duration expires during target waits with no submitted input", () =>
        {
            var clock = new Clock(); var input = new Input(); using var engine = new ClickEngine(input, new Gate { Allowed = false }, timeProvider: clock);
            engine.ConfigureLimits(new(1, 10)); engine.Start(); Until(() => engine.Timeline.Entries.Any(e => e.Kind == SessionEventKind.TargetLost));
            clock.Advance(1); Until(() => !engine.Enabled); Check(input.Count == 0 && engine.LastStopReason == SessionEventKind.DurationLimit);
        });
        test("Deadline rechecked after slow target validation before a pair", () =>
        {
            var clock = new Clock(); var input = new Input(); using var engine = new ClickEngine(input, new AdvancingGate(clock), timeProvider: clock);
            engine.ConfigureLimits(new(1)); engine.Start(); Until(() => !engine.Enabled); Check(input.Count == 0 && engine.LastStopReason == SessionEventKind.DurationLimit);
        });
        test("Pause resume preserve pair progress and explicit new start resets it", () =>
        {
            using var engine = new ClickEngine(new Input(), new Gate()) { Cps = 10 }; engine.ConfigureLimits(new(InputPairs: 2));
            engine.Start(); Until(() => engine.Clicks > 0); engine.TogglePause(); long count = engine.Session.InputPairsSent;
            Thread.Sleep(120); Check(engine.Session.InputPairsSent == count); engine.Start(); Check(engine.IsPaused);
            engine.TogglePause(); Until(() => !engine.Enabled); Check(engine.Session.InputPairsSent == 2);
            engine.Start(); Until(() => !engine.Enabled); Check(engine.Session.InputPairsSent == 2 && engine.Clicks == 4);
        });
        test("Limit edits stop rather than reset session progress or activate", () =>
        {
            using var engine = new ClickEngine(new Input(), new Gate()); engine.Start(); Until(() => engine.Clicks > 0);
            engine.ConfigureLimits(new(60, 20)); var frozen = engine.Session;
            Check(!engine.Enabled && frozen.HasStarted && frozen.InputPairsSent > 0 && engine.Timeline.Entries.Last().Kind == SessionEventKind.ConfigurationStop);
            engine.ConfigureLimits(new(90, 10)); Check(engine.Session == frozen && engine.ActiveSessionLimits == default);
            Invalid(() => engine.ConfigureLimits(new(0))); Check(engine.Limits == new SessionLimits(90, 10));
        });
        test("Limit stop waits for in-flight pair and releases the simulated button", () =>
        {
            var clock = new Clock(); using var input = new BlockingInput(); using var engine = new ClickEngine(input, new Gate(), timeProvider: clock);
            engine.ConfigureLimits(new(1, 1)); engine.Start(); Check(input.Entered.Wait(2000)); clock.Advance(1);
            var stop = Task.Run(engine.Stop); Check(!stop.Wait(25)); input.Finish.Set(); Check(stop.Wait(2000));
            Check(!input.Down && engine.Session.InputPairsSent == 1 && !engine.Enabled);
        });
    }
    private static void WithDirectory(Action<string> action)
    { string dir = Path.GetFullPath(Path.Combine("artifacts", "advanced-test-" + Guid.NewGuid())); Directory.CreateDirectory(dir); try { action(dir); } finally { Directory.Delete(dir, true); } }
    private sealed class Clock : TimeProvider { private long ticks; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Interlocked.Read(ref ticks); public void Advance(int seconds) => Interlocked.Add(ref ticks, seconds * 1000L); }
    private sealed class Gate : IVariableTargetGate { public volatile bool Allowed = true; public bool CanActivate => true; public bool CanClick => Allowed; }
    private sealed class AdvancingGate(Clock clock) : ITargetGate { public bool CanClick { get { clock.Advance(1); return true; } } }
    private sealed class Input : IMouseInput { public int Count, Releases; public void Click() => Interlocked.Increment(ref Count); public void Release() => Interlocked.Increment(ref Releases); }
    private sealed class FailingRelease : IMouseInput { public void Click() { } public void Release() => throw new InvalidOperationException("Mock release failure"); }
    private sealed class BlockingInput : IMouseInput, IDisposable
    { public readonly ManualResetEventSlim Entered = new(), Finish = new(); public bool Down; public void Click() { Down = true; Entered.Set(); Finish.Wait(); Down = false; } public void Release() => Down = false; public void Dispose() { Entered.Dispose(); Finish.Dispose(); } }
}
