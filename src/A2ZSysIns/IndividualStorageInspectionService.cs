using System;
using System.Collections.Generic;
using System.Linq;

namespace A2ZSysIns
{
    // Pass 6: backend architecture for focused, per-drive storage inspection.
    // This layer deliberately reuses the same acquired evidence and interpretation
    // rules as the full-system inspection. UI code should depend on this service,
    // not on SMART IDs or smartctl directly.
    internal static class IndividualStorageInspectionService
    {
        internal sealed class Target
        {
            public int Index;
            public string DisplayName;
            public string Model;
            public string Serial;
            public string DeviceType;
            public string Transport;
            public long SizeBytes;
        }

        internal sealed class Result
        {
            public bool Success;
            public Target Target;
            public DriveInfoRecord Drive;
            public StorageHealthAssessmentService.Result Health;
            public string FailureReason;
        }

        public static List<Target> GetTargets(InspectionReport report)
        {
            var result = new List<Target>();
            if (report == null) return result;

            for (var i = 0; i < report.Drives.Count; i++)
            {
                var drive = report.Drives[i];
                if (drive == null) continue;
                result.Add(new Target
                {
                    Index = i,
                    DisplayName = BuildDisplayName(drive),
                    Model = drive.Model,
                    Serial = drive.Serial,
                    DeviceType = NormalizeDeviceType(drive),
                    Transport = SafeTransport(drive),
                    SizeBytes = drive.SizeBytes
                });
            }
            return result;
        }

        public static Result Inspect(InspectionReport report, Target target)
        {
            if (report == null || target == null)
                return Failure("Inspection report or storage target was not supplied.");

            var drive = Resolve(report, target);
            if (drive == null)
                return Failure("The selected storage device is no longer present in the inspection report.");

            // The evidence has already been acquired by the normal storage path.
            // Re-run interpretation for the selected drive so this service remains
            // the single backend boundary for focused inspection presentation.
            var focusedReport = new InspectionReport();
            focusedReport.Drives.Add(drive);
            StorageInterpretationService.Interpret(focusedReport);

            var health = StorageHealthAssessmentService.Assess(drive);
            return new Result
            {
                Success = true,
                Target = ToTarget(drive, target.Index),
                Drive = drive,
                Health = health
            };
        }

        private static DriveInfoRecord Resolve(InspectionReport report, Target target)
        {
            if (target.Index >= 0 && target.Index < report.Drives.Count)
            {
                var indexed = report.Drives[target.Index];
                if (Matches(indexed, target)) return indexed;
            }
            return report.Drives.FirstOrDefault(x => x != null && Matches(x, target));
        }

        private static bool Matches(DriveInfoRecord drive, Target target)
        {
            if (drive == null) return false;
            if (!string.IsNullOrWhiteSpace(target.Serial) && !string.IsNullOrWhiteSpace(drive.Serial) &&
                string.Equals(target.Serial.Trim(), drive.Serial.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrWhiteSpace(target.Model) && !string.IsNullOrWhiteSpace(drive.Model) &&
                string.Equals(target.Model.Trim(), drive.Model.Trim(), StringComparison.OrdinalIgnoreCase) &&
                target.SizeBytes == drive.SizeBytes) return true;
            return false;
        }

        private static Target ToTarget(DriveInfoRecord drive, int index) => new Target
        {
            Index = index,
            DisplayName = BuildDisplayName(drive),
            Model = drive.Model,
            Serial = drive.Serial,
            DeviceType = NormalizeDeviceType(drive),
            Transport = SafeTransport(drive),
            SizeBytes = drive.SizeBytes
        };

        private static string NormalizeDeviceType(DriveInfoRecord drive)
        {
            var type = drive.SmartDeviceType ?? "";
            if (type.IndexOf("nvme", StringComparison.OrdinalIgnoreCase) >= 0) return "NVMe SSD";
            if (type.IndexOf("scsi", StringComparison.OrdinalIgnoreCase) >= 0) return "SCSI/SAS";
            if (type.IndexOf("ata", StringComparison.OrdinalIgnoreCase) >= 0) return "SATA/ATA";
            if (SafeTransport(drive).IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0) return "USB/removable storage";
            return string.IsNullOrWhiteSpace(type) ? "Unknown storage" : type;
        }

        private static string SafeTransport(DriveInfoRecord drive) =>
            string.IsNullOrWhiteSpace(drive == null ? null : drive.Transport) ? "Unknown" : drive.Transport;

        private static string BuildDisplayName(DriveInfoRecord drive)
        {
            var model = string.IsNullOrWhiteSpace(drive.Model) ? "Unknown drive" : drive.Model.Trim();
            return model + " — " + NormalizeDeviceType(drive);
        }

        private static Result Failure(string reason) => new Result
        {
            Success = false,
            FailureReason = reason
        };
    }
}
