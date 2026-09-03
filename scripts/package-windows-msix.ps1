#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding(DefaultParameterSetName = 'SignedTest')]
param(
    [string] $PublishedDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string] $IdentityName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Publisher,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $PublisherDisplayName,

    [Parameter(Mandatory)]
    [string] $Square44x44LogoPath,

    [Parameter(Mandatory)]
    [string] $Square150x150LogoPath,

    [Parameter(Mandatory)]
    [string] $StoreLogoPath,

    [Parameter(Mandatory, ParameterSetName = 'SignedTest')]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string] $TestCertificateThumbprint,

    [Parameter(Mandatory, ParameterSetName = 'UnsignedDevelopment')]
    [switch] $UnsignedDevelopment,

    [string] $ProductVersion,

    [string] $DisplayName = 'StudyReport Evaluator',

    [string] $Description = 'Evaluate study reports from local Excel workbooks.',

    [string] $WindowsSdkBinDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$UnsignedPackagePublisherMarker = 'OID.2.25.311729368913984317654407730594956997722=1'
$isUnsignedDevelopment = $PSCmdlet.ParameterSetName -ceq 'UnsignedDevelopment'
$PackageFileName = if ($isUnsignedDevelopment) {
    'StudyReportEvaluator-win-x64.unsigned.test.msix'
}
else {
    'StudyReportEvaluator-win-x64.test.msix'
}
$PublicPayloadRelativePaths = @(
    'README.md',
    'LICENSE',
    'docs\README.md',
    'docs\getting-started.md',
    'docs\features.md',
    'docs\custom-evaluator-guide.md',
    'docs\prompt-launch.md',
    'docs\privacy-and-data-handling.md',
    'docs\troubleshooting.md',
    'images\README.md',
    'images\01-input-workbook.png',
    'images\02-input-mapping.png',
    'images\03-design-knowledge.png',
    'images\04-design-custom-prompt.png',
    'images\05-execution-auto.png',
    'images\06-results-review.png',
    'images\07-output-export.png'
)

function Assert-SupportedHost {
    if ($PSVersionTable.PSEdition -cne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
        throw 'PowerShell Core 7 or later is required. Windows PowerShell is not supported.'
    }

    if (-not $IsWindows -or [System.Environment]::OSVersion.Version.Build -lt 22000) {
        throw 'MSIX mechanism packaging requires Windows 11 or later.'
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64 -or
        [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) {
        throw 'MSIX mechanism packaging requires an x64 operating system and x64 PowerShell process.'
    }
}

function Assert-SafeText {
    param(
        [Parameter(Mandatory)]
        [string] $Value,

        [Parameter(Mandatory)]
        [string] $Name,

        [int] $MaximumLength = 256
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value.Length -gt $MaximumLength -or
        $Value -cne $Value.Trim() -or
        $Value.IndexOfAny([char[]]@(0, 10, 13)) -ge 0) {
        throw "$Name is invalid."
    }
}

function Test-UnsignedDevelopmentPublisher {
    param([Parameter(Mandatory)][string] $Value)

    return $Value -match '^CN=[^,=]{1,128}, OID\.2\.25\.311729368913984317654407730594956997722=1$'
}

function Test-ContainsUnsignedPackagePublisherMarker {
    param([Parameter(Mandatory)][string] $Value)

    return $Value.Contains(
        $UnsignedPackagePublisherMarker,
        [System.StringComparison]::Ordinal)
}

function Assert-UnsignedDevelopmentPublisher {
    param([Parameter(Mandatory)][string] $Value)

    if (-not (Test-UnsignedDevelopmentPublisher -Value $Value)) {
        throw "Unsigned Windows 11 development packages require the fixed marker $UnsignedPackagePublisherMarker, which must be the final Publisher field."
    }
}

function Assert-NotReparsePoint {
    param([Parameter(Mandatory)][System.IO.FileSystemInfo] $Item)

    if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reparse points are not allowed in MSIX inputs: $($Item.FullName)"
    }
}

