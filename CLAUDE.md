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
