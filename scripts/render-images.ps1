# Regenerates the README images in docs\images from sample data. Never use real screenshots.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\docs\images'))

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$bin = Join-Path $root 'bin'
& (Join-Path $PSScriptRoot 'build.ps1') -OutDir $bin | Out-Null

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
foreach ($lang in 'en', 'pt') {
    Start-Process (Join-Path $bin 'WindowsIsland.exe') -ArgumentList '--render', "`"$OutDir`"", "--lang=$lang" -Wait
}
"Images written to $OutDir"
