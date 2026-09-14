# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 10 — Real-machine calibration and storage acquisition recovery
Status: Physical-machine testing has begun; the first HP desktop exposed a real packaging/acquisition gap. Recovery is being hardened before the next real-machine run.

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
11. Pass 10 — Real-machine calibration against known-good and known-fault systems — active

## Pass 8 validation

The synthetic validation project contains 12 passing tests covering ATA semantics, unknown attributes, endurance separation, NVMe health, unavailable/partial evidence, focused-drive identity/classification, cross-category isolation, resource/battery behavior and graph-data integrity. Windows CI was corrected to build and execute the x64 test output and the validated changes were merged in PR #4 with merge commit `d624a3b64d7528c3c8f46e2e6bc12768539cb862`.

## Pass 9 documentation

Added `docs/STORAGE_SUPPORT_MATRIX.md` as the normative storage support boundary. It documents device-family support, evidence hierarchy, validated vs unavailable fields, condition/endurance/confidence semantics, interface/link limitations, focused-inspector limitations, graph policy and explicit non-claims.

The documentation deliberately distinguishes "supported acquisition path" from "every field guaranteed on every device". Missing evidence remains unavailable and does not become a healthy result.

## Pass 10 physical calibration — first real-machine finding

The first real-machine run was performed on a Hewlett-Packard 23-d250ee desktop with:
- `ST1000DM003-1CH162` 1 TB Seagate HDD;
- `HS-SSD-WAVE(S) 256G` 256 GB SSD.

Windows WMI correctly enumerated both physical drives, and LibreHardwareMonitor independently exposed per-drive temperatures. The storage step, however, recorded `smartctl.exe` as unavailable because the manually run application package did not contain the executable. The two drives therefore remained `Not assessed` for SMART despite the machine exposing useful storage evidence through other tooling.

This is a real packaging/acquisition integration failure, not evidence that either physical drive lacks SMART capability.

The recovery design now adds a headless CrystalDiskInfo `/CopyExit` evidence provider as a last-resort path. The provider is intentionally not an A2Z scoring engine: it exports storage evidence, A2Z matches the result back to the WMI drive identity, and the existing A2Z interpretation/reporting pipeline remains authoritative. The Windows artifact workflow now packages both official smartmontools and CrystalDiskInfo Standard portable files.

## Pass 10 calibration preparation

`docs/REAL_MACHINE_CALIBRATION.md` contains the representative known-good and known-fault machine categories, controlled capture procedure, storage/cross-category observations, calibration worksheet, pass/fail rules, numeric-scoring boundaries and regression handling.

Physical testing is now underway, but the system is **not yet calibrated**. The first run exposed an acquisition packaging gap that must be verified fixed on the same machine before that case can be considered a successful calibration result.

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
- Physical calibration is active; no final calibration claim has been made.

## Next step

Build and run the corrected package on the same HP desktop. Confirm that smartmontools is actually present in the artifact and that the CrystalDiskInfo fallback can retrieve and safely match both physical drives. Preserve the raw logs/JSON, compare against independent CrystalDiskInfo evidence, and add regression fixtures for every reproducible discrepancy before declaring Pass 10 complete.
