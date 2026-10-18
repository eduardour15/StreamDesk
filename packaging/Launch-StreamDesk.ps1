$ErrorActionPreference = 'Stop'
$state = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installation.json') -Raw | ConvertFrom-Json
if ($state.Format -ne 'StreamDeskInstallation' -or $state.CurrentVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Instalacion no valida.' }
$executable = Join-Path $PSScriptRoot ('versions\' + $state.CurrentVersion + '\app\StreamDesk.Probe.Desktop.exe')
Start-Process -FilePath $executable -WorkingDirectory ([IO.Path]::GetDirectoryName($executable)) -WindowStyle Normal
