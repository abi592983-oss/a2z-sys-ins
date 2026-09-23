# A2Z System Inspector — Development Context

**Status:** Mandatory project-context document  
**Last updated:** 2026-09-15  
**Project:** A2Z System Inspector / A2Z Sys Ins  
**Repository:** `abi592983-oss/a2z-sys-ins`

## 1. Purpose of this document

This is the authoritative running context for development of A2Z System Inspector.

It records **what we are building, what we add, what we remove, what we change, and why**. It is intentionally broader than the permanent problem history and more operational than the storage-engine state document.

### Mandatory workflow rule

**Before any future work on this project — inspection, planning, analysis, modification, debugging, continuation, or implementation — this exact document must be read first.**

**After every project change, this document must be updated with the change and the reason for it.**

Never silently replace historical context. Append or revise the current-state sections while preserving the development history.

## 2. What we are developing

A2Z System Inspector is an internal Windows PC diagnostic application intended to give technicians a structured, evidence-based assessment of a customer's computer.

The application is intended to inspect the **whole computer**, not only storage. Current diagnostic areas include:

- CPU and hardware information
- hardware sensors and thermals
- memory/resource usage
- physical storage devices and storage health evidence
- SMART/NVMe/SCSI storage evidence where available
- Windows event history and stability indicators
- Windows device/PnP status
- battery evidence when available
- correlated diagnostic findings
- optional technician-controlled CPU stress testing
- customer-readable and technician-readable reporting
- JSON/text/diagnostic-log/PDF reporting paths

The guiding principle is **evidence first**: missing evidence must remain unavailable rather than being interpreted as a healthy condition, and diagnostic confidence must remain separate from the observed condition.

## 3. Important architecture principles

1. **Full-system inspection remains the normal workflow.**
2. **Storage is automatically discovered and scanned as part of the full inspection.** A technician should not select HDD/SSD/NVMe devices one by one during normal inspection.
3. **CPU stress testing is optional and technician-controlled.** It deliberately loads the CPU and therefore requires explicit confirmation.
4. **Acquisition and interpretation remain separate.** A provider collects evidence; A2Z's interpretation layer decides what that evidence means.
5. **Condition and endurance are separate.** SSD/HDD endurance indicators are not treated as an overall failure probability or overall system-health percentage.
6. **Confidence and coverage are separate from health.** Missing telemetry reduces what can be concluded; it must not create false reassurance.
7. **Overall numeric scoring remains withheld until it can be calibrated across all relevant diagnostic categories.**
8. **Graphs may use only real timestamped samples.** Historical values must never be invented or inferred merely to make a graph look complete.
9. **The project must preserve development history.** Fixed problems are never deleted from the permanent problem-history document.
10. **Real-machine validation is required before claiming that a correction works physically.** Synthetic tests alone do not constitute physical validation.
11. **Synthetic testing is a regression aid, not a substitute for provider validation.** The synthetic lab exercises the interpretation/assessment pipeline with controlled evidence; physical machines validate actual WMI, smartctl, sensor, Windows and transport behavior.
12. **Acquisition follows a fallback philosophy.** If a field cannot be obtained from the first provider/path, the Inspector should retry or use another applicable provider before declaring the field unavailable. Partial evidence and provenance must be retained.

## 4. Permanent project documents

### `docs/DEVELOPMENT_CONTEXT.md` — this document

The mandatory first-read project context. It records the evolving architecture, additions, removals, changes, reasons, current state and development chronology.

### `docs/THE_PROBLEMS_WE_FOUND_IN_DEVELOPMENT.md`

Permanent problem history. Every discovered development problem remains recorded, including problems that were later fixed. It is not a replacement for this document.

### `docs/STORAGE_ENGINE_STATE.md`

Detailed storage-engine phase/state document covering storage-specific architecture, calibration and acquisition state.

### `docs/REAL_MACHINE_CALIBRATION.md`

Defines representative real-machine calibration procedures, known-good/known-fault categories, evidence comparison and scoring-calibration boundaries.

### `docs/STORAGE_SUPPORT_MATRIX.md`

Defines practical storage support boundaries, evidence hierarchy, unavailable fields and explicit non-claims.

### `docs/CPU_STRESS_SAFETY.md`

Defines CPU stress-test safety policy, telemetry cadence, preflight refusal, abort conditions and evidence-retention rules.

