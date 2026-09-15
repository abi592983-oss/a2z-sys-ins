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
        internal static Canvas BuildUtilizationGraph(IReadOnlyList<CpuStressSample> samples, double width, double height)
        {
            var canvas = CreateCanvas(width, height);
            if (samples == null || samples.Count < 2) return canvas;
            AddSeries(canvas, samples, s => s.CpuLoadPercent, 0, 100, 40, 15, Math.Max(1, width - 55), Math.Max(1, height - 40), Brushes.LimeGreen, "CPU", true);
            AddSeries(canvas, samples, s => s.MemoryUsedPercent, 0, 100, 40, 15, Math.Max(1, width - 55), Math.Max(1, height - 40), Brushes.DeepSkyBlue, "RAM", false);
            AddSeries(canvas, samples, s => s.GpuLoadPercent, 0, 100, 40, 15, Math.Max(1, width - 55), Math.Max(1, height - 40), Brushes.Orange, "GPU", false);
            AddLegend(canvas, 40, height - 20, "CPU", "RAM", "GPU");
            return canvas;
        }

        internal static Canvas BuildThermalClockGraph(IReadOnlyList<CpuStressSample> samples, double width, double height)
        {
            var canvas = CreateCanvas(width, height);
            if (samples == null || samples.Count < 2) return canvas;
            var temps = samples.Where(s => s.CpuTemperatureC.HasValue).Select(s => s.CpuTemperatureC.Value).ToList();
            var clocks = samples.Where(s => s.AverageCoreClockMHz.HasValue).Select(s => s.AverageCoreClockMHz.Value).ToList();
            var minTemp = temps.Count == 0 ? 0 : Math.Floor(temps.Min() - 2);
            var maxTemp = temps.Count == 0 ? 100 : Math.Ceiling(temps.Max() + 2);
            if (maxTemp <= minTemp) maxTemp = minTemp + 1;
            var minClock = clocks.Count == 0 ? 0 : Math.Floor(clocks.Min() * 0.95);
            var maxClock = clocks.Count == 0 ? 100 : Math.Ceiling(clocks.Max() * 1.05);
            if (maxClock <= minClock) maxClock = minClock + 1;
            AddSeries(canvas, samples, s => s.CpuTemperatureC, minTemp, maxTemp, 40, 15, Math.Max(1, width - 55), Math.Max(1, height - 40), Brushes.OrangeRed, "Temperature", true);
            AddSeries(canvas, samples, s => s.AverageCoreClockMHz, minClock, maxClock, 40, 15, Math.Max(1, width - 55), Math.Max(1, height - 40), Brushes.DeepSkyBlue, "Clock", false);
            AddLegend(canvas, 40, height - 20, "Temperature", "Clock", "");
            return canvas;
        }

        private static Canvas CreateCanvas(double width, double height)
        {
            return new Canvas { Width = width, Height = height, Background = Brushes.Transparent, ClipToBounds = true };
        }

        private static void AddSeries(Canvas canvas, IReadOnlyList<CpuStressSample> samples, Func<CpuStressSample, double?> selector,
            double min, double max, double left, double top, double plotW, double plotH, Brush stroke, string label, bool first)
        {
            var valid = samples.Select((s, i) => new { Sample = s, Index = i, Value = selector(s) }).Where(x => x.Value.HasValue).ToList();
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
            if (points.Count >= 2) canvas.Children.Add(new Polyline { Points = points, Stroke = stroke, StrokeThickness = first ? 2.0 : 1.5, SnapsToDevicePixels = true });
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
                Canvas.SetLeft(t, x + offset); Canvas.SetTop(t, y); canvas.Children.Add(t);
                offset += 70;
            }
        }
    }
}
