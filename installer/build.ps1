# Script para compilar el instalador de StreamDesk para Windows x64
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "artifacts\publish\win-x64"
$InstallerDir = Join-Path $Root "artifacts\installer"
$ProjectFile = Join-Path $Root "src\StreamDesk.Probe.Desktop\StreamDesk.Probe.Desktop.csproj"
$IssFile = Join-Path $PSScriptRoot "StreamDesk.iss"

Write-Host "==> 1. Publicando StreamDesk en modo autocontenido (win-x64)..." -ForegroundColor Cyan
dotnet publish $ProjectFile -c $Configuration -r win-x64 --self-contained true -o $PublishDir

Write-Host "==> 2. Buscando compilador de Inno Setup (ISCC.exe)..." -ForegroundColor Cyan
$Iscc = @(
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    throw "No se encontró Inno Setup 6 instalado en el sistema."
}

Write-Host "Usando compilador: $Iscc" -ForegroundColor Gray
Write-Host "==> 3. Generando instalador ejecutable..." -ForegroundColor Cyan
& $Iscc $IssFile

Write-Host "==> ¡Instalador generado con éxito en: $InstallerDir!" -ForegroundColor Green
Get-Item "$InstallerDir\*.exe" | Select-Object Name, @{Name="SizeMB"; Expression={[math]::Round($_.Length / 1MB, 2)}}, LastWriteTime | Format-Table -AutoSize
