using ClickForge.Core;

internal static class MouseShortcutTests
{
    internal static void Run(Action<string,Action> test)
    {
        void Check(bool ok) { if (!ok) throw new Exception("Mouse shortcut assertion failed"); }
        void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Expected invalid shortcut"); }
        test("Shortcut choices preserve keyboard ordering and append both side buttons",()=>
        { Check(ShortcutKeys.Choices.Count==13 && ShortcutKeys.Choices.Take(11).SequenceEqual(Enumerable.Range(0x70,11)) && ShortcutKeys.Choices[11]==5 && ShortcutKeys.Choices[12]==6); Check(ShortcutKeys.Label(5)=="Mouse 4 (Back)"&&ShortcutKeys.Label(6)=="Mouse 5 (Forward)"&&ShortcutKeys.Label(0x75)=="F6"); });
        test("Mouse shortcut values validate distinctly and invalid keys remain rejected",()=>
        { new Settings{ActivateKey=5,PauseKey=6}.Validate(); new Settings{StopKey=5}.Validate(); Reject(()=>new Settings{ActivateKey=5,PauseKey=5}.Validate()); foreach(int invalid in new[]{0,1,2,4,7,0x7B})Reject(()=>new Settings{ActivateKey=invalid}.Validate()); });
        test("Physical side-button downs route once and physical ups clear Hold",()=>
        { var r=new MouseShortcutRouter(); Check(r.Register(1,5)&&r.Register(2,6)); Check(r.Process(0x20B,1u<<16,0)==1&&r.IsHeld(5)); Check(r.Process(0x20B,1u<<16,0) is null); Check(r.Process(0x20C,1u<<16,0) is null&&!r.IsHeld(5)); Check(r.Process(0x20B,1u<<16,0)==1); Check(r.Process(0x20B,2u<<16,0)==2&&r.IsHeld(6)); });
        test("Injected and lower-integrity mouse events cannot trigger or release shortcuts",()=>
        { var r=new MouseShortcutRouter();r.Register(1,5);foreach(uint flags in new uint[]{1,2,3})Check(r.Process(0x20B,1u<<16,flags)is null&&!r.IsHeld(5));r.Process(0x20B,1u<<16,0);foreach(uint flags in new uint[]{1,2,3})Check(r.Process(0x20C,1u<<16,flags)is null&&r.IsHeld(5));r.Process(0x20C,1u<<16,0);Check(!r.IsHeld(5)); });
        test("Unrelated mouse events and unbound side buttons do not activate",()=>
        { var r=new MouseShortcutRouter(); r.Register(1,5); foreach(int message in new[]{0x200,0x201,0x202,0x20A})Check(r.Process(message,1u<<16,0)is null); Check(r.Process(0x20B,2u<<16,0)is null&&r.Process(0x20B,3u<<16,0)is null); });
        test("Router replacement clears held state and rejects duplicate bindings",()=>
        { var r=new MouseShortcutRouter();Check(!r.Register(0,5)&&!r.Register(1,0x75)&&r.Register(1,5)&&!r.Register(2,5)&&!r.Register(1,6));r.Process(0x20B,1u<<16,0);Check(r.Unregister(1)&&!r.Contains(1)&&r.Count==0&&!r.IsHeld(5));Check(r.Process(0x20B,1u<<16,0)is null&&r.Register(1,6)&&r.Process(0x20B,2u<<16,0)==1); });
        test("Mouse Hold stops shared engine when the physical shortcut is released",()=>
        { var r=new MouseShortcutRouter();r.Register(1,5);r.Process(0x20B,1u<<16,0);var m=new Mouse();using var e=new ClickEngine(m,new Gate()){ActivationStillValid=()=>r.IsHeld(5)};e.Start();Check(SpinWait.SpinUntil(()=>e.Clicks>0,2000));r.Process(0x20C,1u<<16,0);Check(SpinWait.SpinUntil(()=>!e.Enabled,2000));Check(!e.Session.TimerRunning); });
        test("Mouse activation and emergency routes use the same existing engine",()=>
        { var r=new MouseShortcutRouter();r.Register(1,5);r.Register(3,6);using var e=new ClickEngine(new Mouse(),new Gate());void Route(int? id){if(id==1)e.Start();if(id==3)e.Stop();}Route(r.Process(0x20B,1u<<16,0));Check(e.Enabled);Route(r.Process(0x20B,2u<<16,1));Check(e.Enabled);Route(r.Process(0x20B,2u<<16,0));Check(!e.Enabled); });
        test("Mouse hotkeys persist and keyboard defaults remain unchanged",()=>
        { string path=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","mouse-shortcuts-"+Guid.NewGuid().ToString("N")+".json");try{var store=new SettingsService(path);store.Save(new(){ActivateKey=5,PauseKey=6});var loaded=store.Load();Check(loaded.ActivateKey==5&&loaded.PauseKey==6&&loaded.StopKey==0x77);store.Save(new());loaded=store.Load();Check(loaded.ActivateKey==0x75&&loaded.PauseKey==0x76&&loaded.StopKey==0x77);}finally{if(File.Exists(path))File.Delete(path);} });
        test("Mouse registration failure rolls back all reserved shortcuts",()=>
        { var registrar=new Registrar();using var b=new HotkeyBindings(registrar);bool refused=false;try{b.Apply(new(){ActivateKey=5});}catch(InvalidOperationException e){refused=e.Message.Contains("Mouse 4 (Back)");}Check(refused&&registrar.Active.Count==0); });
        test("Hold rejects output on its own activation button but Toggle remains eligible",()=>
        { var s=new Settings{Mode=ActivationMode.Hold,ActivateKey=5,Button=ClickButton.Mouse4};Check(ShortcutKeys.ConflictsWithHoldOutput(s)&&!ShortcutKeys.ConflictsWithHoldOutput(s with{Mode=ActivationMode.Toggle})&&!ShortcutKeys.ConflictsWithHoldOutput(s with{Button=ClickButton.Left}));Check(ShortcutKeys.ConflictsWithHoldOutput(s with{ActivateKey=6,Button=ClickButton.Mouse5})); });
    }
    private sealed class Gate:ITargetGate{public bool CanClick=>true;}
    private sealed class Mouse:IMouseInput{public void Click(){}public void Release(){}}
    private sealed class Registrar:IHotkeyRegistrar{public readonly HashSet<int> Active=[];public bool Register(int id,int key){if(ShortcutKeys.IsMouse(key))return false;return Active.Add(id);}public void Unregister(int id)=>Active.Remove(id);}
}
