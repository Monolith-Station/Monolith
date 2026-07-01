# Translation progress

- [x] **Initial full machine-translation pass (es-ES)** — 2026-07-01
  - 1437 files, ~23.5k message keys, via a 185-agent Claude workflow (core-facing dirs first).
  - `paper/book-authorbooks.ftl` (31 long in-game books) re-translated separately — it exceeded
    one agent's 32k output limit in the main run, so it was written in chunks.
  - Verified: `validate-locale.ps1` exit 0 · sync `NEW=0 CHANGED=0` · `CapibaraCultureTest` boot passes.
- [ ] Human editorial review pass (machine output; proofread high-visibility strings first).

## Maintenance after each upstream merge
1. `git fetch upstream && git merge upstream/main`
2. `pwsh _Capibara/sync-locale.ps1` — lists NEW / CHANGED / REMOVED keys.
3. Translate the NEW/CHANGED keys (see `translate-workflow.md`; regenerate batches for just those files).
4. `pwsh _Capibara/validate-locale.ps1` (must exit 0) and re-run `CapibaraCultureTest`.
5. `pwsh _Capibara/sync-locale.ps1 -UpdateManifest` to record the new source hashes.

> Note: sync always reports `REMOVED=1 = capibara-loc-smoke` — the intentional fork-owned seed key
> (`Resources/Locale/es-ES/_Capibara/_capibara.ftl`), which has no en-US counterpart by design.
