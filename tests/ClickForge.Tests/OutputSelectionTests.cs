using ClickForge.Core;
using System.Collections.Concurrent;

internal static class OutputSelectionTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool condition) { if (!condition) throw new Exception("Output selection assertion failed"); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected output rejection"); }
        void Until(Func<bool> condition) { if (!SpinWait.SpinUntil(condition, 2000)) throw new Exception("Output selection timed out"); }
        test("Output type classification maps only CustomKey to keyboard", () =>
        {
            foreach (var button in ClickButtons.MouseChoices) Check(OutputSelection.TypeOf(button) == OutputInputType.Mouse);
            Check(OutputSelection.TypeOf(ClickButton.CustomKey) == OutputInputType.Keyboard && OutputSelection.TypeOf(new Settings().Button) == OutputInputType.Mouse);
            Reject(() => OutputSelection.TypeOf((ClickButton)99));
        });
        test("Output type transitions validate a key without altering shortcuts or target policy", () =>
        {
            var current = new Settings { CustomKey = 69, Button = ClickButton.Mouse5, MaximumSessionPairs = 20 };
            var next = OutputSelection.ChangeType(current, OutputInputType.Keyboard);
            Check(next.Button == ClickButton.CustomKey && next.CustomKey == 69 && next.ActivateKey == current.ActivateKey && next.StopKey == current.StopKey && next.RobloxOnly == current.RobloxOnly && next.MaximumSessionPairs == 20);
            Check(OutputSelection.ChangeType(next, OutputInputType.Mouse, ClickButton.Mouse4).Button == ClickButton.Mouse4);
            Check(OutputSelection.ChangeType(next, OutputInputType.Mouse).Button == ClickButton.Left);
        });
        test("Output category rejects unassigned conflicting and unsupported keys without fallback", () =>
        {
            foreach (int key in new[] { 0, 17, 0x75, 0x76, 0x77 }) Reject(() => OutputSelection.ChangeType(new Settings { CustomKey = key }, OutputInputType.Keyboard));
            Reject(() => OutputSelection.ChangeType(new(), (OutputInputType)99));
            Reject(() => OutputSelection.ChangeType(new(), OutputInputType.Mouse, ClickButton.CustomKey));
        });
        test("Existing settings and version-one profiles persist output category without redundant flags", () =>
        {
            string root = Path.Combine("artifacts", "output-selection-" + Guid.NewGuid()); Directory.CreateDirectory(root);
            try
            {
                var settingsStore = new SettingsService(Path.Combine(root, "settings.json"));
                var profiles = new ProfileStore(Path.Combine(root, "profiles"));
                foreach (var button in Enum.GetValues<ClickButton>())
                {
                    var value = new Settings { Button = button, CustomKey = 69 }; settingsStore.Save(value);
                    Check(OutputSelection.TypeOf(settingsStore.Load().Button) == OutputSelection.TypeOf(button));
                    var profile = profiles.Create(value, button.ToString()); string export = Path.Combine(root, "export.json"); ProfileStore.Export(export, profile);
                    using var engine = new ClickEngine(new ButtonMouseInput(new Transport()), new Gate(), new Gate());
                    var applied = profiles.Import(export).Apply(new(), engine);
                    Check(OutputSelection.TypeOf(applied.Button) == OutputSelection.TypeOf(button) && applied.CustomKey == 69 && !engine.Enabled);
                }
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Button\":99}"); Check(settingsStore.Load().Button == ClickButton.Left && settingsStore.Warning is not null);
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"Button\":3,\"CustomKey\":0}"); Check(settingsStore.Load().Button == ClickButton.Left && settingsStore.Warning is not null);
                File.WriteAllText(Path.Combine(root, "settings.json"), "{}"); Check(settingsStore.Load().Button == ClickButton.Left);
            }
            finally { Directory.Delete(root, true); }
        });
        test("Both output categories use the shared worker in fixed and variable modes", () =>
        {
            foreach (var mode in Enum.GetValues<ClickTimingMode>())
            {
                var transport = new Transport(); using var engine = new ClickEngine(new ButtonMouseInput(transport), new Gate(), new Gate());
                engine.ConfigureTiming(mode, new(50, 50)); engine.ConfigureOutput(ClickButton.Mouse5, 69); engine.Start(); Until(() => engine.Clicks > 0);
                engine.ConfigureOutput(ClickButton.CustomKey, 69); Check(!engine.Enabled);
                engine.Start(); Until(() => transport.Keys.Count > 0); engine.EmergencyStop();
                Check(transport.Mouse.All(b => b == ClickButton.Mouse5) && transport.Keys.All(k => k == 69) && !engine.Enabled);
            }
        });
        test("Unsupported keyboard output cannot silently emit the remembered mouse button", () =>
        {
            var transport = new Transport { KeyboardSupported = false }; using var engine = new ClickEngine(new ButtonMouseInput(transport), new Gate());
            engine.ConfigureOutput(ClickButton.CustomKey, 69); bool rejected = false;
            try { engine.Start(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !engine.Enabled && !engine.Session.HasStarted && transport.Mouse.Count == 0 && transport.Keys.Count == 0);
        });
        test("Pause and emergency stop wait for partial keyboard input and release the original key", () =>
        {
            foreach (bool emergency in new[] { false, true })
            {
                using var transport = new BlockingTransport(); using var engine = new ClickEngine(new ButtonMouseInput(transport), new Gate());
                engine.ConfigureOutput(ClickButton.CustomKey, 69); engine.Start(); Check(transport.Entered.Wait(2000));
                var action = Task.Run(() => { if (emergency) engine.EmergencyStop(); else engine.TogglePause(); });
                try { Check(!action.Wait(25)); } finally { transport.Continue.Set(); }
                Check(action.Wait(2000) && !engine.Enabled && engine.Clicks == 0 && transport.MouseCalls == 0 && transport.Events.Contains((69, true)));
                int count = transport.Events.Count; Thread.Sleep(30); Check(transport.Events.Count == count);
            }
        });
    }
    private sealed class Gate : IVariableTargetGate { public bool CanActivate => true; public bool CanClick => true; }
    private sealed class Transport : IMouseTransport
    {
        public readonly ConcurrentQueue<ClickButton> Mouse = new(); public readonly ConcurrentQueue<int> Keys = new(); public bool KeyboardSupported = true;
        public bool Supports(ClickButton button) => ClickButtons.MouseChoices.Contains(button);
        public bool SupportsKey(int key) => KeyboardSupported && KeyboardOutput.IsValid(key);
        public int LastError => 5;
        public uint Send(ClickButton button, bool releaseOnly) { Mouse.Enqueue(button); return releaseOnly ? 1u : 2u; }
        public uint SendKey(int key, bool releaseOnly) { Keys.Enqueue(key); return releaseOnly ? 1u : 2u; }
    }
    private sealed class BlockingTransport : IMouseTransport, IDisposable
    {
        public readonly ManualResetEventSlim Entered = new(), Continue = new();
        public readonly ConcurrentQueue<(int Key, bool Up)> Events = new(); public int MouseCalls;
        public bool Supports(ClickButton button) => false;
        public bool SupportsKey(int key) => KeyboardOutput.IsValid(key);
        public int LastError => 5;
        public uint Send(ClickButton button, bool releaseOnly) { MouseCalls++; return 0; }
        public uint SendKey(int key, bool releaseOnly)
        {
            Events.Enqueue((key, releaseOnly));
            if (!releaseOnly) { Entered.Set(); if (!Continue.Wait(2000)) throw new TimeoutException("Test input was not released"); }
            return 1;
        }
        public void Dispose() { Entered.Dispose(); Continue.Dispose(); }
    }
}
