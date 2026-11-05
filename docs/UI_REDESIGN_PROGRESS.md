# A2Z System Inspector — UI Reference and Progress

Created: 2026-11-05 (Sri Lanka)
Target date: 2026-11-12 (provisional; UI-only milestone, subject to source inspection)
Status: NOT STARTED — reference image upload pending

## Goal
Recreate the approved A2Z System Inspector dark-blue UI reference with **at least 95% visual similarity** (target 98–100%), then verify the UI before testing backend behavior.

## Approved reference
Image generated on 2026-11-05 in the project conversation: `a_dark_blue_black_modern_ui_mockup_of_the_a2z_sys.png`.
**Image is not yet committed to this repository.** Do not mark the reference-upload task complete until the actual PNG exists in the repository. Intended path: `docs/ui-reference/a2z-inspector-ui-reference.png`.

## Required user workflow
1. Launch → only Job Number and Technician ID.
2. Start Health Check → full-window progress view.
3. On completion → automatically transition to full-window readable results.
4. Export technical evidence with job number and technician ID; ERP later resolves customer/device/serial and generates customer report.
5. No left navigation sidebar. No tiny report window. Responsive dark UI, readable scrolling and zoom where appropriate.

## Execution order
- [ ] Upload actual approved PNG reference to repository.
- [ ] Inspect and edit UI files only; no backend changes or full-system tests.
- [ ] Compare UI screenshots to approved reference; achieve >=95% visual similarity.
- [ ] Test UI layout, navigation, resizing and controls only.
- [ ] After visual threshold passes, test button behavior and automation.
- [ ] Backend integration deferred to a later phase.

## Progress log
- **2026-11-05:** Goal, acceptance criteria, target date and progress tracking created. No implementation or tests performed. Reference image upload still pending because the available GitHub write connector accepts UTF-8 text files, not binary PNG files.

Update this file at every meaningful implementation milestone with date, files changed, evidence, test results and remaining blockers. Do not claim success without verification.
