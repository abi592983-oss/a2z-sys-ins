# A2Z System Inspector

Windows-first, offline, read-only PC health inspection and printable reporting tool for A2Z Tec Solutions.

## Version 2 capabilities

- Windows, hardware, RAM, graphics, volume and device information
- Physical storage inventory with bundled `smartctl` JSON evidence and a Windows ATA SMART fallback
- Bundled live temperature, load, fan, clock and voltage readings through LibreHardwareMonitorLib
- 30-day evidence for unexpected/initiated shutdowns, bug checks, crash-dump failures, WHEA, Disk, NTFS and repeated application crashes
- Basic battery and Device Manager condition
- Evidence-based assessments without an invented overall health percentage
- In-app report preview, Windows printing/PDF printing, JSON evidence, text summary and downloadable diagnostic log
- Optional 60-second staged CPU load test with live temperature monitoring, manual cancellation and a 90°C safety abort
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

## Diagnostic adapters

LibreHardwareMonitorLib is restored during the build and included automatically in the downloadable application artifact. The official smartmontools 7.5 Windows build is also bundled, including its corresponding source archive. The technician does not need to download or copy either collector.

The application requests administrator access at launch because low-level temperature, SMART and Event Log access commonly requires elevation. It does not disable Windows security protections.

For storage, the primary method is structured `smartctl` JSON. If that cannot read a drive, the application attempts the Windows `MSStorageDriver_FailurePredictData` and `MSStorageDriver_FailurePredictStatus` providers, matched by PNP ID rather than list order. Unsupported USB bridges, NVMe drivers and vendor-specific life attributes are explicitly recorded as `Unavailable`/`Cannot measure`; Windows `Status=OK` is never converted into a health percentage.

The diagnostic log is a timestamped replay of System Inspector collector requests, responses, errors, fallbacks and assessment decisions. It may contain serial numbers, PNP identifiers and raw event XML, so review it before sharing.

Review and comply with third-party licences when redistributing the application. Run the app as administrator when complete sensor and Event Log access is required.

## Safety boundary

System Inspector is an inspection tool. It does not run repair switches, SMART self-tests, SFC, DISM repair, CHKDSK repair, cleanup, optimization, driver installation, or Windows updates. The CPU stress test is an explicit, technician-started exception: it never runs automatically, lasts at most 60 seconds, requires an actual CPU temperature sensor and stops at 90°C or when monitoring is lost.

## Important limitation

The report is a read-only screening snapshot. SMART, Windows events and a short temperature sample cannot guarantee future reliability or prove a single root cause. Ruleset `2.0-evidence-only` withholds an overall percentage and reports exactly which measurements were available.
