#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding()]
param(
    [string] $ToolPackagesDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$BuildToolsPackageId = 'Microsoft.Windows.SDK.BuildTools'
$BuildToolsVersion = '10.0.26100.4948'
$BuildToolsContentHash = 'o0T4CVaumDjPNNijKiM7p25vHKdyKqYvaVVLgQO02KTOoUDlgMYJVUQAXn1IG0G9/ZsdZ+bdgWxgQsrO/b37qw=='
$UnsignedPublisherMarker = 'OID.2.25.311729368913984317654407730594956997722=1'
$IdentityName = 'StudyReportEvaluator.UnsignedDev'
$Publisher = "CN=StudyReportEvaluator Unsigned Development, $UnsignedPublisherMarker"
$PublisherDisplayName = 'StudyReport Evaluator Development'
$PackageFileName = 'StudyReportEvaluator-win-x64.unsigned.test.msix'
$EvidenceFileName = 'StudyReportEvaluator-win-x64.unsigned.test.evidence.json'
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$RequiredPublicEntries = @(
    'README.md',
    'LICENSE',
    'docs/README.md',
    'docs/getting-started.md',
    'docs/features.md',
    'docs/custom-evaluator-guide.md',
    'docs/technical-guid.md',
    'docs/prompt-launch.md',
    'docs/privacy-and-data-handling.md',
    'docs/troubleshooting.md',
    'docs/settings.md',
    'docs/third-party-notices.md',
    'images/README.md',
    'images/architecture-overview.svg',
    'images/technical-architecture.svg',
    'images/evaluation-message-flow.svg',
    'images/01-input-workbook.png',
    'images/02-input-mapping.png',
    'images/03-design-knowledge.png',
    'images/04-design-custom-prompt.png',
    'images/05-execution-auto.png',
    'images/06-results-review.png',
    'images/07-output-export.png',
    'images/08-settings.png'
)
$ExpectedNegativePolicyChecks = @(
    'INVALID_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED',
    'SIGNED_PUBLISHER_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED'
)
$ExpectedLimitations = @(
    'Development-only unsigned package; not for distribution.',
    'Standard App Installer UI, non-admin setup, signature trust, and production identity are not verified.',
    'Installation and package-installed application behavior were not run by this script.'
)

function Assert-SupportedHost {
    if ($PSVersionTable.PSEdition -cne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
        throw 'PowerShell Core 7 or later is required. Windows PowerShell is not supported.'
    }

    if (-not $IsWindows -or [System.Environment]::OSVersion.Version.Build -lt 22000) {
        throw 'Unsigned MSIX development testing requires Windows 11 or later.'
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64 -or
        [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) {
        throw 'Unsigned MSIX development testing requires an x64 OS and x64 PowerShell process.'
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string] $Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return [System.Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($stream))
    }
    finally {
        $stream.Dispose()
    }
}

function Get-Utf8Sha256Hex {
    param([Parameter(Mandatory)][AllowEmptyString()][string] $Value)

    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($Utf8NoBom.GetBytes($Value)))
}

