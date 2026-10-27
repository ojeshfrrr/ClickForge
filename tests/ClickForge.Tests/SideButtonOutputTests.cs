using ClickForge.Core;

internal static class SideButtonOutputTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Side-button output assertion failed"); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejected side-button binding"); }
        test("Side output IDs map physical Back to Left and Forward to Right", () =>
        {
            var router = new MouseShortcutRouter();
            foreach (var (id, key) in SideButtonOutputs.Bindings) Check(router.Register(id, key));
            Check(SideButtonOutputs.OutputFor(router.Process(0x20B, 1u << 16, 0)!.Value) == ClickButton.Left);
            Check(SideButtonOutputs.OutputFor(router.Process(0x20B, 2u << 16, 0)!.Value) == ClickButton.Right);
            Check(router.Process(0x20B, 1u << 16, 0) is null);
            router.Process(0x20C, 1u << 16, 0); Check(router.Process(0x20B, 1u << 16, 0) == SideButtonOutputs.LeftId);
            Reject(() => SideButtonOutputs.OutputFor(1));
        });
        test("Injected side events cannot activate output toggles", () =>
        {
            var router = new MouseShortcutRouter();
            foreach (var (id, key) in SideButtonOutputs.Bindings) Check(router.Register(id, key));
            foreach (uint flags in new uint[] { 1, 2, 3 })
                foreach (uint button in new uint[] { 1, 2 }) Check(router.Process(0x20B, button << 16, flags) is null);
        });
        test("Side output registration preserves emergency and rolls back either side failure", () =>
        {
            foreach (int fail in new[] { 0, SideButtonOutputs.LeftId, SideButtonOutputs.RightId })
            {
                var registrar = new Registrar { Fail = fail }; using var bindings = new HotkeyBindings(registrar);
                bool rejected = false; try { bindings.Apply(new() { SideButtonToggles = true }); } catch (InvalidOperationException) { rejected = true; }
                Check(registrar.Order[0] == 3 && rejected == (fail != 0));
                Check(registrar.Active.Count == (fail == 0 ? 5 : 0));
                if (fail == 0) { bindings.Apply(new()); Check(registrar.Active.Count == 3 && !registrar.Active.Contains(4) && !registrar.Active.Contains(5)); }
            }
        });
        test("Side output reservation rejects all conflicting global side shortcuts", () =>
        {
            foreach (int key in new[] { 5, 6 })
            {
                Reject(() => new Settings { SideButtonToggles = true, ActivateKey = key }.Validate());
                Reject(() => new Settings { SideButtonToggles = true, PauseKey = key }.Validate());
                Reject(() => new Settings { SideButtonToggles = true, StopKey = key }.Validate());
            }
        });
        test("Side output opt-in persists with defaults off and invalid loaded conflicts recover safely", () =>
        {
            string path = Path.Combine("artifacts", "side-toggle-" + Guid.NewGuid() + ".json");
            try
            {
                var store = new SettingsService(path); Check(!store.Load().SideButtonToggles);
                store.Save(new() { SideButtonToggles = true }); Check(store.Load().SideButtonToggles && store.Load().Mode == ActivationMode.Toggle);
                File.WriteAllText(path, "{\"SideButtonToggles\":true,\"ActivateKey\":5}"); Check(!store.Load().SideButtonToggles && store.Warning is not null);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        test("Timing profiles preserve side toggle preference without enabling input", () =>
        {
            using var engine = new ClickEngine(new Mouse(), new Gate());
            var settings = new Settings { SideButtonToggles = true };
            var next = TimingProfile.Capture(settings, "Toggles").Apply(settings, engine);
            Check(next.SideButtonToggles && !engine.Enabled);
        });
    }
    private sealed class Registrar : IHotkeyRegistrar
    {
        internal int Fail; internal readonly List<int> Order = []; internal readonly HashSet<int> Active = [];
        public bool Register(int id, int key) { Order.Add(id); return id != Fail && Active.Add(id); }
        public void Unregister(int id) => Active.Remove(id);
    }
    private sealed class Gate : ITargetGate { public bool CanClick => true; }
    private sealed class Mouse : IMouseInput { public void Click() { } public void Release() { } }
}
