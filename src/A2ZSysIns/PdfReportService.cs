using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace A2ZSysIns
{
    internal static class PdfReportService
    {
        public static async Task<string> SavePdfAsync(InspectionReport report, Window owner)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF report (*.pdf)|*.pdf",
                FileName = "A2Z-Inspection-" + report.InspectionId + ".pdf",
                AddExtension = true,
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog(owner) != true) return null;
            var path = dialog.FileName;
            EvidenceEngine.Log(report, "PDF export requested", path);
            await GeneratePdfForValidationAsync(report, path, TimeSpan.FromSeconds(45));
            EvidenceEngine.Log(report, "PDF export completed", path);
            return path;
        }

        // Keeps the UI responsive and gives both production export and CI a bounded,
        // deterministic PDF-generation path. The background writer is deliberately
        // isolated from the WPF dispatcher because PDFsharp does not require it.
        internal static async Task GeneratePdfForValidationAsync(InspectionReport report, string path, TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("timeout");
            var build = BuildPdfOnStaAsync(report, path);
            if (await Task.WhenAny(build, Task.Delay(timeout)).ConfigureAwait(false) != build)
                throw new TimeoutException("PDF generation did not complete within " + timeout.TotalSeconds.ToString("0") + " seconds.");
            await build.ConfigureAwait(false);
        }

        private static Task BuildPdfOnStaAsync(InspectionReport report, string path)
        {
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    BuildPdf(report, path);
                    completion.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "A2Z-PdfExport"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void BuildPdf(InspectionReport r, string path)
        {
            if (r == null) throw new ArgumentNullException("r");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("PDF output path is empty.", "path");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var document = new PdfDocument();
            document.Info.Title = "A2Z System Inspector - Computer Health Inspection Report";
            document.Info.Author = "A2Z Tec Solutions";
            document.Info.Subject = "Evidence-based computer health inspection";

            var writer = new PdfWriter(document);
            writer.Title("A2Z SYSTEM INSPECTOR");
            writer.Subtitle("Computer Health Inspection Report");
            writer.KeyValue("Inspection ID", Safe(r.InspectionId));
            writer.KeyValue("Customer / reference", Safe(r.CustomerReference));
            writer.KeyValue("Job number", Safe(r.JobNumber));
            writer.KeyValue("Technician", Safe(r.Technician));
            writer.KeyValue("Inspection completed", r.CompletedAt == default(DateTime) ? "N/A" : r.CompletedAt.ToString("yyyy-MM-dd HH:mm"));
            writer.KeyValue("Reported problem", Safe(r.ReportedProblem));
            writer.Space(8);
            writer.Status(Safe(r.OverallStatus));
            writer.Paragraph(Safe(r.CustomerSummary));
            writer.Heading("What you should do next");
            if (r.PriorityActions.Count == 0) writer.Bullet("No immediate action was generated from the available evidence.");
            else foreach (var x in r.PriorityActions) writer.Bullet(x);
            writer.Heading("Your computer at a glance");
            writer.Paragraph("Green = no flagged problem; amber = needs attention; red = critical; grey = not verified. These ratings describe the evidence collected, not a guarantee.");
            if (r.CustomerHealth != null && r.CustomerHealth.Components != null && r.CustomerHealth.Components.Count > 0)
            {
                int item = 0;
                foreach (var component in r.CustomerHealth.Components)
                {
                    item++;
                    writer.CustomerResult(item, Safe(component.Component), Safe(component.Status),
                        Safe(component.Title), Safe(component.Explanation));
                }
            }
            else
            {
                int item = 0;
                foreach (var score in r.Scores)
                    writer.CustomerResult(++item, Safe(score.Category), Safe(score.Status), Safe(score.Reason), "");
            }
            writer.Small("Percentage indicators are shown only where a device actually reports a measurable value, such as SSD endurance or battery capacity. An overall PC health percentage is not scientifically validated yet.");
            writer.Heading("Important findings");
            var visible = r.Findings.Where(x => x.Severity != "Information").ToList();
            if (visible.Count == 0) writer.Paragraph("No critical or attention-level finding was generated by the evidence collected.");
            foreach (var f in visible) writer.Finding(f);
            var info = r.Findings.Where(x => x.Severity == "Information").ToList();
            if (info.Count > 0)
            {
                writer.Heading("Other observations");
                foreach (var f in info) writer.Finding(f);
            }
            writer.Heading("What could not be tested");
            var unavailable = r.Measurements.Where(x => x.Status == "Unavailable" || x.Status == "Failed").ToList();
            if (unavailable.Count == 0 && r.Limitations.Count == 0) writer.Bullet("No collector explicitly reported an unavailable measurement. This still does not guarantee future reliability.");
            else
            {
                foreach (var x in unavailable) writer.Bullet(Safe(x.Target) + " - " + Safe(x.Reason));
                foreach (var x in r.Limitations.Distinct()) writer.Bullet(x);
            }

            writer.NewPage();
            writer.Title("TECHNICAL EVIDENCE");
            writer.Paragraph("This section is intended for a technician and preserves the measurement context behind the customer summary.");
            writer.Heading("System information");
            foreach (var x in r.System) writer.KeyValue(x.Key, x.Value);
            writer.Heading("Physical storage evidence");
            foreach (var d in r.Drives)
            {
                writer.ResultLine(Safe(d.Model), Safe(d.Assessment), "Capacity " + Collectors.FormatBytes(d.SizeBytes) + "; life indicator " + (d.RemainingLifePercent.HasValue ? d.RemainingLifePercent.Value.ToString("0") + "%" : "Cannot measure") + "; SMART " + Safe(d.SmartStatus));
            }
            writer.Heading("CPU stress test");
            if (r.CpuStressTest == null) writer.Paragraph("Not run.");
            else
            {
                var t = r.CpuStressTest;
                writer.KeyValue("Status", Safe(t.Status));
                writer.KeyValue("Duration", t.ActualDurationSeconds + " s");
                writer.KeyValue("Workers", t.LogicalWorkers.ToString());
                writer.KeyValue("Pre-test temperature", Number(t.BaselineTemperatureC));
                writer.KeyValue("Maximum temperature", Number(t.MaximumTemperatureC));
                writer.KeyValue("Stop reason", Safe(t.StopReason));
            }
            writer.Heading("Actual temperature sensors");
            foreach (var s in r.Sensors.Where(EvidenceEngine.ActualTemperature)) writer.ResultLine(Safe(s.Hardware) + " / " + Safe(s.Name), Number(s.Current, s.Unit), "min " + Number(s.Minimum, s.Unit) + "; max " + Number(s.Maximum, s.Unit));
            writer.Heading("Windows event evidence");
            foreach (var e in r.Events) writer.ResultLine(Safe(e.Source) + " / " + e.EventId, e.Count + " occurrence(s)", Safe(e.Cause));
            writer.Heading("Measurement coverage");
            foreach (var m in r.Measurements) writer.ResultLine(Safe(m.Target), Safe(m.Status), Safe(m.Source) + " - " + Safe(m.Reason));
            writer.Heading("Technician notes");
            writer.Paragraph(Safe(r.TechnicianNotes));
            writer.Space(10);
            writer.Small("Important: This report is a non-invasive screening based on evidence available during the inspection. GOOD means no defined warning was detected in that check; it does not guarantee future reliability. NOT TESTED / unavailable never means healthy.");
            writer.FooterAllPages("A2Z System Inspector  |  Rules " + Safe(r.RuleSetVersion) + "  |  Inspection " + Safe(r.InspectionId));

            var temp = path + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
            document.Save(temp);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
        private static string Number(double? value) => value.HasValue ? value.Value.ToString("0.0") + " C" : "N/A";
        private static string Number(float? value, string unit) => value.HasValue ? value.Value.ToString("0.0") + " " + Safe(unit) : "N/A";

        private sealed class PdfWriter
        {
            private readonly PdfDocument _doc;
            private PdfPage _page;
            private XGraphics _gfx;
            private double _y;
            private const double Left = 44;
            private const double Right = 44;
            private const double Top = 46;
            private const double Bottom = 48;
            private readonly XFont _body = new XFont("Segoe UI", 9.5, XFontStyleEx.Regular);
            private readonly XFont _bold = new XFont("Segoe UI", 9.5, XFontStyleEx.Bold);
            private readonly XFont _small = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
            private readonly XFont _h1 = new XFont("Segoe UI", 22, XFontStyleEx.Bold);
            private readonly XFont _h2 = new XFont("Segoe UI", 13.5, XFontStyleEx.Bold);
            private readonly XFont _subtitle = new XFont("Segoe UI", 12, XFontStyleEx.Regular);
            private readonly XBrush _text = new XSolidBrush(XColor.FromArgb(35, 44, 55));
            private readonly XBrush _muted = new XSolidBrush(XColor.FromArgb(95, 105, 116));
            private readonly XBrush _navy = new XSolidBrush(XColor.FromArgb(20, 48, 74));

            public PdfWriter(PdfDocument doc) { _doc = doc; NewPage(); }

            public void NewPage()
            {
                DisposeGraphics();
                _page = _doc.AddPage();
                _page.Size = PdfSharp.PageSize.A4;
                _gfx = XGraphics.FromPdfPage(_page);
                _y = Top;
            }

            public void Title(string text) { Ensure(34); DrawWrapped(text, _h1, _navy, 24, 1.08); Space(3); }
            public void Subtitle(string text) { DrawWrapped(text, _subtitle, _muted, 17, 1.1); Space(8); }
            public void Heading(string text) { Space(7); Ensure(26); DrawWrapped(text, _h2, _navy, 19, 1.1); Space(2); }
            public void Paragraph(string text) { DrawWrapped(text, _body, _text, 14, 1.18); Space(3); }
            public void Small(string text) { DrawWrapped(text, _small, _muted, 11, 1.15); }
            public void Bullet(string text) { DrawWrapped("- " + Safe(text), _body, _text, 14, 1.15, 10); Space(1); }
            public void Space(double points) { _y += points; }

            public void KeyValue(string key, string value)
            {
                Ensure(18);
                var available = Width;
                var keyWidth = Math.Min(155, available * 0.30);
                _gfx.DrawString(Safe(key), _bold, _text, new XRect(Left, _y, keyWidth, 14), XStringFormats.TopLeft);
                var lines = Wrap(Safe(value), _body, available - keyWidth - 8);
                var h = Math.Max(14, lines.Count * 12.5);
                Ensure(h + 2);
                for (var i = 0; i < lines.Count; i++) _gfx.DrawString(lines[i], _body, _text, Left + keyWidth + 8, _y + 10 + (i * 12.5));
                _y += h + 2;
            }

            public void Status(string text)
            {
                var fill = XColor.FromArgb(228, 236, 242);
                if (text.StartsWith("CRITICAL", StringComparison.OrdinalIgnoreCase)) fill = XColor.FromArgb(255, 220, 220);
                else if (text.StartsWith("ATTENTION", StringComparison.OrdinalIgnoreCase)) fill = XColor.FromArgb(255, 239, 194);
                else if (text.StartsWith("NO CRITICAL", StringComparison.OrdinalIgnoreCase)) fill = XColor.FromArgb(220, 243, 225);
                var lines = Wrap(text, _h2, Width - 20);
                var h = Math.Max(34, 18 + lines.Count * 18);
                Ensure(h + 5);
                _gfx.DrawRectangle(new XSolidBrush(fill), Left, _y, Width, h);
                for (var i = 0; i < lines.Count; i++) _gfx.DrawString(lines[i], _h2, _text, Left + 10, _y + 22 + i * 18);
                _y += h + 7;
            }

            public void ResultLine(string left, string state, string reason)
            {
                const double leftWidth = 145;
                const double stateWidth = 135;
                const double gap = 8;
                var reasonWidth = Width - leftWidth - stateWidth - gap;
                var leftLines = Wrap(Safe(left), _bold, leftWidth - 10);
                var stateLines = Wrap(Safe(state), _body, stateWidth - 10);
                var reasonLines = Wrap(Safe(reason), _body, reasonWidth - 10);
                var lineCount = Math.Max(leftLines.Count, Math.Max(stateLines.Count, reasonLines.Count));
                var h = Math.Max(22, lineCount * 12.5 + 8);
                Ensure(h + 2);
                _gfx.DrawRectangle(new XPen(XColor.FromArgb(220, 225, 230), 0.6), Left, _y, Width, h);
                for (var i = 0; i < leftLines.Count; i++) _gfx.DrawString(leftLines[i], _bold, _text, Left + 5, _y + 14 + i * 12.5);
                for (var i = 0; i < stateLines.Count; i++) _gfx.DrawString(stateLines[i], _body, _navy, Left + leftWidth + 2, _y + 14 + i * 12.5);
                for (var i = 0; i < reasonLines.Count; i++) _gfx.DrawString(reasonLines[i], _body, _text, Left + leftWidth + stateWidth + gap, _y + 14 + i * 12.5);
                _y += h + 2;
            }

            public void CustomerResult(int number, string area, string status, string title, string explanation)
            {
                var normalized = (status ?? "").ToUpperInvariant();
                var accent = normalized == "CRITICAL" ? XColor.FromArgb(190, 38, 38) :
                    normalized == "ATTENTION" || normalized == "DEGRADED" ? XColor.FromArgb(181, 117, 0) :
                    normalized == "GOOD" ? XColor.FromArgb(24, 130, 65) :
                    XColor.FromArgb(107, 114, 128);
                var description = Wrap(Safe(explanation), _small, Width - 30);
                var heading = Wrap(number + ". " + Safe(area) + "  |  " + Safe(status) + "  |  " + Safe(title), _bold, Width - 30);
                var height = 13 + heading.Count * 13 + description.Count * 11 + 9;
                Ensure(height + 4);
                _gfx.DrawRectangle(new XPen(XColor.FromArgb(221, 227, 233), 0.7), Left, _y, Width, height);
                _gfx.DrawRectangle(new XSolidBrush(accent), Left, _y, 4, height);
                var baseline = _y + 16;
                foreach (var line in heading) { _gfx.DrawString(line, _bold, new XSolidBrush(accent), Left + 12, baseline); baseline += 13; }
                baseline += 2;
                foreach (var line in description) { _gfx.DrawString(line, _small, _text, Left + 12, baseline); baseline += 11; }
                _y += height + 4;
            }

            public void Finding(Finding f)
            {
                var body = Safe(f.Explanation) + " Action: " + Safe(f.Recommendation) + " Confidence: " + Safe(f.Confidence) + "; priority: " + Safe(f.ActionLevel) + ".";
                var titleLines = Wrap(Safe(f.Severity).ToUpperInvariant() + " - " + Safe(f.Title), _bold, Width - 16);
                var bodyLines = Wrap(body, _body, Width - 16);
                var h = 10 + titleLines.Count * 13 + bodyLines.Count * 12.5 + 8;
                Ensure(h + 3);
                var fill = string.Equals(f.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ? XColor.FromArgb(255, 231, 231) : string.Equals(f.Severity, "Attention", StringComparison.OrdinalIgnoreCase) ? XColor.FromArgb(255, 247, 220) : XColor.FromArgb(234, 240, 246);
                _gfx.DrawRectangle(new XSolidBrush(fill), Left, _y, Width, h);
                var yy = _y + 16;
                foreach (var line in titleLines) { _gfx.DrawString(line, _bold, _text, Left + 8, yy); yy += 13; }
                yy += 2;
                foreach (var line in bodyLines) { _gfx.DrawString(line, _body, _text, Left + 8, yy); yy += 12.5; }
                _y += h + 4;
            }

            public void FooterAllPages(string text)
            {
                // The active page already owns an XGraphics instance. Dispose it
                // before opening an Append graphics instance on that same page.
                DisposeGraphics();
                for (var i = 0; i < _doc.Pages.Count; i++)
                {
                    using (var g = XGraphics.FromPdfPage(_doc.Pages[i], XGraphicsPdfPageOptions.Append))
                    {
                        g.DrawLine(new XPen(XColor.FromArgb(210, 216, 222), 0.5), Left, _doc.Pages[i].Height.Point - 31, _doc.Pages[i].Width.Point - Right, _doc.Pages[i].Height.Point - 31);
                        g.DrawString(text + "  |  Page " + (i + 1) + " of " + _doc.Pages.Count, _small, _muted, new XRect(Left, _doc.Pages[i].Height.Point - 26, _doc.Pages[i].Width.Point - Left - Right, 14), XStringFormats.TopLeft);
                    }
                }
            }

            private double Width => _page.Width.Point - Left - Right;
            private void Ensure(double needed) { if (_y + needed > _page.Height.Point - Bottom) NewPage(); }

            private void DrawWrapped(string text, XFont font, XBrush brush, double lineHeight, double factor, double indent = 0)
            {
                var lines = Wrap(Safe(text), font, Width - indent);
                Ensure(lines.Count * lineHeight + 2);
                foreach (var line in lines)
                {
                    _gfx.DrawString(line, font, brush, Left + indent, _y + lineHeight * 0.78);
                    _y += lineHeight * factor;
                }
            }

            private List<string> Wrap(string text, XFont font, double maxWidth)
            {
                var result = new List<string>();
                foreach (var paragraph in Safe(text).Replace("\r", "").Split('\n'))
                {
                    var words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length == 0) { result.Add(""); continue; }
                    var line = words[0];
                    for (var i = 1; i < words.Length; i++)
                    {
                        var candidate = line + " " + words[i];
                        if (_gfx.MeasureString(candidate, font).Width <= maxWidth) line = candidate;
                        else { result.Add(line); line = words[i]; }
                    }
                    result.Add(line);
                }
                return result;
            }

            private void DisposeGraphics()
            {
                if (_gfx == null) return;
                _gfx.Dispose();
                _gfx = null;
            }
        }
    }
}
