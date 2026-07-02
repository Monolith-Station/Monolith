# Capibara Spanish Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a full Spanish (`es-ES`) machine-translation of the game with English fallback, plus the tooling and docs to keep it in sync with upstream Monolith without merge conflicts.

**Architecture:** All Spanish text lives in a new, additive `Resources/Locale/es-ES/` Fluent tree (upstream never touches it → no conflicts). Exactly one upstream C# file is edited — `ContentLocalizationManager.cs` — to switch the active culture to `es-ES` and register `en-US` as the fallback culture (untranslated keys render English). A Claude-subagent workflow performs the bulk translation. `_Capibara/` holds a glossary, a structural validator, and a sync tool for ongoing maintenance.

**Tech Stack:** C# (.NET), Project Fluent (`.ftl`), RobustToolbox `ILocalizationManager`, NUnit integration tests (`PoolManager`), PowerShell 7 tooling, Claude `Workflow` orchestration.

**Upstream:** `https://github.com/Monolith-Station/Monolith.git`

---

## File Structure

**Created:**
- `Resources/Locale/es-ES/**/*.ftl` — the Spanish translation tree (mirrors `en-US/`), produced by the workflow.
- `Resources/Locale/es-ES/_Capibara/_capibara.ftl` — seed file (fork-owned keys + loc smoke key).
- `_Capibara/glossary.md` — canonical term map, injected into every translation agent.
- `_Capibara/validate-locale.ps1` — structural Fluent validator (placeables/IDs preserved).
- `_Capibara/sync-locale.ps1` — en-US↔es-ES diff + hash manifest driver.
- `_Capibara/manifest.json` — per-key hash of en-US source at last translation (generated).
- `_Capibara/batches.json` — ordered translation batches for the workflow (generated).
- `_Capibara/progress.md` — translation progress checklist.
- `_Capibara/translate-workflow.md` — how to run/resume the translation workflow.
- `_Capibara/tests/` — fixtures for validating the two PowerShell scripts.
- `Content.IntegrationTests/Tests/Localization/CapibaraCultureTest.cs` — culture-switch + fallback test.
- `CLAUDE.md` — repo-root guidance (translation-focused).

**Modified (the single documented divergence):**
- `Content.Shared/Localizations/ContentLocalizationManager.cs` — culture switch + fallback registration.

---

## Task 1: Configure the upstream remote

**Files:** none (git config only).

- [ ] **Step 1: Add the upstream remote (idempotent)**

```powershell
git remote get-url upstream 2>$null; if ($LASTEXITCODE -ne 0) { git remote add upstream https://github.com/Monolith-Station/Monolith.git }
```

- [ ] **Step 2: Fetch upstream and verify**

Run:
```powershell
git fetch upstream; git remote -v
```
Expected: output lists both `origin  https://github.com/TheLacrox/Monolith-Capibara-ESP.git` and `upstream  https://github.com/Monolith-Station/Monolith.git` (fetch + push).

No commit — this is local git config only.

---

## Task 2: Seed es-ES tree + culture switch with en-US fallback

Switches the active game language to `es-ES` and registers `en-US` as fallback, verified by an integration test. The seed file guarantees the `es-ES/` resource directory exists and is non-empty before the culture loads.

**Files:**
- Create: `Resources/Locale/es-ES/_Capibara/_capibara.ftl`
- Modify: `Content.Shared/Localizations/ContentLocalizationManager.cs` (Initialize, lines ~26-53)
- Test: `Content.IntegrationTests/Tests/Localization/CapibaraCultureTest.cs`

- [ ] **Step 1: Create the seed locale file**

`Resources/Locale/es-ES/_Capibara/_capibara.ftl`:
```ftl
# Capibara ESP — fork-owned locale keys. Safe to edit (never in upstream).
capibara-loc-smoke = Prueba de localización de Capibara
```

- [ ] **Step 2: Write the failing integration test**

