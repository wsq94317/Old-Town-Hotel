# Cross-engine parity check (Godot migration, Phase 1).
#
# Runs ParityScenario under two hosts and asserts the ledgers are byte-identical:
#   1. plain .NET  -> dotnet test regenerates/validates Sim/golden/parity-ledger.txt
#   2. Godot       -> headless run writes Godot/parity-output.txt
#
# Both hosts compile the SAME Sim source (Assets/Game/Scripts/Sim/**), so a mismatch
# can only mean a host-side wiring difference -- never a rules difference. That is the
# whole point: it removes "maybe the sim disagrees" from every future investigation.
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads .ps1 as ANSI, so non-ASCII
# comments turn into mojibake and can break regex literals.
#
# Usage:
#   .\parity-check.ps1
#   .\parity-check.ps1 -Godot "D:\path\to\Godot_console.exe"

[CmdletBinding()]
param(
    [string]$Godot = "$env:USERPROFILE\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe"
)

$ErrorActionPreference = 'Stop'
$repo   = $PSScriptRoot
$golden = Join-Path $repo 'Sim\golden\parity-ledger.txt'
$actual = Join-Path $repo 'Godot\parity-output.txt'

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }

# --- 1. .NET host -----------------------------------------------------------
Write-Host '[1/3] dotnet test (pure .NET host)' -ForegroundColor Cyan
$testOut = dotnet test (Join-Path $repo 'Sim.Tests\OldTownHotel.Sim.Tests.csproj') --nologo 2>&1
if ($LASTEXITCODE -ne 0) { $testOut | Select-Object -Last 20; Fail 'dotnet test failed' }
# Summary wording is localized (English "Passed!" / Chinese "已通过!"), so match on the
# always-present ".dll (net8.0)" tail instead of on any language-specific word.
$summary = @($testOut | Select-String '\.dll \(net') | Select-Object -Last 1
if ($summary) { Write-Host "      $($summary.Line.Trim())" }

if (-not (Test-Path $golden)) { Fail "golden missing: $golden (regenerate with PARITY_UPDATE=1)" }

# --- 2. Godot host ----------------------------------------------------------
Write-Host '[2/3] godot --headless (Godot host)' -ForegroundColor Cyan
if (-not (Test-Path $Godot)) { Fail "Godot not found: $Godot  (pass -Godot <path>)" }
if (Test-Path $actual) { Remove-Item $actual -Force }

# Name the scene explicitly rather than relying on project.godot's main_scene -- that
# now points at the game (Main.tscn), and a parity check that silently runs the wrong
# scene would report a stale ledger as a pass.
& $Godot --headless --path (Join-Path $repo 'Godot') 'res://ParityCheck.tscn' 2>&1 |
    Select-String '\[parity\]|ERROR|Exception' | ForEach-Object { "      $($_.Line)" }
if (-not (Test-Path $actual)) { Fail "Godot produced no output at $actual" }

# --- 3. Compare -------------------------------------------------------------
# Hash the LF-normalized content, not the raw file. .gitattributes pins the golden to
# LF, but an editor or a stray autocrlf setting can still rewrite it -- and a failure
# caused by line endings rather than by the numbers is worse than no check at all,
# because it teaches you to ignore this script.
Write-Host '[3/3] byte comparison (LF-normalized)' -ForegroundColor Cyan

function Get-NormalizedHash($path) {
    $text  = [IO.File]::ReadAllText($path) -replace "`r`n", "`n"
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    $sha   = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '') }
    finally { $sha.Dispose() }
}

$hGolden = Get-NormalizedHash $golden
$hActual = Get-NormalizedHash $actual

if ($hGolden -eq $hActual) {
    Write-Host "      sha256 $hGolden"
    Write-Host 'PARITY OK - Godot and .NET ledgers are byte-identical' -ForegroundColor Green
    exit 0
}

Write-Host "      golden $hGolden"
Write-Host "      godot  $hActual"
Write-Host 'Ledgers differ:' -ForegroundColor Red
$a = [IO.File]::ReadAllText($golden) -replace "`r`n", "`n"
$b = [IO.File]::ReadAllText($actual) -replace "`r`n", "`n"
Compare-Object ($a -split "`n") ($b -split "`n") |
    Select-Object -First 10 |
    Format-Table @{n='side';e={if ($_.SideIndicator -eq '<=') {'golden'} else {'godot'}}},
                 @{n='line'; e={$_.InputObject}} -AutoSize -Wrap
Fail 'parity mismatch'