function Write-AtomicText {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Value
    )

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    [void][System.IO.Directory]::CreateDirectory($directory)
    $temporaryPath = "$Path.$([System.Guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($temporaryPath, $Value, $Utf8NoBom)
        [System.IO.File]::Move($temporaryPath, $Path, $true)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Test-OwnedOutputSetValid {
    param(
        [Parameter(Mandatory)][string] $PackagePath,
        [Parameter(Mandatory)][string] $SidecarPath,
        [Parameter(Mandatory)][string] $EvidencePath
    )

    try {
        foreach ($path in @($PackagePath, $SidecarPath, $EvidencePath)) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                return $false
            }

            $item = Get-Item -LiteralPath $path -Force
            if ($item.Length -le 0 -or
                ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $false
            }
        }

        $packageHash = Get-Sha256Hex -Path $PackagePath
        $expectedSidecar = "$packageHash  $PackageFileName`n"
        if ([System.IO.File]::ReadAllText($SidecarPath) -cne $expectedSidecar) {
            return $false
        }

        $inspection = Get-UnsignedMsixInspection -Path $PackagePath
        $evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json
        $measuredAtUtc = [System.DateTimeOffset]::MinValue
        if (-not [System.DateTimeOffset]::TryParseExact(
                [string]$evidence.measuredAtUtc,
                'O',
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::RoundtripKind,
                [ref]$measuredAtUtc) -or
            $measuredAtUtc -gt [System.DateTimeOffset]::UtcNow.AddMinutes(5)) {
            return $false
        }

        $statusLines = @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) {
            return $false
        }

        $sourceCommit = ([string](@(git -C $repositoryRoot rev-parse HEAD) -join '')).Trim()
        if ($LASTEXITCODE -ne 0) {
            return $false
        }

        $dotnetSdk = ([string](@(dotnet --version) -join '')).Trim()
        if ($LASTEXITCODE -ne 0) {
            return $false
        }

        $negativeChecks = @($evidence.negativePolicyChecks)
        $limitations = @($evidence.limitations)
        $assets = @($evidence.syntheticAssets)
        $assetContractValid = $assets.Count -eq 3
        foreach ($expectedAsset in @(
                [pscustomobject]@{ Name = 'Square44x44Logo.png'; Width = 44; Height = 44 },
                [pscustomobject]@{ Name = 'Square150x150Logo.png'; Width = 150; Height = 150 },
                [pscustomobject]@{ Name = 'StoreLogo.png'; Width = 50; Height = 50 })) {
            $assetCandidates = @($assets | Where-Object { [string]$_.fileName -ceq $expectedAsset.Name })
            $archiveCandidates = @($inspection.SyntheticAssets | Where-Object {
                    [string]$_.fileName -ceq $expectedAsset.Name
                })
            $assetContractValid = $assetContractValid -and
                $assetCandidates.Count -eq 1 -and
                $archiveCandidates.Count -eq 1 -and
                [int]$assetCandidates[0].width -eq $expectedAsset.Width -and
                [int]$assetCandidates[0].height -eq $expectedAsset.Height -and
                [string]$assetCandidates[0].sha256 -match '^[0-9A-F]{64}$' -and
                [int]$archiveCandidates[0].width -eq $expectedAsset.Width -and
                [int]$archiveCandidates[0].height -eq $expectedAsset.Height -and
                [string]$archiveCandidates[0].sha256 -ceq [string]$assetCandidates[0].sha256
        }

        return [int]$evidence.schemaVersion -eq 1 -and
            [string]$evidence.evidenceKind -ceq 'windows-unsigned-msix-development-mechanism' -and
            [string]$evidence.status -ceq 'PASS_MECHANISM' -and
            [string]$evidence.productionStatus -ceq 'BLOCKED_EXTERNAL' -and
            [string]$evidence.installStatus -ceq 'NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST' -and
            [string]$evidence.sourceCommit -ceq $sourceCommit -and
            [int]$evidence.sourceStatusEntryCount -eq $statusLines.Count -and
            [string]$evidence.sourceStatusSha256 -ceq (Get-Utf8Sha256Hex -Value ([string]::Join("`n", $statusLines))) -and
            [int]$evidence.host.osBuild -eq [System.Environment]::OSVersion.Version.Build -and
            [string]$evidence.host.osArchitecture -ceq [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -and
            [string]$evidence.host.processArchitecture -ceq [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() -and
            [string]$evidence.host.powerShell -ceq $PSVersionTable.PSVersion.ToString() -and
            [string]$evidence.host.dotnetSdk -ceq $dotnetSdk -and
            [string]$evidence.tool.packageId -ceq $BuildToolsPackageId -and
            [string]$evidence.tool.packageVersion -ceq $BuildToolsVersion -and
            [string]$evidence.tool.packageContentHash -ceq $BuildToolsContentHash -and
            [string]$evidence.tool.makeAppxFileVersion -like "$BuildToolsVersion*" -and
            [string]$evidence.tool.makeAppxSha256 -match '^[0-9A-F]{64}$' -and
            [string]$evidence.tool.authenticodeStatus -ceq 'Valid' -and
            [string]$evidence.tool.signerSubject -like '*O=Microsoft Corporation*' -and
            [string]$evidence.package.fileName -ceq $PackageFileName -and
            [string]$evidence.package.publisher -ceq $Publisher -and
            [string]$evidence.package.sha256 -ceq $packageHash -and
            [long]$evidence.package.bytes -eq (Get-Item -LiteralPath $PackagePath).Length -and
            [int]$evidence.package.entryCount -eq $inspection.EntryCount -and
            [string]$evidence.package.identityName -ceq $IdentityName -and
            [string]$evidence.package.version -ceq $packageVersion -and
            [string]$evidence.package.architecture -ceq 'x64' -and
            [int]$evidence.package.signatureEntryCount -eq 0 -and
            [string]$evidence.package.blockMapHashMethod -ceq 'http://www.w3.org/2001/04/xmlenc#sha256' -and
            [string]$evidence.package.bundledCliSha256 -ceq $inspection.BundledCliSha256 -and
            $assetContractValid -and
            $negativeChecks.Count -eq $ExpectedNegativePolicyChecks.Count -and
            @($ExpectedNegativePolicyChecks | Where-Object { $negativeChecks -cnotcontains $_ }).Count -eq 0 -and
            $limitations.Count -eq $ExpectedLimitations.Count -and
            @($ExpectedLimitations | Where-Object { $limitations -cnotcontains $_ }).Count -eq 0
    }
    catch {
        return $false
    }
}

