#Requires -Version 7.0
#Requires -PSEdition Core

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $repositoryRoot ('work\build-singlefile-tests-' + [guid]::NewGuid().ToString('N'))
$pwshPath = (Get-Process -Id $PID).Path
$passed = 0

try {
    foreach ($case in @(
        @{ Name = 'default'; Arguments = @(); Events = 'pull,publish,package,launch' },
        @{ Name = 'skip-pull'; Arguments = @('-SkipPull'); Events = 'publish,package,launch' },
        @{ Name = 'build-only'; Arguments = @('-SkipPull', '-SkipLaunch'); Events = 'publish,package' },
        @{ Name = 'validate'; Arguments = @('-SkipPull', '-Validate'); Events = 'publish,package,validate,launch' },
        @{ Name = 'publish-failure'; Arguments = @('-SkipPull'); Events = 'publish'; Fails = $true },
        @{ Name = 'package-failure'; Arguments = @('-SkipPull'); Events = 'publish,package'; Fails = $true },
        @{ Name = 'validation-failure'; Arguments = @('-SkipPull', '-Validate'); Events = 'publish,package,validate'; Fails = $true },
        @{ Name = 'missing-exe'; Arguments = @('-SkipPull'); Events = 'publish,package'; Fails = $true },
        @{ Name = 'launch-failure'; Arguments = @('-SkipPull'); Events = 'publish,package,launch'; Fails = $true },
        @{ Name = 'commit-failure'; Arguments = @('-SkipPull'); Events = ''; Fails = $true }
    )) {
        $caseRoot = Join-Path $fixtureRoot ($case.Name + ' with spaces')
        $scriptsRoot = Join-Path $caseRoot 'scripts'
        [void][System.IO.Directory]::CreateDirectory($scriptsRoot)
        $packageRoot = Join-Path $caseRoot 'artifacts\package'
        [void][System.IO.Directory]::CreateDirectory($packageRoot)
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'build-singlefile.ps1') -Destination $caseRoot

        # A stale EXE must never be launched after any failed build step.
        Set-Content -LiteralPath (Join-Path $packageRoot 'StudyReportEvaluator-win-x64.exe') -Value 'stale'
        Set-Content -LiteralPath (Join-Path $scriptsRoot 'publish-windows.ps1') -Value @'
param([switch] $SingleFile)
if (-not $SingleFile) { throw 'SingleFile was not passed.' }
Add-Content -LiteralPath $env:BUILD_TEST_EVENTS -Value 'publish'
if ($env:BUILD_TEST_CASE -eq 'publish-failure') {
    & (Get-Process -Id $PID).Path -NoProfile -Command 'exit 23'
}
'@
        Set-Content -LiteralPath (Join-Path $scriptsRoot 'package-windows-singlefile.ps1') -Value @'
Add-Content -LiteralPath $env:BUILD_TEST_EVENTS -Value 'package'
if ($env:BUILD_TEST_CASE -eq 'package-failure') { throw 'Package failed.' }
$exe = Join-Path $PSScriptRoot '..\artifacts\package\StudyReportEvaluator-win-x64.exe'
if ($env:BUILD_TEST_CASE -eq 'missing-exe') {
    Remove-Item -LiteralPath $exe
} else {
    Set-Content -LiteralPath $exe -Value 'new'
}
'@
        Set-Content -LiteralPath (Join-Path $scriptsRoot 'test-windows-singlefile.ps1') -Value @'
param([string] $ResultsDirectory)
if (-not [System.IO.Path]::IsPathFullyQualified($ResultsDirectory)) { throw 'Results path is not absolute.' }
Add-Content -LiteralPath $env:BUILD_TEST_EVENTS -Value 'validate'
if ($env:BUILD_TEST_CASE -eq 'validation-failure') {
    & (Get-Process -Id $PID).Path -NoProfile -Command 'exit 24'
}
'@
        $wrapperPath = Join-Path $caseRoot 'invoke.ps1'
        Set-Content -LiteralPath $wrapperPath -Value @'
$ErrorActionPreference = 'Stop'
function git {
    if ($args[0] -eq 'status') { $global:LASTEXITCODE = 0; return }
    if ($args[0] -eq 'pull') {
        Add-Content -LiteralPath $env:BUILD_TEST_EVENTS -Value 'pull'
        $global:LASTEXITCODE = 0
        return
    }
    if ($args[0] -eq 'rev-parse') {
        $global:LASTEXITCODE = 0
        if ($env:BUILD_TEST_CASE -eq 'commit-failure') { $global:LASTEXITCODE = 25; return }
        return '123abcd'
    }
    throw 'Unexpected git command.'
}
function Start-Process {
    param([string] $FilePath, [string] $WorkingDirectory, [switch] $PassThru)
    if (-not $PassThru) { throw 'Missing PassThru.' }
    if ($FilePath -cne (Join-Path $PSScriptRoot 'artifacts\package\StudyReportEvaluator-win-x64.exe')) {
        throw 'Incorrect executable path.'
    }
    if ($WorkingDirectory -cne (Split-Path -Parent $FilePath)) { throw 'Incorrect working directory.' }
    if ((Get-Content -LiteralPath $FilePath -Raw).Trim() -cne 'new') { throw 'Stale EXE was launched.' }
    Add-Content -LiteralPath $env:BUILD_TEST_EVENTS -Value 'launch'
    if ($env:BUILD_TEST_CASE -eq 'launch-failure') { throw 'Launch denied.' }
    return [pscustomobject]@{ Id = 12345 }
}
$before = (Get-Location).Path
try {
    & (Join-Path $PSScriptRoot 'build-singlefile.ps1') @args
} catch {
    [Console]::Error.WriteLine($_.ToString())
    exit 1
} finally {
    if ((Get-Location).Path -cne $before) { throw 'Caller location was not restored.' }
}
'@
        $eventsPath = Join-Path $caseRoot 'events.txt'
        Set-Content -LiteralPath $eventsPath -Value '' -NoNewline
        $originalCase = $env:BUILD_TEST_CASE
        $originalEvents = $env:BUILD_TEST_EVENTS
        try {
            $env:BUILD_TEST_CASE = $case.Name
            $env:BUILD_TEST_EVENTS = $eventsPath
            $output = @(& $pwshPath -NoLogo -NoProfile -File $wrapperPath @($case.Arguments) 2>&1)
            $exitCode = $LASTEXITCODE
        } finally {
            $env:BUILD_TEST_CASE = $originalCase
            $env:BUILD_TEST_EVENTS = $originalEvents
        }
        $expectedFailure = $case.ContainsKey('Fails') -and $case.Fails
        $events = @(Get-Content -LiteralPath $eventsPath) -join ','
        if (($exitCode -ne 0) -ne $expectedFailure -or $events -cne $case.Events) {
            throw "$($case.Name): exit=$exitCode events='$events', expected failure=$expectedFailure events='$($case.Events)'.`n$($output -join "`n")"
        }
        $passed++
        Write-Host "PASS $($case.Name)"
    }
    Write-Host "PASS: $passed build-and-launch scenarios."
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

exit 0
