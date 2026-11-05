# A2Z System Inspector — UI Reference and Progress

Created: 2026-11-05 (Sri Lanka)
Target date: 2026-11-12 (provisional; UI-only milestone, subject to source inspection)
Status: PARTIAL UI IMPLEMENTATION — 95% similarity NOT verified

## Goal
Recreate the approved A2Z System Inspector dark-blue UI reference with **at least 95% visual similarity** (target 98–100%), then verify the UI before testing backend behavior.

## Approved reference
Image generated on 2026-11-05 in the project conversation: `a_dark_blue_black_modern_ui_mockup_of_the_a2z_sys.png`.
**Canonical reference is stored in Google Drive:** [approved PNG](https://drive.google.com/file/d/1Vygc2ICvg6xPN-cru4kd2IBapvSJ7OSi/view?usp=drivesdk) in [UI References folder](https://drive.google.com/drive/folders/1JhNBOczdYJJorbX0v7lAvOszc7-_AHet). The PNG is not yet committed to this repository. Do not mark the reference-upload task complete until the actual PNG exists in the repository. Intended path: `docs/ui-reference/a2z-inspector-ui-reference.png`.

## Required user workflow
1. Launch → only Job Number and Technician ID.
2. Start Health Check → full-window progress view.
3. On completion → automatically transition to full-window readable results.
4. Export technical evidence with job number and technician ID; ERP later resolves customer/device/serial and generates customer report.
5. No left navigation sidebar. No tiny report window. Responsive dark UI, readable scrolling and zoom where appropriate.

## Execution order
- [ ] Upload actual approved PNG reference to repository.
- [x] Inspect current UI file locations and styling; no backend changes or full-system tests.
- [x] Initial UI XAML changes committed (partial, not visually approved).
- [ ] Complete one-stage-at-a-time layout and screenshot comparison.
- [ ] Compare UI screenshots to approved reference; achieve >=95% visual similarity.
- [ ] Test UI layout, navigation, resizing and controls only.
- [ ] After visual threshold passes, test button behavior and automation.
- [ ] Backend integration deferred to a later phase.

## Progress log
- **2026-11-05:** Goal, acceptance criteria, target date and progress tracking created. No implementation or tests performed. Reference image upload still pending because the available GitHub write connector accepts UTF-8 text files, not binary PNG files.

- **2026-11-05:** Created Google Drive folder and verified approved PNG is inside it. Inspected `src/A2ZSysIns/MainWindow.xaml` and `src/A2ZSysIns/App.xaml`: WPF UI; MainWindow has dark styling, navigation/tab styles and 1180×720 minimum; App.xaml has light global TextBox/ListView/Tab styles that could conflict. No UI edits or UI tests yet. Visual similarity unmeasured. Drive link is the canonical image reference until a binary copy can be committed.

- **2026-11-05:** Updated `src/A2ZSysIns/MainWindow.xaml` in commits `c106145` and `93f66de`: zero-width sidebar column, minimum size 840×600, hide customer/problem inputs, label technician ID, revised stage headings, expand report by collapsing graph row, enable report zoom 50–250%, rename JSON export for ERP. Read-back checks: {"sidebarColumnRemoved":true,"jobField":true,"techField":true,"customerHidden":true,"problemHidden":true,"reportZoom":true,"reportGraphRowCollapsed":true,"backendUntouched":true}. No Windows rendering or screenshot comparison available here; 95% visual similarity not achieved/verified. Existing tab navigation, extra controls, and auto-transition still need work. No backend tests run.

Update this file at every meaningful implementation milestone with date, files changed, evidence, test results and remaining blockers. Do not claim success without verification.
