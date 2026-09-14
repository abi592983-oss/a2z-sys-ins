using System;
using System.Globalization;
using System.Linq;

namespace A2ZSysIns
{
    internal static class StorageHealthAssessmentService
    {
        internal sealed class Result
        {
            public string Condition; public string ConditionConfidence; public string Endurance; public string EnduranceConfidence; public string Reason;
        }

        public static void Record(InspectionReport report)
        {
            if (report == null) return;
            report.Measurements.RemoveAll(x => x != null && (x.Target ?? "").StartsWith("Storage health model — ", StringComparison.OrdinalIgnoreCase));
            foreach (var drive in report.Drives)
            {
                var result = Assess(drive);
                report.Measurements.Add(new Measurement
                {
                    Target = "Storage health model — " + Safe(drive == null ? null : drive.Model),
                    Source = "StorageHealthAssessmentService",
                    Status = result.Condition,
                    Reason = result.Reason,
                    Response = "ConditionConfidence=" + result.ConditionConfidence + "; Endurance=" + result.Endurance + "; EnduranceConfidence=" + result.EnduranceConfidence
                });
            }
        }

        public static Result Assess(DriveInfoRecord drive)
        {
            if (drive == null) return new Result { Condition = "UNKNOWN", ConditionConfidence = "Low", Endurance = "UNAVAILABLE", EnduranceConfidence = "Low", Reason = "No drive record." };
            var hasHealthEvidence = drive.SmartPassed.HasValue || drive.SmartAttributes.Count > 0 || drive.NvmeHealth != null || drive.StorageEvidence.HasScsiHealth;
            var critical = drive.SmartPassed == false || (drive.NvmeHealth != null && (drive.NvmeHealth.CriticalWarning ?? 0) != 0) || ValidRaw(drive, 197) > 0 || ValidRaw(drive, 198) > 0;
            var errors = ValidRaw(drive, 5) > 0 || ValidRaw(drive, 199) > 0 || (drive.NvmeHealth != null && (drive.NvmeHealth.MediaErrors ?? 0) > 0);
            string condition, confidence;
            if (!hasHealthEvidence) { condition = "UNKNOWN"; confidence = "Low"; }
            else if (critical) { condition = "CRITICAL"; confidence = drive.SmartPassed == false || drive.NvmeHealth != null ? "High" : "Moderate"; }
            else if (errors) { condition = "ATTENTION"; confidence = drive.NvmeHealth != null || drive.SmartPassed.HasValue ? "High" : "Moderate"; }
            else { condition = "GOOD / NO FLAGGED INDICATOR"; confidence = drive.StorageEvidence.Quality == "Validated" ? "High" : drive.SmartPassed.HasValue || drive.SmartAttributes.Any(x => x.SemanticsValidated) ? "Moderate" : "Low"; }
            string endurance, enduranceConfidence;
            if (!drive.RemainingLifePercent.HasValue) { endurance = "UNAVAILABLE"; enduranceConfidence = "Low"; }
            else { var value = Math.Max(0, Math.Min(100, drive.RemainingLifePercent.Value)); endurance = value.ToString("0.0", CultureInfo.InvariantCulture) + "% remaining"; enduranceConfidence = drive.SmartAttributes.Any(x => x.SemanticsValidated && !string.IsNullOrWhiteSpace(x.Interpretation)) || drive.NvmeHealth != null ? "High" : "Moderate"; }
            var reason = condition + "; condition confidence=" + confidence + "; endurance=" + endurance + "; endurance confidence=" + enduranceConfidence + ". Condition and endurance are separate dimensions; endurance is not a failure probability and neither dimension is an overall system-health percentage.";
            return new Result { Condition = condition, ConditionConfidence = confidence, Endurance = endurance, EnduranceConfidence = enduranceConfidence, Reason = reason };
        }

        private static long ValidRaw(DriveInfoRecord d, int id) => d.SmartAttributes.Where(x => x.SemanticsValidated && x.Id == id && x.RawValue.HasValue).Select(x => x.RawValue.Value).FirstOrDefault();
        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown drive" : value.Trim();
    }
}
