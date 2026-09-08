using System;
using System.Globalization;
using System.Linq;

namespace A2ZSysIns
{
    internal static class AdvancedAssessment
    {
        public static void Apply(InspectionReport r)
        {
            AssessWhea(r);
            AssessStorageStack(r);
            AssessPnP(r);
            AssessStorageLinks(r);
            AssessFanCorrelation(r);
            AssessStressPerformance(r);
            AssessSecurityInfo(r);
            RefreshOverall(r);
        }

        private static void AssessWhea(InspectionReport r)
        {
            foreach (var e in r.Events.Where(x => x.Source == "Microsoft-Windows-WHEA-Logger"))
            {
                if (e.EventId == 18) continue;
                var repeated = e.Count > 2;
                r.Scores.Add(new CategoryScore { Category = "WHEA — Event " + e.EventId, Score = null, Status = repeated ? "ATTENTION" : "INFORMATION", Reason = e.Count + " occurrence(s) for this hardware-error signature in 30 days. " + e.Summary });
                if (repeated) AddFinding(r, "Attention", "Hardware stability", "Repeated WHEA hardware-error signature", "Windows recorded the same WHEA hardware-error signature " + e.Count + " times in the last 30 days. This is a genuine recurrence signal, but WHEA evidence must still be correlated with the component/error record before replacement.", "Review the raw WHEA records and correlate with CPU, memory, PCIe, storage and device symptoms.", e.Signature, "High", "Recommended");
            }
        }

        private static void AssessStorageStack(InspectionReport r)
        {
            foreach (var e in r.Events.Where(x => (x.Signature ?? "").StartsWith("StorageStack:", StringComparison.OrdinalIgnoreCase)))
            {
                var repeated = e.Count > 2;
                r.Scores.Add(new CategoryScore { Category = "Storage stack — " + e.Source + " " + e.EventId, Score = null, Status = repeated ? "ATTENTION" : "INFORMATION", Reason = e.Count + " matching occurrence(s) in 30 days; not automatically a failed drive." });
                if (repeated) AddFinding(r, "Attention", "Storage", "Repeated Windows storage-stack event", "Windows recorded the same storage-stack event signature " + e.Count + " times. Repetition can reveal controller, cable, enclosure, driver or device-path instability that SMART may not expose.", "Correlate this event with the affected device, SMART evidence and customer symptoms before replacing hardware.", e.Signature, "Moderate", "Recommended");
            }
        }

        private static void AssessPnP(InspectionReport r)
        {
            foreach (var e in r.Events.Where(x => (x.Signature ?? "").StartsWith("PnP:", StringComparison.OrdinalIgnoreCase)))
            {
                var repeated = e.Count > 2;
                r.Scores.Add(new CategoryScore { Category = "PnP — " + e.Source + " " + e.EventId, Score = null, Status = repeated ? "ATTENTION" : "INFORMATION", Reason = e.Count + " matching warning/error occurrence(s) in 30 days." });
                if (repeated) AddFinding(r, "Attention", "Device stability", "Repeated Plug and Play device warning/error", "Windows recorded the same Plug and Play warning/error signature " + e.Count + " times. Repeated enumeration or device-state failures can indicate a device, connector, controller or driver problem, but the event alone does not identify which physical part should be replaced.", "Correlate the device instance from the raw event with Device Manager, symptoms and other hardware evidence.", e.Signature, "Moderate", "Recommended");
            }
        }

        private static void AssessStorageLinks(InspectionReport r)
        {
            foreach (var d in r.Drives)
            {
                if (string.IsNullOrWhiteSpace(d.LinkCurrent) || string.IsNullOrWhiteSpace(d.LinkMaximum))
                {
                    r.Scores.Add(new CategoryScore { Category = "Storage link — " + Safe(d.Model), Score = null, Status = "NOT MEASURED", Reason = "Negotiated/reference link speed was not fully exposed; no degradation inference was made." });
                    continue;
                }
                var equal = string.Equals(d.LinkCurrent.Trim(), d.LinkMaximum.Trim(), StringComparison.OrdinalIgnoreCase);
                r.Scores.Add(new CategoryScore { Category = "Storage link — " + Safe(d.Model), Score = null, Status = equal ? "GOOD" : "OBSERVED", Reason = "Negotiated " + d.LinkCurrent + "; device maximum " + d.LinkMaximum + ". Platform/controller capability is not assumed." });
                if (!equal) AddFinding(r, "Information", "Storage", "Storage link is below the drive's reported maximum", "The drive reports a negotiated link of " + d.LinkCurrent + " while the drive itself reports a maximum of " + d.LinkMaximum + ". This is not enough to diagnose a fault because the computer/controller may intentionally support a lower speed.", "Only escalate if controller/platform capability confirms the higher speed or if repeated interface/controller errors are also present.", "smartctl interface_speed fields", "Moderate", "Monitor");
            }
        }

