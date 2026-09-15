# Pass 13 — Synthetic Inspection Lab

## Purpose

The Synthetic Inspection Lab is a deterministic, randomized test environment for the A2Z System Inspector. It exercises the post-acquisition diagnostic pipeline without requiring a physical PC, real storage devices, Windows provider access, or repair operations.

The historical Inspector session is treated as the reference evidence surface. The lab therefore generates system identity, CPU, stress telemetry, RAM modules, GPU, storage identity/health/endurance, volumes, Windows events, device status, battery state, Windows integrity results, provider attempts, provenance, and controlled faults.

The lab is a **regression and interpretation test surface**. It does not replace real-machine validation of the actual acquisition providers.

## Horizontal coverage

Each scenario passes through the same chain used by the application:

`synthetic evidence -> normalization -> storage interpretation -> storage assessment -> scoring -> advanced assessment -> summary -> customer health`

The scenarios deliberately cross category boundaries so a fault in one evidence family can be checked for correct propagation while unrelated categories remain populated.

## Scenario families

- healthy-desktop
- aging-ssd
- failing-hdd
- healthy-nvme
- thermal-problem
- high-memory-use
- windows-integrity-problem
- missing-evidence
- conflicting-providers
- sparse-machine
- provider-fallback
- mixed-faults

The `aging-ssd` fixture includes the historical 36% remaining-life case. The expected invariant is **36% remaining / 64% endurance used**, never 0% remaining or 100% used.

## Required invariants

1. Validated SSD life-remaining values preserve their semantics.
2. Missing evidence never becomes numeric zero.
3. SMART threshold pass remains a condition statement, not a health percentage.
4. Endurance alone does not make storage condition critical.
5. High RAM utilization does not claim physical RAM failure.
6. A critical component propagates to overall customer status.
7. Sparse or unknown evidence cannot be declared healthy merely because no fault was found.
8. Provider attempts and fallback responses remain visible as evidence/provenance.
9. Different providers may supply different fields.
10. Raw evidence remains available even when it is excluded from scoring.

## Running

From a Windows development environment with the .NET Framework 4.8 build toolchain:

```text
msbuild tests\SyntheticInspectionLab\SyntheticInspectionLab.csproj /t:Build /p:Configuration=Release
A2ZSysIns.SyntheticInspectionLab.exe 20260915 5
```

The first argument is the random seed. The second is the number of generated machines per scenario. Because the generator is seeded, a failing case can be reproduced exactly.

The runner returns process exit code `0` only when every generated machine passes its invariants.

## Pass 13 real-machine validation

Pass 13 is also being exercised on physical computers. These runs are separate from the synthetic lab and are the evidence used to validate actual Windows/provider behavior.

Real runs have demonstrated:

- WMI and physical-storage discovery on actual machines
- smartctl provider/path fallback, including a working `/dev/sda` ATA path after a direct Windows physical-drive path failed
- conservative handling of a device with unavailable SMART evidence (`UNKNOWN` rather than healthy)
- real PnP/device-problem detection
- real customer-health report generation
- CPU-stress safety gating, including both refusal and completed safe-test behavior depending on telemetry
- real storage findings, including a CRITICAL HDD result when pending sectors were observed

These observations are valuable physical evidence, but continued multi-machine validation is required before Pass 13 is declared fully closed.

## What this does not replace

The lab does not replace real-world provider validation. Physical validation remains required for WMI, smartctl, LibreHardwareMonitor, CrystalDiskInfo, Windows Storage APIs, device-specific transports, and actual Windows event/integrity behavior.

Synthetic coverage should therefore be treated as **regression PASS**, while physical-machine behavior is tracked separately as **physical validation PASS/FIX/PENDING**.
