using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ClickForge.Core.Updates;

public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Verifying, ReadyToInstall, Installing, Restarting, Failed, Unavailable }
public sealed record UpdateStatus(UpdateState State, string Message, long Downloaded = 0, long? Total = null);
public sealed record ReleaseAsset(long Id, string Name, Uri Url, long Size);
public sealed record UpdateRelease(long Id, string Tag, SemanticVersion Version, string Notes, ReleaseAsset Archive, ReleaseAsset Checksum);
public sealed record StagedUpdate(string Workspace, string Staging, string ArchiveHash, UpdateRelease Release);

public sealed class UpdateClient : IDisposable
{
    private readonly HttpClient http;
    private readonly string root;
    private readonly UpdatePackage package;
    private readonly SemaphoreSlim operation = new(1, 1);
    private UpdateStatus status = new(UpdateState.Idle, "Check for a newer stable release.");
    public event Action<UpdateStatus>? StatusChanged;
    public UpdateStatus Status => Volatile.Read(ref status);
    public UpdateRelease? Latest { get; private set; }
    public StagedUpdate? Staged { get; private set; }
    public UpdateClient(HttpClient? client = null, string? updatesRoot = null, UpdatePackage? verifier = null)
    {
        http = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClickForge-Updater/" + SemanticVersion.Installed(typeof(UpdateClient).Assembly).Value.Split('+')[0]);
        root = updatesRoot ?? UpdateChannel.UpdatesRoot; package = verifier ?? new();
    }
    public void Report(UpdateState state, string message) => Set(new(state, message));
    private void Set(UpdateStatus value) { Volatile.Write(ref status, value); StatusChanged?.Invoke(value); }
    private async Task<HttpResponseMessage> Get(Uri uri, bool api, CancellationToken token)
    {
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (api) { request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json")); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28"); }
            else if (uri.Host == "api.github.com") request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                Uri? next = response.Headers.Location; if (next is not null && !next.IsAbsoluteUri) next = new Uri(uri, next);
                response.Dispose();
                if (api || next is null || !UpdateChannel.IsAllowedRedirect(next)) throw new InvalidDataException("GitHub redirected outside the approved update hosts.");
                uri = next; continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode; response.Dispose();
                throw new HttpRequestException(code is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ? "GitHub rate limit or access restriction. Try again later." : code == HttpStatusCode.NotFound ? "No public stable release is available." : $"GitHub is unavailable (HTTP {(int)code}).", null, code);
            }
            return response;
        }
        throw new InvalidDataException("Too many download redirects.");
    }
    private async Task<byte[]> ReadSmall(Uri uri, bool api, int maximum, CancellationToken token)
    {
        using var response = await Get(uri, api, token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Update metadata is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0) { if (output.Length + read > maximum) throw new InvalidDataException("Update metadata is too large."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    public static UpdateRelease ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json); var r = document.RootElement;
        if (r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("No stable published release was returned.");
        string tag = r.GetProperty("tag_name").GetString() ?? ""; var version = SemanticVersion.Parse(tag);
        if (version.Prerelease is not null || string.IsNullOrEmpty(r.GetProperty("published_at").GetString())) throw new InvalidDataException("The release is not a published stable version.");
        string name = $"ClickForge-v{version.Value}-win-x64.zip";
        ReleaseAsset Asset(string assetName)
        {
            var matches = r.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == assetName).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"The release is missing an unambiguous {assetName} asset.");
            var a = matches[0]; var url = new Uri(a.GetProperty("browser_download_url").GetString() ?? "");
            long id = a.GetProperty("id").GetInt64(), size = a.GetProperty("size").GetInt64();
            if (id <= 0 || size <= 0 || size > UpdatePackage.MaximumArchiveBytes || a.GetProperty("state").GetString() != "uploaded" || !UpdateChannel.IsReleaseAsset(url, tag, assetName)) throw new InvalidDataException("The release asset is invalid or outside the official repository.");
            return new(id, assetName, url, size);
        }
        string notes = r.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        if (notes.Length > 12000) notes = notes[..12000] + "\n…";
        long releaseId = r.GetProperty("id").GetInt64(); if (releaseId <= 0) throw new InvalidDataException("Invalid release identity.");
        return new(releaseId, tag, version, notes, Asset(name), Asset(name + ".sha256"));
    }
    public async Task CheckAsync(SemanticVersion installed, CancellationToken token = default)
    {
        if (!await operation.WaitAsync(0, token).ConfigureAwait(false)) return;
        try
        {
            if (Status.State is UpdateState.Downloading or UpdateState.Verifying or UpdateState.Installing or UpdateState.Restarting) return;
            Set(new(UpdateState.Checking, "Checking GitHub for the latest stable release…"));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            string json = Encoding.UTF8.GetString(await ReadSmall(UpdateChannel.Latest, true, 2 * 1024 * 1024, timeout.Token).ConfigureAwait(false));
            Latest = ParseRelease(json);
            Set(Latest.Version.CompareTo(installed) > 0 ? new(UpdateState.Available, $"Version {Latest.Version} is available.") : new(UpdateState.UpToDate, $"You are up to date ({installed.Value.Split('+')[0]})."));
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { Set(new(UpdateState.Unavailable, token.IsCancellationRequested ? "Update check cancelled." : e is OperationCanceledException ? "GitHub did not respond in time. Try again later." : e.Message)); }
        catch (Exception e) when (e is JsonException or InvalidDataException or FormatException or InvalidOperationException or KeyNotFoundException or UriFormatException) { Set(new(UpdateState.Failed, "Release metadata could not be validated: " + e.Message)); }
        finally { operation.Release(); }
    }
    public async Task DownloadAsync(SemanticVersion installed, CancellationToken token = default)
    {
        if (!await operation.WaitAsync(0, token).ConfigureAwait(false)) return;
        string? workspace = null;
        try
        {
            var release = Latest ?? throw new InvalidOperationException("Check for updates first.");
            if (release.Version.CompareTo(installed) <= 0) throw new InvalidOperationException("This release is not newer than the installed version.");
            if (Status.State is UpdateState.Installing or UpdateState.Restarting) return;
            Directory.CreateDirectory(root); workspace = UpdateChannel.Workspace(root, Path.Combine(root, Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(workspace);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
            string checksum = Encoding.UTF8.GetString(await ReadSmall(UpdateChannel.Asset(release.Checksum.Id), false, 4096, ct).ConfigureAwait(false));
            string hash = UpdatePackage.ParseChecksum(checksum, release.Archive.Name);
            string archive = Path.Combine(workspace, "package.zip");
            Set(new(UpdateState.Downloading, "Downloading the verified release asset…", 0, release.Archive.Size));
            using (var response = await Get(UpdateChannel.Asset(release.Archive.Id), false, ct).ConfigureAwait(false))
            {
                if (response.Content.Headers.ContentLength is long length && length != release.Archive.Size) throw new InvalidDataException("Download size differs from release metadata.");
                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var file = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                byte[] buffer = new byte[81920]; long bytes = 0, lastReport = 0; int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    bytes += read; if (bytes > release.Archive.Size) throw new InvalidDataException("Download exceeds the advertised size.");
                    await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    if (Environment.TickCount64 - lastReport >= 100 || bytes == release.Archive.Size) { lastReport = Environment.TickCount64; Set(new(UpdateState.Downloading, "Downloading…", bytes, release.Archive.Size)); }
                }
                if (bytes != release.Archive.Size) throw new InvalidDataException("The download was truncated.");
            }
            Set(new(UpdateState.Verifying, "Verifying checksum, archive paths, file hashes and executable versions…"));
            string staging = Path.Combine(workspace, "staging");
            await Task.Run(() => { ct.ThrowIfCancellationRequested(); UpdatePackage.VerifyHash(archive, hash, ct); package.ExtractAndVerify(archive, staging, release.Version, ct); }, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            Staged = new(workspace, staging, hash, release); workspace = null;
            Set(new(UpdateState.ReadyToInstall, "Package verified. Install and restart when you are ready."));
        }
        catch (OperationCanceledException) { Set(new(UpdateState.Available, "Download cancelled or timed out. No installed files changed.")); }
        catch (HttpRequestException e) { Set(new(UpdateState.Unavailable, e.Message)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException) { Set(new(UpdateState.Failed, e.Message)); }
        finally
        {
            if (workspace is not null) { try { Directory.Delete(UpdateChannel.Workspace(root, workspace), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            operation.Release();
        }
    }
    public void Dispose() { http.Dispose(); operation.Dispose(); }
}
