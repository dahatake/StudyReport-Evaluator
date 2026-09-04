#Requires -Version 7.4
#Requires -PSEdition Core

[CmdletBinding()]
param(
    [string] $ZipDirectory,

    [string] $MsixDirectory,

    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false, $true)
$MatrixFileName = 'platform-release-matrix.json'
$ZipFiles = [ordered]@{
    Artifact = 'StudyReportEvaluator-win-x64.zip'
    Sidecar = 'StudyReportEvaluator-win-x64.zip.sha256'
    Evidence = 'StudyReportEvaluator-win-x64.evidence.json'
}
$MsixFiles = [ordered]@{
    Artifact = 'StudyReportEvaluator-win-x64.unsigned.test.msix'
    Sidecar = 'StudyReportEvaluator-win-x64.unsigned.test.msix.sha256'
    Evidence = 'StudyReportEvaluator-win-x64.unsigned.test.evidence.json'
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileSystemInfo] $Item,

        [Parameter(Mandatory)]
        [string] $Description
    )

    if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Description cannot be a reparse point."
    }
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        return [System.Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($stream))
    }
    finally {
        $stream.Dispose()
    }
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory)]
        [string] $SourceDirectory,

        [Parameter(Mandatory)]
        [string] $FileName,

        [Parameter(Mandatory)]
        [string] $DestinationDirectory
    )

    $sourcePath = Join-Path $SourceDirectory $FileName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required release matrix input is missing: $FileName"
    }

    $sourceItem = Get-Item -LiteralPath $sourcePath -Force
    Assert-NotReparsePoint -Item $sourceItem -Description "Release matrix input $FileName"
    if ($sourceItem.Length -le 0) {
        throw "Required release matrix input is empty: $FileName"
    }

    $destinationPath = Join-Path $DestinationDirectory $FileName
    Copy-Item -LiteralPath $sourceItem.FullName -Destination $destinationPath -Force
    $destinationItem = Get-Item -LiteralPath $destinationPath -Force
    if ($destinationItem.Length -ne $sourceItem.Length) {
        throw "Release matrix input was not copied faithfully: $FileName"
    }

    return [ordered]@{
        fileName = $FileName
        bytes = $destinationItem.Length
        sha256 = Get-Sha256Hex -Path $destinationItem.FullName
    }
}

function Read-EvidenceObject {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw 'Release matrix evidence input must not use a UTF-8 BOM.'
    }

    return $Utf8NoBom.GetString($bytes) |
        ConvertFrom-Json -AsHashtable -DateKind String -Depth 32
}

if ($PSVersionTable.PSEdition -cne 'Core' -or
    $PSVersionTable.PSVersion -lt [System.Version]'7.4') {
    throw 'PowerShell Core 7.4 or later is required.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionTool = Join-Path $repositoryRoot 'dev\version.ps1'
$validator = Join-Path $repositoryRoot 'scripts\validate-platform-release-matrix.ps1'
foreach ($requiredPath in @($versionTool, $validator)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw 'Release matrix tooling is missing.'
    }
}

function Resolve-InputDirectory {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $DefaultRelativePath
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return Join-Path $repositoryRoot $DefaultRelativePath
    }

    if ([System.IO.Path]::IsPathFullyQualified($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath($Path, $repositoryRoot)
}

$zipRoot = Resolve-InputDirectory -Path $ZipDirectory -DefaultRelativePath 'artifacts\package'
$msixRoot = Resolve-InputDirectory -Path $MsixDirectory -DefaultRelativePath 'artifacts\package\mechanism'
$outputRoot = Resolve-InputDirectory -Path $OutputDirectory -DefaultRelativePath 'artifacts\package\matrix'

$matrixPath = Join-Path $outputRoot $MatrixFileName
if (Test-Path -LiteralPath $outputRoot) {
    $existingOutput = Get-Item -LiteralPath $outputRoot -Force
    Assert-NotReparsePoint -Item $existingOutput -Description 'Release matrix output directory'
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

$statusBefore = @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $statusBefore.Count -ne 0) {
    throw 'The release matrix must be generated from a clean source checkout.'
}

$sourceCommit = ([string](@(git -C $repositoryRoot rev-parse HEAD) -join '')).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the release matrix source commit.'
}

