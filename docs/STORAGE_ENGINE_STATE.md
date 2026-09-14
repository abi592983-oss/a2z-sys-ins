# A2Z System Inspector — Storage Engine State

Last updated: 2026-09-14
Phase: Pass 0 — Baseline / Inventory
Status: Pass 0 complete

## Scope

The refactor will inspect the complete diagnostic system, while the first implementation focus is storage-device evidence and interpretation. Health percentages and scoring must ultimately be calibrated across all diagnostic problem categories, not storage alone.

The existing non-storage sensor collection is considered working enough to leave unchanged during the storage-engine work unless a dependency or calibration issue requires a targeted change.

## Pass sequence

1. Pass 0 — Baseline / Inventory
2. Pass 1 — Storage evidence data model
3. Pass 2 — Storage evidence acquisition
4. Pass 3 — SMART / storage interpretation engine
5. Pass 4 — Health, endurance, confidence and percentage/scoring model
6. Pass 5 — Diagnostic pipeline cleanup and cross-category scoring calibration
7. Pass 6 — Individual storage inspection backend architecture
8. Pass 7 — UI storage inspector and useful diagnostic graphs
9. Pass 8 — Automated/synthetic validation fixtures
10. Pass 9 — Documentation and support matrix
11. Pass 10 — Real-machine calibration against known-good and known-fault systems

## Pass 0 findings

- The full inspection pipeline already collects system information, resource usage, physical storage/SMART evidence, Windows events, advanced correlated evidence and hardware sensors.
- Storage collection currently relies primarily on smartctl JSON with a Windows ATA SMART fallback.
- Storage interpretation currently has limited vendor-specific life/endurance recognition and a shallow attribute model.
- Storage scoring currently uses raw ATA IDs before a later normalization step. The pipeline therefore has a score-then-correct shape that should be removed during the refactor.
- SMART attribute IDs cannot be treated as universal semantics; attribute name/controller/device context must be retained.
- NVMe endurance/percentage-used information must remain distinct from overall device health/condition.
- Missing or unsupported storage evidence must remain Unavailable/Not measured and must never become a healthy zero or an inferred failure.
- Storage link-speed differences are supporting evidence, not automatic hardware failure.
- Existing event correlation is conservative and should be retained while scoring is recalibrated.
- The current README explicitly describes the application as withholding an invented overall health percentage; future percentage work must be evidence-based and calibrated rather than a cosmetic conversion of findings.

## UI direction

The future UI should support a dedicated storage inspection experience after the backend is stable. Useful time-series graphs should be added where the evidence supports them, including storage temperature and CPU temperature. Graphs must represent actual captured measurements and must not invent historical data.

## Future individual-drive inspection

The front window should eventually offer a separate storage inspection mode capable of selecting an individual HDD, SSD, NVMe device or supported removable/USB storage. This is a future UI/backend milestone, not a reason to destabilize the current full-system inspection during early passes.

## Required development record

All discovered development problems are to be recorded separately in `docs/THE_PROBLEMS_WE_FOUND_IN_DEVELOPMENT.md`, including discovery date, status, fix date when applicable, and whether the issue has ever been fixed. This record is historical and must not be silently rewritten when later fixes occur.

## Current files reviewed in Pass 0

- `README.md`
- `src/A2ZSysIns/MainWindow.xaml.cs`
- `docs/DIAGNOSTIC_RULES_V24.md`

The storage implementation files and scoring/assessment files identified for detailed work in later passes include the EvidenceEngine storage path, `Models.cs`, `Scoring.cs`, `SmartInterpretation.cs`, `AdvancedDiagnostics.cs`, and `AdvancedAssessment.cs`.

## Next pass

Pass 1 should redesign the storage evidence model before changing interpretation or scoring rules. No storage scoring rule should be added until the model can preserve enough raw evidence to explain the decision.