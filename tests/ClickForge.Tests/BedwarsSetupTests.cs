using System.Collections.Concurrent;
using ClickForge.Core;

internal static class BedwarsSetupTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Bedwars setup assertion failed"); }
        void Until(Func<bool> value) { if (!SpinWait.SpinUntil(value, 2000)) throw new Exception("Bedwars worker timed out"); }
        test("Bedwars reverses front-back outputs while preserving the original setup", () =>
        {
            Check(SideButtonOutputs.OutputFor(4) == ClickButton.Left && SideButtonOutputs.OutputFor(5) == ClickButton.Right);
            Check(SideButtonOutputs.OutputFor(4, MouseSetup.MinecraftBedwars) == ClickButton.Right && SideButtonOutputs.OutputFor(5, MouseSetup.MinecraftBedwars) == ClickButton.Left);
            var router = new MouseShortcutRouter(); foreach (var (id, key) in SideButtonOutputs.Bindings) router.Register(id, key);
            Check(SideButtonOutputs.OutputFor(router.Process(0x20B, 2u << 16, 0)!.Value, MouseSetup.MinecraftBedwars) == ClickButton.Left);
            Check(SideButtonOutputs.OutputFor(router.Process(0x20B, 1u << 16, 0)!.Value, MouseSetup.MinecraftBedwars) == ClickButton.Right);
        });
        test("Unknown setup values cannot change output mapping or pass settings validation", () =>
        {
            foreach (Action invalid in new Action[] { () => new Settings { MouseSetup = (MouseSetup)99 }.Validate(), () => SideButtonOutputs.OutputFor(4, (MouseSetup)99), () => SideButtonOutputs.OutputFor(0, MouseSetup.MinecraftBedwars) })
            { bool rejected = false; try { invalid(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        });
        test("Bedwars settings persist without weakening restrictions and old settings retain original mappings", () =>
        {
            string root = Path.Combine("artifacts", "bedwars-" + Guid.NewGuid()); Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "settings.json"); var store = new SettingsService(path);
                store.Save(new() { SideButtonToggles = true, MouseSetup = MouseSetup.MinecraftBedwars, RobloxOnly = true });
                var saved = store.Load(); Check(saved.MouseSetup == MouseSetup.MinecraftBedwars && saved.RobloxOnly && saved.SideButtonToggles);
                using var engine = new ClickEngine(new Mouse(), new Gate());
                var applied = TimingProfile.Capture(saved, "Saved speed").Apply(saved, engine);
                Check(applied.MouseSetup == MouseSetup.MinecraftBedwars && applied.RobloxOnly && !engine.Enabled);
                File.WriteAllText(path, "{\"SideButtonToggles\":true}"); Check(store.Load().MouseSetup == MouseSetup.Standard);
                File.WriteAllText(path, "{\"MouseSetup\":99}"); Check(store.Load().MouseSetup == MouseSetup.Standard && !store.Load().SideButtonToggles && store.Warning is not null);
            }
            finally { Directory.Delete(root, true); }
        });
    }
    private sealed class Gate : IVariableTargetGate { public bool CanActivate => CanClick; private volatile bool allowed; public bool CanClick { get => allowed; set => allowed = value; } }
    private sealed class Mouse : IMouseInput
    {
        internal readonly ConcurrentQueue<ClickButton> Outputs = new(); internal int Releases, MaximumConcurrency; private int concurrent;
        public bool Supports(ClickButton button) => button is ClickButton.Left or ClickButton.Right;
        public void Click() => Click(ClickButton.Left);
        public void Click(ClickButton button) { int count = Interlocked.Increment(ref concurrent); MaximumConcurrency = Math.Max(count, MaximumConcurrency); try { Outputs.Enqueue(button); } finally { Interlocked.Decrement(ref concurrent); } }
        public void Release() => Interlocked.Increment(ref Releases);
    }
}
