using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;

namespace A2ZSysIns
{
    public static class Collectors
    {
        public static void CollectSystem(InspectionReport report)
        {
            report.System["Computer name"] = Environment.MachineName;
            report.System["Operating system"] = GetWmi("Win32_OperatingSystem", "Caption");
            report.System["OS version"] = Environment.OSVersion.VersionString;
            report.System["OS architecture"] = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            report.System["Manufacturer"] = GetWmi("Win32_ComputerSystem", "Manufacturer");
            report.System["Model"] = GetWmi("Win32_ComputerSystem", "Model");
            report.System["Serial number"] = GetWmi("Win32_BIOS", "SerialNumber");
            report.System["BIOS"] = GetWmi("Win32_BIOS", "SMBIOSBIOSVersion");
            report.System["CPU"] = GetWmi("Win32_Processor", "Name");
            report.System["Logical processors"] = Environment.ProcessorCount.ToString();
            report.System["Installed RAM"] = FormatBytes(ToLong(GetWmi("Win32_ComputerSystem", "TotalPhysicalMemory")));
            report.System["GPU"] = JoinWmi("Win32_VideoController", "Name");
            report.System["GPU driver"] = JoinWmi("Win32_VideoController", "DriverVersion");
            report.System["System uptime"] = TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue).ToString(@"d\.hh\:mm\:ss");
            report.System[".NET runtime"] = Environment.Version.ToString();
            CollectMemoryModules(report);
            CollectVolumes(report);
            CollectBattery(report);
            CollectWindowsCondition(report);
        }

        private static void CollectMemoryModules(InspectionReport report)
        {
            var rows = Query("Win32_PhysicalMemory", "Capacity", "Speed", "Manufacturer", "PartNumber");
            report.System["Memory modules"] = rows.Count == 0 ? "N/A" : string.Join("; ", rows.Select((r, i) => "Slot " + (i + 1) + ": " + FormatBytes(ToLong(Val(r, "Capacity"))) + " @ " + Val(r, "Speed") + " MHz " + Val(r, "Manufacturer") + " " + Val(r, "PartNumber")));
        }

