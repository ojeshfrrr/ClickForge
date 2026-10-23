$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force artifacts/logs | Out-Null
dotnet restore src/ClickForge/ClickForge.csproj 2>&1 | Tee-Object artifacts/logs/restore.txt
if ($LASTEXITCODE) { throw 'Restore failed' }
dotnet build src/ClickForge/ClickForge.csproj -c Release --no-restore -o artifacts/ui-build 2>&1 | Tee-Object artifacts/logs/build.txt
if ($LASTEXITCODE) { throw 'Build failed' }
dotnet run --project tests/ClickForge.Tests -c Release 2>&1 | Tee-Object artifacts/logs/tests.txt
if ($LASTEXITCODE) { throw 'Core tests failed' }
$uiProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'artifacts/ui-build/ClickForge.exe') -ArgumentList '--ui-self-test', ('"' + (Join-Path $PSScriptRoot 'artifacts/logs/ui-tests.txt') + '"') -WindowStyle Hidden -PassThru
if (-not $uiProcess.WaitForExit(30000)) { throw 'UI checks timed out' }
if ($uiProcess.ExitCode) { throw 'UI checks failed; see artifacts/logs/ui-tests.txt' }
dotnet publish src/ClickForge/ClickForge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/release 2>&1 | Tee-Object artifacts/logs/publish.txt
if ($LASTEXITCODE) { throw 'Publish failed' }
