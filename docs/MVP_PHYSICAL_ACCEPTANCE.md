# MVP physical-machine acceptance checklist

Run this checklist on a technician-controlled machine before declaring an MVP release ready. Do not install optional drivers merely to make a check pass.

## Common workflow

1. Launch the packaged x64 Inspector and confirm the dashboard remains readable at 1366 x 768 and the inspection-mode dropdown uses the dark theme.
2. Enter a customer/reference and technician name, then select **Run inspection**.
3. Confirm every stage ends in `PASS`, `WARNING`, `ERROR`, `SKIPPED`, `NOT_SUPPORTED`, `NOT_MEASURABLE`, or `CANCELLED`; a collector error must not stop unrelated stages.
4. Review the limitations and findings before exporting JSON, diagnostic log, and PDF.
5. Open the PDF, confirm it is A4, readable, and contains the customer, technician, assessment, warnings, actions, and the observed-at-inspection disclaimer.

## Windows 11 desktop

- Confirm Windows, CPU, RAM, volumes, and physical-drive inventory are populated.
- Confirm Event Log collection completes without an application crash.
- If SMART is returned, compare the selected drive serial/model with the report before trusting the condition.

## Windows 11 laptop

- Confirm the desktop checks above.
- Confirm battery condition is reported only when Windows exposes design and full-charge capacity; otherwise it is absent/not measurable, never `GOOD`.

## No readable temperature sensors

- Confirm the sensors stage ends `WARNING`, `NOT_MEASURABLE`, or equivalent limitation rather than `PASS` for temperatures.
- Confirm the report says temperature could not be verified and does not imply normal temperatures.

## No readable SMART data

- Confirm the storage inventory still appears when possible.
- Confirm each affected drive is `UNKNOWN`, `NOT TESTED`, or limited; missing SMART must not be reported as healthy.

## Non-administrator session

- Confirm core inventory, resources, storage capacity, event checks where permitted, JSON, log, and PDF still complete.
- Confirm protected/optional checks are explicitly skipped or limited with the reason recorded.

## Cancellation

- Start an inspection, press **Cancel inspection** during a long-running stage, and confirm the session is `CANCELLED` rather than completed.
- Restart the app and verify that no Inspector-owned PawnIO driver residue remains; retain the diagnostic log if cleanup reports a limitation.

Record the device model, Windows build, administrator state, storage/temperature availability, report paths, and any limitations with the release evidence.