        private static void CollectVolumes(InspectionReport report)
        {
            var items = new List<string>();
            foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed))
                items.Add(d.Name + " " + FormatBytes(d.TotalSize) + " total, " + FormatBytes(d.AvailableFreeSpace) + " free");
            report.System["Volumes"] = items.Count == 0 ? "N/A" : string.Join("; ", items);
        }

        private static void CollectBattery(InspectionReport report)
        {
            var rows = Query("Win32_Battery", "Name", "EstimatedChargeRemaining", "BatteryStatus");
            if (rows.Count == 0) { report.System["Battery"] = "N/A (desktop or not reported)"; return; }
            report.System["Battery"] = string.Join("; ", rows.Select(r => Val(r, "Name") + ", charge " + Val(r, "EstimatedChargeRemaining") + "%, status " + Val(r, "BatteryStatus")));
        }

        private static void CollectWindowsCondition(InspectionReport report)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    report.System["Windows display version"] = Convert.ToString(key == null ? null : (key.GetValue("DisplayVersion") ?? key.GetValue("ReleaseId"))) ?? "N/A";
            }
            catch { report.System["Windows display version"] = "N/A"; }
            var problemDevices = QueryWhere("Win32_PnPEntity", "ConfigManagerErrorCode <> 0", "Name", "ConfigManagerErrorCode");
            report.System["Problem devices"] = problemDevices.Count == 0 ? "None reported" : string.Join("; ", problemDevices.Take(10).Select(r => Val(r, "Name") + " (code " + Val(r, "ConfigManagerErrorCode") + ")"));
        }

        public static void CollectDrives(InspectionReport report)
        {
            foreach (var row in Query("Win32_DiskDrive", "Model", "SerialNumber", "InterfaceType", "Size", "Status", "DeviceID"))
            {
                var drive = new DriveInfoRecord { Model = Val(row, "Model"), Serial = Val(row, "SerialNumber"), Interface = Val(row, "InterfaceType"), SizeBytes = ToLong(Val(row, "Size")), SmartStatus = Val(row, "Status") };
                drive.RawEvidence = RunSmartCtl(Val(row, "DeviceID"));
                if (!string.IsNullOrWhiteSpace(drive.RawEvidence))
                {
                    if (drive.RawEvidence.IndexOf("PASSED", StringComparison.OrdinalIgnoreCase) >= 0) drive.SmartStatus = "PASSED";
                    if (drive.RawEvidence.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0) drive.SmartStatus = "FAILED";
                }
                report.Drives.Add(drive);
            }
            if (report.Drives.Count == 0) report.Limitations.Add("No physical storage devices were reported by Windows.");
        }

        private static string RunSmartCtl(string deviceId)
        {
            var exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "smartctl.exe");
            if (!File.Exists(exe)) return null;
            try
            {
                var p = new Process { StartInfo = new ProcessStartInfo(exe, "-a -j \"" + deviceId + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(20000);
                return output;
            }
            catch { return null; }
        }

        public static void CollectEvents(InspectionReport report)
        {
            var definitions = new[] { Tuple.Create("Kernel-Power", 41, "Unexpected shutdown"), Tuple.Create("BugCheck", 1001, "Windows bug check / BSOD"), Tuple.Create("WHEA-Logger", 18, "Hardware error"), Tuple.Create("Disk", 7, "Disk read/write error"), Tuple.Create("Ntfs", 55, "NTFS file-system error") };
            var start = DateTime.Now.AddDays(-30).ToUniversalTime().ToString("o");
            foreach (var d in definitions)
            {
                try
                {
                    var query = "*[System[(EventID=" + d.Item2 + ") and TimeCreated[@SystemTime >= '" + start + "']]]";
                    var count = 0; DateTime? latest = null;
                    using (var reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, query)))
                    {
                        EventRecord ev;
                        while (count < 500 && (ev = reader.ReadEvent()) != null) { using (ev) { count++; if (!latest.HasValue || ev.TimeCreated > latest) latest = ev.TimeCreated; } }
                    }
                    if (count > 0) report.Events.Add(new EventFinding { Source = d.Item1, EventId = d.Item2, Level = d.Item1 == "Kernel-Power" ? "Warning" : "Error", Count = count, Latest = latest, Summary = d.Item3 });
                }
                catch { report.Limitations.Add("Could not read Event ID " + d.Item2 + ". Try running as administrator."); }
            }
        }

        public static void CollectSensors(InspectionReport report, Action<string> status)
        {
            object computer = null;
            try
            {
                // LibreHardwareMonitorLib is restored and shipped automatically with the app.
                // Load by assembly name so the collector does not depend on a user-selected path.
                var asm = Assembly.Load("LibreHardwareMonitorLib"); var type = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true); computer = Activator.CreateInstance(type);
                foreach (var p in new[] { "IsCpuEnabled", "IsGpuEnabled", "IsMemoryEnabled", "IsMotherboardEnabled", "IsStorageEnabled", "IsControllerEnabled" }) type.GetProperty(p)?.SetValue(computer, true, null);
                type.GetMethod("Open").Invoke(computer, null);
                var values = new Dictionary<string, SensorRecord>();
                for (var sample = 0; sample < 6; sample++)
                {
                    status("Sampling sensors " + (sample + 1) + "/6...");
                    foreach (var hw in (System.Collections.IEnumerable)type.GetProperty("Hardware").GetValue(computer, null)) VisitHardware(hw, values);
                    if (sample < 5) Thread.Sleep(1000);
                }
                report.Sensors.AddRange(values.Values.OrderBy(x => x.Hardware).ThenBy(x => x.Type).ThenBy(x => x.Name));
            }
            catch (Exception ex) { report.Limitations.Add("Bundled sensor collector failed: " + ex.GetBaseException().Message + ". Run as administrator and confirm this hardware exposes supported sensors."); }
            finally { try { if (computer != null) computer.GetType().GetMethod("Close").Invoke(computer, null); } catch { } }
        }

        private static void VisitHardware(object hw, Dictionary<string, SensorRecord> values)
        {
            var type = hw.GetType(); type.GetMethod("Update")?.Invoke(hw, null); var hwName = Convert.ToString(type.GetProperty("Name").GetValue(hw, null));
            foreach (var s in (System.Collections.IEnumerable)type.GetProperty("Sensors").GetValue(hw, null))
            {
                var st = Convert.ToString(s.GetType().GetProperty("SensorType").GetValue(s, null));
                if (st != "Temperature" && st != "Fan" && st != "Load" && st != "Clock" && st != "Voltage") continue;
                var name = Convert.ToString(s.GetType().GetProperty("Name").GetValue(s, null)); var valueObj = s.GetType().GetProperty("Value").GetValue(s, null);
                if (valueObj == null) continue; var value = Convert.ToSingle(valueObj); var key = hwName + "|" + st + "|" + name;
                if (!values.TryGetValue(key, out var rec)) { rec = new SensorRecord { Hardware = hwName, Name = name, Type = st, Current = value, Minimum = value, Maximum = value, Unit = Unit(st) }; values[key] = rec; }
                rec.Current = value; rec.Minimum = Math.Min(rec.Minimum ?? value, value); rec.Maximum = Math.Max(rec.Maximum ?? value, value);
            }
            foreach (var sub in (System.Collections.IEnumerable)type.GetProperty("SubHardware").GetValue(hw, null)) VisitHardware(sub, values);
        }

        private static string Unit(string type) => type == "Temperature" ? "°C" : type == "Fan" ? "RPM" : type == "Load" ? "%" : type == "Clock" ? "MHz" : type == "Voltage" ? "V" : "";
        private static string GetWmi(string cls, string property) { var rows = Query(cls, property); return rows.Count == 0 ? "N/A" : Val(rows[0], property); }
        private static string JoinWmi(string cls, string property) { var r = Query(cls, property).Select(x => Val(x, property)).Where(x => x != "N/A").Distinct().ToArray(); return r.Length == 0 ? "N/A" : string.Join("; ", r); }
        private static List<Dictionary<string, object>> Query(string cls, params string[] props) => QueryWhere(cls, null, props);
        private static List<Dictionary<string, object>> QueryWhere(string cls, string where, params string[] props)
        {
            var result = new List<Dictionary<string, object>>();
            try { using (var s = new ManagementObjectSearcher("SELECT " + string.Join(",", props) + " FROM " + cls + (string.IsNullOrEmpty(where) ? "" : " WHERE " + where))) foreach (ManagementObject o in s.Get()) { var d = new Dictionary<string, object>(); foreach (var p in props) d[p] = o[p]; result.Add(d); o.Dispose(); } } catch { }
            return result;
        }
        private static string Val(Dictionary<string, object> d, string key) => d.ContainsKey(key) && d[key] != null && !string.IsNullOrWhiteSpace(Convert.ToString(d[key])) ? Convert.ToString(d[key]).Trim() : "N/A";
        private static long ToLong(string value) { long.TryParse(value, out var n); return n; }
        public static string FormatBytes(long value) { if (value <= 0) return "N/A"; string[] u = { "B", "KB", "MB", "GB", "TB" }; double n = value; var i = 0; while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; } return n.ToString("0.##") + " " + u[i]; }
    }
}