### `docs/PASS13_SYNTHETIC_LAB.md`

Defines the deterministic randomized synthetic inspection environment used for regression and horizontal diagnostic-pipeline testing. It supplements, but does not replace, real-machine validation.

## 5. Development phases completed

### Pass 0 — Baseline / inventory

Established the existing application's structure and identified the need for a more evidence-based diagnostic architecture.

### Pass 1 — Storage evidence data model

Added richer storage evidence structures while retaining legacy compatibility fields. The model now distinguishes physical-drive identity, SMART attributes, NVMe health, temperature, endurance, source, transport and evidence quality.

**Why:** The original storage evidence was too weak to support reliable interpretation or technician-readable explanations.

### Pass 2 — Storage evidence acquisition

Added `StorageAcquisitionService` using Windows WMI physical-drive enumeration and multiple smartctl acquisition paths. Added device-type preservation, ATA/SAT/NVMe/SCSI recognition, serial validation and raw evidence capture.

**Why:** Acquisition needed to cover real Windows storage configurations instead of depending on one device path.

### Pass 3 — SMART/storage interpretation

Added independent interpretation of validated ATA SMART semantics and NVMe health indicators.

**Why:** Numeric SMART IDs are not universally safe to interpret without validating their actual semantics.

A real-machine SSD exposed an additional bug: an E7 `SSD Life Left` attribute had normalized value 64 and vendor-specific raw value 36. The implementation was corrected to prefer the validated normalized value while preserving the raw evidence.

### Pass 4 — Health, endurance, confidence and scoring model

Added `StorageHealthAssessmentService` and separated condition, condition confidence, endurance, endurance confidence, and explanation/reason.

**Why:** Endurance is useful evidence but is not equivalent to overall health or failure probability.

### Pass 5 — Cross-category diagnostic foundation

Added category-level assessment foundations for storage, thermals, Windows events, resources, battery and Windows-device status. Numeric `OverallScore` remains null.

**Why:** A defensible overall percentage must be calibrated across the entire diagnostic system, not invented from storage alone.

### Pass 6/7 — Individual storage inspection architecture and UI experiment

A focused storage-inspection backend contract and UI were developed as an experiment. Identity matching was designed to prefer serial number and fail safely on ambiguity.

The user-facing individual-storage workflow was subsequently **removed**.

**Why removed:** Normal technician workflow should automatically inspect all physical storage and provide one combined result. Manual device selection added unnecessary work and created identity/UI complexity.

### Pass 8 — Automated/synthetic validation

Added synthetic validation fixtures and CI coverage. The x64 Windows build/test path was corrected. The validation suite reached 27 passing tests after the obsolete stale-telemetry test was removed.

**Why:** Regression coverage was required before relying on physical-machine testing.

### Pass 9/10 — Documentation, support matrix and real-machine calibration

Added storage support documentation, calibration documentation, the CrystalDiskInfo evidence fallback and packaging improvements.

Real-machine testing exposed missing packaged smartctl, direct Windows physical-drive path failure with a working `/dev/sda` fallback, SSD life normalized-vs-raw interpretation error, and additional unrelated Windows findings. The packaging/acquisition path was corrected, and real-machine validation is continuing.

### Pass 11 — CPU stress safety and telemetry

Added a dedicated CPU stress safety supervisor, adaptive telemetry cadence tracking, timestamped CPU stress samples, live/report graphs and automated tests.

Safety design includes required CPU temperature/clock/load telemetry, preflight refusal, 80 °C preflight refusal, 87 °C early abort, 90 °C hard abort, clock-collapse and clock-surge detection, fail-closed telemetry loss, and optional RAM/GPU telemetry that does not abort CPU stress merely because it is unavailable.

**Why:** CPU stress testing can be physically stressful to a machine, so the safety supervisor must be conservative and independent of the general diagnostic score.

### Pass 12 — Diagnostic health model and customer report

Added the customer-facing health model and report layer. Customer components are Storage, Temperature, RAM/resource state, Windows integrity, Windows stability, Devices, Battery, and Storage space. Overall status can be `GOOD`, `ATTENTION`, `CRITICAL`, or `INCOMPLETE`; numeric overall scoring remains withheld.

Storage condition is explicitly separated from endurance. Missing storage health evidence is represented as unknown rather than silently becoming healthy or zero. Validated SSD life-remaining semantics were corrected so a value such as 36% remaining remains 36% remaining and derives 64% endurance used.

