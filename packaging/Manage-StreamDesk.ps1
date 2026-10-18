[CmdletBinding()]
param(
    [ValidateSet('Install','Rollback','Uninstall')][string]$Action = 'Install',
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\StreamDesk'),
    [switch]$SkipRegistration
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\','/')
if ($root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\','/')) { throw 'No se permite instalar en la raiz de una unidad.' }
if ((Test-Path -LiteralPath $root) -and ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'La instalacion no admite enlaces de directorio.' }
$statePath = Join-Path $root 'installation.json'
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StreamDeskCandidate'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'StreamDesk.lnk'
function Test-Version([string]$value) { return $value -match '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$' }
function Write-State($state) {
    $temp = Join-Path $root ('.state-' + [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($temp, ($state | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $statePath) {
        $previousState = Join-Path $root 'installation.previous.json'
        if (Test-Path -LiteralPath $previousState) { [IO.File]::Delete($previousState) }
        [IO.File]::Replace($temp,$statePath,$previousState)
    } else { [IO.File]::Move($temp,$statePath) }
}
function Resolve-PackagePath([string]$base,[string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or $relative -match '(^|[\\/])\.\.?([\\/]|$)') { throw 'Ruta de paquete no valida.' }
    $resolved = [IO.Path]::GetFullPath((Join-Path $base $relative))
    if (-not $resolved.StartsWith($base.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta fuera del paquete.' }
    $cursor = $base
    foreach ($part in ($relative -split '[\\/]')) {
        $cursor = Join-Path $cursor $part
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'El paquete contiene enlaces.' }
    }
    return $resolved
}
function Register-Installation($state) {
    if ($SkipRegistration) { return }
    $folder = Join-Path $root ('versions\' + $state.CurrentVersion)
    New-Item -Path $registry -Force | Out-Null
    New-ItemProperty -Path $registry -Name DisplayName -Value 'StreamDesk candidato' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $registry -Name DisplayVersion -Value $state.CurrentVersion -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $registry -Name InstallLocation -Value $root -PropertyType String -Force | Out-Null
    $uninstall = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $folder 'Manage-StreamDesk.ps1') + '" -Action Uninstall -InstallRoot "' + $root + '"'
    New-ItemProperty -Path $registry -Name UninstallString -Value $uninstall -PropertyType String -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $root 'Launch-StreamDesk.ps1') + '"'
    $shortcut.WorkingDirectory = $root
    $shortcut.Save()
}
foreach ($process in @(Get-Process -Name 'StreamDesk.Probe.Desktop' -ErrorAction SilentlyContinue)) {
    try { $runningPath = $process.Path } catch { $runningPath = $null }
    if ($runningPath -and $runningPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Cierra StreamDesk antes de instalar, revertir o desinstalar.' }
}
if (Test-Path -LiteralPath $statePath) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.Format -ne 'StreamDeskInstallation' -or ($state.CurrentVersion -and -not (Test-Version $state.CurrentVersion)) -or ($state.PreviousVersion -and -not (Test-Version $state.PreviousVersion))) { throw 'Marcador de instalacion no valido.' }
} else {
    if ($Action -ne 'Install') { throw 'No hay una instalacion administrada en esta carpeta.' }
    if ((Test-Path -LiteralPath $root) -and @(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) { throw 'La carpeta no esta vacia y no pertenece al instalador.' }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $state = [pscustomobject]@{ Format='StreamDeskInstallation'; CurrentVersion=$null; PreviousVersion=$null }
    Write-State $state
}
if ($Action -eq 'Uninstall') {
    # The absolute root and ownership marker were checked above. User data is elsewhere.
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    if ($resolvedRoot -ne $root -or -not (Test-Path -LiteralPath $statePath)) { throw 'Ruta de desinstalacion no valida.' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    if (-not $SkipRegistration) {
        if (Test-Path -LiteralPath $registry) { Remove-Item -LiteralPath $registry -Recurse -Force }
        if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
    }
    Write-Output 'Desinstalado. Se conservan tus perfiles y copias en %LocalAppData%\StreamDesk.'
    exit 0
}
if ($Action -eq 'Rollback') {
    if (-not $state.PreviousVersion -or -not (Test-Path -LiteralPath (Join-Path $root ('versions\' + $state.PreviousVersion + '\app\StreamDesk.Probe.Desktop.exe')))) { throw 'No hay una version anterior para restaurar.' }
    $previous = $state.PreviousVersion; $state.PreviousVersion = $state.CurrentVersion; $state.CurrentVersion = $previous
    Write-State $state; Register-Installation $state; Write-Output ('Version activa: ' + $state.CurrentVersion); exit 0
}
$candidate = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'candidate.json') -Raw | ConvertFrom-Json
if (-not (Test-Version $candidate.Version)) { throw 'Version de paquete no valida.' }
$files = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'files.json') -Raw | ConvertFrom-Json
if ($files.Count -lt 1 -or $files.Count -gt 10000) { throw 'Inventario de archivos no valido.' }
$versions = Join-Path $root 'versions'
if ((Test-Path -LiteralPath $versions) -and ((Get-Item -LiteralPath $versions).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No se admiten enlaces en versiones.' }
New-Item -ItemType Directory -Path $versions -Force | Out-Null
$destination = Join-Path $versions $candidate.Version
if (Test-Path -LiteralPath $destination) { throw 'Esta version ya esta instalada; no se sobrescribe.' }
$pending = Join-Path $versions ('.pending-' + [Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $pending | Out-Null
try {
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $files) {
        if (-not $seen.Add([string]$entry.Path) -or $entry.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or $entry.Path -match '\.(db|dpapi|env|log)$') { throw 'Entrada de paquete no permitida.' }
        $source = Resolve-PackagePath $PSScriptRoot $entry.Path
        $target = Resolve-PackagePath $pending $entry.Path
        if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Sha256) { throw 'Integridad de paquete no valida.' }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Sha256) { throw 'La copia no coincide.' }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $pending 'app\StreamDesk.Probe.Desktop.exe'))) { throw 'Falta el ejecutable.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'files.json') -Destination $pending
    foreach ($movePath in @($pending,$destination)) {
        if (-not ([IO.Path]::GetFullPath($movePath)).StartsWith($versions + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Movimiento fuera de versiones.' }
    }
    Move-Item -LiteralPath $pending -Destination $destination
    Copy-Item -LiteralPath (Join-Path $destination 'Launch-StreamDesk.ps1') -Destination $root -Force
    $state.PreviousVersion = $state.CurrentVersion; $state.CurrentVersion = $candidate.Version
    Write-State $state; Register-Installation $state
    Write-Output ('Instalado: ' + $destination)
} finally {
    $resolvedPending = [IO.Path]::GetFullPath($pending)
    if (-not $resolvedPending.StartsWith($versions + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolvedPending).StartsWith('.pending-')) { throw 'Limpieza no permitida.' }
    if (Test-Path -LiteralPath $resolvedPending) { Remove-Item -LiteralPath $resolvedPending -Recurse -Force }
}
