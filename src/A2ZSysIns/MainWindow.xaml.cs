using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace A2ZSysIns
{
    public partial class MainWindow : Window
    {
        private InspectionReport _report;
        private CancellationTokenSource _stressCancellation;
        private CancellationTokenSource _inspectionCancellation;
        private int _lastGraphRenderMs = -1000;

        public MainWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            MaxWidth = area.Width; MaxHeight = area.Height;
            Width = Math.Min(1080, Math.Max(MinWidth, area.Width - 24));
            Height = Math.Min(680, Math.Max(MinHeight, area.Height - 24));
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TechnicianBox.Text)) { MessageBox.Show(this, "Enter the technician name before starting.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            _report = new InspectionReport { InspectionId = DateTime.Now.ToString("yyyyMMdd-HHmmss"), StartedAt = DateTime.Now, CustomerReference = CustomerBox.Text.Trim(), JobNumber = JobBox.Text.Trim(), Technician = TechnicianBox.Text.Trim(), ReportedProblem = ProblemBox.Text.Trim(), InspectionMode = ((System.Windows.Controls.ComboBoxItem)ModeBox.SelectedItem).Content.ToString() };
            _inspectionCancellation = new CancellationTokenSource(); EvidenceEngine.Begin(_report); Tabs.SelectedIndex = 1; StartButton.IsEnabled = false; CancelInspectionButton.IsEnabled = true;
            try
            {
                await Stage("Preflight", () => InspectionStageRunner.Preflight(_report));
                await Stage("System information", () => Collectors.CollectSystem(_report));
                PortableSessionLog.SetDeviceIdentity(_report.System.ContainsKey("Manufacturer") ? _report.System["Manufacturer"] : null, _report.System.ContainsKey("Model") ? _report.System["Model"] : null, _report.System.ContainsKey("Serial number") ? _report.System["Serial number"] : null);
                EvidenceEngine.Log(_report, "Portable diagnostic location", PortableSessionLog.PathName);
                await Stage("Resource usage", () => EvidenceEngine.Resources(_report));
                await Stage("Storage", () => { StorageAcquisitionService.Collect(_report); foreach (var drive in _report.Drives.Where(x => x.SmartAttributes.Count == 0 && x.NvmeHealth == null && x.SmartDataSource != "smartctl JSON")) CrystalDiskInfoFallbackService.TryCollect(_report, drive); });
                await Stage("Windows events", () => EvidenceEngine.Events(_report));
                await Stage("Advanced diagnostics", () => AdvancedDiagnostics.Collect(_report));
                await Stage("Sensors", CollectSensorsWithPawnIoFallback);
                if (_report.InspectionMode == "MANUAL") _report.Stages.Add(new InspectionStageResult { Name = "Windows integrity", Status = "SKIPPED", Reason = "Manual mode: technician did not select integrity checks." });
                else if (_report.InspectionMode == "AUTOMATIC" && !_report.IsAdministrator) _report.Stages.Add(new InspectionStageResult { Name = "Windows integrity", Status = "SKIPPED", Reason = "Automatic mode: skipped because the Inspector is not elevated." });
                else await Stage("Windows integrity", () => WindowsIntegrityService.CollectAsync(_report, _inspectionCancellation.Token).GetAwaiter().GetResult());
                await Stage("Analysis", () => { Pass12NormalizationService.Apply(_report); StorageInterpretationService.Interpret(_report); StorageHealthAssessmentService.Record(_report); Scoring.Calculate(_report); SmartInterpretation.NormalizeReport(_report); AdvancedAssessment.Apply(_report); SmartInterpretation.RefreshSummary(_report); CustomerHealthAssessmentService.Apply(_report); });
                _report.CompletedAt = DateTime.Now; _report.InspectionState = _report.Stages.Any(x => x.Status == "ERROR" || x.Status == "WARNING" || x.Status == "SKIPPED") ? "COMPLETE_WITH_LIMITATIONS" : "COMPLETE"; Progress.Value = 100; ProgressText.Text = "Inspection " + _report.InspectionState + "."; StatusText.Text = "Inspection " + _report.InspectionId + " " + _report.InspectionState;
                EvidenceEngine.Log(_report, "Session completed", "Completed at " + _report.CompletedAt.ToString("o") + "; measurements=" + _report.Measurements.Count + "; findings=" + _report.Findings.Count);
                OverallText.Text = "Assessment: " + _report.OverallStatus; InspectionIdText.Text = "Inspection " + _report.InspectionId; ResultsList.ItemsSource = _report.Scores; ReportViewer.Document = DarkReportPreviewService.Build(_report); Tabs.SelectedIndex = 2;
            }
            catch (OperationCanceledException) { _report.CompletedAt = DateTime.Now; _report.InspectionState = "CANCELLED"; _report.Limitations.Add("Inspection cancelled by technician; collected evidence is partial."); EvidenceEngine.Log(_report, "Inspection cancelled", "Technician requested cancellation."); StatusText.Text = "Inspection CANCELLED"; }
            catch (Exception ex) { if (_report != null) { _report.InspectionState = "FAILED_FATAL"; EvidenceEngine.Log(_report, "Unhandled inspection error", ex.ToString()); } MessageBox.Show(this, "The inspection could not complete. Temporary Inspector-owned resources will be cleaned where applicable; see the retained diagnostic log.\n\n" + ex.GetBaseException().Message, "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Error); StatusText.Text = "Inspection FAILED_FATAL"; }
            finally { if (_report != null) DriverAccessManager.CleanupOwnedPawnIo(_report, "inspection finished"); _inspectionCancellation.Dispose(); _inspectionCancellation = null; StartButton.IsEnabled = true; CancelInspectionButton.IsEnabled = false; }
        }

        private void CollectSensorsWithPawnIoFallback()
        {
            var initialPawnIo = DriverAccessManager.DetectPawnIo(); EvidenceEngine.Log(_report, "Sensor access baseline", initialPawnIo.Detail);
            Collectors.CollectSensors(_report, x => Dispatcher.Invoke(() => ProgressText.Text = x));
            var hasCpuTemperature = _report.Sensors.Any(x => EvidenceEngine.ActualTemperature(x) && ((x.Hardware ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || (x.Name ?? "").StartsWith("CPU", StringComparison.OrdinalIgnoreCase) || x.Name == "Core Max" || x.Name == "Core Average"));
            if (!hasCpuTemperature && !initialPawnIo.Installed)
            {
                string detail; Dispatcher.Invoke(() => ProgressText.Text = "CPU temperature unavailable; trying temporary PawnIO 2.2 fallback...");
                if (DriverAccessManager.EnsureModernPawnIo(_report, out detail)) { EvidenceEngine.Log(_report, "Sensor retry", "Retrying LibreHardwareMonitor after PawnIO fallback. " + detail); _report.Sensors.Clear(); Collectors.CollectSensors(_report, x => Dispatcher.Invoke(() => ProgressText.Text = "PawnIO retry: " + x)); }
                else EvidenceEngine.Log(_report, "PawnIO sensor fallback unavailable", detail);
            }
            else if (!hasCpuTemperature && initialPawnIo.Installed) EvidenceEngine.Log(_report, "PawnIO sensor fallback not installed", "A pre-existing PawnIO installation was detected. Inspector did not modify or replace it.");
            EvidenceEngine.FinishSensors(_report); DriverAccessManager.CleanupOwnedPawnIo(_report, "sensor collection finished");
        }

        private async Task Stage(string name, Action action)
        {
            Progress.IsIndeterminate = true; ProgressText.Text = name + " is running (INDETERMINATE)..."; StatusText.Text = name + " is running";
            var progress = new Progress<InspectionStageResult>(s => { ProgressText.Text = s.Name + " — " + s.Status + ": " + s.Reason; StatusText.Text = s.Name + " " + s.Status; ResultsList.ItemsSource = null; ResultsList.ItemsSource = _report.Stages; });
            await InspectionStageRunner.RunAsync(_report, name, action, _inspectionCancellation.Token, progress);
            Progress.IsIndeterminate = false;
        }

        private void ViewReport_Click(object sender, RoutedEventArgs e) { if (Ready()) { ReportViewer.Document = DarkReportPreviewService.Build(_report); RenderStressGraphs(); Tabs.SelectedIndex = 3; } }
        private void ExportJson_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveJson(_report, this); }
        private void ExportText_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveText(_report, this); }
        private void ExportLog_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveDiagnosticLog(_report, this); }

        private async void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready()) return; SavePdfButton.IsEnabled = false; var old = StatusText.Text;
            try { StatusText.Text = "Generating PDF..."; var path = await PdfReportService.SavePdfAsync(_report, this); if (!string.IsNullOrWhiteSpace(path)) { StatusText.Text = "PDF saved: " + path; MessageBox.Show(this, "PDF saved successfully.\n\n" + path, "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Information); } else StatusText.Text = old; }
            catch (Exception ex) { EvidenceEngine.Log(_report, "PDF export failed", ex.ToString()); StatusText.Text = "PDF export failed"; MessageBox.Show(this, "The PDF could not be generated. The report data is still safe and the application will remain usable.\n\n" + ex.GetBaseException().Message, "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SavePdfButton.IsEnabled = true; }
        }

        private async void Stress_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready() || _stressCancellation != null) return;
            var answer = MessageBox.Show(this, "This optional test deliberately loads every logical CPU for up to 60 seconds. It ramps through 40%, 70% and 100% target load and stops at 90 °C or if CPU temperature monitoring is lost.\n\nTelemetry is sampled at up to 20 times per second and sensor update cadence is learned separately. Do not run it on a visibly damaged, unstable or poorly cooled computer. Continue?", "CPU stress-test safety confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            _stressCancellation = new CancellationTokenSource(); _lastGraphRenderMs = -1000; RunStressButton.IsEnabled = false; CancelStressButton.IsEnabled = true; StartButton.IsEnabled = false; Tabs.SelectedIndex = 3;
            try
            {
                var current = DriverAccessManager.DetectPawnIo();
                if (!current.Installed) { string detail; DriverAccessManager.EnsureModernPawnIo(_report, out detail); EvidenceEngine.Log(_report, "CPU stress driver preparation", detail); } else EvidenceEngine.Log(_report, "CPU stress driver preparation", current.Detail);
                var progress = new Progress<string>(text => { StatusText.Text = text; OverallText.Text = text; });
                var sampleProgress = new Progress<CpuStressSample>(sample => { if (sample.ElapsedMilliseconds - _lastGraphRenderMs >= 100) { _lastGraphRenderMs = sample.ElapsedMilliseconds; RenderStressGraphs(); } });
                var result = await CpuStressTestService.RunAsync(_report, _stressCancellation.Token, progress, sampleProgress);
                RenderStressGraphs();
                Pass12NormalizationService.Apply(_report); StorageInterpretationService.Interpret(_report); StorageHealthAssessmentService.Record(_report); Scoring.Calculate(_report); SmartInterpretation.NormalizeReport(_report); AdvancedAssessment.Apply(_report); SmartInterpretation.RefreshSummary(_report); CustomerHealthAssessmentService.Apply(_report);
                ResultsList.ItemsSource = null; ResultsList.ItemsSource = _report.Scores; OverallText.Text = "CPU test: " + result.Status + " — " + result.StopReason; ReportViewer.Document = DarkReportPreviewService.Build(_report);
            }
            catch (Exception ex) { EvidenceEngine.Log(_report, "CPU stress UI error", ex.ToString()); MessageBox.Show(this, ex.GetBaseException().Message, "CPU stress test", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { DriverAccessManager.CleanupOwnedPawnIo(_report, "CPU stress test finished"); _stressCancellation.Dispose(); _stressCancellation = null; RunStressButton.IsEnabled = true; CancelStressButton.IsEnabled = false; StartButton.IsEnabled = true; StatusText.Text = "CPU stress test finished"; }
        }

        private void RenderStressGraphs()
        {
            if (_report == null || _report.CpuStressTest == null) return;
            CpuStressGraphService.DrawUtilization(StressUtilizationGraph, _report.CpuStressTest.Samples);
            CpuStressGraphService.DrawThermalClock(StressThermalGraph, _report.CpuStressTest.Samples);
        }

        private void CancelStress_Click(object sender, RoutedEventArgs e) { if (_stressCancellation == null) return; EvidenceEngine.Log(_report, "CPU stress cancellation requested", "Technician pressed Stop stress test."); _stressCancellation.Cancel(); }
        private void CancelInspection_Click(object sender, RoutedEventArgs e) { if (_inspectionCancellation == null) return; EvidenceEngine.Log(_report, "Inspection cancellation requested", "Technician pressed Cancel inspection."); _inspectionCancellation.Cancel(); }
        private void New_Click(object sender, RoutedEventArgs e) { if (_stressCancellation != null) return; _report = null; ResultsList.ItemsSource = null; ReportViewer.Document = null; StressUtilizationGraph.Children.Clear(); StressThermalGraph.Children.Clear(); Progress.Value = 0; ProgressText.Text = "Ready"; Tabs.SelectedIndex = 0; StatusText.Text = "Ready"; }
        private bool Ready() { if (_report != null) return true; MessageBox.Show(this, "Complete an inspection first."); return false; }
    }
}