Added non-repairing Windows integrity evidence using SFC `/verifyonly`, DISM `/Online /Cleanup-Image /CheckHealth`, and an online CHKDSK scan of the system volume. The dark report preview was changed to present the customer health summary first while retaining technician evidence.

**Why:** The Inspector needs to turn collected evidence into a customer-understandable conclusion without hiding uncertainty or pretending that endurance is a failure probability.

**Validation:** Pass 12 was merged to `main` as merge commit `7838de3099227bcf09a88d687d818db53b7eb1ba`.

### Pass 13 — Dynamic horizontal synthetic inspection lab + real-machine validation

Added a deterministic randomized synthetic inspection environment covering 12 scenario families: healthy-desktop, aging-ssd, failing-hdd, healthy-nvme, thermal-problem, high-memory-use, windows-integrity-problem, missing-evidence, conflicting-providers, sparse-machine, provider-fallback, and mixed-faults.

The lab feeds synthetic evidence through the same normalization, interpretation, assessment, scoring, advanced-assessment, summary and customer-health chain used by the application. The historical `HS-SSD-WAVE(S) 256G` representation is included as a regression invariant: validated life 36% remaining must normalize to 36% remaining / 64% endurance used.

**Why:** The project needed a repeatable horizontal environment for exercising cross-category logic and regression invariants without requiring physical hardware for every test.

Pass 13 was also exercised on **real physical computers**. These runs confirmed real WMI/provider acquisition and fallback, real customer-health report generation, real PnP problem detection, conservative handling of missing SMART evidence, and CPU-stress safety behavior. One physical run successfully obtained ATA SMART through a `/dev/sda` fallback after the direct Windows physical-drive path failed. Another real machine produced a CRITICAL HDD result based on pending sectors.

**Important distinction:** The synthetic lab and real-machine inspection are separate validation surfaces. The synthetic lab does not make physical provider behavior PASS by itself; the physical runs provide that evidence.

**Validation:** Pass 13 was merged to `main` as merge commit `540c71078efa60b8587d8d3c06f3eb15baf167d3`. Windows CI build for the Pass 13 head succeeded. Real-machine runs have produced actual reports and logs showing provider fallback, missing-evidence handling, PnP findings, storage findings and CPU-stress safety behavior.

## 6. Current CPU telemetry/graph design

The stress sampler polls the safety path at a **50 ms target ceiling** while separately learning observed sensor/provider update cadence.

The distinction is deliberate:

- **Polling interval:** how often the application attempts to read telemetry.
- **Sensor freshness/update cadence:** how often the underlying provider actually changes the reported value.
- **Decision cadence:** how often safety logic evaluates the evidence.
- **Graph refresh cadence:** how often the WPF display is redrawn.

A real-machine test demonstrated why this distinction matters: the software recorded a 50 ms polling interval, while actual captured sample timestamps were typically around 120–150 ms apart. The graph must therefore not visually imply 20 Hz physical sensor updates.

The graph implementation was changed to position points according to their actual `CapturedAt` timestamps rather than their array index. The report graph also records actual captured span, median actual capture interval, telemetry polling target, and plotted-point count.

This is an evidence-honesty requirement, not merely a visual improvement.

## 7. Current real-machine CPU stress evidence

The first real CPU stress validation captured 291 timestamped samples during a planned 60-second test. The test completed without a safety abort.

Observed evidence included maximum CPU temperature 69 °C, baseline temperature 43.5 °C, baseline average clock approximately 3492 MHz, minimum observed average clock approximately 3018 MHz, maximum observed average clock approximately 3592 MHz, CPU load reaching 100%, RAM telemetry captured, and GPU telemetry unavailable on that run.

The isolated clock dip recovered and did not meet the sustained collapse criteria. Temperature remained below the abort thresholds.

This validates the core behavior on that physical machine, but does **not** constitute proof of long-term stability or complete hardware/provider coverage.

## 8. What has been removed and why

### Manual storage selection workflow

**Removed:** User-facing individual storage selection/window and launch control.

**Why:** Full inspection must automatically discover every physical storage device and produce one combined report. Manual selection is unnecessary for normal service work.

### Obsolete identical-telemetry stale abort test

**Removed:** The synthetic test requiring repeated identical telemetry to eventually abort.

