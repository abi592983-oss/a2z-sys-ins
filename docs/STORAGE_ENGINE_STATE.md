# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 2 — Storage evidence acquisition
Status: Pass 2 complete

## Scope

The refactor will inspect the complete diagnostic system, while the first implementation focus is storage-device evidence and interpretation. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection is considered working enough to leave unchanged during the storage-engine work unless a dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory — complete
2. Pass 1 — Storage evidence data model — complete
3. Pass 2 — Storage evidence acquisition — complete
4. Pass 3 — SMART / storage interpretation engine
5. Pass 4 — Health, endurance, confidence and percentage/scoring model
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 2 work completed

Added `src/A2ZSysIns/StorageAcquisitionService.cs` and routed the normal full-system inspection through it.

The acquisition layer now:
- Enumerates physical disks with WMI identity information.
- Uses smartctl structured JSON as the primary acquisition source.
- Uses smartctl `--scan-open -j` device/type information when available instead of ignoring the reported device type.
- Tries automatic and explicit ATA, SAT, NVMe and SCSI acquisition paths without assuming that a transport is supported.
- Covers SATA HDD/SSD, NVMe, SCSI/SAS and USB/SAT-style paths where the underlying bridge exposes usable evidence.
- Validates returned serial numbers against the WMI device identity when both are available and rejects mismatched results.
- Preserves raw SMART JSON and separates acquisition quality from later interpretation.
- Populates the Pass 1 ATA attribute, NVMe health, temperature, endurance, firmware, vendor/product, transport and evidence-quality structures.
- Keeps the Windows `MSStorageDriver_FailurePredictData` / `MSStorageDriver_FailurePredictStatus` fallback, matched by PNP identity rather than enumeration order.
- Records fallback evidence as partial/raw evidence rather than pretending that vendor-specific meaning was recovered.
- Records unsupported, missing or unreadable SMART evidence as unavailable rather than converting it into a healthy value or a failure.

No new health thresholds or percentage calculations were introduced in Pass 2. The acquisition layer intentionally supplies evidence for Pass 3 interpretation.

## Acquisition design rule

The acquisition layer answers **what evidence was obtained and how reliably it was associated with the device**. It does not answer **whether the drive is healthy**. That separation is required before the scoring and percentage model is redesigned.

## Existing limitations retained

- Vendor/controller-specific life interpretation remains open for Pass 3.
- Storage scoring still has the score-then-normalize architecture and remains open for Pass 5.
- Overall health percentage/scoring remains uncalibrated and must eventually cover all diagnostic problem categories.
- Temperature graphs remain a later UI/reporting task and must use actual captured samples only.
- Individual-drive storage inspection remains a future feature.
- Storage link-speed capability inference remains deliberately conservative.

## Files changed in Pass 2

- `src/A2ZSysIns/StorageAcquisitionService.cs` — new storage acquisition layer.
- `src/A2ZSysIns/MainWindow.xaml.cs` — full-system inspection now uses the new acquisition layer.
- `docs/STORAGE_ENGINE_STATE.md` — updated phase/state.

## Next pass

Pass 3 should build the independent SMART/storage interpretation engine on top of the richer evidence model. It should distinguish condition, endurance and unavailable evidence, use device/vendor/controller context where justified, and avoid treating SMART attribute IDs as universal semantics.
