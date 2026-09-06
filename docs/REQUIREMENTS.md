# A2Z System Inspector — Version 1 Requirements

## Purpose

Produce an offline, read-only health screening report for one Windows computer. The technician manually uses the scores in the existing ERP workflow.

## Required behavior

1. Run on Windows 7 SP1, Windows 10 and Windows 11 x64 with .NET Framework 4.8.
2. Continue when an individual collector is unsupported and display N/A rather than zero.
3. Preserve raw SMART evidence in JSON when smartctl is available.
4. Identify every report with inspection ID, timestamps, application schema, ruleset, technician and device metadata.
5. Show category and overall scores, plain-language findings, recommendations and limitations.
6. Cap the overall result when a critical storage failure is reported.
7. Preview, print and export the report locally without an Internet connection.
8. Perform no repair, cleanup, update, deletion, optimization or configuration change.

## Version 1 exclusions

- ERP/API integration
- Accounts and authentication
- Cloud sync or remote monitoring
- Linux or WinPE collectors
- Automated stress, SMART self-test or memory test
- Automatic repairs or recommendations presented as confirmed diagnoses
- Background service or scheduled scanning

## Acceptance checks

- Application starts and completes with optional adapters absent.
- A missing or denied measurement is shown as N/A and does not become a false zero.
- Report prints through the standard Windows print dialog.
- JSON export opens and retains raw evidence and ruleset version.
- No inspection operation changes the tested computer.