**Why:** Slow hardware/provider telemetry can legitimately return unchanged values. The new design distinguishes repeated reads from actual sensor update cadence, so unchanged values alone are not evidence of failure.

### Numeric overall percentage during calibration

**Withheld rather than removed:** `OverallScore` remains null.

**Why:** A percentage without cross-category calibration would create false precision.

## 9. Current known limitations / unfinished work

- Pass 10/12 storage calibration remains open where physical reruns are needed to confirm corrected endurance normalization against independent evidence.
- Pass 13 physical validation is ongoing across multiple machines; real-world provider/normalization behavior may still require correction.
- Storage temperature currently has current-value evidence rather than a historical timestamped series; no storage history graph may be fabricated.
- GPU telemetry depends on what the hardware/provider exposes.
- Battery evidence may be unavailable on systems without a usable battery/WMI source.
- SCSI/SAS and USB/SAT evidence remains conservative and provider-dependent.
- Storage link-speed capability inference remains conservative.
- The existing legacy scoring code still contains migration debt; numeric overall scoring is intentionally withheld.
- The synthetic lab currently provides controlled scenario coverage but still needs deeper field-level provider/fallback modeling and stronger scenario-specific fault generation before it can represent every physical acquisition edge case.
- A completed 60-second CPU stress test is evidence of the recorded workload, not a guarantee of long-term stability.

## 10. Change-log rule

Every future project modification must add an entry here containing at minimum: Date, Pass/area, What changed, Added/removed/modified, Why, Files affected, Validation status, and Remaining limitations if any.

Never record a change as physically validated unless the corrected artifact was actually run on a physical machine.

## 11. Current state — 2026-09-15

**Active phase:** Pass 13 — multi-machine physical validation and storage/provider calibration.

Pass 11 and Pass 12 are merged into `main`. Pass 13 is also merged into `main`. The current application therefore contains the customer-health model, Windows integrity evidence, CPU stress safety/telemetry work, and the synthetic inspection lab.

Current physical testing has demonstrated real provider acquisition and fallback behavior, real customer-health conclusions, conservative handling of missing SMART evidence, PnP fault detection, and CPU stress safety refusal/completion behavior. The project is **not yet declaring Pass 13 fully closed**, because continued multi-machine testing is required to expose and resolve provider-specific normalization/acquisition issues.

The next work should prioritize evidence correctness discovered on physical machines, especially storage normalization/provenance, before adding cosmetic or unrelated features.

## 12. Development history entries

### 2026-09-23 — Production MVP foundation — cancellable process lifecycle

**What changed:** Added one structured process runner with process identity, start/finish/last-output timestamps, streamed stdout/stderr, exit code, timeout and cancellation handling. SFC, DISM and CHKDSK now use it with the active inspection cancellation token; smartctl uses the same runner for its existing synchronous acquisition path.

**Type:** Added / modified.

**Why:** Process execution was previously duplicated across collectors and integrity checks, and long-running integrity commands could not observe an inspection cancellation request.

**Files affected:** `ProcessExecutionService.cs`, `WindowsIntegrityService.cs`, `EvidenceEngine.cs`, cancellation tests.

**Validation:** Isolated-output Release build passed with 0 warnings/errors; tests passed 30/30; Pass 13 seeded synthetic lab passed 60/60.

**Remaining:** Process-tree termination is limited to the direct Inspector-owned process on .NET Framework 4.8; CrystalDiskInfo and PawnIO cleanup still have legacy process wrappers to migrate. Direct smartctl collection does not yet receive the UI cancellation token. The native UI bridge was unable to attach to the elevated Technician Console, so a manual cancel-during-collection run remains required on a controllable physical machine.

### 2026-09-23 — Production MVP foundation — fault-isolated stages

**What changed:** Added an inspection stage contract and runner, a recorded preflight stage, explicit inspection completion states, cancellation entry point, indeterminate progress, and mode selection. Individual collector exceptions now become an `ERROR` stage and preserved limitation rather than immediately terminating later independent stages.

**Type:** Added / modified.

**Why:** The original fixed serial UI pipeline rethrew any stage failure, which could discard the technician workflow despite usable evidence from other collectors.