function Assert-PathWithinRoot {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Path
    )

    $relative = [System.IO.Path]::GetRelativePath($Root, $Path)
    $parentPrefix = '..' + [System.IO.Path]::DirectorySeparatorChar
    if ([System.IO.Path]::IsPathRooted($relative) -or
        $relative -eq '..' -or
        $relative.StartsWith($parentPrefix, [System.StringComparison]::Ordinal)) {
        throw "Path escapes the repository root: $Path"
    }
}

function Assert-SafeRelativePath {
    param([Parameter(Mandatory)][string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [System.IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath -eq '..' -or
        $RelativePath.StartsWith('..\', [System.StringComparison]::Ordinal) -or
        $RelativePath.StartsWith('../', [System.StringComparison]::Ordinal)) {
        throw "Unsafe package-relative path: $RelativePath"
    }

    foreach ($segment in $RelativePath.Replace('\', '/').Split(
            '/',
            [System.StringSplitOptions]::RemoveEmptyEntries)) {
        if ($segment -eq '.' -or
            $segment -eq '..' -or
            $segment.Contains(':', [System.StringComparison]::Ordinal)) {
            throw "Unsafe package-relative path: $RelativePath"
        }
    }
}

function Assert-SafePublishPayload {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][System.IO.FileSystemInfo[]] $Items
    )

    $seen = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $forbiddenSegments = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@(
            'sample', 'samples', 'input', 'inputs', 'artifact', 'artifacts',
            'test', 'tests', 'src', 'source', 'sources', 'secret', 'secrets', '.git'),
        [System.StringComparer]::OrdinalIgnoreCase)
    $forbiddenExtensions = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@(
            '.cs', '.csproj', '.fs', '.fsproj', '.vb', '.vbproj', '.sln', '.slnx',
            '.ps1', '.pdb', '.pfx', '.p12', '.pem', '.key', '.snk',
            '.xlsx', '.xls', '.xlsm', '.csv'),
        [System.StringComparer]::OrdinalIgnoreCase)
    $forbiddenMarkers = @(
        'Microsoft.Office',
        'Office.Interop',
        'Interop.Excel',
        'LibreOffice',
        'soffice',
        'Microsoft.NET.Test.Sdk',
        'xunit',
        'testhost',
        'Avalonia.Headless',
        'StudyReportEvaluator.App.Tests',
        'StudyReportEvaluator.Core.Tests'
    )

    foreach ($item in $Items) {
        $relative = [System.IO.Path]::GetRelativePath($Root, $item.FullName)
        Assert-SafeRelativePath -RelativePath $relative
        $normalized = $relative.Replace('\', '/')
        if (-not $seen.Add($normalized)) {
            throw "Duplicate or case-colliding package input: $normalized"
        }

        foreach ($segment in $normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)) {
            if ($forbiddenSegments.Contains($segment)) {
                throw "Forbidden package input segment: $normalized"
            }
        }

        foreach ($marker in $forbiddenMarkers) {
            if ($normalized.Contains($marker, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Forbidden test or Office dependency marker in package input: $normalized"
            }
        }

        if (-not $item.PSIsContainer) {
            if ($item.Length -le 0) {
                throw "Zero-byte package input is not allowed: $normalized"
            }

            if ($forbiddenExtensions.Contains([System.IO.Path]::GetExtension($item.Name)) -or
                $item.Name -ieq '.env' -or
                [System.Text.RegularExpressions.Regex]::IsMatch(
                    $item.Name,
                    '(^|[._-])(secret|password|credential|token)([._-]|$)',
                    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
                    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
                throw "Forbidden source, symbol, input, or secret file in package input: $normalized"
            }
        }
    }
}

function Assert-NoReparsePointsBelowRoot {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root)
    $targetPath = [System.IO.Path]::GetFullPath($Path)
    Assert-PathWithinRoot -Root $rootPath -Path $targetPath
    $relative = [System.IO.Path]::GetRelativePath($rootPath, $targetPath)
    if ($relative -eq '.') {
        return
    }

    $current = $rootPath
    $separators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    foreach ($segment in $relative.Split($separators, [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) {
            break
        }

        Assert-NotReparsePoint -Item (Get-Item -LiteralPath $current -Force)
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string] $Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream))
    }
    finally {
        $stream.Dispose()
    }
}

