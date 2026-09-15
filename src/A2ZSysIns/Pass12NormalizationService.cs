using System;
using System.Linq;

namespace A2ZSysIns
{
    internal static class Pass12NormalizationService
    {
        public static void Apply(InspectionReport report)
        {
            if (report == null) return;
            foreach (var drive in report.Drives)
            {
                if (drive == null) continue;
                // Several SATA SSDs expose vendor life as ATA attribute 231. For the
                // validated SSD_Life_Left / Percent_Lifetime_Remain semantics, the
                // raw value is the remaining-life percentage. Do not substitute a
                // missing value with zero.
                var life = drive.SmartAttributes.FirstOrDefault(x => x != null && x.SemanticsValidated && x.RawValue.HasValue &&
                    (string.Equals(x.Name, "SSD_Life_Left", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Name, "Percent_Lifetime_Remain", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Name, "Remaining_Lifetime_Perc", StringComparison.OrdinalIgnoreCase)) &&
                    x.RawValue.Value >= 0 && x.RawValue.Value <= 100);
                if (life != null)
                {
                    drive.RemainingLifePercent = life.RawValue.Value;
                    drive.LifeMeaning = "Validated device endurance/life indicator: " + life.Name + " raw value interpreted as percent remaining; not an overall health percentage.";
                }
                if (drive.RemainingLifePercent.HasValue)
                {
                    var remaining = Math.Max(0, Math.Min(100, drive.RemainingLifePercent.Value));
                    drive.RemainingLifePercent = remaining;
                    drive.EnduranceUsedPercent = 100 - remaining;
                    drive.EnduranceMeaning = "Endurance used is derived as 100 minus validated life remaining; not a failure probability.";
                }
                // Smart threshold pass is deliberately kept as a condition signal,
                // never converted into a numeric health percentage.
                if (drive.SmartPassed.HasValue)
                    drive.SmartStatus = drive.SmartPassed.Value ? "Threshold check passed (not a health percentage)" : "FAILED";
            }
            EvidenceEngine.Log(report, "Pass 12 normalization", "Separated storage condition from endurance; preserved missing health as unavailable and normalized validated life-remaining semantics without manufacturing a health percentage.");
        }
    }
}
