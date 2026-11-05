# A2Z System Inspector — UI Reference and Progress

Created: 2026-11-05 (Sri Lanka)
Target date: 2026-11-12 (provisional; UI-only milestone, subject to source inspection)
Status: DRAFT PR #12 — WINDOWS BUILD, PASS 8 AND SIX-SCREEN VISUAL CAPTURE PASS; 95–99% NUMERIC SIMILARITY NOT CERTIFIED

## Goal
Recreate the approved A2Z System Inspector dark-blue UI reference with **at least 95% visual similarity** (target 98–100%), then verify the UI before testing backend behavior.

## Approved reference
Image generated on 2026-11-05 in the project conversation: `a_dark_blue_black_modern_ui_mockup_of_the_a2z_sys.png`.
**Canonical reference is stored in Google Drive:** [approved PNG](https://drive.google.com/file/d/1Vygc2ICvg6xPN-cru4kd2IBapvSJ7OSi/view?usp=drivesdk) in [UI References folder](https://drive.google.com/drive/folders/1JhNBOczdYJJorbX0v7lAvOszc7-_AHet). **Exact original PNG is also committed to the draft branch** at [`docs/ui-reference/a2z-inspector-ui-reference.png`](https://github.com/abi592983-oss/a2z-sys-ins/blob/codex/ui-reference-redesign-20261105/docs/ui-reference/a2z-inspector-ui-reference.png). Git blob `a292248fe74d5380e9ed50ff533d8204536f5efc` matches the original local PNG byte-for-byte.

## Required user workflow
1. Launch → only Job Number and Technician ID.
2. Start Health Check → full-window progress view.
3. On completion → automatically transition to full-window readable results.
4. Export technical evidence with job number and technician ID; ERP later resolves customer/device/serial and generates customer report.
5. No left navigation sidebar. No tiny report window. Responsive dark UI, readable scrolling and zoom where appropriate.

## Execution order
- [x] Upload actual approved PNG reference to repository, in the draft branch (not yet in `main`).
- [x] Inspect current UI file locations and styling; no backend changes or full-system tests.
- [x] Initial UI XAML changes committed (partial, not visually approved).
- [x] Implement separate Job Details, Health Check and Results & Export full-window XAML screens and hide the tab headers.
- [x] Capture genuine offscreen WPF screenshots of each stage at 1366×768 and 1024×768; visually inspect and fix major layout defects.
- [ ] Complete quantitative/human 95–99% reference similarity approval, which cannot be inferred from a passing build.
- [ ] Compare UI screenshots to approved reference; achieve >=95% visual similarity.
- [x] Verify stage navigation visually and static layout at two desktop/laptop sizes using Windows CI screenshots.
- [ ] Test button interactions and complete real inspection on a Windows PC.
- [ ] After visual threshold passes, test button behavior and automation.
- [ ] Backend integration deferred to a later phase.

## Progress log
- **2026-11-05:** Goal, acceptance criteria, target date and progress tracking created. No implementation or tests performed. Reference image upload still pending because the available GitHub write connector accepts UTF-8 text files, not binary PNG files.

- **2026-11-05:** Created Google Drive folder and verified approved PNG is inside it. Inspected `src/A2ZSysIns/MainWindow.xaml` and `src/A2ZSysIns/App.xaml`: WPF UI; MainWindow has dark styling, navigation/tab styles and 1180×720 minimum; App.xaml has light global TextBox/ListView/Tab styles that could conflict. No UI edits or UI tests yet. Visual similarity unmeasured. Drive link is the canonical image reference until a binary copy can be committed.

- **2026-11-05:** Updated `src/A2ZSysIns/MainWindow.xaml` in commits `c106145` and `93f66de`: zero-width sidebar column, minimum size 840×600, hide customer/problem inputs, label technician ID, revised stage headings, expand report by collapsing graph row, enable report zoom 50–250%, rename JSON export for ERP. Read-back checks: {"sidebarColumnRemoved":true,"jobField":true,"techField":true,"customerHidden":true,"problemHidden":true,"reportZoom":true,"reportGraphRowCollapsed":true,"backendUntouched":true}. No Windows rendering or screenshot comparison available here; 95% visual similarity not achieved/verified. Existing tab navigation, extra controls, and auto-transition still need work. No backend tests run.

- **2026-11-05:** Continued UI work: commit `876b852` requires nonblank job number and technician ID, and automatically switches to results after a successful inspection; commit `59474b2` changes global WPF text/list styles to dark colors. Source readback: {"jobRequired":true,"technicianIdRequired":true,"autoResults":true,"darkGlobalTextbox":true,"darkGlobalList":true}. CI audit: Windows build workflow triggers on `main`; Pass 8 workflow does not trigger on a normal main push. No confirmed CI outcome, Windows render, or measured screenshot similarity. No backend changes; code-behind change limited to UI flow and validation.


- **2026-11-05:** Reference-image analysis: the approved image is a **three-column conceptual montage**, not a literal screen with all stages active simultaneously. Following the user's required workflow, each stage occupies the full app workspace, while retaining the montage's dark navy, electric blue accents, rounded cards, numbered top-stepper, progress ring and green/warning/critical summary.

- **2026-11-05:** Draft PR [#12](https://github.com/abi592983-oss/a2z-sys-ins/pull/12) opened from `codex/ui-reference-redesign-20261105` into `main`; **not merged**. UI files: `MainWindow.xaml` (full-window screens, reference header, hidden sidebar/tab headers, Job Number + Technician ID, progress stage list, elapsed timer, evidence console, results/ERP export, report expander); `MainWindow.xaml.cs` (stepper state, checkpoint progress, rough ETA, auto-result transition, results binding, status colors); `DarkReportPreviewService.cs` (navy report theme). No collector/scoring/backend code changed. **Do not generate fake health scores**: current scoring model deliberately uses a nullable, generally unset `OverallScore`; UI shows N/A instead of inventing 87/100.

- **2026-11-05 CI evidence:** Previous `main` commit `794ba701` [Windows build success](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41975827360). Draft PR commit `61ee04b5`: [Windows build success](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41977257376) and [Pass 8 validation success](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41977257263). Later commits may need a fresh run; latest at this writing `beb79223` queued. This is CI build/test evidence **not** a rendered screenshot or 95–99% pixel similarity measurement. UI controls and sizing still require live Windows verification.


- **2026-11-05 — reference and CI screenshot milestone:** Original 1,566,134-byte PNG committed in `docs/ui-reference/` through Git binary blob/tree/commit with Git SHA `a292248fe74d5380e9ed50ff533d8204536f5efc` (locally verified matching byte-for-byte). Added `tools/Capture-InspectorUI.ps1` and `.github/workflows/ui-screenshot.yml`: Windows CI constructs the WPF window offscreen without running collectors and renders six PNGs (all three stages × 1366×768 and 1024×768). **Visual audit caught and fixed**: unwanted legacy tab headers, incorrectly static stepper, overflowing Start form on 768px-high screen, Cancel below the fold, and misleading green pending results. The rendered screen shows the selected stage and controls in the correct viewport. Screenshot audit illustrates layout; it **does not** execute a real health check or validate diagnostic output. Results component data uses bindable UI rows with explicit unknown/warning/critical states. No fake health score is emitted (N/A if unavailable). ERP export currently saves JSON for later import; it is **not direct API upload**.

- **2026-11-05 — current CI evidence, commit `e51e4d29`:** [Windows build: SUCCESS](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41979334960), [Pass 8 Validation: SUCCESS](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41979334811), [UI screenshot audit: SUCCESS](https://github.com/abi592983-oss/a2z-sys-ins/actions/runs/41979334813). Six actual PNG screenshots verified and uploaded to [Google Drive / CI Rendered UI Screenshots — 2026-11-05](https://drive.google.com/drive/folders/15adP-Y03xOK8tr9WtMKTBY2ZeYFcavVO), within the approved reference folder. This confirms build/test/capture only, **not a measured 95–99% visual similarity**, live sensor data, or all button behavior. Draft PR [#12](https://github.com/abi592983-oss/a2z-sys-ins/pull/12) remains unmerged.

Update this file at every meaningful implementation milestone with date, files changed, evidence, test results and remaining blockers. Do not claim success without verification.

- **2026-11-05 — dark resource follow-up:** Commit `d531c289` changes `src/A2ZSysIns/App.xaml` to remove the remaining white global `ListViewItem` background and dark foreground on `GridViewColumnHeader`, replacing them with navy (`#0C2137`) and readable pale blue (`#C7DBF3`). UI resource correction only; no collectors, scoring, backend, or ERP integration altered. Source-level correction committed; Windows screenshot regression and 95–99% visual acceptance remain pending for this commit.

- **2026-11-05 — active job reset guard:** Commit `2d3045c8` modifies only `MainWindow.xaml.cs` so New Inspection cannot clear `_report` while `_inspectionCancellation` or `_stressCancellation` is active; a technician-facing explanation is shown. This prevents resetting the report from another navigation surface during an in-flight task. Requires current-commit Windows CI and physical UI interaction validation; no collector logic changed.
