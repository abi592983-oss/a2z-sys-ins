using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace A2ZSysIns
{
    internal static class WindowsIntegrityService
    {
        public static void Collect(InspectionReport report)
        {
            if (report == null) return;
            report.Measurements.RemoveAll(x => x != null && (x.Target ?? "").StartsWith("Windows integrity", StringComparison.OrdinalIgnoreCase));
            RunCheck(report, "System files", "sfc.exe", "/verifyonly", 180000, ClassifySfc);
            RunCheck(report, "Component store", "DISM.exe", "/Online /Cleanup-Image /CheckHealth", 90000, ClassifyDism);
            var system = Environment.GetEnvironmentVariable("SystemDrive");
            if (!string.IsNullOrWhiteSpace(system)) RunCheck(report, "File system " + system, "chkdsk.exe", system + " /scan", 120000, ClassifyChkdsk);
            else EvidenceEngine.Record(report, "Windows integrity / File system", "CHKDSK", "Unavailable", "System drive could not be identified.");
        }

        private static void RunCheck(InspectionReport report, string target, string exe, string args, int timeoutMs, Func<string, Tuple<string, string>> classifier)
        {
            try
            {
                var result = Run(report, exe, args, timeoutMs);
                var c = classifier(result.Item1 + "\n" + result.Item2);
                EvidenceEngine.Record(report, "Windows integrity / " + target, exe, c.Item1, c.Item2, Trim(result.Item1 + "\n" + result.Item2));
            }
            catch (Exception ex)
            {
                EvidenceEngine.Record(report, "Windows integrity / " + target, exe, "Unavailable", ex.GetBaseException().Message);
            }
        }

        private static Tuple<string, string> ClassifySfc(string text)
        {
            if (Contains(text, "did not find any integrity violations")) return Tuple.Create("Observed", "Windows system-file verification reported no integrity violations.");
            if (Contains(text, "found corrupt files") && Contains(text, "successfully repaired")) return Tuple.Create("Attention", "Windows reported corrupted protected system files and reported that some or all were repaired. The raw verification output is retained.");
            if (Contains(text, "found corrupt files") || Contains(text, "could not fix some")) return Tuple.Create("Critical", "Windows reported protected system-file corruption that was not fully repaired by verification.");
            if (Contains(text, "Windows Resource Protection could not start")) return Tuple.Create("Unavailable", "Windows Resource Protection could not start; system-file integrity could not be verified.");
            return Tuple.Create("Unavailable", "SFC completed without a recognized definitive integrity result; the raw output is retained.");
        }

        private static Tuple<string, string> ClassifyDism(string text)
        {
            if (Contains(text, "No component store corruption detected")) return Tuple.Create("Observed", "Windows reported no component-store corruption.");
            if (Contains(text, "repairable")) return Tuple.Create("Attention", "Windows reported that component-store corruption is repairable.");
            if (Contains(text, "corruption")) return Tuple.Create("Critical", "Windows reported component-store corruption.");
            return Tuple.Create("Unavailable", "DISM did not return a recognized definitive component-store health result.");
        }

        private static Tuple<string, string> ClassifyChkdsk(string text)
        {
            if (Contains(text, "found no problems") || Contains(text, "Windows has scanned the file system and found no problems")) return Tuple.Create("Observed", "Windows file-system scan reported no problems.");
            if (Contains(text, "bad sectors") && !Regex.IsMatch(text, @"\b0\s+KB\s+in bad sectors", RegexOptions.IgnoreCase)) return Tuple.Create("Critical", "Windows file-system scan reported bad sectors. The raw scan output is retained for technician review.");
            if (Contains(text, "corrupt") || Contains(text, "errors")) return Tuple.Create("Attention", "Windows file-system scan reported an error or corruption indicator.");
            return Tuple.Create("Unavailable", "CHKDSK completed without a recognized definitive result.");
        }

        private static Tuple<string, string> Run(InspectionReport report, string exe, string args, int timeoutMs)
        {
            EvidenceEngine.Log(report, "Windows integrity request", exe + " " + args);
            using (var p = new Process { StartInfo = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } })
            {
                p.Start();
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException(exe + " timed out after " + (timeoutMs / 1000) + " seconds.");
                }
                stdout.Wait(5000); stderr.Wait(5000);
                EvidenceEngine.Log(report, "Windows integrity response", exe + " exit=" + p.ExitCode);
                return Tuple.Create(stdout.Result ?? "", stderr.Result ?? "");
            }
        }

        private static bool Contains(string text, string value) => (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string Trim(string text) { var value = text ?? ""; return value.Length <= 12000 ? value : value.Substring(0, 12000) + "\n[output truncated]"; }
    }
}
