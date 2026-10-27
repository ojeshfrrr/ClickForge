using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using ClickForge.Core;
using ClickForge.Core.Updates;

if (args.Length is 3 or 4 && args[0] == "--verify-package")
{
    string stage = args.Length == 4 ? Path.GetFullPath(args[3]) : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "updater", "verify-" + Guid.NewGuid().ToString("N"));
    var manifest = new UpdatePackage().ExtractAndVerify(Path.GetFullPath(args[1]), stage, SemanticVersion.Parse(args[2]), default);
    Console.WriteLine($"VERIFIED package {manifest.Version}, source {manifest.SourceCommit}, {manifest.Files.Count} payload files"); return 0;
}
if (args.Length == 1 && args[0] == "--check-live")
{
    using var live = new UpdateClient(); await live.CheckAsync(SemanticVersion.Installed(typeof(Settings).Assembly)); Console.WriteLine($"LIVE {live.Status.State}: {live.Status.Message}");
    return live.Status.State is UpdateState.UpToDate or UpdateState.Available ? 0 : 1;
}
int passed = 0, failed = 0;
async Task Test(string name, Func<Task> action) { try { await action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
void Throws(Action action) { try { action(); } catch (Exception e) when (e is FormatException or InvalidDataException or IOException or ArgumentException) { return; } throw new Exception("Expected validation failure"); }
Task Done(Action action) { action(); return Task.CompletedTask; }
var installed = SemanticVersion.Parse("1.1.0+commit");

await Test("Updater requests identify the actual assembly release version", async () =>
{
    string? productVersion = null;
    using var handler = new Handler((request, _) =>
    {
        productVersion = request.Headers.UserAgent.Single(p => p.Product?.Name == "ClickForge-Updater").Product!.Version;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    });
    using var client = new UpdateClient(new(handler)); await client.CheckAsync(installed);
    Check(productVersion == SemanticVersion.Installed(typeof(UpdateClient).Assembly).Value.Split('+')[0] && client.Status.State == UpdateState.Unavailable);
});

await Test("Upgrade and rollback preserve Bedwars setup without enabling input", async () =>
{
    foreach (bool startupFails in new[] { false, true })
    {
        using var fixture = new Fixture(); string path = Path.Combine(fixture.Install, "settings.json");
        var store = new SettingsService(path); var preferences = new Settings { MouseSetup = MouseSetup.MinecraftBedwars, SideButtonToggles = true, Cps = 19, RobloxOnly = true };
        store.Save(preferences); string before = File.ReadAllText(path);
        var request = fixture.Prepare(); bool completed = await new UpdateInstaller(new Platform { Confirm = !startupFails }, fixture.Updates, fixture.Package).InstallAsync(request);
        Check(completed != startupFails && File.ReadAllText(path) == before && store.Load() == preferences);
        using var engine = new ClickEngine(new NoMouse(), new NoTarget()); Check(!engine.Enabled && engine.State == EngineState.Stopped);
    }
});

await Test("Semantic version precedence and build metadata", () => Done(() => { Check(SemanticVersion.Parse("v1.10.0").CompareTo(SemanticVersion.Parse("1.9.9")) > 0); Check(installed.CompareTo(SemanticVersion.Parse("1.1.0+other")) == 0); Check(SemanticVersion.Parse("1.2.0-beta.10").CompareTo(SemanticVersion.Parse("1.2.0-beta.2")) > 0); Check(SemanticVersion.Parse("1.2.0").CompareTo(SemanticVersion.Parse("1.2.0-rc.1")) > 0); }));
await Test("Malformed versions rejected", () => Done(() => { foreach (string? value in new[] { "1.2", "01.2.3", "1.2.3-01", "1.2.3-", "../1.2.3", "1.2.3\n", null }) Throws(() => SemanticVersion.Parse(value)); }));
await Test("Installer assets coexist without changing the verified ZIP update channel", () => Done(() =>
{
    using var f = new Fixture(); var node = System.Text.Json.Nodes.JsonNode.Parse(f.Json())!; var assets = node["assets"]!.AsArray();
    foreach (string suffix in new[] { ".exe", ".exe.sha256" })
    {
        var asset = assets[0]!.DeepClone(); string name = "ClickForge-Setup-v1.2.0" + suffix; asset["name"] = name;
        asset["browser_download_url"] = "https://github.com/ojeshfrrr/ClickForge/releases/download/v1.2.0/" + name; assets.Add(asset);
    }
    var parsed = UpdateClient.ParseRelease(node.ToJsonString()); Check(parsed.Archive.Name == Fixture.Name && parsed.Checksum.Name == Fixture.Name + ".sha256");
}));
await Test("Installed assembly metadata is readable", () => Done(() => Check(SemanticVersion.Installed(typeof(Settings).Assembly).Prerelease is null)));
await Test("New stable release available with published notes", async () => { using var f = new Fixture(); using var c = f.Client(); await c.CheckAsync(installed); Check(c.Status.State == UpdateState.Available && c.Latest!.Notes == "Actual release notes"); });
await Test("Same version and older release are not updates", async () => { using var f = new Fixture(); using var c = f.Client(); await c.CheckAsync(SemanticVersion.Parse("1.2.0+local")); Check(c.Status.State == UpdateState.UpToDate); await c.CheckAsync(SemanticVersion.Parse("2.0.0")); Check(c.Status.State == UpdateState.UpToDate); });
await Test("Draft and prerelease metadata ignored", () => Done(() => { using var f = new Fixture(); Throws(() => UpdateClient.ParseRelease(f.Json(draft: true))); Throws(() => UpdateClient.ParseRelease(f.Json(prerelease: true))); Throws(() => UpdateClient.ParseRelease(f.Json().Replace("v1.2.0", "v1.2.0-rc.1"))); }));
await Test("Missing and malformed metadata fail safely", async () => { foreach (string json in new[] { "{", "{}", "[]", "null", "{\"draft\":false,\"prerelease\":false}" }) { using var f = new Fixture(); using var c = f.Client(json); await c.CheckAsync(installed); Check(c.Status.State == UpdateState.Failed); } });
await Test("Missing or duplicate assets rejected", () => Done(() => { using var f = new Fixture(); Throws(() => UpdateClient.ParseRelease(f.Json().Replace(Fixture.Name, "wrong.zip"))); var j = JsonNodeHelper.DuplicateAsset(f.Json()); Throws(() => UpdateClient.ParseRelease(j)); }));
await Test("HTTPS and official repository boundaries enforced", () => Done(() => { using var f = new Fixture(); Throws(() => UpdateClient.ParseRelease(f.Json().Replace("https://github.com/ojeshfrrr", "https://evil.example/ojeshfrrr"))); Throws(() => UpdateClient.ParseRelease(f.Json().Replace("https://github.com/", "http://github.com/"))); Check(!UpdateChannel.IsAllowedRedirect(new("https://release-assets.githubusercontent.com.evil.example/x"))); }));
await Test("API rate limit and offline failures are friendly", async () => { using var h = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))); using var c = new UpdateClient(new(h)); await c.CheckAsync(installed); Check(c.Status.State == UpdateState.Unavailable && c.Status.Message.Contains("rate limit")); });
await Test("Duplicate checks issue one request", async () => { using var f = new Fixture(); var started = new TaskCompletionSource(); var finish = new TaskCompletionSource(); int calls = 0; using var h = new Handler(async (_, token) => { Interlocked.Increment(ref calls); started.SetResult(); await finish.Task.WaitAsync(token); return Fixture.Response(f.Json()); }); using var c = new UpdateClient(new(h)); var first = c.CheckAsync(installed); await started.Task; await c.CheckAsync(installed); Check(calls == 1); finish.SetResult(); await first; });
await Test("Valid download verified and staged with progress", async () => { using var f = new Fixture(); using var c = f.Client(); var states = new List<UpdateState>(); c.StatusChanged += s => states.Add(s.State); await c.CheckAsync(installed); await c.DownloadAsync(installed); Check(c.Status.State == UpdateState.ReadyToInstall && c.Staged is not null); Check(states.Contains(UpdateState.Downloading) && states.Contains(UpdateState.Verifying)); Check(File.Exists(Path.Combine(c.Staged!.Staging, "ClickForge.Updater.exe"))); });
await Test("Incorrect checksum rejected and partial workspace cleaned", async () => { using var f = new Fixture(); f.Checksum = new string('0', 64); using var c = f.Client(); await c.CheckAsync(installed); await c.DownloadAsync(installed); Check(c.Status.State == UpdateState.Failed && c.Staged is null && !Directory.EnumerateDirectories(f.Updates).Any()); });
await Test("Checksum filenames and malformed hashes rejected", () => Done(() => { Throws(() => UpdatePackage.ParseChecksum(new string('a', 64) + " wrong.zip", Fixture.Name)); Throws(() => UpdatePackage.ParseChecksum("bad " + Fixture.Name, Fixture.Name)); }));
await Test("Truncated download rejected", async () => { using var f = new Fixture(); using var c = f.Client(truncate: true); await c.CheckAsync(installed); await c.DownloadAsync(installed); Check(c.Status.State == UpdateState.Failed && c.Staged is null); });
await Test("Download cancellation is prompt and leaves installed files untouched", async () => { using var f = new Fixture(); using var c = f.Client(block: true); await c.CheckAsync(installed); using var cancel = new CancellationTokenSource(100); await c.DownloadAsync(installed, cancel.Token); Check(c.Status.State == UpdateState.Available && File.ReadAllText(Path.Combine(f.Install, "settings.json")) == "private settings"); Check(!Directory.EnumerateDirectories(f.Updates).Any()); });
await Test("ZIP traversal and unexpected contents rejected", () => Done(() => { foreach (string entry in new[] { "../escape.exe", "/escape.exe", "C:/escape.exe", "licenses/../../escape.exe", "licenses\\escape.exe", "extra.exe", "licenses/file:stream" }) { using var f = new Fixture(entry); Throws(() => f.Package.ExtractAndVerify(f.Archive, Path.Combine(f.Root, "stage"), SemanticVersion.Parse("1.2.0"), default)); Check(!File.Exists(Path.Combine(f.Root, "escape.exe"))); } }));
await Test("Duplicate and symlink ZIP entries rejected", () => Done(() => { using var f = new Fixture("ClickForge.exe"); Throws(() => f.Package.ExtractAndVerify(f.Archive, Path.Combine(f.Root, "stage"), SemanticVersion.Parse("1.2.0"), default)); using var s = new Fixture(link: true); Throws(() => s.Package.ExtractAndVerify(s.Archive, Path.Combine(s.Root, "stage"), SemanticVersion.Parse("1.2.0"), default)); }));
await Test("Missing helper, invalid manifest and corrupt payload rejected", () => Done(() => { foreach (int mode in new[] { 1, 2, 3 }) { using var f = new Fixture(bad: mode); Throws(() => f.Package.ExtractAndVerify(f.Archive, Path.Combine(f.Root, "stage"), SemanticVersion.Parse("1.2.0"), default)); } }));
await Test("Wrong expected package version rejected", () => Done(() => { using var f = new Fixture(); Throws(() => f.Package.ExtractAndVerify(f.Archive, Path.Combine(f.Root, "stage"), SemanticVersion.Parse("1.3.0"), default)); }));
await Test("Installer waits for parent exit before replacement", async () => { using var f = new Fixture(); var request = f.Prepare(); var p = new Platform { Wait = new TaskCompletionSource() }; var installer = new UpdateInstaller(p, f.Updates, f.Package); var run = installer.InstallAsync(request); await p.Ready.Task; Check(File.ReadAllText(Path.Combine(f.Install, "ClickForge.exe")) == "1.1.0"); p.Wait.SetResult(); Check(await run && p.Confirmed && File.ReadAllText(Path.Combine(f.Install, "ClickForge.exe")) == "1.2.0"); Check(File.ReadAllText(Path.Combine(f.Install, "settings.json")) == "private settings"); });
await Test("Copy failure rolls back every managed file and preserves settings", async () => { using var f = new Fixture(); var request = f.Prepare(); var p = new Platform(); bool ok = await new UpdateInstaller(p, f.Updates, f.Package, _ => throw new IOException("simulated replacement failure")).InstallAsync(request); Check(!ok && p.Previous && File.ReadAllText(Path.Combine(f.Install, "ClickForge.exe")) == "1.1.0" && File.ReadAllText(Path.Combine(f.Install, "settings.json")) == "private settings"); Check(UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(request.Workspace, "result.json")).Status == "Failed"); });
await Test("Startup failure restores previous version", async () => { using var f = new Fixture(); var r = f.Prepare(); var p = new Platform { Confirm = false }; Check(!await new UpdateInstaller(p, f.Updates, f.Package).InstallAsync(r)); Check(p.Previous && File.ReadAllText(Path.Combine(f.Install, "ClickForge.Updater.exe")) == "1.1.0"); });
await Test("Parent exit timeout never replaces files", async () => { using var f = new Fixture(); var r = f.Prepare(); var p = new Platform { ParentFailure = true }; Check(!await new UpdateInstaller(p, f.Updates, f.Package).InstallAsync(r)); Check(File.ReadAllText(Path.Combine(f.Install, "ClickForge.exe")) == "1.1.0"); });
await Test("Interrupted replacement can be recovered from journal", async () => { using var f = new Fixture(); var r = f.Prepare(); var files = UpdatePackage.Payload.Append("update-manifest.json").Where(n => File.Exists(Path.Combine(f.Install, n))).ToArray(); foreach (string name in files) { string dest = Path.Combine(r.Workspace, "backup", name); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(Path.Combine(f.Install, name), dest); } UpdateFiles.WriteJson(Path.Combine(r.Workspace, "journal.json"), new InstallJournal(f.Install, files, "Replacing", files.ToDictionary(name => name, name => UpdatePackage.Hash(Path.Combine(r.Workspace, "backup", name))))); File.WriteAllText(Path.Combine(f.Install, "ClickForge.exe"), "interrupted"); Check(!await new UpdateInstaller(new Platform(), f.Updates, f.Package).InstallAsync(r, true)); Check(File.ReadAllText(Path.Combine(f.Install, "ClickForge.exe")) == "1.1.0"); });
await Test("Duplicate installer prevented by exclusive lock", async () => { using var f = new Fixture(); var r = f.Prepare(); using var held = new FileStream(Path.Combine(f.Install, ".clickforge-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); bool rejected = false; try { await new UpdateInstaller(new Platform(), f.Updates, f.Package).InstallAsync(r); } catch (IOException) { rejected = true; } Check(rejected); });
await Test("Startup acknowledgement validates version, path and nonce", () => Done(() => { using var f = new Fixture(); var r = f.Prepare(); string result = Path.Combine(r.Workspace, "result.json"), exe = Path.Combine(f.Install, "ClickForge.exe"); UpdateFiles.WriteJson(result, new InstallReceipt(r.Nonce, "1.2.0", exe, "Restarting", "waiting")); Throws(() => UpdateStartup.Acknowledge(result, r.Nonce, installed, exe, f.Updates)); Throws(() => UpdateStartup.Acknowledge(result, new string('b',64), SemanticVersion.Parse("1.2.0"), exe, f.Updates)); UpdateStartup.Acknowledge(result, r.Nonce, SemanticVersion.Parse("1.2.0+commit"), exe, f.Updates); Check(UpdateFiles.ReadJson<StartupAcknowledgement>(Path.Combine(r.Workspace, "ack.json")).Version == "1.2.0+commit"); }));
await Test("Successful transaction reports success only after confirmation", async () => { using var f = new Fixture(); var r = f.Prepare(); var p = new Platform { BeforeConfirm = () => Check(UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(r.Workspace, "result.json")).Status == "Restarting") }; Check(await new UpdateInstaller(p, f.Updates, f.Package).InstallAsync(r)); Check(UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(r.Workspace, "result.json")).Status == "Succeeded"); Check(!Directory.Exists(Path.Combine(r.Workspace, "backup"))); });
await Test("Update preferences persist but activation cannot", () => Done(() => { using var f = new Fixture(); var s = new SettingsService(Path.Combine(f.Root, "prefs.json")); s.Save(new Settings { Cps = 17, CheckForUpdatesOnStartup = true }); Check(s.Load().Cps == 17 && s.Load().CheckForUpdatesOnStartup); using var engine = new ClickEngine(new NoMouse(), new NoTarget()); Check(!engine.Enabled && engine.State == EngineState.Stopped); }));
await Test("Disallowed download redirects rejected", async () => { using var f = new Fixture(); using var h = new Handler((r, _) => Task.FromResult(r.RequestUri == UpdateChannel.Latest ? Fixture.Response(f.Json()) : new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.example/payload") } })); using var c = new UpdateClient(new(h), f.Updates, f.Package); await c.CheckAsync(installed); await c.DownloadAsync(installed); Check(c.Status.State == UpdateState.Failed && c.Staged is null); });
await Test("Duplicate download requests are prevented", async () => { using var f = new Fixture(); using var c = f.Client(block: true); await c.CheckAsync(installed); using var cancel = new CancellationTokenSource(); var first = c.DownloadAsync(installed, cancel.Token); await Task.Delay(30); await c.DownloadAsync(installed); cancel.Cancel(); await first; Check(c.Staged is null && c.Status.State == UpdateState.Available); });
await Test("Interrupted backup corruption requires recovery instead of false success", async () => { using var f = new Fixture(); var r = f.Prepare(); var files = UpdatePackage.Payload; var hashes = new Dictionary<string,string>(); foreach (string name in files) { string dest = Path.Combine(r.Workspace,"backup",name); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(Path.Combine(f.Install,name),dest); hashes[name] = UpdatePackage.Hash(dest); } UpdateFiles.WriteJson(Path.Combine(r.Workspace,"journal.json"), new InstallJournal(f.Install,files,"Replacing",hashes)); File.WriteAllText(Path.Combine(r.Workspace,"backup","ClickForge.exe"),"damaged backup"); Check(!await new UpdateInstaller(new Platform(),f.Updates,f.Package).InstallAsync(r,true)); Check(UpdateFiles.ReadJson<InstallReceipt>(Path.Combine(r.Workspace,"result.json")).Status == "RecoveryRequired"); Check(Directory.Exists(Path.Combine(r.Workspace,"backup"))); });
await Test("Old successful caches cleaned; failed recovery retained", () => Done(() => { using var f = new Fixture(); string completed = Path.Combine(f.Updates,Guid.NewGuid().ToString("N")), failed = Path.Combine(f.Updates,Guid.NewGuid().ToString("N")); foreach (string dir in new[] { completed,failed }) { Directory.CreateDirectory(dir); UpdateFiles.WriteJson(Path.Combine(dir,"result.json"), new InstallReceipt(new string('a',64),"1.2.0",Path.Combine(f.Install,"ClickForge.exe"),dir == completed ? "Succeeded" : "Failed","test")); Directory.SetLastWriteTimeUtc(dir,DateTime.UtcNow.AddDays(-10)); } UpdateCache.CleanupCompleted(f.Updates); Check(!Directory.Exists(completed) && Directory.Exists(failed)); }));
await Test("Co-modified stage and manifest cannot bypass original archive", () => Done(() => { using var f = new Fixture(); var r = f.Prepare(); string stage = Path.Combine(r.Workspace,"staging"), manifestPath = Path.Combine(stage,"update-manifest.json"); var m = UpdateFiles.ReadJson<PackageManifest>(manifestPath); File.WriteAllText(Path.Combine(stage,"README.txt"),"changed stage"); m.Files["README.txt"] = UpdatePackage.Hash(Path.Combine(stage,"README.txt")); UpdateFiles.WriteJson(manifestPath,m); Throws(() => f.Package.VerifyStagingFromArchive(Path.Combine(r.Workspace,"package.zip"),stage,SemanticVersion.Parse("1.2.0"))); }));
await Test("Successful install and rollback preserve profile files alongside preferences", async () =>
{
    foreach (bool failure in new[] { false, true })
    {
        using var f = new Fixture(); string profiles = Path.Combine(f.Install, "Profiles"); Directory.CreateDirectory(profiles);
        var saved = new TimingProfile { Name = "User-owned profile" }; string path = Path.Combine(profiles, saved.Id.ToString("N") + ".json"); ProfileStore.Export(path, saved); string hash = UpdatePackage.Hash(path);
        var platform = new Platform(); bool result = await new UpdateInstaller(platform, f.Updates, f.Package, failure ? _ => throw new IOException("simulated failure") : null).InstallAsync(f.Prepare());
        Check(result != failure && UpdatePackage.Hash(path) == hash && ProfileStore.Read(path) == saved && File.ReadAllText(Path.Combine(f.Install, "settings.json")) == "private settings");
    }
});
Console.WriteLine($"RESULT {passed} passed; {failed} failed");
return failed == 0 ? 0 : 1;

sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken); }
sealed class BlockingStream : MemoryStream { public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<int>(_ => throw new OperationCanceledException(cancellationToken), cancellationToken)); }
sealed class Fixture : IDisposable
{
    public const string Name = "ClickForge-v1.2.0-win-x64.zip";
    public string Root { get; } = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "updater", "test-" + Guid.NewGuid().ToString("N"));
    public string Updates => Path.Combine(Root, "updates"); public string Install => Path.Combine(Root, "installed"); public string Archive => Path.Combine(Root, "package.zip");
    public UpdatePackage Package { get; } = new(path => File.ReadAllText(path));
    public byte[] Bytes { get; } public string Checksum { get; set; }
    public Fixture(string? extra = null, int bad = 0, bool link = false)
    {
        Directory.CreateDirectory(Updates); Directory.CreateDirectory(Install);
        string content = Path.Combine(Root, "content"); Directory.CreateDirectory(content);
        foreach (string name in UpdatePackage.Payload) { string file = Path.Combine(content, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, name.EndsWith(".exe") ? "1.2.0" : "new payload"); string old = Path.Combine(Install, name); Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllText(old, name.EndsWith(".exe") ? "1.1.0" : "old payload"); }
        File.WriteAllText(Path.Combine(Install, "settings.json"), "private settings");
        var hashes = UpdatePackage.Payload.ToDictionary(n => n, n => UpdatePackage.Hash(Path.Combine(content, n)));
        UpdateFiles.WriteJson(Path.Combine(content, "update-manifest.json"), new PackageManifest(bad == 2 ? 99 : 1, "1.2.0", new string('a',40), hashes));
        if (bad == 3) File.WriteAllText(Path.Combine(content, "ClickForge.exe"), "corrupted");
        using (var zip = ZipFile.Open(Archive, ZipArchiveMode.Create))
        {
            foreach (string n in UpdatePackage.Payload.Append("update-manifest.json")) { if (bad == 1 && n == "ClickForge.Updater.exe") continue; var entry = zip.CreateEntry(n); if (link && n == "ClickForge.exe") entry.ExternalAttributes = 0xA000 << 16; using var input = File.OpenRead(Path.Combine(content, n)); using var output = entry.Open(); input.CopyTo(output); }
            if (extra is not null) { var entry = zip.CreateEntry(extra); using var writer = new StreamWriter(entry.Open()); writer.Write("unexpected"); }
        }
        Bytes = File.ReadAllBytes(Archive); Checksum = UpdatePackage.Hash(Archive);
    }
    public string Json(bool draft = false, bool prerelease = false) => JsonSerializer.Serialize(new { id = 1, draft, prerelease, published_at = "2026-10-23T00:00:00Z", tag_name = "v1.2.0", body = "Actual release notes", assets = new[] { new { id=2, name=Name, size=(long)Bytes.Length, state="uploaded", browser_download_url=$"https://github.com/{UpdateChannel.Repository}/releases/download/v1.2.0/{Name}" }, new { id=3, name=Name+".sha256", size=100L, state="uploaded", browser_download_url=$"https://github.com/{UpdateChannel.Repository}/releases/download/v1.2.0/{Name}.sha256" } } });
    public static HttpResponseMessage Response(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    public UpdateClient Client(string? metadata = null, bool truncate = false, bool block = false)
    {
        var h = new Handler((r, _) => Task.FromResult(r.RequestUri == UpdateChannel.Latest ? Response(metadata ?? Json()) : r.RequestUri!.AbsolutePath.EndsWith("/3") ? Response(Checksum + "  " + Name) : new HttpResponseMessage(HttpStatusCode.OK) { Content = block ? BlockContent() : new ByteArrayContent(truncate ? Bytes[..(Bytes.Length/2)] : Bytes) }));
        return new(new HttpClient(h), Updates, Package);
    }
    private HttpContent BlockContent() { var content = new StreamContent(new BlockingStream()); content.Headers.ContentLength = Bytes.Length; return content; }
    public InstallRequest Prepare()
    {
        string work = Path.Combine(Updates, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); File.Copy(Archive, Path.Combine(work, "package.zip")); Package.ExtractAndVerify(Archive, Path.Combine(work,"staging"), SemanticVersion.Parse("1.2.0"), default);
        return new(work, Install, 1, 1, new string('a',64), "1.2.0", Checksum);
    }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
sealed class Platform : IInstallPlatform
{
    public TaskCompletionSource Ready { get; } = new(); public TaskCompletionSource? Wait; public bool Confirm = true, ParentFailure, Previous, Confirmed; public Action? BeforeConfirm;
    public async Task WaitForParentExitAsync(InstallRequest request, Action ready, CancellationToken token) { if (ParentFailure) throw new TimeoutException("parent remains running"); ready(); Ready.TrySetResult(); if (Wait is not null) await Wait.Task.WaitAsync(token); }
    public Task<bool> StartAndConfirmAsync(string executable, string resultPath, string nonce, CancellationToken token) { BeforeConfirm?.Invoke(); Confirmed = true; return Task.FromResult(Confirm); }
    public void StartPrevious(string executable, string resultPath, string nonce) => Previous = true;
}
sealed class NoMouse : IMouseInput { public void Click() => throw new Exception("must not click"); public void Release() { } }
sealed class NoTarget : ITargetGate { public bool CanClick => false; }
static class JsonNodeHelper { public static string DuplicateAsset(string json) { var node = System.Text.Json.Nodes.JsonNode.Parse(json)!; var a = node["assets"]!.AsArray(); a.Add(a[0]!.DeepClone()); return node.ToJsonString(); } }