function Get-PngDimensions {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Approved PNG asset does not exist: $Path"
    }

    $item = Get-Item -LiteralPath $Path -Force
    Assert-NotReparsePoint -Item $item
    $bytes = [System.IO.File]::ReadAllBytes($item.FullName)
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    if ($bytes.Length -lt 24) {
        throw "Approved asset is not a valid PNG: $Path"
    }

    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) {
            throw "Approved asset is not a valid PNG: $Path"
        }
    }

    $width = ($bytes[16] -shl 24) -bor ($bytes[17] -shl 16) -bor ($bytes[18] -shl 8) -bor $bytes[19]
    $height = ($bytes[20] -shl 24) -bor ($bytes[21] -shl 16) -bor ($bytes[22] -shl 8) -bor $bytes[23]
    return [pscustomobject]@{ Path = $item.FullName; Width = $width; Height = $height }
}

function Assert-PngDimensions {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][int] $ExpectedWidth,
        [Parameter(Mandatory)][int] $ExpectedHeight
    )

    $actual = Get-PngDimensions -Path $Path
    if ($actual.Width -ne $ExpectedWidth -or $actual.Height -ne $ExpectedHeight) {
        throw "Approved PNG asset must be ${ExpectedWidth}x${ExpectedHeight}: $Path"
    }

    return $actual.Path
}

function Resolve-WindowsSdkTools {
    param(
        [string] $ExplicitDirectory,
        [Parameter(Mandatory)][bool] $RequireSignTool
    )

    $candidateDirectories = if ([string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        $roots = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
            (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
        foreach ($root in $roots) {
            Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                ForEach-Object { Join-Path $_.FullName 'x64' }
        }
    }
    else {
        @([System.IO.Path]::GetFullPath($ExplicitDirectory))
    }

    $resolved = @($candidateDirectories |
        Where-Object {
            (Test-Path -LiteralPath (Join-Path $_ 'makeappx.exe') -PathType Leaf) -and
            (-not $RequireSignTool -or
                (Test-Path -LiteralPath (Join-Path $_ 'signtool.exe') -PathType Leaf))
        } |
        Sort-Object -Descending)
    if ($resolved.Count -eq 0) {
        if ($RequireSignTool) {
            throw 'Windows SDK x64 MakeAppx.exe and SignTool.exe are required; install an approved Windows SDK or pass -WindowsSdkBinDirectory.'
        }

        throw 'Windows SDK x64 MakeAppx.exe is required; install an approved Windows SDK or pass -WindowsSdkBinDirectory.'
    }

    return [pscustomobject]@{
        MakeAppx = Join-Path $resolved[0] 'makeappx.exe'
        SignTool = if ($RequireSignTool) { Join-Path $resolved[0] 'signtool.exe' } else { $null }
    }
}

function Get-PackageVersion {
    param([string] $RequestedVersion, [string] $RepositoryRoot)

    $version = $RequestedVersion
    if ([string]::IsNullOrWhiteSpace($version)) {
        $result = & (Join-Path $RepositoryRoot 'dev\version.ps1') show -Json |
            Out-String |
            ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or [string]$result.Status -cne 'PASS') {
            throw 'Unable to read the repository product version.'
        }

        $version = [string]$result.Version
    }

    if ($version -notmatch '^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)$') {
        throw 'MSIX mechanism package requires a stable Major.Minor.Patch product version.'
    }

    foreach ($component in @($Matches.major, $Matches.minor, $Matches.patch)) {
        if ([int64]$component -gt 65535) {
            throw 'MSIX package version components must be at most 65535.'
        }
    }

    return "$($Matches.major).$($Matches.minor).$($Matches.patch).0"
}

function Invoke-Tool {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string[]] $Arguments)

    & $Path @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Tool failed with exit code $($LASTEXITCODE): $([System.IO.Path]::GetFileName($Path))"
    }
}

