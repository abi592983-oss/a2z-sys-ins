using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace A2ZSysIns
{
    public partial class MainWindow
    {
        private DispatcherTimer _dashboardFixTimer;

        static MainWindow()
        {
            EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnDashboardWindowLoaded));
        }

        private static void OnDashboardWindowLoaded(object sender, RoutedEventArgs e)
        {
            var window = (MainWindow)sender;
            if (window._dashboardFixTimer != null) return;
            window._dashboardFixTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            window._dashboardFixTimer.Tick += (s, args) => window.ApplyDashboardPresentationFixes();
            window._dashboardFixTimer.Start();
            window.ApplyDashboardPresentationFixes();
        }

        private void ApplyDashboardPresentationFixes()
        {
            if (_report == null) return;

            var health = _report.CustomerHealth ?? new CustomerHealthSummary();
            OverallScoreText.Text = _report.OverallScore.HasValue ? _report.OverallScore.Value.ToString() : "—";
            var scoreParent = OverallScoreText.Parent as StackPanel;
            if (scoreParent != null && scoreParent.Children.Count > 1 && scoreParent.Children[1] is TextBlock scoreSuffix)
                scoreSuffix.Text = _report.OverallScore.HasValue ? "/ 100" : "STATUS";
            var scoreGrid = scoreParent == null ? null : scoreParent.Parent as Grid;
            if (scoreGrid != null)
            {
                var rings = scoreGrid.Children.OfType<Ellipse>().ToList();
                if (rings.Count > 1) rings[1].Stroke = StatusBrush(health.OverallStatus);
            }

            var drive = _report.Drives == null ? null : _report.Drives.FirstOrDefault();
            if (drive == null)
                StorageValueText.Text = "Not detected";
            else if (drive.RemainingLifePercent.HasValue)
                StorageValueText.Text = Math.Round(drive.RemainingLifePercent.Value) + "% life";
            else if (drive.SmartPassed == false)
                StorageValueText.Text = "SMART FAIL";
            else if (drive.SmartAttributes != null && drive.SmartAttributes.Count > 0)
                StorageValueText.Text = "SMART OK";
            else
                StorageValueText.Text = "Detected";

            if (_report.Events == null || _report.Events.Count == 0)
            {
                RecentEventsList.ItemsSource = null;
            }
            else
            {
                RecentEventsList.ItemsSource = _report.Events
                    .OrderByDescending(x => x.Latest)
                    .Take(6)
                    .Select(x => new DashboardEventItem
                    {
                        Summary = string.IsNullOrWhiteSpace(x.Summary)
                            ? (string.IsNullOrWhiteSpace(x.Source) ? "Windows event" : x.Source) + " Event " + x.EventId + " (" + x.Count + ")"
                            : x.Summary
                    })
                    .ToList();
            }

            var samples = _report.CpuStressTest == null ? null : _report.CpuStressTest.Samples;
            if (samples == null || samples.Count == 0)
            {
                DashboardStressGraph.Children.Clear();
                var panel = new StackPanel { Margin = new Thickness(12, 10, 12, 6) };
                panel.Children.Add(new TextBlock
                {
                    Text = "CPU sensor snapshot",
                    Foreground = (Brush)FindResource("Text"),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                var temperature = CpuTemperatureForDashboard();
                var load = CpuLoadForDashboard();
                panel.Children.Add(new TextBlock
                {
                    Text = temperature.HasValue ? "Temperature: " + Math.Round(temperature.Value, 0) + " °C" : "Temperature: not measured",
                    Foreground = temperature.HasValue ? (Brush)FindResource("Green") : (Brush)FindResource("Muted"),
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 2)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = load.HasValue ? "CPU load: " + Math.Round(load.Value, 0) + "%" : "CPU load: not measured",
                    Foreground = load.HasValue ? (Brush)FindResource("Blue") : (Brush)FindResource("Muted"),
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 2)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = "Time-series graph is available after the optional CPU staged test.",
                    Foreground = (Brush)FindResource("Muted"),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                DashboardStressGraph.Children.Add(panel);
            }
        }

        private double? CpuTemperatureForDashboard()
        {
            if (_report == null || _report.Sensors == null) return null;
            var sensors = _report.Sensors
                .Where(EvidenceEngine.ActualTemperature)
                .Where(x => (x.Hardware ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (x.Name ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.Name == "Core Max" || x.Name == "Core Average")
                .ToList();
            return sensors.Count == 0 ? (double?)null : sensors.Max(x => Convert.ToDouble(x.Current));
        }

        private double? CpuLoadForDashboard()
        {
            if (_report == null || _report.Sensors == null) return null;
            var sensors = _report.Sensors
                .Where(x => (x.Name ?? "").IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (x.Type ?? "").IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(x => x.Current.HasValue)
                .ToList();
            return sensors.Count == 0 ? (double?)null : sensors.Max(x => Convert.ToDouble(x.Current));
        }

        private sealed class DashboardEventItem
        {
            public string Summary { get; set; }
        }
    }
}