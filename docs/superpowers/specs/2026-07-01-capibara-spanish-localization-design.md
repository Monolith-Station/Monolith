# Monolith-Capibara-ESP — Spanish Localization Design

**Date:** 2026-07-01
**Status:** Approved (pending spec review)
**Fork:** Monolith-Capibara-ESP (fork of Monolith Station, itself a Space Station 14 downstream)

## Goal

Translate the entire game to Spanish (Spain, `es-ES`) while keeping the fork able to
pull and merge from upstream Monolith **without merge conflicts**. Ship a full
machine-translation first pass over the whole locale tree via Claude subagents, plus the
infrastructure, tooling, and documentation to maintain the translation as upstream evolves.

## Background: how SS14 localization works

- **Engine:** Project Fluent. Every user-facing string lives in `.ftl` files under
  `Resources/Locale/<culture>/` (currently only `en-US/`). Code contains no English text —
  only message-ID keys resolved at runtime.
- **Flat namespace:** Fluent loads *all* `.ftl` files in the active culture directory into a
  single flat message-ID namespace. Folder/file layout is organizational only; key resolution
  does not depend on path. Mirroring the `en-US/` structure in `es-ES/` is a maintainability
  choice (clean diffs), not an engine requirement.
- **Active language is build-time:** set by a single const in
  `Content.Shared/Localizations/ContentLocalizationManager.cs`
  (`private const string Culture = "en-US";`). The file comment states: *"If you want to
  change your codebase's language, do it here."* There is no in-game UI-language picker in
  vanilla SS14; this fork is the Spanish edition.
- **Fallback chain (the enabling feature):** RobustToolbox `ILocalizationManager` supports
  `LoadCulture(CultureInfo)` and `SetFallbackCluture(params CultureInfo[])` (the misspelling
  is the real API name). Lookup order is: **active culture → fallback culture(s) → raw key ID**.
  Loading `es-ES` as active and `en-US` as fallback means any untranslated string silently
  renders in English — the game is always fully playable and translation can proceed
  incrementally.

Scale of the source tree (measured 2026-07-01): **1437 `.ftl` files, ~26,593 lines** under
`Resources/Locale/en-US/`.

## Merge-conflict strategy (core requirement)

The fork must keep merging from upstream Monolith cleanly. Two mechanisms:

1. **Additive locale tree.** All Spanish files live in `Resources/Locale/es-ES/`, a directory
   upstream does not have. Upstream never edits these files, so they can never conflict on merge.
   This mirrors the fork's existing namespaced-folder convention (`_Mono`, `_NF`, `_DV`,
   `_Goobstation`, etc.) used precisely to avoid upstream collisions.
