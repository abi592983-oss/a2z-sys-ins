# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 6 — Individual storage inspection backend architecture
Status: Pass 6 complete; UI not yet implemented

## Scope

The refactor inspects the complete diagnostic system, while storage is the current implementation focus. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection remains unchanged unless a later dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine — complete
5. Pass 4 — Health, endurance, confidence and percentage/scoring model — complete
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration — foundation complete; remaining cleanup retained for later calibration
7. Pass 6 — Individual storage inspection backend architecture — complete
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 6 work completed

Added `src/A2ZSysIns/IndividualStorageInspectionService.cs`.

The backend now provides a stable boundary for a future focused per-drive inspector:
- `GetTargets(report)` exposes selectable physical drives without exposing SMART-ID rules to UI code.
- Targets retain index, model, serial, device type, transport and capacity.
- Supported classifications include SATA/ATA, NVMe SSD, SCSI/SAS and USB/removable storage when the acquisition layer exposes the required evidence.
- `Inspect(report, target)` resolves the selected device using stable identity information and reuses the existing acquisition evidence and storage interpretation/health model.
- The result exposes the selected `DriveInfoRecord` plus the independent condition/endurance/confidence assessment.
- The service deliberately does not implement its own SMART parsing, thresholds, health percentages or UI rules.

This establishes the backend contract for the future CrystalDisk-like individual storage screen while keeping acquisition, interpretation and presentation separated.

## Important boundary

Pass 6 does not yet add a front-window UI and does not claim independent live re-acquisition of a drive. The current focused service operates on the physical-drive evidence already acquired by the normal inspection pipeline. A later backend enhancement can add targeted live acquisition without changing the UI contract.

## Existing scoring status

Pass 5's cross-category scoring foundation remains intentionally conservative. `OverallScore` stays null until evidence-backed calibration exists across storage, thermals, memory, Windows events, PnP/device status, battery, resources and other diagnostic categories.

## Existing limitations retained

- SCSI/SAS evidence remains deliberately conservative.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Storage link-speed capability inference remains deliberately conservative.
- Temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive UI remains the Pass 7 task.
- Real-machine validation is still pending.

## Files changed in Pass 6

- `src/A2ZSysIns/IndividualStorageInspectionService.cs` — per-drive target/result backend boundary.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 7 should build the individual storage inspector UI around this backend and add useful evidence graphs, especially actual captured storage and CPU temperature samples. No historical samples may be fabricated.