`Content.IntegrationTests/Tests/Localization/CapibaraCultureTest.cs`:
```csharp
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Localization;

[TestFixture]
public sealed class CapibaraCultureTest
{
    [Test]
    public async Task ActiveCultureIsSpanishWithEnglishFallback()
    {
        await using var pair = await PoolManager.GetServerClient();
        var loc = pair.Server.ResolveDependency<ILocalizationManager>();

        // 1. Active culture switched to es-ES.
        Assert.That(loc.DefaultCulture?.Name, Is.EqualTo("es-ES"),
            "Active culture should be es-ES after the Capibara switch.");

        // 2. A Spanish key resolves to its Spanish value.
        Assert.That(loc.GetString("capibara-loc-smoke"),
            Is.EqualTo("Prueba de localización de Capibara"),
            "es-ES seed key should resolve from the es-ES tree.");

        // 3. An en-US-only key still resolves via fallback (not the raw id).
        //    zzzz-fmt-playtime lives in en-US/_lib.ftl and is not in the es-ES seed.
        Assert.That(loc.HasString("zzzz-fmt-playtime"), Is.True,
            "en-US fallback should resolve keys missing from es-ES.");

        await pair.CleanReturnAsync();
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run:
```powershell
dotnet test Content.IntegrationTests --filter "FullyQualifiedName~CapibaraCultureTest" -c Release
```
Expected: FAIL — assertion 1 fails because `DefaultCulture` is still `en-US` (culture not yet switched).

- [ ] **Step 4: Edit the culture switch**

In `Content.Shared/Localizations/ContentLocalizationManager.cs`, change line 13:
```csharp
        // If you want to change your codebase's language, do it here.
        // Capibara ESP: active language is Spanish (Spain). KEEP OURS on merge conflict.
        private const string Culture = "es-ES";
```

Then, inside `Initialize()`, replace the `cultureEn` block (currently lines ~44-52) with the following. This loads `es-ES` as active (unchanged above it), then loads `en-US` and registers it as the fallback so untranslated keys render English:
```csharp
            /*
             * The following language functions are specific to the english localization. When working on your own
             * localization you should NOT modify these, instead add new functions specific to your language/culture.
             * This ensures the english translations continue to work as expected when fallbacks are needed.
             */
            // Capibara ESP: en-US is the fallback culture. KEEP OURS on merge conflict.
            var cultureEn = new CultureInfo("en-US");
            _loc.LoadCulture(cultureEn);
            _loc.SetFallbackCluture(cultureEn); // NB: RobustToolbox API is spelled "Cluture".

            _loc.AddFunction(cultureEn, "MAKEPLURAL", FormatMakePlural);
            _loc.AddFunction(cultureEn, "MANY", FormatMany);
            // End Capibara ESP
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```powershell
dotnet test Content.IntegrationTests --filter "FullyQualifiedName~CapibaraCultureTest" -c Release
```
Expected: PASS (all three assertions).

- [ ] **Step 6: Commit**

```powershell
git add Resources/Locale/es-ES/_Capibara/_capibara.ftl Content.Shared/Localizations/ContentLocalizationManager.cs Content.IntegrationTests/Tests/Localization/CapibaraCultureTest.cs
git commit -m @'
feat(loc): switch active culture to es-ES with en-US fallback

Sets the build-time culture to Spanish (Spain) and registers en-US as the
fallback culture so untranslated keys render English. Adds the es-ES seed
file and an integration test asserting the switch + fallback.

This is the single intentional divergence from upstream. On a merge conflict
in ContentLocalizationManager.cs, keep the Capibara block.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Task 3: Translation glossary

Canonical Spanish terms, injected into every translation agent so vocabulary stays consistent across 1400+ files.

**Files:**
- Create: `_Capibara/glossary.md`

- [ ] **Step 1: Write the glossary**

`_Capibara/glossary.md`:
```markdown
# Capibara ESP — Spanish (es-ES) Translation Glossary

Target: Spanish from Spain (es-ES). Register: neutral, in-universe. Use "tú" for
player-facing prompts. Keep SS14 proper nouns and station/faction names untranslated.

