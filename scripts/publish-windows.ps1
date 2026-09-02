#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$TargetFramework = 'net10.0'
$RuntimeIdentifier = 'win-x64'
$ApplicationName = 'StudyReportEvaluator.App'

function Assert-SupportedHost {
    if ($PSVersionTable.PSEdition -cne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
        throw 'PowerShell Core 7 or later is required. Windows PowerShell is not supported.'
    }

    if (-not $IsWindows) {
        throw 'P-01 publish is supported only on Windows 11 x64.'
    }

    if ([System.Environment]::OSVersion.Version.Build -lt 22000) {
        throw 'P-01 publish requires Windows 11 or later.'
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64 -or
        [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) {
        throw 'P-01 publish requires an x64 operating system and x64 PowerShell process.'
    }
}

function Assert-PathWithinRoot {
    param(
        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $relative = [System.IO.Path]::GetRelativePath($Root, $Path)
    $parentPrefix = '..' + [System.IO.Path]::DirectorySeparatorChar
    if ([System.IO.Path]::IsPathRooted($relative) -or
        $relative -eq '..' -or
        $relative.StartsWith($parentPrefix, [System.StringComparison]::Ordinal)) {
        throw "Path escapes the repository root: $Path"
    }
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileSystemInfo] $Item
    )

    if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reparse points are not allowed in P-01 output: $($Item.FullName)"
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

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string] $DotNetPath,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & $DotNetPath @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code $exitCode."
    }
}

function Assert-GlobalJsonSdk {
    param(
        [Parameter(Mandatory)]
        [string] $GlobalJsonPath,

        [Parameter(Mandatory)]
        [string] $ActualVersion
    )

    $globalJson = Get-Content -LiteralPath $GlobalJsonPath -Raw | ConvertFrom-Json
    if ([string]$globalJson.sdk.rollForward -cne 'latestPatch' -or
        [bool]$globalJson.sdk.allowPrerelease) {
        throw 'global.json must select a non-prerelease SDK with latestPatch roll-forward.'
    }

    try {
        $required = [System.Version]::Parse([string]$globalJson.sdk.version)
        $actual = [System.Version]::Parse($ActualVersion)
    }
    catch {
        throw "Unable to parse the SDK version selected by global.json: $ActualVersion"
    }

    $requiredFeatureBand = $required.Build - ($required.Build % 100)
    $actualFeatureBand = $actual.Build - ($actual.Build % 100)
    if ($actual.Major -ne $required.Major -or
        $actual.Minor -ne $required.Minor -or
        $actualFeatureBand -ne $requiredFeatureBand -or
        $actual -lt $required) {
        throw "dotnet selected SDK $actual, which is incompatible with global.json version $required and latestPatch roll-forward."
    }
}

function Assert-JsonEquivalent {
    param(
        [AllowNull()]
        [object] $Expected,

        [AllowNull()]
        [object] $Actual,

        [Parameter(Mandatory)]
        [string] $JsonPath
    )

    if ($null -eq $Expected -or $null -eq $Actual) {
        if ($null -ne $Expected -or $null -ne $Actual) {
            throw "Lock comparison failed at $JsonPath."
        }

        return
    }

    if ($Expected -is [System.Collections.IDictionary]) {
        if ($Actual -isnot [System.Collections.IDictionary] -or $Expected.Count -ne $Actual.Count) {
            throw "Lock object comparison failed at $JsonPath."
        }

        foreach ($key in $Expected.Keys) {
            if (-not $Actual.Contains($key)) {
                throw "RID lock is missing $JsonPath.$key."
            }

            Assert-JsonEquivalent -Expected $Expected[$key] -Actual $Actual[$key] -JsonPath "$JsonPath.$key"
        }

        return
    }

    if ($Expected -is [System.Collections.IList] -and $Expected -isnot [string]) {
        if ($Actual -isnot [System.Collections.IList] -or $Expected.Count -ne $Actual.Count) {
            throw "Lock array comparison failed at $JsonPath."
        }

        for ($index = 0; $index -lt $Expected.Count; $index++) {
            Assert-JsonEquivalent -Expected $Expected[$index] -Actual $Actual[$index] -JsonPath "$JsonPath[$index]"
        }

        return
    }

    if (-not [object]::Equals($Expected, $Actual)) {
        throw "Lock value comparison failed at $JsonPath."
    }
}

