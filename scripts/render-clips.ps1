# Regenerates the small animation of each state shown in the README (docs\images\<state>-<language>.gif) from sample data.
# Never use real screen recordings. Needs Python 3 with Pillow (pip install pillow) to join the frames.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\docs\images'))

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$bin = Join-Path $root 'bin'
& (Join-Path $PSScriptRoot 'build.ps1') -OutDir $bin | Out-Null

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
foreach ($lang in 'en', 'pt') {
    $frames = Join-Path ([IO.Path]::GetTempPath()) "windows-island-clips-$lang"
    if (Test-Path $frames) { Remove-Item $frames -Recurse -Force }
    Start-Process (Join-Path $bin 'WindowsIsland.exe') -ArgumentList '--clips', "`"$frames`"", "--lang=$lang" -Wait
    python (Join-Path $PSScriptRoot 'assemble-clips.py') $frames $OutDir $lang
    Remove-Item $frames -Recurse -Force
}
