<#
.SYNOPSIS
  Builds FreeReturn release artifacts: a portable ZIP and an installer per architecture.
.EXAMPLE
  ./build/build.ps1 -Version 1.0.0
  ./build/build.ps1 -Version 1.1.0 -RuntimeIdentifier win-x64, win-arm64
.NOTES
  Outputs go to artifacts\release together with SHA256SUMS.txt.
  Requires the .NET 10 SDK and Inno Setup 6 (ISCC.exe) for the installer.
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $RuntimeIdentifier = @('win-x64'),
    [switch] $SkipTests,
    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$release = Join-Path $artifacts 'release'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_NOLOGO = '1'

function Invoke-Checked([scriptblock] $Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}

function Find-Iscc {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3 (got '$Version')." }

Remove-Item -Recurse -Force $release -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $release | Out-Null

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'Get-Ipatool.ps1') -RuntimeIdentifier win-x64
    Write-Host "Running tests..." -ForegroundColor Cyan
    Invoke-Checked { dotnet test (Join-Path $root 'tests\FreeReturn.Core.Tests\FreeReturn.Core.Tests.csproj') -c Release }
}

foreach ($rid in $RuntimeIdentifier) {
    $arch = $rid.Split('-')[1]
    $platform = if ($arch -eq 'arm64') { 'ARM64' } else { 'x64' }
    $publish = Join-Path $artifacts "publish\$rid"

    & (Join-Path $PSScriptRoot 'Get-Ipatool.ps1') -RuntimeIdentifier $rid

    Write-Host "Publishing $rid..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $publish -ErrorAction SilentlyContinue
    Invoke-Checked {
        dotnet publish (Join-Path $root 'src\FreeReturn\FreeReturn.csproj') -c Release -r $rid -p:Platform=$platform `
            -p:Version=$Version -p:ContinuousIntegrationBuild=true -o $publish
    }
    if (-not (Test-Path (Join-Path $publish 'tools\ipatool.exe'))) { throw "ipatool.exe is missing from the $rid build." }

    # Portable: same files plus the marker that keeps all data next to the app.
    $portableRoot = Join-Path $artifacts "staging\FreeReturn-$Version-portable-$arch"
    $portableApp = Join-Path $portableRoot 'FreeReturn'
    Remove-Item -Recurse -Force $portableRoot -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $portableApp | Out-Null
    Copy-Item -Path (Join-Path $publish '*') -Destination $portableApp -Recurse
    Set-Content -Path (Join-Path $portableApp 'portable.txt') -Encoding utf8 -Value @(
        'This file makes FreeReturn portable: settings, logs, the App Store sign-in and',
        'downloaded apps are kept in the Data folder next to FreeReturn.exe.',
        'Delete this file to use the per-user locations instead.')
    $zip = Join-Path $release "FreeReturn-$Version-portable-$arch.zip"
    Compress-Archive -Path $portableApp -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Created $zip" -ForegroundColor Green

    if (-not $SkipInstaller) {
        $iscc = Find-Iscc
        if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) was not found. Install it or pass -SkipInstaller.' }
        Write-Host "Building installer for $rid..." -ForegroundColor Cyan
        Invoke-Checked {
            & $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$publish" "/DArch=$arch" "/O$release" (Join-Path $root 'installer\FreeReturn.iss')
        }
        Write-Host "Created FreeReturn-$Version-setup-$arch.exe" -ForegroundColor Green
    }
}

$sums = Get-ChildItem $release -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant(), $_.Name
}
Set-Content -Path (Join-Path $release 'SHA256SUMS.txt') -Value $sums -Encoding ascii
Write-Host ""
Get-ChildItem $release | Format-Table Name, @{ n = 'Size (MB)'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
