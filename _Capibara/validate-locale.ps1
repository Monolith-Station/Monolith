#requires -Version 7
[CmdletBinding()]
param(
    [string]$EnRoot = "Resources/Locale/en-US",
    [string]$EsRoot = "Resources/Locale/es-ES"
)
$ErrorActionPreference = 'Stop'
$errors = [System.Collections.Generic.List[string]]::new()

function Get-Messages([string]$path) {
    $map = @{}; $current = $null
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^([A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$') { $current = $Matches[1]; $map[$current] = $Matches[2] }
        elseif ($line -match '^\s+\.([A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$' -and $current) { $map[$current] += " " + $Matches[2] }
        elseif ($line -match '^\s+\S' -and $current) { $map[$current] += " " + $line }
        else { $current = $null }
    }
    return $map
}
function Get-Vars([string]$text) {
    return @([regex]::Matches($text, '\$[A-Za-z][A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique)
}

$esRootResolved = (Resolve-Path $EsRoot).Path
foreach ($esFile in Get-ChildItem -Recurse -Filter *.ftl -LiteralPath $EsRoot -ErrorAction SilentlyContinue) {
    $rel = $esFile.FullName.Substring($esRootResolved.Length).TrimStart('\','/')
    $enPath = Join-Path $EnRoot $rel
    if (-not (Test-Path $enPath)) {
        if ($rel -notmatch '^_Capibara[\\/]') { $errors.Add("$rel : no matching en-US file (orphan translation)") }
        continue
    }
    $enMsgs = Get-Messages $enPath
    $esMsgs = Get-Messages $esFile.FullName
    foreach ($id in $esMsgs.Keys) {
        if (-not $enMsgs.ContainsKey($id)) { $errors.Add("$rel : message '$id' not in en-US (hallucinated/renamed key)"); continue }
        $enVars = Get-Vars $enMsgs[$id]; $esVars = Get-Vars $esMsgs[$id]
        $missing = @($enVars | Where-Object { $_ -notin $esVars })
        $extra   = @($esVars | Where-Object { $_ -notin $enVars })
        if ($missing) { $errors.Add("$rel : '$id' dropped variable(s): $($missing -join ', ')") }
        if ($extra)   { $errors.Add("$rel : '$id' introduced variable(s) not in source: $($extra -join ', ')") }
        $o = ([regex]::Matches($esMsgs[$id], '\{')).Count
        $c = ([regex]::Matches($esMsgs[$id], '\}')).Count
        if ($o -ne $c) { $errors.Add("$rel : '$id' unbalanced braces ($o open / $c close)") }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    Write-Host "`n$($errors.Count) validation error(s)." -ForegroundColor Red
    exit 1
}
Write-Host "All es-ES files structurally valid." -ForegroundColor Green
exit 0
