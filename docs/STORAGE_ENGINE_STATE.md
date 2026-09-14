# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
Status: Pass 5 foundation complete; numeric scoring still intentionally withheld

## Scope

The refactor inspects the complete diagnostic system, while storage is the current implementation focus. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection remains unchanged unless a later dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine — complete
5. Pass 4 — Health, endurance, confidence and percentage/scoring model — complete
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration — foundation complete
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 5 work completed so far

Added `src/A2ZSysIns/DiagnosticAssessmentService.cs`.

The new pre-scoring layer records cross-category condition/coverage evidence for:
- Storage
- Thermals
- Windows event history
- Resources
- Battery
- Windows device status
- Overall evidence coverage

It deliberately does **not** create a numeric health percentage. Condition, evidence coverage and eventual score contribution remain separate concepts.

`StorageHealthAssessmentService.Record()` now invokes this model before the existing scorer, so the cross-category assessment is available at the correct stage of the inspection pipeline.

## Pass 5 remaining work

- Replace the legacy storage scorer's direct dependency on `EvidenceEngine.DriveAssessment` with the Pass 3/4 interpreted condition model.
- Remove the remaining score-then-normalize dependency from the normal inspection path.
- Establish explicit category-level severity/coverage rules for all existing diagnostic domains.
- Ensure unavailable tests reduce coverage confidence rather than being treated as healthy or as arbitrary score penalties.
- Decide whether/when a numeric category score can be justified by calibration evidence.
- Keep `OverallScore` null until a defensible cross-category model exists.

## Important design rule

A diagnostic category has at least three independent dimensions:
1. **Observed condition** — what the evidence says.
2. **Evidence confidence/coverage** — how well the category was actually measured.
3. **Score contribution** — a future calibrated representation, if justified.

These must not be collapsed prematurely. A missing RAM integrity test, for example, is not evidence of bad RAM and is also not evidence of healthy RAM.

## Existing limitations retained

- The current legacy scorer still owns some storage finding/scoring behavior; this is the main Pass 5 cleanup target.
- The new cross-category model is currently recorded as evidence, not yet the authoritative UI score.
- SCSI/SAS evidence remains deliberately conservative.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive storage inspection remains a future feature.
- Storage link-speed capability inference remains deliberately conservative.
- Real-machine validation is still pending.

## Files changed in Pass 5 so far

- `src/A2ZSysIns/DiagnosticAssessmentService.cs` — pre-scoring cross-category condition/coverage foundation.
- `src/A2ZSysIns/StorageHealthAssessmentService.cs` — invokes the cross-category model before scoring.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass step

Continue Pass 5 by making the existing scorer consume the interpreted condition model directly, then retire the post-score normalization dependency without disturbing the established non-storage collectors.