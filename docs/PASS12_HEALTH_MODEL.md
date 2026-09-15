# Pass 12 — Diagnostic Health Model & Final Customer Report

Pass 12 is the internal development label for the customer-facing diagnostic layer.

## Goals

- Convert collected evidence into plain-language component conclusions.
- Keep condition, endurance/wear, usage, configuration, and test coverage separate.
- Never turn missing health data into `0%` or treat SMART threshold pass as a health percentage.
- Report Windows system-file, component-store, and file-system integrity separately from hardware health.
- Produce actionable recommendations for customers while retaining technical evidence for technicians.
- Use `GOOD`, `ATTENTION`, `CRITICAL`, `UNKNOWN`, and `NOT TESTED` states rather than a synthetic overall percentage.

## Storage semantics

Validated life-remaining indicators represent endurance/wear, not failure probability. For validated ATA life attributes such as `SSD_Life_Left`, the raw 0–100 value is treated as percent remaining. Endurance used is derived as `100 - remaining`. Overall SMART condition remains a separate decision.

## Windows integrity

The inspection records non-repairing checks using SFC verification, DISM component-store check, and an online CHKDSK scan of the system volume when available. Raw output is retained. Counts are reported only when Windows itself exposes a reliable count; the Inspector does not invent a number of corrupted files or sectors.

## Customer report

The report leads with an overall condition, component summaries, and recommended actions. Technician evidence remains available below the customer section.
