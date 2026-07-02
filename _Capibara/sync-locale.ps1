#requires -Version 7
[CmdletBinding()]
param(
    [string]$EnRoot = "Resources/Locale/en-US",
    [string]$EsRoot = "Resources/Locale/es-ES",
    [string]$Manifest = "_Capibara/manifest.json",
    [string]$Report = "_Capibara/sync-report.txt",
    [switch]$UpdateManifest
)
$ErrorActionPreference = 'Stop'

function Read-FlatMessages([string]$root) {
    $map = @{}
    foreach ($f in Get-ChildItem -Recurse -Filter *.ftl -LiteralPath $root -ErrorAction SilentlyContinue) {
        $current = $null; $depth = 0
        foreach ($line in Get-Content -LiteralPath $f.FullName -Encoding utf8) {
            $o = ([regex]::Matches($line, '\{')).Count
            $c = ([regex]::Matches($line, '\}')).Count
            if ($line -match '^([A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$') { $current = $Matches[1]; $map[$current] = $Matches[2]; $depth = $o - $c }
            elseif ($null -ne $current -and ($line -match '^\s' -or $depth -gt 0) -and $line -notmatch '^\s*$') { $map[$current] += ' ' + $line.Trim(); $depth += $o - $c }
            elseif ($line -match '^\s*$') { $current = $null; $depth = 0 }
        }
    }
    return $map
}
function Get-Hash([string]$s) {
    $sha = [System.Security.Cryptography.SHA1]::Create()
    $norm = ($s -replace '\s+', ' ').Trim()
    return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($norm))).Replace('-','')
}

$en = Read-FlatMessages $EnRoot
$es = Read-FlatMessages $EsRoot
$stored = @{}
if (Test-Path -LiteralPath $Manifest) {
    (Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $stored[$_.Name] = $_.Value }
}

$new = @(); $changed = @(); $removed = @()
foreach ($id in $en.Keys) {
    $h = Get-Hash $en[$id]
    if (-not $es.ContainsKey($id)) { $new += $id }
    elseif ($stored.ContainsKey($id) -and $stored[$id] -ne $h) { $changed += $id }
}
# Fork-owned es-ES-only keys are NOT stale: everything defined under es-ES/_Capibara/
# (entity ent-* overrides, seed keys, guide-entry titles, upstream-bug fixes) exists by
# design with no en-US counterpart. Exclude those from REMOVED so the report stays
# meaningful for the mirrored .ftl tree. (Entity drift is tracked by re-running the dumper.)
$forkOwned = Read-FlatMessages (Join-Path $EsRoot "_Capibara")
foreach ($id in $es.Keys) { if (-not $en.ContainsKey($id) -and -not $forkOwned.ContainsKey($id)) { $removed += $id } }

$lines = @("SYNC REPORT", "en-US messages: $($en.Count)  es-ES messages: $($es.Count)", "",
    "== NEW (translate): $($new.Count) ==") + @($new | Sort-Object) +
    @("", "== CHANGED (retranslate): $($changed.Count) ==") + @($changed | Sort-Object) +
    @("", "== REMOVED (stale, prune): $($removed.Count) ==") + @($removed | Sort-Object)
$lines | Set-Content -LiteralPath $Report
Write-Host "NEW=$($new.Count) CHANGED=$($changed.Count) REMOVED=$($removed.Count). Report: $Report"

if ($UpdateManifest) {
    $out = [ordered]@{}
    foreach ($id in ($en.Keys | Sort-Object)) { $out[$id] = Get-Hash $en[$id] }
    $out | ConvertTo-Json -Depth 1 | Set-Content -LiteralPath $Manifest
    Write-Host "Manifest updated: $($out.Count) entries."
}