## Golden rules for translators (human or agent)
- Translate ONLY human-readable text. Never touch message IDs, attribute names,
  `{ $var }`, `{ -term }`, `{ other-id }`, selector keys (`[one] *[other]`),
  function names/args (`{ CAPITALIZE($x) }`), or escapes (`\n`, `{ "" }`).
- Preserve every placeable exactly, including surrounding spaces.
- Do not add or remove `{ }` placeables. Same variables in, same variables out.
- Output must remain valid, parseable Fluent.

## Core term map (English → Spanish)
| English | Spanish |
| --- | --- |
| airlock | esclusa |
| toolbox | caja de herramientas |
| crowbar | palanca |
| wrench | llave inglesa |
| screwdriver | destornillador |
| welder / welding tool | soldador |
| multitool | multiherramienta |
| wire | cable |
| power cell | celda de energía |
| battery | batería |
| APC | APC |
| SMES | SMES |
| reagent | reactivo |
| beaker | vaso de precipitados |
| syringe | jeringa |
| pill | pastilla |
| gauze | gasa |
| ID card | tarjeta de identificación |
| PDA | PDA |
| headset | auricular |
| flatpack | paquete plano |
| bounty | recompensa |
| cargo | carga |
| salvage | salvamento |
| shuttle | transbordador |
| airlock (ship) | esclusa |
| ghost | fantasma |
| round | partida |
| examine | examinar |
| verb | acción |

## Departments / roles (keep recognizable; translate role nouns)
| English | Spanish |
| --- | --- |
| Command | Mando |
| Security | Seguridad |
| Medical | Médico |
| Engineering | Ingeniería |
| Science | Ciencia |
| Cargo / Logistics | Logística |
| Service | Servicio |
| Captain | Capitán |
| Head of Personnel | Jefe de Personal |
| Chief Engineer | Ingeniero Jefe |
| Chief Medical Officer | Médico Jefe |
| Research Director | Director de Investigación |
| Head of Security | Jefe de Seguridad |
| Warden | Alcaide |
| Security Officer | Oficial de Seguridad |
| Detective | Detective |
| Janitor | Conserje |
| Chef | Cocinero |
| Botanist | Botánico |
| Bartender | Camarero |
| Clown | Payaso |
| Mime | Mimo |

## Do NOT translate
- Proper nouns: Nanotrasen, Syndicate, Monolith, Frontier, Space Station 14.
- Faction/species proper names unless a Spanish form already exists in-universe.
- Command/console verb IDs and any token inside `{ }`.
```

- [ ] **Step 2: Verify and commit**

Run:
```powershell
Test-Path _Capibara/glossary.md
```
Expected: `True`.

```powershell
git add _Capibara/glossary.md
git commit -m @'
docs(loc): add Capibara Spanish translation glossary

Canonical es-ES term map and Fluent-preservation rules injected into every
translation agent for consistency across the locale tree.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Task 4: Structural validator (`validate-locale.ps1`)

Automated gate that catches the machine-translation failure modes Fluent cares about: dropped or renamed variables, hallucinated message IDs, unbalanced braces, orphan files.

**Files:**
- Create: `_Capibara/validate-locale.ps1`
- Test: `_Capibara/tests/validate/` fixtures

- [ ] **Step 1: Create failing-then-passing fixtures**

`_Capibara/tests/validate/en-US/sample.ftl`:
```ftl
greet-user = Hello, { $name }!
item-count = You have { $count } items.
plain-line = A plain line.
```

`_Capibara/tests/validate/es-good/sample.ftl` (valid translation):
```ftl
greet-user = ¡Hola, { $name }!
item-count = Tienes { $count } objetos.
plain-line = Una línea simple.
```

`_Capibara/tests/validate/es-bad/sample.ftl` (three deliberate errors):
```ftl
greet-user = ¡Hola, { $nombre }!
item-count = Tienes objetos.
hallucinated-key = No existe en inglés.
```

- [ ] **Step 2: Write the validator**

`_Capibara/validate-locale.ps1`:
```powershell
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
```

- [ ] **Step 3: Run the validator against the GOOD fixture (expect pass)**