function Assert-ExpectedPolicyFailure {
    param(
        [Parameter(Mandatory)][hashtable] $Parameters,
        [Parameter(Mandatory)][string] $ExpectedMessage,
        [Parameter(Mandatory)][string] $Description,
        [Parameter(Mandatory)][string] $ProtectedOutputPath
    )

    $beforeHash = if (Test-Path -LiteralPath $ProtectedOutputPath -PathType Leaf) {
        Get-Sha256Hex -Path $ProtectedOutputPath
    }
    else {
        $null
    }
    $observed = $null
    try {
        & $packageScript @Parameters | Out-Null
    }
    catch {
        $observed = $_.Exception.Message
    }

    if ([string]::IsNullOrWhiteSpace($observed) -or
        -not $observed.Contains($ExpectedMessage, [System.StringComparison]::Ordinal)) {
        throw "$Description did not fail with the expected policy error."
    }

    $afterHash = if (Test-Path -LiteralPath $ProtectedOutputPath -PathType Leaf) {
        Get-Sha256Hex -Path $ProtectedOutputPath
    }
    else {
        $null
    }
    if ($beforeHash -cne $afterHash) {
        throw "$Description changed the protected package output."
    }
}

function New-SyntheticPng {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 32, 86, 156))
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-RequiredEntry {
    param(
        [Parameter(Mandatory)][System.IO.Compression.ZipArchive] $Archive,
        [Parameter(Mandatory)][string] $Name
    )

    $entry = $Archive.GetEntry($Name)
    if ($null -eq $entry -or $entry.Length -le 0) {
        throw "MSIX entry is missing or empty: $Name"
    }

    return $entry
}

function Get-PngEntryInspection {
    param(
        [Parameter(Mandatory)][System.IO.Compression.ZipArchive] $Archive,
        [Parameter(Mandatory)][string] $EntryName,
        [Parameter(Mandatory)][string] $FileName
    )

    $entry = Get-RequiredEntry -Archive $Archive -Name $EntryName
    $entryStream = $entry.Open()
    try {
        $memory = [System.IO.MemoryStream]::new()
        try {
            $entryStream.CopyTo($memory)
            $bytes = $memory.ToArray()
        }
        finally {
            $memory.Dispose()
        }
    }
    finally {
        $entryStream.Dispose()
    }

    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    if ($bytes.Length -lt 24) {
        throw "Packaged synthetic asset is not a valid PNG: $EntryName"
    }

    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) {
            throw "Packaged synthetic asset is not a valid PNG: $EntryName"
        }
    }

    $width = ($bytes[16] -shl 24) -bor ($bytes[17] -shl 16) -bor ($bytes[18] -shl 8) -bor $bytes[19]
    $height = ($bytes[20] -shl 24) -bor ($bytes[21] -shl 16) -bor ($bytes[22] -shl 8) -bor $bytes[23]
    return [pscustomobject]@{
        fileName = $FileName
        width = $width
        height = $height
        sha256 = [System.Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($bytes))
    }
}

