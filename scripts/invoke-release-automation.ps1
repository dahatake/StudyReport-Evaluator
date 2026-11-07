#Requires -Version 7.5
#Requires -PSEdition Core
<#
.SYNOPSIS
FR-121: shared candidate/publication driver. Requests are private observations
from prepare-release-automation.ps1, never workflow_dispatch JSON inputs.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $RequestPath,
    [Parameter(Mandatory)][string] $ResultPath,
    [string] $GitHubApiUrl = $env:GITHUB_API_URL
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GitHubApiUrl)) { $GitHubApiUrl = 'https://api.github.com' }
& python (Join-Path $PSScriptRoot 'release_automation.py') `
    --request $RequestPath --result $ResultPath --api $GitHubApiUrl
exit $LASTEXITCODE
