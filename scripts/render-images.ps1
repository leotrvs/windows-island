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

# The picture at the top of each README: the collapsed states above, the expanded ones below.
Add-Type -AssemblyName System.Drawing
foreach ($lang in 'en', 'pt') {
    $gap = 16
    $images = @{}
    foreach ($name in 'musiccompact', 'claudecompact', 'codexcompact', 'claudeexpanded', 'musicexpanded') {
        $images[$name] = [System.Drawing.Image]::FromFile((Join-Path $OutDir "$name-$lang.png"))
    }
    $bottomWidth = $images['claudeexpanded'].Width + $gap + $images['musicexpanded'].Width
    $topNatural = $images['musiccompact'].Width + $images['claudecompact'].Width + $images['codexcompact'].Width
    $scale = ($bottomWidth - 2 * $gap) / $topNatural
    $topHeight = [int]($images['musiccompact'].Height * $scale)
    $bottomHeight = [Math]::Max($images['claudeexpanded'].Height, $images['musicexpanded'].Height)
    $canvas = New-Object System.Drawing.Bitmap $bottomWidth, ($topHeight + $gap + $bottomHeight)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $x = 0
    foreach ($name in 'musiccompact', 'claudecompact', 'codexcompact') {
        $w = [int]($images[$name].Width * $scale)
        $g.DrawImage($images[$name], $x, 0, $w, $topHeight)
        $x += $w + $gap
    }
    $g.DrawImage($images['claudeexpanded'], 0, $topHeight + $gap)
    $g.DrawImage($images['musicexpanded'], $images['claudeexpanded'].Width + $gap, $topHeight + $gap + [int](($bottomHeight - $images['musicexpanded'].Height) / 2))
    $g.Dispose()
    foreach ($image in $images.Values) { $image.Dispose() }
    $canvas.Save((Join-Path $OutDir "hero-$lang.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}
"Hero images written"
