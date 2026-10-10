using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf.IO;
using System;
using System.IO;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public sealed class PdfReportServiceTests
    {
        [TestMethod]
        public void DeterministicCustomerReport_IsReadableA4Pdf()
        {
            var report = new InspectionReport
            {
                InspectionId = "pdf-validation",
                CustomerReference = "MVP PDF validation",
                Technician = "A2Z technician",
                CompletedAt = new DateTime(2026, 10, 10, 10, 30, 0),
                OverallStatus = "ATTENTION",
                CustomerSummary = "Storage evidence requires technician follow-up."
            };
            report.Scores.Add(new CategoryScore { Category = "Storage health", Status = "ATTENTION", Reason = "Validated SMART warning." });
            report.PriorityActions.Add("Back up important data and review the storage device.");
            report.Limitations.Add("Temperature was not measurable on this fixture.");
            report.System["Operating system"] = "Windows 11 Pro";
            report.System["Processor"] = "Validation CPU";
            report.Drives.Add(new DriveInfoRecord { Model = "Validation SSD", SizeBytes = 512L * 1024 * 1024 * 1024, Assessment = "Attention: errors reported", SmartStatus = "SMART warning" });
            report.Sensors.Add(new SensorRecord { Hardware = "CPU", Name = "Package", Type = "Temperature", Current = 68, Maximum = 72, Unit = "C" });
            report.Events.Add(new EventFinding { Source = "Disk", EventId = 7, Count = 1, Cause = "Example deterministic event evidence." });
            report.Measurements.Add(new Measurement { Target = "Storage SMART", Source = "Validation fixture", Status = "Observed", Reason = "Deterministic sample evidence." });
            report.Findings.Add(new Finding { Severity = "Attention", Title = "Storage warning requires review", Explanation = "The deterministic validation fixture includes a SMART warning.", Recommendation = "Confirm backups and investigate the drive.", Confidence = "High", ActionLevel = "Recommended" });

            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "output", "pdf");
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, "A2Z-MVP-Validation-Report.pdf");
            if (File.Exists(path)) File.Delete(path);
            try
            {
                PdfReportService.GeneratePdfForValidationAsync(report, path, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                Assert.IsTrue(File.Exists(path));
                Assert.IsTrue(new FileInfo(path).Length > 1024, "The generated report is unexpectedly small.");
                using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                {
                    Assert.IsTrue(document.PageCount >= 2, "Customer and technical sections should be separated into readable pages.");
                    Assert.AreEqual("A2Z System Inspector - Computer Health Inspection Report", document.Info.Title);
                }
            }
            finally { }
        }
    }
}
