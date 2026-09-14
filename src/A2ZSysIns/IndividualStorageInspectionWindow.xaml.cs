using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace A2ZSysIns
{
    public partial class IndividualStorageInspectionWindow : Window
    {
        private readonly InspectionReport _report;
        private IndividualStorageInspectionService.Target _selectedTarget;

        public IndividualStorageInspectionWindow(InspectionReport report)
        {
            InitializeComponent();
            _report = report;
            LoadTargets();
        }

        private void LoadTargets()
        {
            DriveSelector.ItemsSource = IndividualStorageInspectionService.GetTargets(_report);
            if (DriveSelector.Items.Count > 0) DriveSelector.SelectedIndex = 0;
            else
            {
                InspectButton.IsEnabled = false;
                DeviceText.Text = "No physical storage targets are available.";
                HealthText.Text = "Run a full inspection first so storage evidence can be acquired.";
            }
        }

        private void DriveSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedTarget = DriveSelector.SelectedItem as IndividualStorageInspectionService.Target;
            if (_selectedTarget == null) return;
            DeviceText.Text = _selectedTarget.DisplayName;
            IdentityText.Text = "Type: " + _selectedTarget.DeviceType + "   Transport: " + _selectedTarget.Transport + "   Capacity: " + FormatBytes(_selectedTarget.SizeBytes) + "\nSerial: " + (string.IsNullOrWhiteSpace(_selectedTarget.Serial) ? "Unavailable" : _selectedTarget.Serial);
            HealthText.Text = "Press INSPECT SELECTED DRIVE to interpret the acquired evidence for this device.";
            EnduranceText.Text = "Not inspected yet.";
            TemperatureText.Text = "Not inspected yet.";
            LimitationsText.Text = "No focused inspection has been performed yet.";
        }

        private void InspectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTarget == null) return;
            var result = IndividualStorageInspectionService.Inspect(_report, _selectedTarget);
            if (!result.Success || result.Drive == null || result.Health == null)
            {
                MessageBox.Show(this, result.FailureReason ?? "Focused storage inspection failed.", "A2Z System Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var drive = result.Drive;
            var health = result.Health;
            DeviceText.Text = health.Condition;
            HealthText.Text = "Condition: " + health.Condition + "\nCondition confidence: " + health.ConditionConfidence + "\nReason: " + health.Reason;
            EnduranceText.Text = health.Endurance + "\nConfidence: " + health.EnduranceConfidence + "\nMeaning: " + (drive.LifeMeaning ?? "No validated life meaning available.");
            var temp = drive.TemperatureC.HasValue ? drive.TemperatureC.Value.ToString("0.0") + " °C" : "Unavailable";
            var controller = drive.ControllerTemperatureC.HasValue ? drive.ControllerTemperatureC.Value.ToString("0.0") + " °C" : "Unavailable";
            TemperatureText.Text = "Drive: " + temp + "\nController: " + controller + "\nSource: " + (drive.SmartDataSource ?? "Unknown");

            var unavailable = drive.StorageEvidence == null || drive.StorageEvidence.UnavailableFields == null
                ? "No unavailable-field list was supplied."
                : (drive.StorageEvidence.UnavailableFields.Count == 0 ? "No explicitly unavailable fields were reported." : string.Join("\n", drive.StorageEvidence.UnavailableFields.Select(x => "• " + x)));
            LimitationsText.Text = unavailable + "\n\nSMART/evidence quality: " + (drive.StorageEvidence == null ? "Unknown" : drive.StorageEvidence.Quality) + "\nSerial validated: " + (drive.StorageEvidence != null && drive.StorageEvidence.SerialValidated ? "Yes" : "No / not established");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "Unknown capacity";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double value = bytes;
            var index = 0;
            while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
            return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00") + " " + units[index];
        }
    }
}
