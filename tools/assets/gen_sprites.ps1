<#
.SYNOPSIS
  Generates the pixel-art item sprites for the PrecisionLab mod.

.DESCRIPTION
  The game loads mod item sprites from <mod>\Textures\Items\*.png through
  ModLoader.LoadTexturesFromFolder: every PNG becomes a Sprite in LoadedMod.Sprites,
  named after the file (no extension), point filtered, pixelsPerUnit 100.
  GameItem.SetSpriteAndShapeFromMod slices the sprite into 16x16 grid cells
  (UISettings.gridSize == 16), so:
    - 16x16 PNG  -> 1x1 item (hand tool: the extractor)
    - 48x48 PNG  -> 3x3 item (machine, same as vanilla purifier)
    - 48x64 PNG  -> 3x4 item (machine with the liquid tank, vanilla purifier_w)
  The occupied cells are decided by the alpha value at each 16x16 cell centre, so the
  drawings below keep those centre pixels opaque on purpose.

  Run (Windows PowerShell 5.1, no admin needed):
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\assets\gen_sprites.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not $OutDir) {
    $OutDir = Join-Path $PSScriptRoot '..\..\dist\PrecisionLab\Textures\Items'
}
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Palette = @{
    border   = '#2c333b'
    dark     = '#39414b'
    metal    = '#5c6773'
    metalHi  = '#8a97a5'
    metalHi2 = '#b7c3ce'
    glowCyan = '#67e8f9'
    cyan     = '#2fa8c7'
    cyanDim  = '#1d7a91'
    green    = '#4ade80'
    greenDim = '#2f8f57'
    amber    = '#fbbf24'
    liquid   = '#3fc6d8'
    liquidHi = '#a5f3fc'
    glass    = '#cfe8ee'
}

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.Dispose()
    return $bmp
}

function Fill-Rect([System.Drawing.Bitmap]$bmp, [int]$x, [int]$y, [int]$w, [int]$h, [string]$colorKey) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($Palette[$colorKey])
    for ($j = 0; $j -lt $h; $j++) {
        for ($i = 0; $i -lt $w; $i++) {
            $px = $x + $i; $py = $y + $j
            if ($px -ge 0 -and $py -ge 0 -and $px -lt $bmp.Width -and $py -lt $bmp.Height) {
                $bmp.SetPixel($px, $py, $c)
            }
        }
    }
}

function Frame-Rect([System.Drawing.Bitmap]$bmp, [int]$x, [int]$y, [int]$w, [int]$h, [string]$colorKey) {
    Fill-Rect $bmp $x $y $w 1 $colorKey
    Fill-Rect $bmp $x ($y + $h - 1) $w 1 $colorKey
    Fill-Rect $bmp $x $y 1 $h $colorKey
    Fill-Rect $bmp ($x + $w - 1) $y 1 $h $colorKey
}

function Save-Sprite([System.Drawing.Bitmap]$bmp, [string]$name) {
    $path = Join-Path $OutDir ($name + '.png')
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "[sprites] wrote $path"
}

function Test-CellCenters([System.Drawing.Bitmap]$bmp, [string]$name) {
    $filled = @()
    for ($cy = 8; $cy -lt $bmp.Height; $cy += 16) {
        for ($cx = 8; $cx -lt $bmp.Width; $cx += 16) {
            if ($bmp.GetPixel($cx, $cy).A -gt 0) { $filled += "($cx,$cy)" }
        }
    }
    Write-Host ("[sprites] {0}: {1}x{2} cells, opaque centres: {3}" -f $name, ($bmp.Width / 16), ($bmp.Height / 16), ($filled -join ' '))
}

