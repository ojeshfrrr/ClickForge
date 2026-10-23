param([string]$OutputDirectory = 'artifacts/package', [switch]$Strict)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$repoRoot = (Get-Location).Path
$version = ([xml](Get-Content -LiteralPath Version.props -Raw)).Project.PropertyGroup.ClickForgeVersion
if ($version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Release version must be stable MAJOR.MINOR.PATCH' }
$commit = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -or $commit -cnotmatch '^[0-9a-f]{40}$') { throw 'A Git checkout with a source commit is required for packaging' }
if ($Strict) {
    if (git status --porcelain) { throw 'Release packaging requires a clean checkout' }
    if ($env:GITHUB_REF_NAME -cne "v$version" -or $env:GITHUB_SHA -cne $commit) { throw 'Tag, version and checked-out commit must match' }
}
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($project in @('src/ClickForge/ClickForge.csproj','src/ClickForge.Updater/ClickForge.Updater.csproj')) {
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE) { throw "Publish failed: $project" }
}
foreach ($exe in @('ClickForge.exe','ClickForge.Updater.exe')) {
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $output $exe))
    if ($info.ProductVersion -cne "$version+$commit") { throw "$exe version/source commit mismatch: $($info.ProductVersion)" }
}
$assets = Get-Content -LiteralPath src/ClickForge/obj/project.assets.json -Raw | ConvertFrom-Json
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$dependencies = @($assets.project.frameworks.PSObject.Properties.Value | ForEach-Object { $_.downloadDependencies })
function RuntimePath([string]$name) {
    $dependency = $dependencies | Where-Object { $_.name -ieq $name } | Select-Object -First 1
    if (-not $dependency) { throw "Runtime dependency version not found: $name" }
    $runtimeVersion = $dependency.version.Trim('[',']').Split(',')[0].Trim()
    return Join-Path $packageRoot ($name.ToLowerInvariant() + '/' + $runtimeVersion)
}
$coreRuntime = RuntimePath 'Microsoft.NETCore.App.Runtime.win-x64'
$desktopRuntime = RuntimePath 'Microsoft.WindowsDesktop.App.Runtime.win-x64'
New-Item -ItemType Directory -Force (Join-Path $output 'licenses') | Out-Null
Copy-Item -LiteralPath (Join-Path $coreRuntime 'LICENSE.TXT') -Destination (Join-Path $output 'licenses/DOTNET-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $coreRuntime 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $output 'licenses/DOTNET-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $desktopRuntime 'LICENSE') -Destination (Join-Path $output 'licenses/WINDOWSDESKTOP-LICENSE.txt')
$readme = @"
ClickForge v$version — portable Windows x64

Extract the entire ZIP to a folder you own, then open ClickForge.exe.
Keep ClickForge.Updater.exe next to it. No separate .NET installation is needed.
Windows 11 x64 is recommended. Windows 10 support follows the .NET 10 OS matrix.
The application and helper are unsigned. Do not disable Windows security tools.

F6: Toggle/hold activation. F7: Pause/resume. F8: Emergency stop.
Clicking starts disabled. Roblox foreground restriction is on by default.
Variable CPS requires an explicitly selected window; verified Roblox clients are supported.
Variable Timing in Safe Click Test remains an isolated logical simulation.
Some Roblox experiences prohibit autoclickers and may enforce account penalties.

Open Updates to check for a stable release, download/verify it, and confirm
installation/restart. Startup checks are optional and never install silently.
Settings remain in %LOCALAPPDATA%/ClickForge/settings.json.
An interrupted update retains a recovery journal and backup under that folder's
Updates subdirectory. See docs/UPDATING.md in the source repository for recovery.
A checksum from the release account is not a digital publisher signature.

Source: https://github.com/ojeshfrrr/ClickForge
Source commit: $commit
No ClickForge source license has been selected. licenses/ covers bundled runtimes.
"@
[IO.File]::WriteAllText((Join-Path $output 'README.txt'), $readme)
$payload = @('ClickForge.exe','ClickForge.Updater.exe','README.txt','licenses/DOTNET-LICENSE.txt','licenses/DOTNET-THIRD-PARTY-NOTICES.txt','licenses/WINDOWSDESKTOP-LICENSE.txt')
$hashes = [ordered]@{}
foreach ($file in $payload) { $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $output $file) -Algorithm SHA256).Hash.ToLowerInvariant() }
$manifest = [ordered]@{ FormatVersion=1; Version=$version; SourceCommit=$commit; Files=$hashes }
[IO.File]::WriteAllText((Join-Path $output 'update-manifest.json'), ($manifest | ConvertTo-Json -Depth 4))
$name = "ClickForge-v$version-win-x64.zip"
$zip = Join-Path (Split-Path $output -Parent) $name
if (Test-Path -LiteralPath $zip) { throw 'Refusing to overwrite an existing package. Choose a fresh output directory.' }
# Explicit files only: never package an output folder indiscriminately.
$zipStream = [IO.File]::Create($zip)
$archive = [IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($payload) + 'update-manifest.json') {
        $entry = $archive.CreateEntry($file,[IO.Compression.CompressionLevel]::Optimal)
        $source = [IO.File]::OpenRead((Join-Path $output $file)); $destination = $entry.Open()
        try { $source.CopyTo($destination) } finally { $source.Dispose(); $destination.Dispose() }
    }
} finally { $archive.Dispose(); $zipStream.Dispose() }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $name`n", [Text.Encoding]::ASCII)
dotnet run --project tests/ClickForge.UpdateTests -c Release -- --verify-package $zip $version
if ($LASTEXITCODE) { throw 'Package validation failed' }
Write-Output "Package: $zip"
Write-Output "SHA-256: $hash"
