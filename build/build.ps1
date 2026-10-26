<#
.SYNOPSIS
  Builds AppBridge release artifacts: a portable ZIP and an installer per architecture.
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
    [switch] $SkipInstaller,
    # Optional Authenticode signing (code-signing certificate as a .pfx file).
    [string] $SignCertificate = $env:APPBRIDGE_SIGN_PFX,
    [string] $SignPassword = $env:APPBRIDGE_SIGN_PASSWORD
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

function Find-SignTool {
    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    Get-ChildItem $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3 (got '$Version')." }

$signTool = $null
if ($SignCertificate) {
    if (-not (Test-Path $SignCertificate)) { throw "Signing certificate not found: $SignCertificate" }
    $signTool = Find-SignTool
    if (-not $signTool) { throw 'signtool.exe (Windows SDK) was not found.' }
    Write-Host "Authenticode signing enabled." -ForegroundColor Cyan
}

function Invoke-Sign([string[]] $Files) {
    if (-not $signTool) { return }
    Invoke-Checked { & $signTool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f $SignCertificate /p $SignPassword @Files }
}

Remove-Item -Recurse -Force $release -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $release | Out-Null

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'Get-Ipatool.ps1') -RuntimeIdentifier win-x64
    Write-Host "Running tests..." -ForegroundColor Cyan
    Invoke-Checked { dotnet test (Join-Path $root 'tests\AppBridge.Core.Tests\AppBridge.Core.Tests.csproj') -c Release }
}

foreach ($rid in $RuntimeIdentifier) {
    $arch = $rid.Split('-')[1]
    $platform = if ($arch -eq 'arm64') { 'ARM64' } else { 'x64' }
    $publish = Join-Path $artifacts "publish\$rid"

    & (Join-Path $PSScriptRoot 'Get-Ipatool.ps1') -RuntimeIdentifier $rid

    Write-Host "Publishing $rid..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $publish -ErrorAction SilentlyContinue
    Invoke-Checked {
        dotnet publish (Join-Path $root 'src\AppBridge\AppBridge.csproj') -c Release -r $rid -p:Platform=$platform `
            -p:Version=$Version -p:ContinuousIntegrationBuild=true -o $publish
    }
    if (-not (Test-Path (Join-Path $publish 'tools\ipatool.exe'))) { throw "ipatool.exe is missing from the $rid build." }
    # Only AppBridge's own binaries are signed; bundled third-party files keep their own signatures.
    Invoke-Sign @((Join-Path $publish 'AppBridge.exe'), (Join-Path $publish 'AppBridge.dll'), (Join-Path $publish 'AppBridge.Core.dll'))

    # Portable: same files plus the marker that keeps all data next to the app.
    $portableRoot = Join-Path $artifacts "staging\AppBridge-$Version-portable-$arch"
    $portableApp = Join-Path $portableRoot 'AppBridge'
    Remove-Item -Recurse -Force $portableRoot -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $portableApp | Out-Null
    Copy-Item -Path (Join-Path $publish '*') -Destination $portableApp -Recurse
    Set-Content -Path (Join-Path $portableApp 'portable.txt') -Encoding utf8 -Value @(
        'This file makes AppBridge portable: settings, logs, the App Store sign-in and',
        'downloaded apps are kept in the Data folder next to AppBridge.exe.',
        'Delete this file to use the per-user locations instead.')
    $zip = Join-Path $release "AppBridge-$Version-portable-$arch.zip"
    Compress-Archive -Path $portableApp -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Created $zip" -ForegroundColor Green

    if (-not $SkipInstaller) {
        $iscc = Find-Iscc
        if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) was not found. Install it or pass -SkipInstaller.' }
        Write-Host "Building installer for $rid..." -ForegroundColor Cyan
        $isccArgs = @('/Q', "/DAppVersion=$Version", "/DSourceDir=$publish", "/DArch=$arch", "/O$release")
        if ($signTool) {
            # Inno Setup signs the installer and its uninstaller; $f is replaced by the file to sign.
            $isccArgs += '/DSign=1'
            $isccArgs += "/Ssigntool=`"$signTool`" sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f `"$SignCertificate`" /p `"$SignPassword`" " + '$f'
        }
        Invoke-Checked { & $iscc @isccArgs (Join-Path $root 'installer\AppBridge.iss') }
        Write-Host "Created AppBridge-$Version-setup-$arch.exe" -ForegroundColor Green
    }
}

$sums = Get-ChildItem $release -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant(), $_.Name
}
# LF line endings so `sha256sum -c SHA256SUMS.txt` works on Linux, macOS and Git Bash.
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), (($sums -join "`n") + "`n"), [Text.Encoding]::ASCII)
Write-Host ""
Get-ChildItem $release | Format-Table Name, @{ n = 'Size (MB)'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
