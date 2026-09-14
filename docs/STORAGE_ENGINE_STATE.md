# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 4 — Health, endurance, confidence and percentage/scoring model
Status: Pass 4 complete

## Scope

The refactor inspects the complete diagnostic system, while storage is the current implementation focus. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection remains unchanged unless a later dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine — complete
5. Pass 4 — Health, endurance, confidence and percentage/scoring model — complete
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 4 work completed

Added `src/A2ZSysIns/StorageHealthAssessmentService.cs`.

The new model explicitly separates:
- **Condition** — UNKNOWN, GOOD / NO FLAGGED INDICATOR, ATTENTION, or CRITICAL.
- **Condition confidence** — Low, Moderate or High based on the quality and type of evidence.
- **Endurance** — a validated remaining-life value when available, otherwise UNAVAILABLE.
- **Endurance confidence** — separate from condition confidence.
- **Interpretation** — explicitly states that endurance is not a failure probability and neither endurance nor storage condition is the overall system-health percentage.

The result is persisted as measurement evidence so it survives the existing scorer's clearing of findings/scores. Missing SMART data remains UNKNOWN/UNAVAILABLE rather than becoming 100% healthy.

No final numeric health percentage was introduced. A numeric percentage will only be added after the cross-category calibration work in Pass 5 and later real-machine validation.

## Important design rule

A drive can have good condition evidence but unavailable endurance evidence, or valid endurance evidence while still having a condition problem. These dimensions must never be collapsed into one number without an evidence-backed calibration model.

## Existing limitations retained

- The current legacy scorer still owns storage finding/scoring behavior; Pass 5 will remove the score-then-normalize architecture.
- The new condition/endurance model is currently recorded as evidence, not yet the authoritative UI score.
- SCSI/SAS evidence remains deliberately conservative.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive storage inspection remains a future feature.
- Storage link-speed capability inference remains deliberately conservative.
- Real-machine validation is still pending.

## Files changed in Pass 4

- `src/A2ZSysIns/StorageHealthAssessmentService.cs` — separate condition/endurance/confidence model.
- `src/A2ZSysIns/MainWindow.xaml.cs` — records the Pass 4 model during normal inspection and after CPU stress recalculation.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 5 should make interpretation authoritative before scoring, remove the remaining score-then-normalize dependency, and calibrate the eventual health/scoring framework across storage, thermals, memory, Windows events, PnP/device status, battery, resources and other diagnostic categories.