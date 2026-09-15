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
            var left = 48.0; var right = 10.0; var top = 22.0; var bottom = 24.0;
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
            AddSeries(canvas, samples, a, min, max, left, top, plotW, plotH, Brushes.LimeGreen, aLabel, true);
            if (b != null) AddSeries(canvas, samples, b, min, max, left, top, plotW, plotH, Brushes.DeepSkyBlue, bLabel, false);
            if (c != null) AddSeries(canvas, samples, c, min, max, left, top, plotW, plotH, Brushes.Orange, cLabel, false);
            AddLegend(canvas, left + plotW - 210, top + 4, aLabel, bLabel, cLabel);
        }

        private static void DrawSecondary(Canvas canvas, IList<CpuStressSample> samples, double max, Func<CpuStressSample, double?> value, string label)
        {
            var width = Math.Max(320, double.IsNaN(canvas.ActualWidth) || canvas.ActualWidth <= 0 ? canvas.Width : canvas.ActualWidth);
            var height = Math.Max(140, canvas.Height);
            var left = 48.0; var right = 10.0; var top = 22.0; var bottom = 24.0;
            var plotW = Math.Max(1, width - left - right); var plotH = Math.Max(1, height - top - bottom);
            AddSeries(canvas, samples, value, 0, max, left, top, plotW, plotH, Brushes.Cyan, label, false);
        }

        private static void AddSeries(Canvas canvas, IList<CpuStressSample> samples, Func<CpuStressSample, double?> selector,
            double min, double max, double left, double top, double plotW, double plotH, Brush stroke, string label, bool first)
        {
            if (samples == null) return;
            var valid = samples.Select((s, i) => new { Index = i, Value = selector(s) }).Where(x => x.Value.HasValue).ToList();
            if (valid.Count == 0) return;
            var total = Math.Max(1, samples.Count - 1);
            var points = new PointCollection();
            foreach (var item in valid)
            {
                var x = left + plotW * item.Index / total;
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
    }
}
