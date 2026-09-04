#Requires -Version 7.4
#Requires -PSEdition Core

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $MatrixPath,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory,

    [string] $ExpectedProductVersion,

    [string] $ExpectedSourceCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false, $true)
$EmptyUtf8Sha256 = 'E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855'
$BuildToolsVersion = '10.0.26100.4948'
$BuildToolsContentHash = 'o0T4CVaumDjPNNijKiM7p25vHKdyKqYvaVVLgQO02KTOoUDlgMYJVUQAXn1IG0G9/ZsdZ+bdgWxgQsrO/b37qw=='
$UnsignedMsixIdentityName = 'StudyReportEvaluator.UnsignedDev'
$UnsignedMsixPublisher = 'CN=StudyReportEvaluator Unsigned Development, OID.2.25.311729368913984317654407730594956997722=1'
$ExpectedRows = [ordered]@{
    'windows-zip' = [ordered]@{
        Publish = $true
        Status = 'PASS_REQUIRED'
        Artifact = 'StudyReportEvaluator-win-x64.zip'
        Sidecar = 'StudyReportEvaluator-win-x64.zip.sha256'
        Evidence = 'StudyReportEvaluator-win-x64.evidence.json'
    }
    'windows-development-msix' = [ordered]@{
        Publish = $false
        Status = 'PASS_MECHANISM'
        Artifact = 'StudyReportEvaluator-win-x64.unsigned.test.msix'
        Sidecar = 'StudyReportEvaluator-win-x64.unsigned.test.msix.sha256'
        Evidence = 'StudyReportEvaluator-win-x64.unsigned.test.evidence.json'
    }
}

function Resolve-RepositoryRelativePath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    if ([System.IO.Path]::IsPathFullyQualified($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath($Path, $RepositoryRoot)
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

function Test-ByteArrayEqual {
    param(
        [Parameter(Mandatory)]
        [byte[]] $Left,

        [Parameter(Mandatory)]
        [byte[]] $Right
    )

    if ($Left.Length -ne $Right.Length) {
        return $false
    }

    for ($index = 0; $index -lt $Left.Length; $index++) {
        if ($Left[$index] -ne $Right[$index]) {
            return $false
        }
    }

    return $true
}

function Assert-NoDuplicateJsonProperties {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element,

        [Parameter(Mandatory)]
        [string] $Location
    )

    switch ($Element.ValueKind) {
        ([System.Text.Json.JsonValueKind]::Object) {
            $names = [System.Collections.Generic.HashSet[string]]::new(
                [System.StringComparer]::Ordinal)
            foreach ($property in $Element.EnumerateObject()) {
                if (-not $names.Add($property.Name)) {
                    throw "Release matrix contains a duplicate JSON property at $Location."
                }

                Assert-NoDuplicateJsonProperties `
                    -Element $property.Value `
                    -Location "$Location/$($property.Name)"
            }
        }
        ([System.Text.Json.JsonValueKind]::Array) {
            $index = 0
            foreach ($item in $Element.EnumerateArray()) {
                Assert-NoDuplicateJsonProperties `
                    -Element $item `
                    -Location "$Location/$index"
                $index++
            }
        }
    }
}

function Assert-ExactPropertySet {
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Value,

        [Parameter(Mandatory)]
        [string[]] $ExpectedNames,

        [Parameter(Mandatory)]
        [string] $Description
    )

    $actualNames = @($Value.Keys | ForEach-Object { [string]$_ })
    if ($actualNames.Count -ne $ExpectedNames.Count -or
        @($actualNames | Where-Object { $_ -cnotin $ExpectedNames }).Count -ne 0 -or
        @($ExpectedNames | Where-Object { $_ -cnotin $actualNames }).Count -ne 0) {
        throw "$Description property set is not closed."
    }
}

