using System.Text.Json;
using ClickForge.Core;

internal static class ProfileTests
{
    public static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Profile assertion failed"); }
        void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or JsonException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected rejected profile operation"); }
        void WithStore(Action<string, ProfileStore> action)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "profile-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); try { action(path, new(Path.Combine(path, "Profiles"))); } finally { Directory.Delete(path, true); }
        }
        test("Profiles create, rename, duplicate and delete persist", () => WithStore((root, store) =>
        {
            var a = store.Create(new(), "Alpha"); Check(store.Load().Single() == a);
            var b = store.Rename(a, "Beta", "Daily timing"); Check(b.Id == a.Id && b.Created == a.Created && b.Modified >= b.Created);
            var copy = store.Duplicate(b); Check(copy.Id != b.Id && copy.FixedCps == b.FixedCps);
            store.Delete(a.Id); Check(store.Load().Single().Id == copy.Id); store.Delete(copy.Id); Check(store.Load().Length == 0);
        }));
        test("Profiles validate every rate, range and mode", () =>
        {
            var good = new TimingProfile(); good.Validate();
            foreach (var bad in new[] { good with { FixedCps = 0 }, good with { FixedCps = 51 }, good with { MinimumCps = 0 }, good with { MaximumCps = 51 }, good with { MinimumCps = 30, MaximumCps = 20 }, good with { TimingMode = (ClickTimingMode)90 }, good with { ActivationMode = (ActivationMode)90 }, good with { Id = Guid.Empty }, good with { Name = "\n" }, good with { Name = new('x', 81) }, good with { Description = new('x', 501) }, good with { SchemaVersion = 2 }, good with { Created = default } }) Reject(bad.Validate);
            (good with { MinimumCps = 1, MaximumCps = 50 }).Validate(); (good with { MinimumCps = 20, MaximumCps = 20 }).Validate();
        });
        test("Applying profiles stops active input and preserves application safety settings", () =>
        {
            var mouse = new ProfileMouse(); var target = new ProfileTarget(); using var engine = new ClickEngine(mouse, target, target);
            engine.Start(); Check(SpinWait.SpinUntil(() => mouse.Count > 0, 2000));
            var old = new Settings { RobloxOnly = true, ActivateKey = 0x70, MinimizeToTray = true, CloseToTray = true, CheckForUpdatesOnStartup = true };
            var profile = new TimingProfile { TimingMode = ClickTimingMode.Variable, MinimumCps = 8, MaximumCps = 12, ActivationMode = ActivationMode.Hold };
            var next = profile.Apply(old, engine); int count = mouse.Count; Thread.Sleep(100);
            Check(!engine.Enabled && mouse.Count == count && mouse.Releases > 0 && engine.TimingMode == ClickTimingMode.Variable);
            Check(next.RobloxOnly && next.ActivateKey == old.ActivateKey && next.MinimizeToTray && next.CloseToTray && next.CheckForUpdatesOnStartup && next.ActiveProfileId == profile.Id && profile.Matches(next));
            next = (profile with { FixedCps = 17, TimingMode = ClickTimingMode.Fixed }).Apply(next, engine);
            Check(!engine.Enabled && engine.Cps == 17 && engine.TimingMode == ClickTimingMode.Fixed);
        });
        test("Profile apply waits for an in-flight input pair", () =>
        {
            var mouse = new BlockingProfileMouse(); using var engine = new ClickEngine(mouse, new ProfileTarget()); engine.Start(); Check(mouse.Entered.Wait(2000));
            var apply = Task.Run(() => new TimingProfile().Apply(new(), engine)); Check(!apply.Wait(40)); mouse.Finish.Set(); Check(apply.Wait(2000)); Check(!engine.Enabled && !mouse.Down);
        });
        test("Invalid profile cannot mutate an active engine", () =>
        {
            using var engine = new ClickEngine(new ProfileMouse(), new ProfileTarget()); engine.Start(); Reject(() => (new TimingProfile { MaximumCps = 0 }).Apply(new(), engine)); Check(engine.Enabled && engine.Cps == 10); engine.Stop();
        });
        test("Variable profile cannot activate without a selected target", () =>
        {
            using var engine = new ClickEngine(new ProfileMouse(), new ProfileTarget()); new TimingProfile { TimingMode = ClickTimingMode.Variable }.Apply(new(), engine);
            bool blocked = false; try { engine.Start(); } catch (InvalidOperationException) { blocked = true; } Check(blocked && !engine.Enabled);
        });
        test("Profile roundtrip and import identity conflicts create an unapplied copy", () => WithStore((root, store) =>
        {
            var a = store.Create(new Settings { TimingMode = ClickTimingMode.Variable }, "Roundtrip"); string path = Path.Combine(root, "export.json"); ProfileStore.Export(path, a); Check(ProfileStore.Read(path) == a);
            var copy = store.Import(path); Check(copy.Id != a.Id && copy.Name == a.Name && copy.Summary == a.Summary && store.Load().Length == 2);
            string json = File.ReadAllText(path); Check(!json.Contains("RobloxOnly") && !json.Contains("ActivateKey") && !json.Contains("Path") && !json.Contains("Enabled"));
        }));
        test("Profile imports reject scripts, target identities, safety flags and updater endpoints", () => WithStore((root, store) =>
        {
            string path = Path.Combine(root, "attack.json"); var p = new TimingProfile(); string json = JsonSerializer.Serialize(p);
            foreach (string field in new[] { "ProcessId", "Handle", "Path", "RobloxOnly", "Enabled", "ActivateKey", "Script", "UpdateEndpoint", "$type" })
            { File.WriteAllText(path, json[..^1] + ",\"" + field + "\":\"untrusted\"}"); Reject(() => store.Import(path)); }
            Check(store.Load().Length == 0);
        }));
        test("Profile imports reject missing, null, duplicate and malformed fields", () => WithStore((root, store) =>
        {
            string path = Path.Combine(root, "invalid.json");
            foreach (string text in new[] { "null", "{}", "{bad", "[]", JsonSerializer.Serialize(new TimingProfile())[..^1] + ",\"SchemaVersion\":1}" })
            { File.WriteAllText(path, text); Reject(() => store.Import(path)); }
        }));
        test("Profile import size and schema limits", () => WithStore((root, store) =>
        {
            string path = Path.Combine(root, "oversized.json"); File.WriteAllText(path, new string(' ', ProfileStore.MaximumImportBytes + 1)); Reject(() => store.Import(path));
            File.WriteAllText(path, JsonSerializer.Serialize(new TimingProfile { SchemaVersion = 2 })); Reject(() => store.Import(path));
        }));
        test("Corrupt profiles do not discard healthy profiles", () => WithStore((root, store) =>
        {
            var healthy = store.Create(new(), "Healthy"); File.WriteAllText(Path.Combine(root, "Profiles", Guid.NewGuid().ToString("N") + ".json"), "bad"); Check(store.Load().Single() == healthy && store.Warnings.Count == 1);
        }));
        test("Profile filenames cannot substitute another identifier", () => WithStore((root, store) =>
        {
            var healthy = store.Create(new(), "Healthy"); File.Move(Path.Combine(root, "Profiles", healthy.Id.ToString("N") + ".json"), Path.Combine(root, "Profiles", Guid.NewGuid().ToString("N") + ".json")); Check(store.Load().Length == 0 && store.Warnings.Count == 1);
        }));
        test("Profile write failures report failure and allow recovery", () => WithStore((root, _) =>
        {
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "preserve"); var store = new ProfileStore(blocked); Reject(() => store.Create(new(), "Fail")); Check(File.ReadAllText(blocked) == "preserve");
            File.Delete(blocked); Check(store.Create(new(), "Recovered").Name == "Recovered");
        }));
        test("Concurrent saves across profile and settings service instances remain valid", () => WithStore((root, store) =>
        {
            var p = store.Create(new(), "Concurrent"); string settingsPath = Path.Combine(root, "settings.json");
            Parallel.For(0, 30, i => { new ProfileStore(Path.Combine(root, "Profiles")).Save(p with { FixedCps = i % 50 + 1 }); new SettingsService(settingsPath).Save(new Settings { Cps = i % 50 + 1 }); });
            store.Load().Single().Validate(); new SettingsService(settingsPath).Load().Validate(); Check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any());
        }));
        test("Profile count bounded and built-in restore retains custom profiles", () => WithStore((root, store) =>
        {
            var p = store.Create(new(), "Custom"); store.RestoreBuiltIns(); store.RestoreBuiltIns(); Check(store.Load().Length == 3 && store.Load().Any(x => x.Id == p.Id));
            for (int i = 3; i < ProfileStore.MaximumProfiles; i++) store.Create(new(), "Profile " + i);
            Reject(() => store.Create(new(), "Overflow")); Check(store.Load().Length == ProfileStore.MaximumProfiles);
        }));
        test("Old settings migrate with conservative tray and profile defaults", () => WithStore((root, _) =>
        {
            string path = Path.Combine(root, "settings.json"); File.WriteAllText(path, "{\"Cps\":19,\"RobloxOnly\":true}"); var store = new SettingsService(path); var s = store.Load(); Check(s.Cps == 19 && s.RobloxOnly && !s.CloseToTray && !s.MinimizeToTray && s.ActiveProfileId is null);
            var next = s with { ActiveProfileId = Guid.NewGuid(), CloseToTray = true }; store.Save(next); Check(store.Load() == next);
        }));
        test("Activity history is bounded, clearable and contains only predefined redacted events", () =>
        {
            var h = new ActivityHistory(3); for (int i = 0; i < 10; i++) h.Add(ActivityKind.ProfileApplied); Check(h.Entries.Length == 3); Check(!h.RedactedReport().Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase)); h.Clear(); Check(h.Entries.Length == 0);
        });
    }
    private sealed class ProfileMouse : IMouseInput { public int Count, Releases; public void Click() => Interlocked.Increment(ref Count); public void Release() => Interlocked.Increment(ref Releases); }
    private sealed class ProfileTarget : IVariableTargetGate { public bool CanActivate => true; public bool CanClick => true; }
    private sealed class BlockingProfileMouse : IMouseInput { public ManualResetEventSlim Entered = new(), Finish = new(); public volatile bool Down; public void Click() { Down = true; Entered.Set(); Finish.Wait(2000); Down = false; } public void Release() => Down = false; }
}
