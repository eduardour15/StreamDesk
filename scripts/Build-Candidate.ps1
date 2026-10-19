[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z.-]+$')][string]$Version = '0.5.0-rc.2')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$candidate = Join-Path $repository ('artifacts\candidates\StreamDesk-' + $Version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $candidate) { throw 'La salida ya existe.' }
New-Item -ItemType Directory -Path $candidate | Out-Null
$project = Join-Path $repository 'src\StreamDesk.Probe.Desktop\StreamDesk.Probe.Desktop.csproj'
& dotnet publish $project -c Release -r win-x64 --self-contained true "-p:Version=$Version" -p:PublishSingleFile=false -p:VlcWindowsX64Enabled=true -p:VlcWindowsX86Enabled=false -p:VlcWindowsArm64Enabled=false -p:UseSharedCompilation=false --verbosity minimal --maxcpucount:1 -o (Join-Path $candidate 'app')
if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet publish.' }
foreach ($name in @('Manage-StreamDesk.ps1','Launch-StreamDesk.ps1','THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $repository ('packaging\' + $name)) -Destination $candidate }
Copy-Item -LiteralPath (Join-Path $repository 'docs\MANUAL_USO.md') -Destination $candidate
$licenses = Join-Path $candidate 'licenses'; New-Item -ItemType Directory -Path $licenses | Out-Null
if (Test-Path -LiteralPath (Join-Path $repository 'packaging\licenses')) { Copy-Item -Path (Join-Path $repository 'packaging\licenses\*') -Destination $licenses -Recurse }
$assets = Get-Content -LiteralPath (Join-Path $repository 'src\StreamDesk.Probe.Desktop\obj\project.assets.json') -Raw | ConvertFrom-Json
$inventory = @()
foreach ($library in $assets.libraries.PSObject.Properties | Sort-Object Name) {
    if ($library.Value.type -ne 'package') { continue }
    $directory = $null
    foreach ($packageRoot in $assets.packageFolders.PSObject.Properties.Name) { $resolved = Join-Path $packageRoot $library.Value.path; if (Test-Path -LiteralPath $resolved) { $directory = $resolved; break } }
    if (-not $directory) { throw ('Falta paquete: ' + $library.Name) }
    $target = Join-Path $licenses $library.Name.Replace('/','-'); New-Item -ItemType Directory -Path $target | Out-Null
    $spec = Get-ChildItem -LiteralPath $directory -Filter '*.nuspec' | Select-Object -First 1
    [xml]$metadata = Get-Content -LiteralPath $spec.FullName -Raw
    $inventory += [pscustomobject]@{ Package=$library.Name; License=[string]$metadata.package.metadata.license.'#text'; LicenseUrl=[string]$metadata.package.metadata.licenseUrl; Source=[string]$metadata.package.metadata.projectUrl }
    Copy-Item -LiteralPath $spec.FullName -Destination $target
    foreach ($file in Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD-PARTY)' }) {
        $relative = $file.FullName.Substring($directory.Length).TrimStart('\','/'); $copy = Join-Path $target $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($copy)) -Force | Out-Null; Copy-Item -LiteralPath $file.FullName -Destination $copy
    }
}
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $licenses 'packages.json') -Encoding UTF8
$runtimeConfig = Get-Content -LiteralPath (Join-Path $candidate 'app\StreamDesk.Probe.Desktop.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $id = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    foreach ($packageRoot in $assets.packageFolders.PSObject.Properties.Name) {
        $runtimeDirectory = Join-Path $packageRoot ($id + '\' + $framework.version)
        if (-not (Test-Path -LiteralPath $runtimeDirectory)) { continue }
        $target = Join-Path $licenses ($id + '-' + $framework.version); New-Item -ItemType Directory -Path $target -Force | Out-Null
        Get-ChildItem -LiteralPath $runtimeDirectory -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD[-_ ]?PARTY)' } | Copy-Item -Destination $target
    }
}
foreach ($required in @('hostfxr.dll','coreclr.dll','PresentationFramework.dll','libvlc\win-x64\libvlc.dll','libvlc\win-x64\libvlccore.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $candidate ('app\' + $required)))) { throw ('Falta runtime: ' + $required) }
}
$native = @(Get-ChildItem -LiteralPath (Join-Path $candidate 'app\libvlc\win-x64') -Filter '*.dll' -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path=$_.FullName.Substring($candidate.Length+1).Replace('\','/'); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash; FileVersion=$_.VersionInfo.FileVersion; LicenseMapping='PENDIENTE_REVISION_COMPONENTE' }
})
$native | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $licenses 'native-files.json') -Encoding UTF8
$start = [DateTime]::UtcNow
$process = Start-Process -FilePath (Join-Path $candidate 'app\StreamDesk.Probe.Desktop.exe') -ArgumentList '--verify-runtime' -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Timeout verificando runtime.' }
if ($process.ExitCode -ne 0) { throw 'No se pudo decodificar el fixture nativo del candidato.' }
@{ Version=$Version; Runtime='win-x64'; SelfContained=$true; NativeDecode='PASA'; LocalSmokeMilliseconds=([DateTime]::UtcNow-$start).TotalMilliseconds; ReleaseApproved=$false; NativeLicenseReview='PENDIENTE'; GeneratedUtc=[DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $candidate 'candidate.json') -Encoding UTF8
$files = @(Get-ChildItem -LiteralPath $candidate -Recurse -File | ForEach-Object {
    if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint -or $_.Extension -match '^\.(db|dpapi|env|log)$') { throw 'Archivo no permitido en candidato.' }
    [pscustomobject]@{ Path=$_.FullName.Substring($candidate.Length+1).Replace('\','/'); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash; Bytes=$_.Length }
})
$files | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $candidate 'files.json') -Encoding UTF8
Write-Output ('Candidato local: ' + $candidate)
