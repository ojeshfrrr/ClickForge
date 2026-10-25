using ClickForge.Core;

internal static class SessionTargetTests
{
    public static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Session/target assertion failed"); }
        void Until(Func<bool> value) { if (!SpinWait.SpinUntil(value, 2000)) throw new Exception("Session test timed out"); }
        test("Session defaults are in-memory zero values", () =>
        { var s = new SessionStatistics(new Clock()); var v = s.Snapshot(100); Check(!v.HasStarted && !v.TimerRunning && v.InputPairsSent == 0 && v.Elapsed == TimeSpan.Zero && v.MeasuredAverageCps == 0); });
        test("Session average uses sent pairs over monotonic wall-clock elapsed", () =>
        { var c = new Clock(); var s = new SessionStatistics(c); s.Start(100); c.Advance(2.5); var v = s.Snapshot(120); Check(v.Elapsed == TimeSpan.FromSeconds(2.5) && v.InputPairsSent == 20 && v.MeasuredAverageCps == 8); });
        test("Zero-duration measured average remains finite", () =>
        { var s = new SessionStatistics(new Clock()); s.Start(0); Check(s.Snapshot(1).MeasuredAverageCps == 0); });
        test("Stopping freezes elapsed pairs and average; repeated stop is idempotent", () =>
        { var c = new Clock(); var s = new SessionStatistics(c); s.Start(5); c.Advance(2); s.Stop(15); var v = s.Snapshot(15); c.Advance(30); s.Stop(99); Check(s.Snapshot(100) == v && !v.TimerRunning && v.MeasuredAverageCps == 5); });
        test("Starting another session resets counts duration and average", () =>
        { var c = new Clock(); var s = new SessionStatistics(c); s.Start(0); c.Advance(2); s.Stop(10); s.Start(10); Check(s.Snapshot(10).Elapsed == TimeSpan.Zero && s.Snapshot(10).InputPairsSent == 0); c.Advance(1); Check(s.Snapshot(12).MeasuredAverageCps == 2); });
        test("Engine duplicate Start does not reset current session", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider: c); e.Start(); Until(() => e.Clicks > 0); e.TogglePause(); c.Advance(3); var before = e.Session; e.Start(); Check(e.Session == before && e.IsPaused && before.Elapsed.TotalSeconds == 3); });
        test("Pause and resume include paused time without inflating average CPS", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider: c); e.Start(); Until(() => e.Clicks > 0); e.TogglePause(); c.Advance(2); var p = e.Session; c.Advance(3); Check(e.Session.InputPairsSent == p.InputPairsSent && e.Session.MeasuredAverageCps < p.MeasuredAverageCps && e.Session.TimerRunning); long n=e.Clicks; e.TogglePause(); Until(() => e.Clicks > n); e.Stop(); Check(e.Session.Elapsed.TotalSeconds == 5 && e.Session.MeasuredAverageCps == e.Session.InputPairsSent / 5d); });
        test("Target waiting time is included in session wall-clock duration", () =>
        { var c = new Clock(); var g = new Gate { Allowed = false }; using var e = new ClickEngine(new Mouse(), g, g, c); e.ConfigureTiming(ClickTimingMode.Variable, new(50,50)); e.Start(); c.Advance(4); Check(e.Session.InputPairsSent == 0 && e.Session.Elapsed.TotalSeconds == 4 && e.Session.TimerRunning); g.Allowed = true; Until(() => e.Clicks > 0); e.Stop(); Check(e.Session.MeasuredAverageCps == e.Session.InputPairsSent / 4d); });
        test("Emergency stop freezes statistics at the existing stop barrier", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider:c); e.Start(); Until(()=>e.Clicks>0); c.Advance(1); e.Stop(); var frozen=e.Session; c.Advance(20); Check(e.Session == frozen && !e.Enabled); });
        test("Engine new activation resets session independently of run count", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider:c); e.Start(); Until(()=>e.Clicks>0); c.Advance(2); e.Stop(); long before=e.Clicks; e.Start(); e.TogglePause(); Check(e.Session.InputPairsSent == e.Clicks-before && e.Session.Elapsed == TimeSpan.Zero && e.Session.TimerRunning); });
        test("Refused variable activation cannot create or reset a session", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider:c); e.ConfigureTiming(ClickTimingMode.Variable,new(5,20)); bool rejected=false; try{e.Start();}catch(InvalidOperationException){rejected=true;} Check(rejected && !e.Session.HasStarted); });
        test("Timing reconfiguration freezes statistics without automatic restart", () =>
        { var c = new Clock(); using var e = new ClickEngine(new Mouse(), new Gate(), timeProvider:c); e.Start(); Until(()=>e.Clicks>0); c.Advance(2); e.ConfigureTiming(ClickTimingMode.Variable,new(5,20)); var v=e.Session; c.Advance(20); Check(!e.Enabled && !v.TimerRunning && e.Session == v); });
        test("Fixed CPS edits preserve the same active session", () =>
        { var c=new Clock(); using var e=new ClickEngine(new Mouse(),new Gate(),timeProvider:c); e.Start(); Until(()=>e.Clicks>0); e.TogglePause(); c.Advance(3); var before=e.Session; e.Cps=25; e.ConfigureTiming(ClickTimingMode.Fixed,new(5,20)); Check(e.Enabled && e.IsPaused && e.Session==before); });
        test("Session stop waits for an in-flight pair and freezes its completed count", () =>
        {
            var c=new Clock(); using var mouse=new BlockingMouse(); using var e=new ClickEngine(mouse,new Gate(),timeProvider:c);
            using var stopping=new ManualResetEventSlim(); e.Start(); Check(mouse.Entered.Wait(2000));
            var stop=Task.Run(()=>{stopping.Set();e.Stop();});
            try { Check(stopping.Wait(2000) && !stop.Wait(50)); c.Advance(2); }
            finally { mouse.Continue.Set(); }
            Check(stop.Wait(2000)); var frozen=e.Session; c.Advance(10); Check(frozen.InputPairsSent==1 && frozen.Elapsed.TotalSeconds==2 && !frozen.TimerRunning && e.Session==frozen);
        });
        test("Hold activation loss freezes the session", () =>
        { var c = new Clock(); bool held = true; using var e=new ClickEngine(new Mouse(),new Gate(),timeProvider:c){ActivationStillValid=()=>Volatile.Read(ref held)}; e.Start(); Until(()=>e.Clicks>0); c.Advance(3); held=false; Until(()=>!e.Enabled); Check(!e.Session.TimerRunning && e.Session.Elapsed.TotalSeconds==3); });
        test("Input failure is not counted and freezes session time", () =>
        { var c=new Clock(); using var e=new ClickEngine(new FailingMouse(c),new Gate(),timeProvider:c); e.Start(); Until(()=>e.State==EngineState.Error); var v=e.Session; c.Advance(20); Check(v.InputPairsSent==0 && v.Elapsed.TotalSeconds==1 && !v.TimerRunning && e.Session==v); });
        test("Disposal freezes the session and cannot resume timing", () =>
        { var c=new Clock(); var e=new ClickEngine(new Mouse(),new Gate(),timeProvider:c); e.Start(); Until(()=>e.Clicks>0); c.Advance(2); e.Dispose(); var v=e.Session; c.Advance(10); e.Dispose(); Check(e.Session==v && !v.TimerRunning); });

        var ready=new SelectedTargetFacts{HasSelection=true,ProcessAlive=true,WindowExists=true,IdentityMatches=true,WindowVisible=true,Foreground=true,PointerKnown=true,PointerInside=true};
        test("Target explanations cover each actual blocking condition", () =>
        {
            (SelectedTargetFacts Facts,TargetBlockReason Reason)[] cases = [
                (ready with {HasSelection=false},TargetBlockReason.NoSelection),
                (ready with {InspectionSucceeded=false},TargetBlockReason.InspectionUnavailable),
                (ready with {ProcessAlive=false},TargetBlockReason.ProcessClosed),
                (ready with {WindowExists=false},TargetBlockReason.WindowClosed),
                (ready with {IdentityMatches=false},TargetBlockReason.WindowChanged),
                (ready with {WindowVisible=false},TargetBlockReason.WindowHidden),
                (ready with {Minimized=true},TargetBlockReason.WindowMinimized),
                (ready with {RequiresRobloxVerification=true},TargetBlockReason.UnverifiedRoblox),
                (ready with {Foreground=false},TargetBlockReason.NotForeground),
                (ready with {PointerKnown=false},TargetBlockReason.PointerUnavailable),
                (ready with {PointerInside=false},TargetBlockReason.PointerOutside)];
            foreach(var (facts,reason) in cases){var status=TargetStatus.Selected(facts); Check(status.Reason==reason && !status.ChecksPassed && status.Label.Length>0 && status.Explanation.Length>0);}
        });
        test("Verified Roblox remains eligible only with all selected-window checks", () =>
        { var f=ready with{RequiresRobloxVerification=true,VerifiedRoblox=true}; Check(TargetStatus.Selected(f).ChecksPassed); foreach(var invalid in new[]{f with{Foreground=false},f with{PointerInside=false},f with{IdentityMatches=false},f with{ProcessAlive=false}})Check(!TargetStatus.Selected(invalid).ChecksPassed); });
        test("Target explanations prioritize identity failures over focus and pointer", () =>
        { Check(TargetStatus.Selected(ready with{ProcessAlive=false,IdentityMatches=false,Foreground=false,PointerInside=false}).Reason==TargetBlockReason.ProcessClosed); Check(TargetStatus.Selected(ready with{IdentityMatches=false,Foreground=false}).Reason==TargetBlockReason.WindowChanged); });
        test("Window metadata requires retained live process and matching window identity", () =>
        { Check(ready.WindowIdentityValid); foreach(var invalid in new[]{ready with{HasSelection=false},ready with{InspectionSucceeded=false},ready with{ProcessAlive=false},ready with{WindowExists=false},ready with{IdentityMatches=false}})Check(!invalid.WindowIdentityValid); Check((ready with{RequiresRobloxVerification=true,Foreground=false,PointerInside=false}).WindowIdentityValid); });
        test("Fixed target messages preserve restriction and verification policy", () =>
        { Check(TargetStatus.Fixed(false,false,false,false,false).Reason==TargetBlockReason.RestrictionOff); Check(!TargetStatus.Fixed(false,true,false,false,false).ChecksPassed); Check(TargetStatus.Fixed(true,false,false,false,true).Reason==TargetBlockReason.NoForeground); Check(TargetStatus.Fixed(true,true,true,false,true).Reason==TargetBlockReason.WindowMinimized); Check(TargetStatus.Fixed(true,true,false,true,false).Reason==TargetBlockReason.CheckingRoblox); Check(TargetStatus.Fixed(true,true,false,false,false).Reason==TargetBlockReason.RobloxVerificationRequired); Check(TargetStatus.Fixed(true,true,false,false,true).ChecksPassed); });
        test("All status reasons have useful content-free labels and explanations", () =>
        { foreach(var reason in Enum.GetValues<TargetBlockReason>()){var v=new TargetStatus(reason);Check(!string.IsNullOrWhiteSpace(v.Label)&&!string.IsNullOrWhiteSpace(v.Explanation));} });
        test("Mock target state explanations agree with variable-engine gating", () =>
        { var g=new FactsGate(ready); using var e=new ClickEngine(new Mouse(),new Gate(),g); e.ConfigureTiming(ClickTimingMode.Variable,new(50,50)); e.Start(); Until(()=>e.Clicks>0); g.Facts=ready with{PointerInside=false}; Until(()=>e.State==EngineState.Paused); long count=e.Clicks; Thread.Sleep(70); Check(e.Clicks==count && g.Status.Reason==TargetBlockReason.PointerOutside); g.Facts=ready; Until(()=>e.Clicks>count); });
    }
    private sealed class Clock : TimeProvider { private long ticks; public override long TimestampFrequency=>1000; public override long GetTimestamp()=>Interlocked.Read(ref ticks); public void Advance(double seconds)=>Interlocked.Add(ref ticks,(long)(seconds*1000)); }
    private sealed class Mouse : IMouseInput { public void Click(){} public void Release(){} }
    private sealed class BlockingMouse : IMouseInput, IDisposable { public readonly ManualResetEventSlim Entered=new(),Continue=new(); public void Click(){Entered.Set();Continue.Wait();} public void Release(){} public void Dispose(){Entered.Dispose();Continue.Dispose();} }
    private sealed class FailingMouse(Clock c) : IMouseInput { public void Click(){c.Advance(1);throw new InvalidOperationException("Mock input failed");} public void Release(){} }
    private sealed class Gate : IVariableTargetGate { public volatile bool Allowed=true; public bool CanActivate=>true; public bool CanClick=>Allowed; }
    private sealed class FactsGate(SelectedTargetFacts initial) : IVariableTargetGate { public SelectedTargetFacts Facts=initial; public TargetStatus Status=>TargetStatus.Selected(Volatile.Read(ref Facts)); public bool CanActivate=>true; public bool CanClick=>Status.ChecksPassed; }
}