        private static void AssessFanCorrelation(InspectionReport r)
        {
            var fans = r.Sensors.Where(x => x.Type == "Fan").ToList();
            var temps = r.Sensors.Where(EvidenceEngine.ActualTemperature).Where(x => (x.Hardware ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || (x.Name ?? "").StartsWith("CPU", StringComparison.OrdinalIgnoreCase) || x.Name == "Core Max" || x.Name == "Core Average").ToList();
            var loads = r.Sensors.Where(x => x.Type == "Load" && ((x.Name ?? "").IndexOf("CPU Total", StringComparison.OrdinalIgnoreCase) >= 0 || (x.Hardware ?? "").IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (fans.Count == 0) { r.Scores.Add(new CategoryScore { Category = "Fan monitoring", Score = null, Status = "NOT MEASURED", Reason = "No fan RPM sensor was exposed. Fan health is not inferred." }); return; }
            var zero = fans.Any(x => (x.Maximum ?? x.Current ?? 0) <= 0); var hot = temps.Any(x => (x.Maximum ?? x.Current ?? 0) >= 80); var loaded = loads.Any(x => (x.Maximum ?? x.Current ?? 0) >= 70);
            if (zero && hot && loaded)
            {
                r.Scores.Add(new CategoryScore { Category = "Fan response", Score = null, Status = "ATTENTION", Reason = "0 RPM was reported while CPU load/temperature evidence was high." });
                AddFinding(r, "Attention", "Cooling", "Fan may not be responding under load", "A fan sensor reported 0 RPM while CPU load reached at least 70% and CPU temperature reached at least 80 °C during the sampled period. This correlation is more meaningful than a zero-RPM reading by itself.", "Confirm the fan physically and repeat the observation before replacing hardware.", "Fan RPM + CPU load + CPU temperature sensor correlation", "Moderate", "Recommended");
            }
            else r.Scores.Add(new CategoryScore { Category = "Fan monitoring", Score = null, Status = "OBSERVED", Reason = "Fan sensor(s) were read; no high-load/high-temperature + zero-RPM correlation was detected." });
        }

        private static void AssessStressPerformance(InspectionReport r)
        {
            if (r.CpuStressTest == null || r.CpuStressTest.Status != "Completed") return;
            var full = r.CpuStressTest.Samples.Where(x => x.TargetLoadPercent == 100 && x.ObservedCpuLoadPercent.HasValue && x.ObservedCpuLoadPercent.Value >= 80 && x.AverageCoreClockMHz.HasValue).ToList();
            if (full.Count == 0)
            {
                r.Scores.Add(new CategoryScore { Category = "CPU throttling evidence", Score = null, Status = "NOT MEASURED", Reason = "The stress test completed, but reliable simultaneous CPU load + core-clock samples were not exposed." });
                return;
            }
            double reference = 0;
            var referenceKnown = r.System.ContainsKey("CPU reported maximum clock") && double.TryParse((r.System["CPU reported maximum clock"] ?? "").Replace("MHz", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out reference) && reference > 0;
            if (!referenceKnown)
            {
                r.Scores.Add(new CategoryScore { Category = "CPU throttling evidence", Score = null, Status = "OBSERVED", Reason = "Loaded clock samples were captured, but no trustworthy reference clock was available; no throttling diagnosis was made." });
                return;
            }
            var collapsed = full.Count(x => x.AverageCoreClockMHz.Value < reference * 0.50);
            if (collapsed >= 5)
            {
                var peak = full.Max(x => x.TemperatureC); var thermal = peak >= 85;
                r.Scores.Add(new CategoryScore { Category = "CPU loaded clock behavior", Score = null, Status = "ATTENTION", Reason = collapsed + " high-load sample(s) fell below 50% of the Windows-reported reference clock; peak temperature " + peak.ToString("0.0") + " °C." });
                AddFinding(r, "Attention", "CPU performance", thermal ? "CPU clock collapse observed under hot sustained load" : "CPU clock collapse observed under sustained load", "During the 100% target stage, at least five samples showed observed CPU load above 80% while average core clock was below half of the Windows-reported reference clock. " + (thermal ? "The same period was also thermally hot, so thermal limiting is plausible but not proven." : "Temperature alone does not explain the limiting; power, firmware or platform limits remain possible."), "Review effective clocks with a second known-good tool and correlate with thermal/power-limit indicators before diagnosing the CPU or cooling system.", "Integrated stress-test load + clock + temperature samples", "Moderate", "Recommended");
            }
            else r.Scores.Add(new CategoryScore { Category = "CPU loaded clock behavior", Score = null, Status = "GOOD", Reason = "No repeated severe clock collapse was detected in high-load samples using the conservative 50%-of-reference rule." });
        }

        private static void AssessSecurityInfo(InspectionReport r)
        {
            if (r.System.ContainsKey("TPM")) r.Scores.Add(new CategoryScore { Category = "TPM configuration", Score = null, Status = "INFORMATION", Reason = r.System["TPM"] + ". Excluded from hardware-health scoring." });
            if (r.System.ContainsKey("BitLocker")) r.Scores.Add(new CategoryScore { Category = "BitLocker configuration", Score = null, Status = "INFORMATION", Reason = r.System["BitLocker"] + ". Excluded from hardware-health scoring." });
        }

        private static void RefreshOverall(InspectionReport r)
        {
            var critical = r.Findings.Any(x => x.Severity == "Critical"); var attention = r.Findings.Any(x => x.Severity == "Attention");
            if (critical) r.OverallStatus = "CRITICAL — immediate action recommended"; else if (attention) r.OverallStatus = "ATTENTION — service or monitoring recommended"; else if (r.Measurements.Any(x => x.Status == "Unavailable" || x.Status == "Failed")) r.OverallStatus = "NO CRITICAL INDICATOR FOUND — assessment incomplete"; else r.OverallStatus = "NO CRITICAL INDICATOR FOUND";
        }

        private static void AddFinding(InspectionReport r, string severity, string category, string title, string explanation, string recommendation, string evidence, string confidence, string action)
        {
            if (r.Findings.Any(x => x.Title == title && x.Evidence == evidence)) return;
            r.Findings.Add(new Finding { Severity = severity, Category = category, Title = title, Explanation = explanation, Recommendation = recommendation, Evidence = evidence, Confidence = confidence, ActionLevel = action });
            if (action == "Immediate" || action == "Recommended") r.PriorityActions.Add(severity + ": " + recommendation);
        }

        private static string Safe(string value) { return string.IsNullOrWhiteSpace(value) ? "Unknown drive" : value.Trim(); }
    }
}
