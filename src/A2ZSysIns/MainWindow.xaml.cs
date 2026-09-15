using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace A2ZSysIns
{
    public partial class MainWindow : Window
    {
        private InspectionReport _report;
        private CancellationTokenSource _stressCancellation;
        private int _lastGraphRenderMs = -1000;

        public MainWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            MaxWidth = area.Width; MaxHeight = area.Height;
            Width = Math.Min(1180, Math.Max(MinWidth, area.Width - 24));
            Height = Math.Min(760, Math.Max(MinHeight, area.Height - 24));
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TechnicianBox.Text))
            {
                MessageBox.Show(this, "Enter the technician name before starting.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _report = new InspectionReport { InspectionId = DateTime.Now.ToString("yyyyMMdd-HHmmss"), StartedAt = DateTime.Now, CustomerReference = CustomerBox.Text.Trim(), JobNumber = JobBox.Text.Trim(), Technician = TechnicianBox.Text.Trim(), ReportedProblem = ProblemBox.Text.Trim() };
            EvidenceEngine.Begin(_report); Tabs.SelectedIndex = 1; StartButton.IsEnabled = false;
            try
            {
                await Step(8, "Collecting Windows and hardware information...", () => Collectors.CollectSystem(_report));
                PortableSessionLog.SetDeviceIdentity(_report.System.ContainsKey("Manufacturer") ? _report.System["Manufacturer"] : null, _report.System.ContainsKey("Model") ? _report.System["Model"] : null, _report.System.ContainsKey("Serial number") ? _report.System["Serial number"] : null);
                EvidenceEngine.Log(_report, "Portable diagnostic location", PortableSessionLog.PathName);
                await Step(18, "Measuring resource usage with fallback methods...", () => EvidenceEngine.Resources(_report));
                await Step(32, "Detecting and scanning all physical storage devices...", () => { StorageAcquisitionService.Collect(_report); foreach (var drive in _report.Drives.Where(x => x.SmartAttributes.Count == 0 && x.NvmeHealth == null && x.SmartDataSource != "smartctl JSON")) CrystalDiskInfoFallbackService.TryCollect(_report, drive); });
                await Step(48, "Reviewing core Windows events and recorded details...", () => EvidenceEngine.Events(_report));
                await Step(62, "Collecting advanced correlated evidence...", () => AdvancedDiagnostics.Collect(_report));
                await Step(78, "Sampling available hardware sensors...", CollectSensorsWithPawnIoFallback);
                await Step(86, "Checking Windows system and file integrity...", () => WindowsIntegrityService.Collect(_report));
                await Step(94, "Building customer health conclusions...", () => { Pass12NormalizationService.Apply(_report); StorageInterpretationService.Interpret(_report); StorageHealthAssessmentService.Record(_report); Scoring.Calculate(_report); SmartInterpretation.NormalizeReport(_report); AdvancedAssessment.Apply(_report); SmartInterpretation.RefreshSummary(_report); CustomerHealthAssessmentService.Apply(_report); });
                _report.CompletedAt = DateTime.Now; Progress.Value = 100; ProgressText.Text = "Inspection completed."; StatusText.Text = "Inspection " + _report.InspectionId + " completed";
                EvidenceEngine.Log(_report, "Session completed", "Completed at " + _report.CompletedAt.ToString("o") + "; measurements=" + _report.Measurements.Count + "; findings=" + _report.Findings.Count);
                OverallText.Text = "Assessment: " + _report.OverallStatus; InspectionIdText.Text = "Inspection " + _report.InspectionId; ResultsList.ItemsSource = _report.Scores; ReportViewer.Document = DarkReportPreviewService.Build(_report);
                UpdateDashboard();
                Tabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                if (_report != null) EvidenceEngine.Log(_report, "Unhandled inspection error", ex.ToString());
                MessageBox.Show(this, "The inspection could not complete. Temporary Inspector-owned resources will be cleaned where applicable; see the retained diagnostic log.\n\n" + ex.GetBaseException().Message, "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Inspection stopped";
            }
            finally { if (_report != null) DriverAccessManager.CleanupOwnedPawnIo(_report, "inspection finished"); StartButton.IsEnabled = true; }
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

        private async Task Step(int value, string text, Action action)
        {
            Progress.Value = value; ProgressText.Text = text; StatusText.Text = text; var timer = Stopwatch.StartNew(); EvidenceEngine.Log(_report, "Step started", text);
            try { await Task.Run(action); EvidenceEngine.Log(_report, "Step completed", text + " Duration=" + timer.Elapsed); }
            catch (Exception ex) { EvidenceEngine.Log(_report, "Step failed", text + " Duration=" + timer.Elapsed + "\n" + ex); throw; }
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
                ResultsList.ItemsSource = null; ResultsList.ItemsSource = _report.Scores; OverallText.Text = "CPU test: " + result.Status + " — " + result.StopReason; ReportViewer.Document = DarkReportPreviewService.Build(_report); UpdateDashboard();
            }
            catch (Exception ex) { EvidenceEngine.Log(_report, "CPU stress UI error", ex.ToString()); MessageBox.Show(this, ex.GetBaseException().Message, "CPU stress test", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { DriverAccessManager.CleanupOwnedPawnIo(_report, "CPU stress test finished"); _stressCancellation.Dispose(); _stressCancellation = null; RunStressButton.IsEnabled = true; CancelStressButton.IsEnabled = false; StartButton.IsEnabled = true; StatusText.Text = "CPU stress test finished"; }
        }

        private void RenderStressGraphs()
        {
            if (_report == null || _report.CpuStressTest == null) return;
            CpuStressGraphService.DrawUtilization(StressUtilizationGraph, _report.CpuStressTest.Samples);
            CpuStressGraphService.DrawThermalClock(StressThermalGraph, _report.CpuStressTest.Samples);
            if (DashboardStressGraph != null) CpuStressGraphService.DrawUtilization(DashboardStressGraph, _report.CpuStressTest.Samples);
        }

        private void UpdateDashboard()
        {
            if (_report == null) return;
            var system = _report.System;
            var model = Value(system, "Model", "Unknown device");
            var cpu = ShortCpu(Value(system, "CPU", "CPU unavailable"));
            var ram = Value(system, "Installed RAM", "RAM unavailable");
            var os = Value(system, "Operating system", "Windows");
            DeviceNameText.Text = model;
            DeviceDetailsText.Text = cpu + "\n" + ram + "  •  " + os;

            OverallScoreText.Text = _report.OverallScore.HasValue ? _report.OverallScore.Value.ToString() : "—";
            var health = _report.CustomerHealth ?? new CustomerHealthSummary();
            OverallStatusBadge.Text = NormalizeStatus(health.OverallStatus);
            OverallStatusBadge.Foreground = StatusBrush(health.OverallStatus);
            OverallHeadlineText.Text = string.IsNullOrWhiteSpace(health.Headline) ? "Inspection completed" : health.Headline;
            RecommendationText.Text = health.RecommendedActions != null && health.RecommendedActions.Count > 0 ? string.Join("   •   ", health.RecommendedActions.Take(3)) : (health.Explanation ?? "No recommendation recorded.");

            var storage = Component("Storage");
            var temperature = Component("Temperature");
            var ramComponent = Component("RAM");
            var windows = Component("Windows integrity");
            if (windows == null) windows = Component("Windows");
            var devices = Component("Devices");

            var temp = CpuTemperature();
            TemperatureValueText.Text = temp.HasValue ? Math.Round(temp.Value, 0) + "°C" : "—";
            TemperatureStatusText.Text = temperature == null ? "Telemetry" : NormalizeStatus(temperature.Status);
            TemperatureStatusText.Foreground = temperature == null ? (Brush)FindResource("Muted") : StatusBrush(temperature.Status);

            CpuValueText.Text = cpu.Length > 20 ? cpu.Substring(0, 20) + "…" : cpu;
            CpuStatusText.Text = _report.CpuStressTest == null ? "Inspection complete" : NormalizeStatus(_report.CpuStressTest.Status);
            CpuStatusText.Foreground = StatusBrush(_report.CpuStressTest == null ? "GOOD" : _report.CpuStressTest.Status);

            MemoryValueText.Text = ram;
            MemoryStatusText.Text = ramComponent == null ? (_report.MemoryUsedPercent.HasValue ? Math.Round(_report.MemoryUsedPercent.Value) + "% in use" : "Measured") : NormalizeStatus(ramComponent.Status);
            MemoryStatusText.Foreground = ramComponent == null ? (Brush)FindResource("Muted") : StatusBrush(ramComponent.Status);

            var drive = _report.Drives.FirstOrDefault();
            StorageValueText.Text = drive == null ? "—" : (drive.RemainingLifePercent.HasValue ? Math.Round(drive.RemainingLifePercent.Value) + "%" : "Detected");
            StorageStatusText.Text = storage == null ? (drive == null ? "Not measured" : "Detected") : NormalizeStatus(storage.Status);
            StorageStatusText.Foreground = storage == null ? (Brush)FindResource("Muted") : StatusBrush(storage.Status);

            WindowsValueText.Text = os.Length > 18 ? os.Substring(0, 18) + "…" : os;
            WindowsStatusText.Text = windows == null ? "Measured" : NormalizeStatus(windows.Status);
            WindowsStatusText.Foreground = windows == null ? (Brush)FindResource("Muted") : StatusBrush(windows.Status);

            var problemDevices = Value(system, "Problem devices", "Not measured");
            DevicesValueText.Text = devices != null && IsGood(devices.Status) ? "OK" : (problemDevices.IndexOf("None reported", StringComparison.OrdinalIgnoreCase) >= 0 ? "OK" : "Check");
            DevicesStatusText.Text = devices == null ? problemDevices : NormalizeStatus(devices.Status);
            DevicesStatusText.Foreground = devices == null ? (Brush)FindResource("Muted") : StatusBrush(devices.Status);

            RecentEventsList.ItemsSource = _report.Events == null || _report.Events.Count == 0 ? null : _report.Events.OrderByDescending(x => x.Latest).Take(6).ToList();
            RenderStressGraphs();
        }

        private ComponentHealth Component(string name)
        {
            if (_report == null || _report.CustomerHealth == null || _report.CustomerHealth.Components == null) return null;
            return _report.CustomerHealth.Components.FirstOrDefault(x => string.Equals(x.Component, name, StringComparison.OrdinalIgnoreCase));
        }

        private double? CpuTemperature()
        {
            if (_report == null || _report.Sensors == null) return null;
            var sensors = _report.Sensors.Where(EvidenceEngine.ActualTemperature).Where(x => (x.Hardware ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || (x.Name ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || x.Name == "Core Max" || x.Name == "Core Average").ToList();
            if (sensors.Count == 0) return null;
            return sensors.Max(x => Convert.ToDouble(x.Current));
        }

        private static string Value(System.Collections.Generic.Dictionary<string, string> values, string key, string fallback)
        {
            if (values != null && values.ContainsKey(key) && !string.IsNullOrWhiteSpace(values[key])) return values[key];
            return fallback;
        }

        private static string ShortCpu(string cpu)
        {
            if (string.IsNullOrWhiteSpace(cpu)) return "CPU unavailable";
            var cut = cpu.IndexOf(" @ ", StringComparison.OrdinalIgnoreCase);
            return (cut > 0 ? cpu.Substring(0, cut) : cpu).Trim();
        }

        private static string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "UNKNOWN";
            return status.Replace("_", " ").ToUpperInvariant();
        }

        private Brush StatusBrush(string status)
        {
            var s = NormalizeStatus(status);
            if (s.Contains("CRITICAL") || s.Contains("FAIL")) return (Brush)FindResource("Red");
            if (s.Contains("ATTENTION") || s.Contains("WARNING") || s.Contains("DEGRADED")) return (Brush)FindResource("Amber");
            if (s.Contains("GOOD") || s.Contains("PASS") || s.Contains("NORMAL") || s.Contains("OK") || s.Contains("COMPLETED")) return (Brush)FindResource("Green");
            return (Brush)FindResource("Muted");
        }

        private static bool IsGood(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.ToUpperInvariant();
            return s.Contains("GOOD") || s.Contains("PASS") || s.Contains("OK");
        }

        private void CancelStress_Click(object sender, RoutedEventArgs e) { if (_stressCancellation == null) return; EvidenceEngine.Log(_report, "CPU stress cancellation requested", "Technician pressed Stop stress test."); _stressCancellation.Cancel(); }
        private void New_Click(object sender, RoutedEventArgs e) { if (_stressCancellation != null) return; _report = null; ResultsList.ItemsSource = null; RecentEventsList.ItemsSource = null; ReportViewer.Document = null; StressUtilizationGraph.Children.Clear(); StressThermalGraph.Children.Clear(); DashboardStressGraph.Children.Clear(); Progress.Value = 0; ProgressText.Text = "Ready"; Tabs.SelectedIndex = 0; StatusText.Text = "Ready"; OverallScoreText.Text = "—"; OverallStatusBadge.Text = "NOT TESTED"; OverallStatusBadge.Foreground = (Brush)FindResource("Muted"); OverallHeadlineText.Text = "No inspection completed"; RecommendationText.Text = "Complete an inspection to receive evidence-based recommendations."; }
        private bool Ready() { if (_report != null) return true; MessageBox.Show(this, "Complete an inspection first."); return false; }
    }
}