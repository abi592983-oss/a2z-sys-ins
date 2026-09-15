using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace A2ZSysIns
{
    // Last-resort storage evidence provider. CrystalDiskInfo is used headlessly only
    // as an evidence source; A2Z interpretation and reporting remain authoritative.
    internal static class CrystalDiskInfoFallbackService
    {
        private sealed class Block
        {
            public string Model;
            public string Serial;
            public string Firmware;
            public string Health;
            public long? SizeBytes;
            public readonly List<SmartAttributeRecord> Attributes = new List<SmartAttributeRecord>();
            public string Raw;
        }

        public static bool TryCollect(InspectionReport report, DriveInfoRecord drive)
        {
            var executable = FindExecutable();
            if (executable == null)
            {
                EvidenceEngine.Record(report, drive.DeviceId + " CrystalDiskInfo fallback", "CrystalDiskInfo", "Unavailable",
                    "No portable CrystalDiskInfo executable was found. Set A2Z_CRYSTALDISKINFO_PATH or place the portable folder under tools\\CrystalDiskInfo.");
                return false;
            }

            string tempRoot = null;
            try
            {
                var sourceDir = Path.GetDirectoryName(executable);
                tempRoot = Path.Combine(Path.GetTempPath(), "A2Z-CDI-" + Guid.NewGuid().ToString("N"));
                CopyDirectory(sourceDir, tempRoot);
                var tempExe = Path.Combine(tempRoot, Path.GetFileName(executable));
                var output = Path.Combine(tempRoot, "DiskInfo.txt");
                if (File.Exists(output)) File.Delete(output);

                EvidenceEngine.Log(report, "CrystalDiskInfo fallback started", Path.GetFileName(executable) + " /CopyExit");
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = tempExe, Arguments = "/CopyExit", WorkingDirectory = tempRoot,
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    process.Start();
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(35000))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("CrystalDiskInfo /CopyExit timed out after 35 seconds.");
                    }
                    System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { stdout, stderr }, 5000);
                    EvidenceEngine.Log(report, "CrystalDiskInfo fallback process", "Exit=" + process.ExitCode + "\nstdout:\n" + stdout.Result + "\nstderr:\n" + stderr.Result);
                }

                if (!File.Exists(output)) throw new InvalidOperationException("CrystalDiskInfo completed without creating DiskInfo.txt.");
                var raw = ReadText(output);
                if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("CrystalDiskInfo produced an empty DiskInfo.txt.");
                var match = MatchDrive(ParseBlocks(raw), drive);
                if (match == null) throw new InvalidOperationException("CrystalDiskInfo returned storage data, but no drive block could be matched safely to this WMI drive.");

                Apply(drive, match);
                drive.RawEvidence = "CrystalDiskInfo /CopyExit\n" + match.Raw;
                EvidenceEngine.Record(report, drive.DeviceId + " SMART", "CrystalDiskInfo", "Measured",
                    "Headless CrystalDiskInfo evidence matched to the WMI drive identity; A2Z interpretation remains independent.", match.Raw);
                return true;
            }
            catch (Exception ex)
            {
                EvidenceEngine.Record(report, drive.DeviceId + " CrystalDiskInfo fallback", "CrystalDiskInfo", "Failed", ex.GetBaseException().Message);
                return false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempRoot)) { try { Directory.Delete(tempRoot, true); } catch { } }
            }
        }

        private static string ReadText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string FindExecutable()
        {
            var candidates = new List<string>();
            var configured = Environment.GetEnvironmentVariable("A2Z_CRYSTALDISKINFO_PATH");
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
            var root = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var relative in new[] { @"tools\CrystalDiskInfo\DiskInfo64S.exe", @"tools\CrystalDiskInfo\DiskInfo64.exe", @"tools\crystaldiskinfo\DiskInfo64S.exe", @"tools\crystaldiskinfo\DiskInfo64.exe", @"CrystalDiskInfo\DiskInfo64S.exe", @"CrystalDiskInfo\DiskInfo64.exe" }) candidates.Add(Path.Combine(root, relative));
            foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                candidates.Add(Path.Combine(dir, "CrystalDiskInfo", "DiskInfo64S.exe"));
                candidates.Add(Path.Combine(dir, "CrystalDiskInfo", "DiskInfo64.exe"));
            }
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            candidates.Add(Path.Combine(desktop, "DiskInfo64S.exe"));
            candidates.Add(Path.Combine(desktop, "DiskInfo64.exe"));
            candidates.Add(Path.Combine(desktop, "CrystalDiskInfo", "DiskInfo64S.exe"));
            candidates.Add(Path.Combine(desktop, "CrystalDiskInfo", "DiskInfo64.exe"));
            foreach (var item in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (File.Exists(item)) return item;
                if (Directory.Exists(item))
                {
                    var found = new[] { "DiskInfo64S.exe", "DiskInfo64.exe" }.Select(x => Path.Combine(item, x)).FirstOrDefault(File.Exists);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static List<Block> ParseBlocks(string text)
        {
            var result = new List<Block>();
            var matches = Regex.Matches(text ?? string.Empty, @"(?ms)^\s*Drive Name:\s*(?<model>[^\r\n]*)\r?\n(?<body>.*?)(?=\r?\n-{10,}\r?\n\r?\nStorage\s*\r?\n\s*Drive Name:|\z)");
            foreach (Match match in matches)
            {
                var block = new Block { Model = match.Groups["model"].Value.Trim(), Raw = match.Value.Trim() };
                var body = match.Groups["body"].Value;
                block.Serial = Field(body, "Serial Number");
                block.Firmware = Field(body, "Firmware Version and Revision");
                block.Health = Field(body, "Health Status");
                block.SizeBytes = ParseSize(Field(body, "Disk Size"));
                var attrMatches = Regex.Matches(body, @"(?m)^\s*(?<id>\d+)\s*,\s*(?<name>[^,\r\n]+?)\s*,\s*(?<value>[^,\r\n]+?)\s*,\s*(?<threshold>[^\r\n]+?)\s*$");
                foreach (Match attr in attrMatches)
                {
                    int id; long value; long threshold = 0;
                    if (!int.TryParse(attr.Groups["id"].Value.Trim(), out id)) continue;
                    if (!TryNumber(attr.Groups["value"].Value.Trim(), out value)) continue;
                    TryNumber(attr.Groups["threshold"].Value.Trim(), out threshold);
                    block.Attributes.Add(new SmartAttributeRecord { Id = id, Name = attr.Groups["name"].Value.Trim(), NormalizedValue = value, RawValue = value, Threshold = threshold, RawString = attr.Groups["value"].Value.Trim(), Source = "CrystalDiskInfo", SemanticsValidated = false });
                }
                result.Add(block);
            }
            return result;
        }

        private static Block MatchDrive(IEnumerable<Block> blocks, DriveInfoRecord drive)
        {
            var list = blocks.ToList();
            if (!string.IsNullOrWhiteSpace(drive.Serial))
            {
                var serialMatches = list.Where(x => string.Equals(x.Serial?.Trim(), drive.Serial.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (serialMatches.Count == 1) return serialMatches[0];
                if (serialMatches.Count > 1) return null;
            }
            var modelMatches = list.Where(x => string.Equals(Normalize(x.Model), Normalize(drive.Model), StringComparison.OrdinalIgnoreCase)).ToList();
            if (modelMatches.Count == 1) return modelMatches[0];
            if (modelMatches.Count == 0 || drive.SizeBytes <= 0) return null;
            var sizeMatches = modelMatches.Where(x => x.SizeBytes.HasValue && Math.Abs(x.SizeBytes.Value - drive.SizeBytes) <= Math.Max(1024L * 1024L, drive.SizeBytes / 100)).ToList();
            return sizeMatches.Count == 1 ? sizeMatches[0] : null;
        }

        private static void Apply(DriveInfoRecord drive, Block block)
        {
            drive.SmartDataSource = "CrystalDiskInfo /CopyExit";
            drive.SmartDeviceType = "CrystalDiskInfo provider";
            drive.Transport = "CrystalDiskInfo";
            drive.StorageEvidence.Source = "CrystalDiskInfo /CopyExit";
            drive.StorageEvidence.Availability = "Measured";
            drive.StorageEvidence.Quality = string.IsNullOrWhiteSpace(block.Serial) ? "Matched by model/capacity" : "Serial/model matched";
            drive.StorageEvidence.DeviceType = "CrystalDiskInfo provider";
            drive.StorageEvidence.Transport = "CrystalDiskInfo";
            drive.StorageEvidence.SerialValidated = !string.IsNullOrWhiteSpace(block.Serial) && string.Equals(block.Serial, drive.Serial, StringComparison.OrdinalIgnoreCase);
            drive.StorageEvidence.HasAtaSmart = block.Attributes.Count > 0;
            if (!string.IsNullOrWhiteSpace(block.Firmware)) drive.Firmware = block.Firmware;
            if (!string.IsNullOrWhiteSpace(block.Health)) drive.SmartStatus = "CrystalDiskInfo: " + block.Health;
            foreach (var attr in block.Attributes)
            {
                var existing = drive.SmartAttributes.FirstOrDefault(x => x.Id == attr.Id && string.Equals(x.Name, attr.Name, StringComparison.OrdinalIgnoreCase));
                if (existing == null) drive.SmartAttributes.Add(attr);
                else { existing.RawValue = attr.RawValue; existing.NormalizedValue = attr.NormalizedValue; existing.Threshold = attr.Threshold; existing.RawString = attr.RawString; existing.Source = "CrystalDiskInfo"; }
                if (attr.Id.HasValue && attr.RawValue.HasValue) drive.Attributes["CDI_" + attr.Id.Value] = attr.RawValue.Value;
                var name = attr.Name ?? string.Empty;
                if ((name.IndexOf("SSD Life Left", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Lifetime Remaining", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Life Left", StringComparison.OrdinalIgnoreCase) >= 0) && attr.RawValue.HasValue)
                {
                    var life = attr.RawValue.Value;
                    if (life >= 0 && life <= 100)
                    {
                        drive.RemainingLifePercent = life;
                        drive.LifeMeaning = "CrystalDiskInfo-reported device endurance/life remaining; not a failure probability.";
                        drive.EnduranceUsedPercent = 100 - life;
                        drive.EnduranceMeaning = "Derived as 100 minus the explicitly reported life-remaining value; not overall health.";
                        drive.StorageEvidence.HasEndurance = true;
                    }
                }
                if (name.IndexOf("Temperature", StringComparison.OrdinalIgnoreCase) >= 0 && attr.RawValue.HasValue && attr.RawValue.Value >= -40 && attr.RawValue.Value <= 150)
                {
                    drive.TemperatureC = attr.RawValue.Value;
                    drive.StorageEvidence.HasTemperature = true;
                }
            }
            drive.StorageEvidence.UnavailableFields.Remove("SMART health data");
            if (block.Attributes.Count == 0) drive.StorageEvidence.UnavailableFields.Add("SMART attributes");
        }

        private static string Field(string body, string name)
        {
            var match = Regex.Match(body ?? string.Empty, @"(?m)^\s*" + Regex.Escape(name) + @"\s*:\s*(?<value>[^\r\n]*)");
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static long? ParseSize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var match = Regex.Match(text, @"(?<value>[0-9.]+)\s*(?<unit>TB|GB|MB|KB|B)?", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            double value;
            if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
            var unit = match.Groups["unit"].Value.ToUpperInvariant();
            var multiplier = unit == "TB" ? Math.Pow(1024, 4) : unit == "GB" ? Math.Pow(1024, 3) : unit == "MB" ? Math.Pow(1024, 2) : unit == "KB" ? 1024 : 1;
            return (long)(value * multiplier);
        }

        private static bool TryNumber(string text, out long value)
        {
            value = 0; double d;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d > long.MaxValue || d < long.MinValue) return false;
            value = (long)d; return true;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().Replace("  ", " ");

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
