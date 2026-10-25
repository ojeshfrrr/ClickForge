param(
    [Parameter(Mandatory)][string]$ZipPath,
    [string]$OutputDirectory = 'artifacts',
    [string]$Version,
    [string]$TestIdentity,
    [switch]$Strict
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$root = (Get-Location).Path
$central = ([xml](Get-Content Version.props -Raw)).Project.PropertyGroup.ClickForgeVersion
if (-not $Version) { $Version = $central }
if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Invalid installer version' }
if ($Version -cne $central -and (-not $TestIdentity -or $Strict)) { throw 'Historical payloads require an explicitly isolated test identity' }
if ($Strict -and $TestIdentity) { throw 'Test identity cannot be published' }
if ($TestIdentity -and $TestIdentity -cnotmatch '^[0-9a-f]{32}$') { throw 'Test identity must be a lowercase GUID without separators' }
$zip = [IO.Path]::GetFullPath($ZipPath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $output | Out-Null
$installer = Join-Path $output "ClickForge-Setup-v$Version.exe"
if (Test-Path -LiteralPath $installer) { throw 'Refusing to overwrite an installer. Choose a fresh output directory.' }
$work = Join-Path $root ('artifacts/installer-work/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
$payload = Join-Path $work 'payload'
# Reuse the same production archive verifier as the portable packaging pipeline.
dotnet run --project tests/ClickForge.UpdateTests -c Release -- --verify-package $zip $Version $payload
if ($LASTEXITCODE) { throw 'Installer payload validation failed' }
$manifest = Get-Content -LiteralPath (Join-Path $payload 'update-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.Version -cne $Version) { throw 'Installer/payload version mismatch' }
if ($Strict) {
    $commit = (git rev-parse HEAD).Trim()
    if ((git status --porcelain) -or $manifest.SourceCommit -cne $commit -or $env:GITHUB_SHA -cne $commit -or $env:GITHUB_REF_NAME -cne "v$Version") { throw 'Strict installer builds require the clean matching tagged source' }
}
$entries = @($manifest.Files.PSObject.Properties.Name) + @('update-manifest.json')
$lines = foreach ($name in $entries) {
    if ($name -match '[";\r\n]' -or [IO.Path]::IsPathRooted($name)) { throw 'Unsafe installer source entry' }
    $relative = $name.Replace('/','\'); $parent = Split-Path $relative -Parent
    $destination = if ($parent) { '{app}\' + $parent } else { '{app}' }
    'Source: "' + (Join-Path $payload $relative) + '"; DestDir: "' + $destination + '"; Flags: ignoreversion'
}
$include = Join-Path $work 'PayloadFiles.iss'
[IO.File]::WriteAllLines($include,[string[]]$lines,[Text.UTF8Encoding]::new($true))
$compiler = & ./scripts/Get-InnoSetup.ps1
$v = [Version]$Version
$arguments = @('/Qp',"/DAppVersion=$Version",('/DVersionMS=' + (($v.Major -shl 16) -bor $v.Minor)),('/DVersionLS=' + ($v.Build -shl 16)),"/DPayloadFiles=$include",('/DAppIcon=' + (Join-Path $root 'src/ClickForge/Assets/ClickForge.ico')),"/DOutputDirectory=$output")
if ($TestIdentity) { $arguments += "/DInstallerId=ClickForge.Test.$TestIdentity"; $arguments += "/DDisplayName=ClickForge Test $TestIdentity" }
& $compiler @arguments (Join-Path $root 'installer/ClickForge.iss')
if ($LASTEXITCODE -or -not (Test-Path -LiteralPath $installer)) { throw 'Installer compilation failed' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($installer)
if ($info.FileMajorPart -ne $v.Major -or $info.FileMinorPart -ne $v.Minor -or $info.FileBuildPart -ne $v.Build -or $info.FilePrivatePart -ne 0 -or $info.ProductVersion.Trim() -cne $Version) { throw 'Setup executable version mismatch' }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n", [Text.Encoding]::ASCII)
@{Version=$Version;SourceCommit=$manifest.SourceCommit;PortableZip=[IO.Path]::GetFileName($zip);PortableSha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant();InstallerSha256=$hash;Files=$entries;TestIdentity=$TestIdentity;Compiler='Inno Setup 6.7.3'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $work 'installer-build.json')
Write-Output "Installer: $installer"
Write-Output "SHA-256: $hash"
