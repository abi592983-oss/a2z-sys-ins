using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace A2ZSysIns
{
    internal static class CpuStressGraphService
    {
        // Legacy entry points are retained because the live report/window code uses them.
        public static void DrawUtilization(Canvas canvas, IList<CpuStressSample> samples)
        {
            Draw(canvas, samples, 0, 100, "CPU / RAM / GPU utilization",
                s => s.ObservedCpuLoadPercent, s => s.MemoryUsedPercent, s => s.GpuLoadPercent,
                "CPU", "RAM", "GPU");
        }

        public static void DrawThermalClock(Canvas canvas, IList<CpuStressSample> samples)
        {
            var validClocks = samples == null
                ? new List<double>()
                : samples.Where(s => s.AverageCoreClockMHz.HasValue).Select(s => s.AverageCoreClockMHz.Value).ToList();
            var maxClock = validClocks.Count == 0 ? 1000 : Math.Max(1000, Math.Ceiling(validClocks.Max() / 500.0) * 500.0);
            Draw(canvas, samples, 0, 100, "CPU temperature / clock (clock scaled separately)",
                s => s.TemperatureC, null, null, "Temp °C", null, null, maxClock);
            DrawSecondary(canvas, samples, maxClock, s => s.AverageCoreClockMHz, "Clock MHz");
        }

        public static FrameworkElement BuildReportGraph(InspectionReport report)
        {
            var root = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            if (report == null || report.CpuStressTest == null || report.CpuStressTest.Samples == null || report.CpuStressTest.Samples.Count == 0)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "CPU stress graphs: not available because no actual stress samples were captured.",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 8)
                });
                return root;
            }

            var a = BuildUtilizationGraph(report.CpuStressTest.Samples, 720, 180);
            var b = BuildThermalClockGraph(report.CpuStressTest.Samples, 720, 180);
            a.Margin = new Thickness(0, 2, 0, 8);
            b.Margin = new Thickness(0, 2, 0, 8);
            root.Children.Add(a);
            root.Children.Add(b);
            root.Children.Add(BuildCadenceSummary(report.CpuStressTest.Samples));
            return root;
        }

        internal static Canvas BuildUtilizationGraph(IReadOnlyList<CpuStressSample> samples, double width, double height)
        {
            var canvas = CreateCanvas(width, height);
            if (samples == null || samples.Count < 2) return canvas;
            Draw(canvas, samples.ToList(), 0, 100, "CPU / RAM / GPU utilization",
                s => s.ObservedCpuLoadPercent, s => s.MemoryUsedPercent, s => s.GpuLoadPercent,
                "CPU", "RAM", "GPU");
            return canvas;
        }

        internal static Canvas BuildThermalClockGraph(IReadOnlyList<CpuStressSample> samples, double width, double height)
        {
            var canvas = CreateCanvas(width, height);
            if (samples == null || samples.Count < 2) return canvas;
            var validClocks = samples.Where(s => s.AverageCoreClockMHz.HasValue).Select(s => s.AverageCoreClockMHz.Value).ToList();
            var maxClock = validClocks.Count == 0 ? 1000 : Math.Max(1000, Math.Ceiling(validClocks.Max() / 500.0) * 500.0);
            Draw(canvas, samples.ToList(), 0, 100, "CPU temperature / clock (clock scaled separately)",
                s => s.TemperatureC, null, null, "Temp °C", null, null, maxClock);
            DrawSecondary(canvas, samples.ToList(), maxClock, s => s.AverageCoreClockMHz, "Clock MHz");
            return canvas;
        }

        private static TextBlock BuildCadenceSummary(IList<CpuStressSample> samples)
        {
            var ordered = (samples ?? new List<CpuStressSample>()).OrderBy(s => s.CapturedAt).ToList();
            var intervals = new List<double>();
            for (var i = 1; i < ordered.Count; i++)
            {
                var interval = (ordered[i].CapturedAt - ordered[i - 1].CapturedAt).TotalMilliseconds;
                if (interval >= 1 && interval <= 10000) intervals.Add(interval);
            }

            var median = intervals.Count == 0 ? (double?)null : Median(intervals);
            var first = ordered.Count == 0 ? (DateTime?)null : ordered[0].CapturedAt;
            var last = ordered.Count == 0 ? (DateTime?)null : ordered[ordered.Count - 1].CapturedAt;
            var spanSeconds = first.HasValue && last.HasValue ? Math.Max(0, (last.Value - first.Value).TotalSeconds) : 0;
            var pollValues = ordered.Where(s => s.TelemetryPollIntervalMilliseconds > 0)
                .Select(s => s.TelemetryPollIntervalMilliseconds).Distinct().OrderBy(x => x).ToArray();
            var pollText = pollValues.Length == 0 ? "not recorded" : string.Join(", ", pollValues.Select(x => x + " ms"));
            var captureText = median.HasValue ? median.Value.ToString("0") + " ms median" : "not available";

            return new TextBlock
            {
                Text = "Actual captured span: " + spanSeconds.ToString("0.0") + " s  •  actual capture interval: " + captureText + "  •  telemetry polling target: " + pollText + "  •  plotted points: " + ordered.Count,
                Foreground = Brushes.Gray,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 2, 6)
            };
        }

        private static Canvas CreateCanvas(double width, double height)
        {
            return new Canvas { Width = width, Height = height, Background = Brushes.Black, ClipToBounds = true };
        }

        private static void Draw(Canvas canvas, IList<CpuStressSample> samples, double min, double max, string title,
            Func<CpuStressSample, double?> a, Func<CpuStressSample, double?> b, Func<CpuStressSample, double?> c,
            string aLabel, string bLabel, string cLabel, double secondaryMax = 0)
        {
            canvas.Children.Clear();
            var width = Math.Max(320, double.IsNaN(canvas.ActualWidth) || canvas.ActualWidth <= 0 ? canvas.Width : canvas.ActualWidth);
            var height = Math.Max(140, canvas.Height);
            var left = 48.0; var right = 10.0; var top = 22.0; var bottom = 30.0;
            var plotW = Math.Max(1, width - left - right); var plotH = Math.Max(1, height - top - bottom);
            canvas.Children.Add(new Rectangle { Width = width, Height = height, Fill = Brushes.Black });
            var titleText = new TextBlock { Text = title, Foreground = Brushes.LightGray, FontWeight = FontWeights.SemiBold, FontSize = 12 };
            Canvas.SetLeft(titleText, left); Canvas.SetTop(titleText, 2); canvas.Children.Add(titleText);
            for (var i = 0; i <= 4; i++)
            {
                var y = top + plotH * i / 4.0;
                canvas.Children.Add(new Line { X1 = left, X2 = left + plotW, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(35, 45, 38)), StrokeThickness = 1 });
                var label = new TextBlock { Text = (max - (max - min) * i / 4.0).ToString("0"), Foreground = Brushes.Gray, FontSize = 9 };
                Canvas.SetLeft(label, 4); Canvas.SetTop(label, y - 7); canvas.Children.Add(label);
            }
            AddTimeAxis(canvas, samples, left, top + plotH, plotW);
            AddSeries(canvas, samples, a, min, max, left, top, plotW, plotH, Brushes.LimeGreen, aLabel, true);
            if (b != null) AddSeries(canvas, samples, b, min, max, left, top, plotW, plotH, Brushes.DeepSkyBlue, bLabel, false);
            if (c != null) AddSeries(canvas, samples, c, min, max, left, top, plotW, plotH, Brushes.Orange, cLabel, false);
            AddLegend(canvas, left + plotW - 210, top + 4, aLabel, bLabel, cLabel);
        }

        private static void AddTimeAxis(Canvas canvas, IList<CpuStressSample> samples, double left, double bottomY, double plotW)
        {
            if (samples == null || samples.Count == 0) return;
            var first = samples[0].CapturedAt;
            var last = samples[samples.Count - 1].CapturedAt;
            var span = Math.Max(0.001, (last - first).TotalSeconds);
            for (var i = 0; i <= 4; i++)
            {
                var seconds = span * i / 4.0;
                var x = left + plotW * i / 4.0;
                var tick = new Line { X1 = x, X2 = x, Y1 = bottomY, Y2 = bottomY + 3, Stroke = Brushes.Gray, StrokeThickness = 1 };
                canvas.Children.Add(tick);
                var label = new TextBlock { Text = seconds.ToString("0.0") + "s", Foreground = Brushes.Gray, FontSize = 9 };
                Canvas.SetLeft(label, x - 12); Canvas.SetTop(label, bottomY + 4); canvas.Children.Add(label);
            }
        }

        private static void DrawSecondary(Canvas canvas, IList<CpuStressSample> samples, double max, Func<CpuStressSample, double?> value, string label)
        {
            var width = Math.Max(320, double.IsNaN(canvas.ActualWidth) || canvas.ActualWidth <= 0 ? canvas.Width : canvas.ActualWidth);
            var height = Math.Max(140, canvas.Height);
            var left = 48.0; var right = 10.0; var top = 22.0; var bottom = 30.0;
            var plotW = Math.Max(1, width - left - right); var plotH = Math.Max(1, height - top - bottom);
            AddSeries(canvas, samples, value, 0, max, left, top, plotW, plotH, Brushes.Cyan, label, false);
        }

        private static void AddSeries(Canvas canvas, IList<CpuStressSample> samples, Func<CpuStressSample, double?> selector,
            double min, double max, double left, double top, double plotW, double plotH, Brush stroke, string label, bool first)
        {
            if (samples == null) return;
            var valid = samples.Select((s, i) => new { Index = i, Sample = s, Value = selector(s) }).Where(x => x.Value.HasValue).ToList();
            if (valid.Count == 0) return;
            var firstCaptured = samples[0].CapturedAt;
            var lastCaptured = samples[samples.Count - 1].CapturedAt;
            var totalSeconds = Math.Max(0.001, (lastCaptured - firstCaptured).TotalSeconds);
            var points = new PointCollection();
            foreach (var item in valid)
            {
                var elapsedSeconds = Math.Max(0, (item.Sample.CapturedAt - firstCaptured).TotalSeconds);
                var x = left + plotW * elapsedSeconds / totalSeconds;
                var ratio = (item.Value.Value - min) / Math.Max(0.0001, max - min);
                ratio = Math.Max(0, Math.Min(1, ratio));
                var y = top + plotH * (1 - ratio);
                points.Add(new Point(x, y));
            }
            if (points.Count >= 2)
                canvas.Children.Add(new Polyline { Points = points, Stroke = stroke, StrokeThickness = first ? 2.0 : 1.5, SnapsToDevicePixels = true });
        }

        private static void AddLegend(Canvas canvas, double x, double y, string a, string b, string c)
        {
            var labels = new[] { a, b, c };
            var brushes = new[] { Brushes.LimeGreen, Brushes.DeepSkyBlue, Brushes.Orange };
            var offset = 0.0;
            for (var i = 0; i < labels.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(labels[i])) continue;
                var t = new TextBlock { Text = labels[i], Foreground = brushes[i], FontSize = 9, Margin = new Thickness(4, 0, 4, 0) };
                Canvas.SetLeft(t, x + offset);
                Canvas.SetTop(t, y);
                canvas.Children.Add(t);
                offset += 55;
            }
        }

        private static double Median(IList<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0) return 0;
            var middle = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0 : sorted[middle];
        }
    }
}
