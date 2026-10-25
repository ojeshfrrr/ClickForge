using System.Text.Json;
using ClickForge.Core;

internal static class CustomKeyTests
{
    internal static void Run(Action<string,Action> test)
    {
        void Check(bool ok){if(!ok)throw new Exception("Custom key assertion failed");}
        void Reject(Action action){try{action();}catch(Exception e)when(e is ArgumentException or InvalidOperationException){return;}throw new Exception("Expected key rejection");}
        void Until(Func<bool> condition){if(!SpinWait.SpinUntil(condition,2000))throw new Exception("Custom key test timed out");}
        test("Custom keyboard validation and extended down/up flags",()=>
        {foreach(int k in new[]{8,9,13,27,32,37,65,90,48,96,111,112,135,186})Check(KeyboardOutput.IsValid(k));foreach(int k in new[]{0,1,5,6,16,17,18,20,91,92,144,999})Reject(()=>KeyboardOutput.Validate(k));Check(KeyboardOutput.Flags(65,false)==0&&KeyboardOutput.Flags(65,true)==2&&KeyboardOutput.Flags(37,false)==1&&KeyboardOutput.Flags(37,true)==3&&KeyboardOutput.Flags(111,true)==3);});
        test("Custom output refuses activation pause and emergency key conflicts",()=>
        {foreach(int key in new[]{0x75,0x76,0x77})Reject(()=>new Settings{Button=ClickButton.CustomKey,CustomKey=key}.Validate());new Settings{Button=ClickButton.CustomKey,CustomKey=69}.Validate();Reject(()=>new Settings{Button=ClickButton.CustomKey}.Validate());});
        test("Keyboard setting persists and missing fields retain Left defaults",()=>Files(root=>
        {var path=Path.Combine(root,"settings.json");var s=new SettingsService(path);s.Save(new(){Button=ClickButton.CustomKey,CustomKey=69});Check(s.Load().CustomKey==69&&s.Load().Button==ClickButton.CustomKey);File.WriteAllText(path,"{}");Check(s.Load().Button==ClickButton.Left&&s.Load().CustomKey==0);File.WriteAllText(path,"{\"Button\":3,\"CustomKey\":17}");Check(s.Load().Button==ClickButton.Left&&s.Warning is not null);}));
        test("Custom profiles export import apply and match without activation",()=>Files(root=>
        {var store=new ProfileStore(Path.Combine(root,"profiles"));var p=store.Create(new(){Button=ClickButton.CustomKey,CustomKey=69},"Keyboard E");string file=Path.Combine(root,"export.json");ProfileStore.Export(file,p);var copy=store.Import(file);var t=new Transport();using var e=new ClickEngine(new ButtonMouseInput(t),new Gate());var next=copy.Apply(new(),e);Check(next.CustomKey==69&&e.CustomKey==69&&e.Button==ClickButton.CustomKey&&!e.Enabled&&copy.Matches(next)&&!copy.Matches(next with{CustomKey=70})&&copy.Summary.Contains("Key: E"));}));
        test("Custom profile conflicts are rejected before stopping or mutating engine",()=>
        {using var e=new ClickEngine(new ButtonMouseInput(new Transport()),new Gate());e.Start();Until(()=>e.Clicks>0);Reject(()=>(new TimingProfile{Button=ClickButton.CustomKey,CustomKey=0x77}).Apply(new(),e));Check(e.Enabled&&e.Button==ClickButton.Left);});
        test("Custom key uses existing worker without any mouse fallback",()=>
        {var t=new Transport();using var e=new ClickEngine(new ButtonMouseInput(t),new Gate());e.ConfigureOutput(ClickButton.CustomKey,69);e.Start();Until(()=>e.Clicks>0);e.TogglePause();long n=e.Clicks;Thread.Sleep(60);Check(e.Clicks==n);e.TogglePause();Until(()=>e.Clicks>n);e.Stop();Check(t.Keys.All(v=>v.Key==69)&&t.MouseCalls==0&&!e.Session.TimerRunning);});
        test("Unsupported keyboard transport cannot start or fallback to mouse",()=>
        {var t=new Transport{Supported=false};using var e=new ClickEngine(new ButtonMouseInput(t),new Gate());e.ConfigureOutput(ClickButton.CustomKey,69);Reject(e.Start);Check(!e.Enabled&&!e.Session.HasStarted&&t.MouseCalls==0&&t.Keys.Count==0);});
        test("Partial keyboard submission releases the original key and is not counted",()=>
        {var t=new Transport{Pair=1};using var e=new ClickEngine(new ButtonMouseInput(t),new Gate());e.ConfigureOutput(ClickButton.CustomKey,69);e.Start();Until(()=>e.State==EngineState.Error);Check(e.Clicks==0&&t.Keys.Contains((69,true))&&t.MouseCalls==0&&!e.Session.TimerRunning);});
        test("Failed key release prevents new keyboard or mouse down until cleanup succeeds",()=>
        {var t=new Transport{Pair=1,Up=0};var m=new ButtonMouseInput(t);Reject(()=>m.Click(ClickButton.CustomKey,69));Reject(()=>m.Click(ClickButton.Mouse4));Reject(()=>m.Click(ClickButton.CustomKey,70));Check(t.MouseCalls==0&&t.Keys.All(v=>v.Key==69));t.Up=1;m.Release();t.Pair=2;m.Click(ClickButton.CustomKey,70);Check(t.Keys.Last()==(70,false));});
        test("Output reconfiguration stops the session and validates before mutation",()=>
        {var t=new Transport();using var e=new ClickEngine(new ButtonMouseInput(t),new Gate());e.ConfigureOutput(ClickButton.CustomKey,69);e.Start();Until(()=>e.Clicks>0);Reject(()=>e.ConfigureOutput(ClickButton.CustomKey,17));Check(e.Enabled&&e.CustomKey==69);e.ConfigureOutput(ClickButton.CustomKey,70);Check(!e.Enabled&&e.CustomKey==70);long n=e.Clicks;Thread.Sleep(50);Check(e.Clicks==n);});
        test("Custom output preserves target gating",()=>
        {var t=new Transport();var g=new Gate{Allowed=false};using var e=new ClickEngine(new ButtonMouseInput(t),g);e.ConfigureOutput(ClickButton.CustomKey,69);e.Start();Thread.Sleep(70);Check(e.Clicks==0&&t.Keys.Count==0);g.Allowed=true;Until(()=>e.Clicks>0);e.Stop();});
        test("Stop and disposal clean up an in-flight custom key failure",()=>
        {foreach(bool dispose in new[]{false,true}){using var t=new Blocking();var e=new ClickEngine(new ButtonMouseInput(t),new Gate());try{e.ConfigureOutput(ClickButton.CustomKey,69);e.Start();Check(t.Entered.Wait(2000));var stop=Task.Run(()=>{if(dispose)e.Dispose();else e.Stop();});try{Check(!stop.Wait(40));}finally{t.Continue.Set();}Check(stop.Wait(2000)&&t.Keys.Contains((69,true))&&e.Clicks==0&&!e.Enabled);}finally{t.Continue.Set();e.Dispose();}}});
    }
    private static void Files(Action<string> action){string p=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","custom-key-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);try{action(p);}finally{Directory.Delete(p,true);}}
    private sealed class Gate:ITargetGate{public volatile bool Allowed=true;public bool CanClick=>Allowed;}
    private class Transport:IMouseTransport
    {public readonly List<(int Key,bool Up)> Keys=[];public int MouseCalls;public bool Supported=true;public uint Pair=2,Up=1;public int LastError=>5;public bool Supports(ClickButton b)=>b!=ClickButton.CustomKey;public bool SupportsKey(int key)=>Supported&&KeyboardOutput.IsValid(key);public uint Send(ClickButton b,bool up){MouseCalls++;return up?1u:2u;}public virtual uint SendKey(int key,bool up){Keys.Add((key,up));return up?Up:Pair;}}
    private sealed class Blocking:Transport,IDisposable
    {public readonly ManualResetEventSlim Entered=new(),Continue=new();public override uint SendKey(int key,bool up){if(!up){Entered.Set();Continue.Wait();Pair=1;}return base.SendKey(key,up);}public void Dispose(){Entered.Dispose();Continue.Dispose();}}
}
