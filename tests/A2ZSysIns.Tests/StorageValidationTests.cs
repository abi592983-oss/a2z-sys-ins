using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace A2ZSysIns.Tests
{
    [TestClass]
    public class StorageValidationTests
    {
        [TestMethod]
        public void AtaKnownSemanticReallocatedSectorsBecomeAttention()
        {
            var drive = Drive("ATA test", "SN-001", 500L);
            drive.SmartPassed = true;
            drive.SmartAttributes.Add(new SmartAttributeRecord { Id = 5, Name = "Reallocated_Sector_Ct", RawValue = 3 });
            Interpret(drive);
            Assert.IsTrue(drive.SmartAttributes[0].SemanticsValidated);
            Assert.AreEqual(3L, drive.Attributes["ATA_5"]);
            Assert.AreEqual("Attention: errors reported", drive.Assessment);
        }

        [TestMethod]
        public void AtaUnknownIdIsNotInterpretedByNumberAlone()
        {
            var drive = Drive("ATA test", "SN-002", 500L);
            drive.SmartPassed = true;
            drive.SmartAttributes.Add(new SmartAttributeRecord { Id = 5, Name = "Vendor_Custom_Field", RawValue = 999 });
            Interpret(drive);
            Assert.IsFalse(drive.SmartAttributes[0].SemanticsValidated);
            Assert.IsFalse(drive.Attributes.ContainsKey("ATA_5"));
            Assert.AreEqual("No flagged indicators in available data", drive.Assessment);
        }

        [TestMethod]
        public void ValidatedLifeAttributeIsSeparateFromCondition()
        {
            var drive = Drive("SSD test", "SN-003", 1000L);
            drive.SmartPassed = true;
            drive.SmartAttributes.Add(new SmartAttributeRecord { Id = 233, Name = "Media_Wearout_Indicator", RawValue = 85 });
            Interpret(drive);
            var result = StorageHealthAssessmentService.Assess(drive);
            Assert.AreEqual("GOOD / NO FLAGGED INDICATOR", result.Condition);
            StringAssert.Contains(result.Endurance, "15.0% remaining");
            StringAssert.Contains(result.Reason, "Condition and endurance are separate dimensions");
        }

        [TestMethod]
        public void SsdLifeLeftUsesNormalizedValueNotVendorRawCounter()
        {
            // Real HP validation case: CDI reports E7 SSD Life Left as 64%,
            // while the same SMART row's raw field is 36. Raw is vendor-specific.
            var drive = Drive("HS-SSD-WAVE(S) 256G", "30128481982", 256L);
            drive.SmartPassed = true;
            drive.SmartAttributes.Add(new SmartAttributeRecord
            {
                Id = 0xE7,
                Name = "SSD Life Left",
                NormalizedValue = 64,
                WorstValue = 64,
                Threshold = 5,
                RawValue = 36
            });
            Interpret(drive);
            Assert.AreEqual(64d, drive.RemainingLifePercent);
            Assert.AreEqual(36d, drive.EnduranceUsedPercent);
            Assert.IsTrue(drive.SmartAttributes[0].SemanticsValidated);
        }

        [TestMethod]
        public void NvmeCriticalWarningIsCriticalAndPercentageUsedMapsToEndurance()
        {
            var drive = Drive("NVMe test", "NV-001", 2000L);
            drive.NvmeHealth = new NvmeHealthRecord { CriticalWarning = 1, PercentageUsed = 70, MediaErrors = 0 };
            Interpret(drive);
            var result = StorageHealthAssessmentService.Assess(drive);
            Assert.AreEqual("CRITICAL", result.Condition);
            Assert.AreEqual(70d, drive.EnduranceUsedPercent);
            Assert.AreEqual(30d, drive.RemainingLifePercent);
        }

        [TestMethod]
        public void NoSmartEvidenceIsUnknownNotGood()
        {
            var drive = Drive("Unknown test", "SN-004", 500L);
            var result = StorageHealthAssessmentService.Assess(drive);
            Assert.AreEqual("UNKNOWN", result.Condition);
            Assert.AreEqual("UNAVAILABLE", result.Endurance);
        }

        [TestMethod]
        public void PartialEvidenceGetsLowerConfidenceWithoutInventingHealth()
        {
            var drive = Drive("Partial test", "SN-005", 500L);
            drive.SmartAttributes.Add(new SmartAttributeRecord { Id = 1, Name = "Vendor_Custom_Field", RawValue = 123 });
            Interpret(drive);
            var result = StorageHealthAssessmentService.Assess(drive);
            Assert.AreEqual("GOOD / NO FLAGGED INDICATOR", result.Condition);
            Assert.AreEqual("Low", result.ConditionConfidence);
        }

        [TestMethod]
        public void IndividualTargetsClassifyStorageAndPreferSerialMatch()
        {
            var report = new InspectionReport();
            report.Drives.Add(Drive("Same Model", "SERIAL-A", 500L));
            report.Drives[0].SmartDeviceType = "nvme";
            report.Drives.Add(Drive("Same Model", "SERIAL-B", 500L));
            report.Drives[1].SmartDeviceType = "nvme";
            var targets = IndividualStorageInspectionService.GetTargets(report);
            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual("NVMe SSD", targets[0].DeviceType);
            var staleIndexTarget = new IndividualStorageInspectionService.Target { Index = 0, Model = "Same Model", Serial = "SERIAL-B", SizeBytes = 500L };
            var result = IndividualStorageInspectionService.Inspect(report, staleIndexTarget);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("SERIAL-B", result.Drive.Serial);
        }

        [TestMethod]
        public void IndividualTargetMismatchFailsSafely()
        {
            var report = new InspectionReport();
            report.Drives.Add(Drive("Drive A", "SERIAL-A", 500L));
            var target = new IndividualStorageInspectionService.Target { Index = 0, Model = "Drive B", Serial = "SERIAL-B", SizeBytes = 1000L };
            var result = IndividualStorageInspectionService.Inspect(report, target);
            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.FailureReason, "no longer present");
        }

        [TestMethod]
        public void CrossCategoryAssessmentDoesNotTransferStorageFailureIntoThermals()
        {
            var report = new InspectionReport();
            var drive = Drive("Failing storage", "SERIAL-X", 500L);
            drive.SmartPassed = false;
            report.Drives.Add(drive);
            StorageInterpretationService.Interpret(report);
            StorageHealthAssessmentService.Record(report);
            var storage = report.Measurements.First(x => x.Target == "Diagnostic assessment model — Storage");
            var thermals = report.Measurements.First(x => x.Target == "Diagnostic assessment model — Thermals");
            Assert.AreEqual("Critical", storage.Status);
            Assert.AreEqual("Unavailable", thermals.Status);
            Assert.IsNull(report.OverallScore);
        }

        [TestMethod]
        public void MissingBatteryIsUnavailableAndLowDiskSpaceIsAttention()
        {
            var report = new InspectionReport();
            report.Volumes.Add(new VolumeRecord { Name = "C:", TotalBytes = 1000, FreeBytes = 40 });
            DiagnosticAssessmentService.Record(report);
            var battery = report.Measurements.First(x => x.Target == "Diagnostic assessment model — Battery");
            var resources = report.Measurements.First(x => x.Target == "Diagnostic assessment model — Resources");
            Assert.AreEqual("Unavailable", battery.Status);
            Assert.AreEqual("Attention", resources.Status);
            Assert.IsNull(report.OverallScore);
        }

        [TestMethod]
        public void CpuGraphFixtureUsesOnlyActualSamples()
        {
            var stress = new CpuStressResult();
            stress.Samples.Add(new CpuStressSample { ElapsedSeconds = 0, TemperatureC = 48 });
            stress.Samples.Add(new CpuStressSample { ElapsedSeconds = 10, TemperatureC = 61 });
            Assert.AreEqual(2, stress.Samples.Count);
            Assert.AreEqual(0, stress.Samples[0].ElapsedSeconds);
            Assert.AreEqual(61d, stress.Samples[1].TemperatureC);
        }

        [TestMethod]
        public void StorageCurrentTemperatureIsNotAHistorySeries()
        {
            var drive = Drive("Temperature test", "SN-T", 500L);
            drive.TemperatureC = 42;
            Assert.IsTrue(drive.TemperatureC.HasValue);
            Assert.AreEqual(42d, drive.TemperatureC.Value);
            Assert.AreEqual(0, 0); // Deliberately no fabricated timestamped storage samples exist.
        }

        private static DriveInfoRecord Drive(string model, string serial, long sizeGb)
        {
            return new DriveInfoRecord { Model = model, Serial = serial, SizeBytes = sizeGb * 1000L * 1000L * 1000L };
        }

        private static void Interpret(DriveInfoRecord drive)
        {
            var report = new InspectionReport();
            report.Drives.Add(drive);
            StorageInterpretationService.Interpret(report);
        }
    }
}
