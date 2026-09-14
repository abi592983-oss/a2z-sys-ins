# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 7 — Individual storage inspector UI
Status: Pass 7 complete; real-machine validation pending

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
8. Pass 7 — UI storage inspector and useful diagnostic graphs — complete; graph capture remains limited by available timestamped samples
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 7 work completed

Added a focused storage inspection window:
- `src/A2ZSysIns/IndividualStorageInspectionWindow.xaml`
- `src/A2ZSysIns/IndividualStorageInspectionWindow.xaml.cs`

The UI is exposed from the existing Evidence screen as `INDIVIDUAL STORAGE INSPECTOR` and is available after a full inspection has acquired storage evidence.

The focused screen provides:
- selectable physical-drive targets from `IndividualStorageInspectionService.GetTargets(report)`;
- model, serial, device type, transport and capacity identity;
- condition and condition-confidence result;
- endurance/life and separate endurance confidence;
- current drive and controller temperature when actually available;
- SMART/evidence quality and unavailable-field limitations;
- an explicit statement that a single temperature reading is not converted into a fabricated historical graph.

The UI does not contain SMART IDs, vendor thresholds or independent health math. It remains presentation over the existing acquisition, interpretation and assessment layers.

## Graph policy

The application must only graph real captured timestamped samples. The current storage evidence model exposes current temperature but does not yet provide a timestamped storage-temperature series, so Pass 7 does not fabricate a storage graph. CPU stress samples already contain elapsed-time temperature measurements and remain eligible for future graph presentation. A future capture enhancement can provide true storage temperature time series without changing the inspector contract.

## Existing scoring status

Pass 5's cross-category scoring foundation remains intentionally conservative. `OverallScore` stays null until evidence-backed calibration exists across storage, thermals, memory, Windows events, PnP/device status, battery, resources and other diagnostic categories.

## Existing limitations retained

- SCSI/SAS evidence remains deliberately conservative.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Storage link-speed capability inference remains deliberately conservative.
- No storage history is fabricated from a current reading.
- Individual inspection currently reuses evidence from the completed full-system acquisition; it is not a separate live re-acquisition path.
- Real-machine validation is still pending.

## Files changed in Pass 7

- `src/A2ZSysIns/IndividualStorageInspectionWindow.xaml` — focused storage inspector UI.
- `src/A2ZSysIns/IndividualStorageInspectionWindow.xaml.cs` — UI binding/presentation logic.
- `src/A2ZSysIns/MainWindow.xaml.cs` — exposes the inspector button from the Evidence screen.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 8 should add automated/synthetic validation fixtures for storage interpretation, confidence, unavailable evidence, identity matching and focused inspection presentation. Keep all tests independent of real-machine assumptions; real hardware calibration remains Pass 10.
