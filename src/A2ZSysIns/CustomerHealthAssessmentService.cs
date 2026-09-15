using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    // Pass 12: translate validated evidence into plain-language component conclusions.
    // This intentionally avoids a synthetic health percentage.
    internal static class CustomerHealthAssessmentService
    {
        public static void Apply(InspectionReport report)
        {
            if (report == null) return;
            var h = new CustomerHealthSummary();
            AddStorage(h, report);
            AddThermals(h, report);
            AddMemory(h, report);
            AddWindowsIntegrity(h, report);
            AddWindowsStability(h, report);
            AddDevices(h, report);
            AddBattery(h, report);
            AddCapacity(h, report);

            var critical = h.Components.Count(x => x.Status == "CRITICAL");
            var attention = h.Components.Count(x => x.Status == "ATTENTION" || x.Status == "DEGRADED");
            var unknown = h.Components.Count(x => x.Status == "NOT TESTED" || x.Status == "UNKNOWN");
            if (critical > 0) h.OverallStatus = "CRITICAL";
            else if (attention > 0) h.OverallStatus = "ATTENTION";
            else if (unknown > 0) h.OverallStatus = "INCOMPLETE";
            else h.OverallStatus = "GOOD";

            h.Headline = h.OverallStatus == "GOOD" ? "Your PC is in good condition" :
                h.OverallStatus == "ATTENTION" ? "Your PC is usable, but needs attention" :
                h.OverallStatus == "CRITICAL" ? "Your PC has a serious problem that needs attention" :
                "The inspection could not fully verify this PC";
            h.Explanation = BuildExplanation(h);
            foreach (var c in h.Components.Where(x => !string.IsNullOrWhiteSpace(x.Action) && x.Status != "GOOD"))
                if (!h.RecommendedActions.Contains(c.Action)) h.RecommendedActions.Add(c.Action);
            h.Limitations = report.Limitations.Distinct().Take(12).ToList();
            report.CustomerHealth = h;
            report.OverallStatus = h.OverallStatus;
            report.CustomerSummary = h.Headline + ". " + h.Explanation;
            report.PriorityActions = h.RecommendedActions.ToList();

            EvidenceEngine.Log(report, "Customer health assessment", "Overall=" + h.OverallStatus + "; components=" + h.Components.Count + "; critical=" + critical + "; attention=" + attention + "; unknown=" + unknown + ". No synthetic health percentage generated.");
        }

        private static void AddStorage(CustomerHealthSummary h, InspectionReport r)
        {
            if (r.Drives.Count == 0) { Add(h, "Storage", "UNKNOWN", "Storage could not be verified", "No physical-drive health evidence was available.", "", "Low", "Run the storage inspection again with storage access available."); return; }
            foreach (var d in r.Drives)
            {
                var result = StorageHealthAssessmentService.Assess(d);
                var name = string.IsNullOrWhiteSpace(d.Model) ? "Storage drive" : d.Model.Trim();
                var life = d.RemainingLifePercent.HasValue ? d.RemainingLifePercent.Value.ToString("0") + "% estimated life remaining" : "life remaining not reported";
                if (result.Condition == "CRITICAL") Add(h, "Storage", "CRITICAL", name + " may be failing", "The drive reported a high-risk storage indicator. " + life + ".", "Back up important files immediately and plan to replace this drive.", result.ConditionConfidence, "SMART condition=" + result.Condition + "; " + life);
                else if (result.Condition == "ATTENTION") Add(h, "Storage", "ATTENTION", name + " needs attention", "The drive reported a storage warning. " + life + ".", "Back up important data and have the drive reviewed.", result.ConditionConfidence, result.Reason);
                else if (result.Condition == "UNKNOWN") Add(h, "Storage", "UNKNOWN", name + " health could not be verified", "The Inspector did not obtain enough validated health evidence to call this drive healthy.", "Do not treat missing SMART data as proof that the drive is healthy.", result.ConditionConfidence, result.Reason);
                else if (d.RemainingLifePercent.HasValue && d.RemainingLifePercent.Value <= 20) Add(h, "Storage", "ATTENTION", name + " is nearing its endurance limit", "The drive reports approximately " + life + ". This describes endurance/wear, not a prediction of failure.", "Plan a replacement and keep current backups.", result.ConditionConfidence, result.Reason);
                else Add(h, "Storage", "GOOD", name + " is operating normally", "No validated critical storage indicator was detected. " + life + ".", "", result.ConditionConfidence, result.Reason);
            }
        }

        private static void AddThermals(CustomerHealthSummary h, InspectionReport r)
        {
            var temps = r.Sensors.Where(EvidenceEngine.ActualTemperature).Select(x => x.Maximum ?? x.Current).Where(x => x.HasValue).Select(x => (double)x.Value).ToList();
            var peak = temps.Count == 0 ? (double?)null : temps.Max();
            if (r.CpuStressTest != null && r.CpuStressTest.MaximumTemperatureC.HasValue) peak = Math.Max(peak ?? 0, r.CpuStressTest.MaximumTemperatureC.Value);
            if (!peak.HasValue) { Add(h, "Temperature", "UNKNOWN", "Temperature could not be verified", "No validated CPU temperature evidence was available.", "", "Low", "Temperature not measured."); return; }
            if (r.CpuStressTest != null && string.Equals(r.CpuStressTest.Status, "Thermal abort", StringComparison.OrdinalIgnoreCase)) Add(h, "Temperature", "CRITICAL", "CPU temperature is too high", "The CPU safety test stopped at its configured thermal limit.", "Stop heavy workloads and inspect the cooling system before continued stress testing.", "High", "Maximum observed temperature=" + peak.Value.ToString("0.0") + " C");
            else if (peak.Value >= 90) Add(h, "Temperature", "CRITICAL", "CPU temperature is too high", "The inspection observed a CPU temperature in the configured safety range.", "Inspect cooling, heatsink contact, fan operation and thermal compound.", "High", "Peak=" + peak.Value.ToString("0.0") + " C");
            else if (peak.Value >= 80) Add(h, "Temperature", "ATTENTION", "CPU temperature is high", "The inspection observed a high CPU temperature of approximately " + peak.Value.ToString("0.0") + " C.", "Check cooling and airflow if high temperatures persist.", "Moderate", "Peak=" + peak.Value.ToString("0.0") + " C");
            else Add(h, "Temperature", "GOOD", "CPU temperature is good", "The observed CPU temperature remained within a normal operating range for this inspection.", "", "Moderate", "Peak=" + peak.Value.ToString("0.0") + " C");
        }

        private static void AddMemory(CustomerHealthSummary h, InspectionReport r)
        {
            if (!r.MemoryUsedPercent.HasValue) { Add(h, "RAM", "UNKNOWN", "RAM could not be fully assessed", "Memory usage was not available.", "", "Low", "Memory usage unavailable."); return; }
            var used = r.MemoryUsedPercent.Value;
            var title = used >= 95 ? "RAM usage is very high" : used >= 85 ? "RAM usage is high" : "RAM usage is normal";
            var status = used >= 95 ? "ATTENTION" : "GOOD";
            var action = used >= 95 ? "Close unnecessary applications and investigate memory pressure if it remains high." : "";
            Add(h, "RAM", status, title, "Current memory use is approximately " + used.ToString("0") + "%. This is a usage measurement, not proof of memory-chip integrity.", action, "Moderate", "Memory used=" + used.ToString("0.0") + "%");
            if (!r.Measurements.Any(x => (x.Target ?? "").IndexOf("memory integrity", StringComparison.OrdinalIgnoreCase) >= 0)) h.Limitations.Add("Dedicated RAM integrity testing was not performed by this inspection.");
        }

        private static void AddWindowsIntegrity(CustomerHealthSummary h, InspectionReport r)
        {
            var m = r.Measurements.Where(x => (x.Target ?? "").StartsWith("Windows integrity", StringComparison.OrdinalIgnoreCase)).ToList();
            if (m.Count == 0) { Add(h, "Windows", "NOT TESTED", "Windows file integrity was not tested", "The inspection did not run a Windows system-file integrity check.", "Run the Windows integrity check when a deeper software-health inspection is required.", "Low", "No Windows integrity measurement recorded."); return; }
            if (m.Any(x => x.Status == "Critical")) Add(h, "Windows", "CRITICAL", "Windows corruption needs repair", FirstReason(m, "Critical"), "Back up important data and repair Windows system components before relying on the PC.", "High", JoinReasons(m));
            else if (m.Any(x => x.Status == "Attention")) Add(h, "Windows", "ATTENTION", "Windows has file-system or system-file issues", FirstReason(m, "Attention"), "Run the recommended Windows repair/check and inspect the retained diagnostic details.", "High", JoinReasons(m));
            else if (m.Any(x => x.Status == "Observed")) Add(h, "Windows", "GOOD", "Windows integrity checks found no reported corruption", FirstReason(m, "Observed"), "", "Moderate", JoinReasons(m));
            else Add(h, "Windows", "UNKNOWN", "Windows integrity could not be verified", JoinReasons(m), "Repeat the inspection with administrator access.", "Low", JoinReasons(m));
        }

        private static void AddWindowsStability(CustomerHealthSummary h, InspectionReport r)
        {
            if (r.Events.Count == 0) { Add(h, "Windows stability", "UNKNOWN", "Windows stability could not be fully assessed", "No monitored event history was returned.", "", "Low", "No event findings."); return; }
            if (r.Events.Any(x => x.Source == "Disk" && x.EventId == 7) || r.Events.Any(x => x.Source == "Microsoft-Windows-WHEA-Logger" && x.EventId == 18 && x.Count >= 2)) Add(h, "Windows stability", "CRITICAL", "Windows recorded a serious hardware-related event pattern", "Repeated high-risk hardware or storage events were found in the retained event history.", "Back up data and investigate the related hardware before relying on the PC.", "High", "Repeated high-risk event pattern detected.");
            else if (r.Events.Any(x => x.EventId == 1001 || x.EventId == 55 || x.Source == "Microsoft-Windows-WHEA-Logger")) Add(h, "Windows stability", "ATTENTION", "Windows has recorded stability warnings", "One or more monitored Windows stability events require correlation.", "Review the event details and monitor whether the problem repeats.", "Moderate", string.Join("; ", r.Events.Take(6).Select(x => x.Source + "/" + x.EventId + " x" + x.Count)));
            else Add(h, "Windows stability", "GOOD", "No high-risk Windows event pattern was detected", "The retained event history did not contain the monitored high-risk patterns.", "", "Moderate", "Monitored event patterns reviewed.");
        }

        private static void AddDevices(CustomerHealthSummary h, InspectionReport r)
        {
            string value; if (!r.System.TryGetValue("Problem devices", out value) || string.IsNullOrWhiteSpace(value) || value == "Not measured") { Add(h, "Devices", "UNKNOWN", "Windows device status could not be verified", "Problem-device information was unavailable.", "", "Low", "Problem devices unavailable."); return; }
            if (value.StartsWith("None reported", StringComparison.OrdinalIgnoreCase)) Add(h, "Devices", "GOOD", "Windows devices are reporting normally", "Windows reported no Config Manager problem for the returned devices.", "", "Moderate", value);
            else Add(h, "Devices", "ATTENTION", "Windows has a device problem", "Windows reported one or more devices in a problem state.", "Open Device Manager and investigate the affected device or driver.", "Moderate", value);
        }

        private static void AddBattery(CustomerHealthSummary h, InspectionReport r)
        {
            if (!r.BatteryWearPercent.HasValue) return;
            var wear = r.BatteryWearPercent.Value;
            if (wear >= 60) Add(h, "Battery", "ATTENTION", "Battery capacity is heavily worn", "The battery has lost approximately " + wear.ToString("0") + "% of its design capacity.", "Plan battery replacement if runtime is no longer adequate.", "High", "Wear=" + wear.ToString("0.0") + "%");
            else Add(h, "Battery", "GOOD", "Battery condition is acceptable", "The battery reports approximately " + (100 - wear).ToString("0") + "% of its design capacity remaining.", "", "High", "Wear=" + wear.ToString("0.0") + "%");
        }

        private static void AddCapacity(CustomerHealthSummary h, InspectionReport r)
        {
            foreach (var v in r.Volumes.Where(x => x.TotalBytes > 0))
            {
                var free = v.FreeBytes * 100.0 / v.TotalBytes;
                if (free < 5) Add(h, "Storage space", "ATTENTION", v.Name + " is almost full", "Only approximately " + free.ToString("0.0") + "% of this volume is free.", "Free space before Windows updates or large application installs; keep a healthy amount of free space.", "High", "Free=" + free.ToString("0.0") + "%");
                else if (free < 10) Add(h, "Storage space", "ATTENTION", v.Name + " has low free space", "Approximately " + free.ToString("0.0") + "% of this volume is free.", "Free some storage space to reduce capacity pressure.", "High", "Free=" + free.ToString("0.0") + "%");
                else Add(h, "Storage space", "GOOD", v.Name + " has adequate free space", "Approximately " + free.ToString("0.0") + "% of this volume is free.", "", "High", "Free=" + free.ToString("0.0") + "%");
            }
        }

        private static void Add(CustomerHealthSummary h, string component, string status, string title, string explanation, string action, string confidence, string evidence)
        {
            h.Components.Add(new ComponentHealth { Component = component, Status = status, Title = title, Explanation = explanation, Action = action, Confidence = confidence, Evidence = evidence });
        }
        private static string BuildExplanation(CustomerHealthSummary h)
        {
            var critical = h.Components.Where(x => x.Status == "CRITICAL").Select(x => x.Title).Take(2).ToList();
            if (critical.Count > 0) return "The main concern is " + string.Join("; ", critical) + ".";
            var attention = h.Components.Where(x => x.Status == "ATTENTION").Select(x => x.Title).Take(2).ToList();
            if (attention.Count > 0) return "The PC is usable, but " + string.Join("; ", attention) + ".";
            var unknown = h.Components.Count(x => x.Status == "UNKNOWN" || x.Status == "NOT TESTED");
            return unknown > 0 ? "No critical problem was identified, but some areas could not be fully verified." : "No significant problem was identified in the available evidence.";
        }
        private static string FirstReason(List<Measurement> m, string status) => m.FirstOrDefault(x => x.Status == status)?.Reason ?? "Windows integrity result was recorded.";
        private static string JoinReasons(List<Measurement> m) => string.Join("; ", m.Select(x => x.Reason).Where(x => !string.IsNullOrWhiteSpace(x)).Take(6));
    }
}
