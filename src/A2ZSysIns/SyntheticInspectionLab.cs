using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    // Deterministic synthetic laboratory used to exercise the diagnostic pipeline
    // without touching real hardware, Windows providers, or repair operations.
    public static class SyntheticInspectionLab
    {
        public sealed class RunResult
        {
            public int Seed;
            public string Scenario;
            public bool Passed;
            public List<string> Failures = new List<string>();
            public InspectionReport Report;
        }

        private static readonly string[] Scenarios =
        {
            "healthy-desktop", "aging-ssd", "failing-hdd", "healthy-nvme",
            "thermal-problem", "high-memory-use", "windows-integrity-problem",
            "missing-evidence", "conflicting-providers", "sparse-machine",
            "provider-fallback", "mixed-faults"
        };

        public static IReadOnlyList<string> ScenarioNames { get { return Scenarios; } }

        public static RunResult Run(int seed, string scenario)
        {
            var result = new RunResult { Seed = seed, Scenario = scenario };
            var rng = new Random(seed);
            var report = Build(rng, scenario);
            result.Report = report;

            // Exercise the same post-acquisition pipeline used by the application.
            Pass12NormalizationService.Apply(report);
            StorageInterpretationService.Interpret(report);
            StorageHealthAssessmentService.Record(report);
            Scoring.Calculate(report);
            SmartInterpretation.NormalizeReport(report);
            AdvancedAssessment.Apply(report);
            SmartInterpretation.RefreshSummary(report);
            CustomerHealthAssessmentService.Apply(report);

            Validate(result);
            return result;
        }

        public static List<RunResult> RunBatch(int seed, int machinesPerScenario)
        {
            var results = new List<RunResult>();
            foreach (var scenario in Scenarios)
                for (var i = 0; i < machinesPerScenario; i++)
                    results.Add(Run(seed + (i * 997) + Array.IndexOf(Scenarios, scenario) * 100003, scenario));
            return results;
        }

        private static InspectionReport Build(Random r, string scenario)
        {
            var now = DateTime.UtcNow;
            var report = new InspectionReport
            {
                InspectionId = "SYN-" + r.Next(100000, 999999),
                StartedAt = now.AddMinutes(-5),
                CompletedAt = now,
                IsAdministrator = true,
                CustomerReference = "SYNTHETIC-CUSTOMER",
                JobNumber = "SYN-" + r.Next(1000, 9999),
                Technician = "Synthetic Lab",
                MemoryUsedPercent = 25 + r.NextDouble() * 45
            };

            AddSystem(report, r);
            AddMemoryModules(report, r);
            AddVolumes(report, r);
            AddGpu(report, r);
            AddSensors(report, r);
            AddBattery(report, r);
            AddEvents(report, r);
            AddDevices(report, r);
            AddIntegrityEvidence(report, scenario);
            AddStress(report, r, scenario);
            AddStorage(report, r, scenario);
            AddProviderEvidence(report, r, scenario);
            return report;
        }

        private static void AddSystem(InspectionReport x, Random r)
        {
            x.System["Manufacturer"] = Pick(r, "Hewlett-Packard", "Dell", "Lenovo", "ASUS");
            x.System["Model"] = Pick(r, "23-d250ee", "OptiPlex 7050", "ThinkCentre M720", "VivoBook X");
            x.System["Serial"] = "SYN" + r.Next(1000000, 9999999);
            x.System["BIOS"] = "SYN-BIOS-" + r.Next(10, 99);
            x.System["OS"] = Pick(r, "Windows 10 Home 22H2", "Windows 10 Pro 22H2", "Windows 11 Pro 24H2");
            x.System["CPU"] = Pick(r, "Intel(R) Core(TM) i7-3770S", "Intel(R) Core(TM) i5-8500", "AMD Ryzen 5 5600G");
            x.System["GPU"] = Pick(r, "Intel HD Graphics 4000", "Intel UHD Graphics 630", "AMD Radeon Graphics");
            x.System["GPU Driver"] = "Synthetic-" + r.Next(1, 30) + "." + r.Next(1000, 9999);
            x.System["Last boot"] = DateTime.UtcNow.AddHours(-r.Next(1, 240)).ToString("O");
        }

        private static void AddMemoryModules(InspectionReport x, Random r)
        {
            x.System["Physical RAM"] = (16L * 1024 * 1024 * 1024).ToString();
            x.System["RAM Module 1"] = "8 GB | " + Pick(r, "Samsung", "OSCOO") + " | DDR3-1600 | SYN-PART-1";
            x.System["RAM Module 2"] = "8 GB | " + Pick(r, "Samsung", "Kingston") + " | DDR3-1600 | SYN-PART-2";
        }

        private static void AddVolumes(InspectionReport x, Random r)
        {
            var free = 15 + r.NextDouble() * 60;
            x.Volumes.Add(new VolumeRecord { Name = "C:", TotalBytes = 256L * 1000 * 1000 * 1000, FreeBytes = (long)(256L * 1000 * 1000 * 1000 * free / 100) });
            x.Volumes.Add(new VolumeRecord { Name = "D:", TotalBytes = 500L * 1000 * 1000 * 1000, FreeBytes = (long)(500L * 1000 * 1000 * 1000 * (25 + r.NextDouble() * 60) / 100) });
        }

        private static void AddGpu(InspectionReport x, Random r)
        {
            x.Sensors.Add(new SensorRecord { Hardware = "Synthetic GPU", Name = "GPU Core Temperature", Type = "Temperature", Current = 40 + r.Next(0, 20), Maximum = 45 + r.Next(0, 30), Unit = "C" });
            x.Sensors.Add(new SensorRecord { Hardware = "Synthetic GPU", Name = "GPU Load", Type = "Load", Current = 10 + r.Next(0, 30), Unit = "%" });
        }

        private static void AddSensors(InspectionReport x, Random r)
        {
            var cpu = 35 + r.Next(0, 30);
            x.Sensors.Add(new SensorRecord { Hardware = "Synthetic CPU", Name = "CPU Package", Type = "Temperature", Current = cpu, Maximum = cpu + r.Next(5, 15), Unit = "C" });
            x.Sensors.Add(new SensorRecord { Hardware = "Synthetic CPU", Name = "CPU Core Clock", Type = "Clock", Current = 3400, Maximum = 3900, Unit = "MHz" });
            x.Measurements.Add(new Measurement { Target = "CPU telemetry", Source = "LibreHardwareMonitor", Status = "Observed", Reason = "Synthetic telemetry", Response = "load=normal" });
        }

        private static void AddBattery(InspectionReport x, Random r)
        {
            if (r.Next(0, 3) == 0)
            {
                x.BatteryDesignedCapacity = 50000;
                x.BatteryFullChargeCapacity = 42000;
                x.BatteryWearPercent = 16;
                x.System["Battery"] = "Present";
            }
            else x.System["Battery"] = "Not present";
        }

        private static void AddEvents(InspectionReport x, Random r)
        {
            x.Events.Add(new EventFinding { Source = "System", EventId = 6005, Level = "Information", Count = 3, Latest = DateTime.UtcNow, Summary = "Synthetic system startup", Signature = "startup" });
        }

        private static void AddDevices(InspectionReport x, Random r)
        {
            x.System["Problem devices"] = "None reported";
        }

        private static void AddIntegrityEvidence(InspectionReport x, string scenario)
        {
            if (scenario == "missing-evidence" || scenario == "sparse-machine") return;
            var status = scenario == "windows-integrity-problem" ? "Critical" : "Observed";
            x.Measurements.Add(new Measurement { Target = "Windows integrity — SFC", Source = "Synthetic SFC", Status = status, Reason = status == "Critical" ? "Synthetic corruption detected" : "No integrity violations reported", Response = "synthetic" });
            x.Measurements.Add(new Measurement { Target = "Windows integrity — DISM", Source = "Synthetic DISM", Status = status, Reason = status == "Critical" ? "Synthetic component corruption detected" : "No component corruption reported", Response = "synthetic" });
            x.Measurements.Add(new Measurement { Target = "Windows integrity — CHKDSK", Source = "Synthetic CHKDSK", Status = "Observed", Reason = "No file-system problems reported", Response = "synthetic" });
        }

        private static void AddStress(InspectionReport x, Random r, string scenario)
        {
            var hot = scenario == "thermal-problem";
            var peak = hot ? 96 : 60 + r.Next(0, 10);
            x.CpuStressTest = new CpuStressResult { StartedAt = DateTime.UtcNow.AddMinutes(-2), CompletedAt = DateTime.UtcNow, Status = hot ? "Thermal abort" : "Completed", StopReason = hot ? "Synthetic thermal limit" : "Synthetic duration complete", LogicalWorkers = 8, PlannedDurationSeconds = 60, ActualDurationSeconds = hot ? 24 : 60, MaximumTemperatureC = peak, BaselineTemperatureC = 42, BaselineClockMHz = 3400, MinimumObservedClockMHz = hot ? 1800 : 3200, MaximumObservedClockMHz = 3900, WorkIterations = 1000000 };
            for (var i = 0; i < 6; i++) x.CpuStressTest.Samples.Add(new CpuStressSample { ElapsedSeconds = i * 10, ElapsedMilliseconds = i * 10000, CapturedAt = DateTime.UtcNow.AddSeconds(-60 + i * 10), TargetLoadPercent = 100, TemperatureC = hot ? 70 + i * 5 : 55 + i, ObservedCpuLoadPercent = 98, AverageCoreClockMHz = hot ? 2500 : 3500, MaximumCoreClockMHz = 3900, MemoryUsedPercent = x.MemoryUsedPercent, GpuLoadPercent = 15, GpuTemperatureC = 50, TelemetryFreshness = "Observed", TelemetryPollIntervalMilliseconds = 1000, SafetySampleValid = true, SafetyAssessment = "Observed" });
        }

        private static void AddStorage(InspectionReport x, Random r, string scenario)
        {
            if (scenario == "sparse-machine") return;
            if (scenario == "healthy-nvme")
            {
                var d = Drive("NVMe-SYN-1", "NVME-SYN-001", 512L * 1000 * 1000 * 1000, "NVMe", r);
                d.NvmeHealth = new NvmeHealthRecord { CriticalWarning = 0, AvailableSparePercent = 100, AvailableSpareThresholdPercent = 10, PercentageUsed = 12, MediaErrors = 0, ErrorLogEntries = 0, TemperatureC = 43, DataUnitsRead = 10000, DataUnitsWritten = 7000 };
                d.StorageEvidence.HasNvmeHealth = true; d.StorageEvidence.HasEndurance = true; d.RemainingLifePercent = 88; d.EnduranceUsedPercent = 12; d.SmartPassed = true;
                x.Drives.Add(d); return;
            }
            if (scenario == "failing-hdd" || scenario == "mixed-faults")
            {
                var d = Drive("ST1000DM003-SYN", "Z1DSYN", 1000L * 1000 * 1000 * 1000, "ATA", r);
                d.SmartPassed = true; d.SmartAttributes.Add(Attr(5, "Reallocated_Sector_Ct", 3, true)); d.SmartAttributes.Add(Attr(197, "Current_Pending_Sector", 8, true)); d.SmartAttributes.Add(Attr(198, "Offline_Uncorrectable", 1, true)); d.SmartAttributes.Add(Attr(199, "UDMA_CRC_Error_Count", 0, true));
                x.Drives.Add(d);
            }
            else
            {
                var d = Drive(scenario == "aging-ssd" ? "HS-SSD-WAVE(S) 256G" : "SYN-SSD-256G", scenario == "aging-ssd" ? "30128481982" : "SYN-SSD", 256L * 1000 * 1000 * 1000, "SATA", r);
                d.SmartPassed = true;
                var life = scenario == "aging-ssd" ? 36 : 82 + r.Next(0, 15);
                d.SmartAttributes.Add(Attr(231, "SSD_Life_Left", life, true));
                d.SmartAttributes.Add(Attr(5, "Reallocated_Sector_Ct", 0, true));
                d.SmartAttributes.Add(Attr(197, "Current_Pending_Sector", 0, true));
                d.SmartAttributes.Add(Attr(198, "Offline_Uncorrectable", 0, true));
                d.SmartAttributes.Add(Attr(199, "UDMA_CRC_Error_Count", 0, true));
                d.TemperatureC = 48; d.StorageEvidence.HasAtaSmart = true; d.StorageEvidence.HasEndurance = true; d.StorageEvidence.HasTemperature = true;
                d.StorageEvidence.Quality = "Validated"; d.StorageEvidence.Availability = "Measured"; d.StorageEvidence.Source = "smartctl JSON";
                d.LinkCurrent = "3.0 Gb/s"; d.LinkMaximum = "6.0 Gb/s";
                x.Drives.Add(d);
            }
        }

        private static void AddProviderEvidence(InspectionReport x, Random r, string scenario)
        {
            var providers = new[] { "smartctl", "WMI", "LibreHardwareMonitor", "CrystalDiskInfo", "Windows Storage API" };
            foreach (var p in providers)
                x.Measurements.Add(new Measurement { Target = "Provider attempt — storage", Source = p, Status = scenario == "provider-fallback" && p == "smartctl" ? "Failed" : "Observed", Reason = scenario == "provider-fallback" && p == "smartctl" ? "Synthetic provider failure; fallback required" : "Synthetic provider response", Response = p });
            if (scenario == "conflicting-providers")
            {
                x.Measurements.Add(new Measurement { Target = "Storage provenance — life", Source = "smartctl", Status = "Observed", Reason = "36% remaining", Response = "36" });
                x.Measurements.Add(new Measurement { Target = "Storage provenance — life", Source = "CrystalDiskInfo", Status = "Observed", Reason = "64% used / 36% remaining", Response = "36" });
            }
            if (scenario == "provider-fallback")
                x.Measurements.Add(new Measurement { Target = "Storage provenance — temperature", Source = "LibreHardwareMonitor", Status = "Observed", Reason = "Fallback provider supplied temperature", Response = "48 C" });
        }

        private static DriveInfoRecord Drive(string model, string serial, long size, string iface, Random r)
        {
            return new DriveInfoRecord { Model = model, Serial = serial, SizeBytes = size, Interface = iface, Firmware = "SYN-FW-1", Vendor = "Synthetic", Product = model, DeviceId = "\\\\.\\PHYSICALDRIVE" + r.Next(0, 3), SmartDeviceType = iface, Transport = iface, SmartStatus = "Threshold check passed (not a health percentage)", StorageEvidence = new StorageEvidenceInfo { Availability = "Measured", Quality = "Validated", Source = "Synthetic provider", DeviceType = iface, Transport = iface, SerialValidated = true } };
        }

        private static SmartAttributeRecord Attr(int id, string name, long raw, bool validated)
        {
            return new SmartAttributeRecord { Id = id, Name = name, RawValue = raw, NormalizedValue = raw, WorstValue = raw, Threshold = 0, RawString = raw.ToString(), Interpretation = "Synthetic validated attribute", Source = "smartctl", SemanticsValidated = validated };
        }

        private static string Pick(Random r, params string[] values) { return values[r.Next(values.Length)]; }

        private static void Validate(RunResult result)
        {
            var r = result.Report;
            void Fail(string s) { result.Failures.Add(s); }
            if (r == null) { Fail("No report generated"); result.Passed = false; return; }
            if (r.Drives.Count > 0)
            {
                foreach (var d in r.Drives)
                {
                    var life = d.SmartAttributes.FirstOrDefault(a => a != null && a.Id == 231 && a.SemanticsValidated);
                    if (life != null && life.RawValue == 36 && Math.Abs((d.RemainingLifePercent ?? -1) - 36) > 0.01) Fail("SSD life 36 was not preserved as 36% remaining");
                    if (life != null && life.RawValue == 36 && Math.Abs((d.EnduranceUsedPercent ?? -1) - 64) > 0.01) Fail("SSD endurance 36% remaining was not normalized to 64% used");
                    if (d.SmartPassed == true && d.SmartStatus.IndexOf("not a health percentage", StringComparison.OrdinalIgnoreCase) < 0) Fail("SMART threshold pass was converted into a health percentage");
                    if (d.SmartAttributes.Any(a => a.SemanticsValidated && a.Id == 231 && a.RawValue.HasValue && a.RawValue.Value == 36) && d.RemainingLifePercent == 0) Fail("Missing/invalid normalization manufactured zero life");
                }
            }
            if (result.Scenario == "failing-hdd" || result.Scenario == "mixed-faults")
                if (r.CustomerHealth.OverallStatus != "CRITICAL") Fail("Known critical storage fault did not propagate to customer overall status");
            if (result.Scenario == "thermal-problem")
                if (r.CustomerHealth.Components.All(c => c.Status != "CRITICAL")) Fail("Thermal abort did not produce a critical customer component");
            if (result.Scenario == "windows-integrity-problem")
                if (r.CustomerHealth.Components.All(c => c.Component != "Windows" || c.Status != "CRITICAL")) Fail("Windows integrity failure did not propagate");
            if (result.Scenario == "sparse-machine")
                if (r.CustomerHealth.OverallStatus == "GOOD") Fail("Sparse evidence was incorrectly declared healthy");
            result.Passed = result.Failures.Count == 0;
        }
    }
}
