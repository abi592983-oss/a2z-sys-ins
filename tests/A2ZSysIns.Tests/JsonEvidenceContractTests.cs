using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public sealed class JsonEvidenceContractTests
    {
        [TestMethod]
        public void InspectionReport_RoundTripsThroughTheJsonExportContract()
        {
            var report = new InspectionReport { InspectionId = "json-validation", Technician = "A2Z technician" };
            report.Measurements.Add(new Measurement { Target = "Storage SMART", Source = "fixture", Status = "Unavailable", Reason = "No SMART provider returned data." });
            report.Stages.Add(new InspectionStageResult { Name = "Storage", Status = "WARNING", Reason = "Unavailable evidence recorded." });

            var json = ReportService.SerializeJson(report);
            Assert.IsTrue(json.Length > 0);
            var restored = JsonConvert.DeserializeObject<InspectionReport>(json);
            Assert.AreEqual("json-validation", restored.InspectionId);
            Assert.AreEqual("Unavailable", restored.Measurements[0].Status);
            Assert.AreEqual("WARNING", restored.Stages[0].Status);
        }
    }
}
