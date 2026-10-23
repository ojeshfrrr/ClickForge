param([string]$PayloadDirectory = 'artifacts/updater/local-package/payload')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$repoRoot = (Get-Location).Path
New-Item -ItemType Directory -Force artifacts/updater/logs | Out-Null
$payload = Join-Path $repoRoot $PayloadDirectory
if (-not (Test-Path (Join-Path $payload 'ClickForge.Updater.exe'))) { throw 'Build the local updater package first' }
 $newVersion = (Get-Content (Join-Path $payload 'update-manifest.json') -Raw | ConvertFrom-Json).Version
foreach ($version in @('0.0.0',$newVersion)) {
    dotnet publish tests/ClickForge.Updater.Fixture/ClickForge.Updater.Fixture.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None "-p:Version=$version" -o "artifacts/updater/fixture-$version"
    if ($LASTEXITCODE) { throw 'Fixture build failed' }
}
$updateRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ClickForge/Updates'
New-Item -ItemType Directory -Force $updateRoot | Out-Null
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $repoRoot 'artifacts/runtime-cache'
$results = @()
foreach ($failure in @($false,$true)) {
    $id = [Guid]::NewGuid().ToString('N')
    $work = Join-Path $updateRoot $id
    $install = Join-Path $repoRoot "artifacts/updater/process-tests/$id/installed"
    New-Item -ItemType Directory -Force $work,$install,(Join-Path $work 'staging') | Out-Null
    Copy-Item -Path (Join-Path $payload '*') -Destination (Join-Path $work 'staging') -Recurse
    Copy-Item -LiteralPath "artifacts/updater/fixture-$newVersion/ClickForge.exe" -Destination (Join-Path $work 'staging/ClickForge.exe')
    Copy-Item -LiteralPath 'artifacts/updater/fixture-0.0.0/ClickForge.exe' -Destination (Join-Path $install 'ClickForge.exe')
    Copy-Item -LiteralPath (Join-Path $payload 'ClickForge.Updater.exe') -Destination (Join-Path $install 'ClickForge.Updater.exe')
    [IO.File]::WriteAllText((Join-Path $install 'settings.json'), 'keep-my-settings')
    if ($failure) { [IO.File]::WriteAllText((Join-Path $install 'fail-start.marker'), 'test fixture only') }
    $manifest = Get-Content (Join-Path $work 'staging/update-manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $manifest.Files.PSObject.Properties) { $file.Value = (Get-FileHash -LiteralPath (Join-Path $work "staging/$($file.Name)") -Algorithm SHA256).Hash.ToLowerInvariant() }
    [IO.File]::WriteAllText((Join-Path $work 'staging/update-manifest.json'), ($manifest | ConvertTo-Json -Depth 4))
    [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $work 'staging'),(Join-Path $work 'package.zip'))
    Copy-Item -LiteralPath (Join-Path $payload 'ClickForge.Updater.exe') -Destination (Join-Path $work 'runner.exe')
    $oldHash = (Get-FileHash (Join-Path $install 'ClickForge.exe') -Algorithm SHA256).Hash
    $parent = Start-Process -FilePath (Join-Path $install 'ClickForge.exe') -ArgumentList '--parent' -WindowStyle Hidden -PassThru
    $nonce = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
    $request = [ordered]@{ Workspace=$work; InstallDirectory=$install; ParentPid=$parent.Id; ParentStartUtcTicks=$parent.StartTime.ToUniversalTime().Ticks; Nonce=$nonce; ExpectedVersion=$newVersion; ArchiveHash=(Get-FileHash (Join-Path $work 'package.zip') -Algorithm SHA256).Hash.ToLowerInvariant() }
    $requestPath = Join-Path $work 'request.json'
    [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json))
    $helper = Start-Process -FilePath (Join-Path $work 'runner.exe') -ArgumentList '--request', ('"' + $requestPath + '"') -WindowStyle Hidden -PassThru
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path (Join-Path $work 'ready.json'))) {
        if ($helper.HasExited -or $deadline.Elapsed.TotalSeconds -gt 20) { throw "Helper did not become ready: $work" }
        Start-Sleep -Milliseconds 100
    }
    if ((Get-FileHash (Join-Path $install 'ClickForge.exe') -Algorithm SHA256).Hash -ne $oldHash) { throw 'Helper replaced a running parent' }
    [IO.File]::WriteAllText((Join-Path $install 'parent-exit'), 'exit fixture cleanly')
    if (-not $helper.WaitForExit(45000)) { throw 'Helper did not finish' }
    $receipt = Get-Content (Join-Path $work 'result.json') -Raw | ConvertFrom-Json
    if ($failure) {
        if ($receipt.Status -ne 'Failed' -or (Get-FileHash (Join-Path $install 'ClickForge.exe') -Algorithm SHA256).Hash -ne $oldHash) { throw 'Rollback failed' }
        $marker = Join-Path $install 'previous-started.txt'
    } else {
        if ($receipt.Status -ne 'Succeeded' -or -not (Test-Path (Join-Path $install 'new-started.txt'))) { throw 'Installation/startup acknowledgement failed' }
        $marker = Join-Path $install 'new-started.txt'
    }
    $wait = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path $marker) -and $wait.Elapsed.TotalSeconds -lt 5) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path $marker)) { throw 'Expected fixture was not restarted' }
    if ([IO.File]::ReadAllText((Join-Path $install 'settings.json')) -ne 'keep-my-settings') { throw 'Settings changed' }
    $results += "PASS real helper process: $(if ($failure) { 'failed startup rolls back and restarts previous fixture' } else { 'waits for parent, replaces files, restarts and confirms new fixture' }); settings preserved"
}
$results | Set-Content -LiteralPath artifacts/updater/logs/process-tests.txt
$results
