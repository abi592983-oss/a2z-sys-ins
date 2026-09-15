using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace A2ZSysIns
{
    internal sealed class CpuStressGraphService
    {
        private readonly List<CpuStressSample> samples = new List<CpuStressSample>();

        public void Reset()
        {
            samples.Clear();
        }

        public void AddSample(CpuStressSample sample)
        {
            samples.Add(sample);
            if (samples.Count > 1200)
            {
                samples.RemoveAt(0);
            }
        }

        public void Render(Canvas canvas, double width, double height)
        {
            canvas.Children.Clear();
            if (canvas == null || samples.Count == 0) return;

            DrawUtilization(canvas, width, height);
            DrawThermalClock(canvas, width, height);
        }

        private void DrawUtilization(Canvas canvas, double width, double height)
        {
            var plot = new Rect(42, 12, Math.Max(10, width - 56), Math.Max(10, height - 30));
            DrawGrid(canvas, plot);

            DrawSeries(canvas, plot, samples.Select(s => s.CpuLoadPercent), 0, 100, Brushes.LimeGreen, true);
            DrawSeries(canvas, plot, samples.Select(s => s.MemoryUsedPercent), 0, 100, Brushes.DeepSkyBlue, false);
            DrawSeries(canvas, plot, samples.Select(s => s.GpuLoadPercent), 0, 100, Brushes.Orange, false);
            AddLegend(canvas, plot.Left, plot.Top, "CPU", "RAM", "GPU");
        }

        private void DrawThermalClock(Canvas canvas, double width, double height)
        {
            var plot = new Rect(42, 12, Math.Max(10, width - 56), Math.Max(10, height - 30));
            DrawGrid(canvas, plot);

            var temperatures = samples.Where(s => s.CpuTemperatureC.HasValue).Select(s => s.CpuTemperatureC.Value).ToList();
            if (temperatures.Count > 0)
            {
                var min = Math.Max(0, Math.Floor(temperatures.Min() - 5));
                var max = Math.Max(min + 10, Math.Ceiling(temperatures.Max() + 5));
                DrawSeries(canvas, plot, samples.Select(s => s.CpuTemperatureC), min, max, Brushes.OrangeRed, true);
            }

            var clocks = samples.Where(s => s.CpuClockMHz.HasValue).Select(s => s.CpuClockMHz.Value).ToList();
            if (clocks.Count > 0)
            {
                var min = Math.Max(0, Math.Floor(clocks.Min() - 100));
                var max = Math.Max(min + 500, Math.Ceiling(clocks.Max() + 100));
                DrawSeries(canvas, plot, samples.Select(s => s.CpuClockMHz), min, max, Brushes.DeepSkyBlue, false);
            }

            AddLegend(canvas, plot.Left, plot.Top, "TEMP", "CLOCK", "");
        }

        private void DrawGrid(Canvas canvas, Rect plot)
        {
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Top + plot.Height * i / 4.0;
                canvas.Children.Add(new Line
                {
                    X1 = plot.Left,
                    X2 = plot.Right,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(45, 130, 155, 170)),
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                });
            }
        }

        private void DrawSeries(Canvas canvas, Rect plot, IEnumerable<double?> values, double min, double max, Brush stroke, bool first)
        {
            var indexed = values.Select((value, index) => new { value, index })
                .Where(x => x.value.HasValue)
                .Select(x => new { Value = x.value.Value, Index = x.index })
                .ToList();

            if (indexed.Count < 2) return;

            var firstCaptured = samples[0].CapturedAt;
            var lastCaptured = samples[samples.Count - 1].CapturedAt;
            var totalSeconds = Math.Max(1.0, (lastCaptured - firstCaptured).TotalSeconds);
            var plotW = Math.Max(1.0, plot.Width);
            var plotH = Math.Max(1.0, plot.Height);
            var top = plot.Top;
            var left = plot.Left;
            var points = new List<Point>(indexed.Count);

            foreach (var item in indexed)
            {
                var sample = samples[item.Index];
                var elapsedSeconds = Math.Max(0, (sample.CapturedAt - firstCaptured).TotalSeconds);
                var x = left + plotW * elapsedSeconds / totalSeconds;
                var ratio = (item.Value - min) / Math.Max(0.0001, max - min);
                ratio = Math.Max(0, Math.Min(1, ratio));
                var y = top + plotH * (1 - ratio);
                points.Add(new Point(x, y));
            }

            if (points.Count < 2) return;

            // Smooth the display only. Raw samples remain unchanged in the report/log.
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], false, false);
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var p0 = i == 0 ? points[i] : points[i - 1];
                    var p1 = points[i];
                    var p2 = points[i + 1];
                    var p3 = i + 2 < points.Count ? points[i + 2] : p2;
                    var c1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                    var c2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);
                    context.BezierTo(c1, c2, p2, true, false);
                }
            }
            geometry.Freeze();
            canvas.Children.Add(new Path
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = first ? 2.2 : 1.7,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            });
        }

        private static void AddLegend(Canvas canvas, double x, double y, string a, string b, string c)
        {
            var labels = new[] { a, b, c };
            var brushes = new[] { Brushes.LimeGreen, Brushes.DeepSkyBlue, Brushes.Orange };
            var offset = 0.0;
            for (var i = 0; i < labels.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(labels[i])) continue;
                canvas.Children.Add(new Rectangle
                {
                    Width = 10,
                    Height = 3,
                    Fill = brushes[i],
                    Margin = new Thickness(x + offset, y, 0, 0)
                });
                var text = new TextBlock
                {
                    Text = labels[i],
                    Foreground = Brushes.LightGray,
                    FontSize = 9,
                    Margin = new Thickness(x + offset + 14, y - 5, 0, 0)
                };
                Canvas.SetLeft(text, x + offset + 14);
                Canvas.SetTop(text, y - 5);
                canvas.Children.Add(text);
                offset += 48;
            }
        }
    }
}
