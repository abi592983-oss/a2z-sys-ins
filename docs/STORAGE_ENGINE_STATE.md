# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 3 — SMART / storage interpretation engine
Status: Pass 3 complete

## Scope

The refactor inspects the complete diagnostic system, while storage is the current implementation focus. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection remains unchanged unless a later dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine — complete
5. Pass 4 — Health, endurance, confidence and percentage/scoring model
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 3 work completed

Added `src/A2ZSysIns/StorageInterpretationService.cs` as a separate interpretation layer over the Pass 2 evidence model.

The interpretation layer now:
- Treats ATA SMART IDs as non-universal and validates important meanings using the reported SMART attribute name.
- Separates raw SMART evidence from the compatibility projection consumed by the current legacy scorer.
- Recognizes validated reallocated-sector, pending-sector, offline-uncorrectable and interface-CRC meanings only when the attribute naming supports that meaning.
- Handles NVMe critical warning, media errors, available-spare threshold and standard percentage-used endurance evidence separately from overall condition.
- Recognizes several public device/vendor-style life/endurance attribute names such as lifetime remaining, SSD life left and media wearout indicators without copying another project's implementation.
- Keeps unknown/vendor-specific attributes as evidence rather than assigning invented meanings.
- Produces a conservative drive assessment: Critical indicators, Attention: errors reported, No flagged indicators in available data, or Not assessed.
- Treats validated endurance/life as an endurance estimate, not an overall health percentage or failure probability.
- Runs before the existing scoring pass so invalid raw ATA IDs do not reach the legacy storage failure rules.
- Keeps the existing `SmartInterpretation` compatibility layer for now; full score-then-normalize removal remains a Pass 5 task.

## Important semantic correction

During Pass 3 review, the NVMe acquisition mapping was identified as assigning `controller_busy_time` to `NvmeHealthRecord.ControllerTemperatureC`. That field is not a temperature measurement. The intended temperature field is the NVMe controller-temperature value. This remains a code-review item to correct in the next focused acquisition cleanup unless fixed before that pass.

## Existing limitations retained

- Final health percentage, confidence model and cross-category scoring remain for Pass 4/5.
- The current legacy scorer still owns storage finding/scoring behavior; Pass 5 will remove the score-then-normalize architecture rather than relying on the compatibility projection indefinitely.
- SCSI/SAS evidence is deliberately conservative because a generic grown-defect/error log is not treated as universal proof of failure.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive storage inspection remains a future feature.
- Storage link-speed capability inference remains deliberately conservative.
- Real-machine validation is still pending.

## Files changed in Pass 3

- `src/A2ZSysIns/StorageInterpretationService.cs` — new interpretation layer.
- `src/A2ZSysIns/MainWindow.xaml.cs` — storage interpretation now runs before scoring in the full inspection and after CPU stress data is collected.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 4 should establish the separate condition/endurance/confidence model and the eventual percentage representation. It must avoid treating endurance as overall health, avoid fake 100% health when evidence is missing, and preserve an explicit N/A state for unsupported or ambiguous measurements.
