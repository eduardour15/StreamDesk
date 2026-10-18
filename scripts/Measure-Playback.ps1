[CmdletBinding()]
param(
    [ValidateRange(1, 3600)][int]$DurationSeconds = 60,
    [ValidateRange(1, 30)][int]$IntervalSeconds = 1,
    [int]$TargetProcessId = 0
)

$ErrorActionPreference = 'Stop'
if ($TargetProcessId -eq 0) {
    $players = @(Get-Process -Name 'StreamDesk.Probe.Desktop' -ErrorAction SilentlyContinue)
    if ($players.Count -ne 1) { throw 'Abre una sola instancia de StreamDesk o indica -TargetProcessId.' }
    $TargetProcessId = $players[0].Id
}
$player = Get-Process -Id $TargetProcessId
if ($player.ProcessName -ne 'StreamDesk.Probe.Desktop') { throw 'El proceso debe corresponder al ejecutable StreamDesk.' }
$directory = Join-Path $PSScriptRoot '../artifacts/metrics'
$null = New-Item -ItemType Directory -Path $directory -Force
$baseName = 'playback-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$csvPath = Join-Path $directory ($baseName + '.csv')
$jsonPath = Join-Path $directory ($baseName + '.json')
$processors = [Environment]::ProcessorCount
$cpuModel = @('NO_DISPONIBLE'); $gpuModel = @('NO_DISPONIBLE')
try { $cpuModel = @((Get-CimInstance Win32_Processor).Name) } catch { }
try { $gpuModel = @((Get-CimInstance Win32_VideoController).Name) } catch { }
$metadata = [ordered]@{
    CapturedAtUtc = [DateTime]::UtcNow.ToString('O')
    OperatingSystem = [Environment]::OSVersion.VersionString
    CpuModel = $cpuModel
    GpuModel = $gpuModel
    LogicalProcessors = $processors
    ProcessId = $TargetProcessId
    CpuMeasure = 'Delta TotalProcessorTime / elapsed / logical processors; percent of total CPU capacity.'
    GpuMeasure = 'Maximum GPU Engine counter for this PID; null if counters are unavailable. Not total GPU utilization.'
    Source = 'Live process sampling; channel/first-frame timings and codec must be recorded separately.'
}
$metadata | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$watch = [Diagnostics.Stopwatch]::StartNew()
$previousCpu = $player.TotalProcessorTime.TotalSeconds
$previousElapsed = 0.0
$counterAvailable = $true
while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
    Start-Sleep -Seconds $IntervalSeconds
    try { $player.Refresh(); if ($player.HasExited) { break } } catch { break }
    $elapsed = $watch.Elapsed.TotalSeconds
    $cpuSeconds = $player.TotalProcessorTime.TotalSeconds
    $cpuPercent = 100.0 * ($cpuSeconds - $previousCpu) / ($elapsed - $previousElapsed) / $processors
    $gpuPeak = $null
    if ($counterAvailable) {
        try {
            $counter = Get-Counter -Counter ("\GPU Engine(pid_{0}_*)\Utilization Percentage" -f $TargetProcessId) -ErrorAction Stop
            $values = @($counter.CounterSamples | Where-Object { $_.Status -eq 0 } | ForEach-Object { $_.CookedValue })
            if ($values.Count -gt 0) { $gpuPeak = [Math]::Round(($values | Measure-Object -Maximum).Maximum, 2) }
        } catch { $counterAvailable = $false }
    }
    [pscustomobject]@{
        ElapsedSeconds = [Math]::Round($elapsed, 2)
        CpuPercent = [Math]::Round($cpuPercent, 2)
        WorkingSetMB = [Math]::Round($player.WorkingSet64 / 1MB, 2)
        PrivateMemoryMB = [Math]::Round($player.PrivateMemorySize64 / 1MB, 2)
        GpuPeakEnginePercent = $gpuPeak
        Threads = $player.Threads.Count
        Handles = $player.HandleCount
    } | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Append -Encoding UTF8
    $previousCpu = $cpuSeconds; $previousElapsed = $elapsed
}
Write-Output "Métricas: $csvPath"
Write-Output "Contexto de hardware: $jsonPath"
Write-Output 'No se capturaron URLs, credenciales ni nombres de canales. GPU vacía significa no disponible.'
