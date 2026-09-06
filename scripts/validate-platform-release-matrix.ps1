#Requires -Version 7.4
#Requires -PSEdition Core

<#
.SYNOPSIS
Read-only v1 legacy, v2 final-matrix, or pre-clean-host candidate validation.
.DESCRIPTION
Use the latest pwsh on PATH with -NoLogo -NoProfile. MatrixPath and
CandidateRecordPath are mutually exclusive. V2/candidate requires explicit
ExpectedRepository and ExpectedCandidateRunId; neither is inferred from JSON.
Product version/source defaults remain the repository version tool and Git HEAD.

CandidateRecordPath must be ArtifactDirectory/release-candidate-record.json.
Candidate validation requires local EXE, ZIP and development MSIX bytes, their
sidecars and package evidence; it never reads or invents clean-host evidence.
Final v2 additionally binds the two fixed root descriptors candidateRecord and
cleanHostEvidence, but NEVER opens, downloads or rebuilds the MSIX binary.

C01's complete draft-2020-12 $defs validate each separate document. Control JSON
is limited to 1 MiB, clean-host JSON to 64 KiB, with strict UTF-8/no BOM, duplicate
member rejection and DateKind String (v2 requires PowerShell 7.5+). All v2 input
files and ancestors must be plain, non-hardlinked paths. Read handles deny writes
and deletes until validation completes. No file is created, changed or cleaned.

Success is one JSON object: schemaVersion=2, productVersion, sourceCommit,
candidate, candidateRecord, cleanHostEvidence (null for candidate), runtime,
rowCount=3, publishableAssets (the fixed EXE/ZIP and two sidecars), status,
developmentMsixVerification, cleanHostVerification, candidateRunVerification,
exeVersionVerification and limitations. PASS_CANDIDATE is NOT publication PASS.
Final PASS reports CANDIDATE_RECORD_ONLY and HUMAN_RECORDED, not execution proof.
The upstream workflow must independently verify the successful trusted
.github/workflows/release.yml run and its tag/commit. TRX/observation descriptors
are upstream/human records, not local raw-file requirements or public assets.
EXE internal version/layout rely on the P07 record bound to the actual EXE hash;
this validator does not independently inspect PE resources or execute binaries.
V2 errors are fixed C02 codes; received JSON, paths and exception bodies are not
printed. Schema-1 matrices retain the legacy contract and diagnostics below.
#>
[CmdletBinding(DefaultParameterSetName = 'Matrix')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Matrix')]
    [string] $MatrixPath,

    [Parameter(Mandatory, ParameterSetName = 'Candidate')]
    [string] $CandidateRecordPath,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory,

    [string] $ExpectedProductVersion,

    [string] $ExpectedSourceCommit,

    [string] $ExpectedRepository,

    [string] $ExpectedCandidateRunId
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

    # Bypass PowerShell's dictionary adapter so an untrusted JSON property
    # named "Keys" cannot replace the dictionary's real key collection.
    $actualNames = @($Value.psbase.Keys | ForEach-Object { [string]$_ })
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

function ConvertFrom-LegacyJsonElement {
    param(
        [Parameter(Mandatory)]
        [System.Text.Json.JsonElement] $Element
    )

    switch ($Element.ValueKind) {
        ([System.Text.Json.JsonValueKind]::Object) {
            # PowerShell hashtables are case-insensitive by default. Preserve
            # case-variant JSON names so the closed property-set checks reject
            # them instead of silently applying last-property-wins semantics.
            $value = [System.Collections.Specialized.OrderedDictionary]::new(
                [System.StringComparer]::Ordinal)
            foreach ($property in $Element.EnumerateObject()) {
                $value[$property.Name] = ConvertFrom-LegacyJsonElement -Element $property.Value
            }
            return $value
        }
        ([System.Text.Json.JsonValueKind]::Array) {
            $items = [System.Collections.Generic.List[object]]::new()
            foreach ($item in $Element.EnumerateArray()) {
                $items.Add((ConvertFrom-LegacyJsonElement -Element $item))
            }
            return ,$items.ToArray()
        }
        ([System.Text.Json.JsonValueKind]::String) {
            return $Element.GetString()
        }
        ([System.Text.Json.JsonValueKind]::Number) {
            $integer = [long]0
            if ($Element.TryGetInt64([ref]$integer)) {
                return $integer
            }

            $decimal = [decimal]0
            if ($Element.TryGetDecimal([ref]$decimal)) {
                return $decimal
            }

            return $Element.GetDouble()
        }
        ([System.Text.Json.JsonValueKind]::True) {
            return $true
        }
        ([System.Text.Json.JsonValueKind]::False) {
            return $false
        }
        ([System.Text.Json.JsonValueKind]::Null) {
            return $null
        }
        default {
            throw 'Release matrix JSON contains an unsupported value kind.'
        }
    }
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
    $value = $null
    try {
        $document = [System.Text.Json.JsonDocument]::Parse($json)
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            throw "$Description root must be an object."
        }

        Assert-NoDuplicateJsonProperties -Element $document.RootElement -Location '$'
        # Preserve ISO-8601 values as strings without the PowerShell 7.5-only
        # ConvertFrom-Json -DateKind parameter. The v1 contract remains 7.4-compatible.
        $value = ConvertFrom-LegacyJsonElement -Element $document.RootElement
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

    return $value
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

function Resolve-C02Path {
    param([string] $Path, [string] $Base)

    if ([string]::IsNullOrWhiteSpace($Path) -or
        ([System.IO.Path]::IsPathRooted($Path) -and -not [System.IO.Path]::IsPathFullyQualified($Path))) {
        throw 'C02_UNSAFE_PATH'
    }
    $root = [System.IO.Path]::GetPathRoot($Path)
    foreach ($segment in $Path.Substring($root.Length).Replace('\', '/').Split('/')) {
        if ($segment -ceq '' -or $segment -ceq '.') { continue }
        if ($segment -ceq '..' -or $segment.EndsWith('.') -or $segment.EndsWith(' ') -or
            $segment.Contains(':') -or $segment -match '~[0-9]+(?:\.|$)' -or
            $segment -match '^(CON|PRN|AUX|NUL|CLOCK\$|CONIN\$|CONOUT\$|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)' -or
            $segment.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
            throw 'C02_UNSAFE_PATH'
        }
    }
    $full = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Path, $Base))
    # Local filesystem only: no UNC/device namespace or network retrieval.
    if ($IsWindows -and [System.IO.Path]::GetPathRoot($full) -notmatch '^[A-Za-z]:\\$') {
        throw 'C02_UNSAFE_PATH'
    }
    return $full
}

function Assert-C02PlainDirectory {
    param([string] $Path)

    while (-not [string]::IsNullOrEmpty($Path)) {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        if (-not $item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'C02_UNSAFE_PARENT'
        }
        $Path = [System.IO.Path]::GetDirectoryName($Path)
    }
}

function Open-C02File {
    param(
        [string] $Path,
        [System.Collections.Generic.List[System.IO.Stream]] $Holds,
        [long] $MaximumBytes = 9007199254740991
    )

    Assert-C02PlainDirectory -Path ([System.IO.Path]::GetDirectoryName($Path))
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'C02_MISSING_FILE' }
    $item = Get-Item -LiteralPath $Path -Force
    if ([string]$item.LinkType -ceq 'HardLink' -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'C02_UNSAFE_FILE'
    }
    $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'Read')
    $Holds.Add($stream)
    if ($stream.Length -le 0 -or $stream.Length -gt $MaximumBytes) { throw 'C02_FILE_SIZE_LIMIT' }
    $hash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream))
    $stream.Position = 0
    return [pscustomobject]@{
        Stream = $stream
        Descriptor = [ordered]@{ fileName = $item.Name; bytes = $stream.Length; sha256 = $hash }
    }
}