Assert-SupportedHost
Assert-SafeText -Value $Publisher -Name 'Publisher' -MaximumLength 512
Assert-SafeText -Value $PublisherDisplayName -Name 'PublisherDisplayName'
Assert-SafeText -Value $DisplayName -Name 'DisplayName'
Assert-SafeText -Value $Description -Name 'Description' -MaximumLength 2048

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$templatePath = Join-Path $repositoryRoot 'eng\packaging\windows\AppxManifest.xml'
$publishScriptPath = Join-Path $PSScriptRoot 'publish-windows.ps1'
$packageVersion = Get-PackageVersion -RequestedVersion $ProductVersion -RepositoryRoot $repositoryRoot
$tools = Resolve-WindowsSdkTools -ExplicitDirectory $WindowsSdkBinDirectory -RequireSignTool (-not $isUnsignedDevelopment)
$certificateThumbprint = $null
if ($isUnsignedDevelopment) {
    Assert-UnsignedDevelopmentPublisher -Value $Publisher
}
else {
    if (Test-ContainsUnsignedPackagePublisherMarker -Value $Publisher) {
        throw 'Signed test packages cannot use the unsigned-development Publisher OID identity.'
    }

    $certificateThumbprint = $TestCertificateThumbprint.ToUpperInvariant()
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$certificateThumbprint" -ErrorAction Stop
    $hasCodeSigningEku = @($certificate.EnhancedKeyUsageList | Where-Object {
            $_.ObjectId.Value -ceq '1.3.6.1.5.5.7.3.3'
        }).Count -ne 0
    if (-not $certificate.HasPrivateKey -or
        -not [string]::Equals($certificate.Subject, $Publisher, [System.StringComparison]::Ordinal) -or
        $certificate.NotBefore -gt [System.DateTime]::Now -or
        $certificate.NotAfter -le [System.DateTime]::Now -or
        -not $hasCodeSigningEku) {
        throw 'Test certificate must be current, have a private key and code-signing EKU, and its Subject must exactly match Publisher.'
    }
}

$square44 = Assert-PngDimensions -Path $Square44x44LogoPath -ExpectedWidth 44 -ExpectedHeight 44
$square150 = Assert-PngDimensions -Path $Square150x150LogoPath -ExpectedWidth 150 -ExpectedHeight 150
$storeLogo = Assert-PngDimensions -Path $StoreLogoPath -ExpectedWidth 50 -ExpectedHeight 50
if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "MSIX manifest template is missing: $templatePath"
}

$packageRoot = Join-Path $repositoryRoot 'artifacts\package\mechanism'
$finalPackagePath = Join-Path $packageRoot $PackageFileName
$runId = [System.Guid]::NewGuid().ToString('N')
$temporaryRoot = Join-Path $packageRoot ".msix-$runId"
$stagingDirectory = Join-Path $temporaryRoot 'staging'
$unpackedDirectory = Join-Path $temporaryRoot 'unpacked'
$temporaryPackagePath = Join-Path $temporaryRoot $PackageFileName
[void][System.IO.Directory]::CreateDirectory($packageRoot)
Assert-NoReparsePointsBelowRoot -Root $repositoryRoot -Path $packageRoot
$packageRootItem = Get-Item -LiteralPath $packageRoot -Force
Assert-NotReparsePoint -Item $packageRootItem
if (Test-Path -LiteralPath $finalPackagePath) {
    $existingPackage = Get-Item -LiteralPath $finalPackagePath -Force
    Assert-NotReparsePoint -Item $existingPackage
    if ($existingPackage.PSIsContainer) {
        throw "Expected an owned mechanism package file but found a directory: $finalPackagePath"
    }
}

