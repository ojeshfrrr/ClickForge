param([switch]$SkipPackage)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force artifacts/logs | Out-Null
foreach ($project in @('src/ClickForge/ClickForge.csproj','src/ClickForge.Updater/ClickForge.Updater.csproj')) {
    dotnet restore $project
    if ($LASTEXITCODE) { throw "Restore failed: $project" }
    dotnet build $project -c Release --no-restore -o artifacts/ui-build
    if ($LASTEXITCODE) { throw "Build failed: $project" }
}
foreach ($tests in @('tests/ClickForge.Tests','tests/ClickForge.UpdateTests')) {
    dotnet run --project $tests -c Release
    if ($LASTEXITCODE) { throw "Tests failed: $tests" }
}
$uiProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'artifacts/ui-build/ClickForge.exe') -ArgumentList '--ui-self-test', ('"' + (Join-Path $PSScriptRoot 'artifacts/logs/ui-tests.txt') + '"') -WindowStyle Hidden -PassThru
if (-not $uiProcess.WaitForExit(40000)) { throw 'UI checks timed out' }
Get-Content -LiteralPath artifacts/logs/ui-tests.txt | Write-Output
if ($uiProcess.ExitCode) { throw 'UI checks failed; see artifacts/logs/ui-tests.txt' }
if (-not $SkipPackage) { & ./scripts/Package-Release.ps1 }
