# Compatibility entry point; the maintained builder produces the current prerelease.
[CmdletBinding()]
param()
& (Join-Path $PSScriptRoot 'Build-Candidate.ps1')