try {
    if ([string]::IsNullOrWhiteSpace($PublishedDirectory)) {
        & $publishScriptPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Windows publish failed.'
        }

        $PublishedDirectory = Join-Path $repositoryRoot 'artifacts\package\publish\win-x64'
    }
    elseif (-not [System.IO.Path]::IsPathRooted($PublishedDirectory)) {
        $PublishedDirectory = [System.IO.Path]::GetFullPath($PublishedDirectory, $repositoryRoot)
    }
    else {
        $PublishedDirectory = [System.IO.Path]::GetFullPath($PublishedDirectory)
    }

    Assert-PathWithinRoot -Root $repositoryRoot -Path $PublishedDirectory
    Assert-NoReparsePointsBelowRoot -Root $repositoryRoot -Path $PublishedDirectory
    if (-not (Test-Path -LiteralPath $PublishedDirectory -PathType Container)) {
        throw "Published directory does not exist: $PublishedDirectory"
    }

    $publishRoot = Get-Item -LiteralPath $PublishedDirectory -Force
    Assert-NotReparsePoint -Item $publishRoot
    $publishItems = @(Get-ChildItem -LiteralPath $PublishedDirectory -Force -Recurse)
    foreach ($item in $publishItems) {
        Assert-NotReparsePoint -Item $item
    }
    Assert-SafePublishPayload -Root $PublishedDirectory -Items $publishItems

    foreach ($requiredFile in @(
        'StudyReportEvaluator.App.exe',
        'StudyReportEvaluator.App.dll',
        'StudyReportEvaluator.App.runtimeconfig.json',
        'StudyReportEvaluator.App.deps.json',
        'StudyReportEvaluator.Core.dll',
        'GitHub.Copilot.SDK.dll',
        'copilot-runtime.json',
        'runtimes\win-x64\native\copilot.exe',
        'coreclr.dll')) {
        $path = Join-Path $PublishedDirectory $requiredFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -le 0) {
            throw "Required publish file is missing or empty: $requiredFile"
        }
    }

    [void][System.IO.Directory]::CreateDirectory($stagingDirectory)
    foreach ($item in @(Get-ChildItem -LiteralPath $PublishedDirectory -Force)) {
        Copy-Item -LiteralPath $item.FullName -Destination $stagingDirectory -Recurse
    }
    $assetsDirectory = Join-Path $stagingDirectory 'Assets'
    [void][System.IO.Directory]::CreateDirectory($assetsDirectory)
    Copy-Item -LiteralPath $square44 -Destination (Join-Path $assetsDirectory 'Square44x44Logo.png')
    Copy-Item -LiteralPath $square150 -Destination (Join-Path $assetsDirectory 'Square150x150Logo.png')
    Copy-Item -LiteralPath $storeLogo -Destination (Join-Path $assetsDirectory 'StoreLogo.png')
    foreach ($relativePath in $PublicPayloadRelativePaths) {
        Assert-SafeRelativePath -RelativePath $relativePath
        $sourcePath = Join-Path $repositoryRoot $relativePath
        Assert-NoReparsePointsBelowRoot -Root $repositoryRoot -Path $sourcePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf) -or
            (Get-Item -LiteralPath $sourcePath -Force).Length -le 0) {
            throw "Required public package payload is missing or empty: $relativePath"
        }

        $destinationPath = Join-Path $stagingDirectory $relativePath
        if (Test-Path -LiteralPath $destinationPath) {
            throw "Public package payload collides with publish input: $relativePath"
        }

        [void][System.IO.Directory]::CreateDirectory(
            [System.IO.Path]::GetDirectoryName($destinationPath))
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
    }

    $manifestText = [System.IO.File]::ReadAllText($templatePath)
    $replacements = [ordered]@{
        '@IDENTITY_NAME@' = $IdentityName
        '@PUBLISHER@' = $Publisher
        '@PACKAGE_VERSION@' = $packageVersion
        '@DISPLAY_NAME@' = $DisplayName
        '@PUBLISHER_DISPLAY_NAME@' = $PublisherDisplayName
        '@DESCRIPTION@' = $Description
    }
    foreach ($entry in $replacements.GetEnumerator()) {
        $escaped = [System.Security.SecurityElement]::Escape([string]$entry.Value)
        $manifestText = $manifestText.Replace([string]$entry.Key, $escaped, [System.StringComparison]::Ordinal)
    }

    if (@($replacements.Keys | Where-Object {
            $manifestText.Contains([string]$_, [System.StringComparison]::Ordinal)
        }).Count -ne 0) {
        throw 'MSIX manifest contains an unresolved placeholder.'
    }

    try {
        [xml]$manifestText | Out-Null
    }
    catch {
        throw 'Generated MSIX manifest is not valid XML.'
    }

    $manifestPath = Join-Path $stagingDirectory 'AppxManifest.xml'
    [System.IO.File]::WriteAllText($manifestPath, $manifestText, $Utf8NoBom)
    Invoke-Tool -Path $tools.MakeAppx -Arguments @('pack', '/o', '/d', $stagingDirectory, '/p', $temporaryPackagePath)
    if (-not $isUnsignedDevelopment) {
        Invoke-Tool -Path $tools.SignTool -Arguments @('sign', '/fd', 'SHA256', '/sha1', $certificateThumbprint, $temporaryPackagePath)
        Invoke-Tool -Path $tools.SignTool -Arguments @('verify', '/pa', '/v', $temporaryPackagePath)
    }

    Invoke-Tool -Path $tools.MakeAppx -Arguments @('unpack', '/o', '/p', $temporaryPackagePath, '/d', $unpackedDirectory)

    if ($isUnsignedDevelopment -and
        (Test-Path -LiteralPath (Join-Path $unpackedDirectory 'AppxSignature.p7x'))) {
        throw 'Unsigned development MSIX unexpectedly contains an AppxSignature.p7x.'
    }

    [xml]$blockMap = Get-Content -LiteralPath (Join-Path $unpackedDirectory 'AppxBlockMap.xml') -Raw
    if ([string]$blockMap.BlockMap.HashMethod -cne 'http://www.w3.org/2001/04/xmlenc#sha256') {
        throw 'MSIX block map must use SHA-256.'
    }

    [xml]$unpackedManifest = Get-Content -LiteralPath (Join-Path $unpackedDirectory 'AppxManifest.xml') -Raw
    if ([string]$unpackedManifest.Package.Identity.Name -cne $IdentityName -or
        [string]$unpackedManifest.Package.Identity.Publisher -cne $Publisher -or
        [string]$unpackedManifest.Package.Identity.Version -cne $packageVersion -or
        [string]$unpackedManifest.Package.Identity.ProcessorArchitecture -cne 'x64') {
        throw 'Packed MSIX identity does not match requested values.'
    }

    foreach ($relativePath in $PublicPayloadRelativePaths) {
        $unpackedPath = Join-Path $unpackedDirectory $relativePath
        if (-not (Test-Path -LiteralPath $unpackedPath -PathType Leaf) -or
            (Get-Item -LiteralPath $unpackedPath -Force).Length -le 0) {
            throw "Packed MSIX is missing required public payload: $relativePath"
        }
    }

    $finalPackageSha256 = Get-Sha256Hex -Path $temporaryPackagePath
    [System.IO.File]::Move($temporaryPackagePath, $finalPackagePath, $true)
    if ($isUnsignedDevelopment) {
        Write-Output "Created unsigned Windows 11 development MSIX mechanism package: $finalPackagePath"
    }
    else {
        Write-Output "Created test-certificate MSIX mechanism package: $finalPackagePath"
    }

    Write-Output "SHA-256: $finalPackageSha256"
    if ($isUnsignedDevelopment) {
        Write-Output 'Release status: PASS_MECHANISM only; install only with elevated Add-AppxPackage -AllowUnsigned; not for distribution.'
    }
    else {
        Write-Output 'Release status: PASS_MECHANISM only; not trusted production signing.'
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
