[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA ('Programs\StreamDesk-MVP-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [switch]$SkipShortcut
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'app'
if (-not (Test-Path -LiteralPath (Join-Path $source 'StreamDesk.Probe.Desktop.exe'))) { throw 'Falta la carpeta app del candidato.' }
$destination = [IO.Path]::GetFullPath($InstallDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Elige una carpeta nueva. No se sobrescribe una instalacion existente.' }
New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $destination -Recurse
if (-not $SkipShortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'StreamDesk MVP.lnk'))
    $shortcut.TargetPath = Join-Path $destination 'StreamDesk.Probe.Desktop.exe'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Save()
}
Write-Output ('Instalado en: ' + $destination)
Write-Output 'Datos personales: %LocalAppData%\StreamDesk. Quitar la carpeta de instalacion no borra perfiles ni favoritos.'