Run:
```powershell
pwsh _Capibara/validate-locale.ps1 -EnRoot _Capibara/tests/validate/en-US -EsRoot _Capibara/tests/validate/es-good; "exit=$LASTEXITCODE"
```
Expected: `All es-ES files structurally valid.` then `exit=0`.

- [ ] **Step 4: Run the validator against the BAD fixture (expect fail)**

Run:
```powershell
pwsh _Capibara/validate-locale.ps1 -EnRoot _Capibara/tests/validate/en-US -EsRoot _Capibara/tests/validate/es-bad; "exit=$LASTEXITCODE"
```
Expected: three FAIL lines — `greet-user` introduced variable `$nombre`, `item-count` dropped variable `$count`, `hallucinated-key` not in en-US — then `exit=1`.

- [ ] **Step 5: Commit**

```powershell
git add _Capibara/validate-locale.ps1 _Capibara/tests/validate
git commit -m @'
feat(loc): add es-ES structural validator

Checks translated Fluent files against en-US for dropped/renamed variables,
hallucinated message IDs, unbalanced braces, and orphan files. Includes
good/bad fixtures. Used as the gate after machine translation.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Task 5: Sync tool + manifest (`sync-locale.ps1`)

The maintenance engine. After every upstream pull it reports which keys are new (translate), changed (retranslate — detected via a stored hash manifest), or removed (prune).

**Files:**
- Create: `_Capibara/sync-locale.ps1`
- Test: `_Capibara/tests/sync/` fixtures
- Generated: `_Capibara/manifest.json`, `_Capibara/sync-report.txt`

- [ ] **Step 1: Create fixtures**

`_Capibara/tests/sync/en-US/a.ftl`:
```ftl
key-unchanged = Same text.
key-changed = New English text.
key-new = Brand new key.
```

`_Capibara/tests/sync/es-ES/a.ftl`:
```ftl
key-unchanged = Mismo texto.
key-changed = Texto antiguo traducido.
key-removed = Clave que ya no existe.
```

`_Capibara/tests/sync/manifest.json` (hash of `key-changed`'s OLD English text, so it registers as CHANGED; `key-unchanged` hash matches current):
```json
{
  "key-unchanged": "__FILL_IN_STEP_2__",
  "key-changed": "__FILL_IN_STEP_2__"
}
```

- [ ] **Step 2: Write the sync tool**

`_Capibara/sync-locale.ps1`:
```powershell
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
        $current = $null
        foreach ($line in Get-Content -LiteralPath $f.FullName) {
            if ($line -match '^([A-Za-z][A-Za-z0-9_-]*)\s*=(.*)$') { $current = $Matches[1]; $map[$current] = $Matches[2] }
            elseif ($line -match '^\s+\S' -and $current) { $map[$current] += " " + $line.Trim() }
            else { $current = $null }
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
$manifest = @{}
if (Test-Path $Manifest) {
    (Get-Content $Manifest -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $manifest[$_.Name] = $_.Value }
}

$new = @(); $changed = @(); $removed = @()
foreach ($id in $en.Keys) {
    $h = Get-Hash $en[$id]
    if (-not $es.ContainsKey($id)) { $new += $id }
    elseif ($manifest.ContainsKey($id) -and $manifest[$id] -ne $h) { $changed += $id }
}
foreach ($id in $es.Keys) { if (-not $en.ContainsKey($id)) { $removed += $id } }

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
```

- [ ] **Step 3: Populate the fixture manifest hashes**

Run this to compute the two hashes and write the fixture manifest (`key-unchanged` = current text hash so it stays unchanged; `key-changed` = a bogus old hash so it registers as changed):
```powershell
pwsh -c '
function H($s){ $sha=[System.Security.Cryptography.SHA1]::Create(); $n=($s -replace "\s+"," ").Trim(); [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($n))).Replace("-","") }
@{ "key-unchanged" = (H "Same text."); "key-changed" = "OLDHASH0000000000000000000000000000" } | ConvertTo-Json | Set-Content _Capibara/tests/sync/manifest.json
Get-Content _Capibara/tests/sync/manifest.json'
```
Expected: JSON with a real 40-char hash for `key-unchanged` and the `OLDHASH...` sentinel for `key-changed`.

- [ ] **Step 4: Run the sync tool against fixtures and verify the diff**

Run:
```powershell
pwsh _Capibara/sync-locale.ps1 -EnRoot _Capibara/tests/sync/en-US -EsRoot _Capibara/tests/sync/es-ES -Manifest _Capibara/tests/sync/manifest.json -Report _Capibara/tests/sync/report.txt
Get-Content _Capibara/tests/sync/report.txt
```
Expected console line: `NEW=1 CHANGED=1 REMOVED=1 ...`. Report lists `key-new` under NEW, `key-changed` under CHANGED, `key-removed` under REMOVED.

- [ ] **Step 5: Commit**

```powershell
git add _Capibara/sync-locale.ps1 _Capibara/tests/sync
git commit -m @'
feat(loc): add upstream sync tool with hash manifest

