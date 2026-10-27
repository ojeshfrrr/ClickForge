using System.Collections.Concurrent;
using ClickForge.Core;

internal static class RightClickTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Right-click assertion failed"); }
        void Until(Func<bool> value) { if (!SpinWait.SpinUntil(value, 2000)) throw new Exception("Right-click timeout"); }
        test("Right packet mapping preserves all existing serialized output values", () =>
        {
            Check((int)ClickButton.Left == 0 && (int)ClickButton.Mouse4 == 1 && (int)ClickButton.Mouse5 == 2 && (int)ClickButton.CustomKey == 3 && (int)ClickButton.Right == 4);
            Check(ClickButtons.Packet(ClickButton.Right, false) == new MousePacket(8, 0) && ClickButtons.Packet(ClickButton.Right, true) == new MousePacket(16, 0));
            Check(ClickButtons.Label(ClickButton.Right) == "Right" && OutputSelection.TypeOf(ClickButton.Right) == OutputInputType.Mouse);
        });
        test("Right uses the same fixed and variable worker pause resume and emergency paths", () =>
        {
            foreach (var mode in Enum.GetValues<ClickTimingMode>())
            {
                var input = new Transport(); using var engine = new ClickEngine(new ButtonMouseInput(input), new Gate(), new Gate()) { Cps = 50 };
                engine.ConfigureTiming(mode, new(50, 50)); engine.ConfigureOutput(ClickButton.Right, 0); engine.Start(); Until(() => engine.Clicks > 0);
                engine.TogglePause(); long count = engine.Clicks; Thread.Sleep(45); Check(count == engine.Clicks);
                engine.TogglePause(); Until(() => engine.Clicks > count); engine.EmergencyStop();
                Check(!engine.Enabled && input.Events.All(e => e.Button == ClickButton.Right));
            }
        });
        test("Unsupported Right input is refused without substituting Left", () =>
        {
            var input = new Transport { Supported = false }; using var engine = new ClickEngine(new ButtonMouseInput(input), new Gate()) { Button = ClickButton.Right };
            bool rejected = false; try { engine.Start(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !engine.Enabled && input.Events.IsEmpty && !engine.Session.HasStarted);
        });
        test("Partial Right input releases Right through pause stop emergency and shutdown barriers", () =>
        {
            foreach (string command in new[] { "pause", "stop", "emergency", "shutdown" })
            {
                using var input = new BlockingTransport(); using var engine = new ClickEngine(new ButtonMouseInput(input), new Gate()) { Button = ClickButton.Right };
                engine.Start(); Check(input.Entered.Wait(2000));
                var finish = Task.Run(() => { if (command == "pause") engine.TogglePause(); else if (command == "emergency") engine.EmergencyStop(); else if (command == "shutdown") engine.Dispose(); else engine.Stop(); });
                try { Check(!finish.Wait(25)); } finally { input.Continue.Set(); }
                Check(finish.Wait(2000) && !engine.Enabled && engine.Clicks == 0 && input.Events.Contains((ClickButton.Right, true)) && input.Events.All(e => e.Button == ClickButton.Right));
            }
        });
        test("Right settings and profiles round-trip while legacy keyboard data remains keyboard", () =>
        {
            string root = Path.Combine("artifacts", "right-tests-" + Guid.NewGuid()); Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "settings.json"); var settings = new SettingsService(path); settings.Save(new() { Button = ClickButton.Right });
                Check(settings.Load().Button == ClickButton.Right);
                File.WriteAllText(path, "{\"Button\":3,\"CustomKey\":69}"); Check(settings.Load().Button == ClickButton.CustomKey && settings.Load().CustomKey == 69);
                var profiles = new ProfileStore(Path.Combine(root, "profiles")); var profile = profiles.Create(new() { Button = ClickButton.Right }, "Right");
                string export = Path.Combine(root, "export.json"); ProfileStore.Export(export, profile);
                using var engine = new ClickEngine(new ButtonMouseInput(new Transport()), new Gate());
                Check(profiles.Import(export).Apply(new(), engine).Button == ClickButton.Right && engine.Button == ClickButton.Right && !engine.Enabled);
            }
            finally { Directory.Delete(root, true); }
        });
    }
    private sealed class Gate : IVariableTargetGate { public bool CanActivate => true; public bool CanClick => true; }
    private class Transport : IMouseTransport
    {
        public readonly ConcurrentQueue<(ClickButton Button, bool Up)> Events = new(); public bool Supported = true;
        public bool Supports(ClickButton button) => Supported && ClickButtons.MouseChoices.Contains(button);
        public int LastError => 5;
        public virtual uint Send(ClickButton button, bool releaseOnly) { Events.Enqueue((button, releaseOnly)); return releaseOnly ? 1u : 2u; }
    }
    private sealed class BlockingTransport : Transport, IDisposable
    {
        public readonly ManualResetEventSlim Entered = new(), Continue = new();
        public override uint Send(ClickButton button, bool releaseOnly)
        {
            uint result = base.Send(button, releaseOnly);
            if (releaseOnly) return result;
            Entered.Set(); if (!Continue.Wait(2000)) throw new TimeoutException("Right test release timeout"); return 1;
        }
        public void Dispose() { Entered.Dispose(); Continue.Dispose(); }
    }
}
