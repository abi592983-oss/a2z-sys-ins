using System;
using System.Linq;
namespace A2ZSysIns
{
    public static class Scoring
    {
        public static void Calculate(InspectionReport r)
        {
            r.Findings.Clear(); r.Scores.Clear();
            foreach (var d in r.Drives)
            {
                d.Assessment = EvidenceEngine.DriveAssessment(d);
                Add(r, "Drive: " + d.Model, d.Assessment, "Life indicator: " +
                    (d.RemainingLifePercent.HasValue ? d.RemainingLifePercent.Value.ToString("0") + "%; " + d.LifeMeaning : "Cannot measure. ") +
                    " SMART threshold status: " + d.SmartStatus);
                if (d.Assessment.StartsWith("Critical"))
                    Finding(r, "Critical", d.Model, "Critical storage indicators", "SMART failure, pending/uncorrectable sectors or NVMe warning detected.",
                        "Protect important data; avoid stress tests and investigate drive replacement/recovery.", "Storage attributes in JSON");
                else if (d.Assessment.StartsWith("Attention"))
                    Finding(r, "Attention", d.Model, "Storage errors reported", "Available SMART data contains errors; interface errors alone do not prove media failure.",
                        "Review raw attributes and backups before corrective work.", "Storage attributes in JSON");
            }
            if (r.Drives.Count == 0) Add(r, "Storage", "Not assessed", "Cannot measure: physical drive evidence unavailable.");
            var temps = r.Sensors.Where(EvidenceEngine.ActualTemperature).ToList();
            Add(r, "Thermals", temps.Count > 0 ? "Observed only" : "Not assessed",
                temps.Count > 0 ? "Highest actual reading: " + temps.Max(x => x.Maximum.Value).ToString("0.0") +
                    " °C. Short observation, not a stress test." : "Cannot measure actual component temperatures.");
            foreach (var e in r.Events)
                Finding(r, e.Level == "Information" ? "Information" : "Attention", "Event history",
                    (e.Count > 2 ? "Repeated pattern: " : "") + e.Summary,
                    e.Count + " matching records within 30 days. " + e.Cause,
                    e.EventId == 41 ? "Confirm power outages or forced shutdowns with the technician; do not assume a PC defect." :
                    "Review matching event fields. A recurring application is not proof of one root cause.", e.Signature);
            Add(r, "Event history", r.Events.Count > 0 ? "Review findings" : "See collection coverage",
                "Shutdowns and application crashes are separate. No hardware penalty for unexplained power loss.");
            foreach (var v in r.Volumes.Where(x => x.TotalBytes > 0))
            {
                var pct = v.FreeBytes * 100.0 / v.TotalBytes;
                Add(r, "Free space " + v.Name, pct < 10 ? "Attention" : "Observed", pct.ToString("0.0") + "% free (this volume only).");
                if (pct < 10) Finding(r, "Attention", "Resources", "Low free space: " + v.Name, pct.ToString("0.0") + "% free.",
                    "Review storage usage with customer approval; no automatic deletion.", "Saved volume snapshot");
            }
            Add(r, "RAM usage", r.MemoryUsedPercent >= 85 ? "Attention" : r.MemoryUsedPercent.HasValue ? "Observed" : "Not assessed",
                r.MemoryUsedPercent.HasValue ? r.MemoryUsedPercent.Value.ToString("0.0") + "% used; workload snapshot, not faulty RAM." : "Cannot measure RAM usage.");
            if (r.MemoryUsedPercent >= 85) Finding(r, "Attention", "Resources", "High memory usage",
                r.MemoryUsedPercent.Value.ToString("0.0") + "% used.", "Review active workload; this is not a RAM integrity test.", "Memory snapshot");
            Add(r, "Battery wear", r.BatteryWearPercent.HasValue ? (r.BatteryWearPercent >= 40 ? "Attention" : "Observed") : "Not assessed",
                r.BatteryWearPercent.HasValue ? r.BatteryWearPercent.Value.ToString("0.0") + "% wear; full-charge capacity " +
                    r.BatteryFullChargeCapacity.Value.ToString("0") + " of design " + r.BatteryDesignedCapacity.Value.ToString("0") + "." :
                    "Cannot measure battery wear; current charge is not a wear indicator.");
            if (r.BatteryWearPercent >= 40) Finding(r, "Attention", "Battery", "Battery capacity wear",
                r.BatteryWearPercent.Value.ToString("0.0") + "% capacity loss from design value.",
                "Confirm runtime under normal use and consider replacement if service time is inadequate.", "Battery capacity measurement");
            Add(r, "System condition", "Inventory only", "Device inventory is not proof of a healthy operating system.");
            r.OverallScore = null;
            r.OverallStatus = r.Findings.Any(x => x.Severity == "Critical") ? "Critical indicators — incomplete assessment" : "Evidence review — no overall health percentage";
            EvidenceEngine.Log(r, "Report assessment", "Numeric health ratings withheld. " + r.OverallStatus);
            foreach (var f in r.Findings) EvidenceEngine.Log(r, "Finding", f.Title + ": " + f.Explanation + " Evidence: " + f.Evidence);
        }
        private static void Add(InspectionReport r, string name, string status, string reason) =>
            r.Scores.Add(new CategoryScore { Category = name, Score = null, Status = status, Reason = reason });
        private static void Finding(InspectionReport r, string severity, string category, string title, string explanation, string recommendation, string evidence) =>
            r.Findings.Add(new Finding { Severity = severity, Category = category, Title = title, Explanation = explanation, Recommendation = recommendation, Evidence = evidence });
    }
}
