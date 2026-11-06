#Requires -Version 7.5
#Requires -PSEdition Core
<#
.SYNOPSIS
Derive FR-121 private orchestration observations from the existing C02/C03 outputs.
Never accepts package status, version verification or approval as user input.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Snapshot','Candidate','Publish','Relocate')][string] $Mode,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $ArtifactDirectory,
    [string] $SnapshotPath,
    [string] $CandidateRequestPath,
    [string] $ResultsDirectory
)
$ErrorActionPreference = 'Stop'
$arguments = @('--mode', $Mode.ToLowerInvariant(), '--output', $OutputDirectory)
foreach ($pair in @(
        @('--artifacts', $ArtifactDirectory), @('--snapshot', $SnapshotPath),
        @('--candidate', $CandidateRequestPath), @('--results', $ResultsDirectory))) {
    if (-not [string]::IsNullOrWhiteSpace($pair[1])) { $arguments += $pair }
}
& python (Join-Path $PSScriptRoot 'prepare_release_automation.py') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Release observation preparation failed.' }
