# A2Z System Inspector — Real-Machine Calibration Protocol

## Purpose

Pass 10 validates the complete inspection pipeline against physical Windows machines. Synthetic fixtures prove deterministic code behavior; they cannot prove that real storage controllers, firmware, drivers, USB bridges and vendor implementations expose evidence in the expected way.

The first physical HP desktop run has already exposed one integration defect: the tested application package did not contain `smartctl.exe`. Both WMI physical-drive identities were correct, but SMART acquisition was skipped. This finding is recorded as DEV-013. The corrected artifact now packages official smartmontools and adds a headless CrystalDiskInfo `/CopyExit` last-resort provider.

## Ground truth rule

A machine is not considered calibrated merely because A2Z produces a report. For each calibration case, capture an independent reference report from an established storage/diagnostic provider and compare the raw evidence, identity, temperature, condition indicators and endurance indicators with A2Z's report.

## Representative machine classes

At minimum, execute cases covering:

1. SATA HDD with healthy SMART data.
2. SATA HDD with known pending/reallocated/uncorrectable evidence.
3. SATA SSD with a vendor-specific life field.
4. NVMe SSD with standard health log fields.
5. USB/SAT storage where SMART is exposed.
6. USB storage where SMART is intentionally unavailable.
7. A system where Windows ATA SMART fallback is the only available path.
8. A system with multiple drives having similar models, validating identity matching.
9. A system with a known thermal/device-status issue outside storage, validating cross-category isolation.

## Controlled procedure

For each machine:

1. Record manufacturer, model, OS build, storage-controller mode and administrator state.
2. Record every physical drive's model, serial, capacity and physical-drive identity independently.
3. Capture an independent reference report before running A2Z.
4. Run A2Z without repairs, cleanup, updates or SMART self-tests.
5. Preserve the A2Z diagnostic log and JSON export.
6. Compare provider source, drive identity, SMART attributes, temperatures, condition indicators and endurance values.
7. Record every discrepancy, including missing fields and identity mismatches.
8. If a discrepancy is reproducible, add a synthetic fixture/regression test where practical.
9. Repeat after the correction on the same machine.

## Storage recovery-provider policy

A2Z uses the following order:

1. `smartctl` structured JSON.
2. Windows `MSStorageDriver_FailurePredictData` / `MSStorageDriver_FailurePredictStatus` fallback where uniquely matched by PNP identity.
3. Headless CrystalDiskInfo Standard `/CopyExit` as a last-resort evidence provider when the preceding paths produce no usable SMART evidence.

The CrystalDiskInfo fallback is deliberately narrow. It exports evidence to A2Z; it does not replace A2Z's condition/endurance semantics or introduce CrystalDiskInfo's UI/health score as A2Z's overall score. The extracted text must be matched to the WMI drive by serial first, then model/capacity only when the match is unique.

## Pass/fail rules

A case passes storage acquisition when every independently visible drive is either:

- successfully measured and safely identity-matched; or
- explicitly reported as unavailable with a concrete reason.

A case fails if:

- a drive is silently omitted;
- a result is assigned to the wrong physical drive;
- missing SMART is presented as healthy;
- an endurance percentage is treated as overall health;
- provider output is available but the report incorrectly marks the drive `Not assessed`; or
- a packaging/build regression removes a required evidence provider.

A calibration case does not require every field to be available. Unsupported fields remain unavailable and are recorded as such.

## First-machine calibration record

The first machine was a Hewlett-Packard 23-d250ee desktop containing:

- Seagate `ST1000DM003-1CH162`, 1 TB HDD;
- `HS-SSD-WAVE(S) 256G`, 256 GB SSD.

WMI identified both drives correctly. LibreHardwareMonitor also exposed per-drive temperatures. The tested application package lacked `smartctl.exe`, so both drives were left `Not assessed` for SMART. Independent storage evidence on the same run showed useful SMART attributes and an SSD life value, proving that the initial A2Z result was an acquisition/package failure rather than proof that the drives lacked usable health data.

The corrected package must be rerun on this same machine before the case can be marked successful.

## Numeric scoring boundary

Pass 10 must not create an overall percentage merely because physical machines are now being tested. Numeric scoring remains withheld until evidence from representative categories is sufficient to justify a calibrated model.

## Completion criterion

Pass 10 is complete only when representative physical systems have been executed, discrepancies have been resolved or explicitly accepted as limitations, reproducible defects have regression coverage, and the support matrix accurately reflects observed hardware behavior.
