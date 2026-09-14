# A2Z System Inspector — Pass 10 Real-Machine Calibration Protocol

Last reviewed: 2026-09-14
Status: Protocol implemented; physical-machine execution remains pending until representative hardware is available.

## Purpose

Synthetic tests prove deterministic behavior for known evidence. They cannot prove that Windows, firmware, controllers, USB bridges and real devices expose evidence in the ways the acquisition layer expects. Pass 10 is therefore a controlled calibration exercise, not a new scoring formula.

No overall health percentage is to be introduced merely because this protocol exists.

## Required machine set

The calibration set should include as many of these as practical:

1. Known-good SATA HDD.
2. Known-good SATA SSD.
3. Known-good NVMe SSD.
4. SATA/SAT device behind a USB enclosure or adapter.
5. USB/removable storage with SMART exposed.
6. USB/removable storage with SMART hidden by its bridge.
7. SCSI/SAS device if available.
8. A drive with a deliberately known SMART warning/failure state, only where safe and ethically available for testing.
9. A system with a known thermal issue or reproducible high-temperature condition, without intentionally damaging hardware.
10. A system with known Windows storage/WHEA/device-event evidence.
11. A system with low free disk space.
12. A system with known battery wear, if a battery-equipped test system is available.

A single machine can cover multiple rows. Known-good and known-fault labels must come from independent evidence, not from the application's own result.

## Capture procedure

For every test machine:

1. Record machine ID, Windows version/build, architecture, motherboard/platform, storage controller mode and test date.
2. Record the physical drive model, serial, capacity, connection path and whether a bridge/enclosure is involved.
3. Run a normal full-system inspection as administrator.
4. Preserve the generated diagnostic log and JSON evidence.
5. Do not modify SMART data, repair the disk, run destructive tests or alter the machine merely to create a failure state.
6. Open the individual storage inspector and select each physical target.
7. Confirm that the selected target matches the physical serial/model/capacity.
8. If CPU stress testing is safe and explicitly authorized, capture the normal 60-second staged sample and preserve the actual temperature samples.
9. Record independent ground truth from vendor tools, firmware, Windows Event Viewer/Device Manager, or another trusted diagnostic source as applicable.
10. Compare A2Z output with ground truth and record discrepancies verbatim.

## Storage observations to record

For every drive, capture:

- inventory detection;
- model, serial and capacity;
- device type and transport;
- smartctl command/device path selected;
- SMART/NVMe/SCSI evidence availability;
- SMART overall status when present;
- relevant SMART attributes and their names;
- NVMe critical warning, spare, threshold, percentage used, media errors and error-log count when present;
- current temperature and controller temperature when present;
- endurance/life field and its interpretation when present;
- serial validation result;
- link current/maximum values when exposed;
- unavailable fields and failure reasons;
- condition, condition confidence, endurance and endurance confidence;
- individual-inspector result and identity match behavior.

## Cross-category observations

Record independently whether the machine has evidence of:

- CPU/thermal stress or thermal abort;
- memory/resource pressure;
- low volume free space;
- battery wear;
- Disk/NTFS/WHEA/bug-check/shutdown events;
- Windows device/PnP problems;
- other diagnostic categories surfaced by the report.

The expected property is **category isolation**: evidence in one category must not manufacture a fault in another category.

## Calibration worksheet

| Test ID | Ground truth | A2Z result | Match? | Evidence completeness | Discrepancy / action |
|---|---|---|---|---|---|
| RM-001 | Known-good SATA HDD | — | — | — | — |
| RM-002 | Known-good SATA SSD | — | — | — | — |
| RM-003 | Known-good NVMe SSD | — | — | — | — |
| RM-004 | USB/SAT with SMART | — | — | — | — |
| RM-005 | USB bridge hides SMART | — | — | — | — |
| RM-006 | SCSI/SAS if available | — | — | — | — |
| RM-007 | Known SMART warning/failure | — | — | — | — |
| RM-008 | Thermal observation | — | — | — | — |
| RM-009 | Windows storage/WHEA evidence | — | — | — | — |
| RM-010 | Low free space | — | — | — | — |
| RM-011 | Battery wear | — | — | — | — |
| RM-012 | Individual-drive identity | — | — | — | — |

Rows not applicable to the available lab should be marked `N/A`, never silently omitted.

## Pass/fail rules

A calibration observation passes when:

- the correct physical device is identified;
- evidence is classified according to the support matrix;
- validated fields have the expected semantics;
- unavailable fields remain unavailable;
- condition and endurance remain separate;
- confidence reflects evidence completeness;
- no unrelated diagnostic category is fabricated;
- no historical graph is produced from point-in-time data;
- the report does not overstate what SMART or a short sample can prove.

A mismatch does not automatically mean the hardware is faulty. First classify the mismatch as acquisition, identity, semantic interpretation, assessment, presentation, or ground-truth uncertainty.

## Scoring calibration boundary

Do not tune numeric weights from one or two machines. Before an overall percentage can be introduced, the test set needs enough known-good and known-fault observations to establish:

- severity ordering;
- category independence;
- evidence-coverage behavior;
- repeatability across device families;
- false-positive and false-negative patterns;
- treatment of unknown/unavailable evidence;
- whether the percentage is useful enough to justify its apparent precision.

Until those criteria are satisfied, `OverallScore = null` remains the correct behavior.

## Regression handling

Every real-machine discrepancy must be added to `docs/THE_PROBLEMS_WE_FOUND_IN_DEVELOPMENT.md` with its discovery date, impact, status, fix date when applicable, validation and whether it has ever been fixed. Do not rewrite history to make a regression disappear.

When a discrepancy is reproducible, add a synthetic regression test or fixture after the root cause is understood. The synthetic fixture protects the fix; the real-machine observation remains the evidence that exposed the problem.

## Completion criterion

Pass 10 is **not** considered physically complete until representative machines have been inspected and the captured results are reviewed. The repository can contain this protocol and the support matrix now, but no claim of real-machine validation is permitted until actual hardware evidence has been collected.
