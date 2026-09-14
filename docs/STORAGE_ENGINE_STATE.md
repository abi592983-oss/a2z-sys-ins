# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 10 — Real-machine calibration
Status: Pass 9 documentation/support matrix implemented; Pass 10 calibration protocol implemented; physical-machine execution pending

## Scope

The refactor inspects the complete diagnostic system, while storage remains the current implementation focus. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection remains unchanged unless a later dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine — complete
5. Pass 4 — Health, endurance, confidence and percentage/scoring model — complete
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration — foundation complete; remaining cleanup/calibration retained
7. Pass 6 — Individual storage inspection backend architecture — complete
8. Pass 7 — UI storage inspector and useful diagnostic graphs — complete; graph capture remains limited by available timestamped samples
9. Pass 8 — Automated/synthetic validation fixtures — complete and merged to `main`
10. Pass 9 — Documentation and support matrix — implemented
11. Pass 10 — Real-machine calibration against known-good and known-fault systems — protocol implemented; physical execution pending

## Pass 8 validation

The synthetic validation project contains 12 passing tests covering ATA semantics, unknown attributes, endurance separation, NVMe health, unavailable/partial evidence, focused-drive identity/classification, cross-category isolation, resource/battery behavior and graph-data integrity. Windows CI was corrected to build and execute the x64 test output and the validated changes were merged in PR #4 with merge commit `d624a3b64d7528c3c8f46e2e6bc12768539cb862`.

## Pass 9 documentation

Added `docs/STORAGE_SUPPORT_MATRIX.md` as the normative storage support boundary. It documents device-family support, evidence hierarchy, validated vs unavailable fields, condition/endurance/confidence semantics, interface/link limitations, focused-inspector limitations, graph policy and explicit non-claims.

The documentation deliberately distinguishes "supported acquisition path" from "every field guaranteed on every device". Missing evidence remains unavailable and does not become a healthy result.

## Pass 10 calibration preparation

Added `docs/REAL_MACHINE_CALIBRATION.md` containing:
- representative known-good and known-fault machine categories;
- a controlled capture procedure;
- storage and cross-category observations to record;
- a calibration worksheet;
- pass/fail rules;
- boundaries for future numeric scoring calibration;
- regression handling requirements;
- a physical completion criterion.

This is an executable calibration protocol, but it is not evidence that real machines have already been tested. Actual hardware access and captured reports are still required.

## Graph policy

The application must only graph real captured timestamped samples. The current storage evidence model exposes current temperature but does not yet provide a timestamped storage-temperature series, so no storage history graph may be fabricated. CPU stress samples contain actual elapsed-time temperature observations and remain eligible for future graph presentation.

## Existing scoring status

`OverallScore` stays null until evidence-backed calibration exists across storage, thermals, memory/resources, Windows events, PnP/device status, battery and other diagnostic categories. Pass 10 must not introduce a percentage solely because real-machine testing begins.

## Existing limitations retained

- SCSI/SAS evidence remains deliberately conservative.
- USB/SAT support depends on the bridge exposing usable ATA evidence.
- Storage link-speed capability inference remains deliberately conservative.
- No storage history is fabricated from a current reading.
- Individual inspection currently reuses evidence from the completed full-system acquisition; it is not a separate live re-acquisition path.
- Physical real-machine calibration is pending.

## Next step

Execute the Pass 10 protocol against representative physical systems. Preserve raw logs/JSON and independent ground truth, record discrepancies, add regression tests for reproducible issues, and only then consider closing calibration gaps or introducing any numeric scoring model.