# ---------------------------------------------------------------------------
# 精确取液器 / Precision Liquid Extractor — 16x48, 1x3 tall pipette.
# The art is imported from the user-provided pixel pipette (yiyeqiang.png) by
# tools\assets\import_extractor_sprite.ps1, which writes
# dist\PrecisionLab\Textures\Items\precision_extractor.png.
# This script intentionally does NOT regenerate that sprite so it cannot
# overwrite the imported art; run the import script instead.
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# 提纯机器 / Purification Machine — 48x48, 3x3 (idle) and 48x64, 3x4 (tank in)
# ---------------------------------------------------------------------------
function Draw-PurifierBody([System.Drawing.Bitmap]$bmp) {
    # side pipes keep the top-left/top-right 16px cells occupied (3x3 shape)
    Fill-Rect $bmp 5 2 4 14 'metal'
    Frame-Rect $bmp 5 2 4 14 'border'
    Fill-Rect $bmp 39 2 4 14 'metal'
    Frame-Rect $bmp 39 2 4 14 'border'
    Fill-Rect $bmp 6 3 2 4 'metalHi'

    # funnel
    Fill-Rect $bmp 12 2 24 4 'metalHi'
    Frame-Rect $bmp 12 2 24 4 'border'
    Fill-Rect $bmp 14 3 20 2 'dark'
    Fill-Rect $bmp 15 6 18 3 'metal'
    Fill-Rect $bmp 17 9 14 3 'metal'
    Fill-Rect $bmp 19 12 10 3 'metal'
    Frame-Rect $bmp 19 12 10 3 'border'
    Fill-Rect $bmp 21 13 6 1 'dark'

    # main body
    Fill-Rect $bmp 4 15 40 27 'metal'
    Frame-Rect $bmp 4 15 40 27 'border'
    Fill-Rect $bmp 6 16 36 2 'metalHi'
    Fill-Rect $bmp 6 41 36 1 'dark'

    # filter cartridges
    Fill-Rect $bmp 8 19 6 18 'dark'
    Frame-Rect $bmp 8 19 6 18 'border'
    for ($ry = 21; $ry -le 35; $ry += 3) { Fill-Rect $bmp 9 $ry 4 1 'metal' }
    Fill-Rect $bmp 34 19 6 18 'dark'
    Frame-Rect $bmp 34 19 6 18 'border'
    for ($ry = 21; $ry -le 35; $ry += 3) { Fill-Rect $bmp 35 $ry 4 1 'metal' }

    # gauge / dial
    Fill-Rect $bmp 20 20 8 8 'border'
    Fill-Rect $bmp 21 21 6 6 'glass'
    Fill-Rect $bmp 22 22 4 4 'cyan'
    Fill-Rect $bmp 23 23 2 2 'liquidHi'
    Fill-Rect $bmp 24 21 1 3 'dark'

    # indicator lamps (idle: cyan)
    Fill-Rect $bmp 16 19 2 2 'glowCyan'
    Fill-Rect $bmp 30 19 2 2 'glowCyan'

    # pipe + output window
    Fill-Rect $bmp 18 29 12 2 'metalHi'
    Frame-Rect $bmp 18 29 12 2 'border'
    Fill-Rect $bmp 9 34 30 8 'border'
    Fill-Rect $bmp 10 35 28 6 'liquid'
    Fill-Rect $bmp 11 36 26 1 'liquidHi'
    Fill-Rect $bmp 10 39 28 2 'cyanDim'

    # base slab (keeps bottom 16px cells occupied)
    Fill-Rect $bmp 4 42 40 3 'metal'
    Frame-Rect $bmp 4 42 40 3 'border'
    Fill-Rect $bmp 6 45 6 3 'dark'
    Fill-Rect $bmp 36 45 6 3 'dark'
    Fill-Rect $bmp 8 45 2 1 'metalHi'
    Fill-Rect $bmp 38 45 2 1 'metalHi'
}

$purifier = New-Canvas 48 48
Draw-PurifierBody $purifier
Test-CellCenters $purifier 'precision_purifier'
Save-Sprite $purifier 'precision_purifier'

# Active variant: same machine plus a bottom liquid tank (48x64 = 3x4) and green lamps.
$purifierActive = New-Canvas 48 64
Draw-PurifierBody $purifierActive
Fill-Rect $purifierActive 16 19 2 2 'green'
Fill-Rect $purifierActive 30 19 2 2 'green'
Fill-Rect $purifierActive 17 29 14 2 'metalHi'
Frame-Rect $purifierActive 17 29 14 2 'border'
# tank
Fill-Rect $purifierActive 4 47 40 15 'metal'
Frame-Rect $purifierActive 4 47 40 15 'border'
Fill-Rect $purifierActive 6 48 36 2 'metalHi'
Fill-Rect $purifierActive 6 52 36 7 'liquid'
Fill-Rect $purifierActive 7 53 34 1 'liquidHi'
Fill-Rect $purifierActive 6 58 36 1 'cyanDim'
# level markers on the tank
for ($ty = 52; $ty -le 58; $ty += 2) { Fill-Rect $purifierActive 4 $ty 2 1 'glowCyan' }
# glow vents
Fill-Rect $purifierActive 10 50 4 1 'greenDim'
Fill-Rect $purifierActive 34 50 4 1 'greenDim'
Test-CellCenters $purifierActive 'precision_purifier_active'
Save-Sprite $purifierActive 'precision_purifier_active'

Write-Host "[sprites] done -> $OutDir"
