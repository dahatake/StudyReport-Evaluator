#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Assert-Equal {
    param(
        [AllowNull()]
        [object] $Expected,

        [AllowNull()]
        [object] $Actual,

        [Parameter(Mandatory)]
        [string] $Message
    )

    if (-not [object]::Equals($Expected, $Actual)) {
        throw "$Message Expected '$Expected', actual '$Actual'."
    }
}

function ConvertFrom-ToolJson {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]] $Output
    )

    return (([string](@($Output) -join "`n")).Trim() | ConvertFrom-Json)
}

if ($PSVersionTable.PSEdition -cne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell Core 7 or later is required.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolPath = Join-Path $PSScriptRoot 'version.ps1'
$sourcePath = Join-Path $repositoryRoot 'Directory.Build.props'
foreach ($requiredPath in @($toolPath, $sourcePath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required test input is missing: $requiredPath"
    }
}

$assertionCount = 0
$show = ConvertFrom-ToolJson -Output @(& $toolPath show -RepositoryRoot $repositoryRoot -Json)
Assert-Equal -Expected 'PASS' -Actual $show.Status -Message 'show status mismatch.'
$assertionCount++
Assert-Equal -Expected '1.0.0' -Actual $show.Version -Message 'repository version mismatch.'
$assertionCount++

$verify = ConvertFrom-ToolJson -Output @(& $toolPath verify -RepositoryRoot $repositoryRoot -Json)
Assert-Equal -Expected 'PASS' -Actual $verify.Status -Message 'verify status mismatch.'
$assertionCount++
Assert-Equal -Expected 2 -Actual @($verify.Projects).Count -Message 'verify project count mismatch.'
$assertionCount++

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('StudyReportEvaluator-VersionTool-' + [System.Guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($temporaryRoot)
try {
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $temporaryRoot 'Directory.Build.props')

    $set = ConvertFrom-ToolJson -Output @(
        & $toolPath set `
            -RepositoryRoot $temporaryRoot `
            -Version '2.3.4-rc.1' `
            -Json)
    Assert-Equal -Expected '2.3.4-rc.1' -Actual $set.Version -Message 'set version mismatch.'
    $assertionCount++
    Assert-Equal -Expected $true -Actual $set.Changed -Message 'set should report a change.'
    $assertionCount++

    $afterSet = ConvertFrom-ToolJson -Output @(
        & $toolPath show -RepositoryRoot $temporaryRoot -Json)
    Assert-Equal -Expected '2.3.4-rc.1' -Actual $afterSet.Version -Message 'persisted version mismatch.'
    $assertionCount++

    $dryRun = ConvertFrom-ToolJson -Output @(
        & $toolPath bump `
            -RepositoryRoot $temporaryRoot `
            -Part patch `
            -Prerelease 'beta.2' `
            -DryRun `
            -Json)
    Assert-Equal -Expected '2.3.5-beta.2' -Actual $dryRun.Version -Message 'dry-run bump mismatch.'
    $assertionCount++
    $afterDryRun = ConvertFrom-ToolJson -Output @(
        & $toolPath show -RepositoryRoot $temporaryRoot -Json)
    Assert-Equal -Expected '2.3.4-rc.1' -Actual $afterDryRun.Version -Message 'dry-run modified the source.'
    $assertionCount++

    $bump = ConvertFrom-ToolJson -Output @(
        & $toolPath bump `
            -RepositoryRoot $temporaryRoot `
            -Part minor `
            -Json)
    Assert-Equal -Expected '2.4.0' -Actual $bump.Version -Message 'minor bump mismatch.'
    $assertionCount++

    $invalidRejected = $false
    try {
        & $toolPath set `
            -RepositoryRoot $temporaryRoot `
            -Version '02.4.0' `
            -ErrorAction Stop | Out-Null
    }
    catch {
        $invalidRejected = $true
    }

    Assert-Equal -Expected $true -Actual $invalidRejected -Message 'invalid SemVer was not rejected.'
    $assertionCount++
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Output "Version tool tests passed: $assertionCount assertions."
