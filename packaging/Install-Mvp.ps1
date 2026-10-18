# Compatibility entry point for the current package manager.
[CmdletBinding()]
param([string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\StreamDesk'), [switch]$SkipShortcut)
& (Join-Path $PSScriptRoot 'Manage-StreamDesk.ps1') -Action Install -InstallRoot $InstallDirectory -SkipRegistration:$SkipShortcut
