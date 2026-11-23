<#
.SYNOPSIS
  Builds and runs the Steam Workshop uploader for Precision Lab (Probably Stolen, appid 4348910).

.DESCRIPTION
  Requires: .NET SDK, the game installed (for its Steamworks.NET assembly + steam_api64.dll),
  and the Steam client running + logged in on the account that owns Probably Stolen.
  First run creates the Workshop item; later runs update the item saved in tools\workshop\item_id.txt.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\workshop\publish.ps1
  powershell -ExecutionPolicy Bypass -File tools\workshop\publish.ps1 -ChangeNote "v1.5.0 content update"
#>
param(
    [string]$GameDir = "F:\SteamLibrary\steamapps\common\Probably Stolen",
    [string]$Visibility = "public",
    [string]$ChangeNote = "Precision Lab update",
    [switch]$CreateNew
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = [System.IO.Path]::GetFullPath((Join-Path $here '..\..'))
$content = Join-Path $root 'dist\PrecisionLab'
$preview = Join-Path $here 'preview.png'
$desc = Join-Path $here 'description.txt'
$itemFile = Join-Path $here 'item_id.txt'
$uploaderProject = Join-Path $here 'uploader'
$out = Join-Path $uploaderProject 'bin\Release\net8.0'

if (-not (Test-Path (Join-Path $content 'manifest.xml'))) {
    throw "Workshop content folder not found: $content (build the mod first)"
}
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
    Write-Warning "Steam client does not look running. Start Steam and log in first."
}

Write-Host "[workshop] building uploader..."
dotnet build (Join-Path $uploaderProject 'WorkshopUploader.csproj') -c Release -v m | Out-Host
if ($LASTEXITCODE -ne 0) { throw "uploader build failed" }

Copy-Item (Join-Path $GameDir 'Probably Stolen_Data\Plugins\x86_64\steam_api64.dll') $out -Force
Set-Content -Path (Join-Path $out 'steam_appid.txt') -Value '4348910' -Encoding ASCII
Copy-Item (Join-Path $out 'steam_appid.txt') $uploaderProject -Force

$argsList = @(
    '--content', $content,
    '--preview', $preview,
    '--title', 'Precision Lab / 精密实验室 (Probably Stolen 0.46D)',
    '--desc-file', $desc,
    '--visibility', $Visibility,
    '--changenote', $ChangeNote,
    '--id-out', $itemFile
)
if ((Test-Path $itemFile) -and -not $CreateNew) {
    $existing = (Get-Content $itemFile -Raw).Trim()
    if ($existing) {
        $argsList += @('--pubfile', $existing)
        Write-Host "[workshop] updating item $existing"
    }
}

Push-Location $out
try {
    & (Join-Path $out 'WorkshopUploader.exe') @argsList
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) { throw "workshop uploader exited with $LASTEXITCODE" }
Write-Host "[workshop] item id: $((Get-Content $itemFile -Raw).Trim())"
Write-Host "[workshop] page: https://steamcommunity.com/sharedfiles/filedetails/?id=$((Get-Content $itemFile -Raw).Trim())"
