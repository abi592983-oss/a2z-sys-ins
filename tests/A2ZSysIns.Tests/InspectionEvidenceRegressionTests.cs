using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public sealed class InspectionEvidenceRegressionTests
    {
        [TestMethod]
        public void CpuHeat_DoesNotMistakeMemoryThermalLimitsForCpuMeasurements()
        {
            var r = new InspectionReport { InspectionId = "cpu-real-log-regression" };
            r.Sensors.Add(new SensorRecord { Hardware = "Intel Core i7-14700K", Name = "CPU Package", Type = "Temperature", Current = 45, Maximum = 47 });
            r.Sensors.Add(new SensorRecord { Hardware = "Intel Core i7-14700K", Name = "Core Max", Type = "Temperature", Current = 44, Maximum = 52 });
            r.Sensors.Add(new SensorRecord { Hardware = "SK Hynix - DIMM #0", Name = "Thermal Sensor Critical High Limit", Type = "Temperature", Current = 85, Maximum = 85 });
            r.Sensors.Add(new SensorRecord { Hardware = "SK Hynix - DIMM #0", Name = "DIMM #0", Type = "Temperature", Current = 33, Maximum = 33 });
            Assert.IsFalse(EvidenceEngine.ActualTemperature(r.Sensors[2]));
            Assert.IsFalse(Scoring.IsCpuTemperature(r.Sensors[3]));
            Assert.IsTrue(Scoring.IsCpuTemperature(r.Sensors[0]));
            CustomerHealthAssessmentService.Apply(r);
            var thermal = r.CustomerHealth.Components.Single(x => x.Component == "Temperature");
            Assert.AreEqual("GOOD", thermal.Status);
            Assert.IsTrue(thermal.Evidence.Contains("52.0"), thermal.Evidence);
        }

        [TestMethod]
        public void WindowsIntegrity_UnknownSfcDoesNotBecomeGoodBecauseChkdskPassed()
        {
            var r = new InspectionReport { InspectionId = "partial-sfc" };
            r.Measurements.Add(new Measurement { Target = "Windows integrity / System files", Status = "Unavailable", Reason = "SFC result missing" });
            r.Measurements.Add(new Measurement { Target = "Windows integrity / File system C:", Status = "Observed", Reason = "No errors" });
            CustomerHealthAssessmentService.Apply(r);
            Assert.AreEqual("UNKNOWN", r.CustomerHealth.Components.Single(x => x.Component == "Windows").Status);
        }

        [TestMethod]
        public void Event51AndRepeatedKernelPnp_DoNotReceiveGoodStabilityGrade()
        {
            var r = new InspectionReport { InspectionId = "event-pattern" };
            r.Events.Add(new EventFinding { Source = "disk", EventId = 51, Count = 71 });
            r.Events.Add(new EventFinding { Source = "Microsoft-Windows-Kernel-PnP", EventId = 219, Count = 38 });
            CustomerHealthAssessmentService.Apply(r);
            Assert.AreEqual("ATTENTION", r.CustomerHealth.Components.Single(x => x.Component == "Windows stability").Status);
        }

        [TestMethod]
        public void PartialSmartDoesNotReceiveUnqualifiedHealthyStatement()
        {
            var r = new InspectionReport { InspectionId = "limited-smart" };
            r.Drives.Add(new DriveInfoRecord { Model = "SK hynix PVC10", SmartPassed = true, SmartDataSource = "Windows ATA fallback" });
            CustomerHealthAssessmentService.Apply(r);
            var drive = r.CustomerHealth.Components.Single(x => x.Component == "Storage");
            Assert.AreEqual("UNKNOWN", drive.Status);
            StringAssert.Contains(drive.Title, "limited health evidence");
        }

        [TestMethod]
        public void StageStatusUsesMeasurementIndex_NotMeasurementPlusLogCount()
        {
            var r = new InspectionReport { InspectionId = "stage-warning" };
            for (var i = 0; i < 15; i++) r.DiagnosticLog.Add(new LogEntry { Action = "Prior log" });
            var stage = InspectionStageRunner.RunAsync(r, "Windows integrity", () =>
                EvidenceEngine.Record(r, "Windows integrity / SFC", "sfc.exe", "Unavailable", "No final verdict"),
                CancellationToken.None, null).GetAwaiter().GetResult();
            Assert.AreEqual("WARNING", stage.Status);
        }

        [TestMethod]
        public void SfcClassifierRecognizesNullInterleavedOutput()
        {
            var raw = string.Join("\0", "Windows Resource Protection did not find any integrity violations".ToCharArray()) + "\0";
            Assert.AreEqual("Observed", WindowsIntegrityService.ClassifySfc(raw).Item1);
            Assert.AreEqual("Unavailable", WindowsIntegrityService.ClassifySfc("Verification 89% complete.").Item1);
        }
    }
}