function Get-UnsignedMsixInspection {
    param([Parameter(Mandatory)][string] $Path)

    Add-Type -AssemblyName System.IO.Compression
    $packageStream = [System.IO.File]::OpenRead($Path)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $packageStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        try {
            $entries = @($archive.Entries)
            $seen = [System.Collections.Generic.HashSet[string]]::new(
                [System.StringComparer]::OrdinalIgnoreCase)
            foreach ($entry in $entries) {
                $normalized = $entry.FullName.Replace('\', '/')
                if ([string]::IsNullOrWhiteSpace($normalized) -or
                    $normalized.StartsWith('/', [System.StringComparison]::Ordinal) -or
                    $normalized.Contains(':', [System.StringComparison]::Ordinal) -or
                    @($normalized.Split('/') | Where-Object { $_ -eq '.' -or $_ -eq '..' }).Count -ne 0 -or
                    -not $seen.Add($normalized)) {
                    throw "MSIX contains an unsafe, duplicate, or case-colliding entry: $normalized"
                }

                if ([System.IO.Path]::GetFileName($normalized) -ieq 'setting.txt') {
                    throw "User settings are not allowed in the MSIX package: $normalized"
                }

                if (-not $normalized.EndsWith('/', [System.StringComparison]::Ordinal) -and
                    $entry.Length -le 0) {
                    throw "MSIX contains an empty file: $normalized"
                }
            }

            if ($null -ne $archive.GetEntry('AppxSignature.p7x')) {
                throw 'Unsigned development MSIX contains a signature entry.'
            }

            $manifestEntry = Get-RequiredEntry -Archive $archive -Name 'AppxManifest.xml'
            $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
            try {
                [xml]$manifest = $manifestReader.ReadToEnd()
            }
            finally {
                $manifestReader.Dispose()
            }

            $identity = $manifest.Package.Identity
            if ([string]$identity.Name -cne $IdentityName -or
                [string]$identity.Publisher -cne $Publisher -or
                [string]$identity.Version -cne $packageVersion -or
                [string]$identity.ProcessorArchitecture -cne 'x64') {
                throw 'Unsigned MSIX manifest identity does not match the development contract.'
            }

            $blockMapEntry = Get-RequiredEntry -Archive $archive -Name 'AppxBlockMap.xml'
            $blockMapReader = [System.IO.StreamReader]::new($blockMapEntry.Open())
            try {
                [xml]$blockMap = $blockMapReader.ReadToEnd()
            }
            finally {
                $blockMapReader.Dispose()
            }

            if ([string]$blockMap.BlockMap.HashMethod -cne 'http://www.w3.org/2001/04/xmlenc#sha256') {
                throw 'Unsigned MSIX block map must use SHA-256.'
            }

            $runtimeEntry = Get-RequiredEntry -Archive $archive -Name 'copilot-runtime.json'
            $runtimeReader = [System.IO.StreamReader]::new($runtimeEntry.Open())
            try {
                $runtimeManifest = $runtimeReader.ReadToEnd() | ConvertFrom-Json
            }
            finally {
                $runtimeReader.Dispose()
            }

            if ([string]$runtimeManifest.runtimeIdentifier -cne 'win-x64' -or
                [string]$runtimeManifest.cliRelativePath -cne 'runtimes/win-x64/native/copilot.exe' -or
                [string]$runtimeManifest.cliSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
                throw 'Packaged Copilot runtime manifest violates the win-x64 contract.'
            }

            $cliEntry = Get-RequiredEntry -Archive $archive -Name 'runtimes/win-x64/native/copilot.exe'
            $cliStream = $cliEntry.Open()
            try {
                $cliHash = [System.Convert]::ToHexString(
                    [System.Security.Cryptography.SHA256]::HashData($cliStream))
            }
            finally {
                $cliStream.Dispose()
            }

            if ($cliHash -cne ([string]$runtimeManifest.cliSha256).ToUpperInvariant()) {
                throw 'Packaged Copilot CLI does not match the runtime manifest hash.'
            }

            foreach ($entryName in $RequiredPublicEntries) {
                [void](Get-RequiredEntry -Archive $archive -Name $entryName)
            }

            $syntheticAssets = @(
                Get-PngEntryInspection `
                    -Archive $archive `
                    -EntryName 'Assets/Square44x44Logo.png' `
                    -FileName 'Square44x44Logo.png'
                Get-PngEntryInspection `
                    -Archive $archive `
                    -EntryName 'Assets/Square150x150Logo.png' `
                    -FileName 'Square150x150Logo.png'
                Get-PngEntryInspection `
                    -Archive $archive `
                    -EntryName 'Assets/StoreLogo.png' `
                    -FileName 'StoreLogo.png'
            )

            return [pscustomobject]@{
                EntryCount = $entries.Count
                BundledCliSha256 = $cliHash
                SyntheticAssets = $syntheticAssets
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $packageStream.Dispose()
    }
}

Assert-SupportedHost
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionTool = Join-Path $repositoryRoot 'dev\version.ps1'
if (-not (Test-Path -LiteralPath $versionTool -PathType Leaf)) {
    throw "Required unsigned MSIX test input is missing: $versionTool"
}

$versionResult = & $versionTool show -Json | Out-String | ConvertFrom-Json
if ([string]$versionResult.Status -cne 'PASS' -or
    [string]$versionResult.Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Unable to resolve a stable repository product version.'
}

$productVersion = [string]$versionResult.Version
$packageVersion = "$productVersion.0"
$nugetConfig = Join-Path $repositoryRoot 'NuGet.Config'
$toolProject = Join-Path $repositoryRoot 'eng\packaging\windows\tools\WindowsSdkBuildTools.csproj'
$toolLock = Join-Path $repositoryRoot 'eng\packaging\windows\tools\packages.lock.json'
$packageScript = Join-Path $PSScriptRoot 'package-windows-msix.ps1'
$mechanismDirectory = Join-Path $repositoryRoot 'artifacts\package\mechanism'
$packagePath = Join-Path $mechanismDirectory $PackageFileName
$sidecarPath = "$packagePath.sha256"
$evidencePath = Join-Path $mechanismDirectory $EvidenceFileName

$ownedOutputPaths = @($packagePath, $sidecarPath, $evidencePath)
$existingOutputCount = @($ownedOutputPaths | Where-Object {
        Test-Path -LiteralPath $_
    }).Count
if ($existingOutputCount -ne 0 -and
    -not (Test-OwnedOutputSetValid `
        -PackagePath $packagePath `
        -SidecarPath $sidecarPath `
        -EvidencePath $evidencePath)) {
    foreach ($path in $ownedOutputPaths) {
        if (Test-Path -LiteralPath $path) {
            $item = Get-Item -LiteralPath $path -Force
            if ($item.PSIsContainer -or
                ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to remove an unsafe owned output path: $path"
            }

            Remove-Item -LiteralPath $path -Force
        }
    }
}

foreach ($requiredPath in @($versionTool, $nugetConfig, $toolProject, $toolLock, $packageScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required unsigned MSIX test input is missing: $requiredPath"
    }
}

if ([string]::IsNullOrWhiteSpace($ToolPackagesDirectory)) {
    $ToolPackagesDirectory = Join-Path $repositoryRoot 'artifacts\package\tools\nuget'
}
elseif (-not [System.IO.Path]::IsPathRooted($ToolPackagesDirectory)) {
    $ToolPackagesDirectory = [System.IO.Path]::GetFullPath($ToolPackagesDirectory, $repositoryRoot)
}
else {
    $ToolPackagesDirectory = [System.IO.Path]::GetFullPath($ToolPackagesDirectory)
}

[void][System.IO.Directory]::CreateDirectory($ToolPackagesDirectory)
$toolPackagesItem = Get-Item -LiteralPath $ToolPackagesDirectory -Force
if (($toolPackagesItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The Windows SDK BuildTools package directory cannot be a reparse point.'
}

$lockHashBefore = Get-Sha256Hex -Path $toolLock
$restoreIntermediate = Join-Path $ToolPackagesDirectory '.restore-obj\'
& dotnet restore $toolProject `
    --locked-mode `
    --packages $ToolPackagesDirectory `
    --configfile $nugetConfig `
    --property:BaseIntermediateOutputPath=$restoreIntermediate
if ($LASTEXITCODE -ne 0) {
    throw "Windows SDK BuildTools locked restore failed with exit code $LASTEXITCODE."
}

if ((Get-Sha256Hex -Path $toolLock) -cne $lockHashBefore) {
    throw 'Windows SDK BuildTools locked restore changed the canonical lock file.'
}

$lock = Get-Content -LiteralPath $toolLock -Raw | ConvertFrom-Json
$lockedPackage = $lock.dependencies.'net10.0'.$BuildToolsPackageId
if ($null -eq $lockedPackage -or
    [string]$lockedPackage.requested -cne "[$BuildToolsVersion, $BuildToolsVersion]" -or
    [string]$lockedPackage.resolved -cne $BuildToolsVersion -or
    [string]$lockedPackage.contentHash -cne $BuildToolsContentHash) {
    throw 'Windows SDK BuildTools lock identity does not match the approved test tool.'
}

$packageRoot = Join-Path $ToolPackagesDirectory "$($BuildToolsPackageId.ToLowerInvariant())\$BuildToolsVersion"
$makeAppxCandidates = @(Get-ChildItem -LiteralPath $packageRoot -Filter 'makeappx.exe' -File -Recurse |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]makeappx\.exe$' })
if ($makeAppxCandidates.Count -ne 1) {
    throw "Expected exactly one x64 MakeAppx.exe, found $($makeAppxCandidates.Count)."
}

$makeAppx = $makeAppxCandidates[0]
$makeAppxSignature = Get-AuthenticodeSignature -LiteralPath $makeAppx.FullName
if ($makeAppxSignature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
    $null -eq $makeAppxSignature.SignerCertificate -or
    -not $makeAppxSignature.SignerCertificate.Subject.Contains(
        'O=Microsoft Corporation',
        [System.StringComparison]::Ordinal)) {
    throw 'MakeAppx.exe must have a valid Microsoft Authenticode signature.'
}

if (-not $makeAppx.VersionInfo.FileVersion.StartsWith(
        $BuildToolsVersion,
        [System.StringComparison]::Ordinal)) {
    throw 'MakeAppx.exe file version does not match the locked BuildTools package.'
}

$windowsSdkBinDirectory = $makeAppx.Directory.FullName
$assetDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
    'StudyReportEvaluator-MsixAssets-' + [System.Guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($assetDirectory)
$packageCommitted = $false
$packageInvocationStarted = $false
$packageHashBeforeInvocation = $null
try {
    Add-Type -AssemblyName System.Drawing.Common
    $square44 = Join-Path $assetDirectory 'Square44x44Logo.png'
    $square150 = Join-Path $assetDirectory 'Square150x150Logo.png'
    $storeLogo = Join-Path $assetDirectory 'StoreLogo.png'
    New-SyntheticPng -Path $square44 -Size 44
    New-SyntheticPng -Path $square150 -Size 150
    New-SyntheticPng -Path $storeLogo -Size 50

    $commonParameters = @{
        IdentityName = $IdentityName
        PublisherDisplayName = $PublisherDisplayName
        Square44x44LogoPath = $square44
        Square150x150LogoPath = $square150
        StoreLogoPath = $storeLogo
        ProductVersion = $productVersion
        WindowsSdkBinDirectory = $windowsSdkBinDirectory
    }
    $invalidUnsignedParameters = [hashtable]$commonParameters.Clone()
    $invalidUnsignedParameters.UnsignedDevelopment = $true
    $invalidUnsignedParameters.Publisher = 'CN=StudyReportEvaluator Unsigned Development, OID.2.25.311729368913984317654407730594956997721=1'
    Assert-ExpectedPolicyFailure `
        -Parameters $invalidUnsignedParameters `
        -ExpectedMessage 'fixed marker' `
        -Description 'Invalid unsigned Publisher marker case' `
        -ProtectedOutputPath $packagePath

    $signedMarkerParameters = [hashtable]$commonParameters.Clone()
    $signedMarkerParameters.Publisher = "CN=Signed Development, $UnsignedPublisherMarker, O=Example"
    $signedMarkerParameters.TestCertificateThumbprint = '0000000000000000000000000000000000000000'
    Assert-ExpectedPolicyFailure `
        -Parameters $signedMarkerParameters `
        -ExpectedMessage 'cannot use the unsigned-development Publisher OID identity' `
        -Description 'Signed Publisher containing unsigned marker case' `
        -ProtectedOutputPath $packagePath

    $packageHashBeforeInvocation = if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
        Get-Sha256Hex -Path $packagePath
    }
    else {
        $null
    }
    $packageInvocationStarted = $true
    & $packageScript `
        -UnsignedDevelopment `
        -IdentityName $IdentityName `
        -Publisher $Publisher `
        -PublisherDisplayName $PublisherDisplayName `
        -Square44x44LogoPath $square44 `
        -Square150x150LogoPath $square150 `
        -StoreLogoPath $storeLogo `
        -WindowsSdkBinDirectory $windowsSdkBinDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Unsigned MSIX package script failed with exit code $LASTEXITCODE."
    }

    $packageCommitted = $true
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw 'Unsigned MSIX package script did not create the owned output.'
    }

    $inspection = Get-UnsignedMsixInspection -Path $packagePath
    $entryCount = $inspection.EntryCount
    $cliHash = $inspection.BundledCliSha256

    $packageItem = Get-Item -LiteralPath $packagePath -Force
    $packageHash = Get-Sha256Hex -Path $packagePath
    $packageSignature = Get-AuthenticodeSignature -LiteralPath $packagePath
    if ($packageSignature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
        throw 'Unsigned development MSIX must have Authenticode status NotSigned.'
    }

    $statusLines = @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to capture the source status fingerprint.'
    }

    $sourceCommit = ([string](@(git -C $repositoryRoot rev-parse HEAD) -join '')).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'Unable to capture the source commit.'
    }

    $assetEvidence = @(
        [ordered]@{ fileName = 'Square44x44Logo.png'; width = 44; height = 44; sha256 = Get-Sha256Hex -Path $square44 },
        [ordered]@{ fileName = 'Square150x150Logo.png'; width = 150; height = 150; sha256 = Get-Sha256Hex -Path $square150 },
        [ordered]@{ fileName = 'StoreLogo.png'; width = 50; height = 50; sha256 = Get-Sha256Hex -Path $storeLogo })
    $evidence = [ordered]@{
        schemaVersion = 1
        evidenceKind = 'windows-unsigned-msix-development-mechanism'
        measuredAtUtc = [System.DateTimeOffset]::UtcNow.ToString('O')
        status = 'PASS_MECHANISM'
        productionStatus = 'BLOCKED_EXTERNAL'
        installStatus = 'NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST'
        sourceCommit = $sourceCommit
        sourceStatusEntryCount = $statusLines.Count
        sourceStatusSha256 = Get-Utf8Sha256Hex -Value ([string]::Join("`n", $statusLines))
        host = [ordered]@{
            osBuild = [System.Environment]::OSVersion.Version.Build
            osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            powerShell = $PSVersionTable.PSVersion.ToString()
            dotnetSdk = ([string](@(dotnet --version) -join '')).Trim()
        }
        tool = [ordered]@{
            packageId = $BuildToolsPackageId
            packageVersion = $BuildToolsVersion
            packageContentHash = $BuildToolsContentHash
            makeAppxFileVersion = $makeAppx.VersionInfo.FileVersion
            makeAppxSha256 = Get-Sha256Hex -Path $makeAppx.FullName
            authenticodeStatus = $makeAppxSignature.Status.ToString()
            signerSubject = $makeAppxSignature.SignerCertificate.Subject
        }
        package = [ordered]@{
            fileName = $PackageFileName
            bytes = $packageItem.Length
            sha256 = $packageHash
            entryCount = $entryCount
            identityName = $IdentityName
            publisher = $Publisher
            version = $packageVersion
            architecture = 'x64'
            signatureEntryCount = 0
            blockMapHashMethod = 'http://www.w3.org/2001/04/xmlenc#sha256'
            bundledCliSha256 = $cliHash
        }
        syntheticAssets = $assetEvidence
        negativePolicyChecks = $ExpectedNegativePolicyChecks
        limitations = $ExpectedLimitations
    }

    Write-AtomicText -Path $sidecarPath -Value "$packageHash  $PackageFileName`n"
    Write-AtomicText -Path $evidencePath -Value (($evidence | ConvertTo-Json -Depth 8) + "`n")
    Write-Output "Unsigned MSIX mechanism evidence: $evidencePath"
    Write-Output "Package SHA-256: $packageHash"
    Write-Output 'Status: PASS_MECHANISM only; production and installation remain unverified.'
}
catch {
    $packageOutputChanged = $false
    if ($packageInvocationStarted) {
        $packageHashAfterFailure = if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
            Get-Sha256Hex -Path $packagePath
        }
        else {
            $null
        }
        $packageOutputChanged = $packageHashBeforeInvocation -cne $packageHashAfterFailure
    }

    if ($packageCommitted -or $packageOutputChanged) {
        Remove-Item -LiteralPath $packagePath, $sidecarPath, $evidencePath -Force -ErrorAction SilentlyContinue
    }

    throw
}
finally {
    if (Test-Path -LiteralPath $assetDirectory) {
        Remove-Item -LiteralPath $assetDirectory -Recurse -Force
    }
}