function Test-IsJsonInteger {
    param([AllowNull()] $Value)

    return $Value -is [byte] -or
        $Value -is [sbyte] -or
        $Value -is [int16] -or
        $Value -is [uint16] -or
        $Value -is [int32] -or
        $Value -is [uint32] -or
        $Value -is [int64] -or
        $Value -is [uint64]
}

function Read-StrictJsonObject {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Description
    )

    try {
        $json = $Utf8NoBom.GetString([System.IO.File]::ReadAllBytes($Path))
    }
    catch {
        throw "$Description must be strict UTF-8."
    }

    $document = $null
    try {
        $document = [System.Text.Json.JsonDocument]::Parse($json)
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            throw "$Description root must be an object."
        }

        Assert-NoDuplicateJsonProperties -Element $document.RootElement -Location '$'
    }
    catch {
        throw "$Description JSON is invalid: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $document) {
            $document.Dispose()
        }
    }

    if (-not (Test-Json -Json $json -ErrorAction SilentlyContinue)) {
        throw "$Description is not strict JSON."
    }

    return $json | ConvertFrom-Json -AsHashtable -DateKind String
}

function Resolve-ReferencedFile {
    param(
        [Parameter(Mandatory)]
        [string] $ArtifactRoot,

        [Parameter(Mandatory)]
        [string] $FileName,

        [Parameter(Mandatory)]
        [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($FileName) -or
        [System.IO.Path]::GetFileName($FileName) -cne $FileName -or
        $FileName.Contains([System.IO.Path]::DirectorySeparatorChar) -or
        $FileName.Contains([System.IO.Path]::AltDirectorySeparatorChar)) {
        throw "$Description must be a safe basename."
    }

    $path = [System.IO.Path]::GetFullPath((Join-Path $ArtifactRoot $FileName))
    $rootPrefix = $ArtifactRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    if (-not $path.StartsWith($rootPrefix, $comparison) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$Description is missing from the artifact directory."
    }

    $item = Get-Item -LiteralPath $path -Force
    Assert-NotReparsePoint -Item $item -Description $Description
    if ($item.Length -le 0) {
        throw "$Description must be nonempty."
    }

    return $item
}

function Assert-FileDescriptor {
    param(
        [Parameter(Mandatory)]
        [string] $ArtifactRoot,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Descriptor,

        [Parameter(Mandatory)]
        [string] $ExpectedFileName,

        [Parameter(Mandatory)]
        [string] $Description
    )

    if ([string]$Descriptor.fileName -cne $ExpectedFileName) {
        throw "$Description basename does not match the artifact kind."
    }

    $item = Resolve-ReferencedFile `
        -ArtifactRoot $ArtifactRoot `
        -FileName ([string]$Descriptor.fileName) `
        -Description $Description
    if ([long]$Descriptor.bytes -ne $item.Length) {
        throw "$Description size does not match the matrix."
    }

    $sha256 = Get-Sha256Hex -Path $item.FullName
    if ([string]$Descriptor.sha256 -cne $sha256) {
        throw "$Description SHA-256 does not match the matrix."
    }

    return [pscustomobject]@{
        Item = $item
        Sha256 = $sha256
    }
}

function Assert-ZipEvidenceBinding {
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Evidence,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Matrix,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Row
    )

    Assert-ExactPropertySet `
        -Value $Evidence `
        -ExpectedNames @(
            'schemaVersion',
            'evidenceKind',
            'status',
            'sourceCommit',
            'sourceStatusEntryCount',
            'sourceStatusSha256',
            'productVersion',
            'host',
            'package',
            'checks') `
        -Description 'Windows ZIP evidence'
    Assert-ExactPropertySet `
        -Value $Evidence.host `
        -ExpectedNames @(
            'osName',
            'osVersion',
            'osBuild',
            'osArchitecture',
            'processArchitecture') `
        -Description 'Windows ZIP evidence host'
    Assert-ExactPropertySet `
        -Value $Evidence.package `
        -ExpectedNames @('fileName', 'bytes', 'sha256') `
        -Description 'Windows ZIP evidence package'
    $requiredChecks = @(
        'sidecarVerified',
        'safeLayoutVerified',
        'bundledCliVerified',
        'cleanExtractVerified',
        'apphostLaunchVerified',
        'externalRuntimeAbsentVerified',
        'userWorkbookExcluded',
        'inputUnchangedVerified')
    Assert-ExactPropertySet `
        -Value $Evidence.checks `
        -ExpectedNames $requiredChecks `
        -Description 'Windows ZIP evidence checks'

    $checksPass = @($requiredChecks | Where-Object {
            $Evidence.checks[$_] -isnot [bool] -or
            -not [bool]$Evidence.checks[$_]
        }).Count -eq 0
    if (-not (Test-IsJsonInteger $Evidence.schemaVersion) -or
        [int]$Evidence.schemaVersion -ne 1 -or
        [string]$Evidence.evidenceKind -cne 'windows-zip-required' -or
        [string]$Evidence.status -cne 'PASS_REQUIRED' -or
        [string]$Evidence.sourceCommit -cne [string]$Matrix.sourceCommit -or
        -not (Test-IsJsonInteger $Evidence.sourceStatusEntryCount) -or
        [int]$Evidence.sourceStatusEntryCount -ne 0 -or
        [string]$Evidence.sourceStatusSha256 -cne $EmptyUtf8Sha256 -or
        [string]$Evidence.productVersion -cne [string]$Matrix.productVersion -or
        [string]$Evidence.host.osName -cne [string]$Row.verification.osName -or
        [string]$Evidence.host.osVersion -cne [string]$Row.verification.osVersion -or
        -not (Test-IsJsonInteger $Evidence.host.osBuild) -or
        [int]$Evidence.host.osBuild -ne [int]$Row.verification.osBuild -or
        [string]$Evidence.host.osArchitecture -cne [string]$Row.verification.osArchitecture -or
        [string]$Evidence.host.processArchitecture -cne [string]$Row.verification.processArchitecture -or
        [string]$Evidence.package.fileName -cne [string]$Row.artifact.fileName -or
        -not (Test-IsJsonInteger $Evidence.package.bytes) -or
        [long]$Evidence.package.bytes -ne [long]$Row.artifact.bytes -or
        [string]$Evidence.package.sha256 -cne [string]$Row.artifact.sha256 -or
        -not $checksPass) {
        throw 'Windows ZIP evidence is not bound to the matrix, package, environment, and required checks.'
    }
}

function Assert-DevelopmentMsixEvidenceBinding {
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Evidence,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Matrix,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary] $Row
    )

    Assert-ExactPropertySet `
        -Value $Evidence `
        -ExpectedNames @(
            'schemaVersion',
            'evidenceKind',
            'measuredAtUtc',
            'status',
            'productionStatus',
            'installStatus',
            'sourceCommit',
            'sourceStatusEntryCount',
            'sourceStatusSha256',
            'host',
            'tool',
            'package',
            'syntheticAssets',
            'negativePolicyChecks',
            'limitations') `
        -Description 'Development MSIX evidence'
    Assert-ExactPropertySet `
        -Value $Evidence.host `
        -ExpectedNames @(
            'osBuild',
            'osArchitecture',
            'processArchitecture',
            'powerShell',
            'dotnetSdk') `
        -Description 'Development MSIX evidence host'
    Assert-ExactPropertySet `
        -Value $Evidence.tool `
        -ExpectedNames @(
            'packageId',
            'packageVersion',
            'packageContentHash',
            'makeAppxFileVersion',
            'makeAppxSha256',
            'authenticodeStatus',
            'signerSubject') `
        -Description 'Development MSIX evidence tool'
    Assert-ExactPropertySet `
        -Value $Evidence.package `
        -ExpectedNames @(
            'fileName',
            'bytes',
            'sha256',
            'entryCount',
            'identityName',
            'publisher',
            'version',
            'architecture',
            'signatureEntryCount',
            'blockMapHashMethod',
            'bundledCliSha256') `
        -Description 'Development MSIX evidence package'

    $measuredAtUtc = [System.DateTimeOffset]::MinValue
    $measuredAtValid = [System.DateTimeOffset]::TryParseExact(
        [string]$Evidence.measuredAtUtc,
        'O',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind,
        [ref]$measuredAtUtc)
    $expectedNegativeChecks = @(
        'INVALID_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED',
        'SIGNED_PUBLISHER_UNSIGNED_MARKER_REJECTED_WITH_OUTPUT_UNCHANGED')
    $expectedLimitations = @(
        'Development-only unsigned package; not for distribution.',
        'Standard App Installer UI, non-admin setup, signature trust, and production identity are not verified.',
        'Installation and package-installed application behavior were not run by this script.')
    $negativeChecks = @($Evidence.negativePolicyChecks)
    $limitations = @($Evidence.limitations)
    $bindingFailures = [System.Collections.Generic.List[string]]::new()
    $bindings = [ordered]@{
        schemaVersion = (Test-IsJsonInteger $Evidence.schemaVersion) -and [int]$Evidence.schemaVersion -eq 1
        evidenceKind = [string]$Evidence.evidenceKind -ceq 'windows-unsigned-msix-development-mechanism'
        measuredAtUtc = $measuredAtValid -and $measuredAtUtc -le [System.DateTimeOffset]::UtcNow.AddMinutes(5)
        status = [string]$Evidence.status -ceq 'PASS_MECHANISM'
        productionStatus = [string]$Evidence.productionStatus -cin @(
            'BLOCKED_EXTERNAL',
            'NOT_REQUIRED_CURRENT_SCOPE')
        installStatus = [string]$Evidence.installStatus -ceq 'NOT_RUN_REQUIRES_ELEVATED_DISPOSABLE_WINDOWS_11_HOST'
        sourceCommit = [string]$Evidence.sourceCommit -ceq [string]$Matrix.sourceCommit
        sourceStatusEntryCount = (Test-IsJsonInteger $Evidence.sourceStatusEntryCount) -and [int]$Evidence.sourceStatusEntryCount -eq 0
        sourceStatusSha256 = [string]$Evidence.sourceStatusSha256 -ceq $EmptyUtf8Sha256
        hostOsBuild = (Test-IsJsonInteger $Evidence.host.osBuild) -and [int]$Evidence.host.osBuild -eq [int]$Row.verification.osBuild
        hostOsArchitecture = [string]$Evidence.host.osArchitecture -ceq [string]$Row.verification.osArchitecture
        hostProcessArchitecture = [string]$Evidence.host.processArchitecture -ceq [string]$Row.verification.processArchitecture
        toolIdentity = [string]$Evidence.tool.packageId -ceq 'Microsoft.Windows.SDK.BuildTools' -and
            [string]$Evidence.tool.packageVersion -ceq $BuildToolsVersion -and
            [string]$Evidence.tool.packageContentHash -ceq $BuildToolsContentHash -and
            [string]$Evidence.tool.makeAppxFileVersion -like "$BuildToolsVersion*" -and
            [string]$Evidence.tool.makeAppxSha256 -match '^[0-9A-F]{64}$' -and
            [string]$Evidence.tool.authenticodeStatus -ceq 'Valid' -and
            [string]$Evidence.tool.signerSubject -like '*O=Microsoft Corporation*'
        hostToolchain = [string]$Evidence.host.powerShell -match '^7\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$' -and
            [string]$Evidence.host.dotnetSdk -ceq '10.0.400'
        packageFileName = [string]$Evidence.package.fileName -ceq [string]$Row.artifact.fileName
        packageBytes = (Test-IsJsonInteger $Evidence.package.bytes) -and [long]$Evidence.package.bytes -eq [long]$Row.artifact.bytes
        packageSha256 = [string]$Evidence.package.sha256 -ceq [string]$Row.artifact.sha256
        packageIdentity = [string]$Evidence.package.identityName -ceq $UnsignedMsixIdentityName -and
            [string]$Evidence.package.publisher -ceq $UnsignedMsixPublisher
        packageVersion = [string]$Evidence.package.version -ceq "$([string]$Matrix.productVersion).0"
        packageArchitecture = [string]$Evidence.package.architecture -ceq 'x64'
        packageEntryCount = (Test-IsJsonInteger $Evidence.package.entryCount) -and [int]$Evidence.package.entryCount -gt 0
        packageSignature = (Test-IsJsonInteger $Evidence.package.signatureEntryCount) -and [int]$Evidence.package.signatureEntryCount -eq 0
        packageBlockMap = [string]$Evidence.package.blockMapHashMethod -ceq 'http://www.w3.org/2001/04/xmlenc#sha256'
        packageBundledCli = [string]$Evidence.package.bundledCliSha256 -match '^[0-9A-F]{64}$'
        negativePolicyChecks = $negativeChecks.Count -eq $expectedNegativeChecks.Count -and
            @($expectedNegativeChecks | Where-Object { $_ -cnotin $negativeChecks }).Count -eq 0
        limitations = $limitations.Count -eq $expectedLimitations.Count -and
            @($expectedLimitations | Where-Object { $_ -cnotin $limitations }).Count -eq 0
    }
    foreach ($binding in $bindings.GetEnumerator()) {
        if (-not [bool]$binding.Value) {
            $bindingFailures.Add([string]$binding.Key)
        }
    }

    if ($bindingFailures.Count -ne 0) {
        throw "Development MSIX evidence is not bound to the matrix, package, environment, and mechanism boundary. Failed checks: $([string]::Join(', ', $bindingFailures))."
    }

    $syntheticAssets = @($Evidence.syntheticAssets)
    if ($syntheticAssets.Count -ne 3) {
        throw 'Development MSIX evidence must contain exactly three synthetic asset records.'
    }

    $assetNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $expectedAssets = [ordered]@{
        'Square44x44Logo.png' = 44
        'Square150x150Logo.png' = 150
        'StoreLogo.png' = 50
    }
    foreach ($asset in $syntheticAssets) {
        Assert-ExactPropertySet `
            -Value $asset `
            -ExpectedNames @('fileName', 'width', 'height', 'sha256') `
            -Description 'Development MSIX synthetic asset evidence'
        $assetName = [string]$asset.fileName
        if (-not $expectedAssets.Contains($assetName) -or
            -not $assetNames.Add($assetName) -or
            -not (Test-IsJsonInteger $asset.width) -or
            -not (Test-IsJsonInteger $asset.height) -or
            [int]$asset.width -ne [int]$expectedAssets[$assetName] -or
            [int]$asset.height -ne [int]$expectedAssets[$assetName] -or
            [string]$asset.sha256 -notmatch '^[0-9A-F]{64}$') {
            throw 'Development MSIX synthetic asset evidence is invalid or duplicated.'
        }
    }

    if (@($expectedAssets.Keys | Where-Object { -not $assetNames.Contains($_) }).Count -ne 0) {
        throw 'Development MSIX synthetic asset evidence is incomplete.'
    }
}

