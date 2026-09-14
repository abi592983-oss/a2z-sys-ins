# A2Z System Inspector — Storage Support Matrix

Last reviewed: 2026-09-14
Ruleset: `2.0-evidence-only`

This document defines what the storage subsystem is allowed to claim. A detected device is not automatically a fully measurable device. Unsupported or missing evidence is reported as unavailable rather than converted into a healthy result.

## Device-family matrix

| Device family | Inventory | Primary evidence | Temperature | Endurance/life | Current confidence boundary |
|---|---|---|---|---|---|
| SATA/ATA HDD | Supported | ATA SMART through smartctl | When exposed | Usually unavailable unless a validated life field exists | Evidence-dependent |
| SATA/ATA SSD | Supported | ATA SMART through smartctl | When exposed | Vendor/controller-dependent; only validated fields | Evidence-dependent |
| SATA device behind SAT | Supported when bridge exposes evidence | ATA SMART through SAT/smartctl | Bridge-dependent | Bridge/device-dependent | Conservative |
| NVMe SSD | Supported | NVMe health data through smartctl | When exposed | Standard Percentage Used / validated fields | Evidence-dependent |
| SCSI/SAS | Inventory supported; health evidence device-dependent | SCSI health/log evidence when exposed | When exposed | Device-dependent | Conservative |
| USB/removable storage | Inventory supported | SMART only when the USB bridge exposes usable evidence | Bridge-dependent | Bridge/device-dependent | Conservative |
| Unsupported/opaque bridge | Inventory may still succeed | No trustworthy SMART/health evidence | Usually unavailable | Unavailable | Unknown / unavailable |

"Supported" means the application has an acquisition/interpretation path. It does not mean every field is guaranteed on every controller, bridge, firmware or driver combination.

## Evidence hierarchy

1. Structured `smartctl` JSON is the primary storage evidence source.
2. Windows `MSStorageDriver_FailurePredictData` / `MSStorageDriver_FailurePredictStatus` is a fallback for supported ATA devices when primary evidence cannot be obtained.
3. Raw evidence is preserved where available, including source, device type, transport and identity information.
4. Vendor-specific or unknown SMART attributes remain evidence but are not assigned universal meanings from attribute ID alone.

## Condition vs endurance

**Condition** answers whether available evidence contains a current storage problem indicator.

**Endurance/life** answers whether the device reports a wear/lifetime estimate that can be interpreted with validated semantics.

They are independent dimensions. For example, a drive can have a good current condition assessment while reporting low remaining endurance. Conversely, an SSD can have no usable endurance field while still having no flagged condition indicator.

An endurance percentage is **not** a probability of failure and is **not** the application's overall health percentage.

## Confidence

Confidence describes evidence completeness and semantic trust, not certainty about future failure.

- **High:** relevant health evidence is present and its semantics are validated for the device/field.
- **Moderate:** useful evidence is present but important fields are missing or the source is partial.
- **Low:** only limited/partial evidence is available, or important identity/health fields cannot be validated.
- **Unknown:** no trustworthy condition evidence is available.

Missing data reduces coverage/confidence. It must never be silently treated as a healthy measurement.

## SMART semantic rules

The interpreter validates ATA semantics by attribute meaning/name rather than assuming that an ID has one universal meaning across every vendor/controller.

Known classes include reallocated sectors, pending sectors, offline uncorrectable sectors and interface CRC errors when the attribute semantics are validated. NVMe standard health fields include critical warning, available spare/threshold, percentage used, media/data integrity errors and error-log information when exposed.

Vendor-specific endurance indicators are accepted only when their semantics can be established from the evidence. Unknown IDs remain unknown.

## Interface/link reporting

Current and maximum interface speed are reported when structured evidence exposes them. The application deliberately does not infer motherboard PCIe generation or controller capability from incomplete evidence. Link observations are informational unless stronger evidence exists.

## Individual storage inspector

The focused inspector can classify and select HDD/SSD/NVMe/SCSI/SAS/USB-removable targets from the completed acquisition. Identity matching prefers serial number and falls back to model plus capacity.

Current limitation: the focused inspector reinterprets evidence already acquired by the full-system inspection. It is not a separate live physical-drive acquisition path.

## Temperature and graph policy

A current storage temperature is a point-in-time observation. It is not a history.

The application must only render a storage temperature graph when timestamped storage-temperature samples have actually been captured. It must never construct historical points from a single current reading or from assumed values.

CPU stress samples contain actual elapsed-time temperature observations and may be graphed. A future storage capture series can be added without changing the individual-inspection result contract.

## Explicit non-claims

The storage subsystem does not claim that:

- SMART can predict every future drive failure;
- Windows `Status=OK` proves storage health;
- missing SMART proves a drive is healthy;
- every USB bridge exposes SMART;
- every SSD exposes a universal endurance percentage;
- an endurance percentage is overall health;
- interface speed alone proves controller capability;
- a focused inspection is a fresh live scan;
- a short temperature sample proves long-term thermal behavior.

## Pass 10 acceptance boundary

This matrix is the reference against which real-machine observations are compared. Any real device that violates an assumption must produce a new development-history entry and a targeted correction rather than a silent semantic expansion.
