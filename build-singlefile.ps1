#Requires -Version 7.0
#Requires -PSEdition Core

<#
.SYNOPSIS
最新のソースでアプリケーションをビルドし、Windows x64 単一EXEを作成して起動します。
.DESCRIPTION
1. (任意) 現在のブランチを origin から fast-forward で最新化します。
2. scripts/publish-windows.ps1 -SingleFile で locked restore / Release publish を行います。
3. scripts/package-windows-singlefile.ps1 で最終EXEと SHA-256 sidecar を作成します。
4. (任意) scripts/test-windows-singlefile.ps1 で実EXEの検証を行います。
5. 作成したEXEを起動します (-SkipLaunch で省略できます)。

出力: artifacts\package\StudyReportEvaluator-win-x64.exe と .exe.sha256
要件: Windows 11 x64、PowerShell 7 以上 (pwsh)、global.json の .NET SDK。
.PARAMETER SkipPull
git pull を行わず、現在の作業ツリーのままビルドします。
.PARAMETER Validate
パッケージ作成後に単一EXEの検証 (test-windows-singlefile.ps1) を実行します。
検証には PowerShell 7.4 以上、interactive desktop、clean checkout が必要です。
事前に dotnet build tests\StudyReportEvaluator.App.Tests -c Release を実行してください。
.PARAMETER SkipLaunch
EXEを作成するだけで、アプリケーションを起動しません。
.EXAMPLE
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1
.EXAMPLE
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1 -SkipPull -Validate
.EXAMPLE
pwsh -NoLogo -NoProfile -File .\build-singlefile.ps1 -SkipPull -SkipLaunch
#>
[CmdletBinding()]
param(
    [switch] $SkipPull,
    [switch] $Validate,
    [switch] $SkipLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = $PSScriptRoot
$scriptsDirectory = Join-Path $repositoryRoot 'scripts'

function Invoke-Step {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [scriptblock] $Action
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    & $Action
    if (-not $? -or $LASTEXITCODE -ne 0) {
        throw "$Name failed (exit code $LASTEXITCODE)."
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    if (-not $SkipPull) {
        Invoke-Step 'Checking working tree' {
            $status = @(& git status --porcelain=v1 --untracked-files=no)
            if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
            if ($status.Count -ne 0) {
                throw 'Tracked files have local changes. Commit/stash them or use -SkipPull.'
            }
        }
        Invoke-Step 'Updating source to latest (git pull --ff-only)' {
            & git pull --ff-only
        }
    }

    $commit = ((& git rev-parse --short HEAD) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw 'Unable to read the source commit.'
    }
    Write-Host "Building commit $commit" -ForegroundColor DarkGray

    Invoke-Step 'Publishing single-file application' {
        & (Join-Path $scriptsDirectory 'publish-windows.ps1') -SingleFile
    }

    Invoke-Step 'Packaging single-file EXE and SHA-256 sidecar' {
        & (Join-Path $scriptsDirectory 'package-windows-singlefile.ps1')
    }

    if ($Validate) {
        Invoke-Step 'Validating single-file EXE' {
            & (Join-Path $scriptsDirectory 'test-windows-singlefile.ps1') `
                -ResultsDirectory (Join-Path $repositoryRoot 'TestResults\windows-singlefile')
        }
    }

    $exePath = Join-Path $repositoryRoot 'artifacts\package\StudyReportEvaluator-win-x64.exe'
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        throw "Expected package was not created: $exePath"
    }

    Write-Host ''
    Write-Host "Done. Single-file package: $exePath" -ForegroundColor Green
    Write-Host "SHA-256 sidecar:           $exePath.sha256" -ForegroundColor Green

    if (-not $SkipLaunch) {
        Invoke-Step 'Starting single-file application' {
            $appProcess = Start-Process -FilePath $exePath `
                -WorkingDirectory (Split-Path -Parent $exePath) -PassThru
            Write-Host "Application started (PID $($appProcess.Id))." -ForegroundColor Green
        }
    }
}
finally {
    Pop-Location
}