if ($PSVersionTable.PSEdition -cne 'Core' -or
    $PSVersionTable.PSVersion -lt [System.Version]'7.4') {
    throw 'PowerShell Core 7.4 or later is required.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$schemaPath = Join-Path $repositoryRoot 'eng\schemas\platform-release-matrix-v1.schema.json'
$versionTool = Join-Path $repositoryRoot 'dev\version.ps1'
$matrixFullPath = Resolve-RepositoryRelativePath -Path $MatrixPath -RepositoryRoot $repositoryRoot
$artifactRoot = Resolve-RepositoryRelativePath -Path $ArtifactDirectory -RepositoryRoot $repositoryRoot

foreach ($requiredFile in @(
        [pscustomobject]@{ Path = $schemaPath; Description = 'Release matrix schema' },
        [pscustomobject]@{ Path = $matrixFullPath; Description = 'Release matrix' })) {
    if (-not (Test-Path -LiteralPath $requiredFile.Path -PathType Leaf)) {
        throw "$($requiredFile.Description) file is missing."
    }

    Assert-NotReparsePoint `
        -Item (Get-Item -LiteralPath $requiredFile.Path -Force) `
        -Description $requiredFile.Description
}

if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
    throw 'Artifact directory is missing.'
}

$artifactRootItem = Get-Item -LiteralPath $artifactRoot -Force
Assert-NotReparsePoint -Item $artifactRootItem -Description 'Artifact directory'
$artifactRoot = $artifactRootItem.FullName

