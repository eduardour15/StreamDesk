[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Candidate)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scratch = Join-Path $repository ('artifacts\i-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $scratch | Out-Null
$candidateRoot = [IO.Path]::GetFullPath($Candidate)
function Assert-Check([bool]$condition,[string]$message) { if (-not $condition) { throw $message } }
function Invoke-Manager([string]$package,[string]$action,[string]$target,[bool]$expectSuccess=$true) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'Manage-StreamDesk.ps1') -Action $action -InstallRoot $target -SkipRegistration
    if ($expectSuccess -and $LASTEXITCODE -ne 0) { throw ('Fallo de instalador: ' + $action) }
    if (-not $expectSuccess -and $LASTEXITCODE -eq 0) { throw 'El paquete manipulado debio rechazarse.' }
}
function New-Fixture([string]$version) {
    $folder = Join-Path $scratch $version; New-Item -ItemType Directory -Path (Join-Path $folder 'app') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $folder 'app\StreamDesk.Probe.Desktop.exe'),('NOT EXECUTABLE - installer fixture ' + $version))
    Copy-Item -LiteralPath (Join-Path $repository 'packaging\Manage-StreamDesk.ps1'),(Join-Path $repository 'packaging\Launch-StreamDesk.ps1') -Destination $folder
    @{ Version=$version } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'candidate.json') -Encoding UTF8
    $inventory = @(Get-ChildItem -LiteralPath $folder -Recurse -File | ForEach-Object { [pscustomobject]@{Path=$_.FullName.Substring($folder.Length+1); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    $inventory | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'files.json') -Encoding UTF8
    return $folder
}
$first = New-Fixture '0.5.0-test.1'; $second = New-Fixture '0.5.0-test.2'; $third = New-Fixture '0.5.0-test.3'
$installRoot = Join-Path $scratch 'fixture-installation'; $sentinel = Join-Path $scratch 'personal-data.txt'; [IO.File]::WriteAllText($sentinel,'KEEP')
Invoke-Manager $first 'Install' $installRoot
Invoke-Manager $second 'Install' $installRoot
$state = Get-Content -LiteralPath (Join-Path $installRoot 'installation.json') -Raw | ConvertFrom-Json
Assert-Check ($state.CurrentVersion -eq '0.5.0-test.2' -and $state.PreviousVersion -eq '0.5.0-test.1') 'Actualizacion no conserva version anterior.'
Invoke-Manager $second 'Rollback' $installRoot
$state = Get-Content -LiteralPath (Join-Path $installRoot 'installation.json') -Raw | ConvertFrom-Json
Assert-Check ($state.CurrentVersion -eq '0.5.0-test.1') 'Rollback no activa version anterior.'
@(@{ Path='candidate.json'; Sha256=(Get-FileHash -LiteralPath (Join-Path $third 'candidate.json')).Hash },@{ Path='..\personal-data.txt'; Sha256=(Get-FileHash -LiteralPath $sentinel).Hash }) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $third 'files.json') -Encoding UTF8
Invoke-Manager $third 'Install' $installRoot $false
Assert-Check ((Get-Content -LiteralPath $sentinel -Raw) -eq 'KEEP') 'El instalador modifico datos externos.'
Invoke-Manager $first 'Uninstall' $installRoot
Assert-Check (-not (Test-Path -LiteralPath $installRoot)) 'La instalacion ficticia no se elimino.'
Assert-Check (Test-Path -LiteralPath $sentinel) 'La desinstalacion borro datos personales.'
$realRoot = Join-Path $scratch 'real'
Invoke-Manager $candidateRoot 'Install' $realRoot
$realState = Get-Content -LiteralPath (Join-Path $realRoot 'installation.json') -Raw | ConvertFrom-Json
$exe = Join-Path $realRoot ('versions\' + $realState.CurrentVersion + '\app\StreamDesk.Probe.Desktop.exe')
$temporary = Join-Path $scratch 'runtime-temp'; New-Item -ItemType Directory -Path $temporary | Out-Null
$info = New-Object Diagnostics.ProcessStartInfo
$info.FileName=$exe; $info.Arguments='--verify-runtime'; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
$info.WorkingDirectory=[IO.Path]::GetDirectoryName($exe); $info.EnvironmentVariables.Clear()
$info.EnvironmentVariables['SystemRoot']=$env:SystemRoot; $info.EnvironmentVariables['WINDIR']=$env:SystemRoot
$info.EnvironmentVariables['PATH']=(Join-Path $env:SystemRoot 'System32'); $info.EnvironmentVariables['TEMP']=$temporary; $info.EnvironmentVariables['TMP']=$temporary
$info.EnvironmentVariables['DOTNET_ROOT']=(Join-Path $scratch 'no-dotnet'); $info.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP']='0'
$process = New-Object Diagnostics.Process; $process.StartInfo=$info
$watch = [Diagnostics.Stopwatch]::StartNew(); [void]$process.Start(); $peak=0L; $cpu=0d; $samples=0
while (-not $process.WaitForExit(20)) {
    if ($watch.Elapsed.TotalSeconds -gt 30) { $process.Kill(); throw 'Timeout de runtime instalado.' }
    try { $process.Refresh(); $peak=[Math]::Max($peak,$process.WorkingSet64); $cpu=[Math]::Max($cpu,$process.TotalProcessorTime.TotalMilliseconds); $samples++ } catch { }
}
$watch.Stop(); Assert-Check ($process.ExitCode -eq 0) 'El candidato instalado no decodifica sin DOTNET_ROOT/PATH de SDK.'
$result = [pscustomobject]@{ Install='PASA'; Update='PASA_FIXTURE'; Rollback='PASA_FIXTURE'; Uninstall='PASA_FIXTURE'; TamperRejected=$true; PersonalDataPreserved=$true;
    NativeDecodeInstalled='PASA'; SdkExcludedFromPath=$true; WindowsCleanMachine='PENDIENTE'; ShortcutRegistry='PENDIENTE'; StartupAndDecodeMilliseconds=$watch.Elapsed.TotalMilliseconds;
    SampledPeakRamMiB=[Math]::Round($peak/1MB,2); SampledCpuMilliseconds=$cpu; Samples=$samples; GPU='NO_MEDIDO'; Scope='Fixture local BMP; no IPTV ni rendimiento de canales reales' }
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $scratch 'evidence.json') -Encoding UTF8
Write-Output ('Evidencia: ' + (Join-Path $scratch 'evidence.json'))
