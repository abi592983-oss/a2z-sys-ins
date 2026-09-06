# A2Z System Inspector

Windows-first, offline, read-only PC health inspection and printable reporting tool for A2Z Tec Solutions.

## Version 1 capabilities

- Windows, hardware, RAM, graphics, volume and device information
- Physical storage inventory with optional `smartctl` JSON evidence
- Bundled live temperature, load, fan, clock and voltage readings through LibreHardwareMonitorLib
- 30-day summary of important Kernel-Power, BugCheck, WHEA, Disk and NTFS events
- Basic battery and Device Manager condition
- Versioned 0–100 category scores, critical storage cap and N/A handling
- In-app report preview, Windows printing/PDF printing, JSON evidence and text summary
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

LibreHardwareMonitorLib is restored during the build and included automatically in the downloadable application artifact. The technician does not need to download or copy the sensor library. Administrator access may still be required for low-level sensor access.

Full SMART evidence remains optional in this version. Place the official `smartctl.exe` and its required runtime files under a `tools` directory beside the executable to enable it.

Review and comply with third-party licences when redistributing the application. Run the app as administrator when complete sensor and Event Log access is required.

## Safety boundary

System Inspector is an inspection tool. It does not run repair switches, SMART self-tests, stress tests, SFC, DISM repair, CHKDSK repair, cleanup, optimization, driver installation, or Windows updates.

## Important Version 1 limitation

The initial score rules are conservative screening rules and are identified as ruleset `1.0`. They must be validated against known healthy and faulty computers before the scores are used as a definitive service decision.
