using System;
using System.Threading.Tasks;
using System.Windows;

namespace A2ZSysIns
{
    public partial class MainWindow : Window
    {
        private InspectionReport _report;
        public MainWindow() { InitializeComponent(); }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TechnicianBox.Text)) { MessageBox.Show(this, "Enter the technician name before starting.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            _report = new InspectionReport { InspectionId = DateTime.Now.ToString("yyyyMMdd-HHmmss"), StartedAt = DateTime.Now, CustomerReference = CustomerBox.Text.Trim(), JobNumber = JobBox.Text.Trim(), Technician = TechnicianBox.Text.Trim(), ReportedProblem = ProblemBox.Text.Trim() };
            Tabs.SelectedIndex = 1; StartButton.IsEnabled = false;
            try
            {
                await Step(10, "Collecting Windows and hardware information...", () => Collectors.CollectSystem(_report));
                await Step(30, "Collecting physical storage and SMART evidence...", () => Collectors.CollectDrives(_report));
                await Step(50, "Reviewing Windows events from the last 30 days...", () => Collectors.CollectEvents(_report));
                await Step(70, "Sampling available hardware sensors...", () => Collectors.CollectSensors(_report, x => Dispatcher.Invoke(() => ProgressText.Text = x)));
                await Step(90, "Calculating evidence-based results...", () => Scoring.Calculate(_report));
                _report.CompletedAt = DateTime.Now; Progress.Value = 100; ProgressText.Text = "Inspection completed."; StatusText.Text = "Inspection " + _report.InspectionId + " completed";
                OverallText.Text = _report.OverallScore.HasValue ? "Overall: " + _report.OverallScore + "/100 — " + _report.OverallStatus : "Overall: N/A"; InspectionIdText.Text = "Inspection " + _report.InspectionId; ResultsList.ItemsSource = _report.Scores; ReportViewer.Document = ReportService.Build(_report); Tabs.SelectedIndex = 2;
            }
            catch (Exception ex) { MessageBox.Show(this, "The inspection could not complete. No changes were made to this PC.\n\n" + ex.GetBaseException().Message, "Inspection error", MessageBoxButton.OK, MessageBoxImage.Error); StatusText.Text = "Inspection stopped"; }
            finally { StartButton.IsEnabled = true; }
        }
        private async Task Step(int value, string text, Action action) { Progress.Value = value; ProgressText.Text = text; StatusText.Text = text; await Task.Run(action); }
        private void ViewReport_Click(object sender, RoutedEventArgs e) { if (Ready()) { ReportViewer.Document = ReportService.Build(_report); Tabs.SelectedIndex = 3; } }
        private void ExportJson_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveJson(_report, this); }
        private void ExportText_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.SaveText(_report, this); }
        private void Print_Click(object sender, RoutedEventArgs e) { if (Ready()) ReportService.Print(ReportService.Build(_report), this); }
        private void New_Click(object sender, RoutedEventArgs e) { _report = null; ResultsList.ItemsSource = null; ReportViewer.Document = null; Progress.Value = 0; ProgressText.Text = "Ready"; Tabs.SelectedIndex = 0; StatusText.Text = "Ready"; }
        private bool Ready() { if (_report != null) return true; MessageBox.Show(this, "Complete an inspection first."); return false; }
    }
}
