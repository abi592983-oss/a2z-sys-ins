using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace A2ZSysIns
{
    // Pass 2: acquisition only. Interpretation/scoring remains in later passes.
    public static class StorageAcquisitionService
    {
        public static void Collect(InspectionReport report)
        {
            List<Dictionary<string, object>> disks;
            try
            {
                disks = EvidenceEngine.Wmi(report, @"root\cimv2",
                    "SELECT Model,SerialNumber,InterfaceType,Size,DeviceID,PNPDeviceID,MediaType FROM Win32_DiskDrive");
            }
            catch (Exception ex)
            {
                EvidenceEngine.Record(report, "Physical drives", "WMI", "Unavailable", ex.Message);
                return;
            }

            var exe = FindSmartCtl();
            if (exe == null)
            {
                EvidenceEngine.Record(report, "smartctl", "Storage acquisition", "Unavailable",
                    "Bundled smartctl.exe is missing from the application package.");
            }

            foreach (var row in disks)
            {
                long.TryParse(EvidenceEngine.Text(row, "Size"), out var size);
                var drive = new DriveInfoRecord
                {
                    Model = EvidenceEngine.Text(row, "Model").Trim(),
                    Serial = EvidenceEngine.Text(row, "SerialNumber").Trim(),
                    DeviceId = EvidenceEngine.Text(row, "DeviceID"),
                    PnpId = EvidenceEngine.Text(row, "PNPDeviceID"),
                    Interface = EvidenceEngine.Text(row, "InterfaceType"),
                    SizeBytes = size,
                    SmartStatus = "Not measured",
                    SmartDeviceType = "Unknown",
                    Transport = "Unknown"
                };
                report.Drives.Add(drive);

                if (exe == null)
                {
                    MarkUnavailable(report, drive, "smartctl unavailable");
                    continue;
                }

                try
                {
                    var result = ReadSmart(report, exe, drive);
                    drive.RawEvidence = result.RawJson;
                    PopulateEvidence(drive, result.Json, result.DeviceType, result.Transport, result.SerialValidated);
                    EvidenceEngine.Record(report, drive.DeviceId + " SMART", "smartctl", "Measured",
                        "Structured JSON collected; acquisition result is preserved separately from interpretation.", result.RawJson);
                }
                catch (Exception ex)
                {
                    EvidenceEngine.Record(report, drive.DeviceId + " SMART", "smartctl", "Failed", ex.Message);
                    SmartFallback(report, drive);
                }

                if (drive.SmartPassed == null && drive.SmartAttributes.Count == 0 && drive.NvmeHealth == null)
                    drive.StorageEvidence.Availability = "Unavailable";
                drive.SmartStatus = drive.SmartPassed == false ? "FAILED" :
                    drive.SmartPassed == true ? "Threshold check passed (not a health percentage)" : "Not measured";
            }

            if (disks.Count == 0)
                EvidenceEngine.Record(report, "Physical drives", "WMI", "Unavailable", "No physical disk devices returned.");
        }

        private static string FindSmartCtl()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            return new[] {
                Path.Combine(root, "tools", "smartctl.exe"),
                Path.Combine(root, "tools", "bin", "smartctl.exe"),
                Path.Combine(root, "smartmontools", "bin", "smartctl.exe")
            }.FirstOrDefault(File.Exists);
        }

        private sealed class SmartReadResult
        {
            public JObject Json;
            public string RawJson;
            public string DeviceType;
            public string Transport;
            public bool SerialValidated;
        }

        private static SmartReadResult ReadSmart(InspectionReport report, string exe, DriveInfoRecord drive)
        {
            var candidates = new List<Tuple<string, string>>();
            AddCandidate(candidates, drive.DeviceId, "");

            try
            {
                var scanRaw = EvidenceEngine.Run(report, exe, "--scan-open -j");
                var scan = JObject.Parse(scanRaw);
                foreach (var item in scan["devices"] as JArray ?? new JArray())
                {
                    var name = (string)item["name"];
                    var type = (string)item["type"] ?? "";
                    if (!string.IsNullOrWhiteSpace(name)) AddCandidate(candidates, name, type);
                }
            }
            catch (Exception ex)
            {
                EvidenceEngine.Log(report, "smartctl scan unavailable", ex.GetBaseException().Message);
            }

            var digits = new string((drive.DeviceId ?? "").Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (digits.Length > 0) AddCandidate(candidates, "/dev/pd" + digits, "");

            // Explicit transports are fallback attempts, not assumptions about the device.
            foreach (var type in new[] { "ata", "sat", "nvme", "scsi" })
                AddCandidate(candidates, drive.DeviceId, type);

            Exception last = null;
            foreach (var candidate in candidates)
            {
                try
                {
                    var typeArg = string.IsNullOrWhiteSpace(candidate.Item2) ? "" : " -d " + candidate.Item2;
                    var raw = EvidenceEngine.Run(report, exe, "-a -j" + typeArg + " \"" + candidate.Item1 + "\"");
                    var json = JObject.Parse(raw);
                    if (!HasUsefulEvidence(json)) throw new InvalidOperationException("No usable SMART/health fields returned.");

                    var returnedSerial = ((string)json["serial_number"] ?? "").Trim();
                    var serialValidated = string.IsNullOrWhiteSpace(drive.Serial) || string.IsNullOrWhiteSpace(returnedSerial);
                    if (!string.IsNullOrWhiteSpace(drive.Serial) && !string.IsNullOrWhiteSpace(returnedSerial))
                    {
                        serialValidated = string.Equals(drive.Serial.Trim(), returnedSerial,
                            StringComparison.OrdinalIgnoreCase);
                        if (!serialValidated) throw new InvalidOperationException("Returned serial does not match the WMI drive identity; result rejected.");
                    }

                    var deviceType = DetectDeviceType(json, candidate.Item2);
                    var transport = DetectTransport(json, candidate.Item2);
                    EvidenceEngine.Log(report, "smartctl device selected",
                        candidate.Item1 + (string.IsNullOrWhiteSpace(candidate.Item2) ? "" : " (-d " + candidate.Item2 + ")") +
                        "; type=" + deviceType + "; transport=" + transport + "; serialValidated=" + serialValidated);
                    return new SmartReadResult { Json = json, RawJson = raw, DeviceType = deviceType, Transport = transport, SerialValidated = serialValidated };
                }
                catch (Exception ex)
                {
                    last = ex;
                    EvidenceEngine.Log(report, "smartctl acquisition attempt failed",
                        candidate.Item1 + (string.IsNullOrWhiteSpace(candidate.Item2) ? "" : " (-d " + candidate.Item2 + ")") + ": " + ex.GetBaseException().Message);
                }
            }
            throw last ?? new InvalidOperationException("No usable smartctl device path.");
        }

        private static void AddCandidate(List<Tuple<string, string>> list, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!list.Any(x => string.Equals(x.Item1, name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Item2, type, StringComparison.OrdinalIgnoreCase)))
                list.Add(Tuple.Create(name, type));
        }

        private static bool HasUsefulEvidence(JObject json)
        {
            return json.SelectToken("smart_status.passed") != null ||
                   json.SelectToken("ata_smart_attributes.table") != null ||
                   json["nvme_smart_health_information_log"] != null ||
                   json["scsi_grown_defect_list"] != null ||
                   json["scsi_error_counter_log"] != null ||
                   json["temperature.current"] != null ||
                   json["device"] != null;
        }

        private static string DetectDeviceType(JObject json, string scanType)
        {
            var type = (string)json.SelectToken("device.type") ?? scanType;
            if (string.IsNullOrWhiteSpace(type))
            {
                if (json["nvme_smart_health_information_log"] != null) return "NVMe";
                if (json["ata_smart_attributes"] != null) return "ATA/SATA";
                if (json["scsi_grown_defect_list"] != null || json["scsi_error_counter_log"] != null) return "SCSI/SAS";
                return "Unknown";
            }
            if (type.IndexOf("nvme", StringComparison.OrdinalIgnoreCase) >= 0) return "NVMe";
            if (type.IndexOf("sat", StringComparison.OrdinalIgnoreCase) >= 0) return "ATA/SATA via SAT";
            if (type.IndexOf("ata", StringComparison.OrdinalIgnoreCase) >= 0) return "ATA/SATA";
            if (type.IndexOf("scsi", StringComparison.OrdinalIgnoreCase) >= 0) return "SCSI/SAS";
            return type;
        }

        private static string DetectTransport(JObject json, string scanType)
        {
            var transport = (string)json.SelectToken("device.protocol") ?? "";
            if (!string.IsNullOrWhiteSpace(transport)) return transport;
            if (scanType.IndexOf("sat", StringComparison.OrdinalIgnoreCase) >= 0) return "USB/SAT or SAT bridge";
            if (scanType.IndexOf("nvme", StringComparison.OrdinalIgnoreCase) >= 0) return "NVMe";
            if (scanType.IndexOf("scsi", StringComparison.OrdinalIgnoreCase) >= 0) return "SCSI/SAS";
            if (scanType.IndexOf("ata", StringComparison.OrdinalIgnoreCase) >= 0) return "ATA/SATA";
            return "Unknown";
        }

        private static void PopulateEvidence(DriveInfoRecord d, JObject json, string deviceType, string transport, bool serialValidated)
        {
            d.SmartDeviceType = deviceType;
            d.Transport = transport;
            d.SmartDataSource = "smartctl JSON";
            d.StorageEvidence.Source = "smartctl JSON";
            d.StorageEvidence.Availability = "Measured";
            d.StorageEvidence.Quality = serialValidated ? "Validated" : "Measured; serial not independently validated";
            d.StorageEvidence.DeviceType = deviceType;
            d.StorageEvidence.Transport = transport;
            d.StorageEvidence.SerialValidated = serialValidated;
            d.StorageEvidence.HasAtaSmart = json["ata_smart_attributes"] != null;
            d.StorageEvidence.HasNvmeHealth = json["nvme_smart_health_information_log"] != null;
            d.StorageEvidence.HasScsiHealth = json["scsi_grown_defect_list"] != null || json["scsi_error_counter_log"] != null;

            d.Vendor = (string)json["vendor"];
            d.Product = (string)json["product"] ?? (string)json["model_name"] ?? d.Model;
            d.Firmware = (string)json["firmware_version"];
            d.LinkCurrent = (string)json.SelectToken("interface_speed.current.string");
            d.LinkMaximum = (string)json.SelectToken("interface_speed.max.string");

            d.SmartPassed = (bool?)json.SelectToken("smart_status.passed");
            var temp = ReadDouble(json.SelectToken("temperature.current"));
            if (temp.HasValue && temp.Value >= -40 && temp.Value <= 150)
            {
                d.TemperatureC = temp;
                d.StorageEvidence.HasTemperature = true;
            }

            foreach (var attr in json.SelectToken("ata_smart_attributes.table") as JArray ?? new JArray())
            {
                var rec = new SmartAttributeRecord
                {
                    Id = (int?)attr["id"],
                    Name = (string)attr["name"],
                    RawValue = (long?)attr.SelectToken("raw.value"),
                    NormalizedValue = (long?)attr["value"],
                    WorstValue = (long?)attr["worst"],
                    Threshold = (long?)attr["thresh"],
                    RawString = (string)attr.SelectToken("raw.string"),
                    Source = "smartctl"
                };
                rec.SemanticsValidated = !string.IsNullOrWhiteSpace(rec.Name);
                d.SmartAttributes.Add(rec);
                if (rec.Id.HasValue && rec.RawValue.HasValue) d.Attributes["ATA_" + rec.Id.Value] = rec.RawValue.Value;
                if (rec.Id.HasValue && rec.RawValue.HasValue && rec.Id.Value == 194 && !d.TemperatureC.HasValue)
                    d.TemperatureC = rec.RawValue;
            }

            var nvme = json["nvme_smart_health_information_log"] as JObject;
            if (nvme != null)
            {
                d.NvmeHealth = new NvmeHealthRecord
                {
                    CriticalWarning = (long?)nvme["critical_warning"],
                    AvailableSparePercent = (long?)nvme["available_spare"],
                    AvailableSpareThresholdPercent = (long?)nvme["available_spare_threshold"],
                    PercentageUsed = ReadDouble(nvme["percentage_used"]),
                    MediaErrors = (long?)nvme["media_errors"],
                    ErrorLogEntries = (long?)nvme["num_err_log_entries"],
                    TemperatureC = ReadDouble(nvme["temperature"]),
                    ControllerTemperatureC = ReadDouble(nvme["controller_busy_time"]),
                    DataUnitsRead = ReadDouble(nvme["data_units_read"]),
                    DataUnitsWritten = ReadDouble(nvme["data_units_written"]),
                    Interpretation = "Raw NVMe health/endurance evidence; interpretation deferred."
                };
                if (d.NvmeHealth.TemperatureC.HasValue) d.TemperatureC = d.NvmeHealth.TemperatureC;
                d.ControllerTemperatureC = ReadDouble(json.SelectToken("nvme_smart_health_information_log.controller_temperature"));
                d.StorageEvidence.HasTemperature = d.TemperatureC.HasValue;
            }

            var endurance = ReadDouble(json.SelectToken("endurance_used.current_percent"));
            if (endurance.HasValue && endurance.Value >= 0)
            {
                d.EnduranceUsedPercent = endurance;
                d.EnduranceMeaning = "Raw endurance_used.current_percent; interpretation deferred.";
                d.StorageEvidence.HasEndurance = true;
            }
            if (d.NvmeHealth != null && d.NvmeHealth.PercentageUsed.HasValue)
            {
                d.EnduranceUsedPercent = d.NvmeHealth.PercentageUsed;
                d.EnduranceMeaning = "Raw NVMe percentage_used; interpretation deferred.";
                d.StorageEvidence.HasEndurance = true;
            }

            d.StorageEvidence.UnavailableFields.Clear();
            if (!d.SmartPassed.HasValue) d.StorageEvidence.UnavailableFields.Add("SMART overall status");
            if (!d.TemperatureC.HasValue) d.StorageEvidence.UnavailableFields.Add("Temperature");
            if (!d.StorageEvidence.HasAtaSmart && !d.StorageEvidence.HasNvmeHealth && !d.StorageEvidence.HasScsiHealth)
                d.StorageEvidence.UnavailableFields.Add("SMART health structure");
        }

        private static double? ReadDouble(JToken token)
        {
            if (token == null) return null;
            double value;
            if (double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            return null;
        }

        private static void SmartFallback(InspectionReport report, DriveInfoRecord d)
        {
            try
            {
                var rows = EvidenceEngine.Wmi(report, @"root\wmi", "SELECT InstanceName,VendorSpecific FROM MSStorageDriver_FailurePredictData");
                var matches = rows.Where(x => EvidenceEngine.Text(x, "InstanceName").StartsWith(d.PnpId + "_", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(EvidenceEngine.Text(x, "InstanceName"), d.PnpId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (string.IsNullOrWhiteSpace(d.PnpId) || matches.Count != 1)
                    throw new InvalidOperationException("No unique PNP-matched ATA SMART response; NVMe and many USB bridges do not expose this provider.");
                var bytes = matches[0]["VendorSpecific"] as byte[];
                if (bytes == null || bytes.Length < 362) throw new InvalidOperationException("Malformed ATA SMART data.");
                for (var offset = 2; offset + 11 < Math.Min(bytes.Length, 362); offset += 12)
                {
                    var id = bytes[offset];
                    if (id == 0) continue;
                    long raw = 0;
                    for (var k = 0; k < 6; k++) raw |= (long)bytes[offset + 5 + k] << (8 * k);
                    d.Attributes["ATA_" + id] = raw;
                    d.SmartAttributes.Add(new SmartAttributeRecord { Id = id, RawValue = raw, Source = "Windows MSStorageDriver_FailurePredictData", SemanticsValidated = false });
                }
                d.RawEvidence = JsonConvert.SerializeObject(matches[0]);
                d.SmartDataSource = "Windows ATA SMART fallback";
                d.StorageEvidence.Source = d.SmartDataSource;
                d.StorageEvidence.Availability = "Partial";
                d.StorageEvidence.Quality = "Partial; raw ATA values only";
                d.StorageEvidence.DeviceType = "ATA/SATA";
                d.StorageEvidence.Transport = "Windows storage driver";
                d.StorageEvidence.HasAtaSmart = true;

                try
                {
                    var statusRows = EvidenceEngine.Wmi(report, @"root\wmi", "SELECT InstanceName,PredictFailure,Reason FROM MSStorageDriver_FailurePredictStatus");
                    var status = statusRows.Where(x => EvidenceEngine.Text(x, "InstanceName").StartsWith(d.PnpId + "_", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(EvidenceEngine.Text(x, "InstanceName"), d.PnpId, StringComparison.OrdinalIgnoreCase)).Single();
                    d.SmartPassed = !Convert.ToBoolean(status["PredictFailure"], CultureInfo.InvariantCulture);
                    d.RawEvidence += "\n" + JsonConvert.SerializeObject(status);
                }
                catch (Exception ex) { EvidenceEngine.Log(report, "Windows SMART threshold fallback unavailable", ex.Message); }

                EvidenceEngine.Record(report, d.DeviceId + " SMART", "Windows ATA SMART fallback", "Partial",
                    "Raw ATA evidence collected by unique PNP identity; semantic interpretation deferred.", d.RawEvidence);
            }
            catch (Exception ex)
            {
                MarkUnavailable(report, d, ex.GetBaseException().Message);
            }
        }

        private static void MarkUnavailable(InspectionReport report, DriveInfoRecord d, string reason)
        {
            d.SmartDataSource = "Unavailable";
            d.StorageEvidence.Source = "Unavailable";
            d.StorageEvidence.Availability = "Unavailable";
            d.StorageEvidence.Quality = "Unavailable";
            d.StorageEvidence.FailureReason = reason;
            d.StorageEvidence.UnavailableFields.Add("SMART health data");
            EvidenceEngine.Record(report, d.DeviceId + " SMART", "Storage acquisition", "Unavailable", reason);
        }
    }
}