$versionResult = & $versionTool show -Json | Out-String | ConvertFrom-Json
if ([string]$versionResult.Status -cne 'PASS' -or
    [string]$versionResult.Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Unable to resolve the stable release matrix product version.'
}
$productVersion = [string]$versionResult.Version

[void][System.IO.Directory]::CreateDirectory($outputRoot)
$zipDescriptors = [ordered]@{}
foreach ($entry in $ZipFiles.GetEnumerator()) {
    $zipDescriptors[$entry.Key] = Copy-RequiredFile `
        -SourceDirectory $zipRoot `
        -FileName $entry.Value `
        -DestinationDirectory $outputRoot
}

$msixDescriptors = [ordered]@{}
foreach ($entry in $MsixFiles.GetEnumerator()) {
    $msixDescriptors[$entry.Key] = Copy-RequiredFile `
        -SourceDirectory $msixRoot `
        -FileName $entry.Value `
        -DestinationDirectory $outputRoot
}

$zipEvidence = Read-EvidenceObject -Path (Join-Path $outputRoot $ZipFiles.Evidence)
$msixEvidence = Read-EvidenceObject -Path (Join-Path $outputRoot $MsixFiles.Evidence)

if ([string]$zipEvidence.sourceCommit -cne $sourceCommit -or
    [string]$msixEvidence.sourceCommit -cne $sourceCommit) {
    throw 'Release matrix evidence was not produced from the checked-out source commit.'
}

# The matrix must never merge evidence measured on different hosts.
if ([int]$zipEvidence.host.osBuild -ne [int]$msixEvidence.host.osBuild -or
    [string]$zipEvidence.host.osArchitecture -cne [string]$msixEvidence.host.osArchitecture -or
    [string]$zipEvidence.host.processArchitecture -cne [string]$msixEvidence.host.processArchitecture) {
    throw 'Release matrix evidence was measured on different Windows hosts.'
}

$verification = [ordered]@{
    osName = [string]$zipEvidence.host.osName
    osVersion = [string]$zipEvidence.host.osVersion
    osBuild = [int]$zipEvidence.host.osBuild
    osArchitecture = [string]$zipEvidence.host.osArchitecture
    processArchitecture = [string]$zipEvidence.host.processArchitecture
}

$matrix = [ordered]@{
    schemaVersion = 1
    productVersion = $productVersion
    sourceCommit = $sourceCommit
    rows = @(
        [ordered]@{
            artifactKind = 'windows-zip'
            platform = 'windows'
            runtimeIdentifier = 'win-x64'
            publish = $true
            status = 'PASS_REQUIRED'
            verification = $verification
            artifact = $zipDescriptors.Artifact
            sidecar = $zipDescriptors.Sidecar
            evidence = $zipDescriptors.Evidence
        },
        [ordered]@{
            artifactKind = 'windows-development-msix'
            platform = 'windows'
            runtimeIdentifier = 'win-x64'
            publish = $false
            status = 'PASS_MECHANISM'
            verification = $verification
            artifact = $msixDescriptors.Artifact
            sidecar = $msixDescriptors.Sidecar
            evidence = $msixDescriptors.Evidence
        }
    )
}

$temporaryPath = "$matrixPath.$([System.Guid]::NewGuid().ToString('N')).tmp"
try {
    $json = ($matrix | ConvertTo-Json -Depth 8) + "`n"
    [System.IO.File]::WriteAllText($temporaryPath, $json, $Utf8NoBom)
    [System.IO.File]::Move($temporaryPath, $matrixPath, $true)
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}

& $validator `
    -MatrixPath $matrixPath `
    -ArtifactDirectory $outputRoot `
    -ExpectedProductVersion $productVersion `
    -ExpectedSourceCommit $sourceCommit
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $matrixPath -Force -ErrorAction SilentlyContinue
    throw 'The generated release matrix failed semantic validation.'
}

$statusAfter = @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $statusAfter.Count -ne 0) {
    throw 'Release matrix generation changed the clean source checkout.'
}

Write-Output "Release matrix: $matrixPath"
Write-Output "Product version: $productVersion"
Write-Output "Source commit: $sourceCommit"
Write-Output 'Publishable assets: Windows ZIP and its SHA-256 sidecar only.'
