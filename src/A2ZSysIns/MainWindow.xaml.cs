using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace A2ZSysIns
{
    public partial class MainWindow : Window
    {
        private InspectionReport _report;
        public MainWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            MaxWidth = area.Width;
            MaxHeight = area.Height;
            Width = Math.Min(1080, Math.Max(MinWidth, area.Width - 24));
            Height = Math.Min(680, Math.Max(MinHeight, area.Height - 24));
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TechnicianBox.Text)) { MessageBox.Show(this, "Enter the technician name before starting.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            _report = new InspectionReport { InspectionId = DateTime.Now.ToString("yyyyMMdd-HHmmss"), StartedAt = DateTime.Now, CustomerReference = CustomerBox.Text.Trim(), JobNumber = JobBox.Text.Trim(), Technician = TechnicianBox.Text.Trim(), ReportedProblem = ProblemBox.Text.Trim() };
            EvidenceEngine.Begin(_report);
            Tabs.SelectedIndex = 1; StartButton.IsEnabled = false;
            try
            {
                await Step(10, "Collecting Windows and hardware information...", () => Collectors.CollectSystem(_report));
                await Step(22, "Measuring resource usage with fallback methods...", () => EvidenceEngine.Resources(_report));
                await Step(38, "Collecting physical storage and SMART evidence...", () => EvidenceEngine.Drives(_report));
                await Step(58, "Reviewing Windows events and their recorded details...", () => EvidenceEngine.Events(_report));
                await Step(75, "Sampling available hardware sensors...", () => { Collectors.CollectSensors(_report, x => Dispatcher.Invoke(() => ProgressText.Text = x)); EvidenceEngine.FinishSensors(_report); });
                await Step(90, "Calculating evidence-based results...", () => Scoring.Calculate(_report));
                _report.CompletedAt = DateTime.Now; Progress.Value = 100; ProgressText.Text = "Inspection completed."; StatusText.Text = "Inspection " + _report.InspectionId + " completed";
                EvidenceEngine.Log(_report, "Session completed", "Completed at " + _report.CompletedAt.ToString("o") + "; measurements=" + _report.Measurements.Count + "; findings=" + _report.Findings.Count);
                OverallText.Text = "Assessment: " + _report.OverallStatus; InspectionIdText.Text = "Inspection " + _report.InspectionId; ResultsList.ItemsSource = _report.Scores; ReportViewer.Document = ReportService.Build(_report); Tabs.SelectedIndex = 2;
            }
            catch (Exception ex) { if (_report != null) EvidenceEngine.Log(_report, "Unhandled inspection error", ex.ToString()); MessageBox.Show(this, "The inspection could not complete. No changes were made to this PC.\n\n" + ex.GetBaseException().Message, "Inspection error", MessageBoxButton.OK, MessageBoxImage.Error); StatusText.Text = "Inspection stopped"; }
            finally { StartButton.IsEnabled = true; }
        }
        private async Task Step(int value, string text, Action action)
        {
            Progress.Value = value; ProgressText.Text = text; StatusText.Text = text;
            var timer = Stopwatch.StartNew();
            EvidenceEngine.Log(_report, "Step started", text);
            try
            {
                await Task.Run(action);
                EvidenceEngine.Log(_report, "Step completed", text + " Duration=" + timer.Elapsed);
            }
            catch (Exception ex)
            {
                EvidenceEngine.Log(_report, "Step failed", text + " Duration=" + timer.Elapsed + "\n" + ex);
                throw;
            }
        }
        private void ViewReport_Click(object sender, RoutedEventArgs e) { if (Ready()) { ReportViewer.Document = ReportService.Build(_report); Tabs.SelectedIndex = 3; } }
        private void ExportJson_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveJson(_report, this); }
        private void ExportText_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveText(_report, this); }
        private void ExportLog_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveDiagnosticLog(_report, this); }
        private void Print_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.Print(ReportService.Build(_report), this); }
        private void New_Click(object sender, RoutedEventArgs e) { _report = null; ResultsList.ItemsSource = null; ReportViewer.Document = null; Progress.Value = 0; ProgressText.Text = "Ready"; Tabs.SelectedIndex = 0; StatusText.Text = "Ready"; }
        private bool Ready() { if (_report != null) return true; MessageBox.Show(this, "Complete an inspection first."); return false; }
    }
}
