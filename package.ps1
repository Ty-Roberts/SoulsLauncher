[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.5'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')

$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'Souls Launcher'
$archive = Join-Path $dist "Souls-Launcher-v$Version.zip"

New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build\Souls Launcher.exe') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'build\assets') -Destination $stage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $stage -Force

if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path $stage -DestinationPath $archive -CompressionLevel Optimal
Remove-Item -LiteralPath $stage -Recurse -Force

Write-Host "Release package created: $archive"
