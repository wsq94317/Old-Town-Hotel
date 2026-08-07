# Cut a Unity multi-sprite atlas into flat PNGs that Godot can import.
#
# Godot cannot read Unity's slice rects, so every spriteMode:2 texture has to be
# pre-cut. This reads the rects straight out of the .png.meta rather than having
# them transcribed -- during recon a hand-copied table silently dropped one slice
# (BottomNav_Rooms_Inactive), and a missing slice looks exactly like a wiring bug
# later on. Parse the source of truth, don't retype it.
#
# Coordinate systems differ: Unity's sprite rect origin is BOTTOM-left, GDI+/PNG
# crops from the TOP-left. The flip is  top = sheetHeight - y - h.  Getting this
# wrong yields a plausible-looking crop of the wrong part of the sheet.
#
# ASCII-only: Windows PowerShell 5.1 reads .ps1 as ANSI.
#
# Usage:
#   .\tools\slice-unity-atlas.ps1 -Atlas "Assets\Game\UI\Sprites\Nav\BottomNav.png" -OutDir "Godot\art\nav"
#   .\tools\slice-unity-atlas.ps1 -Atlas ... -OutDir ... -Only "Rooms_Active,Rooms_Inactive"
#   .\tools\slice-unity-atlas.ps1 -Atlas ... -OutDir ... -List

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Atlas,
    [string]$OutDir,
    [string]$Only,          # comma-separated substrings; omit for all slices
    [switch]$List,          # print slices and exit, cut nothing
    [string]$StripPrefix    # drop this leading text from output filenames
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo = Split-Path $PSScriptRoot -Parent
if (-not [IO.Path]::IsPathRooted($Atlas)) { $Atlas = Join-Path $repo $Atlas }
if (-not (Test-Path $Atlas)) { throw "atlas not found: $Atlas" }

$metaPath = "$Atlas.meta"
if (-not (Test-Path $metaPath)) { throw "no .meta beside atlas: $metaPath" }

# --- parse slices out of the meta -------------------------------------------
# Each entry looks like:
#   - serializedVersion: 2
#     name: BottomNav_Rooms_Active
#     rect:
#       serializedVersion: 2
#       x: 631
#       y: 552
#       width: 268
#       height: 305
$meta   = Get-Content $metaPath -Raw
$slices = @()
foreach ($m in [regex]::Matches($meta,
    '(?ms)^\s*name:\s*(?<name>\S+).*?^\s*x:\s*(?<x>-?\d+)\s*$.*?^\s*y:\s*(?<y>-?\d+)\s*$.*?^\s*width:\s*(?<w>\d+)\s*$.*?^\s*height:\s*(?<h>\d+)\s*$')) {
    $slices += [pscustomobject]@{
        Name = $m.Groups['name'].Value
        X    = [int]$m.Groups['x'].Value
        Y    = [int]$m.Groups['y'].Value
        W    = [int]$m.Groups['w'].Value
        H    = [int]$m.Groups['h'].Value
    }
}

if ($slices.Count -eq 0) { throw "no slices parsed from $metaPath -- is this really a spriteMode:2 atlas?" }

$bmp = [Drawing.Bitmap]::FromFile((Resolve-Path $Atlas))
try {
    Write-Host "atlas $([IO.Path]::GetFileName($Atlas))  $($bmp.Width)x$($bmp.Height)  $($slices.Count) slices" -ForegroundColor Cyan

    if ($List) {
        $slices | Sort-Object Name | Format-Table Name, X, Y, W, H -AutoSize | Out-String -Width 120
        return
    }

    if (-not $OutDir) { throw '-OutDir is required unless -List is passed' }
    if (-not [IO.Path]::IsPathRooted($OutDir)) { $OutDir = Join-Path $repo $OutDir }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    $wanted = if ($Only) { $Only -split ',' | ForEach-Object { $_.Trim() } } else { $null }
    $cut = 0

    foreach ($s in $slices) {
        if ($wanted -and -not ($wanted | Where-Object { $s.Name -like "*$_*" })) { continue }

        # Unity bottom-left -> GDI+ top-left
        $top = $bmp.Height - $s.Y - $s.H
        if ($s.X -lt 0 -or $top -lt 0 -or ($s.X + $s.W) -gt $bmp.Width -or ($top + $s.H) -gt $bmp.Height) {
            Write-Host "  SKIP $($s.Name): rect falls outside the sheet" -ForegroundColor Yellow
            continue
        }

        $name = $s.Name
        if ($StripPrefix -and $name.StartsWith($StripPrefix)) { $name = $name.Substring($StripPrefix.Length) }
        # snake_case output: Godot convention, and sidesteps case-sensitivity surprises on export.
        # Collapse runs of '_' -- source names already contain underscores, so the camelCase
        # split would otherwise emit reception__active.png.
        $file = (($name -creplace '(?<!^)([A-Z])', '_$1') -replace '_+', '_').ToLower().Trim('_') + '.png'
        $dest = Join-Path $OutDir $file

        $rect = New-Object Drawing.Rectangle $s.X, $top, $s.W, $s.H
        $crop = $bmp.Clone($rect, $bmp.PixelFormat)
        try { $crop.Save($dest, [Drawing.Imaging.ImageFormat]::Png) } finally { $crop.Dispose() }

        "  {0,-34} {1,4}x{2,-4} -> {3}" -f $s.Name, $s.W, $s.H, $file
        $cut++
    }

    Write-Host "cut $cut slice(s) into $OutDir" -ForegroundColor Green
}
finally { $bmp.Dispose() }