Diffs en-US vs es-ES message IDs and detects source-text changes via a stored
SHA1 manifest, reporting keys to translate / retranslate / prune. Includes
fixtures. Run after every upstream merge.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Task 6: Bulk translation workflow (the MT pass)

Translates the whole `en-US` tree into `es-ES` via a Claude-subagent workflow, core-facing directories first. Because the workflow agent cap is 1000 lifetime and there are 1437 files, agents work on batches of related files (one subtree per agent).

**Files:**
- Generated: `_Capibara/batches.json`, `Resources/Locale/es-ES/**/*.ftl`
- Create: `_Capibara/translate-workflow.md`, `_Capibara/progress.md`

- [ ] **Step 1: Generate ordered batches (core-first)**

Run this to produce `_Capibara/batches.json` — an array of batches, each an array of en-US-relative `.ftl` paths, ~12 files per batch, core dirs first:
```powershell
pwsh -c '
$en = "Resources/Locale/en-US"
$core = @("main-menu","launcher","lobby","late-join","escape-menu","ui","HUD","preferences",
  "character-appearance","character-info","species","markings","traits","job","chat","radio",
  "voting","communications","examine","verbs","hands","inventory","storage","access","alerts",
  "popup","tips","guidebook","credits","server-info","changelog")
$root = (Resolve-Path $en).Path
$files = Get-ChildItem -Recurse -Filter *.ftl -LiteralPath $en | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart("\","/").Replace("\","/") }
$rank = { param($p) $top = ($p -split "/")[0]; $i = [array]::IndexOf($core,$top); if ($i -lt 0) { 1000 } else { $i } }
$sorted = $files | Sort-Object @{ Expression = { & $rank $_ } }, @{ Expression = { $_ } }
$batches = @(); $b = @(); foreach ($f in $sorted) { $b += $f; if ($b.Count -ge 12) { $batches += ,$b; $b = @() } }
if ($b.Count) { $batches += ,$b }
$batches | ConvertTo-Json -Depth 3 -Compress | Set-Content _Capibara/batches.json
"batches=$($batches.Count) files=$($files.Count)"'
```
Expected: `batches=~120 files=1437`.

- [ ] **Step 2: Write the workflow runbook**

`_Capibara/translate-workflow.md`:
```markdown
# Capibara translation workflow

Translates en-US .ftl files into es-ES via Claude subagents.

## Run
1. Regenerate batches: see Task 6 Step 1 in the plan (or after a sync, pass only the NEW/CHANGED files).
2. From the main Claude session, invoke the `Workflow` tool with the script in Step 3
   of the plan, passing `args` = the parsed contents of `_Capibara/batches.json`.
3. Each agent reads `_Capibara/glossary.md`, then for each en-US path in its batch:
   reads the file, translates human text only (preserving all `{ }` placeables, IDs,
   selectors, functions, escapes), and writes the result to the same path with
   `/en-US/` replaced by `/es-ES/`.
4. After the run: `pwsh _Capibara/validate-locale.ps1` must exit 0.
5. Boot check: `dotnet test Content.IntegrationTests --filter CapibaraCultureTest`.
6. Update manifest: `pwsh _Capibara/sync-locale.ps1 -UpdateManifest`.

## Resume
Re-invoke Workflow with the same script + `resumeFromRunId` of the prior run;
completed agents return cached results, only unfinished batches re-run.
```