if ([string]::IsNullOrWhiteSpace($ExpectedProductVersion)) {
    if (-not (Test-Path -LiteralPath $versionTool -PathType Leaf)) {
        throw 'Product version tool is missing.'
    }

    $versionResult = & $versionTool show -Json | Out-String | ConvertFrom-Json
    if ([string]$versionResult.Status -cne 'PASS') {
        throw 'Product version tool did not return PASS.'
    }

    $ExpectedProductVersion = [string]$versionResult.Version
}

if ($ExpectedProductVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Expected product version must be a stable SemVer value.'
}

if ([string]::IsNullOrWhiteSpace($ExpectedSourceCommit)) {
    $ExpectedSourceCommit = ([string](@(
                git -C $repositoryRoot rev-parse HEAD
            ) -join '')).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the expected source commit.'
    }
}

if ($ExpectedSourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Expected source commit must be a lowercase 40-character Git object ID.'
}

try {
    $matrixBytes = [System.IO.File]::ReadAllBytes($matrixFullPath)
    $matrixJson = $Utf8NoBom.GetString($matrixBytes)
}
catch {
    throw 'Release matrix must be strict UTF-8.'
}

$jsonDocument = $null
try {
    $jsonDocument = [System.Text.Json.JsonDocument]::Parse($matrixJson)
    Assert-NoDuplicateJsonProperties -Element $jsonDocument.RootElement -Location '$'
}
catch {
    throw "Release matrix JSON is invalid: $($_.Exception.Message)"
}
finally {
    if ($null -ne $jsonDocument) {
        $jsonDocument.Dispose()
    }
}

