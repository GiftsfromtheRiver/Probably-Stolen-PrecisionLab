<#
.SYNOPSIS
  Installs (or removes) the PrecisionLab mod for Probably Stolen.

.DESCRIPTION
  Copies dist\PrecisionLab into <game>\Mods\PrecisionLab. Optionally pre-seeds the game's
  mod-enable list + trust record so the mod loads on the next launch without the in-game
  trust prompt (uses tools\seed-trust.ps1, which backs up SaveFile.es3).

.PARAMETER GameDir
  Game install root (the folder that contains "Probably Stolen.exe"). If omitted, the
  PROBABLY_STOLEN_DIR environment variable is used.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\install-mod.ps1 -GameDir "D:\Steam\steamapps\common\Probably Stolen"
  powershell -ExecutionPolicy Bypass -File tools\install-mod.ps1 -GameDir "..." -Enable
  powershell -ExecutionPolicy Bypass -File tools\install-mod.ps1 -GameDir "..." -Uninstall
#>
[CmdletBinding()]
param(
    [string]$GameDir = $env:PROBABLY_STOLEN_DIR,
    [string]$SourceDir = (Join-Path $PSScriptRoot '..\dist\PrecisionLab'),
    [switch]$Enable,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw "Pass -GameDir '<game install root>' (or set the PROBABLY_STOLEN_DIR environment variable)."
}
$modsDir = Join-Path $GameDir 'Mods'
$target = Join-Path $modsDir 'PrecisionLab'
$seedScript = Join-Path $PSScriptRoot 'seed-trust.ps1'

if (-not (Test-Path (Join-Path $GameDir 'Probably Stolen.exe'))) {
    throw "Game not found at '$GameDir' (pass -GameDir)."
}

if ($Uninstall) {
    if (Test-Path $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "[install] removed $target"
    } else {
        Write-Host "[install] nothing to remove at $target"
    }
    return
}

$SourceDir = [System.IO.Path]::GetFullPath($SourceDir)
if (-not (Test-Path (Join-Path $SourceDir 'manifest.xml'))) {
    throw "Mod build not found at '$SourceDir' (run: dotnet build -c Release in src\PrecisionLab)."
}

New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
if (Test-Path $target) {
    $backup = "$target.bak-" + (Get-Date -Format 'yyyyMMdd-HHmmss')
    Move-Item -LiteralPath $target -Destination $backup
    Write-Host "[install] existing install moved to $backup"
}
Copy-Item -LiteralPath $SourceDir -Destination $target -Recurse -Force
Write-Host "[install] copied $SourceDir -> $target"

$missing = @()
foreach ($required in @('manifest.xml', 'PrecisionLab.dll', 'Localization\en.csv', 'Localization\zh.csv',
        'Textures\Items\precision_extractor.png', 'Textures\Items\precision_purifier.png',
        'Textures\Items\precision_purifier_active.png')) {
    if (-not (Test-Path (Join-Path $target $required))) { $missing += $required }
}
if ($missing.Count -gt 0) {
    throw "Install is incomplete, missing: $($missing -join ', ')"
}
Write-Host "[install] payload verified (manifest, dll, localization, textures)"

if ($Enable) {
    if (-not (Test-Path $seedScript)) {
        Write-Warning "[install] -Enable requested but $seedScript is missing; enable the mod from the in-game Mods menu instead."
    } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $seedScript -ModFolder $target
        Write-Host "[install] mod enable/trust pre-seeded; next game launch will load it (a SaveFile.es3 backup was created by the seeder)."
    }
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1) Launch Probably Stolen."
Write-Host "  2) If not pre-seeded with -Enable: open the Mods menu, enable 'Precision Lab Equipment' and approve it."
Write-Host "  3) Buy the two upgrades in the in-game phone -> Wild Network -> 'Precision Lab' tab."