- [ ] **Step 3: Run the translation workflow**

From the main Claude session, read `_Capibara/batches.json`, then invoke the `Workflow` tool with `args` set to the parsed batches array and this script:

```javascript
export const meta = {
  name: 'capibara-translate-es',
  description: 'Machine-translate the en-US Fluent tree into es-ES, preserving placeables',
  phases: [{ title: 'Translate' }],
}

const batches = args // array of arrays of en-US-relative .ftl paths, core-first

const RULES = `You are translating Space Station 14 localization files to Spanish (Spain, es-ES).
FIRST read _Capibara/glossary.md and follow it.
For EACH en-US path given, use Read to load it, then Write the Spanish version to the SAME
path with "/en-US/" replaced by "/es-ES/".
HARD RULES — translate ONLY human-readable text. NEVER modify, translate, add, or remove:
- message IDs or attribute names (left of "=" and ".attr =")
- placeables { $var }, term refs { -term }, message refs { other-id }
- selector/variant syntax and keys ({ $n -> [one] ... *[other] ... })
- function calls/args { CAPITALIZE($x) }, and escapes \\n, { "" }
Keep every placeable and its surrounding spaces identical. Output must be valid Fluent.
Do not create files that have no en-US counterpart. Return the count of files written.`

await pipeline(
  batches,
  (batch, _orig, i) => agent(
    `${RULES}\n\nBatch ${i} paths:\n${batch.join('\n')}`,
    { label: `translate:batch-${i}`, phase: 'Translate' }
  )
)
return { batches: batches.length }
```

After it completes, verify some es-ES files were created:
```powershell
(Get-ChildItem -Recurse -Filter *.ftl Resources/Locale/es-ES).Count
```
Expected: a number in the ~1400s (seed + translated tree).

- [ ] **Step 4: Validate the output structurally**

Run:
```powershell
pwsh _Capibara/validate-locale.ps1
```
Expected: `All es-ES files structurally valid.` and exit 0. If failures print, re-run the workflow on the offending batches (or hand-fix) until it is clean.

- [ ] **Step 5: Boot check with the real culture**

Run:
```powershell
dotnet test Content.IntegrationTests --filter "FullyQualifiedName~CapibaraCultureTest" -c Release
```
Expected: PASS — confirms the es-ES tree loads without Fluent parse errors and fallback still works.

- [ ] **Step 6: Write the manifest and progress**

Run:
```powershell
pwsh _Capibara/sync-locale.ps1 -UpdateManifest
```
Expected: `NEW=0 ...` (everything translated) and `Manifest updated: N entries.`

`_Capibara/progress.md`:
```markdown
# Translation progress

- [x] Initial full machine-translation pass (es-ES) — 2026-07-01
- [ ] Human editorial review pass
- Sync after each upstream merge: run `sync-locale.ps1`, translate NEW/CHANGED, `-UpdateManifest`.
```

- [ ] **Step 7: Commit**

```powershell
git add Resources/Locale/es-ES _Capibara/batches.json _Capibara/manifest.json _Capibara/translate-workflow.md _Capibara/progress.md
git commit -m @'
feat(loc): machine-translate full locale tree to es-ES

Adds the Spanish translation of the en-US Fluent tree (core-facing dirs
first), the generated batch list, the source-hash manifest, and the workflow
runbook. All files are additive; upstream never touches Resources/Locale/es-ES.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Task 7: Repo-root CLAUDE.md

Guidance for future Claude sessions: fork identity, the localization architecture, the single divergence + merge rule, and the maintenance loop.

**Files:**
- Create: `CLAUDE.md`

- [ ] **Step 1: Write CLAUDE.md**

`CLAUDE.md`:
```markdown
# CLAUDE.md — Monolith-Capibara-ESP