function Read-C02BoundedBytes {
    param([System.IO.Stream] $Stream, [long] $Length, [int] $MaximumBytes = 1MB)

    if ($Length -le 0 -or $Length -gt $MaximumBytes) { throw 'C02_FILE_SIZE_LIMIT' }
    $bytes = [byte[]]::new([int]$Length)
    $Stream.ReadExactly($bytes, 0, $bytes.Length)
    if ($Stream.ReadByte() -ne -1) { throw 'C02_FILE_SIZE_LIMIT' }
    return ,$bytes
}

function Assert-C02JsonTokens {
    param([System.Text.Json.JsonElement] $Element)

    switch ($Element.ValueKind) {
        ([System.Text.Json.JsonValueKind]::Object) {
            $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            foreach ($property in $Element.EnumerateObject()) {
                if (-not $names.Add($property.Name)) { throw 'C02_JSON_DUPLICATE_PROPERTY' }
                Assert-C02JsonTokens -Element $property.Value
            }
        }
        ([System.Text.Json.JsonValueKind]::Array) {
            foreach ($item in $Element.EnumerateArray()) { Assert-C02JsonTokens -Element $item }
        }
        ([System.Text.Json.JsonValueKind]::Number) {
            # All numbers in these contracts are integers. Do not round floats,
            # exponents or oversized numbers into a passing counter/descriptor.
            $integer = [long]0
            if (-not $Element.TryGetInt64([ref]$integer)) { throw 'C02_JSON_INTEGER' }
        }
    }
}

