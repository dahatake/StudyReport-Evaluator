#Requires -Version 7.0
#Requires -PSEdition Core

<#
.SYNOPSIS
Fetches the GitHub Copilot CLI with winget (npm is not used), builds the current source with that CLI bundled, and starts the app.

.DESCRIPTION
winget downloads the official MSI and verifies its SHA-256 against the winget manifest. The MSI is extracted
administratively (no system install), and the extracted copilot.exe is handed to the build through the SDK's
CopilotCliBinaryPath. The app still validates the generated copilot-runtime.json (RID, version, SHA-256).
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Debug',
    [switch] $NoRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $repoRoot 'artifacts\winget-copilot'
$project = Join-Path $repoRoot 'src\StudyReportEvaluator.App\StudyReportEvaluator.App.csproj'
$rid = 'win-x64'

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw 'winget was not found. Install App Installer from the Microsoft Store.'
}

New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
winget download GitHub.Copilot --source winget --download-directory $cacheRoot `
    --accept-package-agreements --accept-source-agreements --skip-dependencies | Out-Host
if ($LASTEXITCODE -ne 0) { throw "winget download failed (exit $LASTEXITCODE)." }

$msi = Get-ChildItem -LiteralPath $cacheRoot -Filter '*.msi' -File |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $msi) { throw 'The Copilot CLI MSI was not downloaded.' }

$extractDir = Join-Path $cacheRoot ([IO.Path]::GetFileNameWithoutExtension($msi.Name) + '-extracted')
$cliPath = Join-Path $extractDir 'LocalApp\GitHubCopilotCLI\copilot.exe'
if (-not (Test-Path -LiteralPath $cliPath)) {
    $proc = Start-Process msiexec -ArgumentList '/a', "`"$($msi.FullName)`"", '/qn', "TARGETDIR=`"$extractDir`"" -Wait -PassThru
    if ($proc.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $cliPath)) {
        throw "Failed to extract copilot.exe from the MSI (msiexec exit $($proc.ExitCode))."
    }
}

$cliVersion = (Get-Item -LiteralPath $cliPath).VersionInfo.ProductVersion
if ($cliVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected CLI version: $cliVersion" }
Write-Host "Copilot CLI $cliVersion  SHA-256 $((Get-FileHash -LiteralPath $cliPath -Algorithm SHA256).Hash)"

dotnet build $project -c $Configuration -r $rid --self-contained false `
    "-p:CopilotCliBinaryPath=$cliPath" "-p:CopilotCliVersion=$cliVersion"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }

$outDir = Join-Path $repoRoot "src\StudyReportEvaluator.App\bin\$Configuration\net10.0\$rid"
$exe = Join-Path $outDir 'StudyReportEvaluator.App.exe'
if (-not (Test-Path -LiteralPath (Join-Path $outDir 'copilot-runtime.json'))) {
    throw 'copilot-runtime.json was not generated in the build output.'
}

if ($NoRun) { Write-Host "Built: $exe"; return }
Start-Process -FilePath $exe
