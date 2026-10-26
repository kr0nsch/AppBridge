<#
.SYNOPSIS
  Downloads the pinned ipatool release and verifies its SHA-256 checksum.
.DESCRIPTION
  The executable is extracted to artifacts\tools\<rid>\ipatool.exe, where the FreeReturn
  project picks it up. Version and checksums live in build\ipatool.json.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'ipatool.json') -Raw | ConvertFrom-Json
$asset = $manifest.assets.$RuntimeIdentifier
$targetDir = Join-Path $root "artifacts\tools\$RuntimeIdentifier"
$target = Join-Path $targetDir 'ipatool.exe'
$stamp = Join-Path $targetDir 'ipatool.sha256'

if ((Test-Path $target) -and (Test-Path $stamp) -and ((Get-Content $stamp -Raw).Trim() -eq $asset.sha256)) {
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) is already present."
    return
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("ipatool-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null
try {
    $archive = Join-Path $work $asset.file
    $url = "https://github.com/majd/ipatool/releases/download/v$($manifest.version)/$($asset.file)"
    Write-Host "Downloading $url"
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing

    $actual = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
    if ($actual -ne $asset.sha256) {
        throw "Checksum mismatch for $($asset.file): expected $($asset.sha256), got $actual"
    }

    $extract = Join-Path $work 'x'
    New-Item -ItemType Directory -Force -Path $extract | Out-Null
    tar -xzf $archive -C $extract
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }

    $exe = Get-ChildItem $extract -Recurse -Filter 'ipatool*.exe' | Select-Object -First 1
    if (-not $exe) { throw "No ipatool executable found in $($asset.file)" }

    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Copy-Item $exe.FullName $target -Force
    Set-Content -Path $stamp -Value $asset.sha256 -NoNewline
    Write-Host "ipatool $($manifest.version) ($RuntimeIdentifier) verified and saved to $target"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
