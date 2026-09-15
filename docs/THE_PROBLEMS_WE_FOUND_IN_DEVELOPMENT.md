# The Problems We Found in Development

This document is a permanent development history. Fixed problems are never deleted; fixes are recorded alongside the original problem.

## Status convention
- **Status:** current state of the problem.
- **Date identified:** when the problem was discovered.
- **Date fixed:** when the implementation fix was completed, if applicable.
- **Ever fixed:** Yes/No. This field must remain even after a fix.

### DEV-001 — Baseline storage evidence was too weak
- **Problem:** Initial storage inspection did not provide a sufficiently rich evidence model for physical drives, SMART data, NVMe health, identity, temperature, endurance, and evidence quality.
- **Date identified:** 2026-09-14
- **Area:** Storage / architecture
- **Impact:** Storage conclusions could not be made reliably or explained to technicians.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Added the richer `DriveInfoRecord`, SMART attribute, NVMe health, and storage evidence structures in Pass 1.

### DEV-002 — Storage acquisition and interpretation were mixed together
- **Problem:** Acquisition concerns and health interpretation were too tightly coupled.
- **Date identified:** 2026-09-14
- **Area:** Storage / architecture
- **Impact:** It was difficult to distinguish missing evidence from an unhealthy device.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Separated acquisition in `StorageAcquisitionService` from interpretation in `StorageInterpretationService`.

### DEV-003 — ATA SMART IDs were being treated as universal meanings
- **Problem:** Numeric SMART IDs such as 5, 197, 198 and 199 were vulnerable to being interpreted without validating their actual attribute semantics.
- **Date identified:** 2026-09-14
- **Area:** Storage / SMART interpretation
- **Impact:** Vendor-specific SMART attributes could produce false health conclusions.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Attribute names and validated semantics are used before interpreting ATA health indicators.

### DEV-004 — Storage endurance was confused with overall drive health
- **Problem:** Remaining life/endurance was at risk of being treated as a failure-probability or overall-health percentage.
- **Date identified:** 2026-09-14
- **Area:** Storage / scoring
- **Impact:** A useful endurance indicator could be presented as a misleading overall health score.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Storage health assessment now keeps condition and endurance as separate evidence dimensions.

### DEV-005 — Missing SMART evidence could be mistaken for a healthy drive
- **Problem:** Lack of SMART data was not sufficiently distinguished from a healthy SMART result.
- **Date identified:** 2026-09-14
- **Area:** Storage / evidence quality
- **Impact:** Unsupported or unavailable evidence could create false reassurance.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Storage health uses UNKNOWN/Not assessed when the required evidence is unavailable, with confidence/coverage kept separate.

### DEV-006 — Cross-category health scoring needed an evidence-first foundation
- **Problem:** Storage, thermal, event, resource, battery and Windows-device findings could not safely be combined into a defensible numeric score without calibration.
- **Date identified:** 2026-09-14
- **Area:** Diagnostic scoring
- **Impact:** Arbitrary percentage scores could misrepresent the condition of a machine.
- **Status:** Partially addressed; numeric overall score deliberately withheld pending calibration
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Added category assessment and evidence-coverage foundations while keeping `OverallScore` null until real-machine calibration supports a defensible model.

### DEV-007 — Individual storage inspection needed safe identity matching
- **Problem:** A drive inspection workflow needed to avoid accidentally displaying evidence from another physical drive.
- **Date identified:** 2026-09-14
- **Area:** Storage / identity
- **Impact:** Ambiguous device matching could lead to incorrect technician conclusions.
- **Status:** Fixed at backend-contract level; user-facing individual workflow later removed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Identity resolution prefers serial, then unique model, then safe model/capacity matching; ambiguous targets fail safely.

### DEV-008 — Storage history graphs must not invent historical data
- **Problem:** A single storage-temperature reading cannot legitimately be presented as a historical temperature graph.
- **Date identified:** 2026-09-14
- **Area:** Diagnostics / visualization
- **Impact:** Invented history would undermine diagnostic trust.
- **Status:** Fixed by policy
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Graphs are restricted to actual timestamped samples. Current storage evidence without a time series is not presented as history.

### DEV-009 — Automated validation was missing
- **Problem:** Storage interpretation and scoring changes needed repeatable synthetic regression coverage before relying on physical machines.
- **Date identified:** 2026-09-14
- **Area:** Testing
- **Impact:** Regressions could reach real-machine validation unnoticed.
- **Status:** Fixed
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Pass 8 added automated synthetic validation fixtures and CI coverage; the validation suite passed before merge.

### DEV-010 — Packaging did not reliably include smartctl
- **Problem:** A real HP machine initially reported that the bundled smartctl executable was missing from the application package.
- **Date identified:** 2026-09-14
- **Area:** Packaging / storage acquisition
- **Impact:** The application fell back to unavailable storage SMART evidence despite the machine supporting SMART.
- **Status:** Fixed
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Packaging was updated to include smartmontools and validate the packaged executable.

### DEV-011 — Direct Windows physical-drive smartctl path was not sufficient for every device
- **Problem:** On the HP machine, direct `\\.\\PHYSICALDRIVE0` identification failed while the `/dev/sda` ATA fallback succeeded.
- **Date identified:** 2026-09-15
- **Area:** Storage acquisition / Windows compatibility
- **Impact:** A valid SATA HDD could appear unavailable through the first acquisition path.
- **Status:** Fixed
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Acquisition tries multiple smartctl scan/open and ATA/SAT-compatible paths rather than relying on a single Windows device path.

