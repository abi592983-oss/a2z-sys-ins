# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 1 — Storage evidence data model
Status: Pass 1 complete

## Scope

The refactor will inspect the complete diagnostic system, while the first implementation focus is storage-device evidence and interpretation. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection is considered working enough to leave unchanged during the storage-engine work unless a dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition
4. Pass 3 — SMART / storage interpretation engine
5. Pass 4 — Health, endurance, confidence and percentage/scoring model
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 1 work completed

`src/A2ZSysIns/Models.cs` now has a richer storage evidence model while retaining the legacy fields needed during migration.

Added structures for:
- Individual ATA SMART attributes: ID, name, raw value, normalized/current value, worst value, threshold, raw string, interpretation, source and semantic-validation state.
- NVMe health/endurance: critical warning, available spare, spare threshold, percentage used, media errors, error-log entries, temperatures and data-unit counters.
- Storage evidence availability/quality/source, device/transport classification, serial validation, capability flags and unavailable-field tracking.
- Explicit storage and controller temperature fields.
- Explicit endurance-used percentage and endurance meaning, separate from overall condition/health.
- SMART source/device type, transport, firmware, vendor and product identity.

The legacy `Attributes` dictionary remains temporarily so existing rules continue to compile while later passes migrate consumers to the richer model.

No SMART interpretation rules or scoring thresholds were changed in Pass 1.

## Important open findings

- Storage life/endurance recognition is still too narrow; the richer model enables the later fix but does not itself fix interpretation.
- Storage scoring still has the score-then-normalize architecture and must be corrected later.
- Health percentage/scoring remains intentionally uncalibrated and must eventually cover all diagnostic problem categories.
- Storage and CPU temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive storage inspection remains a future feature.
- Storage link-speed capability inference remains deliberately conservative.

## Required development record

All discovered development problems are recorded separately in `docs/THE_PROBLEMS_WE_FOUND_IN_DEVELOPMENT.md`. Entries retain discovery date, status, fix date when applicable, and whether the issue has ever been fixed. Fixes do not erase history.

## Files changed in Pass 1

- `src/A2ZSysIns/Models.cs` — expanded storage evidence model.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 2 should migrate storage acquisition so smartctl and the Windows fallback populate the richer evidence model for SATA HDD/SSD, NVMe, USB/removable storage and unsupported/unknown devices without changing health/scoring semantics yet. Unknown or unreadable evidence must remain unavailable rather than being interpreted as failure or health.
