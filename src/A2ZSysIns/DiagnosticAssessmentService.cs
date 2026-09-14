using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    // Pass 5 foundation: establish category condition/coverage before scoring.
    // This service deliberately does not manufacture a numeric health percentage.
    internal static class DiagnosticAssessmentService
    {
        private const string Prefix = "Diagnostic assessment model — ";

        public static void Record(InspectionReport report)
        {
            if (report == null) return;

            report.Measurements.RemoveAll(x => x != null && (x.Target ?? "").StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));

            Add(report, "Storage", Storage(report));
            Add(report, "Thermals", Thermals(report));
            Add(report, "Windows event history", Events(report));
            Add(report, "Resources", Resources(report));
            Add(report, "Battery", Battery(report));
            Add(report, "Windows devices", Devices(report));

            var known = report.Measurements.Count(x => x != null && x.Status != "Unavailable" && x.Status != "Failed");
            var unavailable = report.Measurements.Count(x => x != null && (x.Status == "Unavailable" || x.Status == "Failed"));
            Add(report, "Coverage", unavailable == 0
                ? "Observed evidence available for all recorded measurements; unavailable tests are not treated as healthy."
                : unavailable + " recorded measurement(s) are unavailable/failed; coverage is tracked separately from condition.");

            EvidenceEngine.Log(report, "Diagnostic assessment model",
                "Cross-category condition/coverage prepared before scoring; numeric overall health percentage intentionally withheld until calibrated.");
        }

        private static void Add(InspectionReport report, string category, string reason)
        {
            report.Measurements.Add(new Measurement
            {
                Target = Prefix + category,
                Source = "DiagnosticAssessmentService",
                Status = StatusFromReason(reason),
                Reason = reason,
                Response = "Condition and evidence coverage are separate from numeric score."
            });
        }

        private static string StatusFromReason(string reason)
        {
            if (reason.StartsWith("CRITICAL", StringComparison.OrdinalIgnoreCase)) return "Critical";
            if (reason.StartsWith("ATTENTION", StringComparison.OrdinalIgnoreCase)) return "Attention";
            if (reason.StartsWith("UNKNOWN", StringComparison.OrdinalIgnoreCase) || reason.StartsWith("NOT TESTED", StringComparison.OrdinalIgnoreCase)) return "Unavailable";
            return "Observed";
        }

        private static string Storage(InspectionReport report)
        {
            if (report.Drives.Count == 0) return "UNKNOWN — no physical-drive health evidence was available.";
            var results = report.Drives.Select(StorageHealthAssessmentService.Assess).ToList();
            if (results.Any(x => x.Condition == "CRITICAL")) return "CRITICAL — at least one drive has a critical condition indicator.";
            if (results.Any(x => x.Condition == "ATTENTION")) return "ATTENTION — at least one drive has a condition indicator requiring review.";
            if (results.All(x => x.Condition == "UNKNOWN")) return "UNKNOWN — storage condition could not be established.";
            return "OBSERVED — no critical storage condition was identified in the available interpreted evidence.";
        }

        private static string Thermals(InspectionReport report)
        {
            var temps = report.Sensors.Where(IsCpuTemperature).ToList();
            if (temps.Count == 0 && report.CpuStressTest == null) return "UNKNOWN — CPU temperature and staged thermal-test evidence were unavailable.";
            var peak = temps.Count == 0 ? 0 : temps.Max(x => (double)(x.Maximum ?? x.Current ?? 0));
            if (report.CpuStressTest != null && report.CpuStressTest.MaximumTemperatureC.HasValue)
                peak = Math.Max(peak, report.CpuStressTest.MaximumTemperatureC.Value);
            if (report.CpuStressTest != null && report.CpuStressTest.Status == "Thermal abort") return "CRITICAL — staged CPU testing stopped at the configured thermal safety limit.";
            if (peak >= 90) return "CRITICAL — observed CPU temperature reached the configured safety range.";
            if (peak >= 80) return "ATTENTION — observed CPU temperature reached a high range.";
            return "OBSERVED — thermal evidence is available without a critical indicator.";
        }

        private static string Events(InspectionReport report)
        {
            if (report.Events.Count == 0) return "OBSERVED — no monitored event pattern was returned; event-log retention may limit coverage.";
            if (report.Events.Any(x => (x.Source == "Disk" && x.EventId == 7) || (x.Source == "Microsoft-Windows-WHEA-Logger" && x.EventId == 18 && x.Count >= 2)))
                return "CRITICAL — monitored hardware/storage event evidence includes a high-risk repeated pattern.";
            if (report.Events.Any(x => (x.Source == "Microsoft-Windows-WHEA-Logger" && x.EventId == 18) || x.EventId == 1001 || x.EventId == 55))
                return "ATTENTION — monitored Windows stability/storage events require correlation.";
            return "OBSERVED — event history was collected without a high-risk monitored pattern.";
        }

        private static string Resources(InspectionReport report)
        {
            var lowSpace = report.Volumes.Any(x => x.TotalBytes > 0 && x.FreeBytes * 100.0 / x.TotalBytes < 5);
            if (report.Volumes.Count == 0 && !report.MemoryUsedPercent.HasValue) return "UNKNOWN — resource evidence was unavailable.";
            return lowSpace ? "ATTENTION — at least one volume has less than 5% free space." : "OBSERVED — resource evidence is available; usage is not treated as component integrity proof.";
        }

        private static string Battery(InspectionReport report)
        {
            if (!report.BatteryWearPercent.HasValue) return "UNKNOWN — battery wear was unavailable or no battery was reported.";
            return report.BatteryWearPercent.Value >= 60 ? "ATTENTION — battery capacity is heavily degraded." : "OBSERVED — battery capacity evidence is available.";
        }

        private static string Devices(InspectionReport report)
        {
            if (!report.System.TryGetValue("Problem devices", out var devices) || string.IsNullOrWhiteSpace(devices) || devices == "Not measured")
                return "UNKNOWN — Windows problem-device status was unavailable.";
            return devices.StartsWith("None reported", StringComparison.OrdinalIgnoreCase)
                ? "OBSERVED — Windows reported no Config Manager error for the returned devices."
                : "ATTENTION — Windows reported one or more problem devices.";
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
    }
}
