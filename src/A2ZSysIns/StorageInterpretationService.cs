using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace A2ZSysIns
{
    // Pass 3: interpret acquired storage evidence. This layer does not invent
    // health percentages and does not treat SMART IDs as universal semantics.
    internal static class StorageInterpretationService
    {
        public static void Interpret(InspectionReport report)
        {
            if (report == null) return;

            foreach (var drive in report.Drives)
                InterpretDrive(report, drive);
        }

        private static void InterpretDrive(InspectionReport report, DriveInfoRecord drive)
        {
            if (drive == null) return;

            drive.RemainingLifePercent = null;
            drive.LifeMeaning = "Not measured; no validated life attribute";
            drive.EnduranceUsedPercent = null;
            drive.EnduranceMeaning = "Not measured";

            foreach (var attr in drive.SmartAttributes)
            {
                attr.SemanticsValidated = false;
                attr.Interpretation = null;
            }

            // Legacy Attributes is retained as a compatibility projection. Raw
            // evidence remains available in SmartAttributes/RawEvidence.
            RemoveLegacySemanticKeys(drive);

            foreach (var attr in drive.SmartAttributes)
            {
                if (!attr.Id.HasValue) continue;
                var name = Normalize(attr.Name);
                var id = attr.Id.Value;

                if (id == 5 && IsAny(name, "reallocated_sector_ct", "reallocated_sectors_count", "reallocated_sector_count"))
                {
                    Validate(attr, "Reallocated sector count; non-zero values indicate sectors the device has remapped.");
                    Project(drive, attr);
                }
                else if (id == 197 && IsAny(name, "current_pending_sector", "current_pending_sector_count"))
                {
                    Validate(attr, "Current pending sector count; non-zero values indicate sectors awaiting successful rewrite/remap handling.");
                    Project(drive, attr);
                }
                else if (id == 198 && IsAny(name, "offline_uncorrectable", "uncorrectable_sector_count", "offline_uncorrectable"))
                {
                    Validate(attr, "Offline uncorrectable sector count; non-zero values are a high-risk media indicator.");
                    Project(drive, attr);
                }
                else if (id == 199 && IsAny(name, "udma_crc_error_count", "ultradma_crc_error_count", "udma_crc_error"))
                {
                    Validate(attr, "Interface CRC error count; indicates communication errors on the storage path, not necessarily damaged media.");
                    Project(drive, attr);
                }
                else if (id == 194 && IsAny(name, "temperature_celsius", "airflow_temperature_cel", "temperature"))
                {
                    Validate(attr, "Drive temperature reported by the SMART attribute.");
                    Project(drive, attr);
                    if (!drive.TemperatureC.HasValue && attr.RawValue.HasValue && attr.RawValue.Value >= -40 && attr.RawValue.Value <= 150)
                        drive.TemperatureC = attr.RawValue.Value;
                }
                else if (IsRemainingLifeAttribute(name))
                {
                    InterpretLifeAttribute(drive, attr);
                }
                else if (IsUsedEnduranceAttribute(name))
                {
                    InterpretUsedEnduranceAttribute(drive, attr);
                }
            }

            InterpretNvme(drive);
            InterpretScsi(drive);
            SetAssessment(drive);

            drive.StorageEvidence.HasTemperature = drive.TemperatureC.HasValue;
            drive.StorageEvidence.HasEndurance = drive.RemainingLifePercent.HasValue || drive.EnduranceUsedPercent.HasValue;
            if (!drive.TemperatureC.HasValue && !drive.StorageEvidence.UnavailableFields.Contains("Temperature"))
                drive.StorageEvidence.UnavailableFields.Add("Temperature");

            EvidenceEngine.Log(report, "Storage interpretation",
                Safe(drive.Model) + ": " + drive.Assessment + "; life=" +
                (drive.RemainingLifePercent.HasValue ? drive.RemainingLifePercent.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A") +
                "; enduranceUsed=" +
                (drive.EnduranceUsedPercent.HasValue ? drive.EnduranceUsedPercent.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A") +
                "; semantics=" + drive.SmartAttributes.Count(x => x.SemanticsValidated));
        }

        private static void InterpretNvme(DriveInfoRecord drive)
        {
            var nvme = drive.NvmeHealth;
            if (nvme == null) return;

            drive.Attributes["NVMe_critical_warning"] = nvme.CriticalWarning ?? 0;
            drive.Attributes["NVMe_media_errors"] = nvme.MediaErrors ?? 0;

            if (nvme.PercentageUsed.HasValue && nvme.PercentageUsed.Value >= 0 && nvme.PercentageUsed.Value <= 100)
            {
                drive.EnduranceUsedPercent = nvme.PercentageUsed.Value;
                drive.EnduranceMeaning = "NVMe standard percentage_used: estimated percentage of specified endurance consumed; this is not an overall health percentage.";
                drive.RemainingLifePercent = Math.Max(0, 100.0 - nvme.PercentageUsed.Value);
                drive.LifeMeaning = "NVMe endurance estimate derived from the standard percentage_used field; this is not a failure probability.";
            }

            if (nvme.AvailableSparePercent.HasValue && nvme.AvailableSpareThresholdPercent.HasValue &&
                nvme.AvailableSparePercent.Value < nvme.AvailableSpareThresholdPercent.Value)
                nvme.Interpretation = "Available spare is below the device-reported threshold; review the NVMe health log.";
            else
                nvme.Interpretation = "NVMe health log interpreted using standard health-log fields; endurance and condition are reported separately.";
        }

        private static void InterpretScsi(DriveInfoRecord drive)
        {
            if (!drive.StorageEvidence.HasScsiHealth) return;
            // A grown-defect/error log is evidence worth retaining, but without a
            // vendor/controller-specific rule it is not promoted to failure.
            if (drive.NvmeHealth == null && !drive.SmartPassed.HasValue)
                drive.Assessment = "Measured; SCSI health evidence available";
        }

        private static void SetAssessment(DriveInfoRecord drive)
        {
            if (drive.SmartPassed == false || NvmeCritical(drive) || ValidRaw(drive, 198) > 0 || ValidRaw(drive, 197) > 0)
            {
                drive.Assessment = "Critical indicators";
                return;
            }
            if (ValidRaw(drive, 5) > 0 || (drive.NvmeHealth != null && (drive.NvmeHealth.MediaErrors ?? 0) > 0) || ValidRaw(drive, 199) > 0)
            {
                drive.Assessment = "Attention: errors reported";
                return;
            }
            if (drive.SmartPassed.HasValue || drive.SmartAttributes.Count > 0 || drive.NvmeHealth != null || drive.StorageEvidence.HasScsiHealth)
            {
                drive.Assessment = "No flagged indicators in available data";
                return;
            }
            drive.Assessment = "Not assessed";
        }

        private static bool NvmeCritical(DriveInfoRecord d) => d.NvmeHealth != null && (d.NvmeHealth.CriticalWarning ?? 0) != 0;

        private static long ValidRaw(DriveInfoRecord d, int id)
        {
            return d.SmartAttributes.Where(x => x.SemanticsValidated && x.Id == id && x.RawValue.HasValue).Select(x => x.RawValue.Value).FirstOrDefault();
        }

        private static void InterpretLifeAttribute(DriveInfoRecord drive, SmartAttributeRecord attr)
        {
            if (!attr.RawValue.HasValue) return;
            var value = attr.RawValue.Value;
            var name = Normalize(attr.Name);
            double remaining;

            if (name.Contains("lifetime_remaining") || name.Contains("life_left") || name.Contains("remaining_lifetime"))
                remaining = Clamp(value, 0, 100);
            else if (name.Contains("media_wearout_indicator") || name.Contains("percentage_used"))
                remaining = 100 - Clamp(value, 0, 100);
            else
                return;

            attr.SemanticsValidated = true;
            attr.Interpretation = "Validated device endurance/life indicator; interpreted from the attribute name rather than the numeric ID alone.";
            drive.RemainingLifePercent = remaining;
            drive.LifeMeaning = "Validated SMART endurance/life indicator (vendor/device semantics); this is not an overall health percentage.";
            drive.EnduranceUsedPercent = 100 - remaining;
            drive.EnduranceMeaning = "Derived from a validated device endurance/life indicator; not a failure probability.";
            Project(drive, attr);
        }

        private static void InterpretUsedEnduranceAttribute(DriveInfoRecord drive, SmartAttributeRecord attr)
        {
            if (!attr.RawValue.HasValue) return;
            var used = Clamp(attr.RawValue.Value, 0, 100);
            attr.SemanticsValidated = true;
            attr.Interpretation = "Validated percentage of endurance used; converted to remaining endurance for presentation.";
            drive.EnduranceUsedPercent = used;
            drive.RemainingLifePercent = 100 - used;
            drive.LifeMeaning = "Validated device endurance-used indicator; this is not an overall health percentage.";
            drive.EnduranceMeaning = "Validated endurance-used indicator; not a failure probability.";
            Project(drive, attr);
        }

        private static bool IsRemainingLifeAttribute(string name)
        {
            return name.Contains("percent_lifetime_remain") || name.Contains("percentage_lifetime_remaining") ||
                   name.Contains("remaining_lifetime_perc") || name.Contains("ssd_life_left") ||
                   name.Contains("remaining_lifetime") || name.Contains("life_left");
        }

        private static bool IsUsedEnduranceAttribute(string name)
        {
            return name.Contains("media_wearout_indicator") || name == "percentage_used";
        }

        private static void Validate(SmartAttributeRecord attr, string meaning)
        {
            attr.SemanticsValidated = true;
            attr.Interpretation = meaning;
        }

        private static void Project(DriveInfoRecord drive, SmartAttributeRecord attr)
        {
            if (attr.Id.HasValue && attr.RawValue.HasValue)
                drive.Attributes["ATA_" + attr.Id.Value] = attr.RawValue.Value;
        }

        private static void RemoveLegacySemanticKeys(DriveInfoRecord drive)
        {
            foreach (var key in new[] { "ATA_5", "ATA_197", "ATA_198", "ATA_199", "ATA_194" })
                drive.Attributes.Remove(key);
        }

        private static bool IsAny(string value, params string[] names) => names.Any(x => string.Equals(value, Normalize(x), StringComparison.OrdinalIgnoreCase));
        private static string Normalize(string value) => (value ?? "").Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown drive" : value.Trim();
    }
}
