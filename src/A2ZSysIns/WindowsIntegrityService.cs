using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace A2ZSysIns
{
    internal static class WindowsIntegrityService
    {
        public static void Collect(InspectionReport report)
        {
            CollectAsync(report, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static async Task CollectAsync(InspectionReport report, CancellationToken cancellation)
        {
            if (report == null) return;
            report.Measurements.RemoveAll(x => x != null && (x.Target ?? "").StartsWith("Windows integrity", StringComparison.OrdinalIgnoreCase));
            await RunCheckAsync(report, "System files", "sfc.exe", "/verifyonly", 180000, ClassifySfc, cancellation).ConfigureAwait(false);
            await RunCheckAsync(report, "Component store", "DISM.exe", "/Online /Cleanup-Image /CheckHealth", 90000, ClassifyDism, cancellation).ConfigureAwait(false);
            var system = Environment.GetEnvironmentVariable("SystemDrive");
            if (!string.IsNullOrWhiteSpace(system)) await RunCheckAsync(report, "File system " + system, "chkdsk.exe", system + " /scan", 120000, ClassifyChkdsk, cancellation).ConfigureAwait(false);
            else EvidenceEngine.Record(report, "Windows integrity / File system", "CHKDSK", "Unavailable", "System drive could not be identified.");
        }

        private static async Task RunCheckAsync(InspectionReport report, string target, string exe, string args, int timeoutMs, Func<string, Tuple<string, string>> classifier, CancellationToken cancellation)
        {
            try
            {
                var result = await RunAsync(report, exe, args, timeoutMs, cancellation).ConfigureAwait(false);
                var c = classifier(result.StandardOutput + "\n" + result.StandardError);
                EvidenceEngine.Record(report, "Windows integrity / " + target, exe, c.Item1, c.Item2, Trim(result.StandardOutput + "\n" + result.StandardError));
            }
            catch (OperationCanceledException) { throw; }
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

        private static async Task<ProcessExecutionResult> RunAsync(InspectionReport report, string exe, string args, int timeoutMs, CancellationToken cancellation)
        {
            EvidenceEngine.Log(report, "Windows integrity request", exe + " " + args);
            var result = await ProcessExecutionService.RunAsync(new ProcessExecutionRequest { FileName = exe, Arguments = args, TimeoutMilliseconds = timeoutMs,
                Output = (stream, line) => EvidenceEngine.Log(report, "Windows integrity " + stream, line) }, cancellation).ConfigureAwait(false);
            EvidenceEngine.Log(report, "Windows integrity response", exe + " exit=" + result.ExitCode);
            return result;
        }

        private static bool Contains(string text, string value) => (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string Trim(string text) { var value = text ?? ""; return value.Length <= 12000 ? value : value.Substring(0, 12000) + "\n[output truncated]"; }
    }
}