function Get-C02JsonText {
    param([byte[]] $Bytes)

    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
        throw 'C02_UTF8'
    }
    try { $json = $Utf8NoBom.GetString($Bytes) }
    catch { throw 'C02_UTF8' }
    try { $document = [System.Text.Json.JsonDocument]::Parse($json) }
    catch { throw 'C02_JSON_INVALID' }
    try {
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            throw 'C02_JSON_INVALID'
        }
        Assert-C02JsonTokens -Element $document.RootElement
    }
    finally { $document.Dispose() }
    return $json
}

function ConvertFrom-C02Json {
    param([string] $Json, [string] $Schema, [string] $SchemaError = 'C02_SCHEMA_INVALID')

    if (-not [string]::IsNullOrEmpty($Schema) -and
        -not (Test-Json -Json $Json -Schema $Schema -ErrorAction SilentlyContinue -WarningAction SilentlyContinue)) {
        throw $SchemaError
    }
    # Get-C02JsonText must precede this call; ConvertFrom-Json alone accepts
    # comments/duplicate properties and can silently convert calendar strings.
    return ConvertFrom-Json -InputObject $Json -AsHashtable -DateKind String -Depth 64
}

function Read-C02Document {
    param([psobject] $File, [string] $Schema, [string] $SchemaError, [int] $MaximumBytes = 1MB)

    $File.Stream.Position = 0
    $bytes = Read-C02BoundedBytes -Stream $File.Stream -Length $File.Descriptor.bytes -MaximumBytes $MaximumBytes
    $json = Get-C02JsonText -Bytes $bytes
    return ConvertFrom-C02Json -Json $json -Schema $Schema -SchemaError $SchemaError
}

function Test-C02SameValue {
    param([AllowNull()] $Left, [AllowNull()] $Right)

    if ($null -eq $Left -or $null -eq $Right) { return $null -eq $Left -and $null -eq $Right }
    if ($Left -is [string] -or $Right -is [string]) {
        return $Left -is [string] -and $Right -is [string] -and
            [string]::Equals($Left, $Right, [System.StringComparison]::Ordinal)
    }
    if ($Left -is [bool] -or $Right -is [bool]) {
        return $Left -is [bool] -and $Right -is [bool] -and $Left -eq $Right
    }
    if ((Test-IsJsonInteger $Left) -or (Test-IsJsonInteger $Right)) {
        return (Test-IsJsonInteger $Left) -and (Test-IsJsonInteger $Right) -and [long]$Left -eq [long]$Right
    }
    if ($Left -is [System.Collections.IDictionary] -and $Right -is [System.Collections.IDictionary]) {
        # JSON keys named Count/Keys must not replace dictionary metadata.
        if ($Left.psbase.Count -ne $Right.psbase.Count) { return $false }
        foreach ($key in $Left.psbase.Keys) {
            if ($key -cnotin @($Right.psbase.Keys) -or -not (Test-C02SameValue -Left $Left[$key] -Right $Right[$key])) {
                return $false
            }
        }
        return $true
    }
    if ($Left -is [System.Collections.IList] -and $Right -is [System.Collections.IList]) {
        if ($Left.Count -ne $Right.Count) { return $false }
        for ($index = 0; $index -lt $Left.Count; $index++) {
            if (-not (Test-C02SameValue -Left $Left[$index] -Right $Right[$index])) { return $false }
        }
        return $true
    }
    return $false
}

