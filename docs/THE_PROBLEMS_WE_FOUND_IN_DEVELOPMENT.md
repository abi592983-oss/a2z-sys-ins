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
- **Problem:** Storage life/endurance recognition previously understood only a small set of attribute names and did not preserve enough normalized ATA SMART fields for robust vendor/controller-aware interpretation.
- **Date identified:** 2026-09-14
- **Area:** Storage / SMART interpretation
- **Impact:** Valid health/endurance information could be missed or reported as unavailable; a CrystalDisk-like storage report could not yet be produced reliably across device families.
- **Status:** Fixed — Pass 3 interpretation layer added
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Added `StorageInterpretationService` with name-validated ATA semantics, NVMe standard health interpretation, conservative vendor/device-style life-field recognition, and explicit separation of condition from endurance.
- **Validation:** Code-path review complete; real-machine calibration pending.

### DEV-002 — Storage scoring is corrected after initial scoring
- **Problem:** The current inspection sequence still calculates scores and then normalizes storage SMART interpretations before advanced assessment. This creates a remaining score-then-normalize compatibility path.
- **Date identified:** 2026-09-14
- **Area:** Scoring / pipeline
- **Impact:** The architecture remains harder to reason about than a strict interpretation → findings → scoring pipeline, even though Pass 3 now interprets storage before the scorer and prevents invalid legacy SMART IDs from reaching its storage rules.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Pass 3 reduced the risk but did not remove the legacy normalization stage. Full cleanup is retained as technical debt for a future scoring refactor.
- **Validation:** Pending future scoring cleanup.

### DEV-003 — Health percentage/scoring model is not yet calibrated across all problem categories
- **Problem:** The current rules intentionally avoid an invented overall percentage, but the eventual health percentage and scoring system still needs evidence-based calibration across storage, CPU/thermal, memory, Windows events, PnP and other diagnostic categories rather than being designed as storage-only math.
- **Date identified:** 2026-09-14
- **Area:** Cross-system scoring
- **Impact:** A future percentage must not imply precision that the available evidence cannot support.
- **Status:** Open
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Pass 10 defines the real-machine calibration protocol. No numeric percentage is introduced until representative evidence justifies it.
- **Validation:** Physical calibration pending.

### DEV-004 — Diagnostic graphs are not yet part of the evidence presentation
- **Problem:** The current report/UI does not yet provide useful time-series graphs for captured measurements such as storage temperature and CPU temperature.
- **Date identified:** 2026-09-14
- **Area:** UI / reporting
- **Impact:** Temperature behavior and trends are harder for technicians to interpret from a snapshot.
- **Status:** Open — Pass 7 adds graph-safe UI messaging but not fabricated history
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Pass 7 explicitly prevents presentation of a historical storage graph when only a current reading exists. CPU stress samples already provide real elapsed-time temperature data; actual graph rendering remains a later presentation enhancement.
- **Validation:** Code-path review complete; real capture validation pending.

### DEV-005 — Individual storage inspection is not yet available
- **Problem:** The current front-window workflow is a full-system inspection rather than a dedicated per-drive inspection experience.
- **Date identified:** 2026-09-14
- **Area:** UI / storage workflow
- **Impact:** A technician cannot yet select one HDD/SSD/NVMe/removable drive for a focused CrystalDisk-like inspection.
- **Status:** Fixed — Pass 7 UI added; live re-acquisition remains a limitation
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Pass 6 added `IndividualStorageInspectionService`; Pass 7 added `IndividualStorageInspectionWindow` and an Evidence-screen launch button. The UI uses the backend target/result contract and does not implement SMART rules itself.
- **Validation:** UI code-path review complete; build/runtime and real-machine validation pending.

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

### DEV-008 — NVMe controller temperature field was mapped from the wrong SMART field
- **Problem:** Pass 2 acquisition assigned `controller_busy_time` to `NvmeHealthRecord.ControllerTemperatureC`, even though controller busy time is not a temperature measurement.
- **Date identified:** 2026-09-14
- **Area:** Storage / NVMe acquisition semantics
- **Impact:** A report could expose a non-temperature NVMe value as controller temperature, corrupting future thermal interpretation and graphs.
- **Status:** Fixed — Pass 3 correction
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** `NvmeHealthRecord.ControllerTemperatureC` now reads the NVMe `controller_temperature` field, and the top-level drive field uses that same value.
- **Validation:** Code-path review complete; real-machine NVMe validation pending.

