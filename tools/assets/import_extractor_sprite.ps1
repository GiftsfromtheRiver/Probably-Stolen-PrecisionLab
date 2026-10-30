<#
.SYNOPSIS
  Imports the user-provided pixel-art pipette (yiyeqiang.png) as the PrecisionLab
  "precision_liquid_extractor" item sprite, on a 1x3 inventory footprint.

.DESCRIPTION
  Game rules (ModLoader.LoadTexturesFromFolder + GameItem.SetSpriteAndShapeFromMod):
    * item sprites live in <mod>\Textures\Items\*.png, 16 px per inventory cell;
    * the occupied cells are decided by the alpha at each 16x16 cell centre,
      so a 16x48 PNG is a 1x3 (1 wide, 3 tall) item when the pixels at
      x=8, y=8/24/40 are opaque.

  The source art is a large anti-aliased upscale (327x1024), so this script:
    1. trims the transparent border,
    2. scales the drawing to fit a 13x48 area (preserving aspect) with high-quality
       downsampling,
    3. quantises it to the ~24 dominant colours of the source (crisp pixel-art look),
    4. centres it on a 16x48 canvas and makes sure the three cell-centre pixels are
       opaque so the item shape is exactly 1x3,
    5. writes the deployable PNG plus an x8 preview for review.

  Run: powershell -NoProfile -ExecutionPolicy Bypass -File tools\assets\import_extractor_sprite.ps1
#>
[CmdletBinding()]
param(
    [string]$Source = '',
    [string]$OutDir = '',
    [string]$PreviewPath = '',
    [ValidateRange(2, 3)][int]$CellsTall = 3
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $Source) {
    $Source = Join-Path $PSScriptRoot '..\..\yiyeqiang.png'
}
if (-not $OutDir) {
    $OutDir = Join-Path $PSScriptRoot '..\..\dist\PrecisionLab\Textures\Items'
}
if (-not $PreviewPath) {
    $PreviewPath = Join-Path $PSScriptRoot 'preview_extractor_x8.png'
}
$Source = [System.IO.Path]::GetFullPath($Source)
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$PreviewPath = [System.IO.Path]::GetFullPath($PreviewPath)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Cell = 16
$CellsWide = 1
$CanvasW = $Cell * $CellsWide          # 16
$CanvasH = $Cell * $CellsTall          # 32 (1x2) or 48 (1x3)
$Pad = 0

