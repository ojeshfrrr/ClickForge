param([Parameter(Mandatory)][string]$ZipPath, [string]$HistoricalZip)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$root = (Get-Location).Path
$id = [Guid]::NewGuid().ToString('N')
$name = "ClickForge Test $id"
$work = Join-Path $root "artifacts/installer-tests/$id"
New-Item -ItemType Directory -Force $work | Out-Null
$version = ([xml](Get-Content Version.props -Raw)).Project.PropertyGroup.ClickForgeVersion
$registration = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClickForge.Test.${id}_is1"
$install = Join-Path $env:LOCALAPPDATA "Programs/$name"
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) "$name.lnk"
$start = Join-Path ([Environment]::GetFolderPath('Programs')) "$name/$name.lnk"
$shell = New-Object -ComObject WScript.Shell
$results = [Collections.Generic.List[string]]::new()
$running = $null
function Check([bool]$condition,[string]$message) { if (-not $condition) { throw $message }; $results.Add("PASS $message"); Write-Output "PASS $message" }
function RunSetup([string]$exe,[string[]]$extra=@()) {
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/LOG="'+(Join-Path $work ('setup-'+[Guid]::NewGuid().ToString('N')+'.log'))+'"')) + $extra
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $p.WaitForExit(60000)) { throw 'Installer process timed out; no force termination attempted' }
    return $p.ExitCode
}
function LinkedHere([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    $link=$shell.CreateShortcut($path)
    return $link.TargetPath -eq (Join-Path $install 'ClickForge.exe') -and $link.WorkingDirectory.TrimEnd('\') -eq $install.TrimEnd('\') -and $link.IconLocation -eq ((Join-Path $install 'ClickForge.exe')+',0')
}
function VerifyPayload {
    foreach($file in $manifest.Files.PSObject.Properties) {
        if((Get-FileHash -LiteralPath (Join-Path $install $file.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.Value) { throw "Installed payload mismatch: $($file.Name)" }
    }
    if ((Get-FileHash -LiteralPath (Join-Path $install 'update-manifest.json') -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath (Join-Path $testPayload 'update-manifest.json') -Algorithm SHA256).Hash) { throw 'Installed manifest changed' }
    Check $true 'Installed application/helper/runtime notices match every verified package hash'
}
$dataHashes=@{}
$data=Join-Path $env:LOCALAPPDATA 'ClickForge'
foreach($path in @((Join-Path $data 'settings.json'))+@(Get-ChildItem -LiteralPath (Join-Path $data 'Profiles') -File -Filter '*.json' -ErrorAction SilentlyContinue | ForEach-Object FullName)) {
    if(Test-Path -LiteralPath $path){$dataHashes[$path]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}
try {
    if (-not $HistoricalZip) {
        $HistoricalZip = Join-Path $root 'artifacts/installer-tests/fixtures/ClickForge-v1.1.2-win-x64.zip'
        New-Item -ItemType Directory -Force (Split-Path $HistoricalZip -Parent) | Out-Null
        if (-not (Test-Path -LiteralPath $HistoricalZip)) { Invoke-WebRequest 'https://github.com/ojeshfrrr/ClickForge/releases/download/v1.1.2/ClickForge-v1.1.2-win-x64.zip' -OutFile $HistoricalZip }
    }
    Check ((Get-FileHash -LiteralPath $HistoricalZip -Algorithm SHA256).Hash.ToLowerInvariant() -ceq '9f2db6cbf89ea922bc99406aba903c8d78d52a2d2b3bfd8c6d6c7b7d0a25df36') 'Historical upgrade fixture matches the actual published v1.1.2 ZIP'
    & ./scripts/Build-Installer.ps1 -ZipPath $HistoricalZip -Version 1.1.2 -TestIdentity $id -OutputDirectory (Join-Path $work 'old') | Out-Host
    & ./scripts/Build-Installer.ps1 -ZipPath $ZipPath -TestIdentity $id -OutputDirectory (Join-Path $work 'current') | Out-Host
    $old = Join-Path $work 'old/ClickForge-Setup-v1.1.2.exe'
    $setup = Join-Path $work "current/ClickForge-Setup-v$version.exe"
    $testPayload = Join-Path $work 'verified-payload'
    dotnet run --project tests/ClickForge.UpdateTests -c Release -- --verify-package ([IO.Path]::GetFullPath($ZipPath)) $version $testPayload
    if ($LASTEXITCODE) { throw 'Lifecycle-test payload verification failed' }
    $manifest = Get-Content -LiteralPath (Join-Path $testPayload 'update-manifest.json') -Raw | ConvertFrom-Json
    $foreign=Join-Path $work 'foreign'; New-Item -ItemType Directory -Force $foreign | Out-Null
    [IO.File]::WriteAllText((Join-Path $foreign 'user-file.txt'),'unrelated file')
    Check ((RunSetup $old @('/DIR="'+$foreign+'"')) -ne 0 -and [IO.File]::ReadAllText((Join-Path $foreign 'user-file.txt')) -eq 'unrelated file' -and -not(Test-Path -LiteralPath (Join-Path $foreign 'ClickForge.exe'))) 'Setup rejects an unrelated nonempty destination without overwriting files'
    Check ((RunSetup $old) -eq 0) 'Actual clean per-user installation succeeds without elevation'
    $entry=Get-ItemProperty -LiteralPath $registration
    Check ($entry.InstallLocation.TrimEnd('\') -eq $install.TrimEnd('\') -and $entry.DisplayVersion -eq '1.1.2' -and $entry.DisplayName -eq $name -and $entry.ClickForgeInstallerId -eq "ClickForge.Test.$id") 'Installed Apps registration retains ownership, stable per-user path and version'
    Check ((LinkedHere $desktop) -and (LinkedHere $start)) 'Default Desktop and Start menu shortcuts point to installed EXE, working directory and native icon'
    Check (-not(Get-CimInstance Win32_Process -Filter "Name='ClickForge.exe'" | Where-Object ExecutablePath -eq (Join-Path $install 'ClickForge.exe'))) 'Silent installation never launches ClickForge or starts clicking'
    [IO.File]::WriteAllText((Join-Path $install 'unrelated-user-file.txt'),'preserve unrelated user file')
    Check ((RunSetup $setup @('/DIR="'+(Join-Path $work 'other-location')+'"')) -ne 0 -and (Get-ItemProperty -LiteralPath $registration).InstallLocation.TrimEnd('\') -eq $install.TrimEnd('\')) 'Reinstallation cannot silently move an existing installation to another folder'
    Check ((RunSetup $setup) -eq 0) 'Actual upgrade from 1.1.2 to the current candidate succeeds'
    VerifyPayload
    Check ((Get-ItemProperty -LiteralPath $registration).DisplayVersion -eq $version -and (LinkedHere $desktop) -and (LinkedHere $start)) 'Upgrade updates version without duplicate shortcut paths'
    Check ((RunSetup $old) -ne 0) 'Downgrade over a newer installed executable is refused'
    VerifyPayload
    Check ((RunSetup $setup @('/TASKS=desktopicon')) -eq 0 -and (LinkedHere $desktop) -and -not(Test-Path -LiteralPath $start)) 'Start menu opt-out removes only the owned Start shortcut and keeps Desktop integration'
    Check ((RunSetup $setup @('/TASKS=startmenuicon')) -eq 0 -and -not(Test-Path -LiteralPath $desktop) -and (LinkedHere $start)) 'Reinstallation honors explicit Desktop opt-out and retains Start menu integration'
    Check ((RunSetup $setup) -eq 0 -and -not(Test-Path -LiteralPath $desktop)) 'Remembered Desktop opt-out is not silently reversed by reinstallation'
    $link=$shell.CreateShortcut($desktop); $link.TargetPath=Join-Path $env:WINDIR 'System32/notepad.exe'; $link.Save()
    Check ((RunSetup $setup @('/TASKS=desktopicon,startmenuicon')) -ne 0 -and $shell.CreateShortcut($desktop).TargetPath -eq (Join-Path $env:WINDIR 'System32/notepad.exe')) 'Setup refuses to overwrite an unrelated same-name Desktop shortcut'
    $uninstaller=Join-Path $install 'unins000.exe'
    # This is the GUI lifecycle probe, not a background helper. A hidden window
    # has no Process.MainWindowHandle on some Windows runners, so CloseMainWindow
    # cannot request shutdown even though the application's guard already exists.
    $running = Start-Process -FilePath (Join-Path $install 'ClickForge.exe') -ArgumentList '--ui-interactive-test',('"'+(Join-Path $work 'manual-test.txt')+'"') -WindowStyle Normal -PassThru
    $clock=[Diagnostics.Stopwatch]::StartNew(); $guard=$null
    while($clock.Elapsed.TotalSeconds -lt 15 -and -not $guard) { try{$guard=[Threading.Mutex]::OpenExisting('Local\ClickForge.SetupGuard')} catch [Threading.WaitHandleCannotBeOpenedException] {Start-Sleep -Milliseconds 100} }
    if($guard){$guard.Dispose()}
    Check (-not $running.HasExited -and $null -ne $guard) 'Installed application exposes its lifetime guard with mocked input and disposable settings'
    $inputReady = $running.WaitForInputIdle(15000)
    $clock.Restart()
    do { $running.Refresh(); if($running.HasExited -or $running.MainWindowHandle -ne 0){break}; Start-Sleep -Milliseconds 100 } while($clock.Elapsed.TotalSeconds -lt 15)
    Check ($inputReady -and -not $running.HasExited -and $running.MainWindowHandle -ne 0) 'Installed GUI is ready and exposes a visible main window before shutdown checks'
    Check ((RunSetup $setup) -ne 0 -and -not $running.HasExited) 'Setup refuses replacement while ClickForge is running without force-closing it'
    Check ((RunSetup $uninstaller) -ne 0 -and -not $running.HasExited -and (Test-Path -LiteralPath $registration)) 'Uninstall refuses a running application without killing it or removing registration'
    Check ($running.CloseMainWindow()) 'Installed GUI accepts the normal close request'
    Check ($running.WaitForExit(10000)) 'Installed application closes through normal coordinated shutdown'; $running=$null
    $report=Join-Path $work 'installed-ui-tests.txt'
    $p=Start-Process -FilePath (Join-Path $install 'ClickForge.exe') -ArgumentList '--ui-self-test',('"'+$report+'"') -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(45000)){throw 'Installed UI test timed out'}
    Check ($p.ExitCode -eq 0 -and (Select-String -LiteralPath $report -SimpleMatch 'RESULT UI checks completed successfully' -Quiet) -and (Select-String -LiteralPath $report -SimpleMatch 'PASS Startup never enables clicking' -Quiet)) 'Actual installed app passes WPF regressions and explicitly starts disabled (mouse mocked)'
    $shortcutHash=(Get-FileHash -LiteralPath $start -Algorithm SHA256).Hash
    & ./scripts/Test-UpdateProcess.ps1 -PayloadDirectory $testPayload -InstallRoot (Join-Path $env:LOCALAPPDATA "Programs/ClickForge Update Test $id") | Out-Host
    Check ($shortcutHash -eq (Get-FileHash -LiteralPath $start -Algorithm SHA256).Hash -and (LinkedHere $start)) 'Real helper success/rollback in a per-user Programs directory leave installed shortcut targets unchanged'
    Check ((RunSetup $uninstaller) -eq 0) 'Actual uninstall succeeds after coordinated application exit'
    $clock.Restart(); while((Test-Path -LiteralPath $registration) -and $clock.Elapsed.TotalSeconds -lt 15){Start-Sleep -Milliseconds 100}
    Check (-not(Test-Path -LiteralPath $registration) -and -not(Test-Path -LiteralPath (Join-Path $install 'ClickForge.exe')) -and -not(Test-Path -LiteralPath (Join-Path $install 'ClickForge.Updater.exe')) -and -not(Test-Path -LiteralPath $start)) 'Uninstall removes managed executables, owned Start menu shortcut and Installed Apps entry'
    Check ((Test-Path -LiteralPath $desktop) -and [IO.File]::ReadAllText((Join-Path $install 'unrelated-user-file.txt')) -eq 'preserve unrelated user file') 'Uninstall preserves an unrelated shortcut and unrelated user files'
    foreach($path in $dataHashes.Keys){if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $dataHashes[$path]){throw 'Personal configuration changed during installer tests'}}
    Check $true 'Existing personal settings and profile files remain byte-identical across installation, upgrade and uninstall'
    $results.Add("RESULT $($results.Count) installer lifecycle checks passed")
}
catch { $results.Add('FAIL '+$_); throw }
finally {
    $results | Set-Content -LiteralPath (Join-Path $work 'results.txt')
    Write-Output "Installer test report: $work"
    if($running -and -not $running.HasExited){$null=$running.CloseMainWindow();$null=$running.WaitForExit(10000)}
    # Only the test-created foreign shortcut is cleaned here; user shortcuts are
    # never enumerated or deleted. Logs and any failed fixtures remain for audit.
    if((Test-Path -LiteralPath $desktop) -and $desktop.Contains($id)){Remove-Item -LiteralPath $desktop}
}
