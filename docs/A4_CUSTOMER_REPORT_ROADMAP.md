# A2Z customer A4 report - rollout specification

## Goal

An automatically generated, A4-friendly customer report with numbered component findings, traffic-light colors, understandable explanations, recommendations, and a reproducible ruleset version.

## Implemented in PDF export

- Customer findings and technical evidence are separated into readable sections.
- Plain-language assessment, limitations, priority actions, and evidence-backed component results are included.
- A4 pagination, footers, and technical evidence are validated using deterministic fixture data.
- No fabricated numeric overall score is presented.

## Evidence-based score guardrails

1. Report a numeric score only for a calibrated, measurable dimension, such as battery design-capacity retention or vendor-validated SSD endurance.
2. Keep a status and a numeric score separate; the overall PC score remains not rated until validated on representative physical machines and known failures.
3. Critical evidence overrides a reassuring-looking measurement, and `UNKNOWN` must never be scored as `GOOD`.
4. Record the assessment ruleset, application version, evidence timestamp, and unavailable measurements in the report.
5. Maintain regression fixtures and physical calibration comparisons before adding a versioned overall score.

## Field acceptance checks

- Review healthy, attention, critical, and unavailable fixture reports on A4.
- Confirm long component descriptions wrap without overlapping columns.
- Verify SMART association against the actual selected device.
- Perform Windows 11 x64 physical runs covering NVMe, SATA, battery, Event Log, and sensor fallback conditions.
- Confirm no unexpected system changes and no invented health percentages.
