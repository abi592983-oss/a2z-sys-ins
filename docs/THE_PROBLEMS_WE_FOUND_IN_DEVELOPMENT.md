# The Problems We Found in Development

This is the historical record of problems discovered while developing and calibrating A2Z System Inspector. Entries are not deleted when fixed. If a problem is fixed and later regresses, add a new status/update entry rather than erasing the history.

## Record format

- **Problem:**
- **Date identified:**
- **Area:**
- **Impact:**
- **Status:** Open / Fixed / Reopened / Accepted limitation
- **Date fixed:**
- **Ever fixed:** Yes / No
- **Fix / evidence:**
- **Validation:**

## Initial findings — 2026-09-14

### DEV-001 — Storage SMART interpretation is too narrow
- **Problem:** Storage life/endurance recognition currently understands only a small set of attribute names and does not preserve enough normalized ATA SMART fields for robust vendor/controller-aware interpretation.
- **Date identified:** 2026-09-14
- **Area:** Storage / SMART interpretation
- **Impact:** Valid health/endurance information may be missed or reported as unavailable; a CrystalDisk-like storage report cannot yet be produced reliably across device families.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Planned for storage evidence model and interpretation passes.
- **Validation:** Pending real-machine calibration.

### DEV-002 — Storage scoring is corrected after initial scoring
- **Problem:** The current inspection sequence calculates scores and then normalizes storage SMART interpretations before advanced assessment. This creates a score-then-correct architecture.
- **Date identified:** 2026-09-14
- **Area:** Scoring / pipeline
- **Impact:** Raw SMART IDs can influence an initial result before semantic validation removes or changes the finding.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Planned pipeline cleanup after the new interpretation layer is established.
- **Validation:** Pending.

### DEV-003 — Health percentage/scoring model is not yet calibrated across all problem categories
- **Problem:** The current rules intentionally avoid an invented overall percentage, but the eventual health percentage and scoring system still needs evidence-based calibration across storage, CPU/thermal, memory, Windows events, PnP and other diagnostic categories rather than being designed as storage-only math.
- **Date identified:** 2026-09-14
- **Area:** Cross-system scoring
- **Impact:** A future percentage must not imply precision that the available evidence cannot support.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Planned cross-category scoring/calibration pass and real-machine validation.
- **Validation:** Pending.

### DEV-004 — Diagnostic graphs are not yet part of the evidence presentation
- **Problem:** The current report/UI does not yet provide useful time-series graphs for captured measurements such as storage temperature and CPU temperature.
- **Date identified:** 2026-09-14
- **Area:** UI / reporting
- **Impact:** Temperature behavior and trends are harder for technicians to interpret from a snapshot.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Planned after the evidence model and capture paths can provide trustworthy samples.
- **Validation:** Pending.

### DEV-005 — Individual storage inspection is not yet available
- **Problem:** The current front-window workflow is a full-system inspection rather than a dedicated per-drive inspection experience.
- **Date identified:** 2026-09-14
- **Area:** UI / storage workflow
- **Impact:** A technician cannot yet select one HDD/SSD/NVMe/removable drive for a focused CrystalDisk-like inspection.
- **Status:** Open — planned future feature
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Planned as a later backend/UI milestone after storage interpretation is stable.
- **Validation:** Pending.

### DEV-006 — Storage link-speed capability is not independently inferred
- **Problem:** The current advanced storage diagnostics report structured current/maximum interface speed when smartctl exposes it, but do not independently establish PCIe/controller capability.
- **Date identified:** 2026-09-14
- **Area:** Storage / interface diagnostics
- **Impact:** Some negotiated-speed situations can only be reported as supporting/informational evidence.
- **Status:** Accepted limitation for current rules; future enhancement possible
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Current design deliberately avoids unsupported controller-capability guesses.
- **Validation:** Revisit during storage acquisition/calibration.

### DEV-007 — Storage acquisition was tightly coupled to interpretation
- **Problem:** The previous storage path performed device acquisition, SMART parsing, life interpretation and preliminary assessment in one engine method. This made it difficult to preserve raw evidence for later vendor-aware interpretation and to distinguish unavailable data from health conclusions.
- **Date identified:** 2026-09-14
- **Area:** Storage / acquisition architecture
- **Impact:** Adding broader SATA/NVMe/USB coverage risked mixing acquisition assumptions with health rules.
- **Status:** Fixed — acquisition layer separated
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Added `StorageAcquisitionService` and routed the normal full-system inspection through it. The new layer records device-path/type attempts, identity validation, raw JSON, evidence quality and fallback status without introducing new health thresholds.
- **Validation:** Code-path review complete; real-machine validation pending.

## Status convention

A problem marked **Fixed** means a code/documentation change was made. It does not mean the rule is permanently proven. Real-machine validation can reopen an issue if evidence shows the fix is incomplete or introduces a regression.