**Files affected:** `InspectionStageRunner.cs`, `Models.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, regression tests.

**Validation:** Isolated-output Release build passed with 0 warnings/errors; tests passed 28/28; Pass 13 seeded synthetic lab passed 60/60.

**Remaining:** External utilities do not yet accept a cancellation token after launch; cancellation prevents subsequent stages but a currently running external command can run until its existing timeout. Full manual per-test selection, packaged clean-machine validation, and physical-machine acceptance remain open.

### 2026-09-23 — Validation wiring — Pass 13 standalone runner

**What changed:** Corrected the standalone lab runner namespace, added its project to the solution, and added the deterministic Pass 13 run to the pull-request validation workflow.

**Type:** Fixed / modified.

**Why:** The runner namespace shadowed the production `SyntheticInspectionLab` type and could not compile. Because its project was omitted from the solution and CI workflow, the documented Pass 13 validation was neither built nor run by normal validation.

**Files affected:** `tests/SyntheticInspectionLab/Program.cs`, `A2ZSysIns.sln`, `.github/workflows/pass8-validation.yml`.

**Validation:** Clean `Release|x64` solution build passed with 0 warnings/errors; unit suite passed 26/26; seeded Pass 13 run passed 60/60 generated machines across 12 scenario families.

**Remaining:** This validates synthetic interpretation only; physical-machine validation remains required.

### 2026-09-15 — Pass 11 — Real-time graph honesty correction

**What changed:** Stress/report graphs were changed to use real `CapturedAt` timestamps. A cadence summary was added to the report graph.

**Type:** Modified / added.

**Why:** The real machine showed that 50 ms application polling did not equal 50 ms actual telemetry updates. Graphs must represent the physical evidence actually captured.

**Files affected:** `src/A2ZSysIns/CpuStressGraphService.cs` and associated graph/report integration.

**Validation:** Windows build passed and synthetic validation passed for commit `2a0b3ff8343b7c21d29cd7c0a61ae7705a3fe713`.

**Remaining:** Physical-machine validation of the revised visual presentation is still required.

### 2026-09-15 — Pass 12 — Customer health model and report

**What changed:** Added the customer-facing health model, cross-category health assessment, Windows integrity evidence and customer-first dark report preview. Corrected validated SSD life-remaining semantics.

**Type:** Added / modified.

**Why:** The Inspector needed a defensible customer-level conclusion while preserving technician evidence and uncertainty.

**Files affected:** Pass 12 health-model, normalization, Windows-integrity, report-preview and integration files.

**Validation:** PR #7 merged to `main` as `7838de3099227bcf09a88d687d818db53b7eb1ba`.

**Remaining:** Physical rerun of affected storage cases remains required for calibration closure.

### 2026-09-15 — Pass 13 — Dynamic synthetic inspection lab and physical validation

**What changed:** Added `SyntheticInspectionLab`, its .NET Framework 4.8 runner/project and documentation. The lab generates deterministic randomized cross-category machines and validates critical health-model invariants. Pass 13 was then merged to `main` and the resulting build was exercised on real computers.

**Type:** Added / merged / physically exercised.

**Why:** The project needed repeatable horizontal regression coverage while also validating actual WMI, smartctl, sensor, Windows and device behavior on physical machines.

**Files affected:** `src/A2ZSysIns/SyntheticInspectionLab.cs`, `tests/SyntheticInspectionLab/Program.cs`, `tests/SyntheticInspectionLab/SyntheticInspectionLab.csproj`, `docs/PASS13_SYNTHETIC_LAB.md`, plus the Pass 12 integration/model files already merged through PR #7.

**Validation:** PR #8 merged to `main` as `540c71078efa60b8587d8d3c06f3eb15baf167d3`. Windows CI build for the Pass 13 head succeeded. Real-machine runs have produced actual reports and logs showing provider fallback, missing-evidence handling, PnP findings, storage findings and CPU-stress safety behavior.

**Remaining:** Continue multi-machine physical validation and fix any evidence-normalization/provider issues exposed by real hardware. Do not mark synthetic coverage as equivalent to physical validation.

### 2026-09-15 — Documentation governance established

**What changed:** Created this `DEVELOPMENT_CONTEXT.md` as the mandatory project-context document.

**Type:** Added.

**Why:** The project has accumulated many architectural decisions, reversals, fixes and reasons. Those decisions must survive conversation boundaries and must be read before future work.

**Rule:** This document must be read before any future project inspection or modification, and updated after every project change.

**Validation:** Repository document creation completed; ongoing compliance is required for all subsequent project work.
