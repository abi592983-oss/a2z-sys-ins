using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace A2ZSysIns
{
    // SMART attribute IDs are not universal semantics. This layer only scores ATA
    // attributes when smartctl supplies an explicit attribute name that validates
    // the interpretation. Unknown/vendor-specific attributes remain evidence only.
    internal static class SmartInterpretation
    {
        public static bool IsValidatedAttribute(DriveInfoRecord drive, int id, params string[] acceptedNames)
        {
            if (drive == null || string.IsNullOrWhiteSpace(drive.RawEvidence)) return false;
            try
            {
                var root = JObject.Parse(drive.RawEvidence);
                foreach (var attr in root.SelectTokens("ata_smart_attributes.table[*]"))
                {
                    if ((int?)attr["id"] != id) continue;
                    var name = ((string)attr["name"] ?? "").Trim();
                    if (acceptedNames.Any(x => string.Equals(name, x, StringComparison.OrdinalIgnoreCase))) return true;
                }
            }
            catch { }
            return false;
        }

        public static long Raw(DriveInfoRecord drive, int id)
        {
            if (drive != null && drive.Attributes.TryGetValue("ATA_" + id, out var value)) return value;
            return 0;
        }

        public static bool PendingSectors(DriveInfoRecord drive)
        {
            return IsValidatedAttribute(drive, 197, "Current_Pending_Sector", "Current Pending Sector Count", "Current_Pending_Sector_Count");
        }

        public static bool UncorrectableSectors(DriveInfoRecord drive)
        {
            return IsValidatedAttribute(drive, 198, "Offline_Uncorrectable", "Uncorrectable Sector Count", "Offline Uncorrectable");
        }

        public static bool ReallocatedSectors(DriveInfoRecord drive)
        {
            return IsValidatedAttribute(drive, 5, "Reallocated_Sector_Ct", "Reallocated Sectors Count", "Reallocated_Sector_Count");
        }

        public static bool InterfaceCrcErrors(DriveInfoRecord drive)
        {
            return IsValidatedAttribute(drive, 199, "UDMA_CRC_Error_Count", "UltraDMA CRC Error Count", "UDMA_CRC_Error");
        }

        public static bool HasCritical(DriveInfoRecord drive)
        {
            return drive != null && (drive.SmartPassed == false
                || Value(drive, "NVMe_critical_warning") != 0
                || (PendingSectors(drive) && Raw(drive, 197) > 0)
                || (UncorrectableSectors(drive) && Raw(drive, 198) > 0));
        }

        public static bool HasMediaErrors(DriveInfoRecord drive)
        {
            return drive != null && ((ReallocatedSectors(drive) && Raw(drive, 5) > 0)
                || Value(drive, "NVMe_media_errors") > 0);
        }

        public static bool HasInterfaceErrors(DriveInfoRecord drive)
        {
            return drive != null && InterfaceCrcErrors(drive) && Raw(drive, 199) > 0;
        }

        public static string Assessment(DriveInfoRecord drive)
        {
            if (HasCritical(drive)) return "Critical indicators";
            if (HasMediaErrors(drive) || HasInterfaceErrors(drive)) return "Attention: errors reported";
            return drive != null && (drive.SmartPassed.HasValue || drive.Attributes.Count > 0)
                ? "No flagged indicators in available data" : "Not assessed";
        }

        public static string CriticalExplanation(DriveInfoRecord drive)
        {
            var evidence = new List<string>();
            if (drive.SmartPassed == false) evidence.Add("the device SMART threshold check reports failure");
            if (PendingSectors(drive) && Raw(drive, 197) > 0) evidence.Add("validated pending sectors=" + Raw(drive, 197));
            if (UncorrectableSectors(drive) && Raw(drive, 198) > 0) evidence.Add("validated uncorrectable sectors=" + Raw(drive, 198));
            if (Value(drive, "NVMe_critical_warning") != 0) evidence.Add("NVMe critical_warning=" + Value(drive, "NVMe_critical_warning"));
            return evidence.Count == 0
                ? "A validated SMART/device failure indicator was reported, but the exact attribute semantics were not available in the saved evidence."
                : "High-risk storage evidence was detected: " + string.Join(", ", evidence) + ".";
        }

        public static string EvidenceSummary(DriveInfoRecord drive)
        {
            var parts = new List<string>();
            parts.Add("SMART threshold=" + drive.SmartStatus);
            parts.Add("reallocated=" + (ReallocatedSectors(drive) ? Raw(drive, 5).ToString() : "not validated"));
            parts.Add("pending=" + (PendingSectors(drive) ? Raw(drive, 197).ToString() : "not validated / vendor-specific or unknown"));
            parts.Add("uncorrectable=" + (UncorrectableSectors(drive) ? Raw(drive, 198).ToString() : "not validated / vendor-specific or unknown"));
            parts.Add("interfaceCRC=" + (InterfaceCrcErrors(drive) ? Raw(drive, 199).ToString() : "not validated"));
            parts.Add("NVMe critical warning=" + Value(drive, "NVMe_critical_warning"));
            parts.Add("NVMe media errors=" + Value(drive, "NVMe_media_errors"));
            return string.Join("; ", parts);
        }

        public static void NormalizeReport(InspectionReport report)
        {
            if (report == null) return;
            foreach (var drive in report.Drives)
            {
                var old = drive.Assessment;
                drive.Assessment = Assessment(drive);
                if (!string.Equals(old, drive.Assessment, StringComparison.OrdinalIgnoreCase))
                    EvidenceEngine.Log(report, "SMART interpretation corrected", drive.Model + ": " + old + " -> " + drive.Assessment + ". Attribute IDs are scored only when their smartctl names validate the meaning.");

                var falseCritical = report.Findings.Where(x => x.Category == "Storage"
                    && (x.Title ?? "").Equals("Drive may be at risk of failure — " + Safe(drive.Model), StringComparison.OrdinalIgnoreCase)
                    && !HasCritical(drive)).ToList();
                foreach (var finding in falseCritical) report.Findings.Remove(finding);
            }
            RefreshSummary(report);
        }

        public static void RefreshSummary(InspectionReport report)
        {
            if (report == null) return;
            var critical = report.Findings.Count(x => x.Severity == "Critical");
            var attention = report.Findings.Count(x => x.Severity == "Attention");
            var unavailable = report.Measurements.Count(x => x.Status == "Unavailable" || x.Status == "Failed");
            report.OverallScore = null;
            if (critical > 0)
            {
                report.OverallStatus = "CRITICAL — immediate action recommended";
                var names = report.Findings.Where(x => x.Severity == "Critical").Select(x => x.Title).Distinct().Take(4).ToList();
                report.CustomerSummary = "Critical finding(s): " + string.Join("; ", names) + ". Protect important data and address these findings before normal heavy use.";
            }
            else if (attention > 0)
            {
                report.OverallStatus = "ATTENTION — service or monitoring recommended";
                report.CustomerSummary = "No critical failure indicator was confirmed, but " + attention + " condition(s) need attention. Review the recommended actions and monitor the computer for worsening symptoms.";
            }
            else if (unavailable > 0)
            {
                report.OverallStatus = "NO CRITICAL INDICATOR FOUND — assessment incomplete";
                report.CustomerSummary = "The checks that returned evidence did not show a critical failure indicator, but some measurements were unavailable. Unmeasured components must not be assumed healthy.";
            }
            else
            {
                report.OverallStatus = "NO CRITICAL INDICATOR FOUND";
                report.CustomerSummary = "No high-risk failure indicator was detected by the checks performed. This is a screening result, not a guarantee of future reliability.";
            }
        }

        private static long Value(DriveInfoRecord drive, string key)
        {
            return drive != null && drive.Attributes.TryGetValue(key, out var value) ? value : 0;
        }

        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown drive" : value.Trim();
    }
}
