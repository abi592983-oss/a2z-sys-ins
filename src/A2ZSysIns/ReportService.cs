using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace A2ZSysIns
{
    public static class ReportService
    {
        public static FlowDocument Build(InspectionReport r)
        {
            var d = new FlowDocument { PagePadding = new Thickness(55), FontFamily = new FontFamily("Segoe UI"), FontSize = 11, ColumnGap = 0, ColumnWidth = double.PositiveInfinity };
            d.Blocks.Add(new Paragraph(new Run("A2Z SYSTEM INSPECTOR")) { FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 42, 67)), Margin = new Thickness(0, 0, 0, 2) });
            d.Blocks.Add(new Paragraph(new Run("Computer Health Inspection Report")) { FontSize = 14, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 18) });
            d.Blocks.Add(Heading("Inspection summary"));
            d.Blocks.Add(KeyValues(new[] { "Inspection ID", r.InspectionId, "Customer / reference", Empty(r.CustomerReference), "Job number", Empty(r.JobNumber), "Technician", Empty(r.Technician), "Date", r.CompletedAt.ToString("yyyy-MM-dd HH:mm"), "Reported problem", Empty(r.ReportedProblem) }));
            var scoreText = r.OverallScore.HasValue ? r.OverallScore + "/100 — " + r.OverallStatus : "N/A — insufficient data";
            d.Blocks.Add(new Paragraph(new Run(scoreText)) { FontSize = 20, FontWeight = FontWeights.SemiBold, Background = StatusBrush(r.OverallStatus), Padding = new Thickness(10), Margin = new Thickness(0, 16, 0, 12) });
            d.Blocks.Add(Heading("Category results")); var scoreTable = NewTable("Category", "Score", "Status", "Reason"); foreach (var s in r.Scores) Row(scoreTable, s.Category, s.Score.HasValue ? s.Score + "/100" : "N/A", s.Status, s.Reason); d.Blocks.Add(scoreTable);
            d.Blocks.Add(Heading("Findings and recommendations")); if (r.Findings.Count == 0) d.Blocks.Add(new Paragraph(new Run("No significant findings were produced by the available checks."))); foreach (var f in r.Findings) d.Blocks.Add(new Paragraph(new Run(f.Severity + " — " + f.Title + "\n") { FontWeight = FontWeights.Bold }) { Inlines = { new Run(f.Explanation + " Recommendation: " + f.Recommendation) } });
            d.Blocks.Add(new Paragraph(new Run("Technical details")) { BreakPageBefore = true, FontSize = 18, FontWeight = FontWeights.Bold });
            d.Blocks.Add(Heading("System information")); var sys = NewTable("Item", "Value"); foreach (var x in r.System) Row(sys, x.Key, x.Value); d.Blocks.Add(sys);
            d.Blocks.Add(Heading("Physical storage")); var drives = NewTable("Model", "Serial", "Interface", "Capacity", "Status"); foreach (var x in r.Drives) Row(drives, x.Model, x.Serial, x.Interface, Collectors.FormatBytes(x.SizeBytes), x.SmartStatus); d.Blocks.Add(drives);
            d.Blocks.Add(Heading("Live sensors")); var sensors = NewTable("Hardware", "Sensor", "Current", "Minimum", "Maximum"); foreach (var x in r.Sensors) Row(sensors, x.Hardware, x.Name, Num(x.Current, x.Unit), Num(x.Minimum, x.Unit), Num(x.Maximum, x.Unit)); d.Blocks.Add(sensors);
            d.Blocks.Add(Heading("Inspection limitations")); foreach (var x in r.Limitations) d.Blocks.Add(new Paragraph(new Run("• " + x)) { Margin = new Thickness(8, 2, 0, 2) });
            d.Blocks.Add(Heading("Technician notes")); d.Blocks.Add(new Paragraph(new Run(Empty(r.TechnicianNotes))) { MinHeight = 50 });
            d.Blocks.Add(new Paragraph(new Run("This is a read-only screening report, not a guarantee of future reliability. No repairs or modifications were performed by A2Z System Inspector.")) { FontSize = 9, Foreground = Brushes.DimGray, Margin = new Thickness(0, 20, 0, 0) });
            return d;
        }
        public static void SaveJson(InspectionReport report, Window owner)
        {
            var dialog = new SaveFileDialog { Filter = "JSON evidence (*.json)|*.json", FileName = "A2Z-Inspection-" + report.InspectionId + ".json" }; if (dialog.ShowDialog(owner) != true) return;
            using (var fs = File.Create(dialog.FileName)) new DataContractJsonSerializer(typeof(InspectionReport)).WriteObject(fs, report);
        }
        public static void SaveText(InspectionReport r, Window owner)
        {
            var dialog = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "A2Z-Inspection-" + r.InspectionId + ".txt" }; if (dialog.ShowDialog(owner) != true) return;
            var b = new StringBuilder(); b.AppendLine("A2Z SYSTEM INSPECTOR").AppendLine("Inspection: " + r.InspectionId).AppendLine("Customer/reference: " + r.CustomerReference).AppendLine("Technician: " + r.Technician).AppendLine("Overall: " + (r.OverallScore.HasValue ? r.OverallScore + "/100 " + r.OverallStatus : "N/A")); foreach (var s in r.Scores) b.AppendLine(s.Category + ": " + (s.Score.HasValue ? s.Score + "/100" : "N/A") + " — " + s.Reason); foreach (var f in r.Findings) b.AppendLine(f.Severity + ": " + f.Title + " — " + f.Explanation + " Recommendation: " + f.Recommendation); File.WriteAllText(dialog.FileName, b.ToString(), Encoding.UTF8);
        }
        public static void Print(FlowDocument source, Window owner)
        {
            var dialog = new PrintDialog(); if (dialog.ShowDialog() != true) return; source.PageHeight = dialog.PrintableAreaHeight; source.PageWidth = dialog.PrintableAreaWidth; source.ColumnWidth = dialog.PrintableAreaWidth; dialog.PrintDocument(((IDocumentPaginatorSource)source).DocumentPaginator, "A2Z System Inspector Report");
        }
        private static Paragraph Heading(string text) => new Paragraph(new Run(text)) { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(16, 42, 67)), Margin = new Thickness(0, 14, 0, 6) };
        private static Table KeyValues(string[] values) { var t = NewTable("Item", "Value"); for (var i = 0; i < values.Length; i += 2) Row(t, values[i], values[i + 1]); return t; }
        private static Table NewTable(params string[] headers) { var t = new Table { CellSpacing = 0 }; foreach (var h in headers) t.Columns.Add(new TableColumn()); var g = new TableRowGroup(); t.RowGroups.Add(g); var row = new TableRow { Background = new SolidColorBrush(Color.FromRgb(230, 238, 246)), FontWeight = FontWeights.Bold }; foreach (var h in headers) row.Cells.Add(Cell(h)); g.Rows.Add(row); return t; }
        private static void Row(Table table, params string[] values) { var row = new TableRow(); foreach (var v in values) row.Cells.Add(Cell(Empty(v))); table.RowGroups[0].Rows.Add(row); }
        private static TableCell Cell(string value) => new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(0) }) { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) };
        private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value;
        private static string Num(float? n, string unit) => n.HasValue ? n.Value.ToString("0.0") + " " + unit : "N/A";
        private static Brush StatusBrush(string s) => s == "Healthy" ? Brushes.LightGreen : s == "Attention" ? Brushes.LightGoldenrodYellow : s == "Critical" ? Brushes.LightCoral : Brushes.PeachPuff;
    }
}
