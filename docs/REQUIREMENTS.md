# A2Z System Inspector — Version 2 Requirements

## Purpose

Produce an offline, read-only, evidence-based health screening report for one Windows computer. The technician manually uses the findings in the existing ERP workflow.

## Required behavior

1. Run on Windows 7 SP1, Windows 10 and Windows 11 x64 with .NET Framework 4.8.
2. Continue when an individual collector is unsupported and explicitly record `Unavailable`/`Cannot measure` rather than zero or a guessed result.
3. Use a primary and fallback method for important measurements when Windows exposes both; record which method succeeded.
4. Preserve structured SMART evidence, event XML, measurement coverage, collector responses and errors in JSON.
5. Identify every report with inspection ID, timestamps, application schema, ruleset, technician and device metadata.
6. Keep SMART threshold status, device life/endurance indicators and fault attributes separate. Never derive a health percentage from Windows device `Status=OK`.
7. Report recurring event signatures when they occur more than twice in the 30-day window. Do not claim that Event ID 41 proves a hardware fault or a particular cause.
8. Show plain-language evidence assessments, findings, recommendations and limitations; withhold an overall percentage until a validated model exists.
9. Preview, print and export the report locally without an Internet connection.
10. Export a timestamped diagnostic log of application collector actions, raw responses, fallback attempts, errors and assessment decisions.
11. Perform no repair, cleanup, update, deletion, optimization or configuration change.

## Version 2 exclusions

- ERP/API integration
- Accounts and authentication
- Cloud sync or remote monitoring
- Linux or WinPE collectors
- Automated stress, SMART self-test or memory test
- Automatic repairs or recommendations presented as confirmed diagnoses
- Background service or scheduled scanning
- User-activity recording, keylogging or recording actions outside System Inspector

## Acceptance checks

- Application requests elevation and completes even when an individual hardware interface remains unsupported.
- A missing or denied measurement is shown as `Unavailable`/`Cannot measure` in JSON and does not become a false zero.
- Storage assessment agrees with supported raw SMART failure attributes; a life percentage is shown only when a validated explicit field is present.
- Actual temperature rows exclude `Distance to TjMax`/thermal-headroom sensors.
- Event 41 with no recorded bug-check or power-button evidence reports cause unknown, including power loss as one possibility.
- Repeated application and system-event signatures are identified after more than two matches in 30 days.
- Report prints through the standard Windows print dialog.
- JSON export opens and retains raw evidence, coverage, limitations and ruleset version.
- Diagnostic log export contains the ordered collector request/response trail.
- No inspection operation changes the tested computer.