### DEV-012 — CrystalDiskInfo fallback was needed for broader practical storage evidence
- **Problem:** Some Windows storage configurations require an additional evidence provider when smartctl and Windows ATA fallback cannot expose sufficient SMART data.
- **Date identified:** 2026-09-15
- **Area:** Storage acquisition / compatibility
- **Impact:** Supported physical devices could otherwise remain insufficiently assessed.
- **Status:** Fixed at packaging/integration level; real-machine coverage remains part of Pass 10
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Added CrystalDiskInfo 9.9.2 as a last-resort evidence provider using its supported `/CopyExit` output. A2Z does not import its scoring model.

### DEV-013 — Storage inspection workflow was unnecessarily manual
- **Problem:** The earlier design required technicians to select individual storage devices for inspection.
- **Date identified:** 2026-09-15
- **Area:** Workflow / storage UI
- **Impact:** Normal inspection became slower and more complicated than necessary.
- **Status:** Fixed
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Normal inspection now detects and scans all physical storage devices automatically and produces one combined report. CPU stress testing remains technician-controlled.

### DEV-014 — SSD life attribute used vendor raw counter instead of normalized percentage
- **Problem:** On the HP HS-SSD-WAVE(S) 256G, the E7 SSD Life Left SMART row had normalized value 64 but raw value 36. The interpretation code used the raw value and reported 0% remaining / 100% used instead of 64% remaining.
- **Date identified:** 2026-09-15
- **Area:** Storage / SMART interpretation / endurance
- **Impact:** A valid SSD endurance indicator was misreported on a real device.
- **Status:** Fixed in code; repeat physical verification pending
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Life attributes now prefer the normalized SMART value when available because the raw field is vendor-specific. Raw evidence remains preserved. A regression fixture was added for the real E7 representation.
- **Validation:** Synthetic regression test added. Same HP machine must be rerun before Pass 10 closes.

### DEV-015 — Storage selection UI and legacy-looking controls conflict with technician workflow
- **Problem:** The storage selector used a bright/default selection treatment and the controls had an old WPF/1980s-1990s appearance.
- **Date identified:** 2026-09-15
- **Area:** UI / usability / visual design
- **Impact:** Selected storage text could become unreadable against the dark/green theme, and technicians had to perform unnecessary manual storage interaction.
- **Status:** Fixed in UI branch
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Removed the manual storage selector/window and launch button. The main console now uses a modern dark UI, Segoe UI typography, rounded controls, dark selection states and green accents. CPU staged testing remains technician-controlled and optional.
- **Validation:** XAML/code change complete; CI/runtime validation pending.

### DEV-016 — Development history document was accidentally truncated during an append
- **Problem:** While adding DEV-014 and DEV-015, the development-problem document on the branch was reduced to only the new entries, removing the earlier historical record from the file.
- **Date identified:** 2026-09-15
- **Area:** Documentation / development process
- **Impact:** Violated the permanent-history requirement that fixed problems must never be erased.
- **Status:** Fixed
- **Date fixed:** 2026-09-15
- **Ever fixed:** Yes
- **Fix / evidence:** Restored the historical entries DEV-001 through DEV-015 and recorded this incident as DEV-016.

### DEV-017 — Pass 13 real-machine validation exposed provider-path differences that must remain visible
- **Problem:** Real machines can fail on one storage acquisition path while succeeding through another provider/path, and some devices expose incomplete SMART evidence.
- **Date identified:** 2026-09-15
- **Area:** Storage acquisition / physical validation
- **Impact:** A single failed provider attempt must not be treated as device failure or as proof that evidence is unavailable.
- **Status:** Addressed in acquisition design; broader coverage remains under validation
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Pass 13 real runs confirmed useful fallback behavior, including a successful `/dev/sda` ATA fallback after direct Windows physical-drive identification failed. Missing SMART on another device was conservatively represented as UNKNOWN.

### DEV-018 — Pass 13 physical validation showed that real provider behavior cannot be represented by synthetic tests alone
- **Problem:** Synthetic scenarios can validate assessment logic but cannot reproduce every WMI, smartctl, sensor, USB/SAT, Windows-event, or device-specific behavior found on physical computers.
- **Date identified:** 2026-09-15
- **Area:** Testing / validation strategy
- **Impact:** Treating synthetic PASS as physical PASS could hide provider-specific failures.
- **Status:** Open by design
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Pass 13 explicitly separates synthetic regression coverage from multi-machine physical validation. The development index now records physical validation as the active phase.

### DEV-019 — Synthetic Pass 13 scenario generation still needs stronger fault realism
- **Problem:** Some synthetic scenario families currently populate generic/default evidence rather than fully scenario-specific faults, and provider fallback/conflict behavior is not yet modeled at complete field level.
- **Date identified:** 2026-09-15
- **Area:** Testing / synthetic lab
- **Impact:** The lab may under-exercise some intended scenario names and provider-resolution edge cases.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** The lab is useful for deterministic horizontal regression, but additional work is needed for true high-memory utilization, scenario-specific Windows events/PnP faults, controlled provider conflicts, and field-level fallback exhaustion.
