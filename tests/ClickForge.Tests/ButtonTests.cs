using System.Text.Json;
using ClickForge.Core;

internal static class ButtonTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool condition) { if (!condition) throw new Exception("Mouse-button assertion failed"); }
        void Until(Func<bool> condition) { if (!SpinWait.SpinUntil(condition, 2000)) throw new Exception("Mouse-button test timed out"); }
        void Reject(Action action) { try { action(); } catch (Exception e) when(e is ArgumentException or InvalidOperationException or JsonException) { return; } throw new Exception("Expected button rejection"); }
        test("Win32 button flags and data map Left and both X buttons exactly", () =>
        {
            Check(ClickButtons.Packet(ClickButton.Left,false)==new MousePacket(2,0) && ClickButtons.Packet(ClickButton.Left,true)==new MousePacket(4,0));
            Check(ClickButtons.Packet(ClickButton.Mouse4,false)==new MousePacket(0x80,1) && ClickButtons.Packet(ClickButton.Mouse4,true)==new MousePacket(0x100,1));
            Check(ClickButtons.Packet(ClickButton.Mouse5,false)==new MousePacket(0x80,2) && ClickButtons.Packet(ClickButton.Mouse5,true)==new MousePacket(0x100,2));
            Check(ClickButtons.Label(ClickButton.Left)=="Left" && ClickButtons.Label(ClickButton.Mouse4)=="Mouse 4 (Back)" && ClickButtons.Label(ClickButton.Mouse5)=="Mouse 5 (Forward)");
            Reject(()=>ClickButtons.Packet((ClickButton)9,false));
        });
        test("Legacy input services support Left and refuse side buttons without fallback", () =>
        { var legacy=new Legacy(); using var e=new ClickEngine(legacy,new Gate()){Button=ClickButton.Mouse4}; Reject(e.Start); Check(!e.Enabled && !e.Session.HasStarted && legacy.Clicks==0); e.Button=ClickButton.Left; e.Start(); Until(()=>e.Clicks>0); });
        test("Engine routes the selected button through its existing worker", () =>
        { foreach(var button in ClickButtons.MouseChoices){var t=new Transport(); using var e=new ClickEngine(new ButtonMouseInput(t),new Gate()){Button=button}; e.Start(); Until(()=>e.Clicks>0); e.Stop(); Check(t.Events.All(v=>v.Button==button) && t.Events.Any(v=>!v.Up));} });
        test("Unsupported button transport produces no input and no automatic fallback", () =>
        { var t=new Transport{Supported=false}; var mouse=new ButtonMouseInput(t); Reject(()=>mouse.Click(ClickButton.Mouse5)); Check(t.Events.Count==0); using var e=new ClickEngine(mouse,new Gate()){Button=ClickButton.Mouse5}; Reject(e.Start); Check(!e.Enabled && t.Events.Count==0); });
        test("A legacy adapter cannot claim side-button support and silently send Left", () =>
        { var mouse=new ClaimingLegacy(); using var e=new ClickEngine(mouse,new Gate()){Button=ClickButton.Mouse4}; e.Start(); Until(()=>e.State==EngineState.Error); Check(mouse.Clicks==0 && e.Clicks==0 && e.Error!.Contains("not supported")); });
        test("Partial X input releases the original button and is not counted", () =>
        { foreach(var button in new[]{ClickButton.Mouse4,ClickButton.Mouse5}){var t=new Transport{PairResult=1}; using var e=new ClickEngine(new ButtonMouseInput(t),new Gate()){Button=button}; e.Start(); Until(()=>e.State==EngineState.Error); Check(e.Clicks==0 && !e.Session.TimerRunning && t.Events.Any(v=>v.Up&&v.Button==button) && t.Events.All(v=>v.Button==button));} });
        test("Zero accepted events report failure rather than sending Left", () =>
        { var t=new Transport{PairResult=0}; var m=new ButtonMouseInput(t); Reject(()=>m.Click(ClickButton.Mouse4)); Check(t.Events.All(v=>v.Button==ClickButton.Mouse4)); });
        test("A failed release stays pending and prevents a different button down", () =>
        { var t=new Transport{PairResult=1,ReleaseResult=0}; var m=new ButtonMouseInput(t); Reject(()=>m.Click(ClickButton.Mouse4)); Reject(()=>m.Click(ClickButton.Mouse5)); Check(t.Events.All(v=>v.Button==ClickButton.Mouse4)); t.ReleaseResult=1; m.Release(); t.PairResult=2; m.Click(ClickButton.Mouse5); Check(t.Events.Last()==(ClickButton.Mouse5,false)); });
        test("Transport exceptions preserve button identity for cleanup", () =>
        { var t=new Transport{ThrowPair=true}; using var e=new ClickEngine(new ButtonMouseInput(t),new Gate()){Button=ClickButton.Mouse5}; e.Start(); Until(()=>e.State==EngineState.Error); Check(e.Clicks==0 && t.Events.Any(v=>v.Up && v.Button==ClickButton.Mouse5)); });
        test("Button changes stop the old session without starting a competing worker", () =>
        { var t=new Transport(); using var e=new ClickEngine(new ButtonMouseInput(t),new Gate()){Button=ClickButton.Mouse4}; e.Start(); Until(()=>e.Clicks>0); e.Button=ClickButton.Mouse5; Check(!e.Enabled && !e.Session.TimerRunning); long old=e.Clicks; Thread.Sleep(50); Check(e.Clicks==old); e.Start(); Until(()=>e.Clicks>old); e.Stop(); Check(t.Events.Last().Button==ClickButton.Mouse5); Reject(()=>e.Button=(ClickButton)99); Check(e.Button==ClickButton.Mouse5); });
        test("Stop pause and shutdown release a partial side-button pair across cancellation", () =>
        {
            foreach(var end in new[]{"stop","pause","dispose"})
            {
                using var t=new BlockingTransport(); var e=new ClickEngine(new ButtonMouseInput(t),new Gate()){Button=ClickButton.Mouse5};
                try
                {
                    e.Start(); Check(t.Entered.Wait(2000));
                    var stopping=Task.Run(()=>{if(end=="pause")e.TogglePause();else if(end=="dispose")e.Dispose();else e.Stop();});
                    try { Check(!stopping.Wait(40)); } finally { t.Continue.Set(); }
                    Check(stopping.Wait(2000)); Check(!e.Enabled && e.State==EngineState.Error && t.Events.Any(v=>v.Up && v.Button==ClickButton.Mouse5) && t.Events.All(v=>v.Button==ClickButton.Mouse5));
                }
                finally { t.Continue.Set(); e.Dispose(); }
            }
        });
        test("Button settings roundtrip and older files retain Left", () => WithFiles(root=>
        { var store=new SettingsService(Path.Combine(root,"settings.json")); foreach(var button in ClickButtons.MouseChoices){store.Save(new(){Button=button}); Check(store.Load().Button==button);} File.WriteAllText(Path.Combine(root,"settings.json"),"{}"); Check(store.Load().Button==ClickButton.Left); }));
        test("Invalid loaded buttons restore safe defaults with a warning", () => WithFiles(root=>
        { var path=Path.Combine(root,"settings.json"); var store=new SettingsService(path); foreach(var raw in new[]{"99","-1","null","\"Mouse4\""}){File.WriteAllText(path,"{\"Button\":"+raw+"}"); Check(store.Load().Button==ClickButton.Left && store.Warning is not null);} Reject(()=>store.Save(new(){Button=(ClickButton)5})); }));
        test("Profiles persist export import match and apply the selected button stopped", () => WithFiles(root=>
        { var store=new ProfileStore(Path.Combine(root,"profiles")); var p=store.Create(new(){Button=ClickButton.Mouse5},"Side button"); Check(store.Load().Single().Button==ClickButton.Mouse5 && p.Summary.Contains("Mouse 5 (Forward)")); string path=Path.Combine(root,"export.json"); ProfileStore.Export(path,p); var copy=store.Import(path); Check(copy.Button==p.Button); using var e=new ClickEngine(new ButtonMouseInput(new Transport()),new Gate()); var next=copy.Apply(new(),e); Check(next.Button==ClickButton.Mouse5 && e.Button==next.Button && !e.Enabled && copy.Matches(next) && !copy.Matches(next with{Button=ClickButton.Left})); }));
        test("Older profiles default Left while invalid buttons are rejected before mutation", () => WithFiles(root=>
        { var p=new TimingProfile(); var path=Path.Combine(root,"profile.json"); var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(p))!; node.AsObject().Remove("Button"); File.WriteAllText(path,node.ToJsonString()); Check(ProfileStore.Read(path).Button==ClickButton.Left); node["Button"]=99; File.WriteAllText(path,node.ToJsonString()); Reject(()=>ProfileStore.Read(path)); using var e=new ClickEngine(new Legacy(),new Gate()); e.Start(); Reject(()=>(p with{Button=(ClickButton)99}).Apply(new(),e)); Check(e.Enabled && e.Button==ClickButton.Left); }));
    }
    private static void WithFiles(Action<string> action) { string root=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","button-tests",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); try{action(root);}finally{Directory.Delete(root,true);} }
    private sealed class Gate:ITargetGate { public bool CanClick=>true; }
    private sealed class Legacy:IMouseInput { public int Clicks; public void Click()=>Interlocked.Increment(ref Clicks); public void Release(){} }
    private sealed class ClaimingLegacy:IMouseInput { public int Clicks; public bool Supports(ClickButton button)=>true; public void Click()=>Interlocked.Increment(ref Clicks); public void Release(){} }
    private class Transport:IMouseTransport
    {
        public readonly List<(ClickButton Button,bool Up)> Events=[]; public bool Supported=true,ThrowPair; public uint PairResult=2,ReleaseResult=1; public int LastError=>5;
        public bool Supports(ClickButton button)=>Supported;
        public virtual uint Send(ClickButton button,bool releaseOnly){Events.Add((button,releaseOnly)); if(!releaseOnly&&ThrowPair)throw new InvalidOperationException("Mock input unavailable"); return releaseOnly?ReleaseResult:PairResult;}
    }
    private sealed class BlockingTransport:Transport,IDisposable
    { public readonly ManualResetEventSlim Entered=new(),Continue=new(); public override uint Send(ClickButton button,bool releaseOnly){if(!releaseOnly){Entered.Set();Continue.Wait();PairResult=1;}return base.Send(button,releaseOnly);}public void Dispose(){Entered.Dispose();Continue.Dispose();} }
}
