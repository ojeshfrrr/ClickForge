using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

// Disposable process-protocol fixture. No UI, mouse input or live-app references.
if (args.Length == 1 && args[0] == "--parent")
{
    var clock = Stopwatch.StartNew();
    while (!File.Exists(Path.Combine(AppContext.BaseDirectory, "parent-exit")) && clock.Elapsed < TimeSpan.FromSeconds(45)) await Task.Delay(100);
    return 0;
}
string? Value(string flag) { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
string? result = Value("--update-result"), nonce = Value("--update-nonce");
if (result is null || nonce is null) return 2;
using var document = JsonDocument.Parse(File.ReadAllText(result));
string status = document.RootElement.GetProperty("Status").GetString()!;
string version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
if (status == "Restarting")
{
    if (File.Exists(Path.Combine(AppContext.BaseDirectory, "fail-start.marker"))) return 3;
    string ack = Path.Combine(Path.GetDirectoryName(result)!, "ack.json");
    File.WriteAllText(ack + ".tmp", JsonSerializer.Serialize(new { Nonce = nonce, Version = version, Executable = Environment.ProcessPath }));
    File.Move(ack + ".tmp", ack, true);
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "new-started.txt"), version);
}
else File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "previous-started.txt"), version);
return 0;
