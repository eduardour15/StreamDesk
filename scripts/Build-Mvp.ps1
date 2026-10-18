[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$candidate = Join-Path $repository ('artifacts\mvp\StreamDesk-0.1-candidate-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $candidate) { throw 'Ya existe la salida; espera un segundo y vuelve a ejecutar.' }
New-Item -ItemType Directory -Path $candidate | Out-Null
$project = Join-Path $repository 'src\StreamDesk.Probe.Desktop\StreamDesk.Probe.Desktop.csproj'
& dotnet publish $project -c Release -r win-x64 --self-contained false -p:Version=0.1.0 -p:PublishSingleFile=false -p:VlcWindowsX64Enabled=true -p:VlcWindowsX86Enabled=false -p:VlcWindowsArm64Enabled=false -p:UseSharedCompilation=false --verbosity minimal --maxcpucount:1 -o (Join-Path $candidate 'app')
if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet publish. No se ha generado un candidato valido.' }
Copy-Item -LiteralPath (Join-Path $repository 'packaging\Install-Mvp.ps1'),(Join-Path $repository 'packaging\THIRD-PARTY-NOTICES.md') -Destination $candidate
$licenseDirectory = Join-Path $candidate 'licenses'
New-Item -ItemType Directory -Path $licenseDirectory | Out-Null
$assets = Get-Content -LiteralPath (Join-Path $repository 'src\StreamDesk.Probe.Desktop\obj\project.assets.json') -Raw | ConvertFrom-Json
$inventory = @()
foreach ($library in $assets.libraries.PSObject.Properties | Sort-Object Name) {
    if ($library.Value.type -ne 'package') { continue }
    $packageDirectory = $null
    foreach ($root in $assets.packageFolders.PSObject.Properties.Name) {
        $resolved = Join-Path $root $library.Value.path
        if (Test-Path -LiteralPath $resolved) { $packageDirectory = $resolved; break }
    }
    if (-not $packageDirectory) { throw ('Paquete no encontrado: ' + $library.Name) }
    $target = Join-Path $licenseDirectory ($library.Name.Replace('/','-'))
    New-Item -ItemType Directory -Path $target | Out-Null
    $spec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' | Select-Object -First 1
    [xml]$metadata = Get-Content -LiteralPath $spec.FullName -Raw
    $inventory += [pscustomobject]@{ Package=$library.Name; License=[string]$metadata.package.metadata.license.'#text'; LicenseUrl=[string]$metadata.package.metadata.licenseUrl; Source=[string]$metadata.package.metadata.projectUrl }
    Copy-Item -LiteralPath $spec.FullName -Destination $target
    foreach ($file in Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD-PARTY)' }) {
        $relative = $file.FullName.Substring($packageDirectory.Length).TrimStart('\','/')
        $copy = Join-Path $target $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($copy)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $copy
    }
}
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $licenseDirectory 'packages.json') -Encoding UTF8
$executable = Join-Path $candidate 'app\StreamDesk.Probe.Desktop.exe'
if (-not (Test-Path -LiteralPath (Join-Path $candidate 'app\libvlc\win-x64\plugins'))) { throw 'Faltan plugins VLC.' }
$process = Start-Process -FilePath $executable -ArgumentList '--verify-runtime' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Timeout comprobando el runtime del candidato.' }
if ($process.ExitCode -ne 0) { throw 'El candidato no pudo inicializar y liberar LibVLC.' }
@{ Version='0.1.0-candidate'; Runtime='win-x64'; SelfContained=$false; RuntimeSmoke='PASA'; NativeLicenses='REVISION COMPLETA PENDIENTE'; GeneratedUtc=[DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $candidate 'candidate.json') -Encoding UTF8
Write-Output ('Candidato generado: ' + $candidate)