function Open-C02Descriptor {
    param(
        [string] $ArtifactRoot, [System.Collections.IDictionary] $Descriptor,
        [System.Collections.Generic.List[System.IO.Stream]] $Holds,
        [long] $MaximumBytes = 9007199254740991
    )

    # Call only after the document's complete C01 schema: every fileName is a
    # fixed basename, not an extensible artifact or arbitrary observation path.
    $name = $Descriptor.fileName
    if ($name -isnot [string] -or $name -cne [System.IO.Path]::GetFileName($name) -or
        $name.Contains('/') -or $name.Contains('\')) { throw 'C02_UNSAFE_PATH' }
    $path = Resolve-C02Path -Path $name -Base $ArtifactRoot
    $file = Open-C02File -Path $path -Holds $Holds -MaximumBytes $MaximumBytes
    if (-not (Test-C02SameValue -Left $Descriptor.bytes -Right $file.Descriptor.bytes)) {
        throw 'C02_DESCRIPTOR_SIZE'
    }
    if (-not (Test-C02SameValue -Left $Descriptor.sha256 -Right $file.Descriptor.sha256)) {
        throw 'C02_DESCRIPTOR_HASH'
    }
    if ($file.Descriptor.fileName -cne $name) { throw 'C02_DESCRIPTOR_NAME' }
    return $file
}

function Assert-C02Identity {
    param(
        [System.Collections.IDictionary] $Value,
        [string] $Version, [string] $Commit, [string] $Repository, [string] $RunId
    )

    if (-not (Test-C02SameValue -Left $Value.productVersion -Right $Version) -or
        -not (Test-C02SameValue -Left $Value.sourceCommit -Right $Commit)) { throw 'C02_SOURCE_VERSION_BINDING' }
    if (-not (Test-C02SameValue -Left $Value.candidate.repository -Right $Repository) -or
        -not (Test-C02SameValue -Left $Value.candidate.runId -Right $RunId) -or
        $Value.candidate.workflow -cne '.github/workflows/release.yml') { throw 'C02_CANDIDATE_IDENTITY' }
}

function Assert-C02WindowsVersion {
    param([System.Collections.IDictionary] $Value)

    $version = $null
    if (-not [System.Version]::TryParse($Value.osVersion, [ref]$version) -or
        -not (Test-C02SameValue -Left $version.Build -Right $Value.osBuild)) {
        throw 'C02_OS_VERSION_BUILD'
    }
}

function Assert-C02StringFields {
    param([System.Collections.IDictionary] $Value, [string[]] $Names)

    # Keys is not a legacy evidence field. Reject it before the unchanged
    # legacy closed-set check accesses dictionary.Keys through the adapter.
    if (@($Value.psbase.Keys) -icontains 'Keys') { throw 'C02_EVIDENCE_TYPE' }
    foreach ($name in $Names) {
        if ($Value[$name] -isnot [string]) { throw 'C02_EVIDENCE_TYPE' }
    }
}

function Assert-C02LegacyEvidenceTypes {
    param([System.Collections.IDictionary] $Evidence, [switch] $Msix)

    # Legacy checks below remain authoritative for their closed field sets and
    # values. Guard their string casts on v2: a one-element JSON array must not
    # masquerade as a string. Legacy integer and bool checks already test types.
    Assert-C02StringFields -Value $Evidence -Names @('evidenceKind', 'status', 'sourceCommit', 'sourceStatusSha256')
    Assert-C02StringFields -Value $Evidence.host -Names @('osArchitecture', 'processArchitecture')
    Assert-C02StringFields -Value $Evidence.package -Names @('fileName', 'sha256')
    if (-not $Msix) {
        Assert-C02StringFields -Value $Evidence -Names @('productVersion')
        Assert-C02StringFields -Value $Evidence.host -Names @('osName', 'osVersion')
        Assert-C02StringFields -Value $Evidence.checks -Names @()
        return
    }
    Assert-C02StringFields -Value $Evidence -Names @('measuredAtUtc', 'productionStatus', 'installStatus')
    Assert-C02StringFields -Value $Evidence.host -Names @('powerShell', 'dotnetSdk')
    Assert-C02StringFields -Value $Evidence.tool -Names @(
        'packageId', 'packageVersion', 'packageContentHash', 'makeAppxFileVersion',
        'makeAppxSha256', 'authenticodeStatus', 'signerSubject')
    Assert-C02StringFields -Value $Evidence.package -Names @(
        'identityName', 'publisher', 'version', 'architecture', 'blockMapHashMethod', 'bundledCliSha256')
    foreach ($name in @('syntheticAssets', 'negativePolicyChecks', 'limitations')) {
        if ($Evidence[$name] -isnot [System.Collections.IList]) { throw 'C02_EVIDENCE_TYPE' }
    }
    foreach ($asset in $Evidence.syntheticAssets) {
        Assert-C02StringFields -Value $asset -Names @('fileName', 'sha256')
    }
    foreach ($value in @($Evidence.negativePolicyChecks) + @($Evidence.limitations)) {
        if ($value -isnot [string]) { throw 'C02_EVIDENCE_TYPE' }
    }
}

function Assert-C02ZipRuntime {
    param([System.IO.Stream] $Stream, [System.Collections.IDictionary] $Runtime)

    # No extraction, PE parser, native execution, or reading workbook contents.
    # The ZIP test owns the rest of the package layout/runtime-version proof.
    $Stream.Position = 0
    $archive = [System.IO.Compression.ZipArchive]::new($Stream, [System.IO.Compression.ZipArchiveMode]::Read, $true)
    try {
        if ($archive.Entries.Count -gt 10000) { throw 'C02_ZIP_LAYOUT' }
        $root = 'StudyReportEvaluator-win-x64/'
        $cliPath = 'runtimes/win-x64/native/copilot.exe'
        $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith($root, [System.StringComparison]::Ordinal) -or
                $entry.FullName.Contains('\') -or -not $seen.Add($entry.FullName) -or
                @($entry.FullName.Split('/') | Where-Object { $_ -cin @('.', '..') }).Count -ne 0 -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'C02_ZIP_LAYOUT' }
        }
        $manifestEntry = $archive.GetEntry($root + 'copilot-runtime.json')
        $cliEntry = $archive.GetEntry($root + $cliPath)
        if ($null -eq $manifestEntry -or $null -eq $cliEntry) { throw 'C02_ZIP_RUNTIME' }
        $manifestStream = $manifestEntry.Open()
        try {
            $bytes = Read-C02BoundedBytes -Stream $manifestStream -Length $manifestEntry.Length
            # The bundled MSBuild manifest is UTF-8 with BOM. This compatibility
            # applies ONLY to that manifest, never candidate/CH/package evidence.
            if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
                $withoutBom = [byte[]]::new($bytes.Length - 3)
                [System.Array]::Copy($bytes, 3, $withoutBom, 0, $withoutBom.Length)
                $bytes = $withoutBom
            }
            $manifest = ConvertFrom-C02Json -Json (Get-C02JsonText -Bytes $bytes)
        }
        finally { $manifestStream.Dispose() }
        $expectedManifest = [ordered]@{
            schemaVersion = 1
            runtimeIdentifier = 'win-x64'
            cliVersion = $Runtime.cliVersion
            cliSha256 = $Runtime.cliSha256
            sdkVersion = $Runtime.copilotSdk
            cliRelativePath = $cliPath
        }
        if (-not (Test-C02SameValue -Left $manifest -Right $expectedManifest)) { throw 'C02_ZIP_RUNTIME' }
        if ($cliEntry.Length -le 0 -or $cliEntry.Length -gt 512MB) { throw 'C02_ZIP_CLI_SIZE' }
        $cliStream = $cliEntry.Open()
        $hasher = [System.Security.Cryptography.IncrementalHash]::CreateHash([System.Security.Cryptography.HashAlgorithmName]::SHA256)
        try {
            $buffer = [byte[]]::new(65536)
            $count = [long]0
            while (($read = $cliStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $count += $read
                if ($count -gt $cliEntry.Length) { throw 'C02_ZIP_CLI_SIZE' }
                $hasher.AppendData($buffer, 0, $read)
            }
            $hash = [System.Convert]::ToHexString($hasher.GetHashAndReset())
            if ($count -ne $cliEntry.Length -or $hash -cne $Runtime.cliSha256) { throw 'C02_ZIP_CLI_HASH' }
        }
        finally { $hasher.Dispose(); $cliStream.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Assert-C02CleanHost {
    param([System.Collections.IDictionary] $Evidence, [System.Collections.IDictionary] $SingleFileEvidence)

    if (-not (Test-C02SameValue -Left $Evidence.package -Right $SingleFileEvidence.package) -or
        -not (Test-C02SameValue -Left $Evidence.runtime -Right $SingleFileEvidence.runtime)) {
        throw 'C02_CLEAN_HOST_BINDING'
    }
    Assert-C02WindowsVersion -Value $Evidence.host
    # C01 permits up to 19 fractional digits. Parse the calendar at seconds,
    # then round any sub-tick remainder UP solely for the future-time bound.
    $timestampMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Evidence.measuredAtUtc, '\A([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.([0-9]{1,19}))?Z\z')
    $time = [System.DateTimeOffset]::MinValue
    if (-not $timestampMatch.Success -or -not [System.DateTimeOffset]::TryParseExact(
            $timestampMatch.Groups[1].Value + 'Z', "yyyy-MM-dd'T'HH:mm:ss'Z'",
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal,
            [ref]$time)) { throw 'C02_CLEAN_HOST_TIME' }
    $fraction = $timestampMatch.Groups[2].Value
    $ticks = [long]::Parse($fraction.PadRight(7, '0').Substring(0, 7), [System.Globalization.CultureInfo]::InvariantCulture)
    if ($fraction.Length -gt 7 -and $fraction.Substring(7).Trim('0').Length -ne 0) { $ticks++ }
    if ($time.AddTicks($ticks) -gt [System.DateTimeOffset]::UtcNow.AddMinutes(5)) { throw 'C02_CLEAN_HOST_TIME' }
    # The full C01 schema already requires CH-01..06 PASS/non-null records and
    # the nine absent dependencies. There is no JSON switch to downgrade them.
    $protection = $Evidence.protection
    if (($protection.launchOutcome -ceq 'Allowed' -and $protection.warningActions -ne 0) -or
        ($protection.launchOutcome -ceq 'WarnedThenAllowed' -and $protection.warningActions -le 0)) {
        throw 'C02_PROTECTION_CONSISTENCY'
    }
}

function Invoke-C02Validation {
    param([string] $InputPath, [string] $RepositoryRoot, [switch] $CandidateOnly)

    if ($PSVersionTable.PSVersion -lt [System.Version]'7.5') { throw 'C02_POWERSHELL_75_REQUIRED' }
    if ([string]::IsNullOrWhiteSpace($ExpectedRepository) -or [string]::IsNullOrWhiteSpace($ExpectedCandidateRunId)) {
        throw 'C02_EXPECTED_IDENTITY_REQUIRED'
    }
    # Preserve the existing repository defaults, never defaults from a record.
    $version = $ExpectedProductVersion
    if ([string]::IsNullOrWhiteSpace($version)) {
        $versionJson = (& (Join-Path $RepositoryRoot 'dev\version.ps1') show -Json | Out-String)
        $versionValue = ConvertFrom-C02Json -Json (Get-C02JsonText -Bytes $Utf8NoBom.GetBytes($versionJson))
        if ($versionValue.Status -isnot [string] -or $versionValue.Status -cne 'PASS' -or
            $versionValue.Version -isnot [string]) { throw 'C02_EXPECTED_VERSION' }
        $version = $versionValue.Version
    }
    $commit = $ExpectedSourceCommit
    if ([string]::IsNullOrWhiteSpace($commit)) {
        $commit = (@(git -C $RepositoryRoot rev-parse HEAD 2>$null) -join '').Trim()
        if ($LASTEXITCODE -ne 0) { throw 'C02_EXPECTED_COMMIT' }
    }
    if ($version.Length -gt 64 -or $version -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
        throw 'C02_EXPECTED_VERSION'
    }
    if ($commit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'C02_EXPECTED_COMMIT' }

    $holds = [System.Collections.Generic.List[System.IO.Stream]]::new()
    try {
        $artifactRoot = Resolve-C02Path -Path $ArtifactDirectory -Base $RepositoryRoot
        Assert-C02PlainDirectory -Path $artifactRoot
        $inputFullPath = Resolve-C02Path -Path $InputPath -Base $RepositoryRoot
        $candidatePath = Join-Path $artifactRoot 'release-candidate-record.json'
        $pathComparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }
        if ($CandidateOnly -and
            (-not [string]::Equals($inputFullPath, $candidatePath, $pathComparison) -or
                [System.IO.Path]::GetFileName($inputFullPath) -cne 'release-candidate-record.json')) {
            throw 'C02_CANDIDATE_PATH'
        }
        $schemaFile = Open-C02File -Path (Join-Path $RepositoryRoot 'eng\schemas\platform-release-matrix-v2.schema.json') -Holds $holds -MaximumBytes 1MB
        $schemaText = Get-C02JsonText -Bytes (Read-C02BoundedBytes -Stream $schemaFile.Stream -Length $schemaFile.Descriptor.bytes)
        $schema = ConvertFrom-C02Json -Json $schemaText
        $schemas = @{}
        foreach ($definition in @('candidateRecord', 'cleanHostEvidence', 'singleFilePackageEvidence')) {
            # Never validate a detached nested definition: its refs need ALL defs.
            $schemas[$definition] = [ordered]@{
                '$schema' = $schema['$schema']
                '$defs' = $schema['$defs']
                '$ref' = '#/$defs/' + $definition
            } | ConvertTo-Json -Depth 64 -Compress
        }
        $inputFile = Open-C02File -Path $inputFullPath -Holds $holds -MaximumBytes 1MB
        $matrix = $null
        if ($CandidateOnly) {
            $candidateFile = $inputFile
        }
        else {
            $matrix = Read-C02Document -File $inputFile -Schema $schemaText -SchemaError 'C02_MATRIX_SCHEMA'
            Assert-C02Identity -Value $matrix -Version $version -Commit $commit -Repository $ExpectedRepository -RunId $ExpectedCandidateRunId
            $candidateFile = Open-C02Descriptor -ArtifactRoot $artifactRoot -Descriptor $matrix.candidateRecord -Holds $holds -MaximumBytes 1MB
        }
        $candidate = Read-C02Document -File $candidateFile -Schema $schemas.candidateRecord -SchemaError 'C02_CANDIDATE_SCHEMA'
        Assert-C02Identity -Value $candidate -Version $version -Commit $commit -Repository $ExpectedRepository -RunId $ExpectedCandidateRunId
        $byKind = [System.Collections.Generic.Dictionary[string,object]]::new([System.StringComparer]::Ordinal)
        foreach ($row in $candidate.rows) { $byKind.Add($row.artifactKind, $row) }
        if ($null -ne $matrix) {
            foreach ($row in $matrix.rows) {
                $candidateRow = $byKind[$row.artifactKind]
                foreach ($field in @('artifactKind', 'verification', 'artifact', 'sidecar', 'evidence')) {
                    if (-not (Test-C02SameValue -Left $row[$field] -Right $candidateRow[$field])) { throw 'C02_ROW_BINDING' }
                }
            }
        }

        $evidenceByKind = @{}
        $zipFile = $null
        $packageHost = $byKind['windows-singlefile-exe'].verification
        foreach ($kind in @('windows-singlefile-exe', 'windows-zip', 'windows-development-msix')) {
            $row = $byKind[$kind]
            Assert-C02WindowsVersion -Value $row.verification
            foreach ($field in @('osVersion', 'osBuild', 'osArchitecture', 'processArchitecture')) {
                # P07's Windows vs legacy Windows 11 osName is intentional.
                if (-not (Test-C02SameValue -Left $row.verification[$field] -Right $packageHost[$field])) {
                    throw 'C02_PACKAGE_HOST_BINDING'
                }
            }
            if ($CandidateOnly -or $kind -cne 'windows-development-msix') {
                $artifactFile = Open-C02Descriptor -ArtifactRoot $artifactRoot -Descriptor $row.artifact -Holds $holds
                if ($kind -ceq 'windows-zip') { $zipFile = $artifactFile }
            }
            $sidecarFile = Open-C02Descriptor -ArtifactRoot $artifactRoot -Descriptor $row.sidecar -Holds $holds -MaximumBytes 64KB
            $expectedBytes = $Utf8NoBom.GetBytes("$($row.artifact.sha256)  $($row.artifact.fileName)`n")
            $actualBytes = Read-C02BoundedBytes -Stream $sidecarFile.Stream -Length $sidecarFile.Descriptor.bytes -MaximumBytes 64KB
            if (-not (Test-ByteArrayEqual -Left $actualBytes -Right $expectedBytes)) { throw 'C02_SIDECAR_CONTENT' }
            $evidenceFile = Open-C02Descriptor -ArtifactRoot $artifactRoot -Descriptor $row.evidence -Holds $holds -MaximumBytes 1MB
            if ($kind -ceq 'windows-singlefile-exe') {
                $evidence = Read-C02Document -File $evidenceFile -Schema $schemas.singleFilePackageEvidence -SchemaError 'C02_P07_SCHEMA'
                if (-not (Test-C02SameValue -Left $evidence.productVersion -Right $version) -or
                    -not (Test-C02SameValue -Left $evidence.sourceCommit -Right $commit) -or
                    -not (Test-C02SameValue -Left $evidence.host -Right $row.verification) -or
                    -not (Test-C02SameValue -Left $evidence.package -Right $row.artifact)) { throw 'C02_P07_BINDING' }
            }
            else {
                $evidence = Read-C02Document -File $evidenceFile
                # Preserve, rather than approximate, the legacy closed evidence
                # checks; sanitize their diagnostics only on the v2 path.
                if ($kind -ceq 'windows-zip') {
                    try {
                        Assert-C02LegacyEvidenceTypes -Evidence $evidence
                        Assert-ZipEvidenceBinding -Evidence $evidence -Matrix $candidate -Row $row
                    }
                    catch { throw 'C02_ZIP_EVIDENCE' }
                }
                else {
                    try {
                        Assert-C02LegacyEvidenceTypes -Evidence $evidence -Msix
                        Assert-DevelopmentMsixEvidenceBinding -Evidence $evidence -Matrix $candidate -Row $row
                    }
                    catch { throw 'C02_MSIX_EVIDENCE' }
                }
            }
            $evidenceByKind[$kind] = $evidence
        }
        $singleFile = $evidenceByKind['windows-singlefile-exe']
        if (-not (Test-C02SameValue -Left $singleFile.runtime.cliSha256 -Right $evidenceByKind['windows-development-msix'].package.bundledCliSha256)) {
            throw 'C02_CLI_BINDING'
        }
        Assert-C02ZipRuntime -Stream $zipFile.Stream -Runtime $singleFile.runtime
        $cleanHostDescriptor = $null
        if (-not $CandidateOnly) {
            $cleanHostFile = Open-C02Descriptor -ArtifactRoot $artifactRoot -Descriptor $matrix.cleanHostEvidence -Holds $holds -MaximumBytes 64KB
            $cleanHost = Read-C02Document -File $cleanHostFile -Schema $schemas.cleanHostEvidence -SchemaError 'C02_CLEAN_HOST_SCHEMA' -MaximumBytes 64KB
            Assert-C02Identity -Value $cleanHost -Version $version -Commit $commit -Repository $ExpectedRepository -RunId $ExpectedCandidateRunId
            Assert-C02CleanHost -Evidence $cleanHost -SingleFileEvidence $singleFile
            $cleanHostDescriptor = $cleanHostFile.Descriptor
        }
        [ordered]@{
            schemaVersion = 2
            productVersion = $version
            sourceCommit = $commit
            candidate = $candidate.candidate
            candidateRecord = $candidateFile.Descriptor
            cleanHostEvidence = $cleanHostDescriptor
            runtime = $singleFile.runtime
            rowCount = 3
            publishableAssets = @(
                'StudyReportEvaluator-win-x64.exe', 'StudyReportEvaluator-win-x64.exe.sha256',
                'StudyReportEvaluator-win-x64.zip', 'StudyReportEvaluator-win-x64.zip.sha256')
            status = if ($CandidateOnly) { 'PASS_CANDIDATE' } else { 'PASS' }
            developmentMsixVerification = if ($CandidateOnly) { 'LOCAL_BINARY_AND_EVIDENCE' } else { 'CANDIDATE_RECORD_ONLY' }
            cleanHostVerification = if ($CandidateOnly) { 'NOT_RUN' } else { 'HUMAN_RECORDED' }
            candidateRunVerification = 'CALLER_BOUND_UPSTREAM_REQUIRED'
            exeVersionVerification = 'P07_RECORD_AND_PACKAGE_HASH_BOUND'
            limitations = @(
                'Offline validation does not verify GitHub run success, workflow trust or tag identity; the upstream workflow must verify them.',
                'EXE internal version/layout and TRX execution are upstream P07 records bound to package hashes, not independent PE inspection or test execution here.',
                'Clean-host evidence and observation descriptors are human records, not execution proof; raw records are not fetched or published.',
                'PASS_CANDIDATE is not publication eligibility; unsigned hashes do not establish publisher identity or production trust.')
        } | ConvertTo-Json -Depth 8
    }
    finally {
        foreach ($stream in $holds) { $stream.Dispose() }
    }
}

function Get-ReleaseMatrixSchemaVersion {
    param([string] $Path)

    # Even the bounded version probe must not follow a parent junction/hardlink
    # or a UNC path before the v2 reader can enforce its file-handle boundary.
    if ($IsWindows -and [System.IO.Path]::GetPathRoot($Path) -notmatch '^[A-Za-z]:\\$') { throw 'C02_UNSAFE_PATH' }
    Assert-C02PlainDirectory -Path ([System.IO.Path]::GetDirectoryName($Path))
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Release matrix file is missing.' }
    $item = Get-Item -LiteralPath $Path -Force
    Assert-NotReparsePoint -Item $item -Description 'Release matrix'
    if ([string]$item.LinkType -ceq 'HardLink') { throw 'C02_UNSAFE_FILE' }
    $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'Read')
    try { $bytes = Read-C02BoundedBytes -Stream $stream -Length $stream.Length }
    finally { $stream.Dispose() }
    try { $json = $Utf8NoBom.GetString($bytes) }
    catch { throw 'Release matrix must be strict UTF-8.' }
    try { $document = [System.Text.Json.JsonDocument]::Parse($json) }
    catch { throw 'Release matrix JSON is invalid: C02_JSON_INVALID' }
    try {
        $property = [System.Text.Json.JsonElement]::new()
        $version = 0
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object -or
            -not $document.RootElement.TryGetProperty('schemaVersion', [ref]$property) -or
            $property.ValueKind -ne [System.Text.Json.JsonValueKind]::Number -or
            -not $property.TryGetInt32([ref]$version) -or $version -notin @(1, 2)) { throw 'C02_SCHEMA_VERSION' }
        if ($version -eq 1) {
            Assert-NoDuplicateJsonProperties -Element $document.RootElement -Location '$'
        }
        else { Assert-C02JsonTokens -Element $document.RootElement }
        return $version
    }
    finally { $document.Dispose() }
}

