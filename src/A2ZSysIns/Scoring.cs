using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    public static class Scoring
    {
        public static void Calculate(InspectionReport r)
        {
            r.Findings.Clear();
            r.Scores.Clear();
            r.PriorityActions.Clear();

            AssessStorage(r);
            AssessThermals(r);
            AssessEvents(r);
            AssessResources(r);
            AssessBattery(r);
            AssessWindowsDevices(r);
            AssessCoverage(r);
            BuildOverallSummary(r);

            EvidenceEngine.Log(r, "Report assessment", r.OverallStatus + " | " + r.CustomerSummary);
            foreach (var f in r.Findings)
                EvidenceEngine.Log(r, "Finding", f.Severity + " | " + f.Title + " | confidence=" + f.Confidence + " | action=" + f.ActionLevel + " | " + f.Explanation + " Evidence: " + f.Evidence);
        }

        private static void AssessStorage(InspectionReport r)
        {
            if (r.Drives.Count == 0)
            {
                Add(r, "Storage health", "NOT TESTED", "Physical-drive health information was unavailable. A healthy result must not be inferred.");
                return;
            }

            foreach (var d in r.Drives)
            {
                d.Assessment = EvidenceEngine.DriveAssessment(d);
                var life = d.RemainingLifePercent.HasValue
                    ? d.RemainingLifePercent.Value.ToString("0") + "% reported endurance/life remaining; " + d.LifeMeaning
                    : "No validated remaining-life value was available.";
                Add(r, "Storage — " + Safe(d.Model), StorageStatus(d), HumanDriveReason(d) + " " + life);

                long A(string key) => d.Attributes.TryGetValue(key, out var n) ? n : 0;
                var critical = d.SmartPassed == false || A("NVMe_critical_warning") != 0 || A("ATA_197") > 0 || A("ATA_198") > 0;
                var mediaErrors = A("ATA_5") > 0 || A("NVMe_media_errors") > 0;
                var interfaceErrors = A("ATA_199") > 0;

                if (critical)
                {
                    Finding(r, "Critical", "Storage", "Drive may be at risk of failure — " + Safe(d.Model),
                        CriticalDriveExplanation(d),
                        "Back up important data immediately. Avoid unnecessary stress/write tests until the data is protected, then investigate replacement or recovery.",
                        DriveEvidence(d), "High", "Immediate");
                }
                else if (mediaErrors)
                {
                    Finding(r, "Attention", "Storage", "Storage media errors detected — " + Safe(d.Model),
                        "The drive reported reallocated/media-error evidence. This does not prove immediate failure, but it is a meaningful deterioration signal.",
                        "Confirm backups, review the raw SMART values and monitor whether the counts increase. Replace the drive if errors progress or symptoms are present.",
                        DriveEvidence(d), "High", "Recommended");
                }
                else if (interfaceErrors)
                {
                    Finding(r, "Attention", "Storage", "Storage interface errors detected — " + Safe(d.Model),
                        "Communication errors were reported between the drive and host. These can be caused by a cable, connector, enclosure or controller and do not by themselves prove damaged media.",
                        "Check the storage connection/path and monitor the error count. Do not diagnose drive failure from this attribute alone.",
                        DriveEvidence(d), "Moderate", "Recommended");
                }

                if (d.RemainingLifePercent.HasValue && d.RemainingLifePercent.Value <= 10 && !critical)
                    Finding(r, "Attention", "Storage", "Drive endurance indicator is low — " + Safe(d.Model),
                        "The validated device endurance/life indicator reports approximately " + d.RemainingLifePercent.Value.ToString("0") + "% remaining. This is an endurance estimate, not a failure probability.",
                        "Keep current backups and plan replacement based on workload, symptoms and the device's remaining service requirements.",
                        d.LifeMeaning, "High", "Recommended");
            }
        }

        private static void AssessThermals(InspectionReport r)
        {
            var cpuTemps = r.Sensors.Where(IsCpuTemperature).ToList();
            var cpuLoads = r.Sensors.Where(IsCpuLoad).ToList();
            var lowLoadVerified = cpuLoads.Count > 0 && cpuLoads.Max(x => x.Maximum ?? x.Current ?? 100) <= 20;

            if (cpuTemps.Count == 0)
            {
                Add(r, "CPU temperature", "NOT TESTED", "An actual CPU package/core temperature could not be measured. No thermal health conclusion is made.");
            }
            else
            {
                var peak = cpuTemps.Max(x => (double)(x.Maximum ?? x.Current ?? 0));
                var current = cpuTemps.Max(x => (double)(x.Current ?? x.Maximum ?? 0));
                var context = lowLoadVerified ? "CPU load stayed at or below about 20% during the observation." : "The short sensor snapshot was not proven to be an idle period.";
                var status = peak >= 90 ? "CRITICAL" : peak >= 80 ? "ATTENTION" : peak >= 70 && lowLoadVerified ? "ATTENTION" : "GOOD";
                Add(r, "CPU temperature", status, "Observed CPU temperature: current " + current.ToString("0.0") + " °C, short-sample peak " + peak.ToString("0.0") + " °C. " + context);

                if (peak >= 90)
                    Finding(r, "Critical", "Thermals", "CPU temperature reached a dangerous range",
                        "An actual CPU temperature sensor reached " + peak.ToString("0.0") + " °C during inspection.",
                        "Stop unnecessary heavy load and inspect cooling, airflow, fan operation and thermal contact before continued stress testing.",
                        "Actual LibreHardwareMonitor CPU temperature samples", "High", "Immediate");
                else if (peak >= 80)
                    Finding(r, "Attention", "Thermals", "CPU temperature is high",
                        "CPU temperature reached " + peak.ToString("0.0") + " °C during the short observation. " + context,
                        "Check current workload and cooling condition. Use the staged CPU stress test, when safe, to determine loaded thermal behavior.",
                        "Actual LibreHardwareMonitor CPU temperature samples", lowLoadVerified ? "High" : "Moderate", "Recommended");
                else if (peak >= 70 && lowLoadVerified)
                    Finding(r, "Attention", "Thermals", "CPU temperature is elevated at low load",
                        "CPU temperature reached " + peak.ToString("0.0") + " °C while measured CPU load remained low. High low-load temperature can justify cooling inspection even if the processor survives a short stress test.",
                        "Check background activity first, then inspect airflow/fan/thermal condition if the temperature remains elevated at verified low load.",
                        "CPU temperature plus CPU-load sensor samples", "Moderate", "Recommended");
            }

            if (r.CpuStressTest == null)
            {
                Add(r, "CPU staged stress test", "NOT RUN", "Optional 60-second staged safety test. A normal sensor snapshot does not substitute for a loaded thermal test.");
                return;
            }

            var test = r.CpuStressTest;
            var max = test.MaximumTemperatureC;
            var baseline = test.BaselineTemperatureC;
            if (test.Status == "Completed" && max.HasValue)
            {
                var status = max.Value < 80 ? "GOOD" : max.Value < 85 ? "ACCEPTABLE" : "ATTENTION";
                Add(r, "CPU staged stress test", status,
                    "Completed " + test.ActualDurationSeconds + " seconds. Pre-test " + Num(baseline) + "; peak " + Num(max) + ". " + ThermalMeaning(max.Value));
                if (max.Value >= 85)
                    Finding(r, "Attention", "Thermals", "CPU runs hot under staged load",
                        "The CPU stress test completed but reached " + max.Value.ToString("0.0") + " °C. The test did not reach the 90 °C safety-abort threshold.",
                        "Inspect cooling condition if this temperature is unexpected for the device or if the customer reports throttling, fan or shutdown symptoms.",
                        "Integrated staged CPU stress-test samples", "High", "Recommended");
            }
            else if (test.Status == "Thermal abort")
            {
                Add(r, "CPU staged stress test", "CRITICAL", "The safety limit stopped the test. Peak " + Num(max) + ".");
                Finding(r, "Critical", "Thermals", "CPU stress test stopped at thermal safety limit",
                    "The staged load test was automatically stopped because CPU temperature reached the configured 90 °C safety limit.",
                    "Do not repeat heavy stress testing until cooling is inspected.",
                    "Integrated staged CPU stress-test samples", "High", "Immediate");
            }
            else if (test.Status == "Refused")
            {
                Add(r, "CPU staged stress test", "NOT RUN", test.StopReason);
                if (baseline.HasValue && baseline.Value >= 80)
                    Finding(r, "Attention", "Thermals", "CPU was too hot to start the stress test",
                        "The safety preflight refused the test at " + baseline.Value.ToString("0.0") + " °C.",
                        "Check current workload and cooling before attempting a load test.",
                        "Stress-test safety preflight", "High", "Recommended");
            }
            else
            {
                Add(r, "CPU staged stress test", test.Status.ToUpperInvariant(), test.StopReason);
            }
        }

        private static void AssessEvents(InspectionReport r)
        {
            if (r.Events.Count == 0)
            {
                Add(r, "Windows event history", "NO FLAGGED EVENTS", "No matching monitored event patterns were found in the available 30-day event history. Event-log retention may limit coverage.");
                return;
            }

            foreach (var e in r.Events)
            {
                if (e.EventId == 41 || e.EventId == 6008 || e.EventId == 1074)
                {
                    Add(r, "Event — " + e.Summary, "INFORMATION", e.Count + " occurrence(s) in 30 days. " + e.Cause);
                    continue;
                }

                if (e.Source == "Microsoft-Windows-WHEA-Logger" && e.EventId == 18)
                {
                    var sev = e.Count >= 2 ? "Critical" : "Attention";
                    Finding(r, sev, "Hardware stability", "Windows hardware-error event detected",
                        "Windows recorded " + e.Count + " WHEA hardware-error event(s) in the last 30 days. This is stronger evidence than an unexpected-shutdown event, but the failing component still requires investigation.",
                        "Review the WHEA event details and correlate with CPU, memory, PCIe, storage and symptom evidence before replacing parts.",
                        e.Signature, "High", e.Count >= 2 ? "Immediate" : "Recommended");
                    Add(r, "Event — WHEA hardware error", e.Count >= 2 ? "CRITICAL" : "ATTENTION", e.Count + " occurrence(s); inspect raw event evidence.");
                    continue;
                }

                if (e.Source == "Disk" && e.EventId == 7)
                {
                    Finding(r, "Critical", "Storage", "Windows reported a disk bad-block/read-write error",
                        "Windows recorded " + e.Count + " Disk Event 7 occurrence(s). This is a significant storage warning and should be correlated with SMART evidence.",
                        "Protect important data immediately and investigate the affected storage device/path.",
                        e.Signature, "High", "Immediate");
                    Add(r, "Event — Disk error", "CRITICAL", e.Count + " occurrence(s) in 30 days.");
                    continue;
                }

                if (e.EventId == 55 && (e.Source == "Ntfs" || e.Source == "Microsoft-Windows-Ntfs"))
                {
                    Finding(r, "Attention", "Storage", "NTFS file-system error recorded",
                        "Windows recorded " + e.Count + " NTFS Event 55 occurrence(s). File-system corruption can have several causes and does not by itself prove physical drive failure.",
                        "Back up important data and correlate with SMART/storage events before deciding on repair or replacement.",
                        e.Signature, "High", "Recommended");
                    Add(r, "Event — NTFS error", "ATTENTION", e.Count + " occurrence(s) in 30 days.");
                    continue;
                }

                if (e.EventId == 1001)
                {
                    var repeated = e.Count >= 3;
                    Add(r, "Event — BSOD / bug check", repeated ? "ATTENTION" : "INFORMATION", e.Count + " occurrence(s) in 30 days. Root cause is not established by the event alone.");
                    Finding(r, repeated ? "Attention" : "Information", "Stability", repeated ? "Repeated Windows crash/bug-check history" : "Windows crash/bug-check recorded",
                        e.Count + " bug-check record(s) were found. A bug-check proves Windows crashed, but not which hardware or driver caused it.",
                        repeated ? "Review dump/WHEA/storage evidence and reproduce symptoms before diagnosing a component." : "Monitor for recurrence and investigate if the customer reports instability.",
                        e.Signature, "High", repeated ? "Recommended" : "Monitor");
                    continue;
                }

                if (e.EventId == 1000)
                {
                    var repeated = e.Count >= 3;
                    Add(r, "Event — " + e.Summary, repeated ? "ATTENTION" : "INFORMATION", e.Count + " occurrence(s). Application crashes are not system-hardware failures by themselves.");
                    if (repeated)
                        Finding(r, "Attention", "Software stability", "Repeated application crash pattern",
                            e.Summary + " occurred " + e.Count + " times. " + e.Cause,
                            "Review the affected application and supporting Windows evidence. Do not replace hardware based on this event alone.",
                            e.Signature, "High", "Recommended");
                    continue;
                }

                Add(r, "Event — " + e.Summary, "REVIEW", e.Count + " occurrence(s). " + e.Cause);
            }
        }

        private static void AssessResources(InspectionReport r)
        {
            foreach (var v in r.Volumes.Where(x => x.TotalBytes > 0))
            {
                var pct = v.FreeBytes * 100.0 / v.TotalBytes;
                var status = pct < 5 ? "ATTENTION" : pct < 10 ? "LOW" : "GOOD";
                Add(r, "Free space — " + v.Name, status, pct.ToString("0.0") + "% free. Low free space can affect Windows operation but is not proof of failing storage hardware.");
                if (pct < 5)
                    Finding(r, "Attention", "Resources", "System volume has very little free space — " + v.Name,
                        "Only " + pct.ToString("0.0") + "% of the volume is free. This can contribute to update, paging and application problems.",
                        "Review storage usage with customer approval. Do not automatically delete customer files.",
                        "Saved volume-capacity snapshot", "High", "Recommended");
            }
            if (r.Volumes.Count == 0)
                Add(r, "Volume free space", "NOT TESTED", "Volume capacity information was unavailable.");

            if (r.MemoryUsedPercent.HasValue)
            {
                var used = r.MemoryUsedPercent.Value;
                Add(r, "RAM usage snapshot", used >= 90 ? "HIGH USAGE" : "INFORMATION", used.ToString("0.0") + "% in use. This measures workload only; it is not a RAM integrity test.");
                if (used >= 90)
                    Finding(r, "Information", "Resources", "RAM usage was very high during inspection",
                        used.ToString("0.0") + "% of physical memory was in use at the time of measurement.",
                        "Review the active workload if the customer reports slowness. Run a dedicated memory-integrity test if faulty RAM is suspected.",
                        "GlobalMemoryStatusEx/WMI memory snapshot", "High", "Monitor");
            }
            else
                Add(r, "RAM usage snapshot", "NOT TESTED", "RAM utilization could not be measured. RAM integrity is not tested by this inspection.");

            Add(r, "RAM integrity", "NOT TESTED", "No destructive or extended memory-integrity test is currently performed. Normal RAM usage must never be reported as proof that RAM is healthy.");
        }

        private static void AssessBattery(InspectionReport r)
        {
            if (!r.BatteryWearPercent.HasValue)
            {
                Add(r, "Battery capacity", "NOT TESTED / NOT PRESENT", "Battery wear could not be measured or no battery was reported. Current charge level is not used as a wear indicator.");
                return;
            }

            var wear = r.BatteryWearPercent.Value;
            var status = wear >= 60 ? "POOR" : wear >= 40 ? "ATTENTION" : wear >= 20 ? "USED" : "GOOD";
            Add(r, "Battery capacity", status, wear.ToString("0.0") + "% capacity loss from design value. Full-charge capacity " +
                r.BatteryFullChargeCapacity.Value.ToString("0") + " of design " + r.BatteryDesignedCapacity.Value.ToString("0") + ".");
            if (wear >= 40)
                Finding(r, wear >= 60 ? "Attention" : "Information", "Battery", wear >= 60 ? "Battery capacity is heavily degraded" : "Battery capacity is reduced",
                    "Measured full-charge capacity is approximately " + (100 - wear).ToString("0") + "% of the design capacity.",
                    wear >= 60 ? "Confirm runtime under normal use and consider battery replacement if portable runtime is important." : "Monitor real-world runtime; replacement is optional unless service time is inadequate.",
                    "Windows battery capacity measurement", "High", wear >= 60 ? "Recommended" : "Monitor");
        }

        private static void AssessWindowsDevices(InspectionReport r)
        {
            if (!r.System.TryGetValue("Problem devices", out var devices) || string.IsNullOrWhiteSpace(devices) || devices == "Not measured")
            {
                Add(r, "Windows device status", "NOT TESTED", "Windows problem-device status was unavailable.");
                return;
            }
            if (devices.StartsWith("None reported", StringComparison.OrdinalIgnoreCase))
                Add(r, "Windows device status", "GOOD", "Windows did not report a Config Manager error for the devices returned by this query.");
            else
            {
                Add(r, "Windows device status", "ATTENTION", devices);
                Finding(r, "Attention", "Windows devices", "Windows reports one or more device errors",
                    devices,
                    "Review Device Manager/error codes and the affected device before assuming hardware replacement is required.",
                    "Win32_PnPEntity ConfigManagerErrorCode", "High", "Recommended");
            }
        }

        private static void AssessCoverage(InspectionReport r)
        {
            var unavailable = r.Measurements.Count(x => x.Status == "Unavailable" || x.Status == "Failed");
            Add(r, "Inspection coverage", unavailable == 0 ? "COMPLETE FOR AVAILABLE CHECKS" : "PARTIAL",
                unavailable == 0 ? "No collector explicitly reported failure/unavailability." : unavailable + " measurement target(s) reported unavailable/failed. See 'What could not be tested'.");
        }

        private static void BuildOverallSummary(InspectionReport r)
        {
            var critical = r.Findings.Count(x => x.Severity == "Critical");
            var attention = r.Findings.Count(x => x.Severity == "Attention");
            var unavailable = r.Measurements.Count(x => x.Status == "Unavailable" || x.Status == "Failed");

            r.OverallScore = null;
            if (critical > 0)
            {
                r.OverallStatus = "CRITICAL — immediate action recommended";
                r.CustomerSummary = "The inspection found " + critical + " high-risk indicator(s) that could be associated with data loss, hardware instability or unsafe thermal operation. Protect important data and address the critical findings before normal heavy use.";
            }
            else if (attention > 0)
            {
                r.OverallStatus = "ATTENTION — service or monitoring recommended";
                r.CustomerSummary = "No critical failure indicator was confirmed, but " + attention + " condition(s) need attention. Review the recommended actions and monitor the computer for worsening symptoms.";
            }
            else if (unavailable > 0)
            {
                r.OverallStatus = "NO CRITICAL INDICATOR FOUND — assessment incomplete";
                r.CustomerSummary = "The checks that returned evidence did not show a critical failure indicator, but some measurements were unavailable. Unmeasured components must not be assumed healthy.";
            }
            else
            {
                r.OverallStatus = "NO CRITICAL INDICATOR FOUND";
                r.CustomerSummary = "No high-risk failure indicator was detected by the checks performed. This is a screening result, not a guarantee of future reliability.";
            }

            r.PriorityActions.AddRange(r.Findings.Where(x => x.ActionLevel == "Immediate").Select(x => x.Title).Distinct());
            r.PriorityActions.AddRange(r.Findings.Where(x => x.ActionLevel == "Recommended").Select(x => x.Title).Distinct());
            if (r.PriorityActions.Count == 0) r.PriorityActions.Add("No immediate repair action was generated from the available evidence; keep normal backups and monitor reported symptoms.");
        }

        private static string StorageStatus(DriveInfoRecord d)
        {
            if (d.Assessment.StartsWith("Critical")) return "CRITICAL";
            if (d.Assessment.StartsWith("Attention")) return "ATTENTION";
            return d.Assessment == "Not assessed" ? "NOT TESTED" : "GOOD / NO FLAGGED INDICATOR";
        }

        private static string HumanDriveReason(DriveInfoRecord d)
        {
            if (d.Assessment.StartsWith("Critical")) return "SMART/device evidence contains a high-risk failure indicator.";
            if (d.Assessment.StartsWith("Attention")) return "SMART/device evidence contains errors that require review.";
            if (d.Assessment == "Not assessed") return "Drive health evidence could not be established.";
            return "No failure indicator was flagged in the SMART/device fields this Inspector knows how to interpret. This does not guarantee future reliability.";
        }

        private static string CriticalDriveExplanation(DriveInfoRecord d)
        {
            var evidence = new List<string>();
            long A(string key) => d.Attributes.TryGetValue(key, out var n) ? n : 0;
            if (d.SmartPassed == false) evidence.Add("the device SMART threshold check reports failure");
            if (A("ATA_197") > 0) evidence.Add("pending sectors=" + A("ATA_197"));
            if (A("ATA_198") > 0) evidence.Add("uncorrectable sectors=" + A("ATA_198"));
            if (A("NVMe_critical_warning") != 0) evidence.Add("NVMe critical_warning=" + A("NVMe_critical_warning"));
            return "High-risk storage evidence was detected: " + string.Join(", ", evidence) + ". These indicators deserve immediate data-protection action.";
        }

        private static string DriveEvidence(DriveInfoRecord d)
        {
            long A(string key) => d.Attributes.TryGetValue(key, out var n) ? n : 0;
            return "SMART threshold=" + d.SmartStatus + "; reallocated=" + A("ATA_5") + "; pending=" + A("ATA_197") + "; uncorrectable=" + A("ATA_198") + "; interfaceCRC=" + A("ATA_199") + "; NVMe critical warning=" + A("NVMe_critical_warning") + "; NVMe media errors=" + A("NVMe_media_errors");
        }

        private static bool IsCpuTemperature(SensorRecord s)
        {
            if (!EvidenceEngine.ActualTemperature(s)) return false;
            var hardware = s.Hardware ?? "";
            var name = s.Name ?? "";
            return hardware.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Core Average", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCpuLoad(SensorRecord s)
        {
            if (s.Type != "Load") return false;
            var hardware = s.Hardware ?? "";
            var name = s.Name ?? "";
            return hardware.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Total", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ThermalMeaning(double max)
        {
            if (max < 75) return "Strong thermal result for this short test; no overheating was observed.";
            if (max < 80) return "Thermally stable in this short test; no overheating was observed.";
            if (max < 85) return "Warm but below the Inspector attention threshold for the completed stress test.";
            return "High loaded temperature; cooling review is recommended even though the safety limit was not reached.";
        }

        private static string Num(double? value) => value.HasValue ? value.Value.ToString("0.0") + " °C" : "temperature unavailable";
        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown drive" : value;

        private static void Add(InspectionReport r, string name, string status, string reason) =>
            r.Scores.Add(new CategoryScore { Category = name, Score = null, Status = status, Reason = reason });

        private static void Finding(InspectionReport r, string severity, string category, string title, string explanation, string recommendation, string evidence, string confidence, string actionLevel) =>
            r.Findings.Add(new Finding { Severity = severity, Category = category, Title = title, Explanation = explanation, Recommendation = recommendation, Evidence = evidence, Confidence = confidence, ActionLevel = actionLevel });
    }
}
