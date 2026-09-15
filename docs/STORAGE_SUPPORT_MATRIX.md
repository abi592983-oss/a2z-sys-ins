# A2Z System Inspector — Storage Support Matrix

## Purpose

This document is the normative boundary for storage support. "Supported" means A2Z has an acquisition path and can preserve evidence when the device/controller exposes it; it does not mean every SMART, temperature or endurance field is guaranteed on every device.

## Acquisition order

1. `smartctl` structured JSON is the primary storage provider.
2. Windows `MSStorageDriver_FailurePredictData` / `MSStorageDriver_FailurePredictStatus` is the ATA fallback, matched by unique PNP identity rather than enumeration order.
3. CrystalDiskInfo Standard portable `/CopyExit` is the last-resort evidence provider when the preceding paths produce no usable SMART evidence.
4. If all providers fail, the drive remains present in the report with explicit unavailable evidence; missing SMART is never interpreted as healthy.

## Device-family matrix

| Device family / transport | Acquisition | Typical evidence | Important limitation |
|---|---|---|---|
| SATA / ATA HDD | `smartctl`; Windows ATA fallback | SMART table, SMART status, temperature when exposed, power-on counters, vendor life fields when explicitly reported | Attribute semantics are not universal; vendor-specific life fields may remain unavailable |
| SATA SSD | `smartctl`; Windows ATA fallback; CrystalDiskInfo fallback | SMART table, temperature when exposed, vendor/controller endurance indicators | Endurance interpretation is only used when the field semantics are explicit/validated |
| NVMe SSD | `smartctl`; CrystalDiskInfo fallback where supported | Critical warning, available spare, spare threshold, percentage used, media errors, error log count, temperature and controller temperature when exposed | Driver/controller configuration can limit access; vendor-specific fields remain unavailable unless validated |
| SCSI / SAS | `smartctl` where the Windows path exposes the device | SCSI health/error evidence and device metadata | Coverage is deliberately conservative |
| USB / SAT | `smartctl` or CrystalDiskInfo when the bridge exposes SAT/SMART | ATA SMART through the bridge, temperature/endurance when exposed | USB bridge firmware can hide or transform SMART; unavailable is expected for some devices |
| USB/removable without SMART exposure | Device inventory only | Model/serial/capacity and other Windows-visible metadata | No SMART health conclusion is inferred |
| RAID/controller abstraction | Provider-dependent | Whatever the controller exposes | Do not infer member-drive health from array status alone |

## Evidence semantics

### Condition

Condition answers whether available evidence contains current failure/problem indicators.

- `CRITICAL` — a validated critical indicator is present.
- `ATTENTION` — a validated non-critical error/condition indicator is present.
- `GOOD-NO FLAGGED INDICATOR` — available evidence was measured and no rule-recognized problem indicator was found.
- `UNKNOWN` / `NOT ASSESSED` — insufficient evidence exists to make a condition claim.

### Endurance

Endurance is a device-reported wear/life estimate, not a probability of failure and not the overall health score.

For example, a drive can legitimately report:

> Condition: Good — no flagged indicator
>
> Endurance: 36% remaining

The two values must remain separate.

### Confidence

Confidence reflects evidence completeness/quality. It does not turn unavailable fields into healthy evidence.

## Identity safety

Every storage result must be associated with the WMI physical-drive identity. Where a provider exposes a serial number, A2Z validates it. For CrystalDiskInfo text output, matching is:

1. exact serial match;
2. otherwise unique model match;
3. otherwise unique model + capacity match within a small tolerance;
4. otherwise reject the result rather than risk assigning SMART data to the wrong physical drive.

## CrystalDiskInfo fallback boundary

CrystalDiskInfo is used as an external evidence provider only. A2Z invokes its documented `/CopyExit` mode, reads the resulting `DiskInfo.txt`, extracts the drive identity and SMART rows, and passes the resulting evidence into the existing A2Z storage interpretation pipeline.

A2Z does not copy CrystalDiskInfo's user interface or use its overall health display as the A2Z overall health percentage.

The current Windows artifact packages the Standard portable CrystalDiskInfo 9.9.2 files and verifies the official ZIP SHA-256 before packaging. Manual deployments can point A2Z at another portable copy using `A2Z_CRYSTALDISKINFO_PATH`.

## Temperature and graph policy

A current storage temperature is valid snapshot evidence. It is not a historical series. A2Z must not fabricate a storage temperature graph from a single reading.

Future storage history graphs require real timestamped samples captured by the application.

## Explicit non-claims

A2Z does not claim that:

- every USB bridge exposes SMART;
- every NVMe controller exposes every vendor-specific field;
- a reported endurance percentage predicts failure probability;
- Windows `Status=OK` proves drive health;
- an interface speed mismatch proves a controller fault;
- one provider's inability to read SMART proves the physical drive has no SMART support;
- a focused inspection currently performs a fresh physical re-acquisition;
- a current temperature reading represents historical thermal behavior;
- the current evidence justifies a universal overall health percentage.
