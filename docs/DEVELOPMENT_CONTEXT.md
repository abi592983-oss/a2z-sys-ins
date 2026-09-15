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

Added `StorageHealthAssessmentService` and separated:

- condition
- condition confidence
- endurance
- endurance confidence
- explanation/reason

**Why:** Endurance is useful evidence but is not equivalent to overall health or failure probability.

### Pass 5 — Cross-category diagnostic foundation

Added category-level assessment foundations for storage, thermals, Windows events, resources, battery and Windows-device status. Numeric `OverallScore` remains null.

**Why:** A defensible overall percentage must be calibrated across the entire diagnostic system, not invented from storage alone.

### Pass 6/7 — Individual storage inspection architecture and UI experiment

A focused storage-inspection backend contract and UI were developed as an experiment. Identity matching was designed to prefer serial number and fail safely on ambiguity.

The user-facing individual-storage workflow was subsequently **removed**.

**Why removed:** Normal technician workflow should automatically inspect all physical storage and provide one combined result. Manual device selection added unnecessary work and created identity/UI complexity.

The backend contract remains as internal architecture/test material where useful.

### Pass 8 — Automated/synthetic validation

Added synthetic validation fixtures and CI coverage. The x64 Windows build/test path was corrected. The validation suite reached 27 passing tests after the obsolete stale-telemetry test was removed.

**Why:** Regression coverage was required before relying on physical-machine testing.

### Pass 9/10 — Documentation, support matrix and real-machine calibration

Added storage support documentation, calibration documentation, the CrystalDiskInfo evidence fallback and packaging improvements.

Real-machine testing exposed:

- missing packaged smartctl
- direct Windows physical-drive path failure with a working `/dev/sda` fallback
- SSD life normalized-vs-raw interpretation error
- additional unrelated Windows findings

The packaging/acquisition path was corrected, and real-machine validation is continuing.

### Pass 11 — CPU stress safety and telemetry

Added a dedicated CPU stress safety supervisor, adaptive telemetry cadence tracking, timestamped CPU stress samples, live/report graphs and automated tests.

Safety design includes:

- CPU temperature telemetry required for stress
- CPU clock telemetry required
- CPU load telemetry required
- preflight refusal for missing/invalid required telemetry
- 80 °C preflight refusal threshold
- 87 °C early-abort threshold
- 90 °C hard-abort threshold
- clock-collapse detection under sustained high load
- abnormal clock-surge detection under sustained high load
- fail-closed behavior for required telemetry loss
- optional RAM/GPU telemetry that does not abort CPU stress merely because it is unavailable

**Why:** CPU stress testing can be physically stressful to a machine, so the safety supervisor must be conservative and independent of the general diagnostic score.

## 6. Current CPU telemetry/graph design

The stress sampler polls the safety path at a **50 ms target ceiling** while separately learning observed sensor/provider update cadence.

The distinction is deliberate:

- **Polling interval:** how often the application attempts to read telemetry.
- **Sensor freshness/update cadence:** how often the underlying provider actually changes the reported value.
- **Decision cadence:** how often safety logic evaluates the evidence.
- **Graph refresh cadence:** how often the WPF display is redrawn.

A real-machine test demonstrated why this distinction matters: the software recorded a 50 ms polling interval, while actual captured sample timestamps were typically around 120–150 ms apart. The graph must therefore not visually imply 20 Hz physical sensor updates.

The graph implementation was changed to position points according to their actual `CapturedAt` timestamps rather than their array index. The report graph also records:

- actual captured span
- median actual capture interval
- telemetry polling target
- plotted-point count

This is an evidence-honesty requirement, not merely a visual improvement.

## 7. Current real-machine CPU stress evidence

The first real CPU stress validation captured 291 timestamped samples during a planned 60-second test. The test completed without a safety abort.

Observed evidence included:

- maximum CPU temperature: 69 °C
- baseline temperature: 43.5 °C
- baseline average clock: approximately 3492 MHz
- minimum observed average clock: approximately 3018 MHz
- maximum observed average clock: approximately 3592 MHz
- CPU load reached 100%
- RAM telemetry was captured
- GPU telemetry was unavailable on that run

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

- Pass 10 real-machine storage calibration is not considered complete until corrected acquisition/interpretation is physically rerun and compared against independent evidence.
- Storage temperature currently has current-value evidence rather than a historical timestamped series; no storage history graph may be fabricated.
- GPU telemetry depends on what the hardware/provider exposes.
- Battery evidence may be unavailable on systems without a usable battery/WMI source.
- SCSI/SAS and USB/SAT evidence remains conservative and provider-dependent.
- Storage link-speed capability inference remains conservative.
- The existing legacy scoring code still contains migration debt; numeric overall scoring is intentionally withheld.
- A completed 60-second CPU stress test is evidence of the recorded workload, not a guarantee of long-term stability.

## 10. Change-log rule

Every future project modification must add an entry here containing at minimum:

- **Date**
- **Pass / area**
- **What changed**
- **Added / removed / modified**
- **Why**
- **Files affected**
- **Validation status**
- **Remaining limitations**, if any

Never record a change as physically validated unless the corrected artifact was actually run on a physical machine.

## 11. Current state — 2026-09-15

**Active phase:** Pass 11 — CPU stress safety + high-frequency telemetry/live graphs.

The latest graph correction is committed on `pass-11-cpu-safety` as `2a0b3ff8343b7c21d29cd7c0a61ae7705a3fe713`. Windows build and synthetic validation both passed for that commit.

The immediate next activity is physical validation of the revised graph/cadence presentation. After that, continue Pass 10 storage calibration where still required, without losing the Pass 11 evidence work.

## 12. Development history entries

### 2026-09-15 — Pass 11 — Real-time graph honesty correction

**What changed:** Stress/report graphs were changed to use real `CapturedAt` timestamps. A cadence summary was added to the report graph.

**Type:** Modified / added.

**Why:** The real machine showed that 50 ms application polling did not equal 50 ms actual telemetry updates. Graphs must represent the physical evidence actually captured.

**Files affected:** `src/A2ZSysIns/CpuStressGraphService.cs` and associated graph/report integration.

**Validation:** Windows build passed and synthetic validation passed for commit `2a0b3ff8343b7c21d29cd7c0a61ae7705a3fe713`.

**Remaining:** Physical-machine validation of the revised visual presentation is still required.

### 2026-09-15 — Documentation governance established

**What changed:** Created this `DEVELOPMENT_CONTEXT.md` as the mandatory project-context document.

**Type:** Added.

**Why:** The project has accumulated many architectural decisions, reversals, fixes and reasons. Those decisions must survive conversation boundaries and must be read before future work.

**Rule:** This document must be read before any future project inspection or modification, and updated after every project change.

**Validation:** Repository document creation completed; ongoing compliance is required for all subsequent project work.