function Assert-RidLockMatchesCanonicalLock {
    param(
        [Parameter(Mandatory)]
        [string] $CanonicalLockPath,

        [Parameter(Mandatory)]
        [string] $RidLockPath,

        [Parameter(Mandatory)]
        [string] $ProjectName
    )

    if (-not (Test-Path -LiteralPath $RidLockPath -PathType Leaf)) {
        throw "RID lock was not generated for $ProjectName."
    }

    $canonical = Get-Content -LiteralPath $CanonicalLockPath -Raw | ConvertFrom-Json -AsHashtable
    $ridLock = Get-Content -LiteralPath $RidLockPath -Raw | ConvertFrom-Json -AsHashtable
    if ($canonical['version'] -ne 2 -or $ridLock['version'] -ne 2) {
        throw "Version 2 package locks are required for $ProjectName."
    }

    $canonicalTargets = $canonical['dependencies']
    $ridTargets = $ridLock['dependencies']
    if (-not $canonicalTargets.Contains($TargetFramework) -or
        -not $ridTargets.Contains($TargetFramework) -or
        -not $ridTargets.Contains("$TargetFramework/$RuntimeIdentifier") -or
        $ridTargets.Count -ne 2) {
        throw "RID lock targets are invalid for $ProjectName."
    }

    Assert-JsonEquivalent `
        -Expected $canonicalTargets[$TargetFramework] `
        -Actual $ridTargets[$TargetFramework] `
        -JsonPath "$ProjectName.dependencies.$TargetFramework"

    $canonicalPackages = $canonicalTargets[$TargetFramework]
    $ridPackages = $ridTargets["$TargetFramework/$RuntimeIdentifier"]
    foreach ($packageName in $ridPackages.Keys) {
        if (-not $canonicalPackages.Contains($packageName)) {
            throw "RID restore introduced package '$packageName' outside the canonical lock for $ProjectName."
        }

        $canonicalPackage = $canonicalPackages[$packageName]
        $ridPackage = $ridPackages[$packageName]
        foreach ($field in @('type', 'resolved', 'contentHash')) {
            if (-not $ridPackage.Contains($field) -or
                -not $canonicalPackage.Contains($field) -or
                -not [object]::Equals($ridPackage[$field], $canonicalPackage[$field])) {
                throw "RID restore changed $ProjectName package '$packageName' field '$field'."
            }
        }
    }
}

function Assert-Amd64PortableExecutable {
    param(
        [Parameter(Mandatory)]
        [string] $ExecutablePath
    )

    $bytes = [System.IO.File]::ReadAllBytes($ExecutablePath)
    if ($bytes.Length -lt 64 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        throw "Application executable is not a valid PE file: $ExecutablePath"
    }

    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    if ($peOffset -lt 0 -or $peOffset + 6 -gt $bytes.Length -or
        $bytes[$peOffset] -ne 0x50 -or
        $bytes[$peOffset + 1] -ne 0x45 -or
        $bytes[$peOffset + 2] -ne 0 -or
        $bytes[$peOffset + 3] -ne 0) {
        throw "Application executable has an invalid PE header: $ExecutablePath"
    }

    $machine = [System.BitConverter]::ToUInt16($bytes, $peOffset + 4)
    if ($machine -ne 0x8664) {
        throw "Application executable is not AMD64 (machine 0x$($machine.ToString('X4')))."
    }
}

function Get-CopilotCliVersion {
    param(
        [Parameter(Mandatory)]
        [string] $DotNetPath,

        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $output = & $DotNetPath msbuild $ProjectPath `
        -nologo `
        -getProperty:CopilotCliVersion `
        -property:RuntimeIdentifier=$RuntimeIdentifier
    $exitCode = $LASTEXITCODE
    $version = ([string](@($output) -join "`n")).Trim()
    if ($exitCode -ne 0 -or
        $version -notmatch '^[0-9][A-Za-z0-9._+-]{0,127}$') {
        throw 'Unable to resolve a safe Copilot CLI version from the pinned SDK package.'
    }

    return $version
}