2. **A single documented code divergence.** Exactly one upstream file is edited:
   `Content.Shared/Localizations/ContentLocalizationManager.cs`. This is the intended language
   switch. CLAUDE.md records the rule: **on a merge conflict in this file, keep the Capibara
   culture/fallback block.** No other upstream file (C#, YAML, en-US `.ftl`) is ever modified.

## Architecture

### The culture switch (the one upstream edit)

In `ContentLocalizationManager.Initialize()`:

- Change `Culture` to `"es-ES"`.
- Keep `en-US` explicitly loaded as the fallback culture.
- Register the fallback: `_loc.SetFallbackCluture(cultureEn);`

Concretely, the active culture becomes `es-ES` (format functions PRESSURE, POWERWATTS, LOC,
etc. attach to it as today), while the existing `cultureEn = new CultureInfo("en-US")` block is
extended to also `LoadCulture` en-US and register it as fallback. The English-specific
functions (`MAKEPLURAL`, `MANY`) stay bound to `en-US` for fallback correctness, exactly as the
existing comment instructs. Spanish-specific grammar functions (e.g. Spanish pluralization) are
**out of scope for the first pass** and noted as future work — the fallback keeps English plural
helpers working meanwhile.

The edit is kept as small and self-contained as possible to minimize the merge-conflict surface,
and is bracketed with `// Capibara ESP` comments so the "keep ours" rule is unambiguous.

### The `es-ES` locale tree

`Resources/Locale/es-ES/` mirrors the `en-US/` directory structure 1:1. Files contain the same
message IDs with Spanish values. Missing files/keys fall back to English automatically. All files
are new → zero upstream conflict.

### `_Capibara/` tooling folder

A fork-namespaced folder (repo-root `_Capibara/`, following the `_Mono`/`_NF` convention) holds
everything used to build and maintain the translation, so none of it collides with upstream:

- **`glossary.md`** — canonical Spanish term map for consistency across all files and agents
  (e.g. airlock → *esclusa*, toolbox → *caja de herramientas*, department/role/reagent names,
  common SS14 jargon). Injected into every translation agent's prompt.
- **`sync-locale.ps1`** — the maintenance engine. Compares en-US vs es-ES message IDs and reports:
  (a) keys present in en-US but missing in es-ES (need translation), (b) keys whose en-US source
  text changed since last sync (need retranslation — tracked via a stored hash manifest),
  (c) keys present in es-ES but removed upstream (stale, safe to prune). Run after every upstream pull.
- **`translate-workflow.md`** — documents the Claude-subagent translation pipeline and how to
  invoke it (including resume behavior).
- **`progress.md`** — checklist of which folders/subtrees have been translated.
- **`manifest.json`** — per-key hash of the en-US source at last translation, so `sync-locale.ps1`
  can detect upstream text changes (not just added/removed keys).

### Translation pipeline (Claude subagents, Fluent-aware)

A single `Workflow` run fans out subagents over the en-US tree. Because the workflow agent cap is
1000 lifetime / ~10-16 concurrent and there are 1437 files, agents operate on **batches of
related files** (one folder subtree per agent, ~10-15 files each → ~120-150 agents), not
one-file-per-agent. Ordering is **core player-facing directories first** (main-menu, launcher,
lobby, escape-menu, ui, HUD, preferences, character-*, job, chat, radio, voting, examine, verbs,
hands, inventory, storage, access, alerts, popup, tips), then fork/gameplay directories
(`_NF`, `_Mono`, `_Goobstation`, `_DV`, …). If the run is interrupted, the most-visible strings
are already done, and the workflow is resumable.

**Fluent-preservation rules (mandatory for every agent).** Translate only human-readable text.
Never alter:
- message IDs and attribute names (`some-id = …`, `.attr = …`)
- placeables / variable interpolations: `{ $name }`, `{ $count }`
- term references: `{ -ss14 }`, `{ -some-term }`
- message references: `{ some-other-id }`
- selector/variant keys and syntax: `{ $x -> [one] … *[other] … }`
- function calls and their argument names: `{ CAPITALIZE($thing) }`, `{ GENDER($user) }`
- escapes and literal blocks: `\n`, `{ "" }`, unicode escapes

Output must remain valid, parseable Fluent. Gendered/pluralized Spanish forms should use Fluent
selectors where the English source already exposes the needed variable; where English hardcodes a
form, keep a single natural Spanish form for the first pass.

### Verification

- The `es-ES` tree must parse (no Fluent syntax errors) — validated by loading the culture; the
  existing `Content.YAMLLinter` / integration test harness and a smoke build confirm the game boots
  with `es-ES` active and English fallback.
- Spot-check that placeables survive (grep es-ES for balanced `{ }` and intact `$`/`-` tokens vs en-US).
- Confirm an untranslated key renders English (fallback works) and a translated key renders Spanish.

## CLAUDE.md (repo root, translation-focused)

New root `CLAUDE.md` covering what future Claude sessions need:

- **Fork identity:** Monolith-Capibara-ESP = Spanish (`es-ES`) edition of Monolith Station.
- **Localization architecture:** Fluent, `Resources/Locale/es-ES/` mirrors `en-US/`, fallback to English.
- **The one divergence:** `ContentLocalizationManager.cs` — the culture switch; on merge conflict, keep ours.
- **Iron rule:** never edit upstream files (C#, YAML, `en-US/*.ftl`) except that one file. All Spanish is additive.
- **Upstream-merge loop:** `git fetch upstream && git merge upstream/main` → run `_Capibara/sync-locale.ps1`
  → translate new/changed keys → commit.
- **Fluent-preservation rules** (as above) — what never to translate.
- **How to run the translation workflow** and where the glossary/progress live.

## Upstream-merge discipline (operational)

1. `git remote add upstream <Monolith upstream URL>` (one-time).
2. `git fetch upstream && git merge upstream/main`. Expect clean merges except possibly
   `ContentLocalizationManager.cs` → resolve by keeping the Capibara block.
3. Run `_Capibara/sync-locale.ps1` to list added/changed/removed keys.
4. Run the translation workflow (or targeted agents) on the added/changed keys.
5. Commit; update `_Capibara/manifest.json` and `progress.md`.

## Out of scope (first pass)

- Spanish-specific grammar helper functions (pluralization, gender agreement) beyond what Fluent
  selectors already express — English helpers remain via fallback.
- In-game runtime language picker (SS14 language is build-time; not adding a selector).
- Translating in-code string literals (there should be none; if found, they are upstream bugs, not
  localized here).
- Human editorial review of machine output (a follow-on quality pass, not this deliverable).

## Success criteria

- Game boots with `es-ES` active and `en-US` fallback; UI shows Spanish, untranslated strings show English.
- `es-ES` tree parses with no Fluent errors; placeables/terms intact.
- A fresh `git merge upstream/main` produces no conflicts outside the single documented file.
- `_Capibara/` tooling can detect and drive translation of new upstream keys after a pull.
- Root `CLAUDE.md` documents the architecture, the divergence rule, and the maintenance loop.