### DEV-009 — Storage condition and endurance were not represented as independent assessment dimensions
- **Problem:** Before Pass 4, the storage model had a remaining-life field and a general assessment, but no explicit persisted distinction between condition confidence and endurance confidence.
- **Date identified:** 2026-09-14
- **Area:** Storage / assessment model
- **Impact:** Future scoring could accidentally collapse endurance into overall health or treat unavailable endurance as evidence of failure/health.
- **Status:** Fixed — Pass 4 assessment model added
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Added `StorageHealthAssessmentService`, which records condition, condition confidence, endurance availability/value and endurance confidence as separate measurement evidence. No global percentage was introduced.
- **Validation:** Code-path review complete; real-machine calibration pending.

### DEV-010 — Focused storage inspection must not imply live re-acquisition
- **Problem:** The new individual storage UI could be mistaken for an independent live drive scan even though Pass 6/7 currently reuse evidence captured by the completed full-system acquisition.
- **Date identified:** 2026-09-14
- **Area:** Storage / UI semantics
- **Impact:** A technician could assume the focused screen refreshed hardware state when it actually reinterprets already-acquired evidence.
- **Status:** Fixed — UI limitation made explicit
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** The focused inspector is explicitly described as presentation over acquired evidence, and the UI does not claim live re-acquisition. A future targeted acquisition enhancement can be added behind the same backend contract.
- **Validation:** Code-path review complete; technician usability validation pending.

### DEV-011 — Support claims need an explicit evidence boundary
- **Problem:** Storage support spans SATA/ATA, SAT, NVMe, SCSI/SAS and USB/removable paths, but without a normative matrix it is easy for documentation or UI wording to imply that every device exposes every SMART, temperature or endurance field.
- **Date identified:** 2026-09-14
- **Area:** Documentation / support semantics
- **Impact:** Overstated support could make unavailable evidence look like an acquisition failure or, worse, imply a healthy result from missing data.
- **Status:** Fixed — Pass 9 support matrix added
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** Added `docs/STORAGE_SUPPORT_MATRIX.md`, defining supported acquisition paths, evidence hierarchy, confidence boundaries, unavailable-field behavior, explicit non-claims and focused-inspector limitations.
- **Validation:** Documentation/code-path review complete; real-device coverage remains pending.

### DEV-012 — Real-machine calibration has not yet been executed
- **Problem:** Synthetic fixtures can validate deterministic semantics but cannot establish that real Windows controllers, firmware, USB bridges and physical devices expose evidence as expected.
- **Date identified:** 2026-09-14
- **Area:** Validation / calibration
- **Impact:** Real-world false positives, false negatives, missing fields and identity/acquisition quirks could remain undiscovered.
- **Status:** Open — Pass 10 protocol implemented
- **Date fixed:** —
- **Ever fixed:** No
- **Fix / evidence:** Added `docs/REAL_MACHINE_CALIBRATION.md` with representative machine classes, controlled capture procedure, ground-truth requirements, worksheet, pass/fail rules and regression handling. No physical result is claimed by the repository.
- **Validation:** Pending actual machine captures.

### DEV-013 — Real-machine package omitted the primary SMART executable
- **Problem:** The first physical HP desktop run correctly enumerated both a Seagate HDD and an HS-SSD-WAVE SSD, but the actual application package reported `Bundled smartctl.exe is missing from the application package` and skipped SMART acquisition for both drives.
- **Date identified:** 2026-09-14
- **Area:** Storage / packaging / acquisition integration
- **Impact:** Both real drives were reported as `Not assessed` even though independent hardware tooling exposed SMART attributes, temperatures and SSD life data. This made the storage diagnostic appear substantially worse than the actual evidence availability.
- **Status:** Fixed in code/package pipeline; physical verification pending
- **Date fixed:** 2026-09-14
- **Ever fixed:** Yes
- **Fix / evidence:** The Windows build now verifies and packages official smartmontools and adds a headless CrystalDiskInfo `/CopyExit` last-resort provider. CrystalDiskInfo evidence is matched to the WMI drive identity and fed into the existing A2Z interpretation/reporting pipeline rather than replacing it.
- **Validation:** Real-machine discovery confirmed the failure. Corrected artifact build and repeat run on the same HP machine are pending.

## Status convention

A problem marked **Fixed** means a code/documentation change was made. It does not mean the rule is permanently proven. Real-machine validation can reopen an issue if evidence shows the fix is incomplete or introduces a regression.