$schemaErrors = @()
$schemaValid = Test-Json `
    -Json $matrixJson `
    -SchemaFile $schemaPath `
    -ErrorAction SilentlyContinue `
    -ErrorVariable schemaErrors
if (-not $schemaValid) {
    throw 'Release matrix schema validation failed.'
}

$matrix = $matrixJson | ConvertFrom-Json -AsHashtable
if ([string]$matrix.productVersion -cne $ExpectedProductVersion) {
    throw 'Release matrix product version does not match the expected version.'
}

if ([string]$matrix.sourceCommit -cne $ExpectedSourceCommit) {
    throw 'Release matrix source commit does not match the expected commit.'
}

$rows = @($matrix.rows)
if ($rows.Count -ne $ExpectedRows.Count) {
    throw 'Release matrix does not contain the exact required row count.'
}

$observedKinds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
$referencedNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$publishableAssets = [System.Collections.Generic.List[string]]::new()
foreach ($row in $rows) {
    $artifactKind = [string]$row.artifactKind
    if (-not $ExpectedRows.Contains($artifactKind) -or
        -not $observedKinds.Add($artifactKind)) {
        throw 'Release matrix contains an unknown or duplicate artifact kind.'
    }

    $expected = $ExpectedRows[$artifactKind]
    if ([bool]$row.publish -ne [bool]$expected.Publish -or
        [string]$row.status -cne [string]$expected.Status -or
        [string]$row.platform -cne 'windows' -or
        [string]$row.runtimeIdentifier -cne 'win-x64' -or
        [string]$row.verification.osName -cne 'Windows 11' -or
        [int]$row.verification.osBuild -lt 22000 -or
        [string]$row.verification.osArchitecture -cne 'X64' -or
        [string]$row.verification.processArchitecture -cne 'X64') {
        throw 'Release matrix row does not match its required publish, status, platform, or environment boundary.'
    }

    $osVersionParts = ([string]$row.verification.osVersion).Split('.')
    $osVersionBuild = 0
    if ($osVersionParts.Count -lt 3 -or
        -not [int]::TryParse(
            $osVersionParts[2],
            [System.Globalization.NumberStyles]::None,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$osVersionBuild) -or
        $osVersionBuild -ne [int]$row.verification.osBuild) {
        throw 'Release matrix Windows version and build are inconsistent.'
    }

    $artifact = Assert-FileDescriptor `
        -ArtifactRoot $artifactRoot `
        -Descriptor $row.artifact `
        -ExpectedFileName ([string]$expected.Artifact) `
        -Description "$artifactKind artifact"
    $sidecar = Assert-FileDescriptor `
        -ArtifactRoot $artifactRoot `
        -Descriptor $row.sidecar `
        -ExpectedFileName ([string]$expected.Sidecar) `
        -Description "$artifactKind sidecar"
    $evidence = Assert-FileDescriptor `
        -ArtifactRoot $artifactRoot `
        -Descriptor $row.evidence `
        -ExpectedFileName ([string]$expected.Evidence) `
        -Description "$artifactKind evidence"

    foreach ($fileName in @(
            [string]$row.artifact.fileName,
            [string]$row.sidecar.fileName,
            [string]$row.evidence.fileName)) {
        if (-not $referencedNames.Add($fileName)) {
            throw 'Release matrix references the same file more than once.'
        }
    }

    $expectedSidecarBytes = $Utf8NoBom.GetBytes(
        "$($artifact.Sha256)  $([string]$row.artifact.fileName)`n")
    $actualSidecarBytes = [System.IO.File]::ReadAllBytes($sidecar.Item.FullName)
    if (-not (Test-ByteArrayEqual -Left $actualSidecarBytes -Right $expectedSidecarBytes)) {
        throw "$artifactKind sidecar content does not exactly match the artifact."
    }

    $evidenceValue = Read-StrictJsonObject `
        -Path $evidence.Item.FullName `
        -Description "$artifactKind evidence"
    if ($artifactKind -ceq 'windows-zip') {
        Assert-ZipEvidenceBinding -Evidence $evidenceValue -Matrix $matrix -Row $row
    }
    else {
        Assert-DevelopmentMsixEvidenceBinding `
            -Evidence $evidenceValue `
            -Matrix $matrix `
            -Row $row
    }

    if ([bool]$row.publish) {
        $publishableAssets.Add([string]$row.artifact.fileName)
        $publishableAssets.Add([string]$row.sidecar.fileName)
    }
}

if ($observedKinds.Count -ne $ExpectedRows.Count -or
    @($ExpectedRows.Keys | Where-Object { -not $observedKinds.Contains($_) }).Count -ne 0 -or
    $publishableAssets.Count -ne 2 -or
    -not $publishableAssets.Contains('StudyReportEvaluator-win-x64.zip') -or
    -not $publishableAssets.Contains('StudyReportEvaluator-win-x64.zip.sha256')) {
    throw 'Release matrix does not resolve to the exact initial public asset set.'
}

[ordered]@{
    schemaVersion = 1
    productVersion = [string]$matrix.productVersion
    sourceCommit = [string]$matrix.sourceCommit
    rowCount = $rows.Count
    publishableAssets = $publishableAssets.ToArray()
    status = 'PASS'
} | ConvertTo-Json -Depth 3