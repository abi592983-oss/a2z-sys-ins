using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace A2ZSysIns
{
    // All scoring consumes the saved report, never fresh queries to the host.
    public static class EvidenceEngine
    {
        public static void Log(InspectionReport r, string action, string response)
        {
            r.DiagnosticLog.Add(new LogEntry { AtUtc = DateTime.UtcNow, Action = action, Response = response });
        }
        public static void Record(InspectionReport r, string target, string source, string status, string reason, string response = null)
        {
            r.Measurements.Add(new Measurement { Target = target, Source = source, Status = status, Reason = reason, Response = response });
            Log(r, source + " / " + target + " / " + status, reason + "\n" + response);
            if (status == "Unavailable") r.Limitations.Add(target + ": " + reason);
        }
        public static List<Dictionary<string, object>> Wmi(InspectionReport r, string scope, string query)
        {
            Log(r, "WMI request", scope + " " + query);
            var rows = new List<Dictionary<string, object>>();
            try
            {
                using (var search = new ManagementObjectSearcher(scope, query))
                {
                    search.Options.Timeout = TimeSpan.FromSeconds(15);
                    using (var result = search.Get())
                    foreach (ManagementObject obj in result)
                    using (obj)
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (PropertyData p in obj.Properties) row[p.Name] = p.Value;
                        rows.Add(row);
                    }
                }
                Log(r, "WMI response", JsonConvert.SerializeObject(rows));
                return rows;
            }
            catch (Exception ex)
            {
                Log(r, "WMI error", ex.GetBaseException().Message);
                throw;
            }
        }
        public static string Text(Dictionary<string, object> r, string key) =>
            r.ContainsKey(key) ? Convert.ToString(r[key], CultureInfo.InvariantCulture) : "";

        public static string Run(InspectionReport r, string exe, string arguments)
        {
            Log(r, "Process request", Path.GetFileName(exe) + " " + arguments);
            using (var p = new Process { StartInfo = new ProcessStartInfo(exe, arguments) {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } })
            {
                p.Start();
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(25000))
                {
                    try { p.Kill(); } catch { }
                    Log(r, "Process timeout", "Stopped this collector process after 25 seconds.");
                    throw new TimeoutException("Collector timed out after 25 seconds.");
                }
                if (!System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { stdout, stderr }, 5000))
                    throw new TimeoutException("Collector output did not finish.");
                Log(r, "Process response", "Exit=" + p.ExitCode + "\nstdout:\n" + stdout.Result + "\nstderr:\n" + stderr.Result);
                // smartctl uses a bitmask exit code: a nonzero code can contain useful health data.
                return stdout.Result;
            }
        }

        public static void Begin(InspectionReport r)
        {
            using (var identity = WindowsIdentity.GetCurrent())
                r.IsAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            Log(r, "Session started", "App 2.1; rules " + r.RuleSetVersion + "; administrator=" + r.IsAdministrator);
            if (!r.IsAdministrator) r.Limitations.Add("Not elevated: some hardware readings may be unavailable. Security protections are not disabled.");
        }

        public static void Drives(InspectionReport r)
        {
            List<Dictionary<string, object>> disks;
            try { disks = Wmi(r, @"root\cimv2", "SELECT Model,SerialNumber,InterfaceType,Size,DeviceID,PNPDeviceID FROM Win32_DiskDrive"); }
            catch (Exception ex) { Record(r, "Physical drives", "WMI", "Unavailable", ex.Message); return; }
            foreach (var row in disks)
            {
                long.TryParse(Text(row, "Size"), out var size);
                var d = new DriveInfoRecord { Model = Text(row, "Model").Trim(), Serial = Text(row, "SerialNumber").Trim(),
                    DeviceId = Text(row, "DeviceID"), PnpId = Text(row, "PNPDeviceID"), Interface = Text(row, "InterfaceType"),
                    SizeBytes = size, SmartStatus = "Not measured" };
                r.Drives.Add(d);
                try
                {
                    var exe = FindSmartCtl();
                    if (exe == null) throw new FileNotFoundException("Bundled smartctl.exe is missing from the application package.");
                    var raw = RunSmartForDrive(r, exe, d);
                    d.RawEvidence = raw;
                    ParseSmart(d, JObject.Parse(raw));
                    if (d.SmartPassed == null && d.Attributes.Count == 0 && d.RemainingLifePercent == null)
                        throw new InvalidOperationException("smartctl returned no usable health fields; see process response.");
                    Record(r, d.DeviceId + " SMART", "smartctl", "Measured", "Parsed structured JSON; no substring-based status guesses.", raw);
                }
                catch (Exception ex)
                {
                    Record(r, d.DeviceId + " SMART", "smartctl", "Failed", ex.Message);
                    SmartFallback(r, d);
                }
                d.SmartStatus = d.SmartPassed == false ? "FAILED" : d.SmartPassed == true ? "Threshold check passed (not a health percentage)" : "Not measured";
                d.Assessment = DriveAssessment(d);
                Record(r, d.DeviceId + " assessment", "Evidence rules", d.Assessment == "Not assessed" ? "Unavailable" : "Partial",
                    d.Assessment + "; SMART cannot guarantee future reliability.");
                Record(r, d.DeviceId + " SSD remaining life", "Validated SMART life field",
                    d.RemainingLifePercent.HasValue ? "Measured" : "Unavailable", d.LifeMeaning,
                    d.RemainingLifePercent?.ToString(CultureInfo.InvariantCulture));
            }
            if (disks.Count == 0) Record(r, "Physical drives", "WMI", "Unavailable", "No devices returned.");
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

        private static string RunSmartForDrive(InspectionReport r, string exe, DriveInfoRecord drive)
        {
            var candidates = new List<string> { drive.DeviceId };
            var digits = new string((drive.DeviceId ?? "").Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (digits.Length > 0) candidates.Add("/dev/pd" + digits);
            try
            {
                var scan = JObject.Parse(Run(r, exe, "--scan-open -j"));
                foreach (var device in scan["devices"] as JArray ?? new JArray())
                {
                    var name = (string)device["name"];
                    if (!string.IsNullOrWhiteSpace(name)) candidates.Add(name);
                }
            }
            catch (Exception ex) { Log(r, "smartctl scan fallback failed", ex.GetBaseException().Message); }
            Exception last = null;
            foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var type in new[] { "", " -d ata", " -d sat" })
                {
                    try
                    {
                        var raw = Run(r, exe, "-a -j" + type + " \"" + candidate + "\"");
                        var parsed = JObject.Parse(raw);
                        var useful = parsed.SelectToken("smart_status.passed") != null
                            || parsed.SelectToken("ata_smart_attributes.table") != null
                            || parsed["nvme_smart_health_information_log"] != null
                            || parsed["scsi_grown_defect_list"] != null;
                        if (!useful) throw new InvalidOperationException("No SMART health fields returned.");
                        var returnedSerial = ((string)parsed["serial_number"] ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(drive.Serial) && !string.IsNullOrWhiteSpace(returnedSerial)
                            && !string.Equals(drive.Serial.Trim(), returnedSerial, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Returned serial number belongs to a different drive; result rejected.");
                        Log(r, "smartctl device path selected", candidate + (type == "" ? " (automatic type)" : type));
                        return raw;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Log(r, "smartctl device-path attempt failed", candidate + (type == "" ? "" : type) + ": " + ex.GetBaseException().Message);
                    }
                }
            }
            throw last ?? new InvalidOperationException("No usable smartctl device path.");
        }

        public static void ParseSmart(DriveInfoRecord d, JObject j)
        {
            d.SmartPassed = (bool?)j.SelectToken("smart_status.passed");
            foreach (var attr in (j.SelectToken("ata_smart_attributes.table") as JArray ?? new JArray()))
            {
                var id = (int?)attr["id"];
                var raw = (long?)attr.SelectToken("raw.value");
                var name = (string)attr["name"] ?? "";
                if (id.HasValue && raw.HasValue) d.Attributes["ATA_" + id.Value] = raw.Value;
                // Interpret only named, explicit life-remaining attributes; unknown vendor fields stay N/A.
                if ((name == "Percent_Lifetime_Remain" || name == "SSD_Life_Left" || name == "Remaining_Lifetime_Perc") &&
                    (int?)attr["value"] is int remaining && remaining >= 0 && remaining <= 100)
                {
                    d.RemainingLifePercent = remaining;
                    d.LifeMeaning = "Device life indicator: normalized " + name + "; not a failure probability.";
                }
            }
            var nvme = j["nvme_smart_health_information_log"];
            if ((double?)j.SelectToken("temperature.current") is double temp && temp > 0 && temp < 150)
                d.Attributes["TemperatureC"] = (long)temp;
            if (nvme != null)
            {
                foreach (var name in new[] { "critical_warning", "media_errors", "available_spare", "available_spare_threshold" })
                    if ((long?)nvme[name] is long n) d.Attributes["NVMe_" + name] = n;
                if ((double?)nvme["percentage_used"] is double used && used >= 0)
                {
                    d.RemainingLifePercent = Math.Max(0, 100 - used);
                    d.LifeMeaning = "100 minus NVMe percentage_used (endurance estimate, not overall health).";
                }
            }
            if ((double?)j.SelectToken("endurance_used.current_percent") is double enduranceUsed && enduranceUsed >= 0)
            {
                d.RemainingLifePercent = Math.Max(0, 100 - enduranceUsed);
                d.LifeMeaning = "100 minus SMART endurance_used (endurance estimate, not overall health).";
            }
        }
        private static void SmartFallback(InspectionReport r, DriveInfoRecord d)
        {
            try
            {
                var rows = Wmi(r, @"root\wmi", "SELECT InstanceName,VendorSpecific FROM MSStorageDriver_FailurePredictData");
                // Never associate drives by enumeration order. Require unique PNP-instance match.
                var matches = rows.Where(x => Text(x, "InstanceName").StartsWith(d.PnpId + "_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Text(x, "InstanceName"), d.PnpId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (string.IsNullOrEmpty(d.PnpId) || matches.Count != 1) throw new InvalidOperationException("No unique PNP-matched ATA SMART response (NVMe/USB may not support this provider).");
                var bytes = matches[0]["VendorSpecific"] as byte[];
                if (bytes == null || bytes.Length < 362) throw new InvalidOperationException("Malformed ATA SMART data.");
                for (var offset = 2; offset + 11 < Math.Min(bytes.Length, 362); offset += 12)
                {
                    var id = bytes[offset]; if (id == 0) continue;
                    long raw = 0; for (var k = 0; k < 6; k++) raw |= (long)bytes[offset + 5 + k] << (8 * k);
                    d.Attributes["ATA_" + id] = raw;
                }
                d.RawEvidence = JsonConvert.SerializeObject(matches[0]);
                try
                {
                    var statusRows = Wmi(r, @"root\wmi", "SELECT InstanceName,PredictFailure,Reason FROM MSStorageDriver_FailurePredictStatus");
                    var status = statusRows.Where(x => Text(x, "InstanceName").StartsWith(d.PnpId + "_", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Text(x, "InstanceName"), d.PnpId, StringComparison.OrdinalIgnoreCase)).Single();
                    d.SmartPassed = !Convert.ToBoolean(status["PredictFailure"], CultureInfo.InvariantCulture);
                    d.RawEvidence += "\n" + JsonConvert.SerializeObject(status);
                }
                catch (Exception ex) { Log(r, "Windows SMART threshold fallback unavailable", ex.Message); }
                Record(r, d.DeviceId + " SMART", "Windows ATA SMART fallback", "Partial",
                    "Read raw ATA attributes, uniquely matched by PNP ID. Vendor life percentage is not guessed.", d.RawEvidence);
            }
            catch (Exception ex) { Record(r, d.DeviceId + " SMART", "Windows ATA SMART fallback", "Unavailable", ex.Message); }
        }
        public static string DriveAssessment(DriveInfoRecord d)
        {
            long A(string key) => d.Attributes.TryGetValue(key, out var n) ? n : 0;
            if (d.SmartPassed == false || A("NVMe_critical_warning") != 0 || A("ATA_197") > 0 || A("ATA_198") > 0)
                return "Critical indicators";
            if (A("ATA_5") > 0 || A("NVMe_media_errors") > 0 || A("ATA_199") > 0)
                return "Attention: errors reported";
            return d.SmartPassed.HasValue || d.Attributes.Count > 0 ? "No flagged indicators in available data" : "Not assessed";
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        public static void Resources(InspectionReport r)
        {
            try
            {
                Log(r, "Volume request", "DriveInfo.GetDrives (fixed volumes only)");
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType != DriveType.Fixed) continue;
                        r.Volumes.Add(new VolumeRecord { Name = d.Name, TotalBytes = d.TotalSize, FreeBytes = d.AvailableFreeSpace });
                    }
                    catch (Exception ex) { Record(r, d.Name + " capacity", "DriveInfo", "Failed", ex.Message); }
                }
            }
            catch (Exception ex) { Log(r, "DriveInfo error", ex.Message); }
            // Also attempt WMI for missing volumes, not only when every native call failed.
            try
            {
                foreach (var row in Wmi(r, @"root\cimv2", "SELECT DeviceID,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
                {
                    var name = Text(row, "DeviceID").TrimEnd('\\') + "\\";
                    if (r.Volumes.Any(x => x.Name == name)) continue;
                    if (long.TryParse(Text(row, "Size"), out var total) && long.TryParse(Text(row, "FreeSpace"), out var free))
                        r.Volumes.Add(new VolumeRecord { Name = name, TotalBytes = total, FreeBytes = free });
                }
            }
            catch (Exception ex) { Log(r, "Volume fallback error", ex.Message); }
            Record(r, "Volume free space", "DriveInfo + WMI fallback", r.Volumes.Count == 0 ? "Unavailable" : "Measured",
                "Each volume is evaluated independently.", JsonConvert.SerializeObject(r.Volumes));
            try
            {
                var m = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
                Log(r, "Memory request", "GlobalMemoryStatusEx");
                if (!GlobalMemoryStatusEx(ref m) || m.TotalPhysical == 0) throw new InvalidOperationException("Native memory status unavailable.");
                r.MemoryUsedPercent = 100.0 * (m.TotalPhysical - m.AvailablePhysical) / m.TotalPhysical;
                Record(r, "RAM usage", "GlobalMemoryStatusEx", "Measured", "Point-in-time resource usage, not RAM integrity.", JsonConvert.SerializeObject(m));
            }
            catch (Exception ex)
            {
                Log(r, "Memory primary error", ex.Message);
                try
                {
                    var row = Wmi(r, @"root\cimv2", "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem").Single();
                    var total = Convert.ToDouble(row["TotalVisibleMemorySize"]); var free = Convert.ToDouble(row["FreePhysicalMemory"]);
                    if (total <= 0) throw new InvalidOperationException("Invalid memory total.");
                    r.MemoryUsedPercent = (total - free) * 100 / total;
                    Record(r, "RAM usage", "WMI fallback", "Measured", r.MemoryUsedPercent + "% used");
                }
                catch (Exception fallback) { Record(r, "RAM usage", "WMI fallback", "Unavailable", fallback.Message); }
            }
            BatteryWear(r);
        }

        private static void BatteryWear(InspectionReport r)
        {
            try
            {
                var designed = Wmi(r, @"root\wmi", "SELECT InstanceName,DesignedCapacity FROM BatteryStaticData")
                    .Where(x => Convert.ToDouble(x["DesignedCapacity"], CultureInfo.InvariantCulture) > 0).ToList();
                var full = Wmi(r, @"root\wmi", "SELECT InstanceName,FullChargedCapacity FROM BatteryFullChargedCapacity")
                    .Where(x => Convert.ToDouble(x["FullChargedCapacity"], CultureInfo.InvariantCulture) > 0).ToList();
                if (designed.Count != 1 || full.Count != 1) throw new InvalidOperationException("No unique battery capacity pair was returned.");
                SetBatteryWear(r, Convert.ToDouble(designed[0]["DesignedCapacity"], CultureInfo.InvariantCulture),
                    Convert.ToDouble(full[0]["FullChargedCapacity"], CultureInfo.InvariantCulture), "Windows battery WMI");
                return;
            }
            catch (Exception ex) { Log(r, "Battery WMI primary failed", ex.GetBaseException().Message); }

            var output = Path.Combine(Path.GetTempPath(), "A2Z-Battery-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                Run(r, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe"),
                    "/batteryreport /xml /output \"" + output + "\"");
                var xml = XDocument.Load(output);
                double Capacity(string name)
                {
                    var value = xml.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) || n <= 0)
                        throw new InvalidOperationException(name + " was not present in the battery report.");
                    return n;
                }
                Log(r, "powercfg battery XML response", xml.ToString());
                SetBatteryWear(r, Capacity("DesignCapacity"), Capacity("FullChargeCapacity"), "powercfg battery-report fallback");
            }
            catch (Exception ex) { Record(r, "Battery wear", "WMI + powercfg fallback", "Unavailable", "Cannot measure battery wear: " + ex.GetBaseException().Message); }
            finally { try { if (File.Exists(output)) File.Delete(output); } catch { } }
        }

        private static void SetBatteryWear(InspectionReport r, double designed, double full, string source)
        {
            r.BatteryDesignedCapacity = designed;
            r.BatteryFullChargeCapacity = full;
            r.BatteryWearPercent = Math.Max(0, Math.Min(100, 100 * (designed - full) / designed));
            Record(r, "Battery wear", source, "Measured", "Full-charge capacity compared with design capacity; charge level is separate.",
                JsonConvert.SerializeObject(new { DesignedCapacity = designed, FullChargeCapacity = full, WearPercent = r.BatteryWearPercent }));
        }

        public static bool ActualTemperature(SensorRecord s) => s.Type == "Temperature" && s.Maximum.HasValue
            && s.Name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) < 0
            && s.Name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) < 0;
        public static void FinishSensors(InspectionReport r)
        {
            Log(r, "LibreHardwareMonitor readings", JsonConvert.SerializeObject(r.Sensors));
            var cpu = r.Sensors.Any(x => ActualTemperature(x) && (x.Name.StartsWith("CPU") || x.Name == "Core Max" || x.Name == "Core Average"));
            // LHM memory readings provide a second source if native and WMI memory failed.
            if (!r.MemoryUsedPercent.HasValue)
                r.MemoryUsedPercent = r.Sensors.FirstOrDefault(x => x.Hardware == "Total Memory" && x.Type == "Load")?.Current;
            if (!cpu)
            {
                try
                {
                    var zones = Wmi(r, @"root\wmi", "SELECT InstanceName,CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    foreach (var z in zones)
                    {
                        var c = Convert.ToDouble(z["CurrentTemperature"]) / 10 - 273.15;
                        if (c <= 0 || c >= 150) continue;
                        Record(r, "ACPI thermal zone " + Text(z, "InstanceName"), "ACPI fallback", "Partial",
                            "Zone reading only; not equivalent to CPU package/core temperature.", c.ToString("0.0", CultureInfo.InvariantCulture) + " C");
                    }
                }
                catch (Exception ex) { Log(r, "ACPI fallback failed", ex.Message); }
            }
            Record(r, "CPU temperature", "LibreHardwareMonitor / ACPI fallback",
                cpu ? "Measured" : "Unavailable", cpu ? "Actual sensor readings only; headroom excluded." :
                "Cannot measure CPU temperature. ACPI zones cannot establish CPU temperature. Check elevation and bundled-driver diagnostics.");
            foreach (var d in r.Drives)
            {
                if (!r.Sensors.Any(x => ActualTemperature(x) && x.Hardware.Trim() == d.Model) && d.Attributes.TryGetValue("TemperatureC", out var temp))
                    r.Sensors.Add(new SensorRecord { Hardware = d.Model, Name = "SMART temperature (fallback)", Type = "Temperature",
                        Current = temp, Minimum = temp, Maximum = temp, Unit = "°C" });
                Record(r, d.DeviceId + " temperature", "LibreHardwareMonitor / SMART",
                    r.Sensors.Any(x => ActualTemperature(x) && x.Hardware.Trim() == d.Model) ? "Measured" : "Unavailable",
                    "Per-device temperature; no CPU or other-drive substitution.");
            }
        }

        public static void Events(InspectionReport r)
        {
            var defs = new[] {
                new[] {"System","Microsoft-Windows-Kernel-Power","41"},
                new[] {"System","Microsoft-Windows-WER-SystemErrorReporting","1001"},
                new[] {"System","Microsoft-Windows-WHEA-Logger","18"},
                new[] {"System","Disk","7"},
                new[] {"System","Ntfs","55"},
                new[] {"System","Microsoft-Windows-Ntfs","55"},
                new[] {"System","User32","1074"},
                new[] {"System","EventLog","6008"},
                new[] {"System","volmgr","46"},
                new[] {"Application","Application Error","1000"} };
            foreach (var d in defs)
            {
                var query = "*[System[Provider[@Name='" + d[1] + "'] and EventID=" + d[2] +
                    " and TimeCreated[timediff(@SystemTime) <= 2592000000]]]";
                var xmls = new List<string>();
                var limited = false;
                try
                {
                    Log(r, "EventLogReader request", d[0] + " " + query);
                    using (var reader = new EventLogReader(new EventLogQuery(d[0], PathType.LogName, query) { ReverseDirection = true }))
                    {
                        EventRecord ev;
                        while (xmls.Count < 2000 && (ev = reader.ReadEvent(TimeSpan.FromSeconds(5))) != null)
                            using (ev) xmls.Add(ev.ToXml());
                        limited = xmls.Count == 2000;
                    }
                    Record(r, d[1] + " events", "EventLogReader", limited ? "Partial" : "Measured",
                        xmls.Count + " events returned; retention/clearing may limit history.");
                }
                catch (Exception ex)
                {
                    Record(r, d[1] + " events", "EventLogReader", "Failed", ex.Message);
                    xmls.Clear();
                    try
                    {
                        var raw = Run(r, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wevtutil.exe"),
                            "qe " + d[0] + " /q:\"" + query + "\" /f:xml /e:Events /rd:true /c:2000");
                        xmls.AddRange(XDocument.Parse(raw).Descendants().Where(x => x.Name.LocalName == "Event").Select(x => x.ToString()));
                        limited = xmls.Count == 2000;
                        Record(r, d[1] + " events", "wevtutil fallback", limited ? "Partial" : "Measured", xmls.Count + " returned.");
                    }
                    catch (Exception fallback) { Record(r, d[1] + " events", "wevtutil fallback", "Unavailable", fallback.Message); continue; }
                }
                if (limited) r.Limitations.Add(d[1] + ": latest 2000 events only; counts are lower bounds.");
                foreach (var xml in xmls)
                {
                    Log(r, "Event response", xml);
                    try { AddEvent(r, xml); }
                    catch (Exception ex) { Record(r, d[1] + " event parsing", "XML", "Unavailable", ex.Message); }
                }
            }
        }
        public static void AddEvent(InspectionReport r, string xml)
        {
            var doc = XElement.Parse(xml);
            var ns = doc.Name.Namespace;
            var system = doc.Element(ns + "System");
            var provider = (string)system.Element(ns + "Provider").Attribute("Name");
            var id = (int)system.Element(ns + "EventID");
            var allData = doc.Descendants(ns + "Data").ToList();
            var data = allData.Where(x => x.Attribute("Name") != null)
                .GroupBy(x => (string)x.Attribute("Name")).ToDictionary(x => x.Key, x => x.First().Value);
            string Get(string k) => data.TryGetValue(k, out var v) ? v : "";
            string Pos(int index) => index >= 0 && index < allData.Count ? allData[index].Value : "";
            var appName = Get("AppName"); if (id == 1000 && appName == "") appName = Pos(0);
            var moduleName = Get("ModuleName"); if (id == 1000 && moduleName == "") moduleName = Pos(3);
            var exceptionCode = Get("ExceptionCode"); if (id == 1000 && exceptionCode == "") exceptionCode = Pos(6);
            var bugcheck = Get("BugcheckCode"); var powerButton = Get("PowerButtonTimestamp");
            ulong.TryParse(bugcheck, out var bugcheckNumber); ulong.TryParse(powerButton, out var powerButtonNumber);
            // Group application failures by program and exception so changing temporary module names
            // (common with MSI installers) do not hide a recurring program-level pattern.
            var detail = id == 1000 ? appName + "|" + exceptionCode :
                id == 1001 ? Get("param1") : id == 41 ? (bugcheckNumber != 0 ? "bugcheck:" + bugcheck : powerButtonNumber != 0 ? "power-button" : "unknown") :
                id == 1074 ? Get("param1") + "|" + Get("param5") :
                Get("DeviceName") + "|" + Get("ErrorSource");
            var cause = id == 41 ? ShutdownCause(bugcheck, powerButton) :
                id == 1000 ? "Application crash recorded. Faulting module: " + Empty(moduleName) + "; exception: " + Empty(exceptionCode) +
                    ". This is not evidence that the application caused a system shutdown." :
                id == 1001 ? "Windows recorded a bug check. Root cause requires supporting evidence/dump analysis." :
                id == 1074 ? "Windows recorded an initiated shutdown/restart. Process: " + Empty(Get("param1")) + "; reason: " + Empty(Get("param5")) + "." :
                id == 6008 ? "Windows recorded an improper shutdown, but this event does not identify its cause." :
                id == 46 ? "Crash-dump initialization failed; a crash may lack usable dump evidence." :
                "Provider-reported error; root cause not established.";
            var signature = provider + ":" + id + ":" + detail;
            var item = r.Events.FirstOrDefault(x => x.Signature == signature);
            if (item == null)
            {
                item = new EventFinding { Source = provider, EventId = id, Signature = signature, Cause = cause,
                    Summary = id == 41 ? "Unexpected shutdown" : id == 1000 ? "Application crash: " + (appName == "" ? "Unknown application" : appName) :
                    id == 1001 ? "BSOD / bug check" : id == 1074 ? "Initiated shutdown: " + Empty(Get("param1")) : provider + " event " + id,
                    Level = id == 41 || id == 1074 || id == 6008 ? "Information" : "Warning" };
                r.Events.Add(item);
            }
            item.Count++; item.RawEvents.Add(xml);
            if (DateTime.TryParse((string)system.Element(ns + "TimeCreated")?.Attribute("SystemTime"), null, DateTimeStyles.RoundtripKind, out var time))
                if (!item.Latest.HasValue || time > item.Latest) item.Latest = time;
        }
        public static string ShutdownCause(string bugcheck, string powerButton)
        {
            if (ulong.TryParse(bugcheck, out var code) && code != 0) return "Bug-check evidence: code " + code + ". Root cause not established.";
            if (ulong.TryParse(powerButton, out var stamp) && stamp != 0) return "Power-button timestamp recorded; forced shutdown indicated, underlying reason unknown.";
            return "Cause unknown: compatible with power loss, reset, hang or an unrecorded crash. Not proof of a hardware fault or mains outage.";
        }
        private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "not recorded" : value;
    }
}
