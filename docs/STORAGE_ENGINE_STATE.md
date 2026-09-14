# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 8 — Automated/synthetic validation
Status: Test fixtures and Windows CI added; CI validation currently running; real-machine validation pending

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
9. Pass 8 — Automated/synthetic validation fixtures — implementation complete; CI validation pending
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 8 work completed

Created an isolated validation project on branch `testing/pass-8-validation`:
- `tests/A2ZSysIns.Tests/A2ZSysIns.Tests.csproj`
- `tests/A2ZSysIns.Tests/StorageValidationTests.cs`
- `src/A2ZSysIns/Properties/AssemblyInfo.cs` for test access to internal diagnostic services
- `A2ZSysIns.sln` now includes the test project

Synthetic fixtures cover:
- validated ATA reallocated-sector semantics;
- rejection of unknown/vendor ATA IDs as universal meanings;
- validated endurance/life interpretation and condition/endurance separation;
- NVMe critical warning and percentage-used behavior;
- unavailable SMART evidence remaining UNKNOWN rather than GOOD;
- confidence behavior for partial evidence;
- individual storage target classification, serial-preferred identity matching and safe mismatch handling;
- cross-category assessment isolation (storage failure does not fabricate thermal failure);
- missing battery and low-disk-space category behavior;
- CPU graph samples using only actual captured samples;
- regression protection against treating a single storage temperature reading as historical data.

Added `.github/workflows/pass8-validation.yml` to restore, build and run the synthetic test project on Windows for pushes to the validation branch and pull requests to `main`.

A pull request was opened as PR #4 targeting `main`. The latest CI run is currently in progress; no merge to `main` is authorized until the validation run completes successfully.

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

## Next step

Wait for PR #4 CI to complete. If CI fails, diagnose and correct the validation branch, then rerun. If CI passes, review the branch/PR and merge only the validated Pass 8 changes into `main`. After that, continue to Pass 9 documentation/support matrix work and eventually Pass 10 real-machine calibration.
