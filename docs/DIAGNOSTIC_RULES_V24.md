# A2Z System Inspector — v2.4 Correlated Diagnostic Rules

## Design principle

The Inspector does not convert one sensor value or one Windows event into a hardware diagnosis. The decision path is:

`evidence -> recurrence/correlation -> confidence -> finding -> action`

Missing data stays `Unavailable` / `Not measured`. It never becomes healthy or zero.

## WHEA hardware errors

Primary source: Windows System event log, provider `Microsoft-Windows-WHEA-Logger`, warning/error/critical levels, 30-day window.

- Group by Event ID plus component/error-source/device identifiers when exposed.
- One or two events remain informational unless an existing core critical rule applies.
- More than two occurrences of the same signature becomes `Attention`.
- Event 18 remains handled by the existing core rule set.
- A WHEA finding is evidence of a hardware error record, not automatic proof that a named physical component must be replaced.

Fallback: none beyond the existing event-log reader/wevtutil strategy. If the provider cannot be read, record `Unavailable`.

## Storage link speed

Primary source: structured `smartctl -a -j` fields `interface_speed.current.string` and `interface_speed.max.string`.

- Equal negotiated and device maximum speed may be reported as good evidence.
- A lower negotiated speed is informational/supporting evidence only.
- Do not call it a cable/controller failure unless platform/controller capability is independently known or corroborating storage/controller errors exist.
- If either field is absent, record `Not measured`.

The current implementation does not guess PCIe/NVMe lane capability when the device does not expose comparable structured fields.

## Storage-stack errors

Sources include warning/error/critical events from `storahci`, `stornvme`, common Intel storage providers, `Disk`, `Ntfs`, and `Microsoft-Windows-Ntfs`.

- Core Disk Event 7 and NTFS Event 55 remain handled by the core engine and are not duplicated.
- Other provider events are grouped by provider, Event ID and first useful event-data fields.
- More than two matching occurrences becomes `Attention`.
- A recurring storage-stack event is not automatically a failed drive: possible paths include media, controller, cable, enclosure, driver and file system.

## Plug and Play instability

Sources: warning/error/critical events from `Microsoft-Windows-Kernel-PnP` and `Microsoft-Windows-UserPnp`.

- Group by provider, Event ID and device instance identifier when exposed.
- More than two matching occurrences becomes `Attention`.
- Report as device-path instability evidence, not automatic proof of a failed physical device.

## CPU loaded clock behavior

The optional 60-second staged CPU test continues to be technician-started only and retains the 90 C thermal abort.

Each sample attempts to record:
- CPU temperature
- observed CPU load
- average core clock
- maximum core clock
- fan RPM when exposed

Clock-collapse rule is intentionally conservative:
- use only 100% target-stage samples where observed CPU load is at least 80%;
- require a Windows-reported reference maximum clock;
- require at least five samples where average core clock is below 50% of that reference before creating an `Attention` finding;
- if temperature is also at least 85 C, thermal limiting is described as plausible, not proven;
- if load or clock sensors are unavailable, record `Not measured` for throttling evidence.

This is a screening rule, not a replacement for vendor-specific throttle flags or a second calibration tool.

## Fan response

A 0 RPM reading alone is never a failed-fan diagnosis.

A fan-response finding requires all three in the same captured evidence set:
- fan sensor reports 0 RPM;
- CPU temperature reaches at least 80 C;
- CPU load reaches at least 70%.

If no fan sensor exists, fan health is `Not measured`.

## TPM and BitLocker

TPM primary source: `Win32_Tpm` in `Root\CIMV2\Security\MicrosoftTpm`.

BitLocker primary source: `manage-bde -status`.

Both are configuration/security information only and are excluded from hardware-health severity.

If unsupported or unavailable, record `Unavailable`; this must not reduce hardware health.

## CMOS/RTC and voltage rails

Not promoted to diagnostic findings in v2.4.

Reason:
- clock drift is confounded by Windows time synchronization, manual changes, firmware behavior and dual-boot RTC conventions;
- motherboard voltage sensors are frequently unlabeled, scaled differently, unsupported or not sufficiently trustworthy for a generic cross-platform threshold.

These may be added later only with model-aware evidence or validated calibration rules.

## Validation requirement

Before declaring the v2.4 thresholds reliable, physically compare Inspector results on:
1. a known-good machine;
2. a machine with a known storage fault or degraded interface path;
3. a machine with a reproducible thermal/throttling issue if available;
4. a machine with known recurring WHEA/PnP evidence if available.

Calibration should compare the same machine, same state and same time window against established technician tools without copying proprietary implementation details.
