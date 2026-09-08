using System;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace A2ZSysIns
{
    internal static class DarkReportPreviewService
    {
        private static readonly Brush Text = new SolidColorBrush(Color.FromRgb(211, 226, 216));
        private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(112, 132, 119));
        private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(127, 198, 154));
        private static readonly Brush Line = new SolidColorBrush(Color.FromRgb(25, 42, 31));
        private static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(2, 4, 2));

        public static FlowDocument Build(InspectionReport r)
        {
            EvidenceEngine.Log(r, "Dark report preview generated", "Dark technician preview built independently from printable PDF output.");
            var d = new FlowDocument
            {
                PagePadding = new Thickness(34), FontFamily = new FontFamily("Segoe UI"), FontSize = 10.5,
                Foreground = Text, Background = Brushes.Black, ColumnGap = 0, ColumnWidth = double.PositiveInfinity
            };
            d.Blocks.Add(new Paragraph(new Run("A2Z SYSTEM INSPECTOR")) { FontSize = 23, FontWeight = FontWeights.Bold, Foreground = Accent, Margin = new Thickness(0, 0, 0, 2) });
            d.Blocks.Add(new Paragraph(new Run("Computer Health Inspection Report - dark technician preview")) { FontSize = 12, Foreground = Muted, Margin = new Thickness(0, 0, 0, 14) });
            AddKey(d, "Inspection", r.InspectionId);
            AddKey(d, "Customer / reference", r.CustomerReference);
            AddKey(d, "Technician", r.Technician);
            AddKey(d, "Completed", r.CompletedAt == default(DateTime) ? "N/A" : r.CompletedAt.ToString("yyyy-MM-dd HH:mm"));
            AddKey(d, "Reported problem", r.ReportedProblem);

            d.Blocks.Add(new Paragraph(new Run(Safe(r.OverallStatus))) { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Accent, Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(10), Margin = new Thickness(0, 14, 0, 8) });
            d.Blocks.Add(Paragraph(Safe(r.CustomerSummary)));

            Heading(d, "Priority actions");
            if (r.PriorityActions.Count == 0) d.Blocks.Add(Paragraph("No immediate action was generated from the available evidence."));
            else foreach (var x in r.PriorityActions) d.Blocks.Add(Paragraph("- " + x));

            Heading(d, "Evidence summary");
            foreach (var s in r.Scores) AddEvidence(d, s.Category, s.Status, s.Reason);

            Heading(d, "Findings");
            if (r.Findings.Count == 0) d.Blocks.Add(Paragraph("No findings recorded."));
            foreach (var f in r.Findings)
            {
                var p = new Paragraph { Margin = new Thickness(0, 3, 0, 7), Padding = new Thickness(8), Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1) };
                p.Inlines.Add(new Run(Safe(f.Severity).ToUpperInvariant() + " - " + Safe(f.Title) + "\n") { FontWeight = FontWeights.Bold, Foreground = Accent });
                p.Inlines.Add(new Run(Safe(f.Explanation) + "\n"));
                p.Inlines.Add(new Run("Action: ") { FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(Safe(f.Recommendation) + "\n"));
                p.Inlines.Add(new Run("Confidence: " + Safe(f.Confidence) + " | Priority: " + Safe(f.ActionLevel)) { Foreground = Muted, FontSize = 9 });
                d.Blocks.Add(p);
            }

            Heading(d, "Unavailable / limitations");
            var unavailable = r.Measurements.Where(x => x.Status == "Unavailable" || x.Status == "Failed").ToList();
            if (unavailable.Count == 0 && r.Limitations.Count == 0) d.Blocks.Add(Paragraph("No collector explicitly reported an unavailable measurement."));
            else
            {
                foreach (var x in unavailable) d.Blocks.Add(Paragraph("- " + Safe(x.Target) + ": " + Safe(x.Reason)));
                foreach (var x in r.Limitations.Distinct()) d.Blocks.Add(Paragraph("- " + x));
            }

            Heading(d, "Technical evidence");
            foreach (var x in r.System) AddKey(d, x.Key, x.Value);
            Heading(d, "Storage");
            foreach (var x in r.Drives) AddEvidence(d, Safe(x.Model), Safe(x.Assessment), "SMART " + Safe(x.SmartStatus) + "; life indicator " + (x.RemainingLifePercent.HasValue ? x.RemainingLifePercent.Value.ToString("0") + "%" : "not measured"));
            Heading(d, "Windows events");
            foreach (var x in r.Events) AddEvidence(d, Safe(x.Source) + " / " + x.EventId, x.Count + " event(s)", Safe(x.Cause));
            Heading(d, "Measurement coverage");
            foreach (var x in r.Measurements) AddEvidence(d, Safe(x.Target), Safe(x.Status), Safe(x.Source) + " - " + Safe(x.Reason));

            d.Blocks.Add(new Paragraph(new Run("Preview only. The saved PDF uses a white printable layout.")) { Foreground = Muted, FontSize = 9, Margin = new Thickness(0, 18, 0, 0) });
            return d;
        }

        private static void Heading(FlowDocument d, string text) => d.Blocks.Add(new Paragraph(new Run(text)) { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Accent, Margin = new Thickness(0, 14, 0, 6) });
        private static Paragraph Paragraph(string text) => new Paragraph(new Run(Safe(text))) { Foreground = Text, Margin = new Thickness(1, 2, 1, 5) };
        private static void AddKey(FlowDocument d, string key, string value)
        {
            var p = new Paragraph { Margin = new Thickness(0, 1, 0, 3) };
            p.Inlines.Add(new Run(Safe(key) + ": ") { FontWeight = FontWeights.Bold, Foreground = Accent });
            p.Inlines.Add(new Run(Safe(value)) { Foreground = Text });
            d.Blocks.Add(p);
        }
        private static void AddEvidence(FlowDocument d, string category, string status, string reason)
        {
            var p = new Paragraph { Margin = new Thickness(0, 2, 0, 4), Padding = new Thickness(6), Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) };
            p.Inlines.Add(new Run(Safe(category)) { FontWeight = FontWeights.Bold, Foreground = Text });
            p.Inlines.Add(new Run("  [" + Safe(status) + "]  ") { Foreground = Accent, FontWeight = FontWeights.Bold });
            p.Inlines.Add(new Run(Safe(reason)) { Foreground = Muted });
            d.Blocks.Add(p);
        }
        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
    }
}
