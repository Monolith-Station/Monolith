#requires -Version 7
<#
Capibara ESP — guidebook structural validator.
Compares the sequence of markup tags (<...>) in each translated ServerInfo XML (working tree)
against the English baseline (git <BaselineRef>, default HEAD). Translation must change ONLY
human-readable text: every tag — name, attributes, attribute values (entity IDs, colors,
priorities) — must remain byte-identical and in the same order.

Run BEFORE committing a translation pass (baseline = English). After an upstream merge,
resolve ServerInfo conflicts by taking upstream's English version, then retranslate and
validate against the post-merge HEAD the same way.
#>
[CmdletBinding()]
param(
    [string]$Root = "Resources/ServerInfo",
    [string]$BaselineRef = "HEAD"
)
$ErrorActionPreference = 'Stop'
$fails = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($f in Get-ChildItem -Recurse $Root -Include *.xml -File) {
    $rel = [System.IO.Path]::GetRelativePath((Get-Location).Path, $f.FullName).Replace('\','/')
    $base = git show "${BaselineRef}:$rel" 2>$null | Out-String
    if ($LASTEXITCODE -ne 0 -or -not $base) { $fails.Add("$rel : not in $BaselineRef (new file?)"); continue }
    $cur = Get-Content -LiteralPath $f.FullName -Raw
    $baseTags = ([regex]::Matches($base, '<[^>]+>') | ForEach-Object Value) -join "`n"
    $curTags  = ([regex]::Matches($cur,  '<[^>]+>') | ForEach-Object Value) -join "`n"
    if ($baseTags -ne $curTags) {
        # First differing tag for diagnostics.
        $a = $baseTags -split "`n"; $b = $curTags -split "`n"
        $i = 0; while ($i -lt [Math]::Min($a.Count, $b.Count) -and $a[$i] -eq $b[$i]) { $i++ }
        $want = if ($i -lt $a.Count) { $a[$i] } else { '<END>' }
        $got  = if ($i -lt $b.Count) { $b[$i] } else { '<END>' }
        $fails.Add("$rel : tag #$i differs — expected $want got $got (en:$($a.Count) es:$($b.Count) tags)")
    }
    $checked++
}

if ($fails.Count -gt 0) {
    $fails | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    Write-Host "`n$($fails.Count) file(s) failed structural check ($checked checked)." -ForegroundColor Red
    exit 1
}
Write-Host "All $checked guidebook files structurally intact vs $BaselineRef." -ForegroundColor Green
exit 0
