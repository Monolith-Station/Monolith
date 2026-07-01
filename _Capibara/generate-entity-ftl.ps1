#requires -Version 7
<#
Capibara ESP — entity localization generator.
Merges the per-batch translation maps (_Capibara/entities/tmp/tr-*.json) produced by the
entity-translation workflow, then emits valid es-ES Fluent `ent-<id>` override files from
entity-source.json. Names/descriptions with no translation fall back to English (the loc
system would fall back anyway). Handles Fluent brace-escaping and multi-line descriptions.

Output: Resources/Locale/es-ES/_Capibara/entities/entities-NN.ftl
#>
[CmdletBinding()]
param(
    [string]$Source   = "_Capibara/entities/entity-source.json",
    [string]$TmpDir   = "_Capibara/entities/tmp",
    [string]$OutDir   = "Resources/Locale/es-ES/_Capibara/entities",
    [int]   $ChunkSize = 2500
)
$ErrorActionPreference = 'Stop'

# --- merge translation maps ---
$map = @{}
$badFiles = @()
foreach ($f in Get-ChildItem -LiteralPath $TmpDir -Filter tr-*.json -ErrorAction SilentlyContinue) {
    try {
        $obj = Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json -AsHashtable
        foreach ($k in $obj.Keys) { if (-not $map.ContainsKey($k)) { $map[$k] = $obj[$k] } }
    } catch { $badFiles += $f.Name }
}
Write-Host "Merged $($map.Count) translated strings from $TmpDir."
if ($badFiles) { Write-Host "WARN: could not parse $($badFiles.Count) batch file(s): $($badFiles -join ', ')" -ForegroundColor Yellow }

# Fluent-escape: single pass so replacements don't re-match. Literal { and } become {"{"} / {"}"}.
function ConvertTo-Fluent([string]$v) {
    return [regex]::Replace($v, '[{}]', { param($m) if ($m.Value -eq '{') { '{"{"}' } else { '{"}"}' } })
}
# Emit an id/name/desc as a Fluent message, handling multi-line values via indented blocks.
function Format-Entry([string]$id, [string]$name, [string]$desc) {
    $sb = [System.Text.StringBuilder]::new()
    $nl = "`n"
    $nEsc = ConvertTo-Fluent $name
    if ($nEsc -match "\r?\n") {
        [void]$sb.Append("ent-$id =$nl")
        foreach ($ln in ($nEsc -split "\r?\n")) { [void]$sb.Append("    $ln$nl") }
    } else {
        [void]$sb.Append("ent-$id = $nEsc$nl")
    }
    if ($desc -and $desc.Trim()) {
        $dEsc = ConvertTo-Fluent $desc
        if ($dEsc -match "\r?\n") {
            [void]$sb.Append("    .desc =$nl")
            foreach ($ln in ($dEsc -split "\r?\n")) { [void]$sb.Append("        $ln$nl") }
        } else {
            [void]$sb.Append("    .desc = $dEsc$nl")
        }
    }
    return $sb.ToString()
}

# --- generate ---
$data = Get-Content -LiteralPath $Source -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $OutDir | Out-Null
Get-ChildItem -LiteralPath $OutDir -Filter *.ftl -ErrorAction SilentlyContinue | Remove-Item -Force

$missName = 0; $missDesc = 0; $emitted = 0; $chunkIdx = 0
$buf = [System.Text.StringBuilder]::new()
$inChunk = 0
function Flush-Chunk([System.Text.StringBuilder]$b, [int]$idx, [string]$dir) {
    if ($b.Length -eq 0) { return }
    $path = Join-Path $dir ("entities-{0:D2}.ftl" -f $idx)
    Set-Content -LiteralPath $path -Value $b.ToString() -NoNewline
}

foreach ($e in ($data | Sort-Object id)) {
    if (-not ($e.name -and $e.name.Trim())) { continue }   # skip name-less entities (keep English)
    $esName = if ($map.ContainsKey($e.name)) { $map[$e.name] } else { $missName++; $e.name }
    $esDesc = ""
    if ($e.desc -and $e.desc.Trim()) {
        if ($map.ContainsKey($e.desc)) { $esDesc = $map[$e.desc] } else { $missDesc++; $esDesc = $e.desc }
    }
    [void]$buf.Append((Format-Entry $e.id $esName $esDesc))
    [void]$buf.Append("`n")
    $emitted++; $inChunk++
    if ($inChunk -ge $ChunkSize) { Flush-Chunk $buf $chunkIdx $OutDir; $buf.Clear() | Out-Null; $chunkIdx++; $inChunk = 0 }
}
Flush-Chunk $buf $chunkIdx $OutDir

Write-Host "Emitted $emitted entities into $($chunkIdx + 1) file(s) under $OutDir."
Write-Host "Untranslated (fell back to English): names=$missName descs=$missDesc"
