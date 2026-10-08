# A2Z customer A4 report — rollout specification

## Goal
An automatically generated, A4-friendly customer report with numbered component findings, traffic-light colors, understandable explanations, recommendations, and a reproducible ruleset version.

## Implemented in PDF export
- Numbered customer component rows sourced from CustomerHealth.Components.
- Green GOOD, amber ATTENTION/DEGRADED, red CRITICAL, grey UNKNOWN/NOT TESTED.
- Plain-language component titles and explanations.
- Page-aware PDF layout; detailed technical evidence retained on subsequent pages.
- No fabricated numeric overall score.

## Next milestone: evidence-based versioned scores
Do not equate arbitrary weights with hardware health. Keep status and score separate:
1. Define score semantics, eligible evidence, category coverage, and version (e.g. 3.0.0).
2. Report a numeric score only for a calibrated, measurable dimension (e.g. battery design-capacity retention, vendor-validated SSD endurance).
3. Overall score remains 'Not rated' until validated against representative physical machines and known failures.
4. CRITICAL evidence overrides a reassuring-looking score; UNKNOWN must never be scored as GOOD.
5. Record assessment ruleset, application version, evidence timestamp and missing measurements on the customer report.
6. Maintain regression fixtures per ruleset and independent physical calibration comparisons.
7. Add automatic version-aware rule selection only after old/new rules are tested, documented and pinned to report history.

## Field acceptance checks
- Print preview and exported PDF on A4, including 8+ components and long explanations.
- Healthy, attention, critical, and unavailable fixtures.
- No wrong-drive SMART association, no invented percentages, no missing critical findings.
- Windows 11 x64 physical runs including NVMe, SATA, battery, Event Log and PawnIO fallback.
- Confirm no unexpected system changes and customer-safe privacy/redaction.

## Scope
The PDF change does not yet implement a circular gauge, universal numeric score, Windows 11 physical validation, or automatic application updating.