function Get-NpmCopilotCliBinary {
    param(
        [Parameter(Mandatory)]
        [string] $Version,

        [Parameter(Mandatory)]
        [string] $DestinationDirectory
    )

    $npmCommand = Get-Command npm.cmd -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $npmCommand) {
        return $null
    }

    [void][System.IO.Directory]::CreateDirectory($DestinationDirectory)
    $packageName = '@github/copilot-win32-x64'
    $packageSpec = "$packageName@$Version"
    $expectedPackageShasum = '75265752e8f23150a17cd8f09c5e757bd9ff9374'
    $packOutput = & $npmCommand.Source pack $packageSpec `
        --ignore-scripts `
        --fetch-timeout=120000 `
        --fetch-retries=2 `
        --pack-destination $DestinationDirectory `
        --json
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "npm failed to acquire the pinned Copilot CLI package with exit code $exitCode."
    }

    try {
        $packRecords = @(($packOutput | Out-String | ConvertFrom-Json))
    }
    catch {
        throw 'npm returned an invalid package manifest for the Copilot CLI.'
    }

    if ($packRecords.Count -ne 1) {
        throw 'npm did not return exactly one Copilot CLI package record.'
    }

    $packRecord = $packRecords[0]
    if ([string]$packRecord.name -cne $packageName -or
        [string]$packRecord.version -cne $Version -or
        [string]$packRecord.integrity -notmatch '^sha512-[A-Za-z0-9+/]+={0,2}$' -or
        [string]$packRecord.shasum -cne $expectedPackageShasum) {
        throw 'npm returned Copilot CLI package metadata that does not match the pinned package.'
    }

    $archiveName = [string]$packRecord.filename
    if ([string]::IsNullOrWhiteSpace($archiveName) -or
        [System.IO.Path]::GetFileName($archiveName) -cne $archiveName -or
        [System.IO.Path]::GetExtension($archiveName) -cne '.tgz') {
        throw 'npm returned an unsafe Copilot CLI archive name.'
    }

    $archivePath = Join-Path $DestinationDirectory $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw 'The pinned Copilot CLI archive was not created.'
    }

    $tarPath = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path -LiteralPath $tarPath -PathType Leaf)) {
        throw 'Windows system tar is required to inspect the Copilot CLI package.'
    }

    $archiveEntries = @(& $tarPath -tzf $archivePath)
    if ($LASTEXITCODE -ne 0 -or $archiveEntries.Count -eq 0) {
        throw 'The Copilot CLI archive could not be listed.'
    }

    foreach ($archiveEntry in $archiveEntries) {
        $normalized = ([string]$archiveEntry).Replace('\', '/').TrimEnd('/')
        if ([string]::IsNullOrWhiteSpace($normalized) -or
            -not $normalized.StartsWith('package/', [System.StringComparison]::Ordinal)) {
            throw "Unsafe Copilot CLI archive entry: $archiveEntry"
        }

        foreach ($segment in $normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)) {
            if ($segment -eq '.' -or $segment -eq '..' -or
                $segment.Contains(':', [System.StringComparison]::Ordinal)) {
                throw "Unsafe Copilot CLI archive entry: $archiveEntry"
            }
        }
    }

    $extractionDirectory = Join-Path $DestinationDirectory 'extracted'
    [void][System.IO.Directory]::CreateDirectory($extractionDirectory)
    & $tarPath -xzf $archivePath -C $extractionDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'The Copilot CLI archive could not be extracted.'
    }

    foreach ($item in @(Get-ChildItem -LiteralPath $extractionDirectory -Force -Recurse)) {
        Assert-NotReparsePoint -Item $item
    }

    $cliPath = Join-Path $extractionDirectory 'package\copilot.exe'
    if (-not (Test-Path -LiteralPath $cliPath -PathType Leaf) -or
        (Get-Item -LiteralPath $cliPath).Length -le 0) {
        throw 'The pinned Copilot CLI binary is missing or empty.'
    }

    Assert-Amd64PortableExecutable -ExecutablePath $cliPath
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($cliPath)
    if ([string]$versionInfo.ProductVersion -cne $Version -and
        [string]$versionInfo.FileVersion -cne $Version) {
        throw 'The Copilot CLI binary version does not match the pinned SDK runtime version.'
    }

    return [System.IO.Path]::GetFullPath($cliPath)
}

function Assert-BundledCopilotRuntime {
    param(
        [Parameter(Mandatory)]
        [string] $PublishDirectory
    )

    $manifestPath = Join-Path $PublishDirectory 'copilot-runtime.json'
    $expectedRelativePath = 'runtimes/win-x64/native/copilot.exe'
    $cliPath = Join-Path $PublishDirectory $expectedRelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $cliPath -PathType Leaf)) {
        throw 'The bundled Copilot CLI manifest or binary is missing.'
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw 'The bundled Copilot CLI manifest is invalid JSON.'
    }

    $propertyNames = @($manifest.PSObject.Properties.Name)
    $expectedPropertyNames = @(
        'schemaVersion',
        'runtimeIdentifier',
        'cliVersion',
        'cliSha256',
        'sdkVersion',
        'cliRelativePath'
    )
    if ($propertyNames.Count -ne $expectedPropertyNames.Count -or
        @($propertyNames | Where-Object { $_ -cnotin $expectedPropertyNames }).Count -ne 0 -or
        [int]$manifest.schemaVersion -ne 1 -or
        [string]$manifest.runtimeIdentifier -cne $RuntimeIdentifier -or
        [string]$manifest.cliRelativePath -cne $expectedRelativePath -or
        [string]$manifest.cliVersion -notmatch '^[0-9][A-Za-z0-9._+-]{0,127}$' -or
        [string]$manifest.sdkVersion -notmatch '^[0-9][A-Za-z0-9._+-]{0,127}$' -or
        [string]$manifest.cliSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'The bundled Copilot CLI manifest violates the release contract.'
    }

    $cliItem = Get-Item -LiteralPath $cliPath -Force
    Assert-NotReparsePoint -Item $cliItem
    if ($cliItem.Length -le 0 -or
        (Get-Sha256Hex -Path $cliPath) -cne ([string]$manifest.cliSha256).ToUpperInvariant()) {
        throw 'The bundled Copilot CLI does not match its manifest hash.'
    }

    Assert-Amd64PortableExecutable -ExecutablePath $cliPath
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($cliPath)
    if ([string]$versionInfo.ProductVersion -cne [string]$manifest.cliVersion -and
        [string]$versionInfo.FileVersion -cne [string]$manifest.cliVersion) {
        throw 'The bundled Copilot CLI does not match its manifest version.'
    }

    $sdkPath = Join-Path $PublishDirectory 'GitHub.Copilot.SDK.dll'
    $sdkVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($sdkPath)
    $sdkPackageVersion = ([string]$sdkVersionInfo.ProductVersion).Split('+', 2)[0]
    if ($sdkPackageVersion -cne [string]$manifest.sdkVersion) {
        throw 'The bundled Copilot manifest does not match the published SDK package version.'
    }
}

function Assert-SafePublishLayout {
    param(
        [Parameter(Mandatory)]
        [string] $PublishDirectory
    )

    $rootItem = Get-Item -LiteralPath $PublishDirectory -Force
    Assert-NotReparsePoint -Item $rootItem

    $items = @(Get-ChildItem -LiteralPath $PublishDirectory -Force -Recurse)
    $files = @($items | Where-Object { -not $_.PSIsContainer })
    if ($files.Count -eq 0) {
        throw 'Publish output is empty.'
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $forbiddenSegments = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@('sample', 'samples', 'input', 'inputs', 'artifact', 'artifacts', 'test', 'tests', 'src', 'source', 'sources', 'secret', 'secrets', '.git'),
        [System.StringComparer]::OrdinalIgnoreCase)
    $forbiddenExtensions = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@('.cs', '.csproj', '.fs', '.fsproj', '.vb', '.vbproj', '.sln', '.slnx', '.ps1', '.pdb', '.pfx', '.p12', '.pem', '.key', '.snk', '.xlsx', '.xls', '.xlsm', '.csv'),
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

    foreach ($item in $items) {
        Assert-NotReparsePoint -Item $item
        $relative = [System.IO.Path]::GetRelativePath($PublishDirectory, $item.FullName)
        $parentPrefix = '..' + [System.IO.Path]::DirectorySeparatorChar
        if ([System.IO.Path]::IsPathRooted($relative) -or
            $relative -eq '..' -or
            $relative.StartsWith($parentPrefix, [System.StringComparison]::Ordinal)) {
            throw "Publish entry escapes its root: $relative"
        }

        $normalized = $relative.Replace('\', '/')
        if (-not $seen.Add($normalized)) {
            throw "Duplicate or case-colliding publish entry: $normalized"
        }

        foreach ($segment in $normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)) {
            if ($segment -eq '.' -or $segment -eq '..' -or $forbiddenSegments.Contains($segment)) {
                throw "Forbidden publish path segment: $normalized"
            }
        }

        foreach ($marker in $forbiddenMarkers) {
            if ($normalized.Contains($marker, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Forbidden test or Office dependency marker in publish output: $normalized"
            }
        }

        if (-not $item.PSIsContainer) {
            if ($item.Length -le 0) {
                throw "Zero-byte publish file is not allowed: $normalized"
            }

            if ($forbiddenExtensions.Contains([System.IO.Path]::GetExtension($item.Name)) -or
                $item.Name -ieq '.env' -or
                [System.Text.RegularExpressions.Regex]::IsMatch(
                    $item.Name,
                    '(^|[._-])(secret|password|credential|token)([._-]|$)',
                    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
                throw "Forbidden source, symbol, input, or secret file in publish output: $normalized"
            }
        }
    }

    $requiredFiles = @(
        "$ApplicationName.exe",
        "$ApplicationName.dll",
        "$ApplicationName.runtimeconfig.json",
        "$ApplicationName.deps.json",
        'StudyReportEvaluator.Core.dll',
        'DocumentFormat.OpenXml.dll',
        'DocumentFormat.OpenXml.Framework.dll',
        'GitHub.Copilot.SDK.dll',
        'copilot-runtime.json',
        'runtimes\win-x64\native\copilot.exe',
        'Avalonia.dll',
        'Avalonia.Win32.dll',
        'coreclr.dll',
        'hostfxr.dll',
        'hostpolicy.dll',
        'System.Private.CoreLib.dll'
    )
    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $PublishDirectory $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf) -or
            (Get-Item -LiteralPath $requiredPath).Length -le 0) {
            throw "Required self-contained publish file is missing or empty: $requiredFile"
        }
    }

    Assert-BundledCopilotRuntime -PublishDirectory $PublishDirectory

    if (Test-Path -LiteralPath (Join-Path $PublishDirectory 'copilot.exe')) {
        throw 'The Copilot CLI must be stored only under the pinned RID runtime directory.'
    }

    $runtimeConfigPath = Join-Path $PublishDirectory "$ApplicationName.runtimeconfig.json"
    $runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
    $runtimeOptionsProperty = $runtimeConfig.PSObject.Properties['runtimeOptions']
    if ($null -eq $runtimeOptionsProperty) {
        throw 'runtimeconfig.json is missing runtimeOptions.'
    }

    $runtimeOptions = $runtimeOptionsProperty.Value
    if ($null -ne $runtimeOptions.PSObject.Properties['framework']) {
        throw 'Self-contained runtimeconfig.json must not declare a framework property.'
    }

    $includedFrameworksProperty = $runtimeOptions.PSObject.Properties['includedFrameworks']
    if ($null -eq $includedFrameworksProperty) {
        throw 'Self-contained runtimeconfig.json must declare includedFrameworks.'
    }

    $includedFrameworks = @($includedFrameworksProperty.Value)
    $netCoreFramework = @($includedFrameworks | Where-Object { $_.name -ceq 'Microsoft.NETCore.App' })
    if ($netCoreFramework.Count -ne 1 -or
        -not ([string]$netCoreFramework[0].version).StartsWith('10.0.', [System.StringComparison]::Ordinal)) {
        throw 'Self-contained runtimeconfig.json must include Microsoft.NETCore.App 10.0.x.'
    }

    $depsPath = Join-Path $PublishDirectory "$ApplicationName.deps.json"
    $depsText = Get-Content -LiteralPath $depsPath -Raw
    foreach ($marker in $forbiddenMarkers) {
        if ($depsText.Contains($marker, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Forbidden test or Office dependency marker in deps.json: $marker"
        }
    }

    $deps = $depsText | ConvertFrom-Json
    if ([string]$deps.runtimeTarget.name -cne '.NETCoreApp,Version=v10.0/win-x64') {
        throw "deps.json runtime target is not win-x64: $($deps.runtimeTarget.name)"
    }

    $libraryNames = @($deps.libraries.PSObject.Properties.Name)
    foreach ($requiredPrefix in @(
        'StudyReportEvaluator.App/',
        'StudyReportEvaluator.Core/',
        'DocumentFormat.OpenXml/',
        'GitHub.Copilot.SDK/',
        'Avalonia/',
        'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/')) {
        if (-not ($libraryNames | Where-Object { $_.StartsWith($requiredPrefix, [System.StringComparison]::Ordinal) })) {
            throw "deps.json is missing required library '$requiredPrefix'."
        }
    }

    Assert-Amd64PortableExecutable -ExecutablePath (Join-Path $PublishDirectory "$ApplicationName.exe")
}

function Remove-PublishSymbols {
    param(
        [Parameter(Mandatory)]
        [string] $PublishDirectory
    )

    $items = @(Get-ChildItem -LiteralPath $PublishDirectory -Force -Recurse)
    foreach ($item in $items) {
        Assert-NotReparsePoint -Item $item
    }

    foreach ($symbol in @($items | Where-Object { -not $_.PSIsContainer -and $_.Extension -ieq '.pdb' })) {
        Remove-Item -LiteralPath $symbol.FullName -Force
    }
}

function Assert-ApplicationLaunch {
    param(
        [Parameter(Mandatory)]
        [string] $PublishDirectory
    )

    $executablePath = Join-Path $PublishDirectory "$ApplicationName.exe"
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executablePath
    $startInfo.WorkingDirectory = $PublishDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $false
    $startInfo.Environment['DOTNET_ROOT'] = Join-Path $PublishDirectory '__no-installed-dotnet__'
    $startInfo.Environment['DOTNET_ROOT_X64'] = Join-Path $PublishDirectory '__no-installed-dotnet-x64__'
    $startInfo.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    try {
        $started = $process.Start()
        if (-not $started) {
            throw 'The self-contained application process did not start.'
        }

        try {
            [void]$process.WaitForInputIdle(5000)
        }
        catch [System.InvalidOperationException] {
            # A startup liveness probe below remains authoritative.
        }

        if ($process.WaitForExit(1500)) {
            throw "The self-contained application exited during startup with code $($process.ExitCode)."
        }

        $closedGracefully = $process.CloseMainWindow() -and $process.WaitForExit(5000)
        if ($closedGracefully) {
            if ($process.ExitCode -ne 0) {
                throw "The self-contained application exited with code $($process.ExitCode)."
            }

            return
        }

        $process.Kill($true)
        if (-not $process.WaitForExit(5000)) {
            throw 'The self-contained application did not stop during controlled cleanup.'
        }
    }
    finally {
        if ($started) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill($true)
                    [void]$process.WaitForExit(5000)
                }
            }
            catch [System.InvalidOperationException] {
                # The process already ended between checks.
            }
        }

        $process.Dispose()
    }
}

Assert-SupportedHost

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'StudyReportEvaluator.slnx'
$globalJsonPath = Join-Path $repositoryRoot 'global.json'
$appProjectPath = Join-Path $repositoryRoot 'src\StudyReportEvaluator.App\StudyReportEvaluator.App.csproj'
$coreProjectDirectory = Join-Path $repositoryRoot 'src\StudyReportEvaluator.Core'
$appProjectDirectory = Join-Path $repositoryRoot 'src\StudyReportEvaluator.App'
$packageDirectory = Join-Path $repositoryRoot 'artifacts\package'
$publishParent = Join-Path $repositoryRoot 'artifacts\package\publish'
$finalPublishDirectory = Join-Path $publishParent $RuntimeIdentifier
$runId = [System.Guid]::NewGuid().ToString('N')
$temporaryPublishDirectory = Join-Path $publishParent ('.' + $RuntimeIdentifier + '-' + $runId)
$temporaryLockRelativePath = "obj\P01\$runId\packages.$RuntimeIdentifier.lock.json"
$temporaryCopilotDirectory = Join-Path $packageDirectory ('.copilot-cli-' + $runId)

foreach ($requiredPath in @($solutionPath, $globalJsonPath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required repository file is missing: $requiredPath"
    }
}

Assert-PathWithinRoot -Root $repositoryRoot -Path $publishParent
Assert-PathWithinRoot -Root $repositoryRoot -Path $finalPublishDirectory
Assert-PathWithinRoot -Root $repositoryRoot -Path $temporaryPublishDirectory
Assert-PathWithinRoot -Root $repositoryRoot -Path $temporaryCopilotDirectory
[void][System.IO.Directory]::CreateDirectory($publishParent)

foreach ($existingPath in @($publishParent, $finalPublishDirectory)) {
    if (Test-Path -LiteralPath $existingPath) {
        $existingItem = Get-Item -LiteralPath $existingPath -Force
        Assert-NotReparsePoint -Item $existingItem
        if (-not $existingItem.PSIsContainer) {
            throw "Expected an owned publish directory but found a file: $existingPath"
        }
    }
}

$canonicalLockPaths = @(
    (Join-Path $coreProjectDirectory 'packages.lock.json'),
    (Join-Path $appProjectDirectory 'packages.lock.json')
)
$canonicalLockHashes = @{}
foreach ($lockPath in $canonicalLockPaths) {
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        throw "Canonical package lock is missing: $lockPath"
    }

    $canonicalLockHashes[$lockPath] = Get-Sha256Hex -Path $lockPath
}

$temporaryLockDirectories = @(
    (Join-Path $coreProjectDirectory "obj\P01\$runId"),
    (Join-Path $appProjectDirectory "obj\P01\$runId")
)
$published = $false

try {
    Push-Location $repositoryRoot
    try {
        $dotNetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
        $actualSdkVersion = ((& $dotNetPath --version) | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($actualSdkVersion)) {
            throw 'Unable to resolve the dotnet SDK selected by global.json.'
        }

        Assert-GlobalJsonSdk -GlobalJsonPath $globalJsonPath -ActualVersion $actualSdkVersion

        Invoke-DotNet -DotNetPath $dotNetPath -Arguments @(
            'restore',
            $appProjectPath,
            '--locked-mode',
            '--property:CopilotSkipCliDownload=true',
            '--verbosity',
            'minimal'
        )

        Invoke-DotNet -DotNetPath $dotNetPath -Arguments @(
            'restore',
            $appProjectPath,
            '--runtime',
            $RuntimeIdentifier,
            '--force-evaluate',
            '--lock-file-path',
            $temporaryLockRelativePath,
            '--property:CopilotSkipCliDownload=true',
            '--property:SelfContained=true',
            '--property:PublishSingleFile=false',
            '--property:PublishTrimmed=false',
            '--property:PublishReadyToRun=false',
            '--verbosity',
            'minimal'
        )

        Assert-RidLockMatchesCanonicalLock `
            -CanonicalLockPath (Join-Path $coreProjectDirectory 'packages.lock.json') `
            -RidLockPath (Join-Path $coreProjectDirectory $temporaryLockRelativePath) `
            -ProjectName 'StudyReportEvaluator.Core'
        Assert-RidLockMatchesCanonicalLock `
            -CanonicalLockPath (Join-Path $appProjectDirectory 'packages.lock.json') `
            -RidLockPath (Join-Path $appProjectDirectory $temporaryLockRelativePath) `
            -ProjectName 'StudyReportEvaluator.App'

        foreach ($lockPath in $canonicalLockPaths) {
            if ((Get-Sha256Hex -Path $lockPath) -cne $canonicalLockHashes[$lockPath]) {
                throw "RID restore changed canonical package lock: $lockPath"
            }
        }

        $copilotCliVersion = Get-CopilotCliVersion `
            -DotNetPath $dotNetPath `
            -ProjectPath $appProjectPath
        $copilotCliPath = Get-NpmCopilotCliBinary `
            -Version $copilotCliVersion `
            -DestinationDirectory $temporaryCopilotDirectory
        $publishArguments = @(
            'publish',
            $appProjectPath,
            '--configuration',
            'Release',
            '--framework',
            $TargetFramework,
            '--runtime',
            $RuntimeIdentifier,
            '--self-contained',
            'true',
            '--no-restore',
            '--output',
            $temporaryPublishDirectory,
            '--property:CopilotSkipCliDownload=false',
            '--property:PublishSingleFile=false',
            '--property:PublishTrimmed=false',
            '--property:PublishReadyToRun=false',
            '--property:UseAppHost=true',
            '--property:DebugSymbols=false',
            '--property:DebugType=None',
            '--verbosity',
            'minimal'
        )
        if ($null -ne $copilotCliPath) {
            $publishArguments += "--property:CopilotCliBinaryPath=$copilotCliPath"
        }

        Invoke-DotNet -DotNetPath $dotNetPath -Arguments $publishArguments
    }
    finally {
        Pop-Location
    }

    Remove-PublishSymbols -PublishDirectory $temporaryPublishDirectory
    Assert-SafePublishLayout -PublishDirectory $temporaryPublishDirectory
    Assert-ApplicationLaunch -PublishDirectory $temporaryPublishDirectory

    if (Test-Path -LiteralPath $finalPublishDirectory) {
        Remove-Item -LiteralPath $finalPublishDirectory -Recurse -Force
    }

    [System.IO.Directory]::Move($temporaryPublishDirectory, $finalPublishDirectory)
    $published = $true
    Write-Output "Published self-contained unsigned folder: $finalPublishDirectory"
}
finally {
    foreach ($temporaryLockDirectory in $temporaryLockDirectories) {
        if (Test-Path -LiteralPath $temporaryLockDirectory) {
            Remove-Item -LiteralPath $temporaryLockDirectory -Recurse -Force
        }
    }

    if (-not $published -and (Test-Path -LiteralPath $temporaryPublishDirectory)) {
        Remove-Item -LiteralPath $temporaryPublishDirectory -Recurse -Force
    }

    if (Test-Path -LiteralPath $temporaryCopilotDirectory) {
        Remove-Item -LiteralPath $temporaryCopilotDirectory -Recurse -Force
    }

    foreach ($lockPath in $canonicalLockPaths) {
        if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf) -or
            (Get-Sha256Hex -Path $lockPath) -cne $canonicalLockHashes[$lockPath]) {
            throw "Canonical package lock changed during publish: $lockPath"
        }
    }
}
