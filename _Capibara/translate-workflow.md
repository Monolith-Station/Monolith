# Capibara translation workflow

Translates the `en-US` Fluent tree into `es-ES` via Claude subagents.

## Inputs
- `_Capibara/batches.json` — ordered array of batches (core-facing dirs first). Each batch is an
  array of en-US-relative `.ftl` paths, packed to ~180 source lines per batch so no single agent
  gets an oversized file. Regenerate with the batch generator (see the implementation plan,
  Task 6 Step 1) — re-run it after a sync to translate only NEW/CHANGED files.
- `_Capibara/glossary.md` — canonical es-ES terms + Fluent-preservation rules.

## Run (from the main Claude session)
1. Invoke the `Workflow` tool with `args` = the batch count (an integer, e.g. 185) and the script
   below. Passing only the count keeps `args` tiny; each agent reads its own slice of
   `batches.json` by index.
2. Each agent:
   - Extracts its file list: `pwsh -c "(Get-Content _Capibara/batches.json -Raw | ConvertFrom-Json)[<i>] -join [Environment]::NewLine"`
   - Reads `_Capibara/glossary.md` and follows it.
   - For each en-US relative path, reads `Resources/Locale/en-US/<path>`, translates human text
     only (preserving every `{ }` placeable, message ID, selector, function, escape), and writes
     the result to `Resources/Locale/es-ES/<path>`.
3. After the run:
   - `pwsh _Capibara/validate-locale.ps1` MUST exit 0. Re-run failing batches (or hand-fix) until clean.
   - Boot check: `dotnet test Content.IntegrationTests --filter "FullyQualifiedName~CapibaraCultureTest" -c Release`.
   - `pwsh _Capibara/sync-locale.ps1 -UpdateManifest` to record the source hashes.

## Workflow script
```javascript
export const meta = {
  name: 'capibara-translate-es',
  description: 'Machine-translate the en-US Fluent tree into es-ES, preserving placeables',
  phases: [{ title: 'Translate' }],
}
const N = args // integer: number of batches in _Capibara/batches.json
const RULES = `Translate Space Station 14 localization to Spanish (Spain, es-ES).
FIRST read _Capibara/glossary.md and follow it.
Get YOUR file list by running: pwsh -c "(Get-Content _Capibara/batches.json -Raw | ConvertFrom-Json)[INDEX] -join [Environment]::NewLine"
For EACH en-US relative path: Read Resources/Locale/en-US/<path>, translate, Write to Resources/Locale/es-ES/<path>.
HARD RULES — translate ONLY human-readable text. NEVER modify/translate/add/remove:
message IDs & attribute names (left of "=" and ".attr ="), { $var }, { -term }, { other-id },
selector syntax/keys ({ $n -> [one]... *[other]... }), function calls/args { CAPITALIZE($x) }, escapes \\n and { "" }.
Keep every placeable and its surrounding spaces identical. Output must be valid Fluent. Do not create files with no en-US counterpart.`
await pipeline(
  Array.from({ length: N }, (_, i) => i),
  (i) => agent(RULES.replace('INDEX', String(i)) + `\n\nYou are batch #${i}.`,
    { label: `translate:${i}`, phase: 'Translate' })
)
return { batches: N }
```

## Resume
Re-invoke the Workflow with the same script + `resumeFromRunId` of the prior run; completed
agents return cached results, only unfinished batches re-run.
```