# Dispatch before the unchanged v1 entry. Unsupported/ill-typed schema versions
# never fall through into legacy validation. C03 calls these normal CLI sets,
# not extracted private functions or a separate helper-module contract.
try {
    $dispatchRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if ($PSCmdlet.ParameterSetName -ceq 'Candidate') {
        Invoke-C02Validation -InputPath $CandidateRecordPath -RepositoryRoot $dispatchRoot -CandidateOnly
        return
    }
    $dispatchPath = Resolve-RepositoryRelativePath -Path $MatrixPath -RepositoryRoot $dispatchRoot
    if ((Get-ReleaseMatrixSchemaVersion -Path $dispatchPath) -eq 2) {
        Invoke-C02Validation -InputPath $MatrixPath -RepositoryRoot $dispatchRoot
        return
    }
}
catch {
    $code = $_.Exception.Message
    # Preserve the legacy parser diagnostics, without echoing new JSON bodies.
    if ($code.StartsWith('Release matrix ', [System.StringComparison]::Ordinal)) { throw }
    if ($code -cnotmatch '\AC02_[A-Z0-9_]+\z') { $code = 'C02_IO_OR_FORMAT' }
    [System.Console]::Error.WriteLine("C02_FAIL code=$code")
    exit 1
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

$schemaValid = Test-Json `
    -Json $matrixJson `
    -SchemaFile $schemaPath `
    -ErrorAction SilentlyContinue
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