Spanish (es-ES) edition of Monolith Station (a Space Station 14 downstream).
This fork's purpose: a fully Spanish translation kept mergeable with upstream.

## Upstream
- `upstream` = https://github.com/Monolith-Station/Monolith.git (`origin` = this fork).
- Merge loop: `git fetch upstream && git merge upstream/main` →
  `pwsh _Capibara/sync-locale.ps1` → translate NEW/CHANGED keys →
  `pwsh _Capibara/sync-locale.ps1 -UpdateManifest` → commit.

## Localization architecture
- Engine: Project Fluent. All UI text is in `.ftl` files under `Resources/Locale/<culture>/`.
- Active culture is build-time: `Content.Shared/Localizations/ContentLocalizationManager.cs`.
- Spanish lives in `Resources/Locale/es-ES/`, mirroring `en-US/`. `en-US` is the fallback:
  untranslated keys render English automatically (active → fallback → raw key id).

## Iron rules (keep merges conflict-free)
- NEVER edit upstream files (C#, YAML, or `Resources/Locale/en-US/**`). All Spanish is ADDITIVE
  in `Resources/Locale/es-ES/`. Fork tooling/docs live in `_Capibara/`.
- The ONE intentional divergence is `ContentLocalizationManager.cs` (culture switch + fallback),
  bracketed with `// Capibara ESP` comments. **On a merge conflict there, keep the Capibara block.**

## Fluent-preservation rules (any translation, human or agent)
Translate only human-readable text. Never modify/translate/add/remove: message IDs, attribute
names, `{ $var }`, `{ -term }`, `{ other-id }`, selector keys (`[one] *[other]`), function
calls/args (`{ CAPITALIZE($x) }`), or escapes (`\n`, `{ "" }`). Keep placeables and spacing identical.

## Tooling (`_Capibara/`)
- `glossary.md` — canonical es-ES terms; inject into every translation agent.
- `validate-locale.ps1` — structural gate (dropped/renamed vars, hallucinated IDs, braces). Must exit 0.
- `sync-locale.ps1` — en-US↔es-ES diff + hash manifest. `-UpdateManifest` after translating.
- `translate-workflow.md` — how to run/resume the bulk translation workflow.
- `progress.md` — translation status.

## Verify
- `dotnet test Content.IntegrationTests --filter CapibaraCultureTest` — culture es-ES + fallback.
- `pwsh _Capibara/validate-locale.ps1` — es-ES tree structurally sound.
```

- [ ] **Step 2: Verify and commit**

Run:
```powershell
Test-Path CLAUDE.md
```
Expected: `True`.

```powershell
git add CLAUDE.md
git commit -m @'
docs: add repo-root CLAUDE.md for the Capibara ESP fork

Documents the Spanish localization architecture, the single ContentLocalizationManager
divergence and its keep-ours merge rule, the never-edit-upstream discipline, the
Fluent-preservation rules, and the upstream sync loop.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
'@
```

---

## Self-Review notes

- **Spec coverage:** culture switch + fallback → Task 2; es-ES additive tree → Task 6; `_Capibara/` glossary/sync/validate/manifest/progress/workflow → Tasks 3-6; CLAUDE.md → Task 7; upstream remote + merge loop → Task 1 + CLAUDE.md; Fluent-preservation rules → glossary + workflow + validator + CLAUDE.md; verification (parse + fallback) → Task 6 Steps 4-5 + Task 2 test. No gaps.
- **Type/name consistency:** script params (`-EnRoot`, `-EsRoot`, `-Manifest`, `-Report`, `-UpdateManifest`), the `SetFallbackCluture` spelling, the `capibara-loc-smoke` seed key, and the `zzzz-fmt-playtime` fallback key are used identically across tasks.
- **Known soft spot:** the PS `.ftl` parsers are line-based (good enough for the validator/sync gate); authoritative parse-correctness comes from the game boot in Task 6 Step 5.
```
