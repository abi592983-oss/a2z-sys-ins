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

            ApplyResponsiveDeviceProfile();

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

        private void ApplyResponsiveDeviceProfile()
        {
            var placeholder = FindVisualText(this, "PC");
            if (placeholder == null) return;

            var host = placeholder.Parent as Border;
            if (host == null) return;

            var cardGrid = host.Child as Grid;
            if (cardGrid != null && cardGrid.ColumnDefinitions.Count >= 2)
            {
                cardGrid.ColumnDefinitions[0].Width = new GridLength(0.42, GridUnitType.Star);
                cardGrid.ColumnDefinitions[1].Width = new GridLength(0.58, GridUnitType.Star);
            }

            var profile = DetectDeviceProfile();
            var icon = BuildDeviceIcon(profile);
            host.Child = icon;
            host.HorizontalContentAlignment = HorizontalAlignment.Center;
            host.VerticalContentAlignment = VerticalAlignment.Center;
            host.Padding = new Thickness(18);
        }

        private string DetectDeviceProfile()
        {
            var system = _report == null ? null : _report.System;
            var model = system != null && system.ContainsKey("Model") ? system["Model"] : "";
            var manufacturer = system != null && system.ContainsKey("Manufacturer") ? system["Manufacturer"] : "";
            var text = (manufacturer + " " + model).ToLowerInvariant();

            if (text.Contains("tablet") || text.Contains("surface pro") || text.Contains("ipad") || text.Contains("tab "))
                return "tablet";
            if (text.Contains("notebook") || text.Contains("laptop") || text.Contains("portable") || text.Contains("pavilion") || text.Contains("thinkpad") || text.Contains("latitude") || text.Contains("inspiron") || text.Contains("elitebook") || text.Contains("probook") || text.Contains("vivobook") || text.Contains("ideapad") || text.Contains("zenbook"))
                return "laptop";
            if (text.Contains("all-in-one") || text.Contains("all in one") || text.Contains("aio"))
                return "aio";
            return "desktop";
        }

        private FrameworkElement BuildDeviceIcon(string profile)
        {
            var viewBox = new Viewbox { Stretch = Stretch.Uniform, Width = 150, Height = 120 };
            var canvas = new Canvas { Width = 150, Height = 120 };
            var stroke = (Brush)FindResource("Blue");
            var muted = (Brush)FindResource("Border");

            if (profile == "laptop")
            {
                canvas.Children.Add(new Rectangle { Width = 92, Height = 58, RadiusX = 5, RadiusY = 5, Stroke = stroke, StrokeThickness = 5, Fill = (Brush)FindResource("Panel"), Canvas.Left = 29, Canvas.Top = 12 });
                canvas.Children.Add(new Rectangle { Width = 76, Height = 42, Fill = (Brush)FindResource("Bg"), Canvas.Left = 37, Canvas.Top = 20 });
                canvas.Children.Add(new Polygon { Points = new PointCollection { new Point(18, 79), new Point(132, 79), new Point(143, 91), new Point(7, 91) }, Fill = muted, Stroke = stroke, StrokeThickness = 4 });
                canvas.Children.Add(new Rectangle { Width = 28, Height = 3, Fill = stroke, Canvas.Left = 61, Canvas.Top = 83 });
            }
            else if (profile == "tablet")
            {
                canvas.Children.Add(new Rectangle { Width = 82, Height = 104, RadiusX = 9, RadiusY = 9, Stroke = stroke, StrokeThickness = 5, Fill = (Brush)FindResource("Panel"), Canvas.Left = 34, Canvas.Top = 6 });
                canvas.Children.Add(new Rectangle { Width = 66, Height = 82, Fill = (Brush)FindResource("Bg"), Canvas.Left = 42, Canvas.Top = 14 });
                canvas.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = stroke, Canvas.Left = 72, Canvas.Top = 96 });
            }
            else
            {
                canvas.Children.Add(new Rectangle { Width = 92, Height = 68, RadiusX = 4, RadiusY = 4, Stroke = stroke, StrokeThickness = 5, Fill = (Brush)FindResource("Panel"), Canvas.Left = 29, Canvas.Top = 6 });
                canvas.Children.Add(new Rectangle { Width = 76, Height = 52, Fill = (Brush)FindResource("Bg"), Canvas.Left = 37, Canvas.Top = 14 });
                canvas.Children.Add(new Line { X1 = 75, Y1 = 74, X2 = 75, Y2 = 89, Stroke = stroke, StrokeThickness = 5 });
                canvas.Children.Add(new Polygon { Points = new PointCollection { new Point(48, 91), new Point(102, 91), new Point(111, 97), new Point(39, 97) }, Fill = muted, Stroke = stroke, StrokeThickness = 4 });
                if (profile == "aio")
                    canvas.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = stroke, Canvas.Left = 71.5, Canvas.Top = 61 });
            }

            viewBox.Child = canvas;
            return viewBox;
        }

        private static TextBlock FindVisualText(DependencyObject root, string text)
        {
            if (root == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var block = child as TextBlock;
                if (block != null && string.Equals(block.Text, text, StringComparison.Ordinal)) return block;
                var nested = FindVisualText(child, text);
                if (nested != null) return nested;
            }
            return null;
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