# ---------------------------------------------------------------- load + trim
$src = [System.Drawing.Bitmap]::FromFile($Source)
try {
    $minX = $src.Width; $maxX = -1; $minY = $src.Height; $maxY = -1
    for ($y = 0; $y -lt $src.Height; $y++) {
        for ($x = 0; $x -lt $src.Width; $x++) {
            if ($src.GetPixel($x, $y).A -gt 16) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw "Source image '$Source' is fully transparent" }
    $srcRect = New-Object System.Drawing.Rectangle($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
    Write-Host "[sprite] source $($src.Width)x$($src.Height), content $($srcRect.Width)x$($srcRect.Height)"

    # ------------------------------------------------------------ scale-to-fit
    $scale = [Math]::Min(($CanvasW - $Pad) / $srcRect.Width, ($CanvasH - $Pad) / $srcRect.Height)
    $drawW = [Math]::Max(1, [int][Math]::Round($srcRect.Width * $scale))
    $drawH = [Math]::Max(1, [int][Math]::Round($srcRect.Height * $scale))
    $scaled = New-Object System.Drawing.Bitmap($drawW, $drawH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($scaled)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $drawW, $drawH)
    $g.DrawImage($src, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    Write-Host "[sprite] downscaled to ${drawW}x${drawH} (canvas ${CanvasW}x${CanvasH})"

    # ------------------------------------------------------- palette clustering
    # Coarse 4-bit buckets over the source, then average each bucket to a centroid.
    $buckets = @{}
    for ($y = $minY; $y -le $maxY; $y++) {
        for ($x = $minX; $x -le $maxX; $x++) {
            $p = $src.GetPixel($x, $y)
            if ($p.A -le 128) { continue }
            $key = (($p.R -shr 4) -shl 8) -bor (($p.G -shr 4) -shl 4) -bor ($p.B -shr 4)
            if (-not $buckets.ContainsKey($key)) {
                $buckets[$key] = @{ n = 0; r = 0; g = 0; b = 0 }
            }
            $b = $buckets[$key]
            $b.n++; $b.r += $p.R; $b.g += $p.G; $b.b += $p.B
        }
    }
    $palette = @()
    foreach ($b in ($buckets.Values | Sort-Object { $_.n } -Descending | Select-Object -First 24)) {
        $palette += ,@([int]($b.r / $b.n), [int]($b.g / $b.n), [int]($b.b / $b.n))
    }
    Write-Host "[sprite] palette clusters: $($palette.Count)"

    # -------------------------------------------------------- quantise + centre
    $canvas = New-Object System.Drawing.Bitmap($CanvasW, $CanvasH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gc = [System.Drawing.Graphics]::FromImage($canvas)
    $gc.Clear([System.Drawing.Color]::Transparent)
    $gc.Dispose()
    $offsetX = [int][Math]::Floor(($CanvasW - $drawW) / 2)
    $offsetY = [int][Math]::Floor(($CanvasH - $drawH) / 2)
    for ($y = 0; $y -lt $drawH; $y++) {
        for ($x = 0; $x -lt $drawW; $x++) {
            $p = $scaled.GetPixel($x, $y)
            if ($p.A -lt 100) { continue }
            $best = 0; $bestDist = [double]::MaxValue
            for ($i = 0; $i -lt $palette.Count; $i++) {
                $pr = $palette[$i][0]; $pg = $palette[$i][1]; $pb = $palette[$i][2]
                $dr = $p.R - $pr; $dg = $p.G - $pg; $db = $p.B - $pb
                $dist = $dr * $dr + $dg * $dg + $db * $db
                if ($dist -lt $bestDist) { $bestDist = $dist; $best = $i }
            }
            $c = [System.Drawing.Color]::FromArgb(255, $palette[$best][0], $palette[$best][1], $palette[$best][2])
            $tx = $offsetX + $x; $ty = $offsetY + $y
            if ($tx -ge 0 -and $ty -ge 0 -and $tx -lt $CanvasW -and $ty -lt $CanvasH) {
                $canvas.SetPixel($tx, $ty, $c)
            }
        }
    }
    $scaled.Dispose()

    # ------------------------------------------- guarantee 1x3 shape (cell centres)
    $centreX = [int]($CanvasW / 2)
    for ($cellY = 0; $cellY -lt $CellsTall; $cellY++) {
        $cy = $cellY * $Cell + [int]($Cell / 2)
        if ($canvas.GetPixel($centreX, $cy).A -eq 0) {
            # find nearest opaque pixel on that row, else fall back to the row above/below
            $replacement = $null
            for ($dx = 1; $dx -lt $CanvasW; $dx++) {
                foreach ($sx in @($centreX - $dx, $centreX + $dx)) {
                    if ($sx -ge 0 -and $sx -lt $CanvasW) {
                        $q = $canvas.GetPixel($sx, $cy)
                        if ($q.A -gt 0) { $replacement = $q; break }
                    }
                }
                if ($replacement -ne $null) { break }
            }
            if ($replacement -eq $null) {
                for ($dy = 1; $dy -lt $CanvasH; $dy++) {
                    foreach ($sy in @($cy - $dy, $cy + $dy)) {
                        if ($sy -ge 0 -and $sy -lt $CanvasH) {
                            $q = $canvas.GetPixel($centreX, $sy)
                            if ($q.A -gt 0) { $replacement = $q; break }
                        }
                    }
                    if ($replacement -ne $null) { break }
                }
            }
            if ($replacement -ne $null) {
                $canvas.SetPixel($centreX, $cy, [System.Drawing.Color]::FromArgb(255, $replacement.R, $replacement.G, $replacement.B))
                Write-Host "[sprite] filled cell-centre pixel ($centreX,$cy) for 1x${CellsTall} shape"
            }
        }
    }

    $outPath = Join-Path $OutDir 'precision_extractor.png'
    $canvas.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # ------------------------------------------------------------- x8 preview
    $preview = New-Object System.Drawing.Bitmap(($CanvasW * 8), ($CanvasH * 8), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gp = [System.Drawing.Graphics]::FromImage($preview)
    $gp.Clear([System.Drawing.Color]::FromArgb(255, 32, 32, 40))
    $gp.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gp.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $gp.DrawImage($canvas, (New-Object System.Drawing.Rectangle(0, 0, ($CanvasW * 8), ($CanvasH * 8))))
    $gp.Dispose()
    $preview.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # ------------------------------------------------------------------ verify
    $shape = @()
    for ($cellY = 0; $cellY -lt $CellsTall; $cellY++) {
        for ($cellX = 0; $cellX -lt $CellsWide; $cellX++) {
            $cx = $cellX * $Cell + 8
            $cy = $cellY * $Cell + 8
            if ($canvas.GetPixel($cx, $cy).A -gt 0) { $shape += "($cx,$cy)" }
        }
    }
    $canvas.Dispose()
    Write-Host "[sprite] wrote $outPath  (${CanvasW}x${CanvasH}, cells ${CellsWide}x${CellsTall}, opaque centres: $($shape -join ' '))"
    Write-Host "[sprite] preview: $PreviewPath"
}
finally {
    $src.Dispose()
}
