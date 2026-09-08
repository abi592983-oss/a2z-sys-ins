using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace A2ZSysIns
{
    internal static class AdvancedDiagnostics
    {
        public static void Collect(InspectionReport r)
        {
            CollectCpuClockSnapshot(r);
            CollectTpm(r);
            CollectBitLocker(r);
            CollectWhea(r);
            CollectStorageStackEvents(r);
            EnrichStorageLinks(r);
        }

        private static void CollectCpuClockSnapshot(InspectionReport r)
        {
            try
            {
                var rows = EvidenceEngine.Wmi(r, @"root\cimv2", "SELECT Name,CurrentClockSpeed,MaxClockSpeed,LoadPercentage FROM Win32_Processor");
                if (rows.Count == 0) throw new InvalidOperationException("Win32_Processor returned no rows.");
                var row = rows[0];
                r.System["CPU current clock snapshot"] = EvidenceEngine.Text(row, "CurrentClockSpeed") + " MHz";
                r.System["CPU reported maximum clock"] = EvidenceEngine.Text(row, "MaxClockSpeed") + " MHz";
                r.System["CPU load snapshot"] = EvidenceEngine.Text(row, "LoadPercentage") + "%";
                EvidenceEngine.Record(r, "CPU clock/load snapshot", "Win32_Processor", "Measured",
                    "Snapshot only. It is supporting evidence and is not by itself proof of throttling.", JsonConvert.SerializeObject(rows));
            }
            catch (Exception ex) { EvidenceEngine.Record(r, "CPU clock/load snapshot", "Win32_Processor", "Unavailable", ex.GetBaseException().Message); }
        }

        private static void CollectTpm(InspectionReport r)
        {
            try
            {
                var rows = EvidenceEngine.Wmi(r, @"root\CIMV2\Security\MicrosoftTpm", "SELECT IsEnabled_InitialValue,IsActivated_InitialValue,IsOwned_InitialValue,ManufacturerId,ManufacturerVersion,SpecVersion FROM Win32_Tpm");
                if (rows.Count == 0)
                {
                    r.System["TPM"] = "Not detected / not exposed by Windows";
                    EvidenceEngine.Record(r, "TPM status", "Win32_Tpm", "Unavailable", "No Win32_Tpm instance was returned. This is configuration information, not a hardware-health failure.");
                    return;
                }
                var x = rows[0];
                r.System["TPM"] = "Enabled=" + EvidenceEngine.Text(x, "IsEnabled_InitialValue") + "; Activated=" + EvidenceEngine.Text(x, "IsActivated_InitialValue") + "; Owned=" + EvidenceEngine.Text(x, "IsOwned_InitialValue") + "; Spec=" + EvidenceEngine.Text(x, "SpecVersion");
                EvidenceEngine.Record(r, "TPM status", "Win32_Tpm", "Measured", "TPM presence/configuration only; excluded from hardware-health scoring.", JsonConvert.SerializeObject(rows));
            }
            catch (Exception ex) { EvidenceEngine.Record(r, "TPM status", "Win32_Tpm", "Unavailable", ex.GetBaseException().Message); }
        }

        private static void CollectBitLocker(InspectionReport r)
        {
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "manage-bde.exe");
            if (!File.Exists(exe)) { EvidenceEngine.Record(r, "BitLocker status", "manage-bde", "Unavailable", "manage-bde.exe is not present on this Windows installation."); return; }
            try
            {
                var raw = EvidenceEngine.Run(r, exe, "-status");
                r.System["BitLocker"] = SummarizeBitLocker(raw);
                EvidenceEngine.Record(r, "BitLocker status", "manage-bde -status", "Measured", "Configuration/security status only; excluded from hardware-health scoring.", raw);
            }
            catch (Exception ex) { EvidenceEngine.Record(r, "BitLocker status", "manage-bde -status", "Unavailable", ex.GetBaseException().Message); }
        }

        private static string SummarizeBitLocker(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "No status returned";
            var volumes = Regex.Matches(raw, @"Volume\s+([A-Z]:).*?(?=Volume\s+[A-Z]:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var items = new List<string>();
            foreach (Match m in volumes)
            {
                var protection = Regex.Match(m.Value, @"Protection Status:\s*([^\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                var conversion = Regex.Match(m.Value, @"Conversion Status:\s*([^\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                items.Add(m.Groups[1].Value + " protection=" + (protection == "" ? "unknown" : protection) + ", conversion=" + (conversion == "" ? "unknown" : conversion));
            }
            return items.Count == 0 ? "Status returned; see measurement evidence" : string.Join("; ", items);
        }

        private static void CollectWhea(InspectionReport r)
        {
            CollectProviderEvents(r, "System", "Microsoft-Windows-WHEA-Logger", null, 1000, AddWheaEvent);
        }

        private static void AddWheaEvent(InspectionReport r, string xml)
        {
            var doc = XElement.Parse(xml); var ns = doc.Name.Namespace; var system = doc.Element(ns + "System");
            var id = (int)system.Element(ns + "EventID");
            var data = doc.Descendants(ns + "Data").Where(x => x.Attribute("Name") != null)
                .GroupBy(x => (string)x.Attribute("Name")).ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
            string G(string k) { string v; return data.TryGetValue(k, out v) ? v : ""; }
            var component = First(G("Component"), G("ErrorSource"), G("ErrorType"), G("SectionType"));
            var device = First(G("PrimaryDeviceName"), G("DeviceId"), G("BusDeviceFunction"), G("ApicId"), G("ProcessorId"));
            var signature = "WHEA:" + id + ":" + Clean(component) + ":" + Clean(device);
            var item = r.Events.FirstOrDefault(x => x.Signature == signature);
            if (item == null)
            {
                item = new EventFinding { Source = "Microsoft-Windows-WHEA-Logger", EventId = id, Signature = signature,
                    Summary = "WHEA hardware error" + (component == "" ? "" : " — " + component),
                    Cause = "Windows Hardware Error Architecture recorded a hardware error. The event identifies an error record, but component replacement requires correlation with the record details and recurrence.",
                    Level = "Warning" };
                r.Events.Add(item);
            }
            item.Count++; item.RawEvents.Add(xml);
            DateTime time; if (DateTime.TryParse((string)system.Element(ns + "TimeCreated")?.Attribute("SystemTime"), null, DateTimeStyles.RoundtripKind, out time)) if (!item.Latest.HasValue || time > item.Latest) item.Latest = time;
        }

        private static void CollectStorageStackEvents(InspectionReport r)
        {
            var providers = new[] { "storahci", "stornvme", "iaStorA", "iaStorAC", "iaStorV", "disk", "Ntfs", "Microsoft-Windows-Ntfs" };
            foreach (var provider in providers)
                CollectProviderEvents(r, "System", provider, null, 1000, AddStorageStackEvent);
        }

        private static void AddStorageStackEvent(InspectionReport r, string xml)
        {
            var doc = XElement.Parse(xml); var ns = doc.Name.Namespace; var system = doc.Element(ns + "System");
            var provider = (string)system.Element(ns + "Provider").Attribute("Name"); var id = (int)system.Element(ns + "EventID");
            if ((provider.Equals("disk", StringComparison.OrdinalIgnoreCase) && id == 7) ||
                ((provider.Equals("Ntfs", StringComparison.OrdinalIgnoreCase) || provider.Equals("Microsoft-Windows-Ntfs", StringComparison.OrdinalIgnoreCase)) && id == 55)) return; // already collected by the core engine
            var data = doc.Descendants(ns + "Data").Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToArray();
            var detail = string.Join("|", data);
            var signature = "StorageStack:" + provider + ":" + id + ":" + Clean(detail);
            var item = r.Events.FirstOrDefault(x => x.Signature == signature);
            if (item == null)
            {
                item = new EventFinding { Source = provider, EventId = id, Signature = signature, Summary = "Storage-stack event " + provider + " " + id,
                    Cause = "A Windows storage/file-system provider recorded an event. Severity is based on recurrence and correlation; the provider event alone is not automatically a failed drive.", Level = "Warning" };
                r.Events.Add(item);
            }
            item.Count++; item.RawEvents.Add(xml);
        }

        private static void CollectProviderEvents(InspectionReport r, string log, string provider, int? eventId, int limit, Action<InspectionReport, string> add)
        {
            var filter = "Provider[@Name='" + provider + "']" + (eventId.HasValue ? " and EventID=" + eventId.Value : "") + " and TimeCreated[timediff(@SystemTime) <= 2592000000]";
            var query = "*[System[" + filter + "]]";
            var xmls = new List<string>();
            try
            {
                using (var reader = new EventLogReader(new EventLogQuery(log, PathType.LogName, query) { ReverseDirection = true }))
                {
                    EventRecord ev; while (xmls.Count < limit && (ev = reader.ReadEvent(TimeSpan.FromSeconds(5))) != null) using (ev) xmls.Add(ev.ToXml());
                }
                EvidenceEngine.Record(r, provider + " advanced events", "EventLogReader", xmls.Count == limit ? "Partial" : "Measured", xmls.Count + " event(s) returned from the last 30 days.");
                foreach (var xml in xmls) add(r, xml);
            }
            catch (Exception ex) { EvidenceEngine.Record(r, provider + " advanced events", "EventLogReader", "Unavailable", ex.GetBaseException().Message); }
        }

        private static void EnrichStorageLinks(InspectionReport r)
        {
            foreach (var d in r.Drives)
            {
                if (string.IsNullOrWhiteSpace(d.RawEvidence)) { EvidenceEngine.Record(r, d.DeviceId + " link speed", "smartctl JSON", "Unavailable", "No structured SMART JSON was retained for this drive."); continue; }
                try
                {
                    var j = JObject.Parse(d.RawEvidence);
                    var current = (string)j.SelectToken("interface_speed.current.string");
                    var max = (string)j.SelectToken("interface_speed.max.string");
                    if (!string.IsNullOrWhiteSpace(current)) d.LinkCurrent = current;
                    if (!string.IsNullOrWhiteSpace(max)) d.LinkMaximum = max;
                    if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(max))
                    {
                        EvidenceEngine.Record(r, d.DeviceId + " link speed", "smartctl JSON", "Unavailable", "The device did not expose both negotiated and maximum interface-speed fields. No degradation inference is made.");
                        continue;
                    }
                    EvidenceEngine.Record(r, d.DeviceId + " link speed", "smartctl JSON", "Measured", "Negotiated=" + current + "; device maximum=" + max + ". Platform/controller capability is not assumed. A mismatch is supporting evidence only unless corroborated.");
                }
                catch (Exception ex) { EvidenceEngine.Record(r, d.DeviceId + " link speed", "smartctl JSON", "Unavailable", ex.GetBaseException().Message); }
            }
        }

        private static string First(params string[] values) { return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? ""; }
        private static string Clean(string value) { return Regex.Replace(value ?? "", @"\s+", " ").Trim().Substring(0, Math.Min(160, Regex.Replace(value ?? "", @"\s+", " ").Trim().Length)); }
    }
}
