# A2Z System Inspector

Windows-first, offline, read-only PC health inspection and printable reporting tool for A2Z Tec Solutions.

## Version 2 capabilities

- Windows, hardware, RAM, graphics, volume and device information
- Physical storage inventory with bundled `smartctl` JSON evidence, a Windows ATA SMART fallback and a headless CrystalDiskInfo last-resort fallback
- Bundled live temperature, load, fan, clock and voltage readings through LibreHardwareMonitorLib
- 30-day evidence for unexpected/initiated shutdowns, bug checks, crash-dump failures, WHEA, Disk, NTFS and repeated application crashes
- Basic battery and Device Manager condition
- Evidence-based assessments without an invented overall health percentage
- In-app report preview, Windows printing/PDF printing, JSON evidence, text summary and downloadable diagnostic log
- Optional 60-second staged CPU load test with live temperature monitoring, manual cancellation and a 90°C safety abort
- Individual storage inspector for focused review of acquired physical-drive evidence
- No ERP connection, cloud requirement, repair, cleanup, update or system modification

## Supported systems

- Windows 7 SP1 x64 with .NET Framework 4.8 installed
- Windows 10 x64
- Windows 11 x64

Windows 7 itself is no longer supported by Microsoft. Compatibility here means the application targets a runtime that can run on Windows 7 SP1; testing on representative machines is still required.

## Build

1. Install Visual Studio 2022 with **.NET desktop development** and the **.NET Framework 4.8 Developer Pack**.
2. Open `A2ZSysIns.sln`.
3. Select `Release | x64` and build.
4. Output is under `src/A2ZSysIns/bin/x64/Release/net48/` (the exact SDK output path can vary by Visual Studio version).

Or from a Visual Studio Developer PowerShell:

```powershell
msbuild A2ZSysIns.sln /restore /p:Configuration=Release /p:Platform=x64
```

The official Windows artifact workflow additionally downloads and verifies the external storage tools before packaging them. A manually built folder must either use that packaged artifact or provide the portable CrystalDiskInfo folder through `A2Z_CRYSTALDISKINFO_PATH` for the last-resort fallback.

## Diagnostic adapters

LibreHardwareMonitorLib is restored during the build and included automatically in the downloadable application artifact. The official smartmontools 7.5 Windows build and CrystalDiskInfo Standard portable package are also bundled by the Windows artifact workflow, including the corresponding notices/source where applicable.

The application requests administrator access at launch because low-level temperature, SMART and Event Log access commonly requires elevation. It does not disable Windows security protections.

For storage, the primary method is structured `smartctl` JSON. If that cannot read a drive, the application attempts the Windows `MSStorageDriver_FailurePredictData` and `MSStorageDriver_FailurePredictStatus` providers, matched by PNP ID rather than list order. If usable SMART evidence is still absent, the application can invoke CrystalDiskInfo headlessly with `/CopyExit`, parse its text export, safely match the drive by serial/model/capacity, and feed the extracted SMART attributes into the existing A2Z interpretation pipeline. CrystalDiskInfo is an evidence provider only; A2Z does not import its UI or replace its own condition/endurance semantics with a copied scoring engine.

Unsupported USB bridges, unavailable NVMe/vendor-specific fields and unknown life attributes are explicitly recorded as unavailable. Windows `Status=OK` is never converted into a health percentage.

The storage support boundary, evidence hierarchy, condition/endurance/confidence semantics and explicit non-claims are documented in `docs/STORAGE_SUPPORT_MATRIX.md`. The focused storage inspector currently reinterprets evidence captured by the completed full-system inspection; it does not claim a fresh live re-acquisition.

The diagnostic log is a timestamped replay of System Inspector collector requests, responses, errors, fallbacks and assessment decisions. It may contain serial numbers, PNP identifiers and raw event XML, so review it before sharing.

Review and comply with third-party licences when redistributing the application. Run the app as administrator when complete sensor and Event Log access is required.

## Safety boundary

System Inspector is an inspection tool. It does not run repair switches, SMART self-tests, SFC, DISM repair, CHKDSK repair, cleanup, optimization, driver installation, or Windows updates. The CPU stress test is an explicit, technician-started exception: it never runs automatically, lasts at most 60 seconds, requires an actual CPU temperature sensor and stops at 90°C or when monitoring is lost.

## Validation and calibration

Pass 8 synthetic validation is merged into `main` and protects the core evidence/assessment behavior. Pass 9 documents the supported storage boundary in `docs/STORAGE_SUPPORT_MATRIX.md`.

Pass 10 is prepared by `docs/REAL_MACHINE_CALIBRATION.md`. That protocol requires representative physical machines, independent ground truth and preserved logs/JSON. The first physical HP desktop run exposed a real packaging gap: the tested application package did not contain `smartctl.exe`, so both physical drives were left unassessed by SMART. This is now recorded as DEV-013 and the corrected artifact pipeline includes both smartmontools and a CrystalDiskInfo `/CopyExit` recovery path. The repository must not claim final calibration until the corrected package has been rerun on representative machines.

Reproducible discrepancies are converted into regression fixtures and recorded in `docs/THE_PROBLEMS_WE_FOUND_IN_DEVELOPMENT.md`.

## Important limitation

The report is a read-only screening snapshot. SMART, Windows events and a short temperature sample cannot guarantee future reliability or prove a single root cause. Ruleset `2.0-evidence-only` withholds an overall percentage until cross-category evidence is calibrated and the resulting model is shown to be useful rather than falsely precise.
