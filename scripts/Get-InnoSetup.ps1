param([string]$Destination = 'artifacts/tools/inno-6.7.3')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$destinationPath = [IO.Path]::GetFullPath((Join-Path (Get-Location) $Destination))
$compiler = Join-Path $destinationPath 'ISCC.exe'
$expected = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$receipt = Join-Path $destinationPath 'clickforge-tool.json'
if (Test-Path -LiteralPath $compiler) {
    if (Test-Path -LiteralPath $receipt) {
        $record = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        if ($record.SourceSha256 -cne $expected -or $record.CompilerSha256 -cne (Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash) { throw 'Inno tool integrity check failed' }
        return $compiler
    }
}
New-Item -ItemType Directory -Force (Split-Path $destinationPath -Parent) | Out-Null
$download = Join-Path (Split-Path $destinationPath -Parent) 'innosetup-6.7.3.exe'
if (-not (Test-Path -LiteralPath $download)) {
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) { throw 'Inno Setup download SHA-256 mismatch' }
# The compiler tool is installed per-user into the ignored workspace tool directory.
$process = Start-Process -FilePath $download -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER','/NOICONS','/TASKS=""',('/DIR="' + $destinationPath + '"') -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup tool installation failed' }
@{Version='6.7.3';SourceSha256=$expected;CompilerSha256=(Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content -LiteralPath $receipt
return $compiler
