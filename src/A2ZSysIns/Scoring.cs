using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    public static class Scoring
    {
        public static void Calculate(InspectionReport r)
        {
            r.Findings.Clear(); r.Scores.Clear();
            ScoreStorage(r); ScoreThermals(r); ScoreEvents(r); ScoreResources(r); ScoreBattery(r); ScoreSystem(r);
            var available = r.Scores.Where(x => x.Score.HasValue).Select(x => x.Score.Value).ToList();
            r.OverallScore = available.Count == 0 ? (int?)null : (int)Math.Round(available.Average());
            if (r.Drives.Any(x => string.Equals(x.SmartStatus, "FAILED", StringComparison.OrdinalIgnoreCase))) r.OverallScore = Math.Min(r.OverallScore ?? 20, 20);
            r.OverallStatus = Status(r.OverallScore);
        }

        private static void ScoreStorage(InspectionReport r)
        {
            if (r.Drives.Count == 0) { Add(r, "Storage", null, "No drive data"); return; }
            var failed = r.Drives.Any(x => string.Equals(x.SmartStatus, "FAILED", StringComparison.OrdinalIgnoreCase));
            var unknown = r.Drives.All(x => string.IsNullOrWhiteSpace(x.SmartStatus) || x.SmartStatus == "N/A");
            var score = failed ? 10 : unknown ? 70 : 95; Add(r, "Storage", score, failed ? "SMART failure reported" : unknown ? "SMART status unavailable" : "No failure status reported");
            if (failed) r.Findings.Add(F("Critical", "Storage", "Storage failure status", "A drive reported a failed health state.", "Back up important data and arrange a detailed storage diagnosis immediately.", "SMART status"));
            if (r.Drives.All(x => string.IsNullOrWhiteSpace(x.RawEvidence))) r.Limitations.Add("smartctl was unavailable; storage scoring uses Windows device status only.");
        }
        private static void ScoreThermals(InspectionReport r)
        {
            var temps = r.Sensors.Where(x => x.Type == "Temperature" && x.Maximum.HasValue).ToList();
            if (temps.Count == 0) { Add(r, "Thermals", null, "Temperature sensors unavailable"); return; }
            var max = temps.Max(x => x.Maximum.Value); var score = max >= 95 ? 30 : max >= 85 ? 60 : max >= 75 ? 80 : 95; Add(r, "Thermals", score, "Highest observed temperature " + max.ToString("0") + " °C");
            if (max >= 85) r.Findings.Add(F(max >= 95 ? "Critical" : "Warning", "Thermals", "High observed temperature", "A sensor reached " + max.ToString("0") + " °C during the short observation.", "Inspect cooling and confirm under a controlled load test.", "LibreHardwareMonitor sample"));
        }
        private static void ScoreEvents(InspectionReport r)
        {
            var critical = r.Events.Where(x => x.EventId != 41).Sum(x => x.Count); var shutdowns = r.Events.Where(x => x.EventId == 41).Sum(x => x.Count);
            var score = Math.Max(20, 100 - critical * 15 - Math.Min(shutdowns, 10) * 3); Add(r, "Windows stability", score, critical + " critical error events; " + shutdowns + " unexpected shutdown events in 30 days");
            foreach (var e in r.Events)
            {
                var repeated = e.Count > 2;
                var title = repeated ? "Repeated pattern: " + e.Summary : e.Summary;
                var explanation = e.Count + " event(s) found in the last 30 days." + (repeated ? " This exceeds the repeated-event threshold of two occurrences and indicates a recurring pattern." : "");
                var recommendation = e.Source == "Application Error"
                    ? "Review, repair, update, or remove the named application only after customer approval."
                    : "Review the event evidence and investigate whether it matches the reported symptom.";
                r.Findings.Add(F(repeated && e.Level == "Error" ? "Warning" : e.Level == "Error" ? "Attention" : "Attention", "Windows stability", title, explanation, recommendation, e.Source + " Event ID " + e.EventId + "; signature " + e.Signature));
            }
        }
        private static void ScoreResources(InspectionReport r)
        {
            var total = 0L; var free = 0L; foreach (var d in System.IO.DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == System.IO.DriveType.Fixed)) { total += d.TotalSize; free += d.AvailableFreeSpace; }
            if (total == 0) { Add(r, "Resources", null, "Volume capacity unavailable"); return; }
            var pct = free * 100.0 / total; var score = pct < 5 ? 35 : pct < 10 ? 60 : pct < 20 ? 80 : 95; Add(r, "Resources", score, pct.ToString("0") + "% total free storage");
            if (pct < 10) r.Findings.Add(F("Warning", "Resources", "Low free storage", "Less than 10% total free storage was observed.", "Review storage usage with the customer before performing any cleanup.", "Windows volume information"));
        }
        private static void ScoreBattery(InspectionReport r) { Add(r, "Battery", null, r.System.ContainsKey("Battery") ? r.System["Battery"] : "N/A"); }
        private static void ScoreSystem(InspectionReport r) { var problems = r.System.ContainsKey("Problem devices") ? r.System["Problem devices"] : "N/A"; Add(r, "System condition", problems == "None reported" ? 95 : problems == "N/A" ? (int?)null : 65, problems); if (problems != "None reported" && problems != "N/A") r.Findings.Add(F("Attention", "System condition", "Device Manager problems", problems, "Inspect the listed devices and drivers.", "Win32_PnPEntity")); }
        private static void Add(InspectionReport r, string category, int? score, string reason) { r.Scores.Add(new CategoryScore { Category = category, Score = score, Status = Status(score), Reason = reason }); }
        private static string Status(int? score) => !score.HasValue ? "N/A" : score >= 85 ? "Healthy" : score >= 70 ? "Attention" : score >= 40 ? "Service recommended" : "Critical";
        private static Finding F(string severity, string category, string title, string explanation, string recommendation, string evidence) => new Finding { Severity = severity, Category = category, Title = title, Explanation = explanation, Recommendation = recommendation, Evidence = evidence };
    }
}
